using KaoszRubin.Domain;
using KaoszRubin.Domain.Characters;

namespace KaoszRubin.Application;

public sealed class PartyFormationController
{
    public static PartyFormationSnapshot Normalize(
        PartyFormationSnapshot formation,
        IEnumerable<CharacterId> livingMemberIds,
        CharacterId leaderId,
        out bool transitionedToAssembling)
    {
        var previousSlots = formation.Slots;
        var normalized = PartyFormationRules.Normalize(formation, livingMemberIds, leaderId);
        transitionedToAssembling = formation.State == PartyFormationState.Locked && !previousSlots.SequenceEqual(normalized.Slots);
        if (transitionedToAssembling)
        {
            normalized = PartyFormationRules.WithState(normalized, PartyFormationState.Assembling);
        }
        return normalized;
    }

    public static IReadOnlyDictionary<CharacterId, Position> Positions(
        PartyFormationSnapshot formation,
        CharacterId leaderId,
        Position leaderPosition) =>
        PartyFormationRules.Positions(formation, leaderId, leaderPosition);

    public static PartyFormationSnapshot Rotate(PartyFormationSnapshot formation, bool clockwise) =>
        PartyFormationRules.Rotate(formation, clockwise);

    public static int CalculateMoveDelay(IEnumerable<LiveCharacter> members, int controlledMoveDelayMilliseconds = 85)
    {
        var slowestMultiplier = members.Where(member => member.IsAlive)
            .Select(member => CharacterMobilityRules.Evaluate(member).ExplorationDelayMultiplier)
            .DefaultIfEmpty(1)
            .Max();
        return Math.Max(35, (int)Math.Round(controlledMoveDelayMilliseconds * slowestMultiplier * 1.35));
    }

    public static bool CanFormationOccupy(
        IReadOnlyDictionary<CharacterId, Position> positions,
        Maze maze,
        Func<CharacterId, PartyMemberAvatar?> getFormationAvatar,
        Func<PartyMemberAvatar, bool>? canRelocateAvatar = null)
    {
        if (positions.Values.Distinct().Count() != positions.Count) return false;
        var ownAvatars = positions.Keys.Select(getFormationAvatar).Where(avatar => avatar is not null).ToHashSet();
        foreach (var position in positions.Values)
        {
            if (!maze.IsWalkable(position) || maze.GetEnemyAt(position) is not null) return false;
            var occupant = maze.GetObjectAt(position);
            if (occupant is null or GroundItemPile or Corpse or TreasureChest || Maze.IsPassableNeutralNpc(occupant))
                continue;
            if (occupant is PartyMemberAvatar avatar && ownAvatars.Contains(avatar)) continue;
            if (occupant is PartyMemberAvatar relocatable && canRelocateAvatar?.Invoke(relocatable) == true)
                continue;
            return false;
        }
        return true;
    }

    public static IReadOnlyList<Position> EscortPositions(
        IReadOnlyDictionary<CharacterId, Position> formationPositions,
        Direction facing)
    {
        if (formationPositions.Count == 0) return [];
        var forward = DirectionOffset(facing);
        var left = new Position(forward.Y, -forward.X);
        var positions = formationPositions.Values.Distinct().ToArray();
        var rearProjection = positions.Min(position => position.X * forward.X + position.Y * forward.Y);
        var rear = positions.Where(position => position.X * forward.X + position.Y * forward.Y == rearProjection)
            .OrderBy(position => position.X * left.X + position.Y * left.Y)
            .ToArray();
        var directlyBehind = rear.Select(position =>
            new Position(position.X - forward.X, position.Y - forward.Y)).ToArray();
        return directlyBehind
            .Concat(directlyBehind.Select(position => new Position(position.X + left.X, position.Y + left.Y)))
            .Concat(directlyBehind.Select(position => new Position(position.X - left.X, position.Y - left.Y)))
            .Concat(rear.Select(position => new Position(position.X - 2 * forward.X, position.Y - 2 * forward.Y)))
            .Distinct()
            .ToArray();
    }

