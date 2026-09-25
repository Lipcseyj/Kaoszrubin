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
    private void ExecuteAiCharacterTurn(BattleEncounter battle, LiveCharacter character)
    {
        var combatantId = CombatantId.ForCharacter(character.Id);
        var offensiveActionsBlocked = battle.AreOffensiveActionsBlocked(combatantId);
        var rearPreparationOrdered = battle.ShouldPrioritizeRearSelfBuff(character);
        if (rearPreparationOrdered && character.CurrentVitality < character.MaximumVitality &&
            TryExecuteAiHealingPotion(battle, character, chancePercent: 100, allowedWaste: 15))
            return;
        if (!battle.IsCharacterStaggered(character) && battle.HasProtectiveFormation && battle.IsFrontRow(character) &&
            character.CurrentVitality * 3 <= character.MaximumVitality &&
            battle.RearPartnerOf(character) is { IsAlive: true } rearPartner &&
            !battle.IsCharacterStaggered(rearPartner) &&
            TryExecuteSwapToRear(battle, character, out _))
            return;
        if (!offensiveActionsBlocked && TryExecuteAiTurnUndead(battle, character)) return;
        if (!offensiveActionsBlocked)
        {
            EnsureNpcOffensiveSpellPlan(battle, character);
            if (TryExecuteAiSpell(battle, character)) return;
        }
        var hasAdjacentEnemy = AdjacentEnemies(battle, character).Any();
        var attemptedUrgentPotion = character.CurrentVitality * 2 < character.MaximumVitality &&
                                    !hasAdjacentEnemy;
        if (attemptedUrgentPotion &&
            TryExecuteAiHealingPotion(battle, character, chancePercent: 60, allowedWaste: 0))
            return;
        if (TryExecuteAiReserveWeaponSwap(battle, character)) return;
        if (offensiveActionsBlocked)
        {
            if (!attemptedUrgentPotion && character.CurrentVitality < character.MaximumVitality &&
                TryExecuteAiHealingPotion(battle, character, chancePercent: 30, allowedWaste: 0))
                return;
            var statusText = _battleSystem.FinishCharacterAction(character, battle.RuntimeFor(character));
            PresentBattleEntries([new BattleLogEntry(
                $"💫 {character.Name} megingása miatt nem tud támadni vagy varázsolni.{statusText}",
                BattleLogKind.Information)]);
            AdvanceBattleTurn(battle);
            return;
        }
        if (!battle.IsCharacterStaggered(character) && !battle.HasActiveFormation && !battle.IsEngaged(character) &&
            TryExecuteNpcSpellcasterPositioning(battle, character)) return;
        var reachable = ReachableEnemies(battle, character).FirstOrDefault();
        if (reachable is not null)
        {
            ResolveCharacterAttack(battle, character, reachable);
            return;
        }
        if (battle.IsEngaged(character))
        {
            PresentBattleEntries([new BattleLogEntry($"{character.Name} le van kötve, ezért nem tud mozogni.",
                BattleLogKind.Information)]);
            AdvanceBattleTurn(battle);
            return;
        }
        if (!attemptedUrgentPotion && character.CurrentVitality < character.MaximumVitality &&
            TryExecuteAiHealingPotion(battle, character, chancePercent: 30, allowedWaste: 0))
            return;
        if (battle.IsCharacterStaggered(character))
        {
            var statusText = _battleSystem.FinishCharacterAction(character, battle.RuntimeFor(character));
            PresentBattleEntries([new BattleLogEntry(
                $"💫 {character.Name} megingott állapotban van, ezért nem tud közeledni.{statusText}",
                BattleLogKind.Information)]);
            AdvanceBattleTurn(battle);
            return;
        }
        if (battle.HasActiveFormation && battle.FormationSlotFor(character) is not null)
        {
            var statusText = _battleSystem.FinishCharacterAction(character, battle.RuntimeFor(character));
            PresentBattleEntries([new BattleLogEntry(
                $"{character.Name} tartja a helyét az alakzatban.{statusText}", BattleLogKind.Information)]);
            AdvanceBattleTurn(battle);
            return;
        }
        var target = ClosestLivingEnemy(battle, GetCasterPosition(character));
        MoveCharacterToward(battle, character, target.Position, character.AttackWeapon);
    }

    private bool TryExecuteAiHealingPotion(BattleEncounter battle, LiveCharacter character,
        int chancePercent, int allowedWaste)
    {
        var backpackIndex = TacticalBattleCoordinator.ChooseNpcHealingPotionIndex(
            battle, character, allowedWaste);
        if (backpackIndex is null || _random.Next(100) >= chancePercent ||
            !TryUseBattleItem(battle, character, backpackIndex.Value, out var itemMessage))
            return false;
        itemMessage += _battleSystem.FinishCharacterAction(character, battle.RuntimeFor(character));
        PresentBattleEntries([new BattleLogEntry(itemMessage, BattleLogKind.Information)]);
        AdvanceBattleTurn(battle);
        return true;
    }

    private bool TryExecuteAiReserveWeaponSwap(BattleEncounter battle, LiveCharacter character)
    {
        var currentWeapon = character.AttackWeapon;
        var reserveWeapon = character.GetInventoryItem(InventorySlotKind.Weapon, 2) as WeaponDefinition;
        var currentWeaponHasTarget = NpcWeaponHasTarget(battle, character, currentWeapon);
        var reserveWeaponHasTarget = NpcWeaponHasTarget(battle, character, reserveWeapon);
        var engaged = battle.IsEngaged(character);
        if (!TacticalBattleCoordinator.ShouldNpcSwapToReserveWeapon(character, engaged,
                currentWeaponHasTarget, reserveWeaponHasTarget)) return false;
        var unusableWeapon = Enumerable.Range(0, 2)
            .Where(index => character.InventoryItemCondition(InventorySlotKind.Weapon, index) ==
                            EquipmentCondition.Broken)
            .Select(index => character.GetInventoryItem(InventorySlotKind.Weapon, index) as WeaponDefinition)
            .FirstOrDefault(weapon => weapon is not null && weapon.WeaponTypeId != "WT003");
        if (!character.TrySwapReserveWeapon()) return false;
        var replacement = character.AttackWeapon;
        var statusText = _battleSystem.FinishCharacterAction(character, battle.RuntimeFor(character));
        _renderer.RefreshCharacterSheet(PartyLeader);
        var reason = unusableWeapon is not null
            ? $"eltört {unusableWeapon.Name} helyett"
            : currentWeapon is { IsRanged: true } && engaged && replacement?.IsRanged == false
                ? "közelharci lekötésben"
                : currentWeapon is { IsRanged: true } && !RangedWeaponRules.HasAmmunition(character, currentWeapon)
                    ? $"a(z) {currentWeapon.Name} lőszerének elfogyása miatt"
                    : currentWeapon is null
                        ? "használható aktív fegyver híján"
                        : "a célpont eléréséhez";
        PresentBattleEntries([new BattleLogEntry(
            $"🔄 {character.Name} {reason} előveszi a tartalékát: {replacement?.Name}.{statusText}",
            BattleLogKind.Information)]);
        AdvanceBattleTurn(battle);
        return true;
    }

    private bool NpcWeaponHasTarget(BattleEncounter battle, LiveCharacter character, WeaponDefinition? weapon)
    {
        if (weapon is null) return AdjacentEnemies(battle, character).Any();
        if (!RangedWeaponRules.HasAmmunition(character, weapon)) return false;
        var origin = GetCasterPosition(character);
        return battle.Enemies.Any(enemy => enemy.CurrentHitPoints > 0 &&
            RangedWeaponRules.CanReach(weapon, TacticalDistance.Between(origin, enemy.Position)) &&
            (weapon.IsRanged
                ? HasBattleLineOfSight(origin, enemy.Position, weapon.MaximumRange)
                : TacticalDistance.IsMeleeAdjacent(origin, enemy.Position) ||
                  weapon.CanAttackFromRear && battle.RearFormationEnemiesInReach(character).Contains(enemy)));
    }

    private bool TryExecuteAiTurnUndead(BattleEncounter battle, LiveCharacter character)
    {
        if (!IsTurnUndeadReady(character)) return false;
        var undead = TurnUndeadTargets(battle, character)
            .OrderBy(enemy => enemy.CurrentHitPoints).FirstOrDefault();
        if (undead is null) return false;
        var turning = ResolveTurnUndead(character, undead);
        battle.RecordAttack(BattleSide.Friendly);
        if (turning.DamageToEnemy > 0) undead.ReceiveSpellDamage(turning.DamageToEnemy);
        var message = turning.Message +
                      _battleSystem.FinishCharacterAction(character, battle.RuntimeFor(character));
        PresentBattleEntries([new BattleLogEntry(message, turning.Kind)]);
        if (undead.CurrentHitPoints <= 0) ResolveEnemyDefeat(battle, undead, character);
        AdvanceBattleTurn(battle);
        return true;
    }

    private void EnsureNpcOffensiveSpellPlan(BattleEncounter battle, LiveCharacter caster)
    {
        if (!caster.IsSpellcaster || !caster.CanCastSpells || !SpellcastingRules.HasRequiredFocus(caster))
        {
            battle.ClearNpcSpellPlan(caster);
            return;
        }

        var previousPlan = battle.NpcSpellPlanFor(caster);
        if (previousPlan is { } existing)
        {
            var existingTarget = existing.TargetEnemyId is { } enemyId
                ? battle.Enemies.FirstOrDefault(enemy => enemy.Id == enemyId && enemy.CurrentHitPoints > 0)
                : null;
            var existingSpell = caster.MemorizedSpells.FirstOrDefault(spell =>
                string.Equals(spell.Id, existing.SpellId, StringComparison.OrdinalIgnoreCase));
            var canSpendMana = existingSpell is not null && NpcSpellcastingPolicy.CanSpendMana(caster,
                SpellcastingRules.EffectiveManaCost(caster, existingSpell));
            var canReevaluate = NpcSpellPlanningPolicy.CanReevaluatePlan(existing.Status,
                existingTarget is not null, existingSpell is not null, canSpendMana);
            if (!canReevaluate)
            {
                var reason = existingTarget is null ? "a célpont már nem harcképes" :
                    existingSpell is null ? "a tervezett varázslat már nem elérhető" :
                    existing.Status == NpcSpellPlanStatus.Failed ? "a terv nem megvalósítható" :
                    "nincs hozzá elkölthető mana";
                PresentBattleEntries([new BattleLogEntry(
                    $"⚠️ {caster.Name} elveti korábbi varázstervét: {reason}.", BattleLogKind.Information)]);
                battle.ClearNpcSpellPlan(caster);
                previousPlan = null;
            }
            // A következő kiválasztás az élő tervet is újraértékeli: mozgás közben
            // az ellenfelek szétszéledhetnek, vagy kialakulhat egy jobb területi célpont.
        }

        var livingEnemies = battle.Enemies.Where(enemy => enemy.CurrentHitPoints > 0).ToArray();
        if (livingEnemies.Length == 0) return;
        var tactics = NpcTacticsFor(caster).EffectiveProfile(
            livingEnemies.Any(enemy => IsUnholy(enemy.Definition)));
        var enemyStrength = NpcSpellPlanningPolicy.EnemyStrength(
            livingEnemies.Select(enemy => enemy.EffectiveStrength));
        var mayCastOffensively = NpcSpellPlanningPolicy.ShouldCastOffensively(tactics, enemyStrength,
            battle.OffensiveSpellCastsFor(caster));
        if (_locationId == DeveloperBattleTestLocationId)
        {
            var spellDiagnostics = caster.MemorizedSpells.Where(spell => spell.CanUseInCombat).Select(spell =>
            {
                var classification = NpcSpellTacticalClassifier.Classify(spell,
                    _gameData.GetSpellEffects(spell.Id));
                var manaCost = SpellcastingRules.EffectiveManaCost(caster, spell);
                return $"{spell.Name}({spell.Id}):offensive={classification.IsOffensive}," +
                       $"pattern={classification.AttackPattern},mana={manaCost}," +
                       $"spendable={NpcSpellcastingPolicy.CanSpendMana(caster, manaCost)}";
            });
            _developerBattleLog.Append("AI-PLAN-GATE",
                $"battle={battle.Id}; cycle={battle.Turns.Cycle}; caster={caster.Name}; " +
                $"position={GetCasterPosition(caster)}; MP={caster.CurrentMana}/{caster.MaximumMana}; " +
                $"enemies={livingEnemies.Length}; strength={enemyStrength}; " +
                $"strengthBreakdown={string.Join(',', livingEnemies.Select(enemy => enemy.EffectiveStrength))}; " +
                $"profile={DeveloperBattleLog.FormatTactics(tactics)}; " +
                $"offensiveCasts={battle.OffensiveSpellCastsFor(caster)}; allowed={mayCastOffensively}; " +
                $"spells={string.Join(" / ", spellDiagnostics)}");
        }
        if (!mayCastOffensively)
        {
            if (previousPlan is not null)
            {
                battle.ClearNpcSpellPlan(caster);
                PresentBattleEntries([new BattleLogEntry(
                    $"⚠️ {caster.Name} elveti korábbi varázstervét: a megmaradt ellenfélerő már nem indokolja.",
                    BattleLogKind.Information)]);
            }
            return;
        }

        var selected = ChooseNpcOffensiveSpellPlan(battle, caster, livingEnemies);
        if (_locationId == DeveloperBattleTestLocationId)
            _developerBattleLog.Append("AI-PLAN-SELECT",
                selected is null
                    ? $"battle={battle.Id}; caster={caster.Name}; result=none"
                    : $"battle={battle.Id}; caster={caster.Name}; spell={selected.Spell.Name}({selected.Spell.Id}); " +
                      $"target={selected.PrimaryTarget.Name}({selected.PrimaryTarget.Id}); targets={selected.TargetCount}; " +
                      $"castFrom={selected.CastingPosition}; aim={selected.TargetPosition}; " +
                      $"distance={selected.MovementDistance}; ready={selected.ReadyToCast}; " +
                      $"utility={selected.Evaluation.Utility:F2}; usefulDamage={selected.Evaluation.UsefulDamage:F2}; " +
                      $"overkill={selected.Evaluation.Overkill:F2}");
        if (selected is null)
        {
            if (previousPlan is not null)
            {
                battle.ClearNpcSpellPlan(caster);
                PresentBattleEntries([new BattleLogEntry(
                    $"⚠️ {caster.Name} elveti korábbi varázstervét: már nincs elég hasznos célzás.",
                    BattleLogKind.Information)]);
            }
            return;
        }

        var casterPosition = GetCasterPosition(caster);
        var status = selected.ReadyToCast ? NpcSpellPlanStatus.ReadyToCast : NpcSpellPlanStatus.SeekingPosition;
        if (previousPlan is not null && SameNpcSpellPlanIntent(previousPlan, selected))
        {
            battle.SetNpcSpellPlan(caster, previousPlan with
            {
                TargetEnemyId = selected.PrimaryTarget.Id,
                TargetPosition = selected.TargetPosition,
                RequiredCastingPosition = selected.CastingPosition,
                ExpectedTargetCount = selected.TargetCount,
                ExpectedUtility = selected.Evaluation.Utility,
                Status = status
            });
            return;
        }

        var plan = new NpcSpellPlan(Guid.NewGuid(), selected.Spell.Id, selected.PrimaryTarget.Id,
            selected.TargetPosition, selected.CastingPosition,
            selected.Classification.Complexity, selected.Classification.AttackPattern,
            selected.Classification.Roles, battle.Turns.Cycle, selected.TargetCount,
            selected.Evaluation.Utility, status);
        battle.SetNpcSpellPlan(caster, plan);
        PresentBattleEntries([new BattleLogEntry(
            DescribeNpcOffensiveSpellPlan(caster, selected, replanned: previousPlan is not null),
            BattleLogKind.Information)]);
    }

    private static bool SameNpcSpellPlanIntent(NpcSpellPlan existing, NpcOffensiveSpellCandidate selected)
    {
        if (!string.Equals(existing.SpellId, selected.Spell.Id, StringComparison.OrdinalIgnoreCase) ||
            existing.TargetEnemyId != selected.PrimaryTarget.Id) return false;
        return selected.Classification.AttackPattern is NpcSpellAttackPattern.SingleTarget or
            NpcSpellAttackPattern.Chain ||
            existing.TargetPosition == selected.TargetPosition;
    }

    private NpcOffensiveSpellCandidate? ChooseNpcOffensiveSpellPlan(BattleEncounter battle,
        LiveCharacter caster, IReadOnlyList<Enemy> livingEnemies)
    {
        var casterPosition = GetCasterPosition(caster);
        var mayMoveForPlan = NpcSpellPlanningPolicy.CanMoveForPlan(
            caster.CharacterClass.Id == CharacterClassIds.Lovag, battle.HasActiveFormation,
            battle.IsEngaged(caster), battle.IsCharacterStaggered(caster));
        var castingPositions = mayMoveForPlan
            ? ReachableNpcSpellcastingPositions(battle, casterPosition, CombatantId.ForCharacter(caster.Id))
            : new Dictionary<Position, int> { [casterPosition] = 0 };
        var candidates = new List<NpcOffensiveSpellCandidate>();
        foreach (var spell in caster.MemorizedSpells.Where(spell => spell.CanUseInCombat))
        {
            var effects = _gameData.GetSpellEffects(spell.Id).ToArray();
            var classification = NpcSpellTacticalClassifier.Classify(spell, effects);
            var manaCost = SpellcastingRules.EffectiveManaCost(caster, spell);
            if (!classification.IsOffensive ||
                !NpcSpellcastingPolicy.CanSpendMana(caster, manaCost)) continue;

            foreach (var castingPosition in castingPositions)
            {
                if (spell.TargetType == SpellTargetType.Enemy)
                {
                    foreach (var enemy in livingEnemies)
                    {
                        if (!IsValidExplicitSpellTarget(caster, castingPosition.Key, spell,
                                enemy.Position, enemy)) continue;
                        var targets = NpcSpellPlanTargets(classification.AttackPattern, effects, enemy,
                            livingEnemies);
                        candidates.Add(CreateNpcOffensiveSpellCandidate(caster, spell, effects,
                            classification, castingPosition.Key, enemy.Position, enemy, targets,
                            manaCost, castingPosition.Value, battle.CurrentMovementAllowance, livingEnemies));
                    }
                    continue;
                }

                IEnumerable<Position> targetPositions = spell.TargetType switch
                {
                    SpellTargetType.Area => PotentialNpcAreaSpellTargets(livingEnemies, spell.AreaRadius),
                    SpellTargetType.Direction => Directions.Select(direction => castingPosition.Key + direction),
                    _ => []
                };
                foreach (var targetPosition in targetPositions.Distinct())
                {
                    if (!IsValidExplicitSpellTarget(caster, castingPosition.Key, spell,
                            targetPosition, livingEnemies[0])) continue;
                    var affected = ResolveEnemySpellTargets(spell, targetPosition, livingEnemies[0],
                            castingPosition.Key)
                        .Where(livingEnemies.Contains).Distinct().ToArray();
                    if (affected.Length == 0) continue;
                    var primaryTarget = affected.OrderByDescending(enemy => enemy.Definition.StrengthTier)
                        .ThenByDescending(enemy => enemy.Definition.Rank)
                        .ThenByDescending(enemy => enemy.CurrentHitPoints).First();
                    var targets = affected.Select(enemy => new NpcSpellPlanTarget(enemy)).ToArray();
                    candidates.Add(CreateNpcOffensiveSpellCandidate(caster, spell, effects,
                        classification, castingPosition.Key, targetPosition, primaryTarget, targets,
                        manaCost, castingPosition.Value, battle.CurrentMovementAllowance, livingEnemies));
                }
            }
        }

        var scoredCandidates = candidates
            .Select(candidate => ApplyNpcSpellTacticalMemory(battle, caster, candidate))
            .Where(candidate => candidate.Evaluation.Utility > 0)
            .ToArray();
        var rankedCandidates = scoredCandidates
            .OrderByDescending(candidate => candidate.Evaluation.Utility)
            .ThenByDescending(candidate => candidate.Evaluation.UsefulDamage)
            .ThenBy(candidate => candidate.MovementDistance)
            .ThenBy(candidate => SpellcastingRules.EffectiveManaCost(caster, candidate.Spell))
            .ThenBy(candidate => candidate.Spell.Level)
            .ThenBy(candidate => candidate.Spell.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.CastingPosition.Y)
            .ThenBy(candidate => candidate.CastingPosition.X)
            .ThenBy(candidate => candidate.TargetPosition.Y)
            .ThenBy(candidate => candidate.TargetPosition.X)
            .ToArray();
        if (_locationId == DeveloperBattleTestLocationId)
            _developerBattleLog.Append("AI-CANDIDATES",
                $"battle={battle.Id}; caster={caster.Name}; generated={candidates.Count}; positive={rankedCandidates.Length}; " +
                $"top={string.Join(" / ", rankedCandidates.Take(12).Select(candidate =>
                    $"{candidate.Spell.Id}:u={candidate.Evaluation.Utility:F2},dmg={candidate.Evaluation.UsefulDamage:F2}," +
                    $"over={candidate.Evaluation.Overkill:F2},targets={candidate.TargetCount},move={candidate.MovementDistance}," +
                    $"from={candidate.CastingPosition},aim={candidate.TargetPosition}"))}");
        var best = rankedCandidates.FirstOrDefault();
        if (best is not null)
            best = PreferSaferFullCastingMove(battle, caster, livingEnemies, rankedCandidates, best);
        if (best is null || battle.NpcSpellPlanFor(caster) is not { } activePlan) return best;

        var incumbent = rankedCandidates.FirstOrDefault(candidate =>
            CandidateContinuesNpcSpellPlan(candidate, activePlan));
        if (incumbent is null) return best;
        return NpcSpellPlanningPolicy.ShouldRetainPlan(activePlan.ExpectedUtility,
            incumbent.Evaluation.Utility, best.Evaluation.Utility)
            ? incumbent
            : best;
    }

    private NpcOffensiveSpellCandidate PreferSaferFullCastingMove(BattleEncounter battle,
        LiveCharacter caster, IReadOnlyList<Enemy> livingEnemies,
        IReadOnlyList<NpcOffensiveSpellCandidate> rankedCandidates, NpcOffensiveSpellCandidate selected)
    {
        var movementAllowance = Math.Max(1, battle.CurrentMovementAllowance);
        var origin = GetCasterPosition(caster);
        var nearestEnemyDistance = livingEnemies.Min(enemy => TacticalDistance.Between(origin, enemy.Position));
        if (!NpcSpellPlanningPolicy.CanPreferSaferFullCastingMove(nearestEnemyDistance, movementAllowance,
                selected.MovementDistance, selected.Evaluation.Utility, selected.Evaluation.Utility))
            return selected;

        var actorId = CombatantId.ForCharacter(caster.Id);
        return rankedCandidates
            .Where(candidate => string.Equals(candidate.Spell.Id, selected.Spell.Id,
                                    StringComparison.OrdinalIgnoreCase) &&
                                candidate.TargetCount >= selected.TargetCount &&
                                candidate.MovementDistance >= movementAllowance &&
                                NpcSpellPlanningPolicy.CanPreferSaferFullCastingMove(nearestEnemyDistance,
                                    movementAllowance, selected.MovementDistance, selected.Evaluation.Utility,
                                    candidate.Evaluation.Utility))
            .Select(candidate =>
            {
                var path = FindNpcSpellcastingPath(battle, origin, candidate.CastingPosition, actorId);
                if (path.Count < movementAllowance)
                    return (Candidate: (NpcOffensiveSpellCandidate?)null, Safety: -1);
                var firstTurnPosition = path[movementAllowance - 1];
                var safety = livingEnemies.Min(enemy =>
                    TacticalDistance.Between(firstTurnPosition, enemy.Position));
                return (Candidate: (NpcOffensiveSpellCandidate?)candidate, Safety: safety);
            })
            .Where(option => option.Candidate is not null && option.Safety >= nearestEnemyDistance)
            .OrderByDescending(option => option.Safety)
            .ThenByDescending(option => option.Candidate!.Evaluation.Utility)
            .Select(option => option.Candidate!)
            .FirstOrDefault() ?? selected;
    }

    private static bool CandidateContinuesNpcSpellPlan(NpcOffensiveSpellCandidate candidate,
        NpcSpellPlan plan)
    {
        if (!string.Equals(candidate.Spell.Id, plan.SpellId, StringComparison.OrdinalIgnoreCase) ||
            candidate.PrimaryTarget.Id != plan.TargetEnemyId ||
            candidate.CastingPosition != plan.RequiredCastingPosition) return false;
        return candidate.Classification.AttackPattern is NpcSpellAttackPattern.SingleTarget or
            NpcSpellAttackPattern.Chain || candidate.TargetPosition == plan.TargetPosition;
    }

    private static NpcOffensiveSpellCandidate ApplyNpcSpellTacticalMemory(BattleEncounter battle,
        LiveCharacter caster, NpcOffensiveSpellCandidate candidate)
    {
        var memories = battle.NpcOffensiveSpellMemoriesFor(caster);
        var utility = NpcSpellPlanningPolicy.AdjustUtilityForMemory(memories, candidate.Spell.Id,
            candidate.Classification.Complexity, candidate.Classification.AttackPattern,
            candidate.Evaluation.Utility);
        return candidate with { Evaluation = candidate.Evaluation with { Utility = utility } };
    }

    private NpcOffensiveSpellCandidate CreateNpcOffensiveSpellCandidate(LiveCharacter caster,
        SpellDefinition spell, IReadOnlyList<SpellEffectDefinition> effects,
        NpcSpellTacticalClassification classification, Position castingPosition, Position targetPosition,
        Enemy primaryTarget, IReadOnlyList<NpcSpellPlanTarget> targets, int manaCost, int movementDistance,
        int movementAllowance, IReadOnlyList<Enemy> livingEnemies)
    {
        var evaluation = NpcSpellPlanEvaluator.Evaluate(caster, spell, effects, targets, manaCost);
        var adjacentThreat = livingEnemies.Where(enemy =>
                TacticalDistance.IsMeleeAdjacent(castingPosition, enemy.Position))
            .Sum(enemy => Math.Max(1, enemy.Definition.StrengthTier));
        var positionPenalty = NpcSpellPlanningPolicy.PositionPenalty(movementDistance,
            movementAllowance, adjacentThreat);
        return new NpcOffensiveSpellCandidate(spell, castingPosition, targetPosition, primaryTarget,
            classification, targets.Count,
            evaluation with { Utility = evaluation.Utility - positionPenalty },
            ReadyToCast: movementDistance == 0, movementDistance);
    }

    private Dictionary<Position, int> ReachableNpcSpellcastingPositions(BattleEncounter battle,
        Position origin, CombatantId actorId)
    {
        var distances = new Dictionary<Position, int> { [origin] = 0 };
        var queue = new Queue<Position>();
        queue.Enqueue(origin);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var direction in Directions)
            {
                var next = current + direction;
                if (distances.ContainsKey(next) || !battle.Turns.IsInsideBattleArea(next) ||
                    !CanBattleEnter(battle, next, actorId)) continue;
                distances[next] = distances[current] + 1;
                queue.Enqueue(next);
            }
        }
        return distances;
    }

    private IEnumerable<Position> PotentialNpcAreaSpellTargets(IReadOnlyList<Enemy> livingEnemies, int radius)
    {
        var effectiveRadius = Math.Max(0, radius);
        foreach (var enemy in livingEnemies)
        for (var y = enemy.Position.Y - effectiveRadius; y <= enemy.Position.Y + effectiveRadius; y++)
        for (var x = enemy.Position.X - effectiveRadius; x <= enemy.Position.X + effectiveRadius; x++)
        {
            var position = new Position(x, y);
            if (_maze.IsInside(position)) yield return position;
        }
    }

    private static IReadOnlyList<NpcSpellPlanTarget> NpcSpellPlanTargets(NpcSpellAttackPattern pattern,
        IReadOnlyList<SpellEffectDefinition> effects, Enemy primaryTarget, IReadOnlyList<Enemy> livingEnemies)
    {
        if (pattern != NpcSpellAttackPattern.Chain) return [new NpcSpellPlanTarget(primaryTarget)];
        var chainEffect = effects.First(effect => effect.Type == SpellEffectType.ChainDamage);
        var multipliers = (chainEffect.Parameter ?? "100|75|50|25").Split('|')
            .Select(value => int.TryParse(value, out var parsed) ? parsed : 100).ToArray();
        return livingEnemies.OrderBy(enemy => enemy == primaryTarget ? 0 :
                Chebyshev(enemy.Position, primaryTarget.Position))
            .Where(enemy => enemy == primaryTarget ||
                            Chebyshev(enemy.Position, primaryTarget.Position) <= 4)
            .Take(4)
            .Select((enemy, index) => new NpcSpellPlanTarget(enemy,
                multipliers[Math.Min(index, multipliers.Length - 1)] / 100.0))
            .ToArray();
    }

    private static string DescribeNpcOffensiveSpellPlan(LiveCharacter caster,
        NpcOffensiveSpellCandidate candidate, bool replanned = false)
    {
        var lead = replanned ? $"🔄 {caster.Name} új terve" : $"🔮 {caster.Name} terve";
        var movement = candidate.MovementDistance > 0
            ? $" Előbb {candidate.MovementDistance} mezőnyire lévő tüzelőállást keres."
            : string.Empty;
        var plan = candidate.Classification.AttackPattern switch
        {
            NpcSpellAttackPattern.Area =>
                $"{lead}: {candidate.Spell.Name} varázslattal egy " +
                $"{candidate.TargetCount} fős ellenségcsoportot céloz.",
            NpcSpellAttackPattern.Direction =>
                $"{lead}: {candidate.Spell.Name} varázslattal " +
                $"{candidate.TargetCount} ellenfelet fog elérni.",
            NpcSpellAttackPattern.Chain =>
                $"{lead}: {candidate.Spell.Name} varázslatot indít " +
                $"{candidate.PrimaryTarget.Name} felől, várhatóan {candidate.TargetCount} célpontra.",
            _ => $"{lead}: {candidate.Spell.Name} varázslatot használ " +
                 $"{candidate.PrimaryTarget.Name} ellen."
        };
        return plan + movement;
    }

    private bool TryExecuteNpcSpellcasterPositioning(BattleEncounter battle, LiveCharacter caster)
    {
        if (!caster.IsSpellcaster || !caster.CanCastSpells ||
            caster.CharacterClass.Id == CharacterClassIds.Lovag) return false;
        var configuredTactics = NpcTacticsFor(caster);
        var livingEnemies = battle.Enemies.Where(enemy => enemy.CurrentHitPoints > 0).ToArray();
        var tactics = configuredTactics.EffectiveProfile(livingEnemies.Any(enemy => IsUnholy(enemy.Definition)));
        var enemyStrength = NpcSpellPlanningPolicy.EnemyStrength(
            livingEnemies.Select(enemy => enemy.EffectiveStrength));
        var withinCastingPlan = NpcSpellPlanningPolicy.ShouldCastOffensively(tactics, enemyStrength,
            battle.OffensiveSpellCastsFor(caster));
        var activePlan = battle.NpcSpellPlanFor(caster);
        if (activePlan is not null)
            return MoveNpcSpellcasterTowardPlannedCastingPosition(battle, caster, activePlan);
        var offensiveSpells = caster.MemorizedSpells.Where(spell => spell.CanUseInCombat &&
                NpcSpellcastingPolicy.IsSingleTargetOffensive(spell, _gameData.GetSpellEffects(spell.Id)))
            .ToArray();
        var spendableOffensiveSpells = offensiveSpells.Where(spell =>
            NpcSpellcastingPolicy.CanSpendMana(caster, SpellcastingRules.EffectiveManaCost(caster, spell))).ToArray();
        var hasSpendableMana = withinCastingPlan && spendableOffensiveSpells.Length > 0;
        if (_locationId == DeveloperBattleTestLocationId)
            _developerBattleLog.Append("AI-POSITION",
                $"battle={battle.Id}; cycle={battle.Turns.Cycle}; caster={caster.Name}; position={GetCasterPosition(caster)}; " +
                $"strength={enemyStrength}; profile={DeveloperBattleLog.FormatTactics(tactics)}; " +
                $"withinPlan={withinCastingPlan}; offensiveSpells={string.Join(',', offensiveSpells.Select(spell => spell.Id))}; " +
                $"spendable={string.Join(',', spendableOffensiveSpells.Select(spell => spell.Id))}; " +
                $"activePlan={activePlan?.SpellId ?? "none"}; fallback={tactics.ManaFallback}");

        if (!hasSpendableMana)
        {
            if (tactics.ManaFallback != SpellcasterManaFallback.Retreat) return false;
            var preferredSafety = TacticalBattleCoordinator.PreferredSpellcasterRetreatDistance(livingEnemies);
            var currentSafety = livingEnemies.Length == 0 ? preferredSafety : livingEnemies.Min(enemy =>
                TacticalDistance.Between(GetCasterPosition(caster), enemy.Position));
            if (currentSafety >= preferredSafety)
            {
                FinishNpcSpellcasterPositioning(battle, caster, "biztonságos távolságot tart");
                return true;
            }
            return MoveNpcSpellcasterToBestPosition(battle, caster, livingEnemies, offensiveSpells,
                seekLineOfSight: false, preferredSafety);
        }

        var origin = GetCasterPosition(caster);
        if (livingEnemies.Any(enemy => spendableOffensiveSpells.Any(spell =>
                FogOfWar.CanSee(_maze, origin, enemy.Position, Math.Max(1, spell.Range)))))
        {
            FinishNpcSpellcasterPositioning(battle, caster, "megtartja a lővonalat");
            return true;
        }
        return MoveNpcSpellcasterToBestPosition(battle, caster, livingEnemies, spendableOffensiveSpells,
            seekLineOfSight: true);
    }

    private bool MoveNpcSpellcasterTowardPlannedCastingPosition(BattleEncounter battle,
        LiveCharacter caster, NpcSpellPlan plan)
    {
        var origin = GetCasterPosition(caster);
        if (plan.RequiredCastingPosition is not { } destination || destination == origin)
        {
            battle.SetNpcSpellPlan(caster, plan with { Status = NpcSpellPlanStatus.Failed });
            return false;
        }

        var path = FindNpcSpellcastingPath(battle, origin, destination,
            CombatantId.ForCharacter(caster.Id));
        if (path.Count == 0)
        {
            battle.SetNpcSpellPlan(caster, plan with { Status = NpcSpellPlanStatus.Failed });
            FinishNpcSpellcasterPositioning(battle, caster,
                "nem talál járható utat a tervezett tüzelőálláshoz");
            return true;
        }

        var spellName = caster.MemorizedSpells.FirstOrDefault(spell =>
            string.Equals(spell.Id, plan.SpellId, StringComparison.OrdinalIgnoreCase))?.Name ?? plan.SpellId;
        battle.SetNpcSpellPlan(caster, plan with { Status = NpcSpellPlanStatus.SeekingPosition });
        PresentBattleEntries([new BattleLogEntry(
            $"🔮 {caster.Name} a(z) {spellName} tervezett tüzelőállásához mozog " +
            $"({path.Count} mező).", BattleLogKind.Information)]);
        CompleteCharacterMovement(battle, caster, path);
        return true;
    }

    private IReadOnlyList<Position> FindNpcSpellcastingPath(BattleEncounter battle,
        Position origin, Position destination, CombatantId actorId)
    {
        var queue = new Queue<Position>();
        var previous = new Dictionary<Position, Position> { [origin] = origin };
        queue.Enqueue(origin);
        while (queue.Count > 0 && !previous.ContainsKey(destination))
        {
            var current = queue.Dequeue();
            foreach (var direction in Directions)
            {
                var next = current + direction;
                if (previous.ContainsKey(next) || !battle.Turns.IsInsideBattleArea(next) ||
                    !CanBattleEnter(battle, next, actorId)) continue;
                previous[next] = current;
                queue.Enqueue(next);
            }
        }
        if (!previous.ContainsKey(destination)) return [];
        var path = new List<Position>();
        for (var current = destination; current != origin; current = previous[current]) path.Add(current);
        path.Reverse();
        return path;
    }

    private bool MoveNpcSpellcasterToBestPosition(BattleEncounter battle, LiveCharacter caster,
        IReadOnlyList<Enemy> enemies, IReadOnlyList<SpellDefinition> offensiveSpells, bool seekLineOfSight,
        int? preferredSafety = null)
    {
        var origin = GetCasterPosition(caster);
        var actorId = CombatantId.ForCharacter(caster.Id);
        if (seekLineOfSight)
        {
            var lineOfSightPath = FindPathToNearestNpcSpellLineOfSight(battle, origin, actorId,
                enemies, offensiveSpells);
            if (lineOfSightPath.Count > 0)
            {
                if (battle.NpcSpellPlanFor(caster) is { } plan)
                {
                    battle.SetNpcSpellPlan(caster, plan with
                    {
                        RequiredCastingPosition = lineOfSightPath[^1],
                        Status = NpcSpellPlanStatus.SeekingPosition
                    });
                    PresentBattleEntries([new BattleLogEntry(
                        $"🔮 {caster.Name} a tervezett varázslat lővonalához mozog.",
                        BattleLogKind.Information)]);
                }
                CompleteCharacterMovement(battle, caster, lineOfSightPath);
                return true;
            }
            if (battle.NpcSpellPlanFor(caster) is { } failedPlan)
                battle.SetNpcSpellPlan(caster, failedPlan with { Status = NpcSpellPlanStatus.Failed });
            FinishNpcSpellcasterPositioning(battle, caster, "nem talál elérhető lővonalat");
            return true;
        }

        var candidates = new List<(IReadOnlyList<Position> Path, int VisibleTargets, int Safety)>();
        var allowance = Math.Max(1, battle.CurrentMovementAllowance);
        for (var y = Math.Max(0, origin.Y - allowance); y <= Math.Min(_maze.Height - 1, origin.Y + allowance); y++)
        for (var x = Math.Max(0, origin.X - allowance); x <= Math.Min(_maze.Width - 1, origin.X + allowance); x++)
        {
            var position = new Position(x, y);
            if (position == origin || !CanBattleEnter(battle, position, actorId)) continue;
            var path = FindBattlePath(battle, origin, [position], actorId);
            if (path.Count == 0 || path.Count > battle.CurrentMovementAllowance) continue;
            var visibleTargets = enemies.Count(enemy => offensiveSpells.Any(spell =>
                FogOfWar.CanSee(_maze, position, enemy.Position, Math.Max(1, spell.Range))));
            if (seekLineOfSight && visibleTargets == 0) continue;
            var safety = enemies.Count == 0 ? 0 : enemies.Min(enemy => TacticalDistance.Between(position, enemy.Position));
            candidates.Add((path, visibleTargets, safety));
        }
        var selected = preferredSafety is { } safetyGoal
            ? candidates.OrderByDescending(candidate => Math.Min(candidate.Safety, safetyGoal))
                .ThenBy(candidate => candidate.Path.Count)
                .ThenByDescending(candidate => candidate.VisibleTargets).FirstOrDefault()
            : candidates.OrderByDescending(candidate => candidate.VisibleTargets)
                .ThenByDescending(candidate => candidate.Safety)
                .ThenBy(candidate => candidate.Path.Count).FirstOrDefault();
        if (selected.Path is not null)
        {
            CompleteCharacterMovement(battle, caster, selected.Path);
            return true;
        }
        FinishNpcSpellcasterPositioning(battle, caster,
            seekLineOfSight ? "nem talál elérhető lővonalat" : "biztonságos helyen marad");
        return true;
    }

    private IReadOnlyList<Position> FindPathToNearestNpcSpellLineOfSight(BattleEncounter battle,
        Position origin, CombatantId actorId, IReadOnlyList<Enemy> enemies,
        IReadOnlyList<SpellDefinition> offensiveSpells)
    {
        bool HasLineOfSight(Position position) => enemies.Any(enemy => offensiveSpells.Any(spell =>
            FogOfWar.CanSee(_maze, position, enemy.Position, Math.Max(1, spell.Range))));

        var queue = new Queue<Position>();
        var previous = new Dictionary<Position, Position> { [origin] = origin };
        queue.Enqueue(origin);
        Position? destination = null;
        while (queue.Count > 0 && destination is null)
        {
            var current = queue.Dequeue();
            foreach (var direction in Directions)
            {
                var next = current + direction;
                if (previous.ContainsKey(next) || !battle.Turns.IsInsideBattleArea(next) ||
                    !CanBattleEnter(battle, next, actorId)) continue;
                previous[next] = current;
                if (HasLineOfSight(next))
                {
                    destination = next;
                    break;
                }
                queue.Enqueue(next);
            }
        }
        if (destination is null) return [];
        var path = new List<Position>();
        for (var current = destination.Value; current != origin; current = previous[current]) path.Add(current);
        path.Reverse();
        return path;
    }

    private void FinishNpcSpellcasterPositioning(BattleEncounter battle, LiveCharacter caster, string action)
    {
        var statusText = _battleSystem.FinishCharacterAction(caster, battle.RuntimeFor(caster));
        PresentBattleEntries([new BattleLogEntry($"{caster.Name} {action}.{statusText}",
            BattleLogKind.Information)]);
        AdvanceBattleTurn(battle);
    }

    private bool TryExecuteAiSpell(BattleEncounter battle, LiveCharacter caster)
    {
        var plan = ChooseAiSpell(battle, caster);
        if (plan is null) return false;
        if (NpcSpellcastingPolicy.UsesEngagedSpellCadence(caster.CharacterClass.Id) &&
            battle.IsEngaged(caster))
        {
            var urgent = IsUrgentEngagedSupportSpell(battle, caster, plan);
            if (!NpcSpellcastingPolicy.CanCastWhileEngaged(battle.Turns.Cycle, urgent))
            {
                if (_locationId == DeveloperBattleTestLocationId)
                    _developerBattleLog.Append("AI-ENGAGED-SPELL-SKIP",
                        $"battle={battle.Id}; cycle={battle.Turns.Cycle}; caster={caster.Name}; " +
                        $"spell={plan.Spell.Name}({plan.Spell.Id}); urgent={urgent}; reason=routine-cadence");
                return false;
            }
        }
        var attempt = TryCastSpell(caster, GetCasterPosition(caster), plan.Spell, inCombat: true,
            plan.Enemy, explicitTarget: plan.Target);
        if (attempt is not { ConsumesTurn: true }) return false;
        battle.RecordSpellCast(caster);
        if (plan.Offensive)
        {
            battle.RecordOffensiveSpellCast(caster);
            if (battle.NpcSpellPlanFor(caster) is { } completedPlan &&
                string.Equals(completedPlan.SpellId, plan.Spell.Id, StringComparison.OrdinalIgnoreCase) &&
                completedPlan.TargetEnemyId == plan.Enemy?.Id)
            {
                battle.RecordNpcOffensiveSpellMemory(caster, completedPlan);
                battle.ClearNpcSpellPlan(caster);
            }
            battle.RecordAttack(BattleSide.Friendly);
            if (attempt.DamageToCurrentEnemy > 0 && plan.Enemy is not null)
                plan.Enemy.ReceiveSpellDamage(attempt.DamageToCurrentEnemy);
        }
        battle.GrantExtraActions(attempt.ExtraPlayerActions);
        var message = attempt.Message +
                      _battleSystem.FinishCharacterAction(caster, battle.RuntimeFor(caster));
        PresentBattleEntries([new BattleLogEntry(message, attempt.Kind)]);
        SynchronizeBattleDefeats(battle, caster);
        AdvanceBattleTurn(battle);
        return true;
    }

    private bool IsUrgentEngagedSupportSpell(BattleEncounter battle, LiveCharacter caster,
        NpcBattleSpellChoice plan)
    {
        var effects = _gameData.GetSpellEffects(plan.Spell.Id);
        var targets = plan.Spell.TargetType switch
        {
            SpellTargetType.Self => [caster],
            SpellTargetType.Party => battle.Characters.Where(character => character.IsAlive).ToArray(),
            SpellTargetType.PartyMember => battle.Characters.Where(character => character.IsAlive &&
                GetCasterPosition(character) == plan.Target).ToArray(),
            _ => []
        };
        return targets.Any(target => NpcSpellcastingPolicy.NeedsCleansing(target, effects)) ||
               effects.Any(effect => effect.Type == SpellEffectType.Heal) &&
               targets.Any(NpcSpellcastingPolicy.IsEmergency);
    }

    private NpcBattleSpellChoice? ChooseAiSpell(BattleEncounter battle, LiveCharacter caster)
    {
        if (!caster.IsSpellcaster || !caster.CanCastSpells ||
            !SpellcastingRules.HasRequiredFocus(caster)) return null;
        var casterPosition = GetCasterPosition(caster);
        var spells = caster.MemorizedSpells.Where(spell => spell.CanUseInCombat)
            .OrderBy(spell => SpellcastingRules.EffectiveManaCost(caster, spell))
            .ThenBy(spell => spell.Level).ToArray();
        var allies = battle.Characters.Where(character => character.IsAlive)
            .OrderBy(character => VitalityRatio(character)).ToArray();
        var enemies = OrderedNpcSpellTargets(battle, casterPosition).ToArray();
        var currentEnemy = enemies.FirstOrDefault();
        var livingEnemies = battle.Enemies.Where(enemy => enemy.CurrentHitPoints > 0).ToArray();
        var configuredTactics = NpcTacticsFor(caster);
        var tactics = configuredTactics.EffectiveProfile(livingEnemies.Any(enemy => IsUnholy(enemy.Definition)));
        var enemyStrength = NpcSpellPlanningPolicy.EnemyStrength(
            livingEnemies.Select(enemy => enemy.EffectiveStrength));
        var mayCastOffensively = NpcSpellPlanningPolicy.ShouldCastOffensively(tactics, enemyStrength,
            battle.OffensiveSpellCastsFor(caster));

        var prioritizeRearSelfBuff = battle.ShouldPrioritizeRearSelfBuff(caster) &&
            (caster.CurrentVitality >= caster.MaximumVitality ||
             TacticalBattleCoordinator.ChooseNpcHealingPotionIndex(battle, caster, allowedWaste: 15) is null);
        if (prioritizeRearSelfBuff &&
            ChooseAiSelfBuff(battle, caster, casterPosition, spells, allies, currentEnemy) is { } selfBuff)
            return selfBuff;

        foreach (var spell in spells)
        {
            var effects = _gameData.GetSpellEffects(spell.Id);
            if (!effects.Any(effect => effect.Type == SpellEffectType.Heal)) continue;
            var alsoCleanses = effects.Any(effect => effect.Type is SpellEffectType.CureStatus or
                SpellEffectType.Dispel or SpellEffectType.BreakItemCurse);
            var wounded = allies.Where(NpcSpellcastingPolicy.NeedsHealing).ToArray();
            if (wounded.Length == 0) break;
            IEnumerable<LiveCharacter> targets = spell.TargetType switch
            {
                SpellTargetType.Self => wounded.Where(character => character == caster),
                SpellTargetType.Party => [wounded[0]],
                SpellTargetType.PartyMember => wounded,
                _ => []
            };
            foreach (var target in targets)
            {
                if (alsoCleanses && !NpcSpellcastingPolicy.NeedsCleansing(target, effects))
                    continue;
                var targetPosition = spell.TargetType is SpellTargetType.Self or SpellTargetType.Party
                    ? casterPosition : GetCasterPosition(target);
                var emergency = spell.TargetType == SpellTargetType.Party
                    ? wounded.Any(NpcSpellcastingPolicy.IsEmergency)
                    : NpcSpellcastingPolicy.IsEmergency(target);
                var manaCost = SpellcastingRules.EffectiveManaCost(caster, spell);
                if (!NpcSpellcastingPolicy.CanSpendMana(caster, manaCost, emergency) ||
                    ValidateSpellCast(caster, casterPosition, spell, true, currentEnemy,
                        explicitTarget: targetPosition) is not null) continue;
                return new NpcBattleSpellChoice(spell, targetPosition, currentEnemy, Offensive: false);
            }
        }

        foreach (var spell in spells)
        {
            var effects = _gameData.GetSpellEffects(spell.Id);
            if (!effects.Any(effect => effect.Type is SpellEffectType.CureStatus or SpellEffectType.Dispel or
                    SpellEffectType.BreakItemCurse)) continue;
            foreach (var ally in allies.Where(ally => NpcSpellcastingPolicy.NeedsCleansing(ally, effects)))
            {
                var targetPosition = spell.TargetType is SpellTargetType.Self or SpellTargetType.Party
                    ? casterPosition : GetCasterPosition(ally);
                var manaCost = SpellcastingRules.EffectiveManaCost(caster, spell);
                if (!NpcSpellcastingPolicy.CanSpendMana(caster, manaCost) ||
                    ValidateSpellCast(caster, casterPosition, spell, true, currentEnemy,
                        explicitTarget: targetPosition) is not null) continue;
                return new NpcBattleSpellChoice(spell, targetPosition, currentEnemy, Offensive: false);
            }
        }

        var vulnerableAlly = allies.FirstOrDefault();
        var dangerousEnemy = vulnerableAlly is null ? null : enemies.FirstOrDefault(enemy =>
            ShouldUseOffensiveSupportSpell(vulnerableAlly, enemy));
        dangerousEnemy ??= currentEnemy;
        if (dangerousEnemy is null) return null;

        var activePlan = battle.NpcSpellPlanFor(caster);
        if (activePlan is not null)
        {
            var plannedSpell = spells.FirstOrDefault(spell => string.Equals(spell.Id, activePlan.SpellId,
                StringComparison.OrdinalIgnoreCase));
            var plannedTarget = activePlan.TargetEnemyId is { } targetId
                ? enemies.FirstOrDefault(enemy => enemy.Id == targetId)
                : null;
            if (plannedSpell is not null && plannedTarget is not null &&
                activePlan.RequiredCastingPosition == casterPosition &&
                NpcSpellcastingPolicy.CanSpendMana(caster,
                    SpellcastingRules.EffectiveManaCost(caster, plannedSpell)) &&
                ValidateSpellCast(caster, casterPosition, plannedSpell, true, plannedTarget,
                    explicitTarget: activePlan.TargetPosition) is null)
            {
                if (activePlan.Status != NpcSpellPlanStatus.ReadyToCast ||
                    activePlan.RequiredCastingPosition != casterPosition)
                    battle.SetNpcSpellPlan(caster, activePlan with
                    {
                        RequiredCastingPosition = casterPosition,
                        Status = NpcSpellPlanStatus.ReadyToCast
                    });
                return new NpcBattleSpellChoice(plannedSpell, activePlan.TargetPosition, plannedTarget, Offensive: true);
            }
            return null;
        }

        // Az önbuff a hozzá tartozó fallback része; nem előzheti meg a még
        // rendelkezésre álló támadó varázslatokat.
        if (tactics.ManaFallback == SpellcasterManaFallback.SelfBuffAndMelee && !mayCastOffensively)
            foreach (var spell in spells)
            {
                var effects = _gameData.GetSpellEffects(spell.Id);
                if (effects.Any(effect => effect.Type == SpellEffectType.Heal) ||
                    !effects.Any(effect => NpcSpellcastingPolicy.IsBuffEffect(effect.Type)) ||
                    effects.Any(effect => effect.Type == SpellEffectType.ProtectionFromEvil) &&
                    battle.Enemies.Where(enemy => enemy.CurrentHitPoints > 0).All(enemy => !IsUnholy(enemy.Definition)))
                    continue;
                var manaCost = SpellcastingRules.EffectiveManaCost(caster, spell);
                if (!NpcSpellcastingPolicy.CanSpendMana(caster, manaCost)) continue;
                var allowSelfBuff = !battle.HasProtectiveFormation || !battle.IsRearRow(caster) ||
                                    prioritizeRearSelfBuff;
                var targetPosition = ChooseNpcBuffTarget(battle, caster, casterPosition, spell, effects, allies,
                    allowSelfBuff);
                if (targetPosition is null || ValidateSpellCast(caster, casterPosition, spell, true,
                        dangerousEnemy, explicitTarget: targetPosition) is not null) continue;
                var beneficiaryCount = spell.TargetType == SpellTargetType.Party
                    ? allies.Count(ally => effects
                        .Where(effect => NpcSpellcastingPolicy.IsBuffEffect(effect.Type))
                        .Select(effect => NpcSpellcastingPolicy.ActiveTypeFor(effect.Type))
                        .Any(type => type is { } activeType && !ally.HasSpellEffect(activeType)))
                    : 1;
                var castChance = NpcSpellcastingPolicy.BuffCastChancePercent(manaCost, caster.CurrentMana,
                    enemyStrength, livingEnemies.Length, beneficiaryCount, battle.IsEngaged(caster),
                    battle.IsFrontRow(caster), effects);
                if (!NpcSpellcastingPolicy.ShouldCastBuff(castChance, _random.Next(100))) continue;
                return new NpcBattleSpellChoice(spell, targetPosition.Value, dangerousEnemy, Offensive: false);
            }
        return null;
    }

    private NpcBattleSpellChoice? ChooseAiSelfBuff(BattleEncounter battle, LiveCharacter caster,
        Position casterPosition, IReadOnlyList<SpellDefinition> spells, IReadOnlyList<LiveCharacter> allies,
        Enemy? currentEnemy)
    {
        foreach (var spell in spells)
        {
            var effects = _gameData.GetSpellEffects(spell.Id);
            if (effects.Any(effect => effect.Type == SpellEffectType.Heal) ||
                !effects.Any(effect => NpcSpellcastingPolicy.IsBuffEffect(effect.Type)) ||
                effects.Any(effect => effect.Type == SpellEffectType.ProtectionFromEvil) &&
                battle.Enemies.Where(enemy => enemy.CurrentHitPoints > 0).All(enemy => !IsUnholy(enemy.Definition)))
                continue;
            var manaCost = SpellcastingRules.EffectiveManaCost(caster, spell);
            if (!NpcSpellcastingPolicy.CanSpendMana(caster, manaCost)) continue;
            var targetPosition = ChooseNpcBuffTarget(battle, caster, casterPosition, spell, effects,
                spell.TargetType == SpellTargetType.PartyMember ? [caster] : allies, allowSelfBuff: true);
            if (targetPosition != casterPosition || ValidateSpellCast(caster, casterPosition, spell, true,
                    currentEnemy, explicitTarget: casterPosition) is not null) continue;
            return new NpcBattleSpellChoice(spell, casterPosition, currentEnemy, Offensive: false);
        }
        return null;
    }

    private NpcSpellcasterTactics NpcTacticsFor(LiveCharacter caster)
    {
        var defaults = NpcSpellcasterTactics.DefaultFor(caster.CharacterClass.Id);
        var configured = _npcSpellcasterTactics.GetValueOrDefault(caster.Id, defaults).Normalize();
        return caster.CharacterClass.Id == CharacterClassIds.Pap && configured.UnholyProfile is null
            ? configured with { UnholyProfile = defaults.UnholyProfile }
            : configured;
    }

    private Position? ChooseNpcBuffTarget(BattleEncounter battle, LiveCharacter caster,
        Position casterPosition, SpellDefinition spell, IReadOnlyList<SpellEffectDefinition> effects,
        IReadOnlyList<LiveCharacter> allies, bool allowSelfBuff = true) =>
        _battleCoordinator.ChooseNpcBuffTarget(battle, caster, casterPosition, spell, effects, allies,
            GetCasterPosition, (c, pos, sp, tgt, en) => IsValidExplicitSpellTarget(c, pos, sp, tgt, en),
            allowSelfBuff);

    private IEnumerable<Enemy> OrderedNpcSpellTargets(BattleEncounter battle, Position casterPosition) =>
        TacticalBattleCoordinator.OrderedNpcSpellTargets(battle, casterPosition);

    private static double VitalityRatio(LiveCharacter character) =>
        TacticalBattleCoordinator.VitalityRatio(character);
}
