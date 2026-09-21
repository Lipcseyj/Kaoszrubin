using System.Diagnostics;

namespace KaoszRubin.UI;

/// <summary>
/// Lassú, hullámszerű magentafényt futtat a főmenü központi rubinja körül.
/// </summary>
internal sealed class RubyPulseEffect
{
    private const int Left = 54;
    private const int Top = 15;
    private const int Width = 40;
    private const int Height = 26;

    private const double PulseIntervalSeconds = 30;
    private const double LocalPulseDurationSeconds = 1.7;
    private const double OutwardDelaySeconds = 0.65;

    /*
     * A PulseBrightness():
     *
     * sin(t / duration * PI)
     *
     * görbéjének küszöbértékei.
     *
     * Ezekkel rendereléskor már nem kell Math.Sin().
     */
    private static readonly double Bright28Rise =
        LocalTimeForBrightness(0.28);

    private static readonly double Bright28Fall =
        LocalPulseDurationSeconds - Bright28Rise;

    private static readonly double Bright78Rise =
        LocalTimeForBrightness(0.78);

    private static readonly double Bright78Fall =
        LocalPulseDurationSeconds - Bright78Rise;

    private static readonly double Bright88Rise =
        LocalTimeForBrightness(0.88);

    private static readonly double Bright88Fall =
        LocalPulseDurationSeconds - Bright88Rise;

    private readonly Stopwatch _clock = Stopwatch.StartNew();

    /*
     * Csak a tényleges 40×26-os területet tároljuk.
     */
    private readonly char[] _characters =
        new char[Width * Height];

    /*
     * Cellánként előre kiszámoljuk a hullám késleltetését.
     *
     * Így Render() közben nincs:
     * - Math.Abs
     * - osztás
     * - Math.Sqrt
     * - distance * delay
     */
    private readonly double[] _phaseDelay =
        new double[Width * Height];

    /*
     * Előre tudjuk azt is, mely cellákban jelenhet meg
     * a pulzáló '·' részecske.
     */
    private readonly bool[] _sparkEligible =
        new bool[Width * Height];

    /*
     * Egyetlen újrahasznosított sor-buffer.
     */
    private readonly char[] _renderBuffer =
        new char[Width];

    public RubyPulseEffect(string art)
    {
        var artLines =
            art.Replace(
                    "\r",
                    string.Empty,
                    StringComparison.Ordinal)
                .Split('\n');

        var centerX = Left + Width / 2d;
        var centerY = Top + Height / 2d;

        for (var localY = 0; localY < Height; localY++)
        {
            var screenY = Top + localY;

            var sourceLine =
                screenY < artLines.Length
                    ? artLines[screenY]
                    : string.Empty;

            for (var localX = 0; localX < Width; localX++)
            {
                var screenX = Left + localX;
                var index = localY * Width + localX;

                _characters[index] =
                    screenX < sourceLine.Length
                        ? sourceLine[screenX]
                        : ' ';

                var normalizedX =
                    Math.Abs(screenX - centerX) /
                    (Width / 2d);

                var normalizedY =
                    Math.Abs(screenY - centerY) /
                    (Height / 2d);

                var distance =
                    Math.Sqrt(
                        normalizedX * normalizedX +
                        normalizedY * normalizedY);

                _phaseDelay[index] =
                    distance * OutwardDelaySeconds;

                _sparkEligible[index] =
                    distance is > 0.28 and < 1.12;
            }
        }
    }

    // ============================================================
    // 🧪 EREDETI SEGÉDFÜGGVÉNYEK
    // ============================================================

    internal static double BrightnessAt(
        double elapsedSeconds,
        double distance = 0) =>
        PulseBrightness(
            elapsedSeconds % PulseIntervalSeconds -
            distance * OutwardDelaySeconds);

    private static double PulseBrightness(double localTime)
    {
        if (localTime is < 0 or > LocalPulseDurationSeconds)
            return 0;

        return Math.Sin(
            localTime /
            LocalPulseDurationSeconds *
            Math.PI);
    }

