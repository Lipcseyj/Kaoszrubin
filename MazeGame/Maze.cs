using System.Text;

namespace MazeGame;

/// <summary>A pálya látható és bejárható rácsa.</summary>
public sealed class Maze
{
    public static readonly Rune Wall = new('█');
    public static readonly Rune Floor = new(' ');
    public static readonly Rune ExitMarker = new('⌂');
    public static readonly Rune Door = new('╬');

    /// <summary>A pálya cellái. Minden elem egyetlen, keskeny konzolcellára rajzolható rúna.</summary>
    public Rune[,] Tiles { get; }
    public int Width { get; }
    public int Height { get; }
    public Position Entrance { get; }
    public Position Exit { get; private set; }

    public Maze(int width, int height)
    {
        if (width < 5 || height < 5)
            throw new ArgumentException("A labirintus méretei legalább 5-ösek legyenek.");

        Width = width;
        Height = height;
        Tiles = new Rune[width, height];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            Tiles[x, y] = Wall;

        Entrance = new Position(1, 1);
        Exit = new Position(LastInnerOddCoordinate(width), LastInnerOddCoordinate(height));
    }

    public bool IsInside(Position position) => position.X >= 0 && position.X < Width && position.Y >= 0 && position.Y < Height;
    public bool IsWalkable(Position position) => IsInside(position) &&
        (Tiles[position.X, position.Y] == Floor || Tiles[position.X, position.Y] == ExitMarker || Tiles[position.X, position.Y] == Door);

    public void Carve(Position position)
    {
        if (!IsInside(position)) throw new ArgumentOutOfRangeException(nameof(position));
        Tiles[position.X, position.Y] = Floor;
    }

    public void PlaceExit(Position position)
    {
        if (!IsWalkable(position)) throw new ArgumentException("A kijáratnak járható cellán kell lennie.", nameof(position));
        Exit = position;
        Tiles[Exit.X, Exit.Y] = ExitMarker;
    }

    public void SetTile(Position position, Rune tile)
    {
        if (!IsInside(position)) throw new ArgumentOutOfRangeException(nameof(position));
        Tiles[position.X, position.Y] = tile;
    }

    private static int LastInnerOddCoordinate(int length)
    {
        var coordinate = length - 2;
        return coordinate % 2 == 0 ? coordinate - 1 : coordinate;
    }
}
