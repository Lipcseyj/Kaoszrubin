namespace KaoszRubin.UI;

/// <summary>Közös, kivételbiztos konzolméret-kezelés az átméretezés idejére.</summary>
public static class TerminalViewport
{
    public readonly record struct Size(int Width, int Height)
    {
        public bool CanFit(int minimumWidth, int minimumHeight) =>
            Width >= minimumWidth && Height >= minimumHeight;
    }

    public static bool TryGetSize(out Size size)
    {
        try
        {
            size = new Size(
                Math.Min(Console.WindowWidth, Console.BufferWidth),
                Math.Min(Console.WindowHeight, Console.BufferHeight));
            return size.Width > 0 && size.Height > 0;
        }
        catch (Exception exception) when (IsTransientConsoleException(exception))
        {
            size = default;
            return false;
        }
    }

    public static void DrawSizeWarning(Size size, int minimumWidth, int minimumHeight)
    {
        try
        {
            Console.ResetColor();
            Console.Clear();
            var lines = new[]
            {
                "A KÁOSZRUBIN VÁR AZ ABLAKRA",
                "",
                $"A játékhoz legalább {minimumWidth} oszlop és {minimumHeight} sor szükséges.",
                $"Jelenlegi méret: {size.Width} oszlop, {size.Height} sor.",
                "",
                "Növeld meg vagy maximalizáld a terminálablakot.",
                "A játék automatikusan folytatódik, amint ismét elfér."
            };
            var top = Math.Max(0, (size.Height - lines.Length) / 2);
            for (var index = 0; index < lines.Length && top + index < size.Height; index++)
            {
                var line = lines[index];
                if (line.Length > size.Width) line = line[..size.Width];
                var left = Math.Max(0, (size.Width - line.Length) / 2);
                Console.SetCursorPosition(left, top + index);
                Console.Write(line);
            }
        }
        catch (Exception exception) when (IsTransientConsoleException(exception))
        {
            // A felhasználó még húzza az ablak szélét; a következő ciklus újrapróbálja.
        }
    }

    public static bool IsTransientConsoleException(Exception exception) =>
        (exception is IOException or ArgumentOutOfRangeException or InvalidOperationException)
        && (exception.StackTrace?.Contains("System.Console", StringComparison.Ordinal) == true
            || exception is IOException);
}
