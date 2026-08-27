using MazeGame.Data;
using MazeGame.Domain.Characters;

namespace MazeGame.UI;

public sealed class CharacterCreationScreen
{
    private const int AbilityPointTotal = 25;
    private const int FramePreferredWidth = 84;
    private static int _frameLeft;
    private static int _frameWidth;
    private readonly GameDataCatalog _gameData;
    private readonly CharacterRoster _characterRoster;
    private readonly Random _random = new();

    public CharacterCreationScreen(GameDataCatalog gameData, CharacterRoster characterRoster)
    {
        _gameData = gameData;
        _characterRoster = characterRoster;
    }

    public void Run()
    {
        var name = ReadName();
        if (name is null) return;
        var race = ChooseRace();
        if (race is null) return;
        var adaptableAbilityBonus = ChooseAdaptableAbilityBonus(race);
        if (adaptableAbilityBonus is null) return;

        while (true)
        {
            PrimaryAbilities rolledAbilities;
            PrimaryAbilities finalAbilities;
            List<CharacterClassDefinition> eligibleClasses;
            do
            {
                rolledAbilities = RollAbilities();
                finalAbilities = (rolledAbilities + race.AbilityBonuses + adaptableAbilityBonus.Value).Clamp(1, 13);
                eligibleClasses = EligibleClasses(finalAbilities);
            } while (eligibleClasses.Count == 0);

            var vitalityBonus = _random.Next(1, 16);
            var manaBonus = _random.Next(1, 16);

            DrawAbilityRoll(name, race, adaptableAbilityBonus.Value, rolledAbilities, finalAbilities,
                vitalityBonus, manaBonus, eligibleClasses);
            var key = Console.ReadKey(intercept: true).Key;
            if (key == ConsoleKey.Escape) return;
            if (key == ConsoleKey.R) continue;
            if (key != ConsoleKey.Enter) continue;

            var characterClass = ChooseClass(finalAbilities, eligibleClasses);
            if (characterClass is null) continue;

            var color = ChooseColor(characterClass);
            if (color is null) continue;

            var character = LiveCharacterFactory.Create(name, race, characterClass, rolledAbilities,
                vitalityBonus, manaBonus, _gameData, color.Value, adaptableAbilityBonus.Value);
            if (character.IsSpellcaster) ChooseStartingSpells(character);
            _characterRoster.Add(character);
            ShowCreatedCharacter(character);
            return;
        }
    }

    /// <summary>Interakció nélkül elkészíti az első fajhoz tartozó, első elérhető osztályú érvényes karaktert.</summary>
    public LiveCharacter CreateFirstValidCharacter(string name)
        => CreateFirstValidCharacter(_ => name);

    /// <summary>Interakció nélkül készít karaktert, majd az érvényes osztály alapján kéri le a nevét.</summary>
    public LiveCharacter CreateFirstValidCharacter(Func<CharacterClassDefinition, string> nameFactory)
    {
        foreach (var race in _gameData.Races)
        {
            for (var attempt = 0; attempt < 1_000; attempt++)
            {
                var adaptableAbilityBonus = RandomAdaptableAbilityBonus(race);
                var rolledAbilities = RollAbilities();
                var finalAbilities = (rolledAbilities + race.AbilityBonuses + adaptableAbilityBonus).Clamp(1, 13);
                var characterClass = _gameData.CharacterClasses.FirstOrDefault(candidate => finalAbilities.MeetsMinimum(candidate.MinimumAbilities));
                if (characterClass is null) continue;

                var character = LiveCharacterFactory.Create(nameFactory(characterClass), race, characterClass, rolledAbilities,
                    _random.Next(1, 16), _random.Next(1, 16), _gameData, RandomCharacterColor(), adaptableAbilityBonus);
                SpellcastingRules.GiveAutomaticStartingSpells(character, _gameData, _random);
                return character;
            }
        }

        throw new InvalidOperationException("A jelenlegi faj- és osztályadatokból nem generálható érvényes karakter.");
    }

