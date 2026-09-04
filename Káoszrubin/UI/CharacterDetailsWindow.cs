using KaoszRubin.Application;
using KaoszRubin.Data;
using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.UI;

/// <summary>A host és a vendég közös, lapozható karakterdossziéja.</summary>
public static class CharacterDetailsWindow
{
    public const int Width = 84;

    public static IReadOnlyList<(string Text, ConsoleColor Color)> Build(SessionCharacterSnapshot character,
        GameDataCatalog data)
    {
        var sheet = character.CharacterSheet ?? throw new ArgumentException("Hiányzó karakterlap.", nameof(character));
        var lines = new List<(string, ConsoleColor)>
        {
            ($"👤 {character.Name.ToUpperInvariant()} — RÉSZLETES KARAKTERINFÓ", ConsoleColor.Yellow),
            ($"{sheet.RaceName} {sheet.CharacterClassName}  |  {character.Level}. szint", character.Color),
            ($"XP: {sheet.Experience}" + (sheet.NextLevelExperience is { } next ? $" / {next}" : " (maximum)"), ConsoleColor.Gray),
            ($"Állapot: {(character.IsAlive ? "életben" : "halott")}  |  Arany: {character.Gold}", character.IsAlive ? ConsoleColor.Green : ConsoleColor.Red),
            (string.Empty, ConsoleColor.Gray),
            ("ERŐFORRÁSOK ÉS TULAJDONSÁGOK", ConsoleColor.Cyan),
            ($"❤️ HP {character.CurrentVitality}/{character.MaximumVitality}   🔷 Mana {character.CurrentMana}/{character.MaximumMana}", ConsoleColor.White),
            ($"🍖 Étel {character.FoodLevel}/100   💧 Ital {character.WaterLevel}/100", ConsoleColor.White),
            ($"Erő {sheet.Abilities.Strength}  Ügyesség {sheet.Abilities.Dexterity}  Egészség {sheet.Abilities.Health}  Intelligencia {sheet.Abilities.Intelligence}", ConsoleColor.White),
            ($"⚔ ⚖ Harci terhelés: {sheet.EquippedWeight} súly — {sheet.Encumbrance}", LoadColor(sheet.Encumbrance)),
            ($"🎒 Hordott súly: {sheet.CarriedWeight:F2}/{sheet.CarryingCapacity} — {sheet.CarriedEncumbrance}", LoadColor(sheet.CarriedEncumbrance)),
            ($"⚡ Kezdeményezési alap: {sheet.InitiativeBase}  |  👣 Harci mozgás: {sheet.CombatMovementAllowance} mező", ConsoleColor.White),
            ($"🥾 Térképi mozgás: {sheet.ExplorationMovementAllowance}", ConsoleColor.White),
            (string.Empty, ConsoleColor.Gray),
            ("LÁTÁS", ConsoleColor.Cyan),
            ($"👁 Tényleges látótáv: {sheet.VisionRange}  |  természetes: {sheet.NaturalVisionRange}", ConsoleColor.White)
        };
        foreach (var modifier in sheet.VisionModifiers ?? [])
            lines.Add(($"  {(modifier.Value >= 0 ? "+" : string.Empty)}{modifier.Value}  {ResolveVisionName(modifier.Name, data)}",
                modifier.Value > 0 ? ConsoleColor.Green : modifier.Value < 0 ? ConsoleColor.Red : ConsoleColor.Gray));
        lines.Add(($"👂 Hallótáv: {sheet.HearingRange}  |  🔎 Felderítési bónusz: +{sheet.DetectionBonus}", ConsoleColor.White));

        AddSection(lines, "TEHETSÉGEK ÉS ÁLLAPOTOK", sheet.PerkNames, "Nincs tehetség.");
        AddSection(lines, "OSZTÁLYFEJLESZTÉSEK", sheet.ClassFeatureUpgradeNames ?? [], "Nincs osztályfejlesztés.");
        AddSection(lines, "FEGYVERJÁRTASSÁGOK", sheet.DetailedWeaponProficiencyNames ??
            sheet.WeaponProficiencyNames ?? [], "Nincs fegyverjártasság.");
        AddSection(lines, "ÁLLAPOTJELZŐK", sheet.StatusIcons, "Nincs aktív állapot.");
        if (character.SpellInfo is { } spellInfo)
        {
            lines.Add((string.Empty, ConsoleColor.Gray));
            lines.Add(("VARÁZSLATOK", ConsoleColor.Cyan));
            lines.Add(($"  Fókusz: {spellInfo.FocusName}  |  Memória: {spellInfo.KnownSpells.Count(spell => spell.IsMemorized)}/{spellInfo.MemorizationCapacity}", ConsoleColor.White));
            foreach (var spell in spellInfo.KnownSpells)
                lines.Add(($"  {(spell.IsMemorized ? "◆" : "◇")} {spell.Name} ({spell.Level}. kör, {spell.ManaCost} mana)" +
                    (spell.QuickSlot is { } quick ? $" [F{quick + 1}]" : string.Empty),
                    spell.IsMemorized ? ConsoleColor.Cyan : ConsoleColor.DarkGray));
        }

        lines.Add((string.Empty, ConsoleColor.Gray));
        lines.Add(("FELSZERELÉS ÉS HÁTIZSÁK", ConsoleColor.Cyan));
        foreach (var slot in character.Inventory?.Slots.Where(slot => slot.Item is not null) ?? [])
        {
            var item = slot.Item!;
            var quantity = item.Quantity > 1 ? $" ×{item.Quantity}" : string.Empty;
            lines.Add(($"  {SlotName(slot.Kind, slot.Index)}: {item.Name}{quantity}", ConsoleColor.White));
        }
        if (character.Inventory?.Slots.All(slot => slot.Item is null) != false)
            lines.Add(("  — Nincs nála tárgy.", ConsoleColor.DarkGray));

        if (character.History?.NpcJoinedMazeLevel is { } level)
        {
            lines.Add((string.Empty, ConsoleColor.Gray));
            lines.Add(("NPC ELŐÉLET", ConsoleColor.Cyan));
            lines.Add(($"  Csatlakozás: {level}. szint — {character.History.NpcJoinedLocation}", ConsoleColor.Yellow));
            if (!string.IsNullOrWhiteSpace(character.History.NpcBehavior))
                lines.Add(($"  Viselkedés: {character.History.NpcBehavior}", ConsoleColor.White));
        }

        lines.Add((string.Empty, ConsoleColor.Gray));
        lines.Add(("LEGYŐZÖTT SZÖRNYEK", ConsoleColor.Cyan));
        var kills = character.History?.MonsterKills.Where(kill => kill.Count > 0)
            .OrderByDescending(kill => kill.Count).ThenBy(kill => kill.EnemyDefinitionId).ToArray() ?? [];
        if (kills.Length == 0) lines.Add(("  — Még nincs saját ölés.", ConsoleColor.DarkGray));
        foreach (var kill in kills)
        {
            var name = data.Enemies.FirstOrDefault(enemy => string.Equals(enemy.Id, kill.EnemyDefinitionId,
                StringComparison.OrdinalIgnoreCase))?.Name ?? kill.EnemyDefinitionId;
            lines.Add(($"  ☠ {name}: {kill.Count}", ConsoleColor.Green));
        }
        return lines;
    }

