using KaoszRubin.Domain;
using KaoszRubin.Domain.Characters;

namespace KaoszRubin.Application;

public static class NpcChestOpeningController
{
    private static readonly Direction[] Directions =
        [Direction.Up, Direction.Right, Direction.Down, Direction.Left];

    public static PartyMemberAvatar ChooseOpener(IReadOnlyList<PartyMemberAvatar> candidates, Random random)
    {
        if (candidates.Count == 0) throw new ArgumentException("Nincs ládanyitásra alkalmas társ.", nameof(candidates));
        var totalWeight = candidates.Sum(Weight);
        var roll = random.Next(totalWeight);
        foreach (var candidate in candidates)
        {
            roll -= Weight(candidate);
            if (roll < 0) return candidate;
        }
        throw new InvalidOperationException("Érvénytelen ládanyitó-sorsolás.");
    }

    public static IReadOnlyList<Position>? FindPath(Maze maze, Position start, Position chestPosition,
        Position leaderPosition)
    {
        if (start == chestPosition) return [];
        var previous = new Dictionary<Position, Position>();
        var visited = new HashSet<Position> { start };
        var queue = new Queue<Position>();
        queue.Enqueue(start);
        while (queue.TryDequeue(out var current))
        {
            foreach (var direction in Directions)
            {
                var next = current + direction;
                if (!visited.Add(next) || !CanPlanThrough(maze, next, chestPosition, leaderPosition)) continue;
                previous[next] = current;
                if (next == chestPosition)
                {
                    var path = new List<Position>();
                    for (var position = next; position != start; position = previous[position])
                        path.Add(position);
                    path.Reverse();
                    return path;
                }
                queue.Enqueue(next);
            }
        }
        return null;
    }

    private static int Weight(PartyMemberAvatar member) =>
        CharacterClassRules.IsThief(member.Character.CharacterClass.Id) ? 3 : 2;

    private static bool CanPlanThrough(Maze maze, Position position, Position chestPosition,
        Position leaderPosition)
    {
        if (position == leaderPosition || !maze.IsWalkable(position) ||
            maze.GetEnemyAt(position) is not null ||
            maze.GetTrapAt(position) is { IsActive: true, State: TrapState.Detected }) return false;
        var occupant = maze.GetObjectAt(position);
        return position == chestPosition
            ? occupant is TreasureChest
            : occupant is null or PartyMemberAvatar or GroundItemPile or Corpse ||
              Maze.IsPassableNeutralNpc(occupant);
    }
}