    private ConsoleColor? ChooseColor(CharacterClassDefinition characterClass)
    {
        var colors = CharacterColors.Selectable;
        var selectedIndex = 0;
        while (true)
        {
            DrawFrame("🎨 KARAKTERSZÍN", Math.Max(16, colors.Count + 6));
            WriteInside(3, "↑/↓ választás   Enter elfogadás   Esc vissza", ConsoleColor.DarkCyan);
            for (var index = 0; index < colors.Count; index++)
            {
                WriteInside(5 + index,
                    $"{(index == selectedIndex ? "▶" : " ")} ● {CharacterColors.NameOf(colors[index])}", colors[index]);
            }
            DrawClassPortrait(characterClass, colors[selectedIndex]);
            Console.ResetColor();
            switch (Console.ReadKey(intercept: true).Key)
            {
                case ConsoleKey.UpArrow:
                    selectedIndex = (selectedIndex - 1 + colors.Count) % colors.Count;
                    break;
                case ConsoleKey.DownArrow:
                    selectedIndex = (selectedIndex + 1) % colors.Count;
                    break;
                case ConsoleKey.Enter:
                    return colors[selectedIndex];
                case ConsoleKey.Escape:
                    return null;
            }
        }
    }

    private ConsoleColor RandomCharacterColor() => CharacterColors.Selectable[_random.Next(CharacterColors.Selectable.Count)];

    private void ChooseStartingSpells(LiveCharacter character)
    {
        SpellcastingRules.TryGetSchool(character.CharacterClass.Id, out var school);
        var spells = _gameData.GetSpells(school, 1).OrderBy(spell => spell.Name).ToList();
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cursor = 0;
        while (true)
        {
            DrawFrame("🔮 KEZDŐ VARÁZSLATOK", Math.Max(18, spells.Count + 9));
            WriteInside(3, $"Válassz pontosan {SpellcastingRules.StartingSpellCount(character.CharacterClass.Id)} varázslatot.", ConsoleColor.Cyan);
            WriteInside(4, "↑/↓ mozgás   Space kijelölés   Enter elfogadás", ConsoleColor.DarkCyan);
            for (var index = 0; index < spells.Count; index++)
                WriteInside(6 + index,
                    $"{(index == cursor ? "▶" : " ")} [{(selected.Contains(spells[index].Id) ? "✨" : " ")}] {spells[index].Name}",
                    selected.Contains(spells[index].Id) ? ConsoleColor.Magenta : ConsoleColor.Gray);
            WriteInside(7 + spells.Count,
                $"📖 Kijelölve: {selected.Count}/{SpellcastingRules.StartingSpellCount(character.CharacterClass.Id)}", ConsoleColor.Yellow);
            DrawClassPortrait(character.CharacterClass, character.Color);
            switch (Console.ReadKey(intercept: true).Key)
            {
                case ConsoleKey.UpArrow: cursor = (cursor - 1 + spells.Count) % spells.Count; break;
                case ConsoleKey.DownArrow: cursor = (cursor + 1) % spells.Count; break;
                case ConsoleKey.Spacebar:
                    if (!selected.Remove(spells[cursor].Id) && selected.Count < SpellcastingRules.StartingSpellCount(character.CharacterClass.Id))
                        selected.Add(spells[cursor].Id);
                    break;
                case ConsoleKey.Enter when selected.Count == SpellcastingRules.StartingSpellCount(character.CharacterClass.Id):
                    var chosen = spells.Where(spell => selected.Contains(spell.Id)).ToList();
                    foreach (var spell in chosen) character.LearnSpell(spell);
                    character.SetMemorizedSpells(chosen);
                    return;
            }
        }
    }

