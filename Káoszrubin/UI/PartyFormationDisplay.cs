using KaoszRubin.Domain.Characters;

namespace KaoszRubin.UI;

/// <summary>Közös helyi/vendég alakzatikonok: nézés, foglalt helyek, színes sorok, állapot.</summary>
public static class PartyFormationDisplay
{
    public const int PartyHeadingRow = 41;
    public const int PartyStartRow = 42;
    public const int PartyRows = Party.MaximumSize;
    public const int FormationRow = PartyStartRow + PartyRows;

    public static string EmptySlotText(int index, int capacity) => index < capacity
        ? "○ Üres partihely"
        : index == 4 ? "○ Az 5. pálya teljesítése után" : "○ A 8. pálya teljesítése után";

    public static string Text(PartyFormationSnapshot formation) =>
        string.Concat(Segments(formation).Select(segment => segment.Text));

    public static IReadOnlyList<TextSegment> Segments(PartyFormationSnapshot formation,
        IReadOnlyDictionary<CharacterId, ConsoleColor>? colors = null)
    {
        var arrow = formation.Facing switch { Direction.Up => "↑", Direction.Right => "→", Direction.Down => "↓", _ => "←" };
        var state = formation.State switch
        {
            PartyFormationState.Locked => "■",
            PartyFormationState.Assembling => "◌",
            _ => "◇"
        };
        var result = new List<TextSegment> { new($"{arrow} {OccupancyGlyph(formation)} ", ConsoleColor.Cyan) };
        for (var index = 0; index < formation.Slots.Count; index++)
        {
            if (index > 0 && index % formation.Width == 0)
                result.Add(new(formation.Layout == PartyFormationLayout.SingleFile ? "" : "/", ConsoleColor.DarkGray));
            var id = formation.Slots[index];
            result.Add(new(id is null ? "○" : "●", id is { } member
                ? colors?.GetValueOrDefault(member, ConsoleColor.Gray) ?? ConsoleColor.Gray : ConsoleColor.DarkGray));
        }
        result.Add(new($" {(formation.Layout == PartyFormationLayout.SingleFile ? "⋮ " : "")}{state}",
            formation.State == PartyFormationState.Locked ? ConsoleColor.Green : ConsoleColor.DarkCyan));
        return result;
    }

    public static string OccupancyGlyph(PartyFormationSnapshot formation)
    {
        int[] leftBits = [0, 1, 2];
        int[] rightBits = [3, 4, 5];
        var glyphs = new List<char>();
        for (var column = 0; column < formation.Width; column += 2)
        {
            var bits = 0;
            for (var row = 0; row < formation.Depth; row++)
            for (var half = 0; half < 2 && column + half < formation.Width; half++)
                if (formation.Slots[row * formation.Width + column + half] is not null)
                    bits |= 1 << (half == 0 ? leftBits[row] : rightBits[row]);
            glyphs.Add((char)(0x2800 + bits));
        }
        return new string(glyphs.ToArray());
    }
}
