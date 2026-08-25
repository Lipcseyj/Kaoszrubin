using MazeGame.Data;
using MazeGame.Domain.Characters;
using MazeGame.Combat;
using MazeGame.Domain.Inventory;
using MazeGame.Domain.Combat;
using MazeGame.Domain.Magic;
using MazeGame.UI;
using static MazeGame.GameInput;

namespace MazeGame;

/// <summary>A játék futását és felhasználói bemenetét koordinálja.</summary>
public sealed class Game
{
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
    private const int VisionRange = 5;
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
    private readonly GameSaveData? _loadedState;
    private readonly SoundEffects _soundEffects;
    private bool _battleStarted;
    private bool _gameOver;
    private bool _characterSheetFocused;
    private HeldInventoryItem? _heldInventoryItem;
    private DateTime _nextNeedsDrain;
    private readonly Dictionary<Enemy, DateTime> _nextEnemyMoves = [];
    private readonly Dictionary<PartyMemberAvatar, DateTime> _nextPartyMoves = [];
    private readonly List<Position> _leaderTrail = [];
    private bool _partyHoldingPosition;
    private bool _saveAfterBattle;
    private bool _timeStopUsedThisBattle;
    private readonly HashSet<LiveCharacter> _turnUndeadUsedThisBattle = [];
    private readonly List<(LiveCharacter Character, LevelUpResult Result)> _pendingLevelUps = [];
    private DateTime? _partyScatterUntil;
    private Direction _leaderFacing = Direction.Right;
    private int _mazeLevel = 1;
    private bool _hasRestedThisLevel;
    private bool _developerPhasing;
    private readonly HashSet<string> _collectedBossKeyIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _seenBossIds = new(StringComparer.OrdinalIgnoreCase);
    public CharacterRoster CharacterRoster { get; }
    public LiveCharacter SelectedCharacter { get; }

    public Game(GameDataCatalog gameData, CharacterRoster characterRoster, LiveCharacter selectedCharacter,
        GameSaveService gameSaveService, GameSaveData? loadedState = null)
    {
        CharacterRoster = characterRoster;
        SelectedCharacter = selectedCharacter;
        _gameData = gameData;
        _gameSaveService = gameSaveService;
        _gameStateMapper = new GameStateMapper(gameData, characterRoster, selectedCharacter);
        _loadedState = loadedState;
        _renderer = new ConsoleRenderer(gameData, characterRoster.Party);
        _renderer.SetGoldenKeyCount(0);
        _soundEffects = new SoundEffects(message => _renderer.DrawDeveloperMessage(message));
        _doorInteractions = new DoorInteractionController(gameData, _renderer, _soundEffects, _random);
        _innController = new InnController(gameData, characterRoster, selectedCharacter, _renderer, _soundEffects,
            _random, AwardExperienceResult, ResolvePerkOffers, PreparePartySpells);
        _battleSystem = new BattleSystem(_random, gameData.MonsterAbilities, gameData.Statuses,
            gameData.StrengthHitBonuses);
    }

