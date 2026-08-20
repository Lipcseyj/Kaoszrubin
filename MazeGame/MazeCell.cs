namespace MazeGame;

public enum CellType { Wall, Floor }

public sealed class MazeCell
{
    public CellType Type { get; private set; } = CellType.Wall;
    public bool IsWalkable => Type == CellType.Floor;
    public void Carve() => Type = CellType.Floor;
}
