using System.Diagnostics;

namespace KaoszRubin.UI;

/// <summary>Lassú, szinuszos magentafényt futtat a főmenü központi rubinja körül.</summary>
internal sealed class RubyPulseEffect
{
    private const int Left = 54;
    private const int Top = 15;
    private const int Width = 40;
    private const int Height = 26;
    private const double PulseIntervalSeconds = 30;
    private const double LocalPulseDurationSeconds = 1.7;
    private const double OutwardDelaySeconds = 0.65;
    private readonly string[] _artLines;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    public RubyPulseEffect(string art) =>
        _artLines = art.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n');

    internal static double BrightnessAt(double elapsedSeconds, double distance = 0) =>
        PulseBrightness(elapsedSeconds % PulseIntervalSeconds - distance * OutwardDelaySeconds);

    private static double PulseBrightness(double localTime)
    {
        if (localTime is < 0 or > LocalPulseDurationSeconds) return 0;
        return Math.Sin(localTime / LocalPulseDurationSeconds * Math.PI);
    }

    public void Render(int consoleWidth, int consoleHeight)
    {
        var elapsedSeconds = _clock.Elapsed.TotalSeconds;
        var bottom = Math.Min(Top + Height, Math.Min(consoleHeight, _artLines.Length));
        var right = Math.Min(Left + Width, consoleWidth);
        if (bottom <= Top || right <= Left) return;

        var centerX = Left + Width / 2d;
        var centerY = Top + Height / 2d;
        for (var y = Top; y < bottom; y++)
        {
            var line = _artLines[y];
            if (line.Length <= Left) continue;
            Console.SetCursorPosition(Left, y);
            for (var x = Left; x < right; x++)
            {
                var character = x < line.Length ? line[x] : ' ';
                var normalizedX = Math.Abs(x - centerX) / (Width / 2d);
                var normalizedY = Math.Abs(y - centerY) / (Height / 2d);
                var distance = Math.Sqrt(normalizedX * normalizedX + normalizedY * normalizedY);
                // A távolság fáziseltolása miatt a fény nem egyszerre villan fel:
                // lassú, körkörös hullámként indul a rubintól és halad kifelé.
                var localBrightness = BrightnessAt(elapsedSeconds, distance);
                Console.ForegroundColor = PulseColor(localBrightness);
                Console.Write(character == ' ' && distance is > 0.28 and < 1.12 && localBrightness > 0.88
                    ? '·'
                    : character);
            }
        }
        Console.ResetColor();
    }

    internal static ConsoleColor PulseColor(double brightness) => brightness switch
    {
        < 0.28 => ConsoleColor.DarkMagenta,
        < 0.78 => ConsoleColor.Magenta,
        _ => ConsoleColor.White
    };
}
