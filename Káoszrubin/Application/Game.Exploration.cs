using KaoszRubin.Application;
using KaoszRubin.Application.Quests;
using KaoszRubin.Combat;
using KaoszRubin.Data;
using KaoszRubin.Domain;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Magic;
using KaoszRubin.Domain.Quests;
using KaoszRubin.Infrastructure;
using KaoszRubin.Infrastructure.Quests;
using KaoszRubin.UI;
using System.Runtime;
using System.Security.Cryptography.Xml;
using static KaoszRubin.UI.GameInput;
using MainMenu = KaoszRubin.UI.MainMenu;

namespace KaoszRubin.Application;

public sealed partial class Game
{
    private void ShowInGameHelp()
    {
        RunHostPersonalWindow(PlayerWindowKind.Help, () => MainMenu.ShowHelp(CurrentHostCoopWindowStatus));
    }

    private void AssignSelectedSpellQuickSlot(int slotIndex)
    {
        var character = _renderer.CharacterSheet.SpellInfoCharacter;
        var spell = _renderer.CharacterSheet.GetSelectedSpellInfo();
        if (character is null || spell is null) return;
        if (!character.AssignQuickSpell(slotIndex, spell))
        {
            _renderer.DrawInventoryMessage("Csak memorizált varázslat tehető gyorshelyre.", ConsoleColor.Red);
            return;
        }
        _renderer.CharacterSheet.RefreshSpellInfoPage();
        _renderer.DrawInventoryMessage($"{spell.Name} hozzárendelve: F{slotIndex + 1}.", ConsoleColor.Cyan);
    }

    private void CastSelectedSpellInfo()
    {
        var character = _renderer.CharacterSheet.SpellInfoCharacter;
        var spell = _renderer.CharacterSheet.GetSelectedSpellInfo();
        if (character != PartyLeader || spell is null ||
            character.MemorizedSpells.All(candidate => !string.Equals(candidate.Id, spell.Id, StringComparison.OrdinalIgnoreCase)))
        {
            _renderer.DrawInventoryMessage("Csak a partivezér memorizált varázslata süthető el.", ConsoleColor.DarkYellow);
            return;
        }
        _renderer.CharacterSheet.CloseSpellInfoPage();
        BeginExplorationSpellCasting(spell);
    }

    private void BeginExplorationSpellCasting(SpellDefinition? quickSpell = null)
    {
        var spell = quickSpell;
        MagicItemDefinition? castingItem = null;
        int? castingItemSlotIndex = null;
        var caster = PartyLeader;
        if (spell is null)
        {
            var casters = GetSpellcastingPartyMembers();
            if (casters.Count == 0)
            {
                _renderer.DrawInventoryMessage("Senki nem tud varázsolni a partiban.", ConsoleColor.DarkYellow);
                return;
            }
            var startIndex = Math.Max(0, casters.IndexOf(PartyLeader));
            var selection = _renderer.DrawSpellCastingScreen(casters, startIndex, inCombat: false, _maze, _fogOfWar,
                GetCasterPosition, ShowInGameHelp);
            _renderer.RestoreSpellCastingOverlay();
            if (selection is null) return;
            spell = selection.Spell;
            caster = selection.Caster;
            castingItem = selection.CastingItem;
            castingItemSlotIndex = selection.CastingItemSlotIndex;
        }
        var result = TryCastSpell(caster, GetCasterPosition(caster), spell, inCombat: false,
            currentEnemy: null, castingItem: castingItem, castingItemSlotIndex: castingItemSlotIndex);
        if (result is not null)
        {
            _renderer.CharacterSheet.RefreshBattleStatusRows();
            _renderer.DrawInventoryMessage(result.Message, result.Kind == BattleLogKind.Information ? ConsoleColor.Red : ConsoleColor.Magenta);
        }
    }