    internal static ConsoleColor PulseColor(
        double brightness) =>
        brightness switch
        {
            < 0.28 => ConsoleColor.DarkMagenta,
            < 0.78 => ConsoleColor.Magenta,
            _ => ConsoleColor.White
        };


    // ============================================================
    // 💎 RENDER
    // ============================================================

    public void Render(
        int consoleWidth,
        int consoleHeight)
    {
        var renderWidth =
            Math.Clamp(
                consoleWidth - Left,
                0,
                Width);

        var renderHeight =
            Math.Clamp(
                consoleHeight - Top,
                0,
                Height);

        if (renderWidth <= 0 ||
            renderHeight <= 0)
        {
            return;
        }

        /*
         * Modulo csak EGYSZER frame-enként.
         */
        var pulseTime =
            _clock.Elapsed.TotalSeconds %
            PulseIntervalSeconds;

        ConsoleColor? currentColor = null;

        for (var localY = 0;
             localY < renderHeight;
             localY++)
        {
            var rowOffset =
                localY * Width;

            /*
             * Soronként egyetlen kurzormozgatás.
             */
            Console.SetCursorPosition(
                Left,
                Top + localY);

            var x = 0;

            while (x < renderWidth)
            {
                var index =
                    rowOffset + x;

                var localTime =
                    pulseTime -
                    _phaseDelay[index];

                var color =
                    FastPulseColor(localTime);

                var runStart = x;

                // ------------------------------------------------
                // Azonos színű szakasz összegyűjtése
                // ------------------------------------------------

                while (x < renderWidth)
                {
                    index =
                        rowOffset + x;

                    localTime =
                        pulseTime -
                        _phaseDelay[index];

                    var cellColor =
                        FastPulseColor(localTime);

                    if (cellColor != color)
                        break;

                    var character =
                        _characters[index];

                    /*
                     * Rubinparázs csak:
                     *
                     * - üres háttéren
                     * - megfelelő távolságban
                     * - a pulzus csúcsán
                     */
                    if (character == ' ' &&
                        _sparkEligible[index] &&
                        IsSparkBright(localTime))
                    {
                        character = '·';
                    }

                    _renderBuffer[x] =
                        character;

                    x++;
                }

                // ------------------------------------------------
                // Színt csak valódi változáskor állítunk
                // ------------------------------------------------

                if (currentColor != color)
                {
                    Console.ForegroundColor =
                        color;

                    currentColor =
                        color;
                }

                /*
                 * Egy egész színszakasz egyetlen Console.Write().
                 */
                Console.Write(
                    _renderBuffer,
                    runStart,
                    x - runStart);
            }
        }

        Console.ResetColor();
    }


    // ============================================================
    // ⚡ GYORS PULZUS-KIÉRTÉKELÉS
    // ============================================================

    private static ConsoleColor FastPulseColor(
        double localTime)
    {
        /*
         * Pulzuson kívül:
         */
        if (localTime < 0 ||
            localTime > LocalPulseDurationSeconds)
        {
            return ConsoleColor.DarkMagenta;
        }

        /*
         * A szinuszgörbe szimmetrikus:
         *
         *       WHITE
         *      /     \
         * MAGENTA     MAGENTA
         *   /           \
         * DARK           DARK
         */

        if (localTime < Bright28Rise ||
            localTime > Bright28Fall)
        {
            return ConsoleColor.DarkMagenta;
        }

        if (localTime < Bright78Rise ||
            localTime > Bright78Fall)
        {
            return ConsoleColor.Magenta;
        }

        return ConsoleColor.White;
    }

    private static bool IsSparkBright(
        double localTime) =>
        localTime >= Bright88Rise &&
        localTime <= Bright88Fall;


    // ============================================================
    // 🧮 ELŐSZÁMÍTÁS
    // ============================================================

    private static double LocalTimeForBrightness(
        double brightness)
    {
        return Math.Asin(brightness) /
               Math.PI *
               LocalPulseDurationSeconds;
    }
}