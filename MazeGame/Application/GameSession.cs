using System.Threading.Channels;
using MazeGame.Domain.Characters;
using MazeGame.Combat;
using MazeGame.Domain.Inventory;

namespace MazeGame.Application;

/// <summary>
/// Egy futó játék vezérlési határa. Sorosítja és jogosultság szerint szűri az összes helyi vagy hálózati
/// parancsot; a doménállapotot továbbra is kizárólag a Game szimulációs szála módosítja.
/// </summary>
public sealed class GameSession
{
    private readonly object _stateGate = new();
    private readonly Party _party;
    private readonly Channel<GameCommand> _commands = Channel.CreateUnbounded<GameCommand>(
        new UnboundedChannelOptions { SingleReader = true, AllowSynchronousContinuations = false });
    private readonly Dictionary<CharacterId, CharacterControlState> _controls = [];
    private readonly Dictionary<PlayerId, long> _lastCommandIds = [];
    private readonly HashSet<PlayerId> _players = [];
    private long _eventSequence;
    private long _snapshotSequence;
    private BattleId? _activeBattleId;
    private long _activeBattleTurnId;
    private CharacterId? _actingBattleCharacterId;
    private IReadOnlySet<BattleActionKind> _allowedBattleActions = new HashSet<BattleActionKind>();
    private GameSessionPhase _phase = GameSessionPhase.Exploration;

    public GameSession(Party party, LiveCharacter leader, PlayerId? hostPlayerId = null)
    {
        _party = party;
        HostPlayerId = hostPlayerId ?? PlayerId.New();
        _players.Add(HostPlayerId);
        SynchronizeParty();
        _controls[leader.Id] = new CharacterControlState(leader.Id, CharacterControllerKind.HostPlayer,
            HostPlayerId, PlayerConnectionState.Connected);
    }

    public PlayerId HostPlayerId { get; }
    public GameSessionPhase Phase { get { lock (_stateGate) return _phase; } }
    public IReadOnlyCollection<CharacterControlState> CharacterControls
    {
        get { lock (_stateGate) return _controls.Values.ToArray(); }
    }
    public int ConnectedRemoteCharacterCount
    {
        get
        {
            lock (_stateGate) return _controls.Values.Count(control =>
                control.ControllerKind == CharacterControllerKind.RemotePlayer &&
                control.ConnectionState == PlayerConnectionState.Connected);
        }
    }
    public event Action<GameSessionEvent>? EventPublished;

    public bool IsHumanControlled(CharacterId characterId)
    {
        lock (_stateGate) return _controls.TryGetValue(characterId, out var control) &&
            control.ControllerKind is CharacterControllerKind.HostPlayer or CharacterControllerKind.RemotePlayer &&
            control.ConnectionState == PlayerConnectionState.Connected;
    }

    public IReadOnlyList<CoopCharacterOption> GetAvailableRemoteCharacters()
    {
        lock (_stateGate)
        {
            SynchronizeParty();
            return _party.Members.Where(character => character.IsAlive && _controls[character.Id] is
            { ControllerKind: CharacterControllerKind.Npc, AssignedPlayerId: null })
                .Select(character => new CoopCharacterOption(character.Id, character.Name,
                    character.CharacterClass.Name, character.Level)).ToArray();
        }
    }

    public PlayerId RegisterRemotePlayer()
    {
        lock (_stateGate)
        {
            var playerId = PlayerId.New();
            _players.Add(playerId);
            return playerId;
        }
    }

    public bool TryAssignRemoteControl(PlayerId playerId, CharacterId characterId, out string error)
    {
        lock (_stateGate)
        {
            SynchronizeParty();
            if (!_players.Contains(playerId) || playerId == HostPlayerId)
                return Fail("Ismeretlen vagy nem távoli játékos.", out error);
            if (_party.Leader?.Id == characterId)
                return Fail("A party-leader irányítása nem adható át.", out error);
            if (!_controls.TryGetValue(characterId, out var control))
                return Fail("A karakter nem tagja a partinak.", out error);
            if (control.ControllerKind != CharacterControllerKind.Npc || control.AssignedPlayerId is not null)
                return Fail("A karaktert már emberi játékos irányítja vagy foglalja.", out error);

            SetControl(control with
            {
                ControllerKind = CharacterControllerKind.RemotePlayer,
                AssignedPlayerId = playerId,
                ConnectionState = PlayerConnectionState.Connected
            });
            error = string.Empty;
            return true;
        }
    }

