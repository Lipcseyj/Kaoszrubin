using System.Text.Json;
using System.Text.Json.Serialization;
using KaoszRubin.Application;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Magic;

namespace KaoszRubin.Data;

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

    public string Overwrite(LoadedGameSave loaded)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        var path = Path.GetFullPath(loaded.Path);
        if (!File.Exists(path)) throw new FileNotFoundException("A szerkesztendő mentés nem található.", path);
        loaded.State.Version = GameSaveFormat.CurrentVersion;
        loaded.State.RosterJson = _characterSaveService.Serialize(loaded.Roster);
        var backupPath = path + $".pre-edit-{DateTime.Now:yyyyMMdd_HHmmss_fff}.bak";
        var temporaryPath = path + $".{Guid.NewGuid():N}.tmp";
        File.Copy(path, backupPath, overwrite: false);
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(loaded.State, JsonOptions));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
        return backupPath;
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
    public const int CurrentVersion = 17;

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
                6 => MigrateVersion6To7(state),
                7 => MigrateVersion7To8(state),
                8 => MigrateVersion8To9(state),
                9 => MigrateVersion9To10(state),
                10 => MigrateVersion10To11(state),
                11 => MigrateVersion11To12(state),
                12 => MigrateVersion12To13(state),
                13 => MigrateVersion13To14(state),
                14 => MigrateVersion14To15(state),
                15 => MigrateVersion15To16(state),
                16 => MigrateVersion16To17(state),
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

    private static GameSaveData MigrateVersion6To7(GameSaveData state)
    {
        // A 7-es formátum az NPC-küldetések állapotát és haladását őrzi.
        state.Version = 7;
        return state;
    }

    private static GameSaveData MigrateVersion7To8(GameSaveData state)
    {
        // A 8-as formátum az egyetlen ideiglenes követőt és az egyedi NPC beszélgetési állapotát menti.
        state.Version = 8;
        return state;
    }

    private static GameSaveData MigrateVersion8To9(GameSaveData state)
    {
        // A 9-es formátum a pályákon átívelő parti-küldetésnaplót őrzi.
        state.Version = 9;
        return state;
    }

    private static GameSaveData MigrateVersion9To10(GameSaveData state)
    {
        // A 10-es formátum menthető szobaszerepeket és tartalomazonosítókat vezet be.
        // A régi szobák alapértelmezetten normál szobák maradnak.
        state.Version = 10;
        return state;
    }

    private static GameSaveData MigrateVersion10To11(GameSaveData state)
    {
        // A 11-es formátum az ellenfélpéldányhoz kötött garantált questzsákmányt menti.
        state.Version = 11;
        return state;
    }

    private static GameSaveData MigrateVersion11To12(GameSaveData state)
    {
        // A 12-es formátum a kampánytól független küldetéshelyszínt és a
        // mögötte felfüggesztett kampánypályát vezeti be.
        state.Version = 12;
        return state;
    }

    private static GameSaveData MigrateVersion12To13(GameSaveData state)
    {
        // A 13-as formátum a már lejátszott követői ad-hoc beszélgetéseket és cooldownjukat őrzi.
        state.Version = 13;
        return state;
    }

    private static GameSaveData MigrateVersion13To14(GameSaveData state)
    {
        // A 14-es formatum a negyhelyes, iranyitott party-alakzatot orzi.
        state.Formation ??= PartyFormationRules.CreateDefault([], default);
        state.Version = 14;
        return state;
    }

    private static GameSaveData MigrateVersion14To15(GameSaveData state)
    {
        // A 15-os formátum az ellenfelek éberségét, keresését és hazatérési állapotát menti.
        state.Version = 15;
        return state;
    }

    private static GameSaveData MigrateVersion15To16(GameSaveData state)
    {
        // A 16-os formátum a szörnyképességek és különleges fegyverek lehűlését őrzi.
        state.Version = 16;
        return state;
    }

    private static GameSaveData MigrateVersion16To17(GameSaveData state)
    {
        // A 17-es formátum megőrzi az előkészített, következő körben elsülő szörnyfegyvert.
        state.Version = 17;
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
    public AdventureLocationKind LocationKind { get; set; } = AdventureLocationKind.Campaign;
    public string LocationId { get; set; } = string.Empty;
    public int DifficultyLevel { get; set; }
    public GameSaveData? SuspendedCampaign { get; set; }
    public List<string> CollectedBossKeyIds { get; set; } = [];
    public List<string> SeenBossIds { get; set; } = [];
    public bool IsCoopGame { get; set; }
    public List<Guid> RemoteCharacterIds { get; set; } = [];
    public Position PlayerPosition { get; set; }
    public Direction LeaderFacing { get; set; } = Direction.Right;
    public PartyFormationSnapshot? Formation { get; set; }
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
    public List<QuestJournalSaveData> QuestJournal { get; set; } = [];
    public List<string> UsedAdHocConversationIds { get; set; } = [];
    public DateTimeOffset? LastAdHocConversationUtc { get; set; }
    public int AdHocConversationMazeLevel { get; set; } = -1;
}

public enum AdventureLocationKind { Campaign, Quest }

public sealed record QuestJournalSaveData(string QuestId, QuestJournalStatus Status,
    int Progress, int ExperienceReward);

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
    int PursuitMemoryRemainingMoves = -1,
    List<string>? GuaranteedLootIds = null,
    EnemyAlertness Alertness = EnemyAlertness.Alert,
    EnemySearchRole SearchRole = EnemySearchRole.None,
    Position? HomePosition = null,
    Position? LastKnownTargetPosition = null,
    int ReactionDelayMovesRemaining = 0,
    int SearchMovesRemaining = 0,
    int ReturnDelayMovesRemaining = 0,
    string? SelectedWeaponId = null,
    Dictionary<string, int>? AbilityCooldowns = null,
    Dictionary<string, int>? WeaponCooldowns = null,
    string? PreparedWeaponId = null);
public sealed record CorpseSaveData(Position Position, string FormerName, int? PartyCharacterIndex,
    string? EnemyDefinitionId = null, bool IsSearched = false, List<string>? GuaranteedLootIds = null,
    List<string>? CarriedWeaponIds = null);
public sealed record PartyAvatarSaveData(Position Position, int CharacterIndex,
    WorldNpcSaveData? TemporaryFollower = null);
public sealed record WorldNpcSaveData(Position Position, string DefinitionId, int CharacterIndex,
    NpcDisposition Disposition, bool Recruitable, bool IsQuestNpc, string Dialogue, WorldNpcState State,
    int Friendliness = 5, Domain.NpcWorldBehavior Behavior = Domain.NpcWorldBehavior.Guarded,
    List<string>? QuestIds = null, List<NpcQuestProgress>? Quests = null, int ConversationStage = 0,
    string? StoryId = null, string StoryStateId = "INITIAL");
public sealed record GroundPileSaveData(Position Position, List<SavedItemReference> Items);
public sealed record TrapSaveData(Position Position, string DefinitionId, TrapState State,
    bool DetectionAttempted, int FailedDisarmAttempts);
public sealed record SavedItemReference(string Category, string Id, int Charges = 0);