    // NPC spellcasting for combat
    private BattlePlayerAction? ChooseNpcBattlePlayerAction(PartyMemberAvatar member, Enemy enemy,
        bool supportingLeaderBattle = false)
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
        var allies = CharacterRoster.Party.Members.Where(c => c.IsAlive).ToList();
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
                var notes = new List<string>();
                foreach (var effect in effects.Where(e => e.Type == SpellEffectType.Heal))
                    ApplyHealingForCaster(effect, spell, new[] { target }, divine, notes, caster);
                var summary = notes.Count == 0 ? "" : $" {string.Join("; ", notes)}";
                var message = $"{caster.Name} elsüti: {spell.Name} → {target.Name}. -{manaCost} manna.{summary}";
                _renderer.DrawInventoryMessage(message, ConsoleColor.Green);
                _renderer.RefreshBattleStatusRows();
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
            var notes = new List<string>();
            foreach (var effect in effects.Where(e => e.Type == SpellEffectType.CureStatus))
                ApplyStatusCureForCaster(effect, new[] { targetChar }, notes);
            var message = $"{caster.Name} elsüti: {spell.Name} → {targetChar.Name}. -{manaCost} manna. {string.Join("; ", notes)}";
            _renderer.DrawInventoryMessage(message, ConsoleColor.Green);
            _renderer.RefreshBattleStatusRows();
            return new BattlePlayerAction(message, BattleLogKind.PlayerAttack, 0, 0);
        }

        // A leader harcát támadó varázslattal csak valódi vészhelyzetben támogatják.
        // Saját harcukban ez a korlátozás nem érvényes.
        if (supportingLeaderBattle && !ShouldUseOffensiveSupportSpell(enemy)) return null;

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
            var execution = ExecuteSpell(caster, member.Position, spell, enemy.Position, inCombat: true, enemy, divine);
            var message = $"{caster.Name} elsüti: {spell.Name} → {enemy.Name}. -{manaCost} manna. {execution.Summary}";
            _renderer.DrawInventoryMessage(message, ConsoleColor.Green);
            _renderer.RefreshBattleStatusRows();
            return new BattlePlayerAction(message, BattleLogKind.PlayerAttack, execution.DamageToCurrentEnemy, execution.ExtraPlayerActions);
        }

        return null;
    }

    private bool ShouldUseOffensiveSupportSpell(Enemy enemy)
    {
        var enemyCombatAbilities = (enemy.Definition.Strength ?? 0) + (enemy.Definition.Speed ?? 0);
        var leaderCombatAbilities = SelectedCharacter.Abilities.Strength + SelectedCharacter.Abilities.Dexterity;
        return SelectedCharacter.CurrentVitality * 2 <= SelectedCharacter.MaximumVitality ||
               enemy.Definition.IsBoss || enemy.Definition.StrengthTier >= 5 ||
               enemyCombatAbilities > leaderCombatAbilities;
    }

    // Let non-fighting party members act (heal/cure/offensive spells) during the leader's own battle
    private int TryPartyMembersActInLeaderBattle(Enemy enemy)
    {
        var totalDamage = 0;
        foreach (var member in _maze.PartyMembers.Where(member => member.Character.IsAlive))
        {
            member.Character.AdvanceSpellEffects();
            totalDamage += ChooseNpcBattlePlayerAction(member, enemy, supportingLeaderBattle: true)?.DamageToEnemy ?? 0;
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
        var allies = CharacterRoster.Party.Members.Where(c => c.IsAlive).ToList();
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
                var notes = new List<string>();
                foreach (var effect in effects.Where(e => e.Type == SpellEffectType.Heal))
                    ApplyHealingForCaster(effect, spell, new[] { lowest }, divine, notes, caster);
                var summary = notes.Count == 0 ? "" : $" {string.Join("; ", notes)}";
                var message = $"{caster.Name} elsüti: {spell.Name} → {lowest.Name}. -{manaCost} manna.{summary}";
                _renderer.DrawInventoryMessage(message, ConsoleColor.Green);
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
            effect.Value, AdjustedDuration(effect, divineJudgment), effect.Dice,
            (int)Math.Round(caster.Abilities.Intelligence * effect.IntelligenceMultiplier), true,
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
                  (int)Math.Round(caster.Abilities.Intelligence * effect.IntelligenceMultiplier) +
                  caster.Level * effect.LevelMultiplier + effect.Value;
            if (!fullHealing && divineJudgment) amount *= 2;
            if (caster.HasPerk(PerkIds.PriestHealingGrace)) amount = (int)Math.Ceiling(amount * 1.25);
            var before = character.CurrentVitality;
            character.RestoreVitality(amount);
            notes.Add($"{character.Name}: ❤️ +{character.CurrentVitality - before} HP");
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

    public void Run()
    {
        Console.CursorVisible = false;
        if (_loadedState is null)
        {
            StartNewMaze();
            _renderer.DrawStoryOverlay("A KÁOSZRUBIN KRÓNIKÁJA", "I. fejezet — A tizenkét aranykulcs",
                CampaignIntroduction, _maze, _fogOfWar, _player.Position);
        }
        else RestoreGame(_loadedState);
        if (_loadedState is null) _nextNeedsDrain = DateTime.UtcNow + TimeSpan.FromMinutes(1);
        try
        {
            while (!_gameOver)
            {
                if (Console.KeyAvailable)
                {
                    var keyInfo = Console.ReadKey(intercept: true);
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
                    if (keyInfo.Key == ConsoleKey.Tab)
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
                        if (keyInfo.Key == ConsoleKey.UpArrow) _renderer.MoveCharacterSheetSelection(-1);
                        else if (keyInfo.Key == ConsoleKey.DownArrow) _renderer.MoveCharacterSheetSelection(1);
                        else if (keyInfo.Key == ConsoleKey.LeftArrow) _renderer.MoveDisplayedPartyMember(-1);
                        else if (keyInfo.Key == ConsoleKey.RightArrow) _renderer.MoveDisplayedPartyMember(1);
                        else if (keyInfo.Key == ConsoleKey.D) DropSelectedInventoryItem();
                        else if (keyInfo.Key == ConsoleKey.I) InspectSelectedInventoryItem();
                        else if (keyInfo.Key == ConsoleKey.Delete) DismissSelectedPartyMember();
                        else if (keyInfo.Key == ConsoleKey.Enter) UseSelectedInventoryItem();
                        else if (keyInfo.Key == ConsoleKey.Spacebar) GrabOrPlaceInventoryItem();
                        continue;
                    }
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

                    var key = keyInfo.Key;
                    if (key == ConsoleKey.Escape)
                    {
                        if (ConfirmReturnToMainMenu()) return;
                        continue;
                    }
                    if (key == ConsoleKey.N) { _doorInteractions.TryOpenAdjacentDoor(_maze, _fogOfWar, _player, SelectedCharacter); continue; }
                    if (key == ConsoleKey.Z) { _doorInteractions.TryCloseAdjacentDoor(_maze, _fogOfWar, _player, SelectedCharacter); continue; }
                    if (key == ConsoleKey.K)
                    {
                        if (!TrySearchCurrentCell()) _doorInteractions.TryLockAdjacentDoor(_maze, _fogOfWar, _player, SelectedCharacter);
                        continue;
                    }
                    if (key == ConsoleKey.H) { TogglePartyHoldPosition(); continue; }
                    if (key == ConsoleKey.M) { ScatterPartyTemporarily(); continue; }
                    if (key == ConsoleKey.P) { TryRestParty(); continue; }
                    if (key == ConsoleKey.Enter && _player.Position == _maze.Exit)
                    {
                        if (_mazeLevel == MazeLevelConfigurations.FinalLevel)
                        {
                            CompleteCampaign();
                            continue;
                        }
                        var completedLevel = _mazeLevel;
                        _soundEffects.Play(SoundEffect.LevelComplete);
                        _innController.Run(completedLevel);
                        _mazeLevel++;
                        StartNewMaze();
                        continue;
                    }
                    MovePlayer(key);
                }

                if (!_battleStarted) MoveEnemies();

                if (!_battleStarted) MovePartyMembers();

                if (!_battleStarted && DateTime.UtcNow >= _nextNeedsDrain)
                {
                    DrainNeeds();
                    _nextNeedsDrain = DateTime.UtcNow + TimeSpan.FromMinutes(1);
                }

                Thread.Sleep(20);
            }
        }
        finally
        {
            Console.CursorVisible = true;
            Console.SetCursorPosition(0, ConsoleRenderer.PlayfieldHeight + 5);
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

        _soundEffects.Play(SoundEffect.LevelComplete);
        _soundEffects.Play(SoundEffect.Victory);
        _renderer.DrawStoryOverlay("GRATULÁLUNK, KULCSHORDOZÓK!",
            "XV. fejezet — A csillagok választottai", CreateCampaignFinale(),
            _maze, _fogOfWar, _player.Position);
        _gameOver = true;
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

    private void StartNewMaze()
    {
        _hasRestedThisLevel = false;
        foreach (var character in CharacterRoster.Party.Members) character.ResetLevelResurrection();
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
        _fogOfWar = new FogOfWar(_maze.Width, _maze.Height, VisionRange);
        _fogOfWar.RevealFrom(_maze, _player.Position);
        foreach (var member in _maze.PartyMembers) _fogOfWar.RevealFrom(_maze, member.Position);
        _battleStarted = false;
        _gameOver = false;
        InitializeEnemyMoveSchedule(DateTime.UtcNow);
        _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
        CheckBossDiscovery(_maze.Enemies.Where(enemy => _fogOfWar.IsRevealed(enemy.Position)));
        _soundEffects.Play(SoundEffect.LevelStart);
        LogMazeAccessibilityCheck();
    }

    private void LogMazeAccessibilityCheck()
    {
        var report = _maze.CheckFullAccessibility();
        _renderer.DrawDeveloperMessage(report.IsFullyAccessible
            ? $"Bejárhatósági önellenőrzés: OK, mind a(z) {report.TotalWalkableCount} padló-/ajtócella elérhető."
            : $"Bejárhatósági önellenőrzés: HIBA, {report.UnreachablePositions.Count}/{report.TotalWalkableCount} cella nem érhető el " +
              $"(pl. {report.UnreachablePositions[0].X},{report.UnreachablePositions[0].Y}).");
    }

    private void ShowInGameHelp()
    {
        MainMenu.ShowHelp();
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
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _renderer.DrawDeveloperMessage($"A mentés sikertelen: {exception.Message}");
        }
    }

    private GameSaveData CreateGameSaveData()
    {
        return _gameStateMapper.Create(_mazeLevel, _maze, _player, _fogOfWar, _leaderFacing,
            _leaderTrail, _partyHoldingPosition, _hasRestedThisLevel, _partyScatterUntil,
            _nextNeedsDrain, _nextEnemyMoves, _collectedBossKeyIds, _seenBossIds);
    }

    private void RestoreGame(GameSaveData state)
    {
        var restored = _gameStateMapper.Restore(state);
        _mazeLevel = restored.MazeLevel;
        _collectedBossKeyIds.Clear();
        _collectedBossKeyIds.UnionWith(state.CollectedBossKeyIds ?? []);
        _seenBossIds.Clear();
        _seenBossIds.UnionWith(state.SeenBossIds ?? []);
        _renderer.SetGoldenKeyCount(_collectedBossKeyIds.Count);
        _maze = restored.Maze;
        _player = restored.Player;
        _fogOfWar = restored.FogOfWar;
        _leaderFacing = restored.LeaderFacing;
        _leaderTrail.Clear();
        _leaderTrail.AddRange(restored.LeaderTrail);
        _partyHoldingPosition = restored.PartyHoldingPosition;
        _hasRestedThisLevel = restored.HasRestedThisLevel;
        _partyScatterUntil = restored.PartyScatterUntil;
        _nextNeedsDrain = restored.NextNeedsDrain;
        _nextEnemyMoves.Clear();
        foreach (var enemyMove in restored.NextEnemyMoves) _nextEnemyMoves[enemyMove.Key] = enemyMove.Value;
        _nextPartyMoves.Clear();
        foreach (var member in _maze.PartyMembers) ScheduleNextPartyMove(member, DateTime.UtcNow);
        _battleStarted = false;
        _gameOver = false;
        _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
        _renderer.DrawDeveloperMessage($"Mentés betöltve: {state.MainCharacterName}, {_mazeLevel}. pálya.");
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

        var summaries = new List<string>();
        foreach (var character in livingParty)
        {
            var before = character.CurrentVitality;
            character.RestoreVitality(_random.Next(1, 11));
            character.SetCurrentResources(character.CurrentVitality, character.MaximumMana);
            var cured = new List<string>();
            var cureChance = Math.Clamp(30 + character.Abilities.Health * 2, 0, 100);
            foreach (var (statusId, name) in new[]
                     {
                         (CharacterStatusIds.Diseased, "betegség"),
                         (CharacterStatusIds.Poisoned, "mérgezés"),
                         (CharacterStatusIds.Bleeding, "vérzés")
                     })
                if (character.HasStatus(statusId) && _random.Next(100) < cureChance && character.RemoveStatus(statusId))
                    cured.Add(name);
            character.ConsumeFood(10);
            character.ConsumeWater(10);
            character.SynchronizeNeedStatuses(_gameData.GetStatus(CharacterStatusIds.Hungry),
                _gameData.GetStatus(CharacterStatusIds.Thirsty));
            summaries.Add($"{character.Name}: +{character.CurrentVitality - before} HP" +
                (cured.Count > 0 ? $", elmúlt: {string.Join(", ", cured)}" : string.Empty));
        }
        _hasRestedThisLevel = true;
        PreparePartySpells();
        foreach (var door in roomDoors) _maze.SetDoorState(door, DoorState.Closed);
        _nextNeedsDrain = DateTime.UtcNow + TimeSpan.FromMinutes(1);
        InitializeEnemyMoveSchedule(DateTime.UtcNow);
        foreach (var member in _maze.PartyMembers) ScheduleNextPartyMove(member, DateTime.UtcNow);
        _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
        _renderer.DrawDeveloperMessage("A parti kipihente magát. " + string.Join("; ", summaries));
        _soundEffects.Play(SoundEffect.Rest);
    }

    private void PreparePartySpells()
    {
        foreach (var character in CharacterRoster.Party.Members.Where(character => character.IsAlive && character.IsSpellcaster))
            character.SetMemorizedSpells(_renderer.DrawSpellPreparationScreen(character));
    }

    private void MovePlayer(ConsoleKey key)
    {
        if (!TryGetDirection(key, out var direction)) return;
        var previousPosition = _player.Position;
        var targetPosition = previousPosition + direction;

        // Prevent moving into a party member avatar even in developer mode
        if (_maze.GetObjectAt(targetPosition) is PartyMemberAvatar) return;

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

        var newlyRevealed = _fogOfWar.RevealFrom(_maze, _player.Position);
        var justReachedExit = _player.Position == _maze.Exit && previousPosition != _maze.Exit;
        _renderer.DrawMovement(_maze, _fogOfWar, previousPosition, _player.Position, newlyRevealed, justReachedExit);
        CheckBossDiscoveryAt(newlyRevealed);
        switch (_random.Next(5))
        {
            case 0: _soundEffects.Play(SoundEffect.Step1); break;
            case 1: _soundEffects.Play(SoundEffect.Step2); break;
        }
        var chest = _maze.GetTreasureChestAt(_player.Position);
        if (chest is not null)
        {
            var rules = _gameData.LootRules;
            var jackpotChance = AdjustedSearchChance(rules.ChestJackpotChancePercent);
            var jackpot = _random.Next(100) < jackpotChance;
            var rewardMultiplier = jackpot ? rules.ChestJackpotMultiplier : 1;
            if (SelectedCharacter.HasPerk(PerkIds.ThiefMasterThief)) rewardMultiplier *= 2;
            var goldAmount = chest.GoldAmount * rewardMultiplier;
            SelectedCharacter.AddGold(goldAmount);
            var masterThiefLoot = RollMasterThiefChestLoot();
            _maze.RemoveTreasureChest(chest);
            _renderer.RefreshCharacterSheet(SelectedCharacter);
            _renderer.DrawTreasureCollected(goldAmount, jackpot, jackpotChance, rewardMultiplier);
            _soundEffects.Play(SoundEffect.Chest);
            if (masterThiefLoot is not null)
            {
                if (TryStoreLootInParty(masterThiefLoot, out var owner))
                    _renderer.DrawInventoryMessage(
                        $"🎁 Mestertolvaj: {masterThiefLoot.Name} → {owner} hátizsákja.", ConsoleColor.Magenta);
                else
                {
                    _maze.DropItem(_player.Position, masterThiefLoot);
                    _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
                    _renderer.DrawInventoryMessage(
                        $"🎁 Mestertolvaj: {masterThiefLoot.Name} a földön maradt mert a hátizsákok tele vannak.",
                        ConsoleColor.Magenta);
                }
            }
        }
        var enemy = _maze.GetEnemyAt(_player.Position);
        if (enemy is not null) StartBattle(enemy);
    }

    private void DropSelectedInventoryItem()
    {
        var slot = _renderer.GetSelectedInventorySlot();
        if (slot is null) { _renderer.DrawInventoryMessage("Itt nincs ledobható tárgy.", ConsoleColor.DarkYellow); return; }
        var item = slot.Value.Character.GetInventoryItem(slot.Value.Kind, slot.Value.Index);
        if (item is null) { _renderer.DrawInventoryMessage("A kijelölt hely üres.", ConsoleColor.DarkYellow); return; }
        if (SpellcastingRules.IsSpellcastingFocus(item))
        { _renderer.DrawInventoryMessage($"A(z) {item.Name} a karakterhez kötött varázsfókusz, ezért nem dobható el.", ConsoleColor.Red); return; }
        if (!slot.Value.Character.SetInventoryItem(slot.Value.Kind, slot.Value.Index, null))
        { _renderer.DrawInventoryMessage("A kijelölt tárgy nem távolítható el erről a helyről.", ConsoleColor.Red); return; }
        _maze.DropItem(_player.Position, item);
        _renderer.RefreshCharacterSheet(SelectedCharacter);
        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
        var pileCount = _maze.GetGroundItemPileAt(_player.Position)?.Items.Count ?? 1;
        _renderer.DrawInventoryMessage($"Ledobtad: {item.Name}. A mezőn {pileCount} tárgy van.", ConsoleColor.Cyan);
    }

    private bool TrySearchCurrentCell()
    {
        var corpse = _maze.GetCorpseAt(_player.Position);
        var pile = _maze.GetGroundItemPileAt(_player.Position);
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
                SearchMonsterCorpse(monsterCorpse, messages);
            }
        }
        else if (corpse is PartyMemberCorpse)
            messages.Add("Az elesett társ testén nincs elvehető zsákmány");
        else if (corpse is not null)
            messages.Add("Ez a régi tetem már nem tartalmaz azonosítható zsákmányt");

        PickUpGroundItems(messages);
        _renderer.RefreshCharacterSheet(SelectedCharacter);
        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
        _renderer.DrawInventoryMessage("🔎 " + (messages.Count == 0
            ? "A keresés nem hozott eredményt."
            : string.Join("; ", messages) + "."), ConsoleColor.Yellow);
        return true;
    }

    private void SearchMonsterCorpse(MonsterCorpse corpse, ICollection<string> messages)
    {
        var enemy = _gameData.GetEnemy(corpse.EnemyDefinitionId);
        var rules = _gameData.LootRules;
        var keyChance = AdjustedSearchChance(rules.KeyChancePercent);
        var goldChance = AdjustedSearchChance(rules.GoldChancePercent);
        var equipmentDefinition = _gameData.GetMonsterLoot(enemy.Id);
        var equipmentChance = equipmentDefinition is null
            ? 0
            : AdjustedSearchChance(equipmentDefinition.EquipmentChancePercent);
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
            if (TryStoreLootInParty(item, out var owner))
                messages.Add($"{item.Name} → {owner} hátizsákja");
            else
            {
                _maze.DropItem(_player.Position, item);
                messages.Add($"{item.Name} a földön maradt (a hátizsákok tele vannak)");
            }
        }
        if (foundItems.Count == 0 && messages.All(message => !message.StartsWith(ConsoleRenderer.MoneyIcon, StringComparison.Ordinal)))
            messages.Add("a tetemnél nem találtál zsákmányt");
    }

    private int AdjustedSearchChance(int baseChance)
    {
        var chance = Math.Max(0, baseChance);
        if (CharacterClassRules.IsThief(SelectedCharacter.CharacterClass.Id))
            chance = chance * _gameData.LootRules.ThiefChanceMultiplierPercent / 100;
        chance += SelectedCharacter.Abilities.Intelligence * _gameData.LootRules.IntelligenceChanceBonusPerPoint;
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

    private IItemDefinition? RollMasterThiefChestLoot()
    {
        if (!SelectedCharacter.HasPerk(PerkIds.ThiefMasterThief) || _random.Next(100) >= 25) return null;
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

    private void PickUpGroundItems(ICollection<string> messages)
    {
        var pile = _maze.GetGroundItemPileAt(_player.Position);
        if (pile is null) return;
        var pickedUp = new List<string>();
        foreach (var item in pile.Items.ToArray())
        {
            if (!TryStoreLootInParty(item, out var owner)) continue;
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

        var details = item switch
        {
            Domain.Combat.WeaponDefinition weapon =>
                $"Fegyver | típus: {(weapon.WeaponTypeId is { } typeId ? _gameData.GetWeaponType(typeId).Name : "nincs")} | sebzés: {weapon.Damage?.ToString() ?? "nincs"} | " +
                $"minimum Erő: {weapon.MinimumStrength} | {(weapon.IsTwoHanded ? "kétkezes" : "egykezes")} | " +
                (weapon.IsTwoHanded ? "⚒️ páncéltörő: az ellenfél páncéljának 50%-át figyelmen kívül hagyja | " : string.Empty) +
                $"kasztok: {AllowedClassNames(weapon.AllowedClassIds)}",
            Domain.Combat.ArmorDefinition armor => $"Páncél | védelem: {armor.Defense?.ToString() ?? "nincs"} | kasztok: {AllowedClassNames(armor.AllowedClassIds)}",
            Domain.Magic.MagicItemDefinition magic =>
                $"Varázstárgy | típus: {MagicItemKindName(magic.Kind)} | hatás: {MagicItemEffectName(magic.Effect)} {magic.EffectValue}" +
                (magic.SpellId is null ? string.Empty : $" | varázslat: {_gameData.Spells.First(spell => spell.Id == magic.SpellId).Name}") +
                (magic.MaximumCharges > 0 ? $" | töltet: {(slot is { } magicSlot ? magicSlot.Character.GetInventoryItemCharges(magicSlot.Kind, magicSlot.Index) : magic.MaximumCharges)}/{magic.MaximumCharges}" : string.Empty) +
                $" | kasztok: {AllowedClassNames(magic.AllowedClassIds)}",
            MiscItemDefinition misc when SpellcastingRules.IsSpellcastingFocus(misc) =>
                "Karakterhez kötött varázsfókusz | nem mozgatható, nem dobható el és nem kereskedhető",
            MiscItemDefinition misc when misc.Id == MiscItemIds.HerbalTea =>
                $"Használati tárgy | hatás: víz {misc.EffectValue}, HP 5–15",
            MiscItemDefinition misc when IsInitiativeDrink(misc) =>
                $"Használati tárgy | hatás: víz {misc.EffectValue}, +2 kezdeményezés és +1 találat 10 akcióig",
            MiscItemDefinition misc when misc.Effect != ConsumableEffect.None => $"Használati tárgy | hatás: {ConsumableEffectName(misc.Effect)} {misc.EffectValue}",
            _ => "Általános tárgy"
        };
        var description = string.IsNullOrWhiteSpace(item.Description) ? "Nincs jellemzés." : item.Description;
        _renderer.DrawInventoryMessage($"{item.Name} [{item.Id}] — {details}. Ritkaság: {ItemRarityName(item.Rarity)}; mágikus erő: {item.MagicPower}; alapár: {item.BasePrice} arany. Jellemzés: {description}", RarityColor(item.Rarity));
    }

    private void DismissSelectedPartyMember()
    {
        var character = _renderer.GetSelectedPartyMember();
        if (character is null)
        {
            _renderer.DrawInventoryMessage("A Del használatához jelölj ki egy partitársat.", ConsoleColor.DarkYellow);
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

        var character = slot.Value.Character;
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
            _ => string.Empty
        };
        if (string.IsNullOrEmpty(result)) used = false;
        if (!used) { _renderer.DrawInventoryMessage("A tárgy hatására most nincs szükség vagy nem alkalmazható.", ConsoleColor.DarkYellow); return; }

        character.SetInventoryItem(InventorySlotKind.Backpack, slot.Value.Index, null);
        character.SynchronizeNeedStatuses(_gameData.GetStatus(CharacterStatusIds.Hungry), _gameData.GetStatus(CharacterStatusIds.Thirsty));
        _renderer.RefreshCharacterSheet(SelectedCharacter);
        _renderer.DrawInventoryMessage($"{character.Name} használta: {item.Name} — {result}.", ConsoleColor.Green);
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
        if (character.IsAlive) character.RestoreVitality(_random.Next(5, 16));
        return $"víz +{character.WaterLevel - waterBefore}, HP +{character.CurrentVitality - vitalityBefore}";
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

    private static string UseHealing(LiveCharacter character, int amount)
    {
        var before = character.CurrentVitality;
        character.RestoreVitality(amount);
        return $"HP +{character.CurrentVitality - before}";
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
            var charges = target.Character.GetInventoryItemCharges(target.Kind, target.Index);
            if (SpellcastingRules.IsSpellcastingFocus(item))
            { _renderer.DrawInventoryMessage($"A(z) {item.Name} a hátizsák első helyéhez kötött, ezért nem mozgatható.", ConsoleColor.Red); return; }
            if (!target.Character.SetInventoryItem(target.Kind, target.Index, null))
            { _renderer.DrawInventoryMessage("A kijelölt tárgy nem mozgatható.", ConsoleColor.Red); return; }
            _heldInventoryItem = new HeldInventoryItem(item, target, charges);
            _renderer.RefreshInventoryRows();
            _renderer.DrawInventoryMessage($"Kézben: {item.Name}. Válassz célhelyet, majd nyomj Space-t.", ConsoleColor.Yellow);
            return;
        }

        var held = _heldInventoryItem;
        if (target == held.Source)
        {
            held.Source.Character.ApplyInventoryChanges(new InventorySlotChange(held.Source.Kind, held.Source.Index, held.Item, held.Charges));
            _heldInventoryItem = null;
            _renderer.RefreshCharacterSheet(SelectedCharacter);
            _renderer.DrawInventoryMessage($"A(z) {held.Item.Name} visszakerült az eredeti helyére.", ConsoleColor.DarkYellow);
            return;
        }
        var displaced = target.Character.GetInventoryItem(target.Kind, target.Index);
        var changesByCharacter = new Dictionary<LiveCharacter, List<InventorySlotChange>>();
        var displacedCharges = target.Character.GetInventoryItemCharges(target.Kind, target.Index);
        AddInventoryChange(changesByCharacter, target.Character, new(target.Kind, target.Index, held.Item, held.Charges));
        AddInventoryChange(changesByCharacter, held.Source.Character, new(held.Source.Kind, held.Source.Index, displaced, displacedCharges));
        if (changesByCharacter.Any(entry => !entry.Key.CanApplyInventoryChanges(entry.Value.ToArray())))
        {
            _renderer.DrawInventoryMessage("A felszerelés nem használható ezen a helyen, ezzel a kaszttal vagy a karakter jelenlegi Erejével. A kétkezes fegyver csak az első, üres második fegyverhely mellett viselhető.", ConsoleColor.Red);
            return;
        }
        foreach (var entry in changesByCharacter) entry.Key.ApplyInventoryChanges(entry.Value.ToArray());
        _heldInventoryItem = null;
        _renderer.RefreshCharacterSheet(SelectedCharacter);
        _renderer.DrawInventoryMessage(displaced is null
            ? $"Áthelyezted: {held.Item.Name}."
            : $"Felcserélted: {held.Item.Name} ↔ {displaced.Name}.", ConsoleColor.Green);
    }

    private static void AddInventoryChange(Dictionary<LiveCharacter, List<InventorySlotChange>> changes,
        LiveCharacter character, InventorySlotChange change)
    {
        if (!changes.TryGetValue(character, out var characterChanges))
            changes[character] = characterChanges = [];
        characterChanges.Add(change);
    }

    private void CancelHeldInventoryItem()
    {
        if (_heldInventoryItem is not { } held) return;
        held.Source.Character.ApplyInventoryChanges(new InventorySlotChange(held.Source.Kind, held.Source.Index, held.Item, held.Charges));
        _heldInventoryItem = null;
        _renderer.RefreshCharacterSheet(SelectedCharacter);
        _renderer.DrawInventoryMessage($"A(z) {held.Item.Name} visszakerült az eredeti helyére.", ConsoleColor.DarkYellow);
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
            if (enemy.PursuitState == EnemyPursuitState.Undecided &&
                FogOfWar.CanSee(_maze, enemy.Position, _player.Position, VisionRange))
                ResolveEnemyPursuit(enemy);

            Direction? direction = enemy.PursuitState == EnemyPursuitState.Pursuing
                ? FindEnemyStepToward(enemy, _player.Position)
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

    private void ResolveEnemyPursuit(Enemy observer)
    {
        var pursue = _random.Next(100) < 60;
        var group = observer.GroupId is null
            ? [observer]
            : _maze.Enemies.Where(enemy => string.Equals(enemy.GroupId, observer.GroupId,
                StringComparison.Ordinal)).ToList();
        foreach (var enemy in group.Where(enemy => enemy.PursuitState == EnemyPursuitState.Undecided))
            enemy.ResolvePursuit(pursue);
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
            ResolveNpcBattle(encounteredMember, enemy);
            return true;
        }
        if (!_maze.TryMoveEnemy(enemy, destination)) return false;
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
        return _maze.GetObjectAt(position) is null or GroundItemPile or Corpse or PartyMemberAvatar;
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
        if (_partyHoldingPosition && !isScattering) return;
        foreach (var member in _maze.PartyMembers.ToArray())
        {
            if (_nextPartyMoves.GetValueOrDefault(member) > now) continue;
            ScheduleNextPartyMove(member, now);
            // Allow NPCs to cast simple exploration spells (heals/cures) before moving
            TryNpcCastExplorationSpell(member);
            if (isScattering)
            {
                MovePartyMemberAwayFromLeader(member);
                continue;
            }
            if (CanActivelyAttack(member) && TryResolveAdjacentNpcBattle(member)) continue;
            var previous = member.Position;
            var next = ChoosePartyMemberStep(member);
            if (next is null || !_maze.TryMovePartyMember(member, next.Value, _player.Position)) continue;
            member.Character.RegisterExplorationStep();
            var newlyRevealed = _fogOfWar.RevealFrom(_maze, member.Position);
            _renderer.DrawPartyMemberMovement(_maze, _fogOfWar, previous, member.Position, newlyRevealed, _player.Position);
            CheckBossDiscoveryAt(newlyRevealed);
            if (CanActivelyAttack(member)) TryResolveAdjacentNpcBattle(member);
        }
    }

    private void TogglePartyHoldPosition()
    {
        _partyHoldingPosition = !_partyHoldingPosition;
        if (!_partyHoldingPosition)
            foreach (var member in _maze.PartyMembers) _nextPartyMoves[member] = DateTime.UtcNow;
        _renderer.DrawDeveloperMessage(_partyHoldingPosition
            ? "Partiparancs: minden társ tartsa a helyét."
            : "Partiparancs: a társak folytatják korábbi viselkedésüket.");
    }

    private void ScatterPartyTemporarily()
    {
        _partyScatterUntil = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        foreach (var member in _maze.PartyMembers)
            _nextPartyMoves[member] = DateTime.UtcNow + TimeSpan.FromMilliseconds(_random.Next(0, 100));
        _renderer.DrawDeveloperMessage("Partiparancs: szétszóródás 10 másodpercig; a társak 10 mező távolságra húzódnak.");
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
        if (!_maze.TryMovePartyMember(member, next.Value, _player.Position)) return;
        member.Character.RegisterExplorationStep();
        var newlyRevealed = _fogOfWar.RevealFrom(_maze, member.Position);
        _renderer.DrawPartyMemberMovement(_maze, _fogOfWar, previous, member.Position, newlyRevealed, _player.Position);
    }

    private static bool CanActivelyAttack(PartyMemberAvatar member) =>
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
        _turnUndeadUsedThisBattle.Clear();
        _soundEffects.Play(SoundEffect.BattleStart);
        var startingNpcHp = member.Character.CurrentVitality;
        var startingEnemyHp = enemy.CurrentHitPoints;
        var startingStatusIds = member.Character.Statuses.Select(status => status.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = _battleSystem.Resolve(member.Character, enemy, _ => { }, () => ChooseNpcBattlePlayerAction(member, enemy));
        var needLoss = DrainNeedsAfterBattle(member.Character, enemy.Definition.StrengthTier);
        var newStatusText = member.Character.Statuses
            .Where(status => !startingStatusIds.Contains(status.Id))
            .Select(status => $"{status.Icon} {status.Name}")
            .ToList() is { Count: > 0 } newStatuses
            ? $" Új állapot: {string.Join(", ", newStatuses)}."
            : string.Empty;
        var levelUps = new List<ExperienceAward>();
        if (result.PlayerWon)
        {
            AwardBossKey(enemy);
            _soundEffects.Play(SoundEffect.Victory);
            var experienceAwards = DistributeExperience(member.Character, enemy.Definition.ExperienceReward);
            levelUps.AddRange(experienceAwards.Where(award => award.Result.LeveledUp && award.Character.IsAlive));
            var experienceResult = experienceAwards.First(award => award.Character == member.Character).Result;
            _maze.ReplaceEnemyWithCorpse(enemy);
            var levelText = experienceResult.LeveledUp
                ? $" Szint: {experienceResult.PreviousLevel}→{experienceResult.CurrentLevel}; +{experienceResult.VitalityGained} max HP" +
                  (experienceResult.ManaGained > 0 ? $"; +{experienceResult.ManaGained} max manna." : ".")
                : string.Empty;
            _renderer.DrawNpcBattleSummary(
                $"{member.Character.Name} automatikus csatában legyőzte {enemy.Name} ellenfelet {result.Rounds} kör alatt. " +
                $"HP: {startingNpcHp}→{member.Character.CurrentVitality}; ellenfél HP: {startingEnemyHp}→0; " +
                $"XP: {FormatExperienceAwards(experienceAwards)}.{levelText} 🍖💧 -{needLoss}.{newStatusText}",
                ConsoleColor.Green);
        }
        else
        {
            _soundEffects.Play(SoundEffect.Defeat);
            _maze.ReplacePartyMemberWithCorpse(member);
            _nextPartyMoves.Remove(member);
            _renderer.DrawNpcBattleSummary(
                $"{member.Character.Name} elesett a(z) {enemy.Name} elleni automatikus csatában {result.Rounds} kör után. " +
                $"HP: {startingNpcHp}→0; ellenfél HP: {startingEnemyHp}→{enemy.CurrentHitPoints}; 🍖💧 -{needLoss}.{newStatusText}",
                ConsoleColor.Red);
        }
        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
        _renderer.RefreshCharacterSheet(SelectedCharacter);
        _battleStarted = false;
        if (levelUps.Count > 0)
        {
            foreach (var award in levelUps) ResolvePerkOffers(award.Character, award.Result);
            _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
        }
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
        var behavior = member.Character.NpcBehavior ?? NpcBehavior.Defensive;
        var visibleEnemy = _maze.Enemies
            .Where(enemy => FogOfWar.CanSee(_maze, member.Position, enemy.Position, VisionRange))
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
                           (_maze.GetObjectAt(position) is null or GroundItemPile or Corpse));

    private bool CanPartyTraverse(PartyMemberAvatar member, Position position)
    {
        if (!_maze.IsWalkable(position) || position == _player.Position) return false;
        var occupant = _maze.GetObjectAt(position);
        return occupant is null or GroundItemPile or Corpse || occupant == member;
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
        Direction.Up => (0, -1), Direction.Down => (0, 1), Direction.Left => (-1, 0), _ => (1, 0)
    };

    private BattlePlayerAction? ChooseBattlePlayerAction(Enemy enemy)
    {
        var canTurnUndead = CanTurnUndead(SelectedCharacter, enemy) &&
                            !_turnUndeadUsedThisBattle.Contains(SelectedCharacter);
        if (!HasUsableCombatSpell(enemy) && !canTurnUndead) return null;
        while (true)
        {
            _renderer.DrawInventoryMessage("Akció: Space — fegyveres támadás | V — varázslat | F1-F8 — gyorsvarázslat" +
                (canTurnUndead ? " | T — halottűzés" : string.Empty), ConsoleColor.Yellow);
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Spacebar) return null;
            if (key.Key == ConsoleKey.T && canTurnUndead)
                return ResolveTurnUndead(SelectedCharacter, enemy);
            if (IsSaveGameShortcut(key))
            {
                _saveAfterBattle = true;
                _renderer.DrawInventoryMessage("Mentés kérve: a csata lezárása után automatikusan elkészül.", ConsoleColor.Yellow);
                continue;
            }
            if (IsHelpShortcut(key))
            {
                ShowInGameHelp();
                _renderer.DrawBattleStarted(enemy);
                _renderer.RefreshBattleStatusRows();
                continue;
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
                continue;

            if (spell is null)
            {
                if (key.Key != ConsoleKey.V)
                    _renderer.DrawInventoryMessage("Ez a varázslat-gyorshely üres.", ConsoleColor.DarkYellow);
                continue;
            }
            var attempt = TryCastSpell(SelectedCharacter, _player.Position, spell, inCombat: true, enemy,
                castingItem, castingItemSlotIndex);
            if (attempt is null) continue;
            if (attempt.ConsumesTurn) return new BattlePlayerAction(attempt.Message, attempt.Kind,
                attempt.DamageToCurrentEnemy, attempt.ExtraPlayerActions);
            _renderer.DrawInventoryMessage(attempt.Message, ConsoleColor.Red);
        }
    }

    private bool HasUsableCombatSpell(Enemy enemy) =>
        SelectedCharacter.CanCastSpells && SelectedCharacter.MemorizedSpells.Any(spell =>
                spell.CanUseInCombat && SpellcastingRules.EffectiveManaCost(SelectedCharacter, spell) <= SelectedCharacter.CurrentMana &&
                (!_timeStopUsedThisBattle || _gameData.GetSpellEffects(spell.Id).All(effect => effect.Type != SpellEffectType.ExtraActions)) &&
                HasValidSpellTarget(SelectedCharacter, _player.Position, spell, enemy)) ||
        EquippedCastingItems(SelectedCharacter).Any(item =>
            _gameData.GetSpell(item.SpellId!) is { } spell && spell.CanUseInCombat &&
            (!_timeStopUsedThisBattle || _gameData.GetSpellEffects(spell.Id).All(effect => effect.Type != SpellEffectType.ExtraActions)) &&
            HasValidSpellTarget(SelectedCharacter, _player.Position, spell, enemy));

    private static bool CanTurnUndead(LiveCharacter character, Enemy enemy) =>
        character.CharacterClass.Id is CharacterClassIds.Pap or CharacterClassIds.Lovag &&
        enemy.Definition.AbilityIds.Contains(MonsterAbilityIds.Undead, StringComparer.OrdinalIgnoreCase);

    private BattlePlayerAction ResolveTurnUndead(LiveCharacter character, Enemy enemy)
    {
        _turnUndeadUsedThisBattle.Add(character);
        var priest = character.CharacterClass.Id == CharacterClassIds.Pap;
        var ability = priest ? character.Abilities.Intelligence : character.Abilities.Strength;
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
        bool inCombat, Enemy? currentEnemy, MagicItemDefinition? castingItem = null, int? castingItemSlotIndex = null)
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

        var target = SelectSpellTarget(caster, casterPosition, spell, currentEnemy);
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
            var failureChance = Math.Clamp(30 - caster.Abilities.Intelligence - caster.Abilities.Dexterity, 0, 100);
            var roll = _random.Next(1, 101);
            if (roll <= failureChance)
                return new SpellCastAttempt(true,
                    $"{caster.Name} varázslata meghiúsul: {spell.Name} — kockázat {failureChance}%, dobás {roll}. " +
                    (usingItem ? $"{CastingItemUseText(castingItem!)}; az akció elveszett." : $"-{manaCost} manna; az akció elveszett."),
                    BattleLogKind.Information);
        }

        if (IsOffensiveSpell(spell)) caster.BreakSanctuary();
        _soundEffects.Play(IsOffensiveSpell(spell) ? SoundEffect.OffensiveSpell : SoundEffect.DefensiveSpell);
        var targetText = DescribeSpellTarget(caster, spell, target.Value, currentEnemy);
        var execution = ExecuteSpell(caster, casterPosition, spell, target.Value, inCombat, currentEnemy, divineJudgment);
        var judgmentText = divineJudgment ? " ⚡ Isteni ítélet: kétszeres számszerű hatás és ingyenes varázslat." : string.Empty;
        return new SpellCastAttempt(true,
            $"{caster.Name} elsüti: {spell.Name} → {targetText}. " +
            (usingItem ? $"{CastingItemUseText(castingItem!)}; 0 manna." : $"-{manaCost} manna.") +
            $"{judgmentText} {execution.Summary}",
            BattleLogKind.PlayerAttack, execution.DamageToCurrentEnemy, execution.ExtraPlayerActions);
    }

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
                    notes.Add($"láthatatlanság {AdjustedDuration(effect, divineJudgment)} akcióra");
                    break;
                case SpellEffectType.DefenseBonus:
                    ApplyCharacterEffects(caster, characterTargets, effect, spell, ActiveSpellEffectType.DefenseBonus, divineJudgment);
                    notes.Add($"+{effect.Value} védelem {AdjustedDuration(effect, divineJudgment)} akcióra");
                    break;
                case SpellEffectType.PhysicalReduction:
                    ApplyCharacterEffects(caster, characterTargets, effect, spell, ActiveSpellEffectType.PhysicalReduction, divineJudgment);
                    notes.Add($"{effect.Value}% fizikai védelem {AdjustedDuration(effect, divineJudgment)} akcióra");
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
                    notes.Add($"+{effect.Value} találat {AdjustedDuration(effect, divineJudgment)} akcióra");
                    break;
                case SpellEffectType.DamageBonus:
                    ApplyCharacterEffects(caster, characterTargets, effect, spell, ActiveSpellEffectType.DamageBonus, divineJudgment);
                    notes.Add($"+{effect.Value} fizikai sebzés {AdjustedDuration(effect, divineJudgment)} akcióra");
                    break;
                case SpellEffectType.InitiativeBonus:
                    ApplyCharacterEffects(caster, characterTargets, effect, spell, ActiveSpellEffectType.InitiativeBonus, divineJudgment);
                    notes.Add($"+{effect.Value} kezdeményezés {AdjustedDuration(effect, divineJudgment)} akcióra");
                    break;
                case SpellEffectType.ProtectionFromEvil:
                    ApplyCharacterEffects(caster, characterTargets, effect, spell, ActiveSpellEffectType.ProtectionFromEvil, divineJudgment);
                    notes.Add($"gonosz elleni védelem {AdjustedDuration(effect, divineJudgment)} akcióra");
                    break;
                case SpellEffectType.GuardianAngel:
                    ApplyCharacterEffects(caster, characterTargets, effect, spell, ActiveSpellEffectType.GuardianAngel, divineJudgment);
                    notes.Add($"👼 Őrangyal {AdjustedDuration(effect, divineJudgment)} akcióra");
                    break;
                case SpellEffectType.Sanctuary:
                    var sanctuaryTargets = LivingPartyWithPositions()
                        .Where(entry => Chebyshev(entry.Position, casterPosition) <= Math.Max(0, spell.AreaRadius))
                        .Select(entry => entry.Character).ToList();
                    ApplyCharacterEffects(caster, sanctuaryTargets, effect, spell, ActiveSpellEffectType.Sanctuary, divineJudgment);
                    notes.Add($"⛪ Szentély: {sanctuaryTargets.Count} karakter védett {AdjustedDuration(effect, divineJudgment)} akcióra");
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
        foreach (var entry in damage.Where(entry => entry.Value > 0))
        {
            if (inCombat && entry.Key == currentEnemy)
                currentDamage += Math.Min(entry.Value, entry.Key.CurrentHitPoints);
            else
                ApplyExplorationSpellDamage(caster, entry.Key, entry.Value, notes);
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
                     (int)Math.Round(caster.Abilities.Intelligence * effect.IntelligenceMultiplier) +
                     caster.Level * effect.LevelMultiplier + effect.Value;
        if (caster.HasPerk(PerkIds.MageElementalMaster)) rolled = (int)Math.Ceiling(rolled * 1.25);
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
            var bonus = caster.Abilities.Intelligence +
                        (caster.HasPerk(PerkIds.MageArcaneFocus) ? 2 : 0) +
                        caster.GetMagicItemBonus(MagicItemEffect.Hit) +
                        caster.SpellEffectValue(ActiveSpellEffectType.Invisibility) +
                        caster.SpellEffectValue(ActiveSpellEffectType.HitBonus);
            var hit = roll == 20 || roll != 1 && roll + bonus >= 11 + enemy.EffectiveSpeed;
            result = new SpellResolutionResult(hit, false, roll == 20, hit ? $"mágikus támadás {roll + bonus}" : $"mellé {roll + bonus}");
        }
        else
        {
            var dc = 10 + caster.Abilities.Intelligence / 2 + spell.Level;
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
                AdjustedDuration(effect, divineJudgment),
                effect.Dice, (int)Math.Round(caster.Abilities.Intelligence * effect.IntelligenceMultiplier),
                false, effect.Dice is not null && caster.HasPerk(PerkIds.MageElementalMaster) ? 125 : 100));
            notes.Add($"{enemy.Name}: {TimedEffectName(type)} ({AdjustedDuration(effect, divineJudgment)} akció)");
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
            effect.Value, AdjustedDuration(effect, divineJudgment), effect.Dice,
            (int)Math.Round(caster.Abilities.Intelligence * effect.IntelligenceMultiplier), true,
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
                  (int)Math.Round(caster.Abilities.Intelligence * effect.IntelligenceMultiplier) +
                  caster.Level * effect.LevelMultiplier + effect.Value;
            if (!fullHealing && divineJudgment) amount *= 2;
            if (caster.HasPerk(PerkIds.PriestHealingGrace))
                amount = (int)Math.Ceiling(amount * 1.25);
            var before = character.CurrentVitality;
            character.RestoreVitality(amount);
            notes.Add($"{character.Name}: ❤️ +{character.CurrentVitality - before} HP");
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
        _fogOfWar.RevealFrom(_maze, avatar.Position);
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

    private static int AdjustedDuration(SpellEffectDefinition effect, bool divineJudgment) =>
        divineJudgment ? effect.Duration * 2 : effect.Duration;
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
        _fogOfWar.RevealFrom(_maze, target);
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
            _fogOfWar.RevealFrom(_maze, pair.Second);
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
            _ => true
        });
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

    private void CheckBossDiscoveryAt(IEnumerable<Position> positions)
    {
        var revealed = positions.ToHashSet();
        if (revealed.Count == 0) return;
        CheckBossDiscovery(_maze.Enemies.Where(enemy => revealed.Contains(enemy.Position)));
    }

    private void CheckBossDiscovery(IEnumerable<Enemy> enemies)
    {
        var discovered = enemies.Where(enemy => enemy.Definition.IsBoss &&
                !_seenBossIds.Contains(enemy.Definition.Id))
            .DistinctBy(enemy => enemy.Definition.Id, StringComparer.OrdinalIgnoreCase).ToList();
        if (discovered.Count == 0) return;
        foreach (var boss in discovered)
        {
            _seenBossIds.Add(boss.Definition.Id);
            var narrative = BossNarratives.GetValueOrDefault(boss.Definition.Id)
                ?? new BossNarrative("Ismeretlen fejezet",
                    [$"Én vagyok {boss.Name}. E folyosók titkait nem osztom meg veletek."]);
            _renderer.DrawBossIntroduction(boss.Definition, narrative.ChapterTitle, narrative.Speech,
                _maze, _fogOfWar, _player.Position);
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
            _renderer.DrawStoryOverlay("A TIZENKÉT ZÁR FELNYÍLIK", "XIV. fejezet — A Rubin Útja",
                TwelveKeysStory, _maze, _fogOfWar, _player.Position);
    }

    private void StartBattle(Enemy enemy)
    {
        if (_battleStarted) return;
        CheckBossDiscovery([enemy]);
        _timeStopUsedThisBattle = false;
        _turnUndeadUsedThisBattle.Clear();
        if (_renderer.IsSpellInfoPageOpen) _renderer.CloseSpellInfoPage();
        _battleStarted = true;
        _soundEffects.Play(SoundEffect.BattleStart);
        _renderer.DrawBattleStarted(enemy);
        var result = _battleSystem.Resolve(SelectedCharacter, enemy, entry =>
        {
            _renderer.DrawBattleRound(entry);
            PlayBattleRoundSound(entry);
            _renderer.RefreshBattleStatusRows();
            WaitForBattleContinue(enemy);
        }, () => ChooseBattlePlayerAction(enemy), () => TryPartyMembersActInLeaderBattle(enemy));
        var needLoss = DrainNeedsAfterBattle(SelectedCharacter, enemy.Definition.StrengthTier);
        _renderer.RefreshCharacterSheet(SelectedCharacter);

        if (result.PlayerWon)
        {
            AwardBossKey(enemy);
            _soundEffects.Play(SoundEffect.Victory);
            var experienceAwards = DistributeExperience(SelectedCharacter, enemy.Definition.ExperienceReward);
            _maze.ReplaceEnemyWithCorpse(enemy);
            _renderer.DrawMapCellAfterBattle(_maze, _fogOfWar, enemy.Position, _player.Position);
            _renderer.DrawBattleResult(result, enemy);
            _renderer.DrawInventoryMessage($"A csata kifárasztott: 🍖 -{needLoss}, 💧 -{needLoss}.", ConsoleColor.DarkYellow);
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
            _nextNeedsDrain = DateTime.UtcNow + TimeSpan.FromMinutes(1);
            return;
        }

        _renderer.DrawBattleResult(result, enemy);
        _soundEffects.Play(SoundEffect.Defeat);
        _saveAfterBattle = false;
        _renderer.DrawInventoryMessage($"A csata kifárasztott: 🍖 -{needLoss}, 💧 -{needLoss}.", ConsoleColor.DarkYellow);
        _renderer.DrawGameOver(SelectedCharacter.Name);
        _gameOver = true;
    }

    private void DrainNeeds()
    {
        var hungry = _gameData.GetStatus(CharacterStatusIds.Hungry);
        var thirsty = _gameData.GetStatus(CharacterStatusIds.Thirsty);
        foreach (var character in CharacterRoster.Party.Members.Where(character => character.IsAlive))
        {
            var foodLoss = 2 + character.MaximumVitality / 60;
            character.ConsumeFood(foodLoss);
            var waterLoss = 2;
            if (character.CurrentVitality < character.MaximumVitality) waterLoss++;
            if (character.CurrentVitality * 2 < character.MaximumVitality) waterLoss++;
            character.ConsumeWater(waterLoss);
            character.SynchronizeNeedStatuses(hungry, thirsty);
        }
        _renderer.RefreshCharacterSheet(SelectedCharacter);
    }

    private int DrainNeedsAfterBattle(LiveCharacter character, int monsterTier)
    {
        var loss = _random.Next(1, 6) + Math.Clamp(monsterTier, 1, 5);
        character.ConsumeFood(loss);
        character.ConsumeWater(loss);
        character.SynchronizeNeedStatuses(
            _gameData.GetStatus(CharacterStatusIds.Hungry),
            _gameData.GetStatus(CharacterStatusIds.Thirsty));
        return loss;
    }

    private void PlayBattleRoundSound(BattleLogEntry entry)
    {
        if (entry.Kind is not (BattleLogKind.PlayerAttack or BattleLogKind.EnemyAttack or BattleLogKind.CriticalHit)) return;
        _soundEffects.Play(entry.Message.Contains("NEM TALÁL", StringComparison.OrdinalIgnoreCase)
            ? SoundEffect.Miss
            : SoundEffect.Hit);
    }

    private void WaitForBattleContinue(Enemy enemy)
    {
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Spacebar) return;
            if (IsSaveGameShortcut(key))
            {
                _saveAfterBattle = true;
                _renderer.DrawInventoryMessage("Mentés kérve: a csata lezárása után automatikusan elkészül.", ConsoleColor.Yellow);
                continue;
            }
            if (!IsHelpShortcut(key)) continue;
            ShowInGameHelp();
            _renderer.DrawBattleStarted(enemy);
            _renderer.RefreshBattleStatusRows();
        }
    }

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
        _fogOfWar.RevealFrom(_maze, destination.Value);
        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, destination.Value);
        _renderer.DrawDeveloperMessage("Fejlesztői mód: a partyvezér a kijárat mellé teleportált.");
    }

    private void ToggleDeveloperPhasing()
    {
        _developerPhasing = !_developerPhasing;
        _renderer.DrawDeveloperMessage(_developerPhasing
            ? "Fejlesztői mód: fal-áthaladás engedélyezve."
            : "Fejlesztői mód: fal-áthaladás letiltva.");
    }

    private sealed record HeldInventoryItem(IItemDefinition Item, InventorySlotReference Source, int Charges);
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
        foreach (var member in _maze.PartyMembers) _fogOfWar.RevealFrom(_maze, member.Position);
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
        foreach (var avatar in _maze.PartyMembers) _fogOfWar.RevealFrom(_maze, avatar.Position);
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
            _gameData.GetManaGrowth(character.Abilities.Intelligence), _random));

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
        var selectedPerks = _renderer.DrawLevelUpScreen(character, result, offers);
        foreach (var perk in selectedPerks)
            if (character.AddPerk(perk)) character.ApplyPerkAcquisitionBonus(perk);
        ResolveSpellLearning(character, result);
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
                character.LearnSpell(_renderer.DrawSpellLearningScreen(character, choices,
                    learnedNumber, learningCount));
            }
        }
    }

    private sealed record ExperienceAward(LiveCharacter Character, LevelUpResult Result);

    private sealed record BossNarrative(string ChapterTitle, IReadOnlyList<string> Speech);

    private IReadOnlyList<PerkOffer> CreatePerkOffers(LiveCharacter character, LevelUpResult result)
    {
        var offers = new List<PerkOffer>();
        var milestones = new[] { 5, 15, 25 };
        for (var tier = 1; tier <= milestones.Length; tier++)
        {
            if (character.Perks.Any(perk => perk.Tier == tier)) continue;
            var firstLevel = milestones[tier - 1] - 2;
            var lastLevel = milestones[tier - 1] + 2;
            if (result.CurrentLevel < firstLevel) continue;

            int? triggerLevel = null;
            for (var level = Math.Max(result.PreviousLevel + 1, firstLevel); level <= Math.Min(result.CurrentLevel, lastLevel); level++)
            {
                if (level == lastLevel || _random.NextDouble() < 0.40)
                {
                    triggerLevel = level;
                    break;
                }
            }

            // A funkció bevezetése előtt az ablakon túljutott mentések a következő szintlépéskor megkapják a kimaradt választást.
            if (triggerLevel is null && result.CurrentLevel >= lastLevel) triggerLevel = result.CurrentLevel;
            if (triggerLevel is not null)
                offers.Add(new PerkOffer(tier, triggerLevel.Value, _gameData.GetPerkChoices(character.CharacterClass.Id, tier)));
        }
        return offers;
    }

}
