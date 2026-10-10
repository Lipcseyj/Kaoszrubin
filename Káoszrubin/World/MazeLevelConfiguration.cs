using KaoszRubin.Domain.Combat;
using System.ComponentModel;
using System.Globalization;

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
[TypeConverter(typeof(IntRangeTypeConverter))]
public sealed record IntRange(int Minimum, int Maximum)
{
    /// <summary>Véletlen értéket választ a minimum és maximum között, mindkét végpontot beleértve.</summary>
    public int Roll(Random random) => random.Next(Minimum, Maximum + 1);
}

public enum EnemyEncounterPosture
{
    Normal,
    Ambush
}

public sealed class IntRangeTypeConverter : ExpandableObjectConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) =>
        destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is not string text) return base.ConvertFrom(context, culture, value);
        var parts = text.Split("..", StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.Integer, culture, out var minimum) ||
            !int.TryParse(parts[1], NumberStyles.Integer, culture, out var maximum) || minimum > maximum)
            throw new FormatException("A tartomány formátuma: minimum..maximum (például 2..5).");
        return new IntRange(minimum, maximum);
    }
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value,
        Type destinationType) => destinationType == typeof(string) && value is IntRange range
        ? $"{range.Minimum}..{range.Maximum}" : base.ConvertTo(context, culture, value, destinationType);
    public override bool GetCreateInstanceSupported(ITypeDescriptorContext? context) => true;
    public override object CreateInstance(ITypeDescriptorContext? context, System.Collections.IDictionary values) =>
        new IntRange((int)values[nameof(IntRange.Minimum)]!, (int)values[nameof(IntRange.Maximum)]!);
}

/// <summary>
/// Jól olvasható mennyiségi kategóriák pályakonfigurációkhoz. A pontos tartományokat az
/// <see cref="AmountRanges.Range"/> adja meg.
/// </summary>
public enum Amount { One, Few, Pair, TwoThree, Handful, Band, Several, Pack, Lots, Horde }

/// <summary>Az <see cref="Amount"/> kategóriákat konkrét, véletleníthető tartományokká alakítja.</summary>
public static class AmountRanges
{
    /// <summary>Visszaadja a mennyiségi kategóriához tartozó darabszámtartományt.</summary>
    public static IntRange Range(this Amount amount) => amount switch
    {
        Amount.One => new(1, 1),
        Amount.Few => new(1, 2),
        Amount.Pair => new(2, 2),
        Amount.TwoThree => new(2, 3),
        Amount.Handful => new(2, 4),
        Amount.Band => new(3, 6),
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
    RoomKind? TargetRoomKind = null,
    TerrainTag TargetTerrainTags = TerrainTag.None,
    EnemyEncounterPosture Posture = EnemyEncounterPosture.Normal,
    int TriggerDistance = 0);

/// <summary>Egy különleges szobába garantáltan elhelyezett ellenségtípus.</summary>
/// <param name="RoomId">A célként használt quest- vagy boss-szoba tartalomazonosítója.</param>
/// <param name="EnemyId">A szörny azonosítója.</param>
/// <param name="Count">A garantált példányszám.</param>
/// <param name="GuaranteedItemId">Opcionális tárgy, amelyet az encounter garantáltan biztosít.</param>
/// <param name="Role">A garantált csoporttag szerepe; minibossnál tipikusan Leader.</param>
public sealed record QuestRoomEnemyEncounterConfiguration(string RoomId, string EnemyId, int Count,
    string? GuaranteedItemId = null, EnemyGroupRole Role = EnemyGroupRole.Member);

/// <summary>Egy csapdatípus garantált elhelyezése a pályán.</summary>
/// <param name="TrapId">A <c>#Csapdák</c> CSV-szekcióban szereplő csapdaazonosító.</param>
/// <param name="ScreenNumber">
/// Opcionális, 1-től számozott képernyő. Null esetén a rendszer egyenletesen osztja el a garantált csapdákat.
/// </param>
/// <remarks>A garantált példány beleszámít a pálya <see cref="MazeLevelConfiguration.TrapCount"/> értékébe.</remarks>
public sealed record GuaranteedTrapConfiguration(string TrapId, int? ScreenNumber = null);

/// <summary>Küldetésszoba célterülete. A képernyőszám 1-től indul; cél nélkül a kijárati terület az alapértelmezés.</summary>
public sealed record QuestRoomPlacementConfiguration(int? ScreenNumber = null, string? AreaId = null);

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

    /// <summary>Terepre célzott, éber és mozdulatlan csoport, amely a parti közeledésekor támad.</summary>
    public static EnemyEncounterConfiguration TerrainAmbush(string enemyId, Amount groups, Amount size,
        TerrainTag terrainTags, int triggerDistance = 4, int? screen = null) =>
        new(groups.Range(), [new(enemyId, size.Range())], EnemyMovementProfile.Stationary,
            ScreenNumber: screen, TargetTerrainTags: terrainTags,
            Posture: EnemyEncounterPosture.Ambush, TriggerDistance: Math.Max(1, triggerDistance));
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

    /// <summary>A generátor által garantáltan létrehozandó küldetésszobák tartalomazonosítói. Erdős pályán csak épületbelsőbe kerülhetnek.</summary>
    public IReadOnlyList<string> QuestRoomIds { get; init; } = [];

    /// <summary>Küldetésszobánként opcionális képernyőszám vagy stabil AreaId; a főút/mellékág szabály külön megmarad.</summary>
    public IReadOnlyDictionary<string, QuestRoomPlacementConfiguration> QuestRoomPlacements { get; init; }
        = new Dictionary<string, QuestRoomPlacementConfiguration>();

    /// <summary>Küldetésszoba-azonosítóhoz rendelt konkrét questláda.</summary>
    public IReadOnlyDictionary<string, Domain.Quests.QuestChestId> QuestChestPlacements { get; init; }
        = new Dictionary<string, Domain.Quests.QuestChestId>();

