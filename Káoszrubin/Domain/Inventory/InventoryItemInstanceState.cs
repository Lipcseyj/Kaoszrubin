using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Magic;
using KaoszRubin.Domain.Characters;

namespace KaoszRubin.Domain.Inventory;

/// <summary>Egy konkrét inventorytárgy definíciótól független, menthető állapota.</summary>
public readonly record struct InventoryItemInstanceState(Guid InstanceId, bool IsIdentified,
    string? CurseId = null, ItemCurseEffect CurseEffect = ItemCurseEffect.None, int CurseValue = 0,
    int CurseStrength = 0, bool IsCurseActivated = false, CharacterId? BoundCharacterId = null,
    bool IsPurified = false, int DurabilityDamage = 0)
{
    public static InventoryItemInstanceState Create(bool identified = true) => new(Guid.NewGuid(), identified);

    public bool HasCurse => !IsPurified && !string.IsNullOrWhiteSpace(CurseId) &&
                            CurseEffect != ItemCurseEffect.None && CurseValue > 0;
}

public enum EquipmentCondition
{
    NotApplicable,
    Intact,
    Worn,
    Damaged,
    Broken
}

public static class EquipmentDurabilityRules
{
    public static int MaximumDurability(IItemDefinition item) =>
        item is IDurableItemDefinition durable ? Math.Max(0, durable.MaximumDurability) : 0;

    public static int CurrentDurability(IItemDefinition item, InventoryItemInstanceState state) =>
        Math.Max(0, MaximumDurability(item) - Math.Max(0, state.DurabilityDamage));

    public static int CurrentDurability(int maximumDurability, int durabilityDamage) =>
        Math.Max(0, Math.Max(0, maximumDurability) - Math.Max(0, durabilityDamage));

    public static int DurabilityPercent(int maximumDurability, int durabilityDamage) =>
        maximumDurability <= 0
            ? 0
            : (int)Math.Floor(CurrentDurability(maximumDurability, durabilityDamage) * 100d /
                              maximumDurability);

    public static EquipmentCondition Condition(int maximumDurability, int durabilityDamage)
    {
        if (maximumDurability <= 0) return EquipmentCondition.NotApplicable;
        var percent = DurabilityPercent(maximumDurability, durabilityDamage);
        return percent switch
        {
            0 => EquipmentCondition.Broken,
            <= 25 => EquipmentCondition.Damaged,
            <= 50 => EquipmentCondition.Worn,
            _ => EquipmentCondition.Intact
        };
    }

    public static InventoryItemInstanceState Normalize(IItemDefinition item, InventoryItemInstanceState state)
    {
        var maximum = MaximumDurability(item);
        return state with { DurabilityDamage = Math.Clamp(state.DurabilityDamage, 0, maximum) };
    }

    public static InventoryItemInstanceState ApplyWear(IItemDefinition item, InventoryItemInstanceState state,
        int amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        var maximum = MaximumDurability(item);
        return maximum == 0 ? state : state with
        {
            DurabilityDamage = Math.Clamp(state.DurabilityDamage + amount, 0, maximum)
        };
    }

    public static InventoryItemInstanceState Repair(IItemDefinition item, InventoryItemInstanceState state,
        int amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        var maximum = MaximumDurability(item);
        return maximum == 0 ? state : state with
        {
            DurabilityDamage = Math.Clamp(state.DurabilityDamage - amount, 0, maximum)
        };
    }
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
