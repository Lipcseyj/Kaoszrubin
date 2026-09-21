using System.Diagnostics;

namespace KaoszRubin.UI;

/// <summary>
/// Alulról felfelé végigfutó, a képernyőt maga mögött elfeketítő tűzátmenet.
/// </summary>
internal static class ScreenBurnEffect
{
    internal const int FrameCount = 26;

    private const int FrameDurationMilliseconds = 32;
    private const int FlameDepth = 4;

    private const int MinWave = -2;
    private const int MaxWave = 2;

    private static readonly char[] FlameChars = ['█', '▓', '▒', '░'];

    internal static int FrontRowAt(int frame, int x, int height)
        => BaseFrontAt(frame, height) + ColumnWave(x);

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
        if (Console.IsOutputRedirected)
            return;

        try
        {
            var width = Math.Max(1, Console.WindowWidth);
            var height = Math.Max(1, Console.WindowHeight);

            // A hullám oszloponként állandó, ezért egyszer számoljuk ki.
            var columnWaves = new int[width];

            for (var x = 0; x < width; x++)
                columnWaves[x] = ColumnWave(x);

            // Egyetlen sor-buffer, nincs frame-enkénti string-allokáció.
            var rowBuffer = new char[width];

            Console.CursorVisible = false;
            Console.BackgroundColor = ConsoleColor.Black;

            for (var frame = 0; frame < FrameCount; frame++)
            {
                var stopwatch = Stopwatch.StartNew();

                RenderFrame(
                    frame,
                    width,
                    height,
                    columnWaves,
                    rowBuffer);

                stopwatch.Stop();

                // A renderelési idő LEVONÓDIK a frame idejéből.
                var remaining =
                    FrameDurationMilliseconds -
                    (int)stopwatch.ElapsedMilliseconds;

                if (remaining > 0)
                    Thread.Sleep(remaining);
            }

            Console.ResetColor();
            Console.Clear();
        }
        catch (Exception exception) when (
            exception is IOException or
            InvalidOperationException or
            ArgumentOutOfRangeException)
        {
            Console.ResetColor();
        }
    }

    private static void RenderFrame(
        int frame,
        int width,
        int height,
        int[] columnWaves,
        char[] rowBuffer)
    {
        var baseFront = BaseFrontAt(frame, height);

        // Ennél magasabban biztosan nincs még tűz.
        var firstY = Math.Max(
            0,
            baseFront + MinWave);

        /*
         * Nem rajzoljuk újra az egész már kiégett képernyőt.
         *
         * Csak addig kell lefelé renderelnünk, ahol az előző
         * frame tűzcsóvája még lehetett.
         *
         * Ami ez alatt van, az már fekete volt az előző frame-ben.
         */
        int lastY;

        if (frame == 0)
        {
            lastY = height - 1;
        }
        else
        {
            var previousBaseFront =
                BaseFrontAt(frame - 1, height);

            lastY = Math.Min(
                height - 1,
                previousBaseFront + MaxWave + FlameDepth);
        }

        ConsoleColor? currentColor = null;

        for (var y = firstY; y <= lastY; y++)
        {
            var x = 0;

            while (x < width)
            {
                var state = GetCellState(
                    x,
                    y,
                    baseFront,
                    columnWaves,
                    out var distance);

                // Még nem érte el a tűz:
                // SEMMIT nem írunk, így a régi kép megmarad.
                if (state == BurnCellState.Untouched)
                {
                    x++;
                    continue;
                }

                var runStart = x;
                var runState = state;

                /*
                 * Egyforma színű cellákat egyetlen Console.Write-tal
                 * írunk ki.
                 */
                while (x < width)
                {
                    var currentState = GetCellState(
                        x,
                        y,
                        baseFront,
                        columnWaves,
                        out distance);

                    if (currentState != runState)
                        break;

                    rowBuffer[x] = runState switch
                    {
                        BurnCellState.Burned => ' ',

                        _ => FlameChars[
                            (distance + x + frame)
                            % FlameChars.Length]
                    };

                    x++;
                }

                var color = GetStateColor(runState);

                if (currentColor != color)
                {
                    Console.ForegroundColor = color;
                    currentColor = color;
                }

                Console.SetCursorPosition(runStart, y);

                Console.Write(
                    rowBuffer,
                    runStart,
                    x - runStart);
            }
        }
    }

    private static BurnCellState GetCellState(
        int x,
        int y,
        int baseFront,
        int[] columnWaves,
        out int distance)
    {
        var front =
            baseFront + columnWaves[x];

        distance = y - front;

        if (distance < 0)
            return BurnCellState.Untouched;

        if (distance >= FlameDepth)
            return BurnCellState.Burned;

        return distance switch
        {
            0 => BurnCellState.White,
            1 => BurnCellState.Yellow,
            2 => BurnCellState.Red,
            _ => BurnCellState.DarkRed
        };
    }

    private static ConsoleColor GetStateColor(
        BurnCellState state) =>
        state switch
        {
            BurnCellState.White => ConsoleColor.White,
            BurnCellState.Yellow => ConsoleColor.Yellow,
            BurnCellState.Red => ConsoleColor.Red,
            BurnCellState.DarkRed => ConsoleColor.DarkRed,

            // Szóközt írunk fekete háttérre.
            BurnCellState.Burned => ConsoleColor.Black,

            _ => ConsoleColor.Black
        };

    private static int BaseFrontAt(int frame, int height)
    {
        if (height <= 0)
            return 0;

        var progress =
            Math.Clamp(frame, 0, FrameCount - 1) /
            (double)(FrameCount - 1);

        return height - 1 -
               (int)Math.Ceiling(
                   progress *
                   (height + FlameDepth + 2));
    }

    private static int ColumnWave(int x)
    {
        var hash =
            unchecked(x * 1103515245 + 12345);

        return Math.Abs(hash % 5) - 2;
    }

    private enum BurnCellState : byte
    {
        Untouched,
        White,
        Yellow,
        Red,
        DarkRed,
        Burned
    }
}