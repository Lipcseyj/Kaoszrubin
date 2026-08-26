namespace MazeGame;

/// <summary>Egy pályán létező objektum stabil futásidejű azonosítója, a snapshot-delták célzásához.</summary>
public readonly record struct WorldEntityId(Guid Value)
{
    public static WorldEntityId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString("N");
}
