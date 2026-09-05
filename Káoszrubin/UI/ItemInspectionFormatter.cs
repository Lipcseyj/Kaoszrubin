using KaoszRubin.Application;
using KaoszRubin.Data;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Magic;

namespace KaoszRubin.UI;

public readonly record struct ItemInspection(string Text, ConsoleColor Color);
public readonly record struct ItemInspectionMobilityContext(SessionCharacterSnapshot Character,
    InventorySlotKind SourceKind, int SourceIndex);

/// <summary>A host és a vendég közös, katalógusalapú tárgyrészletezője.</summary>
public static class ItemInspectionFormatter
{
    public static ItemInspection Format(IItemDefinition item, GameDataCatalog gameData, int charges = 0,
        IReadOnlyDictionary<string, int>? weaponProficiencies = null,
        ItemInspectionMobilityContext? mobilityContext = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(gameData);
        var details = item switch
        {
            WeaponDefinition weapon =>
                $"Fegyver | típus: {(weapon.WeaponTypeId is { } typeId ? gameData.GetWeaponType(typeId).Name : "nincs")} | " +
                WeaponProficiencyText(weapon, weaponProficiencies) +
                WeaponMagicPowerText(weapon) +
                $"sebzéstípus: {weapon.DamageType.Name()} | célpontok: {weapon.MaximumTargets} | " +
                (weapon.CanAttackFromRear ? "hátsó sorból is használható | " : string.Empty) +
                $"sebzés: {weapon.Damage?.ToString() ?? "nincs"} | minimum Erő: {weapon.MinimumStrength} | " +
                $"súly: {weapon.Weight} | " +
                $"{(weapon.IsTwoHanded ? "kétkezes" : "egykezes")} | " +
                (weapon.IsTwoHanded
                    ? "⚒️ páncéltörő: az ellenfél páncéljának 50%-át figyelmen kívül hagyja | "
                    : string.Empty) + $"kasztok: {AllowedClassNames(weapon.AllowedClassIds, gameData)}",
            ArmorDefinition armor =>
                $"Páncél | típusvédelem: {armor.Resistances ?? new DamageResistance()} | védelem: {armor.Defense?.ToString() ?? "nincs"} | súly: {armor.Weight} | " +
                $"kasztok: {AllowedClassNames(armor.AllowedClassIds, gameData)}",
            MagicItemDefinition magic =>
                $"Varázstárgy | típus: {MagicItemKindName(magic.Kind)} | súly: {magic.Weight} | " +
                $"hatás: {MagicItemEffectName(magic.Effect)} {magic.EffectValue}" +
                (magic.SpellId is null ? string.Empty : $" | varázslat: {gameData.GetSpell(magic.SpellId).Name}") +
                (magic.MaximumCharges > 0 ? $" | töltet: {charges}/{magic.MaximumCharges}" : string.Empty) +
                $" | kasztok: {AllowedClassNames(magic.AllowedClassIds, gameData)}",
            MiscItemDefinition misc when SpellcastingRules.IsSpellcastingFocus(misc) =>
                $"Karakterhez kötött varázsfókusz | súly: {misc.Weight} | nem mozgatható, nem dobható el és nem kereskedhető",
            MiscItemDefinition misc when misc.Id == MiscItemIds.HerbalTea =>
                $"Használati tárgy | súly: {misc.Weight} | hatás: víz {misc.EffectValue}, HP 5–15",
            MiscItemDefinition misc when misc.Id is MiscItemIds.Mead or MiscItemIds.SpicedWine =>
                $"Használati tárgy | súly: {misc.Weight} | hatás: víz {misc.EffectValue}, +2 kezdeményezés és +1 találat 10 akcióig",
            MiscItemDefinition misc when misc.Effect != ConsumableEffect.None =>
                $"Használati tárgy | súly: {misc.Weight} | hatás: {ConsumableEffectName(misc.Effect)} {misc.EffectValue}",
            MiscItemDefinition misc => $"Általános tárgy | súly: {misc.Weight}",
            _ => "Általános tárgy"
        };
        var description = string.IsNullOrWhiteSpace(item.Description) ? "Nincs jellemzés." : item.Description;
        var mobility = mobilityContext is { } context
            ? MobilityPreview(item, gameData, context)
            : string.Empty;
        return new ItemInspection($"{item.Name} [{item.Id}] — {details}. Ritkaság: {RarityName(item.Rarity)}; " +
            $"mágikus erő: {item.MagicPower}; alapár: {item.BasePrice} arany. Jellemzés: {description}" + mobility,
            RarityColor(item.Rarity));
    }

