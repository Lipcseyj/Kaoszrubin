using MazeGame.Data;
using MazeGame.Domain.Characters;
using MazeGame.Transport.SignalR;
using System.Text.Json;

namespace MazeGame.UI;

/// <summary>A program belépési pontja: karakterek és játék indítása közti választás.</summary>
public sealed class MainMenu
{
    private readonly GameDataCatalog _gameData;
    private CharacterRoster _characterRoster;
    private readonly CharacterSaveService _characterSaveService;
    private readonly GameSaveService _gameSaveService;
    private readonly string _applicationVersion;
    private readonly string _catalogHash;
    private readonly Random _random = new();

    private const int SideMenuWidth = 55; 
    private const int SideMenuLeft = 140;

    // Helpers to measure and pad visible width in console cells (surrogate pairs count as width 2).
    private static int DisplayWidth(string? s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        var width = 0;
        for (var i = 0; i < s.Length; i++)
        {
            var ch = s[i];
            if (char.IsHighSurrogate(ch) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
            {
                width += 2;
                i++; // skip low surrogate
            }
            else
            {
                width += 1;
            }
        }
        return width;
    }

    private static string TruncateByDisplayWidth(string s, int maxWidth)
    {
        if (DisplayWidth(s) <= maxWidth) return s;
        var sb = new System.Text.StringBuilder();
        var width = 0;
        for (var i = 0; i < s.Length; i++)
        {
            var ch = s[i];
            if (char.IsHighSurrogate(ch) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
            {
                if (width + 2 > maxWidth) break;
                sb.Append(ch);
                sb.Append(s[i + 1]);
                width += 2;
                i++;
            }
            else
            {
                if (width + 1 > maxWidth) break;
                sb.Append(ch);
                width += 1;
            }
        }
        return sb.ToString();
    }

    private static string PadRightDisplay(string s, int totalWidth)
    {
        var w = DisplayWidth(s);
        if (w >= totalWidth) return TruncateByDisplayWidth(s, totalWidth);
        return s + new string(' ', totalWidth - w);
    }

    public MainMenu(GameDataCatalog gameData, string characterSavePath, string gameSaveDirectory,
        string applicationVersion, string catalogHash)
    {
        _gameData = gameData;
        _characterSaveService = new CharacterSaveService(characterSavePath, gameData);
        _gameSaveService = new GameSaveService(gameSaveDirectory, _characterSaveService);
        _applicationVersion = applicationVersion;
        _catalogHash = catalogHash;
        _characterRoster = _characterSaveService.Load();
    }

    public void Run()
    {
        while (true)
        {
            DrawMainMenu();

            switch (Console.ReadKey(intercept: true).Key)
            {
                case ConsoleKey.D1:
                case ConsoleKey.NumPad1:
                    StartGame();
                    SaveCharacters();
                    break;
                case ConsoleKey.D2:
                case ConsoleKey.NumPad2:
                    LoadGame();
                    SaveCharacters();
                    break;
                case ConsoleKey.D3:
                case ConsoleKey.NumPad3:
                    QuickStart();
                    SaveCharacters();
                    break;
                case ConsoleKey.D4:
                case ConsoleKey.NumPad4:
                    new CharacterCreationScreen(_gameData, _characterRoster).Run();
                    SaveCharacters();
                    break;
                case ConsoleKey.D5:
                case ConsoleKey.NumPad5:
                    ShowCharacters();
                    SaveCharacters();
                    break;
                case ConsoleKey.D6:
                case ConsoleKey.NumPad6:
                    DeleteCharacter();
                    SaveCharacters();
                    break;
                case ConsoleKey.D7:
                case ConsoleKey.NumPad7:
                    ShowHelp();
                    break;
                case ConsoleKey.D8:
                case ConsoleKey.NumPad8:
                    StartHostedGame();
                    SaveCharacters();
                    break;
                case ConsoleKey.D9:
                case ConsoleKey.NumPad9:
                    JoinGame();
                    break;
                case ConsoleKey.Escape:
                    Console.Clear();
                    return;
            }
        }
    }

    private void ShowCharacters()
    {
        if (_characterRoster.Characters.Count == 0)
        {
            ResetConsole();
            Console.WriteLine("Még nincs generált karakter.");
            Console.ReadKey(intercept: true);
            return;
        }

        var selectedIndex = _characterRoster.SelectedCharacter is null
            ? 0
            : Enumerable.Range(0, _characterRoster.Characters.Count)
                .FirstOrDefault(index => _characterRoster.Characters[index] == _characterRoster.SelectedCharacter);
        while (true)
        {
            ResetConsole();
            WriteLine("=== GENERÁLT KARAKTEREK ===", ConsoleColor.Yellow);
            WriteLine("Fel/le: választás | Enter: kijelölés | Esc: vissza", ConsoleColor.DarkCyan);
            Console.WriteLine();
            for (var index = 0; index < _characterRoster.Characters.Count; index++)
            {
                var character = _characterRoster.Characters[index];
                var marker = index == selectedIndex ? ">" : " ";
                var isSelected = character == _characterRoster.SelectedCharacter ? " [aktív]" : string.Empty;
                var deathMarker = character.IsAlive ? string.Empty : " [HALOTT]";
                WriteLine($"{marker} {character.Name} — {character.Race.Name} {character.CharacterClass.Name}{isSelected}{deathMarker}", character.IsAlive ? (index == selectedIndex ? ConsoleColor.Cyan : ConsoleColor.Gray) : ConsoleColor.DarkRed);
                WriteLine($"   HP {character.CurrentVitality}/{character.MaximumVitality}, Manna {(character.UsesMana ? $"{character.CurrentMana}/{character.MaximumMana}" : "nincs")}", character.IsAlive ? ConsoleColor.DarkGray : ConsoleColor.Red);
            }

            switch (Console.ReadKey(intercept: true).Key)
            {
                case ConsoleKey.UpArrow:
                    selectedIndex = (selectedIndex - 1 + _characterRoster.Characters.Count) % _characterRoster.Characters.Count;
                    break;
                case ConsoleKey.DownArrow:
                    selectedIndex = (selectedIndex + 1) % _characterRoster.Characters.Count;
                    break;
                case ConsoleKey.Enter:
                    _characterRoster.Select(_characterRoster.Characters[selectedIndex]);
                    return;
                case ConsoleKey.Escape:
                    return;
            }
        }
    }

    private void StartGame()
    {
        if (_characterRoster.SelectedCharacter is not { } selectedCharacter)
        {
            ResetConsole();
            Console.WriteLine("A játék indításához előbb válassz ki egy karaktert a Karakterek menüben.");
            Console.ReadKey(intercept: true);
            return;
        }

        if (!selectedCharacter.IsAlive)
        {
            ResetConsole();
            WriteLine("Halott karakterrel nem indítható játék. Válassz másik karaktert vagy készíts újat.", ConsoleColor.Red);
            Console.ReadKey(intercept: true);
            return;
        }

        new Game(_gameData, _characterRoster, selectedCharacter, _gameSaveService).Run();
    }

    private void StartHostedGame()
    {
        if (!TryGetPlayableSelectedCharacter(out var selectedCharacter)) return;
        try
        {
            if (_characterRoster.Party.Members.Count == 1)
            {
                var companion = new RandomCharacterGenerator(_gameData, _random).CreateLevelOne(
                    _characterRoster.Characters.Select(character => character.Name).ToArray());
                _characterRoster.Add(companion);
                _characterRoster.Party.Add(companion);
            }
            var game = new Game(_gameData, _characterRoster, selectedCharacter, _gameSaveService);
            var host = CoopHostRuntime.StartAsync(game.Session, _applicationVersion, _catalogHash)
                .GetAwaiter().GetResult();
            try { game.Run(host); }
            finally { host.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or
                                           System.Net.Sockets.SocketException)
        {
            ResetConsole();
            WriteLine($"A coop host nem indítható: {exception.Message}", ConsoleColor.Red);
            Console.ReadKey(intercept: true);
        }
    }

    private void JoinGame()
    {
        ResetConsole();
        WriteLine("=== CSATLAKOZÁS COOP JÁTÉKHOZ ===", ConsoleColor.Yellow);
        Console.Write("Host címe [http://localhost:5127]: ");
        var hostUrl = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(hostUrl)) hostUrl = "http://localhost:5127";
        Console.Write("Játékosnév: ");
        var displayName = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(displayName)) displayName = Environment.UserName;
        try
        {
            new CoopGuestScreen(_applicationVersion, _catalogHash)
                .RunAsync(hostUrl, displayName).GetAwaiter().GetResult();
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or
                                           TimeoutException or HttpRequestException)
        {
            ResetConsole();
            WriteLine($"A csatlakozás sikertelen: {exception.Message}", ConsoleColor.Red);
            Console.ReadKey(intercept: true);
        }
    }

    private bool TryGetPlayableSelectedCharacter(out LiveCharacter selectedCharacter)
    {
        if (_characterRoster.SelectedCharacter is not { } candidate)
        {
            ResetConsole();
            Console.WriteLine("A játék indításához előbb válassz ki egy karaktert a Karakterek menüben.");
            Console.ReadKey(intercept: true);
            selectedCharacter = null!;
            return false;
        }
        if (!candidate.IsAlive)
        {
            ResetConsole();
            WriteLine("Halott karakterrel nem indítható játék. Válassz másik karaktert vagy készíts újat.", ConsoleColor.Red);
            Console.ReadKey(intercept: true);
            selectedCharacter = null!;
            return false;
        }
        selectedCharacter = candidate;
        return true;
    }

    private void LoadGame()
    {
        var saves = _gameSaveService.List();
        if (saves.Count == 0)
        {
            ResetConsole();
            WriteLine("Nincs betölthető játékállás a mentések mappában.", ConsoleColor.DarkYellow);
            Console.ReadKey(intercept: true);
            return;
        }
        var selectedIndex = 0;
        while (true)
        {
            ResetConsole();
            WriteLine("=== JÁTÉK BETÖLTÉSE ===", ConsoleColor.Yellow);
            WriteLine("Fel/le: választás | Enter: betöltés | Esc: vissza", ConsoleColor.DarkCyan);
            Console.WriteLine();
            for (var index = 0; index < saves.Count; index++)
            {
                var save = saves[index];
                var marker = index == selectedIndex ? ">" : " ";
                WriteLine($"{marker} {save.MainCharacterName} — {save.MazeLevel}. pálya — {save.SavedAt:yyyy-MM-dd HH:mm:ss}",
                    index == selectedIndex ? ConsoleColor.Cyan : ConsoleColor.Gray);
            }
            switch (Console.ReadKey(intercept: true).Key)
            {
                case ConsoleKey.UpArrow:
                    selectedIndex = (selectedIndex - 1 + saves.Count) % saves.Count;
                    break;
                case ConsoleKey.DownArrow:
                    selectedIndex = (selectedIndex + 1) % saves.Count;
                    break;
                case ConsoleKey.Enter:
                    try
                    {
                        var loaded = _gameSaveService.Load(saves[selectedIndex].Path);
                        _characterRoster = loaded.Roster;
                        new Game(_gameData, _characterRoster, _characterRoster.SelectedCharacter!, _gameSaveService, loaded.State).Run();
                        return;
                    }
                    catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException)
                    {
                        ResetConsole();
                        WriteLine($"A mentés nem tölthető be: {exception.Message}", ConsoleColor.Red);
                        Console.ReadKey(intercept: true);
                        return;
                    }
                case ConsoleKey.Escape:
                    return;
            }
        }
    }

