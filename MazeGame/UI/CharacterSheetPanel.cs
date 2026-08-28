using MazeGame.Application;
using MazeGame.Domain.Characters;
using MazeGame.Domain.Inventory;

namespace MazeGame.UI;

public readonly record struct InventorySlotAddress(InventorySlotKind Kind, int Index);

public sealed record CharacterSheetPanelLine(int Row, string Text, ConsoleColor Color,
    InventorySlotAddress? InventorySlot = null);

public sealed record PartyStatusLine(string Identity, ConsoleColor IdentityColor,
    string Vitality, ConsoleColor VitalityColor, string Mana, ConsoleColor ManaColor)
{
    public string Text => Identity + Vitality + Mana;
}

/// <summary>A host és a vendég azonos karakterlap- és inventory-sorelrendezése.</summary>
public static class CharacterSheetPanel
{
    public const int Width = 27;
    private const int ResourceIconStep = 10;

    public static string CharacterClassGlyph(string characterClassId) => characterClassId switch
    {
        CharacterClassIds.Harcos => "H",
        CharacterClassIds.Barbár => "B",
        CharacterClassIds.Lovag => "L",
        CharacterClassIds.Tolvaj => "T",
        CharacterClassIds.Pap => "P",
        CharacterClassIds.Mágus => "M",
        _ => "?"
    };

    public static PartyStatusLine BuildPartyStatus(LiveCharacter character, bool isDisplayed, bool isLeader = false) =>
        BuildPartyStatus(character.Name, character.CharacterClass.Id, character.CurrentVitality,
            character.MaximumVitality, character.CurrentMana, character.MaximumMana, character.IsAlive,
            character.Color, isDisplayed, isLeader);

    public static PartyStatusLine BuildPartyStatus(SessionCharacterSnapshot character, bool isDisplayed,
        bool isLeader = false) =>
        BuildPartyStatus(character.Name, character.CharacterClassId, character.CurrentVitality,
            character.MaximumVitality, character.CurrentMana, character.MaximumMana, character.IsAlive,
            character.Color, isDisplayed, isLeader);

    private static PartyStatusLine BuildPartyStatus(string name, string classId, int currentVitality,
        int maximumVitality, int currentMana, int maximumMana, bool isAlive, ConsoleColor identityColor,
        bool isDisplayed, bool isLeader)
    {
        var marker = isDisplayed ? "▶ " : "  ";
        var prefix = $"{marker}{(isLeader ? "👑" : string.Empty)}{CharacterClassGlyph(classId)} ";
        if (!isAlive)
        {
            const string dead = " 💀";
            return new PartyStatusLine(prefix + Shorten(name, Width - prefix.Length - dead.Length),
                identityColor, dead, ConsoleColor.DarkRed, string.Empty, ConsoleColor.DarkGray);
        }

        var vitalityPercent = Percent(currentVitality, maximumVitality);
        var manaPercent = Percent(currentMana, maximumMana);
        var vitality = $" ❤️{vitalityPercent}%";
        var mana = maximumMana > 0 ? $" 🔷{manaPercent}%" : string.Empty;
        var maximumNameLength = Width - prefix.Length - vitality.Length - mana.Length;
        return new PartyStatusLine(prefix + Shorten(name, maximumNameLength), identityColor,
            vitality, vitalityPercent <= 25 ? ConsoleColor.Red :
            vitalityPercent <= 50 ? ConsoleColor.Yellow : ConsoleColor.Green,
            mana, maximumMana <= 0 || currentMana <= 0 ? ConsoleColor.DarkGray :
            manaPercent <= 50 ? ConsoleColor.Blue : ConsoleColor.Cyan);
    }

    private static int Percent(int current, int maximum) => maximum <= 0
        ? 0
        : Math.Clamp((int)Math.Round(current * 100d / maximum), 0, 100);

    private static string Shorten(string value, int maximumLength) =>
        value[..Math.Min(value.Length, Math.Max(1, maximumLength))];

