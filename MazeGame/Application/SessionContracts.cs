using MazeGame.Domain.Characters;

namespace MazeGame.Application;

public readonly record struct PlayerId(Guid Value)
{
    public static PlayerId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}

public enum GameSessionPhase
{
    Exploration,
    Battle,
    Inn,
    Paused,
    GameOver
}

public enum CharacterControllerKind
{
    HostPlayer,
    RemotePlayer,
    Npc
}

public enum PlayerConnectionState
{
    Connected,
    Reconnecting,
    Disconnected
}

public sealed record CharacterControlState(CharacterId CharacterId, CharacterControllerKind ControllerKind,
    PlayerId? AssignedPlayerId, PlayerConnectionState ConnectionState);

public abstract record GameCommand(PlayerId SenderId, long CommandId, CharacterId CharacterId);

public sealed record MoveCharacterCommand(PlayerId SenderId, long CommandId, CharacterId CharacterId,
    Direction Direction) : GameCommand(SenderId, CommandId, CharacterId);

public enum LeaderAction
{
    OpenDoor,
    CloseDoor,
    SearchOrLockDoor,
    ToggleRegrouping,
    ToggleHoldPosition,
    ScatterParty,
    Rest,
    ActivateExit
}

public sealed record LeaderActionCommand(PlayerId SenderId, long CommandId, CharacterId CharacterId,
    LeaderAction Action) : GameCommand(SenderId, CommandId, CharacterId);

public abstract record GameSessionEvent(long Sequence);

public sealed record SessionPhaseChangedEvent(long Sequence, GameSessionPhase PreviousPhase,
    GameSessionPhase CurrentPhase) : GameSessionEvent(Sequence);

public sealed record CharacterControlChangedEvent(long Sequence, CharacterControlState Control)
    : GameSessionEvent(Sequence);

public sealed record GameCommandRejectedEvent(long Sequence, PlayerId PlayerId, long CommandId,
    string Reason) : GameSessionEvent(Sequence);
