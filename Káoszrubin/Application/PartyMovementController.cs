using KaoszRubin.Domain;
using KaoszRubin.Domain.Characters;

namespace KaoszRubin.Application;

public sealed class PartyMovementController
{
    private static readonly Direction[] Directions = [Direction.Up, Direction.Right, Direction.Down, Direction.Left];

    public static Position? ChoosePartyMemberStep(
        PartyMemberAvatar member,
        Maze maze,
        Player player,
        Direction leaderFacing,
        IReadOnlyList<Position> leaderTrail,
        int currentLevelVisionModifier)
    {
        var behavior = member.Character.NpcBehavior ?? NpcBehavior.Defensive;
        var visibleEnemy = maze.Enemies
            .Where(enemy => FogOfWar.CanSee(maze, member.Position, enemy.Position,
                CharacterClassRules.VisionRange(member.Character, currentLevelVisionModifier)))
            .OrderBy(enemy => Manhattan(member.Position, enemy.Position))
            .FirstOrDefault();

        if (behavior == NpcBehavior.Aggressive && visibleEnemy is not null)
        {
            if (Manhattan(member.Position, visibleEnemy.Position) == 1) return null;
            return FindNextStep(member, FreeNeighborsOf(maze, player, visibleEnemy.Position), maze, player);
        }

        if (behavior == NpcBehavior.Defensive && visibleEnemy is not null)
        {
            if (Manhattan(member.Position, visibleEnemy.Position) == 1) return null;
            return FindNextStep(member, FreeNeighborsOf(maze, player, visibleEnemy.Position), maze, player);
        }

        if (behavior == NpcBehavior.Scout)
        {
            if (visibleEnemy is not null)
                return FollowLeaderTrail(member, minimumLag: 2, maze, player, leaderTrail);
            return ChooseForwardStep(member, maximumLeaderDistance: 10, maximumSearchDistance: 10, avoidNarrowFront: false, maze, player, leaderFacing)
                ?? FollowLeaderTrail(member, minimumLag: 2, maze, player, leaderTrail);
        }

        if (behavior == NpcBehavior.Cautious)
            return FollowLeaderTrail(member, minimumLag: 2, maze, player, leaderTrail);

        if (behavior == NpcBehavior.Aggressive)
            return ChooseForwardStep(member, maximumLeaderDistance: 3, maximumSearchDistance: 4, avoidNarrowFront: true, maze, player, leaderFacing)
                ?? FollowLeaderTrail(member, minimumLag: 2, maze, player, leaderTrail);

        return FollowLeaderTrail(member, minimumLag: 2, maze, player, leaderTrail);
    }

    public static Position? FollowLeaderTrail(
        PartyMemberAvatar member,
        int minimumLag,
        Maze maze,
        Player player,
        IReadOnlyList<Position> leaderTrail)
    {
        if (leaderTrail.Count == 0) return null;
        var partyOrder = Enumerable.Range(0, maze.PartyMembers.Count)
            .FirstOrDefault(index => maze.PartyMembers[index] == member);
        var formationLag = Math.Min(1, partyOrder);
        var targetIndex = Math.Max(0, leaderTrail.Count - 1 - minimumLag - formationLag);
        for (var index = targetIndex; index >= 0; index--)
        {
            var target = leaderTrail[index];
            if (target == member.Position) return null;
            if (!CanPartyTraverse(member, target, maze, player)) continue;
            return FindNextStep(member, [target], maze, player);
        }
        return null;
    }

    public static Position? ChooseForwardStep(
        PartyMemberAvatar member,
        int maximumLeaderDistance,
        int maximumSearchDistance,
        bool avoidNarrowFront,
        Maze maze,
        Player player,
        Direction leaderFacing)
    {
        var forward = DirectionOffset(leaderFacing);
        var reachable = FindReachablePositions(member, maximumSearchDistance, maze, player)
            .Where(entry => Manhattan(entry.Position, player.Position) <= maximumLeaderDistance)
            .Select(entry => new
            {
                entry.Position,
                entry.Distance,
                Progress = (entry.Position.X - player.Position.X) * forward.X + (entry.Position.Y - player.Position.Y) * forward.Y
            })
            .Where(entry => entry.Progress > 0)
            .Where(entry => !avoidNarrowFront || CountWalkableNeighbors(entry.Position, maze) >= 3)
            .OrderByDescending(entry => entry.Progress)
            .ThenBy(entry => entry.Distance)
            .FirstOrDefault();
        if (reachable is null) return null;
        var step = FindNextStep(member, [reachable.Position], maze, player);
        if (avoidNarrowFront && step is { } narrowStep && IsAheadOfLeader(narrowStep, player.Position, leaderFacing) && CountWalkableNeighbors(narrowStep, maze) <= 2)
            return null;
        return step;
    }