    public static IReadOnlyList<CharacterSheetPanelLine> Build(LiveCharacter character,
        IReadOnlyDictionary<int, int> experienceByLevel, int mazeLevel, int goldenKeyCount, int bossCount,
        bool isPartyLeader = false)
    {
        var snapshot = new SessionCharacterSnapshot(character.Id, character.Name, character.Race.Id,
            character.CharacterClass.Id, character.Level, character.CurrentVitality, character.MaximumVitality,
            character.CurrentMana, character.MaximumMana, character.FoodLevel, character.WaterLevel,
            character.Gold, character.IsAlive, null, character.Statuses.Select(status => status.Id).ToArray(),
            InventorySnapshotProjector.Create(character),
            CharacterSheetSnapshotProjector.Create(character, experienceByLevel));
        return Build(snapshot, mazeLevel, goldenKeyCount, bossCount, isPartyLeader);
    }

    public static IReadOnlyList<CharacterSheetPanelLine> Build(SessionCharacterSnapshot character,
        int mazeLevel, int goldenKeyCount, int bossCount, bool isPartyLeader = false)
    {
        ArgumentNullException.ThrowIfNull(character);
        var details = character.CharacterSheet ?? throw new ArgumentException(
            "A karakterlap-projekció hiányzik a session snapshotból.", nameof(character));
        var inventory = character.Inventory ?? throw new ArgumentException(
            "Az inventory-projekció hiányzik a session snapshotból.", nameof(character));
        var lines = new List<CharacterSheetPanelLine>
        {
            new(0, $"KARAKTERLAP - {character.Name}", ConsoleColor.Yellow),
            new(1, $"{details.RaceName} {details.CharacterClassName}" + (isPartyLeader ? "  👑 VEZÉR" : string.Empty),
                isPartyLeader ? ConsoleColor.Yellow : ConsoleColor.White)
        };
        var perkRows = CompactRows("Teh", details.PerkNames, 2);
        lines.Add(new(2, perkRows[0], ConsoleColor.Magenta));
        lines.Add(new(3, perkRows[1], ConsoleColor.Magenta));
        var statusIcons = details.StatusIcons
            .Select(icon => icon == "🪨" ? ConsoleRenderer.DamageReductionIcon : icon).ToArray();
        lines.Add(new(4, statusIcons.Length == 0 ? "Áll: nincs" : $"Áll: {string.Join(' ', statusIcons)}",
            details.StatusIcons.Count == 0 ? ConsoleColor.DarkGray : ConsoleColor.Magenta));
        lines.Add(new(5, $"Labirintus: {mazeLevel}  🔑 {goldenKeyCount}/{bossCount}", ConsoleColor.Green));
        lines.Add(new(6, details.NextLevelExperience is { } next
            ? $"Szint: {character.Level}  XP: {details.Experience}/{next}"
            : $"Szint: {character.Level}  XP: MAX", ConsoleColor.Cyan));
        lines.Add(new(7, $"💪{details.Abilities.Strength} 🏹{details.Abilities.Dexterity} ❤️{details.Abilities.Health} 🧠{details.Abilities.Intelligence}", ConsoleColor.White));
        lines.Add(new(8, "OSZTÁLYFEJLESZTÉSEK", ConsoleColor.DarkCyan));
        var upgrades = details.ClassFeatureUpgradeNames ?? [];
        lines.Add(new(9, upgrades.Count > 0 ? Shorten($"L10: {upgrades[0]}", Width) : "L10: —", upgrades.Count > 0 ? ConsoleColor.Cyan : ConsoleColor.DarkGray));
        lines.Add(new(10, upgrades.Count > 1 ? Shorten($"L20: {upgrades[1]}", Width) : "L20: —", upgrades.Count > 1 ? ConsoleColor.Cyan : ConsoleColor.DarkGray));
        var proficiencies = details.WeaponProficiencyNames ?? [];
        lines.Add(new(11, proficiencies.Count > 0 ? $"Fegyver: {string.Join(' ', proficiencies)}" : "Fegyver: —",
            proficiencies.Count > 0 ? ConsoleColor.Yellow : ConsoleColor.DarkGray));
        lines.Add(new(12, $"❤️{character.CurrentVitality}/{character.MaximumVitality}" +
            (details.UsesMana ? $"  🔷{character.CurrentMana}/{character.MaximumMana}" : string.Empty),
            details.UsesMana ? ConsoleColor.Cyan : ConsoleColor.Red));
        lines.Add(new(13, $"É: {ResourceIcons("🍖", character.FoodLevel)}", ConsoleColor.Yellow));
        lines.Add(new(14, $"V: {ResourceIcons("💧", character.WaterLevel)}", ConsoleColor.Cyan));
        lines.Add(new(15, $"Arany: {character.Gold} {ConsoleRenderer.MoneyIcon}", ConsoleColor.Yellow));
        lines.Add(new(17, "FEGYVEREK", ConsoleColor.Yellow));
        AddInventoryLines(lines, inventory);
        return lines;
    }

