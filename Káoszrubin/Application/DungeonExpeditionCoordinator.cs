using KaoszRubin.Data;
using KaoszRubin.Domain;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;

namespace KaoszRubin.Application;

public sealed record ExpeditionEnemyTemplate(string DefinitionId, Position Position,
    EnemyMovementProfile MovementProfile, Direction PatrolDirection, string? GroupId, EnemyGroupRole GroupRole);

public sealed class DungeonExpeditionCoordinator
{
    private readonly GameDataCatalog _gameData;
    private readonly Random _random;

    public DungeonExpeditionCoordinator(GameDataCatalog gameData, Random random)
    {
        _gameData = gameData;
        _random = random;
    }

    public static void CaptureExpeditionEnemyTemplates(
        List<ExpeditionEnemyTemplate> templates,
        Maze maze,
        GameDataCatalog gameData)
    {
        templates.Clear();
        templates.AddRange(maze.Enemies.Where(enemy => !enemy.Definition.IsBoss &&
            enemy.GroupId?.StartsWith("QUEST:", StringComparison.OrdinalIgnoreCase) != true).Select(enemy =>
            new ExpeditionEnemyTemplate(enemy.Definition.Id, enemy.Position, enemy.MovementProfile,
                enemy.PatrolDirection, enemy.GroupId, enemy.GroupRole)));
        templates.AddRange(maze.Corpses.OfType<MonsterCorpse>()
            .Where(corpse => !gameData.GetEnemy(corpse.EnemyDefinitionId).IsBoss &&
                             corpse.GuaranteedLootIds.Count == 0)
            .Select(corpse => new ExpeditionEnemyTemplate(corpse.EnemyDefinitionId, corpse.Position,
                EnemyMovementProfile.Wander, Direction.Right, null, EnemyGroupRole.Member)));
    }

    public void ReplenishExpeditionEnemies(
        List<ExpeditionEnemyTemplate> templates,
        Maze maze)
    {
        var currentNormalCount = maze.Enemies.Count(enemy => !enemy.Definition.IsBoss &&
            enemy.GroupId?.StartsWith("QUEST:", StringComparison.OrdinalIgnoreCase) != true);
        var needed = ReturnExpeditionRules.AdditionalEnemiesNeeded(templates.Count, currentNormalCount);
        var candidates = templates.OrderBy(_ => _random.Next()).ToList();
        foreach (var template in candidates)
        {
            if (needed <= 0) break;
            var position = FindExpeditionSpawnPosition(maze, template.Position);
            if (position is null) continue;
            var enemy = new ConfiguredEnemy(position.Value, _gameData.GetEnemy(template.DefinitionId));
            enemy.ConfigureMovement(template.MovementProfile, template.PatrolDirection);
            enemy.ConfigureGroup(template.GroupId, template.GroupRole);
            maze.AddEnemy(enemy);
            needed--;
        }
    }

    public static Position? FindExpeditionSpawnPosition(Maze maze, Position preferred)
    {
        var positions = new List<Position> { preferred };
        for (var radius = 1; radius <= 5; radius++)
            for (var y = preferred.Y - radius; y <= preferred.Y + radius; y++)
                for (var x = preferred.X - radius; x <= preferred.X + radius; x++)
                    if (Math.Max(Math.Abs(x - preferred.X), Math.Abs(y - preferred.Y)) == radius)
                        positions.Add(new Position(x, y));
        return positions.Where(position => maze.IsInside(position) && maze.IsWalkable(position) &&
                position != maze.Entrance && position != maze.Exit && maze.GetObjectAt(position) is null &&
                maze.GetDoorAt(position) is null && maze.GetTrapAt(position) is null)
            .Select(position => (Position?)position).FirstOrDefault();
    }
}
