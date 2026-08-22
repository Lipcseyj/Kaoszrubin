using MazeGame.Domain.Combat;

namespace MazeGame;

/// <summary>Egy zárt, egész darabszámtartomány a pályaszint konfigurációjához.</summary>
public sealed record IntRange(int Minimum, int Maximum)
{
    public int Roll(Random random) => random.Next(Minimum, Maximum + 1);
}

/// <summary>Egy ellenféltípus pályánkénti, véletlenített darabszáma.</summary>
public sealed record EnemySpawnConfiguration(string EnemyId, IntRange Count);
public sealed record ResolvedEnemySpawn(EnemyDefinition Definition, int Count);

/// <summary>Egy labirintusszint teljes hangolása: forma, szobák, jutalmak és ellenfelek.</summary>
public sealed class MazeLevelConfiguration
{
    public required int Level { get; init; }
    public double DoubleWidthCorridorChance { get; init; } = 0.80;
    public required IntRange RoomCount { get; init; }
    public required IntRange RoomSize { get; init; }
    public required IntRange TreasureChestCount { get; init; }
    public required IntRange TreasureGold { get; init; }
    public required IReadOnlyList<EnemySpawnConfiguration> EnemySpawns { get; init; }

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

/// <summary>A bővítés elsődleges helye: itt adhatók meg az egyes labirintusszintek tartományai.</summary>
public static class MazeLevelConfigurations
{
    private static readonly IReadOnlyDictionary<int, MazeLevelConfiguration> Configurations = new Dictionary<int, MazeLevelConfiguration>
    {
        [1] = new()
        {
            Level = 1,
            DoubleWidthCorridorChance = 0.80,
            RoomCount = new IntRange(10, 10), //3,4
            RoomSize = new IntRange(4, 8),  //2,4
            TreasureChestCount = new IntRange(2, 3),
            TreasureGold = new IntRange(10, 50),
            EnemySpawns =
            [
                new("E001", new IntRange(12, 14)), // kevés óriáspatkány
                new("E002", new IntRange(13, 15)), // közepes koboldcsapat
                new("E003", new IntRange(1, 2))  // kevés goblin
            ]
        },
        [2] = new()
        {
            Level = 2,
            DoubleWidthCorridorChance = 0.75,
            RoomCount = new IntRange(4, 5),
            RoomSize = new IntRange(2, 5),
            TreasureChestCount = new IntRange(3, 5),
            TreasureGold = new IntRange(35, 90),
            EnemySpawns =
            [
                new("E001", new IntRange(1, 3)),
                new("E002", new IntRange(3, 5)),
                new("E003", new IntRange(2, 4)),
                new("E004", new IntRange(1, 2))
            ]
        },
        [3] = new()
        {
            Level = 3,
            DoubleWidthCorridorChance = 0.70,
            RoomCount = new IntRange(5, 6),
            RoomSize = new IntRange(3, 6),
            TreasureChestCount = new IntRange(4, 6),
            TreasureGold = new IntRange(70, 150),
            EnemySpawns =
            [
                new("E002", new IntRange(2, 4)),
                new("E003", new IntRange(3, 5)),
                new("E004", new IntRange(2, 4)),
                new("E005", new IntRange(1, 3))
            ]
        }
    };

    /// <summary>A 3. szint után az utolsó konfiguráció fokozatosan nő, amíg külön szintet nem definiálsz.</summary>
    public static MazeLevelConfiguration Get(int level)
    {
        if (Configurations.TryGetValue(level, out var configuration)) return configuration;
        var baseConfiguration = Configurations[3];
        var increase = level - 3;
        return new MazeLevelConfiguration
        {
            Level = level,
            DoubleWidthCorridorChance = Math.Max(0.50, baseConfiguration.DoubleWidthCorridorChance - increase * 0.03),
            RoomCount = new IntRange(baseConfiguration.RoomCount.Minimum + increase / 2, baseConfiguration.RoomCount.Maximum + increase / 2),
            RoomSize = baseConfiguration.RoomSize,
            TreasureChestCount = new IntRange(baseConfiguration.TreasureChestCount.Minimum + increase, baseConfiguration.TreasureChestCount.Maximum + increase),
            TreasureGold = new IntRange(baseConfiguration.TreasureGold.Minimum + increase * 35, baseConfiguration.TreasureGold.Maximum + increase * 60),
            EnemySpawns = baseConfiguration.EnemySpawns.Select(spawn => new EnemySpawnConfiguration(
                spawn.EnemyId, new IntRange(spawn.Count.Minimum + increase / 2, spawn.Count.Maximum + increase))).ToList()
        };
    }
}
