namespace KaoszRubin.Domain.Characters;

// A sorszámok mentési kompatibilitást őriznek; a sor és oszlop a formától függ.
public enum FormationSlot { FrontLeft, FrontRight, RearLeft, RearRight, ReserveLeft, ReserveRight }
public enum PartyFormationState { Disbanded, Assembling, Locked }
public enum PartyFormationLayout { Block, SingleFile }
public enum PartyFormationShape { Block2x2, Column2x3, Wide3x2 }

public sealed record PartyFormationSnapshot(
    CharacterId? FrontLeft, CharacterId? FrontRight, CharacterId? RearLeft, CharacterId? RearRight,
    Direction Facing, PartyFormationState State, PartyFormationLayout Layout = PartyFormationLayout.Block,
    CharacterId? ReserveLeft = null, CharacterId? ReserveRight = null,
    PartyFormationShape Shape = PartyFormationShape.Block2x2)
{
    public IReadOnlyList<CharacterId?> Slots => Shape == PartyFormationShape.Block2x2
        ? [FrontLeft, FrontRight, RearLeft, RearRight]
        : [FrontLeft, FrontRight, RearLeft, RearRight, ReserveLeft, ReserveRight];
    public int Width => Shape == PartyFormationShape.Wide3x2 ? 3 : 2;
    public int Depth => Shape == PartyFormationShape.Column2x3 ? 3 : 2;
    public CharacterId? CharacterAt(FormationSlot slot) => Slots.ElementAtOrDefault((int)slot);
}

public static class PartyFormationRules
{
    public static PartyFormationSnapshot CreateDefault(IEnumerable<CharacterId> party, CharacterId leader,
        Direction facing = Direction.Right, PartyFormationState state = PartyFormationState.Disbanded)
    {
        var members = OrderedMembers(party, leader);
        return FromSlots(members.Cast<CharacterId?>(), facing, state, PartyFormationLayout.Block,
            members.Count > 4 ? PartyFormationShape.Column2x3 : PartyFormationShape.Block2x2);
    }

    public static PartyFormationSnapshot Normalize(PartyFormationSnapshot? formation,
        IEnumerable<CharacterId> party, CharacterId leader)
    {
        var members = OrderedMembers(party, leader);
        var shape = formation?.Shape ?? PartyFormationShape.Block2x2;
        if (!Enum.IsDefined(shape) || shape == PartyFormationShape.Block2x2 && members.Count > 4)
            shape = members.Count > 4 ? PartyFormationShape.Column2x3 : PartyFormationShape.Block2x2;
        var slots = new CharacterId?[shape == PartyFormationShape.Block2x2 ? 4 : 6];
        var saved = formation?.Slots ?? [];
        var used = new HashSet<CharacterId>();
        for (var index = 0; index < slots.Length; index++)
            if (index < saved.Count && saved[index] is { } candidate && members.Contains(candidate) && used.Add(candidate))
                slots[index] = candidate;
        var remaining = new Queue<CharacterId>(members.Where(id => !used.Contains(id)));
        for (var index = 0; index < slots.Length && remaining.Count > 0; index++)
            if (slots[index] is null) slots[index] = remaining.Dequeue();
        var state = formation?.State ?? PartyFormationState.Disbanded;
        var layout = state == PartyFormationState.Locked && formation?.Layout == PartyFormationLayout.SingleFile
            ? PartyFormationLayout.SingleFile : PartyFormationLayout.Block;
        return FromSlots(slots, formation?.Facing ?? Direction.Right, state, layout, shape);
    }

    private static List<CharacterId> OrderedMembers(IEnumerable<CharacterId> party, CharacterId leader) =>
        party.Prepend(leader).Distinct().Take(Party.MaximumSize).ToList();

    public static PartyFormationSnapshot WithSlots(PartyFormationSnapshot formation,
        IReadOnlyList<CharacterId?> slots) => FromSlots(slots, formation.Facing,
            PartyFormationState.Disbanded, PartyFormationLayout.Block, formation.Shape);

