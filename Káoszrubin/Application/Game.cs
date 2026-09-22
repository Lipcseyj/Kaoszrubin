using KaoszRubin.Application;
using KaoszRubin.Application.Quests;
using KaoszRubin.Combat;
using KaoszRubin.Data;
using KaoszRubin.Domain;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Magic;
using KaoszRubin.Domain.Quests;
using KaoszRubin.Infrastructure;
using KaoszRubin.Infrastructure.Quests;
using KaoszRubin.UI;
using System.Runtime;
using System.Security.Cryptography.Xml;
using static KaoszRubin.UI.GameInput;
using MainMenu = KaoszRubin.UI.MainMenu;

namespace KaoszRubin.Application;

public sealed record NpcQuestUiEntry(string Title, QuestState State, int Progress, int RequiredCount);

/// <summary>A játék futását és felhasználói bemenetét koordinálja.</summary>
public sealed partial class Game : ISessionCommandHandler
{
    private const string EliraStoryId = "ELIRA_RESCUE";
    private const string RodericStoryId = "RODERIC_OATH";
    private const string RodericMalrecQuestId = "NPCQ039";
    private const string DeveloperBattleTestLocationId = "DEVELOPER_COMBAT_TEST";
    private const int RodericPermanentJoinFriendliness = 8;
    private const int ZombieSpeed = 2;
    private const int ZombieMoveIntervalMilliseconds = 700;
    private const int MinimumPartyMoveDelayMilliseconds = 250;
    private const int MaximumPartyMoveDelayMilliseconds = 300;
    private const int CatchUpMoveDelayMilliseconds = 90;
    private const int ControlledMoveDelayMilliseconds = 85;
    private const int FieldRepairAmount = 50;
    private const int FieldRepairMaximumPercent = 75;
    private static readonly TimeSpan StalemateRestartDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan CoopSnapshotHeartbeatInterval = TimeSpan.FromSeconds(2);
    private static readonly Direction[] Directions = Enum.GetValues<Direction>();
    public static GameSettingsService? StaticGameSettings;
    private const int MazeWidth = ConsoleRenderer.PlayfieldWidth;
    private const int MazeHeight = ConsoleRenderer.PlayfieldHeight;
    private readonly GameDataCatalog _gameData;
    private MazeGenerator _generator = null!;
    private DungeonLevel _dungeonLevel = null!;
    private readonly ConsoleRenderer _renderer;
    private Maze _maze = null!;
    private Player _player = null!;
    private FogOfWar _fogOfWar = null!;
    private readonly Random _random = new();
    private readonly BattleSystem _battleSystem;
    private BattleActionDetails? _lastBattleActionDetails;
    private readonly GameSaveService _gameSaveService;
    private readonly GameStateMapper _gameStateMapper;
    private readonly DoorInteractionController _doorInteractions;
    private readonly InnController _innController;
    private ICoopHostLoop? _activeCoopHost;
    private bool _coopSnapshotDirty = true;
    private bool _processingSessionCommands;
    private bool _synchronizingQuestInventory;
    private readonly QuestInventorySynchronizer _questInventorySynchronizer;
    private DateTime _nextCoopSnapshotHeartbeatUtc = DateTime.MinValue;
    private NarrativeSnapshot? _activeNarrative;
    private AdHocConversationSnapshot? _activeAdHocConversation;
    private readonly HashSet<string> _usedAdHocConversationIds = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _lastAdHocConversationUtc = DateTime.MinValue;
    private DateTime _nextAdHocConversationCheckUtc = DateTime.MinValue;
    private int _adHocConversationMazeLevel = -1;
    private readonly HashSet<PlayerId> _narrativeAcknowledgements = [];
    private LevelImageSnapshot? _activeLevelImage;
    private readonly HashSet<PlayerId> _levelImageAcknowledgements = [];
    private InnDepartureSnapshot? _activeInnDeparture;
    private SpellPreparationSnapshot? _activeSpellPreparation;
    private bool _spellPreparationCompleted;
    private PartyRestSnapshot? _latestRestNotice;
    private readonly HashSet<PlayerId> _restAcknowledgements = [];
    private readonly List<string> _hostRestAcknowledgementMessages = [];
    private LevelUpPromptSnapshot? _activeLevelUpPrompt;
    private string? _levelUpResponse;
    private bool _levelUpPromptCompleted;
    private readonly GameSaveData? _loadedState;
    private readonly SoundEffects _soundEffects;
    private readonly BackgroundMusicPlayer _backgroundMusic;
    private readonly GameSettingsService _gameSettings;
    private readonly GameSession _session;
    private readonly SpellExecutionService _spellExecutionService;
    private readonly EnemySpellcastingService _enemySpellcastingService;
    private readonly BattleActionCoordinator _battleActionCoordinator;
    private readonly TacticalBattleCoordinator _battleCoordinator;
    private readonly NpcQuestCoordinator _npcQuestCoordinator;
    private readonly StoryConversationCoordinator _storyConversationCoordinator;
    private readonly CharacterProgressionService _progressionService;
    private readonly PartySustenanceService _sustenanceService;
    private readonly DungeonTrapService _dungeonTrapService;
    private readonly LootAndInventoryService _lootService;
    private readonly DungeonExpeditionCoordinator _expeditionCoordinator;
    private readonly SessionEventService _sessionEventService;
    private readonly DeveloperBattleLog _developerBattleLog = new();
    private readonly PartyCommandController _partyCommandController;
    private readonly PartyAiController _partyAiController;
    private readonly SessionCommandDispatcher _commandDispatcher;
    private long _localCommandId;
    private readonly BattleCommandGate _localBattleCommandGate = new();
    private BattleEncounter? _activeBattle;
    private bool _isQuickBattle;
    private int _quickBattleSuppressedEntryCount;
    private long _preparedBattleTurnId;
    private long _battleMovementTurnId = -1;
    private int _battleMovementRemaining;
    private int _battleMovementSteps;
    private bool _battleStarted;
    private bool _gameOver;
    private bool _characterSheetFocused;
    private Guid? _hostSpellInfoWindowId;
    private HeldInventoryItem? _heldInventoryItem;
    private DateTime _nextNeedsDrain;
    private DateTime _nextNpcSelfCareCheck;
    private DateTime _nextTrapMessageUtc;
    private readonly Dictionary<Enemy, DateTime> _nextEnemyMoves = [];
    private DateTime _nextEnemyActionUtc = DateTime.MaxValue;
    private readonly Dictionary<Position, IReadOnlyDictionary<Position, int>> _enemyDistanceMaps = [];
    private WorldId _enemyDistanceMapMazeId;
    private long _enemyDistanceMapNavigationRevision = -1;
    private readonly Dictionary<PartyMemberAvatar, DateTime> _nextPartyMoves = [];
    private readonly Dictionary<CharacterId, DateTime> _nextControlledMoves = [];
    private readonly List<Position> _leaderTrail = [];
    private bool _partyHoldingPosition;
    private bool _partyRegrouping;
    private bool _partyAttackMode;
    private PartyCommandState _partyCommandState;
    private bool _saveAfterBattle;
    private bool _timeStopUsedThisBattle;
    private readonly Dictionary<LiveCharacter, int> _turnUndeadNextAvailableRounds = [];
    private readonly HashSet<CharacterId> _battleNoPathReported = [];
    private int _battleLogCycle = -1;
    private readonly Dictionary<(CharacterId CharacterId, NpcComplaintKind Kind), DateTime> _nextNpcComplaints = [];
    private readonly HashSet<(CharacterId CharacterId, NpcComplaintKind Kind)> _reportedNpcShortages = [];
    private readonly List<(LiveCharacter Character, LevelUpResult Result)> _pendingLevelUps = [];
    private readonly Dictionary<QuestKey, QuestJournalEntrySnapshot> _questJournal = [];
    private readonly Dictionary<PlayerId, PlayerWindowStateSnapshot> _openPlayerWindows = [];
    private DateTime? _playerWindowPauseStartedUtc;
    private DateTime? _partyScatterUntil;
    private Direction _leaderFacing = Direction.Right;
    private PartyFormationSnapshot _formation;
    private readonly Dictionary<CharacterId, NpcSpellcasterTactics> _npcSpellcasterTactics = [];
    private bool _formationObstacleReported;
    private string? _leaderDecisionMessage;
    private string? _leaderDecisionTitle;
    private ReplicatedWindowSnapshot? _activeSharedWindow;
    private Guid? _sharedWindowId;
    private long _sharedWindowRevision;
    private readonly HashSet<PlayerId> _sharedWindowAcknowledgements = [];
    private bool _captureSharedWindow;
    private int _mazeLevel = 1;
    private AdventureLocationKind _locationKind = AdventureLocationKind.Campaign;
    private string _locationId = string.Empty;
    private int _difficultyLevel = 1;
    private GameSaveData? _suspendedCampaignState;
    private bool _pendingRodericExpedition;
    private bool _pendingRodericReturn;
    private bool _hasRestedThisLevel;
    private bool _developerPhasing;
    private int _lastDeveloperUniqueNpcIndex = -1;
    private readonly HashSet<string> _collectedBossKeyIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _seenBossIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<WorldEntityId> _spottedEnemyIds = [];
    private readonly HashSet<WorldEntityId> _spottedChestIds = [];
    private readonly List<ExpeditionEnemyTemplate> _levelEnemyTemplates = [];
    private readonly List<WorldNpc> _temporaryFollowersEnteringNextMaze = [];
    private readonly List<LiveCharacter> _developerBattleTestCompanions = [];
    private LiveCharacter? _eliraWaitingAtInn;
    private int _eliraInnVisitsRemaining;
    private bool _isReturnExpedition;
    private MazeQuestWorldContext _questWorldContext;
    private readonly QuestManager _questManager;
    private readonly QuestNpcInstanceRegistry _questNpcInstanceRegistry;
    private readonly QuestSaveAdapter _questSaveAdapter;
    private DateTime? _automaticBattleResumeUtc;
    private (BattleId BattleId, int ActionNumber)? _lastDelayedAction;
    public List<CharacterId> _humanMemberIds = [];

