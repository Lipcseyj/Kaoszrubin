using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MazeGame.Domain.Characters;

namespace MazeGame.Application;

public sealed record ClientHello(int ProtocolVersion, string ApplicationVersion, string CatalogHash,
    string DisplayName, string? ReconnectToken = null);

public sealed record ServerHello(bool Accepted, string? RejectionReason, int ProtocolVersion,
    string ApplicationVersion, string CatalogHash, PlayerId? PlayerId = null, PlayerId? HostPlayerId = null,
    string? ReconnectToken = null, IReadOnlyList<CharacterId>? AvailableCharacterIds = null,
    IReadOnlyList<CoopCharacterOption>? AvailableCharacters = null);

public sealed record CoopCharacterOption(CharacterId CharacterId, string Name, string CharacterClassName, int Level);

public sealed record SnapshotAck(PlayerId PlayerId, long SnapshotSequence);
public sealed record SnapshotResyncRequest(PlayerId PlayerId);
public sealed record CharacterControlRequest(PlayerId PlayerId, CharacterId CharacterId);
public sealed record JoinCharacterRequest(PlayerId PlayerId, string CharacterData);
public sealed record CharacterControlResult(PlayerId PlayerId, CharacterId CharacterId, bool Accepted,
    string? RejectionReason = null);
public sealed record CoopProtocolError(string Code, string Message);

public static class CatalogFingerprint
{
    public static string ComputeFile(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("A katalógusfájl nem található.", path);
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    public static string Compute(ReadOnlySpan<byte> content) => Convert.ToHexString(SHA256.HashData(content));
}

public sealed class SessionHandshakeService
{
    private readonly object _gate = new();
    private readonly GameSession _session;
    private readonly string _applicationVersion;
    private readonly string _catalogHash;
    private readonly Dictionary<string, PlayerId> _reconnectTokens = new(StringComparer.Ordinal);

    public SessionHandshakeService(GameSession session, string applicationVersion, string catalogHash)
    {
        _session = session;
        _applicationVersion = string.IsNullOrWhiteSpace(applicationVersion)
            ? throw new ArgumentException("Az alkalmazásverzió nem lehet üres.", nameof(applicationVersion))
            : applicationVersion;
        _catalogHash = NormalizeHash(catalogHash);
    }

    public ServerHello Handle(ClientHello hello)
    {
        lock (_gate)
        {
            ArgumentNullException.ThrowIfNull(hello);
            if (hello.ProtocolVersion != SessionProtocol.Version)
                return Reject($"Nem támogatott protokollverzió: {hello.ProtocolVersion}.");
            if (!HashesEqual(_catalogHash, hello.CatalogHash))
                return Reject("A host és a kliens játékkatalógusa eltér.");
            if (string.IsNullOrWhiteSpace(hello.DisplayName) || hello.DisplayName.Length > 32)
                return Reject("A játékosnév 1 és 32 karakter közötti lehet.");

            PlayerId playerId;
            string reconnectToken;
            if (!string.IsNullOrWhiteSpace(hello.ReconnectToken))
            {
                if (!_reconnectTokens.TryGetValue(hello.ReconnectToken, out playerId) ||
                    !_session.TryReconnectPlayer(playerId))
                    return Reject("A reconnect-token ismeretlen, lejárt vagy a játékos már csatlakoztatva van.");
                reconnectToken = hello.ReconnectToken;
            }
            else
            {
                playerId = _session.RegisterRemotePlayer();
                reconnectToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                _reconnectTokens[reconnectToken] = playerId;
            }

            var available = _session.CharacterControls
                .Where(control => control.ControllerKind == CharacterControllerKind.Npc && control.AssignedPlayerId is null)
                .Select(control => control.CharacterId).ToArray();
            var availableCharacters = _session.GetAvailableRemoteCharacters();
            return new ServerHello(true, null, SessionProtocol.Version, _applicationVersion, _catalogHash,
                playerId, _session.HostPlayerId, reconnectToken, available, availableCharacters);
        }
    }

    private ServerHello Reject(string reason) => new(false, reason, SessionProtocol.Version,
        _applicationVersion, _catalogHash);

    private static string NormalizeHash(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash)) throw new ArgumentException("A katalógushash nem lehet üres.", nameof(hash));
        return hash.Trim().ToUpperInvariant();
    }

    private static bool HashesEqual(string expected, string actual)
    {
        var normalized = string.IsNullOrWhiteSpace(actual) ? string.Empty : actual.Trim().ToUpperInvariant();
        var expectedBytes = Encoding.ASCII.GetBytes(expected);
        var actualBytes = Encoding.ASCII.GetBytes(normalized);
        return expectedBytes.Length == actualBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}