    public static Position? FindNextStep(
        PartyMemberAvatar member,
        IEnumerable<Position> targetPositions,
        Maze maze,
        Player player)
    {
        var targets = targetPositions.Where(position => CanPartyTraverse(member, position, maze, player)).ToHashSet();
        if (targets.Count == 0 || targets.Contains(member.Position)) return null;
        var visited = new HashSet<Position> { member.Position };
        var queue = new Queue<(Position Position, Position FirstStep)>();
        foreach (var direction in Directions)
        {
            var next = member.Position + direction;
            if (!CanPartyTraverse(member, next, maze, player) || !visited.Add(next)) continue;
            queue.Enqueue((next, next));
        }
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (targets.Contains(current.Position)) return current.FirstStep;
            foreach (var direction in Directions)
            {
                var next = current.Position + direction;
                if (!CanPartyTraverse(member, next, maze, player) || !visited.Add(next)) continue;
                queue.Enqueue((next, current.FirstStep));
            }
        }
        return null;
    }

    public static IReadOnlyList<(Position Position, int Distance)> FindReachablePositions(
        PartyMemberAvatar member,
        int maximumDistance,
        Maze maze,
        Player player)
    {
        var result = new List<(Position, int)> { (member.Position, 0) };
        var visited = new HashSet<Position> { member.Position };
        var queue = new Queue<(Position Position, int Distance)>();
        queue.Enqueue((member.Position, 0));
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.Distance >= maximumDistance) continue;
            foreach (var direction in Directions)
            {
                var next = current.Position + direction;
                if (!CanPartyTraverse(member, next, maze, player) || !visited.Add(next)) continue;
                var distance = current.Distance + 1;
                result.Add((next, distance));
                queue.Enqueue((next, distance));
            }
        }
        return result;
    }

    public static IEnumerable<Position> FreeNeighborsOf(Maze maze, Player player, Position origin) => Directions
        .Select(direction => origin + direction)
        .Where(position => maze.IsWalkable(position) && position != player.Position &&
                           (maze.GetObjectAt(position) is null or GroundItemPile or Corpse ||
                            Maze.IsPassableNeutralNpc(maze.GetObjectAt(position))));

    public static bool CanPartyTraverse(PartyMemberAvatar member, Position position, Maze maze, Player player)
    {
        if (!maze.IsWalkable(position) || position == player.Position) return false;
        var occupant = maze.GetObjectAt(position);
        return occupant is null or GroundItemPile or Corpse || occupant == member ||
               Maze.IsPassableNeutralNpc(occupant);
    }

    public static int CountWalkableNeighbors(Position position, Maze maze) =>
        Directions.Count(direction => maze.IsWalkable(position + direction));

    public static bool IsAheadOfLeader(Position position, Position playerPosition, Direction leaderFacing)
    {
        var forward = DirectionOffset(leaderFacing);
        return (position.X - playerPosition.X) * forward.X + (position.Y - playerPosition.Y) * forward.Y > 0;
    }

    public static Position? FindNextFormationAssemblyStep(
        PartyMemberAvatar member,
        Position target,
        IReadOnlyDictionary<CharacterId, Position> formationTargets,
        Maze maze,
        Player player)
    {
        if (!CanFormationAssemblyTraverse(member, target, formationTargets, maze, player) || member.Position == target)
            return null;
        var visited = new HashSet<Position> { member.Position };
        var queue = new Queue<(Position Position, Position FirstStep)>();
        foreach (var direction in Directions)
        {
            var next = member.Position + direction;
            if (!CanFormationAssemblyTraverse(member, next, formationTargets, maze, player) || !visited.Add(next)) continue;
            queue.Enqueue((next, next));
        }
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.Position == target) return current.FirstStep;
            foreach (var direction in Directions)
            {
                var next = current.Position + direction;
                if (!CanFormationAssemblyTraverse(member, next, formationTargets, maze, player) || !visited.Add(next)) continue;
                queue.Enqueue((next, current.FirstStep));
            }
        }
        return null;
    }

    public static bool CanFormationAssemblyTraverse(
        PartyMemberAvatar member,
        Position position,
        IReadOnlyDictionary<CharacterId, Position> formationTargets,
        Maze maze,
        Player player)
    {
        if (!maze.IsWalkable(position) || position == player.Position || maze.GetEnemyAt(position) is not null)
            return false;
        var occupant = maze.GetObjectAt(position);
        if (occupant is null or GroundItemPile or Corpse || occupant == member || Maze.IsPassableNeutralNpc(occupant))
            return true;
        if (occupant is not PartyMemberAvatar friend) return false;
        return !formationTargets.TryGetValue(friend.Character.Id, out var friendTarget) ||
               friendTarget != friend.Position;
    }

    public static int Manhattan(Position first, Position second) =>
        Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y);

    public static (int X, int Y) DirectionOffset(Direction direction) => direction switch
    {
        Direction.Up => (0, -1),
        Direction.Down => (0, 1),
        Direction.Left => (-1, 0),
        _ => (1, 0)
    };
}