    public static void Show(SessionCharacterSnapshot character, GameDataCatalog data)
    {
        var allLines = Build(character, data);
        var offset = 0;
        var pageSize = Math.Max(4, Console.WindowHeight - 8);
        var visibleLineCount = Math.Min(allLines.Count, pageSize) + 2;
        var width = Math.Min(Width, Math.Max(20, Console.WindowWidth));
        var height = visibleLineCount + 2;
        var left = Math.Max(0, (Console.WindowWidth - width) / 2);
        var top = Math.Max(0, (Console.WindowHeight - height) / 2);
        using var background = new BackgroundContentRestorer(left, top, width, height);
        while (true)
        {
            var maximumOffset = Math.Max(0, allLines.Count - pageSize);
            offset = Math.Clamp(offset, 0, maximumOffset);
            var page = allLines.Skip(offset).Take(pageSize).ToList();
            page.Add(($"↑/↓, PgUp/PgDn: lapozás  {offset + 1}–{Math.Min(allLines.Count, offset + pageSize)}/{allLines.Count}", ConsoleColor.DarkYellow));
            page.Add(("R / Enter / Esc: bezárás", ConsoleColor.DarkYellow));
            Draw(page);
            var key = Console.ReadKey(intercept: true).Key;
            if (key is ConsoleKey.R or ConsoleKey.Enter or ConsoleKey.Escape) return;
            offset = key switch { ConsoleKey.UpArrow => offset - 1, ConsoleKey.DownArrow => offset + 1,
                ConsoleKey.PageUp => offset - pageSize, ConsoleKey.PageDown => offset + pageSize, _ => offset };
        }
    }