    public CharacterRoster CharacterRoster { get; }
    public LiveCharacter PartyLeader { get; }
    public GameSession Session => _session;
    public BattleEncounter? ActiveBattle => _activeBattle;

    private sealed record BattlePromptState(
        BattleId BattleId,
        long TurnId,
        CharacterId ActingCharacterId,
        LiveCharacter? ActingCharacter,
        Enemy FocusEnemy,
        IReadOnlyList<BattleActionKind> AllowedActions,
        IReadOnlyList<BattleSpellOption>? SpellOptions,
        IReadOnlyList<BattleTacticOptionSnapshot>? TacticOptions,
        IReadOnlyList<BattleItemOptionSnapshot>? ItemOptions,
        IReadOnlyList<WorldEntityId>? ValidTargetEnemyIds,
        WorldEntityId? TurnUndeadTargetEnemyId,
        IReadOnlyList<BattleActionTargetsSnapshot>? ActionTargets,
        bool IsPaused,
        bool IsPlayerTurn);

    public SessionSnapshot CreateSessionSnapshot()
    {
        if (_maze is null || _player is null)
            throw new InvalidOperationException("Session snapshot csak inicializált játékból készíthető.");
        SynchronizeInventoryQuests();
        var positions = new Dictionary<CharacterId, Position>
        {
            [PartyLeader.Id] = _player.Position
        };
        foreach (var member in _maze.PartyMembers) positions[member.Character.Id] = member.Position;

        BattleSnapshot? battle = _activeBattle is { IsCompleted: false } activeBattle
            ? CreateBattleSnapshot(activeBattle)
            : null;
        var snapshot = _session.CreateSnapshot(new SessionSnapshotContext(_difficultyLevel, _maze.LevelName,
            positions, battle, WorldSnapshotProjector.Create(_maze, _fogOfWar,
                _activeBattle?.Enemies.Where(enemy => enemy.CurrentHitPoints > 0)
                    .Select(enemy => enemy.Id).ToHashSet(),
                new QuestWorldSnapshotProjector(_questManager, _questWorldContext).Create)));
        var followers = _maze.PartyMembers
            .Where(member => member.IsTemporaryFollower)
            .Select(member => member.Character)
            .Distinct()
            .ToArray();
        var characters = CharacterRoster.Party.Members.Concat(followers).ToDictionary(character => character.Id);
        var followerSnapshots = followers.Select(character => new SessionCharacterSnapshot(
            character.Id, character.Name, character.Race.Id, character.CharacterClass.Id, character.Level,
            character.CurrentVitality, character.MaximumVitality, character.CurrentMana, character.MaximumMana,
            character.FoodLevel, character.WaterLevel, PartyLeader.Gold, character.IsAlive,
            positions.GetValueOrDefault(character.Id), character.Statuses.Select(status => status.Id).ToArray(),
            Inventory: InventorySnapshotProjector.Create(character),
            CharacterSheet: CharacterSheetWithCombatConditions(character, battle), Color: character.Color,
            IsTemporaryFollower: true, History: CreateCharacterHistory(character))).ToArray();
        return snapshot with
        {
            GoldenKeyCount = _collectedBossKeyIds.Count,
            BossKeyCount = MonsterIds.Bosses.Count,
            Inn = _innController.CreateSnapshot(),
            InnDeparture = _activeInnDeparture,
            Narrative = _activeNarrative is null ? null : _activeNarrative with
            { AcknowledgedPlayerIds = _narrativeAcknowledgements.ToArray() },
            LevelImage = _activeLevelImage is null ? null : _activeLevelImage with
            { AcknowledgedPlayerIds = _levelImageAcknowledgements.ToArray() },
            SpellPreparation = _activeSpellPreparation,
            RestNotice = _latestRestNotice is null ? null : _latestRestNotice with
            { AcknowledgedPlayerIds = _restAcknowledgements.ToArray() },
            LevelUpPrompt = _activeLevelUpPrompt,
            Activities = _sessionEventService.Activities,
            Sounds = _sessionEventService.Sounds,
            PartyGold = PartyLeader.Gold,
            QuestJournal = OrderedQuestJournal(),
            AdHocConversation = _activeAdHocConversation,
            Formation = _formation,
            LeaderDecisionMessage = _leaderDecisionMessage,
            LeaderDecisionTitle = _leaderDecisionTitle,
            OpenPlayerWindows = _openPlayerWindows.Values.ToArray(),
            SharedWindow = _activeSharedWindow is null ? null : _activeSharedWindow with
            { AcknowledgedPlayerIds = _sharedWindowAcknowledgements.ToArray() },
            MusicContext = _backgroundMusic.Context,
            Party = snapshot.Party.Select(character => character with
            {
                Gold = PartyLeader.Gold,
                CharacterSheet = CharacterSheetWithCombatConditions(characters[character.CharacterId], battle),
                History = CreateCharacterHistory(characters[character.CharacterId]),
                SpellInfo = SpellcastingRules.TryGetSchool(characters[character.CharacterId].CharacterClass.Id, out _)
                    ? SpellInfoSnapshotProjector.Create(characters[character.CharacterId]) : null,
                ExplorationSpellOptions = snapshot.Phase == GameSessionPhase.Exploration &&
                                          positions.TryGetValue(character.CharacterId, out var characterPosition)
                    ? GetSpellOptions(characters[character.CharacterId], characterPosition, null, inCombat: false)
                    : null
            }).Concat(followerSnapshots).ToArray()
        };
    }

