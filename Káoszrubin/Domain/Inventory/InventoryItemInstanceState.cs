using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Magic;
using KaoszRubin.Domain.Characters;

namespace KaoszRubin.Domain.Inventory;

/// <summary>Egy konkrét inventorytárgy definíciótól független, menthető állapota.</summary>
public readonly record struct InventoryItemInstanceState(Guid InstanceId, bool IsIdentified,
    string? CurseId = null, ItemCurseEffect CurseEffect = ItemCurseEffect.None, int CurseValue = 0,
    int CurseStrength = 0, bool IsCurseActivated = false, CharacterId? BoundCharacterId = null,
    bool IsPurified = false)
{
    public static InventoryItemInstanceState Create(bool identified = true) => new(Guid.NewGuid(), identified);

    public bool HasCurse => !IsPurified && !string.IsNullOrWhiteSpace(CurseId) &&
                            CurseEffect != ItemCurseEffect.None && CurseValue > 0;
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

    /// <summary>
    /// A frissen talált tárgy felismerésének esélye. A próba egyszer, a zsákmány létrejöttekor történik;
    /// így a tárgy átadogatásával vagy újrafelvételével nem lehet korlátlanul újradobni.
    /// </summary>
    public static int MageIdentificationChance(LiveCharacter mage, IItemDefinition item) =>
        mage.CharacterClass.Id == CharacterClassIds.Mágus
            ? Math.Clamp(25 + mage.EffectiveAbilities.Intelligence * 5 - item.MagicPower * 10, 5, 95)
            : 0;

    public static MageIdentificationResult AttemptByBestMage(IItemDefinition item,
        InventoryItemInstanceState state, IEnumerable<LiveCharacter> partyMembers, Random random)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (state.IsIdentified || !RequiresIdentification(item))
            return new MageIdentificationResult(state, null, 0, 0, false, state.IsIdentified);

        var mage = partyMembers
            .Where(character => character.IsAlive && character.CharacterClass.Id == CharacterClassIds.Mágus)
            .OrderByDescending(character => character.EffectiveAbilities.Intelligence)
            .ThenByDescending(character => character.Level)
            .FirstOrDefault();
        if (mage is null) return new MageIdentificationResult(state, null, 0, 0, false, false);

        var chance = MageIdentificationChance(mage, item);
        var roll = random.Next(1, 101);
        var success = roll <= chance;
        return new MageIdentificationResult(success ? state with { IsIdentified = true } : state,
            mage, chance, roll, true, success);
    }

    public static int CurseRemovalPrice(IItemDefinition item, InventoryItemInstanceState state) =>
        Math.Max(1, 50 + (int)Math.Ceiling(item.BasePrice * 0.12) + item.MagicPower * 25 +
                    Math.Max(1, state.CurseStrength) * 100);

    public static InventoryItemInstanceState CreateLootState(IItemDefinition item,
        IReadOnlyList<ItemCurseDefinition> curses, Random random, int curseChancePercent)
    {
        var identified = !RequiresIdentification(item);
        if (!RequiresIdentification(item) || random.Next(100) >= Math.Clamp(curseChancePercent, 0, 100))
            return InventoryItemInstanceState.Create(identified);
        var candidates = curses.Where(curse => curse.CanAffect(item)).ToArray();
        if (candidates.Length == 0) return InventoryItemInstanceState.Create(identified);
        var selected = candidates[random.Next(candidates.Length)];
        return new InventoryItemInstanceState(Guid.NewGuid(), identified, selected.Id, selected.Effect,
            selected.Value, selected.Strength);
    }
}

public readonly record struct MageIdentificationResult(InventoryItemInstanceState State, LiveCharacter? Mage,
    int ChancePercent, int Roll, bool Attempted, bool Succeeded);
