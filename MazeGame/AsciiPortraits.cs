namespace MazeGame;

/// <summary>A jobb alsó képpanel rövid, egycellás ASCII-portréi. Új szereplők itt vehetők fel.</summary>
public static class AsciiPortraits
{
    private static readonly AsciiPortrait Warrior = new([
        "      /\\",
        "     (••)  ⚔",
        "    /|==|--╪",
        "     /  \\",
        "    /____\\"
    ], 14);
    private static readonly AsciiPortrait Skeleton = new([
        "      .-.",
        "     (☠ ☠)",
        "     /|_|--†",
        "      / \\",
        "     /___\\"
    ], 14);

    public static AsciiPortrait Get(AsciiPortraitKind kind) => kind switch
    {
        AsciiPortraitKind.Skeleton => Skeleton,
        _ => Warrior
    };
}

public enum AsciiPortraitKind { Warrior, Skeleton }
public sealed record AsciiPortrait(IReadOnlyList<string> Lines, int CanvasWidth);