    public static PartyFormationSnapshot WithShape(PartyFormationSnapshot formation, PartyFormationShape shape)
    {
        if (!Enum.IsDefined(shape)) throw new ArgumentOutOfRangeException(nameof(shape));
        var size = shape == PartyFormationShape.Block2x2 ? 4 : 6;
        if (formation.Slots.Count(id => id is not null) > size)
            throw new InvalidOperationException("A kiválasztott alakzatban nincs elég hely a teljes csapatnak.");
        var width = shape == PartyFormationShape.Wide3x2 ? 3 : 2;
        var slots = new CharacterId?[size];
        var remaining = new List<CharacterId>();
        for (var index = 0; index < formation.Slots.Count; index++)
        {
            if (formation.Slots[index] is not { } id) continue;
            var column = index % formation.Width;
            var row = index / formation.Width;
            var target = row * width + column;
            if (column < width && target < size) slots[target] = id;
            else remaining.Add(id);
        }
        foreach (var id in remaining)
            slots[Array.FindIndex(slots, value => value is null)] = id;
        return FromSlots(slots, formation.Facing, PartyFormationState.Disbanded, PartyFormationLayout.Block, shape);
    }

    public static int SlotIndexOf(PartyFormationSnapshot formation, CharacterId id) =>
        Enumerable.Range(0, formation.Slots.Count).FirstOrDefault(index => formation.Slots[index] == id, -1);
    public static int RowOf(PartyFormationSnapshot formation, int slot) => slot / formation.Width;
    public static int? AdjacentRowSlot(PartyFormationSnapshot formation, int slot, int rowDelta)
    {
        if (slot < 0 || slot >= formation.Slots.Count || Math.Abs(rowDelta) != 1) return null;
        var next = slot + formation.Width * rowDelta;
        return next >= 0 && next < formation.Slots.Count ? next : null;
    }

    public static string ShapeName(PartyFormationShape shape) => shape switch
    {
        PartyFormationShape.Column2x3 => "2×3-as menetoszlop",
        PartyFormationShape.Wide3x2 => "3×2-es széles harcrend",
        _ => "2×2-es blokk"
    };

    public static PartyFormationSnapshot WithState(PartyFormationSnapshot formation, PartyFormationState state) =>
        formation with { State = state, Layout = state == PartyFormationState.Locked ? formation.Layout : PartyFormationLayout.Block };

    public static IReadOnlyList<CharacterId> FollowOrder(PartyFormationSnapshot formation,
        CharacterId leaderId, IEnumerable<CharacterId> partyMemberIds)
    {
        var available = partyMemberIds.Where(id => id != leaderId).Distinct().ToArray();
        var ordered = formation.Slots.OfType<CharacterId>().Where(id => id != leaderId && available.Contains(id)).Distinct().ToList();
        ordered.AddRange(available.Where(id => !ordered.Contains(id)));
        return ordered;
    }

    public static PartyFormationSnapshot Rotate(PartyFormationSnapshot formation, bool clockwise) =>
        formation with { Facing = Rotate(formation.Facing, clockwise) };
    public static PartyFormationSnapshot RotateInPlace(PartyFormationSnapshot formation, bool clockwise) => Rotate(formation, clockwise);
    public static PartyFormationSnapshot FaceInPlace(PartyFormationSnapshot formation, Direction facing) => formation with { Facing = facing };