    private static string MobilityPreview(IItemDefinition item, GameDataCatalog gameData,
        ItemInspectionMobilityContext context)
    {
        if (item is not (WeaponDefinition or ArmorDefinition) ||
            context.Character.CharacterSheet is not { } sheet || context.Character.Inventory is not { } inventory)
            return string.Empty;

        var current = Profile(context.Character, sheet.EquippedWeight);
        if (context.SourceKind == InventorySlotKind.Weapon && context.SourceIndex < 2 && item is WeaponDefinition equippedWeapon)
            return PreviewText("Levétellel", current,
                Profile(context.Character, sheet.EquippedWeight - equippedWeapon.Weight));
        if (context.SourceKind == InventorySlotKind.Armor && item is ArmorDefinition equippedArmor)
            return PreviewText("Levétellel", current,
                Profile(context.Character, sheet.EquippedWeight - equippedArmor.Weight));
        if (context.SourceKind != InventorySlotKind.Backpack) return string.Empty;

        if (item is ArmorDefinition armor)
        {
            if (!armor.CanBeEquippedBy(context.Character.CharacterClassId)) return string.Empty;
            var worn = SlotDefinition(inventory, InventorySlotKind.Armor, 0, gameData) as ArmorDefinition;
            return PreviewText("Felszerelve", current,
                Profile(context.Character, sheet.EquippedWeight - (worn?.Weight ?? 0) + armor.Weight));
        }

        var weapon = (WeaponDefinition)item;
        if (!weapon.CanBeEquippedBy(context.Character.CharacterClassId, sheet.Abilities.Strength)) return string.Empty;
        var first = SlotDefinition(inventory, InventorySlotKind.Weapon, 0, gameData) as WeaponDefinition;
        var second = SlotDefinition(inventory, InventorySlotKind.Weapon, 1, gameData) as WeaponDefinition;
        var previews = new List<(string Label, CharacterMobilityProfile Profile)>();
        if (!weapon.IsTwoHanded || second is null)
            previews.Add(("1. helyre", Profile(context.Character,
                sheet.EquippedWeight - (first?.Weight ?? 0) + weapon.Weight)));
        if (!weapon.IsTwoHanded && first?.IsTwoHanded != true)
            previews.Add(("2. helyre", Profile(context.Character,
                sheet.EquippedWeight - (second?.Weight ?? 0) + weapon.Weight)));
        if (previews.Count == 0) return string.Empty;
        if (previews.Count == 1 || previews.Select(preview => preview.Profile).Distinct().Count() == 1)
            return PreviewText("Felszerelve", current, previews[0].Profile);
        return string.Concat(previews.Select(preview => PreviewText($"Felszerelve a {preview.Label}", current,
            preview.Profile)));
    }

    private static CharacterMobilityProfile Profile(SessionCharacterSnapshot character, double weight)
    {
        var sheet = character.CharacterSheet!;
        return CharacterMobilityRules.EvaluateEquipment(sheet.Abilities, character.CharacterClassId,
            weight, sheet.HasArmorMaster, Math.Max(sheet.CarriedWeight, sheet.EquippedWeight));
    }

    private static IItemDefinition? SlotDefinition(CharacterInventorySnapshot inventory, InventorySlotKind kind,
        int index, GameDataCatalog gameData)
    {
        var item = inventory.Slots.FirstOrDefault(slot => slot.Kind == kind && slot.Index == index)?.Item;
        return item?.Category switch
        {
            ItemCategory.Weapon => gameData.GetWeapon(item.DefinitionId),
            ItemCategory.Armor => gameData.GetArmor(item.DefinitionId),
            _ => null
        };
    }

    private static string PreviewText(string label, CharacterMobilityProfile before,
        CharacterMobilityProfile after) =>
        $" {label}: ⚔ ⚖ {before.EquippedWeight} → {after.EquippedWeight}; " +
        $"{EncumbranceName(before.Encumbrance)} → {EncumbranceName(after.Encumbrance)}; " +
        $"👣 {before.CombatMovementAllowance} → {after.CombatMovementAllowance}; " +
        $"⚡ {before.InitiativeBase} → {after.InitiativeBase}.";