    private BattleSnapshot CreateBattleSnapshot(BattleEncounter battle)
    {
        var current = battle.Current;
        var prompt = CreateBattlePromptState(battle);
        var focusTargetId = BattleFocusTarget(battle, current);

        var participants = battle.Turns.Participants.Select(participant =>
        {
            var character = battle.CharacterFor(participant.Id);
            var enemy = battle.EnemyFor(participant.Id);

            return new TacticalBattleParticipantSnapshot(
                participant.Id,
                character?.Name ?? enemy?.Name ?? participant.Id.Value,
                participant.Side,
                participant.Kind,
                participant.Position,
                participant.CurrentInitiative,
                participant.Id == current.Id && _battleMovementTurnId == battle.Turns.TurnId
                    ? _battleMovementRemaining
                    : participant.MovementAllowance,
                participant.EligibleFromCycle,
                participant.State,
                character?.CurrentVitality ?? enemy?.CurrentHitPoints ?? 0,
                character?.MaximumVitality ?? enemy?.MaximumHitPoints ?? 0,
                participant.Id == current.Id,
                participant.Id == focusTargetId,
                enemy?.Id,
                CombatConditionsFor(battle, participant.Id));
        })
        .OrderByDescending(participant => participant.Initiative)
        .ToArray();

        return new BattleSnapshot(
            prompt.BattleId,
            prompt.TurnId,
            battle.ActionNumber,
            prompt.IsPlayerTurn,
            prompt.ActingCharacterId,
            new SessionEnemySnapshot(
                prompt.FocusEnemy.Definition.Id,
                prompt.FocusEnemy.Name,
                prompt.FocusEnemy.Position,
                prompt.FocusEnemy.CurrentHitPoints,
                prompt.FocusEnemy.MaximumHitPoints,
                prompt.FocusEnemy.Id),
            prompt.AllowedActions,
            prompt.SpellOptions,
            prompt.TacticOptions,
            battle.Turns.Cycle,
            participants,
            prompt.ItemOptions,
            prompt.ValidTargetEnemyIds,
            IsQuickBattle: _isQuickBattle,
            ActionDetails: _lastBattleActionDetails,
            TurnUndeadTargetEnemyId: prompt.TurnUndeadTargetEnemyId,
            ActionTargets: prompt.ActionTargets);
    }

