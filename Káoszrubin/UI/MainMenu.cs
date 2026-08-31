using KaoszRubin.Data;
using KaoszRubin.Application;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Transport.SignalR;
using System.Text.Json;

namespace KaoszRubin.UI;

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
    private readonly SoundEffects _soundEffects;
    private readonly MusicSettingsService _musicSettings = new();

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
        _soundEffects = new SoundEffects();
    }

    public void Run()
    {
        while (true)
        {
            if (_musicSettings.Settings.Enabled)
                _soundEffects.Play(SoundEffect.MainMenu);
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
                case ConsoleKey.D8:
                case ConsoleKey.NumPad8:
                    SettingsScreen.Show(_musicSettings);
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

        new Game(_gameData, _characterRoster, selectedCharacter, _gameSaveService,
            musicSettings: _musicSettings).Run();
    }

    private void StartHostedGame()
    {
        if (!TryGetPlayableSelectedCharacter(out var selectedCharacter)) return;
        try
        {
            // A coop lobby a leaderből indul; a távoli játékos a saját karakterével tölti fel a következő helyet.
            _characterRoster.Party.SetLeader(selectedCharacter);
            var game = new Game(_gameData, _characterRoster, selectedCharacter, _gameSaveService,
                musicSettings: _musicSettings);
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
            new CoopGuestScreen(_applicationVersion, _catalogHash, _gameData, _musicSettings)
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

        var game = new Game(_gameData, _characterRoster, leader, _gameSaveService, loaded.State,
            musicSettings: _musicSettings);
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
                                _gameSaveService, loaded.State, musicSettings: _musicSettings).Run();
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
        var source = new HelpSourceLine[]
        {
            Section("GLOBÁLIS", ConsoleColor.Blue),
            Hotkey("ESC", "Visszatérés a főmenübe, megerősítéssel."),
            Hotkey("SHIFT+F1", "A súgó megnyitása."),
            Hotkey("SHIFT+F2", "Beállítások megnyitása."),
            Blank(),
            Section("LABIRINTUS", ConsoleColor.Green),
            Hotkey("NYILAK", "Mozgás a labirintusban."),
            Hotkey("TAB", "Váltás a térkép és a karakterlap között."),
            Hotkey("V", "Memorizált varázslatok megnyitása Mágussal, Pappal vagy Lovaggal."),
            Hotkey("F1–F8", "Gyorsvarázslatok elsütése."),
            Hotkey("PGUP / PGDN", "A 21 soros eseménynapló lapozása 7 soronként."),
            Hotkey("F9", "A teljes játékállás mentése a mentések mappába."),
            Hotkey("K", "Felfedezett szomszédos csapda hatástalanítása; egyébként az aktuális mező átkutatása."),
            Hotkey("N", "A melletted levő ajtó kinyitása."),
            Hotkey("Z", "Nyitott ajtó becsukása, csukott ajtó kulcsra zárása."),
            Hotkey("P", "Pihenés pályánként egyszer, ellenségmentes és kulcsra zárt szobában."),
            Hotkey("LÁDÁRA LÉPÉS", "Az arany felvétele."),
            Hotkey("KIJÁRAT (⌂) + ENTER", "Továbbjutás a következő labirintusszintre."),
            Text("⚠️ A csapdák megközelítésekor automatikus észlelési próba történik. A Tolvaj jelentős bónuszt kap az észleléshez és a hatástalanításhoz."),
            Blank(),
            Section("KARAKTERLAP", ConsoleColor.DarkMagenta),
            Hotkey("TAB", "Váltás a térkép és a karakterlap között."),
            Hotkey("↑ / ↓", "Kijelölés mozgatása a karakterlapon."),
            Hotkey("← / →", "Partitagváltás a karakterlapon."),
            Hotkey("SPACE", "A kijelölt tárgy mozgatása."),
            Hotkey("R", "A kijelölt karakter részletes karakterinformációi."),
            Hotkey("F", "A kijelölt több darabos hátizsákköteg kettéosztása egy üres hátizsákhelyre."),
            Hotkey("S", "A kijelölt több darabos hátizsákköteg szétosztása a csapattagokkal."),
            Hotkey("K", "A kijelölt több darabos hátizsákköteg szétosztása a követővel."),
            Hotkey("D", "A kijelölt tárgy ledobása."),
            Hotkey("I", "A kijelölt tárgy részletei."),
            Hotkey("ENTER", "A kijelölt tárgy használata."),
            Hotkey("DEL", "A kijelölt partitárs végleges kirúgása, megerősítéssel."),
            Blank(),
            Section("PARTY TAKTIKÁK", ConsoleColor.Yellow),
            Text("A partiparancsok az AI által irányított NPC társak mozgását szabályozzák. A vendég által irányított karakter továbbra is a saját játékosának engedelmeskedik."),
            Hotkey("G", "GYÜLEKEZŐ: az NPC társak harckeresés nélkül a vezér mellé zárkóznak és ott maradnak."),
            Hotkey("T", "TÁMADÁS: az NPC társak agresszívan keresik és megtámadják az ellenfeleket a parancs kikapcsolásáig."),
            Hotkey("H", "MEGÁLLJ: minden NPC társ azonnal tartja a helyét; újbóli H visszaadja a saját viselkedését."),
            Hotkey("M", "SZÉTSZÓRÓDÁS: az NPC társak 10 másodpercig távolodnak a vezértől."),
            Text("A G / T / H módok kölcsönösen kizárják egymást. Az M kikapcsolja mindhármat; lejárta után a társak a saját kaszt- és személyiségalapú viselkedésükhöz térnek vissza."),
            Blank(),
            Section("VARÁZSLATOK", ConsoleColor.Cyan),
            Hotkey("V: ↑ / ↓", "Varázslat kijelölése."),
            Hotkey("V: ENTER", "A kijelölt varázslat elsütése."),
            Hotkey("V: ← / →", "Partitag varázslóváltása."),
            Hotkey("V: F1–F8", "A kijelölt varázslat gyorshelyének beállítása."),
            Hotkey("V: ESC", "Visszalépés."),
            Hotkey("CÉLZÁS: NYILAK", "A célkereszt mozgatása."),
            Hotkey("CÉLZÁS: TAB", "Ugrás a következő érvényes célra."),
            Hotkey("CÉLZÁS: ENTER", "A célpont megerősítése."),
            Hotkey("CÉLZÁS: ESC", "Célzás megszakítása."),
            Text("📜 A tekercs 0 mannás és egyszer használható (mágus: mágusige vagy papi ima; pap/lovag: papi ima)."),
            Text($"{ConsoleRenderer.WandIcon} A felszerelt pálcát minden kaszt használhatja 0 mannából; elsütésenként egy töltet fogy."),
            Text("Memóriaképlet: Mágus = 2 + INT/3 + szint/5; Pap = 2 + INT/4 + szint/5; Lovag = 1 + INT/5 + szint/10, legfeljebb 4. Az osztások lefelé kerekülnek."),
            ColoredText("VARÁZSMEMÓRIA 9 INT ESETÉN", ConsoleColor.Magenta),
            Text("Szint       Mágus   Pap   Lovag"),
            Text(" 1–4          5      4      2"),
            Text(" 5–9          6      5      2"),
            Text("10–14         7      6      3"),
            Text("15–19         8      7      3"),
            Text("20–24         9      8      4"),
            Text("25–29        10      9      4"),
            Text("30           11     10      4"),
            Blank(),
            Section("ÁLLAPOTOK", ConsoleColor.DarkYellow),
            Text("🍖 Éhes: 30 vagy kevesebb élelemnél minden saját fizikai támadás sebzése -2, a HP-gyógyulás 75%-os. Nulla élelemnél minden csata kezdetén a max HP 5%-a elvész."),
            Text("💧 Szomjas: 30 vagy kevesebb víznél kezdeményezés -3 és találat -1. Csatakezdéskor a max manna 5%-a elvész; nulla víznél a büntetések duplázódnak."),
            Text("☠️ Mérgezés: minden saját támadási kör végén 1–4 páncélt figyelmen kívül hagyó sebzés; 6 aktiválódás után elmúlik."),
            Text("🤒 Betegség: a maximális HP és manna 80%-ra, a visszatöltésük 50%-ra csökken. Nem múlik el magától."),
            Text("🩸 Vérzés: minden saját támadási kör végén 1–3 páncélt figyelmen kívül hagyó sebzés; 4 aktiválódás után elmúlik."),
            Blank(),
            Section("BUFFOK ÉS IDŐTARTAM", ConsoleColor.Magenta),
            Text("Akció: csatában a karakter saját köre; térképen ugyanazon karakter minden 10. sikeres lépése."),
            Text("A partitagok saját akciószámlálót használnak. Az Isteni ítélet megduplázza a papi buff időtartamát."),
            Text("👻 Láthatatlanság: 3 akció; támadásig célpontvédelem, az első támadásra +5 találat."),
            Text("🛡️ Védelem: Arkán páncél +5/5; Áldás +1/4; Szent pajzs +5/4; Isteni védelem +3/4 akció."),
            Text($"{ConsoleRenderer.DamageReductionIcon} Sebzéscsökkentés: Kőbőr 50%/4; Isteni védelem 25%/4 akció. 🩸🚫 Kőbőr: vérzésvédelem/4."),
            Text("🎯 Találat: Áldás +1/4; Bátorság imája +2/5; Mézsör/Fűszeres bor +1/10 akció."),
            Text("⚔️✨ Sebzés: Bátorság imája +2/5 akció. ⚡ Kezdeményezés: Áldás +2/4; Bátorság +3/5; ital +2/10."),
            Text("✝️🛡️ Gonosz elleni védelem: +4 védelem, 30% csökkentés és állapotvédelem 5 akcióig."),
            Text("👼 Őrangyal: 5 akcióig kivédi az első halálos csapást és gyógyít; aktiváláskor elfogy."),
            Text("⛪ Szentély: 50% sebzéscsökkentés és állapotvédelem 4 akcióig; saját támadáskor megszűnik."),
            Blank(),
            Section("CSATA", ConsoleColor.Red),
            Hotkey("SPACE", "Saját körben támadás fegyverrel."),
            Hotkey("V / F1–F8", "Saját körben varázslás; a választás csak használható varázslatnál jelenik meg."),
            Hotkey("T", "Pap/Lovag élőholt ellen: csatánként egyszer Halottűzés / Szent elűzés."),
            Text("Harci varázslási kudarc: max(0, 30 - Intelligencia - Ügyesség)%; a manna és az akció elvész."),
            Text("A fegyveres találat: 1d20 + Ügyesség + módosítók az ellenfél 11 + Gyorsaság értéke ellen. A természetes 1 mindig hibázik, a természetes 20 mindig talál és kritikus."),
            Text("A csata alatt a világ ideje megáll."),
            Blank(),
            Section("OSZTÁLYTAKTIKÁK ÉS HARCI MÓDOSÍTÓK", ConsoleColor.DarkCyan),
            Text("⚔️ HARCI ALAPOK — A Harcos, Barbár és Lovag 7/10/13 Erőnél +1/+2/+3 fegyveres találatot kap. A kétkezes fegyver az ellenfél páncéljának csak a felét számítja."),
            Text("A tehetségfokozatok a 5., 15. és 25. karakterszinten választhatók. Az Alkalmazkodó ember az 1. fokozatot már a 4. szinten megkapja. Fokozatonként a felsorolt két tehetség egyikét lehet véglegesen választani."),
            ColoredText("💪🏹❤️🧠 KÉPESSÉGPONT — Minden 3. szinten egy választott képesség +1 pontot kap. Egy képesség értéke legfeljebb 13 lehet; a maximumot elért képesség nem választható.", ConsoleColor.Green),
            Blank(),
            ColoredText("⚔️ HARCOS — TAKTIKUS", ConsoleColor.Red),
            ColoredText("Az első saját kör előtt az egész csatára állást választ. Osztályjártasság: 1–4. szint +1; 5–9. +2; 10–14. +3; 15–20. +4 találat.", ConsoleColor.Yellow),
            ColoredText("🎯 Pontos: +2 találat, sebzés ×0,75.  💥 Erőteljes: -1 találat, sebzés ×1,25 és fél páncél.  🛡️ Védekező: sebzés ×0,75 és +3 védelem.", ConsoleColor.Cyan),
            ColoredText("🌟 10./20. szint: Kimért pontosság — Pontos ×0,85 | Zúzó lendület — 75% páncéltörés | Áthatolhatatlan állás — +4 védelem.", ConsoleColor.DarkCyan),
            Text("1. fokozat — 5. szint (Ember: 4.): Első csapás — +10 kezdeményezés | Robusztusság — +10 max HP."),
            Text("2. fokozat — 15. szint: Fegyvermester — +1 fegyveres találat | Rendíthetetlen — minden elszenvedett találatból -2 sebzés."),
            Text("3. fokozat — 25. szint: Acélvihar — találat után 35% eséllyel újabb támadás | Utolsó erőd — harconként egyszer 1 HP-n túléli a halálos csapást."),
            Blank(),
            ColoredText("🔥 BARBÁR — DÜH", ConsoleColor.DarkRed),
            ColoredText("Amikor először legalább 5 sebzést kap, 3 saját akcióra Dühbe kerül: támadásonként +5–10 sebzés, de -2 védelem. Osztályjártasság: 5/10/15. szinttől +1/+2/+3 találat.", ConsoleColor.Yellow),
            ColoredText("🌟 10./20. szint: Vad düh — +7–12 sebzés, -3 védelem | Kitartó düh — 5 akció, +4–7 sebzés | Vérdüh — dühös találatkor +1–3 HP.", ConsoleColor.DarkCyan),
            Text("1. fokozat — 5. szint (Ember: 4.): Vérszomj — fél HP alatt +3 sebzés | Vastag bőr — +8 max HP és +1 védelem."),
            Text("2. fokozat — 15. szint: Őrjöngés — minden egymást követő találat +1 halmozódó sebzés | Fájdalomtűrés — a 3-nál kisebb beérkező sebzés elvész."),
            Text("3. fokozat — 25. szint: Berserker düh — fél HP alatt körönként két támadás | Őserő — +20 max HP és +5 közelharci sebzés."),
            Blank(),
            ColoredText("🛡️ LOVAG — VÉDELMEZŐ", ConsoleColor.Blue),
            ColoredText("Két mezőn belüli társ csatájában 75% eséllyel közbelép: a társ első találatának teljes sebzését kivédi, a lovag pedig annak felfelé kerekített harmadát kapja. Osztályjártasság: 5/10/15. szinttől +1/+2/+3 találat; élőholt ellen Szent elűzés.", ConsoleColor.Cyan),
            ColoredText("🌟 10./20. szint: Testőr — 90% közbelépés | Márványfal — csak negyed sebzés | Megtorlás — közbelépés után a következő támadás +2 találat, +4 sebzés.", ConsoleColor.DarkCyan),
            ColoredText("Papi imákat használ, 2. szinten kapja az első imáját, legfeljebb 2. szintű imákat képes használni.", ConsoleColor.Cyan),
            Text("1. fokozat — 5. szint (Ember: 4.): Pajzsfal — pajzzsal +2 védelem | Kihívás — az ellenfél első támadása automatikusan hibázik."),
            Text("2. fokozat — 15. szint: Páncélmester — a páncéldobás legalább az átlag | Szent eskü — csatakezdéskor +10 HP."),
            Text("3. fokozat — 25. szint: Őrangyal — egyszer kivédi a halálos sebzést és +25 HP | Legyőzhetetlen — +15 max HP és -4 elszenvedett sebzés."),
            Blank(),
            ColoredText("🗡️ TOLVAJ — RAVASZ MEGKÖZELÍTÉS", ConsoleColor.DarkYellow),
            ColoredText("👁️ FELDERÍTŐ — Alap látótávja 7 az átlagos 5 helyett; elf tolvajként 8. A nagyobb látótávval messzebbről fedi fel a közös térképet és észleli az ellenfeleket.", ConsoleColor.Cyan),
            ColoredText("Az első saját kör előtt választ: 🗡️ Orvtámadás — első találat ×2 | 👁️ Megfigyelés — +2 találat | ☠️ Mérgezett penge — találatonként +1–4 méregsebzés.", ConsoleColor.Yellow),
            ColoredText("🌟 10./20. szint: Halálos rajtaütés — Orvtámadás ×2,5 | Gyenge pont — Megfigyelésnél a természetes 19 is kritikus | Erős méreg — +2–6 sebzés.", ConsoleColor.DarkCyan),
            Text("1. fokozat — 5. szint (Ember: 4.): Orvtámadás — az első találat ×2 | Kitérés — 15% eséllyel elkerüli a nem kritikus találatot."),
            Text("2. fokozat — 15. szint: Méregkeverő — találatonként +1–6 méregsebzés | Árnyéklépés — sikeres kitérés után a következő támadás automatikusan talál."),
            Text("3. fokozat — 25. szint: Halálos pontosság — természetes 18–20-nál ×3 sebzés | Mestertolvaj — dupla ládaarany és 25% ritkatárgy-esély."),
            Blank(),
            ColoredText("✝️ PAP — ISTENI SZOLGÁLAT", ConsoleColor.White),
            ColoredText("Papi imákat használ, élőholt ellen csatánként egyszer Halottűzést végezhet. Az 1. tehetségfokozat megszerzésekor végleges specializációt is választ.", ConsoleColor.Cyan),
            ColoredText("💚 Élet: minden gyógyítás +25% | 🛡️ Védelem: a védőimák +1 akcióig tartanak | ⚖️ Ítélet: minden papi sebző varázslat +20%.", ConsoleColor.Green),
            ColoredText("🌟 10./20. szint: Túláradó élet — további +15% gyógyítás | Rendíthetetlen oltalom — védőimák +1 akció | Irgalmas ítélet — varázssebzés 10%-a gyógyít.", ConsoleColor.DarkCyan),
            Text("1. fokozat — 5. szint (Ember: 4.): Gyógyító kegyelem — gyógyítás ×1,25 | Áldott fegyver — élőholt ellen +2 találat és +2 sebzés."),
            Text("2. fokozat — 15. szint: Szentély — 20% eséllyel elvész az ellenfél támadása | Hitforrás — +12 max manna és csatánként +5 manna."),
            Text("3. fokozat — 25. szint: Feltámadás — pályánként egyszer teljes HP-val visszatér | Isteni ítélet — minden ötödik papi varázslat kétszeres és ingyenes."),
            Blank(),
            ColoredText("🔮 MÁGUS — ARKÁN MÁGIA", ConsoleColor.Magenta),
            ColoredText("Mágusigéket használ; a mágikus találatot és a közös varázslási kudarcot a saját képességei módosítják. Az 1. tehetségfokozat megszerzésekor végleges specializációt is választ.", ConsoleColor.Cyan),
            ColoredText("🔥 Elementalista: közvetlen arkán sebzés +20% | 🎭 Illuzionista: kontroll- és védőigék +1 akcióig tartanak | 💀 Nekromanta: a közvetlen varázssebzés 10%-a visszagyógyul.", ConsoleColor.Magenta),
            ColoredText("🌟 10./20. szint: Tomboló elemek — további +15% arkán sebzés | Tökéletes illúzió — kontroll/védelem +1 akció | Életaratás — sebzés 15%-a gyógyít.", ConsoleColor.DarkCyan),
            Text("1. fokozat — 5. szint (Ember: 4.): Arkán fókusz — +2 mágikus találat | Mannatartalék — +15 max manna."),
            Text("2. fokozat — 15. szint: Elemi mester — sebző varázslatok ×1,25 | Mágikus pajzs — a sebzés 25%-át manna nyeli el."),
            Text("3. fokozat — 25. szint: Láncvarázslat — 30% eséllyel ingyen megismétlődik | Főmágus — +25 max manna és -2 mannaköltség."),
            Blank(),
            Section("FEGYVERJÁRTASSÁGOK", ConsoleColor.Yellow),
            Text("A Harcos, Barbár és Lovag karakteralkotáskor, majd a 7., 17. és 27. szinten kap egy jártassági lépést. A Tolvaj, Pap és Mágus a 7. és 17. szinten kap lépést."),
            Text("Egy karakter legfeljebb két fegyvercsaládot tanulhat. Egy lépés új családban Jártas fokot nyit, vagy egy Jártas családot Mester fokra emel. A járatlan fegyver büntetés nélkül használható."),
            ColoredText("🗡️ Tőr — Jártas: +2 kezdeményezés, +1 sebzés | Mester: természetes 19–20 esetén ×2 kritikus.", ConsoleColor.DarkYellow),
            ColoredText("⚔️ Kard — Jártas: +1 találat | Mester: felszerelt karddal +1 védelem.", ConsoleColor.Cyan),
            ColoredText("🪓 Bárd — Jártas: +2 sebzés | Mester: természetes 20 esetén ×3 kritikus.", ConsoleColor.Red),
            ColoredText("🔨 Zúzófegyver — Jártas: -2 ellenséges páncél | Mester: összesen -4 páncél. A fix csökkentés a páncélfelezés után történik.", ConsoleColor.DarkYellow),
            ColoredText("🔱 Szálfegyver — Jártas: +3 kezdeményezés | Mester: a csata első sikeres találata ×1,5 sebzés.", ConsoleColor.Green),
            ColoredText("🛡️ Pajzs — Jártas: +1 védelem | Mester: a pajzs védelmi dobása kétszer történik, és a jobb eredmény számít.", ConsoleColor.Blue)
        };
        ShowScrollableHelp(source);
    }

    private sealed record HelpSourceLine(string Text, ConsoleColor Color, string? Key = null);
    private sealed record HelpRenderLine(string? Key, string Text, ConsoleColor Color);

    private static HelpSourceLine Section(string text, ConsoleColor color) => new(text, color);
    private static HelpSourceLine Text(string text) => new(text, ConsoleColor.Gray);
    private static HelpSourceLine ColoredText(string text, ConsoleColor color) => new(text, color);
    private static HelpSourceLine Hotkey(string key, string text) => new(text, ConsoleColor.Gray, key);
    private static HelpSourceLine Blank() => Text(string.Empty);

    private static void ShowScrollableHelp(IReadOnlyList<HelpSourceLine> source)
    {
        var offset = 0;
        while (true)
        {
            var width = Math.Max(30, Math.Min(122, Console.WindowWidth - 4));
            var height = Math.Max(8, Console.WindowHeight - 4);
            var contentWidth = width - 4;
            var style = WindowFrameConfiguration.For(FramedWindow.Help);
            var keyColumnWidth = Math.Min(20, Math.Max(12, contentWidth / 3));
            var lines = source.SelectMany(entry => WrapHelpLine(entry, contentWidth, keyColumnWidth)).ToArray();
            var pageSize = Math.Max(1, height - 5);
            offset = Math.Clamp(offset, 0, Math.Max(0, lines.Length - pageSize));
            var left = Math.Max(0, (Console.WindowWidth - width) / 2);
            var top = Math.Max(0, (Console.WindowHeight - height) / 2);
            ResetConsole();
            WriteAt(left, top, WindowFrameCatalog.Horizontal(style, width), ConsoleColor.Magenta, width);
            var interiorRows = height - 2;
            for (var row = 0; row < interiorRows; row++)
            {
                var sides = WindowFrameCatalog.Sides(style, row, interiorRows);
                WriteAt(left, top + row + 1, sides.Left, ConsoleColor.Magenta, sides.Left.Length);
                WriteAt(left + sides.Left.Length, top + row + 1, string.Empty, ConsoleColor.Gray,
                    width - sides.Left.Length - sides.Right.Length);
                WriteAt(left + width - sides.Right.Length, top + row + 1, sides.Right,
                    ConsoleColor.Magenta, sides.Right.Length);
            }
            WriteAt(left + 2, top + 1, "◆ SÚGÓ ◆", ConsoleColor.Yellow, contentWidth);
            for (var row = 0; row < pageSize; row++)
            {
                var line = offset + row < lines.Length
                    ? lines[offset + row]
                    : new HelpRenderLine(null, string.Empty, ConsoleColor.Gray);
                if (line.Key is not null)
                {
                    WriteAt(left + 2, top + row + 2, PadRightDisplay(line.Key, keyColumnWidth),
                        ConsoleColor.Yellow, keyColumnWidth);
                    WriteAt(left + 2 + keyColumnWidth, top + row + 2, line.Text, line.Color,
                        contentWidth - keyColumnWidth);
                }
                else
                {
                    WriteAt(left + 2, top + row + 2, line.Text, line.Color, contentWidth);
                }
            }
            var status = $" ↑↓ görget  PgUp/PgDn lapoz  Home/End  Esc/Enter vissza  {offset + 1}-{Math.Min(lines.Length, offset + pageSize)}/{lines.Length} ";
            WriteAt(left + 2, top + height - 2, TruncateByDisplayWidth(status, width - 4), ConsoleColor.DarkYellow, width - 4);
            WriteAt(left, top + height - 1, WindowFrameCatalog.Horizontal(style, width, bottom: true),
                ConsoleColor.Magenta, width);

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

    private static IEnumerable<HelpRenderLine> WrapHelpLine(
        HelpSourceLine entry, int contentWidth, int keyColumnWidth)
    {
        if (entry.Key is null)
        {
            return WrapHelpText(entry.Text, contentWidth)
                .Select(text => new HelpRenderLine(null, text, entry.Color));
        }

        var descriptionWidth = Math.Max(1, contentWidth - keyColumnWidth);
        return WrapHelpText(entry.Text, descriptionWidth)
            .Select((text, index) => new HelpRenderLine(index == 0 ? entry.Key : string.Empty, text, entry.Color));
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
            "8) Beállítások",
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
        var top = Math.Min(SideMenuTop, Math.Max(0, Console.WindowHeight - 3));
        var style = WindowFrameConfiguration.For(FramedWindow.MainMenu);
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.SetCursorPosition(left, top);
        Console.Write(WindowFrameCatalog.Horizontal(style, SideMenuWidth));
        var visibleLineCount = Math.Min(lines.Count, Math.Max(1, Console.WindowHeight - top - 2));
        for (var i = 0; i < visibleLineCount; i++)
        {
            var line = lines[i] ?? string.Empty;
            var sides = WindowFrameCatalog.Sides(style, i, visibleLineCount);
            var interiorWidth = SideMenuWidth - sides.Left.Length - sides.Right.Length;
            var contentWidth = Math.Max(0, interiorWidth - 2);
            var content = TruncateByDisplayWidth(line, contentWidth);
            Console.SetCursorPosition(left, top + i + 1);
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write(sides.Left + " ");
            Console.ForegroundColor = MainMenuLineColor(line);
            Console.Write(PadRightDisplay(content, contentWidth));
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write(" " + sides.Right);
        }
        Console.SetCursorPosition(left, top + visibleLineCount + 1);
        Console.Write(WindowFrameCatalog.Horizontal(style, SideMenuWidth, bottom: true));
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
        if (line.StartsWith("Aktív", StringComparison.Ordinal)) return ConsoleColor.Yellow;
        if (line.StartsWith("1)", StringComparison.Ordinal)) return ConsoleColor.DarkGreen;
        if (line.StartsWith("2)", StringComparison.Ordinal)) return ConsoleColor.Green;
        if (line.StartsWith("3)", StringComparison.Ordinal)) return ConsoleColor.Cyan;
        if (line.StartsWith("4)", StringComparison.Ordinal)) return ConsoleColor.Blue;
        if (line.StartsWith("8)", StringComparison.Ordinal)) return ConsoleColor.Cyan;
        if (line.Contains("COOP", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("host", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Csatlakozás", StringComparison.OrdinalIgnoreCase)) return ConsoleColor.DarkYellow;
        if (line.StartsWith("──", StringComparison.Ordinal)) return ConsoleColor.DarkMagenta;
        if (line.StartsWith("Esc", StringComparison.Ordinal)) return ConsoleColor.DarkYellow;
        return ConsoleColor.Gray;
    }
}