    private static void AddInventoryLines(ICollection<CharacterSheetPanelLine> lines,
        CharacterInventorySnapshot inventory)
    {
        var weapons = Slots(inventory, InventorySlotKind.Weapon, 2);
        lines.Add(new(18, $"1: {ItemName(weapons[0].Item)}", ConsoleColor.Gray,
            new InventorySlotAddress(InventorySlotKind.Weapon, 0)));
        lines.Add(new(19, weapons[0].Item?.IsTwoHanded == true
                ? "2: ⛔ kétkezes fegyver"
                : $"2: {ItemName(weapons[1].Item)}",
            weapons[0].Item?.IsTwoHanded == true ? ConsoleColor.DarkGray : ConsoleColor.Gray,
            new InventorySlotAddress(InventorySlotKind.Weapon, 1)));
        var armor = Slots(inventory, InventorySlotKind.Armor, 1)[0];
        lines.Add(new(20, $"Páncél: {ItemName(armor.Item)}", ConsoleColor.DarkYellow,
            new InventorySlotAddress(InventorySlotKind.Armor, 0)));

        var magicItems = Slots(inventory, InventorySlotKind.MagicItem, 3);
        lines.Add(new(22, $"VARÁZSTÁRGYAK {magicItems.Count(slot => slot.Item is not null)}/3", ConsoleColor.Magenta));
        for (var index = 0; index < magicItems.Count; index++)
            lines.Add(new(23 + index, $"{index + 1}: {ItemName(magicItems[index].Item)}", ConsoleColor.Gray,
                new InventorySlotAddress(InventorySlotKind.MagicItem, index)));

        var backpack = Slots(inventory, InventorySlotKind.Backpack, LiveCharacter.MaximumBackpackItemCount);
        lines.Add(new(26, $"HÁTIZSÁK {backpack.Count(slot => slot.Item is not null)}/{LiveCharacter.MaximumBackpackItemCount}", ConsoleColor.DarkCyan));
        for (var index = 0; index < backpack.Count; index++)
            lines.Add(new(27 + index, $"{index + 1}: {ItemName(backpack[index].Item)}", ConsoleColor.Gray,
                new InventorySlotAddress(InventorySlotKind.Backpack, index)));
    }

    private static IReadOnlyList<InventorySlotSnapshot> Slots(CharacterInventorySnapshot inventory,
        InventorySlotKind kind, int count) => Enumerable.Range(0, count)
        .Select(index => inventory.Slots.First(slot => slot.Kind == kind && slot.Index == index)).ToArray();

    private static string ItemName(InventoryItemSnapshot? item) => item is null
        ? "üres"
        : (item.MaximumCharges > 0 ? $"{item.Name} ({item.Charges}/{item.MaximumCharges})" : item.Name) +
          (item.Quantity > 1 ? $" ×{item.Quantity}" : string.Empty);

    private static string ResourceIcons(string icon, int level) =>
        string.Concat(Enumerable.Repeat(icon, level / ResourceIconStep));

    private static IReadOnlyList<string> CompactRows(string prefix, IEnumerable<string> values, int rowCount)
    {
        var names = values.ToList();
        if (names.Count == 0) return [$"{prefix}: nincs", .. Enumerable.Repeat(string.Empty, rowCount - 1)];
        var rows = new List<string>(rowCount);
        var namesPerRow = (int)Math.Ceiling(names.Count / (double)rowCount);
        for (var row = 0; row < rowCount; row++)
        {
            var rowNames = names.Skip(row * namesPerRow).Take(namesPerRow).ToList();
            if (rowNames.Count == 0)
            {
                rows.Add(string.Empty);
                continue;
            }
            var rowPrefix = row == 0 ? $"{prefix}: " : new string(' ', prefix.Length + 2);
            var separatorWidth = (rowNames.Count - 1) * 2;
            var availablePerName = Math.Max(1, (Width - rowPrefix.Length - separatorWidth) / rowNames.Count);
            rows.Add(rowPrefix + string.Join(", ", rowNames.Select(name =>
                name.Length <= availablePerName ? name : name[..availablePerName])));
        }
        return rows;
    }
}
