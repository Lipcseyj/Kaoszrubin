namespace KaoszRubin;

/// <summary>Egy ajtóval kapcsolódó téglalap alakú szoba belső területe.</summary>
public sealed record Room(Position TopLeft, int Width, int Height)
{
    public bool Contains(Position position) =>
        position.X >= TopLeft.X && position.X < TopLeft.X + Width &&
        position.Y >= TopLeft.Y && position.Y < TopLeft.Y + Height;

    public IEnumerable<Position> InteriorPositions()
    {
        for (var y = TopLeft.Y; y < TopLeft.Y + Height; y++)
        for (var x = TopLeft.X; x < TopLeft.X + Width; x++)
            yield return new Position(x, y);
    }
}