    private BattlePromptState CreateBattlePromptState(BattleEncounter battle)
    {
        var current = battle.Current;
        var actingCharacter = battle.CurrentCharacter;
        var isPaused = battle.PauseReason != BattlePauseReason.None;
        var focusEnemy =
            battle.CurrentEnemy ??
            battle.SelectedTargetEnemy() ??
            (actingCharacter is null
                ? null
                : ReachableEnemies(battle, actingCharacter)
                    .OrderBy(enemy => enemy.CurrentHitPoints)
                    .FirstOrDefault()) ??
            battle.Enemies
                .Where(enemy => enemy.CurrentHitPoints > 0)
                .OrderBy(enemy => TacticalDistance.Between(current.Position, enemy.Position))
                .First();
        var actingCharacterId = isPaused
            ? PartyLeader.Id
            : actingCharacter?.Id ?? PartyLeader.Id;
        IReadOnlyList<BattleActionKind> allowed = isPaused
            ? [BattleActionKind.ResumeBattle]
            : actingCharacter is null
                ? [BattleActionKind.AdvanceEnemyTurn]
                : GetAllowedBattleActions(battle, actingCharacter, focusEnemy);
        var spellOptions =
            !isPaused &&
            actingCharacter is not null &&
            allowed.Contains(BattleActionKind.CastSpell)
                ? GetSpellOptions(
                    actingCharacter,
                    GetCasterPosition(actingCharacter),
                    focusEnemy,
                    inCombat: true)
                : null;
        var actionTargets = !isPaused && actingCharacter is not null
            ? CreateBattleActionTargets(battle, actingCharacter, allowed)
            : null;

        return new BattlePromptState(
            battle.Id,
            battle.Turns.TurnId,
            actingCharacterId,
            actingCharacter,
            focusEnemy,
            allowed,
            spellOptions,
            !isPaused && actingCharacter is not null
                ? GetBattleTacticOptions(battle, actingCharacter, focusEnemy)
                : null,
            !isPaused && actingCharacter is not null
                ? GetBattleItemOptions(battle, actingCharacter)
                : null,
            !isPaused && actingCharacter is not null
                ? ReachableEnemies(battle, actingCharacter)
                    .Select(enemy => enemy.Id)
                    .ToArray()
                : null,
            !isPaused && actingCharacter is not null && allowed.Contains(BattleActionKind.TurnUndead)
                ? PreferredTurnUndeadTarget(battle, actingCharacter)?.Id
                : null,
            actionTargets,
            isPaused,
            isPaused || actingCharacter is not null);
    }

