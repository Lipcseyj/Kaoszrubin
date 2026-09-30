using KaoszRubin.Domain.Combat;

namespace KaoszRubin.World;

// =================================================================================================
// PÁLYASZERKESZTÉSI GYORSSÚGÓ
// -------------------------------------------------------------------------------------------------
// 1. A normál kampánypályákat a MazeLevelConfigurations.Configurations szótárban keresd.
// 2. Egy pályához általában az alapadatokat, a termeket/kincseket és a két encounter-listát kell megadni.
// 3. Többképernyős pályához WideMazeLayoutConfiguration, erdőhöz ForestMazeLayoutConfiguration használható.
//    A képernyőszámok 1-től indulnak.
// 4. A TrapCount, TrapIds és VisionModifier kampánypályákon központi balanszszabályból érkezik a fájl végén.
// 5. A küldetésszobák és a futásidejű feloldás haladó/belső régióban találhatók.
// =================================================================================================

#region Pályaszerkesztői alap API

/// <summary>Zárt, mindkét végpontját tartalmazó egész számtartomány.</summary>
/// <remarks>Például <c>new IntRange(3, 5)</c> futásonként 3, 4 vagy 5 értéket ad.</remarks>
public sealed record IntRange(int Minimum, int Maximum)
{
    /// <summary>Véletlen értéket választ a minimum és maximum között, mindkét végpontot beleértve.</summary>
    public int Roll(Random random) => random.Next(Minimum, Maximum + 1);
}

/// <summary>
/// Jól olvasható mennyiségi kategóriák pályakonfigurációkhoz. A pontos tartományokat az
/// <see cref="AmountRanges.Range"/> adja meg.
/// </summary>
public enum Amount { One, Few, TwoThree, Handful, Several, Pack, Lots, Horde }

/// <summary>Az <see cref="Amount"/> kategóriákat konkrét, véletleníthető tartományokká alakítja.</summary>
public static class AmountRanges
{
    /// <summary>Visszaadja a mennyiségi kategóriához tartozó darabszámtartományt.</summary>
    public static IntRange Range(this Amount amount) => amount switch
    {
        Amount.One => new(1, 1),
        Amount.Few => new(1, 2),
        Amount.TwoThree => new(2, 3),
        Amount.Handful => new(2, 4),
        Amount.Several => new(5, 9),
        Amount.Pack => new(10, 15),
        Amount.Lots => new(16, 25),
        Amount.Horde => new(26, 50),
        _ => new(1, 1)
    };
}

/// <summary>Egy encounter-csoport egyik szörnytípusa és csoportonkénti darabszáma.</summary>
/// <param name="EnemyId">A szörny <see cref="MonsterIds"/> szerinti azonosítója.</param>
/// <param name="Count">Ennyi példány kerül egy létrejövő csoportba.</param>
/// <param name="Role">A tag szerepe; a vezér szerep befolyásolhatja a csoport elhelyezését.</param>
public sealed record EnemyGroupMemberConfiguration(string EnemyId, IntRange Count,
    EnemyGroupRole Role = EnemyGroupRole.Member);

/// <summary>Az encounter elhelyezési és mozgási jellegének speciális felülbírálása.</summary>
public enum EnemyEncounterBehavior
{
    /// <summary>Normál encounter-viselkedés.</summary>
    Default,
    /// <summary>Nagyobb, vándorló csapatként kezelt encounter.</summary>
    Horde
}

/// <summary>Egy véletlenszerűen létrejövő ellenséges encounter teljes leírása.</summary>
/// <param name="GroupCount">A pályára kerülő ilyen csoportok száma.</param>
/// <param name="Members">A csoporton belüli szörnytípusok és mennyiségek.</param>
/// <param name="MovementProfile">Opcionális közös mozgásprofil; null esetén a szörny saját profilja érvényesül.</param>
/// <param name="Behavior">Normál vagy hordaszerű elhelyezés.</param>
/// <param name="ScreenNumber">Opcionális, 1-től számozott képernyő; null esetén automatikus elosztás.</param>
/// <param name="AreaId">Opcionális stabil területazonosító; explicit gráfnál ezt érdemes használni a sorszám helyett.</param>
/// <param name="TargetRoomKind">Opcionális szobatípus, például csak kúria vagy erdei labirintus.</param>
public sealed record EnemyEncounterConfiguration(IntRange GroupCount,
    IReadOnlyList<EnemyGroupMemberConfiguration> Members,
    EnemyMovementProfile? MovementProfile = null,
    EnemyEncounterBehavior Behavior = EnemyEncounterBehavior.Default,
    int? ScreenNumber = null,
    string? AreaId = null,
    RoomKind? TargetRoomKind = null);

/// <summary>Egy különleges szobába garantáltan elhelyezett ellenségtípus.</summary>
/// <param name="RoomId">A célként használt quest- vagy boss-szoba tartalomazonosítója.</param>
/// <param name="EnemyId">A szörny azonosítója.</param>
/// <param name="Count">A garantált példányszám.</param>
/// <param name="GuaranteedItemId">Opcionális tárgy, amelyet az encounter garantáltan biztosít.</param>
public sealed record QuestRoomEnemyEncounterConfiguration(string RoomId, string EnemyId, int Count,
    string? GuaranteedItemId = null);

/// <summary>Egy csapdatípus garantált elhelyezése a pályán.</summary>
/// <param name="TrapId">A <c>#Csapdák</c> CSV-szekcióban szereplő csapdaazonosító.</param>
/// <param name="ScreenNumber">
/// Opcionális, 1-től számozott képernyő. Null esetén a rendszer egyenletesen osztja el a garantált csapdákat.
/// </param>
/// <remarks>A garantált példány beleszámít a pálya <see cref="MazeLevelConfiguration.TrapCount"/> értékébe.</remarks>
public sealed record GuaranteedTrapConfiguration(string TrapId, int? ScreenNumber = null);

/// <summary>
/// Rövid, olvasható gyármetódusok a leggyakoribb encounter-típusokhoz.
/// </summary>
/// <remarks>
/// A <c>groups</c> az encounter-csoportok számát, a <c>size</c>/<c>count</c>/<c>followers</c> pedig egy csoport
/// létszámát jelenti. A <c>screen</c> mindenhol opcionális és 1-től számozott.
/// </remarks>
public static class Encounters
{
    /// <summary>Azonos szörnyekből álló, egy vagy több csoport.</summary>
    public static EnemyEncounterConfiguration Same(string enemyId, Amount groups, Amount size,
        EnemyMovementProfile? movement = EnemyMovementProfile.Stationary, int? screen = null) =>
        new(groups.Range(), [new(enemyId, size.Range())], movement, ScreenNumber: screen);

    /// <summary>Egyedül elhelyezett példányok ugyanabból a szörnytípusból.</summary>
    public static EnemyEncounterConfiguration Solo(string enemyId, Amount count,
        EnemyMovementProfile? movement = null, int? screen = null) =>
        new(count.Range(), [new(enemyId, Amount.One.Range())], movement, ScreenNumber: screen);

    /// <summary>Két szörnytípust vegyítő csoportok.</summary>
    public static EnemyEncounterConfiguration Mixed(string firstEnemyId, Amount firstCount,
        string secondEnemyId, Amount secondCount, Amount groups,
        EnemyMovementProfile? movement = EnemyMovementProfile.Stationary, int? screen = null) =>
        new(groups.Range(), [new(firstEnemyId, firstCount.Range()), new(secondEnemyId, secondCount.Range())], movement,
            ScreenNumber: screen);

    /// <summary>Egy vezérből és azonos típusú követőkből álló csoportok.</summary>
    public static EnemyEncounterConfiguration LeaderGroup(string leaderId, string followerId,
        Amount groups, Amount followers, EnemyMovementProfile? movement = EnemyMovementProfile.Stationary,
        int? screen = null) =>
        new(groups.Range(),
            [new(leaderId, Amount.One.Range(), EnemyGroupRole.Leader), new(followerId, followers.Range())], movement,
            ScreenNumber: screen);

    /// <summary>Azonos szörnyekből álló, vándorló hordák.</summary>
    public static EnemyEncounterConfiguration Horde(string enemyId, Amount groups, Amount size, int? screen = null) =>
        new(groups.Range(), [new(enemyId, size.Range())], EnemyMovementProfile.Wander,
            EnemyEncounterBehavior.Horde, screen);

    /// <summary>Két szörnytípust vegyítő, vándorló hordák.</summary>
    public static EnemyEncounterConfiguration MixedHorde(string firstEnemyId, Amount firstCount,
        string secondEnemyId, Amount secondCount, Amount groups, int? screen = null) =>
        new(groups.Range(), [new(firstEnemyId, firstCount.Range()), new(secondEnemyId, secondCount.Range())],
            EnemyMovementProfile.Wander, EnemyEncounterBehavior.Horde, screen);

