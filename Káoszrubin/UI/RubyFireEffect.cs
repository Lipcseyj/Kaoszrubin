namespace KaoszRubin.UI;

/// <summary>
/// Keskeny, konzolos rubintűz-animáció a főmenü alsó sávjához.
/// Optimalizálva nagyobb, pl. 200×8-as területhez.
/// </summary>
internal sealed class RubyFireEffect
{
    private static readonly char[] FireChars =
    [
        ' ',
        '·',
        '.',
        ':',
        '*',
        '•',
        '░',
        '▒',
        '▓',
        '█'
    ];

    /// <summary>
    /// Intenzitás -> konzolszín.
    /// Lookup table, így renderelés közben nincs switch.
    /// </summary>
    private static readonly ConsoleColor[] FireColors =
    [
        ConsoleColor.Black,       // 0
        ConsoleColor.DarkGray,    // 1
        ConsoleColor.DarkMagenta, // 2
        ConsoleColor.Magenta,     // 3
        ConsoleColor.Magenta,     // 4
        ConsoleColor.DarkRed,     // 5
        ConsoleColor.Red,         // 6
        ConsoleColor.Red,         // 7
        ConsoleColor.DarkYellow,  // 8
        ConsoleColor.White        // 9
    ];

    private readonly Random _random;

    /*
     * Lapos tömb:
     *
     * [sor 0................]
     * [sor 1................]
     * [sor 2................]
     *
     * index = y * Width + x
     *
     * Gyorsabb és egyszerűbb a forró ciklusban, mint a byte[,].
     */
    private readonly byte[] _fire;

    /*
     * Előre kiszámolt szomszédok.
     *
     * Így Update() közben nincs modulo / Wrap().
     */
    private readonly int[] _left;
    private readonly int[] _right;

    /*
     * Egyetlen újrahasznosított render buffer.
     *
     * Nem készítünk frame-enként új stringeket vagy char tömböket.
     */
    private readonly char[] _renderBuffer;

    private readonly int _halfHeight;

    public RubyFireEffect(
        int width,
        int height,
        Random? random = null)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);

        _random = random ?? new Random();

        _fire = new byte[Width * Height];
        _renderBuffer = new char[Width];

        _left = new int[Width];
        _right = new int[Width];

        _halfHeight = Height / 2;

        // Szomszédos X koordináták előre kiszámítása.
        for (var x = 0; x < Width; x++)
        {
            _left[x] =
                x == 0
                    ? Width - 1
                    : x - 1;

            _right[x] =
                x == Width - 1
                    ? 0
                    : x + 1;
        }
    }

    public int Width { get; }

    public int Height { get; }

    internal byte IntensityAt(int x, int y) =>
        _fire[y * Width + x];

    // ============================================================
    // 🔥 TŰZ SZIMULÁCIÓ
    // ============================================================

    public void Update()
    {
        var bottom = Height - 1;
        var bottomOffset = bottom * Width;

        // --------------------------------------------------------
        // 🔥 Tűzforrás – alsó sor
        // --------------------------------------------------------

        for (var x = 0; x < Width; x++)
        {
            var index = bottomOffset + x;

            var previous = _fire[index];
            var pulse = _random.Next(100);

            var source = pulse switch
            {
                < 7 => 3,
                < 25 => 6,
                < 78 => 8,
                _ => 9
            };

            _fire[index] =
                (byte)((previous + source * 2) / 3);
        }

        // --------------------------------------------------------
        // 🔥 Láng terjedése felfelé
        // --------------------------------------------------------

        for (var y = 0; y < bottom; y++)
        {
            var rowOffset = y * Width;
            var sourceOffset = (y + 1) * Width;

            var deeperY =
                y + 2 <= bottom
                    ? y + 2
                    : bottom;

            var deeperOffset = deeperY * Width;

            /*
             * Fent gyorsabban hűl a tűz.
             *
             * Random.Next(3) => 0,1,2
             * Random.Next(2) => 0,1
             */
            var coolingRange =
                y < _halfHeight
                    ? 3
                    : 2;

            for (var x = 0; x < Width; x++)
            {
                // ----------------------------------------------
                // 🌬️ Enyhe oldalirányú sodródás
                // ----------------------------------------------

                var drift = _random.Next(-1, 2);

                var center = x + drift;

                if (center < 0)
                    center = Width - 1;
                else if (center >= Width)
                    center = 0;

                // ----------------------------------------------
                // 🔥 Környező hő összegyűjtése
                // ----------------------------------------------

                var sum =
                    _fire[sourceOffset + center] * 3 +
                    _fire[sourceOffset + _left[center]] +
                    _fire[sourceOffset + _right[center]] +
                    _fire[deeperOffset + center];

                var value =
                    sum / 6 -
                    _random.Next(coolingRange);

                // ----------------------------------------------
                // ✨ Ritka felszálló rubinparázs
                // ----------------------------------------------

                if (value < 3 &&
                    _random.Next(1000) < 7)
                {
                    value = _random.Next(3, 6);
                }

                /*
                 * Normális esetben value eleve 0–9 között lesz,
                 * de biztonságból clampeljük.
                 */
                _fire[rowOffset + x] =
                    (byte)Math.Clamp(value, 0, 9);
            }
        }
    }

    // ============================================================
    // 🖥️ RENDER
    // ============================================================

    public void Render(
        int left,
        int top,
        int visibleWidth,
        int visibleHeight)
    {
        var renderWidth =
            Math.Clamp(visibleWidth, 0, Width);

        var renderHeight =
            Math.Clamp(visibleHeight, 0, Height);

        if (renderWidth == 0 ||
            renderHeight == 0)
        {
            return;
        }

        /*
         * A háttér mindig fekete.
         *
         * Ezt frame-enként csak egyszer állítjuk.
         */
        Console.BackgroundColor = ConsoleColor.Black;

        ConsoleColor? currentColor = null;

        for (var y = 0; y < renderHeight; y++)
        {
            var rowOffset = y * Width;

            /*
             * FONTOS:
             *
             * Soronként csak EGYSZER mozgatjuk a kurzort.
             *
             * Utána a Console.Write() automatikusan jobbra halad.
             */
            Console.SetCursorPosition(left, top + y);

            var x = 0;

            while (x < renderWidth)
            {
                var intensity =
                    _fire[rowOffset + x];

                var color =
                    FireColors[intensity];

                var runStart = x;

                // ------------------------------------------------
                // Egyforma SZÍNŰ szakasz összegyűjtése
                // ------------------------------------------------

                while (x < renderWidth)
                {
                    var cellIntensity =
                        _fire[rowOffset + x];

                    if (FireColors[cellIntensity] != color)
                        break;

                    _renderBuffer[x] =
                        FireChars[cellIntensity];

                    x++;
                }

                // ------------------------------------------------
                // Szín csak akkor változik, ha tényleg szükséges
                // ------------------------------------------------

                if (currentColor != color)
                {
                    Console.ForegroundColor = color;
                    currentColor = color;
                }

                /*
                 * Egy teljes azonos színű blokk egyetlen
                 * Console.Write().
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
    // 🎨 SEGÉD
    // ============================================================

    internal static ConsoleColor FireColor(byte intensity) =>
        FireColors[Math.Min(
            intensity,
            (byte)(FireColors.Length - 1))];
}