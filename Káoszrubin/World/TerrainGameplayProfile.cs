namespace KaoszRubin.World;

[Flags]
public enum TerrainTag
{
    None = 0,
    Bush = 1,
    Undergrowth = 2,
    DenseUndergrowth = 4,
    Marsh = 8,
    ThicketEdge = 16
}

/// <summary>A terep megjelenésétől független, felfedezés közbeni játékmeneti hatások.</summary>
public sealed record TerrainGameplayProfile(
    TerrainTag Tags = TerrainTag.None,
    int MovementDelayPercent = 0,
    int ExertionCost = 0,
    int ConcealmentBonus = 0,
    int NoiseModifier = 0,
    bool SupportsAmbushPlacement = false)
{
    public static readonly TerrainGameplayProfile None = new();
}