    /// <summary>Vezérből és követőkből álló, vándorló hordák.</summary>
    public static EnemyEncounterConfiguration LeaderHorde(string leaderId, string followerId,
        Amount groups, Amount followers, int? screen = null) =>
        new(groups.Range(),
            [new(leaderId, Amount.One.Range(), EnemyGroupRole.Leader), new(followerId, followers.Range())],
            EnemyMovementProfile.Wander, EnemyEncounterBehavior.Horde, screen);
}

/// <summary>Egy kampánypálya vagy külön küldetéshelyszín teljes szerkesztői konfigurációja.</summary>
/// <remarks>
/// Egy szokásos pályához a szint, név, terem- és kincstartományok, valamint a két encounter-lista elegendő.
/// A többi mezőnek használható alapértéke van, vagy csak speciális pályákhoz szükséges.
/// </remarks>
public sealed class MazeLevelConfiguration
{
    #region Kötelező alapadatok

    /// <summary>A pálya sorszáma és alapértelmezett nehézségi szintje.</summary>
    public required int Level { get; init; }

    /// <summary>A játékban megjelenő pályanév.</summary>
    public required string Name { get; init; }

    /// <summary>A generált termek teljes száma. Többképernyős pályán ez oszlik el a képernyők között.</summary>
    public required IntRange RoomCount { get; init; }

    /// <summary>A generált termek minimális és maximális szélessége/magassága.</summary>
    public required IntRange RoomSize { get; init; }

    /// <summary>A generált kincsesládák teljes száma.</summary>
    public required IntRange TreasureChestCount { get; init; }

    /// <summary>Egy véletlen kincs aranymennyiségének tartománya.</summary>
    public required IntRange TreasureGold { get; init; }

    /// <summary>A szobákban létrejövő ellenséges encounterek választható készlete.</summary>
    public required IReadOnlyList<EnemyEncounterConfiguration> RoomEncounters { get; init; }

    /// <summary>A folyosókon létrejövő ellenséges encounterek választható készlete.</summary>
    public required IReadOnlyList<EnemyEncounterConfiguration> CorridorEncounters { get; init; }

    #endregion

    #region Elrendezés és megjelenés

    /// <summary>
    /// A pálya szerkezete. Null esetén egységes, klasszikus labirintus készül; lineáris többképernyős
    /// pályához <see cref="WideMazeLayoutConfiguration"/>, gráfos erdőhöz
    /// <see cref="ForestMazeLayoutConfiguration"/> használható.
    /// </summary>
    public MazeLayoutConfiguration? Layout { get; init; }

    /// <summary>Dupla széles folyosó esélye 0 és 1 között. Csak klasszikus elrendezésnél hat.</summary>
    public double DoubleWidthCorridorChance { get; init; } = 0.80;

    /// <summary>A pályafalak megjelenítéséhez használt karakter.</summary>
    public System.Text.Rune WallRune { get; init; } = new('█');

    /// <summary>A pályafalak konzolszíne.</summary>
    public ConsoleColor WallColor { get; init; } = ConsoleColor.DarkGray;

    /// <summary>
    /// Ha true, a pálya erdőgráfja futás közben JSON-ból felülírható.
    /// A felülírás csak erdei layout esetén értelmezett.
    /// </summary>
    public bool ForestGraphJsonOverrideEnabled { get; init; }

    #endregion

    #region Zsákmány és csapdák

    /// <summary>A generált tárgyak átokesélye százalékban.</summary>
    public int ItemCurseChancePercent { get; init; } = 8;

    /// <summary>
    /// A pálya teljes csapdaszáma. Kampánypályáknál ezt jelenleg a fájl végi központi balansz állítja be.
    /// </summary>
    public IntRange TrapCount { get; set; } = new(0, 0);

    /// <summary>
    /// A véletlenszerűen választható csapdaazonosítók. Kampánypályáknál ezt jelenleg a fájl végi
    /// központi balansz állítja be.
    /// </summary>
    public IReadOnlyList<string> TrapIds { get; set; } = [];

    /// <summary>
    /// A felsorolt csapdák egy-egy példánya garantáltan megjelenik, és beleszámít a TrapCount értékébe.
    /// A ScreenNumber az encounterökhöz hasonlóan 1-től számozott; null esetén a rendszer osztja el.
    /// </summary>
    public IReadOnlyList<GuaranteedTrapConfiguration> GuaranteedTraps { get; set; } = [];

    #endregion

    #region Haladó: látás és különleges küldetésszobák

    /// <summary>
    /// A karakterek alap látótávjára alkalmazott módosító. Kampánypályáknál központi balansz állítja be.
    /// </summary>
    public int VisionModifier { get; set; }

    /// <summary>A generátor által garantáltan létrehozandó küldetésszobák tartalomazonosítói.</summary>
    public IReadOnlyList<string> QuestRoomIds { get; init; } = [];

    /// <summary>Küldetésszoba-azonosítóhoz rendelt konkrét questláda.</summary>
    public IReadOnlyDictionary<string, Domain.Quests.QuestChestId> QuestChestPlacements { get; init; }
        = new Dictionary<string, Domain.Quests.QuestChestId>();

    /// <summary>A generátor által garantáltan létrehozandó boss-szobák tartalomazonosítói.</summary>
    public IReadOnlyList<string> BossRoomIds { get; init; } = [];

    /// <summary>Különleges szobánként meghatározza, hol helyezkedjen el a szoba a pálya útvonalán.</summary>
    public IReadOnlyDictionary<string, SpecialRoomPlacement> SpecialRoomPlacements { get; init; }
        = new Dictionary<string, SpecialRoomPlacement>();

    /// <summary>Különleges szobánként megadható a belépéshez szükséges küldetés.</summary>
    public IReadOnlyDictionary<string, Domain.Quests.QuestId> QuestDoorRequirements { get; init; }
        = new Dictionary<string, Domain.Quests.QuestId>();

    /// <summary>A különleges szobákba garantáltan elhelyezett ellenségek.</summary>
    public IReadOnlyList<QuestRoomEnemyEncounterConfiguration> QuestRoomEnemyEncounters { get; init; } = [];

    #endregion

    #region Belső generálási adapter – pályakonfigurációhoz általában nem kell módosítani

    /// <summary>A szerkesztői konfigurációból egy konkrét futás generálási beállításait készíti el.</summary>
    public MazeGenerationSettings CreateGenerationSettings(Random random) => new()
    {
        DoubleWidthCorridorChance = Layout is ClassicMazeLayoutConfiguration classic
            ? classic.DoubleWidthCorridorChance
            : DoubleWidthCorridorChance,
        WideCorridorNarrowingChance = Layout is WideMazeLayoutConfiguration wide
            ? wide.NarrowingChance
            : 0,
        RoomCount = RoomCount.Roll(random),
        MinimumRoomSize = RoomSize.Minimum,
        MaximumRoomSize = RoomSize.Maximum,
        TreasureChestCount = TreasureChestCount.Roll(random),
        TreasureGoldRange = TreasureGold,
        WallRune = WallRune,
        WallColor = WallColor,
        LevelName = Name,
        QuestRoomIds = QuestRoomIds,
        BossRoomIds = BossRoomIds,
        SpecialRoomPlacements = SpecialRoomPlacements,
        QuestDoorRequirements = QuestDoorRequirements
    };

    #endregion
}

#endregion

/// <summary>A kampány számozott labirintusszintjeinek konfigurációs katalógusa.</summary>
public static class MazeLevelConfigurations
{
    /// <summary>Az utolsó, kézzel definiált kampánypálya sorszáma.</summary>
    public const int FinalLevel = 22;

    #region Kampánypályák – új pályát és pályatartalmat elsősorban itt szerkessz

