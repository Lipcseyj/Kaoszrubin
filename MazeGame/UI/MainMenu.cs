using MazeGame.Data;
using MazeGame.Application;
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

    private const int SideMenuWidth = 52;
    private const int SideMenuLeft = 142;
    private const int SideMenuTop = 8;

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
                    QuickStart();
                    SaveCharacters();
                    break;
                case ConsoleKey.D3:
                case ConsoleKey.NumPad3:
                    ManageCharacters();
                    SaveCharacters();
                    break;
                case ConsoleKey.D4:
                case ConsoleKey.NumPad4:
                    ShowHelp();
                    break;
                case ConsoleKey.D5:
                case ConsoleKey.NumPad5:
                    LoadGame();
                    SaveCharacters();
                    break;
                case ConsoleKey.D6:
                case ConsoleKey.NumPad6:
                    StartHostedGame();
                    SaveCharacters();
                    break;
                case ConsoleKey.D7:
                case ConsoleKey.NumPad7:
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
            // A coop lobby a leaderből indul; a távoli játékos a saját karakterével tölti fel a következő helyet.
            _characterRoster.Party.SetLeader(selectedCharacter);
            var game = new Game(_gameData, _characterRoster, selectedCharacter, _gameSaveService);
            var host = CoopHostRuntime.StartAsync(game.Session, _applicationVersion, _catalogHash,
                    _characterSaveService.DeserializeCharacter, character => _characterRoster.Add(character))
                .GetAwaiter().GetResult();
            try
            {
                DrawMainBackdrop();
                DrawSidePanel("COOP VÁRAKOZÓSZOBA",
                [
                    "A host elindult.", string.Empty,
                    "Csatlakozási cím:", host.ConnectionHint, string.Empty,
                    $"Leader: {selectedCharacter.Name}",
                    "Várakozás a vendég karakterére…", string.Empty,
                    "Esc) Lobby bezárása"
                ]);
                while (game.Session.ConnectedRemoteCharacterCount == 0)
                {
                    if (Console.KeyAvailable && Console.ReadKey(intercept: true).Key == ConsoleKey.Escape) return;
                    Thread.Sleep(50);
                }
                ResetConsole();
                WriteLine("A vendég csatlakozott. A játék indul…", ConsoleColor.Green);
                Thread.Sleep(500);
                game.Run(host);
            }
            finally { host.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or
                                           System.Net.Sockets.SocketException)
        {
            DrawMainBackdrop();
            DrawSidePanel("HOST HIBA", [$"A coop host nem indítható:", exception.Message, string.Empty, "Bármely billentyű: vissza"]);
            Console.ReadKey(intercept: true);
        }
    }

    private void JoinGame()
    {
        if (!TryGetPlayableSelectedCharacter(out var character)) return;
        DrawMainBackdrop();
        DrawSidePanel("COOP CSATLAKOZÁS",
        [
            $"Karakter: {character.Name}", string.Empty,
            "Add meg a host címét:",
            "(Enter = localhost:5127)", string.Empty,
            ">", string.Empty,
            "Üres Enter = localhost:5127"
        ]);
        var inputLeft = Math.Min(SideMenuLeft, Math.Max(0, Console.WindowWidth - SideMenuWidth - 1)) + 4;
        Console.SetCursorPosition(inputLeft, SideMenuTop + 7);
        Console.ForegroundColor = ConsoleColor.Cyan;
        var hostUrl = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(hostUrl)) hostUrl = "http://localhost:5127";
        try
        {
            new CoopGuestScreen(_applicationVersion, _catalogHash, _gameData)
                .RunAsync(hostUrl, character.Name, character, _characterSaveService.SerializeCharacter(character),
                    PersistGuestCharacterState)
                .GetAwaiter().GetResult();
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or
                                           TimeoutException or HttpRequestException)
        {
            DrawMainBackdrop();
            DrawSidePanel("CSATLAKOZÁSI HIBA", ["A csatlakozás sikertelen:", exception.Message, string.Empty, "Bármely billentyű: vissza"]);
            Console.ReadKey(intercept: true);
        }
    }

    private void PersistGuestCharacterState(CharacterStateSync state)
    {
        lock (_characterRoster)
        {
            var current = _characterRoster.Characters.FirstOrDefault(character => character.Id == state.CharacterId)
                ?? throw new InvalidOperationException("A visszaszinkronizált karakter nincs a helyi karakterlistában.");
            var replacement = _characterSaveService.DeserializeCharacter(state.CharacterData);
            if (replacement.Id != state.CharacterId || !_characterRoster.Replace(current, replacement))
                throw new InvalidOperationException("A host érvénytelen karakterállapotot küldött.");
            _characterSaveService.Save(_characterRoster);
        }
    }

    private void StartHostedLoadedGame(LoadedGameSave loaded)
    {
        _characterRoster = loaded.Roster;
        var leader = _characterRoster.SelectedCharacter
            ?? throw new InvalidOperationException("A mentés nem tartalmaz party-leadert.");
        var reservedGuid = loaded.State.RemoteCharacterIds.FirstOrDefault();
        var reservedId = new CharacterId(reservedGuid);
        var reservedCharacter = _characterRoster.Party.Members.FirstOrDefault(character => character.Id == reservedId);
        if (reservedGuid == Guid.Empty || reservedCharacter is null || reservedCharacter == leader)
            throw new InvalidOperationException("A coop mentés nem tartalmaz érvényes vendégkarakter-slotot.");

        var game = new Game(_gameData, _characterRoster, leader, _gameSaveService, loaded.State);
        var host = CoopHostRuntime.StartAsync(game.Session, _applicationVersion, _catalogHash,
                _characterSaveService.DeserializeCharacter, character => _characterRoster.Add(character),
                reservedRemoteCharacterId: reservedId)
            .GetAwaiter().GetResult();
        try
        {
            DrawMainBackdrop();
            DrawSidePanel("COOP MENTÉS VÁRÓSZOBÁJA",
            [
                $"Mentés: {loaded.State.MazeLevel}. pálya", string.Empty,
                "Csatlakozási cím:", host.ConnectionHint, string.Empty,
                $"Leader: {leader.Name}",
                $"Várt vendég: {reservedCharacter.Name}",
                $"{reservedCharacter.CharacterClass.Name}, {reservedCharacter.Level}. szint", string.Empty,
                "A mentett CharacterId egyezése szükséges.",
                "Esc) Lobby bezárása"
            ]);
            while (game.Session.ConnectedRemoteCharacterCount == 0)
            {
                if (Console.KeyAvailable && Console.ReadKey(intercept: true).Key == ConsoleKey.Escape) return;
                Thread.Sleep(50);
            }
            ResetConsole();
            WriteLine($"{reservedCharacter.Name} visszacsatlakozott. A mentett játék indul…", ConsoleColor.Green);
            Thread.Sleep(500);
            game.Run(host);
        }
        finally { host.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
    }

    private void ManageCharacters()
    {
        var selectedIndex = _characterRoster.SelectedCharacter is null ? 0 :
            Math.Max(0, _characterRoster.Characters.ToList().IndexOf(_characterRoster.SelectedCharacter));
        while (true)
        {
            selectedIndex = _characterRoster.Characters.Count == 0 ? 0 :
                Math.Clamp(selectedIndex, 0, _characterRoster.Characters.Count - 1);
            DrawCharacterManager(selectedIndex);
            switch (Console.ReadKey(intercept: true).Key)
            {
                case ConsoleKey.UpArrow when _characterRoster.Characters.Count > 0:
                    selectedIndex = (selectedIndex - 1 + _characterRoster.Characters.Count) % _characterRoster.Characters.Count;
                    break;
                case ConsoleKey.DownArrow when _characterRoster.Characters.Count > 0:
                    selectedIndex = (selectedIndex + 1) % _characterRoster.Characters.Count;
                    break;
                case ConsoleKey.Enter when _characterRoster.Characters.Count > 0:
                    _characterRoster.Select(_characterRoster.Characters[selectedIndex]);
                    SaveCharacters();
                    break;
                case ConsoleKey.N:
                    var before = _characterRoster.Characters.Count;
                    new CharacterCreationScreen(_gameData, _characterRoster).Run();
                    if (_characterRoster.Characters.Count > before) selectedIndex = _characterRoster.Characters.Count - 1;
                    SaveCharacters();
                    break;
                case ConsoleKey.D when _characterRoster.Characters.Count > 0:
                case ConsoleKey.Delete when _characterRoster.Characters.Count > 0:
                    var character = _characterRoster.Characters[selectedIndex];
                    var confirmationFrame = GetCharacterManagerFrame();
                    WriteAt(confirmationFrame.Left + 4, confirmationFrame.Top + confirmationFrame.Height - 3,
                        $"⚠ Biztosan törlöd: {character.Name}?  I/Y = igen", ConsoleColor.Red,
                        confirmationFrame.Width - 8);
                    if (Console.ReadKey(intercept: true).Key is ConsoleKey.I or ConsoleKey.Y)
                    {
                        _characterRoster.Remove(character);
                        SaveCharacters();
                    }
                    break;
                case ConsoleKey.Escape:
                    return;
            }
        }
    }

    private void DrawCharacterManager(int selectedIndex)
    {
        ResetConsole();
        var frame = GetCharacterManagerFrame();
        DrawCharacterManagerFrame(frame);
        WriteAt(frame.Left + 4, frame.Top + 1, "👥 KARAKTEREK", ConsoleColor.Yellow, frame.Width - 8);
        WriteAt(frame.Left + 4, frame.Top + 3,
            "↑/↓ választ   Enter aktív   N új hős   D/Del törlés   Esc főmenü",
            ConsoleColor.DarkCyan, frame.Width - 8);
        if (_characterRoster.Characters.Count == 0)
            WriteAt(frame.Left + 5, frame.Top + 7,
                "🪶 Még nincs hős. Nyomj N-t egy új karakter megalkotásához.", ConsoleColor.DarkYellow,
                frame.Width - 10);

        var showPortrait = frame.Width >= 90;
        var portraitWidth = showPortrait ? 31 : 0;
        var listWidth = Math.Max(38, frame.Width - portraitWidth - 10);
        var visibleCount = Math.Max(1, (frame.Height - 9) / 2);
        var maximumStart = Math.Max(0, _characterRoster.Characters.Count - visibleCount);
        var firstVisible = Math.Clamp(selectedIndex - visibleCount / 2, 0, maximumStart);
        var lastVisible = Math.Min(_characterRoster.Characters.Count, firstVisible + visibleCount);
        for (var index = firstVisible; index < lastVisible; index++)
        {
            var character = _characterRoster.Characters[index];
            var row = frame.Top + 6 + (index - firstVisible) * 2;
            var active = character == _characterRoster.SelectedCharacter ? " ★ AKTÍV" : string.Empty;
            var dead = character.IsAlive ? string.Empty : " ☠ HALOTT";
            var nameColor = !character.IsAlive ? ConsoleColor.DarkRed :
                index == selectedIndex ? ConsoleColor.Cyan :
                character == _characterRoster.SelectedCharacter ? ConsoleColor.Yellow : ConsoleColor.Gray;
            WriteAt(frame.Left + 4, row,
                $"{(index == selectedIndex ? "▶" : " ")} {CharacterClassIcon(character.CharacterClass.Id)} " +
                $"{character.Name} · {character.Race.Name} {character.CharacterClass.Name}{active}{dead}",
                nameColor, listWidth);
            WriteAt(frame.Left + 8, row + 1,
                $"Szint {character.Level}   ❤️ {character.CurrentVitality}/{character.MaximumVitality}   " +
                $"🔷 {(character.UsesMana ? $"{character.CurrentMana}/{character.MaximumMana}" : "—")}",
                character.IsAlive ? ConsoleColor.DarkGray : ConsoleColor.Red, listWidth - 4);
        }
        if (_characterRoster.Characters.Count > 0)
        {
            if (firstVisible > 0)
                WriteAt(frame.Left + 4, frame.Top + 5, $"▲ még {firstVisible} karakter", ConsoleColor.DarkCyan, listWidth);
            if (lastVisible < _characterRoster.Characters.Count)
                WriteAt(frame.Left + 4, frame.Top + frame.Height - 4,
                    $"▼ még {_characterRoster.Characters.Count - lastVisible} karakter", ConsoleColor.DarkCyan, listWidth);
            if (showPortrait)
                DrawPortrait(_characterRoster.Characters[selectedIndex],
                    frame.Left + frame.Width - portraitWidth - 3, frame.Top + 6);
        }
    }

    private static void DrawPortrait(LiveCharacter character, int left, int top)
    {
        var portrait = AsciiPortraits.ForCharacterClass(character.CharacterClass.Id);
        WriteAt(left, top, "@)" + new string('=', 25) + "(@", character.Color, 29);
        WriteAt(left + 3, top + 1, $"{CharacterClassIcon(character.CharacterClass.Id)} {character.CharacterClass.Name.ToUpperInvariant()}",
            character.Color, 23);
        for (var index = 0; index < portrait.Lines.Count; index++)
        {
            WriteAt(left + 1, top + index + 2, "|", character.Color, 1);
            WriteAt(left + 3, top + index + 2, portrait.Lines[index], character.Color, 23);
            WriteAt(left + 27, top + index + 2, "|", character.Color, 1);
        }
        WriteAt(left, top + portrait.Lines.Count + 2, "@)" + new string('=', 25) + "(@", character.Color, 29);
    }

    private readonly record struct CharacterManagerFrame(int Left, int Top, int Width, int Height);

    private static CharacterManagerFrame GetCharacterManagerFrame()
    {
        var width = Math.Max(10, Math.Min(118, Console.WindowWidth - 2));
        var height = Math.Max(8, Math.Min(Console.WindowHeight - 1, 28));
        return new CharacterManagerFrame(Math.Max(0, (Console.WindowWidth - width) / 2), 0, width, height);
    }

    private static void DrawCharacterManagerFrame(CharacterManagerFrame frame)
    {
        WriteAt(frame.Left, frame.Top, "@)" + new string('=', frame.Width - 4) + "(@", ConsoleColor.DarkYellow, frame.Width);
        for (var row = 1; row < frame.Height - 1; row++)
        {
            WriteAt(frame.Left, frame.Top + row, " |", ConsoleColor.DarkCyan, 2);
            WriteAt(frame.Left + frame.Width - 2, frame.Top + row, "| ", ConsoleColor.DarkCyan, 2);
        }
        WriteAt(frame.Left, frame.Top + frame.Height - 1,
            "@)" + new string('=', frame.Width - 4) + "(@", ConsoleColor.DarkYellow, frame.Width);
    }

    private static string CharacterClassIcon(string characterClassId) => characterClassId switch
    {
        CharacterClassIds.Harcos => "⚔",
        CharacterClassIds.Barbár => "🪓",
        CharacterClassIds.Lovag => "🛡",
        CharacterClassIds.Tolvaj => "🗡",
        CharacterClassIds.Pap => "✝",
        CharacterClassIds.Mágus => "🔮",
        _ => "◆"
    };

    private bool TryGetPlayableSelectedCharacter(out LiveCharacter selectedCharacter)
    {
        if (_characterRoster.SelectedCharacter is not { } candidate)
        {
            DrawMainBackdrop();
            DrawSidePanel("NINCS AKTÍV KARAKTER", ["Előbb válassz aktív karaktert", "a Karakterek menüben.", string.Empty, "Bármely billentyű: vissza"]);
            Console.ReadKey(intercept: true);
            selectedCharacter = null!;
            return false;
        }
        if (!candidate.IsAlive)
        {
            DrawMainBackdrop();
            DrawSidePanel("A KARAKTER HALOTT", ["Válassz másik karaktert", "vagy készíts újat.", string.Empty, "Bármely billentyű: vissza"]);
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
            DrawMainBackdrop();
            DrawSidePanel("JÁTÉK BETÖLTÉSE", ["Nincs betölthető játékállás.", string.Empty, "Bármely billentyű: vissza"]);
            Console.ReadKey(intercept: true);
            return;
        }
        var selectedIndex = 0;
        while (true)
        {
            DrawMainBackdrop();
            var lines = new List<string> { "↑↓ választ  Enter betölt  Esc vissza", string.Empty };
            var maximumVisibleSaves = Math.Max(1, (Console.WindowHeight - SideMenuTop - 6) / 2);
            var firstVisible = Math.Clamp(selectedIndex - maximumVisibleSaves / 2, 0,
                Math.Max(0, saves.Count - maximumVisibleSaves));
            var lastVisible = Math.Min(saves.Count, firstVisible + maximumVisibleSaves);
            if (firstVisible > 0) lines.Add($"  ↑ még {firstVisible} mentés");
            for (var index = firstVisible; index < lastVisible; index++)
            {
                var save = saves[index];
                var marker = index == selectedIndex ? ">" : " ";
                lines.Add($"{marker} {(save.IsCoopGame ? "[COOP] " : string.Empty)}{save.MainCharacterName} — {save.MazeLevel}. pálya");
                lines.Add($"  {save.SavedAt:yyyy-MM-dd HH:mm}");
            }
            if (lastVisible < saves.Count) lines.Add($"  ↓ még {saves.Count - lastVisible} mentés");
            DrawSidePanel("JÁTÉK BETÖLTÉSE", lines);
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
                        var loadAsCoopHost = false;
                        if (loaded.State.IsCoopGame && loaded.State.RemoteCharacterIds.Count > 0)
                        {
                            DrawMainBackdrop();
                            DrawSidePanel("COOP MENTÉS BETÖLTÉSE",
                            [
                                $"{loaded.State.MainCharacterName} — {loaded.State.MazeLevel}. pálya", string.Empty,
                                "S) Betöltés egyjátékos módban",
                                "C) Betöltés coop hostként", string.Empty,
                                "Esc) Vissza"
                            ]);
                            var modeKey = Console.ReadKey(intercept: true).Key;
                            if (modeKey == ConsoleKey.Escape) break;
                            loadAsCoopHost = modeKey == ConsoleKey.C;
                            if (!loadAsCoopHost && modeKey != ConsoleKey.S) break;
                        }
                        if (loadAsCoopHost)
                            StartHostedLoadedGame(loaded);
                        else
                        {
                            _characterRoster = loaded.Roster;
                            new Game(_gameData, _characterRoster, _characterRoster.SelectedCharacter!,
                                _gameSaveService, loaded.State).Run();
                        }
                        return;
                    }
                    catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException or
                                                       System.Net.Sockets.SocketException)
                    {
                        DrawMainBackdrop();
                        DrawSidePanel("BETÖLTÉSI HIBA", ["A mentés nem tölthető be:", exception.Message, string.Empty, "Bármely billentyű: vissza"]);
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
        var source = new (string Text, ConsoleColor Color)[]
        {
            ("LABIRINTUS", ConsoleColor.Green),
            ("Nyilak: mozgás | Esc: visszatérés a főmenübe megerősítéssel", ConsoleColor.Gray),
            ("Tab: térkép/karakterlap | Karakterlap: fel/le kijelölés, bal/jobb partitagváltás", ConsoleColor.Gray),
            ("Karakterlap: Space - tárgy mozgatása | D - ledobás | I - részletek | Enter - használat", ConsoleColor.Gray),
            ("Partitárs kijelölve: Del - végleges kirúgás megerősítéssel", ConsoleColor.Gray),
            ("Mágus/Pap: V - memorizált varázslatok | F1-F8 - gyorsvarázslatok", ConsoleColor.Gray),
            ("V alatt: 📜 tekercs 0 mannás és egyszeri (mágus: mágusige vagy papi ima; pap/lovag: papi ima).", ConsoleColor.Gray),
            ($"Felszerelt {ConsoleRenderer.WandIcon} pálca: minden kaszt használhatja 0 mannából; elsütésenként egy töltet fogy.", ConsoleColor.Gray),
            ("Varázslatlista: fel/le, Enter - elsütés, bal/jobb - partitag varázslóváltás, F1-F8 - gyorshely, Esc - vissza", ConsoleColor.Gray),
            ("Célzás: nyilak - célkereszt | Tab - következő érvényes cél | Enter - megerősítés | Esc - mégse", ConsoleColor.Gray),
            ("Shift+F1: súgó | F9: teljes játékállás mentése a mentések mappába", ConsoleColor.Gray),
            ("K: az aktuális mező átkutatása, tetem és földi tárgyak felvétele", ConsoleColor.Gray),
            ("Ajtó mellett: N - nyitás | Z - nyitott ajtó becsukása, csukott ajtó kulcsra zárása", ConsoleColor.Gray),
            ("P: pihenés (pályánként egyszer, ellenségmentes és kulcsra zárt szobában)", ConsoleColor.Gray),
            ("Partiparancs: H - helyben maradás | Shift+H - szoros gyülekező | M - 10 másodperces szétszóródás", ConsoleColor.Gray),
            ("Ládára lépés: arany felvétele | Kijárat (⌂): következő labirintusszint", ConsoleColor.Gray),
            (string.Empty, ConsoleColor.Gray),
            ("BUFFOK ÉS IDŐTARTAM", ConsoleColor.Magenta),
            ("Akció: csatában a karakter saját köre; térképen ugyanazon karakter minden 10. sikeres lépése.", ConsoleColor.Gray),
            ("A partitagok saját akciószámlálót használnak. Az Isteni ítélet megduplázza a papi buff időtartamát.", ConsoleColor.Gray),
            ("👻 Láthatatlanság: 3 akció; támadásig célpontvédelem, az első támadásra +5 találat.", ConsoleColor.Gray),
            ("🛡️ Védelem: Arkán páncél +5/5; Áldás +1/4; Szent pajzs +5/4; Isteni védelem +3/4 akció.", ConsoleColor.Gray),
            ("🪨 Sebzéscsökkentés: Kőbőr 50%/4; Isteni védelem 25%/4 akció. 🩸🚫 Kőbőr: vérzésvédelem/4.", ConsoleColor.Gray),
            ("🎯 Találat: Áldás +1/4; Bátorság imája +2/5; Mézsör/Fűszeres bor +1/10 akció.", ConsoleColor.Gray),
            ("⚔️✨ Sebzés: Bátorság imája +2/5 akció. ⚡ Kezdeményezés: Áldás +2/4; Bátorság +3/5; ital +2/10.", ConsoleColor.Gray),
            ("✝️🛡️ Gonosz elleni védelem: +4 védelem, 30% csökkentés és állapotvédelem 5 akcióig.", ConsoleColor.Gray),
            ("👼 Őrangyal: 5 akcióig kivédi az első halálos csapást és gyógyít; aktiváláskor elfogy.", ConsoleColor.Gray),
            ("⛪ Szentély: 50% sebzéscsökkentés és állapotvédelem 4 akcióig; saját támadáskor megszűnik.", ConsoleColor.Gray),
            (string.Empty, ConsoleColor.Gray),
            ("CSATA", ConsoleColor.Red),
            ("Saját kör: Space - fegyver | V/F1-F8 - varázslás; választás csak használható varázslatnál jelenik meg.", ConsoleColor.Gray),
            ("Pap/Lovag élőholt ellen: T - csatánként egyszer Halottűzés / Szent elűzés.", ConsoleColor.Gray),
            ("Harci varázslási kudarc: max(0, 30 - Intelligencia - Ügyesség)%; a manna és az akció elvész.", ConsoleColor.Gray),
            ("A csata alatt a világ ideje megáll.", ConsoleColor.Gray)
        };
        ShowScrollableHelp(source);
    }

    private static void ShowScrollableHelp(IReadOnlyList<(string Text, ConsoleColor Color)> source)
    {
        var offset = 0;
        while (true)
        {
            var width = Math.Max(30, Math.Min(122, Console.WindowWidth - 4));
            var height = Math.Max(8, Console.WindowHeight - 4);
            var contentWidth = width - 4;
            var lines = source.SelectMany(entry => WrapHelpText(entry.Text, contentWidth)
                .Select(text => (text, entry.Color))).ToArray();
            var pageSize = Math.Max(1, height - 4);
            offset = Math.Clamp(offset, 0, Math.Max(0, lines.Length - pageSize));
            var left = Math.Max(0, (Console.WindowWidth - width) / 2);
            var top = Math.Max(0, (Console.WindowHeight - height) / 2);
            ResetConsole();
            WriteAt(left, top, "┌" + new string('─', width - 2) + "┐", ConsoleColor.DarkCyan, width);
            WriteAt(left + 3, top, " SÚGÓ ", ConsoleColor.Yellow, 8);
            for (var row = 0; row < pageSize; row++)
            {
                WriteAt(left, top + row + 1, "│", ConsoleColor.DarkCyan, 1);
                var line = offset + row < lines.Length ? lines[offset + row] : (string.Empty, ConsoleColor.Gray);
                WriteAt(left + 2, top + row + 1, line.Item1, line.Item2, contentWidth);
                WriteAt(left + width - 1, top + row + 1, "│", ConsoleColor.DarkCyan, 1);
            }
            var status = $" ↑↓ görget  PgUp/PgDn lapoz  Home/End  Esc/Enter vissza  {offset + 1}-{Math.Min(lines.Length, offset + pageSize)}/{lines.Length} ";
            WriteAt(left, top + height - 2, "├" + new string('─', width - 2) + "┤", ConsoleColor.DarkCyan, width);
            WriteAt(left + 2, top + height - 2, TruncateByDisplayWidth(status, width - 4), ConsoleColor.DarkYellow, width - 4);
            WriteAt(left, top + height - 1, "└" + new string('─', width - 2) + "┘", ConsoleColor.DarkCyan, width);

            switch (Console.ReadKey(intercept: true).Key)
            {
                case ConsoleKey.UpArrow: offset--; break;
                case ConsoleKey.DownArrow: offset++; break;
                case ConsoleKey.PageUp: offset -= pageSize; break;
                case ConsoleKey.PageDown: offset += pageSize; break;
                case ConsoleKey.Home: offset = 0; break;
                case ConsoleKey.End: offset = lines.Length; break;
                case ConsoleKey.Escape:
                case ConsoleKey.Enter: return;
            }
        }
    }

    private static IEnumerable<string> WrapHelpText(string text, int width)
    {
        if (string.IsNullOrEmpty(text)) { yield return string.Empty; yield break; }
        while (text.Length > width)
        {
            var split = text.LastIndexOf(' ', Math.Min(width, text.Length - 1));
            if (split <= 0) split = width;
            yield return text[..split];
            text = text[split..].TrimStart();
        }
        yield return text;
    }

    private void SaveCharacters() => _characterSaveService.Save(_characterRoster);

    private void DrawMainMenu()
    {
        DrawMainBackdrop();
        var lines = new[]
        {
            "🏛️  FŐMENÜ",
            string.Empty,
            $"Aktív karakter: {_characterRoster.SelectedCharacter?.Name ?? "(nincs kiválasztva)"}",
            string.Empty,
            "1) Játék indítása",
            "2) Gyorsindítás",
            $"3) Karakterek ({_characterRoster.Characters.Count})",
            "4) Súgó",
            string.Empty,
            "── JÁTÉKÁLLÁS ÉS COOP ──",
            $"5) Játék betöltése ({_gameSaveService.List().Count})",
            "6) Coop játék hostolása",
            "7) Csatlakozás coop játékhoz",
            string.Empty,
            "Esc) Kilépés"
        };
        DrawSidePanel("KÁOSZRUBIN", lines);
    }

    private void DrawMainBackdrop()
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
    }

    private void DrawSidePanel(string title, IReadOnlyList<string> lines)
    {
        var left = Math.Min(SideMenuLeft, Math.Max(0, Console.WindowWidth - SideMenuWidth - 1));
        var right = left + SideMenuWidth - 2;
        var top = Math.Min(SideMenuTop, Math.Max(0, Console.WindowHeight - 3));
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.SetCursorPosition(left, top);
        Console.Write("╔" + new string('═', SideMenuWidth - 2) + "╗");
        var visibleLineCount = Math.Min(lines.Count, Math.Max(1, Console.WindowHeight - top - 2));
        for (var i = 0; i < visibleLineCount; i++)
        {
            var line = lines[i] ?? string.Empty;
            var content = TruncateByDisplayWidth(line, SideMenuWidth - 4);
            Console.SetCursorPosition(left, top + i + 1);
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write("║ ");
            Console.ForegroundColor = MainMenuLineColor(line);
            Console.Write(PadRightDisplay(content, SideMenuWidth - 4));
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.SetCursorPosition(right, top + i + 1);
            Console.Write(" ║");
        }
        Console.SetCursorPosition(left, top + visibleLineCount + 1);
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

    private static void WriteAt(int left, int top, string text, ConsoleColor color, int width)
    {
        if (left < 0 || top < 0 || left >= Console.WindowWidth || top >= Console.WindowHeight) return;
        Console.SetCursorPosition(left, top);
        Console.ForegroundColor = color;
        Console.BackgroundColor = ConsoleColor.Black;
        Console.Write(PadRightDisplay(text, Math.Min(width, Console.WindowWidth - left)));
        Console.ResetColor();
    }

    private static ConsoleColor MainMenuLineColor(string line)
    {
        if (line.StartsWith("Aktív:", StringComparison.Ordinal)) return ConsoleColor.Yellow;
        if (line.StartsWith("1)", StringComparison.Ordinal)) return ConsoleColor.DarkGreen;
        if (line.StartsWith("2)", StringComparison.Ordinal)) return ConsoleColor.Green;
        if (line.StartsWith("3)", StringComparison.Ordinal)) return ConsoleColor.Cyan;
        if (line.StartsWith("4)", StringComparison.Ordinal)) return ConsoleColor.Blue;
        if (line.Contains("COOP", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("host", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Csatlakozás", StringComparison.OrdinalIgnoreCase)) return ConsoleColor.DarkYellow;
        if (line.StartsWith("──", StringComparison.Ordinal)) return ConsoleColor.DarkMagenta;
        if (line.StartsWith("Esc", StringComparison.Ordinal)) return ConsoleColor.DarkYellow;
        return ConsoleColor.Gray;
    }
}
