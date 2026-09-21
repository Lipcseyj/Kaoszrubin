namespace KaoszRubin.UI;

internal readonly record struct CharacterMenuFrame(int Left, int Top, int Width, int Height);
internal enum CharacterMenuBackdropStyle { Maze, Blocks }

/// <summary>Közös, Scroll-keretes felület a karakterlista és a karakteralkotás minden lépéséhez.</summary>
internal static class CharacterMenuSurface
{
    private const int PreferredWidth = 118;
    private const int PreferredHeight = 34;
    private static int _backgroundWidth;
    private static int _backgroundHeight;
    private static CharacterMenuBackdropStyle _backdropStyle;
    public static bool IsActive { get; private set; }

    public static CharacterMenuFrame Frame
    {
        get
        {
            var width = Math.Max(10, Math.Min(PreferredWidth, Console.WindowWidth - 2));
            var height = Math.Max(8, Math.Min(PreferredHeight, Console.WindowHeight - 2));
            return new CharacterMenuFrame(
                Math.Max(0, (Console.WindowWidth - width) / 2),
                Math.Max(0, (Console.WindowHeight - height) / 2),
                width, height);
        }
    }

    public static void Begin()
    {
        IsActive = true;
        _backdropStyle = Random.Shared.Next(2) == 0
            ? CharacterMenuBackdropStyle.Maze
            : CharacterMenuBackdropStyle.Blocks;
        DrawBackdrop();
    }

    public static void End() => IsActive = false;

    public static CharacterMenuFrame DrawWindow(string title)
    {
        if (!IsActive)
            Begin();
        else if (_backgroundWidth != Console.WindowWidth || _backgroundHeight != Console.WindowHeight)
            DrawBackdrop();

        var frame = Frame;
        var style = WindowFrameConfiguration.For(FramedWindow.CharacterManagement);
        Console.BackgroundColor = ConsoleColor.Black;
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.SetCursorPosition(frame.Left, frame.Top);
        Console.Write(WindowFrameCatalog.Horizontal(style, frame.Width));
        for (var row = 0; row < frame.Height - 2; row++)
        {
            var sides = WindowFrameCatalog.Sides(style, row, frame.Height - 2);
            Console.SetCursorPosition(frame.Left, frame.Top + row + 1);
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write(sides.Left);
            Console.BackgroundColor = ConsoleColor.Black;
            Console.Write(new string(' ', Math.Max(0, frame.Width - sides.Left.Length - sides.Right.Length)));
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write(sides.Right);
        }
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.SetCursorPosition(frame.Left, frame.Top + frame.Height - 1);
        Console.Write(WindowFrameCatalog.Horizontal(style, frame.Width, bottom: true));

        var titleWidth = Math.Min(title.Length, Math.Max(0, frame.Width - 8));
        Console.SetCursorPosition(frame.Left + Math.Max(2, (frame.Width - titleWidth) / 2), frame.Top + 1);
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write(title[..titleWidth]);
        Console.ResetColor();
        return frame;
    }

    private static void DrawBackdrop()
    {
        Console.BackgroundColor = ConsoleColor.Black;
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Clear();
        _backgroundWidth = Console.WindowWidth;
        _backgroundHeight = Console.WindowHeight;
        for (var y = 0; y < _backgroundHeight; y++)
        {
            Console.SetCursorPosition(0, y);
            var rowWidth = y == _backgroundHeight - 1 ? Math.Max(0, _backgroundWidth - 1) : _backgroundWidth;
            for (var x = 0; x < rowWidth; x++)
                Console.Write(_backdropStyle == CharacterMenuBackdropStyle.Maze
                    ? MazeGlyph(x, y)
                    : BlockGlyph(x, y));
        }
        Console.ResetColor();
    }

    internal static char MazeGlyph(int x, int y)
    {
        var horizontal = y % 4 == 0 && ((x / 7 + y / 4 * 3) % 5 != 1);
        var vertical = x % 7 == 0 && ((y / 4 + x / 7 * 2) % 5 != 2);
        if (horizontal && vertical) return '┼';
        if (horizontal) return '─';
        if (vertical) return '│';
        return (x * 17 + y * 31) % 97 == 0 ? '·' : ' ';
    }

    internal static char BlockGlyph(int x, int y)
    {
        const string ramp = "░▒▓██▓▒░";
        var diagonalWave = x / 3 + y / 2 + (x + y) / 17;
        return ramp[diagonalWave % ramp.Length];
    }
}
