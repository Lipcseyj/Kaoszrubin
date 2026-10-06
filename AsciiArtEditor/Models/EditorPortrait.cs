using System.Collections.Generic;

namespace AsciiArtEditor.Models;

public sealed record EditorPortrait(IReadOnlyList<string> Lines, int CanvasWidth);
