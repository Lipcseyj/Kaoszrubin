namespace MazeGame;

/// <summary>A pálya adatait kezeli, a megjelenítéstől és irányítástól függetlenül.</summary>
public sealed class Maze
{
    private readonly MazeCell[,] _cells;
    public int Width { get; }
    public int Height { get; }
    public Position Entrance { get; }
    public Position Exit { get; }

    public Maze(int width, int height)
    {
        if (width < 5 || height < 5 || width % 2 == 0 || height % 2 == 0)
            throw new ArgumentException("A labirintus méretei legalább 5-ösek és páratlanok legyenek.");

        Width = width;
        Height = height;
        _cells = new MazeCell[width, height];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++) _cells[x, y] = new MazeCell();
        Entrance = new Position(1, 1);
        Exit = new Position(width - 2, height - 2);
    }

    public bool IsInside(Position position) => position.X >= 0 && position.X < Width && position.Y >= 0 && position.Y < Height;
    public bool IsWalkable(Position position) => IsInside(position) && _cells[position.X, position.Y].IsWalkable;
    public void Carve(Position position)
    {
        if (!IsInside(position)) throw new ArgumentOutOfRangeException(nameof(position));
        _cells[position.X, position.Y].Carve();
    }
}
