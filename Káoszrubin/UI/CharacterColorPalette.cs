using KaoszRubin.Domain.Characters;

namespace KaoszRubin.UI;

public static class CharacterColorPalette
{
    public const int Columns = 4;
    public const int CellWidth = 12;
    public const int Width = Columns * CellWidth + 8;
    private const int Height = 11;

    public static int Move(int selectedIndex, ConsoleKey key)
    {
        var count = CharacterColors.Selectable.Count;
        if (count == 0) return 0;
        selectedIndex = Math.Clamp(selectedIndex, 0, count - 1);
        return key switch
        {
            ConsoleKey.LeftArrow => (selectedIndex - 1 + count) % count,
            ConsoleKey.RightArrow => (selectedIndex + 1) % count,
            ConsoleKey.UpArrow => (selectedIndex - Columns + count) % count,
            ConsoleKey.DownArrow => (selectedIndex + Columns) % count,
            _ => selectedIndex
        };
    }

    public static ConsoleColor? Show(ConsoleColor current, Func<string?>? coopStatusProvider = null)
    {
        var colors = CharacterColors.Selectable;
        var selected = Math.Max(0, colors.ToList().IndexOf(current));
        var left = Math.Max(0, (Console.WindowWidth - Width) / 2);
        var top = Math.Max(0, (Console.WindowHeight - Height) / 2);
        using var background = new BackgroundContentRestorer(left, top, Width, Height);
        while (true)
        {
            Draw(left, top, colors, selected);
            var key = CoopWindowStatusBanner.ReadKey(coopStatusProvider).Key;
            if (key == ConsoleKey.Enter) return colors[selected];
            if (key == ConsoleKey.Escape) return null;
            selected = Move(selected, key);
        }
    }

    private static void Draw(int left, int top, IReadOnlyList<ConsoleColor> colors, int selected)
    {
        var style = WindowFrameConfiguration.For(FramedWindow.CharacterDetails);
        Write(left, top, WindowFrameCatalog.Horizontal(style, Width), ConsoleColor.Magenta);
        WriteLine(left, top + 1, Width, style, "KARAKTERSZÍN", ConsoleColor.Yellow);
        WriteLine(left, top + 2, Width, style, "Nyilak: választás   Enter: mentés   Esc: mégse", ConsoleColor.DarkCyan);
        for (var row = 0; row < 3; row++)
        {
            WriteLine(left, top + 4 + row * 2, Width, style, string.Empty, ConsoleColor.Gray);
            WriteLine(left, top + 5 + row * 2, Width, style, string.Empty, ConsoleColor.Gray);
            for (var column = 0; column < Columns; column++)
            {
                var index = row * Columns + column;
                if (index >= colors.Count) continue;
                var x = left + 4 + column * CellWidth;
                var color = colors[index];
                WriteSwatch(x, top + 4 + row * 2, index == selected, color);
                Write(x, top + 5 + row * 2,
                    Center(CharacterColors.NameOf(color), CellWidth - 2),
                    index == selected ? ConsoleColor.White : ConsoleColor.Gray);
            }
        }
        Write(left, top + Height - 1, WindowFrameCatalog.Horizontal(style, Width, true), ConsoleColor.Magenta);
        Console.ResetColor();
    }

    private static void WriteSwatch(int left, int top, bool selected, ConsoleColor color)
    {
        Write(left, top, selected ? "▶" : " ", selected ? ConsoleColor.Yellow : ConsoleColor.Gray);
        Console.SetCursorPosition(left + 2, top);
        Console.ForegroundColor = color == ConsoleColor.Black ? ConsoleColor.White : color;
        Console.BackgroundColor = color;
        Console.Write("        ");
        Console.ResetColor();
    }

    private static void WriteLine(int left, int top, int width, WindowFrameStyle style, string text,
        ConsoleColor color)
    {
        var sides = WindowFrameCatalog.Sides(style, 0, 1);
        Write(left, top, sides.Left, ConsoleColor.Magenta);
        Write(left + sides.Left.Length, top, " " + Center(text, width - sides.Left.Length - sides.Right.Length - 2) + " ", color);
        Write(left + width - sides.Right.Length, top, sides.Right, ConsoleColor.Magenta);
    }

    private static string Center(string text, int width)
    {
        if (text.Length >= width) return text[..width];
        var left = (width - text.Length) / 2;
        return new string(' ', left) + text + new string(' ', width - text.Length - left);
    }

    private static void Write(int left, int top, string text, ConsoleColor color)
    {
        if (top < 0 || top >= Console.WindowHeight || left < 0 || left >= Console.WindowWidth) return;
        Console.SetCursorPosition(left, top);
        Console.ForegroundColor = color;
        Console.BackgroundColor = ConsoleColor.Black;
        Console.Write(BattleCommandPanel.TruncateToDisplayWidth(text, Console.WindowWidth - left));
    }
}
