namespace MazeGame;

/// <summary>A jobb alsó képpanel rövid, egycellás ASCII-portréi. Új szereplők itt vehetők fel.</summary>
public static class AsciiPortraits
{
    private static readonly AsciiPortrait Warrior = new([
        "       /\\",
        "      /  \\",
        "      |[]|",
        "     ( O )",
        "    /|===|--->",
        "   /_|___|_\\",
        "      / \\",
        "     /   \\",
        "    /_____\\",
        "     |___|"
    ], 16);
    private static readonly AsciiPortrait Skeleton = new([
        "      .-.",
        "     (o o)",
        "      \\_/",
        "     /|_|--o",
        "    /_|___|_\\",
        "      / \\",
        "     /   \\",
        "    /_____\\"
    ], 16);

    public static AsciiPortrait Get(AsciiPortraitKind kind) => kind switch
    {
        AsciiPortraitKind.Skeleton => Skeleton,
        _ => Warrior
    };
}

public enum AsciiPortraitKind { Warrior, Skeleton }
public sealed record AsciiPortrait(IReadOnlyList<string> Lines, int CanvasWidth);
