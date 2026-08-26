namespace MazeGame.Domain.Characters;

/// <summary>Mentésen és hálózati kapcsolaton át is stabil karakterazonosító.</summary>
public readonly record struct CharacterId(Guid Value)
{
    public static CharacterId New() => new(Guid.NewGuid());
    public static CharacterId From(Guid value) => value == Guid.Empty ? New() : new CharacterId(value);
    public override string ToString() => Value.ToString("N");
}