    // Minimális minta:
    // [23] = new()
    // {
    //     Level = 23,
    //     Name = "Pályanév",
    //     RoomCount = new(10, 14), RoomSize = new(4, 8),
    //     TreasureChestCount = new(6, 10), TreasureGold = new(1000, 2000),
    //     RoomEncounters = [Encounters.Same(MonsterIds.Goblin, Amount.Few, Amount.Several)],
    //     CorridorEncounters = [Encounters.Solo(MonsterIds.Ork, Amount.Few)]
    // };
    private static readonly IReadOnlyDictionary<int, MazeLevelConfiguration> Configurations =
        new Dictionary<int, MazeLevelConfiguration>
        {
            [1] = new()
            {
                Name = "Patkányjáratok",
                //WallRune = new('♠'),
                //WallColor = ConsoleColor.DarkGreen,
                DoubleWidthCorridorChance = 0.95,
                Level = 1,
                RoomCount = Amount.Several.Range(),
                RoomSize = new(3, 5),
                TreasureChestCount = new(2, 3),
                TreasureGold = new(50, 120),
                QuestRoomIds = ["KING_CHEST_ROOM"],
                SpecialRoomPlacements = new Dictionary<string, SpecialRoomPlacement>
                {
                    ["KING_CHEST_ROOM"] = SpecialRoomPlacement.SideBranch,
                },
                QuestDoorRequirements = new Dictionary<string, Domain.Quests.QuestId>
                {
                    ["KING_CHEST_ROOM"] = Domain.Quests.QuestId.AureliosEmissaryChest,
                },
                QuestChestPlacements = new Dictionary<string, Domain.Quests.QuestChestId>
                {
                    ["KING_CHEST_ROOM"] = new("AURELIOS_HELP")
                },
                QuestRoomEnemyEncounters =
                [
                    new("KING_CHEST_ROOM", MonsterIds.Kobold, 3),
                    new("KING_CHEST_ROOM", MonsterIds.Goblin, 1)
                ],
                RoomEncounters =
                [
                    Encounters.Same(MonsterIds.Óriáspatkány, Amount.Several, Amount.Handful),
                    Encounters.Mixed(MonsterIds.Óriáspatkány, Amount.Handful, MonsterIds.Óriásdenevér, Amount.Handful, Amount.One),
                    Encounters.Same(MonsterIds.Kobold, Amount.Few, Amount.Several),
                    Encounters.Same(MonsterIds.Goblin, Amount.Few, Amount.Few)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo(MonsterIds.Óriáspatkány, Amount.Few),
                    Encounters.Solo(MonsterIds.Óriásdenevér, Amount.Few),
                    Encounters.Solo(MonsterIds.Kobold, Amount.Few),
                    Encounters.Horde(MonsterIds.Óriáspatkány, Amount.Few, Amount.Handful)
                ]
            },
            [2] = new()
            {
                Name = "Patkányvezér",
                DoubleWidthCorridorChance = 0.40,
                Level = 2,
                RoomCount = Amount.Several.Range(),
                RoomSize = new(3, 5),
                TreasureChestCount = Amount.Handful.Range(),
                TreasureGold = new(60, 160),
                GuaranteedTraps = [new("TR101")],
                RoomEncounters =
                [
                    Encounters.Mixed(MonsterIds.Óriáspatkány, Amount.Handful, MonsterIds.Óriásdenevér, Amount.Handful, Amount.Few),
                    Encounters.Same(MonsterIds.Óriáspatkány, Amount.Few, Amount.Handful),
                    Encounters.Same(MonsterIds.Csontváz, Amount.One, Amount.One),
                    Encounters.LeaderGroup(MonsterIds.Patkányember, MonsterIds.Óriáspatkány, Amount.One, Amount.Handful)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo(MonsterIds.Óriáspatkány, Amount.Handful),
                    Encounters.Solo(MonsterIds.Kobold, Amount.Few),
                    Encounters.Horde(MonsterIds.Óriáspatkány, Amount.Handful, Amount.Handful)
                ]
            },
            [3] = new()
            {
                Name = "Goblinüregek",
                WallRune = new('▓'),
                WallColor = ConsoleColor.DarkGreen,
                Level = 3,
                DoubleWidthCorridorChance = 0.75,
                RoomCount = new(8, 10),
                RoomSize = new(3, 6),
                TreasureChestCount = new(8, 12),
                TreasureGold = new(80, 200),
                QuestRoomIds = ["GOBLIN_CHIEF_ROOM"],
                SpecialRoomPlacements = new Dictionary<string, SpecialRoomPlacement>
                {
                    ["GOBLIN_CHIEF_ROOM"] = SpecialRoomPlacement.SideBranch,
                },
                QuestDoorRequirements = new Dictionary<string, Domain.Quests.QuestId>
                {
                    ["GOBLIN_CHIEF_ROOM"] = Domain.Quests.QuestId.GoblinChiefHunt,
                },
                QuestChestPlacements = new Dictionary<string, Domain.Quests.QuestChestId>
                {
                    ["GOBLIN_CHIEF_ROOM"] = new("GOBLIN_CHEST")
                },
                QuestRoomEnemyEncounters =
                [
                    new("GOBLIN_CHIEF_ROOM", MonsterIds.GoblinFőnök, 1),
                    new("GOBLIN_CHIEF_ROOM", MonsterIds.Goblin, 3),
                    new("GOBLIN_CHIEF_ROOM", MonsterIds.GoblinÍjász, 2)
                ],
                RoomEncounters =
                [
                    Encounters.Same(MonsterIds.Kobold, Amount.Few, Amount.Few),
                    Encounters.Mixed(MonsterIds.Goblin, Amount.Several, MonsterIds.Kobold, Amount.Few, Amount.Few),
                    Encounters.Mixed(MonsterIds.Goblin, Amount.Several, MonsterIds.GoblinÍjász, Amount.Handful, Amount.Few),
                    Encounters.Same(MonsterIds.Csontváz, Amount.Few, Amount.Few),
                    Encounters.LeaderGroup(MonsterIds.GoblinVajákos, MonsterIds.Goblin, Amount.One, Amount.Several),
                    Encounters.LeaderGroup(MonsterIds.GoblinÍjász, MonsterIds.Goblin, Amount.One, Amount.Several)
                ],
                CorridorEncounters =
                [
                    Encounters.MixedHorde(MonsterIds.Óriáspatkány, Amount.TwoThree, MonsterIds.Óriásdenevér, Amount.TwoThree, Amount.One),
                    Encounters.Solo(MonsterIds.Goblin, Amount.Several, EnemyMovementProfile.Patrol),
                    Encounters.Solo(MonsterIds.Farkas, Amount.Few, EnemyMovementProfile.Patrol),
                ]
            },
            [4] = new()
            {
                Name = "Vadállatok odúi",
                WallRune = new('▒'),
                WallColor = ConsoleColor.DarkYellow,
                Level = 4,
                DoubleWidthCorridorChance = 0.70,
                RoomCount = Amount.Pack.Range(),
                RoomSize = new(3, 7),
                TreasureChestCount = Amount.Several.Range(),
                TreasureGold = new(140, 300),
                RoomEncounters =
                [
                    Encounters.Same(MonsterIds.Vadkan, Amount.Few, Amount.Handful),
                    Encounters.Same(MonsterIds.Goblin, Amount.Few, Amount.Several),
                    Encounters.Mixed(MonsterIds.Csontváz, Amount.Several, MonsterIds.Zombi, Amount.Few, Amount.Few),
                    Encounters.LeaderGroup(MonsterIds.Ork, MonsterIds.Goblin, Amount.Few, Amount.Several),
                    Encounters.LeaderGroup(MonsterIds.KáoszmágusTanítvány, MonsterIds.Goblin,
                        Amount.One, Amount.Several)
                ],
                CorridorEncounters =
                [
                    Encounters.Horde(MonsterIds.Farkas, Amount.Few, Amount.TwoThree),
                    Encounters.Solo(MonsterIds.Farkas, Amount.Several, EnemyMovementProfile.Patrol),
                    Encounters.Solo(MonsterIds.Goblin, Amount.Few),
                    Encounters.Solo(MonsterIds.Patkányember, Amount.Few),
                    Encounters.Solo(MonsterIds.Vadkan, Amount.Few),
                    Encounters.Solo(MonsterIds.HegyiHiúz, Amount.Several)
                ]
            },
            [5] = new()
            {
                Level = 5,
                Name = "A holtak katakombái",
                DoubleWidthCorridorChance = 0.82,
                WallRune = new('▓'),
                WallColor = ConsoleColor.DarkGray,
                RoomCount = Amount.Pack.Range(),
                RoomSize = new(4, 7),
                TreasureChestCount = Amount.Several.Range(),
                TreasureGold = new(240, 480),
                QuestRoomIds = ["RODERIC_MEETING", "RODERIC_INSIGNIA", "RODERIC_PATRIARCHS", "RODERIC_RELICS"],
                SpecialRoomPlacements = new Dictionary<string, SpecialRoomPlacement>
                {
                    ["RODERIC_MEETING"] = SpecialRoomPlacement.MiddleRoute,
                    ["RODERIC_INSIGNIA"] = SpecialRoomPlacement.SideBranch,
                    ["RODERIC_PATRIARCHS"] = SpecialRoomPlacement.SideBranch,
                    ["RODERIC_RELICS"] = SpecialRoomPlacement.SideBranch
                },
                QuestDoorRequirements = new Dictionary<string, Domain.Quests.QuestId>
                {
                    ["RODERIC_INSIGNIA"] = Domain.Quests.QuestId.RodericFallenComradesInsignia,
                    ["RODERIC_PATRIARCHS"] = Domain.Quests.QuestId.RodericSharedBladeTrial,
                    ["RODERIC_RELICS"] = Domain.Quests.QuestId.RodericOrderRelics
                },
                QuestChestPlacements = new Dictionary<string, Domain.Quests.QuestChestId>
                {
                    ["RODERIC_RELICS"] = new("RODERIC_ORDER_RELICS")
                },
                QuestRoomEnemyEncounters =
                [
                    new("RODERIC_INSIGNIA", MonsterIds.CsontvázLovag, 3,
                        Domain.Inventory.MiscItemIds.FallenKnightInsignia),
                    new("RODERIC_PATRIARCHS", MonsterIds.ÉlőholtPátriárka, 2),
                    new("RODERIC_PATRIARCHS", MonsterIds.Csontváz, 3),
                    new("RODERIC_RELICS", MonsterIds.Zombi, 4)
                ],
                RoomEncounters =
                [
                    Encounters.Same(MonsterIds.Csontváz, Amount.Several, Amount.Handful),
                    Encounters.Mixed(MonsterIds.Zombi, Amount.Several, MonsterIds.Csontváz, Amount.Few, Amount.Few),
                    Encounters.LeaderGroup(MonsterIds.Ghoul, MonsterIds.Csontváz, Amount.One, Amount.Several)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo(MonsterIds.Csontváz, Amount.Several, EnemyMovementProfile.Patrol),
                    Encounters.Solo(MonsterIds.Zombi, Amount.Few),
                    Encounters.MixedHorde(MonsterIds.Zombi, Amount.Few, MonsterIds.Csontváz, Amount.Few, Amount.Few),
                ]
            },
            [6] = new()
            {
                Level = 6,
                Name = "Tiltott Erdő",
                ForestGraphJsonOverrideEnabled = true,
                Layout = new ForestMazeLayoutConfiguration(
                    new DungeonAreaGraphConfiguration(new IntRange(6, 8), MinimumExitDistance: 3,
                        MaximumDegree: 3, BranchChance: 0.52, ExtraConnectionChance: 0.18),
                    new ForestGenerationConfiguration
                    {
                        // 0.18: elszórt ligetek; 0.58: erdők és rétek; 0.95: ösvényes rengeteg.
                        ForestDensity = 0.58,
                        GroveSize = new IntRange(5, 14),
                        BiomeSize = 22,
                        PineChance = 0.28,
                        BushChance = 0.17,
                        FlowerBushChance = 0.05,
                        BushGroupSize = new IntRange(2, 5),
                        ForestEdgeWidth = 3,
                        ThicketChance = 0.08,
                        UndergrowthChance = 0.27,
                        DenseUndergrowthChance = 0.11,
                        LakeCount = new IntRange(1, 3),
                        LakeRadius = new IntRange(2, 5),
                        MarshChance = 0.62,
                        MarshCount = new IntRange(1, 3),
                        MarshRadius = new IntRange(3, 7),
                        TrailWidth = 2,
                        TrailWinding = 0.75,
                        ExtraTrailChance = 0.35,
                        BuildingCount = new IntRange(1, 2),
                        BuildingSize = new IntRange(5, 8),
                        BuildingPartitionChance = 0.75,
                        ManorBuildingChance = 0.45,
                        LabyrinthBuildingChance = 0.15,
                        ManorBuildingWidth = new IntRange(10, 18),
                        ManorBuildingHeight = new IntRange(8, 14),
                        ManorRoomCount = new IntRange(3, 8),
                        LabyrinthBuildingWidth = new IntRange(13, 22),
                        LabyrinthBuildingHeight = new IntRange(8, 17),
                        BuildingExtraConnectionChance = 0.18,
                        BuildingSecondEntranceChance = 0.20,
                        BuildingStyles =
                        [
                            new("mossy-timber", new("forbidden-timber-wall", new('▓'),
                                ConsoleColor.DarkYellow, ConsoleColor.Black, false, true), 4),
                            new("old-stone", new("forbidden-stone-wall", new('▣'),
                                ConsoleColor.Gray, ConsoleColor.Black, false, true), 3),
                            new("dark-manor", new("forbidden-manor-wall", new('▤'),
                                ConsoleColor.DarkGray, ConsoleColor.Black, false, true), 2,
                                new HashSet<ForestBuildingLayout>
                                    { ForestBuildingLayout.Manor, ForestBuildingLayout.Labyrinth })
                        ],
                        LockedBuildingDoorChance = 0.18,
                        OpenBuildingDoorChance = 0.12,
                        Palette = new ForestTerrainPalette
                        {
                            Tree = new("forbidden-tree", new('♠'), ConsoleColor.DarkGreen,
                                ConsoleColor.Black, false, true),
                            FlowerBush = new("forbidden-flower-bush", new('✿'), ConsoleColor.DarkMagenta,
                                ConsoleColor.Black, false, false),
                            Water = new("forbidden-water", new('≈'), ConsoleColor.DarkBlue,
                                ConsoleColor.Black, false, false),
                            Marsh = new("forbidden-marsh", new('≋'), ConsoleColor.DarkYellow,
                                ConsoleColor.DarkGreen, true, false),
                            BuildingWall = new("forbidden-building-wall", new('█'), ConsoleColor.Gray,
                                ConsoleColor.Black, false, true)
                        }
                    },
                    new ExplicitForestAreaGraphConfiguration(
                    [
                        new("MOSS_GATE", "Mohakapu", new(0, 0), ForestAreaTemplateCatalog.MixedForest),
                        new("WHISPERING_WOOD", "Suttogó rengeteg", new(1, 0),
                            ForestAreaTemplateCatalog.DenseCabinForest),
                        new("RAVEN_CROSSING", "Hollók elágazása", new(2, 0),
                            ForestAreaTemplateCatalog.MixedForest),
                        new("OLD_PINES", "Az öreg fenyves", new(1, -1),
                            ForestAreaTemplateCatalog.DenseCabinForest,
                            new() { PineChance = 0.72, BuildingCount = new(0, 1) }),
                        new("LOST_MANOR", "Az elveszett kúriák", new(1, 1),
                            ForestAreaTemplateCatalog.LakesAndManors,
                            new() { ManorBuildingChance = 1, LabyrinthBuildingChance = 0 }),
                        new("BLACKWATER", "Feketevíz lápja", new(2, 1),
                            ForestAreaTemplateCatalog.Swamp),
                        new("THORN_MAZE", "A tövisek útvesztője", new(3, 1),
                            ForestAreaTemplateCatalog.ForestLabyrinth),
                        new("WINDLESS_GLADE", "A Szélcsend tisztása", new(3, 2),
                            ForestAreaTemplateCatalog.OpenGroves)
                    ],
                    [
                        new("MOSS_GATE", "WHISPERING_WOOD"),
                        new("WHISPERING_WOOD", "RAVEN_CROSSING"),
                        new("WHISPERING_WOOD", "OLD_PINES"),
                        new("WHISPERING_WOOD", "LOST_MANOR"),
                        new("RAVEN_CROSSING", "BLACKWATER"),
                        new("LOST_MANOR", "BLACKWATER"),
                        new("BLACKWATER", "THORN_MAZE"),
                        new("THORN_MAZE", "WINDLESS_GLADE")
                    ], "MOSS_GATE", "WINDLESS_GLADE")),
                WallRune = new('♠'),
                WallColor = ConsoleColor.DarkGreen,
                RoomCount = new IntRange(42, 56),
                RoomSize = new IntRange(4, 8),
                TreasureChestCount = new IntRange(18, 26),
                TreasureGold = new IntRange(180, 460),
                RoomEncounters =
                [
                    Encounters.Same(MonsterIds.Farkas, Amount.Handful, Amount.Several),
                    Encounters.Mixed(MonsterIds.Vadkan, Amount.Few, MonsterIds.HegyiHiúz, Amount.Few,
                        Amount.Handful),
                    Encounters.Mixed(MonsterIds.Goblin, Amount.Several, MonsterIds.GoblinÍjász, Amount.Few,
                        Amount.Handful),
                    Encounters.LeaderGroup(MonsterIds.GoblinVajákos, MonsterIds.Goblin,
                        Amount.Few, Amount.Several),
                    Encounters.LeaderGroup(MonsterIds.OrkSámán, MonsterIds.Ork,
                        Amount.Few, Amount.Several) with
                        { AreaId = "LOST_MANOR", TargetRoomKind = RoomKind.Manor }
                ],
                CorridorEncounters =
                [
                    Encounters.Horde(MonsterIds.Farkas, Amount.Handful, Amount.Handful),
                    Encounters.Solo(MonsterIds.HegyiHiúz, Amount.Handful, EnemyMovementProfile.Patrol),
                    Encounters.MixedHorde(MonsterIds.Goblin, Amount.Several, MonsterIds.GoblinÍjász,
                        Amount.Few, Amount.Handful),
                    Encounters.Solo(MonsterIds.Vadkan, Amount.Handful)
                ]
            },
            [7] = new()
            {
                Level = 7,
                Name = "A nagy csarnokok szintje",
                Layout = new WideMazeLayoutConfiguration(new IntRange(2, 2), NarrowingChance: 0.12),
                WallRune = new('▦'),
                WallColor = ConsoleColor.DarkYellow,
                RoomCount = Amount.Lots.Range(),
                RoomSize = new(7, 11),
                TreasureChestCount = Amount.Pack.Range(),
                TreasureGold = new(180, 460),
                RoomEncounters =
                [
                    Encounters.Same(MonsterIds.Ork, Amount.Several, Amount.Several),
                    Encounters.Mixed(MonsterIds.Hobgoblin, Amount.Several, MonsterIds.Ork, Amount.Few, Amount.Few),
                    Encounters.LeaderGroup(MonsterIds.Ogre, MonsterIds.Ork, Amount.One, Amount.Pack),
                    Encounters.LeaderGroup(MonsterIds.OrkSámán, MonsterIds.Ork, Amount.One, Amount.Several),
                    Encounters.LeaderGroup(MonsterIds.Káoszpap, MonsterIds.Hobgoblin, Amount.One, Amount.Several)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo(MonsterIds.Ork, Amount.Few, EnemyMovementProfile.Patrol),
                    Encounters.Solo(MonsterIds.Hobgoblin, Amount.Few, EnemyMovementProfile.Patrol),
                    Encounters.MixedHorde(MonsterIds.Gnoll, Amount.Few, MonsterIds.Ork, Amount.Few, Amount.Few)
                ]
            },
            [8] = new()
            {
                Level = 8,
                Name = "A mérgező barlang",
                DoubleWidthCorridorChance = 0.88,
                WallRune = new('▒'),
                WallColor = ConsoleColor.DarkCyan,
                RoomCount = Amount.Pack.Range(),
                RoomSize = new(4, 8),
                TreasureChestCount = Amount.Several.Range(),
                TreasureGold = new(300, 780),
                ItemCurseChancePercent = 15,
                RoomEncounters =
                [
                    Encounters.Same(MonsterIds.Óriáspók, Amount.Several, Amount.Several),
                    Encounters.Mixed(MonsterIds.Savanyálka, Amount.Several, MonsterIds.BarlangiGyík, Amount.Several, Amount.Few),
                    Encounters.LeaderGroup(MonsterIds.ÓriásBaziliszkusz, MonsterIds.Óriáspók, Amount.Few, Amount.Several)
                ],
                CorridorEncounters =
                [
                    Encounters.Horde(MonsterIds.Óriásdenevér, Amount.Several, Amount.Several),
                    Encounters.Solo(MonsterIds.BarlangiGyík, Amount.Few, EnemyMovementProfile.Patrol)
                ]
            },
            [9] = new()
            {
                Level = 9,
                Name = "Az ork haditábor",
                Layout = new WideMazeLayoutConfiguration(new IntRange(2, 2), NarrowingChance: 0.14),
                WallRune = new('▦'),
                WallColor = ConsoleColor.DarkRed,
                RoomCount = new IntRange(18, 24),
                RoomSize = new(5, 8),
                TreasureChestCount = new IntRange(12, 18),
                TreasureGold = new(420, 820),
                RoomEncounters =
                [
                    Encounters.Same(MonsterIds.Goblin, Amount.Handful, Amount.Pack),
                    Encounters.Same(MonsterIds.Ork, Amount.Handful, Amount.Several),
                    Encounters.Mixed(MonsterIds.Hobgoblin, Amount.Few, MonsterIds.Bugbear, Amount.Few, Amount.Handful),
                    Encounters.LeaderGroup(MonsterIds.OrkSámán, MonsterIds.Ork, Amount.Few, Amount.Several),
                    Encounters.LeaderGroup(MonsterIds.OrkVérpap, MonsterIds.Ork, Amount.One, Amount.Pack)
                ],
                CorridorEncounters =
                [
                    Encounters.LeaderHorde(MonsterIds.Hobgoblin, MonsterIds.Goblin,
                        Amount.Handful, Amount.Lots),
                    Encounters.MixedHorde(MonsterIds.Ork, Amount.Several, MonsterIds.Goblin, Amount.Lots,
                        Amount.Handful),
                    Encounters.LeaderHorde(MonsterIds.OrkSámán, MonsterIds.Ork,
                        Amount.Few, Amount.Pack)
                ]
            },
            [10] = new()
            {
                Level = 10,
                Name = "Az elátkozott sírkamrák",
                DoubleWidthCorridorChance = 0.92,
                WallRune = new('▦'),
                WallColor = ConsoleColor.DarkMagenta,
                RoomCount = Amount.Pack.Range(),
                RoomSize = new(4, 8),
                TreasureChestCount = Amount.Pack.Range(),
                TreasureGold = new(520, 1000),
                ItemCurseChancePercent = 30,
                RoomEncounters =
                [
                    Encounters.Same(MonsterIds.Múmia, Amount.Several, Amount.Several),
                    Encounters.Mixed(MonsterIds.Wight, Amount.Several, MonsterIds.Ghoul, Amount.Several, Amount.Few),
                    Encounters.LeaderGroup(MonsterIds.ÉjiBanya, MonsterIds.Múmia, Amount.Few, Amount.Several),
                    Encounters.LeaderGroup(MonsterIds.Boszorkány, MonsterIds.Ghoul, Amount.One, Amount.Several)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo(MonsterIds.Wight, Amount.Few, EnemyMovementProfile.Patrol),
                    Encounters.Solo(MonsterIds.Ghoul, Amount.Several)
                ]
            },
            [11] = new()
            {
                Level = 11,
                Name = "Az óriások erődje",
                Layout = new WideMazeLayoutConfiguration(new IntRange(2, 2), NarrowingChance: 0.10),
                WallRune = new('▩'),
                WallColor = ConsoleColor.Gray,
                RoomCount = new IntRange(18, 24),
                RoomSize = new(7, 11),
                TreasureChestCount = new IntRange(14, 20),
                TreasureGold = new(560, 1050),
                RoomEncounters =
                [
                    Encounters.Same(MonsterIds.Bugbear, Amount.Handful, Amount.Several),
                    Encounters.Same(MonsterIds.Ogre, Amount.Handful, Amount.Handful),
                    Encounters.Mixed(MonsterIds.Troll, Amount.Few, MonsterIds.Ettin, Amount.Few, Amount.Few),
                    Encounters.LeaderGroup(MonsterIds.Fagyóriás, MonsterIds.Ogre, Amount.One, Amount.Several)
                ],
                CorridorEncounters =
                [
                    Encounters.LeaderHorde(MonsterIds.Ogre, MonsterIds.Ork,
                        Amount.Handful, Amount.Pack),
                    Encounters.MixedHorde(MonsterIds.Bugbear, Amount.Handful, MonsterIds.Gnoll, Amount.Handful,
                        Amount.Handful),
                    Encounters.LeaderHorde(MonsterIds.Ettin, MonsterIds.Goblin,
                        Amount.Handful, Amount.Pack)
                ]
            },
            [12] = new()
            {
                Level = 12,
                Name = "A sárkánykultusz szentélye",
                Layout = new WideMazeLayoutConfiguration(new IntRange(2, 2), NarrowingChance: 0.16),
                WallRune = new('▥'),
                WallColor = ConsoleColor.Red,
                RoomCount = new IntRange(19, 25),
                RoomSize = new(5, 9),
                TreasureChestCount = new IntRange(15, 21),
                TreasureGold = new(750, 1500),
                RoomEncounters =
                [
                    Encounters.Same(MonsterIds.Orgyilkos, Amount.Handful, Amount.Pack),
                    Encounters.Same(MonsterIds.Wyvern, Amount.Handful, Amount.Few),
                    Encounters.Mixed(MonsterIds.Kiméra, Amount.Few, MonsterIds.Ork, Amount.Several, Amount.Few),
                    Encounters.LeaderGroup(MonsterIds.VörösSárkány, MonsterIds.Orgyilkos, Amount.One, Amount.Pack),
                    Encounters.Mixed(MonsterIds.KáoszmágusTanítvány, Amount.Few,
                        MonsterIds.Orgyilkos, Amount.Pack, Amount.Few),
                    Encounters.LeaderGroup(MonsterIds.Káoszpap, MonsterIds.Orgyilkos, Amount.One, Amount.Several)
                ],
                CorridorEncounters =
                [
                    Encounters.LeaderHorde(MonsterIds.OrkSámán, MonsterIds.Orgyilkos,
                        Amount.Few, Amount.Pack),
                    Encounters.MixedHorde(MonsterIds.Hárpia, Amount.Handful, MonsterIds.Óriásdenevér, Amount.Several,
                        Amount.Handful),
                    Encounters.LeaderHorde(MonsterIds.Wyvern, MonsterIds.Goblin,
                        Amount.Handful, Amount.Several)
                ]
            },
            [13] = new()
            {
                Level = 13,
                Name = "A rothadó mocsár",
                Layout = new WideMazeLayoutConfiguration(new IntRange(2, 2), NarrowingChance: 0.18),
                WallRune = new('▒'),
                WallColor = ConsoleColor.DarkGreen,
                RoomCount = new IntRange(20, 26),
                RoomSize = new(5, 10),
                TreasureChestCount = new IntRange(16, 22),
                TreasureGold = new(850, 1700),
                ItemCurseChancePercent = 15,
                RoomEncounters =
                [
                    Encounters.Same(MonsterIds.PestishordozóPatkány, Amount.Handful, Amount.Pack),
                    Encounters.Same(MonsterIds.Savanyálka, Amount.Handful, Amount.Several),
                    Encounters.Mixed(MonsterIds.PestishordozóPatkány, Amount.Pack, MonsterIds.Óriáspók, Amount.Several, Amount.Handful),
                    Encounters.LeaderGroup(MonsterIds.Hidra, MonsterIds.BarlangiGyík, Amount.One, Amount.Pack),
                    Encounters.LeaderGroup(MonsterIds.Kígyópap, MonsterIds.Kígyóember, Amount.One, Amount.Several),
                    Encounters.LeaderGroup(MonsterIds.SötétDruida, MonsterIds.Óriáspók, Amount.One, Amount.Pack)
                ],
                CorridorEncounters =
                [
                    Encounters.LeaderHorde(MonsterIds.BarlangiGyík, MonsterIds.PestishordozóPatkány,
                        Amount.Handful, Amount.Pack),
                    Encounters.MixedHorde(MonsterIds.Óriáspók, Amount.Several, MonsterIds.PestishordozóPatkány, Amount.Several,
                        Amount.Handful),
                    Encounters.LeaderHorde(MonsterIds.ÉjiBanya, MonsterIds.Savanyálka,
                        Amount.Handful, Amount.Several),
                    Encounters.LeaderHorde(MonsterIds.SötétDruida, MonsterIds.Óriáspók,
                        Amount.One, Amount.Pack)
                ]
            },
            [14] = new()
            {
                Level = 14,
                Name = "A fojtogató mélyjárat",
                DoubleWidthCorridorChance = 0,
                WallRune = new('█'),
                WallColor = ConsoleColor.DarkGray,
                RoomCount = Amount.Pack.Range(),
                RoomSize = new(3, 5),
                TreasureChestCount = Amount.Several.Range(),
                TreasureGold = new(950, 1850),
                RoomEncounters =
                [
                    Encounters.Same(MonsterIds.Minotaurusz, Amount.Few, Amount.Few),
                    Encounters.Mixed(MonsterIds.Kőgólem, Amount.Few, MonsterIds.BarlangiGyík, Amount.Several, Amount.Few),
                    Encounters.LeaderGroup(MonsterIds.Beholder, MonsterIds.Óriásdenevér, Amount.One, Amount.Pack),
                    Encounters.LeaderGroup(MonsterIds.Kígyópap, MonsterIds.Kígyóember, Amount.One, Amount.Several)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo(MonsterIds.Minotaurusz, Amount.Several, EnemyMovementProfile.Patrol),
                    Encounters.Solo(MonsterIds.Kőgólem, Amount.Few),
                    Encounters.Solo(MonsterIds.Óriásdenevér, Amount.Several)
                ]
            },
            [15] = new()
            {
                Level = 15,
                Name = "A megtört kristálycsarnok",
                DoubleWidthCorridorChance = 0.72,
                WallRune = new('◆'),
                WallColor = ConsoleColor.Cyan,
                RoomCount = Amount.Pack.Range(),
                RoomSize = new(5, 9),
                TreasureChestCount = Amount.Pack.Range(),
                TreasureGold = new(1100, 2100),
                RoomEncounters =
                [
                    Encounters.Same(MonsterIds.Medúza, Amount.Few, Amount.Several),
                    Encounters.Mixed(MonsterIds.Kőgólem, Amount.Several, MonsterIds.Kiméra, Amount.Few, Amount.Few),
                    Encounters.LeaderGroup(MonsterIds.VénBeholder, MonsterIds.Beholder, Amount.One, Amount.Few),
                    Encounters.LeaderGroup(MonsterIds.Káoszmágus, MonsterIds.Kőgólem, Amount.One, Amount.Few)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo(MonsterIds.Medúza, Amount.Few, EnemyMovementProfile.Patrol),
                    Encounters.Solo(MonsterIds.Kiméra, Amount.Few),
                    Encounters.Solo(MonsterIds.Beholder, Amount.Few)
                ]
            },
            [16] = new()
            {
                Level = 16,
                Name = "A dermedt mélység",
                DoubleWidthCorridorChance = 0.62,
                WallRune = new('▒'),
                WallColor = ConsoleColor.White,
                RoomCount = Amount.Pack.Range(),
                RoomSize = new(6, 10),
                TreasureChestCount = Amount.Pack.Range(),
                TreasureGold = new(1250, 2400),
                RoomEncounters =
                [
                    Encounters.Same(MonsterIds.Fagyóriás, Amount.Few, Amount.Few),
                    Encounters.Mixed(MonsterIds.Fagyóriás, Amount.Few, MonsterIds.Lidércfarkas, Amount.Several, Amount.Few),
                    Encounters.LeaderGroup(MonsterIds.Csontsárkány, MonsterIds.Wight, Amount.One, Amount.Several),
                    Encounters.LeaderGroup(MonsterIds.Nekromanta, MonsterIds.Wight, Amount.One, Amount.Several)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo(MonsterIds.Lidércfarkas, Amount.Several, EnemyMovementProfile.Patrol),
                    Encounters.Solo(MonsterIds.Fagyóriás, Amount.Few),
                    Encounters.Solo(MonsterIds.Wight, Amount.Few)
                ]
            },
            [17] = new()
            {
                Level = 17,
                Name = "Az örökéj vámpírerődje",
                Layout = new WideMazeLayoutConfiguration(new IntRange(2, 3), NarrowingChance: 0.13),
                WallRune = new('⣿'),
                WallColor = ConsoleColor.DarkMagenta,
                RoomCount = new IntRange(22, 30),
                RoomSize = new(5, 9),
                TreasureChestCount = new IntRange(18, 25),
                TreasureGold = new(1450, 2750),
                ItemCurseChancePercent = 20,
                RoomEncounters =
                [
                    Encounters.Same(MonsterIds.Ghoul, Amount.Handful, Amount.Pack),
                    Encounters.Same(MonsterIds.Vámpír, Amount.Handful, Amount.Handful),
                    Encounters.Mixed(MonsterIds.Halállovag, Amount.Few, MonsterIds.Wight, Amount.Several, Amount.Few),
                    Encounters.LeaderGroup(MonsterIds.Ősvámpír, MonsterIds.Vámpír, Amount.One, Amount.Several),
                    Encounters.LeaderGroup(MonsterIds.Nekromanta, MonsterIds.Ghoul, Amount.Few, Amount.Pack)
                ],
                CorridorEncounters =
                [
                    Encounters.LeaderHorde(MonsterIds.Nekromanta, MonsterIds.Csontváz,
                        Amount.One, Amount.Pack),
                    Encounters.MixedHorde(MonsterIds.Ghoul, Amount.Several, MonsterIds.Múmia, Amount.Handful,
                        Amount.Handful),
                    Encounters.LeaderHorde(MonsterIds.Vámpír, MonsterIds.Lidércfarkas,
                        Amount.Handful, Amount.Several),
                    Encounters.LeaderHorde(MonsterIds.Halállovag, MonsterIds.Zombi,
                        Amount.Handful, Amount.Pack)
                ]
            },
            [18] = new()
            {
                Level = 18,
                Name = "A sárkányok temetője",
                DoubleWidthCorridorChance = 0.84,
                WallRune = new('█'),
                WallColor = ConsoleColor.Gray,
                RoomCount = Amount.Pack.Range(),
                RoomSize = new(6, 11),
                TreasureChestCount = new(12, 18),
                TreasureGold = new(1650, 3150),
                RoomEncounters =
                [
                    Encounters.Same(MonsterIds.Csontsárkány, Amount.Few, Amount.Few),
                    Encounters.Mixed(MonsterIds.Wyvern, Amount.Several, MonsterIds.Csontsárkány, Amount.Few, Amount.Few),
                    Encounters.LeaderGroup(MonsterIds.Drakolich, MonsterIds.Halállovag, Amount.One, Amount.Several)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo(MonsterIds.Wyvern, Amount.Several, EnemyMovementProfile.Patrol),
                    Encounters.Solo(MonsterIds.Csontsárkány, Amount.Few),
                    Encounters.Solo(MonsterIds.Halállovag, Amount.Few)
                ]
            },
            [19] = new()
            {
                Level = 19,
                Name = "A démoni sík: Parázspusztaság",
                Layout = new WideMazeLayoutConfiguration(new IntRange(3, 3), NarrowingChance: 0.08),
                WallRune = new('█'),
                WallColor = ConsoleColor.DarkRed,
                RoomCount = new IntRange(24, 32),
                RoomSize = new(6, 10),
                TreasureChestCount = new(20, 28),
                TreasureGold = new(1900, 3600),
                ItemCurseChancePercent = 25,
                RoomEncounters =
                [
                    Encounters.Same(MonsterIds.Pokolfajzat, Amount.Handful, Amount.Pack),
                    Encounters.Mixed(MonsterIds.DémoniKorcs, Amount.Several, MonsterIds.Pokolfajzat, Amount.Pack,
                        Amount.Handful),
                    Encounters.Mixed(MonsterIds.Parázsdémon, Amount.Several, MonsterIds.KarmosDémon, Amount.Few,
                        Amount.Few),
                    Encounters.LeaderGroup(MonsterIds.Pokolőr, MonsterIds.Parázsdémon, Amount.Few, Amount.Several),
                    Encounters.LeaderGroup(MonsterIds.Pokolfejedelem, MonsterIds.Démonlovag, Amount.One, Amount.Pack),
                    Encounters.LeaderGroup(MonsterIds.Káoszmágus, MonsterIds.Pokolfajzat, Amount.Few, Amount.Lots),
                    Encounters.LeaderGroup(MonsterIds.Vérmágus, MonsterIds.DémoniKorcs, Amount.One, Amount.Pack)
                ],
                CorridorEncounters =
                [
                    Encounters.LeaderHorde(MonsterIds.KarmosDémon, MonsterIds.Pokolfajzat,
                        Amount.Handful, Amount.Lots),
                    Encounters.MixedHorde(MonsterIds.DémoniKorcs, Amount.Several, MonsterIds.Pokolfajzat, Amount.Lots,
                        Amount.Handful),
                    Encounters.LeaderHorde(MonsterIds.Pokolőr, MonsterIds.Parázsdémon,
                        Amount.Handful, Amount.Pack),
                    Encounters.LeaderHorde(MonsterIds.Démonlovag, MonsterIds.DémoniKorcs,
                        Amount.Few, Amount.Pack),
                    Encounters.LeaderHorde(MonsterIds.Káoszmágus, MonsterIds.Pokolfajzat,
                        Amount.One, Amount.Lots)
                ]
            },
            [20] = new()
            {
                Level = 20,
                Name = "A démoni sík: Vértrónus",
                Layout = new WideMazeLayoutConfiguration(new IntRange(3, 3), NarrowingChance: 0.11),
                WallRune = new('▓'),
                WallColor = ConsoleColor.Red,
                RoomCount = new IntRange(26, 34),
                RoomSize = new(7, 11),
                TreasureChestCount = new(22, 30),
                TreasureGold = new(2200, 4200),
                ItemCurseChancePercent = 25,
                RoomEncounters =
                [
                    Encounters.Same(MonsterIds.DémoniKorcs, Amount.Handful, Amount.Pack),
                    Encounters.Mixed(MonsterIds.Parázsdémon, Amount.Several, MonsterIds.KarmosDémon, Amount.Handful,
                        Amount.Handful),
                    Encounters.Mixed(MonsterIds.Pokolőr, Amount.Few, MonsterIds.Vérdémon, Amount.Few, Amount.Few),
                    Encounters.Mixed(MonsterIds.Pokolfejedelem, Amount.Few, MonsterIds.Démonpók, Amount.Several,
                        Amount.Few),
                    Encounters.LeaderGroup(MonsterIds.BalorDémon, MonsterIds.Démonlovag, Amount.Few, Amount.Several),
                    Encounters.LeaderGroup(MonsterIds.KáoszFőpap, MonsterIds.Pokolfajzat, Amount.Few, Amount.Lots)
                ],
                CorridorEncounters =
                [
                    Encounters.LeaderHorde(MonsterIds.Pokolőr, MonsterIds.DémoniKorcs,
                        Amount.Handful, Amount.Pack),
                    Encounters.MixedHorde(MonsterIds.Vérdémon, Amount.Few, MonsterIds.Pokolfajzat, Amount.Lots,
                        Amount.Handful),
                    Encounters.LeaderHorde(MonsterIds.Démonlovag, MonsterIds.Parázsdémon,
                        Amount.Handful, Amount.Pack),
                    Encounters.LeaderHorde(MonsterIds.Pokolfejedelem, MonsterIds.Pokolfajzat,
                        Amount.Few, Amount.Lots),
                    Encounters.LeaderHorde(MonsterIds.Vérmágus, MonsterIds.Vérdémon,
                        Amount.One, Amount.Several)
                ]
            },
            [21] = new()
            {
                Level = 21,
                Name = "A káosz szíve",
                DoubleWidthCorridorChance = 0.80,
                WallRune = new('▒'),
                WallColor = ConsoleColor.Magenta,
                RoomCount = Amount.Pack.Range(),
                RoomSize = new(7, 12),
                TreasureChestCount = new(16, 22),
                TreasureGold = new(2600, 5000),
                RoomEncounters =
                [
                    Encounters.Mixed(MonsterIds.VénBeholder, Amount.Few, MonsterIds.Drakolich, Amount.Few, Amount.Few),
                    Encounters.LeaderGroup(MonsterIds.Pokolfejedelem, MonsterIds.Démonlovag, Amount.Few, Amount.Several),
                    Encounters.LeaderGroup(MonsterIds.Káoszsárkány, MonsterIds.Drakolich, Amount.One, Amount.Few),
                    Encounters.LeaderGroup(MonsterIds.Feketemágus, MonsterIds.Káoszlovag, Amount.One, Amount.Pack)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo(MonsterIds.VénBeholder, Amount.Few, EnemyMovementProfile.Patrol),
                    Encounters.Solo(MonsterIds.Drakolich, Amount.Few),
                    Encounters.Solo(MonsterIds.Pokolfejedelem, Amount.Few)
                ]
            },
            [22] = new()
            {
                Level = 22,
                Name = "A káosz trónja",
                DoubleWidthCorridorChance = 0.86,
                WallRune = new('▓'),
                WallColor = ConsoleColor.Magenta,
                RoomCount = new(13, 17),
                RoomSize = new(6, 12),
                TreasureChestCount = new(18, 24),
                TreasureGold = new(2800, 5600),
                RoomEncounters =
                [
                    Encounters.Mixed(MonsterIds.Minotaurusz, Amount.Few, MonsterIds.Medúza, Amount.Few, Amount.Few),
                    Encounters.Mixed(MonsterIds.Kőgólem, Amount.Few, MonsterIds.Beholder, Amount.Few, Amount.Few),
                    Encounters.Mixed(MonsterIds.Vámpír, Amount.Few, MonsterIds.Vérfarkas, Amount.Several, Amount.Few),
                    Encounters.Mixed(MonsterIds.FeketeSárkány, Amount.Few, MonsterIds.Kiméra, Amount.Few, Amount.Few),
                    Encounters.LeaderGroup(MonsterIds.Lich, MonsterIds.Halállovag, Amount.Few, Amount.Several),
                    Encounters.LeaderGroup(MonsterIds.Pokolfejedelem, MonsterIds.Démonlovag, Amount.Few, Amount.Pack),
                    Encounters.LeaderGroup(MonsterIds.KáoszFőpap, MonsterIds.Káoszlovag, Amount.One, Amount.Several)
                ],
                CorridorEncounters =
                [
                    Encounters.MixedHorde(MonsterIds.Wyvern, Amount.Few, MonsterIds.Hárpia, Amount.Several,
                        Amount.Few),
                    Encounters.MixedHorde(MonsterIds.Troll, Amount.Few, MonsterIds.ÉjiBanya, Amount.Few, Amount.Few),
                    Encounters.MixedHorde(MonsterIds.Wight, Amount.Few, MonsterIds.Démonpók, Amount.Several, Amount.Few),
                    Encounters.Solo(MonsterIds.VénBeholder, Amount.Few, EnemyMovementProfile.Patrol),
                    Encounters.Solo(MonsterIds.Pokolfejedelem, Amount.Few),
                    Encounters.LeaderHorde(MonsterIds.KáoszFőpap, MonsterIds.Káoszlovag,
                        Amount.One, Amount.Several)
                ]
            }
        };

