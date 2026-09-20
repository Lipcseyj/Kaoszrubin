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
    private void AwardBossKey(Enemy enemy)
    {
        if (!enemy.Definition.IsBoss || !_collectedBossKeyIds.Add(enemy.Definition.Id)) return;
        _renderer.SetGoldenKeyCount(_collectedBossKeyIds.Count);
        _renderer.RefreshCharacterSheet(PartyLeader);
        var completed = _collectedBossKeyIds.Count == MonsterIds.Bosses.Count
            ? " A tizenkét aranykulcs összegyűlt — a küldetés első célja teljesült!"
            : string.Empty;
        _renderer.DrawInventoryMessage($"🔑 Aranykulcs megszerezve: {enemy.Name}. " +
            $"Kulcsok: {_collectedBossKeyIds.Count}/{MonsterIds.Bosses.Count}.{completed}", ConsoleColor.Yellow);
        if (_collectedBossKeyIds.Count == MonsterIds.Bosses.Count)
            ShowSynchronizedNarrative(NarrativeKind.TwelveKeys, "A TIZENKÉT ZÁR FELNYÍLIK",
                "XIV. fejezet — A Rubin Útja", StoryNarratives.TwelveKeysStory);
    }

    private void StartBattle(Enemy enemy, bool enemyStrikesFirst = false)
        => StartTeamBattle(PartyLeader, enemy, enemyStrikesFirst);

    private void StartBattle(PartyMemberAvatar member, Enemy enemy, bool enemyStrikesFirst = false)
        => StartTeamBattle(member.Character, enemy, enemyStrikesFirst);

    private void StartTeamBattle(LiveCharacter initiatingCharacter, Enemy initiatingEnemy, bool enemyStrikesFirst)
    {
        if (_battleStarted || !initiatingCharacter.IsAlive || initiatingEnemy.CurrentHitPoints <= 0) return;
        CheckBossDiscovery([initiatingEnemy], initiatingCharacter);
        _timeStopUsedThisBattle = false;
        _turnUndeadNextAvailableRounds.Clear();
        _battleNoPathReported.Clear();
        _battleLogCycle = -1;
        _lastDelayedAction = null;
        _pendingLevelUps.Clear();
        if (_renderer.CharacterSheet.IsSpellInfoPageOpen)
            _renderer.CharacterSheet.CloseSpellInfoPage();

        var characterParticipants = new List<TeamCharacterParticipant>();
        var preparationEntries = new List<BattleLogEntry>();
        foreach (var (character, position) in LivingPartyWithPositions().DistinctBy(entry => entry.Character.Id))
        {
            var preparation = _battleSystem.PrepareCharacter(character);
            preparationEntries.AddRange(preparation.Entries);
            var avatar = _maze.PartyMembers.FirstOrDefault(member => member.Character == character);
            var kind = avatar?.IsTemporaryFollower == true
                ? TacticalParticipantKind.Follower
                : TacticalParticipantKind.PartyMember;
            var disciplineMovement = character.HasTacticalDiscipline(TacticalDisciplines.Skirmisher) ? 1 : 0;
            characterParticipants.Add(new TeamCharacterParticipant(character, position, kind,
                preparation.Initiative,
                Math.Min(7, CharacterMobilityRules.Evaluate(character).CombatMovementAllowance + disciplineMovement),
                character == initiatingCharacter ? 1 : 2, preparation.Runtime, preparation.OpeningInitiative));
        }

        var friendlyPositions = characterParticipants.Select(value => value.Position).ToArray();
        var enemyParticipants = _maze.Enemies
            .Where(enemy => enemy.CurrentHitPoints > 0 &&
                            TacticalDistance.IsWithin(initiatingEnemy.Position, enemy.Position) &&
                            (enemy == initiatingEnemy || CanEnemyReachBattleWithinCycles(enemy, friendlyPositions)))
            .DistinctBy(enemy => enemy.Id)
            .Select(enemy => new TeamEnemyParticipant(enemy, _battleSystem.RollEnemyInitiative(enemy),
                EnemyMovementAllowance(enemy), enemy == initiatingEnemy ? 1 : 2))
            .ToList();
        if (enemyParticipants.All(value => value.Enemy != initiatingEnemy))
            enemyParticipants.Add(new TeamEnemyParticipant(initiatingEnemy,
                _battleSystem.RollEnemyInitiative(initiatingEnemy),
                EnemyMovementAllowance(initiatingEnemy), 1));

        var participantEnemies = enemyParticipants.Select(value => value.Enemy).ToArray();

        if (participantEnemies.Sum(e => e.Definition.Strength) > characterParticipants.Sum(c => c.Character.Abilities.Strength) ||
            (participantEnemies.Count() > characterParticipants.Count() + 2))
        {
            _backgroundMusic.EnterLargeBattle();
        }
        else
        {
            _backgroundMusic.EnterSmallBattle();
        }


        foreach (var enemy in participantEnemies) _battleSystem.PrepareEnemyForBattle(enemy);
        var quickAssessment = QuickCombatRules.Assess(characterParticipants.Select(value => value.Character),
            participantEnemies.Select(enemy => enemy.Definition),
            hasAvailableReinforcements: HasAvailableTeamReinforcements(participantEnemies),
            hasActiveFormation: _formation.State != PartyFormationState.Disbanded,
            isQuestImportant: participantEnemies.Any(IsQuestImportantEnemy),
            enemyStrikesFirst: enemyStrikesFirst,
            allowPlayerChoice: _gameSettings.Settings.QuickCombat == QuickCombatMode.Ask);
        _isQuickTeamBattle = ShouldUseQuickCombat(quickAssessment);
        _quickBattleSuppressedEntryCount = 0;

        _lastBattleActionDetails = null;
        ResetTeamMovement();
        _activeTeamBattle = new TeamBattleEncounter(initiatingEnemy.Position,
            characterParticipants, enemyParticipants, initiatingCharacter.Id, initiatingEnemy.Id,
            enemyStrikesFirst, formation: ActiveBattleFormation());
        var protectionMessages = new List<string>();
        foreach (var protectedParticipant in characterParticipants)
        {
            var knight = TryRollKnightProtector(protectedParticipant.Character);
            if (knight is null) continue;
            _battleSystem.SetTeamKnightProtection(protectedParticipant.Runtime, knight);
            protectionMessages.Add($"🛡️ {knight.Name} védi {protectedParticipant.Character.Name} első találatát.");
        }
        _activeTeamBattle.Turns.StartTurns();
        _preparedTeamBattleTurnId = 0;
        _battleStarted = true;
        if (_locationId == DeveloperBattleTestLocationId)
            _developerBattleLog.BeginBattle(_activeTeamBattle);
        _session.SetPhase(GameSessionPhase.Battle);
        PlaySessionSound(SoundEffect.BattleStart);
        _renderer.DrawBattleStarted(initiatingEnemy);
        TryLogPartyComments(PartySituationIds.BattleStarted);
        PresentBattleEntries(preparationEntries);
        foreach (var protectionMessage in protectionMessages)
        {
            _renderer.DrawInventoryMessage(protectionMessage, ConsoleColor.Cyan);
            RecordSessionActivity(SessionActivityKind.Battle, protectionMessage, ConsoleColor.Cyan);
        }
        var queue = string.Join(" → ", _activeTeamBattle.Turns.Participants
            .OrderByDescending(participant => participant.CurrentInitiative)
            .Select(participant =>
            {
                var name = _activeTeamBattle.CharacterFor(participant.Id)?.Name ??
                           _activeTeamBattle.EnemyFor(participant.Id)?.Name ?? participant.Id.Value;
                return $"{name} {participant.CurrentInitiative}";
            }));
        string OpeningName(CombatantId id) => _activeTeamBattle.CharacterFor(id)?.Name ??
                                               _activeTeamBattle.EnemyFor(id)?.Name ?? id.Value;
        var openingNames = _activeTeamBattle.OpeningOrder.Select(OpeningName).ToArray();
        var startMessage = _isQuickTeamBattle
            ? $"⚡ GYORSHARC — {initiatingEnemy.Name} ellen. A csapatharc automatikusan lefut."
            : $"⚔️ CSAPATHARC — {characterParticipants.Count} baráti és " +
              $"{enemyParticipants.Count} ellenséges résztvevő. " +
              $"Nyitó ütésváltás: {string.Join(" → ", openingNames)}. Utána kezdeményezés: {queue}.";
        if (_activeTeamBattle.HasProtectiveFormation)
            startMessage += " 🛡️ A zárt alakzat első sora elölről védi a hátsó sort.";
        _renderer.DrawInventoryMessage(startMessage, ConsoleColor.Yellow);
        RecordSessionActivity(SessionActivityKind.Battle, startMessage, ConsoleColor.Yellow);
        ContinueTeamBattle();
    }

    private PartyFormationSnapshot? ActiveBattleFormation()
    {
        if (_formation.State != PartyFormationState.Locked) return null;
        if (_formation.Layout == PartyFormationLayout.SingleFile)
            return _formation.Slots.Where(id => id is not null).All(id =>
                id == PartyLeader.Id || FormationAvatar(id!.Value) is not null)
                ? _formation
                : null;
        var expected = PartyFormationController.Positions(_formation, PartyLeader.Id, _player.Position);
        return expected.All(pair => CharacterRoster.Party.Members.FirstOrDefault(character =>
                    character.Id == pair.Key) is not { IsAlive: true } character ||
                GetCasterPosition(character) == pair.Value)
            ? _formation
            : null;
    }

    private void ContinueTeamBattle()
    {
        while (_activeTeamBattle is { } battle)
        {
            // Központi fék ami meg tudja állítani a csatát, hogy lássuk a csapást
            if (battle.PauseReason != BattlePauseReason.None)
            {
                SetTeamBattlePrompt(battle);
                return;
            }

            SynchronizeTeamBattleDefeats(battle);
            if (!PartyLeader.IsAlive)
            {
                FinishTeamBattle(battle, forceDefeat: true);
                return;
            }
            if (battle.IsCompleted)
            {
                FinishTeamBattle(battle);
                return;
            }
            if (battle.InactiveSidesLastCompletedCycle.Count > 0)
            {
                FinishTeamBattleStalemate(battle);
                return;
            }
            var reinforcementsArrived = TryCallTeamBattleReinforcements(battle);
            if (_isQuickTeamBattle && (reinforcementsArrived || battle.ActionNumber >= 200))
            {
                _isQuickTeamBattle = false;
                var reason = reinforcementsArrived
                    ? "Váratlan erősítés érkezett."
                    : "Az automatikus szimuláció nem tudta gyorsan lezárni az ütközetet.";
                var message = $"⚠️ {reason} A harc taktikai módban folytatódik.";
                _renderer.DrawInventoryMessage(message, ConsoleColor.DarkYellow);
                RecordSessionActivity(SessionActivityKind.Battle, message, ConsoleColor.DarkYellow);
            }

            var current = battle.Current;
            if (_battleLogCycle != battle.Turns.Cycle)
            {
                _battleLogCycle = battle.Turns.Cycle;
                _renderer.SetBattleCommandPanelRound(battle.Turns.Cycle);
                if (battle.InitiativeChangesAtCycleStart.Count > 0)
                {
                    var changes = string.Join(", ", battle.InitiativeChangesAtCycleStart.Select(change =>
                        $"{change.Name} {change.PreviousInitiative}→{change.CurrentInitiative}"));
                    var order = string.Join(" → ", battle.Turns.InitiativeOrder.Select(participant =>
                        battle.CharacterFor(participant.Id)?.Name ?? battle.EnemyFor(participant.Id)?.Name ??
                        participant.Id.Value));
                    PresentBattleEntries([new BattleLogEntry(
                        $"⚡ {battle.Turns.Cycle}. kör: kezdeményezés változott — {changes}. Új sorrend: {order}.",
                        BattleLogKind.Information)]);
                }
            }
            UpdateTeamBattleFocus(battle, current);

            if (DelayAutomaticTurns(battle))
                return;

            if (_preparedTeamBattleTurnId != battle.Turns.TurnId)
            {
                if (battle.CurrentCharacter is { } preparedCharacter &&
                    battle.ShouldAdvanceSpellEffects(CombatantId.ForCharacter(preparedCharacter.Id)))
                    _battleSystem.BeginCharacterTurn(preparedCharacter);
                var stagger = battle.PrepareStaggerAction(current.Id, () => _random.Next(1, 101));
                if (stagger is not null)
                {
                    var actorName = battle.CharacterFor(current.Id)?.Name ?? battle.EnemyFor(current.Id)?.Name ??
                        current.Id.Value;
                    PresentBattleEntries([new BattleLogEntry(stagger.BlocksOffensiveActions
                            ? $"💫 {actorName} megingása megszakítja ennek az akciónak a támadását vagy varázslását " +
                              $"({stagger.Roll}/{stagger.DisruptionChance}%)."
                            : $"💫 {actorName} összeszedi magát, de ebben az akcióban nem mozoghat " +
                              $"({stagger.Roll}/{stagger.DisruptionChance}%).",
                        BattleLogKind.Information)]);
                }
                _preparedTeamBattleTurnId = battle.Turns.TurnId;
            }

            if (battle.CurrentCharacter is { } character)
            {
                if (!character.IsAlive)
                {
                    battle.MarkDefeated(character);
                    AdvanceTeamBattleTurn(battle);
                    continue;
                }
                var isHumanControlled = _session.IsHumanControlled(character.Id);

                // PauseBeforeAnyAction:
                // az AI/NPC karakter teljes köre előtt egyszer megállunk.
                if (!_isQuickTeamBattle &&
                    !isHumanControlled &&
                    _gameSettings.Settings.CombatSpeed == CombatSpeed.PauseBeforeAnyAction &&
                    battle.PreActionPauseHandledTurnId != battle.Turns.TurnId)
                {
                    battle.PreActionPauseHandledTurnId = battle.Turns.TurnId;
                    battle.PauseReason = BattlePauseReason.BeforeAutomaticAction;

                    continue;
                }

                var runtime = battle.RuntimeFor(character);
                if (runtime.RequiresTacticSelection)
                {
                    if (!_isQuickTeamBattle && isHumanControlled)
                    {
                        SetTeamBattlePrompt(battle);
                        return;
                    }
                    ChooseTeamAiTactic(character, runtime);
                    continue;
                }
                if (!_isQuickTeamBattle && isHumanControlled)
                {
                    SetTeamBattlePrompt(battle);
                    return;
                }
                ExecuteTeamAiCharacterTurn(battle, character);
                continue;
            }

            if (battle.CurrentEnemy is { CurrentHitPoints: > 0 } enemyActor)
            {
                // Gyorsharcban, jelentéktelen enemy-akciónál, illetve minden olyan
                // tempónál, ahol nem akarunk az enemy akciója ELŐTT megállni,
                // az ellenfél automatikusan végrehajtja a körét.
                if (_isQuickTeamBattle ||
                    !CanTeamEnemyActMeaningfully(battle, enemyActor) ||
                    _gameSettings.Settings.CombatSpeed != CombatSpeed.PauseBeforeAnyAction)
                {
                    ExecuteTeamEnemyTurn(battle, enemyActor);
                    continue;
                }

                // PauseBeforeAnyAction: az ellenfél akciója előtt Space-re várunk.
                SetTeamBattlePrompt(battle);
                return;
            }

            AdvanceTeamBattleTurn(battle);
        }
    }

    private bool DelayAutomaticTurns(TeamBattleEncounter battle)
    {
        if (_isQuickTeamBattle ||
            _gameSettings.Settings.CombatSpeed == CombatSpeed.PauseBeforeAnyAction ||
            _gameSettings.Settings.CombatDelayMilliseconds <= 0)
            return false;

        var actorId = battle.LastActionActorId;
        if (actorId is null)
            return false;

        var participant = battle.Turns.Find(actorId.Value);
        if (participant is null)
            return false;

        var automatic = participant.Kind switch
        {
            TacticalParticipantKind.Enemy => true,
            TacticalParticipantKind.Follower => true,

            TacticalParticipantKind.PartyMember =>
                battle.CharacterFor(participant.Id) is { } character &&
                !_session.IsHumanControlled(character.Id),

            _ => false
        };

        if (!automatic)
            return false;

        var actionKey = (battle.Id, battle.ActionNumber);
        var now = DateTime.UtcNow;

        // Új automatikus akció fejeződött be:
        // most indul a várakozási idő.
        if (_lastDelayedAction != actionKey)
        {
            _lastDelayedAction = actionKey;

            _automaticBattleResumeUtc =
                now.AddMilliseconds(
                    _gameSettings.Settings.CombatDelayMilliseconds);

            return true;
        }

        // Ugyanezt az akciót már késleltetjük.
        if (_automaticBattleResumeUtc is { } resumeUtc &&
            now < resumeUtc)
        {
            return true;
        }

        // Letelt a várakozás.
        _automaticBattleResumeUtc = null;
        return false;
    }


    private bool TryCallTeamBattleReinforcements(TeamBattleEncounter battle)
    {
        if (!battle.BeginReinforcementCheckForCurrentCycle()) return false;
        var activeGroups = battle.Enemies.Where(enemy => !string.IsNullOrWhiteSpace(enemy.GroupId))
            .Select(enemy => enemy.GroupId!).ToHashSet(StringComparer.Ordinal);
        if (activeGroups.Count == 0) return false;
        var callers = battle.Enemies.Where(enemy => enemy.CurrentHitPoints > 0).ToArray();
        var friendlyPositions = battle.Characters.Where(character => character.IsAlive)
            .Select(GetCasterPosition).ToArray();
        var reinforcements = _maze.Enemies.Where(enemy => enemy.CurrentHitPoints > 0 && !battle.ContainsEnemy(enemy) &&
                enemy.GroupId is { Length: > 0 } groupId && activeGroups.Contains(groupId) &&
                callers.Any(caller => TacticalDistance.IsWithin(caller.Position, enemy.Position,
                    battle.Turns.Radius * 2)) && CanEnemyReachBattleWithinCycles(enemy, friendlyPositions))
            .ToArray();
        if (reinforcements.Length == 0) return false;
        foreach (var enemy in reinforcements)
        {
            _battleSystem.PrepareEnemyForBattle(enemy);
            battle.TryAddEnemy(new TeamEnemyParticipant(enemy, _battleSystem.RollEnemyInitiative(enemy),
                EnemyMovementAllowance(enemy), battle.Turns.Cycle + 1));
        }
        var message = $"📯 Az ellenség erősítést hív: {reinforcements.Length} új harcos " +
                      $"a(z) {battle.Turns.Cycle + 1}. körben kapcsolódik be.";
        _renderer.DrawInventoryMessage(message, ConsoleColor.DarkYellow);
        RecordSessionActivity(SessionActivityKind.Battle, message, ConsoleColor.DarkYellow);
        return true;
    }

    private bool HasAvailableTeamReinforcements(IReadOnlyCollection<Enemy> participants)
    {
        var participantIds = participants.Select(enemy => enemy.Id).ToHashSet();
        var groupIds = participants.Where(enemy => !string.IsNullOrWhiteSpace(enemy.GroupId))
            .Select(enemy => enemy.GroupId!).ToHashSet(StringComparer.Ordinal);
        if (groupIds.Count == 0) return false;
        var friendlyPositions = LivingPartyWithPositions().Select(entry => entry.Position).ToArray();
        return _maze.Enemies.Any(enemy => enemy.CurrentHitPoints > 0 && !participantIds.Contains(enemy.Id) &&
            enemy.GroupId is { Length: > 0 } groupId && groupIds.Contains(groupId) &&
            participants.Any(caller => TacticalDistance.IsWithin(caller.Position, enemy.Position,
                TacticalDistance.DefaultBattleRadius * 2)) &&
            CanEnemyReachBattleWithinCycles(enemy, friendlyPositions));
    }

    private bool CanEnemyReachBattleWithinCycles(Enemy enemy, IReadOnlyCollection<Position> friendlyPositions)
    {
        var movement = EnemyMovementAllowance(enemy);
        return TacticalArrivalRules.CanReachWithin(enemy.Position,
            movement * TacticalArrivalRules.MaximumArrivalCycles,
            position => CanPotentialBattleParticipantEnter(position),
            position => friendlyPositions.Any(target => TacticalDistance.IsMeleeAdjacent(position, target)));
    }

    private bool CanPotentialBattleParticipantEnter(Position position)
    {
        if (!_maze.IsWalkable(position)) return false;
        var occupant = _maze.GetObjectAt(position);
        return occupant is null or GroundItemPile or Corpse or Enemy or PartyMemberAvatar ||
               Maze.IsPassableNeutralNpc(occupant);
    }

    private static bool IsQuestImportantEnemy(Enemy enemy) => TacticalTeamBattleCoordinator.IsQuestImportantEnemy(enemy);

    private static int EnemyMovementAllowance(Enemy enemy) =>
        Math.Clamp((enemy.EffectiveSpeed + 1) / 2 +
                   (enemy.Definition.HasTrait(EnemyTraits.Flying) ? 1 : 0), 1, 7);

    private bool ShouldUseQuickCombat(QuickCombatAssessment assessment)
    {
        if (!assessment.IsEligible || _gameSettings.Settings.QuickCombat == QuickCombatMode.Never) return false;
        if (_gameSettings.Settings.QuickCombat == QuickCombatMode.Automatic) return true;

        var injuryPercent = (int)Math.Ceiling(assessment.PredictedInjuryRatio * 100);
        var message = $"⚡ Gyorsharc elérhető — {assessment.Reason} Becsült sérülés legfeljebb " +
                      $"{assessment.PredictedVitalityLoss} HP ({injuryPercent}%). " +
                      "I / Enter: gyorsharc, N / Esc: taktikai harc.";
        _renderer.DrawInventoryMessage(message, ConsoleColor.Cyan);
        while (true)
        {
            var key = Console.ReadKey(intercept: true).Key;
            if (key is ConsoleKey.I or ConsoleKey.Y or ConsoleKey.Enter) return true;
            if (key is ConsoleKey.N or ConsoleKey.Escape) return false;
        }
    }

    private void ExecuteTeamBattleAction(TeamBattleEncounter battle, BattleActionCommand command)
    {
        if (command.BattleId != battle.Id || command.TurnId != battle.Turns.TurnId) return;

        if (battle.PauseReason != BattlePauseReason.None)
        {
            if (command.Action != BattleActionKind.ResumeBattle)
            {
                RejectTeamBattleAction(command, "A harc folytatásához nyomj Space-t.");
                return;
            }

            battle.PauseReason = BattlePauseReason.None;

            ContinueTeamBattle();
            return;
        }

        if (battle.CurrentEnemy is { } enemyActor)
        {
            if (command.Action != BattleActionKind.AdvanceEnemyTurn || command.CharacterId != PartyLeader.Id)
            {
                RejectTeamBattleAction(command, "Most egy ellenfél következik.");
                return;
            }
            ExecuteTeamEnemyTurn(battle, enemyActor);
            ContinueTeamBattle();
            return;
        }
        if (battle.CurrentCharacter is not { } character || character.Id != command.CharacterId)
        {
            RejectTeamBattleAction(command, "Nem ez a karakter van soron.");
            return;
        }
        var focusEnemy = ClosestLivingTeamEnemy(battle, GetCasterPosition(character));
        var allowed = GetTeamAllowedBattleActions(battle, character, focusEnemy);
        if (!allowed.Contains(command.Action))
        {
            RejectTeamBattleAction(command, "Ez az akció most nem használható.");
            return;
        }
        switch (command.Action)
        {
            case BattleActionKind.SelectTarget:
                var selectedEnemy = command.TargetEnemyId is { } selectedId
                    ? battle.Enemies.FirstOrDefault(enemy => enemy.Id == selectedId)
                    : null;
                if (selectedEnemy is null || !ReachableTeamEnemies(battle, character).Contains(selectedEnemy) ||
                    !battle.TrySelectTarget(selectedEnemy))
                {
                    RejectTeamBattleAction(command, "A választott ellenfél nem elérhető célpont.");
                    return;
                }
                _renderer.DrawInventoryMessage($"Célpont: {selectedEnemy.Name}.", ConsoleColor.Yellow);
                break;
            case BattleActionKind.FighterPrecise:
            case BattleActionKind.FighterPowerful:
            case BattleActionKind.FighterDefensive:
            case BattleActionKind.ThiefAmbush:
            case BattleActionKind.ThiefObserve:
            case BattleActionKind.ThiefPoison:
                var tactic = ToBattleTactic(command.Action);
                if (!battle.RuntimeFor(character).TryChooseTactic(character, tactic))
                {
                    RejectTeamBattleAction(command, "Ez a harci taktika most nem választható.");
                    return;
                }
                var tacticMessage = $"{character.Name} harci taktikája: {BattleTacticName(tactic, character)}.";
                _renderer.DrawInventoryMessage(tacticMessage, ConsoleColor.Cyan);
                RecordSessionActivity(SessionActivityKind.Battle, tacticMessage, ConsoleColor.Cyan);
                break;
            case BattleActionKind.PhysicalAttack:
                var target = command.TargetEnemyId is { } targetId
                    ? battle.Enemies.FirstOrDefault(enemy => enemy.Id == targetId)
                    : battle.SelectedTargetEnemy() ??
                      ReachableTeamEnemies(battle, character).OrderBy(enemy => enemy.CurrentHitPoints).FirstOrDefault();
                if (target is null || target.CurrentHitPoints <= 0 ||
                    !ReachableTeamEnemies(battle, character).Contains(target))
                {
                    RejectTeamBattleAction(command, "A választott ellenfél nincs közelharci távolságban.");
                    return;
                }
                ResolveTeamCharacterAttack(battle, character, target);
                break;
            case BattleActionKind.ShieldBash:
                var bashTarget = command.TargetEnemyId is { } bashTargetId
                    ? battle.Enemies.FirstOrDefault(enemy => enemy.Id == bashTargetId)
                    : null;
                if (bashTarget is null || bashTarget.CurrentHitPoints <= 0 ||
                    !AdjacentTeamEnemies(battle, character).Contains(bashTarget))
                {
                    RejectTeamBattleAction(command, "A pajzslökés célpontja nincs közvetlen közelharci távolságban.");
                    return;
                }
                var bashShield = character.OperationalWeapons.FirstOrDefault(ShieldRules.IsShield);
                if (bashShield is null)
                {
                    RejectTeamBattleAction(command, "A pajzslökéshez működő pajzs szükséges.");
                    return;
                }
                ResolveTeamCharacterShieldBash(battle, character, bashTarget, bashShield);
                break;
            case BattleActionKind.Move when command.Target is { } destination:
                if (!TryExecuteTeamCharacterMove(battle, character, destination, out var movementError))
                {
                    RejectTeamBattleAction(command, movementError);
                    return;
                }
                break;
            case BattleActionKind.MoveFormation when command.Target is { } formationDestination:
                if (!TryExecuteTeamFormationMove(battle, character, formationDestination,
                        out var formationMovementError))
                {
                    RejectTeamBattleAction(command, formationMovementError);
                    return;
                }
                break;
            case BattleActionKind.SwapWeapon:
                if (!character.TrySwapReserveWeapon())
                {
                    RejectTeamBattleAction(command, "A tartalékfegyver most nem vehető kézbe.");
                    return;
                }
                var weaponSwapStatus = _battleSystem.FinishCharacterAction(character, battle.RuntimeFor(character));
                _renderer.RefreshCharacterSheet(PartyLeader);
                PresentBattleEntries([new BattleLogEntry($"{character.Name}: fegyvercsere → {character.AttackWeapon?.Name}.{weaponSwapStatus}", BattleLogKind.Information)]);
                AdvanceTeamBattleTurn(battle);
                break;
            case BattleActionKind.SwapToRear:
                if (!TryExecuteSwapToRear(battle, character, out var swapError))
                {
                    RejectTeamBattleAction(command, swapError);
                    return;
                }
                break;
            case BattleActionKind.PrepareRearLeft:
            case BattleActionKind.PrepareRearRight:
                var preparationSlot = command.Action == BattleActionKind.PrepareRearLeft
                    ? FormationSlot.RearLeft
                    : FormationSlot.RearRight;
                if (!battle.TryOrderRearCombatPreparation(preparationSlot, out var preparingCharacter))
                {
                    RejectTeamBattleAction(command, "A kijelölt hátsó alakzathelyen nincs harcra készíthető társ.");
                    return;
                }
                var preparationStatus = _battleSystem.FinishCharacterAction(character, battle.RuntimeFor(character));
                PresentBattleEntries([new BattleLogEntry(
                    $"{character.Name} jelzi {preparingCharacter!.Name} számára: készülj a harcra! " +
                    $"Amíg hátul marad, az önmaga erősítése az elsődleges feladata.{preparationStatus}",
                    BattleLogKind.Information)]);
                AdvanceTeamBattleTurn(battle);
                break;
            case BattleActionKind.Pass:
                if (IsTeamMovementInProgress(battle))
                {
                    FinishTeamCharacterMovement(battle, character);
                    break;
                }
                var passStatus = _battleSystem.FinishCharacterAction(character, battle.RuntimeFor(character));
                PresentBattleEntries([new BattleLogEntry(
                    $"{character.Name}\t⌛ Kivár.{passStatus}",
                    BattleLogKind.Information)]);
                AdvanceTeamBattleTurn(battle);
                break;
            case BattleActionKind.Retreat:
                ExecuteTeamRetreat(battle, character);
                return;
            case BattleActionKind.UseItem when command.BackpackIndex is { } backpackIndex:
                if (!TryUseTeamBattleItem(battle, character, backpackIndex, out var itemMessage))
                {
                    RejectTeamBattleAction(command, itemMessage);
                    return;
                }
                itemMessage +=
                    _battleSystem.FinishCharacterAction(character, battle.RuntimeFor(character));
                PresentBattleEntries([new BattleLogEntry(itemMessage, BattleLogKind.Information)]);
                AdvanceTeamBattleTurn(battle);
                break;
            case BattleActionKind.TurnUndead:
                var turnUndeadTargets = TurnUndeadTargets(battle, character).ToArray();
                var undead = command.TargetEnemyId is { } undeadId
                    ? turnUndeadTargets.FirstOrDefault(enemy => enemy.Id == undeadId)
                    : turnUndeadTargets.FirstOrDefault();
                if (undead is null)
                {
                    RejectTeamBattleAction(command, "Nincs elűzhető élőholt a közelben.");
                    return;
                }
                var turning = ResolveTurnUndead(character, undead);
                battle.RecordAttack(BattleSide.Friendly);
                if (turning.DamageToEnemy > 0) undead.ReceiveSpellDamage(turning.DamageToEnemy);
                var turnMessage = turning.Message +
                    _battleSystem.FinishCharacterAction(character, battle.RuntimeFor(character));
                PresentBattleEntries([new BattleLogEntry(turnMessage, turning.Kind)]);
                if (undead.CurrentHitPoints <= 0) ResolveTeamEnemyDefeat(battle, undead, character);
                AdvanceTeamBattleTurn(battle);
                break;
            case BattleActionKind.CastSpell:
                ExecuteTeamSpellBattleAction(battle, character, command);
                break;
        }
        ContinueTeamBattle();

    }

    private void ExecuteTeamSpellBattleAction(TeamBattleEncounter battle, LiveCharacter character,
        BattleActionCommand command)
    {
        if (command.SpellId is null || command.Target is null) return;
        var spell = _gameData.Spells.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, command.SpellId, StringComparison.OrdinalIgnoreCase));
        if (spell is null)
        {
            RejectTeamBattleAction(command, "Ismeretlen varázslat.");
            return;
        }
        MagicItemDefinition? castingItem = null;
        if (command.CastingItemSlotIndex is { } slot)
            castingItem = character.MagicItems.ElementAtOrDefault(slot);
        var currentEnemy = command.TargetEnemyId is { } enemyId
            ? battle.Enemies.FirstOrDefault(enemy => enemy.Id == enemyId && enemy.CurrentHitPoints > 0)
            : null;
        currentEnemy ??= battle.Enemies.FirstOrDefault(enemy =>
            enemy.Position == command.Target && enemy.CurrentHitPoints > 0);
        currentEnemy ??= ClosestLivingTeamEnemy(battle, GetCasterPosition(character));
        var attempt = TryCastSpell(character, GetCasterPosition(character), spell, inCombat: true,
            currentEnemy, castingItem, command.CastingItemSlotIndex, command.Target);
        if (attempt is null || !attempt.ConsumesTurn)
        {
            RejectTeamBattleAction(command, attempt?.Message ?? "A varázslat célpontja érvénytelen.");
            return;
        }
        battle.RecordSpellCast(character);
        if (IsOffensiveSpell(spell)) battle.RecordAttack(BattleSide.Friendly);
        if (attempt.DamageToCurrentEnemy > 0) currentEnemy.ReceiveSpellDamage(attempt.DamageToCurrentEnemy);
        battle.GrantExtraActions(attempt.ExtraPlayerActions);
        var message = attempt.Message +
                      _battleSystem.FinishCharacterAction(character, battle.RuntimeFor(character));
        PresentBattleEntries([new BattleLogEntry(message, attempt.Kind, attempt.Details)]);
        SynchronizeTeamBattleDefeats(battle, character);
        AdvanceTeamBattleTurn(battle);
    }

    private bool TryUseTeamBattleItem(TeamBattleEncounter battle, LiveCharacter character,
        int backpackIndex, out string message)
    {
        if (backpackIndex is < 0 or >= LiveCharacter.MaximumBackpackItemCount ||
            character.GetInventoryItem(InventorySlotKind.Backpack, backpackIndex) is not MiscItemDefinition item ||
            item.Effect == ConsumableEffect.None || !battle.CanUseItem(character, item))
        {
            message = "A választott hátizsákhelyen nincs használható tárgy.";
            return false;
        }
        var result = item.Id == MiscItemIds.HerbalTea &&
                     (character.WaterLevel < 100 || character.CurrentVitality < character.MaximumVitality)
            ? UseHerbalTea(character, item.EffectValue)
            : IsInitiativeDrink(item) ? UseInitiativeDrink(character, item)
            : item.Effect switch
            {
                ConsumableEffect.Food when character.FoodLevel < 100 => UseFood(character, item.EffectValue),
                ConsumableEffect.Water when character.WaterLevel < 100 => UseWater(character, item.EffectValue),
                ConsumableEffect.Heal when character.CurrentVitality < character.MaximumVitality => UseHealing(character, item.EffectValue),
                ConsumableEffect.RestoreMana when character.UsesMana && character.CurrentMana < character.MaximumMana => UseManaPotion(character, item.EffectValue),
                ConsumableEffect.CurePoison when character.RemoveStatus(CharacterStatusIds.Poisoned) => "a mérgezés megszűnt",
                ConsumableEffect.CureDisease when character.RemoveStatus(CharacterStatusIds.Diseased) => "a betegség megszűnt",
                ConsumableEffect.StopBleeding when character.RemoveStatus(CharacterStatusIds.Bleeding) => "a vérzés elállt",
                ConsumableEffect.Vision => UseVisionItem(character, item),
                _ => string.Empty
            };
        if (string.IsNullOrEmpty(result))
        {
            message = "A tárgy hatására most nincs szükség vagy nem alkalmazható.";
            return false;
        }
        character.RemoveOneInventoryItem(InventorySlotKind.Backpack, backpackIndex);
        character.SynchronizeNeedStatuses(_gameData.GetStatus(CharacterStatusIds.Hungry),
            _gameData.GetStatus(CharacterStatusIds.Thirsty));
        _renderer.RefreshCharacterSheet(PartyLeader);
        message = $"{character.Name} használta: {item.Name} — {result}.";
        PlaySessionSound(item.Effect == ConsumableEffect.Heal ? SoundEffect.DefensiveSpell : SoundEffect.Item,
            [character.Id]);
        return true;
    }

    private void RejectTeamBattleAction(BattleActionCommand command, string message)
    {
        _session.RejectExecutedCommand(command, message);
        _renderer.DrawInventoryMessage(message, ConsoleColor.Red);
        if (_activeTeamBattle is { IsCompleted: false } battle && battle.CurrentCharacter is { } character)
        {
            SetTeamBattlePrompt(battle);
        }
    }

    private void ChooseTeamAiTactic(LiveCharacter character, CharacterBattleChoices runtime)
    {
        var choices = character.CharacterClass.Id == CharacterClassIds.Harcos
            ? new[] { BattleTactic.FighterPrecise, BattleTactic.FighterPowerful, BattleTactic.FighterDefensive }
            : new[] { BattleTactic.ThiefAmbush, BattleTactic.ThiefObserve, BattleTactic.ThiefPoison };
        var tactic = choices[_random.Next(choices.Length)];
        runtime.TryChooseTactic(character, tactic);
        var message = $"{character.Name} harci taktikája: {BattleTacticName(tactic, character)}.";
        if (!_isQuickTeamBattle) _renderer.DrawInventoryMessage(message, ConsoleColor.Cyan);
        RecordSessionActivity(SessionActivityKind.Battle, message, ConsoleColor.Cyan);
    }
}
