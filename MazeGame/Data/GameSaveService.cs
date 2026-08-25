using System.Text.Json;
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
        state.Version = 1;
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
        var state = JsonSerializer.Deserialize<GameSaveData>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidOperationException("A mentés üres vagy sérült.");
        if (state.Version != 1) throw new InvalidOperationException($"Nem támogatott mentésverzió: {state.Version}.");
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
                var state = JsonSerializer.Deserialize<GameSaveData>(File.ReadAllText(path), JsonOptions);
                if (state is not null) results.Add(new GameSaveInfo(path, state.MainCharacterName, state.MazeLevel, state.SavedAt));
            }
            catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException) { }
        }
        return results;
    }
}

public sealed record LoadedGameSave(string Path, CharacterRoster Roster, GameSaveData State);
public sealed record GameSaveInfo(string Path, string MainCharacterName, int MazeLevel, DateTimeOffset SavedAt);

public sealed class GameSaveData
{
    public int Version { get; set; } = 1;
    public DateTimeOffset SavedAt { get; set; }
    public string MainCharacterName { get; set; } = string.Empty;
    public string RosterJson { get; set; } = string.Empty;
    public int MazeLevel { get; set; } = 1;
    public List<string> CollectedBossKeyIds { get; set; } = [];
    public List<string> SeenBossIds { get; set; } = [];
    public Position PlayerPosition { get; set; }
    public Direction LeaderFacing { get; set; } = Direction.Right;
    public List<Position> LeaderTrail { get; set; } = [];
    public bool PartyHoldingPosition { get; set; }
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
    public List<GroundPileSaveData> GroundPiles { get; set; } = [];
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
    List<ActiveSpellEffect>? ActiveSpellEffects = null);
public sealed record CorpseSaveData(Position Position, string FormerName, int? PartyCharacterIndex,
    string? EnemyDefinitionId = null, bool IsSearched = false);
public sealed record PartyAvatarSaveData(Position Position, int CharacterIndex);
public sealed record GroundPileSaveData(Position Position, List<SavedItemReference> Items);
public sealed record SavedItemReference(string Category, string Id);
