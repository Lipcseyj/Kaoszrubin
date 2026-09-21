namespace KaoszRubin.UI;

/// <summary>Alulról felfelé végigfutó, a képernyőt maga mögött elfeketítő tűzátmenet.</summary>
internal static class ScreenBurnEffect
{
    internal const int FrameCount = 26;
    private const int FrameDelayMilliseconds = 32;
    private const int FlameDepth = 4;
    private static readonly char[] FlameChars = ['█', '▓', '▒', '░'];

    internal static int FrontRowAt(int frame, int x, int height)
    {
        if (height <= 0) return 0;
        var progress = Math.Clamp(frame, 0, FrameCount - 1) / (double)(FrameCount - 1);
        var wave = ColumnWave(x);
        return height - 1 - (int)Math.Ceiling(progress * (height + FlameDepth + 2)) + wave;
    }

    internal static bool IsBurned(int frame, int x, int y, int height) =>
        y >= FrontRowAt(frame, x, height);

    internal static ConsoleColor FlameColor(int distanceFromFront) => distanceFromFront switch
    {
        <= 0 => ConsoleColor.White,
        1 => ConsoleColor.Yellow,
        2 => ConsoleColor.Red,
        _ => ConsoleColor.DarkRed
    };

    public static void Play()
    {
        if (Console.IsOutputRedirected) return;
        try
        {
            var width = Math.Max(1, Console.WindowWidth);
            var height = Math.Max(1, Console.WindowHeight);
            Console.CursorVisible = false;
            Console.BackgroundColor = ConsoleColor.Black;
            for (var frame = 0; frame < FrameCount; frame++)
            {
                RenderFrame(frame, width, height);
                Thread.Sleep(FrameDelayMilliseconds);
            }
            Console.ResetColor();
            Console.Clear();
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or
                                           ArgumentOutOfRangeException)
        {
            Console.ResetColor();
        }
    }

    private static void RenderFrame(int frame, int width, int height)
    {
        var highestFront = Enumerable.Range(0, width).Min(x => FrontRowAt(frame, x, height));
        for (var y = Math.Max(0, highestFront); y < height; y++)
        {
            Console.SetCursorPosition(0, y);
            for (var x = 0; x < width; x++)
            {
                var distance = y - FrontRowAt(frame, x, height);
                if (distance < 0)
                {
                    // Az adott oszlopot még nem érte el a hullám: a régi kép maradjon látható.
                    if (x + 1 < width) Console.SetCursorPosition(x + 1, y);
                    continue;
                }

                Console.BackgroundColor = ConsoleColor.Black;
                if (distance < FlameDepth)
                {
                    Console.ForegroundColor = FlameColor(distance);
                    Console.Write(FlameChars[(distance + x + frame) % FlameChars.Length]);
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Black;
                    Console.Write(' ');
                }
            }
        }
    }

    private static int ColumnWave(int x)
    {
        var hash = unchecked(x * 1103515245 + 12345);
        return Math.Abs(hash % 5) - 2;
    }
}
