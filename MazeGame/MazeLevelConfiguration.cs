using MazeGame.Domain.Combat;

namespace MazeGame;

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
        Amount.Pack => new(10, 19),
        Amount.Lots => new(20, 49),
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
        LevelName = Name
    };
}

public static class MazeLevelConfigurations
{
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
                TreasureChestCount = Amount.Few.Range(),
                TreasureGold = new(10, 50),
                RoomEncounters =
                [
                    Encounters.Same("E001", Amount.Several, Amount.TwoThree),
                    Encounters.Same("E002", Amount.Few, Amount.Several),
                    Encounters.Same("E003", Amount.Few, Amount.Few)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo("E001", Amount.Several),
                    Encounters.Solo("E002", Amount.Few)
                ]
            },
            [2] = new()
            {
                Name = "Patkányvezér",
                DoubleWidthCorridorChance = 0.40,
                Level = 2,
                RoomCount = Amount.TwoThree.Range(),
                RoomSize = new(3, 5),
                TreasureChestCount = Amount.Few.Range(),
                TreasureGold = new(30, 70),
                RoomEncounters =
                [
                    Encounters.Same("E001", Amount.Few, Amount.TwoThree),
                    Encounters.Same("E004", Amount.One, Amount.One),
                    Encounters.LeaderGroup("E051", "E001", Amount.One, Amount.TwoThree)
],
                CorridorEncounters =
                [
                    Encounters.Solo("E001", Amount.Pack),
                    Encounters.Solo("E002", Amount.Few)
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
                TreasureChestCount = Amount.Several.Range(),
                TreasureGold = new(35, 90),
                RoomEncounters =
                [
                    Encounters.Same("E002", Amount.Few, Amount.Several),
                    Encounters.Mixed("E003", Amount.Several, "E002", Amount.Few, Amount.Few),
                    Encounters.Same("E004", Amount.Few, Amount.Few)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo("E001", Amount.Few),
                    Encounters.Solo("E003", Amount.Several, EnemyMovementProfile.Patrol),
                    Encounters.Solo("E005", Amount.Few, EnemyMovementProfile.Patrol)
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
                TreasureGold = new(70, 150),
                RoomEncounters =
                [
                    Encounters.Same("E003", Amount.Few, Amount.Several),
                    Encounters.Mixed("E004", Amount.Several, "E006", Amount.Few, Amount.Few),
                    Encounters.LeaderGroup("E007", "E002", Amount.Few, Amount.Several)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo("E005", Amount.Few, EnemyMovementProfile.Patrol),
                    Encounters.Solo("E003", Amount.Few)
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
                TreasureGold = new(120, 240),
                RoomEncounters =
                [
                    Encounters.Same("E004", Amount.Several, Amount.Several),
                    Encounters.Mixed("E006", Amount.Several, "E004", Amount.Few, Amount.Few),
                    Encounters.LeaderGroup("E033", "E004", Amount.One, Amount.Pack)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo("E006", Amount.Few),
                    Encounters.Solo("E033", Amount.Few, EnemyMovementProfile.Patrol)
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
                TreasureGold = new(180, 360),
                RoomEncounters =
                [
                    Encounters.Same("E007", Amount.Several, Amount.Several),
                    Encounters.Mixed("E008", Amount.Several, "E007", Amount.Few, Amount.Few),
                    Encounters.LeaderGroup("E012", "E007", Amount.One, Amount.Pack)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo("E008", Amount.Few, EnemyMovementProfile.Patrol),
                    Encounters.Solo("E010", Amount.Few)
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
                TreasureGold = new(240, 480),
                RoomEncounters =
                [
                    Encounters.Same("E009", Amount.Several, Amount.Several),
                    Encounters.Mixed("E027", Amount.Several, "E029", Amount.Several, Amount.Few),
                    Encounters.LeaderGroup("E034", "E009", Amount.Few, Amount.Several)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo("E026", Amount.Several),
                    Encounters.Solo("E029", Amount.Few, EnemyMovementProfile.Patrol)
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
                TreasureGold = new(320, 620),
                RoomEncounters =
                [
                    Encounters.Same("E007", Amount.Several, Amount.Pack),
                    Encounters.Mixed("E008", Amount.Several, "E031", Amount.Few, Amount.Few),
                    Encounters.LeaderGroup("E035", "E007", Amount.Few, Amount.Pack)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo("E031", Amount.Few, EnemyMovementProfile.Patrol),
                    Encounters.Solo("E008", Amount.Few)
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
                TreasureGold = new(420, 800),
                RoomEncounters =
                [
                    Encounters.Same("E015", Amount.Several, Amount.Several),
                    Encounters.Mixed("E037", Amount.Several, "E033", Amount.Several, Amount.Few),
                    Encounters.LeaderGroup("E040", "E015", Amount.Few, Amount.Several)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo("E037", Amount.Few, EnemyMovementProfile.Patrol),
                    Encounters.Solo("E033", Amount.Several)
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
                    Encounters.Same("E012", Amount.Few, Amount.Several),
                    Encounters.Mixed("E013", Amount.Few, "E036", Amount.Few, Amount.Few),
                    Encounters.LeaderGroup("E041", "E012", Amount.One, Amount.Several)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo("E036", Amount.Few, EnemyMovementProfile.Patrol),
                    Encounters.Solo("E014", Amount.Few)
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
                TreasureGold = new(750, 1400),
                RoomEncounters =
                [
                    Encounters.Same("E038", Amount.Several, Amount.Several),
                    Encounters.Mixed("E017", Amount.Few, "E035", Amount.Several, Amount.Few),
                    Encounters.LeaderGroup("E021", "E035", Amount.One, Amount.Pack)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo("E038", Amount.Few, EnemyMovementProfile.Patrol),
                    Encounters.Solo("E017", Amount.Few)
                ]
            }
        };

    public static MazeLevelConfiguration Get(int level)
    {
        if (Configurations.TryGetValue(level, out var configuration)) return configuration;
        var increase = level - 11;
        var tier = Math.Clamp(4 + increase / 3, 4, 5);
        var (leader, follower, peer) = tier switch
        {
            4 => ("E018", "E012", "E017"),
            _ => ("E047", "E023", "E046")
        };
        return new MazeLevelConfiguration
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
        };
    }
}
