using System.Text.Json;
using KaoszRubin.Domain.Characters;

namespace KaoszRubin.Application;

/// <summary>Egy konkrét transportkapcsolatnak címzett, már kódolt protokollüzenet.</summary>
public sealed record CoopOutgoingMessage(string ConnectionId, string WireMessage);

/// <summary>
/// A hálózati adapter és a host session közötti transportfüggetlen határ. A kapcsolatot a handshake-kel
/// hitelesített PlayerId-hoz köti, ezért a kliens nem hamisíthatja meg a commandok vagy ACK-ok küldőjét.
/// </summary>
public sealed class CoopHostGateway
{
    private const int MaximumPendingMessages = 256;
    private readonly object _gate = new();
    private readonly GameSession _session;
    private readonly SessionHandshakeService _handshake;
    private readonly SessionReplicationPublisher _publisher;
    private readonly Func<string, LiveCharacter>? _deserializeCharacter;
    private readonly Action<LiveCharacter>? _registerCharacter;
    private readonly CharacterId? _reservedRemoteCharacterId;
    private readonly Dictionary<string, PlayerId> _playersByConnection = new(StringComparer.Ordinal);
    private readonly Dictionary<PlayerId, string> _connectionsByPlayer = [];
    private readonly Queue<CoopOutgoingMessage> _pendingMessages = [];

    public CoopHostGateway(GameSession session, SessionHandshakeService handshake,
        SessionReplicationPublisher publisher, Func<string, LiveCharacter>? deserializeCharacter = null,
        Action<LiveCharacter>? registerCharacter = null, CharacterId? reservedRemoteCharacterId = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _handshake = handshake ?? throw new ArgumentNullException(nameof(handshake));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _deserializeCharacter = deserializeCharacter;
        _registerCharacter = registerCharacter;
        _reservedRemoteCharacterId = reservedRemoteCharacterId;
        _session.EventPublished += HandleSessionEvent;
    }

    public int ConnectedClientCount
    {
        get { lock (_gate) return _connectionsByPlayer.Count; }
    }

    public IReadOnlyList<CoopOutgoingMessage> HandleIncoming(string connectionId, string wireMessage)
    {
        ValidateConnectionId(connectionId);
        object message;
        try
        {
            message = CoopProtocolJson.Decode(wireMessage);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            return Single(connectionId, new CoopProtocolError("invalid-message", exception.Message));
        }

        if (message is ClientHello hello) return HandleHello(connectionId, hello);
        PlayerId authenticatedPlayerId;
        lock (_gate)
        {
            if (!_playersByConnection.TryGetValue(connectionId, out authenticatedPlayerId))
                return Single(connectionId, new CoopProtocolError("handshake-required",
                    "Gameplay üzenet csak sikeres handshake után küldhető."));
        }

        return message switch
        {
            JoinCharacterRequest request => HandleJoinCharacter(connectionId, authenticatedPlayerId, request),
            CharacterControlRequest request => HandleCharacterControl(connectionId, authenticatedPlayerId, request),
            SnapshotAck ack => HandleAck(connectionId, authenticatedPlayerId, ack),
            SnapshotResyncRequest request => HandleResync(connectionId, authenticatedPlayerId, request),
            GameCommand command => HandleCommand(connectionId, authenticatedPlayerId, command),
            _ => Single(connectionId, new CoopProtocolError("invalid-direction",
                "A kliens csak kliens → host protokollüzenetet küldhet."))
        };
    }

