using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AsciiArtEditor.Services;

public static class PortraitPalette
{
    public static IReadOnlyList<string> Collect(IEnumerable<string> portraitContents)
    {
        var glyphs = new List<string> { " " };
        var seen = new HashSet<string>(StringComparer.Ordinal) { " " };

        foreach (var content in portraitContents)
        {
            foreach (var rune in content.EnumerateRunes())
            {
                if (rune.Value is '\r' or '\n')
                    continue;

                var glyph = rune.ToString();
                if (seen.Add(glyph))
                    glyphs.Add(glyph);
            }
        }

        return glyphs;
    }
}
