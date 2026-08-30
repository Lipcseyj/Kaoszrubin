using KaoszRubin.Application;
using KaoszRubin.Data;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Magic;
using KaoszRubin.Transport.SignalR;
using System.Globalization;

namespace KaoszRubin.UI;

/// <summary>Az első LAN vertical slice vendégoldali, snapshotból rajzoló konzolképernyője.</summary>
public sealed class CoopGuestScreen
{
    private readonly string _applicationVersion;
    private readonly string _catalogHash;
    private readonly GameDataCatalog _gameData;
    private readonly SoundEffects _soundEffects;
    private readonly BackgroundMusicPlayer _backgroundMusic;
    private readonly MusicSettingsService _musicSettings;
    private int _redrawRequested = 1;
    private const int MessageLineCount = ConsoleRenderer.MessageLogLineCount;
    private const int MessageBufferLineCount = ConsoleRenderer.MessageLogBufferLineCount;
    private readonly Queue<GuestTextLine> _messageLog = new();
    private int _messageLogScrollOffset;
    private int _messageLineWidth = 80;
    private bool _inventoryOpen;
    private int _inventorySelection;
    private CharacterId? _displayedCharacterId;
    private InventorySlotAddress? _inventorySource;
    private bool _battleSpellMenuOpen;
    private int _battleSpellSelection;
    private BattleSpellOption? _targetedBattleSpell;
    private Position? _spellTargetCursor;
    private bool _spellCastingInBattle;
    private InnVendorKind? _innVendor;
    private bool _innMageMenuOpen;
    private int _innSelection;
    private InnMarketMode _innMarketMode = InnMarketMode.Buy;
    private bool _innRumorOpen;
    private int _innRumorSelection;
    private long _lastInnTransactionSequence;
    private long _lastSessionActivitySequence;
    private long _lastSessionSoundSequence;
    private bool _sessionSoundsInitialized;
    private int _deathStateSynchronized;
    private Guid? _acknowledgedNarrativeId;
    private Guid? _acknowledgedRestId;
    private bool _spellInfoOpen;
    private int _spellInfoSelection;
    private Guid? _spellPreparationPromptId;
    private int _spellPreparationCursor;
    private readonly HashSet<string> _preparedSpellIds = new(StringComparer.OrdinalIgnoreCase);
    private Guid? _levelUpPromptId;
    private int _levelUpSelection;
    private CharacterAction? _doorTargetAction;
    private IReadOnlyList<Position> _doorTargetCandidates = [];
    private int _doorTargetSelection;
    private GuestRenderFrame? _lastFrame;

    public CoopGuestScreen(string applicationVersion, string catalogHash, GameDataCatalog gameData,
        MusicSettingsService? musicSettings = null)
    {
        _applicationVersion = applicationVersion;
        _catalogHash = catalogHash;
        _gameData = gameData ?? throw new ArgumentNullException(nameof(gameData));
        _soundEffects = new SoundEffects(message => SetMessage(message, ConsoleColor.DarkYellow));
        _musicSettings = musicSettings ?? new MusicSettingsService();
        _backgroundMusic = new BackgroundMusicPlayer(_musicSettings.Settings,
            message => SetMessage(message, ConsoleColor.DarkYellow));
    }

