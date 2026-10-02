using KaoszRubin.Domain;
using KaoszRubin.Domain.Characters;

namespace KaoszRubin.Application;

/// <summary>Az egész csapat helyzetét tervezi, hogy a társak helycseréje ne akassza meg az összeállást.</summary>
public static class PartyFormationAssemblyPlanner
{
    private const int MaximumExploredStates = 100_000;
    private static readonly Direction[] Directions =
        [Direction.Up, Direction.Right, Direction.Down, Direction.Left];

    public sealed record Step(PartyMemberAvatar Member, Position From, Position To,
        PartyMemberAvatar? SwappedMember);

    public sealed record Result(IReadOnlyList<Step> Steps, string? Failure)
    {
        public bool Succeeded => Failure is null;
    }

    public sealed record Placement(PartyFormationSnapshot Formation, Result Plan);

    private sealed record Node(Position[] Positions, int Cost, Node? Previous, Step? Step);

    public static Placement PlanAtLeader(Maze maze, Position leaderPosition, CharacterId leaderId,
        PartyFormationSnapshot formation)
    {
        var clockwise = PartyFormationRules.Rotate(formation, clockwise: true);
        var counterclockwise = PartyFormationRules.Rotate(formation, clockwise: false);
        var opposite = PartyFormationRules.Rotate(clockwise, clockwise: true);
        var first = Plan(maze, leaderPosition, leaderId,
            PartyFormationRules.Positions(formation, leaderId, leaderPosition));
        if (first.Succeeded) return new(formation, first);

        foreach (var candidate in new[] { clockwise, counterclockwise, opposite })
        {
            var plan = Plan(maze, leaderPosition, leaderId,
                PartyFormationRules.Positions(candidate, leaderId, leaderPosition));
            if (plan.Succeeded) return new(candidate, plan);
        }

        return new(formation, first);
    }

    public static Result Plan(Maze maze, Position leaderPosition, CharacterId leaderId,
        IReadOnlyDictionary<CharacterId, Position> targets)
    {
        if (!targets.TryGetValue(leaderId, out var leaderTarget) || leaderTarget != leaderPosition)
            return new([], "A vezér nincs a kijelölt alakzati helyén.");

        var members = maze.PartyMembers.Where(member => member.Character.IsAlive).ToArray();
        var selected = targets.Where(pair => pair.Key != leaderId)
            .Select(pair => (Index: Array.FindIndex(members, member => member.Character.Id == pair.Key),
                Target: pair.Value)).ToArray();
        if (selected.Any(entry => entry.Index < 0))
            return new([], "Egy kijelölt partitag nincs ezen a képernyőn.");
        if (targets.Values.Distinct().Count() != targets.Count ||
            selected.Any(entry => !CanTraverse(maze, entry.Target, leaderPosition)))
            return new([], "Az alakzat egyik célmezője nem járható vagy foglalt.");

        var distances = selected.Select(entry => DistancesFrom(maze, entry.Target, leaderPosition)).ToArray();
        var start = members.Select(member => member.Position).ToArray();
        if (selected.Where((entry, index) => !distances[index].ContainsKey(start[entry.Index])).Any())
            return new([], "Egy partitag nem tud eljutni a kijelölt helyére.");

        bool IsGoal(Position[] positions) => selected.All(entry => positions[entry.Index] == entry.Target);
        int Estimate(Position[] positions)
        {
            var total = 0;
            for (var index = 0; index < selected.Length; index++)
                total += distances[index].GetValueOrDefault(positions[selected[index].Index], maze.Width * maze.Height);
            return (total + 1) / 2;
        }

        var initial = new Node(start, 0, null, null);
        var queue = new PriorityQueue<Node, int>();
        queue.Enqueue(initial, Estimate(start));
        var bestCosts = new Dictionary<string, int> { [Key(start)] = 0 };
        var explored = 0;
        while (queue.TryDequeue(out var current, out _))
        {
            if (bestCosts[Key(current.Positions)] != current.Cost) continue;
            if (IsGoal(current.Positions))
            {
                var steps = new List<Step>();
                for (var node = current; node.Step is { } step; node = node.Previous!) steps.Add(step);
                steps.Reverse();
                return new(steps, null);
            }
            if (++explored > MaximumExploredStates)
                return new([], "Az összeálláshoz nem sikerült biztos útvonalat tervezni. Vidd közelebb a társakat.");

            foreach (var (memberIndex, _) in selected)
            foreach (var direction in Directions)
            {
                var from = current.Positions[memberIndex];
                var to = from + direction;
                if (!CanTraverse(maze, to, leaderPosition)) continue;
                var otherIndex = Array.IndexOf(current.Positions, to);
                var next = (Position[])current.Positions.Clone();
                next[memberIndex] = to;
                if (otherIndex >= 0) next[otherIndex] = from;
                var cost = current.Cost + 1;
                var key = Key(next);
                if (bestCosts.TryGetValue(key, out var previousCost) && previousCost <= cost) continue;
                bestCosts[key] = cost;
                var step = new Step(members[memberIndex], from, to,
                    otherIndex >= 0 ? members[otherIndex] : null);
                queue.Enqueue(new Node(next, cost, current, step), cost + Estimate(next) * 2);
            }
        }

        return new([], "A jelenlegi akadályok mellett az alakzat nem tud összeállni.");
    }

    private static Dictionary<Position, int> DistancesFrom(Maze maze, Position target, Position leaderPosition)
    {
        var distances = new Dictionary<Position, int> { [target] = 0 };
        var queue = new Queue<Position>();
        queue.Enqueue(target);
        while (queue.TryDequeue(out var current))
            foreach (var direction in Directions)
            {
                var next = current + direction;
                if (distances.ContainsKey(next) || !CanTraverse(maze, next, leaderPosition)) continue;
                distances.Add(next, distances[current] + 1);
                queue.Enqueue(next);
            }
        return distances;
    }

    private static bool CanTraverse(Maze maze, Position position, Position leaderPosition)
    {
        if (position == leaderPosition || !maze.IsWalkable(position) ||
            maze.GetEnemyAt(position) is not null ||
            maze.GetTrapAt(position) is { State: TrapState.Detected }) return false;
        var occupant = maze.GetObjectAt(position);
        return occupant is null or PartyMemberAvatar or GroundItemPile or Corpse ||
               Maze.IsPassableNeutralNpc(occupant);
    }

    private static string Key(Position[] positions) =>
        string.Join(';', positions.Select(position => $"{position.X},{position.Y}"));
}
