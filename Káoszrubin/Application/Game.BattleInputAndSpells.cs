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
    private void HandleLocalBattleInput(ConsoleKeyInfo key)
    {
        if (_activeBattle is { } battle) HandleLocalBattleInput(battle, key);
    }

    private void HandleLocalBattleInput(BattleEncounter battle, ConsoleKeyInfo key)
    {
        if (IsHelpShortcut(key))
        {
            ShowInGameHelp();
            _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
            ContinueBattle();
            return;
        }
        if (IsSaveGameShortcut(key))
        {
            _saveAfterBattle = true;
            _renderer.DrawInventoryMessage("Mentés kérve: a harc lezárása után elkészül.", ConsoleColor.Yellow);
            return;
        }

        // A konzol több gyors billentyűt is pufferelhet, miközben az előző harci
        // parancs még a session sorában vár. Ezek nem vihetők át a következő körre.
        if (_localBattleCommandGate.IsPending) return;

        if (key.Key == ConsoleKey.Escape)
        {
            //TODO: quit to MainMenu instead
            if (ConfirmReturnToMainMenu()) Environment.Exit(0);
        }

        if (battle.PauseReason != BattlePauseReason.None)
        {
            if (key.Key == ConsoleKey.Spacebar)
                SubmitLocalBattleCommand(BattleActionKind.ResumeBattle);

            return;
        }

        if (battle.IsCompleted) return;

        if (battle.CurrentEnemy is not null)
        {
            if (key.Key == ConsoleKey.Spacebar)
                SubmitLocalBattleCommand(BattleActionKind.AdvanceEnemyTurn);
            return;
        }
        if (battle.CurrentCharacter is not { } character || character != PartyLeader) return;
        var enemy = battle.SelectedTargetEnemy() ?? ClosestLivingEnemy(battle, GetCasterPosition(character));
        var allowed = GetAllowedBattleActions(battle, character, enemy);
        if (battle.RuntimeFor(character).RequiresTacticSelection && key.Key is ConsoleKey.D1 or ConsoleKey.NumPad1 or
                ConsoleKey.D2 or ConsoleKey.NumPad2 or ConsoleKey.D3 or ConsoleKey.NumPad3)
        {
            var option = key.Key is ConsoleKey.D1 or ConsoleKey.NumPad1 ? 1 :
                key.Key is ConsoleKey.D2 or ConsoleKey.NumPad2 ? 2 : 3;
            SubmitLocalBattleCommand(TacticActionFor(character.CharacterClass.Id, option));
            return;
        }
        if (key.Key == ConsoleKey.Tab && allowed.Contains(BattleActionKind.SelectTarget) &&
            NextBattleTarget(battle, character, allowed) is { } selectedTarget)
        {
            SubmitLocalBattleCommand(BattleActionKind.SelectTarget, targetEnemyId: selectedTarget.Id);
            return;
        }
        if (key.Key == ConsoleKey.R && allowed.Contains(BattleActionKind.Retreat))
        {
            SubmitLocalBattleCommand(BattleActionKind.Retreat);
            return;
        }
        if ((key.Key == ConsoleKey.P || key.Key == ConsoleKey.Spacebar && IsBattleMovementInProgress(battle)) &&
            allowed.Contains(BattleActionKind.Pass))
        {
            SubmitLocalBattleCommand(BattleActionKind.Pass);
            return;
        }
        if (key.Key == ConsoleKey.Spacebar && allowed.Contains(BattleActionKind.PhysicalAttack))
        {
            var targetEnemy = PreferredActionTarget(battle,
                TargetsForAction(battle, character, BattleActionKind.PhysicalAttack))!;
            SubmitLocalBattleCommand(BattleActionKind.PhysicalAttack, targetEnemyId: targetEnemy.Id);
            return;
        }
        if (key.Key == ConsoleKey.Q && allowed.Contains(BattleActionKind.ShieldBash))
        {
            var targetEnemy = PreferredActionTarget(battle,
                TargetsForAction(battle, character, BattleActionKind.ShieldBash))!;
            SubmitLocalBattleCommand(BattleActionKind.ShieldBash, targetEnemyId: targetEnemy.Id);
            return;
        }
        if (key.Key == ConsoleKey.C && allowed.Contains(BattleActionKind.SwapWeapon))
        {
            SubmitLocalBattleCommand(BattleActionKind.SwapWeapon);
            return;
        }
        if (key.Key == ConsoleKey.H && allowed.Contains(BattleActionKind.SwapToRear))
        {
            SubmitLocalBattleCommand(BattleActionKind.SwapToRear);
            return;
        }
        if (key.Key == ConsoleKey.B && allowed.Contains(BattleActionKind.PrepareRearLeft))
        {
            SubmitLocalBattleCommand(BattleActionKind.PrepareRearLeft);
            return;
        }
        if (key.Key == ConsoleKey.J && allowed.Contains(BattleActionKind.PrepareRearRight))
        {
            SubmitLocalBattleCommand(BattleActionKind.PrepareRearRight);
            return;
        }
        if (TryGetDirection(key.Key, out var formationDirection) &&
            allowed.Contains(BattleActionKind.MoveFormation))
        {
            SubmitLocalBattleCommand(BattleActionKind.MoveFormation,
                target: GetCasterPosition(character) + formationDirection);
            return;
        }
        if (TryGetDirection(key.Key, out var direction) && allowed.Contains(BattleActionKind.Move))
        {
            SubmitLocalBattleCommand(BattleActionKind.Move, target: GetCasterPosition(character) + direction);
            return;
        }
        if (key.Key == ConsoleKey.U && allowed.Contains(BattleActionKind.UseItem))
        {
            var item = SelectBattleItem(battle, character);
            if (item is not null)
                SubmitLocalBattleCommand(BattleActionKind.UseItem, backpackIndex: item.BackpackIndex);
            return;
        }
        if (key.Key == ConsoleKey.T && allowed.Contains(BattleActionKind.TurnUndead))
        {
            var undeadTarget = PreferredTurnUndeadTarget(battle, character);
            if (undeadTarget is null) return;
            SubmitLocalBattleCommand(BattleActionKind.TurnUndead,
                targetEnemyId: undeadTarget.Id);
            return;
        }

        SpellDefinition? spell = null;
        MagicItemDefinition? castingItem = null;
        int? castingItemSlotIndex = null;
        if (key.Key == ConsoleKey.V && allowed.Contains(BattleActionKind.CastSpell))
        {
            var selection = _renderer.DrawSpellCastingScreen([character], 0, inCombat: true, _maze, _fogOfWar,
                _ => GetCasterPosition(character), () => { });
            _renderer.RestoreSpellCastingOverlay();
            spell = selection?.Spell;
            castingItem = selection?.CastingItem;
            castingItemSlotIndex = selection?.CastingItemSlotIndex;
        }
        else if (TryGetQuickSpellIndex(key, out var slotIndex) && allowed.Contains(BattleActionKind.CastSpell))
            spell = character.QuickSpells[slotIndex];
        if (spell is null) return;
        var validation = ValidateSpellCast(character, GetCasterPosition(character), spell, inCombat: true,
            enemy, castingItem, castingItemSlotIndex);
        if (validation is not null)
        {
            _renderer.DrawInventoryMessage(validation.Message, ConsoleColor.Red);
            return;
        }
        var targetPosition = SelectSpellTarget(character, GetCasterPosition(character), spell, enemy);
        if (targetPosition is null) return;
        SubmitLocalBattleCommand(BattleActionKind.CastSpell, spell.Id, castingItemSlotIndex,
            targetPosition, enemy.Id);
    }

    private BattleItemOptionSnapshot? SelectBattleItem(BattleEncounter battle,
        LiveCharacter character)
    {
        var options = GetBattleItemOptions(battle, character).Take(9).ToArray();
        if (options.Length == 0) return null;
        _renderer.DrawInventoryMessage("Tárgyhasználat: " + string.Join(" | ", options.Select((item, index) =>
            $"{index + 1} - {item.Name} ×{item.Quantity}")) + " | Esc - mégse", ConsoleColor.Cyan);
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Escape)
            {
                _renderer.DrawInventoryMessage("Tárgyhasználat megszakítva.", ConsoleColor.DarkYellow);
                return null;
            }
            if (key.KeyChar is >= '1' and <= '9' && key.KeyChar - '1' < options.Length)
                return options[key.KeyChar - '1'];
        }
    }

    private void SubmitLocalBattleCommand(
        BattleActionKind action,
        string? spellId = null,
        int? castingItemSlotIndex = null,
        Position? target = null,
        WorldEntityId? targetEnemyId = null,
        int? backpackIndex = null)
    {
        var battleId = _activeBattle?.Id;
        var turnId = _activeBattle?.Turns.TurnId;
        if (battleId is null || turnId is null) return;

        var commandId = _localCommandId + 1;

        if (!_localBattleCommandGate.TryBegin(commandId)) return;

        var command = new BattleActionCommand(
            _session.HostPlayerId,
            commandId,
            PartyLeader.Id,
            battleId.Value,
            turnId.Value,
            action,
            spellId,
            castingItemSlotIndex,
            target,
            targetEnemyId,
            backpackIndex);

        var submitted = _session.Submit(command);

        if (submitted)
            _localCommandId = commandId;
        else
            _localBattleCommandGate.Complete(commandId);
    }

    private static BattleActionKind TacticActionFor(string characterClassId, int option) =>
        characterClassId == CharacterClassIds.Harcos
            ? option switch
            {
                1 => BattleActionKind.FighterPrecise,
                2 => BattleActionKind.FighterPowerful,
                _ => BattleActionKind.FighterDefensive
            }
            : option switch
            {
                1 => BattleActionKind.ThiefAmbush,
                2 => BattleActionKind.ThiefObserve,
                _ => BattleActionKind.ThiefPoison
            };

    private void ExecuteBattleAction(BattleActionCommand command)
    {
        if (command.SenderId == _session.HostPlayerId)
            _localBattleCommandGate.Complete(command.CommandId);
        if (_activeBattle is { } battle) ExecuteBattleAction(battle, command);
    }

    private LiveCharacter? TryRollKnightProtector(LiveCharacter protectedCharacter) =>
        _battleActionCoordinator.TryRollKnightProtector(protectedCharacter, GetCasterPosition(protectedCharacter),
            LivingPartyWithPositions());

    private static BattleTactic ToBattleTactic(BattleActionKind action) => BattleActionCoordinator.ToBattleTactic(action);

    private static string BattleTacticName(BattleTactic tactic, LiveCharacter character) =>
        BattleActionCoordinator.BattleTacticName(tactic, character);

    private IReadOnlyList<BattleSpellOption> GetSpellOptions(LiveCharacter character,
        Position characterPosition, Enemy? enemy, bool inCombat) =>
        _battleActionCoordinator.GetSpellOptions(character, characterPosition, enemy, inCombat,
            (c, pos, sp, en) => HasValidSpellTarget(c, pos, sp, en),
            (pos, sp, en) => GetValidSpellTargets(pos, sp, en),
            (pos, sp, en) => GetInvalidSpellTargetIssues(pos, sp, en));

    private void ExecuteExplorationSpell(CastExplorationSpellCommand command)
    {
        var character = CharacterRoster.Party.Members.FirstOrDefault(member => member.Id == command.CharacterId);
        if (character is null || !character.IsAlive) return;
        var spell = _gameData.Spells.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, command.SpellId, StringComparison.OrdinalIgnoreCase));
        if (spell is null)
        {
            _session.RejectExecutedCommand(command, "Ismeretlen varázslat.");
            return;
        }
        MagicItemDefinition? castingItem = null;
        if (command.CastingItemSlotIndex is { } slot)
            castingItem = character.MagicItems.ElementAtOrDefault(slot);
        var result = TryCastSpell(character, GetCasterPosition(character), spell, inCombat: false,
            currentEnemy: null, castingItem: castingItem,
            castingItemSlotIndex: command.CastingItemSlotIndex, explicitTarget: command.Target);
        if (result is null || !result.ConsumesTurn)
        {
            _session.RejectExecutedCommand(command, result?.Message ?? "A varázslat célpontja érvénytelen.");
            return;
        }
        _renderer.RefreshCharacterSheet(PartyLeader);
        _renderer.DrawInventoryMessage(result.Message,
            result.Kind == BattleLogKind.Information ? ConsoleColor.Red : ConsoleColor.Magenta);
        RecordSessionActivity(SessionActivityKind.Spell, result.Message,
            result.Kind == BattleLogKind.Information ? ConsoleColor.Red : ConsoleColor.Magenta);
    }

    private bool HasUsableCombatSpell(LiveCharacter character, Position characterPosition, Enemy enemy) =>
        _battleActionCoordinator.HasUsableCombatSpell(character, characterPosition, enemy,
            _timeStopUsedThisBattle, EquippedCastingItems(character),
            (c, pos, sp, en) => HasValidSpellTarget(c, pos, sp, en));

    private bool IsTurnUndeadReady(LiveCharacter character) =>
        BattleActionCoordinator.IsTurnUndeadReady(character, _activeBattle?.Turns.Cycle ?? 1,
            _turnUndeadNextAvailableRounds);

    private BattlePlayerAction ResolveTurnUndead(LiveCharacter character, Enemy enemy) =>
        _battleActionCoordinator.ResolveTurnUndead(character, enemy, GetCasterPosition(character),
            _activeBattle?.Turns.Cycle ?? 1, _turnUndeadNextAvailableRounds);

    private SpellCastAttempt? TryCastSpell(LiveCharacter caster, Position casterPosition, SpellDefinition spell,
        bool inCombat, Enemy? currentEnemy, MagicItemDefinition? castingItem = null, int? castingItemSlotIndex = null,
        Position? explicitTarget = null)
    {
        var validation = ValidateSpellCast(caster, casterPosition, spell, inCombat, currentEnemy, castingItem,
            castingItemSlotIndex, explicitTarget);
        if (validation is not null) return validation;
        var usingItem = castingItem is not null;
        var castingItemIndex = usingItem ? castingItemSlotIndex ?? -1 : -1;
        var manaCost = usingItem ? 0 : SpellcastingRules.EffectiveManaCost(caster, spell);
        var target = explicitTarget ?? SelectSpellTarget(caster, casterPosition, spell, currentEnemy);
        if (target is null) return null;
        var divineJudgment = !usingItem && caster.RecordDivineSpellCast(spell);
        if (usingItem)
        {
            caster.ConsumeMagicItemCharge(castingItemIndex);
            _renderer.RefreshCharacterSheet(PartyLeader);
        }
        else caster.SpendMana(manaCost);
        _renderer.CharacterSheet.RefreshBattleStatusRows();

        if (inCombat)
        {
            var engaged = _activeBattle?.IsEngaged(caster) == true;
            var failureChance = SpellcastingRules.CombatFailureChance(caster, engaged);
            var roll = _random.Next(1, 101);
            if (roll <= failureChance)
                return new SpellCastAttempt(true,
                    $"{caster.Name} varázslata meghiúsul: {spell.Name} — kockázat {failureChance}%, dobás {roll}. " +
                    (engaged ? "Lekötés: +15%. " : string.Empty) +
                    (usingItem ? $"{CastingItemUseText(castingItem!)}; az akció elveszett." : $"-{manaCost} manna; az akció elveszett."),
                    BattleLogKind.Information);
        }

        if (IsOffensiveSpell(spell)) caster.BreakSanctuary();
        var spellListeners = ResolveCharacterSpellTargets(caster, spell, target.Value)
            .Select(character => character.Id)
            .Append(caster.Id)
            .Concat(inCombat ? [PartyLeader.Id] : [])
            .Distinct()
            .ToArray();
        PlaySessionSound(IsOffensiveSpell(spell) ? SoundEffect.OffensiveSpell : SoundEffect.DefensiveSpell,
            spellListeners);
        var targetText = DescribeSpellTarget(caster, spell, target.Value, currentEnemy);
        var execution = ExecuteSpell(caster, casterPosition, spell, target.Value, inCombat, currentEnemy, divineJudgment);
        var judgmentText = divineJudgment ? " ⚡ Isteni ítélet: kétszeres számszerű hatás és ingyenes varázslat." : string.Empty;
        return new SpellCastAttempt(true,
            $"{caster.Name} elsüti: {spell.Name} → {targetText}. " +
            (usingItem ? $"{CastingItemUseText(castingItem!)}; 0 manna." : $"-{manaCost} manna.") +
            $"{judgmentText} {execution.Summary}",
            BattleLogKind.PlayerAttack, execution.DamageToCurrentEnemy, execution.ExtraPlayerActions,
            execution.Details is { } detail ? detail with
            {
                Calculation = detail.Calculation.Prepend(usingItem ? "🔷 Tárgyhasználat: 0 manna" : $"🔷 Mannaköltség: {manaCost}").ToArray()
            } : null);
    }

    private IEnumerable<(LiveCharacter Character, Position Position)> LivingPartyWithPositions()
    {
        if (PartyLeader.IsAlive && _player is not null) yield return (PartyLeader, _player.Position);
        if (_maze is not null)
        {
            foreach (var member in _maze.PartyMembers.Where(member => member.Character.IsAlive))
                yield return (member.Character, member.Position);
        }
    }

    private SpellCastAttempt? ValidateSpellCast(LiveCharacter caster, Position casterPosition, SpellDefinition spell,
        bool inCombat, Enemy? currentEnemy, MagicItemDefinition? castingItem = null, int? castingItemSlotIndex = null,
        Position? explicitTarget = null) =>
        _spellExecutionService.ValidateSpellCast(caster, casterPosition, spell, inCombat, currentEnemy, castingItem,
            castingItemSlotIndex, explicitTarget, _timeStopUsedThisBattle, LivingPartyWithPositions().ToArray(),
            _maze, _fogOfWar, _player?.Position, PartyLeader);

    private bool IsValidExplicitSpellTarget(LiveCharacter caster, Position casterPosition, SpellDefinition spell,
        Position target, Enemy? currentEnemy) =>
        _spellExecutionService.IsValidExplicitSpellTarget(caster, casterPosition, spell, target, currentEnemy,
            LivingPartyWithPositions().ToArray(), _maze, _fogOfWar, _player?.Position, PartyLeader);

    private static string CastingItemUseText(MagicItemDefinition item) => SpellExecutionService.CastingItemUseText(item);

    private SpellExecutionResult ExecuteSpell(LiveCharacter caster, Position casterPosition, SpellDefinition spell, Position target, bool inCombat,
        Enemy? currentEnemy, bool divineJudgment) =>
        _spellExecutionService.ExecuteSpell(caster, casterPosition, spell, target, inCombat, currentEnemy, divineJudgment,
            ref _timeStopUsedThisBattle, LivingPartyWithPositions().ToArray(), _maze,
            ApplyExplorationSpellDamage, TeleportLeader, TeleportLivingParty, ResurrectPartyMember,
            c => _renderer.RefreshCharacterSheet(c),
            targets => PlaySpellImpact(spell, casterPosition, target, targets));

    private void PlaySpellImpact(SpellDefinition spell, Position casterPosition, Position target,
        IReadOnlyList<Position> enemyTargets) =>
        _renderer.PlaySpellImpact(_maze, _fogOfWar, _player.Position, spell, casterPosition, target, enemyTargets);

    private void ShiftExplorationSchedules(TimeSpan pause)
    {
        _nextNeedsDrain += pause;
        if (_nextExplorationStatusTickUtc != DateTime.MinValue &&
            _nextExplorationStatusTickUtc != DateTime.MaxValue)
            _nextExplorationStatusTickUtc += pause;
        _nextNpcSelfCareCheck += pause;
        _nextAdHocConversationCheckUtc += pause;
        if (_lastAdHocConversationUtc != DateTime.MinValue) _lastAdHocConversationUtc += pause;
        if (_nextEnemyActionUtc != DateTime.MaxValue) _nextEnemyActionUtc += pause;
        ShiftDeadlines(_nextEnemyMoves, pause);
        ShiftDeadlines(_nextPartyMoves, pause);
        ShiftDeadlines(_nextControlledMoves, pause);
        ShiftDeadlines(_nextNpcComplaints, pause);
        _partyScatterUntil += pause;
        _partyCommandState = _partyCommandState with { ScatterUntil = _partyCommandState.ScatterUntil + pause };
    }

    private static void ShiftDeadlines<TKey>(Dictionary<TKey, DateTime> deadlines, TimeSpan pause) where TKey : notnull
    {
        foreach (var key in deadlines.Keys.ToArray())
            if (deadlines[key] != DateTime.MaxValue && deadlines[key] != DateTime.MinValue)
                deadlines[key] += pause;
    }

    private IEnumerable<Enemy> ResolveEnemySpellTargets(SpellDefinition spell, Position target, Enemy? currentEnemy, Position casterPosition) =>
        _spellExecutionService.ResolveEnemySpellTargets(spell, target, currentEnemy, casterPosition, _maze);

    private IEnumerable<LiveCharacter> ResolveCharacterSpellTargets(LiveCharacter caster, SpellDefinition spell, Position target) =>
        _spellExecutionService.ResolveCharacterSpellTargets(caster, spell, target, LivingPartyWithPositions().ToArray(), _maze);

    private bool IsOffensiveSpell(SpellDefinition spell) => _spellExecutionService.IsOffensiveSpell(spell);

    private static bool IsUnholy(EnemyDefinition enemy) => SpellExecutionService.IsUnholy(enemy);

    private string ResurrectPartyMember(Position target, SpellEffectDefinition effect)
    {
        var corpse = _maze.Corpses.OfType<PartyMemberCorpse>().FirstOrDefault(candidate => candidate.Position == target);
        if (corpse is null) return "nincs feltámasztható társ a célmezőn";
        if (corpse.Character.WasResurrectedThisLevel) return $"{corpse.Character.Name} ezen a pályán már visszatért egyszer";
        var revivalPosition = FindResurrectionPosition(corpse);
        if (revivalPosition is null) return "a tetem körül nincs szabad hely a visszatéréshez";

        var parameters = SpellExecutionService.ParseEffectParameters(effect.Parameter);
        var manaPercent = parameters.Count > 0 && int.TryParse(parameters[0], out var parsedMana)
            ? Math.Clamp(parsedMana, 0, 100)
            : 0;
        foreach (var statusId in parameters.Skip(1)) corpse.Character.RemoveStatus(statusId);
        corpse.Character.ClearTemporarySpellEffects();
        corpse.Character.SetCurrentResources(
            Math.Max(1, corpse.Character.MaximumVitality * Math.Clamp(effect.Value, 1, 100) / 100),
            corpse.Character.MaximumMana * manaPercent / 100);
        corpse.Character.MarkResurrectedThisLevel();
        _maze.RemoveCorpse(corpse);
        var avatar = new PartyMemberAvatar(revivalPosition.Value, corpse.Character);
        _maze.AddPartyMember(avatar);
        ScheduleNextPartyMove(avatar, DateTime.UtcNow);
        RevealFor(avatar.Character, avatar.Position);
        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
        _renderer.RefreshCharacterSheet(PartyLeader);
        return $"✨ {corpse.Character.Name} visszatért {corpse.Character.CurrentVitality} HP-val" +
               (corpse.Character.UsesMana ? $" és {corpse.Character.CurrentMana} mannával" : string.Empty);
    }

    private Position? FindResurrectionPosition(PartyMemberCorpse corpse) =>
        SpellExecutionService.FindResurrectionPosition(_maze, _player?.Position, corpse);

    private void ApplyExplorationSpellDamage(LiveCharacter caster, Enemy enemy, int amount, List<string> notes)
    {
        enemy.ReceiveSpellDamage(amount);
        if (enemy.CurrentHitPoints > 0) return;
        PlaySessionSound(SoundEffect.MonsterKilledBySpell);
        RegisterNpcQuestKill(enemy);
        caster.RecordMonsterKill(enemy.Definition.Id);
        _maze.ReplaceEnemyWithCorpse(enemy);
        _nextEnemyMoves.Remove(enemy);
        var awards = DistributeExperience(caster, enemy.Definition.ExperienceReward, isQuest: false);
        notes.Add($"☠ {enemy.Name} elpusztult; {FormatExperienceAwards(awards)}");
        _renderer.DrawMapCellAfterBattle(_maze, _fogOfWar, enemy.Position, _player.Position);
        var leveledAwards = awards.Where(award => award.Result.LeveledUp && award.Character.IsAlive).ToList();
        if (leveledAwards.Count == 0) return;
        if (_battleStarted)
            _pendingLevelUps.AddRange(leveledAwards.Select(award => (award.Character, award.Result)));
        else
        {
            foreach (var award in leveledAwards) ResolvePerkOffers(award.Character, award.Result);
            _renderer.RefreshCharacterSheet(PartyLeader);
        }
    }

    private bool TeleportLeader(Position target, bool inCombat)
    {
        if (!_maze.IsWalkable(target) || _maze.GetObjectAt(target) is not null) return false;
        _player.TeleportTo(target);
        _leaderTrail.Clear();
        _leaderTrail.Add(target);
        RevealFor(PartyLeader, target);
        if (!inCombat) _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, target);
        return true;
    }

    private string TeleportLivingParty(Position target, bool inCombat)
    {
        if (!TeleportLeader(target, inCombat)) return "a dimenziókapu célmezője nem szabad";
        var positions = SpellExecutionService.FindNearbyTeleportPositions(_maze, _player?.Position, target).Take(_maze.PartyMembers.Count).ToList();
        var moved = 0;
        foreach (var pair in _maze.PartyMembers.Zip(positions))
        {
            pair.First.MoveTo(pair.Second);
            RevealFor(pair.First.Character, pair.Second);
            moved++;
        }
        if (!inCombat) _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, target);
        return $"dimenziókapu: a vezér és {moved} társ átkerült";
    }

    private Position? SelectSpellTarget(LiveCharacter caster, Position casterPosition, SpellDefinition spell, Enemy? currentEnemy)
    {
        if (spell.TargetType is SpellTargetType.Self or SpellTargetType.Party) return casterPosition;
        var candidates = GetValidSpellTargets(casterPosition, spell, currentEnemy).Distinct().ToList();
        var forward = DirectionOffset(_leaderFacing);
        var fallback = new Position(
            Math.Clamp(casterPosition.X + forward.X, 0, _maze.Width - 1),
            Math.Clamp(casterPosition.Y + forward.Y, 0, _maze.Height - 1));
        var cursor = candidates.OrderBy(position => Chebyshev(position, casterPosition)).FirstOrDefault(fallback);
        Position? previous = null;

        while (true)
        {
            var validation = ValidateSpellTarget(casterPosition, spell, cursor, currentEnemy);
            var valid = validation.IsValid;
            var prompt = $"╳ {spell.Name} — {ConsoleRenderer.SpellTargetName(spell.TargetType)}, táv {spell.Range}" +
                         (spell.AreaRadius > 0 ? $", sugár {spell.AreaRadius}" : string.Empty) +
                         $" | {(valid ? DescribeSpellTarget(caster, spell, cursor, currentEnemy) :
                             $"érvénytelen cél: {validation.InvalidReason}")} | Enter: célzás, Tab: következő, Esc: mégse";
            _renderer.DrawSpellTargetCursor(_maze, _fogOfWar, previous, cursor, valid, prompt);
            previous = cursor;
            var key = Console.ReadKey(intercept: true);
            if (IsHelpShortcut(key))
            {
                ShowInGameHelp();
                if (currentEnemy is not null) _renderer.DrawBattleStarted(currentEnemy);
                previous = null;
                continue;
            }
            if (key.Key == ConsoleKey.Escape)
            {
                _renderer.FinishSpellTargeting(_maze, _fogOfWar, _player.Position);
                return null;
            }
            if (key.Key == ConsoleKey.Enter && valid)
            {
                _renderer.FinishSpellTargeting(_maze, _fogOfWar, _player.Position);
                return cursor;
            }
            if (key.Key == ConsoleKey.Tab && candidates.Count > 0)
            {
                var index = candidates.IndexOf(cursor);
                cursor = candidates[(index + 1 + candidates.Count) % candidates.Count];
                continue;
            }
            if (!TryGetDirection(key.Key, out var direction)) continue;
            cursor = spell.TargetType == SpellTargetType.Direction
                ? casterPosition + direction
                : cursor + direction;
            if (!_maze.IsInside(cursor)) cursor = previous.Value;
        }
    }

    private IEnumerable<Position> GetValidSpellTargets(Position casterPosition, SpellDefinition spell, Enemy? currentEnemy) =>
        _spellExecutionService.GetValidSpellTargets(casterPosition, spell, currentEnemy, _maze, _fogOfWar, _player?.Position, PartyLeader);

    private bool IsValidSpellTarget(Position casterPosition, SpellDefinition spell, Position position, Enemy? currentEnemy) =>
        _spellExecutionService.IsValidSpellTarget(casterPosition, spell, position, currentEnemy, _maze, _fogOfWar, _player?.Position, PartyLeader);

    private SpellTargetValidation ValidateSpellTarget(Position casterPosition, SpellDefinition spell,
        Position position, Enemy? currentEnemy) =>
        _spellExecutionService.ValidateSpellTarget(casterPosition, spell, position, currentEnemy, _maze,
            _fogOfWar, _player?.Position, PartyLeader);

    private IReadOnlyList<SpellTargetIssue> GetInvalidSpellTargetIssues(Position casterPosition,
        SpellDefinition spell, Enemy? currentEnemy) =>
        _spellExecutionService.GetInvalidSpellTargetIssues(casterPosition, spell, currentEnemy, _maze,
            _fogOfWar, _player?.Position, PartyLeader);

    private bool HasValidSpellTarget(LiveCharacter caster, Position casterPosition, SpellDefinition spell, Enemy? currentEnemy) =>
        _spellExecutionService.HasValidSpellTarget(caster, casterPosition, spell, currentEnemy, LivingPartyWithPositions().ToArray(), _maze, _fogOfWar, _player?.Position, PartyLeader);

    private bool CanAffectCharacter(SpellDefinition spell, LiveCharacter character) =>
        _spellExecutionService.CanAffectCharacter(spell, character);

    private string DescribeSpellTarget(LiveCharacter caster, SpellDefinition spell, Position position, Enemy? currentEnemy) =>
        _spellExecutionService.DescribeSpellTarget(caster, spell, position, currentEnemy, _maze, _player?.Position, PartyLeader);

    private static string DirectionName(Position origin, Position position) => position.X < origin.X ? "bal" :
        position.X > origin.X ? "jobb" : position.Y < origin.Y ? "fel" : "le";

    private static int Chebyshev(Position first, Position second) =>
        Math.Max(Math.Abs(first.X - second.X), Math.Abs(first.Y - second.Y));

    private IReadOnlyList<Position> RevealFor(LiveCharacter character, Position position,
        bool advanceEnemyMemory = false)
    {
        var exitWasRevealed = IsLevelExitDiscovered();
        var sources = LivingPartyWithPositions().Select(entry => new PartyPerceptionSource(entry.Position,
            CharacterClassRules.VisionRange(entry.Character, CurrentLevelVisionModifier),
            CharacterClassRules.HearingRange(entry.Character),
            CharacterClassRules.DetectionBonus(entry.Character))).ToArray();
        var revealed = _fogOfWar.UpdatePartyVisibility(_maze, sources, advanceEnemyMemory);
        if (IsLevelExitDiscovered())
        {
            _backgroundMusic.MarkExitDiscovered();
            if (!exitWasRevealed) ProcessQuestProgressChanges(_questManager.RegisterLocationDiscovered(QuestLocation.Exit));
        }
        return revealed;
    }

    private HashSet<Position> CurrentIlluminatedWallPositions()
    {
        const string lightSpellId = "S026";
        var sources = LivingPartyWithPositions()
            .Where(entry => entry.Character.IsAlive && entry.Character.ActiveSpellEffects.Any(effect =>
                effect.Type == ActiveSpellEffectType.VisionBonus && effect.Value > 0 &&
                effect.SourceSpellId is lightSpellId or MiscItemIds.Torch))
            .Select(entry => (entry.Position,
                Range: CharacterClassRules.VisionRange(entry.Character, CurrentLevelVisionModifier)))
            .ToArray();
        if (sources.Length == 0) return [];

        var result = new HashSet<Position>();
        foreach (var source in sources)
        {
            var minimumX = Math.Max(0, source.Position.X - source.Range);
            var maximumX = Math.Min(_maze.Width - 1, source.Position.X + source.Range);
            var minimumY = Math.Max(0, source.Position.Y - source.Range);
            var maximumY = Math.Min(_maze.Height - 1, source.Position.Y + source.Range);
            for (var y = minimumY; y <= maximumY; y++)
            for (var x = minimumX; x <= maximumX; x++)
            {
                var position = new Position(x, y);
                if (_maze.Tiles[x, y] != _maze.WallRune || result.Contains(position)) continue;
                if (FogOfWar.CanSee(_maze, source.Position, position, source.Range)) result.Add(position);
            }
        }
        return result;
    }

    private MazeLevelConfiguration CurrentLevelConfiguration => _locationKind == AdventureLocationKind.Quest
        ? QuestLocationConfigurations.Get(_locationId)
        : MazeLevelConfigurations.Get(_mazeLevel);

    private int CurrentLevelVisionModifier => CurrentLevelConfiguration.VisionModifier;

    private void CheckBossDiscoveryAt(IEnumerable<Position> positions, LiveCharacter discoverer)
    {
        var revealed = positions.ToHashSet();
        if (revealed.Count > 0)
        {
            var newlySpottedChests = _maze.TreasureChests
                .Where(chest => revealed.Contains(chest.Position) && _spottedChestIds.Add(chest.Id)).ToArray();
            if (newlySpottedChests.Length > 0)
                TryLogPartyComments(PartySituationIds.TreasureChestFound);
        }
        CheckBossDiscovery(_maze.Enemies, discoverer);
    }

    private void CarryPersistentTemporaryFollowers()
    {
        _temporaryFollowersEnteringNextMaze.Clear();
        foreach (var avatar in _maze.PartyMembers.Where(member => member.TemporaryFollower is { } follower &&
                     (string.Equals(follower.StoryId, RodericStoryId, StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(follower.StoryId, EliraStoryId, StringComparison.OrdinalIgnoreCase))).ToArray())
        {
            _temporaryFollowersEnteringNextMaze.Add(avatar.TemporaryFollower!);
            _maze.RemovePartyMember(avatar);
            _nextPartyMoves.Remove(avatar);
        }
    }

    private IReadOnlyList<LiveCharacter> GetSpecialInnRecruitCandidates()
    {
        var candidates = new List<LiveCharacter>();
        if (_eliraWaitingAtInn is not null && _eliraInnVisitsRemaining <= 0)
        {
            AbandonActiveQuestsFromNpc("NPC020");
            CharacterRoster.Remove(_eliraWaitingAtInn);
            _eliraWaitingAtInn = null;
        }
        if (_eliraWaitingAtInn is not null)
        {
            _eliraInnVisitsRemaining--;
            candidates.Add(_eliraWaitingAtInn);
        }

        foreach (var expired in _waitingDismissedCompanions
                     .Where(waiting => waiting.InnVisitsRemaining <= 0).ToArray())
        {
            CharacterRoster.Remove(expired.Character);
            _waitingDismissedCompanions.Remove(expired);
        }
        foreach (var waiting in _waitingDismissedCompanions.ToArray())
        {
            candidates.Add(waiting.Character);
            var index = _waitingDismissedCompanions.IndexOf(waiting);
            _waitingDismissedCompanions[index] = waiting with
            {
                InnVisitsRemaining = waiting.InnVisitsRemaining - 1
            };
        }
        return candidates;
    }

    private int? SpecialInnRecruitmentPrice(LiveCharacter recruit, int completedLevel) =>
        _waitingDismissedCompanions.Any(waiting => ReferenceEquals(waiting.Character, recruit))
            ? RecruitmentRules.StandardPrice(recruit.Level)
            : null;

    private void SpecialInnRecruitAccepted(LiveCharacter recruit)
    {
        if (ReferenceEquals(recruit, _eliraWaitingAtInn))
        {
            _eliraWaitingAtInn = null;
            _eliraInnVisitsRemaining = 0;
        }
        _waitingDismissedCompanions.RemoveAll(waiting => ReferenceEquals(waiting.Character, recruit));
    }

    private void PlaceCarriedTemporaryFollowersNear(Position leaderPosition)
    {
        if (_temporaryFollowersEnteringNextMaze.Count == 0) return;
        var positions = FindNearbyFreePositions(leaderPosition).Take(_temporaryFollowersEnteringNextMaze.Count).ToArray();
        for (var index = 0; index < Math.Min(positions.Length, _temporaryFollowersEnteringNextMaze.Count); index++)
        {
            var follower = _temporaryFollowersEnteringNextMaze[index];
            follower.MoveTo(positions[index]);
            var avatar = new PartyMemberAvatar(positions[index], follower.Character, follower);
            _maze.AddPartyMember(avatar);
            _nextPartyMoves[avatar] = DateTime.UtcNow;
        }
        _temporaryFollowersEnteringNextMaze.Clear();
    }

    private void CheckBossDiscovery(IEnumerable<Enemy> enemies, LiveCharacter? discoverer = null)
    {
        var visibleEnemies = enemies.Where(enemy => _fogOfWar.IsEnemyVisible(enemy.Id, enemy.Position))
            .DistinctBy(enemy => enemy.Id).ToList();
        var newlySpotted = visibleEnemies.Where(enemy => _spottedEnemyIds.Add(enemy.Id)).ToList();
        if (newlySpotted.Any(enemy => enemy.Definition.Id == MonsterIds.ÉlőholtPátriárka) &&
            FindRodericFollower() is { StoryStateId: "FOLLOWING" } &&
            _seenBossIds.Add(MonsterIds.ÉlőholtPátriárka))
        {
            _renderer.DrawInventoryMessage(
                "⚜ Roderic: A pátriárkák... már ők is élőholtak. Szabadítsuk meg őket ettől a gyalázattól!",
                ConsoleColor.Cyan);
        }
        if (newlySpotted.Count > 0)
        {
            PlaySessionSound(SoundEffect.MonsterSpotted);
            TryReportEnemyAlertness(discoverer, newlySpotted);
            TryLogPartyComments(PartySituationIds.EnemySpotted);
        }
        var discovered = visibleEnemies.Where(enemy =>
                (enemy.Definition.IsBoss || enemy.Definition.Rank == EnemyRank.MiniBoss) &&
                !_seenBossIds.Contains(enemy.Definition.Id))
            .DistinctBy(enemy => enemy.Definition.Id, StringComparer.OrdinalIgnoreCase).ToList();
        if (discovered.Count == 0) return;
        foreach (var boss in discovered)
        {
            _seenBossIds.Add(boss.Definition.Id);
            if (string.Equals(boss.Definition.Id, MonsterIds.SirMalrec, StringComparison.OrdinalIgnoreCase) &&
                FindRodericFollower() is { } roderic &&
                string.Equals(roderic.StoryStateId, "MALREC_APPROACH", StringComparison.OrdinalIgnoreCase))
            {
                StageRodericForMalrecEncounter(boss, roderic);
                RunStoryConversation(roderic);
                continue;
            }
            var narrative = StoryNarratives.BossNarratives.GetValueOrDefault(boss.Definition.Id)
                ?? new BossNarrative("Ismeretlen fejezet",
                    [$"Én vagyok {boss.Name}. E folyosók titkait nem osztom meg veletek."]);
            var isMiniBoss = boss.Definition.Rank == EnemyRank.MiniBoss;
            ShowSynchronizedNarrative(NarrativeKind.BossIntroduction,
                isMiniBoss ? "MINIBOSS KÖZELEG" : "BOSS KÖZELEG",
                narrative.ChapterTitle, narrative.Speech,
                new BossPresentationSnapshot(boss.Name, boss.Definition.Appearance,
                    boss.Definition.StrengthTier, isMiniBoss ? "⚔ Nincs aranykulcs" : "🔑 Aranykulcs",
                    boss.BossTier));
        }
    }

    private void StageRodericForMalrecEncounter(Enemy malrec, WorldNpc roderic)
    {
        var avatar = _maze.PartyMembers.FirstOrDefault(member => member.TemporaryFollower == roderic);
        if (avatar is null || Manhattan(avatar.Position, malrec.Position) <= 3) return;
        var candidates = FindNearbyFreePositions(malrec.Position)
            .Where(position => _maze.GetTrapAt(position) is null && _maze.GetDoorAt(position) is null)
            .Take(24)
            .ToArray();
        var destination = candidates
            .Where(position => Manhattan(position, malrec.Position) is >= 2 and <= 3)
            .OrderBy(position => Manhattan(position, _player.Position))
            .Select(position => (Position?)position)
            .FirstOrDefault();
        destination ??= candidates.Select(position => (Position?)position).FirstOrDefault();
        if (destination is null) return;
        avatar.MoveTo(destination.Value);
        _nextPartyMoves[avatar] = DateTime.UtcNow;
    }

    private void TryReportEnemyAlertness(LiveCharacter? discoverer, IReadOnlyCollection<Enemy> newlySpotted)
    {
        if (discoverer is null) return;
        var chance = Math.Clamp(50 + CharacterClassRules.DetectionBonus(discoverer) * 10, 0, 100);
        if (_random.Next(1, 101) > chance) return;

        var observations = newlySpotted
            .GroupBy(enemy => (enemy.Name, enemy.Alertness))
            .Select(group => $"{(group.Count() > 1 ? $"{group.Count()}× " : string.Empty)}{group.Key.Name} — " +
                             EnemyAlertnessName(group.Key.Alertness));
        _sessionEventService.LogPartyComment(discoverer,
            $"Felderítés: {string.Join("; ", observations)}.");
    }

    private static string EnemyAlertnessName(EnemyAlertness alertness) => alertness switch
    {
        EnemyAlertness.Sleeping => "alszik",
        EnemyAlertness.Drowsy => "álmos",
        _ => "éber"
    };
}
