using MazeGame.Data;
using MazeGame.Domain.Characters;
using MazeGame.Domain.Combat;
using MazeGame.Domain.Inventory;
using MazeGame.Domain.Magic;

namespace MazeGame.UI;

public readonly record struct ItemInspection(string Text, ConsoleColor Color);

/// <summary>A host és a vendég közös, katalógusalapú tárgyrészletezője.</summary>
public static class ItemInspectionFormatter
{
    public static ItemInspection Format(IItemDefinition item, GameDataCatalog gameData, int charges = 0,
        IReadOnlyDictionary<string, int>? weaponProficiencies = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(gameData);
        var details = item switch
        {
            WeaponDefinition weapon =>
                $"Fegyver | típus: {(weapon.WeaponTypeId is { } typeId ? gameData.GetWeaponType(typeId).Name : "nincs")} | " +
                WeaponProficiencyText(weapon, weaponProficiencies) +
                $"sebzés: {weapon.Damage?.ToString() ?? "nincs"} | minimum Erő: {weapon.MinimumStrength} | " +
                $"{(weapon.IsTwoHanded ? "kétkezes" : "egykezes")} | " +
                (weapon.IsTwoHanded
                    ? "⚒️ páncéltörő: az ellenfél páncéljának 50%-át figyelmen kívül hagyja | "
                    : string.Empty) + $"kasztok: {AllowedClassNames(weapon.AllowedClassIds, gameData)}",
            ArmorDefinition armor =>
                $"Páncél | védelem: {armor.Defense?.ToString() ?? "nincs"} | " +
                $"kasztok: {AllowedClassNames(armor.AllowedClassIds, gameData)}",
            MagicItemDefinition magic =>
                $"Varázstárgy | típus: {MagicItemKindName(magic.Kind)} | " +
                $"hatás: {MagicItemEffectName(magic.Effect)} {magic.EffectValue}" +
                (magic.SpellId is null ? string.Empty : $" | varázslat: {gameData.GetSpell(magic.SpellId).Name}") +
                (magic.MaximumCharges > 0 ? $" | töltet: {charges}/{magic.MaximumCharges}" : string.Empty) +
                $" | kasztok: {AllowedClassNames(magic.AllowedClassIds, gameData)}",
            MiscItemDefinition misc when SpellcastingRules.IsSpellcastingFocus(misc) =>
                "Karakterhez kötött varázsfókusz | nem mozgatható, nem dobható el és nem kereskedhető",
            MiscItemDefinition misc when misc.Id == MiscItemIds.HerbalTea =>
                $"Használati tárgy | hatás: víz {misc.EffectValue}, HP 5–15",
            MiscItemDefinition misc when misc.Id is MiscItemIds.Mead or MiscItemIds.SpicedWine =>
                $"Használati tárgy | hatás: víz {misc.EffectValue}, +2 kezdeményezés és +1 találat 10 akcióig",
            MiscItemDefinition misc when misc.Effect != ConsumableEffect.None =>
                $"Használati tárgy | hatás: {ConsumableEffectName(misc.Effect)} {misc.EffectValue}",
            _ => "Általános tárgy"
        };
        var description = string.IsNullOrWhiteSpace(item.Description) ? "Nincs jellemzés." : item.Description;
        return new ItemInspection($"{item.Name} [{item.Id}] — {details}. Ritkaság: {RarityName(item.Rarity)}; " +
            $"mágikus erő: {item.MagicPower}; alapár: {item.BasePrice} arany. Jellemzés: {description}",
            RarityColor(item.Rarity));
    }

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
