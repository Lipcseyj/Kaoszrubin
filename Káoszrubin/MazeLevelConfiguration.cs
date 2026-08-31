using KaoszRubin.Domain.Combat;

namespace KaoszRubin;

public sealed record IntRange(int Minimum, int Maximum)
{
    public int Roll(Random random) => random.Next(Minimum, Maximum + 1);
}

public enum Amount { One, Few, TwoThree, Several, Pack, Lots, Horde }

public static class AmountRanges
{
    public static IntRange Range(this Amount amount) => amount switch
    {
        Amount.One => new(1, 1),
        Amount.Few => new(1, 2),
        Amount.TwoThree => new(2, 3),
        Amount.Several => new(3, 9),
        Amount.Pack => new(10, 15),
        Amount.Lots => new(16, 49),
        Amount.Horde => new(50, 100),
        _ => new(1, 1)
    };
}

public sealed record EnemyGroupMemberConfiguration(string EnemyId, IntRange Count,
    EnemyGroupRole Role = EnemyGroupRole.Member);
public sealed record EnemyEncounterConfiguration(IntRange GroupCount,
    IReadOnlyList<EnemyGroupMemberConfiguration> Members,
    EnemyMovementProfile? MovementProfile = null);
public sealed record ResolvedEnemyGroupMember(EnemyDefinition Definition, IntRange Count, EnemyGroupRole Role);
public sealed record ResolvedEnemyEncounter(IntRange GroupCount,
    IReadOnlyList<ResolvedEnemyGroupMember> Members, EnemyMovementProfile? MovementProfile);

public static class Encounters
{
    public static EnemyEncounterConfiguration Same(string enemyId, Amount groups, Amount size,
        EnemyMovementProfile? movement = EnemyMovementProfile.Stationary) =>
        new(groups.Range(), [new(enemyId, size.Range())], movement);

    public static EnemyEncounterConfiguration Solo(string enemyId, Amount count,
        EnemyMovementProfile? movement = null) =>
        new(count.Range(), [new(enemyId, Amount.One.Range())], movement);

    public static EnemyEncounterConfiguration Mixed(string firstEnemyId, Amount firstCount,
        string secondEnemyId, Amount secondCount, Amount groups,
        EnemyMovementProfile? movement = EnemyMovementProfile.Stationary) =>
        new(groups.Range(), [new(firstEnemyId, firstCount.Range()), new(secondEnemyId, secondCount.Range())], movement);

    public static EnemyEncounterConfiguration LeaderGroup(string leaderId, string followerId,
        Amount groups, Amount followers, EnemyMovementProfile? movement = EnemyMovementProfile.Stationary) =>
        new(groups.Range(),
            [new(leaderId, Amount.One.Range(), EnemyGroupRole.Leader), new(followerId, followers.Range())], movement);
}

public sealed class MazeLevelConfiguration
{
    public required int Level { get; init; }
    public required string Name { get; init; }
    public double DoubleWidthCorridorChance { get; init; } = 0.80;
    public System.Text.Rune WallRune { get; init; } = new('█');
    public ConsoleColor WallColor { get; init; } = ConsoleColor.DarkGray;
    public required IntRange RoomCount { get; init; }
    public required IntRange RoomSize { get; init; }
    public required IntRange TreasureChestCount { get; init; }
    public required IntRange TreasureGold { get; init; }
    public IntRange TrapCount { get; set; } = new(0, 0);
    public IReadOnlyList<string> TrapIds { get; set; } = [];
    public int VisionModifier { get; set; }
    public IReadOnlyList<string> QuestRoomIds { get; init; } = [];
    public required IReadOnlyList<EnemyEncounterConfiguration> RoomEncounters { get; init; }
    public required IReadOnlyList<EnemyEncounterConfiguration> CorridorEncounters { get; init; }

    public MazeGenerationSettings CreateGenerationSettings(Random random) => new()
    {
        DoubleWidthCorridorChance = DoubleWidthCorridorChance,
        RoomCount = RoomCount.Roll(random),
        MinimumRoomSize = RoomSize.Minimum,
        MaximumRoomSize = RoomSize.Maximum,
        TreasureChestCount = TreasureChestCount.Roll(random),
        TreasureGoldRange = TreasureGold,
        WallRune = WallRune,
        WallColor = WallColor,
        LevelName = Name,
        QuestRoomIds = QuestRoomIds
    };
}