    public async Task RunAsync(string hostUrl, string displayName, LiveCharacter localCharacter,
        string characterData, Action<CharacterStateSync>? persistCharacterState = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(localCharacter);
        await using var client = new CoopSignalRClient(hostUrl, _applicationVersion, _catalogHash, displayName);
        client.SnapshotChanged += _ => Interlocked.Exchange(ref _redrawRequested, 1);
        client.ConnectionStateChanged += _ => Interlocked.Exchange(ref _redrawRequested, 1);
        client.ProtocolErrorReceived += error => SetMessage($"Protokollhiba: {error.Message}");
        client.CommandRejected += rejected => SetMessage($"A host elutasította: {rejected.Reason}");
        client.CharacterStateReceived += state =>
        {
            if (state.CharacterId != localCharacter.Id)
            {
                SetMessage("A host másik karakterhez küldött mentési állapotot.", ConsoleColor.Red);
                return;
            }
            try
            {
                persistCharacterState?.Invoke(state);
                SetMessage(state.Reason switch
                {
                    CharacterSyncReason.GameSaved => "A host mentette és visszaszinkronizálta a karakteredet.",
                    CharacterSyncReason.CharacterDied => "A karaktered halálállapota visszaszinkronizálva.",
                    _ => "A karaktered végső állapota visszaszinkronizálva."
                }, ConsoleColor.Green);
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException or
                                               UnauthorizedAccessException)
            {
                SetMessage($"A helyi karakter mentése sikertelen: {exception.Message}", ConsoleColor.Red);
            }
            finally
            {
                if (state.Reason == CharacterSyncReason.CharacterDied)
                    Interlocked.Exchange(ref _deathStateSynchronized, 1);
            }
        };

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
                if (Volatile.Read(ref _deathStateSynchronized) != 0 &&
                    client.CurrentSnapshot?.Party.FirstOrDefault(character =>
                        character.CharacterId == selected.CharacterId) is { IsAlive: false })
                {
                    ConsoleRenderer.DrawCoopGuestGameOver(selected.Name);
                    break;
                }
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true);
                    if (key.Key is ConsoleKey.PageUp or ConsoleKey.PageDown)
                    {
                        ScrollMessageLog(key.Key == ConsoleKey.PageUp);
                        continue;
                    }
                    if (GameInput.IsSettingsShortcut(key))
                    {
                        SettingsScreen.Show(_musicSettings, _backgroundMusic.ApplySettings);
                        _lastFrame = null;
                        Interlocked.Exchange(ref _redrawRequested, 1);
                        continue;
                    }
                    if (GameInput.IsHelpShortcut(key))
                    {
                        var playerId = client.PlayerId!.Value;
                        await client.SendCommandAsync(new SetHelpVisibilityCommand(playerId,
                            client.NextCommandId(), selected.CharacterId, true), cancellationToken);
                        try
                        {
                            MainMenu.ShowHelp();
                        }
                        finally
                        {
                            await client.SendCommandAsync(new SetHelpVisibilityCommand(playerId,
                                client.NextCommandId(), selected.CharacterId, false), cancellationToken);
                        }
                        // A súgó közvetlenül a konzolra rajzol, ezért a gyorsítótárazott játék-frame
                        // már nem tükrözi a képernyő tényleges tartalmát. Kényszerítsünk teljes újrarajzolást.
                        _lastFrame = null;
                        Interlocked.Exchange(ref _redrawRequested, 1);
                        continue;
                    }
                    if (key.Key == ConsoleKey.Q &&
                        client.CurrentSnapshot is { Phase: GameSessionPhase.Exploration or GameSessionPhase.Inn } questSnapshot &&
                        questSnapshot.Narrative is null && questSnapshot.RestNotice is null &&
                        questSnapshot.SpellPreparation is null && questSnapshot.LevelUpPrompt is null &&
                        !_inventoryOpen && !_battleSpellMenuOpen && _targetedBattleSpell is null)
                    {
                        QuestJournalWindow.Show(questSnapshot.QuestJournal ?? []);
                        _lastFrame = null;
                        Interlocked.Exchange(ref _redrawRequested, 1);
                        continue;
                    }
                    if (key.Key == ConsoleKey.Escape && client.CurrentSnapshot?.Phase != GameSessionPhase.Inn &&
                        client.CurrentSnapshot?.Narrative is null &&
                        client.CurrentSnapshot?.RestNotice is null &&
                        client.CurrentSnapshot?.SpellPreparation is null &&
                        client.CurrentSnapshot?.LevelUpPrompt is null &&
                        !_inventoryOpen && !_battleSpellMenuOpen && _targetedBattleSpell is null &&
                        _doorTargetAction is null)
                    {
                        if (ConfirmReturnToMainMenu(client, selected)) break;
                        continue;
                    }
                    await HandleInputAsync(client, selected, key.Key, cancellationToken);
                }
                await Task.Delay(20, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            _backgroundMusic.Dispose();
            Console.CursorVisible = true;
            await client.DisconnectAsync(CancellationToken.None);
        }
    }

    private bool ConfirmReturnToMainMenu(CoopSignalRClient client, CoopCharacterOption selected)
    {
        SetMessage(
            "⚠️ Visszatérsz a főmenübe? A legutóbbi mentés óta történt változások elvesznek. I/Y: igen | N/Esc: maradok",
            ConsoleColor.Red);
        Draw(client, selected);
        while (true)
        {
            var key = Console.ReadKey(intercept: true).Key;
            if (key is ConsoleKey.I or ConsoleKey.Y) return true;
            if (key is not (ConsoleKey.N or ConsoleKey.Escape)) continue;

            SetMessage("A játék folytatódik.", ConsoleColor.Cyan);
            Draw(client, selected);
            return false;
        }
    }

    private async Task HandleInputAsync(CoopSignalRClient client, CoopCharacterOption selected, ConsoleKey key,
        CancellationToken cancellationToken)
    {
        var characterId = selected.CharacterId;
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
        if (snapshot.Phase != GameSessionPhase.Inn)
        {
            _innVendor = null;
            _innRumorOpen = false;
        }
        if (snapshot.Phase != GameSessionPhase.Exploration)
            ClearDoorTargeting();
        if (snapshot.SpellPreparation is { CharacterId: var preparingCharacter } preparation &&
            preparingCharacter == characterId)
        {
            var preparationCommand = HandleSpellPreparationInput(client, characterId, preparation, key);
            if (preparationCommand is not null)
            {
                try { await client.SendCommandAsync(preparationCommand, cancellationToken); }
                catch (Exception exception) when (exception is InvalidOperationException or TimeoutException)
                { SetMessage(exception.Message); }
            }
            return;
        }
        _spellPreparationPromptId = null;
        if (snapshot.LevelUpPrompt is { CharacterId: var levelingCharacter } levelUp &&
            levelingCharacter == characterId)
        {
            var levelUpCommand = HandleLevelUpInput(client, characterId, levelUp, key);
            if (levelUpCommand is not null)
            {
                try { await client.SendCommandAsync(levelUpCommand, cancellationToken); }
                catch (Exception exception) when (exception is InvalidOperationException or TimeoutException)
                { SetMessage(exception.Message); }
            }
            return;
        }
        _levelUpPromptId = null;
        SynchronizeSpellUi(snapshot, characterId);
        if (snapshot.RestNotice is { } rest)
        {
            if (key != ConsoleKey.Enter || _acknowledgedRestId == rest.RestId) return;
            _acknowledgedRestId = rest.RestId;
            command = new AcknowledgeRestCommand(client.PlayerId!.Value, client.NextCommandId(), characterId,
                rest.RestId);
        }
        else _acknowledgedRestId = null;
        if (command is null && snapshot.Narrative is { } narrative)
        {
            if (key != ConsoleKey.Enter || _acknowledgedNarrativeId == narrative.NarrativeId) return;
            _acknowledgedNarrativeId = narrative.NarrativeId;
            command = new AcknowledgeNarrativeCommand(client.PlayerId!.Value, client.NextCommandId(), characterId,
                narrative.NarrativeId);
        }
        else if (snapshot.Narrative is null) _acknowledgedNarrativeId = null;
        if (command is null && _spellInfoOpen)
        {
            command = HandleSpellInfoInput(client, characterId, snapshot, key);
            if (command is null) return;
        }
        if (command is null && _battleSpellMenuOpen)
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
        if (command is not null) { }
        else if (_doorTargetAction is not null)
        {
            command = HandleDoorTargetInput(client, selected, snapshot, key);
            if (command is null) return;
        }
        else if (_targetedBattleSpell is not null)
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
        else if (snapshot.Phase == GameSessionPhase.Inn && snapshot.Inn is { } inn)
        {
            command = HandleInnInput(client, characterId, snapshot, inn, key);
            if (command is null) return;
        }
        else if (snapshot.Battle is { } battle && battle.ActingCharacterId == characterId)
        {
            if (TryGetBattleTacticAction(battle, key, out var tacticAction))
            {
                command = new BattleActionCommand(client.PlayerId!.Value, client.NextCommandId(), characterId,
                    battle.BattleId, battle.TurnId, tacticAction);
            }
            else
            if (key == ConsoleKey.V && battle.AllowedActions.Contains(BattleActionKind.CastSpell))
            {
                _battleSpellMenuOpen = true;
                _spellCastingInBattle = true;
                _battleSpellSelection = 0;
                Interlocked.Exchange(ref _redrawRequested, 1);
                return;
            }
            if (TryGetFunctionKeyIndex(key, out var quickSlot) &&
                battle.SpellOptions?.FirstOrDefault(option => option.QuickSlot == quickSlot) is { } quickSpell)
            {
                command = BeginSpellTargeting(client, characterId, snapshot, quickSpell);
                if (command is null) return;
            }
            else
            {
            var action = key switch
            {
                ConsoleKey.Spacebar when battle.AllowedActions.Contains(BattleActionKind.AdvanceEnemyTurn) =>
                    BattleActionKind.AdvanceEnemyTurn,
                ConsoleKey.Spacebar => BattleActionKind.PhysicalAttack,
                ConsoleKey.T => BattleActionKind.TurnUndead,
                _ => (BattleActionKind?)null
            };
            if (action is not null)
                command = new BattleActionCommand(client.PlayerId!.Value, client.NextCommandId(), characterId,
                    battle.BattleId, battle.TurnId, action.Value);
            }
        }
        else if (snapshot.Phase == GameSessionPhase.Exploration && key == ConsoleKey.V)
        {
            _battleSpellMenuOpen = true;
            _spellCastingInBattle = false;
            _battleSpellSelection = 0;
            Interlocked.Exchange(ref _redrawRequested, 1);
            return;
        }
        else if (snapshot.Phase == GameSessionPhase.Exploration &&
                 TryGetFunctionKeyIndex(key, out var explorationQuickSlot) &&
                 OwnExplorationSpellOptions(snapshot, characterId)
                     .FirstOrDefault(option => option.QuickSlot == explorationQuickSlot) is { } explorationQuick)
        {
            command = BeginSpellTargeting(client, characterId, snapshot, explorationQuick);
            if (command is null) return;
        }
        else if (snapshot.Phase is (GameSessionPhase.Exploration or GameSessionPhase.Inn) &&
                 GameInputBindings.IsCharacterSheetToggle(key))
        {
            _inventoryOpen = true;
            _inventorySelection = 0;
            _inventorySource = null;
            _displayedCharacterId = characterId;
            Interlocked.Exchange(ref _redrawRequested, 1);
        }
        else if (snapshot.Phase == GameSessionPhase.Exploration && GameInputBindings.CharacterAction(key) is { } action)
        {
            if (action is CharacterAction.OpenDoor or CharacterAction.CloseOrLockDoor)
            {
                var ownPosition = snapshot.Party.FirstOrDefault(character => character.CharacterId == characterId)
                    ?.Position;
                var doors = ownPosition is { } position && snapshot.World is { } world
                    ? world.Doors.Select(door => door.Position)
                        .Where(candidate => Manhattan(candidate, position) == 1).ToArray()
                    : [];
                if (doors.Length > 1)
                {
                    _doorTargetAction = action;
                    _doorTargetCandidates = doors;
                    _doorTargetSelection = 0;
                    Interlocked.Exchange(ref _redrawRequested, 1);
                    return;
                }
                var target = doors.Length == 1 ? doors[0] : (Position?)null;
                var useKey = GetGuestThiefKeyChoice(client, selected, snapshot, action, target);
                command = new CharacterActionCommand(client.PlayerId!.Value, client.NextCommandId(), characterId,
                    action, target, useKey);
            }
            else
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

    private GameCommand? HandleDoorTargetInput(CoopSignalRClient client, CoopCharacterOption selected,
        SessionSnapshot snapshot, ConsoleKey key)
    {
        var characterId = selected.CharacterId;
        if (_doorTargetAction is not { } action || _doorTargetCandidates.Count == 0)
        { ClearDoorTargeting(); return null; }
        _doorTargetSelection = Math.Clamp(_doorTargetSelection, 0, _doorTargetCandidates.Count - 1);
        if (key == ConsoleKey.Escape) { ClearDoorTargeting(); return null; }
        if (key == ConsoleKey.Enter)
        {
            var target = _doorTargetCandidates[_doorTargetSelection];
            ClearDoorTargeting();
            var useKey = GetGuestThiefKeyChoice(client, selected, snapshot, action, target);
            return new CharacterActionCommand(client.PlayerId!.Value, client.NextCommandId(), characterId,
                action, target, useKey);
        }
        if (key == ConsoleKey.Tab)
            _doorTargetSelection = (_doorTargetSelection + 1) % _doorTargetCandidates.Count;
        else if (TryGetDirection(key, out var direction) &&
                 snapshot.Party.FirstOrDefault(character => character.CharacterId == characterId)?.Position is { } own)
        {
            var target = own + direction;
            var index = _doorTargetCandidates.ToList().IndexOf(target);
            if (index >= 0) _doorTargetSelection = index;
        }
        Interlocked.Exchange(ref _redrawRequested, 1);
        return null;
    }

    private void ClearDoorTargeting()
    {
        _doorTargetAction = null;
        _doorTargetCandidates = [];
        _doorTargetSelection = 0;
        Interlocked.Exchange(ref _redrawRequested, 1);
    }

    private static int Manhattan(Position first, Position second) =>
        Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y);

    private GameCommand? HandleLevelUpInput(CoopSignalRClient client, CharacterId characterId,
        LevelUpPromptSnapshot prompt, ConsoleKey key)
    {
        if (_levelUpPromptId != prompt.PromptId)
        { _levelUpPromptId = prompt.PromptId; _levelUpSelection = 0; }
        if (prompt.Kind == LevelUpPromptKind.Summary)
        {
            return new ResolveLevelUpPromptCommand(client.PlayerId!.Value, client.NextCommandId(), characterId,
                prompt.PromptId, null);
        }
        if (prompt.Choices.Count == 0) return null;
        _levelUpSelection = Math.Clamp(_levelUpSelection, 0, prompt.Choices.Count - 1);
        if (key is ConsoleKey.UpArrow or ConsoleKey.LeftArrow)
            _levelUpSelection = (_levelUpSelection - 1 + prompt.Choices.Count) % prompt.Choices.Count;
        else if (key is ConsoleKey.DownArrow or ConsoleKey.RightArrow)
            _levelUpSelection = (_levelUpSelection + 1) % prompt.Choices.Count;
        else if (key == ConsoleKey.Enter)
            return new ResolveLevelUpPromptCommand(client.PlayerId!.Value, client.NextCommandId(), characterId,
                prompt.PromptId, prompt.Choices[_levelUpSelection].Id);
        Interlocked.Exchange(ref _redrawRequested, 1);
        return null;
    }

    private GameCommand? HandleSpellPreparationInput(CoopSignalRClient client, CharacterId characterId,
        SpellPreparationSnapshot preparation, ConsoleKey key)
    {
        if (_spellPreparationPromptId != preparation.PromptId)
        {
            _spellPreparationPromptId = preparation.PromptId;
            _spellPreparationCursor = 0;
            _preparedSpellIds.Clear();
            _preparedSpellIds.UnionWith(preparation.SelectedSpellIds);
        }
        var spells = preparation.Spells;
        if (spells.Count == 0 && key == ConsoleKey.Enter)
            return new PrepareSpellsCommand(client.PlayerId!.Value, client.NextCommandId(), characterId,
                preparation.PromptId, []);
        if (spells.Count == 0) return null;
        _spellPreparationCursor = Math.Clamp(_spellPreparationCursor, 0, spells.Count - 1);
        if (key == ConsoleKey.UpArrow)
            _spellPreparationCursor = (_spellPreparationCursor - 1 + spells.Count) % spells.Count;
        else if (key == ConsoleKey.DownArrow)
            _spellPreparationCursor = (_spellPreparationCursor + 1) % spells.Count;
        else if (key == ConsoleKey.Spacebar)
        {
            var id = spells[_spellPreparationCursor].SpellId;
            if (!_preparedSpellIds.Remove(id) && _preparedSpellIds.Count < preparation.Capacity)
                _preparedSpellIds.Add(id);
        }
        else if (key == ConsoleKey.Enter)
            return new PrepareSpellsCommand(client.PlayerId!.Value, client.NextCommandId(), characterId,
                preparation.PromptId, _preparedSpellIds.ToArray());
        Interlocked.Exchange(ref _redrawRequested, 1);
        return null;
    }

    private GameCommand? HandleSpellInfoInput(CoopSignalRClient client, CharacterId characterId,
        SessionSnapshot snapshot, ConsoleKey key)
    {
        var own = snapshot.Party.FirstOrDefault(character => character.CharacterId == characterId);
        var spells = own?.SpellInfo?.KnownSpells ?? [];
        _spellInfoSelection = spells.Count == 0 ? 0 : Math.Clamp(_spellInfoSelection, 0, spells.Count - 1);
        if (key == ConsoleKey.Escape)
        {
            _spellInfoOpen = false;
            Interlocked.Exchange(ref _redrawRequested, 1);
            return null;
        }
        if (spells.Count == 0) return null;
        if (key == ConsoleKey.UpArrow) _spellInfoSelection = (_spellInfoSelection - 1 + spells.Count) % spells.Count;
        else if (key == ConsoleKey.DownArrow) _spellInfoSelection = (_spellInfoSelection + 1) % spells.Count;
        else if (TryGetFunctionKeyIndex(key, out var quickSlot))
            return new AssignQuickSpellCommand(client.PlayerId!.Value, client.NextCommandId(), characterId,
                spells[_spellInfoSelection].SpellId, quickSlot);
        else if (key == ConsoleKey.Enter)
        {
            if (snapshot.Phase != GameSessionPhase.Exploration)
                SetMessage("A fogadóban a varázslat csak megtekinthető; elsütni a térképen lehet.");
            else if (OwnExplorationSpellOptions(snapshot, characterId).FirstOrDefault(option =>
                         option.SpellId == spells[_spellInfoSelection].SpellId &&
                         option.CastingItemSlotIndex is null) is { } option)
            {
                _spellInfoOpen = false;
                _inventoryOpen = false;
                return BeginSpellTargeting(client, characterId, snapshot, option);
            }
            else SetMessage("Csak memorizált, jelenleg használható varázslat süthető el.");
        }
        Interlocked.Exchange(ref _redrawRequested, 1);
        return null;
    }

    private GameCommand? HandleInnInput(CoopSignalRClient client, CharacterId characterId,
        SessionSnapshot snapshot, InnSnapshot inn, ConsoleKey key)
    {
        if (inn.LevelCompletion is not null) return null;
        if (GameInputBindings.IsCharacterSheetToggle(key) &&
            !(_innVendor == InnVendorKind.Market && key == ConsoleKey.Tab))
        {
            _inventoryOpen = true;
            _inventorySelection = 0;
            _inventorySource = null;
            Interlocked.Exchange(ref _redrawRequested, 1);
            return null;
        }
        if (_innRumorOpen)
        {
            if (inn.Rumors.Count == 0) { _innRumorOpen = false; return null; }
            _innRumorSelection = Math.Clamp(_innRumorSelection, 0, inn.Rumors.Count - 1);
            if (key is ConsoleKey.Enter or ConsoleKey.Escape)
                _innRumorOpen = false;
            else if (key is ConsoleKey.N or ConsoleKey.RightArrow or ConsoleKey.DownArrow)
                _innRumorSelection = (_innRumorSelection + 1) % inn.Rumors.Count;
            else if (key is ConsoleKey.LeftArrow or ConsoleKey.UpArrow)
                _innRumorSelection = (_innRumorSelection - 1 + inn.Rumors.Count) % inn.Rumors.Count;
            Interlocked.Exchange(ref _redrawRequested, 1);
            return null;
        }
        if (_innVendor is null)
        {
            var options = inn.MenuOptions ?? [];
            var count = options.Count;
            if (count == 0) return null;
            _innSelection = Math.Clamp(_innSelection, 0, Math.Max(0, count - 1));
            if (key == ConsoleKey.UpArrow) _innSelection = (_innSelection - 1 + count) % count;
            else if (key == ConsoleKey.DownArrow) _innSelection = (_innSelection + 1) % count;
            else if (key == ConsoleKey.Enter)
            {
                var option = options[_innSelection];
                if (option.LeaderOnly)
                    SetMessage("Ezt a party-szintű műveletet csak a host aktiválhatja.");
                else if (option.Kind == InnMenuOptionKind.Rumors)
                {
                    _innRumorOpen = true;
                    _innRumorSelection = 0;
                }
                else if (option.Vendor is { } selectedVendor)
                {
                    _innVendor = selectedVendor;
                    _innMageMenuOpen = selectedVendor == InnVendorKind.WanderingMage;
                    _innMarketMode = InnMarketMode.Buy;
                    _innSelection = 0;
                }
            }
            Interlocked.Exchange(ref _redrawRequested, 1);
            return null;
        }

        var vendor = inn.Vendors.FirstOrDefault(candidate => candidate.Kind == _innVendor);
        if (vendor is null) { _innVendor = null; _innSelection = 0; return null; }
        if (_innMageMenuOpen)
        {
            const int mageOptionCount = 4;
            if (key == ConsoleKey.Escape) { _innVendor = null; _innMageMenuOpen = false; _innSelection = 0; }
            else if (key == ConsoleKey.UpArrow) _innSelection = (_innSelection - 1 + mageOptionCount) % mageOptionCount;
            else if (key == ConsoleKey.DownArrow) _innSelection = (_innSelection + 1) % mageOptionCount;
            else if (key == ConsoleKey.Enter && _innSelection == 1) { _innMageMenuOpen = false; _innSelection = 0; }
            else if (key == ConsoleKey.Enter && _innSelection == 3) { _innVendor = null; _innMageMenuOpen = false; _innSelection = 0; }
            else if (key == ConsoleKey.Enter) SetMessage(_innSelection == 0
                ? "A pálcatöltést csak a party leader intézheti."
                : "A varázstárgy-azonosítás még nem használható.");
            Interlocked.Exchange(ref _redrawRequested, 1);
            return null;
        }
        if (key == ConsoleKey.Escape)
        { _innVendor = null; _innSelection = 0; Interlocked.Exchange(ref _redrawRequested, 1); return null; }
        if (vendor.Kind == InnVendorKind.Market &&
            key is ConsoleKey.LeftArrow or ConsoleKey.RightArrow or ConsoleKey.Tab)
        {
            _innMarketMode = _innMarketMode == InnMarketMode.Buy ? InnMarketMode.Sell : InnMarketMode.Buy;
            _innSelection = 0;
            Interlocked.Exchange(ref _redrawRequested, 1);
            return null;
        }
        var own = snapshot.Party.FirstOrDefault(character => character.CharacterId == characterId);
        var sellOffers = _innMarketMode == InnMarketMode.Sell && vendor.Kind == InnVendorKind.Market
            ? GuestInnSellOffers(inn, own).ToArray()
            : [];
        var entryCount = _innMarketMode == InnMarketMode.Sell && vendor.Kind == InnVendorKind.Market
            ? sellOffers.Length
            : vendor.Offers.Count;
        if (entryCount == 0) return null;
        _innSelection = Math.Clamp(_innSelection, 0, entryCount - 1);
        if (key == ConsoleKey.UpArrow) _innSelection = (_innSelection - 1 + entryCount) % entryCount;
        else if (key == ConsoleKey.DownArrow) _innSelection = (_innSelection + 1) % entryCount;
        else if (key == ConsoleKey.Enter)
        {
            if (_innMarketMode == InnMarketMode.Sell && vendor.Kind == InnVendorKind.Market)
            {
                var saleOffer = sellOffers[_innSelection];
                SetMessage($"Eladás: {saleOffer.Slot.Item!.Name}…", ConsoleColor.Cyan);
                return new InnSaleCommand(client.PlayerId!.Value, client.NextCommandId(), characterId,
                    inn.Revision, own!.Inventory!.Revision, saleOffer.Slot.Index);
            }
            var purchaseOffer = vendor.Offers[_innSelection];
            SetMessage($"Vásárlás: {purchaseOffer.Item.Name}…", ConsoleColor.Cyan);
            return new InnPurchaseCommand(client.PlayerId!.Value, client.NextCommandId(), characterId,
                inn.Revision, vendor.Kind, purchaseOffer.Index);
        }
        Interlocked.Exchange(ref _redrawRequested, 1);
        return null;
    }

    private static IEnumerable<(InventorySlotSnapshot Slot, int Price)> GuestInnSellOffers(InnSnapshot inn,
        SessionCharacterSnapshot? character)
    {
        if (character?.Inventory is not { } inventory) yield break;
        var prices = inn.SellPrices.ToDictionary(price => price.ItemDefinitionId, price => price.Price,
            StringComparer.OrdinalIgnoreCase);
        foreach (var slot in inventory.Slots.Where(slot => slot.Kind == InventorySlotKind.Backpack && slot.Item is not null))
            if (prices.TryGetValue(slot.Item!.DefinitionId, out var price)) yield return (slot, price);
    }

    private void SynchronizeSpellUi(SessionSnapshot snapshot, CharacterId characterId)
    {
        if (_spellCastingInBattle && snapshot.Battle is { } battle && battle.ActingCharacterId == characterId)
            return;
        if (!_spellCastingInBattle && snapshot.Phase == GameSessionPhase.Exploration) return;
        _battleSpellMenuOpen = false;
        _targetedBattleSpell = null;
        _spellTargetCursor = null;
    }

    private GameCommand? HandleBattleSpellMenuInput(CoopSignalRClient client, CharacterId characterId,
        SessionSnapshot snapshot, ConsoleKey key)
    {
        var options = CurrentSpellOptions(snapshot, characterId);
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
                return BeginSpellTargeting(client, characterId, snapshot, option);
            }
        }
        Interlocked.Exchange(ref _redrawRequested, 1);
        return null;
    }

    private GameCommand? HandleBattleSpellTargetInput(CoopSignalRClient client, CharacterId characterId,
        SessionSnapshot snapshot, ConsoleKey key)
    {
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
        GameCommand command = _spellCastingInBattle
            ? new BattleActionCommand(client.PlayerId!.Value, client.NextCommandId(), characterId,
                snapshot.Battle!.BattleId, snapshot.Battle.TurnId, BattleActionKind.CastSpell, spell.SpellId,
                spell.CastingItemSlotIndex, _spellTargetCursor)
            : new CastExplorationSpellCommand(client.PlayerId!.Value, client.NextCommandId(), characterId,
                spell.SpellId, spell.CastingItemSlotIndex, _spellTargetCursor.Value);
        _targetedBattleSpell = null;
        _spellTargetCursor = null;
        return command;
    }

    private GameCommand? BeginSpellTargeting(CoopSignalRClient client, CharacterId characterId,
        SessionSnapshot snapshot, BattleSpellOption spell)
    {
        if (spell.ValidTargets.Count == 0)
        {
            SetMessage("A gyorshely varázslatának nincs érvényes célpontja.", ConsoleColor.Red);
            return null;
        }
        if (spell.TargetType is SpellTargetType.Self or SpellTargetType.Party)
            return _spellCastingInBattle
                ? new BattleActionCommand(client.PlayerId!.Value, client.NextCommandId(), characterId,
                    snapshot.Battle!.BattleId, snapshot.Battle.TurnId, BattleActionKind.CastSpell, spell.SpellId,
                    spell.CastingItemSlotIndex, spell.ValidTargets[0])
                : new CastExplorationSpellCommand(client.PlayerId!.Value, client.NextCommandId(), characterId,
                    spell.SpellId, spell.CastingItemSlotIndex, spell.ValidTargets[0]);
        _targetedBattleSpell = spell;
        _spellTargetCursor = spell.ValidTargets[0];
        Interlocked.Exchange(ref _redrawRequested, 1);
        return null;
    }

    private static IReadOnlyList<BattleSpellOption> OwnExplorationSpellOptions(SessionSnapshot snapshot,
        CharacterId characterId) => snapshot.Party.FirstOrDefault(character => character.CharacterId == characterId)
        ?.ExplorationSpellOptions ?? [];

    private IReadOnlyList<BattleSpellOption> CurrentSpellOptions(SessionSnapshot snapshot,
        CharacterId characterId) => _spellCastingInBattle
        ? snapshot.Battle?.SpellOptions ?? []
        : OwnExplorationSpellOptions(snapshot, characterId);

    private static bool TryGetFunctionKeyIndex(ConsoleKey key, out int index)
    {
        index = (int)key - (int)ConsoleKey.F1;
        return key is >= ConsoleKey.F1 and <= ConsoleKey.F8;
    }

    private async Task HandleInventoryInputAsync(CoopSignalRClient client, CharacterId characterId,
        SessionSnapshot snapshot, ConsoleKey key, CancellationToken cancellationToken)
    {
        var sheetCharacters = snapshot.Party.Where(character =>
            character.CharacterId == characterId || character.IsTemporaryFollower).ToArray();
        if (key is ConsoleKey.LeftArrow or ConsoleKey.RightArrow && sheetCharacters.Length > 0)
        {
            var currentIndex = Array.FindIndex(sheetCharacters, character =>
                character.CharacterId == (_displayedCharacterId ?? characterId));
            if (currentIndex < 0) currentIndex = 0;
            var direction = key == ConsoleKey.LeftArrow ? -1 : 1;
            _displayedCharacterId = sheetCharacters[(currentIndex + direction + sheetCharacters.Length) %
                                                     sheetCharacters.Length].CharacterId;
            _inventorySelection = 0;
            _inventorySource = null;
            Interlocked.Exchange(ref _redrawRequested, 1);
            return;
        }
        var own = snapshot.Party.FirstOrDefault(character =>
            character.CharacterId == (_displayedCharacterId ?? characterId));
        if (own?.IsTemporaryFollower == true)
        {
            if (GameInputBindings.IsCharacterSheetToggle(key)) CloseInventory();
            else SetMessage("A követő NPC inventoryja csak megtekinthető.", ConsoleColor.DarkYellow);
            return;
        }
        var inventory = own?.Inventory;
        if (inventory is null)
        {
            if (GameInputBindings.IsCharacterSheetToggle(key)) CloseInventory();
            else SetMessage("A követő NPC inventoryja nem módosítható.", ConsoleColor.DarkYellow);
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
                if (useSlot.Kind == InventorySlotKind.Backpack && useSlot.Item is not null &&
                    SpellcastingRules.IsSpellcastingFocusId(useSlot.Item.DefinitionId))
                {
                    _spellInfoOpen = true;
                    _spellInfoSelection = 0;
                }
                else if (useSlot.Kind == InventorySlotKind.Backpack && useSlot.Item is not null)
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
            case InventoryInputAction.SplitStack when slots.Count > 0:
                var splitSlot = slots[_inventorySelection];
                if (_inventorySource is not null)
                    SetMessage("Előbb fejezd be vagy szakítsd meg a tárgy mozgatását.");
                else if (splitSlot.Kind != InventorySlotKind.Backpack)
                    SetMessage("Hátizsákban levő köteget jelölj ki a felezéshez.");
                else if (splitSlot.Item is null || splitSlot.Item.Quantity < 2)
                    SetMessage("A kijelölt tárgy nem több darabos köteg.");
                else if (!slots.Any(slot => slot.Kind == InventorySlotKind.Backpack && slot.Item is null))
                    SetMessage("A hátizsákban nincs üres hely a köteg felezéséhez.");
                else
                    command = new SplitInventoryStackCommand(client.PlayerId!.Value, client.NextCommandId(),
                        characterId, inventory.Revision, splitSlot.Index);
                break;
            case InventoryInputAction.DistributeStack when slots.Count > 0:
                var distributeSlot = slots[_inventorySelection];
                if (_inventorySource is not null)
                    SetMessage("Előbb fejezd be vagy szakítsd meg a tárgy mozgatását.");
                else if (distributeSlot.Kind != InventorySlotKind.Backpack || distributeSlot.Item is null)
                    SetMessage("Elfogyasztható hátizsáktárgyat jelölj ki a szétosztáshoz.");
                else
                    command = new DistributeInventoryStackCommand(client.PlayerId!.Value, client.NextCommandId(),
                        characterId, inventory.Revision, distributeSlot.Index);
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
                    var inspection = ItemInspectionFormatter.Format(definition, _gameData, inspectSlot.Item.Charges,
                        own?.CharacterSheet?.WeaponProficiencyRanks);
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
        _spellInfoOpen = false;
        _inventorySource = null;
        _displayedCharacterId = null;
        Interlocked.Exchange(ref _redrawRequested, 1);
    }

    private void Draw(CoopSignalRClient client, CoopCharacterOption selected)
    {
        var snapshot = client.CurrentSnapshot;
        if (snapshot is not null)
        {
            _backgroundMusic.SynchronizeMazeLevel(snapshot.MazeLevel,
                exitDiscovered: snapshot.World?.Exit is not null,
                inInn: snapshot.Phase == GameSessionPhase.Inn);
            SynchronizeInnTransactions(snapshot);
            SynchronizeSessionSounds(snapshot, selected.CharacterId);
            SynchronizeSessionActivities(snapshot, selected.CharacterId);
        }
        if (snapshot?.World is not { } world)
        {
            ResetConsole();
            WriteLine($"=== COOP VENDÉG — {selected.Name} ===", ConsoleColor.Yellow);
            WriteLine("Várakozás a host első snapshotjára…", ConsoleColor.DarkCyan);
            _lastFrame = null;
            return;
        }

        var own = snapshot.Party.FirstOrDefault(character => character.CharacterId == selected.CharacterId);
        var ownsCharacter = snapshot.CharacterControls.Any(control =>
            control.CharacterId == selected.CharacterId && control.AssignedPlayerId == client.PlayerId &&
            control.ConnectionState == PlayerConnectionState.Connected);
        if (!ownsCharacter || own?.CharacterSheet is null || own.Inventory is null)
        {
            ResetConsole();
            WriteLine($"=== COOP VENDÉG — {selected.Name} ===", ConsoleColor.Yellow);
            WriteLine("A karakter teljes állapotának szinkronizálása…", ConsoleColor.DarkCyan);
            _lastFrame = null;
            return;
        }

        var frame = BuildFrame(client, selected, snapshot, world);
        RenderFrame(frame, _lastFrame);
        _lastFrame = frame;
    }

    private void SynchronizeInnTransactions(SessionSnapshot snapshot)
    {
        if (snapshot.Inn is not { } inn) return;
        foreach (var transaction in inn.Transactions.Where(transaction =>
                     transaction.Sequence > _lastInnTransactionSequence).OrderBy(transaction => transaction.Sequence))
        {
            var message = transaction.Kind switch
            {
                InnTransactionKind.Purchase => $"🏰 {transaction.ActorName} megvette: {transaction.ItemName} " +
                                               $"({transaction.Price} arany) → {transaction.InventoryOwnerName}",
                InnTransactionKind.Sale => $"🏰 {transaction.ActorName} eladta: {transaction.ItemName} " +
                                           $"({transaction.Price} arany) ← {transaction.InventoryOwnerName}",
                _ => $"🏰 {transaction.ActorName}: {transaction.ItemName}"
            };
            SetMessage(message, ConsoleColor.Yellow);
            _lastInnTransactionSequence = transaction.Sequence;
        }
    }

    private void SynchronizeSessionActivities(SessionSnapshot snapshot, CharacterId characterId)
    {
        foreach (var activity in (snapshot.Activities ?? []).Where(activity =>
                     activity.Sequence > _lastSessionActivitySequence).OrderBy(activity => activity.Sequence))
        {
            var prefix = activity.Kind switch
            {
                SessionActivityKind.Battle => "⚔ ",
                SessionActivityKind.Spell => "✨ ",
                SessionActivityKind.Support => "🤝 ",
                _ => string.Empty
            };
            if (activity.IsVisibleTo(characterId)) SetMessage(prefix + activity.Message, activity.Color);
            _lastSessionActivitySequence = activity.Sequence;
        }
    }

    private void SynchronizeSessionSounds(SessionSnapshot snapshot, CharacterId localCharacterId)
    {
        var sounds = snapshot.Sounds ?? [];
        if (!_sessionSoundsInitialized)
        {
            _lastSessionSoundSequence = sounds.Count == 0 ? 0 : sounds.Max(sound => sound.Sequence);
            _sessionSoundsInitialized = true;
            return;
        }

        foreach (var sound in sounds.Where(sound => sound.Sequence > _lastSessionSoundSequence)
                     .OrderBy(sound => sound.Sequence))
        {
            // A vendég egyetlen UI-hurka kezeli a rajzolást és a billentyűket is. A blokkoló
            // lejátszás MP3-on hangonként legalább egy másodpercre megakasztaná mindkettőt.
            if (sound.IsAudibleTo(localCharacterId)) _soundEffects.Play(sound.Effect);
            _lastSessionSoundSequence = sound.Sequence;
        }
    }

    private GuestRenderFrame BuildFrame(CoopSignalRClient client, CoopCharacterOption selected,
        SessionSnapshot snapshot, WorldSnapshot world)
    {
        var windowWidth = SafeWindowWidth();
        var windowHeight = SafeWindowHeight();
        var mapWidth = Math.Min(world.Width, Math.Max(1, windowWidth - CharacterSheetPanel.Width - 4));
        var mapHeight = Math.Min(world.Height, Math.Max(1, windowHeight - MessageLineCount - 1));
        _messageLineWidth = Math.Max(1, mapWidth - 4);
        var grid = new GuestMapCell[mapWidth, mapHeight];
        for (var y = 0; y < mapHeight; y++)
            for (var x = 0; x < mapWidth; x++)
                grid[x, y] = y < world.Height
                    ? new GuestMapCell("█", ConsoleColor.Black)
                    : new GuestMapCell(" ", ConsoleColor.Gray);
        foreach (var cell in world.RevealedCells)
            Put(grid, cell.Position, char.ConvertFromUtf32(cell.TileCodePoint), cell.ForegroundColor,
                cell.BackgroundColor);
        foreach (var door in world.Doors)
            Put(grid, door.Position, char.ConvertFromUtf32(door.SymbolCodePoint), door.ForegroundColor,
                door.BackgroundColor);
        foreach (var enemy in world.Enemies)
            Put(grid, enemy.Position, char.ConvertFromUtf32(enemy.SymbolCodePoint), enemy.Color);
        foreach (var chest in world.Chests)
            Put(grid, chest.Position, char.ConvertFromUtf32(chest.SymbolCodePoint), chest.ForegroundColor,
                chest.BackgroundColor);
        foreach (var corpse in world.Corpses)
            Put(grid, corpse.Position, char.ConvertFromUtf32(corpse.SymbolCodePoint), corpse.ForegroundColor,
                corpse.BackgroundColor);
        foreach (var pile in world.GroundPiles)
            Put(grid, pile.Position, char.ConvertFromUtf32(pile.SymbolCodePoint), pile.ForegroundColor,
                pile.BackgroundColor);
        foreach (var npc in world.Npcs ?? [])
            Put(grid, npc.Position, char.ConvertFromUtf32(npc.SymbolCodePoint), npc.ForegroundColor,
                npc.BackgroundColor);
        foreach (var character in snapshot.Party.Where(character => character.Position is not null))
            Put(grid, character.Position!.Value, CharacterSheetPanel.CharacterClassGlyph(character.CharacterClassId),
                character.Color);
        if (_doorTargetAction is not null && _doorTargetCandidates.Count > 0)
        {
            _doorTargetSelection = Math.Clamp(_doorTargetSelection, 0, _doorTargetCandidates.Count - 1);
            for (var index = 0; index < _doorTargetCandidates.Count; index++)
                Put(grid, _doorTargetCandidates[index], index == _doorTargetSelection ? "╳" : "◇",
                    index == _doorTargetSelection ? ConsoleColor.Green : ConsoleColor.DarkCyan,
                    ConsoleColor.DarkBlue);
        }

        var stillControlled = snapshot.CharacterControls.Any(control =>
            control.CharacterId == selected.CharacterId && control.AssignedPlayerId == client.PlayerId &&
            control.ConnectionState == PlayerConnectionState.Connected);
        var own = snapshot.Party.FirstOrDefault(character => character.CharacterId ==
            (_inventoryOpen ? _displayedCharacterId ?? selected.CharacterId : selected.CharacterId));
        ApplyBattleSpellUi(grid, snapshot, own);
        ApplyInnUi(grid, snapshot);
        ApplyRestSummaryUi(grid, snapshot, client.PlayerId);
        ApplyNarrativeUi(grid, snapshot, client.PlayerId);
        ApplySpellPreparationUi(grid, snapshot, own);
        ApplyLevelUpUi(grid, snapshot, own);
        var panelLines = _spellInfoOpen && own?.SpellInfo is not null
            ? SpellInfoPanel.Build(own.Name, own.CharacterClassId, own.Level, own.SpellInfo,
                _spellInfoSelection, focused: _inventoryOpen).ToDictionary(line => line.Row)
            : own?.CharacterSheet is not null && own.Inventory is not null
                ? CharacterSheetPanel.Build(own, snapshot.MazeLevel, snapshot.GoldenKeyCount,
                    snapshot.BossKeyCount, own.CharacterId == snapshot.LeaderCharacterId)
                    .ToDictionary(line => line.Row)
                : [];
        var selectedSlot = _inventoryOpen && own is { IsTemporaryFollower: false,
            Inventory.Slots.Count: > 0 } && own.Inventory is { } inventory
            ? new InventorySlotAddress(inventory.Slots[Math.Clamp(_inventorySelection, 0, inventory.Slots.Count - 1)].Kind,
                inventory.Slots[Math.Clamp(_inventorySelection, 0, inventory.Slots.Count - 1)].Index)
            : (InventorySlotAddress?)null;
        var panelHeight = mapHeight + MessageLineCount + 1;
        var panel = new GuestTextLine[panelHeight];
        var partyStatuses = new PartyStatusLine?[panelHeight];
        var resourceLine = !_spellInfoOpen && own?.CharacterSheet is not null
            ? CharacterSheetPanel.BuildResourceLine(own)
            : null;
        for (var y = 0; y < panelHeight; y++)
        {
            if (panelLines.TryGetValue(y, out var line))
            {
                var marker = line.InventorySlot is not null && line.InventorySlot == _inventorySource ? "*" : " ";
                panel[y] = new GuestTextLine(marker + line.Text, line.Color,
                    line.InventorySlot is not null && line.InventorySlot == selectedSlot
                        ? ConsoleColor.DarkCyan
                        : line.Background);
            }
            else
                panel[y] = new GuestTextLine(string.Empty, ConsoleColor.Gray, ConsoleColor.Black);
        }

        var partyMembers = _spellInfoOpen ? [] : snapshot.Party.Take(4).ToArray();
        for (var index = 0; index < partyMembers.Length; index++)
        {
            var member = partyMembers[index];
            if (41 + index < panel.Length)
            {
                panel[41 + index] = new GuestTextLine(string.Empty, ConsoleColor.Gray, ConsoleColor.Black);
                partyStatuses[41 + index] = CharacterSheetPanel.BuildPartyStatus(member,
                    member.CharacterId == selected.CharacterId,
                    member.CharacterId == snapshot.LeaderCharacterId);
            }
        }

        if (!stillControlled && panel.Length > 41)
            panel[41] = new GuestTextLine("Megfigyelő mód", ConsoleColor.DarkYellow, ConsoleColor.Black);

        if (!_spellInfoOpen)
        {
            var portrait = snapshot.Battle is { Enemy: { } battleEnemy }
                ? AsciiPortraits.ForEnemy(battleEnemy.DefinitionId)
                : AsciiPortraits.ForCharacterClass(own?.CharacterClassId ?? string.Empty);
            var portraitColor = snapshot.Battle is { Enemy: { } battleSnapshotEnemy }
                ? world.Enemies.FirstOrDefault(candidate => candidate.DefinitionId == battleSnapshotEnemy.DefinitionId)?.Color
                  ?? ConsoleColor.Red
                : own?.Color ?? ConsoleColor.Cyan;
            var pictureTop = Math.Max(0, panel.Length - 7);
            var pictureStyle = WindowFrameConfiguration.For(FramedWindow.CreaturePortrait);
            panel[pictureTop] = new GuestTextLine(WindowFrameCatalog.Horizontal(pictureStyle,
                CharacterSheetPanel.Width), ConsoleColor.DarkCyan, ConsoleColor.Black);
            for (var index = 0; index < 5; index++)
            {
                var line = index < portrait.Lines.Count ? portrait.Lines[index] : string.Empty;
                var sides = WindowFrameCatalog.Sides(pictureStyle, index, 5);
                var interiorWidth = CharacterSheetPanel.Width - sides.Left.Length - sides.Right.Length;
                panel[pictureTop + index + 1] = new GuestTextLine(
                    sides.Left + CenterPortrait(line, portrait.CanvasWidth, interiorWidth) + sides.Right,
                    portraitColor, ConsoleColor.Black);
            }
            panel[pictureTop + 6] = new GuestTextLine(WindowFrameCatalog.Horizontal(pictureStyle,
                CharacterSheetPanel.Width, bottom: true), ConsoleColor.DarkCyan, ConsoleColor.Black);
        }

        var messages = _messageLog.ToArray();
        var messageStart = Math.Max(0, messages.Length - MessageLineCount - _messageLogScrollOffset);
        var messageEnd = messages.Length - _messageLogScrollOffset;
        var footer = new GuestTextLine[MessageLineCount];
        for (var index = 0; index < footer.Length; index++)
        {
            var messageIndex = messageStart + index;
            footer[index] = messageIndex < messages.Length && messageIndex < messageEnd
                ? messages[messageIndex]
                : new GuestTextLine(string.Empty, ConsoleColor.Gray, ConsoleColor.Black);
        }
        if (_targetedBattleSpell is { } targeted && _spellTargetCursor is { } cursor)
            footer[^1] = new GuestTextLine($"╳ {targeted.Name} — {ConsoleRenderer.SpellTargetName(targeted.TargetType)}, " +
                $"táv {targeted.Range}{(targeted.AreaRadius > 0 ? $", sugár {targeted.AreaRadius}" : string.Empty)} | " +
                $"({cursor.X},{cursor.Y}) | Enter: célzás, Tab: következő, Esc: mégse",
                targeted.ValidTargets.Contains(cursor) ? ConsoleColor.Cyan : ConsoleColor.DarkYellow,
                ConsoleColor.Black);
        else if (_doorTargetAction is { } doorAction && _doorTargetCandidates.Count > 0)
            footer[^1] = new GuestTextLine(
                $"╳ Ajtó kiválasztása ({(doorAction == CharacterAction.OpenDoor ? "nyitás" : "bezárás/zárás")})" +
                " — nyilak/Tab, Enter: kész, Esc: mégse",
                ConsoleColor.Cyan, ConsoleColor.Black);

        return new GuestRenderFrame(world.WorldId, windowWidth, windowHeight, mapWidth, mapHeight, grid, panel,
            partyStatuses, footer, resourceLine);
    }

    private static bool TryGetBattleTacticAction(BattleSnapshot battle, ConsoleKey key,
        out BattleActionKind action)
    {
        var option = key is ConsoleKey.D1 or ConsoleKey.NumPad1 ? 1 :
            key is ConsoleKey.D2 or ConsoleKey.NumPad2 ? 2 :
            key is ConsoleKey.D3 or ConsoleKey.NumPad3 ? 3 : 0;
        action = battle.AllowedActions.Contains(BattleActionKind.FighterPrecise)
            ? option switch
            {
                1 => BattleActionKind.FighterPrecise,
                2 => BattleActionKind.FighterPowerful,
                3 => BattleActionKind.FighterDefensive,
                _ => default
            }
            : option switch
            {
                1 => BattleActionKind.ThiefAmbush,
                2 => BattleActionKind.ThiefObserve,
                3 => BattleActionKind.ThiefPoison,
                _ => default
            };
        return option > 0 && battle.AllowedActions.Contains(action);
    }

    private void ApplyInnUi(GuestMapCell[,] grid, SessionSnapshot snapshot)
    {
        if (snapshot.Phase != GameSessionPhase.Inn || snapshot.Inn is not { } inn)
        {
            _innVendor = null;
            _innMageMenuOpen = false;
            _innRumorOpen = false;
            _innSelection = 0;
            return;
        }
        if (inn.LevelCompletion is { } completion)
        {
            DrawGuestOverlay(grid, ConsoleRenderer.BuildLevelCompletionLines(completion), ConsoleColor.Magenta,
                ConsoleRenderer.LevelCompletionFrameWidth, FramedWindow.Inn);
            return;
        }
        List<(string Text, ConsoleColor Color)> lines;
        if (_innVendor is null)
        {
            var options = inn.MenuOptions ?? [];
            _innSelection = Math.Clamp(_innSelection, 0, Math.Max(0, options.Count - 1));
            lines = ConsoleRenderer.BuildInnMenuLines(inn.PartyCount, inn.PartyGold, options,
                _innSelection, inn.ArtisanNotice, disableLeaderOnly: true, inn.InnName, inn.MazeLevel).ToList();
        }
        else
        {
            var vendor = inn.Vendors.FirstOrDefault(candidate => candidate.Kind == _innVendor);
            if (_innMageMenuOpen)
            {
                var mageOptions = new[]
                {
                    ($"{ConsoleRenderer.WandIcon} Kiürült varázspálcák feltöltése", "Teljes feltöltés a pálca eredeti árának kétharmadáért.", true),
                    ("📜 Varázsportékák", "Egy véletlen varázspálca és egy véletlen tekercs, egyszeri készletről.", false),
                    ("🔮 Varázstárgy azonosítása", "Az azonosítás szolgáltatása hamarosan elérhető lesz.", true),
                    ("🚪 Vissza", "Visszatérés a fogadó főtermébe.", false)
                };
                lines = ConsoleRenderer.BuildWanderingMageMenuLines(inn.PartyGold, mageOptions,
                    _innSelection, "A vándormágus köpenye alól halk, kékes fény szűrődik ki.").ToList();
                DrawGuestOverlay(grid, lines, ConsoleColor.Magenta, ConsoleRenderer.InnMenuFrameWidth,
                    FramedWindow.Inn);
                return;
            }
            var own = snapshot.Party.FirstOrDefault(character => character.Inventory is not null);
            var sellOffers = vendor?.Kind == InnVendorKind.Market && _innMarketMode == InnMarketMode.Sell
                ? GuestInnSellOffers(inn, own).ToArray()
                : [];
            var entryCount = vendor?.Kind == InnVendorKind.Market && _innMarketMode == InnMarketMode.Sell
                ? sellOffers.Length : vendor?.Offers.Count ?? 0;
            _innSelection = entryCount == 0 ? 0 : Math.Clamp(_innSelection, 0, entryCount - 1);
            var displaySellOffers = sellOffers.Select(offer =>
                (offer.Slot.Item!, offer.Price, own?.Name ?? string.Empty)).ToArray();
            lines = ConsoleRenderer.BuildInnVendorLines(vendor!, _innMarketMode, displaySellOffers,
                _innSelection, inn.PartyGold,
                own?.Inventory?.Slots.Count(slot => slot.Kind == InventorySlotKind.Backpack && slot.Item is null) ?? 0,
                vendor?.Kind == InnVendorKind.Market && _innMarketMode == InnMarketMode.Sell
                    ? "Csak a saját hátizsákod tárgyai adhatók el."
                    : "Válassz a fogadó kínálatából.").ToList();
        }
        DrawGuestOverlay(grid, lines, ConsoleColor.Magenta,
            _innVendor is null ? ConsoleRenderer.InnMenuFrameWidth : ConsoleRenderer.InnMarketFrameWidth,
            FramedWindow.Inn);
        if (_innRumorOpen) ApplyInnRumorUi(grid, inn);
    }

    private void ApplyInnRumorUi(GuestMapCell[,] grid, InnSnapshot inn)
    {
        if (inn.Rumors.Count == 0) { _innRumorOpen = false; return; }
        _innRumorSelection = Math.Clamp(_innRumorSelection, 0, inn.Rumors.Count - 1);
        var rumor = inn.Rumors[_innRumorSelection];
        var lines = ConsoleRenderer.BuildInnRumorLines(rumor, _innRumorSelection, inn.Rumors.Count).ToList();
        const int desiredWidth = ConsoleRenderer.InnRumorFrameWidth;
        var width = Math.Min(desiredWidth, Math.Max(10, grid.GetLength(0) - 2));
        var maxRows = Math.Max(1, grid.GetLength(1) - 2);
        if (lines.Count > maxRows)
        {
            var footer = lines[^1];
            lines = lines.Take(Math.Max(0, maxRows - 1)).Append(footer).ToList();
        }
        DrawGuestOverlay(grid, lines, ConsoleColor.Magenta, width, FramedWindow.Inn);
    }

    private void ApplyNarrativeUi(GuestMapCell[,] grid, SessionSnapshot snapshot, PlayerId? playerId)
    {
        if (snapshot.Narrative is not { } narrative) return;
        var acknowledged = playerId is not null && narrative.AcknowledgedPlayerIds.Contains(playerId.Value);
        var lines = NarrativeWindow.Build(narrative.Title, narrative.Subtitle, narrative.Paragraphs,
            acknowledged
                ? "❖  Várakozás a másik játékosra…  ❖"
                : "❖  Nyomj Entert a történet folytatásához...  ❖",
            acknowledged ? ConsoleColor.DarkCyan : ConsoleColor.Green, narrative.Kind, narrative.Boss).ToList();
        const int desiredWidth = NarrativeWindow.Width;
        var width = Math.Min(desiredWidth, Math.Max(10, grid.GetLength(0) - 2));
        var maxContentRows = Math.Max(1, grid.GetLength(1) - 2);
        if (lines.Count > maxContentRows)
        {
            var footer = lines[^1];
            lines = lines.Take(maxContentRows - 1).Append(footer).ToList();
        }
        DrawGuestOverlay(grid, lines, ConsoleColor.Magenta, width, FramedWindow.Storyline);
    }

    private bool? GetGuestThiefKeyChoice(CoopSignalRClient client, CoopCharacterOption selected,
        SessionSnapshot snapshot, CharacterAction action, Position? targetDoorPosition)
    {
        var character = snapshot.Party.FirstOrDefault(candidate => candidate.CharacterId == selected.CharacterId);
        var door = targetDoorPosition is { } target
            ? snapshot.World?.Doors.FirstOrDefault(candidate => candidate.Position == target)
            : null;
        var hasKey = character?.Inventory?.Slots.Any(slot => slot.Kind == InventorySlotKind.Backpack &&
            string.Equals(slot.Item?.DefinitionId, MiscItemIds.Key, StringComparison.OrdinalIgnoreCase)) == true;
        if (character is null || !CharacterClassRules.IsThief(character.CharacterClassId) || !hasKey || door is null ||
            action switch
            {
                CharacterAction.OpenDoor => door.State != DoorState.Locked,
                CharacterAction.CloseOrLockDoor => door.State != DoorState.Closed,
                _ => true
            }) return null;

        SetMessage("🔑 Felhasználjuk a kulcsot? I/Y/Enter: igen | N/Esc: nem, jöjjön a tolvajpróba",
            ConsoleColor.Yellow);
        Draw(client, selected);
        while (true)
        {
            var key = Console.ReadKey(intercept: true).Key;
            if (key is ConsoleKey.I or ConsoleKey.Y or ConsoleKey.Enter) return true;
            if (key is ConsoleKey.N or ConsoleKey.Escape) return false;
        }
    }

    private static void ApplyRestSummaryUi(GuestMapCell[,] grid, SessionSnapshot snapshot, PlayerId? playerId)
    {
        if (snapshot.RestNotice is not { } rest) return;
        var acknowledged = playerId is not null && rest.AcknowledgedPlayerIds.Contains(playerId.Value);
        var lines = RestSummaryWindow.Build(rest,
            acknowledged ? "❖  Várakozás a másik játékosra…  ❖" : "❖  Nyomj Entert a folytatáshoz...  ❖",
            acknowledged ? ConsoleColor.DarkCyan : ConsoleColor.Green);
        DrawGuestOverlay(grid, lines, ConsoleColor.Magenta, RestSummaryWindow.Width, FramedWindow.Inn);
    }

    private void ApplySpellPreparationUi(GuestMapCell[,] grid, SessionSnapshot snapshot,
        SessionCharacterSnapshot? own)
    {
        if (snapshot.SpellPreparation is not { } preparation || own?.CharacterId != preparation.CharacterId) return;
        if (_spellPreparationPromptId != preparation.PromptId)
        {
            _spellPreparationPromptId = preparation.PromptId;
            _spellPreparationCursor = 0;
            _preparedSpellIds.Clear();
            _preparedSpellIds.UnionWith(preparation.SelectedSpellIds);
        }
        var spells = preparation.Spells;
        _spellPreparationCursor = spells.Count == 0 ? 0 : Math.Clamp(_spellPreparationCursor, 0, spells.Count - 1);
        var lines = MagicProgressionWindow.BuildPreparation(preparation.CharacterName, _preparedSpellIds.Count,
            preparation.Capacity, spells, _preparedSpellIds, _spellPreparationCursor);
        DrawGuestOverlay(grid, lines, ConsoleColor.Magenta, MagicProgressionWindow.PreparationWidth,
            FramedWindow.SpellPreparation);
    }

    private void ApplyLevelUpUi(GuestMapCell[,] grid, SessionSnapshot snapshot, SessionCharacterSnapshot? own)
    {
        if (snapshot.LevelUpPrompt is not { } prompt || own?.CharacterId != prompt.CharacterId) return;
        if (_levelUpPromptId != prompt.PromptId)
        { _levelUpPromptId = prompt.PromptId; _levelUpSelection = 0; }
        List<(string Text, ConsoleColor Color)> lines;
        if (prompt.Kind == LevelUpPromptKind.Summary)
        {
            var details = own.CharacterSheet;
            lines = LevelUpWindow.BuildSummary(prompt.CharacterName, prompt.PreviousLevel, prompt.CurrentLevel,
                prompt.Bonuses ?? [], prompt.VitalityGained, prompt.ManaGained, details?.UsesMana == true,
                own.CurrentVitality, own.MaximumVitality, own.CurrentMana, own.MaximumMana, prompt.Message).ToList();
        }
        else if (LevelUpWindow.UsesSwordFrame(prompt.Kind))
        {
            _levelUpSelection = Math.Clamp(_levelUpSelection, 0, Math.Max(0, prompt.Choices.Count - 1));
            lines = LevelUpWindow.BuildChoice(prompt.Kind, prompt.ContextLines ?? [], prompt.Choices,
                _levelUpSelection).ToList();
        }
        else
        {
            _levelUpSelection = Math.Clamp(_levelUpSelection, 0, Math.Max(0, prompt.Choices.Count - 1));
            lines = MagicProgressionWindow.BuildLearning(prompt.CharacterName, prompt.Message, prompt.Choices,
                _levelUpSelection).ToList();
        }
        var framedWindow = prompt.Kind == LevelUpPromptKind.Summary
            ? FramedWindow.LevelUp
            : LevelUpWindow.UsesSwordFrame(prompt.Kind) ? FramedWindow.LevelUpChoice : FramedWindow.SpellLearning;
        var width = prompt.Kind == LevelUpPromptKind.Summary
            ? LevelUpWindow.Width
            : LevelUpWindow.UsesSwordFrame(prompt.Kind) ? LevelUpWindow.ChoiceWidth(prompt.Kind) :
                MagicProgressionWindow.LearningWidth;
        DrawGuestOverlay(grid, lines, ConsoleColor.Yellow, width, framedWindow);
    }

    private static void DrawGuestOverlay(GuestMapCell[,] grid, IReadOnlyList<(string Text, ConsoleColor Color)> lines,
        ConsoleColor borderColor, int desiredWidth, FramedWindow? framedWindow = null)
    {
        var width = Math.Min(desiredWidth, Math.Max(10, grid.GetLength(0) - 2));
        var style = framedWindow is { } window
            ? WindowFrameConfiguration.For(window)
            : WindowFrameStyle.Double;
        var topAdornment = WindowFrameCatalog.Adornment(style, width);
        var bottomAdornment = WindowFrameCatalog.Adornment(style, width, bottom: true);
        var adornmentRows = topAdornment is null ? 0 : 2;
        var contentPadding = WindowFrameCatalog.ContentPadding(style);
        var contentWidth = Math.Max(0, width - contentPadding * 2);
        var maximumRows = Math.Max(1, grid.GetLength(1) - 2 - adornmentRows);
        var visible = lines.Take(maximumRows).ToArray();
        var left = Math.Max(0, (grid.GetLength(0) - width) / 2);
        var top = Math.Max(0, (grid.GetLength(1) - visible.Length - 2 - adornmentRows) / 2);
        var frameTop = topAdornment is null ? top : top + 1;
        if (topAdornment is not null) DrawOverlayText(grid, left, top, topAdornment, borderColor);
        DrawOverlayText(grid, left, frameTop, WindowFrameCatalog.Horizontal(style, width), borderColor);
        for (var row = 0; row < visible.Length; row++)
        {
            var sides = WindowFrameCatalog.Sides(style, row, visible.Length);
            var value = visible[row].Text.Length > contentWidth ? visible[row].Text[..contentWidth] : visible[row].Text;
            DrawOverlayText(grid, left, frameTop + row + 1,
                sides.Left + new string(' ', width - sides.Left.Length - sides.Right.Length) + sides.Right,
                borderColor);
            DrawOverlayText(grid, left + contentPadding, frameTop + row + 1,
                value.PadRight(contentWidth), visible[row].Color);
        }
        var frameBottom = frameTop + visible.Length + 1;
        DrawOverlayText(grid, left, frameBottom,
            WindowFrameCatalog.Horizontal(style, width, bottom: true), borderColor);
        if (bottomAdornment is not null)
            DrawOverlayText(grid, left, frameBottom + 1, bottomAdornment, borderColor);
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
        if (!_battleSpellMenuOpen || own is null) return;
        var options = CurrentSpellOptions(snapshot, own.CharacterId);
        _battleSpellSelection = options.Count == 0 ? 0 : Math.Clamp(_battleSpellSelection, 0, options.Count - 1);
        var visibleStart = options.Count == 0 ? 0 : Math.Clamp(
            _battleSpellSelection - SpellSelectorWindow.PageSize / 2, 0,
            Math.Max(0, options.Count - SpellSelectorWindow.PageSize));
        var projected = options.Select(spell => new SpellSelectorOption(spell.Name, spell.Level, spell.ManaCost,
            spell.TargetType, spell.CastingItemKind switch
            {
                MagicItemKind.Scroll => "📜",
                MagicItemKind.Wand => $"{ConsoleRenderer.WandIcon}{spell.Charges}",
                _ => spell.QuickSlot is { } slot ? $"F{slot + 1}" : "--"
            }, own.CurrentMana >= spell.ManaCost)).ToArray();
        var lines = SpellSelectorWindow.Build(own.Name, own.CurrentMana, own.MaximumMana,
            _spellCastingInBattle, projected, _battleSpellSelection, visibleStart);
        DrawGuestOverlay(grid, lines, ConsoleColor.Magenta, SpellSelectorWindow.Width,
            FramedWindow.SpellSelector);
    }

    private static void DrawOverlayText(GuestMapCell[,] grid, int x, int y, string text, ConsoleColor color)
    {
        var elements = StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext())
        {
            var element = elements.GetTextElement();
            var displayWidth = element.EnumerateRunes().Any(rune =>
                rune.Value > char.MaxValue || rune.Value is 0xFE0F or 0x200D) ? 2 : 1;
            Put(grid, new Position(x, y), element, color);
            if (displayWidth == 2 && x + 1 < grid.GetLength(0) && y >= 0 && y < grid.GetLength(1))
                grid[x + 1, y] = new GuestMapCell(string.Empty, color, ConsoleColor.Black, IsContinuation: true);
            x += displayWidth;
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
                if (!frame.Map[x, y].IsContinuation &&
                    (fullRedraw || previous!.Map[x, y] != frame.Map[x, y]))
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

        if (frame.ResourceLine is { } resources &&
            (fullRedraw || previous!.ResourceLine != resources || previous.Panel[5] != frame.Panel[5]))
            WriteCharacterResourceAt(frame.MapWidth + 3, 5, resources);

        for (var row = 0; row < frame.PartyStatuses.Length; row++)
        {
            var status = frame.PartyStatuses[row];
            if (status is null)
            {
                // Teljes rajzoláskor a panel már elkészült, ezért a hiányzó státusz nem törölheti le.
                // Részleges rajzoláskor viszont egy megszűnt státusz helyére vissza kell tenni a panel sorát.
                if (!fullRedraw && previous!.PartyStatuses[row] is not null)
                    WriteAt(frame.MapWidth + 3, row, frame.Panel[row], CharacterSheetPanel.Width);
                continue;
            }

            if (!fullRedraw && previous!.PartyStatuses[row] == status &&
                previous.Panel[row] == frame.Panel[row]) continue;
            WritePartyStatusAt(frame.MapWidth + 3, row, status);
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

    private static void WritePartyStatusAt(int x, int y, PartyStatusLine? status)
    {
        WriteAt(x, y, new GuestTextLine(string.Empty, ConsoleColor.Gray, ConsoleColor.Black),
            CharacterSheetPanel.Width);
        if (status is null || !TrySetCursorPosition(x, y)) return;
        foreach (var (text, color) in new[]
                 {
                     (status.Identity, status.IdentityColor),
                     (status.Vitality, status.VitalityColor),
                     (status.Mana, status.ManaColor)
                 })
        {
            Console.ForegroundColor = color;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.Write(text);
        }
    }

    private static void WriteCharacterResourceAt(int x, int y, CharacterResourceLine resources)
    {
        WriteAt(x, y, new GuestTextLine(string.Empty, ConsoleColor.Gray, ConsoleColor.Black),
            CharacterSheetPanel.Width);
        if (!TrySetCursorPosition(x, y)) return;
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.BackgroundColor = ConsoleColor.Black;
        Console.Write(' ');
        foreach (var (text, color) in new[]
                 {
                     (resources.Vitality, resources.VitalityColor),
                     (resources.Mana, resources.ManaColor)
                 })
        {
            Console.ForegroundColor = color;
            Console.Write(text);
        }
    }

    private void SetMessage(string message, ConsoleColor color = ConsoleColor.DarkYellow)
    {
        foreach (var line in MessageTextLayout.Wrap(message, _messageLineWidth))
            _messageLog.Enqueue(new GuestTextLine(line, color, ConsoleColor.Black));
        while (_messageLog.Count > MessageBufferLineCount) _messageLog.Dequeue();
        _messageLogScrollOffset = 0;
        Interlocked.Exchange(ref _redrawRequested, 1);
    }

    private void ScrollMessageLog(bool towardOlderMessages)
    {
        var maximumOffset = Math.Max(0, _messageLog.Count - MessageLineCount);
        _messageLogScrollOffset = towardOlderMessages
            ? Math.Min(maximumOffset, _messageLogScrollOffset + MessageLineCount)
            : Math.Max(0, _messageLogScrollOffset - MessageLineCount);
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

    private static string CenterPortrait(string text, int canvasWidth, int interiorWidth = 25)
    {
        var canvas = text.PadRight(canvasWidth);
        var leftPadding = Math.Max(0, (interiorWidth - canvasWidth) / 2);
        return (new string(' ', leftPadding) + canvas).PadRight(interiorWidth);
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
        ConsoleColor Background = ConsoleColor.Black, bool IsContinuation = false);
    private readonly record struct GuestTextLine(string Text, ConsoleColor Foreground, ConsoleColor Background);
    private sealed record GuestRenderFrame(WorldId WorldId, int WindowWidth, int WindowHeight, int MapWidth,
        int MapHeight, GuestMapCell[,] Map, GuestTextLine[] Panel, PartyStatusLine?[] PartyStatuses,
        GuestTextLine[] Footers, CharacterResourceLine? ResourceLine);
}