    public static IReadOnlyDictionary<CharacterId, Position> PositionsInSameFootprint(PartyFormationSnapshot formation,
        CharacterId anchorCharacterId, Position anchorPosition, Direction facing)
    {
        // Csak a régi négyzet fordulhat ugyanabban a lábnyomban. A téglalap a vezér körül fordul.
        if (formation.Layout == PartyFormationLayout.SingleFile || formation.Shape != PartyFormationShape.Block2x2)
            return Positions(formation with { Facing = facing }, anchorCharacterId, anchorPosition);
        var anchorSlot = Math.Max(0, SlotIndexOf(formation, anchorCharacterId));
        var oldAnchorOffset = Offset(formation, anchorSlot, formation.Facing);
        var offsets = Enumerable.Range(0, formation.Slots.Count)
            .Select(index => Subtract(Offset(formation, index, formation.Facing), oldAnchorOffset)).ToArray();
        var origin = Add(anchorPosition, new(offsets.Min(value => value.X), offsets.Min(value => value.Y)));
        var newOffsets = Enumerable.Range(0, formation.Slots.Count).Select(index => Offset(formation, index, facing)).ToArray();
        var originOffset = new Position(newOffsets.Min(value => value.X), newOffsets.Min(value => value.Y));
        return Enumerable.Range(0, formation.Slots.Count).Where(index => formation.Slots[index] is not null)
            .ToDictionary(index => formation.Slots[index]!.Value, index => Add(origin, Subtract(newOffsets[index], originOffset)));
    }

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
        if (formation.Layout == PartyFormationLayout.SingleFile)
        {
            var ordered = formation.Slots.OfType<CharacterId>().Where(id => id != anchorCharacterId).Prepend(anchorCharacterId).Distinct();
            var backward = BackwardOffset(formation.Facing);
            return ordered.Select((id, index) => (id, Position: Add(anchorPosition, new(backward.X * index, backward.Y * index))))
                .ToDictionary(entry => entry.id, entry => entry.Position);
        }
        var anchorSlot = Math.Max(0, SlotIndexOf(formation, anchorCharacterId));
        var anchorOffset = Offset(formation, anchorSlot, formation.Facing);
        return Enumerable.Range(0, formation.Slots.Count).Where(index => formation.Slots[index] is not null)
            .ToDictionary(index => formation.Slots[index]!.Value,
                index => Add(anchorPosition, Subtract(Offset(formation, index, formation.Facing), anchorOffset)));
    }

    public static IReadOnlyList<Position> InteractionOrigins(PartyFormationSnapshot formation,
        CharacterId actorId, Position actorPosition, IReadOnlyDictionary<CharacterId, Position> partyPositions)
    {
        if (formation.State != PartyFormationState.Locked || !formation.Slots.Contains(actorId)) return [actorPosition];
        return formation.Slots.OfType<CharacterId>().Where(partyPositions.ContainsKey).Select(id => partyPositions[id])
            .Append(actorPosition).Distinct().ToArray();
    }

    private static PartyFormationSnapshot FromSlots(IEnumerable<CharacterId?> slots, Direction facing,
        PartyFormationState state, PartyFormationLayout layout, PartyFormationShape shape)
    {
        var values = slots.Concat(Enumerable.Repeat<CharacterId?>(null, 6)).Take(6).ToArray();
        return new(values[0], values[1], values[2], values[3], facing, state, layout,
            shape == PartyFormationShape.Block2x2 ? null : values[4],
            shape == PartyFormationShape.Block2x2 ? null : values[5], shape);
    }
    public static Position ForwardOffset(Direction facing) => facing switch
    {
        Direction.Up => new(0, -1), Direction.Right => new(1, 0), Direction.Down => new(0, 1), _ => new(-1, 0)
    };
    private static Position BackwardOffset(Direction facing) { var forward = ForwardOffset(facing); return new(-forward.X, -forward.Y); }
    private static Position Offset(PartyFormationSnapshot formation, int slot, Direction facing)
    {
        var offset = new Position(slot % formation.Width, slot / formation.Width);
        return facing switch
        {
            Direction.Up => offset, Direction.Right => new(-offset.Y, offset.X),
            Direction.Down => new(-offset.X, -offset.Y), _ => new(offset.Y, -offset.X)
        };
    }
    private static Position Add(Position left, Position right) => new(left.X + right.X, left.Y + right.Y);
    private static Position Subtract(Position left, Position right) => new(left.X - right.X, left.Y - right.Y);
}