    private void QuickStart()
    {
        try
        {
            var character = new CharacterCreationScreen(_gameData, _characterRoster).CreateFirstValidCharacter(ChooseQuickStartName);
            _characterRoster.Add(character);
            _characterRoster.Select(character);
            SaveCharacters();
            StartGame();
        }
        catch (InvalidOperationException exception)
        {
            ResetConsole();
            WriteLine(exception.Message, ConsoleColor.Red);
            Console.ReadKey(intercept: true);
        }
    }

    private string ChooseQuickStartName(CharacterClassDefinition characterClass)
    {
        var names = _gameData.GetCharacterNames(characterClass.Id);
        if (names.Count == 0) throw new InvalidOperationException($"Nincs gyorsindításhoz használható név a(z) {characterClass.Name} osztályhoz.");
        var unusedNames = names.Where(candidate => !_characterRoster.Characters.Any(character =>
            string.Equals(character.Name, candidate.Name, StringComparison.OrdinalIgnoreCase))).ToList();
        var candidates = unusedNames.Count > 0 ? unusedNames : names;
        return candidates[_random.Next(candidates.Count)].Name;
    }

    private void DeleteCharacter()
    {
        if (_characterRoster.Characters.Count == 0)
        {
            ResetConsole();
            Console.WriteLine("Nincs törölhető karakter.");
            Console.ReadKey(intercept: true);
            return;
        }

        var selectedIndex = 0;
        while (true)
        {
            ResetConsole();
            WriteLine("=== KARAKTER TÖRLÉSE ===", ConsoleColor.Red);
            WriteLine("Fel/le: választás | Enter: törlés | O: összes törlése | Esc: vissza", ConsoleColor.DarkCyan);
            Console.WriteLine();
            for (var index = 0; index < _characterRoster.Characters.Count; index++)
            {
                var character = _characterRoster.Characters[index];
                var marker = index == selectedIndex ? ">" : " ";
                var deathMarker = character.IsAlive ? string.Empty : " [HALOTT]";
                WriteLine($"{marker} {character.Name} — {character.Race.Name} {character.CharacterClass.Name}{deathMarker}", character.IsAlive ? (index == selectedIndex ? ConsoleColor.Yellow : ConsoleColor.Gray) : ConsoleColor.DarkRed);
            }

            switch (Console.ReadKey(intercept: true).Key)
            {
                case ConsoleKey.O:
                    WriteLine("\nBiztosan törlöd az ÖSSZES karaktert? (I / N)", ConsoleColor.Red);
                    if (Console.ReadKey(intercept: true).Key is ConsoleKey.I or ConsoleKey.Y)
                    {
                        _characterRoster.Clear();
                        SaveCharacters();
                        ResetConsole();
                        WriteLine("Minden karakter törölve.", ConsoleColor.Green);
                        Console.ReadKey(intercept: true);
                        return;
                    }
                    break;
                case ConsoleKey.UpArrow:
                    selectedIndex = (selectedIndex - 1 + _characterRoster.Characters.Count) % _characterRoster.Characters.Count;
                    break;
                case ConsoleKey.DownArrow:
                    selectedIndex = (selectedIndex + 1) % _characterRoster.Characters.Count;
                    break;
                case ConsoleKey.Enter:
                    var character = _characterRoster.Characters[selectedIndex];
                    WriteLine($"\nBiztosan törlöd: {character.Name}? (I / N)", ConsoleColor.Red);
                    if (Console.ReadKey(intercept: true).Key is ConsoleKey.I or ConsoleKey.Y)
                    {
                        _characterRoster.Remove(character);
                        SaveCharacters();
                        ResetConsole();
                        WriteLine($"{character.Name} törölve.", ConsoleColor.Green);
                        Console.ReadKey(intercept: true);
                        return;
                    }
                    break;
                case ConsoleKey.Escape:
                    return;
            }
        }
    }

