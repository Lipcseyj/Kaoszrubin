using System.Threading.Channels;
using MazeGame.Domain.Characters;

namespace MazeGame.Application;

/// <summary>
/// Egy futó játék vezérlési határa. Sorosítja és jogosultság szerint szűri az összes helyi vagy hálózati
/// parancsot; a doménállapotot továbbra is kizárólag a Game szimulációs szála módosítja.
/// </summary>
public sealed class GameSession
{
    private readonly Party _party;
    private readonly Channel<GameCommand> _commands = Channel.CreateUnbounded<GameCommand>(
        new UnboundedChannelOptions { SingleReader = true, AllowSynchronousContinuations = false });
    private readonly Dictionary<CharacterId, CharacterControlState> _controls = [];
    private readonly Dictionary<PlayerId, long> _lastCommandIds = [];
    private readonly HashSet<PlayerId> _players = [];
    private long _eventSequence;

    public GameSession(Party party, LiveCharacter leader, PlayerId? hostPlayerId = null)
    {
        _party = party;
        HostPlayerId = hostPlayerId ?? PlayerId.New();
        _players.Add(HostPlayerId);
        SynchronizeParty();
        _controls[leader.Id] = new CharacterControlState(leader.Id, CharacterControllerKind.HostPlayer,
            HostPlayerId, PlayerConnectionState.Connected);
    }

    public PlayerId HostPlayerId { get; }
    public GameSessionPhase Phase { get; private set; } = GameSessionPhase.Exploration;
    public IReadOnlyCollection<CharacterControlState> CharacterControls => _controls.Values;
    public event Action<GameSessionEvent>? EventPublished;

    public bool IsHumanControlled(CharacterId characterId) => _controls.TryGetValue(characterId, out var control) &&
        control.ControllerKind is CharacterControllerKind.HostPlayer or CharacterControllerKind.RemotePlayer &&
        control.ConnectionState == PlayerConnectionState.Connected;

    public PlayerId RegisterRemotePlayer()
    {
        var playerId = PlayerId.New();
        _players.Add(playerId);
        return playerId;
    }

    public bool TryAssignRemoteControl(PlayerId playerId, CharacterId characterId, out string error)
    {
        SynchronizeParty();
        if (!_players.Contains(playerId) || playerId == HostPlayerId)
            return Fail("Ismeretlen vagy nem távoli játékos.", out error);
        if (_party.Leader?.Id == characterId)
            return Fail("A party-leader irányítása nem adható át.", out error);
        if (!_controls.TryGetValue(characterId, out var control))
            return Fail("A karakter nem tagja a partinak.", out error);
        if (control.ControllerKind != CharacterControllerKind.Npc || control.AssignedPlayerId is not null)
            return Fail("A karaktert már emberi játékos irányítja vagy foglalja.", out error);

        SetControl(control with
        {
            ControllerKind = CharacterControllerKind.RemotePlayer,
            AssignedPlayerId = playerId,
            ConnectionState = PlayerConnectionState.Connected
        });
        error = string.Empty;
        return true;
    }

    public void MarkPlayerDisconnected(PlayerId playerId)
    {
        if (playerId == HostPlayerId) return;
        foreach (var control in _controls.Values.Where(control => control.AssignedPlayerId == playerId).ToList())
            SetControl(control with
            {
                ControllerKind = CharacterControllerKind.Npc,
                ConnectionState = PlayerConnectionState.Disconnected
            });
    }

    public bool TryReconnectPlayer(PlayerId playerId)
    {
        var reserved = _controls.Values.Where(control => control.AssignedPlayerId == playerId &&
            control.ConnectionState != PlayerConnectionState.Connected).ToList();
        if (reserved.Count == 0) return false;
        _players.Add(playerId);
        foreach (var control in reserved)
            SetControl(control with
            {
                ControllerKind = CharacterControllerKind.RemotePlayer,
                ConnectionState = PlayerConnectionState.Connected
            });
        return true;
    }

    public bool Submit(GameCommand command) => _commands.Writer.TryWrite(command);

    /// <summary>Az első érvényes parancsot adja vissza; a megelőző hibás parancsokról eseményt bocsát ki.</summary>
    public bool TryReadCommand(out GameCommand command)
    {
        SynchronizeParty();
        while (_commands.Reader.TryRead(out var candidate))
        {
            if (Validate(candidate, out var reason))
            {
                _lastCommandIds[candidate.SenderId] = candidate.CommandId;
                command = candidate;
                return true;
            }
            Publish(sequence => new GameCommandRejectedEvent(sequence, candidate.SenderId,
                candidate.CommandId, reason));
        }
        command = null!;
        return false;
    }

    public void SetPhase(GameSessionPhase phase)
    {
        if (Phase == phase) return;
        var previous = Phase;
        Phase = phase;
        Publish(sequence => new SessionPhaseChangedEvent(sequence, previous, phase));
    }

    public void SynchronizeParty()
    {
        var members = _party.Members.Select(character => character.Id).ToHashSet();
        foreach (var removed in _controls.Keys.Where(id => !members.Contains(id)).ToList()) _controls.Remove(removed);
        foreach (var character in _party.Members)
        {
            if (_controls.ContainsKey(character.Id)) continue;
            _controls[character.Id] = character == _party.Leader
                ? new CharacterControlState(character.Id, CharacterControllerKind.HostPlayer, HostPlayerId,
                    PlayerConnectionState.Connected)
                : new CharacterControlState(character.Id, CharacterControllerKind.Npc, null,
                    PlayerConnectionState.Connected);
        }
    }

    private bool Validate(GameCommand command, out string reason)
    {
        if (!_players.Contains(command.SenderId))
            return Fail("A parancs küldője nem tagja a sessionnek.", out reason);
        if (command.CommandId <= 0 || _lastCommandIds.GetValueOrDefault(command.SenderId) >= command.CommandId)
            return Fail("Ismételt vagy sorrenden kívüli parancs.", out reason);
        if (Phase != GameSessionPhase.Exploration)
            return Fail("Ez a parancs csak felfedezés közben hajtható végre.", out reason);
        if (!_controls.TryGetValue(command.CharacterId, out var control) ||
            control.AssignedPlayerId != command.SenderId || control.ConnectionState != PlayerConnectionState.Connected ||
            control.ControllerKind == CharacterControllerKind.Npc)
            return Fail("A játékos nem irányíthatja ezt a karaktert.", out reason);
        if (command is LeaderActionCommand && control.ControllerKind != CharacterControllerKind.HostPlayer)
            return Fail("Ez a party-szintű művelet csak a leader számára engedélyezett.", out reason);
        reason = string.Empty;
        return true;
    }

    private void SetControl(CharacterControlState control)
    {
        _controls[control.CharacterId] = control;
        Publish(sequence => new CharacterControlChangedEvent(sequence, control));
    }

    private void Publish(Func<long, GameSessionEvent> create)
    {
        var sessionEvent = create(++_eventSequence);
        EventPublished?.Invoke(sessionEvent);
    }

    private static bool Fail(string reason, out string error)
    {
        error = reason;
        return false;
    }
}
