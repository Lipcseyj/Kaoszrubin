namespace KaoszRubin;

public static class EnemySearchCoordinator
{
    public static void BeginCoordinatedSearch(IReadOnlyList<Enemy> group, Enemy observer, Random random)
    {
        var active = group.Where(enemy => enemy.CurrentHitPoints > 0).ToArray();
        if (active.Length == 0) return;
        var anchor = observer.LastKnownTargetPosition ?? observer.Position;
        var searchMoves = random.Next(Enemy.MinimumSearchMoves, Enemy.MaximumSearchMoves + 1);
        var scoutCount = active.Length >= 4 ? 2 : 1;
        var scouts = active.OrderByDescending(enemy => enemy.EffectiveSpeed)
            .ThenByDescending(enemy => enemy.GroupRole == EnemyGroupRole.Leader)
            .ThenBy(_ => random.Next()).Take(scoutCount).ToHashSet();
        foreach (var enemy in active)
            enemy.BeginSearch(searchMoves, anchor,
                scouts.Contains(enemy) ? EnemySearchRole.Scout : EnemySearchRole.Guarding);
        foreach (var scout in scouts)
        foreach (var occupiedPosition in active.Select(enemy => enemy.Position))
            scout.RecordSearchVisit(occupiedPosition);
    }
}

public static class EnemySearchNavigator
{
    public static Direction? ChooseScoutDirection(Position current, Position anchor, Direction previousDirection,
        int cohesionRadius, IReadOnlyCollection<Position> searchedPositions,
        IReadOnlyList<Direction> directions, Func<Position, bool> canTraverse, Random random)
    {
        var immediate = directions.Select(direction => (Direction: direction, Position: current + direction))
            .Where(step => WithinRadius(step.Position, anchor, cohesionRadius) && canTraverse(step.Position))
            .ToArray();
        if (immediate.Length == 0) return null;

        var unvisited = immediate.Where(step => !searchedPositions.Contains(step.Position))
            .OrderByDescending(step => UnvisitedExitCount(step.Position, anchor, cohesionRadius,
                searchedPositions, directions, canTraverse))
            .ThenByDescending(step => step.Direction == previousDirection)
            .ThenBy(_ => random.Next()).ToArray();
        if (unvisited.Length > 0) return unvisited[0].Direction;

        var queue = new Queue<(Position Position, Direction FirstDirection)>();
        var reached = new HashSet<Position> { current };
        foreach (var step in immediate)
        {
            if (!reached.Add(step.Position)) continue;
            queue.Enqueue((step.Position, step.Direction));
        }
        while (queue.Count > 0)
        {
            var candidate = queue.Dequeue();
            if (!searchedPositions.Contains(candidate.Position)) return candidate.FirstDirection;
            foreach (var direction in directions)
            {
                var next = candidate.Position + direction;
                if (!WithinRadius(next, anchor, cohesionRadius) || !canTraverse(next) || !reached.Add(next)) continue;
                queue.Enqueue((next, candidate.FirstDirection));
            }
        }

        var reverse = Opposite(previousDirection);
        return immediate.OrderBy(step => step.Direction == reverse)
            .ThenBy(_ => random.Next()).First().Direction;
    }

    private static int UnvisitedExitCount(Position position, Position anchor, int radius,
        IReadOnlyCollection<Position> searchedPositions, IReadOnlyList<Direction> directions,
        Func<Position, bool> canTraverse) => directions.Count(direction =>
    {
        var next = position + direction;
        return WithinRadius(next, anchor, radius) && canTraverse(next) && !searchedPositions.Contains(next);
    });

    private static bool WithinRadius(Position position, Position anchor, int radius) =>
        Math.Abs(position.X - anchor.X) + Math.Abs(position.Y - anchor.Y) <= radius;

    private static Direction Opposite(Direction direction) => direction switch
    {
        Direction.Up => Direction.Down,
        Direction.Down => Direction.Up,
        Direction.Left => Direction.Right,
        Direction.Right => Direction.Left,
        _ => Direction.Right
    };
}
