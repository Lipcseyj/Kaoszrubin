using System.Text;

namespace MazeGame;

/// <summary>A térképen megjelenő, a pályaborítástól független objektum alaposztálya.</summary>
public abstract class WorldObject
{
    protected WorldObject(Position position) => Position = position;

    public Position Position { get; protected set; }
    public abstract Rune Symbol { get; }
}