    private IReadOnlyList<BattleActionTargetsSnapshot> CreateBattleActionTargets(BattleEncounter battle,
        LiveCharacter character, IReadOnlyCollection<BattleActionKind> allowed)
    {
        var targets = new List<BattleActionTargetsSnapshot>();
        if (allowed.Contains(BattleActionKind.PhysicalAttack))
            targets.Add(new BattleActionTargetsSnapshot(BattleActionKind.PhysicalAttack,
                OrderedTargetIds(ReachableEnemies(battle, character))));
        if (allowed.Contains(BattleActionKind.ShieldBash))
            targets.Add(new BattleActionTargetsSnapshot(BattleActionKind.ShieldBash,
                OrderedTargetIds(AdjacentEnemies(battle, character))));
        if (allowed.Contains(BattleActionKind.TurnUndead))
            targets.Add(new BattleActionTargetsSnapshot(BattleActionKind.TurnUndead,
                OrderedTargetIds(TurnUndeadTargets(battle, character))));
        return targets;
    }

    private static IReadOnlyList<WorldEntityId> OrderedTargetIds(IEnumerable<Enemy> enemies) => enemies
        .OrderBy(enemy => enemy.Position.Y)
        .ThenBy(enemy => enemy.Position.X)
        .ThenBy(enemy => enemy.Id.ToString(), StringComparer.Ordinal)
        .Select(enemy => enemy.Id)
        .ToArray();

