namespace KaoszRubin.Data;

/// <summary>
/// Egy alapfegyver vagy alappáncél karaktergeneráláskor használható szinttartománya.
/// </summary>
public sealed record CharacterGenerationEquipmentRule(string ItemId, int MinimumLevel, int MaximumLevel)
{
    public bool Includes(int level) => level >= MinimumLevel && level <= MaximumLevel;

    /// <summary>
    /// A tartomány elején 1, a végén 100 súlyt ad, így a magasabb szintű tárgyak
    /// fokozatosan válnak gyakoribbá, nem egyszerre jelennek meg.
    /// </summary>
    public int SelectionWeight(int level)
    {
        if (!Includes(level)) return 0;
        if (MinimumLevel == MaximumLevel) return 100;
        return 1 + (int)Math.Round(99d * (level - MinimumLevel) / (MaximumLevel - MinimumLevel));
    }
}

/// <summary>Egy +1/+2/+3 tárgybővítés karaktergenerálási szinttartománya.</summary>
public sealed record CharacterGenerationUpgradeRule(string UpgradeId, int MinimumLevel, int MaximumLevel)
{
    public bool Includes(int level) => level >= MinimumLevel && level <= MaximumLevel;

    /// <summary>A mágikus változat esélye a tartomány eleji 10%-ról a végén 90%-ra nő.</summary>
    public int ChancePercent(int level)
    {
        if (!Includes(level)) return 0;
        if (MinimumLevel == MaximumLevel) return 90;
        return 10 + (int)Math.Round(80d * (level - MinimumLevel) / (MaximumLevel - MinimumLevel));
    }
}
