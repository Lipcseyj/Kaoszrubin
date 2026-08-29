using System.Text.Json;
using System.Text.Json.Serialization;
using MazeGame.Domain.Characters;
using MazeGame.Domain.Magic;

namespace MazeGame.Data;

/// <summary>A teljes labirintusfutam időbélyeges mentéseinek kezelése.</summary>
public sealed class GameSaveService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _saveDirectory;
    private readonly CharacterSaveService _characterSaveService;

    public GameSaveService(string saveDirectory, CharacterSaveService characterSaveService)
    {
        _saveDirectory = saveDirectory;
        _characterSaveService = characterSaveService;
    }

    public string Save(GameSaveData state, CharacterRoster roster)
    {
        Directory.CreateDirectory(_saveDirectory);
        state.Version = GameSaveFormat.CurrentVersion;
        state.SavedAt = DateTimeOffset.Now;
        state.RosterJson = _characterSaveService.Serialize(roster);
        var safeName = string.Concat(state.MainCharacterName.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "Nevtelen";
        var path = Path.Combine(_saveDirectory, $"{safeName}_{state.SavedAt:yyyyMMdd_HHmmss_fff}.save");
        File.WriteAllText(path, JsonSerializer.Serialize(state, JsonOptions));
        return path;
    }

    public LoadedGameSave Load(string path)
    {
        var state = DeserializeAndMigrate(File.ReadAllText(path));
        if (string.IsNullOrWhiteSpace(state.RosterJson)) throw new InvalidOperationException("A mentés nem tartalmaz karakteradatokat.");
        var roster = _characterSaveService.Deserialize(state.RosterJson);
        if (roster.SelectedCharacter is null) throw new InvalidOperationException("A mentés nem tartalmaz érvényes főkaraktert.");
        return new LoadedGameSave(path, roster, state);
    }

    public IReadOnlyList<GameSaveInfo> List()
    {
        if (!Directory.Exists(_saveDirectory)) return [];
        var results = new List<GameSaveInfo>();
        foreach (var path in Directory.EnumerateFiles(_saveDirectory, "*.save").OrderByDescending(File.GetLastWriteTimeUtc))
        {
            try
            {
                var state = DeserializeAndMigrate(File.ReadAllText(path));
                results.Add(new GameSaveInfo(path, state.MainCharacterName, state.MazeLevel,
                    state.SavedAt, state.IsCoopGame));
            }
            catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException or
                                                   InvalidOperationException) { }
        }
        return results;
    }

    public string SerializeCharacter(LiveCharacter character) =>
        _characterSaveService.SerializeCharacter(character);

    private static GameSaveData DeserializeAndMigrate(string json)
    {
        var state = JsonSerializer.Deserialize<GameSaveData>(json, JsonOptions)
            ?? throw new InvalidOperationException("A mentés üres vagy sérült.");
        return GameSaveFormat.MigrateToCurrent(state);
    }
}

/// <summary>A teljes játékmentés formátumának verziózása és soros migrációja.</summary>
public static class GameSaveFormat
{
    public const int OldestSupportedVersion = 1;
    public const int CurrentVersion = 6;

    public static GameSaveData MigrateToCurrent(GameSaveData state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Version < OldestSupportedVersion || state.Version > CurrentVersion)
            throw new InvalidOperationException(
                $"Nem támogatott mentésverzió: {state.Version}. Támogatott: {OldestSupportedVersion}–{CurrentVersion}.");

        while (state.Version < CurrentVersion)
        {
            state = state.Version switch
            {
                1 => MigrateVersion1To2(state),
                2 => MigrateVersion2To3(state),
                3 => MigrateVersion3To4(state),
                4 => MigrateVersion4To5(state),
                5 => MigrateVersion5To6(state),
                _ => throw new InvalidOperationException($"Hiányzó mentésmigráció a(z) {state.Version}. verzióhoz.")
            };
        }
        return state;
    }

    private static GameSaveData MigrateVersion1To2(GameSaveData state)
    {
        // A 2-es formátum coop- és bossadatokat adott hozzá. A DTO alapértékei pontosan
        // a régi egyjátékos mentések jelentését őrzik, ezért csak a verziót kell előreléptetni.
        state.Version = 2;
        return state;
    }

    private static GameSaveData MigrateVersion2To3(GameSaveData state)
    {
        // A 3-as verzió vezeti be a kötelező, soros migrációs rendszert. Adatséma nem változott.
        state.Version = 3;
        return state;
    }

    private static GameSaveData MigrateVersion3To4(GameSaveData state)
    {
        // A régi, már üldöző ellenfelek a betöltéskor az új alapértelmezett memóriát kapják.
        state.Version = 4;
        return state;
    }

    private static GameSaveData MigrateVersion4To5(GameSaveData state)
    {
        // Az 5-ös formátum a pályán várakozó NPC-ket vezeti be; régi mentésben a lista üres.
        state.Version = 5;
        return state;
    }

    private static GameSaveData MigrateVersion5To6(GameSaveData state)
    {
        // A 6-os formátum példányonkénti NPC-viszonyt, viselkedést és küldetéskapcsolatot ment.
        state.Version = 6;
        return state;
    }
}

