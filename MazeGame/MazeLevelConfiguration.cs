using MazeGame.Domain.Combat;

namespace MazeGame;

public sealed record IntRange(int Minimum, int Maximum)
{
    public int Roll(Random random) => random.Next(Minimum, Maximum + 1);
}

public enum Amount { One, Few, Several, Band, Many }

public static class AmountRanges
{
    public static IntRange Range(this Amount amount) => amount switch
    {
        Amount.One => new(1, 1),
        Amount.Few => new(1, 2),
        Amount.Several => new(3, 5),
        Amount.Band => new(6, 9),
        Amount.Many => new(10, 14),
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
    public double DoubleWidthCorridorChance { get; init; } = 0.80;
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
        TreasureGoldRange = TreasureGold
    };
}

public static class MazeLevelConfigurations
{
    private static readonly IReadOnlyDictionary<int, MazeLevelConfiguration> Configurations =
        new Dictionary<int, MazeLevelConfiguration>
        {
            [1] = new()
            {
                DoubleWidthCorridorChance = 0.95,
                Level = 1,
                RoomCount = Amount.Several.Range(),
                RoomSize = new(3, 5),
                TreasureChestCount = Amount.Few.Range(),
                TreasureGold = new(10, 50),
                RoomEncounters =
                [
                    Encounters.Same("E001", Amount.Several, Amount.Several),
                    Encounters.Same("E002", Amount.Few, Amount.Several),
                    Encounters.Same("E003", Amount.Few, Amount.Few)
                ],
                CorridorEncounters =
                [
                    Encounters.Solo("E001", Amount.Few),
                    Encounters.Solo("E002", Amount.Few)
                ]
            },
            [2] = new()
            {
                Level = 2,
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
                    Encounters.Solo("E005", Amount.Few, EnemyMovementProfile.Patrol)
                ]
            },
            [3] = new()
            {
                Level = 3,
                DoubleWidthCorridorChance = 0.70,
                RoomCount = Amount.Band.Range(),
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
            }
        };

    public static MazeLevelConfiguration Get(int level)
    {
        if (Configurations.TryGetValue(level, out var configuration)) return configuration;
        var increase = level - 3;
        var tier = Math.Clamp(2 + increase / 3, 2, 5);
        var (leader, follower, peer) = tier switch
        {
            2 => ("E008", "E003", "E009"),
            3 => ("E014", "E007", "E013"),
            4 => ("E018", "E012", "E017"),
            _ => ("E047", "E023", "E046")
        };
        return new MazeLevelConfiguration
        {
            Level = level,
            DoubleWidthCorridorChance = Math.Max(0.50, 0.70 - increase * 0.03),
            RoomCount = new(6 + increase / 2, 9 + increase / 2),
            RoomSize = new(4, Math.Min(9, 7 + increase / 3)),
            TreasureChestCount = new(4 + increase, 6 + increase),
            TreasureGold = new(70 + increase * 35, 150 + increase * 60),
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
