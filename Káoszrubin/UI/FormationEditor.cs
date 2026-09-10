using KaoszRubin.Domain.Characters;

namespace KaoszRubin.UI;

public static class FormationEditor
{
    private const int WindowWidth = 104;
    private const int WindowHeight = 18;

    public sealed record Result(IReadOnlyList<CharacterId?> Slots,
        IReadOnlyDictionary<CharacterId, NpcSpellcasterTactics> SpellcasterTactics);

    public static Result Edit(IReadOnlyList<LiveCharacter> party, PartyFormationSnapshot formation,
        IReadOnlyDictionary<CharacterId, NpcSpellcasterTactics> currentTactics,
        Action<int, IReadOnlyList<(string Text, ConsoleColor Color)>, FramedWindow?>? presentationChanged = null)
    {
        var slots = formation.Slots.ToArray();
        var tactics = currentTactics.ToDictionary(pair => pair.Key, pair => pair.Value);
        var cursor = 0;
        int? pickedUp = null;
        var width = Math.Min(WindowWidth, Console.WindowWidth);
        var height = Math.Min(WindowHeight, Console.WindowHeight);
        var left = Math.Max(0, (Console.WindowWidth - width) / 2);
        var top = Math.Max(0, (Console.WindowHeight - height) / 2);
        using var background = new BackgroundContentRestorer(left, top, width, height);
        while (true)
        {
            presentationChanged?.Invoke(width,
                BuildFormationPresentation(party, slots, cursor, pickedUp, tactics),
                FramedWindow.FormationEditor);
            Draw(party, slots, cursor, pickedUp, tactics, left, top, width, height);
            var key = Console.ReadKey(intercept: true).Key;
            if (key == ConsoleKey.Escape) return new Result(slots, tactics);
            if (key == ConsoleKey.T && slots[cursor] is { } tacticsId &&
                party.FirstOrDefault(member => member.Id == tacticsId) is { IsSpellcaster: true } spellcaster)
            {
                tactics[tacticsId] = EditSpellcasterTactics(spellcaster,
                    tactics.GetValueOrDefault(tacticsId,
                        NpcSpellcasterTactics.DefaultFor(spellcaster.CharacterClass.Id)),
                    left, top, width, height, presentationChanged);
                continue;
            }
            if (key == ConsoleKey.Enter || key == ConsoleKey.Spacebar)
            {
                if (pickedUp is null) pickedUp = cursor;
                else
                {
                    (slots[pickedUp.Value], slots[cursor]) = (slots[cursor], slots[pickedUp.Value]);
                    pickedUp = null;
                }
                continue;
            }
            cursor = key switch
            {
                ConsoleKey.LeftArrow when cursor % 2 == 1 => cursor - 1,
                ConsoleKey.RightArrow when cursor % 2 == 0 => cursor + 1,
                ConsoleKey.UpArrow when cursor >= 2 => cursor - 2,
                ConsoleKey.DownArrow when cursor < 2 => cursor + 2,
                _ => cursor
            };
        }
    }

