using KaoszRubin.Data;
using KaoszRubin.Domain.Combat;
using KaoszRubin.World;

namespace KaoszRubin.Infrastructure.Quests;

public static class QuestRoomEnemyPlacement
{
    public static void Place(DungeonLevel level, GameDataCatalog data,
        IReadOnlyList<QuestRoomEnemyEncounterConfiguration> encounters, Random random,
        EnemyMagicWeaponContext magicWeaponContext)
    {
        var groups = encounters.GroupBy(encounter => QuestChestPlacement.FindRoomArea(level, encounter.RoomId))
            .ToArray();
        foreach (var group in groups)
            Place(group.Key.Maze, data, group, random, magicWeaponContext);
    }

    public static void Place(Maze maze, GameDataCatalog data,
        IEnumerable<QuestRoomEnemyEncounterConfiguration> encounters, Random random,
        EnemyMagicWeaponContext magicWeaponContext)
    {
        static int Distance(Position first, Position second) =>
            Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y);
        foreach (var encounter in encounters)
        {
            var room = maze.GetRoomByContentId(encounter.RoomId) ??
                throw new InvalidOperationException($"A quest room nem található: '{encounter.RoomId}'.");
            var center = new Position(room.TopLeft.X + room.Width / 2, room.TopLeft.Y + room.Height / 2);
            var positions = room.InteriorPositions().Where(position => maze.IsWalkable(position) &&
                    maze.GetObjectAt(position) is null && maze.GetTrapAt(position) is null &&
                    maze.Doors.All(door => Distance(door.Position, position) > 1))
                .OrderBy(position => Distance(position, center)).Take(encounter.Count).ToArray();
            if (positions.Length < encounter.Count)
                throw new InvalidOperationException($"A(z) '{encounter.RoomId}' quest roomban nincs hely " +
                                                    $"{encounter.Count} ellenfélnek.");
            foreach (var position in positions)
            {
                var enemy = new ConfiguredEnemy(position, data.GetEnemy(encounter.EnemyId), random,
                    magicWeaponContext: magicWeaponContext);
                enemy.ConfigureMovement(EnemyMovementProfile.Stationary, Direction.Right);
                enemy.ConfigureGroup($"QUEST:{encounter.RoomId}", encounter.Role);
                if (encounter.GuaranteedItemId is { } itemId) enemy.ConfigureGuaranteedLoot([itemId]);
                maze.AddEnemy(enemy);
            }
        }
    }
}
