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

    private static void Draw(string? message)
    {
        int width;
        try { width = Math.Max(Console.WindowWidth, 170); }
        catch (IOException) { return; }
        var text = string.IsNullOrWhiteSpace(message) ? string.Empty : $" FIGYELEM: {message} ";
        text = BattleCommandPanel.TruncateToDisplayWidth(text, width);
        try
        {
            Console.SetCursorPosition(0, 0);
            Console.ForegroundColor = string.IsNullOrWhiteSpace(message) ? ConsoleColor.Gray : ConsoleColor.Yellow;
            Console.BackgroundColor = string.IsNullOrWhiteSpace(message) ? ConsoleColor.Black : ConsoleColor.DarkRed;
            Console.Write(text.PadRight(width));
            Console.ResetColor();
        }
        catch (Exception exception) when (TerminalViewport.IsTransientConsoleException(exception))
        {
        }
    }
}
