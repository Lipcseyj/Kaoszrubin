using KaoszRubin.Data;
using KaoszRubin.Application;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Combat;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Magic;
using KaoszRubin.Domain;
using KaoszRubin.UI;
using static KaoszRubin.GameInput;
using MainMenu = KaoszRubin.UI.MainMenu;

namespace KaoszRubin;

/// <summary>A játék futását és felhasználói bemenetét koordinálja.</summary>
public sealed class Game
{
    private const string FirstUniqueWorldNpcId = "NPC020";
    private static readonly IReadOnlyList<string> CampaignIntroduction =
    [
        "Az Aranykor hajnalán négy ősi elementálmágus őrizte a világ egyensúlyát: Pyranthos, a Lángok Atyja; Nymara, a Mélytengerek Asszonya; Goram, a Hegyek Szíve; és Zephyriel, az Ég Vándora. Együtt alkották meg a Káoszrubint, amelyben tűz, víz, föld és szél ereje egyetlen, lüktető drágakővé forrt.",
        "A rubin hatalma azonban nagyobbnak bizonyult alkotói bölcsességénél. A négy szövetséges egymás ellen fordult, palotáik elégtek, tengereik felforrtak, hegyeik meghasadtak. A végső összecsapásban Zephyriel ragadta magához a követ, és mielőtt társai elérhették volna, egy másik dimenzióba rejtette: a folyton változó Káoszlabirintusba.",
        "Zephyriel tizenkét aranylakatot kovácsolt a dimenzió kapujára. Mindegyikhez egyetlen aranykulcs tartozik, s azokat a labirintus legfélelmetesebb őrzőire bízta. Aki mind a tizenkettőt megszerzi, megnyithatja a Rubin Útját — és kezébe veheti azt a hatalmat, amely birodalmakat emelhet fel vagy törölhet el.",
        "Most Vhar-Zul, a Sötét Úr is a Káoszrubint keresi. Árnyékhadseregei már áttörték a dimenzió peremét. Ha ő ér előbb a kőhöz, nem marad királyság, amely ellenállhatna neki.",
        "Aurelios Máguskirály ezért hívatott benneteket a Csillagtoronyba. Jósai, a Csillagszeműek ugyanazt a jelet látták mind a hét éjszakai égen: tizenkét aranyfény között a ti alakotok állt. A jóslat szerint ti vagytok a Kulcshordozók — az egyetlenek, akik végigjárhatják a kaotikus szinteket anélkül, hogy a dimenzió elnyelné őket.",
        "Nem indultok teljesen egyedül. Aurelios ügynököket küldött elétek: kereskedőket, mestereket, gyógyítókat és titkok tudóit. A Káoszlabirintus fogadóiban várnak majd rátok, ahol a világok közötti vihar rövid időre elcsendesedik.",
        "Gyűjtsétek össze a tizenkét aranykulcsot. Előzzétek meg Vhar-Zult. Találjátok meg a Káoszrubint — és amikor eljön az idő, döntsétek el, méltó volt-e Aurelios bizalma."
    ];

    private static readonly IReadOnlyList<string> TwelveKeysStory =
    [
        "A tizenkettedik boss elbukik. Az utolsó aranykulcs a levegőbe emelkedik, és társai felelnek hívására: tizenkét fénypont kering körülöttetek, akár egy aranyból rajzolt csillagkép.",
        "A kulcsok egyszerre fordulnak el láthatatlan zárakban. A Káoszlabirintus megrázkódik. Távoli falak omlanak le, eddig nem létező lépcsők nőnek ki a semmiből, és a mélységből olyan harangszó kondul, amelyet nem füllel, hanem a csontjaitokban hallotok.",
        "Ekkor megjelenik előttetek Aurelios Máguskirály áttetsző képmása. „Beteljesítettétek a Csillagszeműek első jóslatát” — mondja. — „De a kulcsok csak a külső pecséteket törték fel. Zephyriel a Rubinhoz vezető utat huszonegy halálos szint mögé rejtette. Ami mögöttetek van, próba volt. Ami előttetek áll, háború.”",
        "A látomás mögött egy másik alak is kirajzolódik: Vhar-Zul fekete koronája, majd két parázsló szem. A Sötét Úr nevetése végigviharzik a dimenzión. Ő is megérezte a zárak felnyílását — és most már pontosan tudja, merre vezet a Rubin Útja.",
        "A tizenkét kulcs egyetlen ragyogó pecsétté olvad a parti előtt. A kapu túloldalán huszonegy új, brutális világ vár, mind közelebb a Káoszrubin lüktető fényéhez. A küldetés első célja teljesült. A valódi verseny most kezdődik."
    ];

    private static readonly IReadOnlyDictionary<string, BossNarrative> BossNarratives =
        new Dictionary<string, BossNarrative>(StringComparer.OrdinalIgnoreCase)
        {
            [MonsterIds.Patkányember] = new("II. fejezet — A csatornák koronája",
            [
                "Rikkancs vagyok, a Patkányjáratok királya! Ne nevess a koronámon — tizenkét kanálból hajlítottam, és mindet becsületesen loptam.",
                "Ezt a fényes kulcsot egy kék köpenyes, szélből szőtt ember dobta a fészkembe. Azt mondta, őrizzem, amíg a falak énekelni nem kezdenek. A falak sosem énekelnek. Csak a patkányok. Főleg éjjel.",
                "Ha a csontzabáló Morghult keresitek, vigyetek neki sót. Utálja a sót. Én meg Morghult utálom, mert megette három unokatestvéremet — bár az egyik talán csak elköltözött."
            ]),
            [MonsterIds.Ghoul] = new("III. fejezet — Morghul lakomája",
            [
                "Morghulnak hívtak, amikor még emlékeztem a saját arcomra. Most a katakombák neveznek el minden éjjel újra, amikor a koporsófedelek alatt megfordulnak a holtak.",
                "A kulcs nem étel. Megpróbáltam. Nem hús, nem csont, még csak nem is sikolt. De amikor a markomban tartom, tizenkét dobbanást hallok a föld mélyéről. A tizenkettedik után mindig hideg szél fúj végig a sírokon.",
                "Grashka, az ork sámán azt állítja, tudja, milyen ajtót nyit. Hazudik. Grashka mindig hazudik. Egyszer azt mondta, a koponya nem levesestál. Ostoba ork."
            ]),
            [MonsterIds.OrkSámán] = new("IV. fejezet — Grashka füstjóslata",
            [
                "Én vagyok Grashka, a Vasagyar törzs füstlátója. A többiek azt hiszik, a szellemek beszélnek hozzám. Valójában többnyire a füst beszél, és annak is rettenetes a memóriája.",
                "Az aranykulcsot álmomban kaptam egy négyarcú vihartól. Négy hang veszekedett benne: láng, hullám, kő és szél. A szél győzött, de úgy remegett, mint aki tudja, hogy egyszer visszajönnek érte.",
                "Északon Hrold, a fagyóriás vár. Fél a vörös szárnyaktól, bár ezt sosem vallaná be. Ha találkoztok vele, mondjátok meg, hogy Grashka szerint a szakálla csak ráfagyott kecskeszőr."
            ]),
            [MonsterIds.Fagyóriás] = new("V. fejezet — Hrold dermedt esküje",
            [
                "Hrold Jégszakáll vagyok. Százhetven telet számoltam, aztán meguntam. Azóta a jégcsapokat számolom. Ez itt a négyszáznyolcvankétezredik. Vagy ugyanaz, mint tegnap.",
                "A kulcsot egy vörös sárkány karmaiból téptem ki, amikor még fiatal és ostoba volt. Azóta nagyobb lett, én pedig bölcsebb: ma már tudom, hogy Azrakar nem felejt. A tüze néha még álmomban is megolvasztja a csarnok falát.",
                "A kulcs belsejében kaput látok, a kapu mögött pedig egy vörös követ. Nem tudom, miért kell tizenkét kulcs egyetlen kapuhoz. Talán a kicsi népek ennyire félnek a huzattól."
            ]),
            [MonsterIds.VörösSárkány] = new("VI. fejezet — Azrakar parázstrónusa",
            [
                "Azrakar vagyok, az Első Parázs örököse. Láttam királyokat megöregedni, birodalmakat hamuvá válni, és Hroldot elfutni a saját megperzselt szakállával. Ezt a részt különösen szívesen láttam.",
                "Tudom, hogy a kulcs pecsétet tör. Zephyriel, a szél ősmágusa maga bízta az elődeimre. Azt mondta, tizenkét őrző közül egy se értse az egész tervet. Bölcs óvatosság — vagy gyáva bizalmatlanság.",
                "A mocsárban Sziszara, a hidra őrzi a következő kulcsot. Kilenc feje van, és mind a kilenc más történetet mesél arról, hogyan győzött le engem. Egyik sem igaz. A tizedik történet viszont talán az lenne."
            ]),
            [MonsterIds.Hidra] = new("VII. fejezet — Sziszara kilenc hangja",
            [
                "Sziszara vagyok. Én mondom ezt, nem a bal szélső fej. Az mindig hazudik. A jobb szélső szerint mindannyian Sziszara vagyunk, de ő egyszer egy követ is tojásnak nézett.",
                "A kulcs a mocsár fenekéről került elő, egy szél nélküli vihar után. Ha közel tesszük a többi aranyhoz, énekel. Ha közel tesszük egy békához, a béka felrobban. Ezt fontosabb felfedezésnek tartom.",
                "A kristálycsarnokban Xyrax figyel minden irányba. Ő látja a kulcsok közötti fonalakat. Mi nem szeretjük Xyraxot. Túl sok szeme van. Ezt kilenc fej teljes egyetértésben mondja."
            ]),
            [MonsterIds.VénBeholder] = new("VIII. fejezet — Xyrax ezer látomása",
            [
                "Xyrax vagyok, a Századik Tekintet. Egyik szemem a jelent látja, három a lehetséges jövőket, kettő a múlt hazugságait. A maradékot viszketésre használom.",
                "Látom a tizenkét kulcs aranyfonalát. Nem ajtót nyitnak: helyet kényszerítenek a káoszra, hogy ajtóvá váljon. Mögötte a négy őserő egyetlen rubinban marja egymást, és huszonegy árnyék áll közte és a világotok között.",
                "Ossyra, a csontsárkány még emlékszik Zephyriel hangjára. Fél saját ősétől, Nharaztól, a drakolichtól. Jogos félelem. Egy lehetséges jövőben Nharaz megeszi a lelkemet. Tizenhét másikban én eszem meg az övét."
            ]),
            [MonsterIds.Csontsárkány] = new("IX. fejezet — Ossyra csontemlékezete",
            [
                "Ossyra volt a nevem, amikor pikkely fedte e csontokat. Zephyriel akkor érkezett a dermedt mélységbe, amikor még az ég is fiatalabb volt. Nem parancsolt. Könyörgött, hogy őrizzem a kulcsot azoktól, akik a Rubint fegyverré tennék.",
                "A halál nem oldotta fel az eskümet. Csak elvette belőle a meleget. Évszázadok óta hallom, ahogy a pecsétek egymást keresik a dimenzión át.",
                "Nharaz, az első sárkányból lett drakolich azt hiszi, a Rubin visszaadhatja a húsát. Téved. A Rubin nem visszaad: átír. Ha eljuttok hozzá, ne higgyetek annak a hangnak, amelyen a halott anyátok szólít majd benneteket."
            ]),
            [MonsterIds.Ősvámpír] = new("X. fejezet — Velkhar örök éjszakája",
            [
                "Velkhar gróf vagyok, és már akkor untam ezt az erődöt, amikor dédapáitok még udvariasan kopogtak a kripták ajtaján. Mostanában csak Aurelios kémei jönnek. Udvariatlanok, de legalább friss vérük van.",
                "Igen, ismerem a Máguskirály tervét. Emberei fogadóról fogadóra építették ki az útvonalatokat. Azt hiszik, nem vettem észre őket. Hagytam, hogy továbbmenjenek; kíváncsi voltam, valóban megérkeznek-e a csillagok kiválasztottjai.",
                "Vhar-Zul is küldött követeket. Azt ígérte, nappaltalan világot ad nekem. Ostoba ajánlat — már van egy. A Drakolich viszont elfogadta az övét. Nharaz tudja, hogyan nyílik a belső út, és kinek a vére kell hozzá."
            ]),
            [MonsterIds.Drakolich] = new("XI. fejezet — Nharaz fekete evangéliuma",
            [
                "Nharaz vagyok, akit a sárkányok is ősüknek neveztek, mielőtt nevemet kivésték a csontjaikból. Ossyra figyelmeztetett rólam, igaz? Mindig szeretett történetekkel védekezni a valóság ellen.",
                "A tizenkét kulcs a külső pecsét gyűrűjét bontja fel. Utána huszonegy szint következik, mindegyik Zephyriel egy-egy emlékéből és félelméből épült. Az út végén nem kincsesház áll, hanem a Káoszrubin börtöne.",
                "Vhar-Zul megígérte, hogy a Rubin lángjával új testet ad nekem. Tudom, hogy hazudik. Én is hazudtam neki. A Balor, Ashkaroth azonban vakon szolgálja. Ha legyőzitek, a Sötét Úr végre személyesen is rátok figyel majd."
            ]),
            [MonsterIds.BalorDémon] = new("XII. fejezet — Ashkaroth vértrónusa",
            [
                "Ashkaroth vagyok, Vhar-Zul ostora és a Vértrónus ura. Nem véletlenül jutottatok idáig. Hagytuk, hogy összegyűjtsétek a kulcsokat, mert a pecséteket sem démon, sem árnyék nem törheti fel — csak azok, akiket a csillagok megjelöltek.",
                "Ti nem Aurelios hősei vagytok. Ti vagytok a kulcs, amelyet mindkét király ugyanabba a zárba próbál illeszteni. Amint az utolsó aranydarab a helyére kerül, uram seregei meglátják a Rubin Útját.",
                "A Káoszsárkány nem szolgál minket. Kael-Zhur még Vhar-Zult is gyűlöli, mert ismeri a Rubin valódi árát. Menjetek csak hozzá. Ha megöltök, a halálom lesz a jelzőtűz, amely odavezeti hozzátok a Sötét Urat."
            ]),
            [MonsterIds.Káoszsárkány] = new("XIII. fejezet — Kael-Zhur, az utolsó lakat",
            [
                "Kael-Zhur vagyok, a Káosz első lélegzete és Zephyriel utolsó bűne. Nem születtem. A Rubin álmodott meg engem, hogy legyen valami, amitől még a négy alkotója is félhet.",
                "Tudom, miért jöttetek. Aurelios azt mondta, meg kell előznötök Vhar-Zult. Vhar-Zul azt hiszi, ti nyitjátok ki neki az utat. Mindketten igazat mondanak, és mindketten hazudnak. A Káoszrubin nem engedelmeskedik annak, aki megtalálja — átformálja őt arra, amire a világ leginkább vágyik vagy amitől legjobban retteg.",
                "Kulcsom az utolsó a tizenkettőből. Ha elveszitek, feltárul Zephyriel huszonegy szintből álló belső útja. Ott már nem őrzők várnak, hanem egy ősmágus emlékei, széthullott törvények és Vhar-Zul közeledő serege.",
                "Évszázadok óta őrzöm ezt a lakatot, és bevallom: rettenetesen unatkozom. Mutassátok meg, Kulcshordozók, hogy a csillagok valóban benneteket láttak — vagy csak egy különösen kegyetlen tréfát játszottak velem."
            ])
        };
    private const int ZombieSpeed = 2;
    private const int ZombieMoveIntervalMilliseconds = 700;
    private const int MinimumPartyMoveDelayMilliseconds = 250;
    private const int MaximumPartyMoveDelayMilliseconds = 300;
    private const int CatchUpMoveDelayMilliseconds = 90;
    private static readonly Direction[] Directions = Enum.GetValues<Direction>();
    private const int MazeWidth = ConsoleRenderer.PlayfieldWidth;
    private const int MazeHeight = ConsoleRenderer.PlayfieldHeight;
    private readonly GameDataCatalog _gameData;
    private MazeGenerator _generator = null!;
    private readonly ConsoleRenderer _renderer;
    private Maze _maze = null!;
    private Player _player = null!;
    private FogOfWar _fogOfWar = null!;
    private readonly Random _random = new();
    private readonly BattleSystem _battleSystem;
    private readonly GameSaveService _gameSaveService;
    private readonly GameStateMapper _gameStateMapper;
    private readonly DoorInteractionController _doorInteractions;
    private readonly InnController _innController;
    private ICoopHostLoop? _activeCoopHost;
    private NarrativeSnapshot? _activeNarrative;
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
    private readonly MusicSettingsService _musicSettings;
    private readonly GameSession _session;
    private long _localCommandId;
    private BattleState? _activeBattleState;
    private int _pendingBattleSupportDamage;
    private bool _battleStarted;
    private bool _gameOver;
    private bool _characterSheetFocused;
    private HeldInventoryItem? _heldInventoryItem;
    private DateTime _nextNeedsDrain;
    private DateTime _nextNpcSelfCareCheck;
    private readonly Dictionary<Enemy, DateTime> _nextEnemyMoves = [];
    private readonly Dictionary<PartyMemberAvatar, DateTime> _nextPartyMoves = [];
    private readonly List<Position> _leaderTrail = [];
    private bool _partyHoldingPosition;
    private bool _partyRegrouping;
    private bool _partyAttackMode;
    private bool _saveAfterBattle;
    private bool _timeStopUsedThisBattle;
    private bool _battleTacticHintLogged;
    private bool _battleActionHintLogged;
    private int _battleStartingVitality;
    private int _battleStartingMana;
    private HashSet<string> _battleStartingStatusIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<LiveCharacter> _turnUndeadUsedThisBattle = [];
    private readonly Dictionary<(CharacterId CharacterId, NpcComplaintKind Kind), DateTime> _nextNpcComplaints = [];
    private readonly HashSet<(CharacterId CharacterId, NpcComplaintKind Kind)> _reportedNpcShortages = [];
    private readonly List<(LiveCharacter Character, LevelUpResult Result)> _pendingLevelUps = [];
    private readonly Queue<SessionActivitySnapshot> _sessionActivities = new();
    private long _sessionActivitySequence;
    private readonly Queue<SessionSoundSnapshot> _sessionSounds = new();
    private readonly Dictionary<string, QuestJournalEntrySnapshot> _questJournal =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<PlayerId> _helpPausePlayers = [];
    private DateTime? _helpPauseStartedUtc;
    private long _sessionSoundSequence;
    private DateTime? _partyScatterUntil;
    private Direction _leaderFacing = Direction.Right;
    private int _mazeLevel = 1;
    private bool _hasRestedThisLevel;
    private bool _developerPhasing;
    private int _lastDeveloperUniqueNpcIndex = -1;
    private readonly HashSet<string> _collectedBossKeyIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _seenBossIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<WorldEntityId> _spottedEnemyIds = [];
    public CharacterRoster CharacterRoster { get; }
    public LiveCharacter SelectedCharacter { get; }
    public GameSession Session => _session;
    public BattleState? ActiveBattle => _activeBattleState;

    public SessionSnapshot CreateSessionSnapshot()
    {
        if (_maze is null || _player is null)
            throw new InvalidOperationException("Session snapshot csak inicializált játékból készíthető.");
        var positions = new Dictionary<CharacterId, Position>
        {
            [SelectedCharacter.Id] = _player.Position
        };
        foreach (var member in _maze.PartyMembers) positions[member.Character.Id] = member.Position;

        BattleSnapshot? battle = null;
        if (_activeBattleState is { IsCompleted: false } state)
        {
            var battleCharacter = state.Player;
            battle = new BattleSnapshot(state.Id, state.TurnId, state.Round, state.IsPlayerTurn,
                state.PlayerCharacterId,
                new SessionEnemySnapshot(state.EnemyDefinitionId, state.Enemy.Name, state.Enemy.Position,
                    state.CurrentEnemyHitPoints, state.Enemy.Definition.HitPoints ?? state.CurrentEnemyHitPoints),
                GetAllowedBattleActions(battleCharacter, GetCasterPosition(battleCharacter), state.Enemy),
                state.IsPlayerTurn
                    ? GetSpellOptions(battleCharacter, GetCasterPosition(battleCharacter), state.Enemy, inCombat: true)
                    : null,
                GetBattleTacticOptions(state));
        }
        var snapshot = _session.CreateSnapshot(new SessionSnapshotContext(_mazeLevel, _maze.LevelName, positions,
            battle, WorldSnapshotProjector.Create(_maze, _fogOfWar, _activeBattleState)));
        var followers = _maze.PartyMembers
            .Where(member => member.IsTemporaryFollower)
            .Select(member => member.Character)
            .Distinct()
            .ToArray();
        var characters = CharacterRoster.Party.Members.Concat(followers).ToDictionary(character => character.Id);
        var followerSnapshots = followers.Select(character => new SessionCharacterSnapshot(
            character.Id, character.Name, character.Race.Id, character.CharacterClass.Id, character.Level,
            character.CurrentVitality, character.MaximumVitality, character.CurrentMana, character.MaximumMana,
            character.FoodLevel, character.WaterLevel, SelectedCharacter.Gold, character.IsAlive,
            positions.GetValueOrDefault(character.Id), character.Statuses.Select(status => status.Id).ToArray(),
            Inventory: InventorySnapshotProjector.Create(character),
            CharacterSheet: CharacterSheetSnapshotProjector.Create(character,
                _gameData.ExperienceByLevel, CurrentLevelVisionModifier), Color: character.Color,
            IsTemporaryFollower: true)).ToArray();
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
            Activities = _sessionActivities.ToArray(),
            Sounds = _sessionSounds.ToArray(),
            PartyGold = SelectedCharacter.Gold,
            QuestJournal = OrderedQuestJournal(),
            Party = snapshot.Party.Select(character => character with
            {
                Gold = SelectedCharacter.Gold,
                CharacterSheet = CharacterSheetSnapshotProjector.Create(characters[character.CharacterId],
                    _gameData.ExperienceByLevel, CurrentLevelVisionModifier),
                SpellInfo = SpellcastingRules.TryGetSchool(characters[character.CharacterId].CharacterClass.Id, out _)
                    ? SpellInfoSnapshotProjector.Create(characters[character.CharacterId]) : null,
                ExplorationSpellOptions = snapshot.Phase == GameSessionPhase.Exploration &&
                                          positions.TryGetValue(character.CharacterId, out var characterPosition)
                    ? GetSpellOptions(characters[character.CharacterId], characterPosition, null, inCombat: false)
                    : null
            }).Concat(followerSnapshots).ToArray()
        };
    }

    public Game(GameDataCatalog gameData, CharacterRoster characterRoster, LiveCharacter selectedCharacter,
        GameSaveService gameSaveService, GameSaveData? loadedState = null, GameSession? session = null,
        MusicSettingsService? musicSettings = null)
    {
        CharacterRoster = characterRoster;
        SelectedCharacter = selectedCharacter;
        _gameData = gameData;
        _gameSaveService = gameSaveService;
        _gameStateMapper = new GameStateMapper(gameData, characterRoster, selectedCharacter);
        _loadedState = loadedState;
        _session = session ?? new GameSession(characterRoster.Party, selectedCharacter);
        _renderer = new ConsoleRenderer(gameData, characterRoster.Party, () => _maze?.PartyMembers
            .Where(member => member.IsTemporaryFollower)
            .Select(member => member.Character)
            .ToArray() ?? []);
        _renderer.SetGoldenKeyCount(0);
        _soundEffects = new SoundEffects(message => _renderer.DrawDeveloperMessage(message));
        _musicSettings = musicSettings ?? new MusicSettingsService();
        _backgroundMusic = new BackgroundMusicPlayer(_musicSettings.Settings,
            message => _renderer.DrawDeveloperMessage(message));
        _doorInteractions = new DoorInteractionController(gameData, _renderer,
            (effect, actor) => PlaySessionSound(effect, [actor.Id]), _random);
        _innController = new InnController(gameData, characterRoster, selectedCharacter, _renderer,
            effect => PlaySessionSound(effect),
            _random, AwardExperienceResult, ResolvePerkOffers, PreparePartySpells, ReadInnKey,
            ShowSynchronizedRest);
        _battleSystem = new BattleSystem(_random, gameData.MonsterAbilities, gameData.Statuses,
            gameData.StrengthHitBonuses);
    }

    // NPC spellcasting for combat
    private BattlePlayerAction? ChooseNpcBattlePlayerAction(PartyMemberAvatar member, Enemy enemy,
        LiveCharacter? supportedFighter = null, Action? onSpellCast = null)
    {
        var caster = member.Character;
        if (!caster.IsAlive) return null;
        if (CanTurnUndead(caster, enemy) && !_turnUndeadUsedThisBattle.Contains(caster))
            return ResolveTurnUndead(caster, enemy);
        if (!caster.IsSpellcaster || !caster.CanCastSpells) return null;
        // Simple mana reserve policy (don't drop below 20% unless emergency)
        var manaReservePercent = 20;
        var manaReserve = Math.Max(0, caster.MaximumMana * manaReservePercent / 100);

        // Emergency heal: any ally under 35% HP, within the spell's range of the caster
        var healThresholdPercent = 35;
        var allies = CharacterRoster.Party.Members.Append(caster).Distinct().Where(c => c.IsAlive).ToList();
        var lowest = allies.OrderBy(c => (double)c.CurrentVitality / c.MaximumVitality).FirstOrDefault();
        if (lowest is not null && (double)lowest.CurrentVitality / lowest.MaximumVitality * 100 <= healThresholdPercent)
        {
            foreach (var spell in caster.MemorizedSpells.Where(s => s.CanUseInCombat))
            {
                var effects = _gameData.GetSpellEffects(spell.Id);
                if (!effects.Any(e => e.Type == SpellEffectType.Heal)) continue;
                var manaCost = SpellcastingRules.EffectiveManaCost(caster, spell);
                if (caster.CurrentMana < manaCost) continue;
                var range = Math.Max(1, spell.Range);
                var reachable = allies.Where(c => Chebyshev(member.Position, GetCasterPosition(c)) <= range).ToList();
                if (reachable.Count == 0) continue;
                var target = reachable.OrderBy(c => (double)c.CurrentVitality / c.MaximumVitality).First();
                var emergency = (double)target.CurrentVitality / target.MaximumVitality <= 0.10;
                if (caster.CurrentMana - manaCost < manaReserve && !emergency) continue;
                // Cast heal
                var divine = caster.RecordDivineSpellCast(spell);
                caster.SpendMana(manaCost);
                PlaySessionSound(SoundEffect.DefensiveSpell, [caster.Id, target.Id]);
                var notes = new List<string>();
                foreach (var effect in effects.Where(e => e.Type == SpellEffectType.Heal))
                    ApplyHealingForCaster(effect, spell, new[] { target }, divine, notes, caster);
                var summary = notes.Count == 0 ? "" : $" {string.Join("; ", notes)}";
                var message = $"{caster.Name} elsüti: {spell.Name} → {target.Name}. -{manaCost} manna.{summary}";
                _renderer.DrawInventoryMessage(message, ConsoleColor.Green);
                RecordSessionActivity(SessionActivityKind.Support, message, ConsoleColor.Green);
                _renderer.RefreshBattleStatusRows();
                onSpellCast?.Invoke();
                return new BattlePlayerAction(message, BattleLogKind.PlayerAttack, 0, 0);
            }
        }

        // Cure status if helpful, within the spell's range of the caster
        foreach (var spell in caster.MemorizedSpells.Where(s => s.CanUseInCombat))
        {
            var effects = _gameData.GetSpellEffects(spell.Id);
            if (!effects.Any(e => e.Type == SpellEffectType.CureStatus)) continue;
            var manaCost = SpellcastingRules.EffectiveManaCost(caster, spell);
            if (caster.CurrentMana < manaCost) continue;
            var range = Math.Max(1, spell.Range);
            var candidates = CharacterRoster.Party.Members.Where(c => c.IsAlive &&
                effects.SelectMany(e => ParseEffectParameters(e.Parameter)).Any(p => c.HasStatus(p)) &&
                Chebyshev(member.Position, GetCasterPosition(c)) <= range).ToList();
            if (!candidates.Any()) continue;
            var targetChar = candidates.First();
            var divine = caster.RecordDivineSpellCast(spell);
            caster.SpendMana(manaCost);
            PlaySessionSound(SoundEffect.DefensiveSpell, [caster.Id, targetChar.Id]);
            var notes = new List<string>();
            foreach (var effect in effects.Where(e => e.Type == SpellEffectType.CureStatus))
                ApplyStatusCureForCaster(effect, new[] { targetChar }, notes);
            var message = $"{caster.Name} elsüti: {spell.Name} → {targetChar.Name}. -{manaCost} manna. {string.Join("; ", notes)}";
            _renderer.DrawInventoryMessage(message, ConsoleColor.Green);
            RecordSessionActivity(SessionActivityKind.Support, message, ConsoleColor.Green);
            _renderer.RefreshBattleStatusRows();
            onSpellCast?.Invoke();
            return new BattlePlayerAction(message, BattleLogKind.PlayerAttack, 0, 0);
        }

        // Más karakter harcát támadó varázslattal csak valódi vészhelyzetben támogatják.
        // Saját harcukban ez a korlátozás nem érvényes.
        if (supportedFighter is not null && !ShouldUseOffensiveSupportSpell(supportedFighter, enemy)) return null;

        // Offensive spell against the enemy the leader is fighting (single-target only, don't waste area/direction spells on one foe)
        foreach (var spell in caster.MemorizedSpells.Where(s => s.CanUseInCombat && s.TargetType == SpellTargetType.Enemy))
        {
            var effects = _gameData.GetSpellEffects(spell.Id);
            if (!effects.Any(e => e.Type == SpellEffectType.Damage)) continue;
            var manaCost = SpellcastingRules.EffectiveManaCost(caster, spell);
            if (caster.CurrentMana < manaCost) continue;
            if (caster.CurrentMana - manaCost < manaReserve) continue;
            if (!IsValidSpellTarget(member.Position, spell, enemy.Position, enemy)) continue;
            var divine = caster.RecordDivineSpellCast(spell);
            caster.SpendMana(manaCost);
            var listeners = new List<CharacterId> { caster.Id };
            if (supportedFighter is not null) listeners.Add(supportedFighter.Id);
            if (_activeBattleState is not null) listeners.Add(SelectedCharacter.Id);
            PlaySessionSound(SoundEffect.OffensiveSpell, listeners);
            var execution = ExecuteSpell(caster, member.Position, spell, enemy.Position, inCombat: true, enemy, divine);
            var message = $"{caster.Name} elsüti: {spell.Name} → {enemy.Name}. -{manaCost} manna. {execution.Summary}";
            _renderer.DrawInventoryMessage(message, ConsoleColor.Green);
            RecordSessionActivity(SessionActivityKind.Support, message, ConsoleColor.Green);
            _renderer.RefreshBattleStatusRows();
            onSpellCast?.Invoke();
            return new BattlePlayerAction(message, BattleLogKind.PlayerAttack, execution.DamageToCurrentEnemy, execution.ExtraPlayerActions);
        }

        return null;
    }

    private bool ShouldUseOffensiveSupportSpell(LiveCharacter fighter, Enemy enemy)
    {
        var enemyCombatAbilities = (enemy.Definition.Strength ?? 0) + (enemy.Definition.Speed ?? 0);
        var fighterCombatAbilities = fighter.EffectiveAbilities.Strength + fighter.EffectiveAbilities.Dexterity;
        return fighter.CurrentVitality * 2 <= fighter.MaximumVitality ||
               enemy.Definition.IsBoss || enemy.Definition.StrengthTier >= 5 ||
               enemyCombatAbilities > fighterCombatAbilities;
    }

    // A globálisan megállított csatában csak az NPC-k adnak automatikus támogatást; más emberi karakter nem.
    private int TryPartyMembersActInBattle(LiveCharacter fighter, Enemy enemy)
    {
        var totalDamage = 0;
        foreach (var member in _maze.PartyMembers.Where(member => member.Character != fighter &&
                     member.Character.IsAlive && !_session.IsHumanControlled(member.Character.Id)))
        {
            member.Character.AdvanceSpellEffects();
            totalDamage += ChooseNpcBattlePlayerAction(member, enemy, fighter)?.DamageToEnemy ?? 0;
        }
        return totalDamage;
    }

    // NPC spellcasting for exploration - simple heals/cures/buffs
    private void TryNpcCastExplorationSpell(PartyMemberAvatar member)
    {
        var caster = member.Character;
        if (!caster.IsAlive || !caster.IsSpellcaster || !caster.CanCastSpells) return;
        var manaReservePercent = 20;
        var manaReserve = Math.Max(0, caster.MaximumMana * manaReservePercent / 100);
        var healThresholdPercent = 50; // more generous during exploration
        var allies = CharacterRoster.Party.Members.Append(caster).Distinct().Where(c => c.IsAlive).ToList();
        var lowest = allies.OrderBy(c => (double)c.CurrentVitality / c.MaximumVitality).FirstOrDefault();
        if (lowest is not null && (double)lowest.CurrentVitality / lowest.MaximumVitality * 100 <= healThresholdPercent)
        {
            foreach (var spell in caster.MemorizedSpells.Where(s => s.CanUseDuringExploration))
            {
                var effects = _gameData.GetSpellEffects(spell.Id);
                if (!effects.Any(e => e.Type == SpellEffectType.Heal)) continue;
                var manaCost = SpellcastingRules.EffectiveManaCost(caster, spell);
                if (caster.CurrentMana < manaCost) continue;
                if (caster.CurrentMana - manaCost < manaReserve) continue;
                var divine = caster.RecordDivineSpellCast(spell);
                caster.SpendMana(manaCost);
                PlaySessionSound(SoundEffect.DefensiveSpell, [caster.Id, lowest.Id]);
                var notes = new List<string>();
                foreach (var effect in effects.Where(e => e.Type == SpellEffectType.Heal))
                    ApplyHealingForCaster(effect, spell, new[] { lowest }, divine, notes, caster);
                var summary = notes.Count == 0 ? "" : $" {string.Join("; ", notes)}";
                var message = $"{caster.Name} elsüti: {spell.Name} → {lowest.Name}. -{manaCost} manna.{summary}";
                _renderer.DrawInventoryMessage(message, ConsoleColor.Green);
                RecordSessionActivity(SessionActivityKind.Support, message, ConsoleColor.Green);
                _renderer.RefreshCharacterSheet(SelectedCharacter);
                return;
            }
        }
    }

    private void ApplyCharacterEffectForCaster(LiveCharacter character, SpellEffectDefinition effect, SpellDefinition spell,
        ActiveSpellEffectType type, bool divineJudgment, LiveCharacter caster)
    {
        var multiplier = divineJudgment ? 200 : 100;
        if (type == ActiveSpellEffectType.GuardianAngel && caster.HasPerk(PerkIds.PriestHealingGrace))
            multiplier = multiplier * 125 / 100;
        character.ApplySpellEffect(new ActiveSpellEffect(spell.Id, type,
            effect.Value, AdjustedDuration(caster, spell, effect, divineJudgment), effect.Dice,
            (int)Math.Round(caster.EffectiveAbilities.Intelligence * effect.IntelligenceMultiplier), true,
            multiplier, effect.Parameter));
    }

    private void ApplyCharacterEffectsForCaster(IEnumerable<LiveCharacter> characters, SpellEffectDefinition effect,
        SpellDefinition spell, ActiveSpellEffectType type, bool divineJudgment, LiveCharacter caster)
    {
        foreach (var character in characters) ApplyCharacterEffectForCaster(character, effect, spell, type, divineJudgment, caster);
    }

    private void ApplyHealingForCaster(SpellEffectDefinition effect, SpellDefinition spell,
        IEnumerable<LiveCharacter> characters, bool divineJudgment, ICollection<string> notes, LiveCharacter caster)
    {
        foreach (var character in characters.Where(character => character.IsAlive))
        {
            var fullHealing = string.Equals(effect.Parameter, "Full", StringComparison.OrdinalIgnoreCase);
            var amount = fullHealing
                ? character.MaximumVitality
                : (effect.Dice?.Roll(_random) ?? 0) +
                  (int)Math.Round(caster.EffectiveAbilities.Intelligence * effect.IntelligenceMultiplier) +
                  caster.Level * effect.LevelMultiplier + effect.Value;
            if (!fullHealing && divineJudgment) amount *= 2;
            if (caster.HasPerk(PerkIds.PriestHealingGrace)) amount = (int)Math.Ceiling(amount * 1.25);
            if (caster.SpecializationId == ClassSpecializations.PriestLife) amount = (int)Math.Ceiling(amount * 1.25);
            var before = character.CurrentVitality;
            character.RestoreVitality(amount);
            notes.Add($"{character.Name}: {FormatHealingResult(character, amount, before)}");
        }
    }

    private void ApplyStatusCureForCaster(SpellEffectDefinition effect, IEnumerable<LiveCharacter> characters,
        ICollection<string> notes)
    {
        var statusIds = ParseEffectParameters(effect.Parameter);
        foreach (var character in characters)
        {
            var removed = statusIds.Where(character.RemoveStatus).Select(StatusName).ToList();
            if (removed.Count > 0) notes.Add($"{character.Name}: ✨ megszűnt {string.Join(" és ", removed)}");
        }
    }

    public void Run(ICoopHostLoop? coopHost = null)
    {
        _activeCoopHost = coopHost;
        Console.CursorVisible = false;
        if (_loadedState is null)
        {
            StartNewMaze(showLevelImage: false);
            ShowSynchronizedNarrative(NarrativeKind.CampaignIntroduction, "A KÁOSZRUBIN KRÓNIKÁJA",
                "I. fejezet — A tizenkét aranykulcs", CampaignIntroduction);
            ShowLevelImage();
        }
        else RestoreGame(_loadedState);
        if (_loadedState is null) _nextNeedsDrain = DateTime.UtcNow + TimeSpan.FromMinutes(1);
        if (coopHost is not null)
            _renderer.DrawDeveloperMessage($"Coop host aktív: {coopHost.ConnectionHint}");
        try
        {
            while (!_gameOver)
            {
                if (Console.KeyAvailable)
                {
                    var keyInfo = Console.ReadKey(intercept: true);
                    if (keyInfo.Key is ConsoleKey.PageUp or ConsoleKey.PageDown)
                    {
                        _renderer.ScrollMessageLog(keyInfo.Key == ConsoleKey.PageUp);
                        continue;
                    }
                    if (GameInput.IsSettingsShortcut(keyInfo))
                    {
                        SettingsScreen.Show(_musicSettings, _backgroundMusic.ApplySettings);
                        _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
                        _renderer.SetCharacterSheetFocused(_characterSheetFocused);
                        continue;
                    }
                    if (_activeBattleState is not null)
                    {
                        HandleLocalBattleInput(keyInfo);
                        continue;
                    }
                    if (IsHelpShortcut(keyInfo))
                    {
                        ShowInGameHelp();
                        continue;
                    }
                    if (IsSaveGameShortcut(keyInfo))
                    {
                        SaveGame();
                        continue;
                    }
                    if (keyInfo.Key == ConsoleKey.Q)
                    {
                        ShowQuestJournal();
                        continue;
                    }
                    if (_renderer.IsSpellInfoPageOpen)
                    {
                        if (keyInfo.Key == ConsoleKey.Escape) _renderer.CloseSpellInfoPage();
                        else if (keyInfo.Key == ConsoleKey.UpArrow) _renderer.MoveSpellInfoSelection(-1);
                        else if (keyInfo.Key == ConsoleKey.DownArrow) _renderer.MoveSpellInfoSelection(1);
                        else if (TryGetQuickSpellIndex(keyInfo, out var spellSlot)) AssignSelectedSpellQuickSlot(spellSlot);
                        else if (keyInfo.Key == ConsoleKey.Enter) CastSelectedSpellInfo();
                        continue;
                    }
                    if (keyInfo.Key == ConsoleKey.V)
                    {
                        BeginExplorationSpellCasting();
                        continue;
                    }
                    if (TryGetQuickSpellIndex(keyInfo, out var quickSpellSlot))
                    {
                        var quickSpell = SelectedCharacter.QuickSpells[quickSpellSlot];
                        if (quickSpell is null)
                            _renderer.DrawInventoryMessage("Ez a varázslat-gyorshely üres.", ConsoleColor.DarkYellow);
                        else
                            BeginExplorationSpellCasting(quickSpell);
                        continue;
                    }
                    if (GameInputBindings.IsCharacterSheetToggle(keyInfo.Key))
                    {
                        if (_characterSheetFocused) CancelHeldInventoryItem();
                        _characterSheetFocused = !_characterSheetFocused;
                        _renderer.SetCharacterSheetFocused(_characterSheetFocused);
                        continue;
                    }
                    if (_characterSheetFocused)
                    {
                        if (keyInfo.Key == ConsoleKey.Escape)
                        {
                            if (ConfirmReturnToMainMenu()) { CancelHeldInventoryItem(); return; }
                            continue;
                        }
                        switch (GameInputBindings.InventoryAction(keyInfo.Key))
                        {
                            case InventoryInputAction.MoveUp: _renderer.MoveCharacterSheetSelection(-1); break;
                            case InventoryInputAction.MoveDown: _renderer.MoveCharacterSheetSelection(1); break;
                            case InventoryInputAction.Drop: DropSelectedInventoryItem(); break;
                            case InventoryInputAction.Inspect: InspectSelectedInventoryItem(); break;
                            case InventoryInputAction.Use: UseSelectedInventoryItem(); break;
                            case InventoryInputAction.MoveItem: GrabOrPlaceInventoryItem(); break;
                            case InventoryInputAction.SplitStack: SplitSelectedInventoryStack(); break;
                            case InventoryInputAction.DistributeStack: DistributeSelectedInventoryStack(); break;
                            default:
                                if (keyInfo.Key == ConsoleKey.LeftArrow) _renderer.MoveDisplayedPartyMember(-1);
                                else if (keyInfo.Key == ConsoleKey.RightArrow) _renderer.MoveDisplayedPartyMember(1);
                                else if (keyInfo.Key == ConsoleKey.Delete) DismissSelectedPartyMember();
                                break;
                        }
                        continue;
                    }
#if DEBUG
                    if (IsRevealMapShortcut(keyInfo))
                    {
                        var isMapRevealed = _fogOfWar.ToggleDeveloperReveal();
                        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
                        _renderer.DrawDeveloperMessage(isMapRevealed
                            ? "Fejlesztői mód: teljes térkép felfedve."
                            : "Fejlesztői mód: köd visszaállítva.");
                        continue;
                    }
                    if (IsNewMazeShortcut(keyInfo))
                    {
                        StartNewMaze();
                        continue;
                    }
                    if (IsTeleportToExitShortcut(keyInfo))
                    {
                        TeleportLeaderNearExit();
                        _player.Character.AddGold(1000);
                        continue;
                    }
                    if (IsTeleportToNextUniqueNpcShortcut(keyInfo))
                    {
                        TeleportLeaderToNextUniqueNpc();
                        continue;
                    }
                    if (IsLevelUpShortcut(keyInfo))
                    {
                        TriggerDeveloperLevelUp();
                        continue;
                    }
                    if (IsFillPartySetYShortcut(keyInfo))
                    {
                        FillPartyForDevelopment([CharacterClassIds.Harcos, CharacterClassIds.Mágus, CharacterClassIds.Lovag], "Y");
                        continue;
                    }
                    if (IsFillPartySetXShortcut(keyInfo))
                    {
                        FillPartyForDevelopment([CharacterClassIds.Barbár, CharacterClassIds.Tolvaj, CharacterClassIds.Pap], "X");
                        continue;
                    }
                    if (IsAddLevelOnePartyMemberShortcut(keyInfo))
                    {
                        AddLevelOnePartyMemberForDevelopment();
                        continue;
                    }
                    if (IsDeveloperPhasingShortcut(keyInfo))
                    {
                        ToggleDeveloperPhasing();
                        continue;
                    }
#endif

                    var key = keyInfo.Key;
                    if (key == ConsoleKey.Escape)
                    {
                        if (ConfirmReturnToMainMenu()) return;
                        continue;
                    }
                    SubmitLocalExplorationCommand(key);
                }

                ProcessSessionCommands();
                ContinueDisconnectedRemoteBattleAsNpc();

                if (_helpPausePlayers.Count > 0)
                {
                    if (coopHost?.ShouldPublish(DateTime.UtcNow) == true)
                        coopHost.TryPublish(CreateSessionSnapshot());
                    Thread.Sleep(20);
                    continue;
                }

                if (!_battleStarted) MoveEnemies();

                if (!_battleStarted) MovePartyMembers();

                if (!_battleStarted && DateTime.UtcNow >= _nextNeedsDrain)
                {
                    DrainNeeds();
                    _nextNeedsDrain = DateTime.UtcNow + TimeSpan.FromMinutes(1);
                }

                if (!_battleStarted && DateTime.UtcNow >= _nextNpcSelfCareCheck)
                {
                    ProcessNpcSelfCare(DateTime.UtcNow);
                    _nextNpcSelfCareCheck = DateTime.UtcNow + TimeSpan.FromSeconds(1);
                }

                if (coopHost?.ShouldPublish(DateTime.UtcNow) == true)
                    coopHost.TryPublish(CreateSessionSnapshot());

                Thread.Sleep(20);
            }
        }
        finally
        {
            _backgroundMusic.Dispose();
            if (_activeCoopHost is not null)
            {
                PublishRemoteCharacterStates(CharacterSyncReason.SessionEnded);
                _activeCoopHost.TryPublish(CreateSessionSnapshot());
            }
            _activeCoopHost = null;
            Console.CursorVisible = true;
            try
            {
                Console.SetCursorPosition(0, Math.Min(ConsoleRenderer.ScreenRowCount - 1,
                    Math.Max(0, Console.BufferHeight - 1)));
            }
            catch (IOException)
            {
            }
        }
    }

    private void CompleteCampaign()
    {
        if (_collectedBossKeyIds.Count < MonsterIds.Bosses.Count)
        {
            _renderer.DrawInventoryMessage(
                $"A Káoszrubin körül még zárva kering néhány aranylakat. Kulcsok: {_collectedBossKeyIds.Count}/{MonsterIds.Bosses.Count}.",
                ConsoleColor.Yellow);
            return;
        }

        PlaySessionSound(SoundEffect.LevelComplete);
        PlaySessionSound(SoundEffect.Victory);
        ShowSynchronizedNarrative(NarrativeKind.CampaignFinale, "GRATULÁLUNK, KULCSHORDOZÓK!",
            "XV. fejezet — A csillagok választottai", CreateCampaignFinale());
        _gameOver = true;
        _session.SetPhase(GameSessionPhase.GameOver);
        _activeCoopHost?.TryPublish(CreateSessionSnapshot());
    }

    private IReadOnlyList<string> CreateCampaignFinale()
    {
        var paragraphs = new List<string>
        {
            "A Káoszrubin a rejtekhely legutolsó termében lebeg. Belsejében tűz, víz, föld és szél kergeti egymást, mintha a négy ősi elementálmágus vitája még mindig nem ért volna véget. Amikor megérintitek, a kő egyetlen szívdobbanásnyi időre elnémul — aztán bíbor fénye elnyeli a labirintust.",
            "Nem zuhantok, mégis világok suhannak el mellettetek. A káosz huszonegy megtört törvénye egyetlen villanásba roskad, majd márvány érinti a lábatokat. Saját világotokban álltok, Aurelios Máguskirály tróntermében, a Káoszrubinnal együtt.",
            "A Csillagszeműek teljes köre vár benneteket az aranykupola alatt. Már órákkal korábban meggyújtották a tizenkét csillaglámpást: megérezték, hogy közeledik a kiválasztott. Vhar-Zul árnyéka visszahúzódik az ólomüveg ablakokról, Aurelios pedig leszáll trónjáról, és király létére fejet hajt előttetek.",
            "A királyi krónikás felnyitja az üresen hagyott aranylapokat. Nemcsak a Káoszrubin visszatérését jegyzi fel, hanem mindazok nevét is, akik élve járták végig az utat:"
        };

        paragraphs.AddRange(CharacterRoster.Party.Members.Where(character => character.IsAlive)
            .Select(CreateSurvivorTribute));
        paragraphs.Add(
            $"Aurelios végül {SelectedCharacter.Name} kezére teszi a kezét. „A csillagok kiválasztottak benneteket, de nem a jóslat győzött helyettetek. Ti tettétek valóra. Mától nem alattvalóimként, hanem a birodalom megmentőiként álltok előttem.”");
        paragraphs.Add(
            "A tizenkét csillaglámpás egyszerre lobban fel, a tróntermet pedig harangzúgás és ujjongás tölti be. A Káoszrubin hazatért, Vhar-Zul terve meghiúsult, és a túlélők neve örökre felkerült az Aranykor új krónikájába. Gratulálunk — végigjártátok a Káoszlabirintust, és megnyertétek a játékot!");
        return paragraphs;
    }

    private static string CreateSurvivorTribute(LiveCharacter character) => character.CharacterClass.Id switch
    {
        CharacterClassIds.Harcos =>
            $"{character.Name}, a harcos erős keze sosem hagyta cserben társait. Ellenfelei rettegtek fegyverének súlyától, barátai pedig tudták, hogy mellette a legvadabb roham is megtörik.",
        CharacterClassIds.Barbár =>
            $"{character.Name}, a barbár fékezhetetlen bátorsága utat tört ott is, ahol más már csak a biztos halált látta. Haragja viharként söpört végig a szörnyeken, de társait mindvégig hűséges szívvel oltalmazta.",
        CharacterClassIds.Lovag =>
            $"{character.Name}, a lovag pajzsa élő várfalként állt a csapat előtt. Becsülete a legsötétebb síkokon sem homályosult el, és esküjét még a Káosz sem tudta megtörni.",
        CharacterClassIds.Tolvaj =>
            $"{character.Name}, a tolvaj ott talált ösvényt, ahol más csak zárakat, csapdákat és árnyakat látott. Éles szeme és gyors keze számtalanszor mentette meg a csapatot, gyakran még azelőtt, hogy társai észrevették volna a veszélyt.",
        CharacterClassIds.Pap =>
            $"{character.Name}, a pap hite fényt gyújtott a holtak és démonok birodalmában. Imái visszahívták társait a kétségbeesés pereméről, szent erejétől pedig még a sír nyughatatlan urai is meghátráltak.",
        CharacterClassIds.Mágus =>
            $"{character.Name}, a mágus tudása megszelídítette a labirintus vad erőit. Varázslatai csillagfényként hasították fel a sötétséget, és elméje olyan titkokat fejtett meg, amelyeket évszázadok óta senki sem mert megérinteni.",
        _ =>
            $"{character.Name}, a {character.CharacterClass.Name.ToLowerInvariant()} rendíthetetlen társként járta végig a Káoszlabirintust; neve méltán került a birodalom legnagyobb hősei közé."
    };

    private void StartNewMaze(bool showLevelImage = true)
    {
        _session.SetPhase(GameSessionPhase.Exploration);
        _session.SynchronizeParty();
        _hasRestedThisLevel = false;
        _spottedEnemyIds.Clear();
        foreach (var character in CharacterRoster.Party.Members)
        {
            character.ResetLevelResurrection();
            character.ResetLevelRelentless();
        }
        var configuration = MazeLevelConfigurations.Get(_mazeLevel);
        ResolvedEnemyEncounter ResolveEncounter(EnemyEncounterConfiguration encounter) => new(
            encounter.GroupCount,
            encounter.Members.Select(member => new ResolvedEnemyGroupMember(
                _gameData.GetEnemy(member.EnemyId), member.Count, member.Role)).ToList(),
            encounter.MovementProfile);
        _generator = new MazeGenerator(configuration.CreateGenerationSettings(_random),
            configuration.RoomEncounters.Select(ResolveEncounter).ToList(),
            configuration.CorridorEncounters.Select(ResolveEncounter).ToList());
        _maze = _generator.Create(MazeWidth, MazeHeight);
        _player = new Player(_maze.Entrance, SelectedCharacter);
        _leaderTrail.Clear();
        _leaderTrail.Add(_player.Position);
        _nextPartyMoves.Clear();
        PlacePartyMembersNear(_player.Position);
        PlaceTraps(configuration);
        PlaceFirstSinglePlayerCompanion();
        PlaceConfiguredWorldNpcs();
        _fogOfWar = new FogOfWar(_maze.Width, _maze.Height, CharacterClassRules.BaseVisionRange);
        RevealFor(SelectedCharacter, _player.Position);
        foreach (var member in _maze.PartyMembers) RevealFor(member.Character, member.Position);
        _battleStarted = false;
        _gameOver = false;
        InitializeEnemyMoveSchedule(DateTime.UtcNow);
        _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
        if (configuration.VisionModifier < 0)
        {
            var darknessMessage = $"🌑 Extra sötét pálya: minden karakter látótávja {configuration.VisionModifier}.";
            _renderer.DrawInventoryMessage(darknessMessage, ConsoleColor.DarkRed);
            RecordSessionActivity(SessionActivityKind.System, darknessMessage, ConsoleColor.DarkRed);
        }
        CheckBossDiscovery(_maze.Enemies.Where(enemy => _fogOfWar.IsRevealed(enemy.Position)));
        PlaySessionSound(SoundEffect.LevelStart);
        _backgroundMusic.SynchronizeMazeLevel(_mazeLevel, _fogOfWar.IsRevealed(_maze.Exit));
        _activeInnDeparture = null;
        if (showLevelImage) ShowLevelImage();
        LogMazeAccessibilityCheck();
    }

    private void ShowLevelImage()
    {
        var fileName = ImageViewer.FileNameForLevel(_maze.LevelName);
        var path = Path.Combine(AppContext.BaseDirectory, "Kepek", fileName);
        if (_activeCoopHost is not null)
        {
            ShowSynchronizedLevelImage(fileName, path);
            return;
        }
        if (!ImageViewer.Show(path))
            _renderer.DrawDeveloperMessage($"Pályakép még nem található: {fileName}");
    }

    private void LogMazeAccessibilityCheck()
    {
        var report = _maze.CheckFullAccessibility();
        _renderer.DrawDeveloperMessage(report.IsFullyAccessible
            ? $"Bejárhatósági önellenőrzés: OK, mind a(z) {report.TotalWalkableCount} padló-/ajtócella elérhető."
            : $"Bejárhatósági önellenőrzés: HIBA, {report.UnreachablePositions.Count}/{report.TotalWalkableCount} cella nem érhető el " +
              $"(pl. {report.UnreachablePositions[0].X},{report.UnreachablePositions[0].Y}).");
    }

    private void PlaceTraps(MazeLevelConfiguration configuration)
    {
        var definitions = configuration.TrapIds.Select(_gameData.GetTrap)
            .Where(trap => trap.MinimumLevel <= _mazeLevel).ToArray();
        if (definitions.Length == 0) return;
        var desiredCount = configuration.TrapCount.Roll(_random);
        var candidates = new List<Position>();
        for (var y = 0; y < _maze.Height; y++)
        for (var x = 0; x < _maze.Width; x++)
        {
            var position = new Position(x, y);
            if (!_maze.IsWalkable(position) || position == _maze.Entrance || position == _maze.Exit ||
                _maze.StartingRoom?.Contains(position) == true || _maze.GetObjectAt(position) is not null ||
                Manhattan(position, _maze.Entrance) < 6 ||
                _maze.Doors.Any(door => Manhattan(door.Position, position) <= 1)) continue;
            candidates.Add(position);
        }
        var placed = new List<Position>();
        foreach (var position in candidates.OrderBy(_ => _random.Next()))
        {
            if (placed.Any(existing => Manhattan(existing, position) < 3)) continue;
            _maze.AddTrap(new MazeTrap(position, definitions[_random.Next(definitions.Length)]));
            placed.Add(position);
            if (placed.Count >= desiredCount) break;
        }
    }

    private void PlaceFirstSinglePlayerCompanion()
    {
        if (_activeCoopHost is not null || _mazeLevel != 1 || CharacterRoster.Party.Members.Count != 1 ||
            _maze.WorldNpcs.Count != 0) return;

        var preferredClassIds = SelectedCharacter.CharacterClass.Id switch
        {
            CharacterClassIds.Harcos or CharacterClassIds.Barbár => new[] { CharacterClassIds.Pap, CharacterClassIds.Tolvaj },
            CharacterClassIds.Lovag => new[] { CharacterClassIds.Mágus, CharacterClassIds.Tolvaj },
            CharacterClassIds.Tolvaj => new[] { CharacterClassIds.Harcos, CharacterClassIds.Lovag },
            CharacterClassIds.Pap => new[] { CharacterClassIds.Harcos, CharacterClassIds.Barbár },
            CharacterClassIds.Mágus => new[] { CharacterClassIds.Lovag, CharacterClassIds.Harcos },
            _ => new[] { CharacterClassIds.Harcos }
        };
        var characterClass = _gameData.GetCharacterClass(preferredClassIds[_random.Next(preferredClassIds.Length)]);
        var recruit = new RandomCharacterGenerator(_gameData, _random).CreateLevelOne(characterClass,
            CharacterRoster.Characters.Select(character => character.Name).ToArray());

        var candidates = new List<Position>();
        for (var y = 0; y < _maze.Height; y++)
        for (var x = 0; x < _maze.Width; x++)
        {
            var position = new Position(x, y);
            var distance = Manhattan(position, _maze.Entrance);
            if (!_maze.IsWalkable(position) || position == _maze.Exit || _maze.GetObjectAt(position) is not null ||
                _maze.GetTrapAt(position) is not null || distance < 6 || distance > 14) continue;
            candidates.Add(position);
        }
        if (candidates.Count == 0) return;

        CharacterRoster.Add(recruit);
        var spawnPosition = candidates[_random.Next(candidates.Count)];
        _maze.AddWorldNpc(new WorldNpc(spawnPosition, "NPC-FIRST-COMPANION", recruit, NpcDisposition.Friendly,
            recruitable: true, isQuestNpc: false,
            "Elvesztem ebben az átkozott labirintusban. Együtt talán kijutunk — veletek tartok, fizetség nélkül.",
            friendliness: 10, behavior: NpcWorldBehavior.Friendly));
    }

    private void PlaceConfiguredWorldNpcs()
    {
        foreach (var encounter in _gameData.NpcEncounters.Where(value => value.MazeLevel == _mazeLevel))
        {
            var definition = _gameData.GetNpc(encounter.NpcId);
            if (definition.Unique && CharacterRoster.Characters.Any(character =>
                    string.Equals(character.Name, definition.Name, StringComparison.OrdinalIgnoreCase))) continue;
            var candidates = new List<Position>();
            for (var y = 0; y < _maze.Height; y++)
            for (var x = 0; x < _maze.Width; x++)
            {
                var position = new Position(x, y);
                var distance = Manhattan(position, _maze.Entrance);
                if (!_maze.IsWalkable(position) || position == _maze.Exit || _maze.GetObjectAt(position) is not null ||
                    _maze.GetTrapAt(position) is not null || _maze.GetDoorAt(position) is not null ||
                    distance < encounter.MinimumDistance || distance > encounter.MaximumDistance) continue;
                candidates.Add(position);
            }
            if (candidates.Count == 0) continue;

            var generator = new RandomCharacterGenerator(_gameData, _random);
            var recruit = definition.Unique && definition.RaceId is { } raceId
                ? generator.CreateUniqueRecruit(definition.Name, _gameData.GetRace(raceId),
                    _gameData.GetCharacterClass(definition.CharacterClassId), SelectedCharacter.Level)
                : generator.CreateRecruit(_gameData.GetCharacterClass(definition.CharacterClassId),
                    SelectedCharacter.Level, CharacterRoster.Characters.Select(character => character.Name).ToArray());
            CharacterRoster.Add(recruit);
            var friendliness = definition.Unique ? 4 : RollNpcFriendliness(definition);
            var dialogue = _gameData.GetNpcDialogues(definition.Id)
                .Where(value => friendliness >= value.MinimumFriendliness && friendliness <= value.MaximumFriendliness)
                .OrderBy(_ => _random.Next()).FirstOrDefault()?.Text ?? "Az idegen óvatosan végigmér benneteket.";
            var questIds = _gameData.GetNpcQuests(definition.Id).Select(quest => quest.Id).ToArray();
            _maze.AddWorldNpc(new WorldNpc(candidates[_random.Next(candidates.Count)], definition.Id, recruit,
                definition.Disposition, definition.Recruitable, questIds.Length > 0, dialogue,
                friendliness: friendliness, behavior: definition.Behavior, questIds: questIds));
        }
    }

    private int RollNpcFriendliness(NpcDefinition definition)
    {
        var baseValue = definition.Disposition switch
        {
            NpcDisposition.Friendly => _random.Next(7, 10),
            NpcDisposition.Neutral => _random.Next(3, 8),
            _ => _random.Next(0, 4)
        };
        var modifier = definition.Behavior switch
        {
            NpcWorldBehavior.Friendly => 1,
            NpcWorldBehavior.Guarded => -1,
            NpcWorldBehavior.Aggressive => -2,
            _ => 0
        };
        return Math.Clamp(baseValue + modifier, 0, 10);
    }

    /// <summary>A rejtett csapda egyszer kap passzív észlelési próbát. A felfedezett aktív csapda
    /// megállítja a mozgást, amíg K-val hatástalanítják.</summary>
    private bool CanEnterTrap(LiveCharacter character, Position destination)
    {
        var trap = _maze.GetTrapAt(destination);
        if (trap is null || !trap.IsActive) return true;
        if (trap.State == TrapState.Detected)
        {
            ShowTrapMessage($"⚠️ {trap.Definition.Name} zárja el az utat. A mellette álló karakter K-val megpróbálhatja hatástalanítani.",
                ConsoleColor.Yellow, character);
            return false;
        }
        if (!trap.DetectionAttempted)
        {
            trap.MarkDetectionAttempted();
            var chance = TrapDetectionChance(character, trap.Definition);
            if (_random.Next(100) < chance)
            {
                trap.Detect();
                _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
                RewardTrapSuccess(character, trap.Definition.DetectionExperience,
                    $"👁️ {character.Name} időben felfedezte: {trap.Definition.Name} ({chance}% esély).",
                    ConsoleColor.Cyan);
                return false;
            }
        }
        return true;
    }

    private int TrapDetectionChance(LiveCharacter character, TrapDefinition definition) => Math.Clamp(
        35 + (character.EffectiveAbilities.Intelligence + character.EffectiveAbilities.Dexterity) * 3 -
        definition.DetectionDifficulty * 5 +
        (CharacterClassRules.IsThief(character.CharacterClass.Id) ? 30 : 0), 15, 95);

    private int TrapDisarmChance(LiveCharacter character, TrapDefinition definition) => Math.Clamp(
        30 + character.EffectiveAbilities.Dexterity * 5 - definition.DisarmDifficulty * 6 +
        (CharacterClassRules.IsThief(character.CharacterClass.Id) ? 30 : 0), 10, 95);

    private bool TryDisarmAdjacentTrap(LiveCharacter character, Position position)
    {
        var traps = Directions.Select(direction => _maze.GetTrapAt(position + direction))
            .Where(trap => trap is { State: TrapState.Detected }).Cast<MazeTrap>().ToArray();
        if (traps.Length == 0) return false;
        var trap = traps[0];
        var chance = TrapDisarmChance(character, trap.Definition);
        if (_random.Next(100) < chance)
        {
            trap.Disarm();
            RegisterNpcQuestProgress(NpcQuestType.Disarm, "ANY");
            _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
            RewardTrapSuccess(character, trap.Definition.DisarmExperience,
                $"🧰 {character.Name} hatástalanította: {trap.Definition.Name} ({chance}% esély).",
                ConsoleColor.Green);
            return true;
        }
        trap.RecordFailedDisarm();
        ShowTrapMessage($"⚠️ {character.Name} nem tudta hatástalanítani: {trap.Definition.Name} ({chance}% esély)." +
                        (trap.FailedDisarmAttempts == 1 ? " A csapda még nem sült el." : string.Empty),
            ConsoleColor.DarkYellow, character);
        if (trap.FailedDisarmAttempts >= 2 && _random.Next(2) == 0) ApplyTrap(character, trap);
        return true;
    }

    private void TriggerTrapAt(LiveCharacter character, Position position)
    {
        if (_maze.GetTrapAt(position) is { IsActive: true } trap) ApplyTrap(character, trap);
    }

    private void ApplyTrap(LiveCharacter character, MazeTrap trap)
    {
        trap.Trigger();
        var scaledDamage = trap.Definition.MaximumDamage == 0 ? 0 :
            _random.Next(trap.Definition.MinimumDamage, trap.Definition.MaximumDamage + 1) + (_mazeLevel - 1) / 3;
        var maximumAllowed = Math.Max(1, character.MaximumVitality / (_mazeLevel <= 4 ? 7 : 4));
        var damage = Math.Min(Math.Min(scaledDamage, maximumAllowed), Math.Max(0, character.CurrentVitality - 1));
        character.ReceiveDamage(damage);
        var extra = string.Empty;
        if (trap.Definition.Effect == TrapEffect.Poison && character.IsAlive &&
            _random.Next(100) < trap.Definition.StatusChancePercent)
        {
            character.AddStatus(_gameData.GetStatus(CharacterStatusIds.Poisoned));
            extra = " Megmérgeződött.";
        }
        else if (trap.Definition.Effect == TrapEffect.Alert)
        {
            foreach (var enemy in _maze.Enemies.Where(enemy => Manhattan(enemy.Position, trap.Position) <= 12))
                enemy.ConfigureMovement(enemy.MovementProfile, enemy.PatrolDirection, EnemyPursuitState.Pursuing);
            extra = " A közeli szörnyek felfigyeltek a zajra.";
        }
        else if (trap.Definition.Effect == TrapEffect.Darkness && character.IsAlive)
        {
            character.ApplySpellEffect(new ActiveSpellEffect(trap.Definition.Id,
                ActiveSpellEffectType.VisionBonus, -2, 6));
            extra = " A koromfelhő 6 akcióra 2-vel csökkentette a látótávját.";
        }
        _renderer.RefreshCharacterSheet(character);
        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
        var damageText = damage > 0 ? $" {character.Name} {damage} sebzést szenvedett." : string.Empty;
        ShowTrapMessage($"💥 Elsült: {trap.Definition.Name}.{damageText}{extra}",
            ConsoleColor.Red, character);
    }

    private void ShowTrapMessage(string message, ConsoleColor color, LiveCharacter character)
    {
        _renderer.DrawInventoryMessage(message, color);
        RecordSessionActivity(SessionActivityKind.System, message, color, [character.Id]);
    }

    private void RewardTrapSuccess(LiveCharacter character, int experience, string message, ConsoleColor color)
    {
        var award = AwardExperience(character, experience);
        var levelText = award.Result.LeveledUp
            ? $" Szint: {award.Result.PreviousLevel}→{award.Result.CurrentLevel}."
            : string.Empty;
        ShowTrapMessage($"{message} +{award.Result.GainedExperience} XP.{levelText}", color, character);
        _renderer.RefreshCharacterSheet(character);
        if (!award.Result.LeveledUp || !character.IsAlive) return;
        ResolvePerkOffers(character, award.Result);
        _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
    }

    private void ShowInGameHelp()
    {
        var synchronizeCoopPause = _activeCoopHost is not null;
        if (synchronizeCoopPause)
        {
            SetHelpVisibility(_session.HostPlayerId, SelectedCharacter.Id, true);
            _activeCoopHost!.TryPublish(CreateSessionSnapshot());
        }
        try
        {
            MainMenu.ShowHelp();
        }
        finally
        {
            if (synchronizeCoopPause)
            {
                SetHelpVisibility(_session.HostPlayerId, SelectedCharacter.Id, false);
                _activeCoopHost!.TryPublish(CreateSessionSnapshot());
            }
        }
        _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
        _renderer.SetCharacterSheetFocused(_characterSheetFocused);
    }

    private void AssignSelectedSpellQuickSlot(int slotIndex)
    {
        var character = _renderer.SpellInfoCharacter;
        var spell = _renderer.GetSelectedSpellInfo();
        if (character is null || spell is null) return;
        if (!character.AssignQuickSpell(slotIndex, spell))
        {
            _renderer.DrawInventoryMessage("Csak memorizált varázslat tehető gyorshelyre.", ConsoleColor.Red);
            return;
        }
        _renderer.RefreshSpellInfoPage();
        _renderer.DrawInventoryMessage($"{spell.Name} hozzárendelve: F{slotIndex + 1}.", ConsoleColor.Cyan);
    }

    private void CastSelectedSpellInfo()
    {
        var character = _renderer.SpellInfoCharacter;
        var spell = _renderer.GetSelectedSpellInfo();
        if (character != SelectedCharacter || spell is null ||
            character.MemorizedSpells.All(candidate => !string.Equals(candidate.Id, spell.Id, StringComparison.OrdinalIgnoreCase)))
        {
            _renderer.DrawInventoryMessage("Csak a partivezér memorizált varázslata süthető el.", ConsoleColor.DarkYellow);
            return;
        }
        _renderer.CloseSpellInfoPage();
        BeginExplorationSpellCasting(spell);
    }

    private void BeginExplorationSpellCasting(SpellDefinition? quickSpell = null)
    {
        var spell = quickSpell;
        MagicItemDefinition? castingItem = null;
        int? castingItemSlotIndex = null;
        var caster = SelectedCharacter;
        if (spell is null)
        {
            var casters = GetSpellcastingPartyMembers();
            if (casters.Count == 0)
            {
                _renderer.DrawInventoryMessage("Senki nem tud varázsolni a partiban.", ConsoleColor.DarkYellow);
                return;
            }
            var startIndex = Math.Max(0, casters.IndexOf(SelectedCharacter));
            var selection = _renderer.DrawSpellCastingScreen(casters, startIndex, inCombat: false, _maze, _fogOfWar,
                GetCasterPosition, ShowInGameHelp);
            _renderer.RestoreSpellCastingOverlay();
            if (selection is null) return;
            spell = selection.Spell;
            caster = selection.Caster;
            castingItem = selection.CastingItem;
            castingItemSlotIndex = selection.CastingItemSlotIndex;
        }
        var result = TryCastSpell(caster, GetCasterPosition(caster), spell, inCombat: false,
            currentEnemy: null, castingItem: castingItem, castingItemSlotIndex: castingItemSlotIndex);
        if (result is not null)
        {
            _renderer.RefreshBattleStatusRows();
            _renderer.DrawInventoryMessage(result.Message, result.Kind == BattleLogKind.Information ? ConsoleColor.Red : ConsoleColor.Magenta);
        }
    }

    private List<LiveCharacter> GetSpellcastingPartyMembers() => CharacterRoster.Party.Members
        .Where(character => character.IsAlive &&
            (character.IsSpellcaster && character.CanCastSpells || EquippedCastingItems(character).Any()))
        .ToList();

    private IEnumerable<MagicItemDefinition> EquippedCastingItems(LiveCharacter character) =>
        character.MagicItems.Select((item, index) => (Item: item, Index: index))
            .Where(entry => entry.Item?.Kind is MagicItemKind.Scroll or MagicItemKind.Wand &&
                entry.Item.SpellId is not null && character.MagicItemCharges[entry.Index] > 0)
            .Where(entry => SpellcastingRules.CanUseCastingItem(character, entry.Item!, _gameData.GetSpell(entry.Item!.SpellId!)))
            .Select(entry => entry.Item!);

    private Position GetCasterPosition(LiveCharacter character) => character == SelectedCharacter
        ? _player.Position
        : _maze.PartyMembers.First(member => member.Character == character).Position;

    private void SaveGame()
    {
        CancelHeldInventoryItem();
        try
        {
            var path = _gameSaveService.Save(CreateGameSaveData(), CharacterRoster);
            _renderer.DrawDeveloperMessage($"Játék elmentve: {Path.GetFileName(path)}");
            if (_activeCoopHost is not null)
            {
                PublishRemoteCharacterStates(CharacterSyncReason.GameSaved);
                _activeCoopHost.TryPublish(CreateSessionSnapshot());
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _renderer.DrawDeveloperMessage($"A mentés sikertelen: {exception.Message}");
        }
    }

    private GameSaveData CreateGameSaveData()
    {
        var state = _gameStateMapper.Create(_mazeLevel, _maze, _player, _fogOfWar, _leaderFacing,
            _leaderTrail, _partyHoldingPosition, _partyRegrouping, _partyAttackMode, _hasRestedThisLevel, _partyScatterUntil,
            _nextNeedsDrain, _nextEnemyMoves, _collectedBossKeyIds, _seenBossIds);
        state.QuestJournal = _questJournal.Values.Select(entry => new QuestJournalSaveData(entry.QuestId,
            entry.Status, entry.Progress, entry.ExperienceReward)).ToList();
        state.IsCoopGame = _activeCoopHost is not null;
        state.RemoteCharacterIds = _session.CharacterControls
            .Where(control => control.AssignedPlayerId is not null &&
                              control.AssignedPlayerId != _session.HostPlayerId)
            .Select(control => control.CharacterId.Value).ToList();
        return state;
    }

    private void PublishRemoteCharacterStates(CharacterSyncReason reason)
    {
        if (_activeCoopHost is null) return;
        foreach (var control in _session.CharacterControls.Where(control =>
                     control.AssignedPlayerId is not null && control.AssignedPlayerId != _session.HostPlayerId))
        {
            var character = CharacterRoster.Party.Members.FirstOrDefault(member => member.Id == control.CharacterId);
            if (character is not null)
                _activeCoopHost.TryPublishCharacterState(character.Id,
                    _gameSaveService.SerializeCharacter(character), reason);
        }
    }

    private void RestoreGame(GameSaveData state)
    {
        var restored = _gameStateMapper.Restore(state);
        _mazeLevel = restored.MazeLevel;
        _collectedBossKeyIds.Clear();
        _collectedBossKeyIds.UnionWith(state.CollectedBossKeyIds ?? []);
        _seenBossIds.Clear();
        _seenBossIds.UnionWith(state.SeenBossIds ?? []);
        _questJournal.Clear();
        foreach (var saved in state.QuestJournal ?? [])
            if (_gameData.NpcQuests.FirstOrDefault(quest => string.Equals(quest.Id, saved.QuestId,
                    StringComparison.OrdinalIgnoreCase)) is { } quest)
                _questJournal[quest.Id] = CreateQuestJournalEntry(quest, saved.Status, saved.Progress,
                    saved.ExperienceReward);
        _renderer.SetGoldenKeyCount(_collectedBossKeyIds.Count);
        _maze = restored.Maze;
        if (_questJournal.Count == 0)
            foreach (var npc in _maze.WorldNpcs.Concat(_maze.PartyMembers
                         .Where(member => member.TemporaryFollower is not null)
                         .Select(member => member.TemporaryFollower!)))
            foreach (var progress in npc.Quests.Where(progress => progress.State != NpcQuestState.Offered))
                if (_gameData.NpcQuests.FirstOrDefault(quest => string.Equals(quest.Id, progress.QuestId,
                        StringComparison.OrdinalIgnoreCase)) is { } quest)
                    SynchronizeQuestJournal(npc, quest);
        _player = restored.Player;
        _fogOfWar = restored.FogOfWar;
        _leaderFacing = restored.LeaderFacing;
        _leaderTrail.Clear();
        _leaderTrail.AddRange(restored.LeaderTrail);
        _partyHoldingPosition = restored.PartyHoldingPosition;
        _partyRegrouping = restored.PartyRegrouping;
        _partyAttackMode = restored.PartyAttackMode;
        _hasRestedThisLevel = restored.HasRestedThisLevel;
        _partyScatterUntil = restored.PartyScatterUntil;
        _nextNeedsDrain = restored.NextNeedsDrain;
        _nextEnemyMoves.Clear();
        foreach (var enemyMove in restored.NextEnemyMoves) _nextEnemyMoves[enemyMove.Key] = enemyMove.Value;
        _nextPartyMoves.Clear();
        foreach (var member in _maze.PartyMembers) ScheduleNextPartyMove(member, DateTime.UtcNow);
        _battleStarted = false;
        _gameOver = false;
        RevealFor(SelectedCharacter, _player.Position);
        _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
        _renderer.DrawDeveloperMessage($"Mentés betöltve: {state.MainCharacterName}, {_mazeLevel}. pálya.");
        _backgroundMusic.SynchronizeMazeLevel(_mazeLevel, _fogOfWar.IsRevealed(_maze.Exit));
    }

    private void TryRestParty()
    {
        if (_hasRestedThisLevel)
        {
            _renderer.DrawDeveloperMessage("Ezen a pályán már pihentetek egyszer.");
            return;
        }
        var room = _maze.Rooms.FirstOrDefault(candidate => candidate.Contains(_player.Position));
        if (room is null)
        {
            _renderer.DrawDeveloperMessage("Pihenni csak egy szoba belsejében lehet.");
            return;
        }
        var livingParty = CharacterRoster.Party.Members.Where(character => character.IsAlive).ToList();
        var everyoneInside = livingParty.All(character => character == SelectedCharacter
            ? room.Contains(_player.Position)
            : _maze.PartyMembers.Any(avatar => avatar.Character == character && room.Contains(avatar.Position)));
        if (!everyoneInside)
        {
            _renderer.DrawDeveloperMessage("Pihenéshez minden élő partitag ugyanabban a szobában legyen.");
            return;
        }
        if (_maze.Enemies.Any(enemy => room.Contains(enemy.Position)))
        {
            _renderer.DrawDeveloperMessage("Ellenség van a szobában; itt nem lehet pihenni.");
            return;
        }
        var roomDoors = _maze.Doors.Where(door => room.InteriorPositions()
            .Any(position => Manhattan(position, door.Position) == 1)).ToList();
        if (roomDoors.Count == 0 || roomDoors.Any(door => door.State != DoorState.Locked))
        {
            _renderer.DrawDeveloperMessage("Pihenéshez a szoba minden ajtaját kulcsra kell zárni.");
            return;
        }

        var restResults = new List<CharacterRestSnapshot>();
        foreach (var character in livingParty)
        {
            var beforeVitality = character.CurrentVitality;
            var beforeMana = character.CurrentMana;
            character.RestoreVitality(_random.Next(1, 11));
            character.SetCurrentResources(character.CurrentVitality, character.MaximumMana);
            var cured = new List<string>();
            var cureChance = Math.Clamp(30 + character.EffectiveAbilities.Health * 2, 0, 100);
            foreach (var (statusId, name) in new[]
                     {
                         (CharacterStatusIds.Diseased, "betegség"),
                         (CharacterStatusIds.Poisoned, "mérgezés"),
                         (CharacterStatusIds.Bleeding, "vérzés")
                     })
                if (character.HasStatus(statusId) && _random.Next(100) < cureChance && character.RemoveStatus(statusId))
                    cured.Add($"{_gameData.GetStatus(statusId).Icon} {name}");
            character.ConsumeFood(10);
            character.ConsumeWater(10);
            character.SynchronizeNeedStatuses(_gameData.GetStatus(CharacterStatusIds.Hungry),
                _gameData.GetStatus(CharacterStatusIds.Thirsty));
            restResults.Add(new CharacterRestSnapshot(character.Id, character.Name, character.Color,
                character.CurrentVitality - beforeVitality, character.CurrentMana - beforeMana,
                character.CurrentVitality, character.MaximumVitality, character.CurrentMana, character.MaximumMana,
                character.UsesMana, cured));
        }
        _hasRestedThisLevel = true;
        ShowSynchronizedRest(new PartyRestSnapshot(Guid.NewGuid(), false, restResults, []));
        PreparePartySpells();
        foreach (var door in roomDoors) _maze.SetDoorState(door, DoorState.Closed);
        _nextNeedsDrain = DateTime.UtcNow + TimeSpan.FromMinutes(1);
        InitializeEnemyMoveSchedule(DateTime.UtcNow);
        foreach (var member in _maze.PartyMembers) ScheduleNextPartyMove(member, DateTime.UtcNow);
        _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
        PlaySessionSound(SoundEffect.Rest);
    }

    private void PreparePartySpells()
    {
        foreach (var character in CharacterRoster.Party.Members.Where(character => character.IsAlive && character.IsSpellcaster))
        {
            var control = _session.CharacterControls.FirstOrDefault(candidate => candidate.CharacterId == character.Id);
            if (control is { ControllerKind: CharacterControllerKind.RemotePlayer,
                    ConnectionState: PlayerConnectionState.Connected, AssignedPlayerId: not null })
                WaitForRemoteSpellPreparation(character);
            else
                character.SetMemorizedSpells(_renderer.DrawSpellPreparationScreen(character));
        }
    }

    private void WaitForRemoteSpellPreparation(LiveCharacter character)
    {
        var previousPhase = _session.Phase;
        var spellInfo = SpellInfoSnapshotProjector.Create(character);
        _activeSpellPreparation = new SpellPreparationSnapshot(Guid.NewGuid(), character.Id, character.Name,
            character.MemorizationCapacity, spellInfo.KnownSpells,
            character.MemorizedSpells.Select(spell => spell.Id).ToArray());
        _spellPreparationCompleted = false;
        _session.SetPhase(GameSessionPhase.Paused);
        _renderer.DrawInventoryMessage(
            $"⌛ Várakozás {character.Name} varázsmemorizálására... ⌛", ConsoleColor.Yellow);
        PlaySessionSound(SoundEffect.Waiting, [SelectedCharacter.Id]);
        _activeCoopHost?.TryPublish(CreateSessionSnapshot());
        while (!_spellPreparationCompleted)
        {
            ProcessSessionCommands();
            var stillConnected = _session.CharacterControls.Any(control => control.CharacterId == character.Id &&
                control.ControllerKind == CharacterControllerKind.RemotePlayer &&
                control.ConnectionState == PlayerConnectionState.Connected);
            if (!stillConnected) break;
            if (_activeCoopHost?.ShouldPublish(DateTime.UtcNow) == true)
                _activeCoopHost.TryPublish(CreateSessionSnapshot());
            Thread.Sleep(20);
        }
        _activeSpellPreparation = null;
        _spellPreparationCompleted = false;
        _session.SetPhase(previousPhase);
        _activeCoopHost?.TryPublish(CreateSessionSnapshot());
    }

    private void MovePlayer(Direction direction)
    {
        var previousPosition = _player.Position;
        var targetPosition = previousPosition + direction;

        // Az ideiglenes követővel ismét a térképen rálépési kísérlettel lehet beszélni.
        if (_maze.GetObjectAt(targetPosition) is PartyMemberAvatar partyAvatar)
        {
            if (partyAvatar.TemporaryFollower is { } follower) ConverseWithFirstUniqueNpc(follower);
            return;
        }
        if (_maze.GetWorldNpcAt(targetPosition) is { } npc)
        {
            if (!EncounterWorldNpc(npc)) return;
        }
        if (!CanEnterTrap(SelectedCharacter, targetPosition)) return;

        var moved = _player.TryMove(direction, _maze);
        if (!moved)
        {
            if (_developerPhasing && _maze.IsInside(targetPosition))
            {
                // Destroy wall/door and move through
                _maze.RemoveDoor(targetPosition);
                _maze.Carve(targetPosition);
                _player.TeleportTo(targetPosition);
            }
            else
            {
                return;
            }
        }
        SelectedCharacter.RegisterExplorationStep();
        _leaderFacing = direction;
        if (_leaderTrail[^1] != _player.Position) _leaderTrail.Add(_player.Position);
        if (_leaderTrail.Count > 256) _leaderTrail.RemoveRange(0, _leaderTrail.Count - 256);

        var newlyRevealed = RevealFor(SelectedCharacter, _player.Position, advanceEnemyMemory: true);
        var justReachedExit = _player.Position == _maze.Exit && previousPosition != _maze.Exit;
        _renderer.DrawMovement(_maze, _fogOfWar, previousPosition, _player.Position, newlyRevealed, justReachedExit);
        CheckBossDiscoveryAt(newlyRevealed);
        PlayCharacterStepSound(SelectedCharacter);
        CollectTreasureChest(SelectedCharacter, _player.Position, shareLootWithParty: true);
        TriggerTrapAt(SelectedCharacter, _player.Position);
        var enemy = _maze.GetEnemyAt(_player.Position);
        if (enemy is not null) StartBattle(enemy);
    }

    private void MoveRemotePartyMember(MoveCharacterCommand command)
    {
        var member = _maze.PartyMembers.FirstOrDefault(candidate => candidate.Character.Id == command.CharacterId);
        if (member is null || !member.Character.IsAlive) return;
        var previous = member.Position;
        var destination = previous + command.Direction;
        if (_maze.GetEnemyAt(destination) is { } enemy)
        {
            StartBattle(member, enemy);
            return;
        }
        if (!CanEnterTrap(member.Character, destination)) return;
        if (!_maze.TryMovePartyMember(member, destination, _player.Position, allowTreasureChest: true)) return;
        member.Character.RegisterExplorationStep();
        var newlyRevealed = RevealFor(member.Character, member.Position, advanceEnemyMemory: true);
        _renderer.DrawPartyMemberMovement(_maze, _fogOfWar, previous, member.Position, newlyRevealed, _player.Position);
        PlayCharacterStepSound(member.Character);
        CheckBossDiscoveryAt(newlyRevealed);
        CollectTreasureChest(member.Character, member.Position, shareLootWithParty: false);
        TriggerTrapAt(member.Character, member.Position);
    }

    private void CollectTreasureChest(LiveCharacter character, Position position, bool shareLootWithParty)
    {
        var chest = _maze.GetTreasureChestAt(position);
        if (chest is null) return;
        var rules = _gameData.LootRules;
        var jackpotChance = AdjustedSearchChance(character, rules.ChestJackpotChancePercent);
        var jackpot = _random.Next(100) < jackpotChance;
        var rewardMultiplier = jackpot ? rules.ChestJackpotMultiplier : 1;
        if (character.HasPerk(PerkIds.ThiefMasterThief)) rewardMultiplier *= 2;
        var goldAmount = chest.GoldAmount * rewardMultiplier;
        SelectedCharacter.AddGold(goldAmount);
        var masterThiefLoot = RollMasterThiefChestLoot(character);
        _maze.RemoveTreasureChest(chest);
        RegisterNpcQuestProgress(NpcQuestType.OpenChest, "ANY");
        _renderer.RefreshCharacterSheet(SelectedCharacter);
        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
        if (character == SelectedCharacter)
            _renderer.DrawTreasureCollected(goldAmount, jackpot, jackpotChance, rewardMultiplier);

        var message = $"🎁 {character.Name} kinyitotta a kincsesládát: {goldAmount} arany" +
                      (jackpot ? $" (jackpot, {jackpotChance}% esély)" : string.Empty) + ".";
        _renderer.DrawInventoryMessage(message, jackpot ? ConsoleColor.Magenta : ConsoleColor.Yellow);
        RecordSessionActivity(SessionActivityKind.System, message,
            jackpot ? ConsoleColor.Magenta : ConsoleColor.Yellow, [character.Id]);
        PlaySessionSound(jackpot ? SoundEffect.Chest2 : SoundEffect.Chest, [character.Id]);

        if (masterThiefLoot is null) return;
        if (TryStoreSearchedLoot(character, masterThiefLoot, shareLootWithParty, out var owner))
            message = $"🎁 Mestertolvaj: {masterThiefLoot.Name} → {owner} hátizsákja.";
        else
        {
            _maze.DropItem(position, masterThiefLoot);
            message = $"🎁 Mestertolvaj: {masterThiefLoot.Name} a földön maradt, mert a hátizsák tele van.";
        }
        _renderer.DrawInventoryMessage(message, ConsoleColor.Magenta);
        RecordSessionActivity(SessionActivityKind.System, message, ConsoleColor.Magenta, [character.Id]);
    }

    private void SubmitLocalExplorationCommand(ConsoleKey key)
    {
        GameCommand? command = null;
        var commandId = _localCommandId + 1;
        if (TryGetDirection(key, out var direction))
            command = new MoveCharacterCommand(_session.HostPlayerId, commandId, SelectedCharacter.Id, direction);
        else if (GameInputBindings.CharacterAction(key) is { } characterAction)
        {
            Position? targetDoor = null;
            if (characterAction is CharacterAction.OpenDoor or CharacterAction.CloseOrLockDoor)
            {
                var doors = AdjacentDoorPositions(_player.Position);
                if (doors.Count == 1) targetDoor = doors[0];
                else if (doors.Count > 1)
                {
                    targetDoor = SelectDoorTarget(doors, characterAction);
                    if (targetDoor is null) return;
                }
            }
            var useKey = GetLocalThiefKeyChoice(characterAction, targetDoor);
            command = new CharacterActionCommand(_session.HostPlayerId, commandId, SelectedCharacter.Id,
                characterAction, targetDoor, useKey);
        }
        else
        {
            var action = GameInputBindings.LeaderAction(key, _player.Position == _maze.Exit);
            if (action is not null)
                command = new LeaderActionCommand(_session.HostPlayerId, commandId, SelectedCharacter.Id, action.Value);
        }
        if (command is null || !_session.Submit(command)) return;
        _localCommandId = commandId;
    }

    private void ProcessSessionCommands()
    {
        while (_session.TryReadCommand(out var command))
        {
            if (command is SetHelpVisibilityCommand helpVisibility)
            {
                SetHelpVisibility(helpVisibility.SenderId, helpVisibility.CharacterId, helpVisibility.IsOpen);
                continue;
            }
            if (_helpPausePlayers.Count > 0)
            {
                _session.RejectExecutedCommand(command, "A játék szünetel, amíg egy játékos a súgót olvassa.");
                continue;
            }
            switch (command)
            {
                case MoveCharacterCommand move when move.CharacterId == SelectedCharacter.Id:
                    MovePlayer(move.Direction);
                    break;
                case MoveCharacterCommand move:
                    MoveRemotePartyMember(move);
                    break;
                case CharacterActionCommand characterAction:
                    ExecuteCharacterAction(characterAction);
                    break;
                case LeaderActionCommand action:
                    ExecuteLeaderAction(action.Action);
                    break;
                case InventoryTransferCommand inventoryTransfer:
                    ExecuteInventoryTransfer(inventoryTransfer);
                    break;
                case UseInventoryItemCommand useItem:
                    ExecuteUseInventoryItem(useItem);
                    break;
                case DropInventoryItemCommand dropItem:
                    ExecuteDropInventoryItem(dropItem);
                    break;
                case SplitInventoryStackCommand splitStack:
                    ExecuteSplitInventoryStack(splitStack);
                    break;
                case DistributeInventoryStackCommand distributeStack:
                    ExecuteDistributeInventoryStack(distributeStack);
                    break;
                case PickUpGroundItemCommand pickUpItem:
                    ExecutePickUpGroundItem(pickUpItem);
                    break;
                case BattleActionCommand battleAction:
                    ExecuteBattleAction(battleAction);
                    break;
                case CastExplorationSpellCommand castSpell:
                    ExecuteExplorationSpell(castSpell);
                    break;
                case InnPurchaseCommand purchase:
                    ExecuteInnPurchase(purchase);
                    break;
                case InnSaleCommand sale:
                    ExecuteInnSale(sale);
                    break;
                case AcknowledgeNarrativeCommand acknowledgement:
                    ExecuteNarrativeAcknowledgement(acknowledgement);
                    break;
                case AcknowledgeLevelImageCommand imageAcknowledgement:
                    ExecuteLevelImageAcknowledgement(imageAcknowledgement);
                    break;
                case AcknowledgeRestCommand restAcknowledgement:
                    ExecuteRestAcknowledgement(restAcknowledgement);
                    break;
                case AssignQuickSpellCommand quickSpell:
                    ExecuteAssignQuickSpell(quickSpell);
                    break;
                case PrepareSpellsCommand preparation:
                    ExecuteSpellPreparation(preparation);
                    break;
                case ResolveLevelUpPromptCommand levelUp:
                    ExecuteLevelUpPrompt(levelUp);
                    break;
            }
        }
    }

    private void ExecuteInnPurchase(InnPurchaseCommand command)
    {
        var recipient = CharacterRoster.Party.Members.FirstOrDefault(character => character.Id == command.CharacterId);
        if (recipient is null)
        {
            _session.RejectExecutedCommand(command, "A vásárló karakter már nem tagja a partinak.");
            return;
        }
        if (!_innController.TryPurchase(command.Vendor, command.OfferIndex, command.ExpectedInnRevision,
                recipient, out var message))
            _session.RejectExecutedCommand(command, message);
    }

    private void ExecuteInnSale(InnSaleCommand command)
    {
        var seller = CharacterRoster.Party.Members.FirstOrDefault(character => character.Id == command.CharacterId);
        if (seller is null)
        {
            _session.RejectExecutedCommand(command, "Az eladó karakter már nem tagja a partinak.");
            return;
        }
        if (!_innController.TrySell(command.ExpectedInnRevision, command.ExpectedInventoryRevision,
                command.BackpackIndex, seller, out var message))
            _session.RejectExecutedCommand(command, message);
    }

    private ConsoleKeyInfo ReadInnKey()
    {
        var key = ReadInnKeyCore();
        if (key.Key == ConsoleKey.Q)
        {
            ShowQuestJournal();
            return new ConsoleKeyInfo('\0', InnController.StateChangedKey, false, false, false);
        }
        if (GameInputBindings.IsCharacterSheetToggle(key.Key))
        {
            ManageCharacterSheetAtInn();
            return new ConsoleKeyInfo('\0', InnController.StateChangedKey, false, false, false);
        }
        return key;
    }

    private ConsoleKeyInfo ReadInnKeyCore()
    {
        var initialRevision = _innController.Revision;
        while (!Console.KeyAvailable)
        {
            ProcessSessionCommands();
            if (_innController.Revision != initialRevision)
            {
                _activeCoopHost?.TryPublish(CreateSessionSnapshot());
                return new ConsoleKeyInfo('\0', InnController.StateChangedKey, false, false, false);
            }
            if (_activeCoopHost?.ShouldPublish(DateTime.UtcNow) == true)
                _activeCoopHost.TryPublish(CreateSessionSnapshot());
            Thread.Sleep(20);
        }
        return Console.ReadKey(intercept: true);
    }

    private void ManageCharacterSheetAtInn()
    {
        CancelHeldInventoryItem();
        _characterSheetFocused = true;
        _renderer.DrawInnCharacterSheet(SelectedCharacter);
        while (true)
        {
            var keyInfo = ReadInnKeyCore();
            if (keyInfo.Key == InnController.StateChangedKey)
            {
                _renderer.RefreshCharacterSheet(SelectedCharacter);
                continue;
            }
            if (GameInputBindings.IsCharacterSheetToggle(keyInfo.Key) || keyInfo.Key == ConsoleKey.Escape)
            {
                CancelHeldInventoryItem();
                _characterSheetFocused = false;
                _renderer.SetCharacterSheetFocused(false);
                return;
            }
            if (keyInfo.Key == ConsoleKey.Q)
            {
                ShowQuestJournal();
                _renderer.DrawInnCharacterSheet(SelectedCharacter);
                continue;
            }
            switch (GameInputBindings.InventoryAction(keyInfo.Key))
            {
                case InventoryInputAction.MoveUp: _renderer.MoveCharacterSheetSelection(-1); break;
                case InventoryInputAction.MoveDown: _renderer.MoveCharacterSheetSelection(1); break;
                case InventoryInputAction.Inspect: InspectSelectedInventoryItem(); break;
                case InventoryInputAction.Use: UseSelectedInventoryItem(); break;
                case InventoryInputAction.MoveItem: GrabOrPlaceInventoryItem(); break;
                case InventoryInputAction.SplitStack: SplitSelectedInventoryStack(); break;
                case InventoryInputAction.DistributeStack: DistributeSelectedInventoryStack(); break;
                case InventoryInputAction.Drop:
                    _renderer.DrawInventoryMessage("A fogadóban nem dobhatsz tárgyat a földre.", ConsoleColor.DarkYellow);
                    break;
                default:
                    if (keyInfo.Key == ConsoleKey.LeftArrow) _renderer.MoveDisplayedPartyMember(-1);
                    else if (keyInfo.Key == ConsoleKey.RightArrow) _renderer.MoveDisplayedPartyMember(1);
                    else if (keyInfo.Key == ConsoleKey.Delete) DismissSelectedPartyMember();
                    break;
            }
        }
    }

    private void ExecuteNarrativeAcknowledgement(AcknowledgeNarrativeCommand command)
    {
        if (_activeNarrative?.NarrativeId != command.NarrativeId)
        {
            _session.RejectExecutedCommand(command, "Ez a történeti ablak már nem aktív.");
            return;
        }
        _narrativeAcknowledgements.Add(command.SenderId);
        PlaySessionSound(SoundEffect.Waiting, [command.CharacterId]);
    }

    private void ExecuteLevelImageAcknowledgement(AcknowledgeLevelImageCommand command)
    {
        if (_activeLevelImage?.ImageId != command.ImageId)
        {
            _session.RejectExecutedCommand(command, "Ez a pályakép már nem aktív.");
            return;
        }
        AcknowledgeLevelImage(command.SenderId, command.CharacterId);
    }

    private bool EncounterWorldNpc(WorldNpc npc)
    {
        if (string.Equals(npc.DefinitionId, FirstUniqueWorldNpcId, StringComparison.OrdinalIgnoreCase))
        {
            ConverseWithFirstUniqueNpc(npc);
            return false;
        }
        var questDefinitions = _gameData.GetNpcQuests(npc.DefinitionId);
        var result = _renderer.DrawWorldNpcRecruitment(npc, questDefinitions);
        ProcessNpcQuests(npc);
        if (result == WorldNpcInteractionResult.Continue)
        {
            _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
            return true;
        }
        if (result == WorldNpcInteractionResult.Join && CharacterRoster.Party.Add(npc.Character))
        {
            _maze.RemoveWorldNpc(npc);
            var avatar = new PartyMemberAvatar(npc.Position, npc.Character);
            _maze.AddPartyMember(avatar);
            _nextPartyMoves[avatar] = DateTime.UtcNow;
            RevealFor(npc.Character, avatar.Position);
            _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
            _renderer.DrawInventoryMessage($"🤝 {npc.Character.Name} ingyen csatlakozott a partihoz.", ConsoleColor.Green);
            _activeCoopHost?.TryPublish(CreateSessionSnapshot());
            return false;
        }

        npc.Decline();
        _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
        _renderer.DrawInventoryMessage(result == WorldNpcInteractionResult.Join ? "A parti megtelt; előbb helyet kell felszabadítani."
            : $"{npc.Character.Name} egyelőre itt marad.", ConsoleColor.Yellow);
        return false;
    }

    private void ConverseWithFirstUniqueNpc(WorldNpc npc)
    {
        if (npc.ConversationStage == 0)
        {
            var sameRaceMembers = CharacterRoster.Party.Members.Count(character =>
                string.Equals(character.Race.Id, npc.Character.Race.Id, StringComparison.OrdinalIgnoreCase));
            var affinity = string.Equals(SelectedCharacter.Race.Id, npc.Character.Race.Id,
                StringComparison.OrdinalIgnoreCase) ? 2 : sameRaceMembers > 0 ? 1 : 0;
            if (affinity > 0)
            {
                npc.AdjustFriendliness(affinity);
                _renderer.DrawInventoryMessage($"🌿 Faji rokonszenv: Elira viszonya +{affinity}.", ConsoleColor.Green);
            }
        }

        var result = _renderer.DrawUniqueNpcConversation(npc);
        var friendlinessChange = result.FriendlinessChange;
        if (npc.ConversationStage == 2 && result.ChoiceIndex == 1 &&
            !CharacterRoster.Party.Members.Any(character => string.Equals(character.Race.Id,
                npc.Character.Race.Id, StringComparison.OrdinalIgnoreCase))) friendlinessChange = -1;
        npc.AdjustFriendliness(friendlinessChange);
        if (result.ChoiceIndex >= 0) npc.AdvanceConversation();
        if (result.FollowRequested && npc.State != WorldNpcState.Following)
            BeginTemporaryFollowing(npc);
        if (npc.State == WorldNpcState.Following) ProcessNpcQuests(npc);
        _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
        _renderer.DrawInventoryMessage($"🌿 Elira viszonya: {npc.Friendliness}/10.",
            friendlinessChange >= 0 ? ConsoleColor.Green : ConsoleColor.DarkYellow);
    }

    private void BeginTemporaryFollowing(WorldNpc npc)
    {
        if (_maze.PartyMembers.Any(member => member.IsTemporaryFollower))
        {
            _renderer.DrawInventoryMessage("Már van egy ideiglenes követőtök.", ConsoleColor.DarkYellow);
            return;
        }
        if (!_maze.RemoveWorldNpc(npc)) return;
        npc.BeginFollowing();
        foreach (var quest in npc.Quests.Where(progress => progress.State == NpcQuestState.Offered).ToArray())
        {
            npc.ActivateQuest(quest.QuestId);
            var definition = _gameData.NpcQuests.First(value =>
                string.Equals(value.Id, quest.QuestId, StringComparison.OrdinalIgnoreCase));
            SynchronizeQuestJournal(npc, definition);
        }
        var avatar = new PartyMemberAvatar(npc.Position, npc.Character, npc);
        _maze.AddPartyMember(avatar);
        _nextPartyMoves[avatar] = DateTime.UtcNow;
        _renderer.DrawUniqueNpcQuestOffer(npc, _gameData.GetNpcQuests(npc.DefinitionId));
        _renderer.DrawInventoryMessage("🌿 Elira ideiglenes követőként csatlakozott. Nem foglal partyhelyet.",
            ConsoleColor.Cyan);
    }

    private void ProcessNpcQuests(WorldNpc npc)
    {
        foreach (var progress in npc.Quests.Where(quest => quest.State != NpcQuestState.Completed).ToArray())
        {
            var quest = _gameData.NpcQuests.First(value =>
                string.Equals(value.Id, progress.QuestId, StringComparison.OrdinalIgnoreCase));
            if (progress.State == NpcQuestState.Offered)
            {
                npc.ActivateQuest(quest.Id);
                SynchronizeQuestJournal(npc, quest);
                _renderer.DrawInventoryMessage($"📜 Új küldetés: {quest.Title} — {quest.Description} " +
                    $"Jutalom: {quest.ExperienceReward} XP{DescribeNpcQuestItemRewards(quest)}.", ConsoleColor.Cyan);
            }

            var current = npc.Quests.First(value => string.Equals(value.QuestId, quest.Id,
                StringComparison.OrdinalIgnoreCase));
            if (quest.Type == NpcQuestType.Collect)
            {
                var available = CountPartyBackpackItems(quest.TargetId);
                if (available < quest.RequiredCount)
                {
                    _renderer.DrawInventoryMessage(
                        $"📜 {quest.Title}: {available}/{quest.RequiredCount}", ConsoleColor.DarkYellow);
                    SynchronizeQuestJournal(npc, quest, available);
                    continue;
                }
                RemovePartyBackpackItems(quest.TargetId, quest.RequiredCount);
                npc.AddQuestProgress(quest.Id, quest.RequiredCount, quest.RequiredCount);
            }
            else if (current.Progress < quest.RequiredCount)
            {
                SynchronizeQuestJournal(npc, quest);
                _renderer.DrawInventoryMessage(
                    $"📜 {quest.Title}: {current.Progress}/{quest.RequiredCount}", ConsoleColor.DarkYellow);
                continue;
            }

            if (!npc.CompleteQuest(quest.Id)) continue;
            SynchronizeQuestJournal(npc, quest);
            if (string.Equals(npc.DefinitionId, FirstUniqueWorldNpcId, StringComparison.OrdinalIgnoreCase))
                npc.AdjustFriendliness(1);
            var awards = DistributeExperience(SelectedCharacter, quest.ExperienceReward);
            foreach (var award in awards.Where(award => award.Result.LeveledUp && award.Character.IsAlive))
                ResolvePerkOffers(award.Character, award.Result);
            var itemRewards = GrantNpcQuestItems(quest);
            _renderer.DrawInventoryMessage(
                $"✅ Küldetés teljesítve: {quest.Title}. XP: {FormatExperienceAwards(awards)}." +
                (itemRewards.Length > 0 ? $" 🎁 {itemRewards}" : string.Empty), ConsoleColor.Green);
        }
        _activeCoopHost?.TryPublish(CreateSessionSnapshot());
    }

    private void ShowQuestJournal()
    {
        QuestJournalWindow.Show(OrderedQuestJournal());
        _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
        _renderer.SetCharacterSheetFocused(_characterSheetFocused);
    }

    private IReadOnlyList<QuestJournalEntrySnapshot> OrderedQuestJournal() => _questJournal.Values
        .OrderBy(entry => entry.Status)
        .ThenBy(entry => entry.Title, StringComparer.CurrentCultureIgnoreCase)
        .ToArray();

    private void SynchronizeQuestJournal(WorldNpc npc, NpcQuestDefinition quest, int? visibleProgress = null)
    {
        var progress = npc.Quests.First(value => string.Equals(value.QuestId, quest.Id,
            StringComparison.OrdinalIgnoreCase));
        if (progress.State == NpcQuestState.Offered) return;
        var status = progress.State == NpcQuestState.Completed
            ? QuestJournalStatus.Completed : QuestJournalStatus.Active;
        _questJournal[quest.Id] = CreateQuestJournalEntry(quest, status,
            visibleProgress ?? progress.Progress, quest.ExperienceReward);
    }

    private QuestJournalEntrySnapshot CreateQuestJournalEntry(NpcQuestDefinition quest,
        QuestJournalStatus status, int progress, int experienceReward) =>
        new(quest.Id, quest.Title, quest.Description, _gameData.GetNpc(quest.NpcId).Name, status,
            Math.Clamp(progress, 0, quest.RequiredCount), quest.RequiredCount, experienceReward);

    private string GrantNpcQuestItems(NpcQuestDefinition quest)
    {
        var rewards = new List<IItemDefinition>();
        if (quest.RewardItemId is { } fixedItemId)
        {
            var fixedItem = FindQuestRewardItem(fixedItemId);
            for (var count = 0; count < quest.RewardItemCount; count++) rewards.Add(fixedItem);
        }
        for (var count = 0; count < quest.RandomRewardCount; count++)
            if (RollQuestReward(quest.ExperienceReward) is { } reward) rewards.Add(reward);
        if (rewards.Count == 0) return string.Empty;

        var dropped = 0;
        foreach (var reward in rewards)
        {
            if (TryStoreLootInParty(reward, out _)) continue;
            _maze.DropItem(_player.Position, reward);
            dropped++;
        }
        PlaySessionSound(SoundEffect.Item);
        var summary = string.Join(", ", rewards.GroupBy(item => item.Name)
            .Select(group => $"{group.Key} ×{group.Count()}"));
        return dropped == 0 ? summary : $"{summary} ({dropped} a földön)";
    }

    private string DescribeNpcQuestItemRewards(NpcQuestDefinition quest)
    {
        var parts = new List<string>();
        if (quest.RewardItemId is { } fixedItemId)
            parts.Add($"{FindQuestRewardItem(fixedItemId).Name} ×{quest.RewardItemCount}");
        if (quest.RandomRewardCount > 0) parts.Add($"{quest.RandomRewardCount} véletlen tárgy");
        return parts.Count == 0 ? string.Empty : " + " + string.Join(" + ", parts);
    }

    private IItemDefinition? RollQuestReward(int experienceReward)
    {
        var maximumRarity = experienceReward >= 2000 ? ItemRarity.Legendary :
            experienceReward >= 800 ? ItemRarity.Magic : ItemRarity.Normal;
        var maximumPrice = Math.Max(80, experienceReward * 2);
        var maximumMagicPower = Math.Max(0, experienceReward / 300);
        var candidates = QuestRewardItems().Where(item => item.Rarity <= maximumRarity &&
            item.BasePrice <= maximumPrice && item.MagicPower <= maximumMagicPower).ToArray();
        return candidates.Length == 0 ? null : candidates[_random.Next(candidates.Length)];
    }

    private IItemDefinition FindQuestRewardItem(string itemId) => QuestRewardItems()
        .First(item => string.Equals(item.Id, itemId, StringComparison.OrdinalIgnoreCase));

    private IEnumerable<IItemDefinition> QuestRewardItems() => _gameData.Items.Cast<IItemDefinition>()
        .Concat(_gameData.Weapons).Concat(_gameData.Armors).Concat(_gameData.MagicItems)
        .Where(item => !SpellcastingRules.IsRestrictedFromTradingAndGeneration(item));

    private int CountPartyBackpackItems(string itemId) => CharacterRoster.Party.Members.Sum(character =>
        Enumerable.Range(0, LiveCharacter.MaximumBackpackItemCount)
            .Where(index => string.Equals(character.Backpack[index]?.Id, itemId, StringComparison.OrdinalIgnoreCase))
            .Sum(index => character.GetInventoryItemQuantity(InventorySlotKind.Backpack, index)));

    private void RemovePartyBackpackItems(string itemId, int count)
    {
        var remaining = count;
        foreach (var character in CharacterRoster.Party.Members)
        for (var index = 0; index < LiveCharacter.MaximumBackpackItemCount && remaining > 0; index++)
            while (remaining > 0 && string.Equals(character.Backpack[index]?.Id, itemId,
                       StringComparison.OrdinalIgnoreCase) &&
                   character.RemoveOneInventoryItem(InventorySlotKind.Backpack, index)) remaining--;
    }

    private void RegisterNpcQuestKill(string enemyDefinitionId) =>
        RegisterNpcQuestProgress(NpcQuestType.Kill, enemyDefinitionId);

    private void RegisterNpcQuestProgress(NpcQuestType type, string targetId, int amount = 1)
    {
        var questNpcs = _maze.WorldNpcs.Concat(_maze.PartyMembers
            .Where(member => member.TemporaryFollower is not null)
            .Select(member => member.TemporaryFollower!));
        foreach (var npc in questNpcs)
        foreach (var progress in npc.Quests.Where(value => value.State == NpcQuestState.Active).ToArray())
        {
            var quest = _gameData.NpcQuests.FirstOrDefault(value => string.Equals(value.Id, progress.QuestId,
                StringComparison.OrdinalIgnoreCase));
            if (quest?.Type == type && string.Equals(quest.TargetId, targetId, StringComparison.OrdinalIgnoreCase))
            {
                npc.AddQuestProgress(quest.Id, amount, quest.RequiredCount);
                SynchronizeQuestJournal(npc, quest);
            }
        }
    }

    private void ExecuteRestAcknowledgement(AcknowledgeRestCommand command)
    {
        if (_latestRestNotice?.RestId != command.RestId)
        {
            _session.RejectExecutedCommand(command, "Ez a pihenési összegző már nem aktív.");
            return;
        }
        AcknowledgeRest(command.SenderId, command.CharacterId);
    }

    private void AcknowledgeRest(PlayerId playerId, CharacterId characterId)
    {
        if (!_restAcknowledgements.Add(playerId)) return;
        if (_session.ConnectedHumanPlayerIds.Any(other => other != playerId))
            PlaySessionSound(SoundEffect.Waiting, [characterId]);
        var characterName = CharacterRoster.Party.Members
            .FirstOrDefault(character => character.Id == characterId)?.Name ?? "Egy játékos";
        var message = $"✓ {characterName} bezárta a pihenési összegzőt.";
        var otherCharacters = _session.CharacterControls
            .Where(control => control.AssignedPlayerId != playerId && control.AssignedPlayerId is not null)
            .Select(control => control.CharacterId).ToArray();
        RecordSessionActivity(SessionActivityKind.System, message, ConsoleColor.DarkCyan, otherCharacters);
        if (playerId != _session.HostPlayerId) _hostRestAcknowledgementMessages.Add(message);
    }

    private void ExecuteAssignQuickSpell(AssignQuickSpellCommand command)
    {
        var character = CharacterRoster.Party.Members.FirstOrDefault(member => member.Id == command.CharacterId);
        var spell = character?.KnownSpells.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, command.SpellId, StringComparison.OrdinalIgnoreCase));
        if (character is null || spell is null || !character.AssignQuickSpell(command.QuickSlot, spell))
            _session.RejectExecutedCommand(command, "Csak memorizált varázslat tehető gyorshelyre.");
    }

    private void ExecuteSpellPreparation(PrepareSpellsCommand command)
    {
        if (_activeSpellPreparation is null || _activeSpellPreparation.PromptId != command.PromptId ||
            _activeSpellPreparation.CharacterId != command.CharacterId)
        {
            _session.RejectExecutedCommand(command, "Ez a memorizálási kérés már nem aktív.");
            return;
        }
        var character = CharacterRoster.Party.Members.FirstOrDefault(member => member.Id == command.CharacterId);
        var ids = command.SpellIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var spells = character?.KnownSpells.Where(spell => ids.Contains(spell.Id, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (character is null || spells is null || spells.Length != ids.Length || !character.SetMemorizedSpells(spells))
        {
            _session.RejectExecutedCommand(command, "A választott varázslatlista nem memorizálható.");
            return;
        }
        _spellPreparationCompleted = true;
    }

    private void ExecuteLevelUpPrompt(ResolveLevelUpPromptCommand command)
    {
        if (_activeLevelUpPrompt is null || _activeLevelUpPrompt.PromptId != command.PromptId ||
            _activeLevelUpPrompt.CharacterId != command.CharacterId ||
            (_activeLevelUpPrompt.Kind != LevelUpPromptKind.Summary &&
             _activeLevelUpPrompt.Choices.All(choice => choice.Id != command.ChoiceId)))
        {
            _session.RejectExecutedCommand(command, "Ez a szintlépési választás már nem aktív vagy nem érvényes.");
            return;
        }
        _levelUpResponse = command.ChoiceId;
        _levelUpPromptCompleted = true;
    }

    private void ShowSynchronizedNarrative(NarrativeKind kind, string title, string subtitle,
        IReadOnlyList<string> paragraphs, BossPresentationSnapshot? boss = null)
    {
        var previousPhase = _session.Phase;
        _narrativeAcknowledgements.Clear();
        _activeNarrative = new NarrativeSnapshot(Guid.NewGuid(), kind, title, subtitle, paragraphs, [], boss);
        _session.SetPhase(GameSessionPhase.Paused);
        _renderer.ShowStoryOverlay(title, subtitle, paragraphs, _maze, _fogOfWar, _player.Position, kind, boss);
        _activeCoopHost?.TryPublish(CreateSessionSnapshot());
        while (true)
        {
            ProcessSessionCommands();
            if (Console.KeyAvailable && Console.ReadKey(intercept: true).Key == ConsoleKey.Enter)
            {
                _narrativeAcknowledgements.Add(_session.HostPlayerId);
                if (_session.ConnectedHumanPlayerIds.Any(player => player != _session.HostPlayerId))
                    PlaySessionSound(SoundEffect.Waiting, [SelectedCharacter.Id]);
            }
            var required = _session.ConnectedHumanPlayerIds;
            if (required.All(_narrativeAcknowledgements.Contains)) break;
            if (_activeCoopHost?.ShouldPublish(DateTime.UtcNow) == true)
                _activeCoopHost.TryPublish(CreateSessionSnapshot());
            Thread.Sleep(20);
        }
        _renderer.CloseStoryOverlay();
        _activeNarrative = null;
        _narrativeAcknowledgements.Clear();
        _session.SetPhase(previousPhase);
        _activeCoopHost?.TryPublish(CreateSessionSnapshot());
    }

    private void ShowSynchronizedLevelImage(string fileName, string path)
    {
        var previousPhase = _session.Phase;
        _levelImageAcknowledgements.Clear();
        _activeLevelImage = new LevelImageSnapshot(Guid.NewGuid(), _maze.LevelName, fileName, []);
        _session.SetPhase(GameSessionPhase.Paused);
        _activeCoopHost?.TryPublish(CreateSessionSnapshot());

        if (!ImageViewer.Show(path))
            _renderer.DrawDeveloperMessage($"Pályakép még nem található: {fileName}");
        AcknowledgeLevelImage(_session.HostPlayerId, SelectedCharacter.Id);
        _activeCoopHost?.TryPublish(CreateSessionSnapshot());

        while (true)
        {
            ProcessSessionCommands();
            if (_session.ConnectedHumanPlayerIds.All(_levelImageAcknowledgements.Contains)) break;
            if (_activeCoopHost?.ShouldPublish(DateTime.UtcNow) == true)
                _activeCoopHost.TryPublish(CreateSessionSnapshot());
            Thread.Sleep(20);
        }

        _activeLevelImage = null;
        _levelImageAcknowledgements.Clear();
        _session.SetPhase(previousPhase);
        _activeCoopHost?.TryPublish(CreateSessionSnapshot());
    }

    private void AcknowledgeLevelImage(PlayerId playerId, CharacterId characterId)
    {
        if (!_levelImageAcknowledgements.Add(playerId)) return;
        var characterName = CharacterRoster.Party.Members
            .FirstOrDefault(character => character.Id == characterId)?.Name ?? "Egy játékos";
        var message = $"👤 {characterName} készen áll a játékra.";
        var otherCharacters = _session.CharacterControls
            .Where(control => control.AssignedPlayerId != playerId && control.AssignedPlayerId is not null &&
                              control.ConnectionState == PlayerConnectionState.Connected)
            .Select(control => control.CharacterId).ToArray();
        RecordSessionActivity(SessionActivityKind.System, message, ConsoleColor.Green, otherCharacters);
        if (playerId != _session.HostPlayerId)
            _renderer.DrawInventoryMessage(message, ConsoleColor.Green);
    }

    private void ShowSynchronizedRest(PartyRestSnapshot rest)
    {
        var previousPhase = _session.Phase;
        _restAcknowledgements.Clear();
        _hostRestAcknowledgementMessages.Clear();
        _latestRestNotice = rest;
        _session.SetPhase(GameSessionPhase.Paused);
        DrawRestSummaryForHost();
        var renderedAcknowledgementCount = _restAcknowledgements.Count;
        _activeCoopHost?.TryPublish(CreateSessionSnapshot());
        while (true)
        {
            ProcessSessionCommands();
            if (Console.KeyAvailable && Console.ReadKey(intercept: true).Key == ConsoleKey.Enter)
                AcknowledgeRest(_session.HostPlayerId, SelectedCharacter.Id);
            var required = _session.ConnectedHumanPlayerIds;
            if (required.All(_restAcknowledgements.Contains)) break;
            if (_restAcknowledgements.Count != renderedAcknowledgementCount)
            {
                DrawRestSummaryForHost();
                renderedAcknowledgementCount = _restAcknowledgements.Count;
            }
            if (_activeCoopHost?.ShouldPublish(DateTime.UtcNow) == true)
                _activeCoopHost.TryPublish(CreateSessionSnapshot());
            Thread.Sleep(20);
        }
        _latestRestNotice = null;
        _restAcknowledgements.Clear();
        _session.SetPhase(previousPhase);
        _activeCoopHost?.TryPublish(CreateSessionSnapshot());
        foreach (var message in _hostRestAcknowledgementMessages)
            _renderer.DrawInventoryMessage(message, ConsoleColor.DarkCyan);
        _hostRestAcknowledgementMessages.Clear();
    }

    private void DrawRestSummaryForHost()
    {
        if (_latestRestNotice is not { } rest) return;
        var acknowledged = _restAcknowledgements.Contains(_session.HostPlayerId);
        _renderer.DrawRestSummaryScreen(rest,
            acknowledged ? "❖  Várakozás a másik játékosra…  ❖" : "❖  Nyomj Entert a folytatáshoz...  ❖",
            acknowledged ? ConsoleColor.DarkCyan : ConsoleColor.Green);
    }

    private void ContinueDisconnectedRemoteBattleAsNpc()
    {
        if (_activeBattleState is not { IsCompleted: false } state ||
            state.PlayerCharacterId == SelectedCharacter.Id ||
            _session.IsHumanControlled(state.PlayerCharacterId)) return;

        _renderer.DrawInventoryMessage(
            $"{state.Player.Name} kapcsolata megszakadt; az AI fegyveres támadással folytatja a csatát.",
            ConsoleColor.DarkYellow);
        if (state.IsPlayerTurn) ResolveActiveBattleAction(null);
        else ResolveActiveEnemyTurn();
    }

    private void ExecuteLeaderAction(LeaderAction action)
    {
        switch (action)
        {
            case LeaderAction.ToggleRegrouping:
                TogglePartyRegrouping();
                break;
            case LeaderAction.ToggleHoldPosition:
                TogglePartyHoldPosition();
                break;
            case LeaderAction.ScatterParty:
                ScatterPartyTemporarily();
                break;
            case LeaderAction.ToggleAttackMode:
                TogglePartyAttackMode();
                break;
            case LeaderAction.Rest:
                TryRestParty();
                break;
            case LeaderAction.ActivateExit:
                ActivateExit();
                break;
        }
    }

    private void ExecuteCharacterAction(CharacterActionCommand command)
    {
        var character = CharacterRoster.Party.Members.FirstOrDefault(candidate => candidate.Id == command.CharacterId);
        var position = character is null ? null : GetCharacterWorldPosition(character);
        if (character is null || position is null || !character.IsAlive) return;
        var isLeader = character == SelectedCharacter;
        switch (command.Action)
        {
            case CharacterAction.OpenDoor:
                _doorInteractions.TryOpenAdjacentDoor(_maze, _fogOfWar, position.Value, _player.Position,
                    character, allowPartyAssistanceAndPrompts: isLeader, command.TargetDoorPosition, command.UseKey);
                break;
            case CharacterAction.CloseOrLockDoor:
                _doorInteractions.TryCloseOrLockAdjacentDoor(_maze, _fogOfWar, position.Value, _player.Position,
                    character, command.TargetDoorPosition, command.UseKey);
                break;
            case CharacterAction.SearchCurrentPosition:
                if (!TryDisarmAdjacentTrap(character, position.Value))
                    TrySearchCurrentCell(character, position.Value, shareLootWithParty: isLeader);
                break;
        }
    }

    private IReadOnlyList<Position> AdjacentDoorPositions(Position position) =>
        Enum.GetValues<Direction>()
            .Select(direction => position + direction)
            .Where(candidate => _maze.GetDoorAt(candidate) is not null)
            .ToArray();

    private bool? GetLocalThiefKeyChoice(CharacterAction action, Position? targetDoorPosition)
    {
        if (!CharacterClassRules.IsThief(SelectedCharacter.CharacterClass.Id) ||
            !SelectedCharacter.Backpack.Any(item =>
                string.Equals(item?.Id, MiscItemIds.Key, StringComparison.OrdinalIgnoreCase)) ||
            targetDoorPosition is not { } target || _maze.GetDoorAt(target) is not { } door ||
            action switch
            {
                CharacterAction.OpenDoor => door.State != DoorState.Locked,
                CharacterAction.CloseOrLockDoor => door.State != DoorState.Closed,
                _ => true
            }) return null;

        _renderer.DrawDoorMessage(
            "🔑 Felhasználjuk a kulcsot? I/Y/Enter: igen | N/Esc: nem, jöjjön a tolvajpróba",
            ConsoleColor.Yellow);
        while (true)
        {
            var key = Console.ReadKey(intercept: true).Key;
            if (key is ConsoleKey.I or ConsoleKey.Y or ConsoleKey.Enter) return true;
            if (key is ConsoleKey.N or ConsoleKey.Escape) return false;
        }
    }

    private Position? SelectDoorTarget(IReadOnlyList<Position> doors, CharacterAction action)
    {
        var selected = 0;
        Position? previous = null;
        while (true)
        {
            var current = doors[selected];
            var verb = action == CharacterAction.OpenDoor ? "nyitás" : "bezárás/zárás";
            _renderer.DrawSpellTargetCursor(_maze, _fogOfWar, previous, current, true,
                $"Ajtó kiválasztása ({verb}): nyilak/Tab, Enter: kész, Esc: mégse");
            previous = current;
            _activeCoopHost?.TryPublish(CreateSessionSnapshot());
            while (!Console.KeyAvailable)
            {
                ProcessSessionCommands();
                if (_activeCoopHost?.ShouldPublish(DateTime.UtcNow) == true)
                    _activeCoopHost.TryPublish(CreateSessionSnapshot());
                Thread.Sleep(20);
            }
            var key = Console.ReadKey(intercept: true).Key;
            if (key == ConsoleKey.Escape)
            {
                _renderer.FinishSpellTargeting(_maze, _fogOfWar, _player.Position);
                return null;
            }
            if (key == ConsoleKey.Enter)
            {
                _renderer.FinishSpellTargeting(_maze, _fogOfWar, _player.Position);
                return current;
            }
            if (key == ConsoleKey.Tab)
                selected = (selected + 1) % doors.Count;
            else if (TryGetDirection(key, out var direction))
            {
                var directionalDoor = _player.Position + direction;
                var index = doors.ToList().IndexOf(directionalDoor);
                if (index >= 0) selected = index;
            }
        }
    }

    private void ActivateExit()
    {
        if (_player.Position != _maze.Exit) return;
        if (_maze.PartyMembers.FirstOrDefault(member => member.IsTemporaryFollower) is { } escort &&
            Manhattan(escort.Position, _player.Position) > 3)
        {
            _renderer.DrawInventoryMessage("🌿 Elira túl messze van a kijárattól. Várjátok meg vagy hívjátok magatokhoz Gyülekező paranccsal.",
                ConsoleColor.Yellow);
            return;
        }
        ResolveTemporaryFollowerAtExit();
        if (_mazeLevel == MazeLevelConfigurations.FinalLevel)
        {
            CompleteCampaign();
            return;
        }
        var completedLevel = _mazeLevel;
        PlaySessionSound(SoundEffect.LevelComplete);
        _backgroundMusic.EnterInn();
        _session.SetPhase(GameSessionPhase.Inn);
        _innController.Run(completedLevel);
        _activeInnDeparture = new InnDepartureSnapshot("A csapat szedelőzködik, és elhagyjátok a fogadót.");
        _session.SetPhase(GameSessionPhase.Paused);
        _activeCoopHost?.TryPublish(CreateSessionSnapshot());
        _mazeLevel++;
        StartNewMaze();
    }

    private void ResolveTemporaryFollowerAtExit()
    {
        var avatar = _maze.PartyMembers.FirstOrDefault(member => member.TemporaryFollower is not null);
        if (avatar?.TemporaryFollower is not { } follower) return;
        foreach (var progress in follower.Quests.Where(value => value.State == NpcQuestState.Active).ToArray())
        {
            var quest = _gameData.NpcQuests.FirstOrDefault(value =>
                string.Equals(value.Id, progress.QuestId, StringComparison.OrdinalIgnoreCase));
            if (quest is { Type: NpcQuestType.Escort })
            {
                follower.AddQuestProgress(quest.Id, 1, quest.RequiredCount);
                SynchronizeQuestJournal(follower, quest);
            }
        }
        ProcessNpcQuests(follower);
        follower.AdjustFriendliness(2);

        var joined = false;
        if (follower.Friendliness >= 10)
        {
            var hasRoom = CharacterRoster.Party.Members.Count < Party.MaximumSize;
            joined = _renderer.ConfirmUniqueNpcPermanentJoin(follower, hasRoom) &&
                     CharacterRoster.Party.Add(follower.Character);
        }
        if (joined)
        {
            avatar.MakePermanent();
            _renderer.DrawInventoryMessage("🤝 Elira Ezüstág végleg csatlakozott a partihoz.", ConsoleColor.Green);
            return;
        }

        _maze.RemovePartyMember(avatar);
        _nextPartyMoves.Remove(avatar);
        CharacterRoster.Remove(follower.Character);
        _renderer.DrawInventoryMessage(follower.Friendliness >= 10
            ? "🌿 Elira hálásan elbúcsúzott."
            : $"🌿 Elira kijutott de még nem bízik eléggé a végleges csatlakozáshoz ({follower.Friendliness}/10).",
            ConsoleColor.Cyan);
    }

    private void DropSelectedInventoryItem()
    {
        var slot = _renderer.GetSelectedInventorySlot();
        if (slot is null) { _renderer.DrawInventoryMessage("Itt nincs ledobható tárgy.", ConsoleColor.DarkYellow); return; }
        var item = slot.Value.Character.GetInventoryItem(slot.Value.Kind, slot.Value.Index);
        if (item is null) { _renderer.DrawInventoryMessage("A kijelölt hely üres.", ConsoleColor.DarkYellow); return; }
        if (SpellcastingRules.IsSpellcastingFocus(item))
        { _renderer.DrawInventoryMessage($"A(z) {item.Name} a karakterhez kötött varázsfókusz, ezért nem dobható el.", ConsoleColor.Red); return; }
        var commandId = _localCommandId + 1;
        if (!_session.Submit(new DropInventoryItemCommand(_session.HostPlayerId, commandId,
                slot.Value.Character.Id, slot.Value.Character.InventoryRevision, slot.Value.Kind, slot.Value.Index))) return;
        _localCommandId = commandId;
    }

    private bool TrySearchCurrentCell(LiveCharacter character, Position position, bool shareLootWithParty)
    {
        var corpse = _maze.GetCorpseAt(position);
        var pile = _maze.GetGroundItemPileAt(position);
        if (corpse is null && pile is null) return false;
        if (corpse is MonsterCorpse { IsSearched: true } && pile is null) return false;

        var messages = new List<string>();
        if (corpse is MonsterCorpse monsterCorpse)
        {
            if (monsterCorpse.IsSearched)
                messages.Add($"{monsterCorpse.FormerName} tetemét már átkutattad");
            else
            {
                monsterCorpse.MarkSearched();
                SearchMonsterCorpse(monsterCorpse, character, position, shareLootWithParty, messages);
            }
        }
        else if (corpse is PartyMemberCorpse)
            messages.Add("Az elesett társ testén nincs elvehető zsákmány");
        else if (corpse is not null)
            messages.Add("Ez a régi tetem már nem tartalmaz azonosítható zsákmányt");

        PickUpGroundItems(character, position, shareLootWithParty, messages);
        _renderer.RefreshCharacterSheet(SelectedCharacter);
        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
        var resultMessage = "🔎 " + (messages.Count == 0
            ? "A keresés nem hozott eredményt."
            : string.Join("; ", messages) + ".");
        _renderer.DrawInventoryMessage(resultMessage, ConsoleColor.Yellow);
        RecordSessionActivity(SessionActivityKind.System, resultMessage, ConsoleColor.Yellow, [character.Id]);
        return true;
    }

    private void SearchMonsterCorpse(MonsterCorpse corpse, LiveCharacter character, Position position,
        bool shareLootWithParty, ICollection<string> messages)
    {
        var enemy = _gameData.GetEnemy(corpse.EnemyDefinitionId);
        var rules = _gameData.LootRules;
        var keyChance = AdjustedSearchChance(character, rules.KeyChancePercent);
        var goldChance = AdjustedSearchChance(character, rules.GoldChancePercent);
        var equipmentDefinition = _gameData.GetMonsterLoot(enemy.Id);
        var equipmentChance = equipmentDefinition is null
            ? 0
            : AdjustedSearchChance(character, equipmentDefinition.EquipmentChancePercent);
        messages.Add($"esélyek: 🔑 {keyChance}%, {ConsoleRenderer.MoneyIcon} {goldChance}%" +
                     (equipmentDefinition is null ? string.Empty : $", 🎁 {equipmentChance}%"));

        var foundItems = new List<IItemDefinition>();
        if (_random.Next(100) < keyChance) foundItems.Add(_gameData.GetItem(MiscItemIds.Key));
        if (_random.Next(100) < goldChance)
        {
            var maximumGold = Math.Max(1, enemy.StrengthTier * rules.GoldPerStrengthTier);
            var gold = _random.Next(1, maximumGold + 1);
            SelectedCharacter.AddGold(gold);
            messages.Add($"{ConsoleRenderer.MoneyIcon} {gold} arany");
        }
        if (equipmentDefinition is not null && _random.Next(100) < equipmentChance &&
            RollEquipmentLoot(equipmentDefinition) is { } equipment)
            foundItems.Add(equipment);

        foreach (var item in foundItems)
        {
            if (TryStoreSearchedLoot(character, item, shareLootWithParty, out var owner))
                messages.Add($"{item.Name} → {owner} hátizsákja");
            else
            {
                _maze.DropItem(position, item);
                messages.Add($"{item.Name} a földön maradt (a hátizsákok tele vannak)");
            }
        }
        if (foundItems.Count == 0 && messages.All(message => !message.StartsWith(ConsoleRenderer.MoneyIcon, StringComparison.Ordinal)))
            messages.Add("a tetemnél nem találtál zsákmányt");
    }

    private int AdjustedSearchChance(LiveCharacter character, int baseChance)
    {
        var chance = Math.Max(0, baseChance);
        if (CharacterClassRules.IsThief(character.CharacterClass.Id))
            chance = chance * _gameData.LootRules.ThiefChanceMultiplierPercent / 100;
        if (character.Race.HasTrait(RaceTraits.KeenSenses)) chance += 15;
        chance += character.EffectiveAbilities.Intelligence * _gameData.LootRules.IntelligenceChanceBonusPerPoint;
        return Math.Clamp(chance, 0, 100);
    }

    private IItemDefinition? RollEquipmentLoot(MonsterLootDefinition loot)
    {
        bool Eligible(IItemDefinition item) => item.Rarity >= loot.MinimumRarity &&
            item.Rarity <= loot.MaximumRarity && item.MagicPower <= loot.MaximumMagicPower &&
            item.BasePrice <= loot.MaximumBasePrice &&
            !SpellcastingRules.IsRestrictedFromTradingAndGeneration(item);

        var categoryCandidates = new List<List<IItemDefinition>>();
        if (loot.CanDropWeapon)
            categoryCandidates.Add(_gameData.Weapons.Where(Eligible).Cast<IItemDefinition>().ToList());
        if (loot.CanDropArmor)
            categoryCandidates.Add(_gameData.Armors.Where(Eligible).Cast<IItemDefinition>().ToList());
        if (loot.CanDropMagicItem)
            categoryCandidates.Add(_gameData.MagicItems.Where(Eligible).Cast<IItemDefinition>().ToList());
        categoryCandidates.RemoveAll(candidates => candidates.Count == 0);
        if (categoryCandidates.Count == 0) return null;
        var candidates = categoryCandidates[_random.Next(categoryCandidates.Count)];
        return candidates[_random.Next(candidates.Count)];
    }

    private IItemDefinition? RollMasterThiefChestLoot(LiveCharacter character)
    {
        if (!character.HasPerk(PerkIds.ThiefMasterThief) || _random.Next(100) >= 25) return null;
        var candidates = AllTradableItems().Where(item => item.Rarity == ItemRarity.Magic).ToList();
        return candidates.Count == 0 ? null : candidates[_random.Next(candidates.Count)];
    }

    private bool TryStoreLootInParty(IItemDefinition item, out string ownerName)
    {
        foreach (var character in new[] { SelectedCharacter }.Concat(CharacterRoster.Party.Members
                     .Where(character => character != SelectedCharacter && character.IsAlive)))
        {
            if (!character.AddToBackpack(item)) continue;
            ownerName = character.Name;
            return true;
        }
        ownerName = string.Empty;
        return false;
    }

    private bool TryStoreSearchedLoot(LiveCharacter character, IItemDefinition item, bool shareLootWithParty,
        out string ownerName)
    {
        var candidates = shareLootWithParty
            ? new[] { character }.Concat(CharacterRoster.Party.Members.Where(candidate =>
                candidate != character && candidate.IsAlive))
            : [character];
        foreach (var candidate in candidates)
        {
            if (!candidate.AddToBackpack(item)) continue;
            ownerName = candidate.Name;
            return true;
        }
        ownerName = string.Empty;
        return false;
    }

    private void PickUpGroundItems(LiveCharacter character, Position position, bool shareLootWithParty,
        ICollection<string> messages)
    {
        var pile = _maze.GetGroundItemPileAt(position);
        if (pile is null) return;
        var pickedUp = new List<string>();
        foreach (var item in pile.Items.ToArray())
        {
            if (!TryStoreSearchedLoot(character, item, shareLootWithParty, out var owner)) continue;
            pile.Remove(item);
            pickedUp.Add($"{item.Name} → {owner}");
        }
        if (pickedUp.Count > 0) messages.Add("felvéve: " + string.Join(", ", pickedUp));
        if (pile.Items.Count == 0) _maze.RemoveGroundItemPile(pile);
        else messages.Add($"a földön maradt {pile.Items.Count} tárgy (nincs hely)");
    }

    private void InspectSelectedInventoryItem()
    {
        var slot = _renderer.GetSelectedInventorySlot();
        if (slot is null && _renderer.GetSelectedPartyMember() is { } partyMember)
        {
            _renderer.DrawInventoryMessage($"{partyMember.Name} — mozgásprofil: {NpcBehaviorName(partyMember.NpcBehavior)}.", partyMember.Color);
            return;
        }
        var item = slot is { } selected ? selected.Character.GetInventoryItem(selected.Kind, selected.Index) : null;
        if (item is null) { _renderer.DrawInventoryMessage("A kijelölt helyen nincs megvizsgálható tárgy.", ConsoleColor.DarkYellow); return; }

        var inspection = ItemInspectionFormatter.Format(item, _gameData,
            slot is { } itemSlot
                ? itemSlot.Character.GetInventoryItemCharges(itemSlot.Kind, itemSlot.Index)
                : 0,
            slot?.Character.WeaponProficiencies.ToDictionary(proficiency => proficiency.FamilyId,
                proficiency => (int)proficiency.Rank, StringComparer.OrdinalIgnoreCase));
        _renderer.DrawInventoryMessage(inspection.Text, inspection.Color);
    }

    private void DismissSelectedPartyMember()
    {
        var character = _renderer.GetSelectedPartyMember();
        if (character is null)
        {
            _renderer.DrawInventoryMessage("A Del használatához jelölj ki egy partitársat.", ConsoleColor.DarkYellow);
            return;
        }
        if (character == SelectedCharacter)
        {
            _renderer.DrawInventoryMessage("👑 A party leaderét nem lehet kirúgni.", ConsoleColor.DarkYellow);
            return;
        }

        CancelHeldInventoryItem();
        _renderer.DrawInventoryMessage(
            $"⚠️ Biztosan kirúgod {character.Name} karaktert? Felszerelésével együtt végleg távozik. I/Y: igen | N/Esc: nem",
            ConsoleColor.Red);
        while (true)
        {
            var key = Console.ReadKey(intercept: true).Key;
            if (key is ConsoleKey.N or ConsoleKey.Escape)
            {
                _renderer.DrawInventoryMessage($"{character.Name} a partiban marad.", ConsoleColor.DarkYellow);
                return;
            }
            if (key is not (ConsoleKey.I or ConsoleKey.Y)) continue;
            break;
        }

        var changedPositions = new List<Position>();
        var avatar = _maze.PartyMembers.FirstOrDefault(member => member.Character == character);
        if (avatar is not null)
        {
            changedPositions.Add(avatar.Position);
            _maze.RemovePartyMember(avatar);
            _nextPartyMoves.Remove(avatar);
        }
        foreach (var corpse in _maze.Corpses.OfType<PartyMemberCorpse>()
                     .Where(corpse => corpse.Character == character).ToList())
        {
            changedPositions.Add(corpse.Position);
            _maze.RemoveCorpse(corpse);
        }

        CharacterRoster.Remove(character);
        foreach (var position in changedPositions.Distinct())
            _renderer.DrawMapCellAfterBattle(_maze, _fogOfWar, position, _player.Position);
        _renderer.RefreshAfterPartyMemberRemoved(character, SelectedCharacter);
        _renderer.DrawInventoryMessage($"👋 {character.Name} felszerelésével együtt végleg távozott a partiból.",
            ConsoleColor.DarkYellow);
    }

    private bool ConfirmReturnToMainMenu()
    {
        _renderer.DrawInventoryMessage(
            "⚠️ Visszatérsz a főmenübe? A legutóbbi mentés óta történt változások elvesznek. I/Y: igen | N/Esc: maradok",
            ConsoleColor.Red);
        while (true)
        {
            var key = Console.ReadKey(intercept: true).Key;
            if (key is ConsoleKey.I or ConsoleKey.Y) return true;
            if (key is ConsoleKey.N or ConsoleKey.Escape)
            {
                _renderer.DrawInventoryMessage("A játék folytatódik.", ConsoleColor.Cyan);
                return false;
            }
        }
    }

    private void UseSelectedInventoryItem()
    {
        var slot = _renderer.GetSelectedInventorySlot();
        if (slot is null || slot.Value.Kind != InventorySlotKind.Backpack)
        { _renderer.DrawInventoryMessage("Használható tárgyat a hátizsákban jelölj ki.", ConsoleColor.DarkYellow); return; }
        var selectedItem = slot.Value.Character.GetInventoryItem(slot.Value.Kind, slot.Value.Index);
        if (SpellcastingRules.IsSpellcastingFocus(selectedItem))
        {
            _renderer.DrawSpellInfoPage(slot.Value.Character, 0);
            return;
        }
        if (selectedItem is not MiscItemDefinition item || item.Effect == ConsumableEffect.None)
        { _renderer.DrawInventoryMessage("A kijelölt tárgy közvetlenül nem használható.", ConsoleColor.DarkYellow); return; }

        var commandId = _localCommandId + 1;
        if (!_session.Submit(new UseInventoryItemCommand(_session.HostPlayerId, commandId,
                slot.Value.Character.Id, slot.Value.Character.InventoryRevision, slot.Value.Index))) return;
        _localCommandId = commandId;
    }

    private void ExecuteUseInventoryItem(UseInventoryItemCommand command)
    {
        var character = CharacterRoster.Party.Members.FirstOrDefault(member => member.Id == command.CharacterId);
        if (character?.GetInventoryItem(InventorySlotKind.Backpack, command.BackpackIndex) is not MiscItemDefinition item ||
            item.Effect == ConsumableEffect.None || character.InventoryRevision != command.ExpectedInventoryRevision)
            return;

        var used = true;
        var result = item.Id == MiscItemIds.HerbalTea &&
                     (character.WaterLevel < 100 || character.IsAlive && character.CurrentVitality < character.MaximumVitality)
            ? UseHerbalTea(character, item.EffectValue)
            : IsInitiativeDrink(item) && character.IsAlive
                ? UseInitiativeDrink(character, item)
            : item.Effect switch
            {
                ConsumableEffect.Food when character.FoodLevel < 100 => UseFood(character, item.EffectValue),
                ConsumableEffect.Water when character.WaterLevel < 100 => UseWater(character, item.EffectValue),
                ConsumableEffect.Heal when character.IsAlive && character.CurrentVitality < character.MaximumVitality => UseHealing(character, item.EffectValue),
                ConsumableEffect.RestoreMana when character.IsAlive && character.UsesMana && character.CurrentMana < character.MaximumMana => UseManaPotion(character, item.EffectValue),
                ConsumableEffect.CurePoison when character.RemoveStatus(CharacterStatusIds.Poisoned) => "a mérgezés megszűnt",
                ConsumableEffect.CureDisease when character.RemoveStatus(CharacterStatusIds.Diseased) => "a betegség megszűnt",
                ConsumableEffect.StopBleeding when character.RemoveStatus(CharacterStatusIds.Bleeding) => "a vérzés elállt",
                ConsumableEffect.Vision when character.IsAlive => UseVisionItem(character, item),
                _ => string.Empty
            };
        if (string.IsNullOrEmpty(result)) used = false;
        if (!used) { _renderer.DrawInventoryMessage("A tárgy hatására most nincs szükség vagy nem alkalmazható.", ConsoleColor.DarkYellow); return; }

        character.RemoveOneInventoryItem(InventorySlotKind.Backpack, command.BackpackIndex);
        character.SynchronizeNeedStatuses(_gameData.GetStatus(CharacterStatusIds.Hungry), _gameData.GetStatus(CharacterStatusIds.Thirsty));
        _renderer.RefreshCharacterSheet(SelectedCharacter);
        var message = $"{character.Name} használta: {item.Name} — {result}.";
        _renderer.DrawInventoryMessage(message, ConsoleColor.Green);
        RecordSessionActivity(SessionActivityKind.System, message, ConsoleColor.Green, [character.Id]);
        if (item.Effect == ConsumableEffect.Heal)
            PlaySessionSound(SoundEffect.DefensiveSpell, [character.Id]);
    }

    private void ExecuteDropInventoryItem(DropInventoryItemCommand command)
    {
        var character = CharacterRoster.Party.Members.FirstOrDefault(member => member.Id == command.CharacterId);
        if (character is null || character.InventoryRevision != command.ExpectedInventoryRevision) return;
        var item = character.GetInventoryItem(command.SlotKind, command.SlotIndex);
        if (item is null || SpellcastingRules.IsSpellcastingFocus(item)) return;
        var charges = character.GetInventoryItemCharges(command.SlotKind, command.SlotIndex);
        var position = GetCharacterWorldPosition(character);
        if (position is null || !character.RemoveOneInventoryItem(command.SlotKind, command.SlotIndex)) return;
        _maze.DropItem(position.Value, item, charges);
        _renderer.RefreshCharacterSheet(SelectedCharacter);
        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
        var pileCount = _maze.GetGroundItemPileAt(position.Value)?.Items.Count ?? 1;
        _renderer.DrawInventoryMessage($"Ledobtad: {item.Name}. A mezőn {pileCount} tárgy van.", ConsoleColor.Cyan);
        PlaySessionSound(SoundEffect.Item, [character.Id]);
    }

    private void ExecutePickUpGroundItem(PickUpGroundItemCommand command)
    {
        var character = CharacterRoster.Party.Members.FirstOrDefault(member => member.Id == command.CharacterId);
        var pile = _maze.GroundItemPiles.FirstOrDefault(candidate => candidate.Id == command.GroundPileId);
        var position = character is null ? null : GetCharacterWorldPosition(character);
        if (character is null || pile is null || position != pile.Position ||
            character.InventoryRevision != command.ExpectedInventoryRevision ||
            pile.Revision != command.ExpectedGroundPileRevision || command.GroundItemIndex < 0 ||
            command.GroundItemIndex >= pile.Entries.Count)
            return;
        var entry = pile.Entries[command.GroundItemIndex];
        var destinationItem = character.GetInventoryItem(InventorySlotKind.Backpack,
            command.DestinationBackpackIndex);
        var destinationQuantity = character.GetInventoryItemQuantity(InventorySlotKind.Backpack,
            command.DestinationBackpackIndex);
        if (destinationItem is not null && (!string.Equals(destinationItem.Id, entry.Item.Id,
                StringComparison.OrdinalIgnoreCase) ||
            character.GetInventoryItemCharges(InventorySlotKind.Backpack, command.DestinationBackpackIndex) !=
            entry.Charges || destinationQuantity >= LiveCharacter.MaximumBackpackStackSize)) return;
        var change = new InventorySlotChange(InventorySlotKind.Backpack, command.DestinationBackpackIndex,
            entry.Item, entry.Charges, destinationQuantity + 1);
        if (!character.CanApplyInventoryChanges(change) ||
            !pile.TryTake(command.GroundItemIndex, command.ExpectedGroundPileRevision, out _)) return;
        character.ApplyInventoryChanges(change);
        if (pile.Entries.Count == 0) _maze.RemoveGroundItemPile(pile);
        _renderer.RefreshCharacterSheet(SelectedCharacter);
        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
        _renderer.DrawInventoryMessage($"Felvetted: {entry.Item.Name}.", ConsoleColor.Green);
        PlaySessionSound(SoundEffect.Item, [character.Id]);
    }

    private Position? GetCharacterWorldPosition(LiveCharacter character)
    {
        if (character == SelectedCharacter) return _player.Position;
        return _maze.PartyMembers.FirstOrDefault(member => member.Character == character)?.Position;
    }

    private static string UseFood(LiveCharacter character, int amount)
    {
        var before = character.FoodLevel;
        character.RestoreFood(amount);
        return $"élelem +{character.FoodLevel - before}";
    }

    private static string UseWater(LiveCharacter character, int amount)
    {
        var before = character.WaterLevel;
        character.RestoreWater(amount);
        return $"víz +{character.WaterLevel - before}";
    }

    private string UseHerbalTea(LiveCharacter character, int waterAmount)
    {
        var waterBefore = character.WaterLevel;
        var vitalityBefore = character.CurrentVitality;
        character.RestoreWater(waterAmount);
        var healing = _random.Next(5, 16);
        if (character.IsAlive) character.RestoreVitality(healing);
        return $"víz +{character.WaterLevel - waterBefore}, {FormatHealingResult(character, healing, vitalityBefore)}";
    }

    private static bool IsInitiativeDrink(MiscItemDefinition item) =>
        item.Id is MiscItemIds.Mead or MiscItemIds.SpicedWine;

    private static string UseInitiativeDrink(LiveCharacter character, MiscItemDefinition item)
    {
        var waterBefore = character.WaterLevel;
        character.RestoreWater(item.EffectValue);
        character.ApplySpellEffect(new ActiveSpellEffect(item.Id, ActiveSpellEffectType.InitiativeBonus,
            2, 10, Beneficial: true));
        character.ApplySpellEffect(new ActiveSpellEffect(item.Id, ActiveSpellEffectType.HitBonus,
            1, 10, Beneficial: true));
        return $"víz +{character.WaterLevel - waterBefore}, +2 kezdeményezés és +1 találat 10 akcióig";
    }

    private string UseVisionItem(LiveCharacter character, MiscItemDefinition item)
    {
        character.ApplySpellEffect(new ActiveSpellEffect(item.Id, ActiveSpellEffectType.VisionBonus,
            item.EffectValue, 12, Beneficial: true));
        if (GetCharacterWorldPosition(character) is { } position) RevealFor(character, position);
        return $"látótáv +{item.EffectValue} 12 akcióig";
    }

    private static string UseHealing(LiveCharacter character, int amount)
    {
        var before = character.CurrentVitality;
        character.RestoreVitality(amount);
        return FormatHealingResult(character, amount, before);
    }

    private static string FormatHealingResult(LiveCharacter character, int requestedAmount, int vitalityBefore)
    {
        var actual = character.CurrentVitality - vitalityBefore;
        var adjusted = character.PreviewVitalityRecovery(requestedAmount);
        var penalties = character.Statuses
            .Where(status => status.VitalityRecoveryPercent < 100)
            .Select(status => $"{status.Icon} {status.VitalityRecoveryPercent}%")
            .ToArray();
        var reduction = adjusted < requestedAmount && penalties.Length > 0
            ? $" (állapotok csökkentették: {requestedAmount} → {adjusted}; {string.Join(" × ", penalties)})"
            : string.Empty;
        return $"❤️ +{actual} HP{reduction}";
    }

    private static string UseManaPotion(LiveCharacter character, int amount)
    {
        var before = character.CurrentMana;
        character.RestoreMana(amount);
        return $"manna +{character.CurrentMana - before}";
    }

    private static string ItemRarityName(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Magic => "Varázs",
        ItemRarity.Legendary => "Legendás",
        _ => "Sima"
    };

    private static ConsoleColor RarityColor(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Magic => ConsoleColor.Cyan,
        ItemRarity.Legendary => ConsoleColor.Yellow,
        _ => ConsoleColor.Gray
    };

    private static string ConsumableEffectName(ConsumableEffect effect) => effect switch
    {
        ConsumableEffect.Food => "élelem",
        ConsumableEffect.Water => "víz",
        ConsumableEffect.Heal => "HP",
        ConsumableEffect.RestoreMana => "manna",
        ConsumableEffect.CurePoison => "mérgezés gyógyítása",
        ConsumableEffect.CureDisease => "betegség gyógyítása",
        ConsumableEffect.StopBleeding => "vérzés elállítása",
        ConsumableEffect.Vision => "látótáv",
        _ => "nincs"
    };

    private static string MagicItemKindName(Domain.Magic.MagicItemKind kind) => kind switch
    {
        Domain.Magic.MagicItemKind.Amulet => "amulett",
        Domain.Magic.MagicItemKind.Wand => "varázspálca",
        Domain.Magic.MagicItemKind.Scroll => "varázstekercs",
        _ => "varázsgyűrű"
    };

    private static string MagicItemEffectName(Domain.Magic.MagicItemEffect effect) => effect switch
    {
        Domain.Magic.MagicItemEffect.Initiative => "kezdeményezés",
        Domain.Magic.MagicItemEffect.Hit => "találati próba",
        Domain.Magic.MagicItemEffect.Damage => "sebzés",
        Domain.Magic.MagicItemEffect.Defense => "védelem",
        Domain.Magic.MagicItemEffect.BattleHeal => "csata eleji HP",
        Domain.Magic.MagicItemEffect.BattleMana => "csata eleji manna",
        Domain.Magic.MagicItemEffect.Strength => "Erő",
        Domain.Magic.MagicItemEffect.Dexterity => "Ügyesség",
        Domain.Magic.MagicItemEffect.Health => "Egészség",
        Domain.Magic.MagicItemEffect.Intelligence => "Intelligencia",
        _ => "varázslattároló"
    };

    private static string NpcBehaviorName(NpcBehavior? behavior) => behavior switch
    {
        NpcBehavior.Defensive => "Defenzív",
        NpcBehavior.Aggressive => "Aggresszív",
        NpcBehavior.Scout => "Felderítő",
        NpcBehavior.Cautious => "Óvatos",
        _ => "inaktív"
    };

    private string AllowedClassNames(IReadOnlySet<string> classIds) => string.Join(", ",
        _gameData.CharacterClasses.Where(characterClass => classIds.Contains(characterClass.Id)).Select(characterClass => characterClass.Name));

    private void GrabOrPlaceInventoryItem()
    {
        var slot = _renderer.GetSelectedInventorySlot();
        if (slot is null) { _renderer.DrawInventoryMessage("Válassz egy felszerelés- vagy hátizsákhelyet.", ConsoleColor.DarkYellow); return; }
        var target = slot.Value;
        if (_heldInventoryItem is null)
        {
            var item = target.Character.GetInventoryItem(target.Kind, target.Index);
            if (item is null) { _renderer.DrawInventoryMessage("A kijelölt hely üres.", ConsoleColor.DarkYellow); return; }
            if (SpellcastingRules.IsSpellcastingFocus(item))
            { _renderer.DrawInventoryMessage($"A(z) {item.Name} a hátizsák első helyéhez kötött, ezért nem mozgatható.", ConsoleColor.Red); return; }
            _heldInventoryItem = new HeldInventoryItem(item, target, target.Character.InventoryRevision);
            _renderer.DrawInventoryMessage($"Kézben: {item.Name}. Válassz célhelyet, majd nyomj Space-t.", ConsoleColor.Yellow);
            return;
        }

        var held = _heldInventoryItem;
        if (target == held.Source)
        {
            _heldInventoryItem = null;
            _renderer.DrawInventoryMessage($"A(z) {held.Item.Name} áthelyezése megszakítva.", ConsoleColor.DarkYellow);
            return;
        }
        var commandId = _localCommandId + 1;
        var command = new InventoryTransferCommand(_session.HostPlayerId, commandId, held.Source.Character.Id,
            held.SourceRevision, held.Source.Kind, held.Source.Index, target.Character.Id,
            target.Character.InventoryRevision, target.Kind, target.Index);
        if (!_session.Submit(command)) return;
        _localCommandId = commandId;
        _heldInventoryItem = null;
    }

    private void CancelHeldInventoryItem()
    {
        if (_heldInventoryItem is not { } held) return;
        _heldInventoryItem = null;
        _renderer.DrawInventoryMessage($"A(z) {held.Item.Name} áthelyezése megszakítva.", ConsoleColor.DarkYellow);
    }

    private void SplitSelectedInventoryStack()
    {
        if (_heldInventoryItem is not null)
        {
            _renderer.DrawInventoryMessage("Előbb fejezd be vagy szakítsd meg a kézben tartott tárgy mozgatását.",
                ConsoleColor.DarkYellow);
            return;
        }
        var slot = _renderer.GetSelectedInventorySlot();
        if (slot is null || slot.Value.Kind != InventorySlotKind.Backpack)
        {
            _renderer.DrawInventoryMessage("Hátizsákban levő köteget jelölj ki a felezéshez.",
                ConsoleColor.DarkYellow);
            return;
        }
        var selected = slot.Value;
        var commandId = _localCommandId + 1;
        var command = new SplitInventoryStackCommand(_session.HostPlayerId, commandId,
            selected.Character.Id, selected.Character.InventoryRevision, selected.Index);
        if (!InventoryStackService.Validate(CharacterRoster.Party, command, out var error))
        {
            _renderer.DrawInventoryMessage(error, ConsoleColor.Red);
            return;
        }
        if (!_session.Submit(command)) return;
        _localCommandId = commandId;
    }

    private void ExecuteSplitInventoryStack(SplitInventoryStackCommand command)
    {
        if (!InventoryStackService.TryExecute(CharacterRoster.Party, command, out var result, out var error))
        {
            _renderer.DrawInventoryMessage(error, ConsoleColor.Red);
            return;
        }
        _renderer.RefreshCharacterSheet(SelectedCharacter);
        _renderer.DrawInventoryMessage(
            $"Köteg megfelezve: {result.ItemName} ({result.RemainingQuantity}+{result.NewQuantity}).",
            ConsoleColor.Green);
        PlaySessionSound(SoundEffect.Item, [command.CharacterId]);
    }

    private void DistributeSelectedInventoryStack()
    {
        if (_heldInventoryItem is not null)
        {
            _renderer.DrawInventoryMessage("Előbb fejezd be vagy szakítsd meg a kézben tartott tárgy mozgatását.",
                ConsoleColor.DarkYellow);
            return;
        }
        var slot = _renderer.GetSelectedInventorySlot();
        if (slot is null || slot.Value.Kind != InventorySlotKind.Backpack)
        {
            _renderer.DrawInventoryMessage("Elfogyasztható hátizsáktárgyat jelölj ki a szétosztáshoz.",
                ConsoleColor.DarkYellow);
            return;
        }
        var selected = slot.Value;
        var commandId = _localCommandId + 1;
        var command = new DistributeInventoryStackCommand(_session.HostPlayerId, commandId,
            selected.Character.Id, selected.Character.InventoryRevision, selected.Index);
        if (!InventoryDistributionService.Validate(CharacterRoster.Party, command, out var error))
        {
            _renderer.DrawInventoryMessage(error, ConsoleColor.Red);
            return;
        }
        if (!_session.Submit(command)) return;
        _localCommandId = commandId;
    }

    private void ExecuteDistributeInventoryStack(DistributeInventoryStackCommand command)
    {
        if (!InventoryDistributionService.TryExecute(CharacterRoster.Party, command, out var result, out var error))
        {
            _renderer.DrawInventoryMessage(error, ConsoleColor.Red);
            return;
        }
        _renderer.RefreshCharacterSheet(SelectedCharacter);
        var recipients = result.RecipientNames.Count == 0 ? string.Empty :
            $" → {string.Join(", ", result.RecipientNames)}";
        _renderer.DrawInventoryMessage(
            $"Szétosztva: {result.ItemName}, {result.DistributedQuantity} db{recipients}. " +
            $"A forráshelyen maradt: {result.RemainingSourceQuantity} db.", ConsoleColor.Green);
        RecordSessionActivity(SessionActivityKind.System,
            $"{result.ItemName} szétosztva a partyban ({result.DistributedQuantity} db).", ConsoleColor.Green);
        PlaySessionSound(SoundEffect.Item);
    }

    private void ExecuteInventoryTransfer(InventoryTransferCommand command)
    {
        if (!InventoryTransferService.TryExecute(CharacterRoster.Party, command, out var result, out var error))
        {
            _renderer.DrawInventoryMessage(error, ConsoleColor.Red);
            return;
        }
        _renderer.RefreshCharacterSheet(SelectedCharacter);
        _renderer.DrawInventoryMessage(result.DisplacedItemName is null
            ? $"Áthelyezted: {result.SourceItemName}."
            : $"Felcserélted: {result.SourceItemName} ↔ {result.DisplacedItemName}.", ConsoleColor.Green);
        if (command.SenderId != _session.HostPlayerId && CharacterRoster.Party.Leader is { } leader)
        {
            var guestCharacter = _session.CharacterControls
                .Where(control => control.AssignedPlayerId == command.SenderId &&
                                  control.ConnectionState == PlayerConnectionState.Connected)
                .Select(control => CharacterRoster.Party.Members.FirstOrDefault(character =>
                    character.Id == control.CharacterId))
                .FirstOrDefault(character => character is not null);
            string? hostTransferMessage = null;
            if (command.DestinationCharacterId == leader.Id && command.CharacterId != leader.Id)
                hostTransferMessage = $"{guestCharacter?.Name ?? "A vendég"} átadta a hostnak: " +
                                      $"{result.SourceItemName}.";
            else if (command.CharacterId == leader.Id && command.DestinationCharacterId != leader.Id)
                hostTransferMessage = $"{guestCharacter?.Name ?? "A vendég"} elvette a hosttól: " +
                                      $"{result.SourceItemName}.";
            if (hostTransferMessage is not null)
            {
                _renderer.DrawInventoryMessage(hostTransferMessage, ConsoleColor.Yellow);
                RecordSessionActivity(SessionActivityKind.System, hostTransferMessage, ConsoleColor.Yellow,
                    [leader.Id]);
            }
        }
        PlaySessionSound(SoundEffect.Item, [command.CharacterId]);
    }

    private void MoveEnemies()
    {
        var now = DateTime.UtcNow;
        foreach (var enemy in _maze.Enemies.Where(enemy => _nextEnemyMoves.GetValueOrDefault(enemy) <= now)
                     .OrderBy(_ => _random.Next()).ToArray())
        {
            ScheduleNextEnemyMove(enemy, now);
            var spellTick = enemy.AdvanceSpellEffects(_random);
            if (spellTick.Damage > 0)
            {
                var spellNotes = new List<string>();
                ApplyExplorationSpellDamage(SelectedCharacter, enemy, spellTick.Damage, spellNotes);
                _renderer.DrawInventoryMessage(string.Join("; ", spellTick.Notes.Concat(spellNotes)), ConsoleColor.Magenta);
                if (enemy.CurrentHitPoints <= 0) continue;
            }
            if (spellTick.SkipAction) continue;
            var pursuitTarget = FindEnemyPursuitTarget(enemy);
            if (enemy.PursuitState == EnemyPursuitState.Pursuing &&
                enemy.PursuitTargetCharacterId is null && pursuitTarget is not null)
                enemy.ResolvePursuit(true, pursuitTarget.Value.Character.Id);
            if (enemy.PursuitState == EnemyPursuitState.Pursuing && pursuitTarget is null)
            {
                enemy.ResetPursuit();
                pursuitTarget = FindEnemyPursuitTarget(enemy);
            }
            if (enemy.PursuitState == EnemyPursuitState.Undecided && pursuitTarget is not null)
            {
                ResolveEnemyPursuit(enemy, pursuitTarget.Value.Character.Id);
                pursuitTarget = FindEnemyPursuitTarget(enemy);
            }

            Direction? direction = enemy.PursuitState == EnemyPursuitState.Pursuing && pursuitTarget is not null
                ? FindEnemyStepToward(enemy, pursuitTarget.Value.Position)
                : enemy.MovementProfile switch
                {
                    EnemyMovementProfile.Stationary => null,
                    EnemyMovementProfile.Patrol => enemy.PatrolDirection,
                    _ => Directions[_random.Next(Directions.Length)]
                };
            if (direction is null) continue;
            if (TryMoveEnemy(enemy, direction.Value))
            {
                if (_battleStarted) return;
                continue;
            }
            if (enemy.PursuitState != EnemyPursuitState.Pursuing && enemy.MovementProfile == EnemyMovementProfile.Patrol)
            {
                enemy.ReversePatrolDirection();
                if (TryMoveEnemy(enemy, enemy.PatrolDirection) && _battleStarted) return;
            }
        }
    }

    private (LiveCharacter Character, Position Position)? FindEnemyPursuitTarget(Enemy enemy)
    {
        var livingParty = LivingPartyWithPositions().ToArray();
        if (enemy.PursuitTargetCharacterId is { } targetId)
        {
            foreach (var candidate in livingParty)
                if (candidate.Character.Id == targetId)
                {
                    if (FogOfWar.CanSee(_maze, enemy.Position, candidate.Position,
                            enemy.Definition.VisionRange))
                    {
                        enemy.RefreshPursuitMemory();
                        return candidate;
                    }
                    if (enemy.TryRememberPursuitTarget()) return candidate;
                    enemy.ResetPursuit();
                    return null;
                }
            enemy.ResetPursuit();
            return null;
        }

        return EnemyTargeting.ChooseNearestVisible(enemy.Position, livingParty,
            position => FogOfWar.CanSee(_maze, enemy.Position, position, enemy.Definition.VisionRange), _random);
    }

    private void ResolveEnemyPursuit(Enemy observer, CharacterId targetCharacterId)
    {
        var pursue = _random.Next(100) < 60;
        var group = observer.GroupId is null
            ? [observer]
            : _maze.Enemies.Where(enemy => string.Equals(enemy.GroupId, observer.GroupId,
                StringComparison.Ordinal)).ToList();
        foreach (var enemy in group.Where(enemy => enemy.PursuitState == EnemyPursuitState.Undecided))
            enemy.ResolvePursuit(pursue, targetCharacterId);
    }

    private void InitializeEnemyMoveSchedule(DateTime from)
    {
        _nextEnemyMoves.Clear();
        foreach (var enemy in _maze.Enemies) ScheduleNextEnemyMove(enemy, from);
    }

    private void ScheduleNextEnemyMove(Enemy enemy, DateTime from) =>
        _nextEnemyMoves[enemy] = from + EnemyMoveInterval(enemy);

    private static TimeSpan EnemyMoveInterval(Enemy enemy)
    {
        var speed = Math.Max(1, enemy.EffectiveSpeed);
        return TimeSpan.FromMilliseconds((double)ZombieMoveIntervalMilliseconds * ZombieSpeed / speed);
    }

    private bool TryMoveEnemy(Enemy enemy, Direction direction)
    {
        var previousPosition = enemy.Position;
        var destination = previousPosition + direction;
        if (_maze.GetPartyMemberAt(destination) is { } encounteredMember)
        {
            if (_session.IsHumanControlled(encounteredMember.Character.Id))
                StartBattle(encounteredMember, enemy);
            else
                ResolveNpcBattle(encounteredMember, enemy);
            return true;
        }
        if (!_maze.TryMoveEnemy(enemy, destination)) return false;
        RevealFor(SelectedCharacter, _player.Position);
        _renderer.DrawEnemyMovement(_maze, _fogOfWar, previousPosition, enemy.Position, _player.Position);
        if (enemy.Position == _player.Position) StartBattle(enemy);
        return true;
    }

    private Direction? FindEnemyStepToward(Enemy enemy, Position target)
    {
        var queue = new Queue<Position>();
        var previous = new Dictionary<Position, Position>();
        queue.Enqueue(enemy.Position);
        previous[enemy.Position] = enemy.Position;
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == target) break;
            foreach (var direction in Directions)
            {
                var next = current + direction;
                if (previous.ContainsKey(next) || !CanEnemyPathThrough(next, target)) continue;
                previous[next] = current;
                queue.Enqueue(next);
            }
        }
        if (!previous.ContainsKey(target)) return null;
        var step = target;
        while (previous[step] != enemy.Position) step = previous[step];
        return Directions.First(direction => enemy.Position + direction == step);
    }

    private bool CanEnemyPathThrough(Position position, Position target)
    {
        if (position == target) return _maze.IsWalkable(position);
        if (!_maze.IsWalkable(position) || position == _maze.Entrance || position == _maze.Exit) return false;
        var occupant = _maze.GetObjectAt(position);
        return occupant is null or GroundItemPile or Corpse or PartyMemberAvatar ||
               Maze.IsPassableNeutralNpc(occupant);
    }

    private void MovePartyMembers()
    {
        var now = DateTime.UtcNow;
        if (_partyScatterUntil is { } scatterUntil && now >= scatterUntil)
        {
            _partyScatterUntil = null;
            _renderer.DrawDeveloperMessage(_partyHoldingPosition
                ? "A szétszóródás véget ért; a parti ismét helyben marad."
                : "A szétszóródás véget ért; a parti folytatja korábbi viselkedését.");
        }
        var isScattering = _partyScatterUntil is not null;
        if (_partyRegrouping) isScattering = false;
        if (_partyHoldingPosition && !isScattering && !_partyRegrouping) return;
        foreach (var member in _maze.PartyMembers.ToArray())
        {
            if (_session.IsHumanControlled(member.Character.Id)) continue;
            if (_nextPartyMoves.GetValueOrDefault(member) > now) continue;
            ScheduleNextPartyMove(member, now);
            // Allow NPCs to cast simple exploration spells (heals/cures) before moving
            TryNpcCastExplorationSpell(member);
            if (isScattering)
            {
                MovePartyMemberAwayFromLeader(member);
                continue;
            }
            if (_partyRegrouping)
            {
                MovePartyMemberTowardLeader(member);
                continue;
            }
            if (CanActivelyAttack(member) && TryResolveAdjacentNpcBattle(member)) continue;
            var previous = member.Position;
            var next = ChoosePartyMemberStep(member);
            if (next is null || !CanEnterTrap(member.Character, next.Value) ||
                !_maze.TryMovePartyMember(member, next.Value, _player.Position)) continue;
            member.Character.RegisterExplorationStep();
            var newlyRevealed = RevealFor(member.Character, member.Position, advanceEnemyMemory: true);
            _renderer.DrawPartyMemberMovement(_maze, _fogOfWar, previous, member.Position, newlyRevealed, _player.Position);
            CheckBossDiscoveryAt(newlyRevealed);
            TriggerTrapAt(member.Character, member.Position);
            if (CanActivelyAttack(member)) TryResolveAdjacentNpcBattle(member);
        }
    }

    private void TogglePartyHoldPosition()
    {
        _partyHoldingPosition = !_partyHoldingPosition;
        if (_partyHoldingPosition)
        {
            _partyRegrouping = false;
            _partyAttackMode = false;
            _partyScatterUntil = null;
        }
        if (!_partyHoldingPosition)
            foreach (var member in _maze.PartyMembers) _nextPartyMoves[member] = DateTime.UtcNow;
        AnnouncePartyCommand(_partyHoldingPosition
            ? "✋ MEGÁLLJ: minden NPC társ azonnal tartja a helyét; a Támadás és Gyülekező kikapcsolt."
            : "✋ A Megállj parancs kikapcsolt; az NPC társak folytatják saját viselkedésüket.",
            _partyHoldingPosition ? ConsoleColor.Yellow : ConsoleColor.Gray);
    }

    private void TogglePartyRegrouping()
    {
        _partyRegrouping = !_partyRegrouping;
        if (_partyRegrouping)
        {
            _partyHoldingPosition = false;
            _partyAttackMode = false;
            _partyScatterUntil = null;
        }
        foreach (var member in _maze.PartyMembers) _nextPartyMoves[member] = DateTime.UtcNow;
        AnnouncePartyCommand(_partyRegrouping
            ? "🛡️ GYÜLEKEZŐ: minden NPC társ harc keresése nélkül a vezér mellé zárkózik és ott marad; a Támadás és Megállj kikapcsolt."
            : "🛡️ A Gyülekező kikapcsolt; az NPC társak folytatják saját viselkedésüket.",
            _partyRegrouping ? ConsoleColor.Cyan : ConsoleColor.Gray);
    }

    private void TogglePartyAttackMode()
    {
        _partyAttackMode = !_partyAttackMode;
        if (_partyAttackMode)
        {
            _partyHoldingPosition = false;
            _partyRegrouping = false;
            _partyScatterUntil = null;
        }
        foreach (var member in _maze.PartyMembers) _nextPartyMoves[member] = DateTime.UtcNow;
        AnnouncePartyCommand(_partyAttackMode
            ? "⚔️ TÁMADÁS: minden NPC társ agresszívan keresi és támadja az ellenfeleket a parancs kikapcsolásáig."
            : "⚔️ A Támadás kikapcsolt; az NPC társak visszatértek saját viselkedésükhöz.",
            _partyAttackMode ? ConsoleColor.Red : ConsoleColor.Gray);
    }

    private void AnnouncePartyCommand(string message, ConsoleColor color)
    {
        _renderer.DrawDeveloperMessage(message);
        RecordSessionActivity(SessionActivityKind.System, message, color);
    }

    private void ScatterPartyTemporarily()
    {
        _partyHoldingPosition = false;
        _partyRegrouping = false;
        _partyAttackMode = false;
        _partyScatterUntil = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        foreach (var member in _maze.PartyMembers)
            _nextPartyMoves[member] = DateTime.UtcNow + TimeSpan.FromMilliseconds(_random.Next(0, 100));
        AnnouncePartyCommand("Partiparancs: szétszóródás 10 másodpercig; a Támadás, Gyülekező és Megállj kikapcsolt.", ConsoleColor.Magenta);
    }

    private void MovePartyMemberTowardLeader(PartyMemberAvatar member)
    {
        if (Manhattan(member.Position, _player.Position) <= 1) return;
        var next = FindNextStep(member, FreeNeighborsOf(_player.Position))
                   ?? FollowLeaderTrail(member, minimumLag: 1);
        if (next is null) return;
        var previous = member.Position;
        if (!CanEnterTrap(member.Character, next.Value) ||
            !_maze.TryMovePartyMember(member, next.Value, _player.Position)) return;
        member.Character.RegisterExplorationStep();
        var newlyRevealed = RevealFor(member.Character, member.Position, advanceEnemyMemory: true);
        _renderer.DrawPartyMemberMovement(_maze, _fogOfWar, previous, member.Position, newlyRevealed,
            _player.Position);
        CheckBossDiscoveryAt(newlyRevealed);
        TriggerTrapAt(member.Character, member.Position);
    }

    private void MovePartyMemberAwayFromLeader(PartyMemberAvatar member)
    {
        if (Manhattan(member.Position, _player.Position) >= 10) return;
        var target = FindReachablePositions(member, 12)
            .Where(entry => Manhattan(entry.Position, _player.Position) <= 10)
            .OrderByDescending(entry => Manhattan(entry.Position, _player.Position))
            .ThenBy(entry => entry.Distance)
            .FirstOrDefault();
        if (target == default) return;
        var next = FindNextStep(member, [target.Position]);
        if (next is null) return;
        var previous = member.Position;
        if (!CanEnterTrap(member.Character, next.Value) ||
            !_maze.TryMovePartyMember(member, next.Value, _player.Position)) return;
        member.Character.RegisterExplorationStep();
        var newlyRevealed = RevealFor(member.Character, member.Position, advanceEnemyMemory: true);
        _renderer.DrawPartyMemberMovement(_maze, _fogOfWar, previous, member.Position, newlyRevealed, _player.Position);
        TriggerTrapAt(member.Character, member.Position);
    }

    private bool CanActivelyAttack(PartyMemberAvatar member) => _partyAttackMode ||
        member.Character.NpcBehavior is NpcBehavior.Defensive or NpcBehavior.Aggressive;

    private bool TryResolveAdjacentNpcBattle(PartyMemberAvatar member)
    {
        var enemy = Directions.Select(direction => _maze.GetEnemyAt(member.Position + direction))
            .FirstOrDefault(candidate => candidate is not null);
        if (enemy is null) return false;
        ResolveNpcBattle(member, enemy);
        return true;
    }

    private void ResolveNpcBattle(PartyMemberAvatar member, Enemy enemy)
    {
        if (_battleStarted || !member.Character.IsAlive || !_maze.Enemies.Contains(enemy)) return;
        _battleStarted = true;
        _session.SetPhase(GameSessionPhase.Battle);
        _turnUndeadUsedThisBattle.Clear();
        PlaySessionSound(SoundEffect.BattleStart);
        var startingNpcHp = member.Character.CurrentVitality;
        var startingNpcMana = member.Character.CurrentMana;
        var startingStatusIds = member.Character.Statuses.Select(status => status.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var spellsCast = 0;
        var knightProtector = TryRollKnightProtector(member.Character);
        var result = _battleSystem.Resolve(member.Character, enemy, _ => { },
            () => ChooseNpcBattlePlayerAction(member, enemy, onSpellCast: () => spellsCast++),
            knightProtector: knightProtector);
        var needLoss = DrainNeedsAfterBattle(member.Character, enemy.Definition.StrengthTier);
        TryNpcUseConsumables(member.Character);
        var gainedStatusIcons = member.Character.Statuses
            .Where(status => !startingStatusIds.Contains(status.Id))
            .Select(status => status.Icon).ToArray();
        var vitalityLost = Math.Max(0, startingNpcHp - member.Character.CurrentVitality);
        var manaLost = Math.Max(0, startingNpcMana - member.Character.CurrentMana);
        var levelUps = new List<ExperienceAward>();
        if (result.PlayerWon)
        {
            AwardBossKey(enemy);
            PlayBattleVictorySound();
            var experienceAwards = DistributeExperience(member.Character, enemy.Definition.ExperienceReward);
            levelUps.AddRange(experienceAwards.Where(award => award.Result.LeveledUp && award.Character.IsAlive));
            RegisterNpcQuestKill(enemy.Definition.Id);
            _maze.ReplaceEnemyWithCorpse(enemy);
            var summary = ConsoleRenderer.FormatAutoBattleVictorySummary(result, member.Character.Name, enemy,
                vitalityLost, manaLost, gainedStatusIcons, needLoss, spellsCast, enemy.Definition.ExperienceReward);
            _renderer.DrawNpcBattleSummary(summary, ConsoleColor.Green);
            RecordSessionActivity(SessionActivityKind.Battle, summary, ConsoleColor.Green);
        }
        else
        {
            PlaySessionSound(SoundEffect.MemberKilled);
            _maze.ReplacePartyMemberWithCorpse(member);
            _nextPartyMoves.Remove(member);
            var summary = ConsoleRenderer.FormatAutoBattleDefeatSummary(result, member.Character.Name, enemy,
                startingNpcHp, manaLost, gainedStatusIcons, needLoss, spellsCast);
            _renderer.DrawNpcBattleSummary(summary, ConsoleColor.Red);
            RecordSessionActivity(SessionActivityKind.Battle, summary, ConsoleColor.Red);
        }
        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
        _renderer.RefreshCharacterSheet(SelectedCharacter);
        _battleStarted = false;
        if (levelUps.Count > 0)
        {
            foreach (var award in levelUps) ResolvePerkOffers(award.Character, award.Result);
            _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
        }
        _session.SetPhase(GameSessionPhase.Exploration);
    }

    private void ScheduleNextPartyMove(PartyMemberAvatar member, DateTime from)
    {
        var distance = Manhattan(member.Position, _player.Position);
        var minimumDelay = distance >= 8 ? CatchUpMoveDelayMilliseconds :
            distance >= 5 ? 130 : MinimumPartyMoveDelayMilliseconds;
        var maximumDelay = distance >= 8 ? CatchUpMoveDelayMilliseconds + 30 :
            distance >= 5 ? 170 : MaximumPartyMoveDelayMilliseconds;
        _nextPartyMoves[member] = from + TimeSpan.FromMilliseconds(_random.Next(minimumDelay, maximumDelay + 1));
    }

    private Position? ChoosePartyMemberStep(PartyMemberAvatar member)
    {
        var behavior = _partyAttackMode ? NpcBehavior.Aggressive :
            member.Character.NpcBehavior ?? NpcBehavior.Defensive;
        var visibleEnemy = _maze.Enemies
            .Where(enemy => FogOfWar.CanSee(_maze, member.Position, enemy.Position,
                CharacterClassRules.VisionRange(member.Character, CurrentLevelVisionModifier)))
            .OrderBy(enemy => Manhattan(member.Position, enemy.Position))
            .FirstOrDefault();

        if (behavior == NpcBehavior.Aggressive && visibleEnemy is not null)
        {
            if (Manhattan(member.Position, visibleEnemy.Position) == 1) return null;
            return FindNextStep(member, FreeNeighborsOf(visibleEnemy.Position));
        }

        if (behavior == NpcBehavior.Defensive && visibleEnemy is not null)
        {
            if (Manhattan(member.Position, visibleEnemy.Position) == 1) return null;
            return FindNextStep(member, FreeNeighborsOf(visibleEnemy.Position));
        }

        if (behavior == NpcBehavior.Scout)
        {
            if (visibleEnemy is not null)
                return FollowLeaderTrail(member, minimumLag: 2);
            return ChooseForwardStep(member, maximumLeaderDistance: 10, maximumSearchDistance: 10, avoidNarrowFront: false)
                ?? FollowLeaderTrail(member, minimumLag: 2);
        }

        if (behavior == NpcBehavior.Cautious)
            return FollowLeaderTrail(member, minimumLag: 2);

        if (behavior == NpcBehavior.Aggressive)
            return ChooseForwardStep(member, maximumLeaderDistance: 3, maximumSearchDistance: 4, avoidNarrowFront: true)
                ?? FollowLeaderTrail(member, minimumLag: 2);

        return FollowLeaderTrail(member, minimumLag: 2);
    }

    private Position? FollowLeaderTrail(PartyMemberAvatar member, int minimumLag)
    {
        if (_leaderTrail.Count == 0) return null;
        var partyOrder = Enumerable.Range(0, _maze.PartyMembers.Count)
            .FirstOrDefault(index => _maze.PartyMembers[index] == member);
        var formationLag = Math.Min(1, partyOrder);
        var targetIndex = Math.Max(0, _leaderTrail.Count - 1 - minimumLag - formationLag);
        for (var index = targetIndex; index >= 0; index--)
        {
            var target = _leaderTrail[index];
            if (target == member.Position) return null;
            if (!CanPartyTraverse(member, target)) continue;
            return FindNextStep(member, [target]);
        }
        return null;
    }

    private Position? ChooseForwardStep(PartyMemberAvatar member, int maximumLeaderDistance, int maximumSearchDistance, bool avoidNarrowFront)
    {
        var forward = DirectionOffset(_leaderFacing);
        var reachable = FindReachablePositions(member, maximumSearchDistance)
            .Where(entry => Manhattan(entry.Position, _player.Position) <= maximumLeaderDistance)
            .Select(entry => new
            {
                entry.Position,
                entry.Distance,
                Progress = (entry.Position.X - _player.Position.X) * forward.X + (entry.Position.Y - _player.Position.Y) * forward.Y
            })
            .Where(entry => entry.Progress > 0)
            .Where(entry => !avoidNarrowFront || CountWalkableNeighbors(entry.Position) >= 3)
            .OrderByDescending(entry => entry.Progress)
            .ThenBy(entry => entry.Distance)
            .FirstOrDefault();
        if (reachable is null) return null;
        var step = FindNextStep(member, [reachable.Position]);
        if (avoidNarrowFront && step is { } narrowStep && IsAheadOfLeader(narrowStep) && CountWalkableNeighbors(narrowStep) <= 2)
            return null;
        return step;
    }

    private Position? FindNextStep(PartyMemberAvatar member, IEnumerable<Position> targetPositions)
    {
        var targets = targetPositions.Where(position => CanPartyTraverse(member, position)).ToHashSet();
        if (targets.Count == 0 || targets.Contains(member.Position)) return null;
        var visited = new HashSet<Position> { member.Position };
        var queue = new Queue<(Position Position, Position FirstStep)>();
        foreach (var direction in Directions)
        {
            var next = member.Position + direction;
            if (!CanPartyTraverse(member, next) || !visited.Add(next)) continue;
            queue.Enqueue((next, next));
        }
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (targets.Contains(current.Position)) return current.FirstStep;
            foreach (var direction in Directions)
            {
                var next = current.Position + direction;
                if (!CanPartyTraverse(member, next) || !visited.Add(next)) continue;
                queue.Enqueue((next, current.FirstStep));
            }
        }
        return null;
    }

    private IReadOnlyList<(Position Position, int Distance)> FindReachablePositions(PartyMemberAvatar member, int maximumDistance)
    {
        var result = new List<(Position, int)> { (member.Position, 0) };
        var visited = new HashSet<Position> { member.Position };
        var queue = new Queue<(Position Position, int Distance)>();
        queue.Enqueue((member.Position, 0));
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.Distance >= maximumDistance) continue;
            foreach (var direction in Directions)
            {
                var next = current.Position + direction;
                if (!CanPartyTraverse(member, next) || !visited.Add(next)) continue;
                var distance = current.Distance + 1;
                result.Add((next, distance));
                queue.Enqueue((next, distance));
            }
        }
        return result;
    }

    private IEnumerable<Position> FreeNeighborsOf(Position origin) => Directions
        .Select(direction => origin + direction)
        .Where(position => _maze.IsWalkable(position) && position != _player.Position &&
                           (_maze.GetObjectAt(position) is null or GroundItemPile or Corpse ||
                            Maze.IsPassableNeutralNpc(_maze.GetObjectAt(position))));

    private bool CanPartyTraverse(PartyMemberAvatar member, Position position)
    {
        if (!_maze.IsWalkable(position) || position == _player.Position) return false;
        var occupant = _maze.GetObjectAt(position);
        return occupant is null or GroundItemPile or Corpse || occupant == member ||
               Maze.IsPassableNeutralNpc(occupant);
    }

    private int CountWalkableNeighbors(Position position) => Directions.Count(direction => _maze.IsWalkable(position + direction));
    private bool IsAheadOfLeader(Position position)
    {
        var forward = DirectionOffset(_leaderFacing);
        return (position.X - _player.Position.X) * forward.X + (position.Y - _player.Position.Y) * forward.Y > 0;
    }
    private static int Manhattan(Position first, Position second) => Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y);
    private static (int X, int Y) DirectionOffset(Direction direction) => direction switch
    {
        Direction.Up => (0, -1),
        Direction.Down => (0, 1),
        Direction.Left => (-1, 0),
        _ => (1, 0)
    };

    private void HandleLocalBattleInput(ConsoleKeyInfo key)
    {
        if (_activeBattleState is null || _activeBattleState.IsCompleted) return;
        if (_activeBattleState.PlayerCharacterId != SelectedCharacter.Id) return;
        if (IsHelpShortcut(key))
        {
            var helpEnemy = _activeBattleState.Enemy;
            ShowInGameHelp();
            _renderer.DrawBattleStarted(helpEnemy);
            _renderer.RefreshBattleStatusRows();
            DrawBattleActionPrompt(helpEnemy);
            return;
        }
        if (!_activeBattleState.IsPlayerTurn)
        {
            if (key.Key == ConsoleKey.Spacebar)
                SubmitLocalBattleCommand(BattleActionKind.AdvanceEnemyTurn);
            return;
        }
        var enemy = _activeBattleState.Enemy;
        var canTurnUndead = CanTurnUndead(SelectedCharacter, enemy) &&
                            !_turnUndeadUsedThisBattle.Contains(SelectedCharacter);
        if (IsSaveGameShortcut(key))
        {
            _saveAfterBattle = true;
            _renderer.DrawInventoryMessage("Mentés kérve: a csata lezárása után automatikusan elkészül.", ConsoleColor.Yellow);
            return;
        }
        if (_activeBattleState.IsAwaitingTacticSelection && key.Key is ConsoleKey.D1 or ConsoleKey.NumPad1 or
                ConsoleKey.D2 or ConsoleKey.NumPad2 or ConsoleKey.D3 or ConsoleKey.NumPad3)
        {
            var option = key.Key is ConsoleKey.D1 or ConsoleKey.NumPad1 ? 1 :
                key.Key is ConsoleKey.D2 or ConsoleKey.NumPad2 ? 2 : 3;
            SubmitLocalBattleCommand(TacticActionFor(SelectedCharacter.CharacterClass.Id, option));
            return;
        }
        if (key.Key == ConsoleKey.Spacebar)
        {
            SubmitLocalBattleCommand(BattleActionKind.PhysicalAttack);
            return;
        }
        if (key.Key == ConsoleKey.T && canTurnUndead)
        {
            SubmitLocalBattleCommand(BattleActionKind.TurnUndead);
            return;
        }

        SpellDefinition? spell = null;
        MagicItemDefinition? castingItem = null;
        int? castingItemSlotIndex = null;
        if (key.Key == ConsoleKey.V)
        {
            var selection = _renderer.DrawSpellCastingScreen([SelectedCharacter], 0, inCombat: true, _maze, _fogOfWar,
                _ => _player.Position, () =>
            {
                ShowInGameHelp();
                _renderer.DrawBattleStarted(enemy);
                _renderer.RefreshBattleStatusRows();
            });
            _renderer.RestoreSpellCastingOverlay();
            spell = selection?.Spell;
            castingItem = selection?.CastingItem;
            castingItemSlotIndex = selection?.CastingItemSlotIndex;
        }
        else if (TryGetQuickSpellIndex(key, out var slotIndex))
            spell = SelectedCharacter.QuickSpells[slotIndex];
        else
            return;

        if (spell is null)
        {
            if (key.Key != ConsoleKey.V)
                _renderer.DrawInventoryMessage("Ez a varázslat-gyorshely üres.", ConsoleColor.DarkYellow);
            DrawBattleActionPrompt(enemy);
            return;
        }
        var validation = ValidateSpellCast(SelectedCharacter, _player.Position, spell, inCombat: true, enemy,
            castingItem, castingItemSlotIndex);
        if (validation is not null)
        {
            _renderer.DrawInventoryMessage(validation.Message, ConsoleColor.Red);
            DrawBattleActionPrompt(enemy);
            return;
        }
        var target = SelectSpellTarget(SelectedCharacter, _player.Position, spell, enemy);
        if (target is null)
        {
            DrawBattleActionPrompt(enemy);
            return;
        }
        SubmitLocalBattleCommand(BattleActionKind.CastSpell, spell.Id, castingItemSlotIndex, target);
    }

    private void SubmitLocalBattleCommand(BattleActionKind action, string? spellId = null,
        int? castingItemSlotIndex = null, Position? target = null)
    {
        if (_activeBattleState is null) return;
        var commandId = _localCommandId + 1;
        if (_session.Submit(new BattleActionCommand(_session.HostPlayerId, commandId, SelectedCharacter.Id,
                _activeBattleState.Id, _activeBattleState.TurnId, action, spellId, castingItemSlotIndex, target)))
            _localCommandId = commandId;
    }

    private void DrawBattleActionPrompt(Enemy enemy)
    {
        if (_activeBattleState is not { } state) return;
        PublishBattleControlHintOnce(state, enemy);
    }

    private void PublishBattleControlHintOnce(BattleState state, Enemy enemy)
    {
        string? message = null;
        if (state.IsAwaitingTacticSelection && !_battleTacticHintLogged)
        {
            _battleTacticHintLogged = true;
            message = BattlePromptText.Tactic(state.Player.CharacterClass.Id, GetBattleTacticOptions(state));
        }
        else if (state.IsPlayerTurn && !state.IsAwaitingTacticSelection && !_battleActionHintLogged)
        {
            _battleActionHintLogged = true;
            var character = state.Player;
            var position = GetCasterPosition(character);
            message = BattlePromptText.PlayerAction(HasUsableCombatSpell(character, position, enemy),
                CanTurnUndead(character, enemy) && !_turnUndeadUsedThisBattle.Contains(character));
        }
        if (message is null) return;
        RecordSessionActivity(SessionActivityKind.System, message, ConsoleColor.Yellow, [state.PlayerCharacterId]);
        if (state.Player == SelectedCharacter)
            _renderer.DrawInventoryMessage(message, ConsoleColor.Yellow);
    }

    private static BattleActionKind TacticActionFor(string characterClassId, int option) =>
        characterClassId == CharacterClassIds.Harcos
            ? option switch
            {
                1 => BattleActionKind.FighterPrecise,
                2 => BattleActionKind.FighterPowerful,
                _ => BattleActionKind.FighterDefensive
            }
            : option switch
            {
                1 => BattleActionKind.ThiefAmbush,
                2 => BattleActionKind.ThiefObserve,
                _ => BattleActionKind.ThiefPoison
            };

    private void ExecuteBattleAction(BattleActionCommand command)
    {
        if (_activeBattleState is null || command.BattleId != _activeBattleState.Id ||
            command.TurnId != _activeBattleState.TurnId || command.CharacterId != _activeBattleState.PlayerCharacterId) return;
        var battleCharacter = _activeBattleState.Player;
        switch (command.Action)
        {
            case BattleActionKind.FighterPrecise:
            case BattleActionKind.FighterPowerful:
            case BattleActionKind.FighterDefensive:
            case BattleActionKind.ThiefAmbush:
            case BattleActionKind.ThiefObserve:
            case BattleActionKind.ThiefPoison:
                if (_activeBattleState.TryChooseTactic(ToBattleTactic(command.Action)))
                {
                    _renderer.DrawInventoryMessage($"Harci taktika: {BattleTacticName(_activeBattleState.Tactic!.Value, battleCharacter)}.", ConsoleColor.Cyan);
                    ContinueActiveBattle();
                }
                else RejectBattleAction(command, "Ez a harci taktika most nem választható.");
                break;
            case BattleActionKind.PhysicalAttack:
                ResolveActiveBattleAction(null);
                break;
            case BattleActionKind.AdvanceEnemyTurn:
                if (!_activeBattleState.IsPlayerTurn)
                    ResolveActiveEnemyTurn();
                else
                    RejectBattleAction(command, "Az ellenfél körét most nem lehet léptetni.");
                break;
            case BattleActionKind.TurnUndead:
                if (CanTurnUndead(battleCharacter, _activeBattleState.Enemy) &&
                    !_turnUndeadUsedThisBattle.Contains(battleCharacter))
                    ResolveActiveBattleAction(ResolveTurnUndead(battleCharacter, _activeBattleState.Enemy));
                else
                    RejectBattleAction(command, "A halottűzés ebben a körben nem használható.");
                break;
            case BattleActionKind.CastSpell:
                ExecuteSpellBattleAction(command);
                break;
        }
    }

    private void ExecuteSpellBattleAction(BattleActionCommand command)
    {
        if (_activeBattleState is null || command.SpellId is null || command.Target is null) return;
        var battleCharacter = _activeBattleState.Player;
        var spell = _gameData.Spells.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, command.SpellId, StringComparison.OrdinalIgnoreCase));
        if (spell is null)
        {
            RejectBattleAction(command, "Ismeretlen varázslat.");
            return;
        }
        MagicItemDefinition? castingItem = null;
        if (command.CastingItemSlotIndex is { } slot)
        {
            if (slot is < 0 or >= LiveCharacter.MaximumMagicItemCount)
            {
                RejectBattleAction(command, "Érvénytelen varázstárgy-hely.");
                return;
            }
            castingItem = battleCharacter.MagicItems[slot];
            if (castingItem is null)
            {
                RejectBattleAction(command, "A kiválasztott varázstárgy-hely üres.");
                return;
            }
        }
        var attempt = TryCastSpell(battleCharacter, GetCasterPosition(battleCharacter), spell, inCombat: true,
            _activeBattleState.Enemy, castingItem, command.CastingItemSlotIndex, command.Target);
        if (attempt is null || !attempt.ConsumesTurn)
        {
            RejectBattleAction(command, attempt?.Message ?? "A varázslat célpontja érvénytelen.");
            return;
        }
        ResolveActiveBattleAction(new BattlePlayerAction(attempt.Message, attempt.Kind,
            attempt.DamageToCurrentEnemy, attempt.ExtraPlayerActions), spellCanKillEnemy: attempt.DamageToCurrentEnemy > 0);
    }

    private void TryAssignKnightProtection(BattleState state, LiveCharacter protectedCharacter)
    {
        var knight = TryRollKnightProtector(protectedCharacter);
        if (knight is null) return;
        state.SetKnightProtection(knight);
        _renderer.DrawInventoryMessage($"🛡️ {knight.Name} készen áll közbelépni: a társ első találatát teljesen kivédi, " +
                                       "de a sebzés harmadát ő kapja.",
            ConsoleColor.Cyan);
    }

    private LiveCharacter? TryRollKnightProtector(LiveCharacter protectedCharacter)
    {
        var protectedPosition = GetCasterPosition(protectedCharacter);
        var knight = LivingPartyWithPositions()
            .Where(entry => entry.Character != protectedCharacter && entry.Character.IsAlive &&
                            entry.Character.CharacterClass.Id == CharacterClassIds.Lovag &&
                            Chebyshev(entry.Position, protectedPosition) <= 2)
            .OrderBy(entry => Chebyshev(entry.Position, protectedPosition))
            .Select(entry => entry.Character).FirstOrDefault();
        var chance = knight?.HasClassFeatureUpgrade(ClassFeatureUpgrades.KnightBodyguard) == true ? 90 : 75;
        return knight is not null && _random.Next(100) < chance ? knight : null;
    }

    private static BattleTactic ToBattleTactic(BattleActionKind action) => action switch
    {
        BattleActionKind.FighterPrecise => BattleTactic.FighterPrecise,
        BattleActionKind.FighterPowerful => BattleTactic.FighterPowerful,
        BattleActionKind.FighterDefensive => BattleTactic.FighterDefensive,
        BattleActionKind.ThiefAmbush => BattleTactic.ThiefAmbush,
        BattleActionKind.ThiefObserve => BattleTactic.ThiefObserve,
        BattleActionKind.ThiefPoison => BattleTactic.ThiefPoison,
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };

    private static string BattleTacticName(BattleTactic tactic, LiveCharacter character) => tactic switch
    {
        BattleTactic.FighterPrecise => $"Pontos állás (+2 találat, ×{(character.HasClassFeatureUpgrade(ClassFeatureUpgrades.FighterPrecise) ? "0,85" : "0,75")} sebzés)",
        BattleTactic.FighterPowerful => $"Erőteljes állás (-1 találat, ×1,25 sebzés, {(character.HasClassFeatureUpgrade(ClassFeatureUpgrades.FighterPowerful) ? 75 : 50)}% páncéltörés)",
        BattleTactic.FighterDefensive => $"Védekező állás (×0,75 sebzés, +{(character.HasClassFeatureUpgrade(ClassFeatureUpgrades.FighterDefensive) ? 4 : 3)} védelem)",
        BattleTactic.ThiefAmbush => "Orvtámadás (az első sikeres támadás dupla sebzés)",
        BattleTactic.ThiefObserve => "Megfigyelés (+2 találat)",
        BattleTactic.ThiefPoison => "Mérgezett penge (+1-4 sebzés találatonként)",
        _ => tactic.ToString()
    };

    private IReadOnlyList<BattleTacticOptionSnapshot>? GetBattleTacticOptions(BattleState state)
    {
        if (!state.IsAwaitingTacticSelection) return null;
        if (state.Player.CharacterClass.Id != CharacterClassIds.Harcos) return null;
        return
        [
            new(BattleActionKind.FighterPrecise, "🎯 Pontos",
                $"sebzés ×{(state.Player.HasClassFeatureUpgrade(ClassFeatureUpgrades.FighterPrecise) ? "0,85" : "0,75")}",
                _battleSystem.EstimatePlayerHitChance(state.Player, state.Enemy, BattleTactic.FighterPrecise)),
            new(BattleActionKind.FighterPowerful, "💥 Erőteljes",
                $"sebzés ×1,25, {(state.Player.HasClassFeatureUpgrade(ClassFeatureUpgrades.FighterPowerful) ? "negyed" : "fél")} páncél",
                _battleSystem.EstimatePlayerHitChance(state.Player, state.Enemy, BattleTactic.FighterPowerful)),
            new(BattleActionKind.FighterDefensive, "🛡️ Védekező",
                $"sebzés ×0,75, védelem +{(state.Player.HasClassFeatureUpgrade(ClassFeatureUpgrades.FighterDefensive) ? 4 : 3)}",
                _battleSystem.EstimatePlayerHitChance(state.Player, state.Enemy, BattleTactic.FighterDefensive))
        ];
    }

    private void RejectBattleAction(BattleActionCommand command, string message)
    {
        _session.RejectExecutedCommand(command, message);
        _renderer.DrawInventoryMessage(message, ConsoleColor.Red);
        if (_activeBattleState is { IsCompleted: false, IsPlayerTurn: true } state)
        {
            var battleCharacter = state.Player;
            _session.SetBattlePrompt(state.Id, state.TurnId, state.PlayerCharacterId,
                GetAllowedBattleActions(battleCharacter, GetCasterPosition(battleCharacter), state.Enemy));
            if (battleCharacter == SelectedCharacter) DrawBattleActionPrompt(state.Enemy);
        }
    }

    private IReadOnlyList<BattleActionKind> GetAllowedBattleActions(LiveCharacter character,
        Position characterPosition, Enemy enemy)
    {
        if (_activeBattleState is { IsCompleted: false, IsPlayerTurn: false } enemyTurn &&
            enemyTurn.PlayerCharacterId == character.Id)
            return [BattleActionKind.AdvanceEnemyTurn];
        if (_activeBattleState is { IsAwaitingTacticSelection: true } state && state.PlayerCharacterId == character.Id)
            return character.CharacterClass.Id == CharacterClassIds.Harcos
                ? [BattleActionKind.FighterPrecise, BattleActionKind.FighterPowerful, BattleActionKind.FighterDefensive]
                : [BattleActionKind.ThiefAmbush, BattleActionKind.ThiefObserve, BattleActionKind.ThiefPoison];
        var actions = new List<BattleActionKind> { BattleActionKind.PhysicalAttack };
        if (HasUsableCombatSpell(character, characterPosition, enemy)) actions.Add(BattleActionKind.CastSpell);
        if (CanTurnUndead(character, enemy) && !_turnUndeadUsedThisBattle.Contains(character))
            actions.Add(BattleActionKind.TurnUndead);
        return actions;
    }

    private IReadOnlyList<BattleSpellOption> GetSpellOptions(LiveCharacter character,
        Position characterPosition, Enemy? enemy, bool inCombat)
    {
        return character.MemorizedSpells
            .Where(spell => inCombat ? spell.CanUseInCombat : spell.CanUseDuringExploration)
            .Select(spell => (Spell: spell, Item: (MagicItemDefinition?)null, Slot: (int?)null))
            .Concat(character.MagicItems.Select((item, index) => (Item: item, Index: index))
                .Where(entry => entry.Item?.Kind is MagicItemKind.Scroll or MagicItemKind.Wand &&
                                entry.Item.SpellId is not null && character.MagicItemCharges[entry.Index] > 0)
                .Select(entry => (Spell: _gameData.GetSpell(entry.Item!.SpellId!), Item: (MagicItemDefinition?)entry.Item,
                    Slot: (int?)entry.Index))
                .Where(entry => (inCombat ? entry.Spell.CanUseInCombat : entry.Spell.CanUseDuringExploration) &&
                                SpellcastingRules.CanUseCastingItem(character, entry.Item!, entry.Spell)))
            .OrderBy(entry => entry.Spell.Level).ThenBy(entry => entry.Spell.Name)
            .ThenBy(entry => entry.Item is not null)
            .Select(entry =>
            {
                var targets = entry.Spell.TargetType is SpellTargetType.Self or SpellTargetType.Party
                    ? HasValidSpellTarget(character, characterPosition, entry.Spell, enemy)
                        ? new[] { characterPosition }
                        : []
                    : GetValidSpellTargets(characterPosition, entry.Spell, enemy).Distinct().ToArray();
                var quickIndex = character.QuickSpells.ToList().FindIndex(candidate =>
                    string.Equals(candidate?.Id, entry.Spell.Id, StringComparison.OrdinalIgnoreCase));
                return new BattleSpellOption(entry.Spell.Id, entry.Spell.Name, entry.Spell.Level,
                    entry.Item is null ? SpellcastingRules.EffectiveManaCost(character, entry.Spell) : 0,
                    entry.Spell.TargetType, entry.Spell.Range, entry.Spell.AreaRadius, entry.Slot,
                    entry.Item?.Kind, entry.Slot is { } slot ? character.MagicItemCharges[slot] : 0,
                    entry.Item is null && quickIndex >= 0 ? quickIndex : null, targets);
            }).ToArray();
    }

    private void ExecuteExplorationSpell(CastExplorationSpellCommand command)
    {
        var character = CharacterRoster.Party.Members.FirstOrDefault(member => member.Id == command.CharacterId);
        if (character is null || !character.IsAlive) return;
        var spell = _gameData.Spells.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, command.SpellId, StringComparison.OrdinalIgnoreCase));
        if (spell is null)
        {
            _session.RejectExecutedCommand(command, "Ismeretlen varázslat.");
            return;
        }
        MagicItemDefinition? castingItem = null;
        if (command.CastingItemSlotIndex is { } slot)
            castingItem = character.MagicItems.ElementAtOrDefault(slot);
        var result = TryCastSpell(character, GetCasterPosition(character), spell, inCombat: false,
            currentEnemy: null, castingItem: castingItem,
            castingItemSlotIndex: command.CastingItemSlotIndex, explicitTarget: command.Target);
        if (result is null || !result.ConsumesTurn)
        {
            _session.RejectExecutedCommand(command, result?.Message ?? "A varázslat célpontja érvénytelen.");
            return;
        }
        _renderer.RefreshCharacterSheet(SelectedCharacter);
        _renderer.DrawInventoryMessage(result.Message,
            result.Kind == BattleLogKind.Information ? ConsoleColor.Red : ConsoleColor.Magenta);
        RecordSessionActivity(SessionActivityKind.Spell, result.Message,
            result.Kind == BattleLogKind.Information ? ConsoleColor.Red : ConsoleColor.Magenta);
    }

    private bool HasUsableCombatSpell(LiveCharacter character, Position characterPosition, Enemy enemy) =>
        character.CanCastSpells && character.MemorizedSpells.Any(spell =>
                spell.CanUseInCombat && SpellcastingRules.EffectiveManaCost(character, spell) <= character.CurrentMana &&
                (!_timeStopUsedThisBattle || _gameData.GetSpellEffects(spell.Id).All(effect => effect.Type != SpellEffectType.ExtraActions)) &&
                HasValidSpellTarget(character, characterPosition, spell, enemy)) ||
        EquippedCastingItems(character).Any(item =>
            _gameData.GetSpell(item.SpellId!) is { } spell && spell.CanUseInCombat &&
            (!_timeStopUsedThisBattle || _gameData.GetSpellEffects(spell.Id).All(effect => effect.Type != SpellEffectType.ExtraActions)) &&
            HasValidSpellTarget(character, characterPosition, spell, enemy));

    private static bool CanTurnUndead(LiveCharacter character, Enemy enemy) =>
        character.CharacterClass.Id is CharacterClassIds.Pap or CharacterClassIds.Lovag &&
        enemy.Definition.AbilityIds.Contains(MonsterAbilityIds.Undead, StringComparer.OrdinalIgnoreCase);

    private BattlePlayerAction ResolveTurnUndead(LiveCharacter character, Enemy enemy)
    {
        _turnUndeadUsedThisBattle.Add(character);
        var priest = character.CharacterClass.Id == CharacterClassIds.Pap;
        var ability = priest ? character.EffectiveAbilities.Intelligence : character.EffectiveAbilities.Strength;
        var levelBonus = priest ? character.Level / 2 : character.Level / 3;
        var roll = _random.Next(1, 21);
        var total = roll + ability + levelBonus;
        var difficulty = 10 + enemy.Definition.StrengthTier * 2;
        var abilityName = priest ? "Halottűzés" : "Szent elűzés";
        if (total < difficulty)
            return new BattlePlayerAction($"{character.Name} megkísérli: {abilityName}, de az élőholt ellenáll " +
                $"({roll} + {ability} + {levelBonus} = {total}, cél {difficulty}).", BattleLogKind.Information);

        if (priest && total >= difficulty + 10 && enemy.Definition.StrengthTier <= 2 && enemy.GroupRole != EnemyGroupRole.Leader)
            return new BattlePlayerAction($"{character.Name} {abilityName} képessége szent fénnyel megsemmisíti {enemy.Name} ellenfelet " +
                $"({total}, cél {difficulty}+10).", BattleLogKind.PlayerAttack, enemy.CurrentHitPoints);

        if (priest)
        {
            enemy.ApplySpellEffect(new ActiveSpellEffect("TURN-UNDEAD", ActiveSpellEffectType.SkipNext, 0, 2));
            return new BattlePlayerAction($"{character.Name} sikeresen használja: {abilityName}. {enemy.Name} két akciót kihagy " +
                $"({total}, cél {difficulty}).", BattleLogKind.PlayerAttack);
        }

        var damage = _random.Next(1, 7) + character.Level / 2;
        enemy.ApplySpellEffect(new ActiveSpellEffect("HOLY-TURNING", ActiveSpellEffectType.SkipNext, 0, 1));
        character.ApplySpellEffect(new ActiveSpellEffect("HOLY-TURNING", ActiveSpellEffectType.DefenseBonus, 2, 2, Beneficial: true));
        return new BattlePlayerAction($"{character.Name} sikeresen használja: {abilityName}. {enemy.Name} -{damage} HP, " +
            "kihagyja következő akcióját; a Lovag +2 védelmet kap 2 akcióig " +
            $"({total}, cél {difficulty}).", BattleLogKind.PlayerAttack, damage);
    }

    private SpellCastAttempt? TryCastSpell(LiveCharacter caster, Position casterPosition, SpellDefinition spell,
        bool inCombat, Enemy? currentEnemy, MagicItemDefinition? castingItem = null, int? castingItemSlotIndex = null,
        Position? explicitTarget = null)
    {
        var validation = ValidateSpellCast(caster, casterPosition, spell, inCombat, currentEnemy, castingItem,
            castingItemSlotIndex, explicitTarget);
        if (validation is not null) return validation;
        var usingItem = castingItem is not null;
        var castingItemIndex = usingItem ? castingItemSlotIndex ?? -1 : -1;
        var manaCost = usingItem ? 0 : SpellcastingRules.EffectiveManaCost(caster, spell);
        var target = explicitTarget ?? SelectSpellTarget(caster, casterPosition, spell, currentEnemy);
        if (target is null) return null;
        var divineJudgment = !usingItem && caster.RecordDivineSpellCast(spell);
        if (usingItem)
        {
            caster.ConsumeMagicItemCharge(castingItemIndex);
            _renderer.RefreshCharacterSheet(SelectedCharacter);
        }
        else caster.SpendMana(manaCost);
        _renderer.RefreshBattleStatusRows();

        if (inCombat)
        {
            var failureChance = Math.Clamp(30 - caster.EffectiveAbilities.Intelligence -
                caster.EffectiveAbilities.Dexterity, 0, 100);
            var roll = _random.Next(1, 101);
            if (roll <= failureChance)
                return new SpellCastAttempt(true,
                    $"{caster.Name} varázslata meghiúsul: {spell.Name} — kockázat {failureChance}%, dobás {roll}. " +
                    (usingItem ? $"{CastingItemUseText(castingItem!)}; az akció elveszett." : $"-{manaCost} manna; az akció elveszett."),
                    BattleLogKind.Information);
        }

        if (IsOffensiveSpell(spell)) caster.BreakSanctuary();
        var spellListeners = ResolveCharacterSpellTargets(caster, spell, target.Value)
            .Select(character => character.Id)
            .Append(caster.Id)
            .Concat(inCombat ? [SelectedCharacter.Id] : [])
            .Distinct()
            .ToArray();
        PlaySessionSound(IsOffensiveSpell(spell) ? SoundEffect.OffensiveSpell : SoundEffect.DefensiveSpell,
            spellListeners);
        var targetText = DescribeSpellTarget(caster, spell, target.Value, currentEnemy);
        var execution = ExecuteSpell(caster, casterPosition, spell, target.Value, inCombat, currentEnemy, divineJudgment);
        var judgmentText = divineJudgment ? " ⚡ Isteni ítélet: kétszeres számszerű hatás és ingyenes varázslat." : string.Empty;
        return new SpellCastAttempt(true,
            $"{caster.Name} elsüti: {spell.Name} → {targetText}. " +
            (usingItem ? $"{CastingItemUseText(castingItem!)}; 0 manna." : $"-{manaCost} manna.") +
            $"{judgmentText} {execution.Summary}",
            BattleLogKind.PlayerAttack, execution.DamageToCurrentEnemy, execution.ExtraPlayerActions);
    }

    private SpellCastAttempt? ValidateSpellCast(LiveCharacter caster, Position casterPosition, SpellDefinition spell,
        bool inCombat, Enemy? currentEnemy, MagicItemDefinition? castingItem = null, int? castingItemSlotIndex = null,
        Position? explicitTarget = null)
    {
        if (!caster.IsAlive)
            return new SpellCastAttempt(false, $"{caster.Name} nem képes varázsolni.", BattleLogKind.Information);
        var usingItem = castingItem is not null;
        var castingItemIndex = usingItem ? castingItemSlotIndex ?? -1 : -1;
        if (usingItem && (castingItem!.Kind is not (MagicItemKind.Scroll or MagicItemKind.Wand) || castingItem.SpellId != spell.Id ||
                castingItemIndex is < 0 or >= LiveCharacter.MaximumMagicItemCount ||
                caster.MagicItems[castingItemIndex]?.Id != castingItem.Id || caster.MagicItemCharges[castingItemIndex] <= 0 ||
                !SpellcastingRules.CanUseCastingItem(caster, castingItem, spell)))
            return new SpellCastAttempt(false, "A kiválasztott tekercs vagy pálca nem használható.", BattleLogKind.Information);
        if (!usingItem && !caster.IsSpellcaster)
            return new SpellCastAttempt(false, "Ez az osztály nem használ varázslatokat.", BattleLogKind.Information);
        if (!usingItem && !SpellcastingRules.HasRequiredFocus(caster))
            return new SpellCastAttempt(false, "A varázsláshoz hiányzik a megfelelő fókusztárgy.", BattleLogKind.Information);
        if (!usingItem && caster.MemorizedSpells.All(candidate =>
                !string.Equals(candidate.Id, spell.Id, StringComparison.OrdinalIgnoreCase)))
            return new SpellCastAttempt(false, $"A(z) {spell.Name} nincs memorizálva.", BattleLogKind.Information);
        if (inCombat ? !spell.CanUseInCombat : !spell.CanUseDuringExploration)
            return new SpellCastAttempt(false, $"A(z) {spell.Name} ebben a helyzetben nem használható.", BattleLogKind.Information);
        if (inCombat && _timeStopUsedThisBattle && _gameData.GetSpellEffects(spell.Id)
                .Any(effect => effect.Type == SpellEffectType.ExtraActions))
            return new SpellCastAttempt(false, "Az Időmegállítás csatánként csak egyszer használható.", BattleLogKind.Information);
        var manaCost = usingItem ? 0 : SpellcastingRules.EffectiveManaCost(caster, spell);
        if (caster.CurrentMana < manaCost)
            return new SpellCastAttempt(false, $"Nincs elég manna: {spell.Name} {manaCost} mannát igényel.", BattleLogKind.Information);
        if (!HasValidSpellTarget(caster, casterPosition, spell, currentEnemy))
            return new SpellCastAttempt(false, $"A(z) {spell.Name} számára nincs érvényes célpont.", BattleLogKind.Information);
        if (explicitTarget is { } target && !IsValidExplicitSpellTarget(caster, casterPosition, spell, target, currentEnemy))
            return new SpellCastAttempt(false, "A varázslat célpontja érvénytelen.", BattleLogKind.Information);
        return null;
    }

    private bool IsValidExplicitSpellTarget(LiveCharacter caster, Position casterPosition, SpellDefinition spell,
        Position target, Enemy? currentEnemy) => spell.TargetType switch
        {
            SpellTargetType.Self => target == casterPosition && CanAffectCharacter(spell, caster),
            SpellTargetType.Party => target == casterPosition &&
                                     CharacterRoster.Party.Members.Any(character => character.IsAlive && CanAffectCharacter(spell, character)),
            _ => IsValidSpellTarget(casterPosition, spell, target, currentEnemy)
        };

    private static string CastingItemUseText(MagicItemDefinition item) => item.Kind == MagicItemKind.Scroll
        ? "📜 A tekercs elhasználódott"
        : $"{ConsoleRenderer.WandIcon} A pálca egy töltete elfogyott";

    private SpellExecutionResult ExecuteSpell(LiveCharacter caster, Position casterPosition, SpellDefinition spell, Position target, bool inCombat,
        Enemy? currentEnemy, bool divineJudgment)
    {
        var effects = _gameData.GetSpellEffects(spell.Id);
        var targets = ResolveEnemySpellTargets(spell, target, currentEnemy, casterPosition).ToList();
        var characterTargets = ResolveCharacterSpellTargets(caster, spell, target).ToList();
        var damage = targets.ToDictionary(enemy => enemy, _ => 0);
        var initialHitPoints = targets.ToDictionary(enemy => enemy, enemy => enemy.CurrentHitPoints);
        var resolutionCache = new Dictionary<(Enemy Enemy, SpellResolution Resolution), SpellResolutionResult>();
        var notes = new List<string>();
        var extraActions = 0;

        foreach (var effect in effects)
        {
            switch (effect.Type)
            {
                case SpellEffectType.Damage:
                    foreach (var enemy in targets)
                        damage[enemy] += ResolveSpellDamage(caster, effect, spell, enemy, resolutionCache, notes, divineJudgment);
                    break;
                case SpellEffectType.ChainDamage:
                    ApplyChainDamage(caster, effect, spell, target, currentEnemy, damage, initialHitPoints, notes);
                    break;
                case SpellEffectType.Burning:
                    ApplyEnemyTimedEffect(caster, effect, spell, targets, ActiveSpellEffectType.Burning, resolutionCache, notes, divineJudgment);
                    break;
                case SpellEffectType.Storm:
                    ApplyEnemyTimedEffect(caster, effect, spell, targets, ActiveSpellEffectType.Storm, resolutionCache, notes, divineJudgment);
                    break;
                case SpellEffectType.SpeedPenalty:
                    ApplyEnemyTimedEffect(caster, effect, spell, targets, ActiveSpellEffectType.SpeedPenalty, resolutionCache, notes, divineJudgment);
                    break;
                case SpellEffectType.SkipAlternate:
                    ApplyEnemyTimedEffect(caster, effect, spell, targets,
                        string.Equals(effect.Parameter, "Next", StringComparison.OrdinalIgnoreCase)
                            ? ActiveSpellEffectType.SkipNext
                            : ActiveSpellEffectType.SkipAlternate,
                        resolutionCache, notes, divineJudgment);
                    break;
                case SpellEffectType.Invisibility:
                    ApplyCharacterEffects(caster, characterTargets, effect, spell, ActiveSpellEffectType.Invisibility, divineJudgment);
                    notes.Add($"láthatatlanság {AdjustedDuration(caster, spell, effect, divineJudgment)} akcióra");
                    break;
                case SpellEffectType.DefenseBonus:
                    ApplyCharacterEffects(caster, characterTargets, effect, spell, ActiveSpellEffectType.DefenseBonus, divineJudgment);
                    notes.Add($"+{effect.Value} védelem {AdjustedDuration(caster, spell, effect, divineJudgment)} akcióra");
                    break;
                case SpellEffectType.PhysicalReduction:
                    ApplyCharacterEffects(caster, characterTargets, effect, spell, ActiveSpellEffectType.PhysicalReduction, divineJudgment);
                    notes.Add($"{effect.Value}% fizikai védelem {AdjustedDuration(caster, spell, effect, divineJudgment)} akcióra");
                    break;
                case SpellEffectType.BleedingImmunity:
                    foreach (var characterTarget in characterTargets)
                    {
                        ApplyCharacterEffect(caster, characterTarget, effect, spell, ActiveSpellEffectType.BleedingImmunity, divineJudgment);
                        characterTarget.RemoveStatus(CharacterStatusIds.Bleeding);
                    }
                    notes.Add("vérzés megszüntetve és ideiglenesen kivédve");
                    break;
                case SpellEffectType.TeleportSelf:
                    notes.Add(TeleportLeader(target, inCombat) ? "teleportáció sikeres" : "a célmező nem szabad");
                    break;
                case SpellEffectType.TeleportParty:
                    notes.Add(TeleportLivingParty(target, inCombat));
                    break;
                case SpellEffectType.Dispel:
                    notes.Add(DispelAt(target, spell.AreaRadius));
                    break;
                case SpellEffectType.ExtraActions:
                    if (_timeStopUsedThisBattle && inCombat)
                        notes.Add("az Időmegállítás ebben a csatában már nem ismételhető");
                    else
                    {
                        extraActions += effect.Value;
                        if (inCombat) _timeStopUsedThisBattle = true;
                        notes.Add($"+{effect.Value} azonnali akció");
                    }
                    break;
                case SpellEffectType.Execute:
                    foreach (var enemy in targets)
                    {
                        var resolution = ResolveAgainstEnemy(caster, effect, spell, enemy, resolutionCache);
                        if (!resolution.Applies || enemy.Definition.StrengthTier >= 5 ||
                            initialHitPoints[enemy] * 100 > enemy.Definition.HitPoints * effect.Value) continue;
                        damage[enemy] = Math.Max(damage[enemy], enemy.CurrentHitPoints);
                        notes.Add($"💀 {enemy.Name}: megsemmisítés");
                    }
                    break;
                case SpellEffectType.RandomElement:
                    ApplyRandomElement(caster, effect, spell, targets, damage, resolutionCache, notes);
                    break;
                case SpellEffectType.Heal:
                    ApplyHealing(caster, effect, spell, characterTargets, divineJudgment, notes);
                    break;
                case SpellEffectType.CureStatus:
                    ApplyStatusCure(effect, characterTargets, notes);
                    break;
                case SpellEffectType.HitBonus:
                    ApplyCharacterEffects(caster, characterTargets, effect, spell, ActiveSpellEffectType.HitBonus, divineJudgment);
                    notes.Add($"+{effect.Value} találat {AdjustedDuration(caster, spell, effect, divineJudgment)} akcióra");
                    break;
                case SpellEffectType.DamageBonus:
                    ApplyCharacterEffects(caster, characterTargets, effect, spell, ActiveSpellEffectType.DamageBonus, divineJudgment);
                    notes.Add($"+{effect.Value} fizikai sebzés {AdjustedDuration(caster, spell, effect, divineJudgment)} akcióra");
                    break;
                case SpellEffectType.InitiativeBonus:
                    ApplyCharacterEffects(caster, characterTargets, effect, spell, ActiveSpellEffectType.InitiativeBonus, divineJudgment);
                    notes.Add($"+{effect.Value} kezdeményezés {AdjustedDuration(caster, spell, effect, divineJudgment)} akcióra");
                    break;
                case SpellEffectType.ProtectionFromEvil:
                    ApplyCharacterEffects(caster, characterTargets, effect, spell, ActiveSpellEffectType.ProtectionFromEvil, divineJudgment);
                    notes.Add($"gonosz elleni védelem {AdjustedDuration(caster, spell, effect, divineJudgment)} akcióra");
                    break;
                case SpellEffectType.GuardianAngel:
                    ApplyCharacterEffects(caster, characterTargets, effect, spell, ActiveSpellEffectType.GuardianAngel, divineJudgment);
                    notes.Add($"👼 Őrangyal {AdjustedDuration(caster, spell, effect, divineJudgment)} akcióra");
                    break;
                case SpellEffectType.Sanctuary:
                    var sanctuaryTargets = LivingPartyWithPositions()
                        .Where(entry => Chebyshev(entry.Position, casterPosition) <= Math.Max(0, spell.AreaRadius))
                        .Select(entry => entry.Character).ToList();
                    ApplyCharacterEffects(caster, sanctuaryTargets, effect, spell, ActiveSpellEffectType.Sanctuary, divineJudgment);
                    notes.Add($"⛪ Szentély: {sanctuaryTargets.Count} karakter védett {AdjustedDuration(caster, spell, effect, divineJudgment)} akcióra");
                    break;
                case SpellEffectType.Resurrect:
                    notes.Add(ResurrectPartyMember(target, effect));
                    break;
                case SpellEffectType.DispelBeneficial:
                    foreach (var enemy in targets)
                    {
                        var resolution = ResolveAgainstEnemy(caster, effect, spell, enemy, resolutionCache);
                        if (!resolution.Applies) continue;
                        var removed = enemy.RemoveSpellEffects(active => active.Beneficial);
                        notes.Add($"{enemy.Name}: {removed} pozitív varázshatás szétoszlatva");
                    }
                    break;
                case SpellEffectType.RestoreNeeds:
                    ApplyNeedRestoration(characterTargets, effect, divineJudgment, notes);
                    break;
                case SpellEffectType.VisionBonus:
                    ApplyCharacterEffects(caster, characterTargets, effect, spell,
                        ActiveSpellEffectType.VisionBonus, divineJudgment);
                    foreach (var characterTarget in characterTargets)
                        RevealFor(characterTarget, GetCasterPosition(characterTarget));
                    notes.Add($"👁️ +{effect.Value} látótáv {AdjustedDuration(caster, spell, effect, divineJudgment)} akcióra");
                    break;
            }
        }

        var chainRepeated = caster.HasPerk(PerkIds.MageChainSpell) &&
                            effects.Any(effect => effect.Type is SpellEffectType.Damage or SpellEffectType.ChainDamage) &&
                            _random.Next(100) < 30;
        if (chainRepeated)
        {
            foreach (var effect in effects.Where(effect => effect.Type == SpellEffectType.Damage))
                foreach (var enemy in targets)
                    damage[enemy] += ResolveSpellDamage(caster, effect, spell, enemy,
                        new Dictionary<(Enemy, SpellResolution), SpellResolutionResult>(), notes);
            foreach (var effect in effects.Where(effect => effect.Type == SpellEffectType.ChainDamage))
                ApplyChainDamage(caster, effect, spell, target, currentEnemy, damage, initialHitPoints, notes);
            notes.Add("🔁 Láncvarázs: a sebzés ingyen megismétlődött");
        }

        var currentDamage = 0;
        var actualDamage = 0;
        foreach (var entry in damage.Where(entry => entry.Value > 0))
        {
            if (inCombat && entry.Key == currentEnemy)
            {
                var inflicted = Math.Min(entry.Value, entry.Key.CurrentHitPoints);
                currentDamage += inflicted;
                actualDamage += inflicted;
            }
            else
            {
                actualDamage += Math.Min(entry.Value, entry.Key.CurrentHitPoints);
                ApplyExplorationSpellDamage(caster, entry.Key, entry.Value, notes);
            }
        }
        if (actualDamage > 0 && caster.SpecializationId == ClassSpecializations.MageNecromancer)
        {
            var before = caster.CurrentVitality;
            var lifeStealPercent = caster.HasClassFeatureUpgrade(ClassFeatureUpgrades.MageLifeHarvest) ? 15 : 10;
            caster.RestoreVitality(Math.Max(1, (int)Math.Ceiling(actualDamage * lifeStealPercent / 100d)));
            var restored = caster.CurrentVitality - before;
            if (restored > 0) notes.Add($"💀 Nekromancia: ❤️ +{restored} HP");
        }
        else if (actualDamage > 0 && caster.HasClassFeatureUpgrade(ClassFeatureUpgrades.MageLifeHarvest))
        {
            var before = caster.CurrentVitality;
            caster.RestoreVitality(Math.Max(1, (int)Math.Ceiling(actualDamage * 0.15)));
            var restored = caster.CurrentVitality - before;
            if (restored > 0) notes.Add($"💀 Életaratás: ❤️ +{restored} HP");
        }
        if (actualDamage > 0 && caster.HasClassFeatureUpgrade(ClassFeatureUpgrades.PriestMercifulJudgment))
        {
            var before = caster.CurrentVitality;
            caster.RestoreVitality(Math.Max(1, (int)Math.Ceiling(actualDamage * 0.10)));
            var restored = caster.CurrentVitality - before;
            if (restored > 0) notes.Add($"⚖️ Irgalmas ítélet: ❤️ +{restored} HP");
        }
        if (damage.Values.Any(value => value > 0)) caster.BreakInvisibility();
        if (!inCombat) _renderer.RefreshCharacterSheet(caster);
        return new SpellExecutionResult(currentDamage, extraActions,
            notes.Count == 0 ? "A varázslat nem talált érvényes célpontot." : string.Join("; ", notes.Distinct()));
    }

    private IEnumerable<Enemy> ResolveEnemySpellTargets(SpellDefinition spell, Position target, Enemy? currentEnemy, Position casterPosition)
    {
        var enemies = _maze.Enemies.Where(enemy => enemy.CurrentHitPoints > 0);
        return spell.TargetType switch
        {
            SpellTargetType.Enemy => enemies.Where(enemy => enemy.Position == target)
                .Concat(currentEnemy is not null && currentEnemy.Position == target ? [currentEnemy] : []).Distinct(),
            SpellTargetType.Area => enemies.Where(enemy => Chebyshev(enemy.Position, target) <= spell.AreaRadius),
            SpellTargetType.Direction => enemies.Where(enemy => IsInSpellCone(casterPosition, enemy.Position, target)),
            _ => []
        };
    }

    private IEnumerable<LiveCharacter> ResolveCharacterSpellTargets(LiveCharacter caster, SpellDefinition spell, Position target) => spell.TargetType switch
    {
        SpellTargetType.Self => [caster],
        SpellTargetType.Party => CharacterRoster.Party.Members.Where(character => character.IsAlive),
        SpellTargetType.PartyMember when target == _player.Position => [SelectedCharacter],
        SpellTargetType.PartyMember => _maze.GetPartyMemberAt(target) is { } member ? [member.Character] : [],
        _ => []
    };

    private IEnumerable<(LiveCharacter Character, Position Position)> LivingPartyWithPositions()
    {
        if (SelectedCharacter.IsAlive) yield return (SelectedCharacter, _player.Position);
        foreach (var member in _maze.PartyMembers.Where(member => member.Character.IsAlive))
            yield return (member.Character, member.Position);
    }

    private static bool IsInSpellCone(Position casterPosition, Position position, Position selectedDirection)
    {
        var dx = selectedDirection.X - casterPosition.X;
        var dy = selectedDirection.Y - casterPosition.Y;
        var relativeX = position.X - casterPosition.X;
        var relativeY = position.Y - casterPosition.Y;
        var forward = relativeX * dx + relativeY * dy;
        var lateral = Math.Abs(relativeX * dy - relativeY * dx);
        return forward is >= 1 and <= 2 && lateral <= forward - 1;
    }

    private int ResolveSpellDamage(LiveCharacter caster, SpellEffectDefinition effect, SpellDefinition spell, Enemy enemy,
        Dictionary<(Enemy Enemy, SpellResolution Resolution), SpellResolutionResult> cache, List<string> notes,
        bool divineJudgment = false)
    {
        var resolution = ResolveAgainstEnemy(caster, effect, spell, enemy, cache);
        if (!resolution.Applies)
        {
            notes.Add($"{enemy.Name}: a varázslat célt tévesztett ({resolution.Text})");
            return 0;
        }
        var rolled = (effect.Dice?.Roll(_random) ?? 0) +
                     (int)Math.Round(caster.EffectiveAbilities.Intelligence * effect.IntelligenceMultiplier) +
                     caster.Level * effect.LevelMultiplier + effect.Value;
        if (caster.HasPerk(PerkIds.MageElementalMaster)) rolled = (int)Math.Ceiling(rolled * 1.25);
        if (caster.SpecializationId == ClassSpecializations.PriestJudgment && spell.School == SpellSchool.Divine)
            rolled = (int)Math.Ceiling(rolled * 1.20);
        if (caster.SpecializationId == ClassSpecializations.MageElementalist && spell.School == SpellSchool.Arcane)
            rolled = (int)Math.Ceiling(rolled * 1.20);
        if (caster.HasClassFeatureUpgrade(ClassFeatureUpgrades.MageRagingElements) && spell.School == SpellSchool.Arcane)
            rolled = (int)Math.Ceiling(rolled * 1.15);
        if (IsHolyEffect(effect) && IsUnholy(enemy.Definition))
        {
            rolled = (int)Math.Ceiling(rolled * 1.5);
            notes.Add($"{enemy.Name}: ✨ szent sebezhetőség +50%");
        }
        if (divineJudgment) rolled *= 2;
        if (resolution.Critical) rolled *= 2;
        if (resolution.Half) rolled = Math.Max(1, rolled / 2);
        notes.Add($"{enemy.Name}: -{rolled} HP ({resolution.Text})");
        return rolled;
    }

    private SpellResolutionResult ResolveAgainstEnemy(LiveCharacter caster, SpellEffectDefinition effect, SpellDefinition spell, Enemy enemy,
        Dictionary<(Enemy Enemy, SpellResolution Resolution), SpellResolutionResult> cache)
    {
        if (effect.Resolution == SpellResolution.Auto) return new SpellResolutionResult(true, false, false, "automatikus");
        var cacheResolution = effect.Resolution == SpellResolution.SaveNegates
            ? SpellResolution.SaveHalf
            : effect.Resolution;
        var key = (enemy, cacheResolution);
        if (cache.TryGetValue(key, out var cached))
            return effect.Resolution == SpellResolution.SaveNegates && cached.Half
                ? cached with { Applies = false, Half = false }
                : cached with { Half = effect.Resolution == SpellResolution.SaveHalf && cached.Half };
        SpellResolutionResult result;
        if (effect.Resolution == SpellResolution.Attack)
        {
            var roll = _random.Next(1, 21);
            var bonus = caster.EffectiveAbilities.Intelligence +
                        (caster.HasPerk(PerkIds.MageArcaneFocus) ? 2 : 0) +
                        caster.GetMagicItemBonus(MagicItemEffect.Hit) +
                        caster.SpellEffectValue(ActiveSpellEffectType.Invisibility) +
                        caster.SpellEffectValue(ActiveSpellEffectType.HitBonus);
            var hit = roll == 20 || roll != 1 && roll + bonus >= 11 + enemy.EffectiveSpeed;
            result = new SpellResolutionResult(hit, false, roll == 20, hit ? $"mágikus támadás {roll + bonus}" : $"mellé {roll + bonus}");
        }
        else
        {
            var dc = 10 + caster.EffectiveAbilities.Intelligence / 2 + spell.Level;
            var roll = _random.Next(1, 21) + enemy.EffectiveSpeed;
            var saved = roll >= dc;
            result = new SpellResolutionResult(!saved || effect.Resolution == SpellResolution.SaveHalf,
                saved && effect.Resolution == SpellResolution.SaveHalf, false,
                saved ? $"sikeres ellenpróba {roll}/{dc}" : $"rontott ellenpróba {roll}/{dc}");
        }
        cache[key] = result;
        return result;
    }

    private void ApplyEnemyTimedEffect(LiveCharacter caster, SpellEffectDefinition effect, SpellDefinition spell, IEnumerable<Enemy> targets,
        ActiveSpellEffectType type, Dictionary<(Enemy Enemy, SpellResolution Resolution), SpellResolutionResult> cache,
        List<string> notes, bool divineJudgment = false)
    {
        foreach (var enemy in targets)
        {
            var resolution = ResolveAgainstEnemy(caster, effect, spell, enemy, cache);
            if (!resolution.Applies || _random.Next(100) >= effect.ChancePercent) continue;
            enemy.ApplySpellEffect(new ActiveSpellEffect(spell.Id, type, effect.Value,
                AdjustedDuration(caster, spell, effect, divineJudgment),
                effect.Dice, (int)Math.Round(caster.EffectiveAbilities.Intelligence * effect.IntelligenceMultiplier),
                false, effect.Dice is not null && caster.HasPerk(PerkIds.MageElementalMaster) ? 125 : 100));
            notes.Add($"{enemy.Name}: {TimedEffectName(type)} ({AdjustedDuration(caster, spell, effect, divineJudgment)} akció)");
        }
    }

    private static string TimedEffectName(ActiveSpellEffectType type) => type switch
    {
        ActiveSpellEffectType.Burning => "🔥 égés",
        ActiveSpellEffectType.Storm => "⚡ vihar",
        ActiveSpellEffectType.SpeedPenalty => "🐌 lassítás",
        ActiveSpellEffectType.SkipAlternate => "⏳ minden második akció kimarad",
        ActiveSpellEffectType.SkipNext => "⏳ következő akció kimarad",
        ActiveSpellEffectType.Frost => "❄️ fagyás",
        _ => "varázshatás"
    };

    private void ApplyCharacterEffect(LiveCharacter caster, LiveCharacter character, SpellEffectDefinition effect, SpellDefinition spell,
        ActiveSpellEffectType type, bool divineJudgment = false)
    {
        var multiplier = divineJudgment ? 200 : 100;
        if (type == ActiveSpellEffectType.GuardianAngel && caster.HasPerk(PerkIds.PriestHealingGrace))
            multiplier = multiplier * 125 / 100;
        character.ApplySpellEffect(new ActiveSpellEffect(spell.Id, type,
            effect.Value, AdjustedDuration(caster, spell, effect, divineJudgment), effect.Dice,
            (int)Math.Round(caster.EffectiveAbilities.Intelligence * effect.IntelligenceMultiplier), true,
            multiplier, effect.Parameter));
    }

    private void ApplyCharacterEffects(LiveCharacter caster, IEnumerable<LiveCharacter> characters, SpellEffectDefinition effect,
        SpellDefinition spell, ActiveSpellEffectType type, bool divineJudgment)
    {
        foreach (var character in characters) ApplyCharacterEffect(caster, character, effect, spell, type, divineJudgment);
    }

    private void ApplyHealing(LiveCharacter caster, SpellEffectDefinition effect, SpellDefinition spell,
        IEnumerable<LiveCharacter> characters, bool divineJudgment, ICollection<string> notes)
    {
        foreach (var character in characters.Where(character => character.IsAlive))
        {
            var fullHealing = string.Equals(effect.Parameter, "Full", StringComparison.OrdinalIgnoreCase);
            var amount = fullHealing
                ? character.MaximumVitality
                : (effect.Dice?.Roll(_random) ?? 0) +
                  (int)Math.Round(caster.EffectiveAbilities.Intelligence * effect.IntelligenceMultiplier) +
                  caster.Level * effect.LevelMultiplier + effect.Value;
            if (!fullHealing && divineJudgment) amount *= 2;
            if (caster.HasPerk(PerkIds.PriestHealingGrace))
                amount = (int)Math.Ceiling(amount * 1.25);
            if (caster.SpecializationId == ClassSpecializations.PriestLife)
                amount = (int)Math.Ceiling(amount * 1.25);
            if (caster.HasClassFeatureUpgrade(ClassFeatureUpgrades.PriestOverflowingLife))
                amount = (int)Math.Ceiling(amount * 1.15);
            var before = character.CurrentVitality;
            character.RestoreVitality(amount);
            notes.Add($"{character.Name}: {FormatHealingResult(character, amount, before)}");
        }
    }

    private void ApplyStatusCure(SpellEffectDefinition effect, IEnumerable<LiveCharacter> characters,
        ICollection<string> notes)
    {
        var statusIds = ParseEffectParameters(effect.Parameter);
        foreach (var character in characters)
        {
            var removed = statusIds.Where(character.RemoveStatus).Select(StatusName).ToList();
            if (removed.Count > 0) notes.Add($"{character.Name}: ✨ megszűnt {string.Join(" és ", removed)}");
        }
    }

    private string ResurrectPartyMember(Position target, SpellEffectDefinition effect)
    {
        var corpse = _maze.Corpses.OfType<PartyMemberCorpse>().FirstOrDefault(candidate => candidate.Position == target);
        if (corpse is null) return "nincs feltámasztható társ a célmezőn";
        if (corpse.Character.WasResurrectedThisLevel) return $"{corpse.Character.Name} ezen a pályán már visszatért egyszer";
        var revivalPosition = FindResurrectionPosition(corpse);
        if (revivalPosition is null) return "a tetem körül nincs szabad hely a visszatéréshez";

        var parameters = ParseEffectParameters(effect.Parameter);
        var manaPercent = parameters.Count > 0 && int.TryParse(parameters[0], out var parsedMana)
            ? Math.Clamp(parsedMana, 0, 100)
            : 0;
        foreach (var statusId in parameters.Skip(1)) corpse.Character.RemoveStatus(statusId);
        corpse.Character.ClearTemporarySpellEffects();
        corpse.Character.SetCurrentResources(
            Math.Max(1, corpse.Character.MaximumVitality * Math.Clamp(effect.Value, 1, 100) / 100),
            corpse.Character.MaximumMana * manaPercent / 100);
        corpse.Character.MarkResurrectedThisLevel();
        _maze.RemoveCorpse(corpse);
        var avatar = new PartyMemberAvatar(revivalPosition.Value, corpse.Character);
        _maze.AddPartyMember(avatar);
        ScheduleNextPartyMove(avatar, DateTime.UtcNow);
        RevealFor(avatar.Character, avatar.Position);
        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
        _renderer.RefreshCharacterSheet(SelectedCharacter);
        return $"✨ {corpse.Character.Name} visszatért {corpse.Character.CurrentVitality} HP-val" +
               (corpse.Character.UsesMana ? $" és {corpse.Character.CurrentMana} mannával" : string.Empty);
    }

    private Position? FindResurrectionPosition(PartyMemberCorpse corpse)
    {
        bool CanUse(Position position) => position != _player.Position && position != _maze.Entrance &&
            position != _maze.Exit && _maze.IsWalkable(position) &&
            (_maze.GetObjectAt(position) is null || _maze.GetObjectAt(position) == corpse);
        if (CanUse(corpse.Position)) return corpse.Position;
        return FindNearbyTeleportPositions(corpse.Position).Where(CanUse).Select(position => (Position?)position).FirstOrDefault();
    }

    private static int AdjustedDuration(LiveCharacter caster, SpellDefinition spell,
        SpellEffectDefinition effect, bool divineJudgment)
    {
        var duration = divineJudgment ? effect.Duration * 2 : effect.Duration;
        var priestProtection = caster.SpecializationId == ClassSpecializations.PriestProtection &&
                               spell.School == SpellSchool.Divine && effect.Type is
                                   SpellEffectType.DefenseBonus or SpellEffectType.PhysicalReduction or
                                   SpellEffectType.BleedingImmunity or SpellEffectType.HitBonus or
                                   SpellEffectType.DamageBonus or SpellEffectType.InitiativeBonus or
                                   SpellEffectType.ProtectionFromEvil or SpellEffectType.GuardianAngel or
                                   SpellEffectType.Sanctuary;
        var mageIllusion = caster.SpecializationId == ClassSpecializations.MageIllusionist &&
                           spell.School == SpellSchool.Arcane && effect.Type is
                               SpellEffectType.Invisibility or SpellEffectType.DefenseBonus or
                               SpellEffectType.PhysicalReduction or SpellEffectType.BleedingImmunity or
                               SpellEffectType.SpeedPenalty or SpellEffectType.SkipAlternate;
        var bonusDuration = 0;
        if (duration > 0 && priestProtection) bonusDuration++;
        if (duration > 0 && mageIllusion) bonusDuration++;
        if (duration > 0 && caster.HasClassFeatureUpgrade(ClassFeatureUpgrades.PriestSteadfastProtection) &&
            spell.School == SpellSchool.Divine && effect.Type is
                SpellEffectType.DefenseBonus or SpellEffectType.PhysicalReduction or
                SpellEffectType.BleedingImmunity or SpellEffectType.HitBonus or
                SpellEffectType.DamageBonus or SpellEffectType.InitiativeBonus or
                SpellEffectType.ProtectionFromEvil or SpellEffectType.GuardianAngel or SpellEffectType.Sanctuary)
            bonusDuration++;
        if (duration > 0 && caster.HasClassFeatureUpgrade(ClassFeatureUpgrades.MagePerfectIllusion) &&
            spell.School == SpellSchool.Arcane && effect.Type is
                SpellEffectType.Invisibility or SpellEffectType.DefenseBonus or
                SpellEffectType.PhysicalReduction or SpellEffectType.BleedingImmunity or
                SpellEffectType.SpeedPenalty or SpellEffectType.SkipAlternate)
            bonusDuration++;
        return duration + bonusDuration;
    }
    private static IReadOnlyList<string> ParseEffectParameters(string? parameter) =>
        string.IsNullOrWhiteSpace(parameter)
            ? []
            : parameter.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    private string StatusName(string statusId) => _gameData.Statuses.FirstOrDefault(status =>
        string.Equals(status.Id, statusId, StringComparison.OrdinalIgnoreCase))?.Name ?? statusId;
    private static bool IsHolyEffect(SpellEffectDefinition effect) =>
        string.Equals(effect.Parameter, "Holy50", StringComparison.OrdinalIgnoreCase);
    private static bool IsUnholy(EnemyDefinition enemy) => enemy.AbilityIds.Any(abilityId =>
        string.Equals(abilityId, MonsterAbilityIds.Undead, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(abilityId, MonsterAbilityIds.Demonic, StringComparison.OrdinalIgnoreCase));

    private bool IsOffensiveSpell(SpellDefinition spell) => _gameData.GetSpellEffects(spell.Id).Any(effect =>
        effect.Type is SpellEffectType.Damage or SpellEffectType.ChainDamage or SpellEffectType.Burning or
            SpellEffectType.Storm or SpellEffectType.SpeedPenalty or SpellEffectType.SkipAlternate or
            SpellEffectType.Execute or SpellEffectType.RandomElement or SpellEffectType.DispelBeneficial);

    private void ApplyChainDamage(LiveCharacter caster, SpellEffectDefinition effect, SpellDefinition spell, Position target,
        Enemy? currentEnemy, Dictionary<Enemy, int> damage, Dictionary<Enemy, int> initialHitPoints, List<string> notes)
    {
        var candidates = _maze.Enemies.Where(enemy => enemy.CurrentHitPoints > 0)
            .Concat(currentEnemy is null ? [] : [currentEnemy]).Distinct()
            .OrderBy(enemy => enemy.Position == target ? 0 : Chebyshev(enemy.Position, target))
            .Where(enemy => enemy.Position == target || Chebyshev(enemy.Position, target) <= 4).Take(4).ToList();
        var multipliers = (effect.Parameter ?? "100|75|50|25").Split('|')
            .Select(value => int.TryParse(value, out var parsed) ? parsed : 100).ToArray();
        for (var index = 0; index < candidates.Count; index++)
        {
            var enemy = candidates[index];
            if (!damage.ContainsKey(enemy)) damage[enemy] = 0;
            if (!initialHitPoints.ContainsKey(enemy)) initialHitPoints[enemy] = enemy.CurrentHitPoints;
            var baseDamage = ResolveSpellDamage(caster, effect, spell, enemy,
                new Dictionary<(Enemy, SpellResolution), SpellResolutionResult>(), notes);
            damage[enemy] += baseDamage * multipliers[Math.Min(index, multipliers.Length - 1)] / 100;
        }
    }

    private void ApplyRandomElement(LiveCharacter caster, SpellEffectDefinition effect, SpellDefinition spell, IEnumerable<Enemy> targets,
        Dictionary<Enemy, int> damage, Dictionary<(Enemy Enemy, SpellResolution Resolution), SpellResolutionResult> cache,
        List<string> notes)
    {
        var element = (effect.Parameter ?? "Fire|Frost|Lightning").Split('|')[_random.Next(3)];
        foreach (var enemy in targets)
        {
            if (!ResolveAgainstEnemy(caster, effect, spell, enemy, cache).Applies) continue;
            if (element.Equals("Fire", StringComparison.OrdinalIgnoreCase))
            {
                var fireDamage = effect.Dice?.Roll(_random) ?? 0;
                if (caster.HasPerk(PerkIds.MageElementalMaster))
                    fireDamage = (int)Math.Ceiling(fireDamage * 1.25);
                damage[enemy] += fireDamage;
                notes.Add($"{enemy.Name}: 🔥 -{fireDamage} HP");
            }
            else if (element.Equals("Frost", StringComparison.OrdinalIgnoreCase))
                enemy.ApplySpellEffect(new ActiveSpellEffect(spell.Id, ActiveSpellEffectType.Frost, effect.Value, effect.Duration));
            else if (_random.Next(100) < effect.ChancePercent)
                enemy.ApplySpellEffect(new ActiveSpellEffect(spell.Id, ActiveSpellEffectType.SkipAlternate, 0, effect.Duration));
        }
        notes.Add($"🎲 véletlen elem: {element}");
    }

    private void ApplyExplorationSpellDamage(LiveCharacter caster, Enemy enemy, int amount, List<string> notes)
    {
        enemy.ReceiveSpellDamage(amount);
        if (enemy.CurrentHitPoints > 0) return;
        PlaySessionSound(SoundEffect.MonsterKilledBySpell);
        RegisterNpcQuestKill(enemy.Definition.Id);
        _maze.ReplaceEnemyWithCorpse(enemy);
        _nextEnemyMoves.Remove(enemy);
        var awards = DistributeExperience(caster, enemy.Definition.ExperienceReward);
        notes.Add($"☠ {enemy.Name} elpusztult; {FormatExperienceAwards(awards)}");
        _renderer.DrawMapCellAfterBattle(_maze, _fogOfWar, enemy.Position, _player.Position);
        var leveledAwards = awards.Where(award => award.Result.LeveledUp && award.Character.IsAlive).ToList();
        if (leveledAwards.Count == 0) return;
        if (_battleStarted)
            _pendingLevelUps.AddRange(leveledAwards.Select(award => (award.Character, award.Result)));
        else
        {
            foreach (var award in leveledAwards) ResolvePerkOffers(award.Character, award.Result);
            _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
        }
    }

    private bool TeleportLeader(Position target, bool inCombat)
    {
        if (!_maze.IsWalkable(target) || _maze.GetObjectAt(target) is not null) return false;
        _player.TeleportTo(target);
        _leaderTrail.Clear();
        _leaderTrail.Add(target);
        RevealFor(SelectedCharacter, target);
        if (!inCombat) _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, target);
        return true;
    }

    private string TeleportLivingParty(Position target, bool inCombat)
    {
        if (!TeleportLeader(target, inCombat)) return "a dimenziókapu célmezője nem szabad";
        var positions = FindNearbyTeleportPositions(target).Take(_maze.PartyMembers.Count).ToList();
        var moved = 0;
        foreach (var pair in _maze.PartyMembers.Zip(positions))
        {
            pair.First.MoveTo(pair.Second);
            RevealFor(pair.First.Character, pair.Second);
            moved++;
        }
        if (!inCombat) _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, target);
        return $"dimenziókapu: a vezér és {moved} társ átkerült";
    }

    private IEnumerable<Position> FindNearbyTeleportPositions(Position origin)
    {
        var queue = new Queue<Position>();
        var visited = new HashSet<Position> { origin };
        queue.Enqueue(origin);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var direction in Directions)
            {
                var next = current + direction;
                if (!visited.Add(next) || !_maze.IsWalkable(next)) continue;
                queue.Enqueue(next);
                if (next != _player.Position && _maze.GetObjectAt(next) is null) yield return next;
            }
        }
    }

    private string DispelAt(Position target, int radius)
    {
        var removed = _maze.Enemies.Where(enemy => Chebyshev(enemy.Position, target) <= radius)
            .Sum(enemy => enemy.RemoveSpellEffects());
        if (Chebyshev(_player.Position, target) <= radius) removed += SelectedCharacter.RemoveSpellEffects();
        foreach (var member in _maze.PartyMembers.Where(member => Chebyshev(member.Position, target) <= radius))
            removed += member.Character.RemoveSpellEffects();
        return $"✨ szétoszlatott varázshatások: {removed}";
    }

    private Position? SelectSpellTarget(LiveCharacter caster, Position casterPosition, SpellDefinition spell, Enemy? currentEnemy)
    {
        if (spell.TargetType is SpellTargetType.Self or SpellTargetType.Party) return casterPosition;
        var candidates = GetValidSpellTargets(casterPosition, spell, currentEnemy).Distinct().ToList();
        var forward = DirectionOffset(_leaderFacing);
        var fallback = new Position(
            Math.Clamp(casterPosition.X + forward.X, 0, _maze.Width - 1),
            Math.Clamp(casterPosition.Y + forward.Y, 0, _maze.Height - 1));
        var cursor = candidates.OrderBy(position => Chebyshev(position, casterPosition)).FirstOrDefault(fallback);
        Position? previous = null;

        while (true)
        {
            var valid = IsValidSpellTarget(casterPosition, spell, cursor, currentEnemy);
            var prompt = $"╳ {spell.Name} — {ConsoleRenderer.SpellTargetName(spell.TargetType)}, táv {spell.Range}" +
                         (spell.AreaRadius > 0 ? $", sugár {spell.AreaRadius}" : string.Empty) +
                         $" | {(valid ? DescribeSpellTarget(caster, spell, cursor, currentEnemy) : "érvénytelen cél")} | Enter: célzás, Tab: következő, Esc: mégse";
            _renderer.DrawSpellTargetCursor(_maze, _fogOfWar, previous, cursor, valid, prompt);
            previous = cursor;
            var key = Console.ReadKey(intercept: true);
            if (IsHelpShortcut(key))
            {
                ShowInGameHelp();
                if (currentEnemy is not null) _renderer.DrawBattleStarted(currentEnemy);
                previous = null;
                continue;
            }
            if (key.Key == ConsoleKey.Escape)
            {
                _renderer.FinishSpellTargeting(_maze, _fogOfWar, _player.Position);
                return null;
            }
            if (key.Key == ConsoleKey.Enter && valid)
            {
                _renderer.FinishSpellTargeting(_maze, _fogOfWar, _player.Position);
                return cursor;
            }
            if (key.Key == ConsoleKey.Tab && candidates.Count > 0)
            {
                var index = candidates.IndexOf(cursor);
                cursor = candidates[(index + 1 + candidates.Count) % candidates.Count];
                continue;
            }
            if (!TryGetDirection(key.Key, out var direction)) continue;
            cursor = spell.TargetType == SpellTargetType.Direction
                ? casterPosition + direction
                : cursor + direction;
            if (!_maze.IsInside(cursor)) cursor = previous.Value;
        }
    }

    private IEnumerable<Position> GetValidSpellTargets(Position casterPosition, SpellDefinition spell, Enemy? currentEnemy)
    {
        IEnumerable<Position> possible = spell.TargetType switch
        {
            SpellTargetType.Enemy when currentEnemy is not null => [currentEnemy.Position],
            SpellTargetType.Enemy => _maze.Enemies.Where(enemy => enemy.CurrentHitPoints > 0).Select(enemy => enemy.Position),
            SpellTargetType.PartyMember => new[] { _player.Position }.Concat(_maze.PartyMembers
                .Where(member => member.Character.IsAlive).Select(member => member.Position)),
            SpellTargetType.Corpse => _maze.Corpses.OfType<PartyMemberCorpse>().Select(corpse => corpse.Position),
            SpellTargetType.Direction => Directions.Select(direction => casterPosition + direction),
            SpellTargetType.Cell or SpellTargetType.Area =>
                from y in Enumerable.Range(Math.Max(0, casterPosition.Y - spell.Range),
                    Math.Min(_maze.Height - 1, casterPosition.Y + spell.Range) - Math.Max(0, casterPosition.Y - spell.Range) + 1)
                from x in Enumerable.Range(Math.Max(0, casterPosition.X - spell.Range),
                    Math.Min(_maze.Width - 1, casterPosition.X + spell.Range) - Math.Max(0, casterPosition.X - spell.Range) + 1)
                select new Position(x, y),
            _ => []
        };
        return possible.Where(position => IsValidSpellTarget(casterPosition, spell, position, currentEnemy));
    }

    private bool IsValidSpellTarget(Position casterPosition, SpellDefinition spell, Position position, Enemy? currentEnemy)
    {
        if (!_maze.IsInside(position) || !_fogOfWar.IsVisible(position)) return false;
        var inRange = Chebyshev(casterPosition, position) <= Math.Max(1, spell.Range);
        if (!inRange || spell.RequiresLineOfSight && !FogOfWar.CanSee(_maze, casterPosition, position, Math.Max(1, spell.Range))) return false;
        return spell.TargetType switch
        {
            SpellTargetType.Enemy => currentEnemy is not null
                ? currentEnemy.CurrentHitPoints > 0 && currentEnemy.Position == position
                : _maze.GetEnemyAt(position)?.CurrentHitPoints > 0,
            SpellTargetType.PartyMember => position == _player.Position && SelectedCharacter.IsAlive &&
                                           CanAffectCharacter(spell, SelectedCharacter) ||
                                           _maze.PartyMembers.Any(member => member.Position == position && member.Character.IsAlive &&
                                               CanAffectCharacter(spell, member.Character)),
            SpellTargetType.Corpse => _maze.Corpses.OfType<PartyMemberCorpse>().Any(corpse =>
                corpse.Position == position && !corpse.Character.WasResurrectedThisLevel &&
                FindResurrectionPosition(corpse) is not null),
            SpellTargetType.Direction => Manhattan(casterPosition, position) == 1,
            SpellTargetType.Cell when _gameData.GetSpellEffects(spell.Id).Any(effect =>
                effect.Type is SpellEffectType.TeleportSelf or SpellEffectType.TeleportParty) =>
                _maze.IsWalkable(position) && _maze.GetObjectAt(position) is null,
            SpellTargetType.Cell or SpellTargetType.Area => true,
            _ => false
        };
    }

    private bool HasValidSpellTarget(LiveCharacter caster, Position casterPosition, SpellDefinition spell, Enemy? currentEnemy) => spell.TargetType switch
    {
        SpellTargetType.Self => CanAffectCharacter(spell, caster),
        SpellTargetType.Party => CharacterRoster.Party.Members.Any(character => character.IsAlive && CanAffectCharacter(spell, character)),
        _ => GetValidSpellTargets(casterPosition, spell, currentEnemy).Any()
    };

    private bool CanAffectCharacter(SpellDefinition spell, LiveCharacter character)
    {
        var effects = _gameData.GetSpellEffects(spell.Id);
        return effects.Any(effect => effect.Type switch
        {
            SpellEffectType.Heal => character.CurrentVitality < character.MaximumVitality,
            SpellEffectType.CureStatus => ParseEffectParameters(effect.Parameter).Any(character.HasStatus),
            SpellEffectType.RestoreNeeds => character.FoodLevel < 100 || character.WaterLevel < 100,
            _ => true
        });
    }

    private void ApplyNeedRestoration(IEnumerable<LiveCharacter> characters, SpellEffectDefinition effect,
        bool divineJudgment, ICollection<string> notes)
    {
        var amount = effect.Value * (divineJudgment ? 2 : 1);
        foreach (var character in characters.Where(character => character.IsAlive))
        {
            var foodBefore = character.FoodLevel;
            var waterBefore = character.WaterLevel;
            character.RestoreFood(amount);
            character.RestoreWater(amount);
            character.SynchronizeNeedStatuses(_gameData.GetStatus(CharacterStatusIds.Hungry),
                _gameData.GetStatus(CharacterStatusIds.Thirsty));
            notes.Add($"{character.Name}: 🍖+{character.FoodLevel - foodBefore} 💧+{character.WaterLevel - waterBefore}");
        }
    }

    private string DescribeSpellTarget(LiveCharacter caster, SpellDefinition spell, Position position, Enemy? currentEnemy) => spell.TargetType switch
    {
        SpellTargetType.Self => caster.Name,
        SpellTargetType.Party => "az egész parti",
        SpellTargetType.Enemy when currentEnemy is not null && currentEnemy.Position == position => currentEnemy.Name,
        SpellTargetType.Enemy => _maze.GetEnemyAt(position)?.Name ?? $"({position.X},{position.Y})",
        SpellTargetType.PartyMember when position == _player.Position => SelectedCharacter.Name,
        SpellTargetType.PartyMember => _maze.PartyMembers.FirstOrDefault(member => member.Position == position)?.Character.Name ?? $"({position.X},{position.Y})",
        SpellTargetType.Corpse => _maze.Corpses.OfType<PartyMemberCorpse>().FirstOrDefault(corpse => corpse.Position == position)?.Character.Name ?? $"({position.X},{position.Y})",
        SpellTargetType.Direction => $"{DirectionName(_player.Position, position)} irány",
        _ => $"({position.X},{position.Y})"
    };

    private static string DirectionName(Position origin, Position position) => position.X < origin.X ? "bal" :
        position.X > origin.X ? "jobb" : position.Y < origin.Y ? "fel" : "le";

    private static int Chebyshev(Position first, Position second) =>
        Math.Max(Math.Abs(first.X - second.X), Math.Abs(first.Y - second.Y));

    private IReadOnlyList<Position> RevealFor(LiveCharacter character, Position position,
        bool advanceEnemyMemory = false)
    {
        var exitWasRevealed = _fogOfWar.IsRevealed(_maze.Exit);
        var sources = LivingPartyWithPositions().Select(entry => new PartyPerceptionSource(entry.Position,
            CharacterClassRules.VisionRange(entry.Character, CurrentLevelVisionModifier),
            CharacterClassRules.HearingRange(entry.Character),
            CharacterClassRules.DetectionBonus(entry.Character))).ToArray();
        var revealed = _fogOfWar.UpdatePartyVisibility(_maze, sources, advanceEnemyMemory);
        if (_fogOfWar.IsRevealed(_maze.Exit))
        {
            _backgroundMusic.MarkExitDiscovered();
            if (!exitWasRevealed) RegisterNpcQuestProgress(NpcQuestType.Explore, "EXIT");
        }
        return revealed;
    }

    private int CurrentLevelVisionModifier => MazeLevelConfigurations.Get(_mazeLevel).VisionModifier;

    private void CheckBossDiscoveryAt(IEnumerable<Position> positions)
    {
        var revealed = positions.ToHashSet();
        if (revealed.Count == 0) return;
        CheckBossDiscovery(_maze.Enemies.Where(enemy => revealed.Contains(enemy.Position)));
    }

    private void CheckBossDiscovery(IEnumerable<Enemy> enemies)
    {
        var visibleEnemies = enemies.Where(enemy => _fogOfWar.IsEnemyVisible(enemy.Id, enemy.Position))
            .DistinctBy(enemy => enemy.Id).ToList();
        var newlySpotted = visibleEnemies.Where(enemy => _spottedEnemyIds.Add(enemy.Id)).ToList();
        if (newlySpotted.Count > 0)
            PlaySessionSound(SoundEffect.MonsterSpotted);
        var discovered = visibleEnemies.Where(enemy => enemy.Definition.IsBoss &&
                !_seenBossIds.Contains(enemy.Definition.Id))
            .DistinctBy(enemy => enemy.Definition.Id, StringComparer.OrdinalIgnoreCase).ToList();
        if (discovered.Count == 0) return;
        foreach (var boss in discovered)
        {
            _seenBossIds.Add(boss.Definition.Id);
            var narrative = BossNarratives.GetValueOrDefault(boss.Definition.Id)
                ?? new BossNarrative("Ismeretlen fejezet",
                    [$"Én vagyok {boss.Name}. E folyosók titkait nem osztom meg veletek."]);
            ShowSynchronizedNarrative(NarrativeKind.BossIntroduction, "BOSS KÖZELEG",
                narrative.ChapterTitle, narrative.Speech,
                new BossPresentationSnapshot(boss.Name, boss.Definition.Appearance,
                    boss.Definition.StrengthTier, "🔑 Aranykulcs"));
        }
    }

    private void AwardBossKey(Enemy enemy)
    {
        if (!enemy.Definition.IsBoss || !_collectedBossKeyIds.Add(enemy.Definition.Id)) return;
        _renderer.SetGoldenKeyCount(_collectedBossKeyIds.Count);
        _renderer.RefreshCharacterSheet(SelectedCharacter);
        var completed = _collectedBossKeyIds.Count == MonsterIds.Bosses.Count
            ? " A tizenkét aranykulcs összegyűlt — a küldetés első célja teljesült!"
            : string.Empty;
        _renderer.DrawInventoryMessage($"🔑 Aranykulcs megszerezve: {enemy.Name}. " +
            $"Kulcsok: {_collectedBossKeyIds.Count}/{MonsterIds.Bosses.Count}.{completed}", ConsoleColor.Yellow);
        if (_collectedBossKeyIds.Count == MonsterIds.Bosses.Count)
            ShowSynchronizedNarrative(NarrativeKind.TwelveKeys, "A TIZENKÉT ZÁR FELNYÍLIK",
                "XIV. fejezet — A Rubin Útja", TwelveKeysStory);
    }

    private void StartBattle(Enemy enemy)
        => StartInteractiveBattle(SelectedCharacter, enemy);

    private void StartBattle(PartyMemberAvatar member, Enemy enemy)
        => StartInteractiveBattle(member.Character, enemy);

    private void StartInteractiveBattle(LiveCharacter battleCharacter, Enemy enemy)
    {
        if (_battleStarted) return;
        CheckBossDiscovery([enemy]);
        _timeStopUsedThisBattle = false;
        _battleTacticHintLogged = false;
        _battleActionHintLogged = false;
        _turnUndeadUsedThisBattle.Clear();
        _battleStartingVitality = battleCharacter.CurrentVitality;
        _battleStartingMana = battleCharacter.CurrentMana;
        _battleStartingStatusIds = battleCharacter.Statuses.Select(status => status.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (_renderer.IsSpellInfoPageOpen) _renderer.CloseSpellInfoPage();
        _battleStarted = true;
        _session.SetPhase(GameSessionPhase.Battle);
        PlaySessionSound(SoundEffect.BattleStart);
        _renderer.DrawBattleStarted(enemy);
        if (battleCharacter != SelectedCharacter)
        {
            _renderer.DrawInventoryMessage(
                $"🎮 Most {battleCharacter.Name} csatázik; a döntéseinél időnként várni kell rá.",
                ConsoleColor.Yellow);
            PlaySessionSound(SoundEffect.Waiting, [SelectedCharacter.Id]);
        }
        var started = _battleSystem.StartBattle(battleCharacter, enemy);
        _activeBattleState = started.State;
        TryAssignKnightProtection(_activeBattleState, battleCharacter);
        PresentBattleEntries(started.Entries);
        ContinueActiveBattle();
    }

    private void ContinueActiveBattle()
    {
        while (_activeBattleState is { IsCompleted: false } state)
        {
            if (state.IsOpeningEnemyTurn)
            {
                var openingSupportDamage = TryPartyMembersActInBattle(state.Player, state.Enemy);
                var openingStep = _battleSystem.Advance(state, supportDamage: openingSupportDamage);
                PresentBattleEntries(openingStep.Entries);
                continue;
            }
            if (state.IsAwaitingTacticSelection)
            {
                var battleCharacter = state.Player;
                _session.SetBattlePrompt(state.Id, state.TurnId, state.PlayerCharacterId,
                    GetAllowedBattleActions(battleCharacter, GetCasterPosition(battleCharacter), state.Enemy));
                PublishBattleControlHintOnce(state, state.Enemy);
                return;
            }
            if (state.IsPlayerTurn)
            {
                var battleCharacter = state.Player;
                _pendingBattleSupportDamage = TryPartyMembersActInBattle(battleCharacter, state.Enemy);
                if (_pendingBattleSupportDamage < state.CurrentEnemyHitPoints)
                {
                    _session.SetBattlePrompt(state.Id, state.TurnId, state.PlayerCharacterId,
                        GetAllowedBattleActions(battleCharacter, GetCasterPosition(battleCharacter), state.Enemy));
                    PublishBattleControlHintOnce(state, state.Enemy);
                    return;
                }
                var supportStep = _battleSystem.Advance(state, supportDamage: _pendingBattleSupportDamage);
                _pendingBattleSupportDamage = 0;
                PresentBattleEntries(supportStep.Entries);
                continue;
            }

            _session.SetBattlePrompt(state.Id, state.TurnId, state.PlayerCharacterId,
                [BattleActionKind.AdvanceEnemyTurn]);
            return;
        }
        if (_activeBattleState is { IsCompleted: true }) FinishActiveBattle();
    }

    private void ResolveActiveBattleAction(BattlePlayerAction? action, bool spellCanKillEnemy = false)
    {
        if (_activeBattleState is not { IsCompleted: false, IsPlayerTurn: true } state) return;
        var step = _battleSystem.Advance(state, action, _pendingBattleSupportDamage);
        _pendingBattleSupportDamage = 0;
        if (spellCanKillEnemy && step.Result is { PlayerWon: true })
            PlaySessionSound(SoundEffect.MonsterKilledBySpell);
        PresentBattleEntries(step.Entries);
        ContinueActiveBattle();
    }

    private void ResolveActiveEnemyTurn()
    {
        if (_activeBattleState is not { IsCompleted: false, IsPlayerTurn: false } state) return;
        var supportDamage = TryPartyMembersActInBattle(state.Player, state.Enemy);
        var step = _battleSystem.Advance(state, supportDamage: supportDamage);
        if (step.Result is { PlayerWon: true } && step.Entries.Any(entry =>
                entry.Message.Contains("elbukik a varázshatásoktól", StringComparison.OrdinalIgnoreCase)))
            PlaySessionSound(SoundEffect.MonsterKilledBySpell);
        PresentBattleEntries(step.Entries);
        ContinueActiveBattle();
    }

    private void FinishActiveBattle()
    {
        if (_activeBattleState is not { IsCompleted: true, Result: { } result } state) return;
        var enemy = state.Enemy;
        var battleCharacter = state.Player;
        _session.EndBattle(state.Id);
        _activeBattleState = null;
        _pendingBattleSupportDamage = 0;
        var vitalityLost = Math.Max(0, _battleStartingVitality - battleCharacter.CurrentVitality);
        var manaLost = Math.Max(0, _battleStartingMana - battleCharacter.CurrentMana);
        var gainedStatusIcons = battleCharacter.Statuses
            .Where(status => !_battleStartingStatusIds.Contains(status.Id))
            .Select(status => status.Icon)
            .ToArray();
        if (battleCharacter != SelectedCharacter)
        {
            FinishRemoteBattle(state, result, battleCharacter, enemy, vitalityLost, manaLost, gainedStatusIcons);
            return;
        }
        var needLoss = DrainNeedsAfterBattle(SelectedCharacter, enemy.Definition.StrengthTier);
        _renderer.RefreshCharacterSheet(SelectedCharacter);

        if (result.PlayerWon)
        {
            AwardBossKey(enemy);
            PlayBattleVictorySound();
            var experienceAwards = DistributeExperience(SelectedCharacter, enemy.Definition.ExperienceReward);
            RegisterNpcQuestKill(enemy.Definition.Id);
            _maze.ReplaceEnemyWithCorpse(enemy);
            _renderer.DrawMapCellAfterBattle(_maze, _fogOfWar, enemy.Position, _player.Position);
            var victoryMessage = ConsoleRenderer.FormatBattleVictorySummary(result, enemy, vitalityLost,
                manaLost, gainedStatusIcons, needLoss);
            RecordSessionActivity(SessionActivityKind.Battle,
                victoryMessage, ConsoleColor.Green);
            _renderer.DrawBattleResult(result, enemy, victoryMessage);
            _renderer.DrawExperienceDistribution(FormatExperienceAwards(experienceAwards),
                experienceAwards.Any(award => award.Result.LeveledUp));
            _renderer.RefreshCharacterSheet(SelectedCharacter);
            var levelUps = _pendingLevelUps.ToList();
            _pendingLevelUps.Clear();
            levelUps.AddRange(experienceAwards.Where(award => award.Result.LeveledUp && award.Character.IsAlive)
                .Select(award => (award.Character, award.Result)));
            if (levelUps.Count > 0)
            {
                foreach (var (character, levelUpResult) in levelUps) ResolvePerkOffers(character, levelUpResult);
                _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
            }
            if (_saveAfterBattle)
            {
                _saveAfterBattle = false;
                SaveGame();
            }
            InitializeEnemyMoveSchedule(DateTime.UtcNow);
            _battleStarted = false;
            _session.SetPhase(GameSessionPhase.Exploration);
            _nextNeedsDrain = DateTime.UtcNow + TimeSpan.FromMinutes(1);
            return;
        }

        _renderer.DrawBattleResult(result, enemy);
        PlaySessionSound(SoundEffect.MemberKilled);
        _saveAfterBattle = false;
        _renderer.DrawInventoryMessage($"A csata kifárasztott: 🍖 -{needLoss}, 💧 -{needLoss}.", ConsoleColor.DarkYellow);
        _renderer.DrawGameOver(SelectedCharacter.Name);
        _gameOver = true;
        _session.SetPhase(GameSessionPhase.GameOver);
    }

    private void FinishRemoteBattle(BattleState state, BattleResult result, LiveCharacter battleCharacter,
        Enemy enemy, int vitalityLost, int manaLost, IReadOnlyList<string> gainedStatusIcons)
    {
        var needLoss = DrainNeedsAfterBattle(battleCharacter, enemy.Definition.StrengthTier);
        var companionDied = !result.PlayerWon;
        if (result.PlayerWon)
        {
            AwardBossKey(enemy);
            PlayBattleVictorySound();
            var experienceAwards = DistributeExperience(battleCharacter, enemy.Definition.ExperienceReward);
            RegisterNpcQuestKill(enemy.Definition.Id);
            _maze.ReplaceEnemyWithCorpse(enemy);
            _renderer.DrawMapCellAfterBattle(_maze, _fogOfWar, enemy.Position, _player.Position);
            var victoryMessage = ConsoleRenderer.FormatBattleVictorySummary(result, enemy, vitalityLost,
                manaLost, gainedStatusIcons, needLoss);
            RecordSessionActivity(SessionActivityKind.Battle,
                victoryMessage, ConsoleColor.Green);
            _renderer.DrawBattleResult(result, enemy, victoryMessage);
            _renderer.DrawNpcBattleSummary(
                $"{battleCharacter.Name}: {FormatExperienceAwards(experienceAwards)}.", ConsoleColor.Green);
            var levelUps = experienceAwards.Where(award => award.Result.LeveledUp && award.Character.IsAlive).ToList();
            // Az első vertical slice-ban a perk-/varázsválasztás még a host konzolján történik.
            foreach (var award in levelUps) ResolvePerkOffers(award.Character, award.Result);
        }
        else
        {
            _renderer.DrawBattleResult(result, enemy);
            PlaySessionSound(SoundEffect.MemberKilled);
            var member = _maze.PartyMembers.FirstOrDefault(candidate => candidate.Character == battleCharacter);
            if (member is not null)
            {
                _maze.ReplacePartyMemberWithCorpse(member);
                _nextPartyMoves.Remove(member);
            }
            _activeCoopHost?.TryPublishCharacterState(battleCharacter.Id,
                _gameSaveService.SerializeCharacter(battleCharacter), CharacterSyncReason.CharacterDied);
            _session.ReleaseCharacterControl(battleCharacter.Id);
            _renderer.DrawNpcBattleSummary(
                $"{battleCharacter.Name} elesett a(z) {enemy.Name} elleni távoli csatában {result.Rounds} kör után. " +
                $"A vendég visszatér a főmenübe; 🍖💧 -{needLoss}.", ConsoleColor.Red);
        }

        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
        _renderer.RefreshCharacterSheet(SelectedCharacter);
        InitializeEnemyMoveSchedule(DateTime.UtcNow);
        _battleStarted = false;
        _session.SetPhase(GameSessionPhase.Exploration);
        _nextNeedsDrain = DateTime.UtcNow + TimeSpan.FromMinutes(1);
        if (companionDied)
        {
            _activeCoopHost?.TryPublish(CreateSessionSnapshot());
            _renderer.DrawCompanionDeath(battleCharacter.Name);
            _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
        }
    }

    private void SetHelpVisibility(PlayerId playerId, CharacterId characterId, bool isOpen)
    {
        var characterName = CharacterRoster.Party.Members
            .FirstOrDefault(character => character.Id == characterId)?.Name ?? "Egy játékos";
        if (isOpen)
        {
            if (!_helpPausePlayers.Add(playerId)) return;
            _helpPauseStartedUtc ??= DateTime.UtcNow;
            var message = $"⏸ {characterName} megnyitotta a súgót. A közös játék szünetel.";
            RecordSessionActivity(SessionActivityKind.System, message, ConsoleColor.Yellow);
            if (playerId != _session.HostPlayerId)
                _renderer.DrawInventoryMessage(message, ConsoleColor.Yellow);
            return;
        }

        if (!_helpPausePlayers.Remove(playerId)) return;
        var resumedMessage = $"▶ {characterName} bezárta a súgót.";
        RecordSessionActivity(SessionActivityKind.System, resumedMessage, ConsoleColor.Green);
        if (playerId != _session.HostPlayerId)
            _renderer.DrawInventoryMessage(resumedMessage, ConsoleColor.Green);
        if (_helpPausePlayers.Count > 0 || _helpPauseStartedUtc is not { } pauseStarted) return;

        var pauseDuration = DateTime.UtcNow - pauseStarted;
        _helpPauseStartedUtc = null;
        _nextNeedsDrain += pauseDuration;
        foreach (var characterIdKey in _nextEnemyMoves.Keys.ToArray())
            _nextEnemyMoves[characterIdKey] += pauseDuration;
    }

    private void DrainNeeds()
    {
        var hungry = _gameData.GetStatus(CharacterStatusIds.Hungry);
        var thirsty = _gameData.GetStatus(CharacterStatusIds.Thirsty);
        var followers = _maze.PartyMembers.Where(member => member.IsTemporaryFollower)
            .Select(member => member.Character);
        foreach (var character in CharacterRoster.Party.Members.Concat(followers).Distinct()
                     .Where(character => character.IsAlive))
        {
            var foodBefore = character.FoodLevel;
            var waterBefore = character.WaterLevel;
            var foodLoss = 2 + character.MaximumVitality / 60;
            character.ConsumeFood(foodLoss);
            var waterLoss = 2;
            if (character.CurrentVitality < character.MaximumVitality) waterLoss++;
            if (character.CurrentVitality * 2 < character.MaximumVitality) waterLoss++;
            character.ConsumeWater(waterLoss);
            character.SynchronizeNeedStatuses(hungry, thirsty);
            if (IsAutonomousNpc(character))
            {
                LogNewZeroNeed(character, NpcComplaintKind.Hunger, foodBefore, character.FoodLevel);
                LogNewZeroNeed(character, NpcComplaintKind.Thirst, waterBefore, character.WaterLevel);
                TryNpcUseConsumables(character);
            }
        }
        _renderer.RefreshCharacterSheet(SelectedCharacter);
    }

    private int DrainNeedsAfterBattle(LiveCharacter character, int monsterTier)
    {
        var foodBefore = character.FoodLevel;
        var waterBefore = character.WaterLevel;
        var loss = _random.Next(1, 6) + Math.Clamp(monsterTier, 1, 5);
        character.ConsumeFood(loss);
        character.ConsumeWater(loss);
        character.SynchronizeNeedStatuses(
            _gameData.GetStatus(CharacterStatusIds.Hungry),
            _gameData.GetStatus(CharacterStatusIds.Thirsty));
        if (IsAutonomousNpc(character))
        {
            LogNewZeroNeed(character, NpcComplaintKind.Hunger, foodBefore, character.FoodLevel);
            LogNewZeroNeed(character, NpcComplaintKind.Thirst, waterBefore, character.WaterLevel);
        }
        return loss;
    }

    private bool IsAutonomousNpc(LiveCharacter character) =>
        character != SelectedCharacter && !_session.IsHumanControlled(character.Id) &&
        _maze.PartyMembers.Any(member => member.Character == character);

    private void TryNpcUseConsumables(LiveCharacter character)
    {
        if (!character.IsAlive || !IsAutonomousNpc(character)) return;
        if (character.HasStatus(CharacterStatusIds.Hungry))
            TryNpcConsumeNeedItems(character, ConsumableEffect.Food, NpcComplaintKind.Hunger);
        else ClearNpcShortage(character, NpcComplaintKind.Hunger);
        if (character.HasStatus(CharacterStatusIds.Thirsty))
            TryNpcConsumeNeedItems(character, ConsumableEffect.Water, NpcComplaintKind.Thirst);
        else ClearNpcShortage(character, NpcComplaintKind.Thirst);
        if (character.CurrentVitality < character.MaximumVitality)
            TryNpcConsumeHealingPotions(character);
        if (character.CurrentVitality * 2 >= character.MaximumVitality || HasHealingPotion(character))
            ClearNpcShortage(character, NpcComplaintKind.Injured);
        character.SynchronizeNeedStatuses(_gameData.GetStatus(CharacterStatusIds.Hungry),
            _gameData.GetStatus(CharacterStatusIds.Thirsty));
    }

    private void TryNpcConsumeNeedItems(LiveCharacter character, ConsumableEffect effect, NpcComplaintKind kind)
    {
        var desiredServings = _random.Next(1, 4);
        var consumed = new List<string>();
        for (var serving = 0; serving < desiredServings; serving++)
        {
            var current = effect == ConsumableEffect.Food ? character.FoodLevel : character.WaterLevel;
            if (current >= 100) break;
            var candidates = BackpackConsumables(character, effect)
                .Where(entry => Math.Max(0, current + entry.Item.EffectValue - 100) <= 15)
                .ToArray();
            if (candidates.Length == 0) break;
            var selected = candidates[_random.Next(candidates.Length)];
            if (!character.RemoveOneInventoryItem(InventorySlotKind.Backpack, selected.Index)) break;
            if (effect == ConsumableEffect.Food) character.RestoreFood(selected.Item.EffectValue);
            else if (string.Equals(selected.Item.Id, MiscItemIds.HerbalTea, StringComparison.OrdinalIgnoreCase))
                UseHerbalTea(character, selected.Item.EffectValue);
            else if (IsInitiativeDrink(selected.Item)) UseInitiativeDrink(character, selected.Item);
            else character.RestoreWater(selected.Item.EffectValue);
            consumed.Add(selected.Item.Name);
        }
        if (consumed.Count > 0)
        {
            ClearNpcShortage(character, kind);
            var level = effect == ConsumableEffect.Food ? character.FoodLevel : character.WaterLevel;
            var action = effect == ConsumableEffect.Food ? "evett" : "ivott";
            LogNpcAutomation(character, $"{character.Name} {action}: {string.Join(", ", consumed)}. " +
                $"{(effect == ConsumableEffect.Food ? "🍖" : "💧")} {level}/100.", ConsoleColor.Cyan);
            return;
        }
        ReportNpcShortageOnce(character, kind, effect == ConsumableEffect.Food
            ? $"{character.Name} éhes, de nincs pazarlás nélkül elfogyasztható étele."
            : $"{character.Name} szomjas, de nincs pazarlás nélkül elfogyasztható itala.");
    }

    private void TryNpcConsumeHealingPotions(LiveCharacter character)
    {
        var desiredServings = _random.Next(1, 4);
        var consumed = new List<string>();
        for (var serving = 0; serving < desiredServings; serving++)
        {
            var missingVitality = character.MaximumVitality - character.CurrentVitality;
            if (missingVitality <= 0) break;
            var candidates = BackpackConsumables(character, ConsumableEffect.Heal)
                .Where(entry => Math.Max(0, character.PreviewVitalityRecovery(entry.Item.EffectValue) - missingVitality) <= 15)
                .ToArray();
            if (candidates.Length == 0) break;
            var selected = candidates[_random.Next(candidates.Length)];
            if (!character.RemoveOneInventoryItem(InventorySlotKind.Backpack, selected.Index)) break;
            character.RestoreVitality(selected.Item.EffectValue);
            consumed.Add(selected.Item.Name);
        }
        if (consumed.Count > 0)
        {
            ClearNpcShortage(character, NpcComplaintKind.Injured);
            LogNpcAutomation(character, $"{character.Name} gyógyitalt ivott: {string.Join(", ", consumed)}. " +
                $"❤️ {character.CurrentVitality}/{character.MaximumVitality}.", ConsoleColor.Green);
        }
        else if (character.CurrentVitality * 2 < character.MaximumVitality && !HasHealingPotion(character))
            ReportNpcShortageOnce(character, NpcComplaintKind.Injured,
                $"{character.Name} súlyosan sérült, de nincs használható gyógyitala.");
    }

    private static IEnumerable<(int Index, MiscItemDefinition Item)> BackpackConsumables(
        LiveCharacter character, ConsumableEffect effect) => Enumerable.Range(0, LiveCharacter.MaximumBackpackItemCount)
        .Select(index => (Index: index,
            Item: character.GetInventoryItem(InventorySlotKind.Backpack, index) as MiscItemDefinition))
        .Where(entry => entry.Item?.Effect == effect)
        .Select(entry => (entry.Index, entry.Item!));

    private static bool HasHealingPotion(LiveCharacter character) =>
        BackpackConsumables(character, ConsumableEffect.Heal).Any();

    private void LogNewZeroNeed(LiveCharacter character, NpcComplaintKind kind, int previous, int current)
    {
        if (previous <= 0 || current > 0) return;
        var message = kind == NpcComplaintKind.Hunger
            ? $"{character.Name} élelemszintje nullára csökkent. Nagyon éhes!"
            : $"{character.Name} vízszintje nullára csökkent. Nagyon szomjas!";
        LogNpcAutomation(character, message, ConsoleColor.Red);
        ScheduleNpcComplaint(character, kind, DateTime.UtcNow);
    }

    private void ProcessNpcComplaints(DateTime now)
    {
        foreach (var character in _maze.PartyMembers.Select(member => member.Character).Distinct()
                     .Where(IsAutonomousNpc))
        {
            ProcessNpcComplaint(character, NpcComplaintKind.Hunger, character.FoodLevel == 0,
                $"{character.Name}: Nagyon éhes vagyok, elfogyott az élelmem!", now);
            ProcessNpcComplaint(character, NpcComplaintKind.Thirst, character.WaterLevel == 0,
                $"{character.Name}: Nagyon szomjas vagyok, nincs mit innom!", now);
            ProcessNpcComplaint(character, NpcComplaintKind.Injured,
                character.CurrentVitality * 2 < character.MaximumVitality && !HasHealingPotion(character),
                $"{character.Name}: Súlyosan megsérültem, és nincs gyógyitalom!", now);
        }
    }

    private void ProcessNpcSelfCare(DateTime now)
    {
        foreach (var character in _maze.PartyMembers.Select(member => member.Character).Distinct()
                     .Where(IsAutonomousNpc))
        {
            if (character.IsAlive && character.CurrentVitality < character.MaximumVitality)
                TryNpcConsumeHealingPotions(character);
            if (character.CurrentVitality * 2 >= character.MaximumVitality || HasHealingPotion(character))
                ClearNpcShortage(character, NpcComplaintKind.Injured);
        }
        ProcessNpcComplaints(now);
    }

    private void ProcessNpcComplaint(LiveCharacter character, NpcComplaintKind kind, bool active,
        string message, DateTime now)
    {
        var key = (character.Id, kind);
        if (!active)
        {
            _nextNpcComplaints.Remove(key);
            return;
        }
        if (!_nextNpcComplaints.TryGetValue(key, out var next))
        {
            ScheduleNpcComplaint(character, kind, now);
            return;
        }
        if (now < next) return;
        LogNpcAutomation(character, message, ConsoleColor.DarkYellow);
        ScheduleNpcComplaint(character, kind, now);
    }

    private void ScheduleNpcComplaint(LiveCharacter character, NpcComplaintKind kind, DateTime from) =>
        _nextNpcComplaints[(character.Id, kind)] = from + TimeSpan.FromSeconds(_random.Next(120, 181));

    private void ReportNpcShortageOnce(LiveCharacter character, NpcComplaintKind kind, string message)
    {
        if (!_reportedNpcShortages.Add((character.Id, kind))) return;
        LogNpcAutomation(character, message, ConsoleColor.DarkYellow);
    }

    private void ClearNpcShortage(LiveCharacter character, NpcComplaintKind kind)
    {
        _reportedNpcShortages.Remove((character.Id, kind));
        _nextNpcComplaints.Remove((character.Id, kind));
    }

    private void LogNpcAutomation(LiveCharacter character, string message, ConsoleColor color)
    {
        _renderer.DrawInventoryMessage(message, color);
        RecordSessionActivity(SessionActivityKind.System, message, color);
    }

    private void PlayBattleRoundSound(BattleLogEntry entry)
    {
        if (entry.Kind is not (BattleLogKind.PlayerAttack or BattleLogKind.EnemyAttack or BattleLogKind.CriticalHit)) return;
        var battle = _activeBattleState;
        var listeners = battle is not null
            ? new[] { battle.PlayerCharacterId, SelectedCharacter.Id }.Distinct().ToArray()
            : [SelectedCharacter.Id];
        var missed = entry.Message.Contains("💨", StringComparison.Ordinal);
        var enemyHitPlayer = !missed && battle is not null &&
            entry.Message.Contains($"{battle.Enemy.Name} támadja {battle.Player.Name}", StringComparison.Ordinal);
        if (enemyHitPlayer)
            PlaySessionSound(SoundEffect.PlayerGotHit, [battle!.PlayerCharacterId]);
        else
            PlaySessionSound(missed ? SoundEffect.Miss : SoundEffect.Hit, listeners);
    }

    private void PresentBattleEntries(IEnumerable<BattleLogEntry> entries)
    {
        foreach (var entry in entries)
        {
            _renderer.DrawBattleRound(entry);
            RecordSessionActivity(SessionActivityKind.Battle, entry.Message, BattleEntryColor(entry.Kind));
            PlayBattleRoundSound(entry);
            _renderer.RefreshBattleStatusRows();
        }
    }

    private void RecordSessionActivity(SessionActivityKind kind, string message, ConsoleColor color,
        IReadOnlyCollection<CharacterId>? listeners = null)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        _sessionActivities.Enqueue(new SessionActivitySnapshot(++_sessionActivitySequence, kind, message, color,
            listeners?.Distinct().ToArray()));
        while (_sessionActivities.Count > 24) _sessionActivities.Dequeue();
    }

    private void PlayCharacterStepSound(LiveCharacter character)
    {
        switch (_random.Next(5))
        {
            case 0: PlaySessionSound(SoundEffect.Step1, [character.Id]); break;
            case 1: PlaySessionSound(SoundEffect.Step2, [character.Id]); break;
        }
    }

    private void PlayBattleVictorySound() =>
        PlaySessionSound(_random.Next(2) == 0 ? SoundEffect.Victory : SoundEffect.Victory2);

    private void PlaySessionSound(SoundEffect effect, IReadOnlyCollection<CharacterId>? listeners = null)
    {
        var listenerIds = listeners?.Distinct().ToArray();
        RecordSessionSound(effect, listenerIds);
        if (listenerIds is not null && !listenerIds.Contains(SelectedCharacter.Id)) return;
        _soundEffects.Play(effect);
    }

    private void RecordSessionSound(SoundEffect effect, IReadOnlyList<CharacterId>? listenerCharacterIds)
    {
        _sessionSounds.Enqueue(new SessionSoundSnapshot(++_sessionSoundSequence, effect, listenerCharacterIds));
        while (_sessionSounds.Count > 48) _sessionSounds.Dequeue();
    }

    private static ConsoleColor BattleEntryColor(BattleLogKind kind) => kind switch
    {
        BattleLogKind.PlayerAttack => ConsoleColor.Green,
        BattleLogKind.EnemyAttack => ConsoleColor.Red,
        BattleLogKind.CriticalHit => ConsoleColor.Yellow,
        _ => ConsoleColor.Gray
    };

    private void TeleportLeaderNearExit()
    {
        Position? destination = Directions
            .Select(direction => _maze.Exit + direction)
            .Where(position => _maze.IsWalkable(position) && _maze.GetObjectAt(position) is null)
            .OrderBy(position => Manhattan(position, _player.Position))
            .Select(position => (Position?)position)
            .FirstOrDefault();
        if (destination is null)
        {
            _renderer.DrawDeveloperMessage("Fejlesztői mód: nincs üres járható mező a kijárat mellett.");
            return;
        }

        _player.TeleportTo(destination.Value);
        _leaderTrail.Clear();
        _leaderTrail.Add(destination.Value);
        RevealFor(SelectedCharacter, destination.Value);
        // A fejlesztői teleport közvetlenül is jelzi a kijárat elérését; ne függjön
        // attól, hogy az általános látómező-frissítés új cellának számította-e a kijáratot.
        _backgroundMusic.MarkExitDiscovered();
        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, destination.Value);
        _renderer.DrawDeveloperMessage("Fejlesztői mód: a partyvezér a kijárat mellé teleportált.");
    }

    private void TeleportLeaderToNextUniqueNpc()
    {
        var targets = _gameData.NpcEncounters
            .Where(encounter => _gameData.GetNpc(encounter.NpcId).Unique)
            .GroupBy(encounter => encounter.NpcId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderBy(encounter => encounter.MazeLevel).First())
            .OrderBy(encounter => encounter.MazeLevel)
            .ThenBy(encounter => encounter.NpcId, StringComparer.OrdinalIgnoreCase)
            .Select(encounter => new DeveloperUniqueNpcTarget(_gameData.GetNpc(encounter.NpcId),
                encounter.MazeLevel))
            .ToArray();
        if (targets.Length == 0)
        {
            _renderer.DrawDeveloperMessage("Fejlesztői mód: nincs pályához rendelt egyedi NPC.");
            return;
        }

        _lastDeveloperUniqueNpcIndex = (_lastDeveloperUniqueNpcIndex + 1) % targets.Length;
        var target = targets[_lastDeveloperUniqueNpcIndex];
        if (!TryFindUniqueNpcPosition(target.Definition, out var npcPosition))
        {
            RemoveStaleUniqueNpcCharacter(target.Definition);
            _mazeLevel = target.MazeLevel;
            StartNewMaze(showLevelImage: false);
            if (!TryFindUniqueNpcPosition(target.Definition, out npcPosition))
            {
                _renderer.DrawDeveloperMessage($"Fejlesztői mód: {target.Definition.Name} nem helyezhető el a(z) " +
                    $"{target.MazeLevel}. pályán.");
                return;
            }
        }

        Position? destination = Directions
            .Select(direction => npcPosition + direction)
            .Where(IsFreeDeveloperTeleportDestination)
            .OrderBy(position => Manhattan(position, _player.Position))
            .Select(position => (Position?)position)
            .FirstOrDefault();
        destination ??= FindNearbyFreePositions(npcPosition)
            .Where(position => _maze.GetTrapAt(position) is null && _maze.GetDoorAt(position) is null)
            .Select(position => (Position?)position)
            .FirstOrDefault();
        if (destination is null)
        {
            _renderer.DrawDeveloperMessage($"Fejlesztői mód: nincs szabad mező {target.Definition.Name} mellett.");
            return;
        }

        _player.TeleportTo(destination.Value);
        _leaderTrail.Clear();
        _leaderTrail.Add(destination.Value);
        RevealFor(SelectedCharacter, destination.Value);
        RevealFor(SelectedCharacter, npcPosition);
        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, destination.Value);
        _renderer.DrawDeveloperMessage($"Fejlesztői mód: egyedi NPC " +
            $"{_lastDeveloperUniqueNpcIndex + 1}/{targets.Length} — {target.Definition.Name}, " +
            $"{_mazeLevel}. pálya.");
        _activeCoopHost?.TryPublish(CreateSessionSnapshot());
    }

    private bool TryFindUniqueNpcPosition(NpcDefinition definition, out Position position)
    {
        var worldNpc = _maze.WorldNpcs.FirstOrDefault(npc =>
            string.Equals(npc.DefinitionId, definition.Id, StringComparison.OrdinalIgnoreCase));
        if (worldNpc is not null)
        {
            position = worldNpc.Position;
            return true;
        }

        var avatar = _maze.PartyMembers.FirstOrDefault(member =>
            string.Equals(member.TemporaryFollower?.DefinitionId, definition.Id,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(member.Character.Name, definition.Name, StringComparison.OrdinalIgnoreCase));
        if (avatar is not null)
        {
            position = avatar.Position;
            return true;
        }

        if (string.Equals(SelectedCharacter.Name, definition.Name, StringComparison.OrdinalIgnoreCase))
        {
            position = _player.Position;
            return true;
        }

        position = default;
        return false;
    }

    private void RemoveStaleUniqueNpcCharacter(NpcDefinition definition)
    {
        var partyMembers = CharacterRoster.Party.Members.ToHashSet();
        foreach (var character in CharacterRoster.Characters.Where(character =>
                     !partyMembers.Contains(character) &&
                     string.Equals(character.Name, definition.Name, StringComparison.OrdinalIgnoreCase)).ToArray())
            CharacterRoster.Remove(character);
    }

    private bool IsFreeDeveloperTeleportDestination(Position position) =>
        _maze.IsWalkable(position) && _maze.GetObjectAt(position) is null &&
        _maze.GetTrapAt(position) is null && _maze.GetDoorAt(position) is null;

    private void ToggleDeveloperPhasing()
    {
        _developerPhasing = !_developerPhasing;
        _renderer.DrawDeveloperMessage(_developerPhasing
            ? "Fejlesztői mód: fal-áthaladás engedélyezve."
            : "Fejlesztői mód: fal-áthaladás letiltva.");
    }

    private sealed record HeldInventoryItem(IItemDefinition Item, InventorySlotReference Source, long SourceRevision);
    private sealed record DeveloperUniqueNpcTarget(NpcDefinition Definition, int MazeLevel);
    private sealed record SpellCastAttempt(bool ConsumesTurn, string Message, BattleLogKind Kind,
        int DamageToCurrentEnemy = 0, int ExtraPlayerActions = 0);
    private sealed record SpellExecutionResult(int DamageToCurrentEnemy, int ExtraPlayerActions, string Summary);
    private sealed record SpellResolutionResult(bool Applies, bool Half, bool Critical, string Text);

    private void FillPartyForDevelopment(IReadOnlyList<string> characterClassIds, string setName)
    {
        if (CharacterRoster.Party.Members.Count >= Party.MaximumSize)
        {
            _renderer.DrawDeveloperMessage("Fejlesztői mód: a parti már teljes (4/4). ");
            return;
        }

        var generator = new RandomCharacterGenerator(_gameData, _random);
        var added = new List<LiveCharacter>();
        foreach (var characterClassId in characterClassIds)
        {
            if (CharacterRoster.Party.Members.Count >= Party.MaximumSize) break;
            var member = generator.CreateDevelopmentCharacter(_gameData.GetCharacterClass(characterClassId),
                CharacterRoster.Characters.Select(character => character.Name).ToList());
            CharacterRoster.Add(member);
            CharacterRoster.Party.Add(member);
            added.Add(member);
        }
        PlacePartyMembersNear(_player.Position);
        foreach (var member in _maze.PartyMembers) RevealFor(member.Character, member.Position);
        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
        _renderer.RefreshCharacterSheet(SelectedCharacter);
        _renderer.DrawDeveloperMessage($"Fejlesztői mód: {setName} osztályszett hozzáadva: " +
            string.Join(", ", added.Select(member => $"{member.Name} ({member.CharacterClass.Name})")) + ".");
    }

    private void AddLevelOnePartyMemberForDevelopment()
    {
        if (CharacterRoster.Party.Members.Count >= Party.MaximumSize)
        {
            _renderer.DrawDeveloperMessage("Fejlesztői mód: a parti már teljes (4/4). ");
            return;
        }

        var generator = new RandomCharacterGenerator(_gameData, _random);
        var member = generator.CreateLevelOne(CharacterRoster.Characters.Select(character => character.Name).ToList());
        CharacterRoster.Add(member);
        CharacterRoster.Party.Add(member);
        PlacePartyMembersNear(_player.Position);
        foreach (var avatar in _maze.PartyMembers) RevealFor(avatar.Character, avatar.Position);
        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
        _renderer.RefreshCharacterSheet(SelectedCharacter);
        _renderer.DrawDeveloperMessage($"Fejlesztői mód: {member.Name} ({member.CharacterClass.Name}) 1. szinten csatlakozott. Profil: {NpcBehaviorName(member.NpcBehavior)}.");
    }

    private void PlacePartyMembersNear(Position origin)
    {
        var alreadyPlaced = _maze.PartyMembers.Select(member => member.Character).ToHashSet();
        var companions = CharacterRoster.Party.Members.Where(member => member != SelectedCharacter && member.IsAlive && !alreadyPlaced.Contains(member)).ToList();
        if (companions.Count == 0) return;

        var positions = FindNearbyFreePositions(origin).Take(companions.Count).ToList();
        for (var index = 0; index < Math.Min(companions.Count, positions.Count); index++)
        {
            if (companions[index].NpcBehavior is null) companions[index].SetNpcBehavior(NpcBehavior.Defensive);
            var avatar = new PartyMemberAvatar(positions[index], companions[index]);
            _maze.AddPartyMember(avatar);
            _nextPartyMoves[avatar] = DateTime.UtcNow + TimeSpan.FromMilliseconds(_random.Next(80, MaximumPartyMoveDelayMilliseconds + 1));
        }
    }

    private IEnumerable<Position> FindNearbyFreePositions(Position origin)
    {
        var yielded = new HashSet<Position>();
        if (_maze.StartingRoom is { } startingRoom && startingRoom.Contains(origin))
        {
            foreach (var position in startingRoom.InteriorPositions()
                         .Where(position => position != origin && _maze.GetObjectAt(position) is null && !IsStartingRoomDoorApproach(startingRoom, position))
                         .OrderByDescending(position => Math.Abs(position.X - origin.X) + Math.Abs(position.Y - origin.Y)))
            {
                yielded.Add(position);
                yield return position;
            }
        }

        var visited = new HashSet<Position> { origin };
        var queue = new Queue<Position>();
        queue.Enqueue(origin);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var direction in Directions)
            {
                var next = current + direction;
                if (!visited.Add(next) || !_maze.IsWalkable(next)) continue;
                queue.Enqueue(next);
                if (!yielded.Contains(next) && next != _maze.Entrance && next != _maze.Exit && next != _player.Position && _maze.GetObjectAt(next) is null)
                {
                    yielded.Add(next);
                    yield return next;
                }
            }
        }
    }

    private bool IsStartingRoomDoorApproach(Room room, Position position)
    {
        var rightBoundary = new Position(room.TopLeft.X + room.Width, position.Y);
        if (position.X == room.TopLeft.X + room.Width - 1 && _maze.IsInside(rightBoundary) &&
            _maze.GetDoorAt(rightBoundary) is not null) return true;

        var bottomBoundary = new Position(position.X, room.TopLeft.Y + room.Height);
        return position.Y == room.TopLeft.Y + room.Height - 1 && _maze.IsInside(bottomBoundary) &&
            _maze.GetDoorAt(bottomBoundary) is not null;
    }

    private LevelUpResult AddExperience(int amount) => SelectedCharacter.AddExperience(
        amount,
        _gameData.ExperienceByLevel,
        _gameData.GetVitalityGrowth(SelectedCharacter.Abilities.Health),
        _gameData.GetManaGrowth(SelectedCharacter.Abilities.Intelligence),
        _gameData.GetCharacterResourceGrowth(SelectedCharacter.CharacterClass.Id),
        _random);

    private IReadOnlyList<ExperienceAward> DistributeExperience(LiveCharacter winner, int totalExperience)
    {
        var total = Math.Max(0, totalExperience);
        var others = CharacterRoster.Party.Members
            .Where(character => character != winner && character.IsAlive)
            .ToList();
        if (others.Count == 0)
            return [AwardExperience(winner, total)];

        var winnerShare = total * 60 / 100;
        var remainder = total - winnerShare;
        var sharedBase = remainder / others.Count;
        var sharedRemainder = remainder % others.Count;
        var awards = new List<ExperienceAward> { AwardExperience(winner, winnerShare) };
        for (var index = 0; index < others.Count; index++)
            awards.Add(AwardExperience(others[index], sharedBase + (index < sharedRemainder ? 1 : 0)));
        return awards;
    }

    private static readonly HashSet<string> MerchantExcludedItemIds = ["W001", "W005", "A001", "A002",
        // Witcher-only consumables (potions and medical supplies)
        "T011", "T012", "T013", "T014", "T015", "T016", "T017", "T018", "T019", "T020",
        // Secret-stash-only drinks
        "T023", "T024"];


    private IReadOnlyList<IItemDefinition> AllTradableItems() => _gameData.Items.Cast<IItemDefinition>()
        .Concat(_gameData.Weapons).Concat(_gameData.Armors).Concat(_gameData.MagicItems)
        .Where(item => !SpellcastingRules.IsRestrictedFromTradingAndGeneration(item))
        .Where(item => !MerchantExcludedItemIds.Contains(item.Id)).ToList();

    private ExperienceAward AwardExperience(LiveCharacter character, int amount) => new(character,
        character.AddExperience(amount, _gameData.ExperienceByLevel,
            _gameData.GetVitalityGrowth(character.Abilities.Health),
            _gameData.GetManaGrowth(character.Abilities.Intelligence),
            _gameData.GetCharacterResourceGrowth(character.CharacterClass.Id), _random));

    private LevelUpResult AwardExperienceResult(LiveCharacter character, int amount) =>
        AwardExperience(character, amount).Result;

    private static string FormatExperienceAwards(IEnumerable<ExperienceAward> awards) => string.Join("; ", awards.Select(award =>
        $"{award.Character.Name} +{award.Result.GainedExperience}" +
        (award.Result.LeveledUp ? $" (L{award.Result.PreviousLevel}→L{award.Result.CurrentLevel})" : string.Empty)));

    private void GrantPartyExperienceForDevelopment()
    {
        var awards = CharacterRoster.Party.Members.Where(character => character.IsAlive)
            .Select(character => AwardExperience(character, 5000)).ToList();
        foreach (var award in awards.Where(award => award.Result.LeveledUp))
            ResolvePerkOffers(award.Character, award.Result);
        _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
        _renderer.RefreshCharacterSheet(SelectedCharacter);
        _renderer.DrawDeveloperMessage($"Fejlesztői mód: 5000 XP minden partitagnak. {FormatExperienceAwards(awards)}");
    }

    private void TriggerDeveloperLevelUp()
    {
        var neededExperience = SelectedCharacter.GetExperienceNeededForNextLevel(_gameData.ExperienceByLevel);
        if (neededExperience <= 0)
        {
            _renderer.DrawDeveloperMessage("Fejlesztői mód: a karakter már elérte a maximális szintet.");
            return;
        }

        var result = AddExperience(neededExperience);
        ResolvePerkOffers(SelectedCharacter, result);
        _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
    }

    private void ResolvePerkOffers(LiveCharacter character, LevelUpResult result)
    {
        var offers = CreatePerkOffers(character, result);
        var control = _session.CharacterControls.FirstOrDefault(candidate => candidate.CharacterId == character.Id);
        if (control is { ControllerKind: CharacterControllerKind.RemotePlayer,
                ConnectionState: PlayerConnectionState.Connected, AssignedPlayerId: not null })
        {
            ResolveRemoteLevelUp(character, result, offers);
            return;
        }
        var selectedPerks = _renderer.DrawLevelUpScreen(character, result, offers);
        foreach (var perk in selectedPerks)
            if (character.AddPerk(perk))
            {
                character.ApplyPerkAcquisitionBonus(perk);
                PlaySessionSound(SoundEffect.NewSkill, [character.Id]);
            }
        if (ShouldChooseSpecialization(character, offers)) ResolveLocalSpecialization(character);
        ResolveLocalClassFeatureUpgrades(character, result);
        ResolveLocalAbilityIncreases(character, result);
        ResolveLocalWeaponProficiencies(character, result);
        ResolveSpellLearning(character, result);
    }

    private void ResolveRemoteLevelUp(LiveCharacter character, LevelUpResult result,
        IReadOnlyList<PerkOffer> offers)
    {
        WaitForRemoteLevelUpChoice(character, result, LevelUpPromptKind.Summary, [],
            offers.Count > 0
                ? "🌠 Új TEHETSÉG ébred benned! Nyomj meg egy billentyűt... 🌠"
                : "🌟 Nyomj meg egy billentyűt a kaland folytatásához! 🌟");
        foreach (var offer in offers)
        {
            var choices = offer.Choices.Select(perk => new LevelUpChoiceSnapshot(perk.Id, perk.Name, perk.Description)).ToArray();
            var selectedId = WaitForRemoteLevelUpChoice(character, result, LevelUpPromptKind.PerkChoice, choices,
                $"{offer.Tier}. tehetségfokozat — a nem választott tehetség végleg elveszik.",
                [new($"{character.Name} — {character.CharacterClass.Name} — {offer.Tier}. fokozat", ConsoleColor.Cyan),
                 new($"A tehetség a {offer.TriggerLevel}. szint elérésekor vált elérhetővé.", ConsoleColor.DarkCyan),
                 new("A nem választott tehetség végleg elveszik ennél a karakternél.", ConsoleColor.Red)]);
            var perk = offer.Choices.FirstOrDefault(candidate => candidate.Id == selectedId) ?? offer.Choices[0];
            if (character.AddPerk(perk))
            {
                character.ApplyPerkAcquisitionBonus(perk);
                PlaySessionSound(SoundEffect.NewSkill, [character.Id]);
            }
            if (offer.Tier == 1) ResolveRemoteSpecialization(character, result);
        }
        if (ShouldChooseSpecialization(character, offers)) ResolveRemoteSpecialization(character, result);
        ResolveRemoteClassFeatureUpgrades(character, result);
        ResolveRemoteAbilityIncreases(character, result);
        ResolveRemoteWeaponProficiencies(character, result);
        ResolveRemoteSpellLearning(character, result);
    }

    private static bool ShouldChooseSpecialization(LiveCharacter character, IReadOnlyList<PerkOffer> offers) =>
        character.SpecializationId is null && ClassSpecializations.ForClass(character.CharacterClass.Id).Count > 0 &&
        (offers.Any(offer => offer.Tier == 1) || character.Perks.Any(perk => perk.Tier == 1));

    private void ResolveLocalSpecialization(LiveCharacter character)
    {
        if (character.SpecializationId is not null) return;
        var choices = ClassSpecializations.ForClass(character.CharacterClass.Id);
        if (choices.Count > 0) character.ChooseSpecialization(_renderer.DrawSpecializationChoice(character, choices).Id);
    }

    private void ResolveRemoteSpecialization(LiveCharacter character, LevelUpResult result)
    {
        if (character.SpecializationId is not null) return;
        var choices = ClassSpecializations.ForClass(character.CharacterClass.Id);
        if (choices.Count == 0) return;
        var projected = choices.Select(choice => new LevelUpChoiceSnapshot(choice.Id, choice.Name, choice.Description)).ToArray();
        var selectedId = WaitForRemoteLevelUpChoice(character, result, LevelUpPromptKind.SpecializationChoice,
            projected, "Válassz végleges papi vagy mágusi specializációt.",
            [new($"{character.Name} — {character.CharacterClass.Name}", ConsoleColor.Cyan),
             new("Ez a választás végleges.", ConsoleColor.Red)]);
        character.ChooseSpecialization(choices.FirstOrDefault(choice => choice.Id == selectedId)?.Id ?? choices[0].Id);
    }

    private static IEnumerable<int> PendingClassFeatureMilestones(LiveCharacter character, LevelUpResult result)
    {
        var acquired = character.ClassFeatureUpgrades.Count;
        foreach (var milestone in new[] { 10, 20 })
        {
            if (result.CurrentLevel < milestone || acquired >= milestone / 10) continue;
            acquired++;
            yield return milestone;
        }
    }

    private void ResolveLocalClassFeatureUpgrades(LiveCharacter character, LevelUpResult result)
    {
        foreach (var milestone in PendingClassFeatureMilestones(character, result).ToArray())
        {
            var choices = ClassFeatureUpgrades.ForClass(character.CharacterClass.Id)
                .Where(choice => !character.HasClassFeatureUpgrade(choice.Id)).ToArray();
            if (choices.Length == 0) return;
            character.ChooseClassFeatureUpgrade(
                _renderer.DrawClassFeatureUpgradeChoice(character, choices, milestone).Id);
        }
    }

    private void ResolveRemoteClassFeatureUpgrades(LiveCharacter character, LevelUpResult result)
    {
        foreach (var milestone in PendingClassFeatureMilestones(character, result).ToArray())
        {
            var choices = ClassFeatureUpgrades.ForClass(character.CharacterClass.Id)
                .Where(choice => !character.HasClassFeatureUpgrade(choice.Id)).ToArray();
            if (choices.Length == 0) return;
            var projected = choices.Select(choice =>
                new LevelUpChoiceSnapshot(choice.Id, choice.Name, choice.Description)).ToArray();
            var selectedId = WaitForRemoteLevelUpChoice(character, result, LevelUpPromptKind.ClassFeatureChoice,
                projected, $"{milestone}. szint — válassz végleges osztályképesség-fejlesztést.",
                [new($"{character.Name} — {character.CharacterClass.Name} — {milestone}. szint", ConsoleColor.Cyan),
                 new("A választás végleges; a 20. szinten egy másik fejlesztés választható.", ConsoleColor.Red)]);
            character.ChooseClassFeatureUpgrade(choices.FirstOrDefault(choice => choice.Id == selectedId)?.Id ?? choices[0].Id);
        }
    }

    private static IReadOnlyList<(string Id, string Name, string Description)> AbilityIncreaseChoices(
        LiveCharacter character)
    {
        var choices = new List<(string, string, string)>();
        if (character.Abilities.Strength < 13)
            choices.Add(("STR", $"💪 Erő: {character.Abilities.Strength} → {character.Abilities.Strength + 1}",
                "Növeli a közelharci sebzést, és a harci osztályok találati bónuszát is elérheti."));
        if (character.Abilities.Dexterity < 13)
            choices.Add(("DEX", $"🏹 Ügyesség: {character.Abilities.Dexterity} → {character.Abilities.Dexterity + 1}",
                "Javítja a fegyveres találatot, a kezdeményezést és az ellenséges támadások elleni védelmet."));
        if (character.Abilities.Health < 13)
            choices.Add(("HEA", $"❤️ Egészség: {character.Abilities.Health} → {character.Abilities.Health + 1}",
                "Azonnal növelheti az alap HP-t, és a következő szintlépések HP-növekedését is javítja."));
        if (character.Abilities.Intelligence < 13)
            choices.Add(("INT", $"🧠 Intelligencia: {character.Abilities.Intelligence} → {character.Abilities.Intelligence + 1}",
                "Erősíti a varázslatokat, csökkenti a kudarcot, és azonnal növelheti az alap mannát is."));
        return choices;
    }

    private void ResolveLocalAbilityIncreases(LiveCharacter character, LevelUpResult result)
    {
        var earned = result.CurrentLevel / 3;
        while (character.AbilityIncreasesClaimed < earned)
        {
            var choices = AbilityIncreaseChoices(character);
            if (choices.Count == 0) { character.ClaimUnspendableAbilityIncrease(); continue; }
            var milestone = (character.AbilityIncreasesClaimed + 1) * 3;
            ApplyAbilityIncrease(character, _renderer.DrawAbilityIncreaseChoice(character, choices, milestone));
        }
    }

    private void ResolveRemoteAbilityIncreases(LiveCharacter character, LevelUpResult result)
    {
        var earned = result.CurrentLevel / 3;
        while (character.AbilityIncreasesClaimed < earned)
        {
            var choices = AbilityIncreaseChoices(character);
            if (choices.Count == 0) { character.ClaimUnspendableAbilityIncrease(); continue; }
            var milestone = (character.AbilityIncreasesClaimed + 1) * 3;
            var projected = choices.Select(choice =>
                new LevelUpChoiceSnapshot(choice.Id, choice.Name, choice.Description)).ToArray();
            var selectedId = WaitForRemoteLevelUpChoice(character, result, LevelUpPromptKind.AbilityChoice,
                projected, $"{milestone}. szint — növelj meg egy képességet 1 ponttal (maximum 13).",
                [new($"{character.Name} — {milestone}. szint", ConsoleColor.Cyan),
                 new("Növelj meg egy képességet 1 ponttal! Maximum: 13.", ConsoleColor.Green)]);
            ApplyAbilityIncrease(character,
                choices.FirstOrDefault(choice => choice.Id == selectedId).Id ?? choices[0].Id);
        }
    }

    private bool ApplyAbilityIncrease(LiveCharacter character, string abilityId)
    {
        var oldVitalityBase = _gameData.GetMinimumVitality(character.Abilities.Health);
        var oldManaBase = character.UsesMana
            ? CharacterClassRules.AdjustStartingMana(character.CharacterClass.Id,
                _gameData.GetMinimumMana(character.Abilities.Intelligence) + character.ManaBonus)
            : 0;
        if (!character.TryIncreaseAbility(abilityId)) return false;
        var newVitalityBase = _gameData.GetMinimumVitality(character.Abilities.Health);
        var newManaBase = character.UsesMana
            ? CharacterClassRules.AdjustStartingMana(character.CharacterClass.Id,
                _gameData.GetMinimumMana(character.Abilities.Intelligence) + character.ManaBonus)
            : 0;
        character.ApplyAbilityResourceIncrease(newVitalityBase - oldVitalityBase, newManaBase - oldManaBase);
        PlaySessionSound(SoundEffect.NewSkill, [character.Id]);
        return true;
    }

    private static int EarnedWeaponProficiencyAdvances(LiveCharacter character, int level) =>
        WeaponProficiencyProgression.EarnedAdvances(character.CharacterClass.Id, level);

    private IReadOnlyList<(string Id, string Name, string Description)> WeaponProficiencyChoices(
        LiveCharacter character) => WeaponFamilies.AvailableFor(character.CharacterClass.Id, _gameData.Weapons)
        .Where(family => character.WeaponProficiencyRankFor(family.Id) != WeaponProficiencyRank.Master &&
                         (character.WeaponProficiencies.Count < 2 ||
                          character.WeaponProficiencyRankFor(family.Id) is not null))
        .Select(family => character.WeaponProficiencyRankFor(family.Id) is null
            ? (family.Id, $"{family.Icon} {family.Name} — Jártas", family.TrainedDescription)
            : (family.Id, $"{family.Icon} {family.Name} — Mester", family.MasterDescription))
        .ToArray();

    private static int NextWeaponProficiencyMilestone(LiveCharacter character)
    {
        var index = character.WeaponProficiencyAdvances;
        return WeaponProficiencyProgression.MilestonesFor(character.CharacterClass.Id).ElementAtOrDefault(index);
    }

    private void ResolveLocalWeaponProficiencies(LiveCharacter character, LevelUpResult result)
    {
        var earned = EarnedWeaponProficiencyAdvances(character, result.CurrentLevel);
        while (character.WeaponProficiencyAdvances < earned)
        {
            var choices = WeaponProficiencyChoices(character);
            if (choices.Count == 0) return;
            var milestone = NextWeaponProficiencyMilestone(character);
            if (character.TryAdvanceWeaponProficiency(
                    _renderer.DrawWeaponProficiencyChoice(character, choices, milestone)))
                PlaySessionSound(SoundEffect.NewWeaponProficiency, [character.Id]);
        }
    }

    private void ResolveRemoteWeaponProficiencies(LiveCharacter character, LevelUpResult result)
    {
        var earned = EarnedWeaponProficiencyAdvances(character, result.CurrentLevel);
        while (character.WeaponProficiencyAdvances < earned)
        {
            var choices = WeaponProficiencyChoices(character);
            if (choices.Count == 0) return;
            var milestone = NextWeaponProficiencyMilestone(character);
            var projected = choices.Select(choice =>
                new LevelUpChoiceSnapshot(choice.Id, choice.Name, choice.Description)).ToArray();
            var selectedId = WaitForRemoteLevelUpChoice(character, result, LevelUpPromptKind.WeaponProficiencyChoice,
                projected, $"{milestone}. szint — válassz fegyverjártassági fejlesztést.",
                [new($"{character.Name} — {(milestone == 1 ? "karakteralkotás" : $"{milestone}. szint")}", ConsoleColor.Cyan),
                 new("Legfeljebb két fegyvercsalád tanulható; egy család Jártas, majd Mester lehet.", ConsoleColor.Green)]);
            if (character.TryAdvanceWeaponProficiency(
                    choices.FirstOrDefault(choice => choice.Id == selectedId).Id ?? choices[0].Id))
                PlaySessionSound(SoundEffect.NewWeaponProficiency, [character.Id]);
        }
    }

    private void ResolveRemoteSpellLearning(LiveCharacter character, LevelUpResult result)
    {
        if (!character.IsSpellcaster) return;
        var simulatedKnown = character.KnownSpells.Select(spell => spell.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var learningCount = result.Bonuses.Count(bonus =>
        {
            if (!SpellcastingRules.TryGetSchool(character.CharacterClass.Id, out var school)) return false;
            var candidate = _gameData.Spells.FirstOrDefault(spell => spell.School == school &&
                spell.Level <= SpellcastingRules.MaximumSpellLevel(bonus.Level) && !simulatedKnown.Contains(spell.Id));
            if (candidate is null) return false;
            simulatedKnown.Add(candidate.Id);
            return true;
        });
        var learnedNumber = 0;
        foreach (var bonus in result.Bonuses)
        {
            var choices = SpellcastingRules.AvailableUnknownSpells(character, _gameData, bonus.Level);
            if (choices.Count == 0) continue;
            learnedNumber++;
            var projected = choices.Select(spell => new LevelUpChoiceSnapshot(spell.Id,
                $"{spell.Level}. szint — {spell.Name}", spell.Description)).ToArray();
            var selectedId = WaitForRemoteLevelUpChoice(character, result, LevelUpPromptKind.SpellChoice,
                projected, $"{learnedNumber}/{learningCount}. új varázslat");
            if (character.LearnSpell(choices.FirstOrDefault(spell => spell.Id == selectedId) ?? choices[0]))
                PlaySessionSound(SoundEffect.NewSpellUnlocked, [character.Id]);
        }
    }

    private string? WaitForRemoteLevelUpChoice(LiveCharacter character, LevelUpResult result,
        LevelUpPromptKind kind, IReadOnlyList<LevelUpChoiceSnapshot> choices, string message,
        IReadOnlyList<LevelUpTextLineSnapshot>? contextLines = null)
    {
        var previousPhase = _session.Phase;
        _activeLevelUpPrompt = new LevelUpPromptSnapshot(Guid.NewGuid(), character.Id, character.Name, kind,
            result.PreviousLevel, result.CurrentLevel, result.VitalityGained, result.ManaGained, choices, message,
            result.Bonuses.Select(bonus =>
                new LevelUpBonusSnapshot(bonus.Level, bonus.Vitality, bonus.Mana)).ToArray(), contextLines);
        _levelUpResponse = null;
        _levelUpPromptCompleted = false;
        _session.SetPhase(GameSessionPhase.Paused);
        _renderer.DrawInventoryMessage(
            $"⌛ Várakozás {character.Name} szintlépési döntésére... ⌛", ConsoleColor.Yellow);
        PlaySessionSound(SoundEffect.Waiting, [SelectedCharacter.Id]);
        _activeCoopHost?.TryPublish(CreateSessionSnapshot());
        while (!_levelUpPromptCompleted)
        {
            ProcessSessionCommands();
            var stillConnected = _session.CharacterControls.Any(control => control.CharacterId == character.Id &&
                control.ControllerKind == CharacterControllerKind.RemotePlayer &&
                control.ConnectionState == PlayerConnectionState.Connected);
            if (!stillConnected) break;
            if (_activeCoopHost?.ShouldPublish(DateTime.UtcNow) == true)
                _activeCoopHost.TryPublish(CreateSessionSnapshot());
            Thread.Sleep(20);
        }
        var response = _levelUpResponse;
        _activeLevelUpPrompt = null;
        _levelUpResponse = null;
        _levelUpPromptCompleted = false;
        _session.SetPhase(previousPhase);
        _activeCoopHost?.TryPublish(CreateSessionSnapshot());
        return response;
    }

    private void ResolveSpellLearning(LiveCharacter character, LevelUpResult result)
    {
        if (!character.IsSpellcaster) return;
        var simulatedKnown = character.KnownSpells.Select(spell => spell.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var learningCount = 0;
        foreach (var bonus in result.Bonuses)
        {
            if (!SpellcastingRules.TryGetSchool(character.CharacterClass.Id, out var school)) break;
            var simulatedChoice = _gameData.Spells.FirstOrDefault(spell => spell.School == school &&
                spell.Level <= SpellcastingRules.MaximumSpellLevel(bonus.Level) && !simulatedKnown.Contains(spell.Id));
            if (simulatedChoice is null) continue;
            simulatedKnown.Add(simulatedChoice.Id);
            learningCount++;
        }
        var learnedNumber = 0;
        foreach (var bonus in result.Bonuses)
        {
            var choices = SpellcastingRules.AvailableUnknownSpells(character, _gameData, bonus.Level);
            if (choices.Count > 0)
            {
                learnedNumber++;
                if (character.LearnSpell(_renderer.DrawSpellLearningScreen(character, choices,
                        learnedNumber, learningCount)))
                    PlaySessionSound(SoundEffect.NewSpellUnlocked, [character.Id]);
            }
        }
    }

    private sealed record ExperienceAward(LiveCharacter Character, LevelUpResult Result);

    private enum NpcComplaintKind { Hunger, Thirst, Injured }

    private sealed record BossNarrative(string ChapterTitle, IReadOnlyList<string> Speech);

    private IReadOnlyList<PerkOffer> CreatePerkOffers(LiveCharacter character, LevelUpResult result)
    {
        var offers = new List<PerkOffer>();
        for (var tier = 1; tier <= 3; tier++)
        {
            if (character.Perks.Any(perk => perk.Tier == tier)) continue;
            var milestone = PerkProgressionRules.TriggerLevel(character.Race, tier);
            if (result.CurrentLevel < milestone) continue;

            // Régi mentésnél a következő szintlépés pótolja a már elhagyott, de ki nem választott tehetséget.
            var triggerLevel = result.PreviousLevel < milestone ? milestone : result.CurrentLevel;
            offers.Add(new PerkOffer(tier, triggerLevel, _gameData.GetPerkChoices(character.CharacterClass.Id, tier)));
        }
        return offers;
    }

}
