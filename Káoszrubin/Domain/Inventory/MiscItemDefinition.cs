namespace KaoszRubin.Domain.Inventory;

/// <summary>Általános, még részletes statisztika nélküli tárgy (például élelem vagy kulacs).</summary>
public sealed record MiscItemDefinition(string Id, string Name, string Description, int BasePrice,
    ConsumableEffect Effect = ConsumableEffect.None, int EffectValue = 0) : IItemDefinition
{
    public ItemCategory Category => ItemCategory.Miscellaneous;
    public ItemRarity Rarity => ItemRarity.Normal;
    public int MagicPower => 0;
}

public enum ConsumableEffect
{
    None,
    Food,
    Water,
    Heal,
    RestoreMana,
    CurePoison,
    CureDisease,
    StopBleeding,
    Vision
}

public static class MiscItemIds
{
    public const string Key = "T003";
    public const string HerbalTea = "T020";
    public const string Mead = "T023";
    public const string SpicedWine = "T024";
    public const string Torch = "T025";
    public const string FallenKnightInsignia = "T026";
}

public static class QuestItemIds
{
    public static bool Contains(string itemId) =>
        string.Equals(itemId, MiscItemIds.FallenKnightInsignia, StringComparison.OrdinalIgnoreCase);
}
