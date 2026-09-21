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
    private MazeQuestWorldContext CreateQuestWorldContext()
    {
        var ret = new MazeQuestWorldContext(
        getMaze:
            () => _maze,

        countPartyItem:
            item =>
                CountPartyBackpackItems(
                    item.Id),

        tryConsumePartyItem:
            (item, amount) =>
            {
                if (CountPartyBackpackItems(item.Id) < amount)
                    return false;

                RemovePartyBackpackItems(
                    item.Id,
                    amount);

                return true;
            },

        instanceRegistry:
            _questNpcInstanceRegistry,
        hasDiscoveredLocation: location => location == QuestLocation.Exit && IsLevelExitDiscovered(),
        getMazes: () => _dungeonLevel is null ? [_maze] :
            _dungeonLevel.Areas.Select(area => area.Maze));

        return ret;
    }

    private bool IsLevelExitDiscovered()
    {
        if (_dungeonLevel is not null)
        {
            var finalArea = _dungeonLevel.Areas[^1];
            return finalArea.FogOfWar.IsRevealed(finalArea.Maze.Exit);
        }
        return _fogOfWar is not null && _maze is not null && _fogOfWar.IsRevealed(_maze.Exit);
    }

    private QuestManager CreateQuestManager(GameDataCatalog gameData)
    {
        // ------------------------------------------------------------
        // Definíciók
        // ------------------------------------------------------------

        var questCatalog = gameData.Quests;

        // ------------------------------------------------------------
        // Runtime state
        // ------------------------------------------------------------

        var questStateStore =
            new QuestStateStore(
                questCatalog);

        // ------------------------------------------------------------
        // Quest availability / progress
        // ------------------------------------------------------------

        var availabilityService =
            new QuestAvailabilityService(
                questCatalog,
                questStateStore,
                _questWorldContext);

        var progressEngine =
            new QuestProgressEngine(
                questCatalog,
                questStateStore,
                _questWorldContext);

        // ------------------------------------------------------------
        // Jutalmazás
        // ------------------------------------------------------------

        var rewardContext =
            new QuestRewardContext(
                getSelectedCharacter:
                    () => PartyLeader,

                getPartyMembers:
                    () => CharacterRoster.Party.Members,

                rollRandomReward:
                    experienceReward =>
                        RollQuestReward(
                            experienceReward),

                tryStoreItem:
                    (IItemDefinition item, out string ownerName) =>
                        LootAndInventoryService.TryStoreLootInParty(
                            item,
                            PartyLeader,
                            CharacterRoster.Party.Members,
                            out ownerName),

                dropItem:
                    item =>
                        _maze.DropItem(
                            _player.Position,
                            item));

        var rewardService =
            new QuestRewardService(
                _progressionService,
                rewardContext);

        // ------------------------------------------------------------
        // Quest completion
        // ------------------------------------------------------------

        var completionProcessor =
            new QuestCompletionProcessor(
                questCatalog,
                questStateStore,
                progressEngine,
                _questWorldContext,
                rewardService);

        // ------------------------------------------------------------
        // NPC beszélgetés
        // ------------------------------------------------------------

        var conversationService =
            new QuestNpcConversationService(
                _questWorldContext,
                RunStoryConversation);

        // ------------------------------------------------------------
        // Publikus façade
        // ------------------------------------------------------------

        return new QuestManager(
            questCatalog,
            questStateStore,
            availabilityService,
            progressEngine,
            completionProcessor,
            conversationService);
    }

    // NPC spellcasting for combat
    private BattlePlayerAction? ChooseNpcBattlePlayerAction(PartyMemberAvatar member, Enemy enemy,
        LiveCharacter? supportedFighter = null, Action? onSpellCast = null)
    {
        var caster = member.Character;
        if (!caster.IsAlive) return null;
        if (BattleActionCoordinator.CanTurnUndead(caster, enemy, member.Position) &&
            IsTurnUndeadReady(caster))
            return ResolveTurnUndead(caster, enemy);
        if (!caster.IsSpellcaster || !caster.CanCastSpells) return null;
        // Emergency heal: any ally under 35% HP, within the spell's range of the caster
        var allies = CharacterRoster.Party.Members.Append(caster).Distinct().Where(c => c.IsAlive).ToList();
        var lowest = allies.OrderBy(c => (double)c.CurrentVitality / c.MaximumVitality).FirstOrDefault();
        if (lowest is not null && NpcSpellcastingPolicy.NeedsHealing(lowest))
        {
            foreach (var spell in caster.MemorizedSpells.Where(s => s.CanUseInCombat))
            {
                var effects = _gameData.GetSpellEffects(spell.Id);
                if (!effects.Any(e => e.Type == SpellEffectType.Heal)) continue;
                var manaCost = SpellcastingRules.EffectiveManaCost(caster, spell);
                if (caster.CurrentMana < manaCost) continue;
                var range = Math.Max(1, spell.Range);
                var reachable = allies.Where(c => Chebyshev(member.Position, GetCasterPosition(c)) <= range).ToList();
                if (reachable.Count == 0) continue;
                var target = reachable.OrderBy(c => (double)c.CurrentVitality / c.MaximumVitality).First();
                var emergency = NpcSpellcastingPolicy.IsEmergency(target);
                if (!NpcSpellcastingPolicy.CanSpendMana(caster, manaCost, emergency)) continue;
                // Cast heal
                var divine = caster.RecordDivineSpellCast(spell);
                caster.SpendMana(manaCost);
                PlaySessionSound(SoundEffect.DefensiveSpell, [caster.Id, target.Id]);
                var notes = new List<string>();
                foreach (var effect in effects.Where(e => e.Type == SpellEffectType.Heal))
                    ApplyHealingForCaster(effect, spell, new[] { target }, divine, notes, caster);
                var summary = notes.Count == 0 ? "" : $" {string.Join("; ", notes)}";
                var message = $"{caster.Name} elsüti: {spell.Name} → {target.Name}. -{manaCost} manna.{summary}";
                _renderer.DrawInventoryMessage(message, ConsoleColor.Green);
                RecordSessionActivity(SessionActivityKind.Support, message, ConsoleColor.Green);
                _renderer.CharacterSheet.RefreshBattleStatusRows();
                onSpellCast?.Invoke();
                return new BattlePlayerAction(message, BattleLogKind.PlayerAttack, 0, 0);
            }
        }

        // Káros állapot vagy mágikus debuff tisztítása, ha hasznos és hatótávon belül van.
        foreach (var spell in caster.MemorizedSpells.Where(s => s.CanUseInCombat))
        {
            var effects = _gameData.GetSpellEffects(spell.Id);
            var curesStatus = effects.Any(e => e.Type == SpellEffectType.CureStatus);
            var breaksCurse = effects.Any(e => e.Type == SpellEffectType.Dispel &&
                string.Equals(e.Parameter, "HarmfulOnly", StringComparison.OrdinalIgnoreCase));
            var breaksItemCurse = effects.Any(e => e.Type == SpellEffectType.BreakItemCurse);
            if (!curesStatus && !breaksCurse && !breaksItemCurse) continue;
            var manaCost = SpellcastingRules.EffectiveManaCost(caster, spell);
            if (caster.CurrentMana < manaCost) continue;
            var range = Math.Max(1, spell.Range);
            var candidates = CharacterRoster.Party.Members.Where(c => c.IsAlive &&
                (effects.Where(e => e.Type == SpellEffectType.CureStatus)
                     .SelectMany(e => SpellExecutionService.ParseEffectParameters(e.Parameter)).Any(c.HasStatus) ||
                 breaksCurse && c.ActiveSpellEffects.Any(active => !active.Beneficial) ||
                 breaksItemCurse && c.HasActiveCurse) &&
                Chebyshev(member.Position, GetCasterPosition(c)) <= range).ToList();
            if (!candidates.Any()) continue;
            var targetChar = candidates.First();
            var divine = caster.RecordDivineSpellCast(spell);
            caster.SpendMana(manaCost);
            PlaySessionSound(SoundEffect.DefensiveSpell, [caster.Id, targetChar.Id]);
            var notes = new List<string>();
            foreach (var effect in effects.Where(e => e.Type == SpellEffectType.CureStatus))
                ApplyStatusCureForCaster(effect, [targetChar], notes);
            if (breaksCurse)
                notes.Add($"{targetChar.RemoveSpellEffects(active => !active.Beneficial)} káros varázshatás megtörve");
            if (breaksItemCurse)
            {
                var purifiedItem = targetChar.PurifyStrongestActiveCurse();
                notes.Add(purifiedItem is null
                    ? "nincs aktív tárgyátok"
                    : $"✨ a(z) {purifiedItem.Name} átka megtört");
            }
            var message = $"{caster.Name} elsüti: {spell.Name} → {targetChar.Name}. -{manaCost} manna. {string.Join("; ", notes)}";
            _renderer.DrawInventoryMessage(message, ConsoleColor.Green);
            RecordSessionActivity(SessionActivityKind.Support, message, ConsoleColor.Green);
            _renderer.CharacterSheet.RefreshBattleStatusRows();
            onSpellCast?.Invoke();
            return new BattlePlayerAction(message, BattleLogKind.PlayerAttack, 0, 0);
        }

        // Más karakter harcát támadó varázslattal csak valódi vészhelyzetben támogatják.
        // Saját harcukban ez a korlátozás nem érvényes.
        if (supportedFighter is not null && !ShouldUseOffensiveSupportSpell(supportedFighter, enemy)) return null;

        // Offensive spell against the enemy the leader is fighting (single-target only, don't waste area/direction spells on one foe)
        foreach (var spell in caster.MemorizedSpells.Where(s => s.CanUseInCombat && s.TargetType == SpellTargetType.Enemy))
        {
            var effects = _gameData.GetSpellEffects(spell.Id);
            if (!NpcSpellcastingPolicy.IsSingleTargetOffensive(spell, effects)) continue;
            var manaCost = SpellcastingRules.EffectiveManaCost(caster, spell);
            if (!NpcSpellcastingPolicy.CanSpendMana(caster, manaCost)) continue;
            if (!IsValidSpellTarget(member.Position, spell, enemy.Position, enemy)) continue;
            var divine = caster.RecordDivineSpellCast(spell);
            caster.SpendMana(manaCost);
            var listeners = new List<CharacterId> { caster.Id };
            if (supportedFighter is not null) listeners.Add(supportedFighter.Id);
            PlaySessionSound(SoundEffect.OffensiveSpell, listeners);
            var execution = ExecuteSpell(caster, member.Position, spell, enemy.Position, inCombat: true, enemy, divine);
            var message = $"{caster.Name} elsüti: {spell.Name} → {enemy.Name}. -{manaCost} manna. {execution.Summary}";
            _renderer.DrawInventoryMessage(message, ConsoleColor.Green);
            RecordSessionActivity(SessionActivityKind.Support, message, ConsoleColor.Green);
            _renderer.CharacterSheet.RefreshBattleStatusRows();
            onSpellCast?.Invoke();
            return new BattlePlayerAction(message, BattleLogKind.PlayerAttack, execution.DamageToCurrentEnemy, execution.ExtraPlayerActions);
        }

        return null;
    }

    private bool ShouldUseOffensiveSupportSpell(LiveCharacter fighter, Enemy enemy)
    {
        var enemyCombatAbilities = (enemy.Definition.Strength ?? 0) + (enemy.Definition.Speed ?? 0);
        var fighterCombatAbilities = fighter.EffectiveAbilities.Strength + fighter.EffectiveAbilities.Dexterity;
        return fighter.CurrentVitality * 2 <= fighter.MaximumVitality ||
               enemy.Definition.IsBoss || enemy.Definition.StrengthTier >= 5 ||
               enemyCombatAbilities > fighterCombatAbilities;
    }

    // A globálisan megállított csatában csak az NPC-k adnak automatikus támogatást; más emberi karakter nem.
    private int TryPartyMembersActInBattle(LiveCharacter fighter, Enemy enemy)
    {
        var totalDamage = 0;
        foreach (var member in _maze.PartyMembers.Where(member => member.Character != fighter &&
                     member.Character.IsAlive && !_session.IsHumanControlled(member.Character.Id)))
        {
            member.Character.AdvanceSpellEffects();
            totalDamage += ChooseNpcBattlePlayerAction(member, enemy, fighter)?.DamageToEnemy ?? 0;
        }
        return totalDamage;
    }


    // NPC spellcasting for exploration - simple heals/cures/buffs
    private bool TryNpcCastExplorationSpell(PartyMemberAvatar member)
    {
        var caster = member.Character;
        if (!caster.IsAlive || !caster.IsSpellcaster || !caster.CanCastSpells) return false;
        var manaReservePercent = 20;
        var manaReserve = Math.Max(0, caster.MaximumMana * manaReservePercent / 100);
        var healThresholdPercent = 50; // more generous during exploration
        var allies = CharacterRoster.Party.Members.Append(caster).Distinct().Where(c => c.IsAlive).ToList();
        var lowest = allies.OrderBy(c => (double)c.CurrentVitality / c.MaximumVitality).FirstOrDefault();
        if (lowest is not null && (double)lowest.CurrentVitality / lowest.MaximumVitality * 100 <= healThresholdPercent)
        {
            foreach (var spell in caster.MemorizedSpells.Where(s => s.CanUseDuringExploration))
            {
                var effects = _gameData.GetSpellEffects(spell.Id);
                if (!effects.Any(e => e.Type == SpellEffectType.Heal)) continue;
                var manaCost = SpellcastingRules.EffectiveManaCost(caster, spell);
                if (caster.CurrentMana < manaCost) continue;
                if (caster.CurrentMana - manaCost < manaReserve) continue;
                var divine = caster.RecordDivineSpellCast(spell);
                caster.SpendMana(manaCost);
                PlaySessionSound(SoundEffect.DefensiveSpell, [caster.Id, lowest.Id]);
                var notes = new List<string>();
                foreach (var effect in effects.Where(e => e.Type == SpellEffectType.Heal))
                    ApplyHealingForCaster(effect, spell, new[] { lowest }, divine, notes, caster);
                var summary = notes.Count == 0 ? "" : $" {string.Join("; ", notes)}";
                var message = $"{caster.Name} elsüti: {spell.Name} → {lowest.Name}. -{manaCost} manna.{summary}";
                _renderer.DrawInventoryMessage(message, ConsoleColor.Green);
                RecordSessionActivity(SessionActivityKind.Support, message, ConsoleColor.Green);
                _renderer.RefreshCharacterSheet(PartyLeader);
                return true;
            }
        }
        return false;
    }

    private void ApplyCharacterEffectForCaster(LiveCharacter character, SpellEffectDefinition effect, SpellDefinition spell,
        ActiveSpellEffectType type, bool divineJudgment, LiveCharacter caster) =>
        _spellExecutionService.ApplyCharacterEffect(caster, character, effect, spell, type, divineJudgment);

    private void ApplyCharacterEffectsForCaster(IEnumerable<LiveCharacter> characters, SpellEffectDefinition effect,
        SpellDefinition spell, ActiveSpellEffectType type, bool divineJudgment, LiveCharacter caster) =>
        _spellExecutionService.ApplyCharacterEffects(caster, characters, effect, spell, type, divineJudgment);

    private void ApplyHealingForCaster(SpellEffectDefinition effect, SpellDefinition spell,
        IEnumerable<LiveCharacter> characters, bool divineJudgment, ICollection<string> notes, LiveCharacter caster) =>
        _spellExecutionService.ApplyHealing(caster, effect, spell, characters, divineJudgment, notes);

    private void ApplyStatusCureForCaster(SpellEffectDefinition effect, IEnumerable<LiveCharacter> characters,
        ICollection<string> notes) =>
        _spellExecutionService.ApplyStatusCure(effect, characters, notes);


    public void Run(ICoopHostLoop? coopHost = null)
    {
        _activeCoopHost = coopHost;
        _coopSnapshotDirty = true;
        _nextCoopSnapshotHeartbeatUtc = DateTime.MinValue;
        WaitForUsableTerminal(processSession: false);
        var previousViewport = TerminalViewport.TryGetSize(out var initialViewport)
            ? initialViewport
            : default;
        try { Console.CursorVisible = false; }
        catch (Exception exception) when (TerminalViewport.IsTransientConsoleException(exception)) { }
        if (_loadedState is null)
        {
            StartNewMaze(showLevelImage: false);
#if !DEBUG
            ShowSynchronizedNarrative(NarrativeKind.CampaignIntroduction, "A KÁOSZRUBIN KRÓNIKÁJA",
                "I. fejezet — A tizenkét aranykulcs", StoryNarratives.CampaignIntroduction);
#endif
            ShowLevelImage();
        }
        else RestoreGame(_loadedState);
        if (_loadedState is null) _nextNeedsDrain = DateTime.UtcNow + TimeSpan.FromMinutes(1);
        _nextAdHocConversationCheckUtc = DateTime.UtcNow + TimeSpan.FromMinutes(1);
        if (coopHost is not null)
            _renderer.DrawDeveloperMessage($"Coop host aktív: {coopHost.ConnectionHint}");
        try
        {
            for (int i = 0; i < CharacterRoster.Party.Members.Count; i++)
            {
                var member = CharacterRoster.Party.Members[i];
                if (Session.IsHumanControlled(member.Id))
                {
                    _humanMemberIds.Add(member.Id);
                    Log.Info($"Character {member.Name}(Id={member.Id}) is human controlled.");
                }
            }

            while (!_gameOver)
            {
                try
                {
                WaitForUsableTerminal(processSession: true);
                if (TerminalViewport.TryGetSize(out var currentViewport) && currentViewport != previousViewport)
                {
                    previousViewport = currentViewport;
                    _renderer.DrawInitialState(_maze, _player, _fogOfWar, _difficultyLevel);
                    _renderer.CharacterSheet.SetCharacterSheetFocused(_characterSheetFocused);
                }
                if (Console.KeyAvailable)
                {
                    var keyInfo = Console.ReadKey(intercept: true);
                    if (_activeBattle is not null && !_isQuickBattle &&
                        GameInputBindings.BattleDetailsPageDirection(keyInfo) is var detailDirection && detailDirection != 0)
                    {
                        _renderer.CharacterSheet.PageBattleDetails(detailDirection);
                        continue;
                    }
                    if (keyInfo.Key is ConsoleKey.PageUp or ConsoleKey.PageDown)
                    {
                        _renderer.ScrollMessageLog(keyInfo.Key == ConsoleKey.PageUp);
                        continue;
                    }
                    if (GameInput.IsSettingsShortcut(keyInfo))
                    {
                        RunHostPersonalWindow(PlayerWindowKind.Settings,
                            () => SettingsScreen.Show(_gameSettings, ApplyAudioSettings,
                                CurrentHostCoopWindowStatus));
                        _renderer.DrawInitialState(_maze, _player, _fogOfWar, _difficultyLevel);
                        _renderer.CharacterSheet.SetCharacterSheetFocused(_characterSheetFocused);
                        continue;
                    }
                    MarkCoopSnapshotDirty();
                    if (_activeBattle is not null)
                    {
                        HandleLocalBattleInput(keyInfo);
                        continue;
                    }
                    if (IsHelpShortcut(keyInfo))
                    {
                        ShowInGameHelp();
                        continue;
                    }
                    if (IsSaveGameShortcut(keyInfo))
                    {
                        SaveGame();
                        continue;
                    }
                    if (keyInfo.Key == ConsoleKey.F12)
                    {
                            _renderer.DrawInitialState(_maze, _player, _fogOfWar, _difficultyLevel);
                            continue;
                    }
                    if (keyInfo.Key == ConsoleKey.Q)
                    {
                        ShowQuestJournal();
                        continue;
                    }
                    if (keyInfo.Key == ConsoleKey.V)
                    {
                        BeginExplorationSpellCasting();
                        continue;
                    }
                    if (!(_characterSheetFocused && _renderer.CharacterSheet.IsSpellInfoPageOpen) &&
                        TryGetQuickSpellIndex(keyInfo, out var quickSpellSlot))
                    {
                        var quickSpell = PartyLeader.QuickSpells[quickSpellSlot];
                        if (quickSpell is null)
                            _renderer.DrawInventoryMessage("Ez a varázslat-gyorshely üres.", ConsoleColor.DarkYellow);
                        else
                            BeginExplorationSpellCasting(quickSpell);
                        continue;
                    }
                    if (GameInputBindings.IsCharacterSheetToggle(keyInfo.Key))
                    {
                        if (_characterSheetFocused) CancelHeldInventoryItem();
                        _characterSheetFocused = !_characterSheetFocused;
                        _renderer.CharacterSheet.SetCharacterSheetFocused(_characterSheetFocused);
                        continue;
                    }

                    if (CheckDevToolsKeys(keyInfo))
                        continue;

                    if (_characterSheetFocused)
                    {
                        if (_renderer.CharacterSheet.IsItemInspectionPageOpen)
                        {
                            if (keyInfo.Key is ConsoleKey.Escape or ConsoleKey.I or ConsoleKey.Enter)
                                _renderer.CharacterSheet.CloseItemInspectionPage();
                            continue;
                        }
                        if (_renderer.CharacterSheet.IsSpellInfoPageOpen)
                        {
                            if (keyInfo.Key == ConsoleKey.Escape)
                                _renderer.CharacterSheet.CloseSpellInfoPage();
                            else if (keyInfo.Key == ConsoleKey.UpArrow) _renderer.CharacterSheet.MoveSpellInfoSelection(-1);
                            else if (keyInfo.Key == ConsoleKey.DownArrow) _renderer.CharacterSheet.MoveSpellInfoSelection(1);
                            else if (TryGetQuickSpellIndex(keyInfo, out var spellSlot)) AssignSelectedSpellQuickSlot(spellSlot);
                            else if (keyInfo.Key == ConsoleKey.Enter) CastSelectedSpellInfo();
                            continue;
                        }
                        if (keyInfo.Key == ConsoleKey.Escape)
                        {
                                CancelHeldInventoryItem();
                                _characterSheetFocused = !_characterSheetFocused;
                                _renderer.CharacterSheet.SetCharacterSheetFocused(_characterSheetFocused);
                                continue;
                        }
                        if (keyInfo.Key == ConsoleKey.A && _renderer.CharacterSheet.DisplayedCharacter == PartyLeader)
                        {
                            EditFormation();
                            continue;
                        }
                        switch (GameInputBindings.InventoryAction(keyInfo.Key))
                        {
                            case InventoryInputAction.MoveUp: _renderer.CharacterSheet.MoveCharacterSheetSelection(-1); break;
                            case InventoryInputAction.MoveDown: _renderer.CharacterSheet.MoveCharacterSheetSelection(1); break;
                            case InventoryInputAction.Drop: DropSelectedInventoryItem(); break;
                            case InventoryInputAction.Inspect: InspectSelectedInventoryItem(); break;
                            case InventoryInputAction.Use: UseSelectedInventoryItem(); break;
                            case InventoryInputAction.MoveItem: GrabOrPlaceInventoryItem(); break;
                            case InventoryInputAction.SplitStack: SplitSelectedInventoryStack(); break;
                            case InventoryInputAction.DistributeStack: DistributeSelectedInventoryStack(); break;
                            case InventoryInputAction.CharacterDetails: ShowCharacterDetails(); break;
                            case InventoryInputAction.GiveFollowerStack: GiveSelectedStackToFollower(); break;
                            default:
                                if (keyInfo.Key == ConsoleKey.LeftArrow) _renderer.CharacterSheet.MoveDisplayedPartyMember(-1);
                                else if (keyInfo.Key == ConsoleKey.RightArrow) _renderer.CharacterSheet.MoveDisplayedPartyMember(1);
                                else if (keyInfo.Key == ConsoleKey.Delete) DismissSelectedPartyMember();
                                break;
                        }
                        continue;
                    }

                    var key = keyInfo.Key;
                    if (key == ConsoleKey.Escape)
                    {
                        if (ConfirmReturnToMainMenu()) return;
                        continue;
                    }
                    SubmitLocalExplorationCommand(keyInfo);
                }

                var now = DateTime.UtcNow;
                ProcessSessionCommands();

                if (_activeBattle is not null &&
                    _automaticBattleResumeUtc is { } battleResumeUtc &&
                    DateTime.UtcNow >= battleResumeUtc)
                {
                    ContinueBattle();
                }
                    
                if (PruneDisconnectedPlayerWindows()) RefreshCoopWindowStatus();
                    ContinueDisconnectedRemoteBattleAsNpc();

                if (!_battleStarted && ProcessPendingRodericTransition()) continue;

                if (_openPlayerWindows.Count > 0)
                {
                    TryPublishScheduledCoopSnapshot(now);
                    Thread.Sleep(20);
                    continue;
                }

                if (!_battleStarted && now >= _nextEnemyActionUtc)
                {
                    if (MoveEnemies(now)) MarkCoopSnapshotDirty();
                }

                if (!_battleStarted && ShouldProcessPartyMembers(now))
                {
                    if (MovePartyMembers(now)) MarkCoopSnapshotDirty();
                }

                if (!_battleStarted && now >= _nextAdHocConversationCheckUtc)
                {
                    _nextAdHocConversationCheckUtc = now + TimeSpan.FromMinutes(1);
                    if (TryStartAdHocFollowerConversation(now)) MarkCoopSnapshotDirty();
                }

                if (!_battleStarted && now >= _nextNeedsDrain)
                {
                    DrainNeeds();
                    MarkCoopSnapshotDirty();
                    _nextNeedsDrain = now + TimeSpan.FromMinutes(1);
                }

                if (!_battleStarted && now >= _nextNpcSelfCareCheck)
                {
                    if (ProcessNpcSelfCare(now)) MarkCoopSnapshotDirty();
                    _nextNpcSelfCareCheck = now + TimeSpan.FromSeconds(1);
                }

                TryPublishScheduledCoopSnapshot(now);

                Thread.Sleep(20);
                }
                catch (Exception exception) when (TerminalViewport.IsTransientConsoleException(exception))
                {
                    // Az ablak átméretezése közben a konzol koordinátái egy pillanatra
                    // érvénytelenné válhatnak. A következő ciklus stabil méretnél újrarajzol.
                    previousViewport = default;
                    Thread.Sleep(50);
                }
            }
        }
        finally
        {
            try { _soundEffects.Dispose(); }
            catch (Exception exception) { Log.Error("audio.effects-dispose-failed", exception); }
            if (_activeCoopHost is not null)
            {
                try
                {
                    PublishRemoteCharacterStates(CharacterSyncReason.SessionEnded);
                    _activeCoopHost.TryPublish(CreateSessionSnapshot());
                }
                catch (Exception exception)
                {
                    // A leállítási snapshot soha nem fedheti el a játékhurkot megszakító eredeti hibát.
                    Log.Error("session.shutdown-publish-failed", exception);
                }
            }
            _activeCoopHost = null;
            try { Console.CursorVisible = true; }
            catch (Exception exception) when (TerminalViewport.IsTransientConsoleException(exception)) { }
            try
            {
                Console.SetCursorPosition(0, Math.Min(ConsoleRenderer.ScreenRowCount - 1,
                    Math.Max(0, Console.BufferHeight - 1)));
            }
            catch (Exception exception) when (TerminalViewport.IsTransientConsoleException(exception))
            {
            }
        }
    }

    private bool CheckDevToolsKeys(ConsoleKeyInfo keyInfo)
    {
        bool devToolStarted = false;

#if DEBUG
        if (IsRevealMapShortcut(keyInfo))
        {
            var isMapRevealed = _fogOfWar.ToggleDeveloperReveal();
            _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
            _renderer.DrawDeveloperMessage(isMapRevealed
                ? "Fejlesztői mód: teljes térkép felfedve."
                : "Fejlesztői mód: köd visszaállítva.");
            devToolStarted = true;
        }
        if (IsNewMazeShortcut(keyInfo))
        {
            StartNewMaze();
            devToolStarted = true;
        }
        if (IsTeleportToExitShortcut(keyInfo))
        {
            TeleportLeaderNearExit();
            _player.Character.AddGold(1000);
            devToolStarted = true;
        }
        if (IsTeleportToNextUniqueNpcShortcut(keyInfo))
        {
            TeleportLeaderToNextUniqueNpc();
            devToolStarted = true;
        }
        if (IsTeleportPartyToPositionShortcut(keyInfo))
        {
            TeleportPartyToSelectedPosition();
            devToolStarted = true;
        }
        if (IsLevelUpShortcut(keyInfo))
        {
            TriggerDeveloperLevelUp();
            devToolStarted = true;
        }
        if (IsLevelUpPartyShortcut(keyInfo))
        {
            GrantPartyExperienceForDevelopment();
            devToolStarted = true;
        }
        if (IsDeveloperBattleTestShortcut(keyInfo))
        {
            StartDeveloperBattleTest();
            devToolStarted = true;
        }
        if (IsFillPartySetYShortcut(keyInfo))
        {
            FillPartyForDevelopment([CharacterClassIds.Harcos, CharacterClassIds.Mágus, CharacterClassIds.Lovag], "Y");
            devToolStarted = true;
        }
        if (IsFillPartySetXShortcut(keyInfo))
        {
            FillPartyForDevelopment([CharacterClassIds.Barbár, CharacterClassIds.Tolvaj, CharacterClassIds.Pap], "X");
            devToolStarted = true;
        }
        if (IsAddLevelOnePartyMemberShortcut(keyInfo))
        {
            AddLevelOnePartyMemberForDevelopment();
            devToolStarted = true;
        }
        if (IsDeveloperPhasingShortcut(keyInfo))
        {
            ToggleDeveloperPhasing();
            devToolStarted = true;
        }
#endif
        return devToolStarted;
    }

    private void WaitForUsableTerminal(bool processSession)
    {
        var minimumWidth = 200;
        var minimumHeight = 50;
        TerminalViewport.Size lastWarningSize = default;
        var warningDrawn = false;
        while (true)
        {
            if (TerminalViewport.TryGetSize(out var size) && size.CanFit(minimumWidth, minimumHeight)) return;

            if (size != lastWarningSize || !warningDrawn)
            {
                TerminalViewport.DrawSizeWarning(size, minimumWidth, minimumHeight);
                lastWarningSize = size;
                warningDrawn = true;
            }
            if (processSession)
            {
                ProcessSessionCommands();
                TryPublishScheduledCoopSnapshot(DateTime.UtcNow);
            }
            Thread.Sleep(80);
        }
    }

    private void CompleteCampaign()
    {
        if (_collectedBossKeyIds.Count < MonsterIds.Bosses.Count)
        {
            _renderer.DrawInventoryMessage(
                $"A Káoszrubin körül még zárva kering néhány aranylakat. Kulcsok: {_collectedBossKeyIds.Count}/{MonsterIds.Bosses.Count}.",
                ConsoleColor.Yellow);
            return;
        }

        PlaySessionSound(SoundEffect.LevelComplete);
        PlaySessionSound(SoundEffect.Victory);
        _renderer.PlayScreenBurnEffect();
        ShowSynchronizedNarrative(NarrativeKind.CampaignFinale, "GRATULÁLUNK, KULCSHORDOZÓK!",
            "XV. fejezet — A csillagok választottai",
            StoryNarratives.CreateCampaignFinale(CharacterRoster.Party.Members.Where(character => character.IsAlive), PartyLeader.Name));
        _gameOver = true;
        _session.SetPhase(GameSessionPhase.GameOver);
        RequestCoopSnapshotPublish();
    }

    private void StartNewMaze(bool showLevelImage = true)
    {
        _locationKind = AdventureLocationKind.Campaign;
        _locationId = $"CAMPAIGN_{_mazeLevel:00}";
        _difficultyLevel = _mazeLevel;
        _suspendedCampaignState = null;
        _session.SetPhase(GameSessionPhase.Exploration);
        _session.SynchronizeParty();
        NormalizeFormation();
        _formation = PartyFormationRules.WithState(_formation, PartyFormationState.Disbanded);
        _renderer.CharacterSheet.SetFormationStatus(_formation);
        _session.SetFormationMovementLocked(false);
        _hasRestedThisLevel = false;
        _spottedEnemyIds.Clear();
        _spottedChestIds.Clear();
        foreach (var character in CharacterRoster.Party.Members)
        {
            character.ResetLevelResurrection();
            character.ResetLevelRelentless();
        }
        var configuration = MazeLevelConfigurations.Get(_mazeLevel);
        _dungeonLevel = GenerateDungeonLevel(configuration);
        _maze = _dungeonLevel.ActiveArea.Maze;
        _fogOfWar = _dungeonLevel.ActiveArea.FogOfWar;

        foreach (var roomId in configuration.QuestRoomIds) 
        { 
            if (!_dungeonLevel.Areas.Any(area => area.Maze.GetRoomByContentId(roomId) is not null))
                throw new InvalidOperationException($"A generált pályáról hiányzik a kötelező questroom: {roomId}.");
        }

        _player = new Player(_maze.Entrance, PartyLeader);
        _leaderTrail.Clear();
        _leaderTrail.Add(_player.Position);
        _nextPartyMoves.Clear();
        PlacePartyMembersNear(_player.Position);
        PlaceCarriedTemporaryFollowersNear(_player.Position);
        PlaceTrapsAcrossAreas(configuration);
        PlaceFirstSinglePlayerCompanion();
        PlaceConfiguredWorldNpcs();
        PlaceSpecialRoomContent(configuration);
        CaptureExpeditionEnemyTemplates();
        RevealFor(PartyLeader, _player.Position);
        foreach (var member in _maze.PartyMembers) RevealFor(member.Character, member.Position);
        _battleStarted = false;
        _gameOver = false;
        InitializeEnemyMoveSchedule(DateTime.UtcNow);
        _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
        if (configuration.VisionModifier < 0)
        {
            var darknessMessage = $"🌑 Extra sötét pálya: minden karakter látótávja {configuration.VisionModifier}.";
            _renderer.DrawInventoryMessage(darknessMessage, ConsoleColor.DarkRed);
            RecordSessionActivity(SessionActivityKind.System, darknessMessage, ConsoleColor.DarkRed);
        }
        CheckBossDiscovery(_maze.Enemies.Where(enemy => _fogOfWar.IsRevealed(enemy.Position)));
        PlaySessionSound(SoundEffect.LevelStart);
        _backgroundMusic.SynchronizeMazeLevel(_mazeLevel, IsLevelExitDiscovered());
        _activeInnDeparture = null;
        if (showLevelImage) ShowLevelImage();
        LogMazeAccessibilityCheck();
    }

    private DungeonLevel GenerateDungeonLevel(MazeLevelConfiguration configuration)
    {
        ResolvedEnemyEncounter ResolveEncounter(EnemyEncounterConfiguration encounter) => new(
            encounter.GroupCount,
            encounter.Members.Select(member => new ResolvedEnemyGroupMember(
                _gameData.GetEnemy(member.EnemyId), member.Count, member.Role)).ToList(),
            encounter.MovementProfile,
            encounter.Behavior);

        var layout = configuration.Layout ??
                     new ClassicMazeLayoutConfiguration(configuration.DoubleWidthCorridorChance);
        if (layout is WideMazeLayoutConfiguration invalidWide &&
            (invalidWide.AreaCount.Minimum < 1 || invalidWide.AreaCount.Maximum < invalidWide.AreaCount.Minimum ||
             invalidWide.NarrowingChance is < 0 or > 1))
            throw new InvalidOperationException("A széles pálya területszáma vagy szűkületi esélye érvénytelen.");
        var areaCount = layout is WideMazeLayoutConfiguration wide ? wide.AreaCount.Roll(_random) : 1;
        if (areaCount <= 0) throw new InvalidOperationException("A szint területszáma nem lehet nulla.");

        var roomBuckets = DistributeEncounters(configuration.RoomEncounters.Select(ResolveEncounter), areaCount);
        var corridorBuckets = DistributeEncounters(configuration.CorridorEncounters.Select(ResolveEncounter), areaCount);
        var rolledSettings = configuration.CreateGenerationSettings(_random);
        var areas = new List<DungeonArea>(areaCount);
        for (var index = 0; index < areaCount; index++)
        {
            var isFinal = index == areaCount - 1;
            var settings = AreaGenerationSettings(rolledSettings, index, areaCount, isFinal);
            _generator = layout.Style == MazeLayoutStyle.Wide
                ? new WideMazeGenerator(settings, roomBuckets[index], corridorBuckets[index], _random)
                : new MazeGenerator(settings, roomBuckets[index], corridorBuckets[index], _random);
            var maze = _generator.Create(MazeWidth, MazeHeight);
            areas.Add(new DungeonArea($"AREA_{index + 1}", maze,
                new FogOfWar(maze.Width, maze.Height, CharacterClassRules.BaseVisionRange)));
        }

        for (var index = 0; index < areas.Count - 1; index++)
        {
            var source = areas[index];
            var destination = areas[index + 1];
            var sourcePassage = MazeEdgePassageCarver.Carve(source.Maze, _random);
            var destinationPassage = MazeEdgePassageCarver.Carve(destination.Maze, _random,
                sourcePassage.OppositeEdge, sourcePassage.RelativeOffset);
            // Köztes képernyőn nincs valódi szintkijárat: a régi jobb alsó jel helyét az átjáró veszi át.
            source.Maze.PlaceExit(sourcePassage.Position);
            source.Maze.AddPassage(new MazePassage(sourcePassage.Position, destination.Id,
                destinationPassage.Position));
            destination.Maze.AddPassage(new MazePassage(destinationPassage.Position, source.Id,
                sourcePassage.Position));
        }
        return new DungeonLevel(areas, areas[0].Id);
    }

    private List<ResolvedEnemyEncounter>[] DistributeEncounters(
        IEnumerable<ResolvedEnemyEncounter> encounters, int areaCount)
    {
        if (areaCount == 1) return [encounters.ToList()];
        var result = Enumerable.Range(0, areaCount).Select(_ => new List<ResolvedEnemyEncounter>()).ToArray();
        foreach (var encounter in encounters)
        {
            var total = encounter.GroupCount.Roll(_random);
            var offset = _random.Next(areaCount);
            for (var group = 0; group < total; group++)
                result[(offset + group) % areaCount].Add(encounter with { GroupCount = new IntRange(1, 1) });
        }
        return result;
    }

    private static MazeGenerationSettings AreaGenerationSettings(MazeGenerationSettings source,
        int areaIndex, int areaCount, bool includeSpecialRooms)
    {
        static int Share(int total, int index, int count) => total / count + (index < total % count ? 1 : 0);
        var specialCount = includeSpecialRooms ? source.QuestRoomIds.Count + source.BossRoomIds.Count : 0;
        return new MazeGenerationSettings
        {
            DoubleWidthCorridorChance = source.DoubleWidthCorridorChance,
            WideCorridorNarrowingChance = source.WideCorridorNarrowingChance,
            RoomCount = Math.Max(specialCount, Share(source.RoomCount, areaIndex, areaCount)),
            MinimumRoomSize = source.MinimumRoomSize,
            MaximumRoomSize = source.MaximumRoomSize,
            TreasureChestCount = Share(source.TreasureChestCount, areaIndex, areaCount),
            TreasureGoldRange = source.TreasureGoldRange,
            WallRune = source.WallRune,
            WallColor = source.WallColor,
            LevelName = areaCount == 1 ? source.LevelName : $"{source.LevelName} — {areaIndex + 1}/{areaCount}",
            QuestRoomIds = includeSpecialRooms ? source.QuestRoomIds : [],
            BossRoomIds = includeSpecialRooms ? source.BossRoomIds : [],
            SpecialRoomPlacements = includeSpecialRooms ? source.SpecialRoomPlacements
                : new Dictionary<string, SpecialRoomPlacement>(),
            QuestDoorRequirements = includeSpecialRooms ? source.QuestDoorRequirements
                : new Dictionary<string, QuestId>()
        };
    }


        private void StartRodericQuestLocation()
    {
        var follower = FindRodericFollower() ??
            throw new InvalidOperationException("Roderic nélkül nem indítható el Sir Malrec küldetéshelyszíne.");
        _suspendedCampaignState = CreateGameSaveData();
        ActivateNpcQuest(follower, RodericMalrecQuestId);
        CarryPersistentTemporaryFollowers();

        _locationKind = AdventureLocationKind.Quest;
        _locationId = QuestLocationConfigurations.RodericMalrec;
        var configuration = QuestLocationConfigurations.Get(_locationId);
        _difficultyLevel = configuration.Level;
        _session.SetPhase(GameSessionPhase.Exploration);
        _session.SynchronizeParty();
        NormalizeFormation();
        _formation = PartyFormationRules.WithState(_formation, PartyFormationState.Disbanded);
        _renderer.CharacterSheet.SetFormationStatus(_formation);
        _session.SetFormationMovementLocked(false);
        _hasRestedThisLevel = false;
        _spottedEnemyIds.Clear();
        _spottedChestIds.Clear();

        ResolvedEnemyEncounter ResolveEncounter(EnemyEncounterConfiguration encounter) => new(
            encounter.GroupCount,
            encounter.Members.Select(member => new ResolvedEnemyGroupMember(
                _gameData.GetEnemy(member.EnemyId), member.Count, member.Role)).ToList(),
            encounter.MovementProfile,
            encounter.Behavior);
        _generator = new MazeGenerator(configuration.CreateGenerationSettings(_random),
            configuration.RoomEncounters.Select(ResolveEncounter).ToList(),
            configuration.CorridorEncounters.Select(ResolveEncounter).ToList());
        _maze = _generator.Create(MazeWidth, MazeHeight);
        _player = new Player(_maze.Entrance, PartyLeader);
        _leaderTrail.Clear();
        _leaderTrail.Add(_player.Position);
        _nextPartyMoves.Clear();
        PlacePartyMembersNear(_player.Position);
        PlaceCarriedTemporaryFollowersNear(_player.Position);
        PlaceTraps(configuration);
        QuestChestPlacement.Place(_maze, _gameData, configuration.QuestChestPlacements);
        PlaceQuestRoomEnemies(configuration);
        _fogOfWar = new FogOfWar(_maze.Width, _maze.Height, CharacterClassRules.BaseVisionRange);
        _dungeonLevel = new DungeonLevel([new DungeonArea("AREA_1", _maze, _fogOfWar)], "AREA_1");
        CaptureExpeditionEnemyTemplates();
        RevealFor(PartyLeader, _player.Position);
        foreach (var member in _maze.PartyMembers) RevealFor(member.Character, member.Position);
        _battleStarted = false;
        InitializeEnemyMoveSchedule(DateTime.UtcNow);
        _renderer.DrawInitialState(_maze, _player, _fogOfWar, _difficultyLevel);
        _renderer.DrawInventoryMessage(
            "⚔ Küldetéshelyszín: Sir Malrec sírkápolnája (5. nehézség). A katakombák állapota megmaradt.",
            ConsoleColor.Cyan);
        _backgroundMusic.SynchronizeMazeLevel(_difficultyLevel, IsLevelExitDiscovered());
        LogMazeAccessibilityCheck();
    }

    private bool ProcessPendingRodericTransition()
    {
        if (FindRodericFollower() is { } follower)
        {
            var quest = follower.StoryStateId switch
            {
                "FOLLOWING" => _questManager.Roderic.Quests.PatriarchsShadows,
                "RELICS_ACTIVE" => _questManager.Roderic.Quests.OrderRelics,
                "MALREC_FIGHT" => _questManager.Roderic.Quests.OathbreakerKnight,
                _ => null
            };
            if (quest is not null && (quest.IsReadyToTurnIn || quest.IsCompleted))
            {
                ConverseWithRoderic(follower);
                return true;
            }
            if (follower.StoryStateId is "TRUSTED" or "RELICS_COMPLETE")
            {
                ConverseWithRoderic(follower);
                return true;
            }
            if (follower.StoryStateId == "MALREC_READY") _pendingRodericExpedition = true;
        }
        if (_pendingRodericExpedition)
        {
            _pendingRodericExpedition = false;
            var roderic = FindRodericFollower() ??
                throw new InvalidOperationException("Roderic eltűnt a saját történeti átmenete előtt.");
            if (roderic.StoryStateId != "MALREC_READY")
                return true;
            roderic.SetStoryState("MALREC_APPROACH");
            StartRodericQuestLocation();
            return true;
        }
        if (!_pendingRodericReturn) return false;
        _pendingRodericReturn = false;
        if (FindRodericFollower() is { } returningRoderic) RunStoryConversation(returningRoderic);
        ShowSynchronizedNarrative(NarrativeKind.QuestTransition, "AZ ESKÜSZEGŐ BUKÁSA",
            "Visszatérés a katakombákba",
            [
                "Sir Malrec páncélja üresen roskad a kőre. Roderic sokáig hallgat, majd letérdel egykori bajtársa mellé.",
                "„A múltat nem változtathatom meg. De többé nem hagyom, hogy helyettem döntsön.” A lovag visszavezet benneteket ugyanahhoz a pillanathoz, amelyben elhagytátok a katakombákat."
            ]);
        RestoreSuspendedCampaign();
        if (ResolveFailedRodericOath()) return true;
        TryFinalizeRodericPermanentJoin();
        return true;
    }

    private WorldNpc? FindRodericFollower() => _maze.PartyMembers
        .Select(member => member.TemporaryFollower)
        .FirstOrDefault(follower => follower is not null &&
            string.Equals(follower.StoryId, RodericStoryId, StringComparison.OrdinalIgnoreCase));

    private bool ResolveFailedRodericOath()
    {
        var avatar = _maze.PartyMembers.FirstOrDefault(member => member.TemporaryFollower is { } follower &&
            string.Equals(follower.StoryId, RodericStoryId, StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(follower.StoryStateId, "JOIN_REFUSED", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(follower.StoryStateId, "OATH_BROKEN", StringComparison.OrdinalIgnoreCase)));
        if (avatar?.TemporaryFollower is not { } roderic) return false;
        _maze.RemovePartyMember(avatar);
        _nextPartyMoves.Remove(avatar);
        CharacterRoster.Remove(roderic.Character);
        _session.SynchronizeParty();
        _renderer.DrawInventoryMessage(
            $"⚜ Roderic külön úton távozott. A viszonyotok {roderic.Friendliness}/10 volt; " +
            $"a végleges csatlakozáshoz legalább {RodericPermanentJoinFriendliness}/10 kellett volna.",
            ConsoleColor.DarkYellow);
        RequestCoopSnapshotPublish();
        return true;
    }

    private int RodericTargetLevel()
    {
        var requested = PartyLeader.Level >= 7 ? PartyLeader.Level + 2 : 7;
        return Math.Min(requested, _gameData.ExperienceByLevel.Keys.DefaultIfEmpty(requested).Max());
    }

    private void RestoreSuspendedCampaign()
    {
        var suspended = _suspendedCampaignState ??
            throw new InvalidOperationException("A felfüggesztett katakombapálya nem található.");
        if (FindRodericFollower() is { } currentRoderic)
        {
            for (var index = 0; index < suspended.Maze.PartyAvatars.Count; index++)
            {
                var avatar = suspended.Maze.PartyAvatars[index];
                if (avatar.TemporaryFollower is not { } saved ||
                    !string.Equals(saved.StoryId, RodericStoryId, StringComparison.OrdinalIgnoreCase)) continue;
                suspended.Maze.PartyAvatars[index] = avatar with
                {
                    TemporaryFollower = saved with
                    {
                        Disposition = currentRoderic.Disposition,
                        State = currentRoderic.State,
                        Friendliness = currentRoderic.Friendliness,
                        Behavior = currentRoderic.Behavior,
                        ConversationStage = currentRoderic.ConversationStage,
                        StoryStateId = currentRoderic.StoryStateId
                    }
                };
                break;
            }
        }

        var restored = _gameStateMapper.Restore(suspended, skipDepartedNpcCharacters: true);
        _mazeLevel = restored.MazeLevel;
        _locationKind = AdventureLocationKind.Campaign;
        _locationId = string.IsNullOrWhiteSpace(suspended.LocationId)
            ? $"CAMPAIGN_{_mazeLevel:00}" : suspended.LocationId;
        _difficultyLevel = suspended.DifficultyLevel > 0 ? suspended.DifficultyLevel : _mazeLevel;
        _suspendedCampaignState = null;
        _dungeonLevel = RestoreDungeonLevel(suspended, restored, skipDepartedNpcCharacters: true);
        _maze = _dungeonLevel.ActiveArea.Maze;
        _player = restored.Player;
        _fogOfWar = _dungeonLevel.ActiveArea.FogOfWar;
        _leaderFacing = restored.LeaderFacing;
        _formation = PartyFormationRules.Normalize(suspended.Formation,
            CharacterRoster.Party.Members.Select(member => member.Id), PartyLeader.Id);
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
        _spottedEnemyIds.Clear();
        _spottedChestIds.Clear();
        CaptureExpeditionEnemyTemplates();
        _battleStarted = false;
        _session.SetPhase(GameSessionPhase.Exploration);
        // A kampány pillanatképe csak a világot állítja vissza: a questhaladás azóta előreléphetett.
        _questManager.SynchronizeCollectQuests();
        _questManager.PublishState();
        RevealFor(PartyLeader, _player.Position);
        _renderer.DrawInitialState(_maze, _player, _fogOfWar, _difficultyLevel);
        _renderer.DrawInventoryMessage("↩ Visszatértetek a katakombák ugyanazon pontjára.", ConsoleColor.Cyan);
        _backgroundMusic.SynchronizeMazeLevel(_difficultyLevel, IsLevelExitDiscovered());
    }

    private bool TryFinalizeRodericPermanentJoin()
    {
        var avatar = _maze.PartyMembers.FirstOrDefault(member => member.TemporaryFollower is { } follower &&
            string.Equals(follower.StoryId, RodericStoryId, StringComparison.OrdinalIgnoreCase));
        if (avatar?.TemporaryFollower is not { } roderic ||
            !string.Equals(roderic.StoryStateId, "JOIN_ACCEPTED", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(roderic.StoryStateId, "JOIN_PENDING", StringComparison.OrdinalIgnoreCase)) return false;
        if (CharacterRoster.Party.Members.Count >= Party.MaximumSize)
        {
            roderic.SetStoryState("JOIN_PENDING");
            _renderer.DrawInventoryMessage(
                "⚜ Roderic letette az új esküt de a parti megtelt. Ideiglenes követő marad és az első felszabaduló helyre automatikusan belép.",
                ConsoleColor.DarkYellow);
            return false;
        }
        if (!CharacterRoster.Party.Add(roderic.Character)) return false;
        roderic.SetStoryState("JOINED");
        avatar.MakePermanent();
        _session.SynchronizeParty();
        _renderer.DrawInventoryMessage(
            "⚜ Roderic: „Amíg utunk közös a kardom a ti kardotok. A pajzsom a ti pajzsotok.” 🤝 Roderic végleg csatlakozott.",
            ConsoleColor.Green);
        RequestCoopSnapshotPublish();
        return true;
    }

    private void ShowLevelImage()
    {
#if !DEBUG
        var fileName = ImageViewer.FileNameForLevel(_maze.LevelName);
        var path = Path.Combine(AppContext.BaseDirectory, "Pictures", fileName);

        if (_activeCoopHost is not null)
        {
            ShowSynchronizedLevelImage(fileName, path);
            return;
        }

        if (!ImageViewer.Show(path))
            _renderer.DrawDeveloperMessage($"Pályakép még nem található: {fileName}");
#endif

    }

    private void LogMazeAccessibilityCheck()
    {
        var report = _maze.CheckFullAccessibility();
        _renderer.DrawDeveloperMessage(report.IsFullyAccessible
            ? $"Bejárhatósági önellenőrzés: OK, mind a(z) {report.TotalWalkableCount} padló-/ajtócella elérhető."
            : $"Bejárhatósági önellenőrzés: HIBA, {report.UnreachablePositions.Count}/{report.TotalWalkableCount} cella nem érhető el " +
              $"(pl. {report.UnreachablePositions[0].X},{report.UnreachablePositions[0].Y}).");
    }

    private void PlaceTrapsAcrossAreas(MazeLevelConfiguration configuration)
    {
        var total = configuration.TrapCount.Roll(_random);
        var active = _dungeonLevel.ActiveArea;
        for (var index = 0; index < _dungeonLevel.Areas.Count; index++)
        {
            var area = _dungeonLevel.Areas[index];
            _maze = area.Maze;
            _fogOfWar = area.FogOfWar;
            var count = total / _dungeonLevel.Areas.Count +
                        (index < total % _dungeonLevel.Areas.Count ? 1 : 0);
            PlaceTraps(configuration, count);
        }
        _maze = active.Maze;
        _fogOfWar = active.FogOfWar;
    }

    private void PlaceSpecialRoomContent(MazeLevelConfiguration configuration)
    {
        var active = _dungeonLevel.ActiveArea;
        var specialArea = _dungeonLevel.Areas.LastOrDefault(area =>
            configuration.QuestRoomIds.Concat(configuration.BossRoomIds)
                .Any(id => area.Maze.GetRoomByContentId(id) is not null)) ?? active;
        _maze = specialArea.Maze;
        _fogOfWar = specialArea.FogOfWar;
        QuestChestPlacement.Place(_maze, _gameData, configuration.QuestChestPlacements);
        PlaceQuestRoomEnemies(configuration);
        _maze = active.Maze;
        _fogOfWar = active.FogOfWar;
    }

    private void PlaceTraps(MazeLevelConfiguration configuration, int? requestedCount = null)
    {
        var definitions = configuration.TrapIds.Select(_gameData.GetTrap)
            .Where(trap => trap.MinimumLevel <= _difficultyLevel).ToArray();
        if (definitions.Length == 0) return;
        var desiredCount = requestedCount ?? configuration.TrapCount.Roll(_random);
        var candidates = new List<Position>();
        for (var y = 0; y < _maze.Height; y++)
        for (var x = 0; x < _maze.Width; x++)
        {
            var position = new Position(x, y);
            if (!_maze.IsWalkable(position) || position == _maze.Entrance || position == _maze.Exit ||
                _maze.StartingRoom?.Contains(position) == true || _maze.GetObjectAt(position) is not null ||
                _maze.GetPassageAt(position) is not null ||
                _maze.Rooms.Any(room => !room.AllowsRandomContent && room.Contains(position)) ||
                Manhattan(position, _maze.Entrance) < 6 ||
                _maze.Doors.Any(door => Manhattan(door.Position, position) <= 1)) continue;
            candidates.Add(position);
        }
        var placed = new List<Position>();
        foreach (var position in candidates.OrderBy(_ => _random.Next()))
        {
            if (placed.Any(existing => Manhattan(existing, position) < 3)) continue;
            _maze.AddTrap(new MazeTrap(position, definitions[_random.Next(definitions.Length)]));
            placed.Add(position);
            if (placed.Count >= desiredCount) break;
        }
    }

    private void PlaceFirstSinglePlayerCompanion()
    {
        if (_activeCoopHost is not null || _mazeLevel != 1 || CharacterRoster.Party.Members.Count != 1 ||
            _maze.WorldNpcs.Count != 0) return;

        var preferredClassIds = PartyLeader.CharacterClass.Id switch
        {
            CharacterClassIds.Harcos or CharacterClassIds.Barbár => new[] { CharacterClassIds.Pap, CharacterClassIds.Tolvaj },
            CharacterClassIds.Lovag => new[] { CharacterClassIds.Mágus, CharacterClassIds.Tolvaj },
            CharacterClassIds.Tolvaj => new[] { CharacterClassIds.Harcos, CharacterClassIds.Lovag },
            CharacterClassIds.Pap => new[] { CharacterClassIds.Harcos, CharacterClassIds.Barbár },
            CharacterClassIds.Mágus => new[] { CharacterClassIds.Lovag, CharacterClassIds.Harcos },
            _ => new[] { CharacterClassIds.Harcos }
        };
        var characterClass = _gameData.GetCharacterClass(preferredClassIds[_random.Next(preferredClassIds.Length)]);
        var recruit = new RandomCharacterGenerator(_gameData, _random).GenerateWorldNpc(characterClass, 1,
            CharacterRoster.Characters.Select(character => character.Name).ToArray(),
            RandomCharacterGenerator.EquipmentOptions.Starting);

        var candidates = new List<Position>();
        for (var y = 0; y < _maze.Height; y++)
        for (var x = 0; x < _maze.Width; x++)
        {
            var position = new Position(x, y);
            var distance = Manhattan(position, _maze.Entrance);
            if (!_maze.IsWalkable(position) || position == _maze.Exit || _maze.GetObjectAt(position) is not null ||
                _maze.GetTrapAt(position) is not null || distance < 6 || distance > 14) continue;
            candidates.Add(position);
        }
        if (candidates.Count == 0) return;

        CharacterRoster.Add(recruit);
        var spawnPosition = candidates[_random.Next(candidates.Count)];
        _maze.AddWorldNpc(new WorldNpc(spawnPosition, "NPC-FIRST-COMPANION", recruit, NpcDisposition.Friendly,
            recruitable: true, isQuestNpc: false,
            "Elvesztem ebben az átkozott labirintusban. Együtt talán kijutunk — veletek tartok, fizetség nélkül.",
            friendliness: 10, behavior: NpcWorldBehavior.Friendly));
    }

    private void PlaceConfiguredWorldNpcs()
    {
        var activeArea = _dungeonLevel.ActiveArea;
        foreach (var encounter in _gameData.NpcEncounters.Where(value => value.MazeLevel == _mazeLevel))
        {
            var targetArea = encounter.QuestRoomId is { } targetRoomId
                ? _dungeonLevel.Areas.FirstOrDefault(area => area.Maze.GetRoomByContentId(targetRoomId) is not null)
                : activeArea;
            targetArea ??= activeArea;
            _maze = targetArea.Maze;
            _fogOfWar = targetArea.FogOfWar;
            var definition = _gameData.GetNpc(encounter.NpcId);
            if (definition.Unique && CharacterRoster.Characters.Any(character =>
                    string.Equals(character.Name, definition.Name, StringComparison.OrdinalIgnoreCase))) 
            { 
                Log.Warning($"A(z) '{definition.Id}' egyedi NPC már szerepel a karakterlistában, ezért nem kerül elhelyezésre a pályán.");
                continue; 
            }
            var candidates = new List<Position>();
            for (var y = 0; y < _maze.Height; y++)
            for (var x = 0; x < _maze.Width; x++)
            {
                var position = new Position(x, y);
                var distance = Manhattan(position, _maze.Entrance);
                var questRoom = encounter.QuestRoomId is { } roomId ? _maze.GetRoomByContentId(roomId) : null;
                var isEligibleRoom = questRoom is not null
                    ? questRoom.Contains(position)
                    : !_maze.Rooms.Any(room => !room.AllowsRandomContent && room.Contains(position));
                if (!_maze.IsWalkable(position) || position == _maze.Exit || _maze.GetObjectAt(position) is not null ||
                    _maze.GetTrapAt(position) is not null || _maze.GetDoorAt(position) is not null ||
                    !isEligibleRoom || questRoom is null &&
                    (distance < encounter.MinimumDistance || distance > encounter.MaximumDistance)) continue;
                candidates.Add(position);
            }

            if (candidates.Count == 0) 
            {
                Log.Warning($"A(z) '{definition.Id}' NPC számára nincs megfelelő pozíció a pályán.");
                continue;
            }

            var generator = new RandomCharacterGenerator(_gameData, _random);
            var recruit = definition.Unique && _gameData.GetUniqueNpcCharacter(definition.Id) is not null
                ? new UniqueNpcCharacterFactory(_gameData).Create(definition,
                    string.Equals(definition.Id, "NPC021", StringComparison.OrdinalIgnoreCase)
                        ? RodericTargetLevel()
                        : null)
                : definition.Unique && definition.RaceId is { } raceId
                    ? generator.GenerateUniqueWorldNpc(definition.Name, _gameData.GetRace(raceId),
                        _gameData.GetCharacterClass(definition.CharacterClassId), PartyLeader.Level)
                : generator.GenerateWorldNpc(_gameData.GetCharacterClass(definition.CharacterClassId),
                    PartyLeader.Level, CharacterRoster.Characters.Select(character => character.Name).ToArray());
            CharacterRoster.Add(recruit);
            var friendliness = definition.Unique ? 4 : RollNpcFriendliness(definition);
            var dialogue = _gameData.GetNpcDialogues(definition.Id)
                .Where(value => friendliness >= value.MinimumFriendliness && friendliness <= value.MaximumFriendliness)
                .OrderBy(_ => _random.Next()).FirstOrDefault()?.Text ?? "Az idegen óvatosan végigmér benneteket.";
            var isQuestNpc = _gameData.Quests.GetByGiver(LegacyNpcIdMap.ToQuestNpcId(definition.Id)).Count > 0;
            _maze.AddWorldNpc(new WorldNpc(candidates[_random.Next(candidates.Count)], definition.Id, recruit,
                definition.Disposition, definition.Recruitable, isQuestNpc, dialogue,
                friendliness: friendliness, behavior: definition.Behavior,
                storyId: definition.StoryId));
        }
        _maze = activeArea.Maze;
        _fogOfWar = activeArea.FogOfWar;
    }

    private void PlaceQuestRoomEnemies(MazeLevelConfiguration configuration)
    {
        foreach (var encounter in configuration.QuestRoomEnemyEncounters)
        {
            var room = _maze.GetRoomByContentId(encounter.RoomId) ??
                throw new InvalidOperationException($"A quest room nem található: '{encounter.RoomId}'.");
            var center = new Position(room.TopLeft.X + room.Width / 2, room.TopLeft.Y + room.Height / 2);
            var positions = room.InteriorPositions().Where(position => _maze.IsWalkable(position) &&
                    _maze.GetObjectAt(position) is null && _maze.GetTrapAt(position) is null &&
                    _maze.Doors.All(door => Manhattan(door.Position, position) > 1))
                .OrderBy(position => Manhattan(position, center)).Take(encounter.Count).ToArray();
            if (positions.Length < encounter.Count)
                throw new InvalidOperationException($"A(z) '{encounter.RoomId}' quest roomban nincs hely " +
                                                    $"{encounter.Count} ellenfélnek.");
            foreach (var position in positions)
            {
                var enemy = new ConfiguredEnemy(position, _gameData.GetEnemy(encounter.EnemyId), _random);
                enemy.ConfigureMovement(EnemyMovementProfile.Stationary, Direction.Right);
                enemy.ConfigureGroup($"QUEST:{encounter.RoomId}");
                if (encounter.GuaranteedItemId is { } itemId) enemy.ConfigureGuaranteedLoot([itemId]);
                _maze.AddEnemy(enemy);
            }
        }
    }

    private int RollNpcFriendliness(NpcDefinition definition)
    {
        var baseValue = definition.Disposition switch
        {
            NpcDisposition.Friendly => _random.Next(7, 10),
            NpcDisposition.Neutral => _random.Next(3, 8),
            _ => _random.Next(0, 4)
        };
        var modifier = definition.Behavior switch
        {
            NpcWorldBehavior.Friendly => 1,
            NpcWorldBehavior.Guarded => -1,
            NpcWorldBehavior.Aggressive => -2,
            _ => 0
        };
        return Math.Clamp(baseValue + modifier, 0, 10);
    }

    /// <summary>A rejtett csapda egyszer kap passzív észlelési próbát. A felfedezett aktív csapda
    /// megállítja a mozgást, amíg K-val hatástalanítják.</summary>
}
