using System.Text;

namespace KaoszRubin;

/// <summary>A térképen megjelenő, a pályaborítástól független objektum alaposztálya.</summary>
public abstract class WorldObject
{
    protected WorldObject(Position position)
    {
        Id = WorldEntityId.New();
        Position = position;
    }

    public WorldEntityId Id { get; }
    public Position Position { get; protected set; }
    public abstract Rune Symbol { get; }
}
