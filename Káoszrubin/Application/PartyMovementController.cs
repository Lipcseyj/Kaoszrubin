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
        int currentLevelVisionModifier,
        int followOrder = 0,
        bool leaderIdle = false)
    {
        var behavior = member.Character.NpcBehavior ?? NpcBehavior.Defensive;
        var travelDirection = RecentTravelDirection(leaderTrail, player.Position, leaderFacing);
        var visibleEnemy = maze.Enemies
            .Where(enemy => FogOfWar.CanSee(maze, member.Position, enemy.Position,
                CharacterClassRules.VisionRange(member.Character, currentLevelVisionModifier)))
            .OrderBy(enemy => Manhattan(member.Position, enemy.Position))
            .FirstOrDefault();

        // Megállás után a régi nyomvonal ne húzza vissza a társakat a vezér mellé.
        var enemyNeedsResponse = visibleEnemy is not null && (behavior switch
        {
            NpcBehavior.Aggressive => true,
            NpcBehavior.Rearguard => !IsAheadOfLeader(visibleEnemy.Position, player.Position, leaderFacing) &&
                Manhattan(visibleEnemy.Position, player.Position) <= 5,
            NpcBehavior.Cautious or NpcBehavior.Scout =>
                Manhattan(visibleEnemy.Position, member.Position) <= 3,
            _ => Manhattan(visibleEnemy.Position, player.Position) <= 3
        });
        if (leaderIdle && !enemyNeedsResponse &&
            behavior is not (NpcBehavior.Aggressive or NpcBehavior.Scout) &&
            Manhattan(member.Position, player.Position) <= 3)
            return Manhattan(member.Position, player.Position) == 1
                ? ChooseStepAwayFromLeader(member, maze, player, travelDirection)
                : null;

        Position? step = null;
        if (behavior == NpcBehavior.Aggressive && visibleEnemy is not null)
        {
            if (Manhattan(member.Position, visibleEnemy.Position) == 1) return null;
            step = FindNextStep(member, FreeNeighborsOf(maze, player, visibleEnemy.Position)
                .Where(position => Manhattan(position, player.Position) <= 6), maze, player);
        }
        if (behavior == NpcBehavior.Defensive && visibleEnemy is not null &&
            Manhattan(visibleEnemy.Position, player.Position) <= 3)
        {
            if (Manhattan(member.Position, visibleEnemy.Position) == 1) return null;
            step = FindNextStep(member, FreeNeighborsOf(maze, player, visibleEnemy.Position)
                .Where(position => Manhattan(position, player.Position) <= 3), maze, player);
        }
        if (behavior == NpcBehavior.Bodyguard && visibleEnemy is not null &&
            Manhattan(visibleEnemy.Position, player.Position) <= 2)
        {
            if (Manhattan(member.Position, visibleEnemy.Position) == 1) return null;
            step = FindNextStep(member, FreeNeighborsOf(maze, player, visibleEnemy.Position)
                .Where(position => Manhattan(position, player.Position) <= 2), maze, player);
        }
        if (step is null && behavior == NpcBehavior.Rearguard && visibleEnemy is not null &&
            !IsAheadOfLeader(visibleEnemy.Position, player.Position, leaderFacing) &&
            Manhattan(visibleEnemy.Position, player.Position) <= 5)
            step = FindNextStep(member, FreeNeighborsOf(maze, player, visibleEnemy.Position)
                .Where(position => Manhattan(position, player.Position) <= 5), maze, player);
        if (step is null && behavior is (NpcBehavior.Cautious or NpcBehavior.Scout) && visibleEnemy is not null &&
            Manhattan(member.Position, visibleEnemy.Position) <= 3)
            return Directions.Select(direction => member.Position + direction)
                .Where(position => CanPartyTraverse(member, position, maze, player))
                .Where(position => Manhattan(position, visibleEnemy.Position) > Manhattan(member.Position, visibleEnemy.Position))
                .Where(position => PreservesLeaderExit(member, position, maze, player))
                .OrderBy(position => Manhattan(position, player.Position))
                .Select(position => (Position?)position)
                .FirstOrDefault();
        if (step is null && behavior == NpcBehavior.Scout)
            step = ChooseForwardStep(member, maximumLeaderDistance: 9, maximumSearchDistance: 13,
                avoidNarrowFront: false, maze, player, travelDirection,
                visibleEnemy?.Position, minimumEnemyDistance: 4);
        if (step is null && behavior == NpcBehavior.Aggressive && visibleEnemy is null)
            step = ChooseForwardStep(member, maximumLeaderDistance: 5, maximumSearchDistance: 9,
                avoidNarrowFront: false, maze, player, travelDirection);
        if (step is null && behavior is (NpcBehavior.Aggressive or NpcBehavior.Scout) &&
            IsAheadOfLeader(member.Position, player.Position, travelDirection) &&
            Manhattan(member.Position, player.Position) <= (behavior == NpcBehavior.Scout ? 9 : 5))
            return null;
        step ??= FollowLeaderTrail(member, behavior switch
        {
            NpcBehavior.Bodyguard => 1,
            NpcBehavior.Cautious or NpcBehavior.Rearguard => 3,
            _ => 2
        }, maze, player, leaderTrail, followOrder);
        if (step is { } destination && !PreservesLeaderExit(member, destination, maze, player)) return null;
        return step;
    }

    public static Position? ChooseStepAwayFromLeader(PartyMemberAvatar member, Maze maze, Player player,
        Direction leaderFacing)
    {
        var forward = DirectionOffset(leaderFacing);
        return Directions.Select(direction => member.Position + direction)
            .Where(position => CanPartyTraverse(member, position, maze, player) &&
                               Manhattan(position, player.Position) > Manhattan(member.Position, player.Position))
            .Where(position => PreservesLeaderExit(member, position, maze, player))
            .OrderBy(position => (position.X - player.Position.X) * forward.X +
                                 (position.Y - player.Position.Y) * forward.Y)
            .Select(position => (Position?)position)
            .FirstOrDefault();
    }

    /// <summary>A kért gyülekező a vezér utolsó szomszédos helyét is elfoglalhatja.</summary>
    public static Position? ChooseRegroupStep(PartyMemberAvatar member, Maze maze, Player player,
        IReadOnlyList<Position> leaderTrail)
    {
        if (Manhattan(member.Position, player.Position) <= 1) return null;
        return FindNextStep(member, FreeNeighborsOf(maze, player, player.Position), maze, player)
            ?? FollowLeaderTrail(member, minimumLag: 1, maze, player, leaderTrail);
    }

    public static bool PreservesLeaderExit(PartyMemberAvatar member, Position destination, Maze maze, Player player)
    {
        if (Manhattan(destination, player.Position) != 1) return true;
        return Directions.Any(direction =>
        {
            var neighbor = player.Position + direction;
            if (neighbor == destination || !maze.IsWalkable(neighbor) || HasBlockingTrap(maze, neighbor) ||
                maze.GetEnemyAt(neighbor) is not null) return false;
            if (neighbor == member.Position) return true;
            var occupant = maze.GetObjectAt(neighbor);
            return occupant is null or GroundItemPile or Corpse or ForestInn || Maze.IsPassableNeutralNpc(occupant);
        });
    }

    public static Position? FollowLeaderTrail(
        PartyMemberAvatar member,
        int minimumLag,
        Maze maze,
        Player player,
        IReadOnlyList<Position> leaderTrail,
        int followOrder = 0)
    {
        if (leaderTrail.Count == 0) return null;
        var formationLag = Math.Max(0, followOrder);
        var targetIndex = Math.Max(0, leaderTrail.Count - 1 - minimumLag - formationLag);
        for (var index = targetIndex; index >= 0; index--)
        {
            var target = leaderTrail[index];
            if (target == member.Position) return null;
            if (!CanPartyPlanThrough(member, target, maze, player)) continue;
            return FindNextTrailStep(member, target, maze, player);
        }
        return null;
    }

    /// <summary>
    /// A nyomvonal tervezésekor a csapattársak később felszabaduló mezőin is átvezethet az út,
    /// de az azonnali lépés csak ténylegesen szabad mezőre történhet.
    /// </summary>
    private static Position? FindNextTrailStep(PartyMemberAvatar member, Position target, Maze maze, Player player)
    {
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
            if (current.Position == target) return current.FirstStep;
            foreach (var direction in Directions)
            {
                var next = current.Position + direction;
                if (!CanPartyPlanThrough(member, next, maze, player) || !visited.Add(next)) continue;
                queue.Enqueue((next, current.FirstStep));
            }
        }
        return null;
    }

    private static bool CanPartyPlanThrough(PartyMemberAvatar member, Position position, Maze maze, Player player)
    {
        if (!maze.IsWalkable(position) || position == player.Position || HasBlockingTrap(maze, position)) return false;
        var occupant = maze.GetObjectAt(position);
        return occupant is null or GroundItemPile or Corpse or ForestInn or PartyMemberAvatar || occupant == member ||
               Maze.IsPassableNeutralNpc(occupant);
    }

    public static Position? ChooseForwardStep(
        PartyMemberAvatar member,
        int maximumLeaderDistance,
        int maximumSearchDistance,
        bool avoidNarrowFront,
        Maze maze,
        Player player,
        Direction leaderFacing,
        Position? visibleEnemyPosition = null,
        int minimumEnemyDistance = 0)
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
            .Where(entry => visibleEnemyPosition is null ||
                Manhattan(entry.Position, visibleEnemyPosition.Value) >= minimumEnemyDistance)
            .Where(entry => !avoidNarrowFront || CountWalkableNeighbors(entry.Position, maze) >= 3)
            .OrderByDescending(entry => entry.Progress)
            .ThenBy(entry => entry.Distance)
            .FirstOrDefault();
        if (reachable is null) return null;
        var step = FindNextStep(member, [reachable.Position], maze, player);
        if (step is { } guardedStep && visibleEnemyPosition is { } threat &&
            Manhattan(guardedStep, threat) < minimumEnemyDistance)
            return null;
        if (avoidNarrowFront && step is { } narrowStep && IsAheadOfLeader(narrowStep, player.Position, leaderFacing) && CountWalkableNeighbors(narrowStep, maze) <= 2)
            return null;
        return step;
    }

    public static Direction RecentTravelDirection(IReadOnlyList<Position> trail, Position leaderPosition,
        Direction fallback)
    {
        if (trail.Count < 2 || trail[^1] != leaderPosition) return fallback;
        var previous = trail[^2];
        return Manhattan(previous, leaderPosition) == 1
            ? Directions.First(direction => previous + direction == leaderPosition)
            : fallback;
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
        .Where(position => IsFreeNeighbor(maze, player, position));

    private static bool IsFreeNeighbor(Maze maze, Player player, Position position)
    {
        if (!maze.IsWalkable(position) || position == player.Position || HasBlockingTrap(maze, position)) return false;
        var occupant = maze.GetObjectAt(position);
        return occupant is null or GroundItemPile or Corpse or ForestInn || Maze.IsPassableNeutralNpc(occupant);
    }

    public static bool CanPartyTraverse(PartyMemberAvatar member, Position position, Maze maze, Player player)
    {
        if (!maze.IsWalkable(position) || position == player.Position || HasBlockingTrap(maze, position)) return false;
        var occupant = maze.GetObjectAt(position);
        return occupant is null or GroundItemPile or Corpse or ForestInn || occupant == member ||
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
        if (!maze.IsWalkable(position) || position == player.Position || HasBlockingTrap(maze, position) ||
            maze.GetEnemyAt(position) is not null)
            return false;
        var occupant = maze.GetObjectAt(position);
        if (occupant is null or GroundItemPile or Corpse or ForestInn || occupant == member || Maze.IsPassableNeutralNpc(occupant))
            return true;
        if (occupant is not PartyMemberAvatar friend) return false;
        return !formationTargets.TryGetValue(friend.Character.Id, out var friendTarget) ||
               friendTarget != friend.Position;
    }

    public static int Manhattan(Position first, Position second) =>
        Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y);

    private static bool HasBlockingTrap(Maze maze, Position position) =>
        maze.GetTrapAt(position) is { IsActive: true, State: TrapState.Detected };

    public static (int X, int Y) DirectionOffset(Direction direction) => direction switch
    {
        Direction.Up => (0, -1),
        Direction.Down => (0, 1),
        Direction.Left => (-1, 0),
        _ => (1, 0)
    };
}