    public static void ShowHelp()
    {
        ResetConsole();
        WriteLine("=== SÚGÓ ===", ConsoleColor.Yellow);
        Console.WriteLine();
        WriteLine("LABIRINTUS", ConsoleColor.Green);
        Console.WriteLine("Nyilak: mozgás | Esc: visszatérés a főmenübe megerősítéssel");
        Console.WriteLine("Tab: térkép/karakterlap | Karakterlap: fel/le kijelölés, bal/jobb partitagváltás");
        Console.WriteLine("Karakterlap: Space - tárgy mozgatása | D - ledobás | I - részletek | Enter - használat");
        Console.WriteLine("Partitárs kijelölve: Del - végleges kirúgás megerősítéssel");
        Console.WriteLine("Mágus/Pap: V - memorizált varázslatok | F1-F8 - gyorsvarázslatok");
        Console.WriteLine("V alatt: 📜 tekercs 0 mannás és egyszeri (mágus: mágusige vagy papi ima; pap/lovag: papi ima).");
        Console.WriteLine($"Felszerelt {ConsoleRenderer.WandIcon} pálca: minden kaszt használhatja 0 mannából; elsütésenként egy töltet fogy.");
        Console.WriteLine("Varázslatlista: fel/le, Enter - elsütés, bal/jobb - partitag varázslóváltás, F1-F8 - gyorshely, Esc - vissza");
        Console.WriteLine("Célzás: nyilak - célkereszt | Tab - következő érvényes cél | Enter - megerősítés | Esc - mégse");
        Console.WriteLine("Shift+F1: súgó | F9: teljes játékállás mentése a mentések mappába");
        Console.WriteLine("K: tetem átkutatása/földi tárgyak felvétele | máshol, ajtó mellett: kulcsra zárás");
        Console.WriteLine("Ajtó mellett: N - nyitás | Z - bezárás");
        Console.WriteLine("P: pihenés (pályánként egyszer, ellenségmentes és kulcsra zárt szobában)");
        Console.WriteLine("Partiparancs: H - helyben maradás | Shift+H - szoros gyülekező | M - 10 másodperces szétszóródás");
        Console.WriteLine("Ládára lépés: arany felvétele | Kijárat (⌂): következő labirintusszint");
        Console.WriteLine();
        WriteLine("BUFFOK ÉS IDŐTARTAM", ConsoleColor.Magenta);
        Console.WriteLine("Akció: csatában a karakter saját köre; térképen ugyanazon karakter minden 10. sikeres lépése.");
        Console.WriteLine("A partitagok saját akciószámlálót használnak. Az Isteni ítélet megduplázza a papi buff időtartamát.");
        Console.WriteLine("👻 Láthatatlanság: 3 akció; támadásig célpontvédelem, az első támadásra +5 találat.");
        Console.WriteLine("🛡️ Védelem: Arkán páncél +5/5; Áldás +1/4; Szent pajzs +5/4; Isteni védelem +3/4 akció.");
        Console.WriteLine("🪨 Sebzéscsökkentés: Kőbőr 50%/4; Isteni védelem 25%/4 akció. 🩸🚫 Kőbőr: vérzésvédelem/4.");
        Console.WriteLine("🎯 Találat: Áldás +1/4; Bátorság imája +2/5; Mézsör/Fűszeres bor +1/10 akció.");
        Console.WriteLine("⚔️✨ Sebzés: Bátorság imája +2/5 akció. ⚡ Kezdeményezés: Áldás +2/4; Bátorság +3/5; ital +2/10.");
        Console.WriteLine("✝️🛡️ Gonosz elleni védelem: +4 védelem, 30% csökkentés és állapotvédelem 5 akcióig.");
        Console.WriteLine("👼 Őrangyal: 5 akcióig kivédi az első halálos csapást és gyógyít; aktiváláskor elfogy.");
        Console.WriteLine("⛪ Szentély: 50% sebzéscsökkentés és állapotvédelem 4 akcióig; saját támadáskor megszűnik.");
        Console.WriteLine();
        WriteLine("CSATA", ConsoleColor.Red);
        Console.WriteLine("Saját kör: Space - fegyver | V/F1-F8 - varázslás; választás csak használható varázslatnál jelenik meg.");
        Console.WriteLine("Pap/Lovag élőholt ellen: T - csatánként egyszer Halottűzés / Szent elűzés.");
        Console.WriteLine("Harci varázslási kudarc: max(0, 30 - Intelligencia - Ügyesség)%; a manna és az akció elvész.");
        Console.WriteLine("A csata alatt a világ ideje megáll.");
        Console.WriteLine();
        WriteLine("Bármely billentyű: vissza az előző képernyőre", ConsoleColor.DarkYellow);
        Console.ReadKey(intercept: true);
    }

