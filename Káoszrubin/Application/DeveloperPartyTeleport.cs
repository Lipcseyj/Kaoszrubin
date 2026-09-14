namespace KaoszRubin.Application;

internal static class DeveloperPartyTeleport
{
    // A teljes elhelyezést a mozgatás előtt számoljuk ki; a saját társak régi mezői felszabadulnak.
    public static IReadOnlyList<Position> FindDestinations(Maze maze, Position target,
        IReadOnlyCollection<PartyMemberAvatar> companions)
    {
        bool IsFree(Position position) => maze.IsWalkable(position) &&
            position != maze.Exit && maze.GetTrapAt(position) is null &&
            (maze.GetObjectAt(position) is null ||
             maze.GetObjectAt(position) is PartyMemberAvatar avatar && companions.Contains(avatar));
        if (!IsFree(target)) return [];
        var result = new List<Position> { target };
        var visited = new HashSet<Position> { target };
        var queue = new Queue<Position>();
        queue.Enqueue(target);
        while (queue.TryDequeue(out var current) && result.Count <= companions.Count)
        {
            foreach (var direction in Enum.GetValues<Direction>())
            {
                var next = current + direction;
                if (!visited.Add(next) || !IsFree(next)) continue;
                result.Add(next);
                if (result.Count == companions.Count + 1) return result;
                queue.Enqueue(next);
            }
        }
        return result.Count == companions.Count + 1 ? result : [];
    }
}
