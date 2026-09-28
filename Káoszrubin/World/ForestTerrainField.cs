namespace KaoszRubin.World;

/// <summary>Folytonos, több léptékű mező a cellánkénti sorsolás helyett. Csak a kapott RNG-t használja.</summary>
internal sealed class ForestTerrainField
{
    private readonly double[,] _values;
    private readonly double[] _ordered;

    public ForestTerrainField(int width, int height, IntRange scale, Random random)
    {
        _values = new double[width, height];
        _ordered = new double[(width - 2) * (height - 2)];
        var coarseSeed = random.Next();
        var detailSeed = random.Next();
        var warpSeed = random.Next();
        var size = scale.Minimum + random.NextDouble() * (scale.Maximum - scale.Minimum);
        var index = 0;
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            // A konzolcellák magasabbak, mint szélesek; a táj ne csak függőleges csíkokból álljon.
            var px = x * 0.65;
            var py = (double)y;
            var warpX = (Noise(px / size, py / size, warpSeed) - 0.5) * size;
            var warpY = (Noise(px / size, py / size, warpSeed + 1) - 0.5) * size;
            var value = 0.72 * Noise((px + warpX) / size, (py + warpY) / size, coarseSeed) +
                        0.23 * Noise(px / scale.Minimum, py / scale.Minimum, detailSeed) +
                        0.05 * Noise(px / 2.5, py / 2.5, detailSeed + 1);
            _values[x, y] = value;
            if (x > 0 && x < width - 1 && y > 0 && y < height - 1) _ordered[index++] = value;
        }
        Array.Sort(_ordered);
    }

    public double this[Position position] => _values[position.X, position.Y];

    // A kvantilis a tényleges térképből jön: ugyanaz a sűrűség kis/nagy pályán is azonos borítást jelent.
    public double Threshold(double coverage) => coverage <= 0 ? double.PositiveInfinity :
        coverage >= 1 ? double.NegativeInfinity :
        _ordered[Math.Clamp((int)Math.Round((1 - coverage) * _ordered.Length), 0, _ordered.Length - 1)];

    private static double Noise(double x, double y, int seed)
    {
        var ix = (int)Math.Floor(x);
        var iy = (int)Math.Floor(y);
        var tx = x - ix;
        var ty = y - iy;
        tx = tx * tx * (3 - 2 * tx);
        ty = ty * ty * (3 - 2 * ty);
        var top = Lerp(Hash(ix, iy, seed), Hash(ix + 1, iy, seed), tx);
        var bottom = Lerp(Hash(ix, iy + 1, seed), Hash(ix + 1, iy + 1, seed), tx);
        return Lerp(top, bottom, ty);
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private static double Hash(int x, int y, int seed)
    {
        unchecked
        {
            var value = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
            value = (value ^ (value >> 13)) * 1274126177;
            return (value ^ (value >> 16)) / (double)uint.MaxValue;
        }
    }
}