    private static IReadOnlyList<(string Text, ConsoleColor Color)> BuildFormationPresentation(
        IReadOnlyList<LiveCharacter> party, IReadOnlyList<CharacterId?> slots, int cursor, int? pickedUp,
        IReadOnlyDictionary<CharacterId, NpcSpellcasterTactics> tactics)
    {
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            ("⚔  ALAKZATSZERKESZTŐ  ⚔", ConsoleColor.Yellow),
            (string.Empty, ConsoleColor.Gray),
            ("A host az egész csapat alakzatát szerkeszti — read-only nézet.", ConsoleColor.Cyan),
            (string.Empty, ConsoleColor.Gray),
            ("HALADÁSI IRÁNY  ▲", ConsoleColor.Cyan),
            (string.Empty, ConsoleColor.Gray)
        };
        var positionNames = new[] { "ELSŐ BAL", "ELSŐ JOBB", "HÁTSÓ BAL", "HÁTSÓ JOBB" };
        for (var index = 0; index < positionNames.Length; index++)
        {
            var character = slots[index] is { } id ? party.FirstOrDefault(member => member.Id == id) : null;
            var marker = index == cursor ? "▶" : index == pickedUp ? "◆" : " ";
            lines.Add(($"{marker} {positionNames[index],-12}: {character?.Name ?? "— üres —"}",
                index == cursor ? ConsoleColor.Yellow : character?.Color ?? ConsoleColor.DarkGray));
        }
        if (slots[cursor] is { } selectedId &&
            party.FirstOrDefault(member => member.Id == selectedId) is { IsSpellcaster: true } spellcaster)
        {
            var value = tactics.GetValueOrDefault(selectedId,
                NpcSpellcasterTactics.DefaultFor(spellcaster.CharacterClass.Id));
            lines.Add((string.Empty, ConsoleColor.Gray));
            lines.Add(($"Varázstaktika: {value.OffensiveSpellsPerBattle}/csata, erő " +
                       $"{value.MinimumEnemyStrength}–{value.FullOffenseEnemyStrength}, " +
                       (value.ManaFallback == SpellcasterManaFallback.Retreat
                           ? "hátravonulás" : "önbuff + közelharc"), ConsoleColor.DarkCyan));
        }
        return lines;
    }

    private static void Draw(IReadOnlyList<LiveCharacter> party, IReadOnlyList<CharacterId?> slots,
        int cursor, int? pickedUp, IReadOnlyDictionary<CharacterId, NpcSpellcasterTactics> tactics,
        int left, int top, int width, int height)
    {
        DrawEmptyWindow(left, top, width, height);

        WriteCentered(left, top + 1, width, "⚔  ALAKZATSZERKESZTŐ  ⚔", ConsoleColor.Yellow);
        WriteCentered(left, top + 3, width,
            "Nyilak: helyválasztás   Enter/Space: felemelés és csere", ConsoleColor.Gray);
        WriteCentered(left, top + 4, width,
            "T: varázshasználó taktikája   Esc: mentés és vissza", ConsoleColor.DarkYellow);
        WriteCentered(left, top + 6, width, "HALADÁSI IRÁNY", ConsoleColor.Cyan);
        WriteCentered(left, top + 7, width, "▲", ConsoleColor.Cyan);

        const int slotWidth = 43;
        var slotsLeft = left + Math.Max(2, (width - slotWidth * 2 - 3) / 2);
        for (var row = 0; row < 2; row++)
        {
            for (var column = 0; column < 2; column++)
            {
                var index = row * 2 + column;
                var character = slots[index] is { } id ? party.FirstOrDefault(member => member.Id == id) : null;
                var selected = index == cursor;
                var slotLeft = slotsLeft + column * (slotWidth + 3);
                var slotTop = top + 9 + row * 2;
                var positionName = $"{(row == 0 ? "ELSŐ" : "HÁTSÓ")} {(column == 0 ? "BAL" : "JOBB")}";
                Write(slotLeft, slotTop, selected ? "▶ " : index == pickedUp ? "◆ " : "  ",
                    selected ? ConsoleColor.Yellow : ConsoleColor.Cyan);
                Write(slotLeft + 2, slotTop, $"{positionName,-12}: ", ConsoleColor.Gray);
                Write(slotLeft + 17, slotTop, character?.Name ?? "— üres —",
                    character?.Color ?? ConsoleColor.DarkGray);
            }
        }
        if (slots[cursor] is { } selectedId &&
            party.FirstOrDefault(member => member.Id == selectedId) is { IsSpellcaster: true })
        {
            var character = party.First(member => member.Id == selectedId);
            var value = tactics.GetValueOrDefault(selectedId,
                NpcSpellcasterTactics.DefaultFor(character.CharacterClass.Id));
            var detail = $"Varázstaktika: {value.OffensiveSpellsPerBattle}/csata, erő {value.MinimumEnemyStrength}–{value.FullOffenseEnemyStrength}, " +
                (value.ManaFallback == SpellcasterManaFallback.Retreat ? "hátravonulás" : "önbuff + közelharc");
            WriteCentered(left, top + 14, width, detail, ConsoleColor.Cyan);
            var unholy = value.UnholyProfile ?? NpcSpellcasterTactics.DefaultFor(
                character.CharacterClass.Id).UnholyProfile;
            if (character.CharacterClass.Id == CharacterClassIds.Pap && unholy is not null)
                WriteCentered(left, top + 15, width,
                    $"Élőholt/démon: {unholy.OffensiveSpellsPerBattle}/csata, erő " +
                    $"{unholy.MinimumEnemyStrength}–{unholy.FullOffenseEnemyStrength}, önbuff + közelharc",
                    ConsoleColor.DarkCyan);
        }
        Console.ResetColor();
    }

    private static void DrawEmptyWindow(int left, int top, int width, int height)
    {
        var style = WindowFrameConfiguration.For(FramedWindow.FormationEditor);
        var contentRows = Math.Max(0, height - 2);
        Write(left, top, WindowFrameCatalog.Horizontal(style, width), ConsoleColor.DarkYellow);
        for (var row = 0; row < contentRows; row++)
        {
            var sides = WindowFrameCatalog.Sides(style, row, contentRows);
            Write(left, top + row + 1, sides.Left, ConsoleColor.DarkYellow);
            Write(left + sides.Left.Length, top + row + 1,
                new string(' ', Math.Max(0, width - sides.Left.Length - sides.Right.Length)), ConsoleColor.Gray);
            Write(left + width - sides.Right.Length, top + row + 1, sides.Right, ConsoleColor.DarkYellow);
        }
        Write(left, top + height - 1, WindowFrameCatalog.Horizontal(style, width, true), ConsoleColor.DarkYellow);
    }

    private static void WriteCentered(int left, int top, int width, string text, ConsoleColor color)
    {
        var textWidth = BattleCommandPanel.DisplayWidth(text);
        Write(left + Math.Max(0, (width - textWidth) / 2), top, text, color);
    }

    private static void Write(int left, int top, string text, ConsoleColor color,
        ConsoleColor background = ConsoleColor.Black)
    {
        if (left < 0 || top < 0 || left >= Console.WindowWidth || top >= Console.WindowHeight) return;
        Console.SetCursorPosition(left, top);
        Console.ForegroundColor = color;
        Console.BackgroundColor = background;
        Console.Write(BattleCommandPanel.TruncateToDisplayWidth(text, Console.WindowWidth - left));
    }

    private static NpcSpellcasterTactics EditSpellcasterTactics(LiveCharacter character,
        NpcSpellcasterTactics initial, int left, int top, int width, int height,
        Action<int, IReadOnlyList<(string Text, ConsoleColor Color)>, FramedWindow?>? presentationChanged)
    {
        var value = initial.Normalize();
        if (character.CharacterClass.Id == CharacterClassIds.Pap && value.UnholyProfile is null)
            value = value with { UnholyProfile = NpcSpellcasterTactics.DefaultFor(
                CharacterClassIds.Pap).UnholyProfile };
        var row = 0;
        while (true)
        {
            var lines = new[]
            {
                $"Támadó varázslatok csatánként: {value.OffensiveSpellsPerBattle}",
                $"Ez alatt az ellenfél-összerő alatt nem varázsol: {value.MinimumEnemyStrength}",
                $"Ettől az összerőtől teljes támadás: {value.FullOffenseEnemyStrength}",
                "Mana elfogyásakor: " + (value.ManaFallback == SpellcasterManaFallback.Retreat
                    ? "hátravonulás" : "önbuff és közelharc")
            };
            presentationChanged?.Invoke(width,
                BuildProfilePresentation(character, "VARÁZSHASZNÁLÓ TAKTIKA", lines, row,
                    character.CharacterClass.Id == CharacterClassIds.Pap
                        ? "U: élőholt/démon profil szerkesztése" : null),
                FramedWindow.FormationEditor);
            DrawProfileEditor(character, "VARÁZSHASZNÁLÓ TAKTIKA", lines, row, left, top, width, height,
                character.CharacterClass.Id == CharacterClassIds.Pap
                    ? "U: élőholt/démon profil szerkesztése" : null);
            var key = Console.ReadKey(intercept: true).Key;
            if (key is ConsoleKey.Enter or ConsoleKey.Escape) return value.Normalize();
            if (key == ConsoleKey.U && character.CharacterClass.Id == CharacterClassIds.Pap)
            {
                var profile = value.UnholyProfile ??
                    NpcSpellcasterTactics.DefaultFor(CharacterClassIds.Pap).UnholyProfile!;
                value = value with { UnholyProfile = EditUnholyProfile(character, profile,
                    left, top, width, height, presentationChanged) };
                continue;
            }
            if (key == ConsoleKey.UpArrow) row = Math.Max(0, row - 1);
            if (key == ConsoleKey.DownArrow) row = Math.Min(3, row + 1);
            var delta = key is ConsoleKey.RightArrow or ConsoleKey.Add or ConsoleKey.OemPlus ? 1 :
                key is ConsoleKey.LeftArrow or ConsoleKey.Subtract or ConsoleKey.OemMinus ? -1 : 0;
            if (delta == 0) continue;
            value = row switch
            {
                0 => value with { OffensiveSpellsPerBattle = value.OffensiveSpellsPerBattle + delta },
                1 => value with { MinimumEnemyStrength = value.MinimumEnemyStrength + delta },
                2 => value with { FullOffenseEnemyStrength = value.FullOffenseEnemyStrength + delta },
                _ => value with { ManaFallback = value.ManaFallback == SpellcasterManaFallback.Retreat
                    ? SpellcasterManaFallback.SelfBuffAndMelee : SpellcasterManaFallback.Retreat }
            };
            value = value.Normalize();
        }
    }

    private static NpcSpellcasterCombatProfile EditUnholyProfile(LiveCharacter character,
        NpcSpellcasterCombatProfile initial, int left, int top, int width, int height,
        Action<int, IReadOnlyList<(string Text, ConsoleColor Color)>, FramedWindow?>? presentationChanged)
    {
        var value = initial.Normalize();
        var row = 0;
        while (true)
        {
            var lines = new[]
            {
                $"Támadó varázslatok csatánként: {value.OffensiveSpellsPerBattle}",
                $"Alsó ellenfél-összerő: {value.MinimumEnemyStrength}",
                $"Teljes támadás összereje: {value.FullOffenseEnemyStrength}",
                "Mana elfogyásakor: " + (value.ManaFallback == SpellcasterManaFallback.Retreat
                    ? "hátravonulás" : "önbuff és közelharc")
            };
            presentationChanged?.Invoke(width,
                BuildProfilePresentation(character, "ÉLŐHOLT/DÉMON PROFIL", lines, row),
                FramedWindow.FormationEditor);
            DrawProfileEditor(character, "ÉLŐHOLT/DÉMON PROFIL", lines, row, left, top, width, height);
            var key = Console.ReadKey(intercept: true).Key;
            if (key is ConsoleKey.Enter or ConsoleKey.Escape) return value.Normalize();
            if (key == ConsoleKey.UpArrow) row = Math.Max(0, row - 1);
            if (key == ConsoleKey.DownArrow) row = Math.Min(3, row + 1);
            var delta = key is ConsoleKey.RightArrow or ConsoleKey.Add or ConsoleKey.OemPlus ? 1 :
                key is ConsoleKey.LeftArrow or ConsoleKey.Subtract or ConsoleKey.OemMinus ? -1 : 0;
            if (delta == 0) continue;
            value = row switch
            {
                0 => value with { OffensiveSpellsPerBattle = value.OffensiveSpellsPerBattle + delta },
                1 => value with { MinimumEnemyStrength = value.MinimumEnemyStrength + delta },
                2 => value with { FullOffenseEnemyStrength = value.FullOffenseEnemyStrength + delta },
                _ => value with { ManaFallback = value.ManaFallback == SpellcasterManaFallback.Retreat
                    ? SpellcasterManaFallback.SelfBuffAndMelee : SpellcasterManaFallback.Retreat }
            };
            value = value.Normalize();
        }
    }

    private static IReadOnlyList<(string Text, ConsoleColor Color)> BuildProfilePresentation(
        LiveCharacter character, string title, IReadOnlyList<string> values, int selectedRow,
        string? extraCommand = null)
    {
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            ($"✦  {title}  ✦", ConsoleColor.Yellow),
            (string.Empty, ConsoleColor.Gray),
            (character.Name, character.Color),
            (string.Empty, ConsoleColor.Gray)
        };
        lines.AddRange(values.Select((value, index) =>
            ($"{(index == selectedRow ? "▶" : " ")} {value}",
                index == selectedRow ? ConsoleColor.White : ConsoleColor.Gray)));
        if (!string.IsNullOrWhiteSpace(extraCommand))
        {
            lines.Add((string.Empty, ConsoleColor.Gray));
            lines.Add((extraCommand, ConsoleColor.Cyan));
        }
        lines.Add((string.Empty, ConsoleColor.Gray));
        lines.Add(("A host módosítja az értékeket — read-only nézet.", ConsoleColor.DarkYellow));
        return lines;
    }

    private static void DrawProfileEditor(LiveCharacter character, string title, IReadOnlyList<string> lines,
        int selectedRow, int left, int top, int width, int height, string? extraCommand = null)
    {
        DrawEmptyWindow(left, top, width, height);
        WriteCentered(left, top + 1, width, $"✦  {title}  ✦", ConsoleColor.Yellow);
        WriteCentered(left, top + 3, width, character.Name, character.Color);
        var lineWidth = Math.Min(82, Math.Max(10, width - 16));
        var lineLeft = left + Math.Max(1, (width - lineWidth) / 2);
        for (var index = 0; index < lines.Count; index++)
        {
            var selected = index == selectedRow;
            var text = (selected ? "▶ " : "  ") + lines[index];
            Write(lineLeft, top + 6 + index * 2, text.PadRight(lineWidth),
                selected ? ConsoleColor.White : ConsoleColor.Gray,
                selected ? ConsoleColor.DarkCyan : ConsoleColor.Black);
        }
        WriteCentered(left, top + 14, width,
            "↑/↓: sor   ←/→ vagy +/−: érték   Enter/Esc: kész", ConsoleColor.DarkYellow);
        if (!string.IsNullOrEmpty(extraCommand))
            WriteCentered(left, top + 15, width, extraCommand, ConsoleColor.Cyan);
        Console.ResetColor();
    }
}
