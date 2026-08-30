namespace KaoszRubin.Domain.Inventory;

/// <summary>CSV-ből betöltött szabály normál felszerelések mágikus változatainak előállításához.</summary>
public sealed record ItemUpgradeDefinition(string Id, string NameSuffix, int CombatBonus,
    double PriceMultiplier, int MagicPower);
