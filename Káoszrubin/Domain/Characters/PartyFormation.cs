namespace KaoszRubin.Domain.Characters;

public enum FormationSlot { FrontLeft, FrontRight, RearLeft, RearRight }

public enum PartyFormationState { Disbanded, Assembling, Locked }

public sealed record PartyFormationSnapshot(
    CharacterId? FrontLeft,
    CharacterId? FrontRight,
    CharacterId? RearLeft,
    CharacterId? RearRight,
    Direction Facing,
    PartyFormationState State)
{
    public IReadOnlyList<CharacterId?> Slots => [FrontLeft, FrontRight, RearLeft, RearRight];

    public CharacterId? CharacterAt(FormationSlot slot) => Slots[(int)slot];
}

public static class PartyFormationRules
{
    public static PartyFormationSnapshot CreateDefault(IEnumerable<CharacterId> party, CharacterId leader,
        Direction facing = Direction.Right, PartyFormationState state = PartyFormationState.Disbanded)
    {
        var members = party.Distinct().ToList();
        members.Remove(leader);
        members.Insert(0, leader);
        return FromSlots(members.Cast<CharacterId?>().Concat(Enumerable.Repeat<CharacterId?>(null, 4)).Take(4),
            facing, state);
    }

    public static PartyFormationSnapshot Normalize(PartyFormationSnapshot? formation,
        IEnumerable<CharacterId> party, CharacterId leader)
    {
        var members = party.Distinct().Take(4).ToList();
        if (!members.Contains(leader)) members.Insert(0, leader);
        var remaining = new Queue<CharacterId>(members);
        var used = new HashSet<CharacterId>();
        var slots = new CharacterId?[4];
        var saved = formation?.Slots ?? [];
        for (var index = 0; index < slots.Length; index++)
        {
            var candidate = index < saved.Count ? saved[index] : null;
            if (candidate is not { } id || !members.Contains(id) || !used.Add(id)) continue;
            slots[index] = id;
        }
        foreach (var index in Enumerable.Range(0, slots.Length).Where(index => slots[index] is null))
        {
            while (remaining.Count > 0 && used.Contains(remaining.Peek())) remaining.Dequeue();
            if (remaining.Count == 0) break;
            slots[index] = remaining.Dequeue();
            used.Add(slots[index]!.Value);
        }
        return FromSlots(slots, formation?.Facing ?? Direction.Right,
            formation?.State ?? PartyFormationState.Disbanded);
    }

    public static PartyFormationSnapshot WithSlots(PartyFormationSnapshot formation,
        IReadOnlyList<CharacterId?> slots) => FromSlots(slots, formation.Facing, PartyFormationState.Disbanded);

    public static PartyFormationSnapshot WithState(PartyFormationSnapshot formation, PartyFormationState state) =>
        formation with { State = state };

    public static PartyFormationSnapshot Rotate(PartyFormationSnapshot formation, bool clockwise) =>
        formation with { Facing = Rotate(formation.Facing, clockwise) };

    public static Direction Rotate(Direction direction, bool clockwise) => (direction, clockwise) switch
    {
        (Direction.Up, true) or (Direction.Down, false) => Direction.Right,
        (Direction.Right, true) or (Direction.Left, false) => Direction.Down,
        (Direction.Down, true) or (Direction.Up, false) => Direction.Left,
        _ => Direction.Up
    };

    public static IReadOnlyDictionary<CharacterId, Position> Positions(PartyFormationSnapshot formation,
        CharacterId anchorCharacterId, Position anchorPosition)
    {
        var anchorSlot = Enumerable.Range(0, 4)
            .FirstOrDefault(index => formation.Slots[index] == anchorCharacterId);
        var anchorOffset = Offset((FormationSlot)anchorSlot, formation.Facing);
        return Enumerable.Range(0, 4)
            .Where(index => formation.Slots[index] is not null)
            .ToDictionary(index => formation.Slots[index]!.Value,
                index => Add(anchorPosition, Subtract(Offset((FormationSlot)index, formation.Facing), anchorOffset)));
    }

    public static IReadOnlyList<Position> InteractionOrigins(PartyFormationSnapshot formation,
        CharacterId actorId, Position actorPosition, IReadOnlyDictionary<CharacterId, Position> partyPositions)
    {
        if (formation.State != PartyFormationState.Locked || !formation.Slots.Contains(actorId))
            return [actorPosition];
        return formation.Slots.Where(id => id is not null)
            .Select(id => partyPositions.TryGetValue(id!.Value, out var position) ? (Position?)position : null)
            .Where(position => position is not null)
            .Select(position => position!.Value)
            .Append(actorPosition)
            .Distinct()
            .ToArray();
    }

    private static PartyFormationSnapshot FromSlots(IEnumerable<CharacterId?> slots, Direction facing,
        PartyFormationState state)
    {
        var values = slots.Concat(Enumerable.Repeat<CharacterId?>(null, 4)).Take(4).ToArray();
        return new(values[0], values[1], values[2], values[3], facing, state);
    }

    private static Position Offset(FormationSlot slot, Direction facing)
    {
        var offset = slot switch
        {
            FormationSlot.FrontLeft => new Position(0, 0),
            FormationSlot.FrontRight => new Position(1, 0),
            FormationSlot.RearLeft => new Position(0, 1),
            _ => new Position(1, 1)
        };
        return facing switch
        {
            Direction.Up => offset,
            Direction.Right => new Position(-offset.Y, offset.X),
            Direction.Down => new Position(-offset.X, -offset.Y),
            _ => new Position(offset.Y, -offset.X)
        };
    }

    private static Position Add(Position position, Position delta) =>
        new(position.X + delta.X, position.Y + delta.Y);

    private static Position Subtract(Position left, Position right) =>
        new(left.X - right.X, left.Y - right.Y);
}