    private List<LiveCharacter> GetSpellcastingPartyMembers() => CharacterRoster.Party.Members
        .Where(character => character.IsAlive &&
            (character.IsSpellcaster && character.CanCastSpells || EquippedCastingItems(character).Any()))
        .ToList();

    private IEnumerable<MagicItemDefinition> EquippedCastingItems(LiveCharacter character) =>
        character.MagicItems.Select((item, index) => (Item: item, Index: index))
            .Where(entry => entry.Item?.Kind is MagicItemKind.Scroll or MagicItemKind.Wand &&
                character.IsInventoryItemIdentified(InventorySlotKind.MagicItem, entry.Index) &&
                entry.Item.SpellId is not null && character.MagicItemCharges[entry.Index] > 0)
            .Where(entry => SpellcastingRules.CanUseCastingItem(character, entry.Item!, _gameData.GetSpell(entry.Item!.SpellId!)))
            .Select(entry => entry.Item!);

    private Position GetCasterPosition(LiveCharacter character) => character == PartyLeader
        ? _player.Position
        : _maze.PartyMembers.First(member => member.Character == character).Position;



            private void SaveGame()
    {
        CancelHeldInventoryItem();
        if (_isReturnExpedition)
        {
            _renderer.DrawInventoryMessage(
                "A visszatérő expedíció közben nem menthetsz. Érd el a régi kijáratot és térj vissza a fogadóba.",
                ConsoleColor.DarkYellow);
            return;
        }
        try
        {
            var path = _gameSaveService.Save(CreateGameSaveData(), CharacterRoster);
            _renderer.DrawDeveloperMessage($"Játék elmentve: {Path.GetFileName(path)}");
            if (_activeCoopHost is not null)
            {
                PublishRemoteCharacterStates(CharacterSyncReason.GameSaved);
                RequestCoopSnapshotPublish();
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _renderer.DrawDeveloperMessage($"A mentés sikertelen: {exception.Message}");
        }
    }

    private GameSaveData CreateGameSaveData()
    {
        SynchronizeInventoryQuests();
        var state = _gameStateMapper.Create(_mazeLevel, _maze, _player, _fogOfWar, _leaderFacing,
            _leaderTrail, _partyHoldingPosition, _partyRegrouping, _partyAttackMode, _hasRestedThisLevel, _partyScatterUntil,
            _nextNeedsDrain, _nextEnemyMoves, _collectedBossKeyIds, _seenBossIds);
        state.QuestJournal = _questJournal.Values.Select(entry => new QuestJournalSaveData(LegacyQuestIdMap.ToExternalId(entry.Key.QuestId),
            entry.Status, entry.Progress, entry.ExperienceReward,
            entry.CompletionExperienceSummary, entry.CompletionItemRewardSummary)).ToList();
        state.Quests = _questSaveAdapter.Export(_questManager, _questJournal.Values);
        state.LocationKind = _locationKind;
        state.LocationId = _locationId;
        state.DifficultyLevel = _difficultyLevel;
        state.SuspendedCampaign = _suspendedCampaignState;
        state.IsCoopGame = _activeCoopHost is not null;
        state.UsedAdHocConversationIds = _usedAdHocConversationIds.ToList();
        state.LastAdHocConversationUtc = _lastAdHocConversationUtc == DateTime.MinValue
            ? null : new DateTimeOffset(_lastAdHocConversationUtc, TimeSpan.Zero);
        state.AdHocConversationMazeLevel = _adHocConversationMazeLevel;
        state.Formation = _formation;
        state.NpcSpellcasterTactics = _npcSpellcasterTactics
            .Select(pair => new NpcSpellcasterTacticsEntry(pair.Key, pair.Value.Normalize())).ToList();
        state.RemoteCharacterIds = _session.CharacterControls
            .Where(control => control.AssignedPlayerId is not null &&
                              control.AssignedPlayerId != _session.HostPlayerId)
            .Select(control => control.CharacterId.Value).ToList();
        var waitingIndex = _eliraWaitingAtInn is null
            ? -1 : CharacterRoster.Characters.ToList().IndexOf(_eliraWaitingAtInn);
        state.EliraInnCharacterIndex = waitingIndex >= 0 ? waitingIndex : null;
        state.EliraInnVisitsRemaining = state.EliraInnCharacterIndex is null ? 0 : _eliraInnVisitsRemaining;
        return state;
    }

    private void PublishRemoteCharacterStates(CharacterSyncReason reason)
    {
        if (_activeCoopHost is null) return;
        foreach (var control in _session.CharacterControls.Where(control =>
                     control.AssignedPlayerId is not null && control.AssignedPlayerId != _session.HostPlayerId))
        {
            var character = CharacterRoster.Party.Members.FirstOrDefault(member => member.Id == control.CharacterId);
            if (character is not null)
                _activeCoopHost.TryPublishCharacterState(character.Id,
                    _gameSaveService.SerializeCharacter(character), reason);
        }
    }

    private void RestoreGame(GameSaveData state)
    {
        var questStates = _questSaveAdapter.PrepareRestore(state, CharacterRoster);
        var restored = _gameStateMapper.Restore(state);
        _mazeLevel = restored.MazeLevel;
        _locationKind = state.LocationKind;
        _locationId = string.IsNullOrWhiteSpace(state.LocationId)
            ? $"CAMPAIGN_{_mazeLevel:00}" : state.LocationId;
        _difficultyLevel = state.DifficultyLevel > 0 ? state.DifficultyLevel : _mazeLevel;
        _suspendedCampaignState = state.SuspendedCampaign;
        _eliraWaitingAtInn = state.EliraInnCharacterIndex is { } waitingIndex &&
                             waitingIndex >= 0 && waitingIndex < CharacterRoster.Characters.Count
            ? CharacterRoster.Characters[waitingIndex]
            : null;
        _eliraInnVisitsRemaining = _eliraWaitingAtInn is null
            ? 0 : Math.Clamp(state.EliraInnVisitsRemaining, 0, 3);
        _collectedBossKeyIds.Clear();
        _collectedBossKeyIds.UnionWith(state.CollectedBossKeyIds ?? []);
        _seenBossIds.Clear();
        _seenBossIds.UnionWith(state.SeenBossIds ?? []);
        _usedAdHocConversationIds.Clear();
        _usedAdHocConversationIds.UnionWith(state.UsedAdHocConversationIds ?? []);
        _lastAdHocConversationUtc = state.LastAdHocConversationUtc?.UtcDateTime ?? DateTime.MinValue;
        _adHocConversationMazeLevel = state.AdHocConversationMazeLevel;
        _questJournal.Clear();
        _renderer.SetGoldenKeyCount(_collectedBossKeyIds.Count);
        _maze = restored.Maze;
        CaptureExpeditionEnemyTemplates();
        _player = restored.Player;
        _fogOfWar = restored.FogOfWar;
        _leaderFacing = restored.LeaderFacing;
        _formation = PartyFormationRules.Normalize(state.Formation,
            CharacterRoster.Party.Members.Select(member => member.Id), PartyLeader.Id);
        _npcSpellcasterTactics.Clear();
        foreach (var entry in state.NpcSpellcasterTactics ?? [])
            if (CharacterRoster.Party.Members.Any(member => member.Id == entry.CharacterId && member.IsSpellcaster))
                _npcSpellcasterTactics[entry.CharacterId] = entry.Tactics.Normalize();
        _renderer.CharacterSheet.SetFormationStatus(_formation);
        _session.SetFormationMovementLocked(_formation.State == PartyFormationState.Locked);
        _leaderTrail.Clear();
        _leaderTrail.AddRange(restored.LeaderTrail);
        _partyHoldingPosition = restored.PartyHoldingPosition;
        _partyRegrouping = restored.PartyRegrouping;
        _partyAttackMode = restored.PartyAttackMode;
        _hasRestedThisLevel = restored.HasRestedThisLevel;
        _partyScatterUntil = restored.PartyScatterUntil;
        _nextNeedsDrain = restored.NextNeedsDrain;
        _nextEnemyMoves.Clear();
        foreach (var enemyMove in restored.NextEnemyMoves) _nextEnemyMoves[enemyMove.Key] = enemyMove.Value;
        RefreshNextEnemyActionUtc();
        _nextPartyMoves.Clear();
        foreach (var member in _maze.PartyMembers) ScheduleNextPartyMove(member, DateTime.UtcNow);
        _battleStarted = false;
        _gameOver = false;
        // A roster, a világ és a fog már a betöltött állapotot mutatja. Nincs aktiválás vagy jutalmazás.
        _questManager.RestoreState(questStates);
        foreach (var entry in _questSaveAdapter.CreateJournal(_questManager))
            _questJournal[entry.Key] = entry;
        RevealFor(PartyLeader, _player.Position);
        _renderer.DrawInitialState(_maze, _player, _fogOfWar, _difficultyLevel);
        _renderer.DrawDeveloperMessage(_locationKind == AdventureLocationKind.Quest
            ? $"Mentés betöltve: {state.MainCharacterName}, {_maze.LevelName} ({_difficultyLevel}. nehézség)."
            : $"Mentés betöltve: {state.MainCharacterName}, {_mazeLevel}. pálya.");
        _backgroundMusic.SynchronizeMazeLevel(_difficultyLevel, _fogOfWar.IsRevealed(_maze.Exit));
        if (_locationKind == AdventureLocationKind.Quest &&
            string.Equals(_locationId, QuestLocationConfigurations.RodericMalrec, StringComparison.OrdinalIgnoreCase) &&
            FindRodericFollower() is { StoryStateId: "TRUSTED" } legacyRoderic)
            legacyRoderic.SetStoryState("MALREC_APPROACH");
        if (_locationKind == AdventureLocationKind.Quest &&
            string.Equals(_locationId, QuestLocationConfigurations.RodericMalrec, StringComparison.OrdinalIgnoreCase) &&
            FindRodericFollower() is { StoryStateId: "MALREC_DEFEATED" })
            _pendingRodericReturn = true;
        else if (_locationKind == AdventureLocationKind.Campaign && _suspendedCampaignState is null &&
                 FindRodericFollower() is { StoryStateId: "MALREC_READY" } &&
                 _questManager.Roderic.Quests.OathbreakerKnight.State is (QuestState.Locked or QuestState.Available))
            _pendingRodericExpedition = true;
    }

    private void TryRestParty()
    {
        if (_hasRestedThisLevel)
        {
            _renderer.DrawDeveloperMessage("Ezen a pályán már pihentetek egyszer.");
            return;
        }
        var room = _maze.Rooms.FirstOrDefault(candidate => candidate.Contains(_player.Position));
        if (room is null)
        {
            _renderer.DrawDeveloperMessage("Pihenni csak egy szoba belsejében lehet.");
            return;
        }
        var livingParty = CharacterRoster.Party.Members.Where(character => character.IsAlive).ToList();
        var everyoneInside = livingParty.All(character => character == PartyLeader
            ? room.Contains(_player.Position)
            : _maze.PartyMembers.Any(avatar => avatar.Character == character && room.Contains(avatar.Position)));
        if (!everyoneInside)
        {
            _renderer.DrawDeveloperMessage("Pihenéshez minden élő partitag ugyanabban a szobában legyen.");
            return;
        }
        if (_maze.Enemies.Any(enemy => room.Contains(enemy.Position)))
        {
            _renderer.DrawDeveloperMessage("Ellenség van a szobában; itt nem lehet pihenni.");
            return;
        }
        var roomDoors = _maze.Doors.Where(door => room.InteriorPositions()
            .Any(position => Manhattan(position, door.Position) == 1)).ToList();
        if (roomDoors.Count == 0 || roomDoors.Any(door => door.State != DoorState.Locked))
        {
            _renderer.DrawDeveloperMessage("Pihenéshez a szoba minden ajtaját kulcsra kell zárni.");
            return;
        }

        var restResults = new List<CharacterRestSnapshot>();
        foreach (var character in livingParty)
        {
            var beforeVitality = character.CurrentVitality;
            var beforeMana = character.CurrentMana;
            character.RestoreVitality(_random.Next(1, 11));
            character.SetCurrentResources(character.CurrentVitality, character.MaximumMana);
            var cured = new List<string>();
            var cureChance = Math.Clamp(30 + character.EffectiveAbilities.Health * 2, 0, 100);
            foreach (var (statusId, name) in new[]
                     {
                         (CharacterStatusIds.Diseased, "betegség"),
                         (CharacterStatusIds.Poisoned, "mérgezés"),
                         (CharacterStatusIds.Bleeding, "vérzés")
                     })
                if (character.HasStatus(statusId) && _random.Next(100) < cureChance && character.RemoveStatus(statusId))
                    cured.Add($"{_gameData.GetStatus(statusId).Icon} {name}");
            character.ConsumeFood(10);
            character.ConsumeWater(10);
            character.SynchronizeNeedStatuses(_gameData.GetStatus(CharacterStatusIds.Hungry),
                _gameData.GetStatus(CharacterStatusIds.Thirsty));
            restResults.Add(new CharacterRestSnapshot(character.Id, character.Name, character.Color,
                character.CurrentVitality - beforeVitality, character.CurrentMana - beforeMana,
                character.CurrentVitality, character.MaximumVitality, character.CurrentMana, character.MaximumMana,
                character.UsesMana, cured));
        }
        _hasRestedThisLevel = true;
        PlaySessionSound(SoundEffect.Rest);
        ShowSynchronizedRest(new PartyRestSnapshot(Guid.NewGuid(), false, restResults, []));
        TryLogPartyComments(PartySituationIds.Resting);
        PreparePartySpells();
        foreach (var door in roomDoors) _maze.SetDoorState(door, DoorState.Closed);
        _nextNeedsDrain = DateTime.UtcNow + TimeSpan.FromMinutes(1);
        InitializeEnemyMoveSchedule(DateTime.UtcNow);
        foreach (var member in _maze.PartyMembers) ScheduleNextPartyMove(member, DateTime.UtcNow);
        _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
    }

    private void PreparePartySpells()
    {
        foreach (var character in CharacterRoster.Party.Members.Where(character => character.IsAlive && character.IsSpellcaster))
        {
            var control = _session.CharacterControls.FirstOrDefault(candidate => candidate.CharacterId == character.Id);
            if (control is { ControllerKind: CharacterControllerKind.RemotePlayer,
                    ConnectionState: PlayerConnectionState.Connected, AssignedPlayerId: not null })
                WaitForRemoteSpellPreparation(character);
            else
            {
                var spellInfo = SpellInfoSnapshotProjector.Create(character);
                _activeSpellPreparation = new SpellPreparationSnapshot(Guid.NewGuid(), character.Id,
                    character.Name, character.MemorizationCapacity, spellInfo.KnownSpells,
                    character.MemorizedSpells.Select(spell => spell.Id).ToArray());
                try
                {
                    RunHostWindow($"Varázsmemorizálás — {character.Name}",
                        $"A vezető {character.Name} varázslatait készíti elő…",
                        () => character.SetMemorizedSpells(_renderer.DrawSpellPreparationScreen(character)));
                }
                finally
                {
                    _activeSpellPreparation = null;
                    ForceCoopSnapshotPublish();
                }
            }
        }
    }

    private void WaitForRemoteSpellPreparation(LiveCharacter character)
    {
        var previousPhase = _session.Phase;
        var spellInfo = SpellInfoSnapshotProjector.Create(character);
        _activeSpellPreparation = new SpellPreparationSnapshot(Guid.NewGuid(), character.Id, character.Name,
            character.MemorizationCapacity, spellInfo.KnownSpells,
            character.MemorizedSpells.Select(spell => spell.Id).ToArray());
        _spellPreparationCompleted = false;
        _session.SetPhase(GameSessionPhase.Paused);
        CoopWindowStatusBanner.Refresh(() => $"Várakozás {character.Name} varázsmemorizálására...");
        _renderer.DrawReplicatedWindow(MagicProgressionWindow.PreparationWidth,
            MagicProgressionWindow.BuildPreparation(character.Name, _activeSpellPreparation.SelectedSpellIds.Count,
                _activeSpellPreparation.Capacity, _activeSpellPreparation.Spells,
                _activeSpellPreparation.SelectedSpellIds.ToHashSet(StringComparer.OrdinalIgnoreCase), 0),
            FramedWindow.SpellPreparation);
        PlaySessionSound(SoundEffect.Waiting, [PartyLeader.Id]);
        RequestCoopSnapshotPublish();
        while (!_spellPreparationCompleted)
        {
            ProcessSessionCommands();
            var stillConnected = _session.CharacterControls.Any(control => control.CharacterId == character.Id &&
                control.ControllerKind == CharacterControllerKind.RemotePlayer &&
                control.ConnectionState == PlayerConnectionState.Connected);
            if (!stillConnected) break;
            TryPublishScheduledCoopSnapshot(DateTime.UtcNow);
            Thread.Sleep(20);
        }
        _activeSpellPreparation = null;
        _spellPreparationCompleted = false;
        _renderer.ClearReplicatedWindow();
        CoopWindowStatusBanner.Clear();
        _session.SetPhase(previousPhase);
        RequestCoopSnapshotPublish();
    }


    private void MovePlayer(Direction direction, bool preserveFormationFacing = false)
    {
        if (!CanControlledCharacterMove(PartyLeader)) return;
        if (_formation.State == PartyFormationState.Locked)
        {
            MoveLockedFormation(direction, preserveFormationFacing);
            return;
        }
        var previousPosition = _player.Position;
        var targetPosition = previousPosition + direction;

        // A társak és a követők továbbra sem átjárhatók, de az ütközés nem indít párbeszédet.
        if (_maze.GetObjectAt(targetPosition) is PartyMemberAvatar) return;
        if (_maze.GetEnemyAt(targetPosition) is { } encounteredEnemy)
        {
            StartBattle(encounteredEnemy);
            return;
        }
        if (_maze.GetWorldNpcAt(targetPosition) is { } npc)
        {
            if (!EncounterWorldNpc(npc)) return;
        }
        if (!CanEnterTrap(PartyLeader, targetPosition)) return;

        var moved = _player.TryMove(direction, _maze);
        if (!moved)
        {
            if (_developerPhasing && _maze.IsInside(targetPosition))
            {
                // Destroy wall/door and move through
                _maze.RemoveDoor(targetPosition);
                _maze.Carve(targetPosition);
                _player.TeleportTo(targetPosition);
            }
            else
            {
                return;
            }
        }
        PartyLeader.RegisterExplorationStep();
        ScheduleNextControlledMove(PartyLeader);
        _leaderFacing = direction;
        if (_leaderTrail[^1] != _player.Position) _leaderTrail.Add(_player.Position);
        if (_leaderTrail.Count > 256) _leaderTrail.RemoveRange(0, _leaderTrail.Count - 256);

        var newlyRevealed = RevealFor(PartyLeader, _player.Position, advanceEnemyMemory: true);
        var justReachedExit = _player.Position == _maze.Exit && previousPosition != _maze.Exit;
        _renderer.DrawMovement(_maze, _fogOfWar, previousPosition, _player.Position, newlyRevealed, justReachedExit);
        CheckBossDiscoveryAt(newlyRevealed, PartyLeader);
        PlayCharacterStepSound(PartyLeader);
        CollectTreasureChest(PartyLeader, _player.Position, shareLootWithParty: true);
        TriggerTrapAt(PartyLeader, _player.Position);
        var enemy = _maze.GetEnemyAt(_player.Position);
        if (enemy is not null) StartBattle(enemy);
    }

    private void MoveRemotePartyMember(MoveCharacterCommand command)
    {
        var member = _maze.PartyMembers.FirstOrDefault(candidate => candidate.Character.Id == command.CharacterId);
        if (member is null || !member.Character.IsAlive) return;
        if (!CanControlledCharacterMove(member.Character)) return;
        var previous = member.Position;
        var destination = previous + command.Direction;
        if (_maze.GetEnemyAt(destination) is { } enemy)
        {
            StartBattle(member, enemy);
            return;
        }
        if (!CanEnterTrap(member.Character, destination)) return;
        if (!_maze.TryMovePartyMember(member, destination, _player.Position, allowTreasureChest: true)) return;
        member.Character.RegisterExplorationStep();
        ScheduleNextControlledMove(member.Character);
        var newlyRevealed = RevealFor(member.Character, member.Position, advanceEnemyMemory: true);
        _renderer.DrawPartyMemberMovement(_maze, _fogOfWar, previous, member.Position, newlyRevealed, _player.Position);
        PlayCharacterStepSound(member.Character);
        CheckBossDiscoveryAt(newlyRevealed, member.Character);
        CollectTreasureChest(member.Character, member.Position, shareLootWithParty: false);
        TriggerTrapAt(member.Character, member.Position);
    }


    private void CollectTreasureChest(LiveCharacter character, Position position, bool shareLootWithParty)
    {
        var chest = _maze.GetTreasureChestAt(position);
        if (chest is null) return;
        if (chest.Definition is not null)
        {
            var result = new QuestChestService(_questManager).Collect(chest,
                item => TryStoreSearchedLoot(character, item, shareLootWithParty, out _),
                PartyLeader.AddGold);
            ProcessQuestProgressChanges(result.Changes);
            SynchronizeInventoryQuests();
            var text = $"🎁 {chest.Definition.Name}: {result.Gold} arany, {result.ItemCount} tárgy felvéve. " +
                (result.RemainingCount > 0
                    ? $"{result.RemainingCount} tárgy a ládában maradt; később újra átkutathatod."
                    : "A láda üres.");
            _renderer.RefreshCharacterSheet(PartyLeader);
            _renderer.DrawMapCellsChanged(_maze, _fogOfWar, _player.Position, [position]);
            _renderer.DrawInventoryMessage(text, ConsoleColor.Yellow);
            RecordSessionActivity(SessionActivityKind.System, text, ConsoleColor.Yellow, [character.Id]);
            if (result.FirstOpening) PlaySessionSound(SoundEffect.Chest, [character.Id]);
            RequestCoopSnapshotPublish();
            return;
        }
        var rules = _gameData.LootRules;
        var jackpotChance = AdjustedSearchChance(character, rules.ChestJackpotChancePercent);
        var jackpot = _random.Next(100) < jackpotChance;
        var rewardMultiplier = jackpot ? rules.ChestJackpotMultiplier : 1;
        if (character.HasPerk(PerkIds.ThiefMasterThief)) rewardMultiplier *= 2;
        var goldAmount = chest.GoldAmount * rewardMultiplier;
        PartyLeader.AddGold(goldAmount);
        var masterThiefLoot = RollMasterThiefChestLoot(character);
        _maze.RemoveTreasureChest(chest);
        ProcessQuestProgressChanges(_questManager.RegisterChestOpened());
        _renderer.RefreshCharacterSheet(PartyLeader);
        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
        if (character == PartyLeader)
            _renderer.DrawTreasureCollected(goldAmount, jackpot, jackpotChance, rewardMultiplier);

        var message = $"🎁 {character.Name} kinyitotta a kincsesládát: {goldAmount} arany" +
                      (jackpot ? $" (jackpot, {jackpotChance}% esély)" : string.Empty) + ".";
        _renderer.DrawInventoryMessage(message, jackpot ? ConsoleColor.Magenta : ConsoleColor.Yellow);
        RecordSessionActivity(SessionActivityKind.System, message,
            jackpot ? ConsoleColor.Magenta : ConsoleColor.Yellow, [character.Id]);
        PlaySessionSound(jackpot ? SoundEffect.Chest2 : SoundEffect.Chest, [character.Id]);

        if (masterThiefLoot is null) return;
        var masterIdentification = RollLootItemState(masterThiefLoot);
        var masterLootName = ItemIdentificationRules.DisplayName(masterThiefLoot, masterIdentification.State.IsIdentified);
        if (TryStoreSearchedLoot(character, masterThiefLoot, shareLootWithParty, out var owner, masterIdentification.State))
            message = $"🎁 Mestertolvaj: {masterLootName} → {owner} hátizsákja.{FormatMageIdentification(masterIdentification)}";
        else
        {
            _maze.DropItem(position, masterThiefLoot, state: masterIdentification.State);
            message = $"🎁 Mestertolvaj: {masterLootName} a földön maradt, mert a hátizsák tele van.{FormatMageIdentification(masterIdentification)}";
        }
        _renderer.DrawInventoryMessage(message, ConsoleColor.Magenta);
        RecordSessionActivity(SessionActivityKind.System, message, ConsoleColor.Magenta, [character.Id]);
    }

    private void SubmitLocalExplorationCommand(ConsoleKeyInfo keyInfo)
    {
        GameCommand? command = null;
        var commandId = _localCommandId + 1;
        var key = keyInfo.Key;
        if ((keyInfo.Modifiers & ConsoleModifiers.Control) != 0 &&
            key is ConsoleKey.LeftArrow or ConsoleKey.RightArrow)
            command = new LeaderActionCommand(_session.HostPlayerId, commandId, PartyLeader.Id,
                key == ConsoleKey.LeftArrow ? LeaderAction.RotateFormationLeft : LeaderAction.RotateFormationRight);
        else if (TryGetDirection(key, out var direction))
            command = new MoveCharacterCommand(_session.HostPlayerId, commandId, PartyLeader.Id, direction,
                GameInputBindings.PreserveFormationFacing(keyInfo.Modifiers));
        else if (GameInputBindings.CharacterAction(key) is { } characterAction)
        {
            Position? targetDoor = null;
            if (characterAction is CharacterAction.OpenDoor or CharacterAction.CloseOrLockDoor)
            {
                var doors = AdjacentDoorPositions(PartyLeader, _player.Position, includeFormation: true);
                if (doors.Count == 1) targetDoor = doors[0];
                else if (doors.Count > 1)
                {
                    targetDoor = SelectDoorTarget(doors, characterAction);
                    if (targetDoor is null) return;
                }
            }
            var keyChoice = GetLocalThiefKeyChoice(characterAction, targetDoor);
            command = new CharacterActionCommand(_session.HostPlayerId, commandId, PartyLeader.Id,
                characterAction, targetDoor, keyChoice.UseKey, keyChoice.KeyOwnerCharacterId);
        }
        else
        {
            var action = GameInputBindings.LeaderAction(key, _player.Position == _maze.Exit);
            if (action is not null)
                command = new LeaderActionCommand(_session.HostPlayerId, commandId, PartyLeader.Id, action.Value);
        }
        if (command is null || !_session.Submit(command)) return;
        _localCommandId = commandId;
    }
}