    private CharacterSheetSnapshot CharacterSheetWithCombatConditions(LiveCharacter character,
        BattleSnapshot? battle)
    {
        var combatIcons = battle?.Participants?
            .FirstOrDefault(participant => participant.Id == CombatantId.ForCharacter(character.Id))?
            .Conditions?
            .Select(condition => condition.Icon) ?? [];
        return CharacterSheetWithCombatIcons(character, combatIcons);
    }

    private CharacterSheetSnapshot CharacterSheetWithCombatIcons(LiveCharacter character,
        IEnumerable<string> combatIcons)
    {
        var sheet = CharacterSheetSnapshotProjector.Create(character, _gameData.ExperienceByLevel,
            CurrentLevelVisionModifier);
        return sheet with
        {
            StatusIcons = sheet.StatusIcons.Concat(combatIcons).Distinct(StringComparer.Ordinal).ToArray()
        };
    }

    internal static IReadOnlyList<CombatConditionSnapshot> CombatConditionsFor(BattleEncounter battle,
        CombatantId combatantId)
    {
        var conditions = new List<CombatConditionSnapshot>();
        if (battle.StaggerFor(combatantId) is { } stagger)
            conditions.Add(new CombatConditionSnapshot(CombatConditionKind.Staggered,
                CombatConditionPresentation.StaggerName, CombatConditionPresentation.StaggerIcon,
                stagger.Severity, stagger.IsResolved, stagger.BlocksMovement,
                stagger.BlocksOffensiveActions));
        if (battle.CharacterFor(combatantId) is { } character && battle.RuntimeFor(character).IsBarbarianRaging)
            conditions.Add(new CombatConditionSnapshot(CombatConditionKind.BarbarianRage,
                CombatConditionPresentation.BarbarianRageName,
                CombatConditionPresentation.BarbarianRageIcon));
        return conditions;
    }

    private IReadOnlyList<BattleItemOptionSnapshot> GetBattleItemOptions(BattleEncounter battle,
        LiveCharacter character) => TacticalBattleCoordinator.GetBattleItemOptions(battle, character);

    private static bool IsBattleItemUseful(LiveCharacter character, MiscItemDefinition item) =>
        TacticalBattleCoordinator.IsBattleItemUseful(character, item);

    private static CharacterHistorySnapshot CreateCharacterHistory(LiveCharacter character) => new(
        character.MonsterKills.Select(pair => new MonsterKillSnapshot(pair.Key, pair.Value)).ToArray(),
        character.NpcJoinedMazeLevel, character.NpcJoinedLocation, character.NpcBehavior?.ToString());

    private SessionCharacterSnapshot CreateCharacterDetailsSnapshot(LiveCharacter character) => new(
        character.Id, character.Name, character.Race.Id, character.CharacterClass.Id, character.Level,
        character.CurrentVitality, character.MaximumVitality, character.CurrentMana, character.MaximumMana,
        character.FoodLevel, character.WaterLevel, PartyLeader.Gold, character.IsAlive, null,
        character.Statuses.Select(status => status.Id).ToArray(), InventorySnapshotProjector.Create(character),
        CharacterSheetWithCombatIcons(character, _activeBattle is { IsCompleted: false } battle
            ? CombatConditionsFor(battle, CombatantId.ForCharacter(character.Id)).Select(condition => condition.Icon)
            : []),
        character.Color, SpellInfo: character.IsSpellcaster ? SpellInfoSnapshotProjector.Create(character) : null,
        History: CreateCharacterHistory(character));

    private void HandleLocalSessionEvent(GameSessionEvent sessionEvent)
    {
        switch (sessionEvent)
        {
            case GameCommandRejectedEvent rejected
                when rejected.RecipientPlayerId == _session.HostPlayerId:

                _localBattleCommandGate.Complete(rejected.CommandId);
                _renderer.DrawInventoryMessage(
                    rejected.Reason,
                    ConsoleColor.Red);
                break;
        }
    }

