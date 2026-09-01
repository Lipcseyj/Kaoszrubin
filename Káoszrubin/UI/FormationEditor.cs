using KaoszRubin.Domain.Characters;

namespace KaoszRubin.UI;

public static class FormationEditor
{
    public static IReadOnlyList<CharacterId?> Edit(IReadOnlyList<LiveCharacter> party,
        PartyFormationSnapshot formation)
    {
        var slots = formation.Slots.ToArray();
        var cursor = 0;
        int? pickedUp = null;
        while (true)
        {
            Draw(party, slots, cursor, pickedUp);
            var key = Console.ReadKey(intercept: true).Key;
            if (key == ConsoleKey.Escape) return slots;
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
        int cursor, int? pickedUp)
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("ALAKZATSZERKESZTO");
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine("A nyilakkal valassz helyet, Enterrel emelj fel es cserelj karaktert.");
        Console.WriteLine("Esc: alakzat mentese es vissza a jatekba\n");
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
    }
}