public sealed record LoadedGameSave(string Path, CharacterRoster Roster, GameSaveData State);
public sealed record GameSaveInfo(string Path, string MainCharacterName, int MazeLevel, DateTimeOffset SavedAt,
    bool IsCoopGame);

public sealed class GameSaveData
{
    [JsonRequired]
    public int Version { get; set; } = GameSaveFormat.CurrentVersion;
    public DateTimeOffset SavedAt { get; set; }
    public string MainCharacterName { get; set; } = string.Empty;
    public string RosterJson { get; set; } = string.Empty;
    public int MazeLevel { get; set; } = 1;
    public List<string> CollectedBossKeyIds { get; set; } = [];
    public List<string> SeenBossIds { get; set; } = [];
    public bool IsCoopGame { get; set; }
    public List<Guid> RemoteCharacterIds { get; set; } = [];
    public Position PlayerPosition { get; set; }
    public Direction LeaderFacing { get; set; } = Direction.Right;
    public List<Position> LeaderTrail { get; set; } = [];
    public bool PartyHoldingPosition { get; set; }
    public bool PartyRegrouping { get; set; }
    public bool PartyAttackMode { get; set; }
    public bool HasRestedThisLevel { get; set; }
    public int ScatterRemainingMilliseconds { get; set; }
    public int NeedsDrainRemainingMilliseconds { get; set; }
    public int EnemyMoveRemainingMilliseconds { get; set; }
    public MazeSaveData Maze { get; set; } = new();
    public FogSaveData Fog { get; set; } = new();
}

public sealed class MazeSaveData
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int WallCodePoint { get; set; }
    public ConsoleColor WallColor { get; set; } = ConsoleColor.DarkGray;
    public string LevelName { get; set; } = "Labirintus";
    public List<int> TileCodePoints { get; set; } = [];
    public Position Exit { get; set; }
    public Room? StartingRoom { get; set; }
    public List<Room> Rooms { get; set; } = [];
    public List<DoorSaveData> Doors { get; set; } = [];
    public List<ChestSaveData> Chests { get; set; } = [];
    public List<EnemySaveData> Enemies { get; set; } = [];
    public List<CorpseSaveData> Corpses { get; set; } = [];
    public List<PartyAvatarSaveData> PartyAvatars { get; set; } = [];
    public List<WorldNpcSaveData> Npcs { get; set; } = [];
    public List<GroundPileSaveData> GroundPiles { get; set; } = [];
    public List<TrapSaveData> Traps { get; set; } = [];
}

public sealed class FogSaveData
{
    public List<Position> RevealedPositions { get; set; } = [];
    public bool DeveloperRevealActive { get; set; }
}

public sealed record DoorSaveData(Position Position, DoorState State);
public sealed record ChestSaveData(Position Position, int GoldAmount);
public sealed record EnemySaveData(Position Position, string DefinitionId, int CurrentHitPoints,
    EnemyMovementProfile MovementProfile = EnemyMovementProfile.Wander,
    Direction PatrolDirection = Direction.Right,
    EnemyPursuitState PursuitState = EnemyPursuitState.Undecided,
    int NextMoveRemainingMilliseconds = -1,
    string? GroupId = null,
    EnemyGroupRole GroupRole = EnemyGroupRole.Member,
    List<ActiveSpellEffect>? ActiveSpellEffects = null,
    CharacterId? PursuitTargetCharacterId = null,
    int PursuitMemoryRemainingMoves = -1);
public sealed record CorpseSaveData(Position Position, string FormerName, int? PartyCharacterIndex,
    string? EnemyDefinitionId = null, bool IsSearched = false);
public sealed record PartyAvatarSaveData(Position Position, int CharacterIndex);
public sealed record WorldNpcSaveData(Position Position, string DefinitionId, int CharacterIndex,
    NpcDisposition Disposition, bool Recruitable, bool IsQuestNpc, string Dialogue, WorldNpcState State,
    int Friendliness = 5, Domain.NpcWorldBehavior Behavior = Domain.NpcWorldBehavior.Guarded,
    List<string>? QuestIds = null);
public sealed record GroundPileSaveData(Position Position, List<SavedItemReference> Items);
public sealed record TrapSaveData(Position Position, string DefinitionId, TrapState State,
    bool DetectionAttempted, int FailedDisarmAttempts);
public sealed record SavedItemReference(string Category, string Id, int Charges = 0);
