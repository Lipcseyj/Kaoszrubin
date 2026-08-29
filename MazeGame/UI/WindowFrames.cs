namespace MazeGame.UI;

public enum WindowFrameStyle { Single, Double, Scroll, Scroll2, Sword, Ruby, Stone, Magic, Magic2 }

public enum FramedWindow
{
    MainMenu, Help, SpellSelector, CreaturePortrait, Storyline, LevelUp, LevelUpChoice,
    SpellLearning, SpellPreparation
}

/// <summary>Az egyes képernyők kerete itt cserélhető, a rajzolókód módosítása nélkül.</summary>
public static class WindowFrameConfiguration
{
    private static readonly IReadOnlyDictionary<FramedWindow, WindowFrameStyle> Styles =
        new Dictionary<FramedWindow, WindowFrameStyle>
        {
            [FramedWindow.MainMenu] = WindowFrameStyle.Ruby,
            [FramedWindow.Help] = WindowFrameStyle.Ruby,
            [FramedWindow.SpellSelector] = WindowFrameStyle.Magic,
            [FramedWindow.CreaturePortrait] = WindowFrameStyle.Stone,
            [FramedWindow.Storyline] = WindowFrameStyle.Stone,
            [FramedWindow.LevelUp] = WindowFrameStyle.Scroll2,
            [FramedWindow.LevelUpChoice] = WindowFrameStyle.Sword,
            [FramedWindow.SpellLearning] = WindowFrameStyle.Magic2,
            [FramedWindow.SpellPreparation] = WindowFrameStyle.Magic2
        };

    public static WindowFrameStyle For(FramedWindow window) => Styles[window];
}

public readonly record struct WindowFrameRow(string Left, string Right);

/// <summary>Méretezhető konzolkeretek közös katalógusa.</summary>
public static class WindowFrameCatalog
{
    public static string Horizontal(WindowFrameStyle style, int width, bool bottom = false)
    {
        if (width < 2) return new string('─', Math.Max(0, width));
        return style switch
        {
            WindowFrameStyle.Single => (bottom ? "└" : "┌") + new string('─', width - 2) + (bottom ? "┘" : "┐"),
            WindowFrameStyle.Double => (bottom ? "╚" : "╔") + new string('═', width - 2) + (bottom ? "╝" : "╗"),
            WindowFrameStyle.Scroll => "@)" + new string('=', Math.Max(0, width - 4)) + "(@",
            WindowFrameStyle.Scroll2 => (bottom ? "╰" : "╭") + new string('≈', width - 2) +
                                        (bottom ? "╯" : "╮"),
            WindowFrameStyle.Sword => "═══╪" + new string('═', Math.Max(0, width - 8)) + "╪═══",
            WindowFrameStyle.Ruby => Ornament(width, "◆▓▒░", "░▒▓◆", '─'),
            WindowFrameStyle.Stone => Ornament(width, "█▓▒░ ", " ░▒▓█", '─'),
            WindowFrameStyle.Magic => MagicHorizontal(width),
            WindowFrameStyle.Magic2 => Magic2Horizontal(width),
            _ => throw new ArgumentOutOfRangeException(nameof(style))
        };
    }

    public static WindowFrameRow Sides(WindowFrameStyle style, int contentRow, int contentRows)
    {
        var distance = Math.Min(contentRow, Math.Max(0, contentRows - contentRow - 1));
        var side = style switch
        {
            WindowFrameStyle.Single or WindowFrameStyle.Magic or WindowFrameStyle.Magic2 => "│",
            WindowFrameStyle.Double => "║",
            WindowFrameStyle.Scroll => " |",
            WindowFrameStyle.Scroll2 => contentRow % 2 == 0 ? " )" : "( ",
            WindowFrameStyle.Sword => "   │",
            WindowFrameStyle.Ruby => distance == 0 ? "▓" : distance == 1 ? "▒" : "│",
            WindowFrameStyle.Stone => distance == 0 ? "▓" : "▒",
            _ => "│"
        };
        var right = style switch
        {
            WindowFrameStyle.Scroll => "| ",
            WindowFrameStyle.Scroll2 => contentRow % 2 == 0 ? "( " : " )",
            WindowFrameStyle.Sword => "│   ",
            _ => side
        };
        return new WindowFrameRow(side, right);
    }

    public static string? Adornment(WindowFrameStyle style, int width, bool bottom = false)
    {
        if (style != WindowFrameStyle.Sword || width < 8) return null;
        var point = bottom ? '▼' : '▲';
        return "   " + point + new string(' ', width - 8) + point + "   ";
    }

    public static int ContentPadding(WindowFrameStyle style) => style == WindowFrameStyle.Sword ? 7 : 2;

    private static string Ornament(int width, string left, string right, char fill) =>
        left + new string(fill, Math.Max(0, width - left.Length - right.Length)) + right;

    private static string MagicHorizontal(int width)
    {
        const string left = "· ✦ ";
        const string diamond = " ◆ ";
        const string right = " ✦ ·";
        var fill = Math.Max(0, width - left.Length - right.Length - diamond.Length * 2);
        var first = fill / 3;
        var second = (fill - first) / 2;
        var third = fill - first - second;
        return left + new string('─', first) + diamond + new string('─', second) + diamond +
               new string('─', third) + right;
    }

    private static string Magic2Horizontal(int width)
    {
        const string left = "· ✦ ";
        const string diamond = " ◆ ";
        const string right = " ✦ ·";
        var fill = Math.Max(0, width - left.Length - right.Length - diamond.Length * 2);
        var outer = fill / 3;
        var middle = fill - outer * 2;
        return left + new string('─', outer) + diamond + new string('─', middle) + diamond +
               new string('─', outer) + right;
    }
}
