using KaoszRubin.Application;
using KaoszRubin.Data;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Magic;

namespace KaoszRubin.UI;

/// <summary>Közös host–vendég tárgyvizsgálati oldal teljes jobb paneles megjelenítéshez.</summary>
public static class ItemInspectionPanel
{
    private const int ContentStartRow = 2;
    private const int ContentLastRow = 44;
    private const int ControlsRow = 45;
    private const int CloseRow = 46;

    public static IReadOnlyList<CharacterSheetPanelLine> BuildKnown(IItemDefinition item, GameDataCatalog gameData,
        int quantity = 1, int charges = 0, InventoryItemInstanceState? state = null, bool focused = false,
        int width = CharacterSheetPanel.Width)
    {
        var effectiveWidth = Math.Max(CharacterSheetPanel.Width, width);
        var rows = new List<CharacterSheetPanelLine>
        {
            new(0, "🔍 TÁRGYVIZSGÁLAT", ConsoleColor.Yellow,
                Background: focused ? ConsoleColor.DarkGreen : ConsoleColor.Black),
            new(1, $"{CategoryIcon(item.Category)} {item.Name}", RarityColor(item.Rarity))
        };

        var content = BuildCommonContent(item, gameData, quantity, charges, state)
            .Concat(BuildCategorySpecificContent(item, gameData));

        var row = ContentStartRow;
        foreach (var (text, color) in content)
        {
            if (row > ContentLastRow) break;
            foreach (var part in MessageTextLayout.Wrap(text, effectiveWidth))
            {
                if (row > ContentLastRow) break;
                rows.Add(new CharacterSheetPanelLine(row++, part, color));
            }
        }

        rows.Add(new CharacterSheetPanelLine(ControlsRow, "Esc / I / Enter: vissza", ConsoleColor.Green));
        rows.Add(new CharacterSheetPanelLine(CloseRow, "Nézet: teljes tárgyadatlap", ConsoleColor.DarkCyan));
        return rows;
    }

    public static IReadOnlyList<CharacterSheetPanelLine> BuildUnidentified(InventoryItemSnapshot item,
        bool focused = false, int width = CharacterSheetPanel.Width)
    {
        var effectiveWidth = Math.Max(CharacterSheetPanel.Width, width);
        var rows = new List<CharacterSheetPanelLine>
        {
            new(0, "🔍 TÁRGYVIZSGÁLAT", ConsoleColor.Yellow,
                Background: focused ? ConsoleColor.DarkGreen : ConsoleColor.Black),
            new(1, $"❓ {item.Name}", ConsoleColor.DarkCyan)
        };

        var content = new List<(string Text, ConsoleColor Color)>
        {
            ("🕯 Azonosítatlan tárgy", ConsoleColor.DarkYellow),
            ($"📦 Kategória: {CategoryName(item.Category)}", ConsoleColor.Gray),
            ($"✨ Aura: {item.Description}", ConsoleColor.Cyan),
            ($"🔒 Azonosítva: nem", ConsoleColor.DarkYellow),
            ($"🧷 Mágikus erő: ismeretlen", ConsoleColor.DarkYellow),
            ($"💰 Eladási érték (becsült): {item.UnidentifiedSellPrice}", ConsoleColor.DarkYellow),
            ($"🎒 Mennyiség: {Math.Max(1, item.Quantity)}", ConsoleColor.Gray),
            (ItemInspectionFormatter.DurabilityText(item.MaximumDurability, item.DurabilityDamage).Trim(), ConsoleColor.Gray),
            (item.IsCurseActivated
                ? "☠ Az átok aktiválódott és a tárgy a viselőjéhez kötődött."
                : "☠ Átokról nincs megbízható információ azonosítás nélkül.",
                item.IsCurseActivated ? ConsoleColor.Red : ConsoleColor.DarkYellow),
            ("🏨 A Vándormágus teljesen azonosíthatja a fogadóban.", ConsoleColor.Magenta)
        };

        var row = ContentStartRow;
        foreach (var (text, color) in content.Where(entry => !string.IsNullOrWhiteSpace(entry.Text)))
        {
            if (row > ContentLastRow) break;
            foreach (var part in MessageTextLayout.Wrap(text, effectiveWidth))
            {
                if (row > ContentLastRow) break;
                rows.Add(new CharacterSheetPanelLine(row++, part, color));
            }
        }

        rows.Add(new CharacterSheetPanelLine(ControlsRow, "Esc / I / Enter: vissza", ConsoleColor.Green));
        rows.Add(new CharacterSheetPanelLine(CloseRow, "Nézet: teljes tárgyadatlap", ConsoleColor.DarkCyan));
        return rows;
    }

