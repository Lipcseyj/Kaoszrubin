namespace KaoszRubin.UI;

/// <summary>
/// Blokkoló személyes ablakok alatt is figyeli a coop állapotot, és a legfelső sorban jelzi,
/// ha a másik játékos ablaka vagy egy közös esemény várakozik.
/// </summary>
public static class CoopWindowStatusBanner
{
    public static ConsoleKeyInfo ReadKey(Func<string?>? statusProvider = null)
    {
        if (statusProvider is null) return Console.ReadKey(intercept: true);
        string? previous = null;
        while (true)
        {
            string? current;
            try { current = statusProvider(); }
            catch { current = null; }
            if (!string.Equals(previous, current, StringComparison.Ordinal))
            {
                Draw(current);
                previous = current;
            }
            try
            {
                if (Console.KeyAvailable) return Console.ReadKey(intercept: true);
            }
            catch (Exception exception) when (TerminalViewport.IsTransientConsoleException(exception))
            {
            }
            Thread.Sleep(20);
        }
    }

    public static void Refresh(Func<string?>? statusProvider)
    {
        string? current = null;

        if (statusProvider is not null)
        {
            try
            {
                current = statusProvider();
            }
            catch
            {
            }
        }

        Draw(current);
    }

    public static void Clear()
    {
        Draw(null);
    }

    private static readonly Lock Gate = new();
    private static BackgroundContentRestorer? _savedBackground;

    private static void Draw(string? message)
    {
        int width;
        try { width = Math.Min(Console.WindowWidth, 170); }
        catch (IOException) { return; }

        lock (Gate)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                // A sáv eltűnésekor a mögötte lévő eredeti tartalom áll vissza, nem üres sor.
                var restorer = _savedBackground;
                _savedBackground = null;
                restorer?.Dispose();
                return;
            }

            // Az első megjelenítéskor mentjük a legfelső sort; az üzenetváltás nem írhatja felül a mentést.
            _savedBackground ??= new BackgroundContentRestorer(0, 0, width, 1);

            var text = BattleCommandPanel.TruncateToDisplayWidth($" FIGYELEM: {message} ", width);
            try
            {
                Console.SetCursorPosition(0, 0);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.BackgroundColor = ConsoleColor.DarkRed;
                Console.Write(text.PadRight(width));
                Console.ResetColor();
            }
            catch (Exception exception) when (TerminalViewport.IsTransientConsoleException(exception))
            {
            }
        }
    }
}