    /// <summary>Pályán belüli, egyszeri fogadói megállók; mindegyik külön épületet foglal el.</summary>
    public IReadOnlyList<ForestInnConfiguration> ForestInns { get; init; } = [];

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
        QuestRoomPlacements = QuestRoomPlacements.Concat(ForestInns.Select(inn =>
            new KeyValuePair<string, QuestRoomPlacementConfiguration>(inn.RoomId, new(AreaId: inn.AreaId))))
            .ToDictionary(pair => pair.Key, pair => pair.Value),
        SpecialRoomMinimumFreeCells = QuestRoomIds.Concat(BossRoomIds).ToDictionary(id => id,
            id => QuestRoomEnemyEncounters.Where(encounter => encounter.RoomId == id).Sum(encounter => encounter.Count)
                + (QuestChestPlacements.ContainsKey(id) ? 1 : 0))
            .Concat(ForestInns.Select(inn => new KeyValuePair<string, int>(inn.RoomId, 9)))
            .ToDictionary(pair => pair.Key, pair => pair.Value),
        InnRoomIds = ForestInns.Select(inn => inn.RoomId).ToArray(),
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
    public const int FinalLevel = 25;

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
                    Encounters.MixedHorde(MonsterIds.Zombi, Amount.Few, MonsterIds.Csontváz, Amount.Handful, Amount.Few),
                    Encounters.Horde(MonsterIds.Zombi, Amount.Few, Amount.Several)
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
                        BuildingSize = new IntRange(3, 6),
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
                        LockedBuildingDoorChance = 0.6,
                        OpenBuildingDoorChance = 0.12,
                        Palette = new ForestTerrainPalette
                        {
                            Tree = new("forbidden-tree", new('♠'), ConsoleColor.DarkGreen,
                                ConsoleColor.Black, false, true),
                            FlowerBush = new("forbidden-flower-bush", new('✿'), ConsoleColor.DarkMagenta,
                                ConsoleColor.Black, true, false),
                            Water = new("forbidden-water", new('≈'), ConsoleColor.DarkBlue,
                                ConsoleColor.Black, false, false),
                            Marsh = new("forbidden-marsh", new('≋'), ConsoleColor.DarkYellow,
                                ConsoleColor.DarkGreen, true, false),
                            BuildingWall = new("forbidden-building-wall", new('█'), ConsoleColor.Gray,
                                ConsoleColor.Black, false, true)
                        }
                    }, ExplicitGraph: ForbiddenForestGraph.CreateGraph()),
                WallRune = new('♠'),
                WallColor = ConsoleColor.DarkGreen,
                RoomCount = new IntRange(42, 56),
                RoomSize = new IntRange(4, 8),
                TreasureChestCount = new IntRange(18, 26),
                TreasureGold = new IntRange(180, 460),
                RoomEncounters = [
                    Encounters.Mixed(firstEnemyId: MonsterIds.Útonálló, firstCount: Amount.Pair, secondEnemyId: MonsterIds.HegyiHiúz, secondCount: Amount.Pair, groups: Amount.Handful) with { TargetRoomKind = RoomKind.Clearing },
                    Encounters.Mixed(firstEnemyId: MonsterIds.Goblin, firstCount: Amount.Several, secondEnemyId: MonsterIds.GoblinÍjász, secondCount: Amount.TwoThree, groups: Amount.Handful),
                    Encounters.LeaderGroup(leaderId: MonsterIds.GoblinVajákos, followerId: MonsterIds.Goblin, groups: Amount.Few, followers: Amount.Several),
                    Encounters.LeaderGroup(MonsterIds.OrkSámán, MonsterIds.Ork,
                        Amount.Few, Amount.Several) with
                        { AreaId = "LOST_MANOR", TargetRoomKind = RoomKind.Manor },
                    new EnemyEncounterConfiguration(GroupCount: new IntRange(3, 4), Members: [new(MonsterIds.Goblin, new IntRange(4, 6), EnemyGroupRole.Member), new(MonsterIds.GoblinÍjász, new IntRange(2, 4), EnemyGroupRole.Member), new(MonsterIds.GoblinVajákos, new IntRange(2, 4), EnemyGroupRole.Member), new(MonsterIds.GoblinFőnök, new IntRange(1, 1), EnemyGroupRole.Leader)], MovementProfile: EnemyMovementProfile.Stationary) with { AreaId = "MOSS_GATE" },
                    Encounters.LeaderGroup(leaderId: MonsterIds.KáoszmágusTanítvány, followerId: MonsterIds.Útonálló, groups: Amount.TwoThree, followers: Amount.Several) with { AreaId = "WHISPERING_WOOD" },
                    Encounters.Mixed(firstEnemyId: MonsterIds.GoblinÍjász, firstCount: Amount.TwoThree, secondEnemyId: MonsterIds.Útonálló, secondCount: Amount.Band, groups: Amount.Few) with { AreaId = "RAVEN_CROSSING" },
                    Encounters.LeaderGroup(leaderId: MonsterIds.Ghoul, followerId: MonsterIds.PáncélozottZombi, groups: Amount.Few, followers: Amount.Band) with { TargetRoomKind = RoomKind.Manor, AreaId = "LOST_MANOR" },
                    new EnemyEncounterConfiguration(GroupCount: new IntRange(1, 2), Members: [new(MonsterIds.Ghoul, new IntRange(1, 2), EnemyGroupRole.Member), new(MonsterIds.Csontváz, new IntRange(2, 3), EnemyGroupRole.Member), new(MonsterIds.CsontvázÍjász, new IntRange(2, 3), EnemyGroupRole.Member), new(MonsterIds.Zombi, new IntRange(3, 5), EnemyGroupRole.Member), new(MonsterIds.KáoszmágusTanítvány, new IntRange(1, 1), EnemyGroupRole.Leader)]) with { AreaId = "LOST_MANOR" },
                    Encounters.LeaderGroup(leaderId: MonsterIds.KáoszmágusTanítvány, followerId: MonsterIds.GyíkemberPortyázó, groups: Amount.TwoThree, followers: Amount.Band) with { AreaId = "BLACKWATER" },
                    new EnemyEncounterConfiguration(GroupCount: new IntRange(1, 1), Members: [new(MonsterIds.Káoszpap, new IntRange(1, 1), EnemyGroupRole.Leader), new(MonsterIds.KáoszmágusTanítvány, new IntRange(1, 1), EnemyGroupRole.Member), new(MonsterIds.GoblinVajákos, new IntRange(1, 2), EnemyGroupRole.Member), new(MonsterIds.GoblinÍjász, new IntRange(2, 4), EnemyGroupRole.Member), new(MonsterIds.GyíkemberPortyázó, new IntRange(3, 5), EnemyGroupRole.Member)], TargetRoomKind: RoomKind.Labyrinth),
                    new EnemyEncounterConfiguration(GroupCount: new IntRange(1, 1), Members: [new(MonsterIds.Káoszpap, new IntRange(1, 2), EnemyGroupRole.Leader), new(MonsterIds.Martalóc, new IntRange(4, 6), EnemyGroupRole.Member), new(MonsterIds.GyíkemberPortyázó, new IntRange(3, 4), EnemyGroupRole.Member), new(MonsterIds.Kígyóember, new IntRange(1, 2), EnemyGroupRole.Member)]) with { AreaId = "WINDLESS_GLADE" },
                    Encounters.Mixed(firstEnemyId: MonsterIds.Goblin, firstCount: Amount.TwoThree, secondEnemyId: MonsterIds.Útonálló, secondCount: Amount.TwoThree, groups: Amount.Handful),
                    Encounters.LeaderGroup(leaderId: MonsterIds.SötétDruida, followerId: MonsterIds.Óriásdenevér, groups: Amount.Few, followers: Amount.Several)
                ],
                CorridorEncounters = [
                    Encounters.Horde(MonsterIds.Farkas, Amount.Handful, Amount.Handful),
                    Encounters.Horde(enemyId: MonsterIds.HegyiHiúz, groups: Amount.Handful, size: Amount.TwoThree),
                    Encounters.MixedHorde(firstEnemyId: MonsterIds.Goblin, firstCount: Amount.Several, secondEnemyId: MonsterIds.GoblinÍjász, secondCount: Amount.Few, groups: Amount.Handful) with { AreaId = "MOSS_GATE" },
                    Encounters.Solo(enemyId: MonsterIds.Vadkan, count: Amount.Handful, movement: EnemyMovementProfile.Wander) with { AreaId = "MOSS_GATE" },
                    Encounters.Horde(enemyId: MonsterIds.Vadkan, groups: Amount.Handful, size: Amount.Several),
                    Encounters.Same(enemyId: MonsterIds.HegyiHiúz, groups: Amount.Few, size: Amount.TwoThree) with { TriggerDistance = 3, Posture = EnemyEncounterPosture.Ambush, TargetTerrainTags = TerrainTag.Bush | TerrainTag.Undergrowth | TerrainTag.DenseUndergrowth | TerrainTag.ThicketEdge, AreaId = "WHISPERING_WOOD" },
                    Encounters.Horde(enemyId: MonsterIds.HegyiHiúz, groups: Amount.TwoThree, size: Amount.TwoThree) with { AreaId = "WHISPERING_WOOD" },
                    Encounters.Mixed(firstEnemyId: MonsterIds.Útonálló, firstCount: Amount.Handful, secondEnemyId: MonsterIds.Orgyilkos, secondCount: Amount.Few, groups: Amount.TwoThree) with { Posture = EnemyEncounterPosture.Ambush, TriggerDistance = 4, TargetTerrainTags = TerrainTag.Bush | TerrainTag.Undergrowth | TerrainTag.DenseUndergrowth | TerrainTag.ThicketEdge, AreaId = "WHISPERING_WOOD" },
                    Encounters.Same(enemyId: MonsterIds.Orgyilkos, groups: Amount.Handful, size: Amount.Handful) with { Posture = EnemyEncounterPosture.Ambush, TriggerDistance = 4, AreaId = "RAVEN_CROSSING", TargetTerrainTags = TerrainTag.Bush | TerrainTag.Undergrowth | TerrainTag.DenseUndergrowth | TerrainTag.Marsh | TerrainTag.ThicketEdge },
                    Encounters.LeaderHorde(leaderId: MonsterIds.Káoszpap, followerId: MonsterIds.Martalóc, groups: Amount.Pair, followers: Amount.Band) with { AreaId = "RAVEN_CROSSING" },
                    Encounters.MixedHorde(firstEnemyId: MonsterIds.Martalóc, firstCount: Amount.Band, secondEnemyId: MonsterIds.Útonálló, secondCount: Amount.Several, groups: Amount.Pair) with { AreaId = "OLD_PINES" },
                    Encounters.Mixed(firstEnemyId: MonsterIds.Orgyilkos, firstCount: Amount.TwoThree, secondEnemyId: MonsterIds.GoblinÍjász, secondCount: Amount.Band, groups: Amount.Few) with { Posture = EnemyEncounterPosture.Ambush, TriggerDistance = 5, TargetTerrainTags = TerrainTag.Bush | TerrainTag.Undergrowth | TerrainTag.DenseUndergrowth | TerrainTag.ThicketEdge, AreaId = "OLD_PINES" },
                    Encounters.Mixed(firstEnemyId: MonsterIds.Útonálló, firstCount: Amount.Band, secondEnemyId: MonsterIds.OrkÍjász, secondCount: Amount.TwoThree, groups: Amount.Few, movement: EnemyMovementProfile.Wander) with { Behavior = EnemyEncounterBehavior.Horde, AreaId = "LOST_MANOR" },
                    Encounters.Mixed(firstEnemyId: MonsterIds.Óriáspióca, firstCount: Amount.Band, secondEnemyId: MonsterIds.MocsáriVipera, secondCount: Amount.Band, groups: Amount.TwoThree) with { Posture = EnemyEncounterPosture.Ambush, TargetTerrainTags = TerrainTag.Marsh, TriggerDistance = 3 },
                    Encounters.Mixed(firstEnemyId: MonsterIds.MérgesVarangy, firstCount: Amount.TwoThree, secondEnemyId: MonsterIds.MocsáriKrokodil, secondCount: Amount.TwoThree, groups: Amount.Few) with { TargetTerrainTags = TerrainTag.Marsh, AreaId = "BLACKWATER", TriggerDistance = 4, Posture = EnemyEncounterPosture.Ambush },
                    Encounters.LeaderGroup(leaderId: MonsterIds.Óriáskrokodil, followerId: MonsterIds.MocsáriKrokodil, groups: Amount.One, followers: Amount.Several) with { TriggerDistance = 4, TargetTerrainTags = TerrainTag.Marsh, AreaId = "BLACKWATER" },
                    Encounters.LeaderGroup(leaderId: MonsterIds.MocsáriOgre, followerId: MonsterIds.Goblin, groups: Amount.Few, followers: Amount.Several) with { TargetTerrainTags = TerrainTag.Marsh },
                    Encounters.Mixed(firstEnemyId: MonsterIds.MocsáriVipera, firstCount: Amount.TwoThree, secondEnemyId: MonsterIds.MérgesVarangy, secondCount: Amount.Handful, groups: Amount.Band) with { TargetTerrainTags = TerrainTag.Marsh },
                    Encounters.LeaderHorde(leaderId: MonsterIds.SötétDruida, followerId: MonsterIds.Kobold, groups: Amount.Few, followers: Amount.Several) with { AreaId = "THORN_MAZE" },
                    Encounters.Mixed(firstEnemyId: MonsterIds.SötétDruida, firstCount: Amount.One, secondEnemyId: MonsterIds.Orgyilkos, secondCount: Amount.TwoThree, groups: Amount.Few) with { Posture = EnemyEncounterPosture.Ambush, TriggerDistance = 3, TargetTerrainTags = TerrainTag.Bush | TerrainTag.Undergrowth | TerrainTag.DenseUndergrowth | TerrainTag.ThicketEdge, AreaId = "THORN_MAZE" },
                    Encounters.LeaderHorde(leaderId: MonsterIds.Lidércfarkas, followerId: MonsterIds.Farkas, groups: Amount.Few, followers: Amount.Band) with { AreaId = "WINDLESS_GLADE" },
                    Encounters.Horde(enemyId: MonsterIds.Óriásdenevér, groups: Amount.Pair, size: Amount.Pack)
                ],
                QuestRoomIds = ["RAVENS_LOOT_ROOM", "ORC_TRIBE_ROOM"],
                QuestRoomPlacements = new Dictionary<string, QuestRoomPlacementConfiguration>
                {
                    ["RAVENS_LOOT_ROOM"] = new(AreaId: "RAVEN_CROSSING"),
                    ["ORC_TRIBE_ROOM"] = new(AreaId: "LOST_MANOR")
                },
                SpecialRoomPlacements = new Dictionary<string, SpecialRoomPlacement>
                {
                    ["RAVENS_LOOT_ROOM"] = SpecialRoomPlacement.SideBranch,
                    ["ORC_TRIBE_ROOM"] = SpecialRoomPlacement.SideBranch
                },
                QuestChestPlacements = new Dictionary<string, Domain.Quests.QuestChestId>
                {
                    ["RAVENS_LOOT_ROOM"] = Domain.Quests.QuestChestId.RavensLootChest,
                    ["ORC_TRIBE_ROOM"] = Domain.Quests.QuestChestId.OrcTribeChest
                },
                QuestRoomEnemyEncounters =
                [
                    new("RAVENS_LOOT_ROOM", MonsterIds.HollóKlánvezér, 1, Role: EnemyGroupRole.Leader),
                    new("RAVENS_LOOT_ROOM", MonsterIds.Orgyilkos, 4),
                    new("ORC_TRIBE_ROOM", MonsterIds.OrkRaktárnok, 1, Role: EnemyGroupRole.Leader),
                    new("ORC_TRIBE_ROOM", MonsterIds.OrkTestőr, 2),
                    new("ORC_TRIBE_ROOM", MonsterIds.OrkÍjász, 2),
                    new("ORC_TRIBE_ROOM", MonsterIds.OrkVérpap, 1)
                ],
                ForestInns = [new("RAVEN_INN", "A Fáradt Holló", "RAVEN_CROSSING")]
            },
            [7] = new()
            {
                Level = 7,
                Name = "A nagy csarnokok szintje",
                GuaranteedTraps = [new("TR105"), new("TR105")],
                Layout = new WideMazeLayoutConfiguration(new IntRange(2, 2), NarrowingChance: 0.12),
                WallRune = new('▦'),
                WallColor = ConsoleColor.DarkYellow,
                RoomCount = Amount.Lots.Range(),
                RoomSize = new(7, 11),
                TreasureChestCount = Amount.Pack.Range(),
                TreasureGold = new(180, 460),
                RoomEncounters = [
                    Encounters.Same(MonsterIds.Ork, Amount.Several, Amount.Several),
                    Encounters.Mixed(firstEnemyId: MonsterIds.Hobgoblin, firstCount: Amount.Several, secondEnemyId: MonsterIds.Káoszpap, secondCount: Amount.Few, groups: Amount.TwoThree),
                    Encounters.LeaderGroup(MonsterIds.Ogre, MonsterIds.Ork, Amount.One, Amount.Pack),
                    Encounters.LeaderGroup(MonsterIds.OrkSámán, MonsterIds.Ork, Amount.One, Amount.Several),
                    Encounters.LeaderGroup(leaderId: MonsterIds.Ogre, followerId: MonsterIds.Hobgoblin, groups: Amount.One, followers: Amount.Several),
                    Encounters.Mixed(firstEnemyId: MonsterIds.Óriáspók, firstCount: Amount.TwoThree, secondEnemyId: MonsterIds.PestishordozóPatkány, secondCount: Amount.Several, groups: Amount.Few)
                ],
                CorridorEncounters = [
                    Encounters.MixedHorde(firstEnemyId: MonsterIds.Gnoll, firstCount: Amount.Band, secondEnemyId: MonsterIds.Ork, secondCount: Amount.Band, groups: Amount.TwoThree),
                    Encounters.MixedHorde(firstEnemyId: MonsterIds.Hobgoblin, firstCount: Amount.Band, secondEnemyId: MonsterIds.GoblinÍjász, secondCount: Amount.Band, groups: Amount.Handful),
                    Encounters.LeaderHorde(leaderId: MonsterIds.Ogre, followerId: MonsterIds.Hobgoblin, groups: Amount.Few, followers: Amount.Several),
                    Encounters.MixedHorde(firstEnemyId: MonsterIds.KáoszmágusTanítvány, firstCount: Amount.TwoThree, secondEnemyId: MonsterIds.Gnoll, secondCount: Amount.Band, groups: Amount.TwoThree),
                    Encounters.Same(enemyId: MonsterIds.Hobgoblin, groups: Amount.Several, size: Amount.Pair, movement: EnemyMovementProfile.Patrol),
                    Encounters.Same(enemyId: MonsterIds.Óriáspatkány, groups: Amount.Handful, size: Amount.Few, movement: EnemyMovementProfile.Wander)
                ],
            },
            [8] = new()
            {
                Level = 8,
                Name = "A mérgező barlang",
                DoubleWidthCorridorChance = 0.88,
                WallRune = new('▒'),
                WallColor = ConsoleColor.DarkCyan,
                RoomCount = new(14, 18),
                RoomSize = new(6, 9),
                TreasureChestCount = new(8, 11),
                TreasureGold = new(280, 680),
                ItemCurseChancePercent = 12,
                // Fertőzött fészkek: sok apró lény, kevés nagy ragadozó és korai mágusok.
                RoomEncounters =
                [
                    new(GroupCount: new(3, 4),
                        Members: [new(MonsterIds.Óriáspók, new(3, 5)), new(MonsterIds.PestishordozóPatkány, new(5, 8))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Savanyálka, new(3, 5)), new(MonsterIds.BarlangiGyík, new(5, 8))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.ÓriásBaziliszkusz, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Óriáspók, new(3, 5)), new(MonsterIds.BarlangiGyík, new(4, 6)), new(MonsterIds.BarlangiTroll, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 2),
                        Members: [new(MonsterIds.GoblinVajákos, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Goblin, new(4, 6)), new(MonsterIds.PestishordozóPatkány, new(5, 8))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 2),
                        Members: [new(MonsterIds.KáoszmágusTanítvány, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Hobgoblin, new(3, 4)), new(MonsterIds.Óriáspók, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.Boszorkány, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Óriásdenevér, new(6, 9)), new(MonsterIds.BarlangiGyík, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Stationary)
                ],
                CorridorEncounters =
                [
                    new(GroupCount: new(3, 4),
                        Members: [new(MonsterIds.Óriásdenevér, new(8, 12))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.BarlangiGyík, new(5, 8)), new(MonsterIds.PestishordozóPatkány, new(6, 10))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Goblin, new(5, 8)), new(MonsterIds.GoblinÍjász, new(2, 3)), new(MonsterIds.GoblinVajákos, new(1, 1), EnemyGroupRole.Leader)],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(1, 2),
                        Members: [new(MonsterIds.Óriásskorpió, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Patrol),
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.ÓriásBaziliszkusz, new(1, 1)), new(MonsterIds.BarlangiGyík, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Patrol)
                ]
            },
            [9] = new()
            {
                Level = 9,
                Name = "Az ork haditábor",
                Layout = new WideMazeLayoutConfiguration(new(2, 2), NarrowingChance: 0.14),
                WallRune = new('▦'),
                WallColor = ConsoleColor.DarkRed,
                RoomCount = new(24, 30),
                RoomSize = new(7, 10),
                TreasureChestCount = new(14, 20),
                TreasureGold = new(500, 1050),
                ItemCurseChancePercent = 8,
                // Hatfős parti: hadrendek és nagy menetoszlopok; a törzsfő a második képernyőn.
                RoomEncounters =
                [
                    new(GroupCount: new(3, 4),
                        Members: [new(MonsterIds.OrkSámán, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Ork, new(10, 14)), new(MonsterIds.OrkÍjász, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Hobgoblin, new(3, 4), EnemyGroupRole.Leader), new(MonsterIds.Goblin, new(8, 12)), new(MonsterIds.GoblinÍjász, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.OrkVérpap, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.OrkTestőr, new(3, 5)), new(MonsterIds.Ork, new(8, 12))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Ogre, new(1, 2), EnemyGroupRole.Leader), new(MonsterIds.Ork, new(8, 12)), new(MonsterIds.OrkÍjász, new(2, 4))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.OrkTörzsfő, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.OrkTestőr, new(4, 6)), new(MonsterIds.OrkVérpap, new(1, 1)), new(MonsterIds.OrkSámán, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Stationary, ScreenNumber: 2)
                ],
                CorridorEncounters =
                [
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Ork, new(8, 12)), new(MonsterIds.Goblin, new(8, 12)), new(MonsterIds.OrkÍjász, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.OrkSámán, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.OrkVérpap, new(1, 1)), new(MonsterIds.Ork, new(12, 18)), new(MonsterIds.OrkÍjász, new(4, 6))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(1, 2),
                        Members: [new(MonsterIds.Ogre, new(1, 2), EnemyGroupRole.Leader), new(MonsterIds.Ork, new(12, 18)), new(MonsterIds.Goblin, new(12, 18))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Hobgoblin, new(4, 6)), new(MonsterIds.Gnoll, new(4, 6)), new(MonsterIds.Ork, new(6, 10))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde)
                ]
            },
            [10] = new()
            {
                Level = 10,
                Name = "Az elátkozott sírkamrák",
                GuaranteedTraps = [new("TR102"), new("TR102")],
                DoubleWidthCorridorChance = 0.92,
                WallRune = new('▦'),
                WallColor = ConsoleColor.DarkMagenta,
                RoomCount = new(18, 22),
                RoomSize = new(6, 9),
                TreasureChestCount = new(10, 14),
                TreasureGold = new(600, 1150),
                ItemCurseChancePercent = 28,
                // A korai élőholtak tömegei mögött nekromanták, múmiák és ritka vén múmia.
                RoomEncounters =
                [
                    new(GroupCount: new(3, 4),
                        Members: [new(MonsterIds.Nekromanta, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Csontváz, new(6, 10)), new(MonsterIds.CsontvázÍjász, new(2, 4)), new(MonsterIds.Zombi, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Múmia, new(2, 3)), new(MonsterIds.PáncélozottZombi, new(3, 5)), new(MonsterIds.Wight, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Boszorkány, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.CsontvázLovag, new(2, 3)), new(MonsterIds.CsontvázÍjász, new(2, 4))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Ghoul, new(2, 3)), new(MonsterIds.Zombi, new(6, 9)), new(MonsterIds.PestishordozóPatkány, new(5, 8))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.VénMúmia, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Múmia, new(2, 3)), new(MonsterIds.CsontvázŐr, new(4, 6))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 2),
                        Members: [new(MonsterIds.Káoszpap, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.PáncélozottZombi, new(4, 6)), new(MonsterIds.CsontvázÍjász, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Stationary)
                ],
                CorridorEncounters =
                [
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Csontváz, new(12, 18)), new(MonsterIds.Zombi, new(8, 12))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Ghoul, new(2, 3)), new(MonsterIds.PestishordozóPatkány, new(8, 12))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Nekromanta, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Csontváz, new(8, 12)), new(MonsterIds.CsontvázÍjász, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Wight, new(1, 1)), new(MonsterIds.CsontvázLovag, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Patrol),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Óriásdenevér, new(6, 9))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde)
                ]
            },
            [11] = new()
            {
                Level = 11,
                Name = "Az óriások erődje",
                GuaranteedTraps = [new("TR108"), new("TR108")],
                Layout = new WideMazeLayoutConfiguration(new(2, 2), NarrowingChance: 0.10),
                WallRune = new('▩'),
                WallColor = ConsoleColor.Gray,
                RoomCount = new(22, 28),
                RoomSize = new(8, 11),
                TreasureChestCount = new(12, 17),
                TreasureGold = new(700, 1400),
                ItemCurseChancePercent = 8,
                // Ork segédcsapatok és ogre rajok után ritka nagy óriások; nem minden őr elit.
                RoomEncounters =
                [
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Ogre, new(1, 2), EnemyGroupRole.Leader), new(MonsterIds.Ork, new(8, 12)), new(MonsterIds.OrkTestőr, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.OrkVérpap, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Hobgoblin, new(6, 9)), new(MonsterIds.OrkÍjász, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Ettin, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Troll, new(1, 1)), new(MonsterIds.Gnoll, new(4, 6))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.Fagyóriás, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Ogre, new(4, 6))],
                        MovementProfile: EnemyMovementProfile.Stationary, ScreenNumber: 2),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.Küklopsz, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Goblin, new(6, 9)), new(MonsterIds.GoblinÍjász, new(3, 4))],
                        MovementProfile: EnemyMovementProfile.Stationary, ScreenNumber: 2),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.OrkSámán, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.OrkTestőr, new(4, 6)), new(MonsterIds.OrkÍjász, new(3, 4))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 2),
                        Members: [new(MonsterIds.GoblinVajákos, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Goblin, new(8, 12)), new(MonsterIds.Ogre, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Stationary)
                ],
                CorridorEncounters =
                [
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Ogre, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Ork, new(12, 18))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Ettin, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Goblin, new(10, 15)), new(MonsterIds.GoblinÍjász, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Ork, new(8, 12)), new(MonsterIds.OrkÍjász, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.OrkVérpap, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Gnoll, new(5, 8)), new(MonsterIds.Martalóc, new(5, 8))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Troll, new(1, 1)), new(MonsterIds.OrkTestőr, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Patrol)
                ]
            },
            [12] = new()
            {
                Level = 12,
                Name = "A sárkánykultusz szentélye",
                GuaranteedTraps = [new("TR106"), new("TR106")],
                Layout = new WideMazeLayoutConfiguration(new(2, 2), NarrowingChance: 0.16),
                WallRune = new('▥'),
                WallColor = ConsoleColor.Red,
                RoomCount = new(22, 28),
                RoomSize = new(7, 10),
                TreasureChestCount = new(14, 19),
                TreasureGold = new(900, 1800),
                ItemCurseChancePercent = 12,
                // A külső kultistákból fokozatos átmenet a szentély mágusai és szárnyas őrei felé.
                RoomEncounters =
                [
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Káoszpap, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Martalóc, new(6, 9)), new(MonsterIds.KáoszmágusTanítvány, new(1, 2))],
                        MovementProfile: EnemyMovementProfile.Stationary, ScreenNumber: 1),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.OrkVérpap, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Ork, new(6, 9)), new(MonsterIds.OrkÍjász, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Stationary, ScreenNumber: 1),
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Wyvern, new(1, 2)), new(MonsterIds.Hárpia, new(4, 6))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.VörösSárkány, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Orgyilkos, new(6, 9)), new(MonsterIds.Káoszpap, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Stationary, ScreenNumber: 2),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.Káoszmágus, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Martalóc, new(4, 6)), new(MonsterIds.Gargoyle, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Stationary, ScreenNumber: 2),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Kiméra, new(1, 2)), new(MonsterIds.OrkTestőr, new(2, 3)), new(MonsterIds.KáoszmágusTanítvány, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.Káoszpap, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Pokolfajzat, new(6, 9))],
                        MovementProfile: EnemyMovementProfile.Stationary, ScreenNumber: 2)
                ],
                CorridorEncounters =
                [
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Orgyilkos, new(3, 5)), new(MonsterIds.Martalóc, new(6, 9)), new(MonsterIds.KáoszmágusTanítvány, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Hárpia, new(4, 6)), new(MonsterIds.Óriásdenevér, new(8, 12))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.OrkSámán, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Ork, new(8, 12)), new(MonsterIds.OrkÍjász, new(3, 4))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Káoszpap, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Orgyilkos, new(4, 6)), new(MonsterIds.OrkTestőr, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Wyvern, new(1, 1)), new(MonsterIds.Hárpia, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Patrol)
                ]
            },
            [13] = new()
            {
                Level = 13,
                Name = "Süllyedt koronák lápvidéke",
                ForestGraphJsonOverrideEnabled = true,
                Layout = new ForestMazeLayoutConfiguration(
                    new DungeonAreaGraphConfiguration(new(12, 12), MinimumExitDistance: 5, MaximumDegree: 4),
                    new ForestGenerationConfiguration
                    {
                        ForestDensity = 0.48,
                        GroveSize = new(5, 12),
                        BiomeSize = 18,
                        PineChance = 0.03,
                        BushChance = 0.26,
                        FlowerBushChance = 0.02,
                        BushGroupSize = new(3, 7),
                        ForestEdgeWidth = 3,
                        ThicketChance = 0.08,
                        UndergrowthChance = 0.34,
                        DenseUndergrowthChance = 0.18,
                        LakeCount = new(3, 5),
                        LakeRadius = new(3, 7),
                        MarshChance = 1,
                        MarshCount = new(4, 7),
                        MarshRadius = new(4, 8),
                        TrailWidth = 2,
                        TrailWinding = 0.84,
                        ExtraTrailChance = 0.32,
                        BuildingCount = new(1, 2),
                        BuildingSize = new(4, 7),
                        BuildingPartitionChance = 0.60,
                        ManorBuildingChance = 0.55,
                        LabyrinthBuildingChance = 0.10,
                        ManorBuildingWidth = new(14, 22),
                        ManorBuildingHeight = new(10, 16),
                        ManorRoomCount = new(4, 6),
                        LabyrinthBuildingWidth = new(16, 24),
                        LabyrinthBuildingHeight = new(10, 17),
                        BuildingExtraConnectionChance = 0.24,
                        BuildingSecondEntranceChance = 0.35,
                        LockedBuildingDoorChance = 0.35,
                        OpenBuildingDoorChance = 0.30,
                        BuildingStyles =
                        [
                            new("sunken-timber", new("sunken-timber-wall", new('▓'),
                                ConsoleColor.DarkYellow, ConsoleColor.Black, false, true), 3),
                            new("sunken-stone", new("sunken-stone-wall", new('▣'),
                                ConsoleColor.DarkGray, ConsoleColor.Black, false, true), 4),
                            new("sunken-court", new("sunken-court-wall", new('▤'),
                                ConsoleColor.Gray, ConsoleColor.Black, false, true), 3,
                                new HashSet<ForestBuildingLayout> { ForestBuildingLayout.Manor })
                        ],
                        Palette = new ForestTerrainPalette
                        {
                            Tree = new("sunken-tree", new('♠'), ConsoleColor.DarkGreen, ConsoleColor.Black, false, true),
                            Bush = new("sunken-reeds", new('♣'), ConsoleColor.DarkYellow, ConsoleColor.Black, true, false),
                            Pine = new("sunken-pine", new('▲'), ConsoleColor.DarkGreen, ConsoleColor.Black, false, true),
                            FlowerBush = new("sunken-flowers", new('✿'), ConsoleColor.DarkMagenta, ConsoleColor.Black, true, false),
                            Thicket = new("sunken-thicket", new('#'), ConsoleColor.DarkGreen, ConsoleColor.Black, false, true),
                            Undergrowth = new("sunken-growth", new('░'), ConsoleColor.DarkYellow, ConsoleColor.Black, true, false),
                            DenseUndergrowth = new("sunken-dense-growth", new('▒'), ConsoleColor.DarkGreen, ConsoleColor.Black, true, false),
                            Water = new("sunken-water", new('≈'), ConsoleColor.DarkBlue, ConsoleColor.Black, false, false),
                            Marsh = new("sunken-marsh", new('≋'), ConsoleColor.DarkYellow, ConsoleColor.DarkBlue, true, false),
                            BuildingWall = new("sunken-wall", new('█'), ConsoleColor.DarkGray, ConsoleColor.Black, false, true)
                        }
                    },
                    ExplicitGraph: SunkenCrownsForest.CreateGraph()),
                WallRune = new('♠'),
                WallColor = ConsoleColor.DarkGreen,
                RoomCount = new(96, 120),
                RoomSize = new(5, 9),
                TreasureChestCount = new(28, 36),
                TreasureGold = new(650, 1400),
                ItemCurseChancePercent = 18,
                QuestRoomIds = ["SLUICE_SUPPLY_ROOM", "SUNKEN_COURT_SUPPLY_ROOM"],
                QuestRoomPlacements = new Dictionary<string, QuestRoomPlacementConfiguration>
                {
                    ["SLUICE_SUPPLY_ROOM"] = new(AreaId: "OLD_SLUICE"),
                    ["SUNKEN_COURT_SUPPLY_ROOM"] = new(AreaId: "SUNKEN_COURT")
                },
                SpecialRoomPlacements = new Dictionary<string, SpecialRoomPlacement>
                {
                    ["SLUICE_SUPPLY_ROOM"] = SpecialRoomPlacement.SideBranch,
                    ["SUNKEN_COURT_SUPPLY_ROOM"] = SpecialRoomPlacement.SideBranch
                },
                QuestChestPlacements = new Dictionary<string, Domain.Quests.QuestChestId>
                {
                    ["SLUICE_SUPPLY_ROOM"] = Domain.Quests.QuestChestId.SluiceSupplies,
                    ["SUNKEN_COURT_SUPPLY_ROOM"] = Domain.Quests.QuestChestId.SunkenCourtSupplies
                },
                QuestRoomEnemyEncounters =
                [
                    new("SLUICE_SUPPLY_ROOM", MonsterIds.ZsilipŐrkapitány, 1, Role: EnemyGroupRole.Leader),
                    new("SLUICE_SUPPLY_ROOM", MonsterIds.CsontvázLovag, 3),
                    new("SLUICE_SUPPLY_ROOM", MonsterIds.PáncélozottZombi, 3),
                    new("SUNKEN_COURT_SUPPLY_ROOM", MonsterIds.LápiUdvarmester, 1, Role: EnemyGroupRole.Leader),
                    new("SUNKEN_COURT_SUPPLY_ROOM", MonsterIds.Martalóc, 4),
                    new("SUNKEN_COURT_SUPPLY_ROOM", MonsterIds.KáoszmágusTanítvány, 2)
                ],
                ForestInns =
                [
                    new("FERRY_INN", "A Száraz Kulacs", "FERRY_ISLAND"),
                    new("COURT_INN", "A Rozsdás Korona", "SUNKEN_COURT")
                ],
                // A nyílt lápot tömegek uralják; az udvarházakban mágusokkal támogatott őrségek várnak.
                RoomEncounters =
                [
                    new(GroupCount: new(6, 8),
                        Members: [new(MonsterIds.MérgesVarangy, new(5, 8)), new(MonsterIds.MocsáriVipera, new(4, 6))],
                        MovementProfile: EnemyMovementProfile.Stationary, TargetRoomKind: RoomKind.Clearing),
                    new(GroupCount: new(4, 6),
                        Members: [new(MonsterIds.Óriáspióca, new(8, 12)), new(MonsterIds.Savanyálka, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Stationary, TargetRoomKind: RoomKind.Clearing),
                    new(GroupCount: new(4, 5),
                        Members: [new(MonsterIds.MocsáriOgre, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Goblin, new(6, 9)), new(MonsterIds.GoblinVajákos, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(3, 4),
                        Members: [new(MonsterIds.LápiLidérc, new(2, 3)), new(MonsterIds.Zombi, new(5, 8))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.SötétDruida, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Útonálló, new(4, 6))],
                        MovementProfile: EnemyMovementProfile.Stationary, AreaId: "FERRY_ISLAND"),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.SötétDruida, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.MérgesVarangy, new(5, 7)), new(MonsterIds.LápiLidérc, new(1, 2))],
                        MovementProfile: EnemyMovementProfile.Stationary, AreaId: "WITCH_GROVE"),
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Káoszpap, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Martalóc, new(5, 7)), new(MonsterIds.KáoszmágusTanítvány, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Stationary, AreaId: "SUNKEN_COURT", TargetRoomKind: RoomKind.Manor),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Nekromanta, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.PáncélozottZombi, new(4, 6)), new(MonsterIds.CsontvázLovag, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Stationary, AreaId: "OLD_SLUICE", TargetRoomKind: RoomKind.Manor),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.Kígyópap, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Kígyóember, new(1, 2)), new(MonsterIds.GyíkemberPortyázó, new(4, 6)), new(MonsterIds.PajzsosGyíkőr, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Stationary, AreaId: "DROWNED_THRONE", TargetRoomKind: RoomKind.Manor),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.Káoszpap, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Orgyilkos, new(3, 5)), new(MonsterIds.Martalóc, new(5, 7))],
                        MovementProfile: EnemyMovementProfile.Stationary, AreaId: "DROWNED_THRONE", TargetRoomKind: RoomKind.Manor),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.Krokodilidomár, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Óriáskígyó, new(1, 2)), new(MonsterIds.MocsáriKrokodil, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Stationary, AreaId: "CROCODILE_LAKES", TargetRoomKind: RoomKind.Clearing),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.Óriáskrokodil, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.MocsáriKrokodil, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Stationary, AreaId: "CROCODILE_LAKES", TargetRoomKind: RoomKind.Clearing)
                ],
                CorridorEncounters =
                [
                    new(GroupCount: new(10, 14),
                        Members: [new(MonsterIds.Óriáspióca, new(10, 14)), new(MonsterIds.MocsáriVipera, new(6, 9))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde, TargetTerrainTags: TerrainTag.Marsh),
                    new(GroupCount: new(8, 10),
                        Members: [new(MonsterIds.MérgesVarangy, new(5, 8)), new(MonsterIds.MocsáriVipera, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Wander, TargetTerrainTags: TerrainTag.Marsh, Posture: EnemyEncounterPosture.Ambush, TriggerDistance: 4),
                    new(GroupCount: new(4, 6),
                        Members: [new(MonsterIds.MocsáriKrokodil, new(2, 3)), new(MonsterIds.BarlangiGyík, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Patrol),
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.ÉjiBanya, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Savanyálka, new(4, 6)), new(MonsterIds.Óriáspók, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.LápiLidérc, new(2, 3)), new(MonsterIds.Zombi, new(4, 6))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde, AreaId: "BLACK_MIRROR"),
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.MocsáriOgre, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Goblin, new(8, 12))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde, AreaId: "LEECH_MIRE"),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.GyíkemberPortyázó, new(4, 6)), new(MonsterIds.GyíkemberVadász, new(1, 2)), new(MonsterIds.MocsáriVipera, new(4, 6))],
                        MovementProfile: EnemyMovementProfile.Patrol, AreaId: "REED_LABYRINTH", TargetTerrainTags: TerrainTag.Marsh, Posture: EnemyEncounterPosture.Ambush, TriggerDistance: 4),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Orgyilkos, new(3, 5)), new(MonsterIds.Martalóc, new(5, 8)), new(MonsterIds.KáoszmágusTanítvány, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Patrol, AreaId: "CROWN_CAUSEWAY"),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.SötétDruida, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Óriáspók, new(4, 6)), new(MonsterIds.MérgesVarangy, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde, AreaId: "DROWNED_WOOD")
                ]
            },
            [14] = new()
            {
                Level = 14,
                Name = "A rothadó mocsár",
                GuaranteedTraps = [new("TR109"), new("TR109")],
                Layout = new WideMazeLayoutConfiguration(new(2, 2), NarrowingChance: 0.18),
                WallRune = new('▒'),
                WallColor = ConsoleColor.DarkGreen,
                RoomCount = new(22, 28),
                RoomSize = new(7, 10),
                TreasureChestCount = new(10, 15),
                TreasureGold = new(900, 1900),
                ItemCurseChancePercent = 16,
                // Fertőzött tömegek és mocsári állatok; a hüllők itt csak a későbbi birodalom előőrsei.
                RoomEncounters =
                [
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.GyíkemberSámán, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.PajzsosGyíkőr, new(2, 3)), new(MonsterIds.GyíkemberPortyázó, new(4, 6))],
                        MovementProfile: EnemyMovementProfile.Stationary, ScreenNumber: 1),
                    new(GroupCount: new(1, 2),
                        Members: [new(MonsterIds.Óriáskígyó, new(2, 3)), new(MonsterIds.MocsáriVipera, new(5, 8))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.PestishordozóPatkány, new(12, 18)), new(MonsterIds.Óriáspók, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Savanyálka, new(4, 6)), new(MonsterIds.MérgesVarangy, new(5, 8))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.Hidra, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.BarlangiGyík, new(4, 6)), new(MonsterIds.MocsáriVipera, new(6, 10))],
                        MovementProfile: EnemyMovementProfile.Stationary, ScreenNumber: 2),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.Kígyópap, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Kígyóember, new(1, 2)), new(MonsterIds.Kígyóíjász, new(1, 2)), new(MonsterIds.GyíkemberPortyázó, new(5, 8))],
                        MovementProfile: EnemyMovementProfile.Stationary, ScreenNumber: 2),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.MocsáriOgre, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Goblin, new(8, 12)), new(MonsterIds.GoblinVajákos, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 2),
                        Members: [new(MonsterIds.SötétDruida, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Óriáspók, new(3, 5)), new(MonsterIds.MocsáriKrokodil, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.Óriáskrokodil, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.MocsáriKrokodil, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 2),
                        Members: [new(MonsterIds.LápiLidérc, new(2, 3)), new(MonsterIds.Zombi, new(6, 9))],
                        MovementProfile: EnemyMovementProfile.Stationary)
                ],
                CorridorEncounters =
                [
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.BarlangiGyík, new(6, 9)), new(MonsterIds.PestishordozóPatkány, new(10, 16))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Óriáspióca, new(10, 16)), new(MonsterIds.MocsáriVipera, new(8, 12))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Krokodilidomár, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.MocsáriKrokodil, new(2, 3)), new(MonsterIds.GyíkemberPortyázó, new(4, 6)), new(MonsterIds.GyíkemberVadász, new(1, 2))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.ÉjiBanya, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Savanyálka, new(5, 8)), new(MonsterIds.PestishordozóPatkány, new(6, 9))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.SötétDruida, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.MérgesVarangy, new(4, 6)), new(MonsterIds.Óriáspók, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Óriásskorpió, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Patrol)
                ]
            },
            [15] = new()
            {
                Level = 15,
                Name = "A pikkelytrón elsüllyedt palotája",
                Layout = new WideMazeLayoutConfiguration(new(3, 4), NarrowingChance: 0.08),
                WallRune = new('▓'),
                WallColor = ConsoleColor.DarkCyan,
                RoomCount = new(36, 44),
                RoomSize = new(8, 12),
                TreasureChestCount = new(16, 22),
                TreasureGold = new(1000, 2000),
                ItemCurseChancePercent = 14,
                QuestRoomIds = ["SCALED_KING_THRONE", "SCALED_CROCODILE_POOL"],
                QuestRoomPlacements = new Dictionary<string, QuestRoomPlacementConfiguration>
                {
                    ["SCALED_CROCODILE_POOL"] = new(ScreenNumber: 2)
                },
                SpecialRoomPlacements = new Dictionary<string, SpecialRoomPlacement>
                {
                    ["SCALED_KING_THRONE"] = SpecialRoomPlacement.MiddleRoute,
                    ["SCALED_CROCODILE_POOL"] = SpecialRoomPlacement.SideBranch
                },
                QuestRoomEnemyEncounters =
                [
                    new("SCALED_KING_THRONE", MonsterIds.GyíkemberKirály, 1, Role: EnemyGroupRole.Leader),
                    new("SCALED_KING_THRONE", MonsterIds.PajzsosGyíkőr, 6),
                    new("SCALED_KING_THRONE", MonsterIds.GyíkemberSámán, 2),
                    new("SCALED_KING_THRONE", MonsterIds.GyíkemberVadász, 3),
                    new("SCALED_CROCODILE_POOL", MonsterIds.Krokodilidomár, 2, Role: EnemyGroupRole.Leader),
                    new("SCALED_CROCODILE_POOL", MonsterIds.MocsáriKrokodil, 6),
                    new("SCALED_CROCODILE_POOL", MonsterIds.Óriáskrokodil, 1)
                ],
                RoomEncounters =
                [
                    new(GroupCount: new(3, 4), Members: [new(MonsterIds.PajzsosGyíkőr, new(3, 4)), new(MonsterIds.GyíkemberPortyázó, new(8, 12))],
                        MovementProfile: EnemyMovementProfile.Stationary, ScreenNumber: 1),
                    new(GroupCount: new(2, 3), Members: [new(MonsterIds.GyíkemberSámán, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.PajzsosGyíkőr, new(4, 6)),
                        new(MonsterIds.GyíkemberVadász, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 3), Members: [new(MonsterIds.Krokodilidomár, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.MocsáriKrokodil, new(3, 4)),
                        new(MonsterIds.GyíkemberVadász, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Stationary, ScreenNumber: 2),
                    new(GroupCount: new(3, 4), Members: [new(MonsterIds.GyíkemberVadász, new(4, 6)), new(MonsterIds.GyíkemberPortyázó, new(6, 9))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 2), Members: [new(MonsterIds.GyíkemberSámán, new(2, 2), EnemyGroupRole.Leader), new(MonsterIds.PajzsosGyíkőr, new(5, 7))],
                        MovementProfile: EnemyMovementProfile.Stationary, ScreenNumber: 3),
                    new(GroupCount: new(1, 1), Members: [new(MonsterIds.Kígyópap, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Kígyóember, new(2, 3)),
                        new(MonsterIds.Kígyóíjász, new(2, 2))],
                        MovementProfile: EnemyMovementProfile.Stationary, ScreenNumber: 3),
                    new(GroupCount: new(2, 2), Members: [new(MonsterIds.Óriáskígyó, new(2, 3)), new(MonsterIds.BarlangiGyík, new(4, 6))],
                        MovementProfile: EnemyMovementProfile.Stationary)
                ],
                CorridorEncounters =
                [
                    new(GroupCount: new(4, 5), Members: [new(MonsterIds.GyíkemberPortyázó, new(8, 12)), new(MonsterIds.GyíkemberVadász, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Patrol, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 3), Members: [new(MonsterIds.PajzsosGyíkőr, new(2, 3)), new(MonsterIds.GyíkemberVadász, new(3, 4))],
                        MovementProfile: EnemyMovementProfile.Patrol),
                    new(GroupCount: new(2, 2), Members: [new(MonsterIds.GyíkemberSámán, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.GyíkemberPortyázó, new(6, 8))],
                        MovementProfile: EnemyMovementProfile.Patrol, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 2), Members: [new(MonsterIds.Óriáskígyó, new(2, 3)), new(MonsterIds.MocsáriVipera, new(5, 8))],
                        MovementProfile: EnemyMovementProfile.Patrol, Behavior: EnemyEncounterBehavior.Horde)
                ]
            },
            [16] = new()
            {
                Level = 16,
                Name = "A vedlő isten temploma",
                Layout = new WideMazeLayoutConfiguration(new(4, 5), NarrowingChance: 0.16),
                WallRune = new('▒'),
                WallColor = ConsoleColor.DarkYellow,
                RoomCount = new(42, 52),
                RoomSize = new(8, 12),
                TreasureChestCount = new(18, 24),
                TreasureGold = new(1100, 2200),
                ItemCurseChancePercent = 20,
                GuaranteedTraps = [new("TR109", 2), new("TR109", 3)],
                QuestRoomIds = ["SHEDDING_HIGH_ALTAR", "SHEDDING_HYDRA_SANCTUM"],
                QuestRoomPlacements = new Dictionary<string, QuestRoomPlacementConfiguration>
                {
                    ["SHEDDING_HYDRA_SANCTUM"] = new(ScreenNumber: 4)
                },
                SpecialRoomPlacements = new Dictionary<string, SpecialRoomPlacement>
                {
                    ["SHEDDING_HIGH_ALTAR"] = SpecialRoomPlacement.MiddleRoute,
                    ["SHEDDING_HYDRA_SANCTUM"] = SpecialRoomPlacement.SideBranch
                },
                QuestRoomEnemyEncounters =
                [
                    new("SHEDDING_HIGH_ALTAR", MonsterIds.KígyóFőpap, 1, Role: EnemyGroupRole.Leader),
                    new("SHEDDING_HIGH_ALTAR", MonsterIds.KígyóTemplomőr, 5),
                    new("SHEDDING_HIGH_ALTAR", MonsterIds.Kígyópap, 2),
                    new("SHEDDING_HIGH_ALTAR", MonsterIds.Kígyóíjász, 3),
                    new("SHEDDING_HYDRA_SANCTUM", MonsterIds.ŐsiHidra, 1, Role: EnemyGroupRole.Leader),
                    new("SHEDDING_HYDRA_SANCTUM", MonsterIds.Óriáskígyó, 3)
                ],
                RoomEncounters =
                [
                    new(GroupCount: new(3, 4), Members: [new(MonsterIds.KígyóTemplomőr, new(3, 5)), new(MonsterIds.Kígyóember, new(5, 7))],
                        MovementProfile: EnemyMovementProfile.Stationary, ScreenNumber: 1),
                    new(GroupCount: new(3, 4), Members: [new(MonsterIds.Kígyóíjász, new(4, 6)), new(MonsterIds.Kígyóember, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(3, 3), Members: [new(MonsterIds.Kígyópap, new(1, 2), EnemyGroupRole.Leader), new(MonsterIds.KígyóTemplomőr, new(3, 4)),
                        new(MonsterIds.Kígyóíjász, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 3), Members: [new(MonsterIds.Méregmágus, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Kígyóíjász, new(3, 4)),
                        new(MonsterIds.KígyóTemplomőr, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Stationary, ScreenNumber: 3),
                    new(GroupCount: new(2, 2), Members: [new(MonsterIds.Kígyópap, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.GyíkemberPortyázó, new(4, 6)),
                        new(MonsterIds.PajzsosGyíkőr, new(2, 2))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 3), Members: [new(MonsterIds.Óriáskígyó, new(3, 4)), new(MonsterIds.MocsáriVipera, new(8, 12))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 1), Members: [new(MonsterIds.Medúza, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.KígyóTemplomőr, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Stationary, ScreenNumber: 4),
                    new(GroupCount: new(1, 1), Members: [new(MonsterIds.ÓriásBaziliszkusz, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Óriáskígyó, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Stationary, ScreenNumber: 4)
                ],
                CorridorEncounters =
                [
                    new(GroupCount: new(3, 4), Members: [new(MonsterIds.Kígyóember, new(5, 7)), new(MonsterIds.Kígyóíjász, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Patrol, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 3), Members: [new(MonsterIds.KígyóTemplomőr, new(3, 4)), new(MonsterIds.Kígyópap, new(1, 1), EnemyGroupRole.Leader)],
                        MovementProfile: EnemyMovementProfile.Patrol),
                    new(GroupCount: new(2, 2), Members: [new(MonsterIds.Méregmágus, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Óriáspók, new(4, 6)),
                        new(MonsterIds.MocsáriVipera, new(6, 9))],
                        MovementProfile: EnemyMovementProfile.Patrol, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(3, 4), Members: [new(MonsterIds.MocsáriVipera, new(10, 14)), new(MonsterIds.Óriáskígyó, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Patrol, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 2), Members: [new(MonsterIds.GyíkemberVadász, new(2, 3)), new(MonsterIds.GyíkemberPortyázó, new(3, 4))],
                        MovementProfile: EnemyMovementProfile.Patrol)
                ]
            },
            [17] = new()
            {
                Level = 17,
                Name = "A fojtogató mélyjárat",
                GuaranteedTraps = [new("TR108"), new("TR108")],
                DoubleWidthCorridorChance = 0,
                WallRune = new('█'),
                WallColor = ConsoleColor.DarkGray,
                RoomCount = new(18, 22),
                RoomSize = new(4, 6),
                TreasureChestCount = new(6, 9),
                TreasureGold = new(1300, 2500),
                ItemCurseChancePercent = 12,
                // Szűk járatok: rövid, erős őrrajok és kisebb varázshasználó kíséretek.
                RoomEncounters =
                [
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Minotaurusz, new(1, 1)), new(MonsterIds.BarlangiGyík, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.BarlangiTroll, new(1, 1)), new(MonsterIds.Goblin, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.KáoszmágusTanítvány, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Orgyilkos, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Kőgólem, new(1, 1)), new(MonsterIds.Gargoyle, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.ŐsiMinotaurusz, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Minotaurusz, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Boszorkány, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Óriáspók, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.Beholder, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Óriásdenevér, new(2, 4))],
                        MovementProfile: EnemyMovementProfile.Stationary)
                ],
                CorridorEncounters =
                [
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Minotaurusz, new(1, 2))],
                        MovementProfile: EnemyMovementProfile.Patrol),
                    new(GroupCount: new(1, 2),
                        Members: [new(MonsterIds.BarlangiTroll, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Patrol),
                    new(GroupCount: new(3, 3),
                        Members: [new(MonsterIds.Óriásdenevér, new(6, 10)), new(MonsterIds.PestishordozóPatkány, new(5, 8))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.KáoszmágusTanítvány, new(1, 1)), new(MonsterIds.Orgyilkos, new(1, 1)), new(MonsterIds.Martalóc, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Patrol),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.BarlangiGyík, new(4, 6)), new(MonsterIds.Óriáskígyó, new(1, 2)), new(MonsterIds.Óriáspók, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde)
                ]
            },
            [18] = new()
            {
                Level = 18,
                Name = "A megtört kristálycsarnok",
                GuaranteedTraps = [new("TR110"), new("TR110")],
                DoubleWidthCorridorChance = 0.72,
                WallRune = new('◆'),
                WallColor = ConsoleColor.Cyan,
                RoomCount = new(18, 22),
                RoomSize = new(7, 10),
                TreasureChestCount = new(12, 16),
                TreasureGold = new(1400, 2800),
                ItemCurseChancePercent = 10,
                // Mágikus őrség, kőlények és egy-egy nagy erejű teremvédő.
                RoomEncounters =
                [
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Kőgólem, new(1, 1)), new(MonsterIds.Gargoyle, new(1, 1)), new(MonsterIds.KáoszmágusTanítvány, new(1, 2))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Medúza, new(1, 1)), new(MonsterIds.Martalóc, new(4, 6))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Káoszmágus, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Kőgólem, new(1, 1)), new(MonsterIds.OrkTestőr, new(2, 4))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Kiméra, new(1, 1)), new(MonsterIds.Minotaurusz, new(1, 1)), new(MonsterIds.Kobold, new(4, 6))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.VénBeholder, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Beholder, new(1, 2))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.ÉlőPáncél, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.OrkTestőr, new(3, 4))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.Káoszpap, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Gargoyle, new(2, 3)), new(MonsterIds.KáoszmágusTanítvány, new(2, 2))],
                        MovementProfile: EnemyMovementProfile.Stationary)
                ],
                CorridorEncounters =
                [
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.KáoszmágusTanítvány, new(2, 2)), new(MonsterIds.Martalóc, new(6, 10))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Gargoyle, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Patrol),
                    new(GroupCount: new(1, 2),
                        Members: [new(MonsterIds.Beholder, new(1, 1)), new(MonsterIds.Óriásskorpió, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Patrol),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Minotaurusz, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Hobgoblin, new(6, 9))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Káoszpap, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Orgyilkos, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde)
                ]
            },
            [19] = new()
            {
                Level = 19,
                Name = "A dermedt mélység",
                GuaranteedTraps = [new("TR107"), new("TR107"), new("TR104"), new("TR104")],
                DoubleWidthCorridorChance = 0.62,
                WallRune = new('▒'),
                WallColor = ConsoleColor.White,
                RoomCount = new(18, 22),
                RoomSize = new(7, 10),
                TreasureChestCount = new(10, 14),
                TreasureGold = new(1500, 3000),
                ItemCurseChancePercent = 8,
                // Kevés nagy óriás, sok kísérő; az élőholt és farkasrajok előkészítik az örökéjt.
                RoomEncounters =
                [
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Fagyóriás, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Ogre, new(2, 3)), new(MonsterIds.OrkÍjász, new(3, 5)), new(MonsterIds.Káoszmágus, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.CsontvázLovag, new(2, 2)), new(MonsterIds.Csontváz, new(8, 12)), new(MonsterIds.CsontvázÍjász, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Nekromanta, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Wight, new(1, 1)), new(MonsterIds.CsontvázÍjász, new(4, 6))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Lidércfarkas, new(3, 5)), new(MonsterIds.Farkas, new(6, 9))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.Csontsárkány, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Wight, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Ettin, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Ogre, new(2, 2)), new(MonsterIds.Hobgoblin, new(4, 6))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.VénMúmia, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Múmia, new(3, 5)), new(MonsterIds.CsontvázŐr, new(4, 6)), new(MonsterIds.Vámpír, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Stationary)
                ],
                CorridorEncounters =
                [
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Lidércfarkas, new(3, 5)), new(MonsterIds.Farkas, new(8, 12))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(1, 2),
                        Members: [new(MonsterIds.Fagyóriás, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Patrol),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Csontváz, new(10, 16)), new(MonsterIds.PáncélozottZombi, new(4, 6))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Boszorkány, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Wight, new(1, 1)), new(MonsterIds.CsontvázÍjász, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Küklopsz, new(1, 1)), new(MonsterIds.OrkTestőr, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Patrol)
                ]
            },
            [20] = new()
            {
                Level = 20,
                Name = "Az örökéj vámpírerődje",
                GuaranteedTraps = [new("TR109"), new("TR109")],
                Layout = new WideMazeLayoutConfiguration(new(3, 3), NarrowingChance: 0.13),
                WallRune = new('⣿'),
                WallColor = ConsoleColor.DarkMagenta,
                RoomCount = new(30, 36),
                RoomSize = new(7, 10),
                TreasureChestCount = new(18, 24),
                TreasureGold = new(1600, 3300),
                ItemCurseChancePercent = 20,
                // Három képernyő: szolgák és farkasok, kardmesterek, majd a vérmágikus belső udvar.
                RoomEncounters =
                [
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Ghoul, new(2, 4)), new(MonsterIds.Zombi, new(8, 12)), new(MonsterIds.CsontvázÍjász, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Stationary, ScreenNumber: 1),
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Nekromanta, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Csontváz, new(8, 12)), new(MonsterIds.Wight, new(1, 2))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.VámpírKardmester, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Vámpír, new(1, 2)), new(MonsterIds.Ghoul, new(5, 8))],
                        MovementProfile: EnemyMovementProfile.Stationary, ScreenNumber: 2),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.AlfaVérfarkas, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Vérfarkas, new(2, 3)), new(MonsterIds.Lidércfarkas, new(4, 6))],
                        MovementProfile: EnemyMovementProfile.Stationary, ScreenNumber: 1),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.Ősvámpír, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Vámpír, new(3, 5)), new(MonsterIds.Nekromanta, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Stationary, ScreenNumber: 3),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Halállovag, new(1, 2), EnemyGroupRole.Leader), new(MonsterIds.CsontvázLovag, new(4, 6)), new(MonsterIds.CsontvázÍjász, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 2),
                        Members: [new(MonsterIds.Vérmágus, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.VámpírKardmester, new(1, 2)), new(MonsterIds.Zombi, new(6, 9))],
                        MovementProfile: EnemyMovementProfile.Stationary, ScreenNumber: 3),
                    new(GroupCount: new(1, 2),
                        Members: [new(MonsterIds.VénMúmia, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Múmia, new(3, 5)), new(MonsterIds.Csontváz, new(6, 9))],
                        MovementProfile: EnemyMovementProfile.Stationary)
                ],
                CorridorEncounters =
                [
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Zombi, new(16, 24)), new(MonsterIds.Ghoul, new(4, 6))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Vámpír, new(1, 2), EnemyGroupRole.Leader), new(MonsterIds.Óriásdenevér, new(8, 12))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Vámpír, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Lidércfarkas, new(4, 6)), new(MonsterIds.Farkas, new(5, 8))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Nekromanta, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Csontváz, new(12, 18)), new(MonsterIds.CsontvázÍjász, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.VámpírKardmester, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.CsontvázŐr, new(4, 6))],
                        MovementProfile: EnemyMovementProfile.Patrol),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Árnylidérc, new(1, 1)), new(MonsterIds.Wight, new(2, 3)), new(MonsterIds.Ghoul, new(4, 6))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde)
                ]
            },
            [21] = new()
            {
                Level = 21,
                Name = "A sárkányok temetője",
                GuaranteedTraps = [new("TR111")],
                DoubleWidthCorridorChance = 0.84,
                WallRune = new('█'),
                WallColor = ConsoleColor.Gray,
                RoomCount = new(16, 20),
                RoomSize = new(8, 11),
                TreasureChestCount = new(10, 14),
                TreasureGold = new(2200, 4200),
                ItemCurseChancePercent = 16,
                // Nagy szárnyas veszélyek között csontváz-tömegek és a nekromanták teljes lánca.
                RoomEncounters =
                [
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.Drakolich, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Halállovag, new(2, 3)), new(MonsterIds.Nekromanta, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 2),
                        Members: [new(MonsterIds.Csontsárkány, new(1, 1)), new(MonsterIds.Wyvern, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Lich, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Csontváz, new(12, 18)), new(MonsterIds.CsontvázÍjász, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Nekromanta, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.PáncélozottZombi, new(4, 6)), new(MonsterIds.CsontvázLovag, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 2),
                        Members: [new(MonsterIds.VénMúmia, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Múmia, new(3, 5)), new(MonsterIds.Wight, new(1, 2)), new(MonsterIds.VámpírKardmester, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Wyvern, new(1, 2)), new(MonsterIds.Hárpia, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.ÉlőPáncél, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Gargoyle, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Stationary)
                ],
                CorridorEncounters =
                [
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.CsontvázLovag, new(3, 5)), new(MonsterIds.CsontvázÍjász, new(4, 6))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Wight, new(1, 2), EnemyGroupRole.Leader), new(MonsterIds.Csontváz, new(8, 12))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Wyvern, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Patrol),
                    new(GroupCount: new(1, 2),
                        Members: [new(MonsterIds.Árnylidérc, new(1, 2))],
                        MovementProfile: EnemyMovementProfile.Patrol),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Nekromanta, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Ghoul, new(5, 8)), new(MonsterIds.Óriásdenevér, new(6, 9))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde)
                ]
            },
            [22] = new()
            {
                Level = 22,
                Name = "A démoni sík: Parázspusztaság",
                Layout = new WideMazeLayoutConfiguration(new(3, 3), NarrowingChance: 0.08),
                WallRune = new('█'),
                WallColor = ConsoleColor.DarkRed,
                RoomCount = new(30, 36),
                RoomSize = new(8, 11),
                TreasureChestCount = new(18, 24),
                TreasureGold = new(2200, 4200),
                ItemCurseChancePercent = 22,
                // Démoni tömegek: a szolgák száma nő, a nagy démonok ritka vezérek maradnak.
                RoomEncounters =
                [
                    new(GroupCount: new(3, 4),
                        Members: [new(MonsterIds.Káoszpap, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Pokolfajzat, new(12, 18)), new(MonsterIds.DémoniKorcs, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Stationary, ScreenNumber: 1),
                    new(GroupCount: new(3, 3),
                        Members: [new(MonsterIds.Parázsdémon, new(3, 5)), new(MonsterIds.Pokolfajzat, new(8, 12)), new(MonsterIds.KáoszmágusTanítvány, new(1, 2))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Vérmágus, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.KarmosDémon, new(1, 2)), new(MonsterIds.DémoniKorcs, new(6, 9))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Pokolőr, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Pokolfajzat, new(12, 18)), new(MonsterIds.Káoszmágus, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.Pokolfejedelem, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Démonlovag, new(3, 5)), new(MonsterIds.Vérmágus, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Stationary, ScreenNumber: 3),
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Pokolkutya, new(1, 2)), new(MonsterIds.Pokolfajzat, new(6, 9))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 2),
                        Members: [new(MonsterIds.Káoszpap, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Káoszlovag, new(3, 5)), new(MonsterIds.DémoniKorcs, new(4, 6))],
                        MovementProfile: EnemyMovementProfile.Stationary)
                ],
                CorridorEncounters =
                [
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.KarmosDémon, new(1, 2), EnemyGroupRole.Leader), new(MonsterIds.Pokolfajzat, new(16, 24))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.DémoniKorcs, new(6, 9)), new(MonsterIds.Pokolfajzat, new(12, 18))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Pokolőr, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Parázsdémon, new(4, 6)), new(MonsterIds.Pokolfajzat, new(8, 12))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Káoszmágus, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.DémoniKorcs, new(6, 9)), new(MonsterIds.Pokolfajzat, new(8, 12))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Démonlovag, new(1, 1)), new(MonsterIds.Pokolkutya, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Patrol)
                ]
            },
            [23] = new()
            {
                Level = 23,
                Name = "A démoni sík: Vértrónus",
                GuaranteedTraps = [new("TR111"), new("TR111")],
                Layout = new WideMazeLayoutConfiguration(new(3, 3), NarrowingChance: 0.11),
                WallRune = new('▓'),
                WallColor = ConsoleColor.Red,
                RoomCount = new(32, 38),
                RoomSize = new(8, 11),
                TreasureChestCount = new(20, 26),
                TreasureGold = new(2600, 5200),
                ItemCurseChancePercent = 25,
                // Vérmágikus hadrendek: gyógyítók, tüzérmágusok és tömeges démoni fedezet.
                RoomEncounters =
                [
                    new(GroupCount: new(3, 4),
                        Members: [new(MonsterIds.DémoniKorcs, new(8, 12)), new(MonsterIds.Pokolfajzat, new(10, 16))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Vérmágus, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.DémoniKorcs, new(8, 12)), new(MonsterIds.Pokolőr, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Káoszmágus, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Parázsdémon, new(3, 5)), new(MonsterIds.KarmosDémon, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.KáoszFőpap, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Démonlovag, new(3, 5)), new(MonsterIds.Pokolfajzat, new(8, 12))],
                        MovementProfile: EnemyMovementProfile.Stationary, ScreenNumber: 2),
                    new(GroupCount: new(1, 2),
                        Members: [new(MonsterIds.Pokolfejedelem, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.DémoniKorcs, new(8, 12))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.BalorDémon, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Démonlovag, new(3, 5)), new(MonsterIds.Vérmágus, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Stationary, ScreenNumber: 3),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Démonpók, new(1, 1)), new(MonsterIds.Pokolkutya, new(2, 3)), new(MonsterIds.Pokolfajzat, new(8, 12))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.Feketemágus, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.ÉlőPáncél, new(1, 1)), new(MonsterIds.Káoszlovag, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Stationary, ScreenNumber: 3)
                ],
                CorridorEncounters =
                [
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Pokolőr, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.DémoniKorcs, new(8, 12)), new(MonsterIds.Pokolfajzat, new(8, 12))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Vérdémon, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Pokolfajzat, new(16, 24))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Démonlovag, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Parázsdémon, new(4, 6)), new(MonsterIds.Pokolfajzat, new(8, 12))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Vérmágus, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Vérdémon, new(1, 1)), new(MonsterIds.DémoniKorcs, new(8, 12))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Pokolfejedelem, new(1, 1)), new(MonsterIds.KarmosDémon, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Patrol),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Káoszpap, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Pokolkutya, new(2, 3)), new(MonsterIds.Pokolfajzat, new(8, 12))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde)
                ]
            },
            [24] = new()
            {
                Level = 24,
                Name = "A káosz szíve",
                GuaranteedTraps = [new("TR112"), new("TR112"), new("TR110"), new("TR110")],
                DoubleWidthCorridorChance = 0.80,
                WallRune = new('▒'),
                WallColor = ConsoleColor.Magenta,
                RoomCount = new(20, 24),
                RoomSize = new(8, 12),
                TreasureChestCount = new(12, 16),
                TreasureGold = new(3200, 6200),
                ItemCurseChancePercent = 18,
                // Minőségi csúcspont: veszélyes mágusok és nagy őrök, köztük gyenge fedezőrajok.
                RoomEncounters =
                [
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.Káoszsárkány, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Drakolich, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.VénBeholder, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Kőgólem, new(1, 1)), new(MonsterIds.Káoszmágus, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Feketemágus, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Káoszlovag, new(4, 6)), new(MonsterIds.Káoszpap, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.KáoszFőpap, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Démonlovag, new(2, 3)), new(MonsterIds.DémoniKorcs, new(4, 6))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.Pokolfejedelem, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Pokolőr, new(2, 3)), new(MonsterIds.Pokolfajzat, new(8, 12))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Lich, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.CsontvázLovag, new(6, 9)), new(MonsterIds.CsontvázÍjász, new(4, 6))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.ÉlőPáncél, new(1, 1)), new(MonsterIds.Gargoyle, new(2, 3)), new(MonsterIds.KáoszmágusTanítvány, new(2, 2))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.Vérmágus, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Vérdémon, new(1, 1)), new(MonsterIds.Démonpók, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Stationary)
                ],
                CorridorEncounters =
                [
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Pokolfajzat, new(12, 18)), new(MonsterIds.DémoniKorcs, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Halállovag, new(1, 1)), new(MonsterIds.Wight, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Patrol),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Gargoyle, new(2, 3)), new(MonsterIds.Óriásdenevér, new(6, 9))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(1, 2),
                        Members: [new(MonsterIds.Vérmágus, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Démonpók, new(1, 1)), new(MonsterIds.DémoniKorcs, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Patrol),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Káoszpap, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Káoszlovag, new(3, 5)), new(MonsterIds.Martalóc, new(6, 9))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde)
                ]
            },
            [25] = new()
            {
                Level = 25,
                Name = "A káosz trónja",
                DoubleWidthCorridorChance = 0.86,
                WallRune = new('▓'),
                WallColor = ConsoleColor.Magenta,
                RoomCount = new(22, 26),
                RoomSize = new(8, 12),
                TreasureChestCount = new(16, 20),
                TreasureGold = new(3600, 7000),
                ItemCurseChancePercent = 12,
                // A végső őrség vegyes rendjei: elit termek és olcsóbb utánpótlás, minden mágusszereppel.
                RoomEncounters =
                [
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.Feketemágus, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.ÉlőPáncél, new(1, 1)), new(MonsterIds.Káoszlovag, new(4, 6))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.KáoszFőpap, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Káoszlovag, new(4, 6)), new(MonsterIds.Káoszmágus, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Pokolfejedelem, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Démonlovag, new(3, 5)), new(MonsterIds.Pokolfajzat, new(6, 10))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Lich, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.VénMúmia, new(1, 1)), new(MonsterIds.CsontvázLovag, new(4, 6))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 2),
                        Members: [new(MonsterIds.ŐsiMinotaurusz, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Kőgólem, new(1, 1)), new(MonsterIds.Medúza, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.FeketeSárkány, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Kiméra, new(1, 2))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(1, 1),
                        Members: [new(MonsterIds.VámpírKardmester, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Vámpír, new(2, 2)), new(MonsterIds.AlfaVérfarkas, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Stationary),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Vérmágus, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Martalóc, new(6, 9)), new(MonsterIds.Káoszlovag, new(3, 5))],
                        MovementProfile: EnemyMovementProfile.Stationary)
                ],
                CorridorEncounters =
                [
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Gargoyle, new(3, 5)), new(MonsterIds.Pokolkutya, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Patrol),
                    new(GroupCount: new(2, 3),
                        Members: [new(MonsterIds.Vérmágus, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Pokolfajzat, new(12, 18)), new(MonsterIds.DémoniKorcs, new(4, 6))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.Nekromanta, new(1, 1), EnemyGroupRole.Leader), new(MonsterIds.Wight, new(2, 3)), new(MonsterIds.Ghoul, new(6, 9))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(2, 2),
                        Members: [new(MonsterIds.KáoszmágusTanítvány, new(2, 2)), new(MonsterIds.Martalóc, new(6, 9)), new(MonsterIds.Káoszlovag, new(2, 3))],
                        MovementProfile: EnemyMovementProfile.Wander, Behavior: EnemyEncounterBehavior.Horde),
                    new(GroupCount: new(1, 2),
                        Members: [new(MonsterIds.ÉlőPáncél, new(1, 1)), new(MonsterIds.Káoszpap, new(1, 1))],
                        MovementProfile: EnemyMovementProfile.Patrol)
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
            5 or 6 or 13 or 14 or 15 => -1,
            10 or 16 or 17 or 21 or 24 => -2,
            _ => 0
        };
        (configuration.TrapCount, configuration.TrapIds) = configuration.Level switch
        {
            1 => (new IntRange(3, 7), BasicTraps),
            2 => (new IntRange(4, 8), LevelTwoTraps),
            <= 7 => (new IntRange(5, 9), EarlyTraps),
            <= 10 => (new IntRange(6, 11), MidTraps),
            15 => (new IntRange(12, 18), AdvancedTraps),
            16 => (new IntRange(16, 22), AdvancedTraps),
            <= 17 => (new IntRange(6, 11), AdvancedTraps),
            <= 21 => (new IntRange(6, 13), DeadlyTraps),
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
                Encounters.Solo(MonsterIds.Zombi, Amount.Several, EnemyMovementProfile.Wander),
                Encounters.Solo(MonsterIds.CsontvázLovag, Amount.Several, EnemyMovementProfile.Patrol),
                Encounters.Horde(MonsterIds.Ghoul, Amount.Few, Amount.Pair)
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
    RoomKind? TargetRoomKind = null,
    TerrainTag TargetTerrainTags = TerrainTag.None,
    EnemyEncounterPosture Posture = EnemyEncounterPosture.Normal,
    int TriggerDistance = 0);

#endregion