    private static IEnumerable<(string Text, ConsoleColor Color)> BuildCommonContent(IItemDefinition item,
        GameDataCatalog gameData, int quantity, int charges, InventoryItemInstanceState? state)
    {
        yield return ($"🧾 ID: {item.Id}", ConsoleColor.DarkGray);
        yield return ($"📦 Kategória: {CategoryName(item.Category)}", ConsoleColor.Gray);
        yield return ($"🏷 Ritkaság: {RarityName(item.Rarity)}", RarityColor(item.Rarity));
        yield return ($"✨ Mágikus erő: {item.MagicPower}", item.MagicPower > 0 ? ConsoleColor.Cyan : ConsoleColor.DarkGray);
        yield return ($"💰 Alapár: {item.BasePrice}", ConsoleColor.Yellow);
        yield return ($"⚖ Súly: {item.Weight:0.##}", ConsoleColor.Gray);
        yield return ($"🎒 Mennyiség: {Math.Max(1, quantity)}", ConsoleColor.Gray);

        if (item is MagicItemDefinition magic)
            yield return ($"🔋 Töltet: {charges}/{magic.MaximumCharges}", ConsoleColor.Cyan);

        if (state is { IsIdentified: true })
        {
            if (state.Value.HasCurse)
            {
                var curseName = gameData.GetItemCurse(state.Value.CurseId!).Name;
                yield return ($"☠ Átok: {curseName} (erősség: {state.Value.CurseStrength}/3)", ConsoleColor.Red);
                yield return (state.Value.IsCurseActivated
                    ? "⛓ Állapot: aktivált, karakterhez kötődhet"
                    : "🕯 Állapot: még nem aktivált", state.Value.IsCurseActivated ? ConsoleColor.Red : ConsoleColor.DarkYellow);
            }
            else if (state.Value.IsPurified)
                yield return ("✨ Átoktisztított tárgy", ConsoleColor.Green);
            else
                yield return ("🛡 Átok: nincs", ConsoleColor.DarkGreen);

            var maximumDurability = EquipmentDurabilityRules.MaximumDurability(item);
            if (maximumDurability > 0)
            {
                yield return (ItemInspectionFormatter.DurabilityText(maximumDurability,
                    state.Value.DurabilityDamage).Trim(), ConsoleColor.Gray);
                var combatEffect = ItemInspectionFormatter.DurabilityCombatEffectText(item,
                    state.Value.DurabilityDamage).Trim();
                if (!string.IsNullOrWhiteSpace(combatEffect))
                    yield return ($"⚔ {combatEffect}", ConsoleColor.DarkYellow);
            }
        }

        var description = string.IsNullOrWhiteSpace(item.Description) ? "Nincs jellemzés." : item.Description;
        yield return ($"📜 Leírás: {description}", ConsoleColor.White);
    }

    private static IEnumerable<(string Text, ConsoleColor Color)> BuildCategorySpecificContent(IItemDefinition item,
        GameDataCatalog gameData)
    {
        switch (item)
        {
            case WeaponDefinition weapon:
                yield return ("", ConsoleColor.Gray);
                yield return ("⚔ FEGYVER TULAJDONSÁGOK", ConsoleColor.Magenta);
                yield return ($"🗡 Típus: {(weapon.WeaponTypeId is { } typeId ? gameData.GetWeaponType(typeId).Name : "nincs")}", ConsoleColor.Gray);
                yield return ($"🎯 Sebzéstípus: {weapon.DamageType.Name()}", ConsoleColor.Gray);
                yield return ($"💥 Sebzés: {weapon.Damage?.ToString() ?? "nincs"}", ConsoleColor.Gray);
                yield return ($"👊 Minimum Erő: {weapon.MinimumStrength}", ConsoleColor.Gray);
                yield return ($"🎯 Célpontok száma: {weapon.MaximumTargets}", ConsoleColor.Gray);
                yield return ($"↘ Hátsó sorból használható: {(weapon.CanAttackFromRear ? "igen" : "nem")}", ConsoleColor.Gray);
                yield return ($"🤲 Kezelés: {(weapon.IsTwoHanded ? "kétkezes" : "egykezes")}", ConsoleColor.Gray);
                yield return ($"🎓 Engedélyezett kasztok: {AllowedClassNames(weapon.AllowedClassIds, gameData)}", ConsoleColor.Gray);
                if (!string.IsNullOrWhiteSpace(weapon.FamilyId))
                    yield return ($"🧬 Fegyvercsalád: {weapon.FamilyId}", ConsoleColor.DarkCyan);
                if (!string.IsNullOrWhiteSpace(weapon.BaseWeaponId))
                    yield return ($"🛠 Alapfegyver: {weapon.BaseWeaponId}", ConsoleColor.DarkCyan);
                yield break;

            case ArmorDefinition armor:
                yield return ("", ConsoleColor.Gray);
                yield return ("🛡 PÁNCÉL TULAJDONSÁGOK", ConsoleColor.Magenta);
                yield return ($"🧱 Védelem: {armor.Defense?.ToString() ?? "nincs"}", ConsoleColor.Gray);
                yield return ($"🪨 Ellenállások: {armor.Resistances ?? new DamageResistance()}", ConsoleColor.Gray);
                yield return ($"🎓 Engedélyezett kasztok: {AllowedClassNames(armor.AllowedClassIds, gameData)}", ConsoleColor.Gray);
                if (!string.IsNullOrWhiteSpace(armor.BaseArmorId))
                    yield return ($"🛠 Alappáncél: {armor.BaseArmorId}", ConsoleColor.DarkCyan);
                yield break;

            case MagicItemDefinition magic:
                yield return ("", ConsoleColor.Gray);
                yield return ("🔮 VARÁZSTÁRGY TULAJDONSÁGOK", ConsoleColor.Magenta);
                yield return ($"💍 Típus: {MagicItemKindName(magic.Kind)}", ConsoleColor.Gray);
                yield return ($"✨ Hatás: {MagicItemEffectName(magic.Effect)} {magic.EffectValue}", ConsoleColor.Gray);
                yield return ($"🎓 Engedélyezett kasztok: {AllowedClassNames(magic.AllowedClassIds, gameData)}", ConsoleColor.Gray);
                yield return (magic.SpellId is null
                    ? "📜 Beépített varázslat: nincs"
                    : $"📜 Beépített varázslat: {gameData.GetSpell(magic.SpellId).Name} ({magic.SpellId})", ConsoleColor.Cyan);
                yield break;

            case MiscItemDefinition misc:
                yield return ("", ConsoleColor.Gray);
                yield return ("🎒 HASZNÁLATI TÁRGY TULAJDONSÁGOK", ConsoleColor.Magenta);
                yield return ($"🍶 Hatás: {ConsumableEffectName(misc.Effect)} {misc.EffectValue}", ConsoleColor.Gray);
                yield return ($"⚔ Csatában használható: {(misc.UsableInCombat ? "igen" : "nem")}", ConsoleColor.Gray);
                yield break;
        }
    }

