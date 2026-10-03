namespace KaoszRubin.UI;

internal readonly record struct InnSurfaceRegion(int Left, int Top, int Width, int Height)
{
    private const int PreferredRightExclusive = 170;
    private const int PreferredBottomExclusive = 44;

    internal static InnSurfaceRegion ForViewport(int windowWidth, int windowHeight)
    {
        const int left = 0;
        const int top = 0;
        var right = Math.Max(left + 1, Math.Min(PreferredRightExclusive, Math.Max(1, windowWidth - 1)));
        var bottom = Math.Max(top + 1, Math.Min(PreferredBottomExclusive, Math.Max(2, windowHeight - 1)));
        return new InnSurfaceRegion(left, top, right - left, bottom - top);
    }

    internal (int Left, int Top) Center(int width, int height) =>
        (Left + Math.Max(0, (Width - width) / 2), Top + Math.Max(0, (Height - height) / 2));
}
