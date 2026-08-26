using MazeGame.Combat;
using MazeGame.Domain.Characters;

namespace MazeGame.Application;

/// <summary>A hálózati szerződés jelenlegi verziója. Inkompatibilis DTO-változáskor növelendő.</summary>
public static class SessionProtocol
{
    public const int Version = 1;
}

/// <summary>A host doménállapotától leválasztott, JSON-nal továbbítható teljes session-kép.</summary>
public sealed record SessionSnapshot(int ProtocolVersion, long SnapshotSequence, long LastEventSequence,
    GameSessionPhase Phase, PlayerId HostPlayerId, CharacterId LeaderCharacterId, int MazeLevel,
    string LevelName, IReadOnlyList<SessionCharacterSnapshot> Party,
    IReadOnlyList<CharacterControlState> CharacterControls, BattleSnapshot? Battle, WorldSnapshot? World = null);

public sealed record SessionCharacterSnapshot(CharacterId CharacterId, string Name, string RaceId,
    string CharacterClassId, int Level, int CurrentVitality, int MaximumVitality, int CurrentMana,
    int MaximumMana, int FoodLevel, int WaterLevel, int Gold, bool IsAlive, Position? Position,
    IReadOnlyList<string> StatusIds);

public sealed record BattleSnapshot(BattleId BattleId, long TurnId, int Round, bool IsPlayerTurn,
    CharacterId ActingCharacterId, SessionEnemySnapshot Enemy,
    IReadOnlyList<BattleActionKind> AllowedActions);

public sealed record SessionEnemySnapshot(string DefinitionId, string Name, Position Position,
    int CurrentHitPoints, int MaximumHitPoints);

/// <summary>Host-oldali projekciós input; nem része a hálózaton fogadott parancsoknak.</summary>
public sealed record SessionSnapshotContext(int MazeLevel, string LevelName,
    IReadOnlyDictionary<CharacterId, Position> CharacterPositions, BattleSnapshot? Battle = null,
    WorldSnapshot? World = null);