public static class MazeLevelConfigurations
{
    public const int FinalLevel = 21;
    private static readonly string[] BasicTraps = ["TR001"];
    private static readonly string[] EarlyTraps = ["TR001", "TR002", "TR003"];
    private static readonly string[] MidTraps = ["TR001", "TR002", "TR003", "TR004", "TR008"];
    private static readonly string[] AdvancedTraps = ["TR002", "TR003", "TR004", "TR005", "TR008"];
    private static readonly string[] DeadlyTraps = ["TR003", "TR004", "TR005", "TR006", "TR008"];
    private static readonly string[] ChaosTraps = ["TR004", "TR005", "TR006", "TR007", "TR008"];

    private static readonly IReadOnlyDictionary<int, MazeLevelConfiguration> Configurations =
        new Dictionary<int, MazeLevelConfiguration>
        {
            [1] = new()
            {
                Name = "Patkányjáratok",
                DoubleWidthCorridorChance = 0.95,
                Level = 1,
                RoomCount = Amount.Several.Range(),
                RoomSize = new(3, 5),
                TreasureChestCount = new(2, 2),
                TreasureGold = new(40, 100),
                RoomEncounters =
                [
                    Encounters.Same(MonsterIds.Óriáspatkány, Amount.Several, Amount.TwoThree),
                    Encounters.Same(MonsterIds.Kobold, Amount.Few, Amount.Several),
                    Encounters.Same(MonsterIds.Goblin, Amount.Few, Amount.Few)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo(MonsterIds.Óriáspatkány, Amount.Several),
                    Encounters.Solo(MonsterIds.Kobold, Amount.Few)
                ]
            },
            [2] = new()
            {
                Name = "Patkányvezér",
                DoubleWidthCorridorChance = 0.40,
                Level = 2,
                RoomCount = Amount.Several.Range(),
                RoomSize = new(3, 5),
                TreasureChestCount = Amount.TwoThree.Range(),
                TreasureGold = new(60, 140),
                RoomEncounters =
                [
                    Encounters.Same(MonsterIds.Óriáspatkány, Amount.Few, Amount.TwoThree),
                    Encounters.Same(MonsterIds.Csontváz, Amount.One, Amount.One),
                    Encounters.LeaderGroup(MonsterIds.Patkányember, MonsterIds.Óriáspatkány, Amount.One, Amount.TwoThree)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo(MonsterIds.Óriáspatkány, Amount.Pack),
                    Encounters.Solo(MonsterIds.Kobold, Amount.Few)
                ]
            },
            [3] = new()
            {
                Name = "Goblinüregek",
                WallRune = new('▓'),
                WallColor = ConsoleColor.DarkGreen,
                Level = 3,
                DoubleWidthCorridorChance = 0.75,
                RoomCount = Amount.Several.Range(),
                RoomSize = new(3, 6),
                TreasureChestCount = new(8, 12),
                TreasureGold = new(80, 200),
                RoomEncounters =
                [
                    Encounters.Same(MonsterIds.Kobold, Amount.Few, Amount.Several),
                    Encounters.Mixed(MonsterIds.Goblin, Amount.Several, MonsterIds.Kobold, Amount.Few, Amount.Few),
                    Encounters.Same(MonsterIds.Csontváz, Amount.Few, Amount.Few)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo(MonsterIds.Óriáspatkány, Amount.Few),
                    Encounters.Solo(MonsterIds.Goblin, Amount.Several, EnemyMovementProfile.Patrol),
                    Encounters.Solo(MonsterIds.Farkas, Amount.Few, EnemyMovementProfile.Patrol)
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
                    Encounters.Same(MonsterIds.Goblin, Amount.Few, Amount.Several),
                    Encounters.Mixed(MonsterIds.Csontváz, Amount.Several, MonsterIds.Zombi, Amount.Few, Amount.Few),
                    Encounters.LeaderGroup(MonsterIds.Ork, MonsterIds.Goblin, Amount.Few, Amount.Several)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo(MonsterIds.Farkas, Amount.Several, EnemyMovementProfile.Patrol),
                    Encounters.Solo(MonsterIds.Goblin, Amount.Few)
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
                QuestRoomIds = ["RODERIC_MEETING", "RODERIC_INSIGNIA"],
                RoomEncounters =
                [
                    Encounters.Same(MonsterIds.Csontváz, Amount.Several, Amount.Several),
                    Encounters.Mixed(MonsterIds.Zombi, Amount.Several, MonsterIds.Csontváz, Amount.Few, Amount.Few),
                    Encounters.LeaderGroup(MonsterIds.Ghoul, MonsterIds.Csontváz, Amount.One, Amount.Pack)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo(MonsterIds.Zombi, Amount.Few),
                    Encounters.Mixed(MonsterIds.Zombi, Amount.Few, MonsterIds.Csontváz, Amount.Few, Amount.Few),
                ]
            },
            [6] = new()
            {
                Level = 6,
                Name = "A nagy csarnokok szintje",
                DoubleWidthCorridorChance = 0.20,
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
                    Encounters.LeaderGroup(MonsterIds.Ogre, MonsterIds.Ork, Amount.One, Amount.Pack)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo(MonsterIds.Ork, Amount.Few, EnemyMovementProfile.Patrol),
                    Encounters.Solo(MonsterIds.Hobgoblin, Amount.Few, EnemyMovementProfile.Patrol),
                    Encounters.Solo(MonsterIds.Gnoll, Amount.Few)
                ]
            },
            [7] = new()
            {
                Level = 7,
                Name = "A mérgező barlang",
                DoubleWidthCorridorChance = 0.88,
                WallRune = new('▒'),
                WallColor = ConsoleColor.DarkCyan,
                RoomCount = Amount.Pack.Range(),
                RoomSize = new(4, 8),
                TreasureChestCount = Amount.Several.Range(),
                TreasureGold = new(300, 780),
                RoomEncounters =
                [
                    Encounters.Same(MonsterIds.Óriáspók, Amount.Several, Amount.Several),
                    Encounters.Mixed(MonsterIds.Savanyálka, Amount.Several, MonsterIds.BarlangiGyík, Amount.Several, Amount.Few),
                    Encounters.LeaderGroup(MonsterIds.IfjúBaziliszkusz, MonsterIds.Óriáspók, Amount.Few, Amount.Several)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo(MonsterIds.Óriásdenevér, Amount.Several),
                    Encounters.Solo(MonsterIds.BarlangiGyík, Amount.Few, EnemyMovementProfile.Patrol)
                ]
            },
            [8] = new()
            {
                Level = 8,
                Name = "Az ork haditábor",
                DoubleWidthCorridorChance = 0.78,
                WallRune = new('▓'),
                WallColor = ConsoleColor.DarkRed,
                RoomCount = Amount.Pack.Range(),
                RoomSize = new(5, 8),
                TreasureChestCount = Amount.Several.Range(),
                TreasureGold = new(420, 820),
                RoomEncounters =
                [
                    Encounters.Same(MonsterIds.Ork, Amount.Several, Amount.Pack),
                    Encounters.Mixed(MonsterIds.Hobgoblin, Amount.Several, MonsterIds.Bugbear, Amount.Few, Amount.Few),
                    Encounters.LeaderGroup(MonsterIds.OrkSámán, MonsterIds.Ork, Amount.Few, Amount.Pack)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo(MonsterIds.Bugbear, Amount.Few, EnemyMovementProfile.Patrol),
                    Encounters.Solo(MonsterIds.Hobgoblin, Amount.Few)
                ]
            },
            [9] = new()
            {
                Level = 9,
                Name = "Az elátkozott sírkamrák",
                DoubleWidthCorridorChance = 0.92,
                WallRune = new('▦'),
                WallColor = ConsoleColor.DarkMagenta,
                RoomCount = Amount.Pack.Range(),
                RoomSize = new(4, 8),
                TreasureChestCount = Amount.Pack.Range(),
                TreasureGold = new(520, 1000),
                RoomEncounters =
                [
                    Encounters.Same(MonsterIds.Múmia, Amount.Several, Amount.Several),
                    Encounters.Mixed(MonsterIds.Wight, Amount.Several, MonsterIds.Ghoul, Amount.Several, Amount.Few),
                    Encounters.LeaderGroup(MonsterIds.ÉjiBanya, MonsterIds.Múmia, Amount.Few, Amount.Several)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo(MonsterIds.Wight, Amount.Few, EnemyMovementProfile.Patrol),
                    Encounters.Solo(MonsterIds.Ghoul, Amount.Several)
                ]
            },
            [10] = new()
            {
                Level = 10,
                Name = "Az óriások erődje",
                DoubleWidthCorridorChance = 0.12,
                WallRune = new('▩'),
                WallColor = ConsoleColor.Gray,
                RoomCount = Amount.Pack.Range(),
                RoomSize = new(7, 11),
                TreasureChestCount = Amount.Pack.Range(),
                TreasureGold = new(560, 1050),
                RoomEncounters =
                [
                    Encounters.Same(MonsterIds.Ogre, Amount.Few, Amount.Several),
                    Encounters.Mixed(MonsterIds.Troll, Amount.Few, MonsterIds.Ettin, Amount.Few, Amount.Few),
                    Encounters.LeaderGroup(MonsterIds.Fagyóriás, MonsterIds.Ogre, Amount.One, Amount.Several)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo(MonsterIds.Ettin, Amount.Few, EnemyMovementProfile.Patrol),
                    Encounters.Solo(MonsterIds.Minotaurusz, Amount.Few)
                ]
            },
            [11] = new()
            {
                Level = 11,
                Name = "A sárkánykultusz szentélye",
                DoubleWidthCorridorChance = 0.80,
                WallRune = new('▥'),
                WallColor = ConsoleColor.Red,
                RoomCount = Amount.Pack.Range(),
                RoomSize = new(5, 9),
                TreasureChestCount = Amount.Pack.Range(),
                TreasureGold = new(750, 1500),
                RoomEncounters =
                [
                    Encounters.Same(MonsterIds.Wyvern, Amount.Several, Amount.Several),
                    Encounters.Mixed(MonsterIds.Kiméra, Amount.Few, MonsterIds.OrkSámán, Amount.Several, Amount.Few),
                    Encounters.LeaderGroup(MonsterIds.VörösSárkány, MonsterIds.OrkSámán, Amount.One, Amount.Pack)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo(MonsterIds.Wyvern, Amount.Few, EnemyMovementProfile.Patrol),
                    Encounters.Solo(MonsterIds.Kiméra, Amount.Few)
                ]
            },
            [12] = new()
            {
                Level = 12,
                Name = "A rothadó mocsár",
                DoubleWidthCorridorChance = 0.90,
                WallRune = new('▒'),
                WallColor = ConsoleColor.DarkGreen,
                RoomCount = Amount.Pack.Range(),
                RoomSize = new(5, 10),
                TreasureChestCount = Amount.Pack.Range(),
                TreasureGold = new(850, 1700),
                RoomEncounters =
                [
                    Encounters.Same(MonsterIds.Savanyálka, Amount.Several, Amount.Several),
                    Encounters.Mixed(MonsterIds.PestishordozóPatkány, Amount.Pack, MonsterIds.Óriáspók, Amount.Several, Amount.Few),
                    Encounters.LeaderGroup(MonsterIds.Hidra, MonsterIds.BarlangiGyík, Amount.One, Amount.Pack)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo(MonsterIds.PestishordozóPatkány, Amount.Several),
                    Encounters.Solo(MonsterIds.ÉjiBanya, Amount.Few, EnemyMovementProfile.Patrol),
                    Encounters.Solo(MonsterIds.Savanyálka, Amount.Few)
                ]
            },
            [13] = new()
            {
                Level = 13,
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
                    Encounters.LeaderGroup(MonsterIds.Beholder, MonsterIds.Óriásdenevér, Amount.One, Amount.Pack)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo(MonsterIds.Minotaurusz, Amount.Several, EnemyMovementProfile.Patrol),
                    Encounters.Solo(MonsterIds.Kőgólem, Amount.Few),
                    Encounters.Solo(MonsterIds.Óriásdenevér, Amount.Several)
                ]
            },
            [14] = new()
            {
                Level = 14,
                Name = "A megtört kristálycsarnok",
                DoubleWidthCorridorChance = 0.72,
                WallRune = new('▓'),
                WallColor = ConsoleColor.Cyan,
                RoomCount = Amount.Pack.Range(),
                RoomSize = new(5, 9),
                TreasureChestCount = Amount.Pack.Range(),
                TreasureGold = new(1100, 2100),
                RoomEncounters =
                [
                    Encounters.Same(MonsterIds.Medúza, Amount.Few, Amount.Several),
                    Encounters.Mixed(MonsterIds.Kőgólem, Amount.Several, MonsterIds.Kiméra, Amount.Few, Amount.Few),
                    Encounters.LeaderGroup(MonsterIds.VénBeholder, MonsterIds.Beholder, Amount.One, Amount.Few)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo(MonsterIds.Medúza, Amount.Few, EnemyMovementProfile.Patrol),
                    Encounters.Solo(MonsterIds.Kiméra, Amount.Few),
                    Encounters.Solo(MonsterIds.Beholder, Amount.Few)
                ]
            },
            [15] = new()
            {
                Level = 15,
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
                    Encounters.LeaderGroup(MonsterIds.Csontsárkány, MonsterIds.Wight, Amount.One, Amount.Several)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo(MonsterIds.Lidércfarkas, Amount.Several, EnemyMovementProfile.Patrol),
                    Encounters.Solo(MonsterIds.Fagyóriás, Amount.Few),
                    Encounters.Solo(MonsterIds.Wight, Amount.Few)
                ]
            },
            [16] = new()
            {
                Level = 16,
                Name = "Az örökéj vámpírerődje",
                DoubleWidthCorridorChance = 0.76,
                WallRune = new('▓'),
                WallColor = ConsoleColor.DarkMagenta,
                RoomCount = Amount.Pack.Range(),
                RoomSize = new(5, 9),
                TreasureChestCount = Amount.Pack.Range(),
                TreasureGold = new(1450, 2750),
                RoomEncounters =
                [
                    Encounters.Same(MonsterIds.Vámpír, Amount.Few, Amount.Several),
                    Encounters.Mixed(MonsterIds.Halállovag, Amount.Few, MonsterIds.Wight, Amount.Several, Amount.Few),
                    Encounters.LeaderGroup(MonsterIds.Ősvámpír, MonsterIds.Vámpír, Amount.One, Amount.Several)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo(MonsterIds.Vámpír, Amount.Few, EnemyMovementProfile.Patrol),
                    Encounters.Solo(MonsterIds.Halállovag, Amount.Few),
                    Encounters.Solo(MonsterIds.ÉjiBanya, Amount.Few)
                ]
            },
            [17] = new()
            {
                Level = 17,
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
            [18] = new()
            {
                Level = 18,
                Name = "A démoni sík: Parázspusztaság",
                DoubleWidthCorridorChance = 0.88,
                WallRune = new('█'),
                WallColor = ConsoleColor.DarkRed,
                RoomCount = Amount.Pack.Range(),
                RoomSize = new(6, 10),
                TreasureChestCount = new(12, 18),
                TreasureGold = new(1900, 3600),
                RoomEncounters =
                [
                    Encounters.Same(MonsterIds.Démonpók, Amount.Several, Amount.Several),
                    Encounters.Mixed(MonsterIds.Démonlovag, Amount.Several, MonsterIds.Démonpók, Amount.Several, Amount.Few),
                    Encounters.LeaderGroup(MonsterIds.Pokolfejedelem, MonsterIds.Démonlovag, Amount.One, Amount.Pack)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo(MonsterIds.Démonpók, Amount.Several, EnemyMovementProfile.Patrol),
                    Encounters.Solo(MonsterIds.Démonlovag, Amount.Few),
                    Encounters.Solo(MonsterIds.Pokolfejedelem, Amount.One)
                ]
            },
            [19] = new()
            {
                Level = 19,
                Name = "A démoni sík: Vértrónus",
                DoubleWidthCorridorChance = 0.68,
                WallRune = new('▓'),
                WallColor = ConsoleColor.Red,
                RoomCount = Amount.Pack.Range(),
                RoomSize = new(7, 11),
                TreasureChestCount = new(14, 20),
                TreasureGold = new(2200, 4200),
                RoomEncounters =
                [
                    Encounters.Same(MonsterIds.Démonlovag, Amount.Several, Amount.Several),
                    Encounters.Mixed(MonsterIds.Pokolfejedelem, Amount.Few, MonsterIds.Démonpók, Amount.Pack, Amount.Few),
                    Encounters.LeaderGroup(MonsterIds.BalorDémon, MonsterIds.Démonlovag, Amount.Few, Amount.Pack)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo(MonsterIds.Démonlovag, Amount.Several, EnemyMovementProfile.Patrol),
                    Encounters.Solo(MonsterIds.Pokolfejedelem, Amount.Few),
                    Encounters.Solo(MonsterIds.BalorDémon, Amount.One)
                ]
            },
            [20] = new()
            {
                Level = 20,
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
                    Encounters.LeaderGroup(MonsterIds.Káoszsárkány, MonsterIds.Drakolich, Amount.One, Amount.Few)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo(MonsterIds.VénBeholder, Amount.Few, EnemyMovementProfile.Patrol),
                    Encounters.Solo(MonsterIds.Drakolich, Amount.Few),
                    Encounters.Solo(MonsterIds.Pokolfejedelem, Amount.Few)
                ]
            },
            [21] = new()
            {
                Level = 21,
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
                    Encounters.Mixed(MonsterIds.Lich, Amount.Few, MonsterIds.Halállovag, Amount.Several, Amount.Few),
                    Encounters.LeaderGroup(MonsterIds.Pokolfejedelem, MonsterIds.Démonlovag, Amount.Few, Amount.Pack)
                ],
                CorridorEncounters =
                [
                    Encounters.Mixed(MonsterIds.Wyvern, Amount.Few, MonsterIds.Hárpia, Amount.Several,
                        Amount.Few, EnemyMovementProfile.Patrol),
                    Encounters.Mixed(MonsterIds.Troll, Amount.Few, MonsterIds.ÉjiBanya, Amount.Few, Amount.Few),
                    Encounters.Mixed(MonsterIds.Wight, Amount.Few, MonsterIds.Démonpók, Amount.Several, Amount.Few),
                    Encounters.Solo(MonsterIds.VénBeholder, Amount.Few, EnemyMovementProfile.Patrol),
                    Encounters.Solo(MonsterIds.Pokolfejedelem, Amount.Few)
                ]
            }
        };

    public static MazeLevelConfiguration Get(int level)
    {
        if (Configurations.TryGetValue(level, out var configuration)) return ConfigureTraps(configuration);
        var increase = level - 11;
        var tier = Math.Clamp(4 + increase / 3, 4, 5);
        var (leader, follower, peer) = tier switch
        {
            4 => (MonsterIds.Beholder, MonsterIds.Ogre, MonsterIds.Kiméra),
            _ => (MonsterIds.Pokolfejedelem, MonsterIds.Démonlovag, MonsterIds.Ősvámpír)
        };
        return ConfigureTraps(new MazeLevelConfiguration
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

    private static MazeLevelConfiguration ConfigureTraps(MazeLevelConfiguration configuration)
    {
        configuration.VisionModifier = configuration.Level switch
        {
            5 or 12 => -1,
            9 or 13 or 17 or 20 => -2,
            _ => 0
        };
        (configuration.TrapCount, configuration.TrapIds) = configuration.Level switch
        {
            <= 2 => (new IntRange(2, 5), BasicTraps),
            <= 6 => (new IntRange(3, 5), EarlyTraps),
            <= 9 => (new IntRange(3, 6), MidTraps),
            <= 13 => (new IntRange(3, 6), AdvancedTraps),
            <= 17 => (new IntRange(4, 6), DeadlyTraps),
            _ => (new IntRange(4, 8), ChaosTraps)
        };
        return configuration;
    }
}