public sealed record ProtocolWireEnvelope(string Type, JsonElement Payload);

public static class CoopProtocolJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false,
        MaxDepth = 32
    };

    public static string Encode(object message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var (type, payloadType) = message switch
        {
            ClientHello => ("client.hello", typeof(ClientHello)),
            SnapshotAck => ("client.snapshot-ack", typeof(SnapshotAck)),
            SnapshotResyncRequest => ("client.snapshot-resync", typeof(SnapshotResyncRequest)),
            CharacterControlRequest => ("client.character-control", typeof(CharacterControlRequest)),
            JoinCharacterRequest => ("client.join-character", typeof(JoinCharacterRequest)),
            MoveCharacterCommand => ("command.move", typeof(MoveCharacterCommand)),
            CharacterActionCommand => ("command.character-action", typeof(CharacterActionCommand)),
            LeaderActionCommand => ("command.leader-action", typeof(LeaderActionCommand)),
            InventoryTransferCommand => ("command.inventory-transfer", typeof(InventoryTransferCommand)),
            UseInventoryItemCommand => ("command.inventory-use", typeof(UseInventoryItemCommand)),
            DropInventoryItemCommand => ("command.inventory-drop", typeof(DropInventoryItemCommand)),
            PickUpGroundItemCommand => ("command.inventory-pickup", typeof(PickUpGroundItemCommand)),
            BattleActionCommand => ("command.battle-action", typeof(BattleActionCommand)),
            CastExplorationSpellCommand => ("command.exploration-spell", typeof(CastExplorationSpellCommand)),
            InnPurchaseCommand => ("command.inn-purchase", typeof(InnPurchaseCommand)),
            ServerHello => ("server.hello", typeof(ServerHello)),
            CharacterControlResult => ("server.character-control", typeof(CharacterControlResult)),
            CoopProtocolError => ("server.protocol-error", typeof(CoopProtocolError)),
            SessionReplicationFrame => ("server.replication", typeof(SessionReplicationFrame)),
            GameCommandRejectedEvent => ("server.command-rejected", typeof(GameCommandRejectedEvent)),
            _ => throw new NotSupportedException($"Nem támogatott protokollüzenet: {message.GetType().Name}.")
        };
        var payload = JsonSerializer.SerializeToElement(message, payloadType, Options);
        return JsonSerializer.Serialize(new ProtocolWireEnvelope(type, payload), Options);
    }

    public static object Decode(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new JsonException("Az üzenet üres.");
        var envelope = JsonSerializer.Deserialize<ProtocolWireEnvelope>(json, Options)
            ?? throw new JsonException("Az üzenet envelope-ja hiányzik.");
        return envelope.Type switch
        {
            "client.hello" => Deserialize<ClientHello>(envelope),
            "client.snapshot-ack" => Deserialize<SnapshotAck>(envelope),
            "client.snapshot-resync" => Deserialize<SnapshotResyncRequest>(envelope),
            "client.character-control" => Deserialize<CharacterControlRequest>(envelope),
            "client.join-character" => Deserialize<JoinCharacterRequest>(envelope),
            "command.move" => Deserialize<MoveCharacterCommand>(envelope),
            "command.character-action" => Deserialize<CharacterActionCommand>(envelope),
            "command.leader-action" => Deserialize<LeaderActionCommand>(envelope),
            "command.inventory-transfer" => Deserialize<InventoryTransferCommand>(envelope),
            "command.inventory-use" => Deserialize<UseInventoryItemCommand>(envelope),
            "command.inventory-drop" => Deserialize<DropInventoryItemCommand>(envelope),
            "command.inventory-pickup" => Deserialize<PickUpGroundItemCommand>(envelope),
            "command.battle-action" => Deserialize<BattleActionCommand>(envelope),
            "command.exploration-spell" => Deserialize<CastExplorationSpellCommand>(envelope),
            "command.inn-purchase" => Deserialize<InnPurchaseCommand>(envelope),
            "server.hello" => Deserialize<ServerHello>(envelope),
            "server.character-control" => Deserialize<CharacterControlResult>(envelope),
            "server.protocol-error" => Deserialize<CoopProtocolError>(envelope),
            "server.replication" => Deserialize<SessionReplicationFrame>(envelope),
            "server.command-rejected" => Deserialize<GameCommandRejectedEvent>(envelope),
            _ => throw new JsonException($"Ismeretlen protokollüzenet-típus: '{envelope.Type}'.")
        };
    }

    private static T Deserialize<T>(ProtocolWireEnvelope envelope) =>
        envelope.Payload.Deserialize<T>(Options) ?? throw new JsonException($"A(z) '{envelope.Type}' payloadja hiányzik.");
}
