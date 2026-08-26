using MazeGame.Application;
using MazeGame.Data;
using MazeGame.Domain.Characters;
using MazeGame.Domain.Inventory;
using MazeGame.Domain.Magic;
using MazeGame.Transport.SignalR;

namespace MazeGame.UI;

/// <summary>Az első LAN vertical slice vendégoldali, snapshotból rajzoló konzolképernyője.</summary>
public sealed class CoopGuestScreen
{
    private readonly string _applicationVersion;
    private readonly string _catalogHash;
    private readonly GameDataCatalog _gameData;
    private int _redrawRequested = 1;
    private const int MessageLineCount = 5;
    private readonly Queue<GuestTextLine> _messageLog = new();
    private bool _inventoryOpen;
    private int _inventorySelection;
    private InventorySlotAddress? _inventorySource;
    private bool _battleSpellMenuOpen;
    private int _battleSpellSelection;
    private BattleSpellOption? _targetedBattleSpell;
    private Position? _spellTargetCursor;
    private GuestRenderFrame? _lastFrame;

    public CoopGuestScreen(string applicationVersion, string catalogHash, GameDataCatalog gameData)
    {
        _applicationVersion = applicationVersion;
        _catalogHash = catalogHash;
        _gameData = gameData ?? throw new ArgumentNullException(nameof(gameData));
    }