    public bool TryJoinRemoteCharacter(PlayerId playerId, LiveCharacter character, out string error)
    {
        ArgumentNullException.ThrowIfNull(character);
        lock (_stateGate)
        {
            if (!_players.Contains(playerId) || playerId == HostPlayerId)
                return Fail("Ismeretlen vagy nem távoli játékos.", out error);
            if (!character.IsAlive) return Fail("Halott karakter nem csatlakozhat a partihoz.", out error);
            if (_controls.Values.Any(control => control.AssignedPlayerId == playerId))
                return Fail("Ehhez a játékoshoz már tartozik karakter.", out error);
            if (_party.Members.Any(member => member.Id == character.Id))
                return Fail("Ezzel az azonosítóval már van karakter a partiban.", out error);
            if (!_party.Add(character)) return Fail("A parti megtelt.", out error);

            _controls[character.Id] = new CharacterControlState(character.Id,
                CharacterControllerKind.RemotePlayer, playerId, PlayerConnectionState.Connected);
            Publish(sequence => new CharacterControlChangedEvent(sequence, _controls[character.Id]));
            error = string.Empty;
            return true;
        }
    }

    public void MarkPlayerDisconnected(PlayerId playerId)
    {
        lock (_stateGate)
        {
            if (playerId == HostPlayerId) return;
            foreach (var control in _controls.Values.Where(control => control.AssignedPlayerId == playerId).ToList())
                SetControl(control with
                {
                    ControllerKind = CharacterControllerKind.Npc,
                    ConnectionState = PlayerConnectionState.Disconnected
                });
        }
    }

    public bool TryReconnectPlayer(PlayerId playerId)
    {
        lock (_stateGate)
        {
            if (playerId == HostPlayerId || !_players.Contains(playerId)) return false;
            var reserved = _controls.Values.Where(control => control.AssignedPlayerId == playerId &&
                control.ConnectionState != PlayerConnectionState.Connected).ToList();
            foreach (var control in reserved)
                SetControl(control with
                {
                    ControllerKind = CharacterControllerKind.RemotePlayer,
                    ConnectionState = PlayerConnectionState.Connected
                });
            return true;
        }
    }

    public bool Submit(GameCommand command) => _commands.Writer.TryWrite(command);

