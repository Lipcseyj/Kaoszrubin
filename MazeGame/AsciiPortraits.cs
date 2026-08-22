namespace MazeGame;

/// <summary>A jobb alsó képpanel legfeljebb ötsoros, egycellás karakterekből álló portréi.</summary>
public static class AsciiPortraits
{
    private const int CanvasWidth = 17;

    private static readonly IReadOnlyDictionary<string, AsciiPortrait> CharacterClasses =
        new Dictionary<string, AsciiPortrait>(StringComparer.OrdinalIgnoreCase)
        {
            ["C001"] = Portrait(
                "     └__┘",
                "     (••)  │",
                "    /|==|--╪",
                "     /  \\",
                "    /____\\"),
            ["C002"] = Portrait(
                "    ╭━━╮  Đ",
                "    (òó) / ",
                "   /|##|/▲ ",
                "    |  |",
                "   /_/\\_\\"),
            ["C003"] = Portrait(
                "      ___  ║",
                "     [• •] ║",
                "   ╔═|===|═╣",
                "   ║ /   \\ ║",
                "   ╚/_____\\╝"),
            ["C004"] = Portrait(
                "      ___",
                "     /_•_)  †",
                "    /|___|--╯",
                "     /  \\",
                "    /_  _\\"),
            ["C005"] = Portrait(
                "      _†_",
                "     (• •)  ☼",
                "    /|___|--┤",
                "     |   |",
                "    /_____\\"),
            ["C006"] = Portrait(
                "      /\\   ✦",
                "     /__\\ ( )",
                "     (••)--╂",
                "    /|~~|  │",
                "     /__\\ │")
        };

    private static readonly IReadOnlyDictionary<string, AsciiPortrait> Enemies =
        new Dictionary<string, AsciiPortrait>(StringComparer.OrdinalIgnoreCase)
        {
            ["E001"] = Portrait(
                "",
                "    /╲_/\\",
                "   ( o.o )__",
                "    > ^ <  ╲╲__",
                "           ╲___)"),
            ["E002"] = Portrait(
                "      /\\",
                "   __/..\\__",
                "  /  (oo)  ╲  /",
                "  ╲__|==|__╲/",
                "     /  \\"),
            ["E003"] = Portrait(
                "    /╲____/\\",
                "   /  >  <  \\",
                "  (    ▽    )--†",
                "   ╲__|_|__/",
                "      / \\"),
            ["E004"] = Portrait(
                "       __",
                "      (00) │",
                "     /|_|--†",
                "      / \\",
                "     /___\\"),
            ["E005"] = Portrait(
                "    /╲      /\\",
                "   /  ╲____/  \\",
                "  /  (•  •)   \\",
                "  ╲___/△╲___/",
                "     /  \\"),
            ["E006"] = Portrait(
                "      ____",
                "     (x  •)",
                "    /|___|╲_",
                "     |   |  ╲",
                "    /_╲ /_╲"),
            ["E007"] = Portrait(
                "     _/╲_",
                "   _(ò  ó)_",
                "  /  |##|  ╲__",
                "  ╲__|  |__/",
                "     /  \\"),
            ["E008"] = Portrait(
                "    __/╲__",
                "   / (• •) \\",
                "  /__|██|__╲-->",
                "     |  |",
                "    /_/\\_\\"),
            ["E009"] = Portrait(
                "      /╲  /\\",
                "   __/ (••) ╲__",
                "  /_ ╲_(==)_/ _\\",
                " /  ╲_/ || ╲_/  \\",
                "     /_||_\\"),
            ["E010"] = Portrait(
                "      /╲___",
                "   __/ •  •╲__",
                "  /   ╲_▲_/  ╲--",
                "  ╲__|===|__/",
                "     /   \\")
        };

    private static readonly AsciiPortrait Unknown = Portrait(
        "      ???",
        "     (? ?)",
        "    /|___|╲",
        "     /   \\",
        "    /_____\\");

    public static AsciiPortrait ForCharacterClass(string classId) =>
        CharacterClasses.GetValueOrDefault(classId, Unknown);

    public static AsciiPortrait ForEnemy(string enemyId) =>
        Enemies.GetValueOrDefault(enemyId, Unknown);

    private static AsciiPortrait Portrait(params string[] lines) => new(lines, CanvasWidth);
}

public sealed record AsciiPortrait(IReadOnlyList<string> Lines, int CanvasWidth);
