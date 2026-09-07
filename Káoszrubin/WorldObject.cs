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
    public Position Position { get; private set; }
    internal event Action<WorldObject, Position, Position>? PositionChanged;
    public abstract Rune Symbol { get; }

    protected void SetPosition(Position position)
    {
        if (position == Position) return;
        var previous = Position;
        Position = position;
        PositionChanged?.Invoke(this, previous, position);
    }
}
