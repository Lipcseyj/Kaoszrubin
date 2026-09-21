namespace KaoszRubin.UI;

/// <summary>Keskeny, konzolos rubintűz-animáció a főmenü alsó sávjához.</summary>
internal sealed class RubyFireEffect(int width, int height, Random? random = null)
{
    private static readonly char[] FireChars = [' ', '·', '.', ':', '*', '•', '░', '▒', '▓', '█'];
    private readonly Random _random = random ?? new Random();
    private readonly byte[,] _fire = new byte[Math.Max(1, width), Math.Max(1, height)];

    public int Width => _fire.GetLength(0);
    public int Height => _fire.GetLength(1);

    internal byte IntensityAt(int x, int y) => _fire[x, y];

    public void Update()
    {
        var bottom = Height - 1;
        for (var x = 0; x < Width; x++)
        {
            var previous = _fire[x, bottom];
            var pulse = _random.Next(0, 100);
            var source = pulse switch
            {
                < 7 => 3,
                < 25 => 6,
                < 78 => 8,
                _ => 9
            };
            _fire[x, bottom] = (byte)Math.Clamp((previous + source * 2) / 3, 0, 9);
        }

        // Alulról felfelé terjedő, enyhén oldalra sodródó lángok. A két sorral
        // lejjebbi minta hosszabb, csúcsosabb nyelveket ad a prototípus egyenletes ködénél.
        for (var y = 0; y < bottom; y++)
        {
            var sourceY = y + 1;
            var deeperY = Math.Min(bottom, y + 2);
            for (var x = 0; x < Width; x++)
            {
                var drift = _random.Next(-1, 2);
                var center = Wrap(x + drift);
                var sum = _fire[center, sourceY] * 3 +
                          _fire[Wrap(center - 1), sourceY] +
                          _fire[Wrap(center + 1), sourceY] +
                          _fire[center, deeperY];
                var value = sum / 6 - _random.Next(0, y < Height / 2 ? 3 : 2);

                // Ritka, felfelé szakadó rubinparázs teszi kevésbé szabályossá a peremet.
                if (value < 3 && _random.Next(1000) < 7) value = _random.Next(3, 6);
                _fire[x, y] = (byte)Math.Clamp(value, 0, 9);
            }
        }
    }

    public void Render(int left, int top, int visibleWidth, int visibleHeight)
    {
        var renderWidth = Math.Clamp(visibleWidth, 0, Width);
        var renderHeight = Math.Clamp(visibleHeight, 0, Height);
        for (var y = 0; y < renderHeight; y++)
        {
            Console.SetCursorPosition(left, top + y);
            for (var x = 0; x < renderWidth; x++)
            {
                var intensity = _fire[x, y];
                Console.ForegroundColor = FireColor(intensity);
                Console.Write(FireChars[intensity]);
            }
        }
        Console.ResetColor();
    }

    internal static ConsoleColor FireColor(byte intensity) => intensity switch
    {
        0 => ConsoleColor.Black,
        1 => ConsoleColor.DarkGray,
        2 => ConsoleColor.DarkMagenta,
        3 or 4 => ConsoleColor.Magenta,
        5 => ConsoleColor.DarkRed,
        6 or 7 => ConsoleColor.Red,
        8 => ConsoleColor.DarkYellow,
        _ => ConsoleColor.White
    };

    private int Wrap(int x) => (x % Width + Width) % Width;
}
