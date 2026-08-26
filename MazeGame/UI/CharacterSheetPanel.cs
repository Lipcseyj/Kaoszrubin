using MazeGame.Application;
using MazeGame.Domain.Characters;
using MazeGame.Domain.Inventory;

namespace MazeGame.UI;

public readonly record struct InventorySlotAddress(InventorySlotKind Kind, int Index);

public sealed record CharacterSheetPanelLine(int Row, string Text, ConsoleColor Color,
    InventorySlotAddress? InventorySlot = null);

/// <summary>A host és a vendég azonos karakterlap- és inventory-sorelrendezése.</summary>
public static class CharacterSheetPanel
{
    public const int Width = 27;
    private const int ResourceIconStep = 10;

    public static IReadOnlyList<CharacterSheetPanelLine> Build(LiveCharacter character,
        IReadOnlyDictionary<int, int> experienceByLevel, int mazeLevel, int goldenKeyCount, int bossCount)
    {
        var snapshot = new SessionCharacterSnapshot(character.Id, character.Name, character.Race.Id,
            character.CharacterClass.Id, character.Level, character.CurrentVitality, character.MaximumVitality,
            character.CurrentMana, character.MaximumMana, character.FoodLevel, character.WaterLevel,
            character.Gold, character.IsAlive, null, character.Statuses.Select(status => status.Id).ToArray(),
            InventorySnapshotProjector.Create(character),
            CharacterSheetSnapshotProjector.Create(character, experienceByLevel));
        return Build(snapshot, mazeLevel, goldenKeyCount, bossCount);
    }

    public static IReadOnlyList<CharacterSheetPanelLine> Build(SessionCharacterSnapshot character,
        int mazeLevel, int goldenKeyCount, int bossCount)
    {
        ArgumentNullException.ThrowIfNull(character);
        var details = character.CharacterSheet ?? throw new ArgumentException(
            "A karakterlap-projekció hiányzik a session snapshotból.", nameof(character));
        var inventory = character.Inventory ?? throw new ArgumentException(
            "Az inventory-projekció hiányzik a session snapshotból.", nameof(character));
        var lines = new List<CharacterSheetPanelLine>
        {
            new(0, $"KARAKTERLAP - {character.Name}", ConsoleColor.Yellow),
            new(1, $"{details.RaceName} {details.CharacterClassName}", ConsoleColor.White)
        };
        var perkRows = CompactRows("Teh", details.PerkNames, 2);
        lines.Add(new(2, perkRows[0], ConsoleColor.Magenta));
        lines.Add(new(3, perkRows[1], ConsoleColor.Magenta));
        lines.Add(new(4, details.StatusIcons.Count == 0 ? "Áll: nincs" : $"Áll: {string.Join(' ', details.StatusIcons)}",
            details.StatusIcons.Count == 0 ? ConsoleColor.DarkGray : ConsoleColor.Magenta));
        lines.Add(new(5, $"Labirintus: {mazeLevel}  🔑 {goldenKeyCount}/{bossCount}", ConsoleColor.Green));
        lines.Add(new(6, details.NextLevelExperience is { } next
            ? $"Szint: {character.Level}  XP: {details.Experience}/{next}"
            : $"Szint: {character.Level}  XP: MAX", ConsoleColor.Cyan));
        lines.Add(new(7, $"Erő: {details.Abilities.Strength}", ConsoleColor.Red));
        lines.Add(new(8, $"Ügy: {details.Abilities.Dexterity}", ConsoleColor.Green));
        lines.Add(new(9, $"Egs: {details.Abilities.Health}", ConsoleColor.DarkYellow));
        lines.Add(new(10, $"Int: {details.Abilities.Intelligence}", ConsoleColor.Magenta));
        lines.Add(new(11, $"HP: {character.CurrentVitality}/{character.MaximumVitality}", ConsoleColor.Red));
        lines.Add(new(12, details.UsesMana ? $"Manna: {character.CurrentMana}/{character.MaximumMana}" : "Manna: nincs",
            ConsoleColor.Blue));
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

        var backpack = Slots(inventory, InventorySlotKind.Backpack, 10);
        lines.Add(new(26, $"HÁTIZSÁK {backpack.Count(slot => slot.Item is not null)}/10", ConsoleColor.DarkCyan));
        for (var index = 0; index < backpack.Count; index++)
            lines.Add(new(27 + index, $"{index + 1}: {ItemName(backpack[index].Item)}", ConsoleColor.Gray,
                new InventorySlotAddress(InventorySlotKind.Backpack, index)));
    }

    private static IReadOnlyList<InventorySlotSnapshot> Slots(CharacterInventorySnapshot inventory,
        InventorySlotKind kind, int count) => Enumerable.Range(0, count)
        .Select(index => inventory.Slots.First(slot => slot.Kind == kind && slot.Index == index)).ToArray();

    private static string ItemName(InventoryItemSnapshot? item) => item is null
        ? "üres"
        : item.MaximumCharges > 0 ? $"{item.Name} ({item.Charges}/{item.MaximumCharges})" : item.Name;

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