    #endregion

    #region Nyilvános lekérdezés

    /// <summary>Visszaadja a kért kampánypálya teljes, automatikus balanszértékekkel kiegészített konfigurációját.</summary>
    /// <param name="level">Az 1-től számozott kampányszint.</param>
    /// <remarks>
    /// A kézzel fel nem sorolt, magasabb szintekhez a metódus tartalék konfigurációt generál. A normál kampány
    /// jelenlegi felső határát a <see cref="FinalLevel"/> adja meg.
    /// </remarks>
    public static MazeLevelConfiguration Get(int level)
    {
        if (Configurations.TryGetValue(level, out var configuration)) return ConfigureVisionAndTraps(configuration);
        var increase = level - 12;
        var tier = Math.Clamp(4 + increase / 3, 4, 5);
        var (leader, follower, peer) = tier switch
        {
            4 => (MonsterIds.Beholder, MonsterIds.Ogre, MonsterIds.Kiméra),
            _ => (MonsterIds.Pokolfejedelem, MonsterIds.Démonlovag, MonsterIds.Ősvámpír)
        };
        return ConfigureVisionAndTraps(new MazeLevelConfiguration
        {
            Level = level,
            Name = $"A mélység {level}. szintje",
            WallRune = level % 2 == 0 ? new('▓') : new('█'),
            WallColor = level % 2 == 0 ? ConsoleColor.DarkMagenta : ConsoleColor.DarkGray,
            DoubleWidthCorridorChance = Math.Max(0.60, 0.80 - increase * 0.02),
            RoomCount = new(8 + increase / 2, 11 + increase / 2),
            RoomSize = new(5, Math.Min(10, 8 + increase / 3)),
            TreasureChestCount = new(7 + increase, 10 + increase),
            TreasureGold = new(750 + increase * 80, 1400 + increase * 130),
            RoomEncounters =
            [
                Encounters.Same(peer, Amount.Few, Amount.Several),
                Encounters.LeaderGroup(leader, follower, Amount.Few, Amount.Several),
                Encounters.Mixed(follower, Amount.Several, peer, Amount.Few, Amount.Few)
            ],
            CorridorEncounters =
            [
                Encounters.Solo(follower, Amount.Few, EnemyMovementProfile.Patrol),
                Encounters.Solo(peer, Amount.Few)
            ]
        });
    }

