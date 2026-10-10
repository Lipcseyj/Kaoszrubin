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
        foreach (var group in encounters.GroupBy(encounter => QuestChestPlacement.FindRoomArea(level, encounter.RoomId)))
            Place(group.Key.Maze, data, group, random, magicWeaponContext);
    }

    public static void Place(Maze maze, GameDataCatalog data,
        IEnumerable<QuestRoomEnemyEncounterConfiguration> encounters, Random random,
        EnemyMagicWeaponContext magicWeaponContext)
    {
        foreach (var group in encounters.GroupBy(encounter => encounter.RoomId))
        {
            var room = maze.GetRoomByContentId(group.Key) ??
                throw new InvalidOperationException($"A különleges szoba nem található: '{group.Key}'.");
            var center = new Position(room.TopLeft.X + room.Width / 2, room.TopLeft.Y + room.Height / 2);
            var entry = maze.Doors
                .OrderBy(door => room.InteriorPositions().Min(position => Distance(position, door.Position)))
                .ThenBy(door => Distance(door.Position, maze.Entrance))
                .Select(door => door.Position).FirstOrDefault(center);
            var positions = room.InteriorPositions().Where(position => maze.IsWalkable(position) &&
                    maze.GetObjectAt(position) is null && maze.GetTrapAt(position) is null &&
                    maze.Doors.All(door => Distance(door.Position, position) > 1))
                .ToList();
            var reserve = CampaignBosses.IsBossRoom(group.Key) ? 6 : 0;
            if (positions.Count < group.Sum(encounter => encounter.Count) + reserve)
                throw new InvalidOperationException($"A(z) '{group.Key}' szobában nincs hely az őrségnek és a partinak.");

            // A bejárat közelében helyet hagyunk a hatfős parti felállásának.
            var approach = positions.OrderBy(position => Distance(position, entry)).Take(reserve).ToHashSet();
            positions.RemoveAll(approach.Contains);
            var dx = center.X - entry.X;
            var dy = center.Y - entry.Y;
            int Depth(Position position) => (position.X - entry.X) * dx + (position.Y - entry.Y) * dy;
            int Flank(Position position) => Math.Abs((position.X - center.X) * dy - (position.Y - center.Y) * dx);
            var placements = new List<(QuestRoomEnemyEncounterConfiguration Encounter, Position Position)>();
            foreach (var encounter in group.OrderBy(encounter =>
                         data.GetEnemy(encounter.EnemyId).Rank >= EnemyRank.MiniBoss ? 0 :
                         encounter.Role == EnemyGroupRole.Leader ? 1 : 2))
            {
                var definition = data.GetEnemy(encounter.EnemyId);
                var leader = definition.Rank >= EnemyRank.MiniBoss || encounter.Role == EnemyGroupRole.Leader;
                var caster = definition.SpellcasterProfile is not null;
                var ranged = definition.Weapons?.Any(weapon => weapon.IsRanged) == true;
                IEnumerable<Position> ordered = leader
                    ? positions.OrderBy(position => Distance(position, center))
                    : caster ? positions.OrderByDescending(Depth).ThenBy(position => Distance(position, center))
                    : ranged ? positions.OrderByDescending(Flank).ThenByDescending(Depth)
                    : positions.OrderBy(Depth).ThenBy(position => Distance(position, center));
                var selected = ordered.Take(encounter.Count).ToArray();
                foreach (var position in selected) placements.Add((encounter, position));
                positions.RemoveAll(selected.Contains);
            }
            // Csak a teljes, ellenőrzött hadrendet tesszük a térképre.
            foreach (var (encounter, position) in placements)
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

    private static int Distance(Position first, Position second) =>
        Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y);
}