    private void SaveCharacters() => _characterSaveService.Save(_characterRoster);

    private void DrawMainMenu()
    {
        Console.Clear();
        try
        {
            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            var art = AsciiArts.GetMainScreen();
            Console.Write(art);
        }
        catch
        {
            // If ASCII art can't be loaded, fall back to a simple header.
            Console.WriteLine("=== Káoszrubin ===");
        }

        var left = Math.Min(SideMenuLeft, Math.Max(0, Console.WindowWidth - SideMenuWidth - 1));
        var right = left + SideMenuWidth - 2;
        var top = 8; // slightly below the top of the ASCII art

        string[] lines = new[]
        {
            "🏛️  FŐMENÜ  🏛",
            string.Empty,
            $"Választott karakter: {_characterRoster.SelectedCharacter?.Name ?? "(nincs)"}",
            string.Empty,
            "1) ▶ Játék indítása",
            $"2) ▶ Játék betöltése ({_gameSaveService.List().Count})",
            "3) ▶ Gyorsindítás (autogenerált karakter)",
            "4) ▶ Karaktergenerálás",
            $"5) ▶ Karakterek listája({_characterRoster.Characters.Count})",
            "6) ▶ Karakterek törlése",
            "7) ▶ Súgó",
            "8) ▶ Többjátékos coop játék hostolása (LAN) - ",
            "9) ▶ Csatlakozás LAN játékhoz",
            string.Empty,
            "Esc - Kilépés"
        };

        // Top border
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.SetCursorPosition(left, top);
        Console.Write("╔" + new string('═', SideMenuWidth - 2) + "╗");

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i] ?? string.Empty;
            // simple truncation that doesn't consider emoji width; keep small lines so alignment ok
            var content = line.Length > SideMenuWidth - 4 ? line.Substring(0, SideMenuWidth - 4) : line;
            Console.SetCursorPosition(left, top + i + 1);
            Console.Write("║ ");
            // Color each menu point individually
            switch (i - 3)
            {
                case 0:
                    Console.ForegroundColor = ConsoleColor.Yellow; // heading
                    break;
                case 1: // 1) Játék indítása
                case 3: // 3) Gyorsindítás
                    Console.ForegroundColor = ConsoleColor.Green;
                    break;
                case 2: // 2) Betöltés
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    break;
                case 4: // 4) Karaktergenerálás
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    break;
                case 5: // 5) Karakterek
                    Console.ForegroundColor = ConsoleColor.Blue;
                    break;
                case 6: // 6) Karakter törlése
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case 7: // 7) Súgó
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    break;
                case 8: // 8) LAN host
                case 9: // 9) Csatlakozás
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case 10: // Esc
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    break;
                default:
                    Console.ForegroundColor = ConsoleColor.Gray;
                    break;
            }
            Console.Write(content.PadRight(SideMenuWidth - 4));
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.SetCursorPosition(right, top + i + 1);
            Console.Write(" ║");
        }

        // Bottom border
        Console.SetCursorPosition(left, top + lines.Length + 1);
        Console.Write("╚" + new string('═', SideMenuWidth - 2) + "╝");
        Console.ResetColor();
    }

    private static void ResetConsole()
    {
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.BackgroundColor = ConsoleColor.Black;
        Console.Clear();
    }

    private static void WriteLine(string text, ConsoleColor foregroundColor)
    {
        Console.ForegroundColor = foregroundColor;
        Console.BackgroundColor = ConsoleColor.Black;
        Console.WriteLine(text);
    }
}