    public IReadOnlyList<CoopOutgoingMessage> CreateReplicationMessages(SessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_gate)
        {
            return _connectionsByPlayer.Select(pair => new CoopOutgoingMessage(pair.Value,
                CoopProtocolJson.Encode(_publisher.CreateFrame(pair.Key, snapshot)))).ToArray();
        }
    }

    public IReadOnlyList<CoopOutgoingMessage> DrainPendingMessages()
    {
        lock (_gate)
        {
            if (_pendingMessages.Count == 0) return [];
            var messages = _pendingMessages.Where(message =>
                _playersByConnection.ContainsKey(message.ConnectionId)).ToArray();
            _pendingMessages.Clear();
            return messages;
        }
    }

    public bool QueueCharacterState(CharacterId characterId, string characterData, CharacterSyncReason reason)
    {
        if (string.IsNullOrWhiteSpace(characterData)) return false;
        var control = _session.CharacterControls.FirstOrDefault(candidate =>
            candidate.CharacterId == characterId && candidate.AssignedPlayerId is not null);
        if (control?.AssignedPlayerId is not { } playerId) return false;
        lock (_gate)
        {
            if (!_connectionsByPlayer.TryGetValue(playerId, out var connectionId)) return false;
            while (_pendingMessages.Count >= MaximumPendingMessages) _pendingMessages.Dequeue();
            _pendingMessages.Enqueue(new CoopOutgoingMessage(connectionId,
                CoopProtocolJson.Encode(new CharacterStateSync(playerId, characterId, characterData, reason))));
            return true;
        }
    }

    public void Disconnect(string connectionId)
    {
        ValidateConnectionId(connectionId);
        PlayerId playerId;
        lock (_gate)
        {
            if (!_playersByConnection.Remove(connectionId, out playerId)) return;
            if (_connectionsByPlayer.GetValueOrDefault(playerId) == connectionId)
                _connectionsByPlayer.Remove(playerId);
        }
        _publisher.RemoveClient(playerId);
        _session.MarkPlayerDisconnected(playerId);
    }

    private IReadOnlyList<CoopOutgoingMessage> HandleHello(string connectionId, ClientHello hello)
    {
        lock (_gate)
        {
            if (_playersByConnection.ContainsKey(connectionId))
                return Single(connectionId, new CoopProtocolError("already-authenticated",
                    "Ehhez a kapcsolathoz már tartozik játékos."));
        }

        var response = _handshake.Handle(hello);
        if (response is { Accepted: true, PlayerId: { } playerId })
        {
            lock (_gate)
            {
                if (_connectionsByPlayer.TryGetValue(playerId, out var previousConnection))
                    _playersByConnection.Remove(previousConnection);
                _playersByConnection[connectionId] = playerId;
                _connectionsByPlayer[playerId] = connectionId;
            }
            _publisher.RequestFullSnapshot(playerId);
        }
        return Single(connectionId, response);
    }

    private IReadOnlyList<CoopOutgoingMessage> HandleCharacterControl(string connectionId,
        PlayerId authenticatedPlayerId, CharacterControlRequest request)
    {
        if (request.PlayerId != authenticatedPlayerId)
            return SenderMismatch(connectionId);
        var accepted = _session.TryAssignRemoteControl(authenticatedPlayerId, request.CharacterId, out var error);
        return Single(connectionId, new CharacterControlResult(authenticatedPlayerId, request.CharacterId,
            accepted, accepted ? null : error));
    }

    private IReadOnlyList<CoopOutgoingMessage> HandleJoinCharacter(string connectionId,
        PlayerId authenticatedPlayerId, JoinCharacterRequest request)
    {
        if (request.PlayerId != authenticatedPlayerId) return SenderMismatch(connectionId);
        if (_deserializeCharacter is null || _registerCharacter is null)
            return Single(connectionId, new CharacterControlResult(authenticatedPlayerId, default, false,
                "A host nem fogad kliensoldali karaktert."));
        try
        {
            var character = _deserializeCharacter(request.CharacterData);
            if (_reservedRemoteCharacterId is { } reserved)
            {
                if (character.Id != reserved)
                    return Single(connectionId, new CharacterControlResult(authenticatedPlayerId, reserved, false,
                        "Ez a coop mentés másik vendégkaraktert vár."));
                var assigned = _session.TryAssignRemoteControl(authenticatedPlayerId, reserved, out var assignmentError);
                return Single(connectionId, new CharacterControlResult(authenticatedPlayerId, reserved,
                    assigned, assigned ? null : assignmentError));
            }
            var accepted = _session.TryJoinRemoteCharacter(authenticatedPlayerId, character, out var error);
            if (accepted) _registerCharacter(character);
            return Single(connectionId, new CharacterControlResult(authenticatedPlayerId, character.Id,
                accepted, accepted ? null : error));
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
        {
            return Single(connectionId, new CharacterControlResult(authenticatedPlayerId, default, false,
                $"A karakteradat nem fogadható el: {exception.Message}"));
        }
    }

    private IReadOnlyList<CoopOutgoingMessage> HandleAck(string connectionId, PlayerId authenticatedPlayerId,
        SnapshotAck ack)
    {
        if (ack.PlayerId != authenticatedPlayerId) return SenderMismatch(connectionId);
        return _publisher.TryAcknowledge(authenticatedPlayerId, ack.SnapshotSequence, out var error)
            ? []
            : Single(connectionId, new CoopProtocolError("snapshot-ack-rejected", error));
    }

    private IReadOnlyList<CoopOutgoingMessage> HandleResync(string connectionId, PlayerId authenticatedPlayerId,
        SnapshotResyncRequest request)
    {
        if (request.PlayerId != authenticatedPlayerId) return SenderMismatch(connectionId);
        _publisher.RequestFullSnapshot(authenticatedPlayerId);
        return [];
    }

    private IReadOnlyList<CoopOutgoingMessage> HandleCommand(string connectionId,
        PlayerId authenticatedPlayerId, GameCommand command)
    {
        if (command.SenderId != authenticatedPlayerId) return SenderMismatch(connectionId);
        return _session.Submit(command)
            ? []
            : Single(connectionId, new CoopProtocolError("command-queue-closed",
                "A host már nem fogad gameplay parancsokat."));
    }

    private void HandleSessionEvent(GameSessionEvent sessionEvent)
    {
        if (sessionEvent is not GameCommandRejectedEvent rejected) return;
        lock (_gate)
        {
            if (_connectionsByPlayer.TryGetValue(rejected.PlayerId, out var connectionId))
            {
                while (_pendingMessages.Count >= MaximumPendingMessages) _pendingMessages.Dequeue();
                _pendingMessages.Enqueue(new CoopOutgoingMessage(connectionId,
                    CoopProtocolJson.Encode(rejected)));
            }
        }
    }

    private static IReadOnlyList<CoopOutgoingMessage> SenderMismatch(string connectionId) =>
        Single(connectionId, new CoopProtocolError("sender-mismatch",
            "Az üzenet PlayerId-ja nem egyezik a hitelesített kapcsolatéval."));

    private static IReadOnlyList<CoopOutgoingMessage> Single(string connectionId, object message) =>
        [new CoopOutgoingMessage(connectionId, CoopProtocolJson.Encode(message))];

    private static void ValidateConnectionId(string connectionId)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
            throw new ArgumentException("A transport connection ID nem lehet üres.", nameof(connectionId));
    }
}
