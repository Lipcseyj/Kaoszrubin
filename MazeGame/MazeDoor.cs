using System.Text;

namespace MazeGame;

public enum DoorState { Locked, Open, Closed, Smashed }

/// <summary>Állapottal rendelkező ajtó; a bezúzott állapot végleges.</summary>
public sealed class MazeDoor(Position position, DoorState state)
{
    public Position Position { get; } = position;
    public DoorState State { get; private set; } = state;
    public bool IsWalkable => State is DoorState.Open or DoorState.Smashed;
    public bool BlocksSight => State is DoorState.Locked or DoorState.Closed;
    public Rune Symbol => State switch
    {
        DoorState.Locked => new Rune('╫'),
        DoorState.Open => new Rune('╱'),
        DoorState.Closed => new Rune('╬'),
        DoorState.Smashed => new Rune('▒'),
        _ => new Rune('?')
    };

    public bool TrySetState(DoorState state)
    {
        if (State == DoorState.Smashed && state != DoorState.Smashed) return false;
        State = state;
        return true;
    }
}