    #endregion

    #region Belső automatikus balansz – pályatartalom szerkesztésekor általában nem kell módosítani

    private static readonly string[] BasicTraps = ["TR001"];
    private static readonly string[] SpellTraps =
        ["TR101", "TR102", "TR103", "TR104", "TR105", "TR106", "TR107", "TR108", "TR109", "TR110", "TR111", "TR112"];
    private static readonly string[] LevelTwoTraps = ["TR001", "TR101"];
    private static readonly string[] EarlyTraps = ["TR001", "TR002", "TR003", .. SpellTraps];
    private static readonly string[] MidTraps = ["TR001", "TR002", "TR003", "TR004", "TR008", .. SpellTraps];
    private static readonly string[] AdvancedTraps = ["TR002", "TR003", "TR004", "TR005", "TR008", .. SpellTraps];
    private static readonly string[] DeadlyTraps = ["TR003", "TR004", "TR005", "TR006", "TR008", .. SpellTraps];
    private static readonly string[] ChaosTraps = ["TR004", "TR005", "TR006", "TR007", "TR008", .. SpellTraps];

    private static MazeLevelConfiguration ConfigureVisionAndTraps(MazeLevelConfiguration configuration)
    {
        configuration.VisionModifier = configuration.Level switch
        {
            5 or 6 or 13 => -1,
            10 or 14 or 18 or 21 => -2,
            _ => 0
        };
        (configuration.TrapCount, configuration.TrapIds) = configuration.Level switch
        {
            1 => (new IntRange(3, 7), BasicTraps),
            2 => (new IntRange(4, 8), LevelTwoTraps),
            <= 7 => (new IntRange(5, 9), EarlyTraps),
            <= 10 => (new IntRange(6, 11), MidTraps),
            <= 14 => (new IntRange(6, 11), AdvancedTraps),
            <= 18 => (new IntRange(6, 13), DeadlyTraps),
            _ => (new IntRange(7, 14), ChaosTraps)
        };
        return configuration;
    }