    public static IReadOnlyDictionary<CharacterId, Position> SingleFileDestinations(
        PartyFormationSnapshot formation,
        IReadOnlyDictionary<CharacterId, Position> currentPositions,
        CharacterId leaderId,
        Position leaderDestination)
    {
        var order = SingleFileOrder(formation, currentPositions, leaderId);
        var destinations = new Dictionary<CharacterId, Position>();
        if (order.Count == 0) return destinations;
        destinations[leaderId] = leaderDestination;
        for (var index = 1; index < order.Count; index++)
            destinations[order[index]] = currentPositions[order[index - 1]];
        return destinations;
    }

    public static IReadOnlyList<Position> SingleFileEscortPositions(
        PartyFormationSnapshot formation,
        IReadOnlyDictionary<CharacterId, Position> currentPositions,
        CharacterId leaderId)
    {
        var order = SingleFileOrder(formation, currentPositions, leaderId);
        if (order.Count == 0) return [];
        var tail = currentPositions[order[^1]];
        var backward = order.Count > 1
            ? UnitOffset(currentPositions[order[^2]], tail)
            : Negate(DirectionOffset(formation.Facing));
        return EscortPositionsBehind([tail], backward);
    }

    private static IReadOnlyList<CharacterId> SingleFileOrder(
        PartyFormationSnapshot formation,
        IReadOnlyDictionary<CharacterId, Position> currentPositions,
        CharacterId leaderId)
    {
        if (!currentPositions.ContainsKey(leaderId)) return [];
        var slotOrder = formation.Slots.Where(id => id is not null).Select(id => id!.Value)
            .Where(currentPositions.ContainsKey).Distinct().ToArray();
        var remaining = slotOrder.Where(id => id != leaderId).ToList();
        var result = new List<CharacterId> { leaderId };
        while (remaining.Count > 0)
        {
            var previousPosition = currentPositions[result[^1]];
            var next = remaining.OrderBy(id => Manhattan(previousPosition, currentPositions[id]))
                .ThenBy(id => Array.IndexOf(slotOrder, id)).First();
            result.Add(next);
            remaining.Remove(next);
        }
        return result;
    }

    private static IReadOnlyList<Position> EscortPositionsBehind(
        IReadOnlyList<Position> rear,
        Position backward)
    {
        var left = new Position(backward.Y, -backward.X);
        var directlyBehind = rear.Select(position => Add(position, backward)).ToArray();
        return directlyBehind
            .Concat(directlyBehind.Select(position => Add(position, left)))
            .Concat(directlyBehind.Select(position => new Position(position.X - left.X, position.Y - left.Y)))
            .Concat(rear.Select(position => new Position(position.X + 2 * backward.X,
                position.Y + 2 * backward.Y)))
            .Distinct()
            .ToArray();
    }

    public static bool IsSingleFilePassage(
        IReadOnlyDictionary<CharacterId, Position> blockPositions,
        IReadOnlyDictionary<CharacterId, Position> singleFilePositions,
        Maze maze) =>
        blockPositions.Values.Any(position => !maze.IsWalkable(position)) &&
        singleFilePositions.Values.All(maze.IsWalkable);

    private static Position DirectionOffset(Direction direction) => direction switch
    {
        Direction.Up => new Position(0, -1),
        Direction.Right => new Position(1, 0),
        Direction.Down => new Position(0, 1),
        _ => new Position(-1, 0)
    };

    private static Position Negate(Position position) => new(-position.X, -position.Y);

    private static Position Add(Position left, Position right) =>
        new(left.X + right.X, left.Y + right.Y);

    private static Position UnitOffset(Position from, Position to) =>
        new(Math.Sign(to.X - from.X), Math.Sign(to.Y - from.Y));

    private static int Manhattan(Position first, Position second) =>
        Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y);
}