    public static IReadOnlyList<(string Text, ConsoleColor Color)> Page(IReadOnlyList<(string Text, ConsoleColor Color)> lines,
        int offset, int pageSize) => lines.Skip(Math.Clamp(offset, 0, Math.Max(0, lines.Count - pageSize)))
        .Take(pageSize).Append(("↑/↓, PgUp/PgDn: lapozás | R/Esc: bezárás", ConsoleColor.DarkYellow)).ToArray();

    private static void AddSection(List<(string, ConsoleColor)> lines, string title, IEnumerable<string> values,
        string empty)
    {
        lines.Add((string.Empty, ConsoleColor.Gray)); lines.Add((title, ConsoleColor.Cyan));
        var items = values.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        if (items.Length == 0) lines.Add(("  — " + empty, ConsoleColor.DarkGray));
        else foreach (var item in items) AddWrapped(lines, $"  • {item}", ConsoleColor.White);
    }

    private static void AddWrapped(ICollection<(string Text, ConsoleColor Color)> lines, string text,
        ConsoleColor color)
    {
        const int maximumLength = 74;
        var remaining = text;
        while (remaining.Length > maximumLength)
        {
            var split = remaining.LastIndexOf(' ', maximumLength);
            if (split < 8) split = maximumLength;
            lines.Add((remaining[..split], color));
            remaining = "      " + remaining[split..].TrimStart();
        }
        lines.Add((remaining, color));
    }

    private static string ResolveVisionName(string id, GameDataCatalog data) =>
        data.Spells.FirstOrDefault(value => SameId(value.Id, id))?.Name ??
        data.Items.FirstOrDefault(value => SameId(value.Id, id))?.Name ??
        data.MagicItems.FirstOrDefault(value => SameId(value.Id, id))?.Name ??
        data.Weapons.FirstOrDefault(value => SameId(value.Id, id))?.Name ??
        data.Armors.FirstOrDefault(value => SameId(value.Id, id))?.Name ?? id;
    private static bool SameId(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    private static ConsoleColor LoadColor(string encumbrance) => encumbrance switch
    {
        "Nehéz" => ConsoleColor.Red,
        "Közepes" => ConsoleColor.Yellow,
        _ => ConsoleColor.Green
    };
    private static string SlotName(InventorySlotKind kind, int index) => kind switch
    { InventorySlotKind.Weapon => $"Fegyver {index + 1}", InventorySlotKind.Armor => "Páncél",
      InventorySlotKind.MagicItem => $"Varázstárgy {index + 1}", _ => $"Hátizsák {index + 1}" };

    private static void Draw(IReadOnlyList<(string Text, ConsoleColor Color)> lines)
    {
        var width = Math.Min(Width, Math.Max(20, Console.WindowWidth));
        var style = WindowFrameConfiguration.For(FramedWindow.CharacterDetails);
        var left = Math.Max(0, (Console.WindowWidth - width) / 2); var top = Math.Max(0, (Console.WindowHeight - lines.Count - 2) / 2);
        Write(left, top, WindowFrameCatalog.Horizontal(style, width), ConsoleColor.Magenta);
        for (var index = 0; index < lines.Count; index++)
        {
            var sides = WindowFrameCatalog.Sides(style, index, lines.Count); var contentWidth = Math.Max(0, width - sides.Left.Length - sides.Right.Length - 2);
            var text = lines[index].Text.Length <= contentWidth ? lines[index].Text : lines[index].Text[..contentWidth];
            Write(left, top + index + 1, sides.Left, ConsoleColor.Magenta);
            Write(left + sides.Left.Length, top + index + 1, " " + text.PadRight(contentWidth) + " ", lines[index].Color);
            Write(left + width - sides.Right.Length, top + index + 1, sides.Right, ConsoleColor.Magenta);
        }
        Write(left, top + lines.Count + 1, WindowFrameCatalog.Horizontal(style, width, true), ConsoleColor.Magenta); Console.ResetColor();
    }

    private static void Write(int left, int top, string text, ConsoleColor color)
    {
        if (top < 0 || top >= Console.WindowHeight || left >= Console.WindowWidth) return;
        Console.SetCursorPosition(Math.Max(0, left), top); Console.ForegroundColor = color;
        Console.Write(text.Length <= Console.WindowWidth - Math.Max(0, left) ? text : text[..Math.Max(0, Console.WindowWidth - Math.Max(0, left))]);
    }
}