    /// <summary>A queue-validáció után, a host végrehajtási rétegében felismert szemantikai hibát jelzi.</summary>
    public void RejectExecutedCommand(GameCommand command, string reason)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Az elutasítás oka nem lehet üres.", nameof(reason));
        lock (_stateGate)
            Publish(sequence => new GameCommandRejectedEvent(sequence, command.SenderId, command.CommandId, reason));
    }

    public void ReleaseCharacterControl(CharacterId characterId)
    {
        lock (_stateGate)
        {
            if (!_controls.TryGetValue(characterId, out var control) ||
                control.ControllerKind != CharacterControllerKind.RemotePlayer) return;
            SetControl(control with
            {
                ControllerKind = CharacterControllerKind.Npc,
                AssignedPlayerId = null,
                ConnectionState = PlayerConnectionState.Disconnected
            });
        }
    }

    public SessionSnapshot CreateSnapshot(SessionSnapshotContext context)
    {
        lock (_stateGate)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (context.MazeLevel <= 0) throw new ArgumentOutOfRangeException(nameof(context), "A pályaszintnek pozitívnak kell lennie.");
            if (string.IsNullOrWhiteSpace(context.LevelName)) throw new ArgumentException("A pályanév nem lehet üres.", nameof(context));
            SynchronizeParty();
            ValidateSnapshotBattle(context.Battle);

            var party = _party.Members.Select(character => new SessionCharacterSnapshot(
                character.Id,
                character.Name,
                character.Race.Id,
                character.CharacterClass.Id,
                character.Level,
                character.CurrentVitality,
                character.MaximumVitality,
                character.CurrentMana,
                character.MaximumMana,
                character.FoodLevel,
                character.WaterLevel,
                character.Gold,
                character.IsAlive,
                context.CharacterPositions.TryGetValue(character.Id, out var position) ? position : null,
                character.Statuses.Select(status => status.Id).ToArray(),
                InventorySnapshotProjector.Create(character), Color: character.Color)).ToArray();
            var controls = _party.Members.Select(character => _controls[character.Id]).ToArray();
            return new SessionSnapshot(SessionProtocol.Version, ++_snapshotSequence, _eventSequence, Phase,
                HostPlayerId, _party.Leader!.Id, context.MazeLevel, context.LevelName, party, controls, context.Battle,
                context.World);
        }
    }

    public void SetBattlePrompt(BattleId battleId, long turnId, CharacterId actingCharacterId,
        IReadOnlyList<BattleActionKind>? allowedActions = null)
    {
        lock (_stateGate)
        {
            if (turnId <= 0) throw new ArgumentOutOfRangeException(nameof(turnId));
            allowedActions ??= [BattleActionKind.PhysicalAttack];
            if (allowedActions.Count == 0) throw new ArgumentException("Legalább egy harci akció engedélyezése szükséges.", nameof(allowedActions));
            SetPhase(GameSessionPhase.Battle);
            _activeBattleId = battleId;
            _activeBattleTurnId = turnId;
            _actingBattleCharacterId = actingCharacterId;
            _allowedBattleActions = allowedActions.ToHashSet();
            Publish(sequence => new BattlePromptEvent(sequence, battleId, turnId, actingCharacterId,
                allowedActions));
        }
    }

    public void EndBattle(BattleId battleId)
    {
        lock (_stateGate)
        {
            if (_activeBattleId != battleId) return;
            _activeBattleId = null;
            _activeBattleTurnId = 0;
            _actingBattleCharacterId = null;
            _allowedBattleActions = new HashSet<BattleActionKind>();
            Publish(sequence => new BattleEndedEvent(sequence, battleId));
        }
    }

    /// <summary>Az első érvényes parancsot adja vissza; a megelőző hibás parancsokról eseményt bocsát ki.</summary>
    public bool TryReadCommand(out GameCommand command)
    {
        lock (_stateGate)
        {
            SynchronizeParty();
            while (_commands.Reader.TryRead(out var candidate))
            {
                if (Validate(candidate, out var reason))
                {
                    _lastCommandIds[candidate.SenderId] = candidate.CommandId;
                    command = candidate;
                    return true;
                }
                Publish(sequence => new GameCommandRejectedEvent(sequence, candidate.SenderId,
                    candidate.CommandId, reason));
            }
            command = null!;
            return false;
        }
    }

    public void SetPhase(GameSessionPhase phase)
    {
        lock (_stateGate)
        {
            if (_phase == phase) return;
            var previous = _phase;
            _phase = phase;
            Publish(sequence => new SessionPhaseChangedEvent(sequence, previous, phase));
        }
    }

    public void SynchronizeParty()
    {
        lock (_stateGate)
        {
            var members = _party.Members.Select(character => character.Id).ToHashSet();
            foreach (var removed in _controls.Keys.Where(id => !members.Contains(id)).ToList()) _controls.Remove(removed);
            foreach (var character in _party.Members)
            {
                if (_controls.ContainsKey(character.Id)) continue;
                _controls[character.Id] = character == _party.Leader
                    ? new CharacterControlState(character.Id, CharacterControllerKind.HostPlayer, HostPlayerId,
                        PlayerConnectionState.Connected)
                    : new CharacterControlState(character.Id, CharacterControllerKind.Npc, null,
                        PlayerConnectionState.Connected);
            }
        }
    }

    private bool Validate(GameCommand command, out string reason)
    {
        if (!_players.Contains(command.SenderId))
            return Fail("A parancs küldője nem tagja a sessionnek.", out reason);
        if (command.CommandId <= 0 || _lastCommandIds.GetValueOrDefault(command.SenderId) >= command.CommandId)
            return Fail("Ismételt vagy sorrenden kívüli parancs.", out reason);
        if (command is InventoryTransferCommand inventoryTransfer)
            return ValidateInventoryTransfer(inventoryTransfer, out reason);
        if (command is UseInventoryItemCommand useItem)
            return ValidateUseInventoryItem(useItem, out reason);
        if (command is DropInventoryItemCommand dropItem)
            return ValidateDropInventoryItem(dropItem, out reason);
        if (command is PickUpGroundItemCommand pickUpItem)
        {
            if (pickUpItem.GroundPileId.Value == Guid.Empty || pickUpItem.ExpectedGroundPileRevision <= 0 ||
                pickUpItem.GroundItemIndex < 0)
                return Fail("A földi tárgy hivatkozása érvénytelen.", out reason);
            return ValidateInventorySlotCommand(pickUpItem.SenderId, pickUpItem.CharacterId,
                pickUpItem.ExpectedInventoryRevision, InventorySlotKind.Backpack,
                pickUpItem.DestinationBackpackIndex, allowInn: false, requireItem: false, out reason);
        }
        if (!_controls.TryGetValue(command.CharacterId, out var control) ||
            control.AssignedPlayerId != command.SenderId || control.ConnectionState != PlayerConnectionState.Connected ||
            control.ControllerKind == CharacterControllerKind.Npc)
            return Fail("A játékos nem irányíthatja ezt a karaktert.", out reason);
        if (command is CharacterActionCommand characterAction && !Enum.IsDefined(characterAction.Action))
            return Fail("Ismeretlen karakterakció.", out reason);
        if (command is LeaderActionCommand leaderAction && !Enum.IsDefined(leaderAction.Action))
            return Fail("Ismeretlen leader-akció.", out reason);
        if (command is BattleActionCommand battleAction)
        {
            if (Phase != GameSessionPhase.Battle || _activeBattleId != battleAction.BattleId)
                return Fail("A harci parancs nem az aktív csatához tartozik.", out reason);
            if (_activeBattleTurnId != battleAction.TurnId || _actingBattleCharacterId != battleAction.CharacterId)
                return Fail("A harci parancs egy lejárt vagy más karakterhez tartozó körre érkezett.", out reason);
            if (!_allowedBattleActions.Contains(battleAction.Action))
                return Fail("Ez a harci akció nem engedélyezett az aktuális promptban.", out reason);
            if (!HasValidBattleActionShape(battleAction))
                return Fail("A harci parancs adatai hiányosak vagy érvénytelenek.", out reason);
            reason = string.Empty;
            return true;
        }
        if (Phase != GameSessionPhase.Exploration)
            return Fail("Ez a parancs csak felfedezés közben hajtható végre.", out reason);
        if (command is CastExplorationSpellCommand explorationSpell &&
            (string.IsNullOrWhiteSpace(explorationSpell.SpellId) ||
             explorationSpell.CastingItemSlotIndex is < 0 or >= LiveCharacter.MaximumMagicItemCount))
            return Fail("A felfedezési varázslat parancsa hiányos vagy érvénytelen.", out reason);
        if (command is LeaderActionCommand && control.ControllerKind != CharacterControllerKind.HostPlayer)
            return Fail("Ez a party-szintű művelet csak a leader számára engedélyezett.", out reason);
        reason = string.Empty;
        return true;
    }

    private bool ValidateInventoryTransfer(InventoryTransferCommand command, out string reason)
    {
        if (Phase is not (GameSessionPhase.Exploration or GameSessionPhase.Inn))
            return Fail("Inventory csak felfedezés vagy fogadó közben módosítható.", out reason);
        var source = _party.Members.FirstOrDefault(character => character.Id == command.CharacterId);
        var destination = _party.Members.FirstOrDefault(character => character.Id == command.DestinationCharacterId);
        if (source is null || destination is null)
            return Fail("Az inventory-command egyik karaktere nem tagja a partinak.", out reason);
        if (command.SenderId != HostPlayerId &&
            (!CanPlayerManageInventory(command.SenderId, source.Id) ||
             !CanPlayerManageInventory(command.SenderId, destination.Id)))
            return Fail("A vendég csak a saját karakterének inventoryját kezelheti.", out reason);
        return InventoryTransferService.Validate(_party, command, out reason);
    }

    private bool CanPlayerManageInventory(PlayerId playerId, CharacterId characterId) =>
        _controls.TryGetValue(characterId, out var control) && control.AssignedPlayerId == playerId &&
        control.ControllerKind != CharacterControllerKind.Npc &&
        control.ConnectionState == PlayerConnectionState.Connected;

    private bool ValidateInventorySlotCommand(PlayerId senderId, CharacterId characterId, long expectedRevision,
        InventorySlotKind kind, int index, bool allowInn, bool requireItem, out string reason)
    {
        if (Phase != GameSessionPhase.Exploration && !(allowInn && Phase == GameSessionPhase.Inn))
            return Fail("Ez az inventory-művelet az aktuális fázisban nem használható.", out reason);
        var character = _party.Members.FirstOrDefault(member => member.Id == characterId);
        if (character is null) return Fail("A karakter nem tagja a partinak.", out reason);
        if (senderId != HostPlayerId && !CanPlayerManageInventory(senderId, characterId))
            return Fail("A vendég csak a saját karakterének inventoryját kezelheti.", out reason);
        if (!InventoryTransferService.IsValidSlotAddress(kind, index))
            return Fail("Az inventory slotcíme érvénytelen.", out reason);
        if (character.InventoryRevision != expectedRevision)
            return Fail("Az inventory azóta megváltozott; friss snapshot szükséges.", out reason);
        if (requireItem && character.GetInventoryItem(kind, index) is null)
            return Fail("A kijelölt inventory-slot üres.", out reason);
        if (!requireItem && character.GetInventoryItem(kind, index) is not null)
            return Fail("A cél inventory-slot nem üres.", out reason);
        reason = string.Empty;
        return true;
    }

    private bool ValidateUseInventoryItem(UseInventoryItemCommand command, out string reason)
    {
        if (!ValidateInventorySlotCommand(command.SenderId, command.CharacterId,
                command.ExpectedInventoryRevision, InventorySlotKind.Backpack, command.BackpackIndex,
                allowInn: true, requireItem: true, out reason)) return false;
        var character = _party.Members.First(member => member.Id == command.CharacterId);
        if (character.GetInventoryItem(InventorySlotKind.Backpack, command.BackpackIndex) is not MiscItemDefinition
            { Effect: not ConsumableEffect.None })
            return Fail("A kijelölt tárgy közvetlenül nem használható.", out reason);
        reason = string.Empty;
        return true;
    }

    private bool ValidateDropInventoryItem(DropInventoryItemCommand command, out string reason)
    {
        if (!ValidateInventorySlotCommand(command.SenderId, command.CharacterId,
                command.ExpectedInventoryRevision, command.SlotKind, command.SlotIndex,
                allowInn: false, requireItem: true, out reason)) return false;
        var character = _party.Members.First(member => member.Id == command.CharacterId);
        if (SpellcastingRules.IsSpellcastingFocus(character.GetInventoryItem(command.SlotKind, command.SlotIndex)))
            return Fail("A karakterhez kötött varázsfókusz nem dobható el.", out reason);
        reason = string.Empty;
        return true;
    }

    private void SetControl(CharacterControlState control)
    {
        _controls[control.CharacterId] = control;
        Publish(sequence => new CharacterControlChangedEvent(sequence, control));
    }

    private void Publish(Func<long, GameSessionEvent> create)
    {
        var sessionEvent = create(++_eventSequence);
        EventPublished?.Invoke(sessionEvent);
    }

    private static bool Fail(string reason, out string error)
    {
        error = reason;
        return false;
    }

    private static bool HasValidBattleActionShape(BattleActionCommand command) => command.Action switch
    {
        BattleActionKind.PhysicalAttack or BattleActionKind.TurnUndead =>
            command.SpellId is null && command.CastingItemSlotIndex is null && command.Target is null,
        BattleActionKind.CastSpell => !string.IsNullOrWhiteSpace(command.SpellId) && command.Target is not null &&
                                      command.CastingItemSlotIndex is null or >= 0 and < LiveCharacter.MaximumMagicItemCount,
        _ => false
    };

    private void ValidateSnapshotBattle(BattleSnapshot? battle)
    {
        if (battle is null)
        {
            if (Phase == GameSessionPhase.Battle && _activeBattleId is not null)
                throw new ArgumentException("Az aktív csatához harci snapshot szükséges.", nameof(battle));
            return;
        }
        if (Phase != GameSessionPhase.Battle || _activeBattleId != battle.BattleId ||
            _activeBattleTurnId != battle.TurnId || _actingBattleCharacterId != battle.ActingCharacterId)
            throw new ArgumentException("A harci snapshot nem az aktív session-prompthoz tartozik.", nameof(battle));
        if (!_allowedBattleActions.SetEquals(battle.AllowedActions))
            throw new ArgumentException("A harci snapshot engedélyezett akciói eltérnek az aktív prompttól.", nameof(battle));
    }
}
