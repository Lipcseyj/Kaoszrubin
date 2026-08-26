using MazeGame.Application;
using MazeGame.Domain.Characters;
using MazeGame.Domain.Inventory;
using MazeGame.Transport.SignalR;

namespace MazeGame.UI;

/// <summary>Az első LAN vertical slice vendégoldali, snapshotból rajzoló konzolképernyője.</summary>
public sealed class CoopGuestScreen
{
    private readonly string _applicationVersion;
    private readonly string _catalogHash;
    private int _redrawRequested = 1;
    private string? _message;
    private bool _inventoryOpen;
    private int _inventorySelection;
    private InventorySlotAddress? _inventorySource;
    private GuestRenderFrame? _lastFrame;

    public CoopGuestScreen(string applicationVersion, string catalogHash)
    {
        _applicationVersion = applicationVersion;
        _catalogHash = catalogHash;
    }

    public async Task RunAsync(string hostUrl, string displayName, CancellationToken cancellationToken = default)
    {
        await using var client = new CoopSignalRClient(hostUrl, _applicationVersion, _catalogHash, displayName);
        client.SnapshotChanged += _ => Interlocked.Exchange(ref _redrawRequested, 1);
        client.ConnectionStateChanged += _ => Interlocked.Exchange(ref _redrawRequested, 1);
        client.ProtocolErrorReceived += error => SetMessage($"Protokollhiba: {error.Message}");
        client.CommandRejected += rejected => SetMessage($"A host elutasította: {rejected.Reason}");

        var hello = await client.ConnectAsync(cancellationToken);
        var options = hello.AvailableCharacters ?? [];
        if (options.Count == 0)
            throw new InvalidOperationException("A host partijában nincs átvehető NPC karakter.");
        var selected = ChooseCharacter(options);
        if (selected is null) return;
        var control = await client.RequestCharacterControlAsync(selected.CharacterId, cancellationToken);
        if (!control.Accepted)
            throw new InvalidOperationException(control.RejectionReason ?? "A karakter átvételét a host elutasította.");

        Console.CursorVisible = false;
        try
        {
            while (!cancellationToken.IsCancellationRequested &&
                   client.State is not (CoopClientConnectionState.Disconnected or CoopClientConnectionState.Faulted))
            {
                if (Interlocked.Exchange(ref _redrawRequested, 0) != 0)
                    Draw(client, selected);
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    if (key.Key == ConsoleKey.Escape) break;
                    await HandleInputAsync(client, selected.CharacterId, key.Key, cancellationToken);
                }
                await Task.Delay(20, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            Console.CursorVisible = true;
            await client.DisconnectAsync(CancellationToken.None);
        }
    }

    private static CoopCharacterOption? ChooseCharacter(IReadOnlyList<CoopCharacterOption> options)
    {
        var selectedIndex = 0;
        while (true)
        {
            ResetConsole();
            WriteLine("=== COOP KARAKTERVÁLASZTÁS ===", ConsoleColor.Yellow);
            WriteLine("Fel/le: választás | Enter: átvétel | Esc: vissza", ConsoleColor.DarkCyan);
            Console.WriteLine();
            for (var index = 0; index < options.Count; index++)
            {
                var option = options[index];
                WriteLine($"{(index == selectedIndex ? ">" : " ")} {option.Name} — " +
                          $"{option.CharacterClassName}, {option.Level}. szint",
                    index == selectedIndex ? ConsoleColor.Cyan : ConsoleColor.Gray);
            }
            switch (Console.ReadKey(intercept: true).Key)
            {
                case ConsoleKey.UpArrow:
                    selectedIndex = (selectedIndex - 1 + options.Count) % options.Count;
                    break;
                case ConsoleKey.DownArrow:
                    selectedIndex = (selectedIndex + 1) % options.Count;
                    break;
                case ConsoleKey.Enter:
                    return options[selectedIndex];
                case ConsoleKey.Escape:
                    return null;
            }
        }
    }

    private async Task HandleInputAsync(CoopSignalRClient client, CharacterId characterId, ConsoleKey key,
        CancellationToken cancellationToken)
    {
        if (client.State != CoopClientConnectionState.Connected) return;
        GameCommand? command = null;
        var snapshot = client.CurrentSnapshot;
        if (snapshot is null) return;
        var ownsCharacter = snapshot.CharacterControls.Any(control =>
            control.CharacterId == characterId && control.AssignedPlayerId == client.PlayerId &&
            control.ConnectionState == PlayerConnectionState.Connected);
        if (!ownsCharacter)
        {
            if (_inventoryOpen) CloseInventory();
            return;
        }
        if (_inventoryOpen && snapshot.Phase is not (GameSessionPhase.Exploration or GameSessionPhase.Inn))
            CloseInventory();
        if (_inventoryOpen)
        {
            await HandleInventoryInputAsync(client, characterId, snapshot, key, cancellationToken);
            return;
        }
        if (snapshot.Battle is { } battle && battle.ActingCharacterId == characterId)
        {
            var action = key switch
            {
                ConsoleKey.Spacebar => BattleActionKind.PhysicalAttack,
                ConsoleKey.T => BattleActionKind.TurnUndead,
                _ => (BattleActionKind?)null
            };
            if (action is not null)
                command = new BattleActionCommand(client.PlayerId!.Value, client.NextCommandId(), characterId,
                    battle.BattleId, battle.TurnId, action.Value);
        }
        else if (snapshot.Phase is (GameSessionPhase.Exploration or GameSessionPhase.Inn) &&
                 GameInputBindings.IsCharacterSheetToggle(key))
        {
            _inventoryOpen = true;
            _inventorySelection = 0;
            _inventorySource = null;
            Interlocked.Exchange(ref _redrawRequested, 1);
        }
        else if (snapshot.Phase == GameSessionPhase.Exploration && GameInputBindings.CharacterAction(key) is { } action)
        {
            command = new CharacterActionCommand(client.PlayerId!.Value, client.NextCommandId(), characterId,
                action);
        }
        else if (snapshot.Phase == GameSessionPhase.Exploration && TryGetDirection(key, out var direction))
        {
            command = new MoveCharacterCommand(client.PlayerId!.Value, client.NextCommandId(), characterId, direction);
        }
        if (command is null) return;
        try
        {
            await client.SendCommandAsync(command, cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or TimeoutException)
        {
            SetMessage(exception.Message);
        }
    }

    private async Task HandleInventoryInputAsync(CoopSignalRClient client, CharacterId characterId,
        SessionSnapshot snapshot, ConsoleKey key, CancellationToken cancellationToken)
    {
        var own = snapshot.Party.FirstOrDefault(character => character.CharacterId == characterId);
        var inventory = own?.Inventory;
        if (inventory is null)
        {
            CloseInventory();
            return;
        }
        var slots = inventory.Slots;
        _inventorySelection = Math.Clamp(_inventorySelection, 0, Math.Max(0, slots.Count - 1));
        GameCommand? command = null;
        if (GameInputBindings.IsCharacterSheetToggle(key))
        {
            CloseInventory();
            return;
        }
        switch (GameInputBindings.InventoryAction(key))
        {
            case InventoryInputAction.MoveUp when slots.Count > 0:
                _inventorySelection = (_inventorySelection - 1 + slots.Count) % slots.Count;
                break;
            case InventoryInputAction.MoveDown when slots.Count > 0:
                _inventorySelection = (_inventorySelection + 1) % slots.Count;
                break;
            case InventoryInputAction.MoveItem when slots.Count > 0:
                var selected = slots[_inventorySelection];
                if (_inventorySource is null)
                {
                    if (selected.Item is null)
                        SetMessage("Először egy tárgyat tartalmazó forrásslotot jelölj ki.");
                    else
                        _inventorySource = new InventorySlotAddress(selected.Kind, selected.Index);
                }
                else
                {
                    command = new InventoryTransferCommand(client.PlayerId!.Value, client.NextCommandId(),
                        characterId, inventory.Revision, _inventorySource.Value.Kind, _inventorySource.Value.Index,
                        characterId, inventory.Revision, selected.Kind, selected.Index);
                    _inventorySource = null;
                }
                break;
            case InventoryInputAction.Use when slots.Count > 0:
                var useSlot = slots[_inventorySelection];
                if (useSlot.Kind == InventorySlotKind.Backpack && useSlot.Item is not null)
                    command = new UseInventoryItemCommand(client.PlayerId!.Value, client.NextCommandId(),
                        characterId, inventory.Revision, useSlot.Index);
                else
                    SetMessage("Használható tárgyat a hátizsákban jelölj ki.");
                break;
            case InventoryInputAction.Drop when slots.Count > 0 && snapshot.Phase == GameSessionPhase.Exploration:
                var dropSlot = slots[_inventorySelection];
                if (dropSlot.Item is not null)
                    command = new DropInventoryItemCommand(client.PlayerId!.Value, client.NextCommandId(),
                        characterId, inventory.Revision, dropSlot.Kind, dropSlot.Index);
                else
                    SetMessage("Az üres slot nem dobható el.");
                break;
            case InventoryInputAction.Inspect when slots.Count > 0:
                var inspectSlot = slots[_inventorySelection];
                SetMessage(inspectSlot.Item is null
                    ? "A kijelölt hely üres."
                    : $"{inspectSlot.Item.Name} [{inspectSlot.Item.DefinitionId}] — " +
                      $"ritkaság: {inspectSlot.Item.Rarity}; mágikus erő: {inspectSlot.Item.MagicPower}; " +
                      $"alapár: {inspectSlot.Item.BasePrice}. {inspectSlot.Item.Description}");
                break;
        }
        Interlocked.Exchange(ref _redrawRequested, 1);
        if (command is null) return;
        try
        {
            await client.SendCommandAsync(command, cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or TimeoutException)
        {
            SetMessage(exception.Message);
        }
    }

    private void CloseInventory()
    {
        _inventoryOpen = false;
        _inventorySource = null;
        Interlocked.Exchange(ref _redrawRequested, 1);
    }

    private void Draw(CoopSignalRClient client, CoopCharacterOption selected)
    {
        var snapshot = client.CurrentSnapshot;
        if (snapshot?.World is not { } world)
        {
            ResetConsole();
            WriteLine($"=== COOP VENDÉG — {selected.Name} ===", ConsoleColor.Yellow);
            WriteLine("Várakozás a host első snapshotjára…", ConsoleColor.DarkCyan);
            _lastFrame = null;
            return;
        }

        var frame = BuildFrame(client, selected, snapshot, world);
        RenderFrame(frame, _lastFrame);
        _lastFrame = frame;
    }

    private GuestRenderFrame BuildFrame(CoopSignalRClient client, CoopCharacterOption selected,
        SessionSnapshot snapshot, WorldSnapshot world)
    {
        var windowWidth = SafeWindowWidth();
        var windowHeight = SafeWindowHeight();
        var mapWidth = Math.Min(world.Width, Math.Max(1, windowWidth - CharacterSheetPanel.Width - 4));
        var mapHeight = Math.Min(Math.Max(world.Height, 37), Math.Max(1, windowHeight - 7));
        var grid = new GuestMapCell[mapWidth, mapHeight];
        for (var y = 0; y < mapHeight; y++)
            for (var x = 0; x < mapWidth; x++)
                grid[x, y] = y < world.Height
                    ? new GuestMapCell("░", ConsoleColor.DarkGray)
                    : new GuestMapCell(" ", ConsoleColor.Gray);
        foreach (var cell in world.RevealedCells)
            Put(grid, cell.Position, char.ConvertFromUtf32(cell.TileCodePoint), ConsoleColor.Gray);
        foreach (var enemy in world.Enemies) Put(grid, enemy.Position, "e", ConsoleColor.Red);
        foreach (var chest in world.Chests) Put(grid, chest.Position, "$", ConsoleColor.Yellow);
        foreach (var corpse in world.Corpses) Put(grid, corpse.Position, "%", ConsoleColor.DarkRed);
        foreach (var pile in world.GroundPiles) Put(grid, pile.Position, "◆", ConsoleColor.Cyan);
        foreach (var character in snapshot.Party.Where(character => character.Position is not null))
            Put(grid, character.Position!.Value, CharacterSheetPanel.CharacterClassGlyph(character.CharacterClassId),
                character.Color);

        var stillControlled = snapshot.CharacterControls.Any(control =>
            control.CharacterId == selected.CharacterId && control.AssignedPlayerId == client.PlayerId &&
            control.ConnectionState == PlayerConnectionState.Connected);
        var own = snapshot.Party.FirstOrDefault(character => character.CharacterId == selected.CharacterId);
        var panelLines = own?.CharacterSheet is not null && own.Inventory is not null
            ? CharacterSheetPanel.Build(own, snapshot.MazeLevel, snapshot.GoldenKeyCount, snapshot.BossKeyCount)
                .ToDictionary(line => line.Row)
            : [];
        var selectedSlot = _inventoryOpen && own?.Inventory is { Slots.Count: > 0 } inventory
            ? new InventorySlotAddress(inventory.Slots[Math.Clamp(_inventorySelection, 0, inventory.Slots.Count - 1)].Kind,
                inventory.Slots[Math.Clamp(_inventorySelection, 0, inventory.Slots.Count - 1)].Index)
            : (InventorySlotAddress?)null;
        var panel = new GuestTextLine[mapHeight];
        for (var y = 0; y < mapHeight; y++)
        {
            if (panelLines.TryGetValue(y, out var line))
            {
                var marker = line.InventorySlot == _inventorySource ? "*" : " ";
                panel[y] = new GuestTextLine(marker + line.Text, line.Color,
                    line.InventorySlot is not null && line.InventorySlot == selectedSlot
                        ? ConsoleColor.DarkCyan
                        : ConsoleColor.Black);
            }
            else
                panel[y] = new GuestTextLine(string.Empty, ConsoleColor.Gray, ConsoleColor.Black);
        }

        var groundItems = string.Empty;
        if (_inventoryOpen)
        {
            var pile = own?.Position is { } position
                ? world.GroundPiles.FirstOrDefault(candidate => candidate.Position == position)
                : null;
            if (pile is not null)
                groundItems = "A földön: " + string.Join(", ", pile.Items.Select(item => item.Name +
                    (item.Charges > 0 ? $" ({item.Charges}/{item.MaximumCharges})" : string.Empty)));
        }

        return new GuestRenderFrame(snapshot.MazeLevel, windowWidth, windowHeight, mapWidth, mapHeight, grid, panel,
        [
            new GuestTextLine($"=== COOP VENDÉG — {selected.Name} ===", ConsoleColor.Yellow, ConsoleColor.Black),
            new GuestTextLine($"Kapcsolat: {client.State} | Tab: térkép/inventory | Nyilak: mozgás | N/Z/K: ajtó/keresés | Harc: Space/T",
                client.State == CoopClientConnectionState.Connected ? ConsoleColor.Green : ConsoleColor.DarkYellow,
                ConsoleColor.Black),
            new GuestTextLine($"{snapshot.MazeLevel}. pálya — {snapshot.LevelName} — {snapshot.Phase}",
                ConsoleColor.Cyan, ConsoleColor.Black),
            new GuestTextLine(stillControlled ? string.Empty :
                "Megfigyelő mód: a karaktered már nincs a vezérlésed alatt.", ConsoleColor.DarkYellow,
                ConsoleColor.Black)
        ],
        [
            new GuestTextLine(_inventoryOpen
                ? "Inventory fókusz — Fel/le | Enter: használ | D: eldob | Space: mozgat | I: vizsgál | Tab: térkép"
                : string.Empty, ConsoleColor.Green, ConsoleColor.Black),
            new GuestTextLine(groundItems, ConsoleColor.Yellow, ConsoleColor.Black),
            new GuestTextLine(_message ?? string.Empty, ConsoleColor.DarkYellow, ConsoleColor.Black)
        ]);
    }

    private static void RenderFrame(GuestRenderFrame frame, GuestRenderFrame? previous)
    {
        var fullRedraw = previous is null || previous.MazeLevel != frame.MazeLevel ||
                         previous.WindowWidth != frame.WindowWidth || previous.WindowHeight != frame.WindowHeight ||
                         previous.MapWidth != frame.MapWidth || previous.MapHeight != frame.MapHeight;
        if (fullRedraw) ResetConsole();

        for (var row = 0; row < frame.Headers.Length; row++)
            if (fullRedraw || previous!.Headers[row] != frame.Headers[row])
                WriteAt(0, row, frame.Headers[row], Math.Max(1, frame.WindowWidth - 1));

        const int mapTop = 4;
        for (var y = 0; y < frame.MapHeight; y++)
        {
            for (var x = 0; x < frame.MapWidth; x++)
                if (fullRedraw || previous!.Map[x, y] != frame.Map[x, y])
                    WriteMapCell(x, mapTop + y, frame.Map[x, y]);
            if (fullRedraw)
                WriteAt(frame.MapWidth, mapTop + y,
                    new GuestTextLine(" │ ", ConsoleColor.Gray, ConsoleColor.Black), 3);
            if (fullRedraw || previous!.Panel[y] != frame.Panel[y])
                WriteAt(frame.MapWidth + 3, mapTop + y, frame.Panel[y], CharacterSheetPanel.Width);
        }

        for (var row = 0; row < frame.Footers.Length; row++)
            if (fullRedraw || previous!.Footers[row] != frame.Footers[row])
                WriteAt(0, mapTop + frame.MapHeight + row, frame.Footers[row],
                    Math.Max(1, frame.WindowWidth - 1));

        Console.ResetColor();
        TrySetCursorPosition(0, Math.Min(frame.WindowHeight - 1, mapTop + frame.MapHeight + frame.Footers.Length));
    }

    private static void WriteMapCell(int x, int y, GuestMapCell cell)
    {
        if (!TrySetCursorPosition(x, y)) return;
        Console.ForegroundColor = cell.Color;
        Console.BackgroundColor = ConsoleColor.Black;
        Console.Write(cell.Glyph);
    }

    private static void WriteAt(int x, int y, GuestTextLine line, int width)
    {
        if (!TrySetCursorPosition(x, y)) return;
        Console.ForegroundColor = line.Foreground;
        Console.BackgroundColor = line.Background;
        Console.Write(FitConsoleLine(line.Text, width));
    }

    private void SetMessage(string message)
    {
        _message = message;
        Interlocked.Exchange(ref _redrawRequested, 1);
    }

    private static bool TryGetDirection(ConsoleKey key, out Direction direction)
    {
        direction = key switch
        {
            ConsoleKey.UpArrow => Direction.Up,
            ConsoleKey.DownArrow => Direction.Down,
            ConsoleKey.LeftArrow => Direction.Left,
            ConsoleKey.RightArrow => Direction.Right,
            _ => default
        };
        return key is ConsoleKey.UpArrow or ConsoleKey.DownArrow or ConsoleKey.LeftArrow or ConsoleKey.RightArrow;
    }

    private static void Put(GuestMapCell[,] grid, Position position, string value, ConsoleColor color)
    {
        if (position.X >= 0 && position.X < grid.GetLength(0) && position.Y >= 0 && position.Y < grid.GetLength(1))
            grid[position.X, position.Y] = new GuestMapCell(value, color);
    }

    private static bool TrySetCursorPosition(int x, int y)
    {
        try { Console.SetCursorPosition(x, y); return true; }
        catch (ArgumentOutOfRangeException) { return false; }
        catch (IOException) { return false; }
    }

    private static int SafeWindowWidth()
    {
        try { return Console.WindowWidth; } catch (IOException) { return 80; }
    }

    private static int SafeWindowHeight()
    {
        try { return Console.WindowHeight; } catch (IOException) { return 30; }
    }

    private static void ResetConsole(bool clear = true)
    {
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.BackgroundColor = ConsoleColor.Black;
        if (clear) Console.Clear();
        else Console.SetCursorPosition(0, 0);
    }

    private static void WriteLine(string text, ConsoleColor color, ConsoleColor background = ConsoleColor.Black)
    {
        Console.ForegroundColor = color;
        Console.BackgroundColor = background;
        Console.WriteLine(FitConsoleLine(text, Math.Max(1, SafeWindowWidth() - 1)));
        Console.ResetColor();
    }

    private static string FitConsoleLine(string text, int width)
    {
        if (text.Length > width) text = text[..width];
        return text.PadRight(width);
    }

    private readonly record struct GuestMapCell(string Glyph, ConsoleColor Color);
    private readonly record struct GuestTextLine(string Text, ConsoleColor Foreground, ConsoleColor Background);
    private sealed record GuestRenderFrame(int MazeLevel, int WindowWidth, int WindowHeight, int MapWidth,
        int MapHeight, GuestMapCell[,] Map, GuestTextLine[] Panel, GuestTextLine[] Headers,
        GuestTextLine[] Footers);
}
