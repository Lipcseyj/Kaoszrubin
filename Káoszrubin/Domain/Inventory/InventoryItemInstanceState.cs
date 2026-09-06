using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Magic;

namespace KaoszRubin.Domain.Inventory;

/// <summary>Egy konkrét inventorytárgy definíciótól független, menthető állapota.</summary>
public readonly record struct InventoryItemInstanceState(Guid InstanceId, bool IsIdentified)
{
    public static InventoryItemInstanceState Create(bool identified = true) => new(Guid.NewGuid(), identified);
}

public static class ItemIdentificationRules
{
    public static bool RequiresIdentification(IItemDefinition item) =>
        item.Rarity == ItemRarity.Magic && !CharacterBoundItemRules.IsBound(item);

    public static string DisplayName(IItemDefinition item, bool identified) => identified
        ? item.Name
        : item switch
        {
            WeaponDefinition => "❓ Azonosítatlan mágikus fegyver",
            ArmorDefinition => "❓ Azonosítatlan mágikus páncél",
            MagicItemDefinition { Kind: MagicItemKind.Ring } => "❓ Ismeretlen rúnás gyűrű",
            MagicItemDefinition { Kind: MagicItemKind.Amulet } => "❓ Ismeretlen mágikus amulett",
            MagicItemDefinition { Kind: MagicItemKind.Wand } => "❓ Ismeretlen varázspálca",
            MagicItemDefinition { Kind: MagicItemKind.Scroll } => "❓ Lepecsételt varázstekercs",
            _ => "❓ Azonosítatlan mágikus tárgy"
        };

    public static string AuraStrength(IItemDefinition item) => item.MagicPower switch
    {
        <= 1 => "gyenge",
        <= 3 => "közepes",
        <= 6 => "erős",
        _ => "rendkívüli"
    };

    public static int IdentificationPrice(IItemDefinition item) =>
        Math.Max(1, 20 + (int)Math.Ceiling(item.BasePrice * 0.08) + item.MagicPower * 15);
}