    private static string EncumbranceName(EncumbranceLevel level) => level switch
    {
        EncumbranceLevel.Medium => "Közepes",
        EncumbranceLevel.Heavy => "Nehéz",
        _ => "Könnyű"
    };

    private static string WeaponProficiencyText(WeaponDefinition weapon,
        IReadOnlyDictionary<string, int>? proficiencies)
    {
        var family = WeaponFamilies.Find(WeaponFamilies.ForWeapon(weapon) ?? string.Empty);
        if (family is null) return "család: nincs | ";
        var rank = proficiencies is not null && proficiencies.TryGetValue(family.Id, out var value)
            ? (WeaponProficiencyRank?)Math.Clamp(value, 1, 2)
            : null;
        var rankText = rank switch
        {
            WeaponProficiencyRank.Master => "Mester",
            WeaponProficiencyRank.Trained => "Jártas",
            _ => "járatlan"
        };
        var active = rank switch
        {
            WeaponProficiencyRank.Master => family.MasterDescription,
            WeaponProficiencyRank.Trained => family.TrainedDescription,
            _ => "Nincs aktív jártassági bónusz."
        };
        var next = rank is null ? $" Következő fok: {family.TrainedDescription}"
            : rank == WeaponProficiencyRank.Trained ? $" Következő fok: {family.MasterDescription}" : string.Empty;
        return $"család: {family.Icon} {family.Name} | jártasság: {rankText} — {active}{next} | ";
    }

    private static string WeaponMagicPowerText(WeaponDefinition weapon)
    {
        if (weapon.MagicPower <= 0) return string.Empty;
        var criticalText = weapon.MagicPower switch
        {
            2 => "+5% kritikus esély (természetes 19–20)",
            >= 3 => "+10% kritikus esély (természetes 18–20)",
            _ => string.Empty
        };
        return $"mágikus módosítók: +{weapon.MagicPower} minimum és maximum sebzés, +{weapon.MagicPower} találat" +
               (criticalText.Length == 0 ? string.Empty : $", {criticalText}") + " | ";
    }

    private static string AllowedClassNames(IReadOnlySet<string> ids, GameDataCatalog gameData) => string.Join(", ",
        gameData.CharacterClasses.Where(characterClass => ids.Contains(characterClass.Id))
            .Select(characterClass => characterClass.Name));

    private static string RarityName(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Magic => "Varázs", ItemRarity.Legendary => "Legendás", _ => "Sima"
    };

    private static ConsoleColor RarityColor(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Magic => ConsoleColor.Cyan, ItemRarity.Legendary => ConsoleColor.Yellow, _ => ConsoleColor.Gray
    };

    private static string ConsumableEffectName(ConsumableEffect effect) => effect switch
    {
        ConsumableEffect.Food => "élelem", ConsumableEffect.Water => "víz", ConsumableEffect.Heal => "HP",
        ConsumableEffect.RestoreMana => "manna", ConsumableEffect.CurePoison => "mérgezés gyógyítása",
        ConsumableEffect.CureDisease => "betegség gyógyítása", ConsumableEffect.StopBleeding => "vérzés elállítása",
        _ => "nincs"
    };

    private static string MagicItemKindName(MagicItemKind kind) => kind switch
    {
        MagicItemKind.Amulet => "amulett", MagicItemKind.Wand => "varázspálca",
        MagicItemKind.Scroll => "varázstekercs", _ => "varázsgyűrű"
    };

    private static string MagicItemEffectName(MagicItemEffect effect) => effect switch
    {
        MagicItemEffect.Initiative => "kezdeményezés", MagicItemEffect.Hit => "találati próba",
        MagicItemEffect.Damage => "sebzés", MagicItemEffect.Defense => "védelem",
        MagicItemEffect.BattleHeal => "csata eleji HP", MagicItemEffect.BattleMana => "csata eleji manna",
        MagicItemEffect.Strength => "Erő", MagicItemEffect.Dexterity => "Ügyesség",
        MagicItemEffect.Health => "Egészség", MagicItemEffect.Intelligence => "Intelligencia",
        _ => "varázslattároló"
    };
}
