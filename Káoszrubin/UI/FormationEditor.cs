using KaoszRubin.Domain.Characters;

namespace KaoszRubin.UI;

public static class FormationEditor
{
    public sealed record Result(IReadOnlyList<CharacterId?> Slots,
        IReadOnlyDictionary<CharacterId, NpcSpellcasterTactics> SpellcasterTactics);

    public static Result Edit(IReadOnlyList<LiveCharacter> party, PartyFormationSnapshot formation,
        IReadOnlyDictionary<CharacterId, NpcSpellcasterTactics> currentTactics)
    {
        var slots = formation.Slots.ToArray();
        var tactics = currentTactics.ToDictionary(pair => pair.Key, pair => pair.Value);
        var cursor = 0;
        int? pickedUp = null;
        while (true)
        {
            Draw(party, slots, cursor, pickedUp, tactics);
            var key = Console.ReadKey(intercept: true).Key;
            if (key == ConsoleKey.Escape) return new Result(slots, tactics);
            if (key == ConsoleKey.T && slots[cursor] is { } tacticsId &&
                party.FirstOrDefault(member => member.Id == tacticsId) is { IsSpellcaster: true } spellcaster)
            {
                tactics[tacticsId] = EditSpellcasterTactics(spellcaster,
                    tactics.GetValueOrDefault(tacticsId,
                        NpcSpellcasterTactics.DefaultFor(spellcaster.CharacterClass.Id)));
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

    private static void Draw(IReadOnlyList<LiveCharacter> party, IReadOnlyList<CharacterId?> slots,
        int cursor, int? pickedUp, IReadOnlyDictionary<CharacterId, NpcSpellcasterTactics> tactics)
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("ALAKZATSZERKESZTO");
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine("A nyilakkal valassz helyet, Enterrel emelj fel es cserelj karaktert.");
        Console.WriteLine("T: kijelolt varazshasznalo taktikaja   Esc: mentes es vissza\n");
        Console.WriteLine("                 HALADASI IRANY");
        Console.WriteLine("                       ^\n");
        for (var row = 0; row < 2; row++)
        {
            for (var column = 0; column < 2; column++)
            {
                var index = row * 2 + column;
                var character = slots[index] is { } id ? party.FirstOrDefault(member => member.Id == id) : null;
                var label = character?.Name ?? "ures";
                var selected = index == cursor;
                Console.ForegroundColor = selected ? ConsoleColor.Black :
                    index == pickedUp ? ConsoleColor.Yellow : ConsoleColor.Gray;
                Console.BackgroundColor = selected ? ConsoleColor.DarkCyan : ConsoleColor.Black;
                Console.Write($"  {(row == 0 ? "ELSO" : "HATSO")} {(column == 0 ? "BAL" : "JOBB"),-5}: {label,-20}  ");
                Console.ResetColor();
            }
            Console.WriteLine("\n");
        }
        if (slots[cursor] is { } selectedId &&
            party.FirstOrDefault(member => member.Id == selectedId) is { IsSpellcaster: true })
        {
            var character = party.First(member => member.Id == selectedId);
            var value = tactics.GetValueOrDefault(selectedId,
                NpcSpellcasterTactics.DefaultFor(character.CharacterClass.Id));
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Varazstaktika: {value.OffensiveSpellsPerBattle}/csata, ero {value.MinimumEnemyStrength}-{value.FullOffenseEnemyStrength}, " +
                (value.ManaFallback == SpellcasterManaFallback.Retreat ? "hatra" : "onbuff+kozelharc"));
            var unholy = value.UnholyProfile ?? NpcSpellcasterTactics.DefaultFor(
                character.CharacterClass.Id).UnholyProfile;
            if (character.CharacterClass.Id == CharacterClassIds.Pap && unholy is not null)
                Console.WriteLine($"Elholt/demon: {unholy.OffensiveSpellsPerBattle}/csata, ero " +
                    $"{unholy.MinimumEnemyStrength}-{unholy.FullOffenseEnemyStrength}, onbuff+kozelharc");
            Console.ResetColor();
        }
    }

    private static NpcSpellcasterTactics EditSpellcasterTactics(LiveCharacter character,
        NpcSpellcasterTactics initial)
    {
        var value = initial.Normalize();
        if (character.CharacterClass.Id == CharacterClassIds.Pap && value.UnholyProfile is null)
            value = value with { UnholyProfile = NpcSpellcasterTactics.DefaultFor(
                CharacterClassIds.Pap).UnholyProfile };
        var row = 0;
        while (true)
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{character.Name.ToUpperInvariant()} - VARAZSHASZNALO TAKTIKA\n");
            var lines = new[]
            {
                $"Tamado varazslatok csatankent: {value.OffensiveSpellsPerBattle}",
                $"Ez alatt az ellenfel osszero alatt nem varazsol: {value.MinimumEnemyStrength}",
                $"Ettol az osszerotol teljes tamadas: {value.FullOffenseEnemyStrength}",
                "Mana elfogyasakor: " + (value.ManaFallback == SpellcasterManaFallback.Retreat
                    ? "hatravonulas" : "onbuff es kozelharc")
            };
            for (var index = 0; index < lines.Length; index++)
            {
                Console.ForegroundColor = index == row ? ConsoleColor.Black : ConsoleColor.Gray;
                Console.BackgroundColor = index == row ? ConsoleColor.DarkCyan : ConsoleColor.Black;
                Console.WriteLine("  " + lines[index].PadRight(76));
                Console.ResetColor();
            }
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("\nFel/le: sor   Bal/jobb vagy +/-: ertek   Enter/Esc: kesz");
            if (character.CharacterClass.Id == CharacterClassIds.Pap)
                Console.WriteLine("U: eloholt/demon profil szerkesztese");
            var key = Console.ReadKey(intercept: true).Key;
            if (key is ConsoleKey.Enter or ConsoleKey.Escape) return value.Normalize();
            if (key == ConsoleKey.U && character.CharacterClass.Id == CharacterClassIds.Pap)
            {
                var profile = value.UnholyProfile ??
                    NpcSpellcasterTactics.DefaultFor(CharacterClassIds.Pap).UnholyProfile!;
                value = value with { UnholyProfile = EditUnholyProfile(character, profile) };
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
        NpcSpellcasterCombatProfile initial)
    {
        var value = initial.Normalize();
        var row = 0;
        while (true)
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{character.Name.ToUpperInvariant()} - ELOHOLT/DEMON PROFIL\n");
            var lines = new[]
            {
                $"Tamado varazslatok csatankent: {value.OffensiveSpellsPerBattle}",
                $"Also ellenfel-osszero: {value.MinimumEnemyStrength}",
                $"Teljes tamadas osszereje: {value.FullOffenseEnemyStrength}",
                "Mana elfogyasakor: " + (value.ManaFallback == SpellcasterManaFallback.Retreat
                    ? "hatravonulas" : "onbuff es kozelharc")
            };
            for (var index = 0; index < lines.Length; index++)
            {
                Console.ForegroundColor = index == row ? ConsoleColor.Black : ConsoleColor.Gray;
                Console.BackgroundColor = index == row ? ConsoleColor.DarkCyan : ConsoleColor.Black;
                Console.WriteLine("  " + lines[index].PadRight(76));
                Console.ResetColor();
            }
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("\nFel/le: sor   Bal/jobb vagy +/-: ertek   Enter/Esc: kesz");
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
}