    private static string CategoryIcon(ItemCategory category) => category switch
    {
        ItemCategory.Weapon => "⚔",
        ItemCategory.Armor => "🛡",
        ItemCategory.MagicItem => "🔮",
        _ => "🎒"
    };

    private static string CategoryName(ItemCategory category) => category switch
    {
        ItemCategory.Weapon => "Fegyver",
        ItemCategory.Armor => "Páncél",
        ItemCategory.MagicItem => "Varázstárgy",
        _ => "Általános tárgy"
    };

    private static string RarityName(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Magic => "Varázs",
        ItemRarity.Legendary => "Legendás",
        _ => "Sima"
    };

    private static ConsoleColor RarityColor(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Magic => ConsoleColor.Cyan,
        ItemRarity.Legendary => ConsoleColor.Yellow,
        _ => ConsoleColor.Gray
    };

    private static string AllowedClassNames(IReadOnlySet<string> ids, GameDataCatalog gameData) =>
        string.Join(", ", gameData.CharacterClasses.Where(characterClass => ids.Contains(characterClass.Id))
            .Select(characterClass => characterClass.Name));

    private static string ConsumableEffectName(ConsumableEffect effect) => effect switch
    {
        ConsumableEffect.Food => "élelem",
        ConsumableEffect.Water => "víz",
        ConsumableEffect.Heal => "HP",
        ConsumableEffect.RestoreMana => "manna",
        ConsumableEffect.CurePoison => "mérgezés gyógyítása",
        ConsumableEffect.CureDisease => "betegség gyógyítása",
        ConsumableEffect.StopBleeding => "vérzés elállítása",
        ConsumableEffect.Vision => "látótáv",
        ConsumableEffect.RepairEquipment => "terepi javítás",
        _ => "nincs"
    };

    private static string MagicItemKindName(MagicItemKind kind) => kind switch
    {
        MagicItemKind.Amulet => "amulett",
        MagicItemKind.Wand => "varázspálca",
        MagicItemKind.Scroll => "varázstekercs",
        _ => "varázsgyűrű"
    };

    private static string MagicItemEffectName(MagicItemEffect effect) => effect switch
    {
        MagicItemEffect.Initiative => "kezdeményezés",
        MagicItemEffect.Hit => "találati próba",
        MagicItemEffect.Damage => "sebzés",
        MagicItemEffect.Defense => "védelem",
        MagicItemEffect.BattleHeal => "csata eleji HP",
        MagicItemEffect.BattleMana => "csata eleji manna",
        MagicItemEffect.Strength => "Erő",
        MagicItemEffect.Dexterity => "Ügyesség",
        MagicItemEffect.Health => "Egészség",
        MagicItemEffect.Intelligence => "Intelligencia",
        _ => "varázslattároló"
    };
}
