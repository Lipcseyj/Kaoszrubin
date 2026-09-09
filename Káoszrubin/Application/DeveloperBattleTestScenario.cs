using KaoszRubin.Domain.Combat;
using KaoszRubin.World;

namespace KaoszRubin.Application;

public sealed record DeveloperBattleTestOptions(int PartyLevel, int EnemyGroupCount, int EnemiesPerGroup)
{
    public const int MinimumPartyLevel = 1;
    public const int MaximumEnemyGroupCount = 8;
    public const int MaximumEnemiesPerGroup = 12;

    public void Validate(int maximumPartyLevel)
    {
        if (PartyLevel < MinimumPartyLevel || PartyLevel > maximumPartyLevel)
            throw new ArgumentOutOfRangeException(nameof(PartyLevel));
        if (EnemyGroupCount is < 1 or > MaximumEnemyGroupCount)
            throw new ArgumentOutOfRangeException(nameof(EnemyGroupCount));
        if (EnemiesPerGroup is < 1 or > MaximumEnemiesPerGroup)
            throw new ArgumentOutOfRangeException(nameof(EnemiesPerGroup));
    }
}

public sealed record DeveloperBattleTestScenario(Maze Maze, Position LeaderPosition,
    Position CorridorTopLeft, IReadOnlyList<IReadOnlyList<ConfiguredEnemy>> EnemyGroups,
    IReadOnlyList<TreasureChest> GroupMarkers);

/// <summary>Nyílt, ismételhető harci tesztteret épít a kézi AI- és balanszpróbákhoz.</summary>
public static class DeveloperBattleTestScenarioBuilder
{
    public const int CorridorWidth = 2;
    public const int CorridorLength = 8;
    public const int MinimumEnemyDistance = 10;
    public const int MaximumEnemyDistance = 20;
    private const int GroupsPerRow = 4;
    private const int GroupColumnSpacing = 10;
    private const int GroupRowSpacing = 4;
    private const int GroupWidth = 4;

    public static DeveloperBattleTestScenario Create(int width, int height,
        DeveloperBattleTestOptions options, IReadOnlyList<EnemyDefinition> enemyDefinitions,
        Random random, int maximumPartyLevel)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(enemyDefinitions);
        ArgumentNullException.ThrowIfNull(random);
        options.Validate(maximumPartyLevel);
        if (width < 50 || height < 36)
            throw new ArgumentException("A harci tesztpályához legalább 50×36 cella szükséges.");

        var maze = new Maze(width, height, levelName: $"Harci tesztpálya — L{options.PartyLevel}");
        for (var y = 1; y < height - 1; y++)
        for (var x = 1; x < width - 1; x++)
            maze.Carve(new Position(x, y));
        maze.PlaceExit(new Position(width - 3, height - 3));

        var leaderPosition = new Position(width / 2, height - 9);
        var corridorTopLeft = BuildCentralCorridor(maze, leaderPosition);
        var definitions = SelectEnemyDefinitions(enemyDefinitions, options.PartyLevel);
        var enemyGroups = new List<IReadOnlyList<ConfiguredEnemy>>();
        var markers = new List<TreasureChest>();
        var rowCount = (options.EnemyGroupCount + GroupsPerRow - 1) / GroupsPerRow;

        for (var groupIndex = 0; groupIndex < options.EnemyGroupCount; groupIndex++)
        {
            var row = groupIndex / GroupsPerRow;
            var column = groupIndex % GroupsPerRow;
            var columnsInRow = Math.Min(GroupsPerRow,
                options.EnemyGroupCount - row * GroupsPerRow);
            var occupiedWidth = (columnsInRow - 1) * GroupColumnSpacing + GroupWidth;
            var startX = leaderPosition.X - occupiedWidth / 2 + column * GroupColumnSpacing;
            var startY = leaderPosition.Y - (rowCount == 1 ? 18 : 20) + row * GroupRowSpacing;
            var groupId = $"DEV-TEST-{groupIndex + 1:00}";
            var group = new List<ConfiguredEnemy>();
            for (var memberIndex = 0; memberIndex < options.EnemiesPerGroup; memberIndex++)
            {
                var position = new Position(startX + memberIndex % GroupWidth,
                    startY + memberIndex / GroupWidth);
                var enemy = new ConfiguredEnemy(position, definitions[random.Next(definitions.Count)], random);
                enemy.ConfigureMovement(EnemyMovementProfile.Stationary, Direction.Down);
                enemy.ConfigureGroup(groupId, memberIndex == 0 ? EnemyGroupRole.Leader : EnemyGroupRole.Member);
                enemy.ConfigureAwareness(EnemyAlertness.Alert);
                maze.AddEnemy(enemy);
                group.Add(enemy);
            }

            var markerPosition = group.SelectMany(enemy => Enum.GetValues<Direction>()
                    .Select(direction => enemy.Position + direction))
                .Where(position => maze.IsWalkable(position) &&
                                   maze.GetObjectAt(position) is null &&
                                   position != maze.Entrance && position != maze.Exit)
                .OrderByDescending(position => position.X)
                .ThenBy(position => Math.Abs(position.Y - startY))
                .First();
            var chest = new TreasureChest(markerPosition,
                Math.Max(1, options.PartyLevel * 10));
            maze.AddTreasureChest(chest);
            markers.Add(chest);
            enemyGroups.Add(group);
        }

        return new DeveloperBattleTestScenario(maze, leaderPosition, corridorTopLeft,
            enemyGroups, markers);
    }

    public static int EnemyStrengthTierForPartyLevel(int partyLevel) =>
        Math.Clamp(1 + (Math.Max(1, partyLevel) - 1) / 5, 1, 5);

    private static Position BuildCentralCorridor(Maze maze, Position leaderPosition)
    {
        var topLeft = new Position(leaderPosition.X - 1, leaderPosition.Y - 12);
        var leftWallX = topLeft.X - 1;
        var rightWallX = topLeft.X + CorridorWidth;
        for (var offset = 0; offset < CorridorLength; offset++)
        {
            maze.SetTile(new Position(leftWallX, topLeft.Y + offset), maze.WallRune);
            maze.SetTile(new Position(rightWallX, topLeft.Y + offset), maze.WallRune);
        }
        return topLeft;
    }

    private static IReadOnlyList<EnemyDefinition> SelectEnemyDefinitions(
        IReadOnlyList<EnemyDefinition> definitions, int partyLevel)
    {
        var eligible = definitions.Where(definition => !definition.IsBoss &&
            definition.Rank is EnemyRank.Normal or EnemyRank.Elite &&
            definition.HitPoints > 0).ToArray();
        if (eligible.Length == 0)
            throw new InvalidOperationException("Nincs használható normál vagy elit ellenfél a tesztpályához.");
        var targetTier = EnemyStrengthTierForPartyLevel(partyLevel);
        var exact = eligible.Where(definition => definition.StrengthTier == targetTier).ToArray();
        return exact.Length > 0 ? exact : eligible
            .Where(definition => Math.Abs(definition.StrengthTier - targetTier) ==
                                 eligible.Min(candidate => Math.Abs(candidate.StrengthTier - targetTier)))
            .ToArray();
    }
}