    private string? ReadName()
    {
        while (true)
        {
            DrawFrame("⚔ KARAKTERGENERÁLÁS ⚔", 12);
            WriteInside(3, "🪶 Add meg a hős nevét", ConsoleColor.Yellow);
            WriteInside(5, $"Legfeljebb {LiveCharacter.MaximumNameLength} karakter · üresen: vissza", ConsoleColor.DarkGray);
            WriteInside(7, "Név: ", ConsoleColor.Cyan);
            Console.SetCursorPosition(_frameLeft + 10, 7);
            Console.ForegroundColor = ConsoleColor.White;
            var name = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(name)) return null;
            if (name.Length <= LiveCharacter.MaximumNameLength) return name;

            WriteInside(9, $"⚠ A név túl hosszú. Maximum {LiveCharacter.MaximumNameLength} karakter.", ConsoleColor.Red);
            Console.ReadKey(intercept: true);
        }
    }

    private RaceDefinition? ChooseRace()
    {
        while (true)
        {
            DrawFrame("🧬 FAJ VÁLASZTÁSA", 16);
            WriteInside(3, "Válassz származást az 1–4 billentyűkkel", ConsoleColor.DarkCyan);
            for (var index = 0; index < _gameData.Races.Count; index++)
            {
                var race = _gameData.Races[index];
                WriteInside(5 + index * 2, $"{index + 1}  {RaceIcon(race)} {race.Name}  {FormatAbilities(race.AbilityBonuses)}", RaceColor(race));
                WriteInside(6 + index * 2, $"   {FormatRaceTraits(race)}", ConsoleColor.DarkGray);
            }
            WriteInside(14, "Esc · vissza", ConsoleColor.DarkCyan);

            var key = Console.ReadKey(intercept: true).Key;
            if (key == ConsoleKey.Escape) return null;
            if (TryGetNumberKey(key, out var choice) && choice <= _gameData.Races.Count) return _gameData.Races[choice - 1];
        }
    }

    private List<CharacterClassDefinition> EligibleClasses(PrimaryAbilities abilities) =>
        _gameData.CharacterClasses.Where(characterClass =>
            abilities.MeetsMinimum(characterClass.MinimumAbilities)).ToList();

    private CharacterClassDefinition? ChooseClass(PrimaryAbilities abilities,
        IReadOnlyList<CharacterClassDefinition> eligibleClasses)
    {
        var selectedIndex = 0;
        while (true)
        {
            DrawFrame("🛡 OSZTÁLY VÁLASZTÁSA", Math.Max(17, eligibleClasses.Count + 12));
            WriteInside(3, $"Végső képességek: {FormatAbilities(abilities)}", ConsoleColor.Yellow);
            for (var index = 0; index < eligibleClasses.Count; index++)
            {
                var characterClass = eligibleClasses[index];
                WriteInside(5 + index, $"{(index == selectedIndex ? "▶" : " ")} {ClassIcon(characterClass)} {characterClass.Name}",
                    index == selectedIndex ? ConsoleColor.Cyan : ConsoleColor.Gray);
                if (index == selectedIndex)
                    WriteInside(7 + eligibleClasses.Count, $"Minimum: {FormatAbilities(characterClass.MinimumAbilities)}", ConsoleColor.DarkGray);
            }
            WriteInside(9 + eligibleClasses.Count, "↑/↓ választás   Enter elfogadás   R újradobás   Esc kilépés", ConsoleColor.DarkCyan);
            DrawClassPortrait(eligibleClasses[selectedIndex], ConsoleColor.Cyan);

            var key = Console.ReadKey(intercept: true).Key;
            if (key is ConsoleKey.R or ConsoleKey.Escape) return null;
            if (key == ConsoleKey.UpArrow) selectedIndex = (selectedIndex - 1 + eligibleClasses.Count) % eligibleClasses.Count;
            else if (key == ConsoleKey.DownArrow) selectedIndex = (selectedIndex + 1) % eligibleClasses.Count;
            else if (key == ConsoleKey.Enter) return eligibleClasses[selectedIndex];
        }
    }

    private static PrimaryAbilities? ChooseAdaptableAbilityBonus(RaceDefinition race)
    {
        if (!race.HasTrait(RaceTraits.Adaptable)) return PrimaryAbilities.Zero;
        while (true)
        {
            DrawFrame("🌟 ALKALMAZKODÓ", 14);
            WriteInside(3, "Válassz egy képességet, amely +1 bónuszt kap:", ConsoleColor.Yellow);
            WriteInside(5, "1  💪 Erő", ConsoleColor.Red);
            WriteInside(6, "2  🏹 Ügyesség", ConsoleColor.Green);
            WriteInside(7, "3  ❤️ Egészség", ConsoleColor.DarkYellow);
            WriteInside(8, "4  🧠 Intelligencia", ConsoleColor.Magenta);
            WriteInside(11, "Esc · vissza", ConsoleColor.DarkCyan);
            var key = Console.ReadKey(intercept: true).Key;
            if (key == ConsoleKey.Escape) return null;
            if (!TryGetNumberKey(key, out var choice) || choice > 4) continue;
            return choice switch
            {
                1 => new PrimaryAbilities(1, 0, 0, 0),
                2 => new PrimaryAbilities(0, 1, 0, 0),
                3 => new PrimaryAbilities(0, 0, 1, 0),
                _ => new PrimaryAbilities(0, 0, 0, 1)
            };
        }
    }

    private PrimaryAbilities RandomAdaptableAbilityBonus(RaceDefinition race)
    {
        if (!race.HasTrait(RaceTraits.Adaptable)) return PrimaryAbilities.Zero;
        return _random.Next(4) switch
        {
            0 => new PrimaryAbilities(1, 0, 0, 0),
            1 => new PrimaryAbilities(0, 1, 0, 0),
            2 => new PrimaryAbilities(0, 0, 1, 0),
            _ => new PrimaryAbilities(0, 0, 0, 1)
        };
    }

    private void DrawAbilityRoll(string name, RaceDefinition race, PrimaryAbilities adaptableAbilityBonus,
        PrimaryAbilities rolled, PrimaryAbilities final,
        int vitalityBonus, int manaBonus, IReadOnlyList<CharacterClassDefinition> eligibleClasses)
    {
        DrawFrame("🎲 KÉPESSÉGDOBÁS", 18);
        WriteInside(3, $"⚔ {name}  ·  {RaceIcon(race)} {race.Name}", ConsoleColor.Yellow);
        var rolledPointTotal = rolled.Strength + rolled.Dexterity + rolled.Health + rolled.Intelligence;
        WriteInside(5, $"🎲 Dobás ({rolledPointTotal} pont): {FormatAbilities(rolled)}", ConsoleColor.Gray);
        WriteInside(6, $"🧬 Faji módosító:      {FormatAbilities(race.AbilityBonuses)}", RaceColor(race));
        if (race.HasTrait(RaceTraits.Adaptable))
            WriteInside(7, $"🌟 Választott bónusz:  {FormatAbilities(adaptableAbilityBonus)}", ConsoleColor.Cyan);
        WriteInside(8, $"✨ Végső értékek:      {FormatAbilities(final)}", ConsoleColor.White);
        WriteInside(10, $"🏷 Osztályok: {string.Join(", ", eligibleClasses.Select(characterClass => characterClass.Name))}", ConsoleColor.Green);
        WriteInside(12, $"❤️ Életerő: {_gameData.GetMinimumVitality(final.Health)} + {vitalityBonus} = {_gameData.GetMinimumVitality(final.Health) + vitalityBonus}", ConsoleColor.Red);
        WriteInside(13, $"🔷 Manna:   {_gameData.GetMinimumMana(final.Intelligence)} + {manaBonus} = {_gameData.GetMinimumMana(final.Intelligence) + manaBonus}", ConsoleColor.Blue);
        WriteInside(15, "Enter elfogadás   R újradobás   Esc megszakítás", ConsoleColor.DarkCyan);
    }

    private PrimaryAbilities RollAbilities()
    {
        var values = new[] { 1, 1, 1, 1 };
        var pointTotal = RollAbilityPointTotal();
        for (var remainingPoints = pointTotal - values.Sum(); remainingPoints > 0; remainingPoints--)
        {
            var availableIndices = Enumerable.Range(0, values.Length).Where(index => values[index] < 10).ToArray();
            values[availableIndices[_random.Next(availableIndices.Length)]]++;
        }
        return new PrimaryAbilities(values[0], values[1], values[2], values[3]);
    }

    private int RollAbilityPointTotal()
    {
        var roll = _random.Next(100);
        return AbilityPointTotal + (roll switch
        {
            < 15 => 0,
            < 65 => 1,
            < 90 => 2,
            _ => 3
        });
    }

    private static void ShowCreatedCharacter(LiveCharacter character)
    {
        DrawFrame("✨ A HŐS ELKÉSZÜLT ✨", 16);
        WriteInside(3, $"⚔ {character.Name}", character.Color);
        WriteInside(5, $"{RaceIcon(character.Race)} {character.Race.Name}  ·  {ClassIcon(character.CharacterClass)} {character.CharacterClass.Name}", ConsoleColor.Yellow);
        WriteInside(7, FormatAbilities(character.Abilities), ConsoleColor.White);
        WriteInside(9, $"❤️ HP: {character.CurrentVitality}/{character.MaximumVitality}", ConsoleColor.Red);
        WriteInside(10, $"🔷 Manna: {(character.UsesMana ? $"{character.CurrentMana}/{character.MaximumMana}" : "nincs")}", ConsoleColor.Blue);
        DrawClassPortrait(character.CharacterClass, character.Color);
        WriteInside(13, "Bármely billentyű · vissza a karakterlistához", ConsoleColor.DarkCyan);
        Console.ReadKey(intercept: true);
    }

    private static void DrawClassPortrait(CharacterClassDefinition characterClass, ConsoleColor color)
    {
        var portrait = AsciiPortraits.ForCharacterClass(characterClass.Id);
        var preferredLeft = Math.Max(_frameLeft + 42, _frameLeft + _frameWidth - portrait.CanvasWidth - 4);
        var left = Math.Max(0, Math.Min(preferredLeft, Console.WindowWidth - portrait.CanvasWidth));
        var top = 3;
        var returnLeft = Console.CursorLeft;
        var returnTop = Console.CursorTop;
        Console.ForegroundColor = color;
        for (var index = 0; index < portrait.Lines.Count; index++)
        {
            if (top + index >= Console.WindowHeight) break;
            Console.SetCursorPosition(left, top + index);
            Console.Write(portrait.Lines[index].PadRight(portrait.CanvasWidth));
        }
        Console.ResetColor();
        Console.SetCursorPosition(returnLeft, returnTop);
    }

    private static string FormatRaceTraits(RaceDefinition race) => race.Traits switch
    {
        RaceTraits.Adaptable => "Alkalmazkodó: választott képesség +1, első tehetség a 4. szinten",
        RaceTraits.Resilient => "Rendíthetetlen: 50% mérgezés- és betegség-ellenállás",
        RaceTraits.KeenSenses => "Éles érzékek: +15% keresési esély",
        RaceTraits.Relentless => "Könyörtelen: +2 ajtóbetörés, pályánként egyszer 1 HP-n túlél",
        _ => "nincs különleges tulajdonság"
    };

    private static string RaceIcon(RaceDefinition race) => race.Traits switch
    {
        RaceTraits.Adaptable => "🌟",
        RaceTraits.Resilient => "⛰",
        RaceTraits.KeenSenses => "🌙",
        RaceTraits.Relentless => "🔥",
        _ => "◆"
    };

    private static ConsoleColor RaceColor(RaceDefinition race) => race.Traits switch
    {
        RaceTraits.Adaptable => ConsoleColor.Yellow,
        RaceTraits.Resilient => ConsoleColor.DarkYellow,
        RaceTraits.KeenSenses => ConsoleColor.Cyan,
        RaceTraits.Relentless => ConsoleColor.Red,
        _ => ConsoleColor.White
    };

    private static string ClassIcon(CharacterClassDefinition characterClass) => characterClass.Id switch
    {
        CharacterClassIds.Harcos => "⚔",
        CharacterClassIds.Barbár => "🪓",
        CharacterClassIds.Lovag => "🛡",
        CharacterClassIds.Tolvaj => "🗡",
        CharacterClassIds.Pap => "✝",
        CharacterClassIds.Mágus => "🔮",
        _ => "◆"
    };

    private static string FormatAbilities(PrimaryAbilities abilities) =>
        $"💪 {abilities.Strength}  🏹 {abilities.Dexterity}  ❤️ {abilities.Health}  🧠 {abilities.Intelligence}";

    private static void DrawFrame(string title, int requestedHeight)
    {
        Console.Clear();
        _frameWidth = Math.Max(10, Math.Min(FramePreferredWidth, Console.WindowWidth - 2));
        var height = Math.Max(4, Math.Min(requestedHeight, Console.WindowHeight - 1));
        _frameLeft = Math.Max(0, (Console.WindowWidth - _frameWidth) / 2);
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.SetCursorPosition(_frameLeft, 0);
        Console.Write("@)" + new string('=', _frameWidth - 4) + "(@");
        for (var row = 1; row < height - 1; row++)
        {
            Console.SetCursorPosition(_frameLeft, row);
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.Write(" |");
            Console.SetCursorPosition(_frameLeft + _frameWidth - 2, row);
            Console.Write("| ");
        }
        Console.SetCursorPosition(_frameLeft, height - 1);
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.Write("@)" + new string('=', _frameWidth - 4) + "(@");
        WriteInside(1, title, ConsoleColor.Yellow, centered: true);
        Console.ResetColor();
    }

    private static void WriteInside(int row, string text, ConsoleColor color, bool centered = false)
    {
        if (row < 1 || row >= Console.WindowHeight) return;
        var maximumLength = Math.Max(0, _frameWidth - 8);
        if (text.Length > maximumLength) text = text[..maximumLength];
        var offset = centered ? Math.Max(0, (maximumLength - text.Length) / 2) : 0;
        Console.SetCursorPosition(_frameLeft + 4 + offset, row);
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ResetColor();
    }

    private static bool TryGetNumberKey(ConsoleKey key, out int number)
    {
        number = key switch
        {
            ConsoleKey.D1 or ConsoleKey.NumPad1 => 1,
            ConsoleKey.D2 or ConsoleKey.NumPad2 => 2,
            ConsoleKey.D3 or ConsoleKey.NumPad3 => 3,
            ConsoleKey.D4 or ConsoleKey.NumPad4 => 4,
            ConsoleKey.D5 or ConsoleKey.NumPad5 => 5,
            ConsoleKey.D6 or ConsoleKey.NumPad6 => 6,
            ConsoleKey.D7 or ConsoleKey.NumPad7 => 7,
            ConsoleKey.D8 or ConsoleKey.NumPad8 => 8,
            ConsoleKey.D9 or ConsoleKey.NumPad9 => 9,
            _ => 0
        };
        return number != 0;
    }
}