    #endregion
}

#region Speciális küldetéshelyszínek – normál kampánypályához nem kell módosítani

/// <summary>A normál kampányszintektől külön betöltött, önálló küldetéshelyszínek konfigurációi.</summary>
public static class QuestLocationConfigurations
{
    /// <summary>Sir Malrec sírkápolnájának helyszínazonosítója.</summary>
    public const string RodericMalrec = "RODERIC_MALREC_CHAPEL";

    /// <summary>Az összes regisztrált, önálló küldetéshelyszín.</summary>
    public static IReadOnlyList<MazeLevelConfiguration> All => [Get(RodericMalrec)];

    /// <summary>Azonosító alapján visszaadja az önálló küldetéshelyszín konfigurációját.</summary>
    /// <exception cref="KeyNotFoundException">Az azonosítóhoz nem tartozik regisztrált helyszín.</exception>
    public static MazeLevelConfiguration Get(string id) => id switch
    {
        RodericMalrec => new MazeLevelConfiguration
        {
            Level = 5,
            Name = "Sir Malrec sírkápolnája",
            DoubleWidthCorridorChance = 0.55,
            WallRune = new('▓'),
            WallColor = ConsoleColor.DarkMagenta,
            RoomCount = new(6, 8),
            RoomSize = new(4, 7),
            TreasureChestCount = Amount.Several.Range(),
            TreasureGold = new(180, 360),
            BossRoomIds = ["MALREC_CHAMBER"],
            SpecialRoomPlacements = new Dictionary<string, SpecialRoomPlacement>
            {
                ["MALREC_CHAMBER"] = SpecialRoomPlacement.SideBranch
            },
            QuestDoorRequirements = new Dictionary<string, Domain.Quests.QuestId>
            {
                ["MALREC_CHAMBER"] = Domain.Quests.QuestId.RodericOathbreakerKnight,
            },
            QuestRoomEnemyEncounters =
            [
                new("MALREC_CHAMBER", MonsterIds.SirMalrec, 1),
                new("MALREC_CHAMBER", MonsterIds.CsontvázLovag, 4)
            ],
            RoomEncounters =
            [
                Encounters.Same(MonsterIds.Csontváz, Amount.Few, Amount.Few),
                Encounters.Mixed(MonsterIds.Zombi, Amount.Handful, MonsterIds.PáncélozottZombi, Amount.Handful, Amount.Few),
                Encounters.LeaderGroup(MonsterIds.Ghoul, MonsterIds.CsontvázŐr, Amount.One, Amount.Several),
                Encounters.LeaderGroup(MonsterIds.Ghoul, MonsterIds.PáncélozottZombi, Amount.One, Amount.Several)
            ],
            CorridorEncounters =
            [
                Encounters.Solo(MonsterIds.CsontvázŐr, Amount.Several, EnemyMovementProfile.Patrol),
                Encounters.Solo(MonsterIds.Zombi, Amount.Several),
                Encounters.Solo(MonsterIds.CsontvázLovag, Amount.Several, EnemyMovementProfile.Patrol)
            ]
        },
        _ => throw new KeyNotFoundException($"Ismeretlen küldetéshelyszín: {id}")
    };
}

#endregion

#region Belső, adatbetöltés után feloldott encounter-típusok

/// <summary>A szöveges szörnyazonosítóból feloldott futásidejű csoporttag.</summary>
public sealed record ResolvedEnemyGroupMember(EnemyDefinition Definition, IntRange Count, EnemyGroupRole Role);

/// <summary>A szöveges szörnyazonosítókból feloldott, generálásra kész futásidejű encounter.</summary>
public sealed record ResolvedEnemyEncounter(IntRange GroupCount,
    IReadOnlyList<ResolvedEnemyGroupMember> Members, EnemyMovementProfile? MovementProfile,
    EnemyEncounterBehavior Behavior = EnemyEncounterBehavior.Default,
    int? ScreenNumber = null,
    string? AreaId = null,
    RoomKind? TargetRoomKind = null);

#endregion
