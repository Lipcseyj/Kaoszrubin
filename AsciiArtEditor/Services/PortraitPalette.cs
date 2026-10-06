using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AsciiArtEditor.Services;

public static class PortraitPalette
{
    private static readonly (int First, int Last)[] DrawingRanges =
    [
        (0x0021, 0x007E),
        (0x00A1, 0x00FF),
        (0x0100, 0x024F),
        (0x0370, 0x03FF),
        (0x0400, 0x04FF),
        (0x2190, 0x21FF),
        (0x2200, 0x22FF),
        (0x2300, 0x23FF),
        (0x2460, 0x24FF),
        (0x2500, 0x257F),
        (0x2580, 0x259F),
        (0x25A0, 0x25FF),
        (0x2600, 0x26FF),
        (0x2700, 0x27BF),
        (0x2800, 0x28FF),
        (0x2900, 0x297F),
        (0x2B00, 0x2BFF)
    ];

    public static IReadOnlyList<string> Collect(IEnumerable<string> portraitContents)
    {
        var glyphs = new List<string> { " " };
        var seen = new HashSet<string>(StringComparer.Ordinal) { " " };

        foreach (var (first, last) in DrawingRanges)
        {
            for (var value = first; value <= last; value++)
            {
                if (Rune.TryCreate(value, out var rune) && IsUsefulGlyph(rune))
                    AddGlyph(rune.ToString());
            }
        }

        foreach (var content in portraitContents)
        {
            foreach (var rune in content.EnumerateRunes())
            {
                if (rune.Value is '\r' or '\n')
                    continue;

                AddGlyph(rune.ToString());
            }
        }

        return glyphs;

        void AddGlyph(string glyph)
        {
            if (seen.Add(glyph))
                glyphs.Add(glyph);
        }
    }

    private static bool IsUsefulGlyph(Rune rune)
    {
        var category = Rune.GetUnicodeCategory(rune);
        return category is not (UnicodeCategory.Control or
            UnicodeCategory.Format or
            UnicodeCategory.Surrogate or
            UnicodeCategory.PrivateUse or
            UnicodeCategory.OtherNotAssigned or
            UnicodeCategory.NonSpacingMark or
            UnicodeCategory.SpacingCombiningMark or
            UnicodeCategory.EnclosingMark or
            UnicodeCategory.LineSeparator or
            UnicodeCategory.ParagraphSeparator);
    }
}