    public Game(GameDataCatalog gameData, CharacterRoster characterRoster, LiveCharacter selectedCharacter,
        GameSaveService gameSaveService, BackgroundMusicPlayer backgroundMusicPlayer, GameSaveData? loadedState = null, GameSession? session = null,
        GameSettingsService? gameSettings = null)
    {
        CharacterRoster = characterRoster;
        PartyLeader = selectedCharacter;
        _gameData = gameData;
        _gameSaveService = gameSaveService;
        _backgroundMusic = backgroundMusicPlayer;
        _formation = PartyFormationRules.CreateDefault(characterRoster.Party.Members.Select(member => member.Id),
            selectedCharacter.Id);
        _questNpcInstanceRegistry = new QuestNpcInstanceRegistry();
        _gameStateMapper = new GameStateMapper(gameData, characterRoster, selectedCharacter, _questNpcInstanceRegistry);
        _loadedState = loadedState;
        _session = session ?? new GameSession(characterRoster.Party, selectedCharacter);
        StaticGameSettings = _gameSettings = gameSettings ?? new GameSettingsService();
        _renderer = new ConsoleRenderer(gameData, characterRoster.Party, () => _maze?.PartyMembers
            .Where(member => member.IsTemporaryFollower)
            .Select(member => member.Character)
            .ToArray() ?? [], _gameSettings.Settings);
        _renderer.SharedWindowPresented = CaptureSharedWindowPresentation;
        _renderer.CharacterSheet.SetFormationStatus(_formation);
        _renderer.SetGoldenKeyCount(0);
        _soundEffects = new SoundEffects(_gameSettings.Settings,
            message => _renderer.DrawDeveloperMessage(message));
        _innController = new InnController(gameData, characterRoster, selectedCharacter, _renderer,
            effect => PlaySessionSound(effect),
            _random, AwardExperienceResult, ResolvePerkOffers, PreparePartySpells, ReadInnKey,
            ShowSynchronizedRest, () => _maze?.PartyMembers
                .Where(member => member.IsTemporaryFollower)
                .Select(member => member.Character)
                .ToArray() ?? [], GetSpecialInnRecruitCandidates, SpecialInnRecruitAccepted,
            RunHostWindow, _backgroundMusic);
        _battleSystem = new BattleSystem(_random, gameData.MonsterAbilities, gameData.Statuses,
            gameData.StrengthHitBonuses);
        _spellExecutionService = new SpellExecutionService(gameData, _random);
        _enemySpellcastingService = new EnemySpellcastingService(gameData, _random);
        _battleActionCoordinator = new BattleActionCoordinator(gameData, _battleSystem, _spellExecutionService, _random);
        _battleCoordinator = new TacticalBattleCoordinator(gameData, _battleSystem, _random);
        _storyConversationCoordinator = new StoryConversationCoordinator(gameData, _random);
        _progressionService = new CharacterProgressionService(gameData, _random);
        _sustenanceService = new PartySustenanceService(gameData, _random);
        _dungeonTrapService = new DungeonTrapService(gameData, _random);
        _lootService = new LootAndInventoryService(gameData, _random);
        _expeditionCoordinator = new DungeonExpeditionCoordinator(gameData, _random);
        _sessionEventService = new SessionEventService(_renderer, _soundEffects, _random);
        _partyCommandController = new PartyCommandController(_random);
        _partyAiController = new PartyAiController(_random);
        _partyCommandState = new PartyCommandState(false, false, false, null);
        _commandDispatcher = new SessionCommandDispatcher(_session, this, selectedCharacter.Id);
        _questWorldContext = CreateQuestWorldContext();
        _questManager = CreateQuestManager(gameData);
        _questSaveAdapter = new QuestSaveAdapter(gameData, _questNpcInstanceRegistry);
        _npcQuestCoordinator = new NpcQuestCoordinator(_gameData, _questManager, _questWorldContext);
        _questManager.QuestChanged += ProjectQuestChange;
        _questInventorySynchronizer = new QuestInventorySynchronizer(_questManager);
        _doorInteractions = new DoorInteractionController(gameData, _renderer,
        (effect, actor) => PlaySessionSound(effect, [actor.Id]), _random,
        (message, color, actor) => RecordSessionActivity(SessionActivityKind.System, message, color, [actor.Id]),
        new QuestDoorAccessService(_questManager).TryGrantAccess);
        _backgroundMusic.SetReportCallback(message =>
        {
            if (_session.Phase == GameSessionPhase.Inn)
                _innController.ReportMessage(message);
            else
                _renderer.DrawDeveloperMessage(message);
        });
        _session.EventPublished += HandleLocalSessionEvent;
    }
}
