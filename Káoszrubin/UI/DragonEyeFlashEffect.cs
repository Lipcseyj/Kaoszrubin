using System.Diagnostics;

namespace KaoszRubin.UI;

/// <summary>A főmenü sárkányának ritkán felizzó szemei.</summary>
internal sealed class DragonEyeFlashEffect
{
    private const int EyeRow = 24;
    private static readonly int[] EyeColumns = [17, 20];
    private const double FlashIntervalSeconds = 45;
    private const double FlashDurationSeconds = 3;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    internal static double BrightnessAt(double elapsedSeconds)
    {
        var localTime = elapsedSeconds % FlashIntervalSeconds;
        if (localTime > FlashDurationSeconds) return 0;
        return Math.Sin(localTime / FlashDurationSeconds * Math.PI);
    }

    public void Render(int consoleWidth, int consoleHeight)
    {
        if (EyeRow >= consoleHeight) return;
        var brightness = BrightnessAt(_clock.Elapsed.TotalSeconds);
        foreach (var column in EyeColumns)
        {
            if (column >= consoleWidth) continue;
            Console.SetCursorPosition(column, EyeRow);
            Console.ForegroundColor = EyeColor(brightness);
            Console.Write(brightness > 0.08 ? '●' : '0');
        }
        Console.ResetColor();
    }

    internal static ConsoleColor EyeColor(double brightness) => brightness switch
    {
        < 0.08 => ConsoleColor.DarkMagenta,
        < 0.35 => ConsoleColor.DarkRed,
        < 0.68 => ConsoleColor.Red,
        < 0.9 => ConsoleColor.Yellow,
        _ => ConsoleColor.White
    };
}