    public async Task RunAsync(string hostUrl, string displayName, LiveCharacter localCharacter,
        string characterData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(localCharacter);
        await using var client = new CoopSignalRClient(hostUrl, _applicationVersion, _catalogHash, displayName);
        client.SnapshotChanged += _ => Interlocked.Exchange(ref _redrawRequested, 1);
        client.ConnectionStateChanged += _ => Interlocked.Exchange(ref _redrawRequested, 1);
        client.ProtocolErrorReceived += error => SetMessage($"Protokollhiba: {error.Message}");
        client.CommandRejected += rejected => SetMessage($"A host elutasította: {rejected.Reason}");

        await client.ConnectAsync(cancellationToken);
        var selected = new CoopCharacterOption(localCharacter.Id, localCharacter.Name,
            localCharacter.CharacterClass.Name, localCharacter.Level);
        var control = await client.JoinCharacterAsync(characterData, cancellationToken);
        if (!control.Accepted)
            throw new InvalidOperationException(control.RejectionReason ?? "A karakter belépését a host elutasította.");
        if (control.CharacterId != localCharacter.Id)
            throw new InvalidOperationException("A host nem a kiválasztott helyi karaktert regisztrálta.");

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
        SynchronizeBattleSpellUi(snapshot, characterId);
        if (_battleSpellMenuOpen)
        {
            command = HandleBattleSpellMenuInput(client, characterId, snapshot, key);
            if (command is not null)
            {
                try { await client.SendCommandAsync(command, cancellationToken); }
                catch (Exception exception) when (exception is InvalidOperationException or TimeoutException)
                {
                    SetMessage(exception.Message);
                }
            }
            return;
        }
        if (_targetedBattleSpell is not null)
        {
            command = HandleBattleSpellTargetInput(client, characterId, snapshot, key);
            if (command is null) return;
        }
        else
        if (_inventoryOpen)
        {
            await HandleInventoryInputAsync(client, characterId, snapshot, key, cancellationToken);
            return;
        }
        else if (snapshot.Battle is { } battle && battle.ActingCharacterId == characterId)
        {
            if (key == ConsoleKey.V && battle.AllowedActions.Contains(BattleActionKind.CastSpell))
            {
                _battleSpellMenuOpen = true;
                _battleSpellSelection = 0;
                Interlocked.Exchange(ref _redrawRequested, 1);
                return;
            }
            if (TryGetFunctionKeyIndex(key, out var quickSlot) &&
                battle.SpellOptions?.FirstOrDefault(option => option.QuickSlot == quickSlot) is { } quickSpell)
            {
                command = BeginBattleSpellTargeting(client, characterId, battle, quickSpell);
                if (command is null) return;
            }
            else
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

    private void SynchronizeBattleSpellUi(SessionSnapshot snapshot, CharacterId characterId)
    {
        if (snapshot.Battle is { } battle && battle.ActingCharacterId == characterId) return;
        _battleSpellMenuOpen = false;
        _targetedBattleSpell = null;
        _spellTargetCursor = null;
    }

    private GameCommand? HandleBattleSpellMenuInput(CoopSignalRClient client, CharacterId characterId,
        SessionSnapshot snapshot, ConsoleKey key)
    {
        var options = snapshot.Battle?.SpellOptions ?? [];
        if (key == ConsoleKey.Escape)
        {
            _battleSpellMenuOpen = false;
        }
        else if (options.Count > 0 && key == ConsoleKey.UpArrow)
            _battleSpellSelection = (_battleSpellSelection - 1 + options.Count) % options.Count;
        else if (options.Count > 0 && key == ConsoleKey.DownArrow)
            _battleSpellSelection = (_battleSpellSelection + 1) % options.Count;
        else if (options.Count > 0 && key == ConsoleKey.Enter)
        {
            _battleSpellMenuOpen = false;
            var option = options[Math.Clamp(_battleSpellSelection, 0, options.Count - 1)];
            if (option.ValidTargets.Count == 0)
                SetMessage("A varázslatnak jelenleg nincs érvényes célpontja.", ConsoleColor.Red);
            else
            {
                Interlocked.Exchange(ref _redrawRequested, 1);
                return BeginBattleSpellTargeting(client, characterId, snapshot.Battle!, option);
            }
        }
        Interlocked.Exchange(ref _redrawRequested, 1);
        return null;
    }

    private GameCommand? HandleBattleSpellTargetInput(CoopSignalRClient client, CharacterId characterId,
        SessionSnapshot snapshot, ConsoleKey key)
    {
        var battle = snapshot.Battle!;
        var spell = _targetedBattleSpell!;
        if (key == ConsoleKey.Escape)
        {
            _targetedBattleSpell = null;
            _spellTargetCursor = null;
            _battleSpellMenuOpen = true;
            Interlocked.Exchange(ref _redrawRequested, 1);
            return null;
        }
        if (key == ConsoleKey.Tab && spell.ValidTargets.Count > 0)
        {
            var index = spell.ValidTargets.ToList().IndexOf(_spellTargetCursor!.Value);
            _spellTargetCursor = spell.ValidTargets[(index + 1 + spell.ValidTargets.Count) % spell.ValidTargets.Count];
            Interlocked.Exchange(ref _redrawRequested, 1);
            return null;
        }
        if (TryGetDirection(key, out var direction))
        {
            var next = _spellTargetCursor!.Value + direction;
            if (next.X >= 0 && next.Y >= 0 && next.X < snapshot.World!.Width && next.Y < snapshot.World.Height)
                _spellTargetCursor = next;
            Interlocked.Exchange(ref _redrawRequested, 1);
            return null;
        }
        if (key != ConsoleKey.Enter || !spell.ValidTargets.Contains(_spellTargetCursor!.Value)) return null;
        var command = new BattleActionCommand(client.PlayerId!.Value, client.NextCommandId(), characterId,
            battle.BattleId, battle.TurnId, BattleActionKind.CastSpell, spell.SpellId,
            spell.CastingItemSlotIndex, _spellTargetCursor);
        _targetedBattleSpell = null;
        _spellTargetCursor = null;
        return command;
    }

    private GameCommand? BeginBattleSpellTargeting(CoopSignalRClient client, CharacterId characterId,
        BattleSnapshot battle, BattleSpellOption spell)
    {
        if (spell.ValidTargets.Count == 0)
        {
            SetMessage("A gyorshely varázslatának nincs érvényes célpontja.", ConsoleColor.Red);
            return null;
        }
        if (spell.TargetType is SpellTargetType.Self or SpellTargetType.Party)
            return new BattleActionCommand(client.PlayerId!.Value, client.NextCommandId(), characterId,
                battle.BattleId, battle.TurnId, BattleActionKind.CastSpell, spell.SpellId,
                spell.CastingItemSlotIndex, spell.ValidTargets[0]);
        _targetedBattleSpell = spell;
        _spellTargetCursor = spell.ValidTargets[0];
        Interlocked.Exchange(ref _redrawRequested, 1);
        return null;
    }

    private static bool TryGetFunctionKeyIndex(ConsoleKey key, out int index)
    {
        index = (int)key - (int)ConsoleKey.F1;
        return key is >= ConsoleKey.F1 and <= ConsoleKey.F8;
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
                if (inspectSlot.Item is null)
                    SetMessage("A kijelölt helyen nincs megvizsgálható tárgy.");
                else
                {
                    IItemDefinition definition = inspectSlot.Item.Category switch
                    {
                        ItemCategory.Weapon => _gameData.GetWeapon(inspectSlot.Item.DefinitionId),
                        ItemCategory.Armor => _gameData.GetArmor(inspectSlot.Item.DefinitionId),
                        ItemCategory.MagicItem => _gameData.GetMagicItem(inspectSlot.Item.DefinitionId),
                        _ => _gameData.GetItem(inspectSlot.Item.DefinitionId)
                    };
                    var inspection = ItemInspectionFormatter.Format(definition, _gameData, inspectSlot.Item.Charges);
                    SetMessage(inspection.Text, inspection.Color);
                }
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
        var mapHeight = Math.Min(world.Height, Math.Max(1, windowHeight - MessageLineCount - 1));
        var grid = new GuestMapCell[mapWidth, mapHeight];
        for (var y = 0; y < mapHeight; y++)
            for (var x = 0; x < mapWidth; x++)
                grid[x, y] = y < world.Height
                    ? new GuestMapCell("░", ConsoleColor.DarkGray, ConsoleColor.DarkBlue)
                    : new GuestMapCell(" ", ConsoleColor.Gray);
        foreach (var cell in world.RevealedCells)
            Put(grid, cell.Position, char.ConvertFromUtf32(cell.TileCodePoint), cell.ForegroundColor,
                cell.BackgroundColor);
        foreach (var door in world.Doors)
            Put(grid, door.Position, char.ConvertFromUtf32(door.SymbolCodePoint), door.ForegroundColor,
                door.BackgroundColor);
        foreach (var enemy in world.Enemies) Put(grid, enemy.Position, "e", enemy.Color);
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
        ApplyBattleSpellUi(grid, snapshot, own);
        var panelLines = own?.CharacterSheet is not null && own.Inventory is not null
            ? CharacterSheetPanel.Build(own, snapshot.MazeLevel, snapshot.GoldenKeyCount, snapshot.BossKeyCount)
                .ToDictionary(line => line.Row)
            : [];
        var selectedSlot = _inventoryOpen && own?.Inventory is { Slots.Count: > 0 } inventory
            ? new InventorySlotAddress(inventory.Slots[Math.Clamp(_inventorySelection, 0, inventory.Slots.Count - 1)].Kind,
                inventory.Slots[Math.Clamp(_inventorySelection, 0, inventory.Slots.Count - 1)].Index)
            : (InventorySlotAddress?)null;
        var panelHeight = mapHeight + MessageLineCount + 1;
        var panel = new GuestTextLine[panelHeight];
        for (var y = 0; y < panelHeight; y++)
        {
            if (panelLines.TryGetValue(y, out var line))
            {
                var marker = line.InventorySlot is not null && line.InventorySlot == _inventorySource ? "*" : " ";
                panel[y] = new GuestTextLine(marker + line.Text, line.Color,
                    line.InventorySlot is not null && line.InventorySlot == selectedSlot
                        ? ConsoleColor.DarkCyan
                        : ConsoleColor.Black);
            }
            else
                panel[y] = new GuestTextLine(string.Empty, ConsoleColor.Gray, ConsoleColor.Black);
        }

        var companions = snapshot.Party.Where(character => character.CharacterId != snapshot.LeaderCharacterId)
            .Take(3).ToArray();
        for (var index = 0; index < companions.Length; index++)
        {
            var companion = companions[index];
            if (38 + index < panel.Length)
                panel[38 + index] = new GuestTextLine(FormatPartyMember(companion,
                    companion.CharacterId == selected.CharacterId), companion.Color, ConsoleColor.Black);
        }

        if (!stillControlled && panel.Length > 41)
            panel[41] = new GuestTextLine("Megfigyelő mód", ConsoleColor.DarkYellow, ConsoleColor.Black);

        var portrait = snapshot.Battle is { Enemy: { } battleEnemy }
            ? AsciiPortraits.ForEnemy(battleEnemy.DefinitionId)
            : AsciiPortraits.ForCharacterClass(own?.CharacterClassId ?? string.Empty);
        var portraitColor = snapshot.Battle is { Enemy: { } battleSnapshotEnemy }
            ? world.Enemies.FirstOrDefault(candidate => candidate.DefinitionId == battleSnapshotEnemy.DefinitionId)?.Color
              ?? ConsoleColor.Red
            : own?.Color ?? ConsoleColor.Cyan;
        var pictureTop = Math.Max(0, panel.Length - 7);
        panel[pictureTop] = new GuestTextLine("┌────────── KÉP ──────────┐", ConsoleColor.DarkCyan, ConsoleColor.Black);
        for (var index = 0; index < 5; index++)
        {
            var line = index < portrait.Lines.Count ? portrait.Lines[index] : string.Empty;
            panel[pictureTop + index + 1] = new GuestTextLine($"│{CenterPortrait(line, portrait.CanvasWidth)}│",
                portraitColor, ConsoleColor.Black);
        }
        panel[pictureTop + 6] = new GuestTextLine("└─────────────────────────┘", ConsoleColor.DarkCyan,
            ConsoleColor.Black);

        var messages = _messageLog.ToArray();
        var footer = new GuestTextLine[MessageLineCount];
        for (var index = 0; index < footer.Length; index++)
            footer[index] = index < messages.Length
                ? messages[index]
                : new GuestTextLine(string.Empty, ConsoleColor.Gray, ConsoleColor.Black);
        if (_targetedBattleSpell is { } targeted && _spellTargetCursor is { } cursor)
            footer[^1] = new GuestTextLine($"╳ {targeted.Name} — {ConsoleRenderer.SpellTargetName(targeted.TargetType)}, " +
                $"táv {targeted.Range}{(targeted.AreaRadius > 0 ? $", sugár {targeted.AreaRadius}" : string.Empty)} | " +
                $"({cursor.X},{cursor.Y}) | Enter: célzás, Tab: következő, Esc: mégse",
                targeted.ValidTargets.Contains(cursor) ? ConsoleColor.Cyan : ConsoleColor.DarkYellow,
                ConsoleColor.Black);

        return new GuestRenderFrame(world.WorldId, windowWidth, windowHeight, mapWidth, mapHeight, grid, panel,
            footer);
    }

    private void ApplyBattleSpellUi(GuestMapCell[,] grid, SessionSnapshot snapshot,
        SessionCharacterSnapshot? own)
    {
        if (_targetedBattleSpell is not null && _spellTargetCursor is { } cursor)
        {
            Put(grid, cursor, "╳",
                _targetedBattleSpell.ValidTargets.Contains(cursor) ? ConsoleColor.Green : ConsoleColor.Red,
                ConsoleColor.DarkBlue);
            return;
        }
        if (!_battleSpellMenuOpen || snapshot.Battle is not { } battle) return;
        var options = battle.SpellOptions ?? [];
        _battleSpellSelection = options.Count == 0 ? 0 : Math.Clamp(_battleSpellSelection, 0, options.Count - 1);
        var visibleStart = options.Count == 0 ? 0 : Math.Clamp(_battleSpellSelection - 6, 0,
            Math.Max(0, options.Count - 12));
        var visible = options.Skip(visibleStart).Take(12).ToArray();
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            ("⚔ HARCI VARÁZSLÁS", ConsoleColor.Magenta),
            ($"{own?.Name}  ◆ {own?.CurrentMana}/{own?.MaximumMana} manna", ConsoleColor.Cyan),
            ("↑↓ választ  Enter célzás  Esc bezár", ConsoleColor.Green),
            (new string('─', 68), ConsoleColor.DarkMagenta)
        };
        if (options.Count == 0)
            lines.Add(("Ebben a helyzetben nincs használható memorizált vagy tárgyban tárolt varázslat.",
                ConsoleColor.DarkYellow));
        else
            lines.AddRange(visible.Select((spell, index) =>
            {
                var absoluteIndex = visibleStart + index;
                var quick = spell.CastingItemKind switch
                {
                    MagicItemKind.Scroll => "📜",
                    MagicItemKind.Wand => $"{ConsoleRenderer.WandIcon}{spell.Charges}",
                    _ => spell.QuickSlot is { } slot ? $"F{slot + 1}" : "--"
                };
                var text = $"{(absoluteIndex == _battleSpellSelection ? "▶" : " ")} [{quick}] L{spell.Level}  " +
                           $"{spell.Name,-24} {spell.ManaCost}M  {ConsoleRenderer.SpellTargetName(spell.TargetType)}";
                var color = (own?.CurrentMana ?? 0) < spell.ManaCost ? ConsoleColor.DarkRed :
                    absoluteIndex == _battleSpellSelection ? ConsoleColor.Yellow : ConsoleColor.Gray;
                return (text, color);
            }));

        const int desiredWidth = 76;
        var width = Math.Min(desiredWidth, Math.Max(10, grid.GetLength(0) - 2));
        var left = Math.Max(0, (grid.GetLength(0) - width) / 2);
        var top = Math.Max(1, (grid.GetLength(1) - lines.Count - 2) / 2);
        DrawOverlayText(grid, left, top, "╔" + new string('═', width - 2) + "╗", ConsoleColor.Magenta);
        for (var row = 0; row < lines.Count; row++)
        {
            var text = lines[row].Text.Length > width - 4 ? lines[row].Text[..(width - 4)] : lines[row].Text;
            DrawOverlayText(grid, left, top + row + 1, "║", ConsoleColor.Magenta);
            DrawOverlayText(grid, left + 1, top + row + 1, new string(' ', width - 2), ConsoleColor.Gray);
            DrawOverlayText(grid, left + 2, top + row + 1, text.PadRight(width - 4), lines[row].Color);
            DrawOverlayText(grid, left + width - 1, top + row + 1, "║", ConsoleColor.Magenta);
        }
        DrawOverlayText(grid, left, top + lines.Count + 1, "╚" + new string('═', width - 2) + "╝",
            ConsoleColor.Magenta);
    }

    private static void DrawOverlayText(GuestMapCell[,] grid, int x, int y, string text, ConsoleColor color)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            Put(grid, new Position(x++, y), rune.ToString(), color);
            if (x >= grid.GetLength(0)) break;
        }
    }

    private static void RenderFrame(GuestRenderFrame frame, GuestRenderFrame? previous)
    {
        var fullRedraw = previous is null || previous.WorldId != frame.WorldId ||
                         previous.WindowWidth != frame.WindowWidth || previous.WindowHeight != frame.WindowHeight ||
                         previous.MapWidth != frame.MapWidth || previous.MapHeight != frame.MapHeight;
        if (fullRedraw) ResetConsole();

        const int mapTop = 0;
        for (var y = 0; y < frame.MapHeight; y++)
        {
            for (var x = 0; x < frame.MapWidth; x++)
                if (fullRedraw || previous!.Map[x, y] != frame.Map[x, y])
                    WriteMapCell(x, mapTop + y, frame.Map[x, y]);
        }

        if (fullRedraw)
            WriteAt(0, frame.MapHeight, new GuestTextLine(new string('─', frame.MapWidth),
                ConsoleColor.DarkCyan, ConsoleColor.Black), frame.MapWidth);

        for (var row = 0; row < frame.Panel.Length; row++)
        {
            if (fullRedraw)
                WriteAt(frame.MapWidth, row, new GuestTextLine(" │ ", ConsoleColor.DarkCyan, ConsoleColor.Black), 3);
            if (fullRedraw || previous!.Panel[row] != frame.Panel[row])
                WriteAt(frame.MapWidth + 3, row, frame.Panel[row], CharacterSheetPanel.Width);
        }

        for (var row = 0; row < frame.Footers.Length; row++)
            if (fullRedraw || previous!.Footers[row] != frame.Footers[row])
                WriteAt(2, frame.MapHeight + 1 + row, frame.Footers[row], Math.Max(1, frame.MapWidth - 4));

        Console.ResetColor();
        TrySetCursorPosition(0, Math.Min(frame.WindowHeight - 1, frame.MapHeight + frame.Footers.Length));
    }

    private static void WriteMapCell(int x, int y, GuestMapCell cell)
    {
        if (!TrySetCursorPosition(x, y)) return;
        Console.ForegroundColor = cell.Color;
        Console.BackgroundColor = cell.Background;
        Console.Write(cell.Glyph);
    }

    private static void WriteAt(int x, int y, GuestTextLine line, int width)
    {
        if (!TrySetCursorPosition(x, y)) return;
        Console.ForegroundColor = line.Foreground;
        Console.BackgroundColor = line.Background;
        Console.Write(FitConsoleLine(line.Text, width));
    }

    private void SetMessage(string message, ConsoleColor color = ConsoleColor.DarkYellow)
    {
        foreach (var line in WrapMessage(message, Math.Max(1, SafeWindowWidth() - CharacterSheetPanel.Width - 8)))
            _messageLog.Enqueue(new GuestTextLine(line, color, ConsoleColor.Black));
        while (_messageLog.Count > MessageLineCount) _messageLog.Dequeue();
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

    private static void Put(GuestMapCell[,] grid, Position position, string value, ConsoleColor color,
        ConsoleColor background = ConsoleColor.Black)
    {
        if (position.X >= 0 && position.X < grid.GetLength(0) && position.Y >= 0 && position.Y < grid.GetLength(1))
            grid[position.X, position.Y] = new GuestMapCell(value, color, background);
    }

    private static string FormatPartyMember(SessionCharacterSnapshot character, bool isDisplayed)
    {
        var marker = isDisplayed ? "▶ " : "  ";
        var suffix = character.IsAlive
            ? $" L{character.Level} {character.CurrentVitality}/{character.MaximumVitality}"
            : $" L{character.Level} 💀";
        var glyph = CharacterSheetPanel.CharacterClassGlyph(character.CharacterClassId);
        var maximumNameLength = Math.Max(1, CharacterSheetPanel.Width - marker.Length - glyph.Length - 1 - suffix.Length);
        var name = character.Name[..Math.Min(character.Name.Length, maximumNameLength)];
        return $"{marker}{glyph} {name}{suffix}";
    }

    private static string CenterPortrait(string text, int canvasWidth)
    {
        const int interiorWidth = 25;
        var canvas = text.PadRight(canvasWidth);
        var leftPadding = Math.Max(0, (interiorWidth - canvasWidth) / 2);
        return (new string(' ', leftPadding) + canvas).PadRight(interiorWidth);
    }

    private static IEnumerable<string> WrapMessage(string message, int width)
    {
        while (message.Length > width)
        {
            var splitAt = message.LastIndexOf(' ', width);
            if (splitAt <= 0) splitAt = width;
            yield return message[..splitAt];
            message = message[splitAt..].TrimStart();
        }
        yield return message;
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

    private readonly record struct GuestMapCell(string Glyph, ConsoleColor Color,
        ConsoleColor Background = ConsoleColor.Black);
    private readonly record struct GuestTextLine(string Text, ConsoleColor Foreground, ConsoleColor Background);
    private sealed record GuestRenderFrame(WorldId WorldId, int WindowWidth, int WindowHeight, int MapWidth,
        int MapHeight, GuestMapCell[,] Map, GuestTextLine[] Panel, GuestTextLine[] Footers);
}
