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
    private void ResolveCharacterAttack(BattleEncounter battle, LiveCharacter character, Enemy enemy)
    {
        battle.RecordAttack(BattleSide.Friendly);
        var origin = GetCasterPosition(character);
        var activeWeapon = character.AttackWeapon;
        var rangedAttack = activeWeapon?.IsRanged == true;
        if (!rangedAttack && TacticalDistance.IsMeleeAdjacent(origin, enemy.Position))
            battle.Engage(character, enemy);
        var dualWielding = DualWieldingRules.TryGetWeapons(character, out var mainHand, out var offhand);
        var targets = rangedAttack
            ? new List<Enemy> { enemy }
            : TacticalBattleCoordinator.SweepTargets(battle, character, origin, enemy);
        var positionalDaggerHit = false;
        for (var index = 0; index < targets.Count; index++)
        {
            var target = targets[index];
            var rearFormationStrike = battle.RearFormationEnemiesInReach(character).Contains(target);
            var advantage = rangedAttack
                ? TacticalAttackAdvantage.Front
                : TacticalBattleCoordinator.AttackAdvantage(battle, character, target);
            if (!rangedAttack && TacticalDistance.IsMeleeAdjacent(origin, target.Position))
                battle.Engage(character, target);
            var before = target.CurrentHitPoints;
            var damagePercent = TacticalBattleCoordinator.SweepDamagePercent(character,
                battle.RuntimeFor(character), secondaryTarget: index > 0);
            var entry = _battleSystem.ResolveCharacterAttack(character, battle.RuntimeFor(character), target,
                finishAction: !dualWielding && index == targets.Count - 1, damagePercent: damagePercent,
                positionalHitBonus: advantage.HitBonus, positionalAdvantage: advantage.Name,
                tacticalBackstab: character.CharacterClass.Id == CharacterClassIds.Tolvaj &&
                                   battle.RuntimeFor(character).Tactic == BattleTactic.ThiefAmbush &&
                                   (advantage.IsRear || rearFormationStrike),
                attackWeapon: dualWielding ? mainHand : null,
                attackWeaponSlotIndex: dualWielding ? 0 : null,
                armorPenalty: battle.EnemyArmorPenalty(target),
                rangedHitModifier: RangedWeaponRules.CloseRangeModifier(character,
                    dualWielding ? mainHand : activeWeapon, TacticalDistance.Between(origin, target.Position)));
            var hit = target.CurrentHitPoints < before;

            if (_gameSettings.Settings.CombatSpeed == CombatSpeed.PauseAfterHit && hit)
            {
                battle.PauseReason = BattlePauseReason.AfterHit;
            }

            positionalDaggerHit |= hit && advantage.Arc != TacticalAttackArc.Front &&
                                   WeaponFamilies.ForWeapon(dualWielding ? mainHand : character.AttackWeapon) ==
                                   WeaponFamilies.Dagger;
            if (hit) ApplyWeaponTacticalHitEffects(battle, character, target,
                dualWielding ? mainHand : character.AttackWeapon, secondaryTarget: index > 0);
            PresentBattleEntries([entry]);
            if (target.CurrentHitPoints <= 0) ResolveEnemyDefeat(battle, target, character);
        }

        if (dualWielding && offhand is not null)
        {
            var offhandTarget = enemy.CurrentHitPoints > 0 && ReachableEnemies(battle, character).Contains(enemy)
                ? enemy
                : ReachableEnemies(battle, character).OrderBy(target => target.CurrentHitPoints).FirstOrDefault();
            if (offhandTarget is not null)
            {
                var advantage = TacticalBattleCoordinator.AttackAdvantage(battle, character, offhandTarget);
                if (TacticalDistance.IsMeleeAdjacent(GetCasterPosition(character), offhandTarget.Position))
                    battle.Engage(character, offhandTarget);
                var before = offhandTarget.CurrentHitPoints;
                var offhandEntry = _battleSystem.ResolveCharacterAttack(character, battle.RuntimeFor(character),
                    offhandTarget, finishAction: true, damagePercent: DualWieldingRules.OffhandDamagePercent,
                    positionalHitBonus: advantage.HitBonus, positionalAdvantage: advantage.Name,
                    attackWeapon: offhand, allowTriggeredExtraAttacks: false, allowAmbush: false,
                    attackWeaponSlotIndex: 1,
                    armorPenalty: battle.EnemyArmorPenalty(offhandTarget), damageScaleName: "Mellékkéz");
                var offhandHit = offhandTarget.CurrentHitPoints < before;

                if (_gameSettings.Settings.CombatSpeed == CombatSpeed.PauseAfterHit && offhandHit)
                {
                    battle.PauseReason = BattlePauseReason.AfterHit;
                }

                positionalDaggerHit |= offhandHit && advantage.Arc != TacticalAttackArc.Front &&
                                       WeaponFamilies.ForWeapon(offhand) == WeaponFamilies.Dagger;
                PresentBattleEntries([offhandEntry with { Message = $"⚔️ Mellékkéz — {offhandEntry.Message}" }]);
                if (offhandTarget.CurrentHitPoints <= 0)
                    ResolveEnemyDefeat(battle, offhandTarget, character);
            }
            else
            {
                var statusText = _battleSystem.FinishCharacterAction(character, battle.RuntimeFor(character));
                if (!string.IsNullOrEmpty(statusText))
                    PresentBattleEntries([new BattleLogEntry($"{character.Name}:{statusText}",
                        BattleLogKind.Information)]);
            }
        }

        if (positionalDaggerHit &&
            character.WeaponProficiencyRankFor(WeaponFamilies.Dagger) == WeaponProficiencyRank.Master &&
            battle.Disengage(character) > 0)
            PresentBattleEntries([new BattleLogEntry(
                $"🗡️ {character.Name} tőrmesterként az oldal-/hátbatámadás után kicsúszik a lekötésből.",
                BattleLogKind.Information)]);
        if (!character.IsAlive) ResolveCharacterDefeat(battle, character);
        AdvanceBattleTurn(battle);
    }

    private void ApplyWeaponTacticalHitEffects(BattleEncounter battle, LiveCharacter character,
        Enemy target, WeaponDefinition? weapon, bool secondaryTarget)
    {
        var family = WeaponFamilies.ForWeapon(weapon);
        if (family == WeaponFamilies.Axe && secondaryTarget &&
            character.WeaponProficiencyRankFor(family) == WeaponProficiencyRank.Master &&
            battle.ApplyArmorShred(target, 2))
            PresentBattleEntries([new BattleLogEntry(
                $"🪓 {character.Name} söprése megrepeszti {target.Name} páncélját: -2 páncél a csata végéig.",
                BattleLogKind.Information)]);

        if (family != WeaponFamilies.Blunt ||
            character.WeaponProficiencyRankFor(family) != WeaponProficiencyRank.Master) return;
        if (target.PreparedWeaponId is { } interrupted)
        {
            target.ClearPreparedWeapon();
            PresentBattleEntries([new BattleLogEntry(
                $"🔨 {character.Name} zúzó csapása megszakítja {target.Name} előkészített fegyverét ({interrupted}).",
                BattleLogKind.Information)]);
        }
        else if (battle.StaggerEnemy(target, StaggerSeverity.Light))
            PresentBattleEntries([new BattleLogEntry(
                $"🔨 {character.Name} könnyen megingatja {target.Name} ellenfelet.",
                BattleLogKind.Information)]);
    }

    private void ResolveCharacterShieldBash(BattleEncounter battle, LiveCharacter character,
        Enemy target, WeaponDefinition shield)
    {
        battle.Engage(character, target);
        var result = _battleSystem.ResolvePlayerShieldBash(character, target, shield);
        var actualOutcome = result.Outcome;
        var pushBlocked = false;
        if (result.Outcome == MonsterStrengthContestOutcome.Push)
        {
            if (!TryPushBattleEnemy(battle, character, target))
            {
                actualOutcome = MonsterStrengthContestOutcome.Stagger;
                pushBlocked = true;
                battle.StaggerEnemy(target, StaggerSeverity.Heavy);
            }
        }
        else if (result.Outcome == MonsterStrengthContestOutcome.Stagger)
            battle.StaggerEnemy(target, StaggerSeverity.Normal);

        if (result.Damage > 0)
            target.SetCurrentHitPoints(target.CurrentHitPoints - result.Damage);
        battle.RecordAttack(BattleSide.Friendly);
        var outcomeText = actualOutcome switch
        {
            MonsterStrengthContestOutcome.Push => "hátralökődik",
            MonsterStrengthContestOutcome.Stagger when pushBlocked => "nem tud hátralépni, ezért meginog",
            MonsterStrengthContestOutcome.Stagger => "meginog",
            _ => "ellenáll"
        };
        var statusText = _battleSystem.FinishCharacterAction(character, battle.RuntimeFor(character));
        PresentBattleEntries([new BattleLogEntry(
            $"🛡️ {character.Name} pajzzsal meglöki {target.Name} ellenfelet: {outcomeText}" +
            (result.Damage > 0 ? $", -{result.Damage} HP" : string.Empty) + $".{statusText}",
            BattleLogKind.PlayerAttack,
            BattleSystem.DescribeShieldBash(character.Name, target.Name, result, actualOutcome, pushBlocked))]);
        if (target.CurrentHitPoints <= 0) ResolveEnemyDefeat(battle, target, character);
        AdvanceBattleTurn(battle);
    }

    private void ExecuteEnemyTurn(BattleEncounter battle, Enemy enemy)
    {
        var turnStart = battle.ShouldAdvanceSpellEffects(CombatantId.ForEnemy(enemy.Id))
            ? _battleSystem.BeginEnemyTurn(enemy)
            : new EnemyTurnStartResult(true, []);
        if (turnStart.Entries.Count > 0) PresentBattleEntries(turnStart.Entries);
        if (enemy.CurrentHitPoints <= 0)
        {
            ResolveEnemyDefeat(battle, enemy, null);
            AdvanceBattleTurn(battle);
            return;
        }
        if (!turnStart.CanAct)
        {
            AdvanceBattleTurn(battle);
            return;
        }

        if (battle.AreOffensiveActionsBlocked(CombatantId.ForEnemy(enemy.Id)))
        {
            PresentBattleEntries([new BattleLogEntry(
                $"💫 {enemy.Name} megingása miatt nem tud támadni vagy képességet használni.",
                BattleLogKind.Information)]);
            AdvanceBattleTurn(battle);
            return;
        }

        var livingTargets = EnemyTargets(battle, enemy)
            .OrderBy(character => TacticalDistance.Between(enemy.Position, GetCasterPosition(character))).ToArray();
        var closestDistance = livingTargets.Length == 0 ? int.MaxValue :
            TacticalDistance.Between(enemy.Position, GetCasterPosition(livingTargets[0]));
        var spellPlan = enemy.PreparedWeaponId is null
            ? _enemySpellcastingService.SelectSpell(enemy,
                battle.Enemies.Where(candidate => candidate.CurrentHitPoints > 0).ToArray(),
                livingTargets.Select(target => (target, GetCasterPosition(target))).ToArray(),
                (origin, target, range) => FogOfWar.CanSee(_maze, origin, target, range))
            : null;
        if (spellPlan is not null)
        {
            if (spellPlan.HostileTargets.Count > 0) battle.FaceEnemyToward(enemy, spellPlan.HostileTargets[0]);
            PlaySpellImpact(spellPlan.Spell, enemy.Position, spellPlan.TargetPosition,
                spellPlan.HostileTargets.Select(GetCasterPosition).ToArray());
            PresentBattleEntries([_enemySpellcastingService.Execute(enemy, spellPlan)]);
            if (spellPlan.HostileTargets.Count > 0) battle.RecordAttack(BattleSide.Hostile);
            foreach (var target in spellPlan.HostileTargets.Where(target => !target.IsAlive))
                ResolveCharacterDefeat(battle, target);
            AdvanceBattleTurn(battle);
            return;
        }
        var activeAbility = enemy.PreparedWeaponId is null
            ? _battleSystem.SelectEnemyActiveAbility(enemy, closestDistance, ability =>
                (!ability.UsesRangedAttackRoll || !battle.IsEngaged(enemy) ||
                 enemy.AttackWeapons.All(weapon => weapon.IsRanged)) &&
                livingTargets.Any(target => EnemyAbilityCanTarget(enemy, target, ability)))
            : null;
        if (activeAbility is not null)
        {
            var abilityTargets = livingTargets.Where(character => EnemyAbilityCanTarget(enemy, character,
                    activeAbility))
                .Take(activeAbility.MaximumTargets).ToArray();
            if (abilityTargets.Length > 0) battle.FaceEnemyToward(enemy, abilityTargets[0]);
            var abilityEntries = abilityTargets.Select((target, index) =>
            {
                var entry = _battleSystem.ResolveEnemyAbility(enemy, target, battle.RuntimeFor(target), activeAbility,
                    consumeResources: index == 0,
                    targetDistance: TacticalDistance.Between(enemy.Position, GetCasterPosition(target)));
                if (entry.Kind != BattleLogKind.Information &&
                    activeAbility.Effects.FirstOrDefault(effect => effect.Effect == MonsterAbilityEffect.Stagger)
                        is { } stagger)
                {
                    battle.StaggerCharacter(target, stagger.Value >= 3 ? StaggerSeverity.Heavy :
                        stagger.Value >= 2 ? StaggerSeverity.Normal : StaggerSeverity.Light);
                    entry = entry with { Message = entry.Message + $" {target.Name} meginog." };
                }
                return entry;
            }).ToArray();
            PresentBattleEntries(abilityEntries);
            battle.RecordAttack(BattleSide.Hostile);
            foreach (var target in abilityTargets.Where(target => !target.IsAlive))
                ResolveCharacterDefeat(battle, target);
            if (activeAbility.RetreatStepsAfterUse > 0 && !battle.IsEngaged(enemy))
                TryRetreatEnemyAfterAbility(battle, enemy, abilityTargets,
                    activeAbility.RetreatStepsAfterUse);
            AdvanceBattleTurn(battle);
            return;
        }

        var bashTarget = enemy.EquippedShield is { ShieldTier: > 0 } &&
                         _random.Next(100) < (battle.HasActiveFormation ? 35 : 20)
            ? livingTargets.FirstOrDefault(target =>
                TacticalDistance.IsMeleeAdjacent(enemy.Position, GetCasterPosition(target)))
            : null;
        if (bashTarget is not null)
        {
            battle.FaceEnemyToward(enemy, bashTarget);
            battle.Engage(bashTarget, enemy);
            var bash = _battleSystem.ResolveEnemyShieldBash(enemy, bashTarget);
            var actualOutcome = bash.Outcome;
            var pushBlocked = false;
            if (bash.Outcome == MonsterStrengthContestOutcome.Push)
            {
                if (!TryPushBattleTarget(battle, enemy, bashTarget, out _))
                {
                    actualOutcome = MonsterStrengthContestOutcome.Stagger;
                    pushBlocked = true;
                    battle.StaggerCharacter(bashTarget, StaggerSeverity.Heavy);
                }
            }
            else if (bash.Outcome == MonsterStrengthContestOutcome.Stagger)
                battle.StaggerCharacter(bashTarget, StaggerSeverity.Normal);
            if (bash.Damage > 0) bashTarget.ReceiveDamage(bash.Damage);
            battle.RecordAttack(BattleSide.Hostile);
            var outcomeText = actualOutcome switch
            {
                MonsterStrengthContestOutcome.Push => "hátralöki",
                MonsterStrengthContestOutcome.Stagger when pushBlocked => "a falnak löki és megingatja",
                MonsterStrengthContestOutcome.Stagger => "megingatja",
                _ => "nem tudja kimozdítani"
            };
            PresentBattleEntries([new BattleLogEntry(
                $"🛡️ {enemy.Name} pajzslökéssel {outcomeText} {bashTarget.Name} karaktert" +
                (bash.Damage > 0 ? $", -{bash.Damage} HP." : "."),
                BattleLogKind.EnemyAttack,
                BattleSystem.DescribeShieldBash(enemy.Name, bashTarget.Name, bash, actualOutcome, pushBlocked))]);
            if (!bashTarget.IsAlive) ResolveCharacterDefeat(battle, bashTarget);
            AdvanceBattleTurn(battle);
            return;
        }

        var attackWeapon = _battleSystem.SelectEnemyAttackWeapon(enemy, weapon =>
            TacticalBattleCoordinator.EnemyAttackTargets(battle, enemy, weapon, GetCasterPosition,
                HasBattleLineOfSight).Count, closestDistance);
        if (attackWeapon?.IsRanged == true && !battle.IsEngaged(enemy) &&
            !battle.IsMovementBlocked(CombatantId.ForEnemy(enemy.Id)) &&
            closestDistance < PreferredEnemyRangedDistance(attackWeapon) &&
            TryMoveEnemyToPreferredRangedPosition(battle, enemy, attackWeapon, livingTargets))
            return;
        var targets = TacticalBattleCoordinator.EnemyAttackTargets(battle, enemy, attackWeapon,
            GetCasterPosition, HasBattleLineOfSight);
        if (targets.Count == 0)
        {
            var target = EnemyTargets(battle, enemy)
                .OrderBy(character => TacticalDistance.Between(enemy.Position, GetCasterPosition(character)))
                .First();
            MoveEnemyToward(battle, enemy, GetCasterPosition(target), attackWeapon);
            return;
        }
        battle.FaceEnemyToward(enemy, targets[0]);
        if (BattleSystem.IsTelegraphedWeapon(attackWeapon) && !enemy.IsWeaponPrepared(attackWeapon!.Id))
        {
            PresentBattleEntries([_battleSystem.PrepareEnemyWeapon(enemy, attackWeapon)]);
            AdvanceBattleTurn(battle);
            return;
        }

        var entries = new List<BattleLogEntry>();
        for (var index = 0; index < targets.Count; index++)
        {
            var target = targets[index];
            var meleeAttack = attackWeapon?.IsRanged != true &&
                              TacticalDistance.IsMeleeAdjacent(enemy.Position, GetCasterPosition(target));
            if (meleeAttack) battle.Engage(target, enemy);
            var resolution = _battleSystem.ResolveEnemyActionDetailed(enemy, target, battle.RuntimeFor(target),
                attackWeapon, advanceAttackerEffects: index == 0,
                alliedGuardDefense: TacticalBattleCoordinator.AlliedGuardDefense(
                    battle, target, GetCasterPosition),
                rangedHitModifier: EnemyRangedHitModifier(attackWeapon,
                    TacticalDistance.Between(enemy.Position, GetCasterPosition(target))));

            if (_gameSettings.Settings.CombatSpeed == CombatSpeed.PauseAfterHit && resolution.Hit)
            {
                battle.PauseReason = BattlePauseReason.AfterHit;
            }

            var entry = resolution.Entry;
            entries.Add(entry);
            if (entry.Kind is BattleLogKind.EnemyAttack or BattleLogKind.CriticalHit)
                battle.RecordAttack(BattleSide.Hostile);
            if (meleeAttack && resolution.Hit && resolution.DamageDealt > 0 && target.IsAlive &&
                battle.TryBeginStrengthContest(enemy))
            {
                var pressure = ResolveMonsterStrengthPressure(battle, enemy, target);
                if (pressure.LogEntry is { } strengthEntry)
                    entries.Add(strengthEntry);
                else
                {
                    var attackIndex = entries.Count - 1;
                    entries[attackIndex] = entries[attackIndex] with
                    {
                        Details = AppendBattleActionDetails(entries[attackIndex].Details, pressure.Details)
                    };
                }
            }
            if (!target.IsAlive) ResolveCharacterDefeat(battle, target);
            if (enemy.CurrentHitPoints <= 0 || entry.Kind == BattleLogKind.Information) break;
        }
        _battleSystem.MarkEnemyWeaponUsed(enemy, attackWeapon);
        PresentBattleEntries(entries);
        if (enemy.CurrentHitPoints <= 0) ResolveEnemyDefeat(battle, enemy, null);
        AdvanceBattleTurn(battle);
    }

    private void AdvanceBattleTurn(BattleEncounter battle)
    {
        ResetBattleMovement();
        battle.CaptureNewStatuses();
        if (battle.IsCompleted)
        {
            battle.RecordCompletedFinalAction();
            return;
        }
        battle.AdvanceTurn();
        _preparedBattleTurnId = 0;
    }

    private void ExecuteRetreat(BattleEncounter battle, LiveCharacter character)
    {
        if (character != PartyLeader || battle.Turns.Cycle <= 1)
        {
            _renderer.DrawInventoryMessage("A visszavonulást csak a vezér rendelheti el a nyitó ütésváltás után.",
                ConsoleColor.Red);
            return;
        }

        foreach (var retreatingCharacter in battle.Characters.Where(candidate => candidate.IsAlive))
        {
            var attacker = battle.EngagedEnemies(retreatingCharacter)
                .Where(enemy => TacticalDistance.IsMeleeAdjacent(enemy.Position,
                    GetCasterPosition(retreatingCharacter)))
                .OrderByDescending(enemy => enemy.EffectiveSpeed).FirstOrDefault();
            if (attacker is null) continue;
            var entry = _battleSystem.ResolveEnemyAttackOnRetreatingCharacter(attacker, retreatingCharacter,
                battle.RuntimeFor(retreatingCharacter),
                TacticalDistance.Between(attacker.Position, GetCasterPosition(retreatingCharacter)));
            PresentBattleEntries([entry]);
            if (!retreatingCharacter.IsAlive) ResolveCharacterDefeat(battle, retreatingCharacter);
        }
        var statusText = _battleSystem.FinishCharacterAction(character, battle.RuntimeFor(character));
        if (!string.IsNullOrEmpty(statusText))
            PresentBattleEntries([new BattleLogEntry($"{character.Name} visszavonulási kísérlete.{statusText}",
                BattleLogKind.Information)]);
        if (!character.IsAlive) ResolveCharacterDefeat(battle, character);
        if (!PartyLeader.IsAlive)
        {
            FinishBattle(battle, forceDefeat: true);
            return;
        }

        var livingCharacters = battle.Characters.Where(candidate => candidate.IsAlive).ToArray();
        var livingEnemies = battle.Enemies.Where(enemy => enemy.CurrentHitPoints > 0).ToArray();
        var friendlySpeed = livingCharacters
            .Select(candidate => CharacterMobilityRules.Evaluate(candidate).CombatMovementAllowance)
            .DefaultIfEmpty(0).Min();
        var hostileSpeed = livingEnemies
            .Select(EnemyMovementAllowance).DefaultIfEmpty(0).Max();
        var everyEnemyHasTwoCellGap = livingEnemies.All(enemy => livingCharacters.All(candidate =>
            TacticalDistance.Between(GetCasterPosition(candidate), enemy.Position) >= 3));
        var perceptionSources = livingCharacters.Select(candidate => new PartyPerceptionSource(
            GetCasterPosition(candidate),
            CharacterClassRules.VisionRange(candidate, CurrentLevelVisionModifier),
            CharacterClassRules.HearingRange(candidate),
            CharacterClassRules.DetectionBonus(candidate))).ToArray();
        var anyEnemyVisible = livingEnemies.Any(enemy => perceptionSources.Any(source =>
            FogOfWar.CanDetectEnemyFrom(_maze, source, enemy)));
        var canBreakPursuit = TacticalBattleCoordinator.CanRetreat(
            friendlySpeed, hostileSpeed, everyEnemyHasTwoCellGap, anyEnemyVisible);
        Dictionary<LiveCharacter, Position> destinations = [];
        var hasSafeRoute = canBreakPursuit && TryFindRetreatDestinations(battle, out destinations);
        if (!hasSafeRoute)
        {
            var failed = canBreakPursuit
                ? "🏃 A visszavonulás nem sikerült: nincs elérhető biztonságos visszavonulási hely."
                : friendlySpeed == hostileSpeed
                ? "🏃 A visszavonulás nem sikerült: azonos sebességnél legalább két mezőnek kell lennie " +
                  "minden partitag és ellenség között."
                : $"🏃 A visszavonulás nem sikerült: a csapat sebessége {friendlySpeed}, " +
                  $"az üldözőké {hostileSpeed}, és még van ellenség a parti látóterében.";
            _renderer.DrawInventoryMessage(failed, ConsoleColor.Red);
            RecordSessionActivity(SessionActivityKind.Battle, failed, ConsoleColor.Red);
            AdvanceBattleTurn(battle);
            ContinueBattle();
            return;
        }

        foreach (var (retreatingCharacter, destination) in destinations)
        {
            if (retreatingCharacter == PartyLeader) _player.TeleportTo(destination);
            else if (_maze.PartyMembers.FirstOrDefault(member => member.Character == retreatingCharacter) is { } avatar)
                avatar.MoveTo(destination);
            battle.UpdatePosition(retreatingCharacter, destination);
            battle.MarkRetreated(retreatingCharacter);
            RevealFor(retreatingCharacter, destination);
        }
        var reason = friendlySpeed > hostileSpeed
            ? "A csapat gyorsabb az üldözőknél."
            : friendlySpeed == hostileSpeed
                ? "A kétmezőnyi távolsági előny elég az elszakadáshoz."
                : "Az üldözők gyorsabbak, de már egyikük sincs a parti látóterében.";
        FinishSuccessfulRetreat(battle, friendlySpeed, hostileSpeed, reason);
    }

    private bool TryFindRetreatDestinations(BattleEncounter battle,
        out Dictionary<LiveCharacter, Position> destinations)
    {
        destinations = [];
        var occupied = battle.Turns.Participants.Where(participant => participant.Side == BattleSide.Hostile &&
                participant.State is TacticalParticipantState.Active or TacticalParticipantState.Approaching)
            .Select(participant => participant.Position).ToHashSet();
        var friendlyCharacters = battle.Characters.ToHashSet();
        foreach (var character in battle.Characters.Where(candidate => candidate.IsAlive)
                     .OrderByDescending(candidate => candidate == PartyLeader))
        {
            var origin = GetCasterPosition(character);
            occupied.Remove(origin);
            var queue = new Queue<(Position Position, int Distance)>();
            var visited = new HashSet<Position> { origin };
            queue.Enqueue((origin, 0));
            Position? destination = null;
            while (queue.Count > 0)
            {
                var (position, distance) = queue.Dequeue();
                if (distance > 0 && battle.Enemies.Where(enemy => enemy.CurrentHitPoints > 0)
                        .All(enemy => TacticalDistance.Between(position, enemy.Position) >= 3))
                {
                    destination = position;
                    break;
                }
                if (distance >= 8) continue;
                foreach (var direction in Directions)
                {
                    var next = position + direction;
                    if (!visited.Add(next) || occupied.Contains(next) || !_maze.IsWalkable(next)) continue;
                    var mapObject = _maze.GetObjectAt(next);
                    if (mapObject is not null and not GroundItemPile and not Corpse &&
                        !(mapObject is PartyMemberAvatar member && friendlyCharacters.Contains(member.Character))) continue;
                    queue.Enqueue((next, distance + 1));
                }
            }
            if (destination is null) return false;
            destinations.Add(character, destination.Value);
            occupied.Add(destination.Value);
        }
        return true;
    }

    private void FinishSuccessfulRetreat(BattleEncounter battle, int friendlySpeed, int hostileSpeed,
        string reason)
    {
        battle.RecordCompletedFinalAction();
        var cycles = Math.Max(1, battle.Turns.Cycle);
        var characterResults = battle.Characters.Select(battle.ResultFor).ToArray();
        var summary = ConsoleRenderer.FormatBattleRetreatSummary(cycles, battle.ActionNumber, battle.Kills);
        var resourceSummary = ConsoleRenderer.FormatBattleResourceSummary(characterResults, cycles);
        if (_locationId == DeveloperBattleTestLocationId)
            _developerBattleLog.CompleteBattle(battle,
                $"retreat; friendlySpeed={friendlySpeed}; hostileSpeed={hostileSpeed}; reason={reason}");
        _session.EndBattle(battle.Id);
        foreach (var character in battle.Characters.Where(character => character.IsAlive))
            DrainNeedsAfterBattle(character, cycles);
        ResetBattleMovement();
        _activeBattle = null;
        _isQuickBattle = false;
        _preparedBattleTurnId = 0;
        _battleStarted = false;
        _saveAfterBattle = false;
        _session.SetPhase(GameSessionPhase.Exploration);
        _nextNeedsDrain = DateTime.UtcNow + TimeSpan.FromMinutes(1);
        InitializeEnemyMoveSchedule(DateTime.UtcNow + TimeSpan.FromSeconds(2));
        foreach (var member in _maze.PartyMembers) ScheduleNextPartyMove(member, DateTime.UtcNow);
        var message = $"🏃 Sikeres visszavonulás: a csapat sebessége {friendlySpeed}, " +
                      $"az üldözőké {hostileSpeed}. {reason}";
        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
        _renderer.DrawInventoryMessage(summary, ConsoleColor.DarkYellow);
        RecordSessionActivity(SessionActivityKind.Battle, summary, ConsoleColor.DarkYellow);
        var details = $"Eredmény: {resourceSummary}";
        _renderer.DrawInventoryMessage(details, ConsoleColor.Cyan);
        RecordSessionActivity(SessionActivityKind.Battle, details, ConsoleColor.Cyan);
        _renderer.DrawInventoryMessage(message, ConsoleColor.Green);
        RecordSessionActivity(SessionActivityKind.Battle, message, ConsoleColor.Green);
        foreach (var (levelingCharacter, result) in _pendingLevelUps.ToArray())
            ResolvePerkOffers(levelingCharacter, result);
        _pendingLevelUps.Clear();
        _renderer.RestoreAfterBattle();
        RequestCoopSnapshotPublish();
    }

    private IReadOnlyList<BattleActionKind> GetAllowedBattleActions(BattleEncounter battle,
        LiveCharacter character, Enemy focusEnemy)
    {
        if (IsBattleMovementInProgress(battle)) return [BattleActionKind.Move, BattleActionKind.Pass];
        return _battleCoordinator.GetAllowedBattleActions(battle, character, focusEnemy, PartyLeader,
            GetCasterPosition(character), HasUsableCombatSpell(character, GetCasterPosition(character), focusEnemy),
            _turnUndeadNextAvailableRounds, HasBattleLineOfSight);
    }

    private IReadOnlyList<BattleTacticOptionSnapshot>? GetBattleTacticOptions(BattleEncounter battle,
        LiveCharacter character, Enemy enemy) =>
        _battleCoordinator.GetBattleTacticOptions(battle, character, enemy);

    private void SetBattlePrompt(BattleEncounter battle)
    {
        var prompt = CreateBattlePromptState(battle);
        if (!_session.SetBattlePrompt(prompt.BattleId, prompt.TurnId, prompt.ActingCharacterId,
                prompt.AllowedActions))
            return;

        var currentActorName = prompt.ActingCharacter?.Name ?? battle.CurrentEnemy?.Name ?? prompt.FocusEnemy.Name;
        var isHumanControlled = prompt.ActingCharacter is not null &&
                                _session.IsHumanControlled(prompt.ActingCharacter.Id);
        var tacticOptions = !prompt.IsPaused &&
                            prompt.ActingCharacter is not null &&
                            battle.RuntimeFor(prompt.ActingCharacter).RequiresTacticSelection
            ? prompt.TacticOptions
            : null;
        var message = BattleCommandPanel.Format(prompt.AllowedActions, isHumanControlled, currentActorName,
            tacticOptions, PhysicalAttackLabel(prompt.ActingCharacter));
        _renderer.DrawBattleCommandPanel(message);
        RequestCoopSnapshotPublish();
    }

    private static string? PhysicalAttackLabel(LiveCharacter? character)
    {
        var weapon = character?.AttackWeapon;
        if (weapon?.IsRanged != true) return null;
        if (!weapon.UsesAmmunition) return "lövés";
        var ammunition = RangedWeaponRules.AmmunitionCount(character!, weapon);
        var ammunitionName = string.Equals(weapon.AmmunitionItemId, AmmunitionIds.Arrow,
            StringComparison.OrdinalIgnoreCase) ? "nyíl" : "lövedék";
        return $"lövés ({ammunition} {ammunitionName})";
    }

    private IEnumerable<Enemy> AdjacentEnemies(BattleEncounter battle, LiveCharacter character) =>
        TacticalBattleCoordinator.AdjacentEnemies(battle, character, GetCasterPosition(character));

    private IEnumerable<Enemy> ReachableEnemies(BattleEncounter battle, LiveCharacter character) =>
        TacticalBattleCoordinator.ReachableEnemies(battle, character, GetCasterPosition(character),
            HasBattleLineOfSight);

    private bool HasBattleLineOfSight(Position origin, Position target, int range) =>
        FogOfWar.CanSee(_maze, origin, target, range);

    private IEnumerable<Enemy> TurnUndeadTargets(BattleEncounter battle, LiveCharacter character) =>
        TacticalBattleCoordinator.TurnUndeadTargets(battle, character, GetCasterPosition(character));

    private Enemy? PreferredTurnUndeadTarget(BattleEncounter battle, LiveCharacter character)
    {
        var targets = TurnUndeadTargets(battle, character).ToArray();
        return battle.SelectedTargetEnemy() is { } selected && targets.Contains(selected)
            ? selected : targets.FirstOrDefault();
    }

    private IEnumerable<LiveCharacter> AdjacentCharacters(BattleEncounter battle, Enemy enemy) =>
        TacticalBattleCoordinator.AdjacentCharacters(battle, enemy, GetCasterPosition);

    private IEnumerable<LiveCharacter> EnemyTargets(BattleEncounter battle, Enemy enemy) =>
        TacticalBattleCoordinator.EnemyTargets(battle, enemy);

    private static IEnumerable<Position> MeleePositions(Position center) =>
        TacticalBattleCoordinator.MeleePositions(center);

    private CombatantId? BattleFocusTarget(BattleEncounter battle, TacticalBattleParticipant current)
    {
        if (battle.CharacterFor(current.Id) is { } character)
        {
            var enemy = battle.SelectedTargetEnemy() ??
                        ReachableEnemies(battle, character).OrderBy(candidate => candidate.CurrentHitPoints)
                .FirstOrDefault() ?? battle.Enemies.Where(candidate => candidate.CurrentHitPoints > 0)
                .OrderBy(candidate => TacticalDistance.Between(current.Position, candidate.Position)).FirstOrDefault();
            return enemy is null ? null : CombatantId.ForEnemy(enemy.Id);
        }
        if (battle.EnemyFor(current.Id) is not { } actingEnemy) return null;
        var target = AdjacentCharacters(battle, actingEnemy)
            .OrderBy(candidate => (double)candidate.CurrentVitality / Math.Max(1, candidate.MaximumVitality))
            .FirstOrDefault() ?? EnemyTargets(battle, actingEnemy)
            .OrderBy(candidate => TacticalDistance.Between(current.Position, GetCasterPosition(candidate))).FirstOrDefault();
        return target is null ? null : CombatantId.ForCharacter(target.Id);
    }

    private Enemy? NextBattleTarget(BattleEncounter battle, LiveCharacter character,
        IReadOnlyCollection<BattleActionKind> allowed)
    {
        var targets = SelectableEnemies(battle, character, allowed)
            .OrderBy(enemy => enemy.Position.Y)
            .ThenBy(enemy => enemy.Position.X)
            .ThenBy(enemy => enemy.Id.ToString(), StringComparer.Ordinal)
            .ToArray();
        if (targets.Length == 0) return null;
        var selectedIndex = battle.SelectedTargetEnemyId is { } selectedId
            ? Array.FindIndex(targets, enemy => enemy.Id == selectedId)
            : -1;
        return targets[(selectedIndex + 1) % targets.Length];
    }

    private void UpdateBattleFocus(BattleEncounter battle, TacticalBattleParticipant current)
    {
        if (_isQuickBattle) return;
        _renderer.UpdateBattleConditions(_maze, _fogOfWar, _player.Position,
            battle.Characters.Where(battle.IsCharacterStaggered).Select(character => character.Id),
            battle.Enemies.Where(battle.IsEnemyStaggered).Select(enemy => enemy.Id),
            battle.Characters.Where(character => battle.RuntimeFor(character).IsBarbarianRaging)
                .Select(character => character.Id));
        _renderer.DrawTacticalBattleActor(battle.CharacterFor(current.Id), battle.EnemyFor(current.Id));
        var targetId = BattleFocusTarget(battle, current);
        var targetPosition = targetId is { } id ? battle.Turns.Find(id)?.Position : null;
        _renderer.DrawBattleFocus(_maze, _fogOfWar, _player.Position, current.Position, targetPosition);
    }

    private bool CanEnemyActMeaningfully(BattleEncounter battle, Enemy enemy)
    {
        if (battle.AreOffensiveActionsBlocked(CombatantId.ForEnemy(enemy.Id))) return false;
        var possibleWeapons = enemy.EquippedWeapon is { } selected
            ? new[] { selected }
            : enemy.AttackWeapons;
        if (possibleWeapons.Any(weapon => TacticalBattleCoordinator.EnemyAttackTargets(
                battle, enemy, weapon, GetCasterPosition, HasBattleLineOfSight).Count > 0)) return true;
        if (battle.IsEngaged(enemy)) return false;
        var target = EnemyTargets(battle, enemy)
            .OrderBy(character => TacticalDistance.Between(enemy.Position, GetCasterPosition(character)))
            .First();
        var goals = possibleWeapons.SelectMany(weapon =>
                EnemyApproachPositions(battle, enemy, GetCasterPosition(target), weapon))
            .Distinct().ToArray();
        return FindBattlePath(battle, enemy.Position, goals, CombatantId.ForEnemy(enemy.Id)).Count > 0;
    }

    private Enemy ClosestLivingEnemy(BattleEncounter battle, Position origin) =>
        TacticalBattleCoordinator.ClosestLivingEnemy(battle, origin);

    private void MoveCharacterToward(BattleEncounter battle, LiveCharacter character, Position target,
        WeaponDefinition? attackWeapon)
    {
        var actorId = CombatantId.ForCharacter(character.Id);
        var goals = CharacterApproachPositions(battle, target, attackWeapon, actorId);
        var path = FindBattlePath(battle, GetCasterPosition(character), goals,
            actorId);
        CompleteCharacterMovement(battle, character, path);
    }

    private IReadOnlyList<Position> CharacterApproachPositions(BattleEncounter battle, Position target,
        WeaponDefinition? weapon, CombatantId actorId)
    {
        if (weapon?.IsRanged != true)
            return MeleePositions(target).Where(position => CanBattleEnter(battle, position, actorId)).ToArray();

        var positions = new List<Position>();
        for (var y = target.Y - weapon.MaximumRange; y <= target.Y + weapon.MaximumRange; y++)
        for (var x = target.X - weapon.MaximumRange * TacticalDistance.HorizontalCellsPerUnit;
             x <= target.X + weapon.MaximumRange * TacticalDistance.HorizontalCellsPerUnit; x++)
        {
            var position = new Position(x, y);
            if (!RangedWeaponRules.CanReach(weapon, TacticalDistance.Between(position, target)) ||
                !CanBattleEnter(battle, position, actorId) ||
                !HasBattleLineOfSight(position, target, weapon.MaximumRange)) continue;
            positions.Add(position);
        }
        return positions;
    }

    private void MoveEnemyToward(BattleEncounter battle, Enemy enemy, Position target,
        WeaponDefinition? attackWeapon)
    {
        if (battle.IsMovementBlocked(CombatantId.ForEnemy(enemy.Id)))
        {
            PresentBattleEntries([new BattleLogEntry(
                $"💫 {enemy.Name} megingott, ezért ebben az akcióban nem tud közeledni.",
                BattleLogKind.Information)]);
            AdvanceBattleTurn(battle);
            return;
        }
        var goals = EnemyApproachPositions(battle, enemy, target, attackWeapon);
        var path = FindBattlePath(battle, enemy.Position, goals, CombatantId.ForEnemy(enemy.Id));
        var traversed = path.Take(battle.Current.MovementAllowance).ToArray();
        LiveCharacter? interceptor = null;
        for (var index = 0; index < traversed.Length; index++)
        {
            interceptor = TacticalBattleCoordinator.PolearmMasterControlling(battle, traversed[index]);
            if (interceptor is null) continue;
            traversed = traversed.Take(index + 1).ToArray();
            break;
        }
        var landingIndex = Array.FindLastIndex(traversed, position =>
            CanBattleEnter(battle, position, CombatantId.ForEnemy(enemy.Id)));
        var steps = landingIndex < 0 ? Array.Empty<Position>() : traversed.Take(landingIndex + 1).ToArray();
        var previousPosition = enemy.Position;
        if (steps.Length > 0)
        {
            battle.RecordMovement(BattleSide.Hostile);
            enemy.MoveTo(steps[^1]);
            battle.UpdatePosition(enemy);
            if (!_isQuickBattle)
                _renderer.DrawEnemyMovement(_maze, _fogOfWar, previousPosition, enemy.Position, _player.Position);
        }
        if (steps.Length > 0)
            PresentBattleEntries([new BattleLogEntry($"{enemy.Name} {steps.Length} mezőt közeledik.",
                BattleLogKind.Information)]);
        if (interceptor is not null && steps.Length > 0 &&
            TacticalDistance.IsMeleeAdjacent(battle.PositionOf(interceptor), enemy.Position))
        {
            battle.Engage(interceptor, enemy);
            PresentBattleEntries([new BattleLogEntry(
                $"🔱 {interceptor.Name} szálfegyverrel feltartóztatja {enemy.Name} előrenyomulását.",
                BattleLogKind.Information)]);
        }
        AdvanceBattleTurn(battle);
    }

    private static int PreferredEnemyRangedDistance(WeaponDefinition weapon) =>
        Math.Clamp(weapon.MaximumRange - 1, 2, weapon.MaximumRange);

    private bool EnemyAbilityCanTarget(Enemy enemy, LiveCharacter target, MonsterAbilityDefinition ability)
    {
        var targetPosition = GetCasterPosition(target);
        return TacticalDistance.Between(enemy.Position, targetPosition) <= ability.Range &&
               (!ability.RequiresLineOfSight ||
                HasBattleLineOfSight(enemy.Position, targetPosition, ability.Range));
    }

    private static int EnemyRangedHitModifier(WeaponDefinition? weapon, int distance) =>
        weapon is { IsRanged: true } && distance <= 1 ? RangedWeaponRules.CloseRangeHitPenalty : 0;

    private bool TryMoveEnemyToPreferredRangedPosition(BattleEncounter battle, Enemy enemy,
        WeaponDefinition weapon, IReadOnlyList<LiveCharacter> targets)
    {
        if (targets.Count == 0) return false;
        var origin = enemy.Position;
        var actorId = CombatantId.ForEnemy(enemy.Id);
        var allowance = Math.Max(1, battle.Current.MovementAllowance);
        var currentSafety = targets.Min(target => TacticalDistance.Between(origin, GetCasterPosition(target)));
        var preferred = PreferredEnemyRangedDistance(weapon);
        var candidates = new List<(IReadOnlyList<Position> Path, int Safety, int VisibleTargets)>();
        for (var y = Math.Max(0, origin.Y - allowance); y <= Math.Min(_maze.Height - 1, origin.Y + allowance); y++)
        for (var x = Math.Max(0, origin.X - allowance * TacticalDistance.HorizontalCellsPerUnit);
             x <= Math.Min(_maze.Width - 1, origin.X + allowance * TacticalDistance.HorizontalCellsPerUnit); x++)
        {
            var position = new Position(x, y);
            if (position == origin || !CanBattleEnter(battle, position, actorId)) continue;
            var path = FindBattlePath(battle, origin, [position], actorId);
            if (path.Count == 0 || path.Count > allowance) continue;
            var visibleTargets = targets.Count(target =>
                RangedWeaponRules.CanReach(weapon,
                    TacticalDistance.Between(position, GetCasterPosition(target))) &&
                HasBattleLineOfSight(position, GetCasterPosition(target), weapon.MaximumRange));
            if (visibleTargets == 0) continue;
            var safety = targets.Min(target => TacticalDistance.Between(position, GetCasterPosition(target)));
            if (safety <= currentSafety) continue;
            candidates.Add((path, safety, visibleTargets));
        }
        var selected = candidates.OrderByDescending(candidate => Math.Min(candidate.Safety, preferred))
            .ThenByDescending(candidate => candidate.VisibleTargets)
            .ThenBy(candidate => candidate.Path.Count).FirstOrDefault();
        if (selected.Path is null) return false;
        var previousPosition = enemy.Position;
        var steps = selected.Path.Take(allowance).ToArray();
        if (steps.Length == 0) return false;
        battle.RecordMovement(BattleSide.Hostile);
        enemy.MoveTo(steps[^1]);
        battle.UpdatePosition(enemy);
        if (!_isQuickBattle)
            _renderer.DrawEnemyMovement(_maze, _fogOfWar, previousPosition, enemy.Position, _player.Position);
        PresentBattleEntries([new BattleLogEntry(
            $"{enemy.Name} {steps.Length} mezőt hátrál egy jobb tüzelőállásba.",
            BattleLogKind.Information)]);
        AdvanceBattleTurn(battle);
        return true;
    }

    private bool TryRetreatEnemyAfterAbility(BattleEncounter battle, Enemy enemy,
        IReadOnlyList<LiveCharacter> targets, int maximumSteps)
    {
        if (targets.Count == 0 || maximumSteps <= 0) return false;
        var actorId = CombatantId.ForEnemy(enemy.Id);
        var origin = enemy.Position;
        var currentSafety = targets.Min(target => TacticalDistance.Between(origin, GetCasterPosition(target)));
        var candidates = new List<(Position Position, IReadOnlyList<Position> Path, int Safety)>();
        for (var y = Math.Max(0, origin.Y - maximumSteps); y <= Math.Min(_maze.Height - 1, origin.Y + maximumSteps); y++)
        for (var x = Math.Max(0, origin.X - maximumSteps * TacticalDistance.HorizontalCellsPerUnit);
             x <= Math.Min(_maze.Width - 1, origin.X + maximumSteps * TacticalDistance.HorizontalCellsPerUnit); x++)
        {
            var position = new Position(x, y);
            if (position == origin || !CanBattleEnter(battle, position, actorId)) continue;
            var path = FindBattlePath(battle, origin, [position], actorId);
            if (path.Count == 0 || path.Count > maximumSteps) continue;
            var safety = targets.Min(target => TacticalDistance.Between(position, GetCasterPosition(target)));
            if (safety > currentSafety) candidates.Add((position, path, safety));
        }
        var selected = candidates
            .OrderByDescending(candidate => candidate.Safety)
            .ThenBy(candidate => candidate.Path.Count)
            .FirstOrDefault();
        if (selected.Path is null) return false;
        var destination = selected.Position;
        battle.RecordMovement(BattleSide.Hostile);
        enemy.MoveTo(destination);
        battle.UpdatePosition(enemy);
        if (!_isQuickBattle)
            _renderer.DrawEnemyMovement(_maze, _fogOfWar, origin, destination, _player.Position);
        PresentBattleEntries([new BattleLogEntry(
            $"{enemy.Name} a lövés után fedezékbe húzódik.", BattleLogKind.Information)]);
        return true;
    }

    private IReadOnlyList<Position> EnemyApproachPositions(BattleEncounter battle, Enemy enemy,
        Position target, WeaponDefinition? weapon)
    {
        var actorId = CombatantId.ForEnemy(enemy.Id);
        if (weapon?.IsRanged != true)
            return MeleePositions(target)
                .Where(position => CanBattleEnter(battle, position, actorId))
                .ToArray();

        var positions = new List<Position>();
        for (var y = target.Y - weapon.MaximumRange; y <= target.Y + weapon.MaximumRange; y++)
        for (var x = target.X - weapon.MaximumRange * TacticalDistance.HorizontalCellsPerUnit;
             x <= target.X + weapon.MaximumRange * TacticalDistance.HorizontalCellsPerUnit; x++)
        {
            var position = new Position(x, y);
            var distance = TacticalDistance.Between(position, target);
            if (!RangedWeaponRules.CanReach(weapon, distance) ||
                !CanBattleEnter(battle, position, actorId) ||
                !HasBattleLineOfSight(position, target, weapon.MaximumRange)) continue;
            positions.Add(position);
        }
        return positions;
    }

    private (BattleLogEntry? LogEntry, BattleActionDetails Details) ResolveMonsterStrengthPressure(
        BattleEncounter battle, Enemy enemy,
        LiveCharacter target)
    {
        var result = _battleSystem.ResolveMonsterStrengthContest(enemy, target, battle.RuntimeFor(target));
        if (result.Outcome == MonsterStrengthContestOutcome.Resisted)
            return (null, BattleSystem.DescribeMonsterStrengthContest(enemy.Name, target.Name, result,
                MonsterStrengthContestOutcome.Resisted));

        if (result.Outcome == MonsterStrengthContestOutcome.Push &&
            TryPushBattleTarget(battle, enemy, target, out var pushedFormation))
        {
            var details = BattleSystem.DescribeMonsterStrengthContest(enemy.Name, target.Name, result,
                MonsterStrengthContestOutcome.Push);
            var message = BattleSystem.MonsterStrengthCombatLogMessage(target.Name,
                MonsterStrengthContestOutcome.Push, pushedFormation)!;
            return (new BattleLogEntry(message, BattleLogKind.Information, details), details);
        }

        var pushBlocked = result.Outcome == MonsterStrengthContestOutcome.Push;
        battle.StaggerCharacter(target,
            pushBlocked ? StaggerSeverity.Heavy : StaggerSeverity.Normal);
        var staggerDetails = BattleSystem.DescribeMonsterStrengthContest(enemy.Name, target.Name, result,
            MonsterStrengthContestOutcome.Stagger, pushBlocked);
        var staggerMessage = BattleSystem.MonsterStrengthCombatLogMessage(target.Name,
            MonsterStrengthContestOutcome.Stagger, pushBlocked: pushBlocked)!;
        return (new BattleLogEntry(staggerMessage, BattleLogKind.Information, staggerDetails), staggerDetails);
    }

    private static BattleActionDetails AppendBattleActionDetails(BattleActionDetails? action,
        BattleActionDetails additional) => action is null
        ? additional
        : action with
        {
            Summary = action.Summary.Concat(additional.Summary).ToArray(),
            Calculation = action.Calculation.Concat(additional.Calculation).ToArray()
        };

    private bool TryPushBattleTarget(BattleEncounter battle, Enemy enemy, LiveCharacter target,
        out bool pushedFormation)
    {
        pushedFormation = false;
        var direction = StrengthPushDirection(enemy.Position, battle.PositionOf(target));
        if (battle.HasActiveFormation && battle.FormationSlotFor(target) is not null)
        {
            var destinations = battle.FormationDestinations(direction);
            if (!CanForceMoveFormation(battle, destinations)) return false;
            foreach (var (member, destination) in destinations)
            {
                MoveBattleCharacterTo(member, destination);
                RevealFor(member, destination);
            }
            battle.UpdateFormationPositions(destinations);
            battle.PruneSeparatedEngagements();
            pushedFormation = true;
        }
        else
        {
            var destination = battle.PositionOf(target) + direction;
            if (!battle.Turns.IsInsideBattleArea(destination) ||
                !CanBattleEnter(battle, destination, CombatantId.ForCharacter(target.Id))) return false;
            MoveBattleCharacterTo(target, destination);
            battle.UpdatePosition(target, destination);
            battle.PruneSeparatedEngagements();
            RevealFor(target, destination);
        }
        if (!_isQuickBattle)
            _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
        return true;
    }

    private bool TryPushBattleEnemy(BattleEncounter battle, LiveCharacter attacker, Enemy target)
    {
        var direction = StrengthPushDirection(battle.PositionOf(attacker), target.Position);
        var destination = target.Position + direction;
        if (!battle.Turns.IsInsideBattleArea(destination) ||
            !CanBattleEnter(battle, destination, CombatantId.ForEnemy(target.Id))) return false;
        var previous = target.Position;
        target.MoveTo(destination);
        battle.UpdatePosition(target);
        battle.PruneSeparatedEngagements();
        if (!_isQuickBattle)
            _renderer.DrawEnemyMovement(_maze, _fogOfWar, previous, destination, _player.Position);
        return true;
    }

    private bool CanForceMoveFormation(BattleEncounter battle,
        IReadOnlyDictionary<LiveCharacter, Position> destinations)
    {
        if (destinations.Count == 0) return false;
        var movingIds = destinations.Keys.Select(member => CombatantId.ForCharacter(member.Id)).ToHashSet();
        var movingAvatars = destinations.Keys.Select(member =>
                _maze.PartyMembers.FirstOrDefault(avatar => avatar.Character == member))
            .Where(avatar => avatar is not null).ToHashSet();
        foreach (var destination in destinations.Values)
        {
            if (!battle.Turns.IsInsideBattleArea(destination) || !_maze.IsWalkable(destination) ||
                _maze.GetEnemyAt(destination) is not null ||
                battle.Turns.Participants.Any(participant => !movingIds.Contains(participant.Id) &&
                    participant.State is TacticalParticipantState.Active or TacticalParticipantState.Approaching &&
                    participant.Position == destination)) return false;
            var occupant = _maze.GetObjectAt(destination);
            if (occupant is null or GroundItemPile or Corpse || Maze.IsPassableNeutralNpc(occupant) ||
                occupant is PartyMemberAvatar avatar && movingAvatars.Contains(avatar)) continue;
            return false;
        }
        return true;
    }

    private static Direction StrengthPushDirection(Position attacker, Position target)
    {
        var deltaX = target.X - attacker.X;
        var deltaY = target.Y - attacker.Y;
        if (Math.Abs(deltaX) >= Math.Abs(deltaY))
            return deltaX >= 0 ? Direction.Right : Direction.Left;
        return deltaY >= 0 ? Direction.Down : Direction.Up;
    }

    private bool TryExecuteFormationMove(BattleEncounter battle, LiveCharacter character,
        Position target, out string error)
    {
        if (character != PartyLeader || !battle.HasActiveFormation)
        {
            error = "Az alakzatot csak a vezér mozgathatja.";
            return false;
        }
        if (battle.HasStaggeredFormationMember)
        {
            error = "Az alakzat egyik tagja megingott, ezért ebben az akcióban az alakzat nem mozoghat.";
            return false;
        }
        var origin = GetCasterPosition(character);
        var deltaX = target.X - origin.X;
        var deltaY = target.Y - origin.Y;
        if (Math.Abs(deltaX) + Math.Abs(deltaY) != 1)
        {
            error = "Az alakzat egy akcióval pontosan egy mezőt mozoghat.";
            return false;
        }
        var direction = deltaX switch
        {
            < 0 => Direction.Left,
            > 0 => Direction.Right,
            _ => deltaY < 0 ? Direction.Up : Direction.Down
        };
        var destinations = battle.FormationDestinations(direction);
        if (destinations.Count == 0)
        {
            error = "Nincs mozgatható harci alakzat.";
            return false;
        }
        if (!battle.PreservesEngagements(destinations))
        {
            error = "Az alakzat lépése szétszakítaná a fennálló lekötést.";
            return false;
        }
        var movingIds = destinations.Keys.Select(value => CombatantId.ForCharacter(value.Id)).ToHashSet();
        var movingAvatars = destinations.Keys.Select(value =>
                _maze.PartyMembers.FirstOrDefault(member => member.Character == value))
            .Where(member => member is not null).ToHashSet();
        foreach (var destination in destinations.Values)
        {
            if (!_maze.IsWalkable(destination) || _maze.GetEnemyAt(destination) is not null ||
                battle.Turns.Participants.Any(participant => !movingIds.Contains(participant.Id) &&
                    participant.State is TacticalParticipantState.Active or TacticalParticipantState.Approaching &&
                    participant.Position == destination))
            {
                error = "Az alakzat egyik célmezője foglalt vagy nem járható.";
                return false;
            }
            var occupant = _maze.GetObjectAt(destination);
            if (occupant is null or GroundItemPile or Corpse || Maze.IsPassableNeutralNpc(occupant) ||
                occupant is PartyMemberAvatar avatar && movingAvatars.Contains(avatar)) continue;
            error = "Az alakzat egyik célmezőjét tereptárgy vagy másik lény foglalja el.";
            return false;
        }

        var previous = destinations.Keys.ToDictionary(value => value, GetCasterPosition);
        foreach (var (member, destination) in destinations)
        {
            if (member == PartyLeader) _player.TeleportTo(destination);
            else _maze.PartyMembers.First(avatar => avatar.Character == member).MoveTo(destination);
        }
        battle.UpdateFormationPositions(destinations);
        battle.RecordMovement(BattleSide.Friendly);
        foreach (var (member, destination) in destinations)
        {
            var revealed = RevealFor(member, destination);
            if (member == PartyLeader)
                _renderer.DrawMovement(_maze, _fogOfWar, previous[member], destination, revealed, hasWon: false);
            else
                _renderer.DrawPartyMemberMovement(_maze, _fogOfWar, previous[member], destination, revealed,
                    _player.Position);
        }
        var statusText = _battleSystem.FinishCharacterAction(character, battle.RuntimeFor(character));
        PresentBattleEntries([new BattleLogEntry($"{character.Name} egy mezővel mozgatja az egész alakzatot.{statusText}",
            BattleLogKind.Information)]);
        AdvanceBattleTurn(battle);
        error = string.Empty;
        return true;
    }

    private bool TryExecuteSwapToRear(BattleEncounter battle, LiveCharacter character, out string error)
    {
        if (battle.IsCharacterStaggered(character) ||
            battle.RearPartnerOf(character) is { } rearPartner && battle.IsCharacterStaggered(rearPartner))
        {
            error = "Megingott alakzattag ebben az akcióban nem cserélhet helyet.";
            return false;
        }
        if (!battle.TrySwapToRear(character, out var rear, out var frontPosition, out var rearPosition,
                out var transferredEngagements) || rear is null)
        {
            error = "A Hátra! akcióhoz élő társ szükséges közvetlenül a karakter mögött.";
            return false;
        }
        MoveBattleCharacterTo(character, rearPosition);
        MoveBattleCharacterTo(rear, frontPosition);
        battle.RecordMovement(BattleSide.Friendly);
        _formation = battle.Formation!;
        _renderer.CharacterSheet.SetFormationStatus(_formation);
        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
        var statusText = _battleSystem.FinishCharacterAction(character, battle.RuntimeFor(character));
        var transferText = transferredEngagements == 0
            ? string.Empty
            : $" {rear.Name} {transferredEngagements} lekötést átvett.";
        PresentBattleEntries([new BattleLogEntry(
            $"Hátra! {character.Name} helyet cserél {rear.Name} karakterrel.{transferText}{statusText}",
            BattleLogKind.Information)]);
        AdvanceBattleTurn(battle);
        error = string.Empty;
        return true;
    }

    private void MoveBattleCharacterTo(LiveCharacter character, Position position)
    {
        if (character == PartyLeader) _player.TeleportTo(position);
        else _maze.PartyMembers.First(member => member.Character == character).MoveTo(position);
    }

    private bool TryExecuteCharacterMove(BattleEncounter battle, LiveCharacter character, Position target,
        out string error)
    {
        if (battle.IsCharacterStaggered(character))
        {
            error = $"{character.Name} megingott, ezért ebben az akcióban nem mozoghat.";
            return false;
        }
        if (battle.IsEngaged(character))
        {
            error = $"{character.Name} le van kötve, ezért nem mozoghat.";
            return false;
        }
        BeginBattleMovementIfNeeded(battle);
        var path = FindStraightBattlePath(battle, GetCasterPosition(character), target,
            CombatantId.ForCharacter(character.Id), maximumSteps: 1);
        if (path.Count == 0)
        {
            error = "Ebben az irányban nincs szabad, elérhető mező. Az akciód megmaradt.";
            return false;
        }
        CompleteCharacterMovement(battle, character, path, finishAction: false);
        _battleMovementRemaining--;
        _battleMovementSteps++;
        if (_battleMovementRemaining <= 0) FinishCharacterMovement(battle, character);
        error = string.Empty;
        return true;
    }

    private IReadOnlyList<Position> FindStraightBattlePath(BattleEncounter battle, Position origin,
        Position target, CombatantId actorId, int maximumSteps)
    {
        var deltaX = target.X - origin.X;
        var deltaY = target.Y - origin.Y;
        if ((deltaX == 0) == (deltaY == 0)) return [];
        var stepX = Math.Sign(deltaX);
        var stepY = Math.Sign(deltaY);
        var requestedSteps = Math.Abs(deltaX != 0 ? deltaX : deltaY);
        var steps = new List<Position>();
        var current = origin;
        for (var index = 0; index < Math.Min(maximumSteps, requestedSteps); index++)
        {
            var next = new Position(current.X + stepX, current.Y + stepY);
            if (!CanBattleEnter(battle, next, actorId)) break;
            steps.Add(next);
            current = next;
        }
        return steps;
    }

    private void CompleteCharacterMovement(BattleEncounter battle, LiveCharacter character,
        IReadOnlyList<Position> path, bool finishAction = true)
    {
        var steps = path.Take(battle.Current.MovementAllowance).ToArray();
        if (steps.Length > 0)
        {
            _battleNoPathReported.Remove(character.Id);
            battle.RecordMovement(BattleSide.Friendly);
            var previousPosition = GetCasterPosition(character);
            var destination = steps[^1];
            if (character == PartyLeader) _player.TeleportTo(destination);
            else _maze.PartyMembers.First(member => member.Character == character).MoveTo(destination);
            battle.UpdatePosition(character, destination);
            var newlyRevealed = RevealFor(character, destination);
            if (!_isQuickBattle)
            {
                if (character == PartyLeader)
                    _renderer.DrawMovement(_maze, _fogOfWar, previousPosition, destination, newlyRevealed, hasWon: false);
                else
                    _renderer.DrawPartyMemberMovement(_maze, _fogOfWar, previousPosition, destination, newlyRevealed,
                        _player.Position);
            }
        }
        if (!finishAction) return;
        var statusText = _battleSystem.FinishCharacterAction(character, battle.RuntimeFor(character));
        var message = steps.Length > 0
            ? $"{character.Name}\t👣 {steps.Length} mezőt mozog{statusText}"
            : $"{character.Name}\t⛔ Nincs járható út{statusText}";
        if (steps.Length > 0 || _battleNoPathReported.Add(character.Id) || !string.IsNullOrEmpty(statusText))
            PresentBattleEntries([new BattleLogEntry(message, BattleLogKind.Information)]);
        AdvanceBattleTurn(battle);
    }

    private bool IsBattleMovementInProgress(BattleEncounter battle) =>
        _battleMovementTurnId == battle.Turns.TurnId && _battleMovementSteps > 0;

    private void BeginBattleMovementIfNeeded(BattleEncounter battle)
    {
        if (_battleMovementTurnId == battle.Turns.TurnId) return;
        _battleMovementTurnId = battle.Turns.TurnId;
        _battleMovementRemaining = battle.Current.MovementAllowance;
        _battleMovementSteps = 0;
    }

    private void FinishCharacterMovement(BattleEncounter battle, LiveCharacter character)
    {
        var statusText = _battleSystem.FinishCharacterAction(character, battle.RuntimeFor(character));
        PresentBattleEntries([new BattleLogEntry(
            $"{character.Name}\t👣 {_battleMovementSteps} mezőt mozog{statusText}", BattleLogKind.Information)]);
        ResetBattleMovement();
        AdvanceBattleTurn(battle);
    }

    private void ResetBattleMovement()
    {
        _battleMovementTurnId = -1;
        _battleMovementRemaining = 0;
        _battleMovementSteps = 0;
    }

    private IReadOnlyList<Position> FindBattlePath(BattleEncounter battle, Position origin,
        IReadOnlyCollection<Position> goals, CombatantId actorId)
    {
        if (goals.Count == 0) return [];
        var goalSet = goals.ToHashSet();
        var queue = new Queue<Position>();
        var previous = new Dictionary<Position, Position> { [origin] = origin };
        queue.Enqueue(origin);
        Position? found = goalSet.Contains(origin) ? origin : null;
        while (queue.Count > 0 && found is null)
        {
            var current = queue.Dequeue();
            foreach (var direction in Directions)
            {
                var next = current + direction;
                if (previous.ContainsKey(next) ||
                    !CanBattleEnter(battle, next, actorId) && !CanFlyingEnemyTraverse(battle, next, actorId)) continue;
                previous[next] = current;
                if (goalSet.Contains(next)) { found = next; break; }
                queue.Enqueue(next);
            }
        }
        if (found is null || found == origin) return [];
        var path = new List<Position>();
        for (var current = found.Value; current != origin; current = previous[current]) path.Add(current);
        path.Reverse();
        return path;
    }

    private bool CanBattleEnter(BattleEncounter battle, Position position, CombatantId actorId)
    {
        if (!_maze.IsWalkable(position)) return false;
        if (battle.Turns.Participants.Any(participant => participant.Id != actorId &&
                participant.State is TacticalParticipantState.Active or TacticalParticipantState.Approaching &&
                participant.Position == position)) return false;
        var occupant = _maze.GetObjectAt(position);
        var actorCharacter = battle.CharacterFor(actorId);
        var actorEnemy = battle.EnemyFor(actorId);
        return occupant is null or GroundItemPile or Corpse || occupant == actorEnemy ||
               occupant is PartyMemberAvatar member && member.Character == actorCharacter ||
               Maze.IsPassableNeutralNpc(occupant);
    }

    private bool CanFlyingEnemyTraverse(BattleEncounter battle, Position position, CombatantId actorId)
    {
        var enemy = battle.EnemyFor(actorId);
        if (enemy is null || !enemy.Definition.HasTrait(EnemyTraits.Flying) || !_maze.IsWalkable(position))
            return false;
        return battle.Turns.Participants.Any(participant => participant.Id != actorId &&
            participant.State is TacticalParticipantState.Active or TacticalParticipantState.Approaching &&
            participant.Position == position);
    }

    private void SynchronizeBattleDefeats(BattleEncounter battle, LiveCharacter? killer = null)
    {
        foreach (var enemy in battle.Enemies.Where(enemy => enemy.CurrentHitPoints <= 0).ToArray())
            ResolveEnemyDefeat(battle, enemy, killer);
        foreach (var character in battle.Characters.Where(character => !character.IsAlive).ToArray())
            ResolveCharacterDefeat(battle, character);
    }

    private void ResolveEnemyDefeat(BattleEncounter battle, Enemy enemy, LiveCharacter? killer)
    {
        if (!battle.TryResolveDeath(enemy)) return;
        battle.MarkDefeated(enemy);
        if (!_maze.Enemies.Contains(enemy)) return;
        AwardBossKey(enemy);
        RegisterNpcQuestKill(enemy);
        var credited = killer ?? battle.Characters.FirstOrDefault(character => character.IsAlive);
        if (credited is not null)
        {
            credited.RecordMonsterKill(enemy.Definition.Id);
            var awards = DistributeExperience(credited, enemy.Definition.ExperienceReward, isQuest: false);
            battle.RecordKill(credited, enemy, awards.Sum(award => award.Result.GainedExperience));
            _pendingLevelUps.AddRange(awards.Where(award => award.Result.LeveledUp && award.Character.IsAlive)
                .Select(award => (award.Character, award.Result)));
        }
        _maze.ReplaceEnemyWithCorpse(enemy);
        _nextEnemyMoves.Remove(enemy);
        var message = $"☠ {enemy.Name} elesett. +{enemy.Definition.ExperienceReward} XP kerül szétosztásra.";
        if (!_isQuickBattle) _renderer.DrawInventoryMessage(message, ConsoleColor.Green);
        RecordSessionActivity(SessionActivityKind.Battle, message, ConsoleColor.Green);
    }

    private void ResolveCharacterDefeat(BattleEncounter battle, LiveCharacter character)
    {
        var avatar = _maze.PartyMembers.FirstOrDefault(member => member.Character == character);
        if (avatar is not null && IsQuestCriticalRoderic(avatar))
        {
            character.RestoreVitality(Math.Max(1, character.MaximumVitality / 3));
            var message = $"{character.Name} eszméletét veszti, de az Ezüst Eskü erejével ismét talpra áll.";
            _renderer.DrawInventoryMessage(message, ConsoleColor.DarkYellow);
            return;
        }
        if (!battle.TryResolveDeath(character)) return;
        battle.MarkDefeated(character);
        if (avatar is not null)
        {
            var defeatedPosition = avatar.Position;
            _maze.ReplacePartyMemberWithCorpse(avatar);
            _nextPartyMoves.Remove(avatar);
            if (!_isQuickBattle)
                _renderer.DrawMapCellsChanged(_maze, _fogOfWar, _player.Position, [defeatedPosition]);
        }
        if (character != PartyLeader)
        {
            _activeCoopHost?.TryPublishCharacterState(character.Id,
                _gameSaveService.SerializeCharacter(character), CharacterSyncReason.CharacterDied);
            _session.ReleaseCharacterControl(character.Id);
        }
        var messageText = $"☠ {character.Name} elesett a harcban.";
        _renderer.DrawInventoryMessage(messageText, ConsoleColor.Red);
        RecordSessionActivity(SessionActivityKind.Battle, messageText, ConsoleColor.Red);
        PlaySessionSound(SoundEffect.MemberKilled);
        TryLogPartyComments(PartySituationIds.PartyMemberDied);
    }

    private void FinishBattleStalemate(BattleEncounter battle)
    {
        _renderer.DrawBattleCommandPanel(string.Empty);
        _session.EndBattle(battle.Id);
        var cycles = Math.Max(1, battle.Turns.Cycle);
        var characterResults = battle.Characters.Select(battle.ResultFor).ToArray();
        var resourceSummary = ConsoleRenderer.FormatBattleResourceSummary(characterResults, cycles);
        foreach (var character in battle.Characters.Where(character => character.IsAlive))
            DrainNeedsAfterBattle(character, cycles);
        var inactive = battle.InactiveSidesLastCompletedCycle;
        if (_locationId == DeveloperBattleTestLocationId)
            _developerBattleLog.CompleteBattle(battle, "stalemate");
        var sideName = inactive.Contains(BattleSide.Friendly) ? "a csapat" : "az ellenséges oldal";
        ResetBattleMovement();
        _activeBattle = null;
        _isQuickBattle = false;
        _preparedBattleTurnId = 0;
        _battleStarted = false;
        _session.SetPhase(GameSessionPhase.Exploration);
        var message = inactive.Count == 2
            ? $"⚖️ Az összecsapás véget ér: egyik oldal sem mozdult vagy támadott " +
              $"{BattleEncounter.InactiveCycleLimit} teljes körön át."
            : $"⚖️ Az összecsapás véget ér: {sideName} nem mozdult és nem támadott " +
              $"{BattleEncounter.InactiveCycleLimit} teljes körön át.";
        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
        _renderer.DrawInventoryMessage(message, ConsoleColor.DarkYellow);
        RecordSessionActivity(SessionActivityKind.Battle, message, ConsoleColor.DarkYellow);
        var details = $"Eredmény: {resourceSummary}";
        _renderer.DrawInventoryMessage(details, ConsoleColor.Cyan);
        RecordSessionActivity(SessionActivityKind.Battle, details, ConsoleColor.Cyan);
        foreach (var (character, result) in _pendingLevelUps.ToArray())
            ResolvePerkOffers(character, result);
        _renderer.RestoreAfterBattle();
        _pendingLevelUps.Clear();
        if (_saveAfterBattle)
        {
            _saveAfterBattle = false;
            SaveGame();
        }
        var resumeAt = DateTime.UtcNow + StalemateRestartDelay;
        InitializeEnemyMoveSchedule(resumeAt);
        foreach (var member in _maze.PartyMembers) ScheduleNextPartyMove(member, resumeAt);
        _nextNeedsDrain = DateTime.UtcNow + TimeSpan.FromMinutes(1);
        ForceCoopSnapshotPublish();
        Thread.Sleep(800);
    }

    private void FinishBattle(BattleEncounter battle, bool forceDefeat = false)
    {
        _renderer.DrawBattleCommandPanel(string.Empty);
        _session.EndBattle(battle.Id);
        var victory = !forceDefeat && battle.HostileSideDefeated && !battle.FriendlySideDefeated;
        var wasQuickBattle = _isQuickBattle;
        var cycles = Math.Max(1, battle.Turns.Cycle);
        var characterResults = battle.Characters.Select(battle.ResultFor).ToArray();
        var resourceSummary = ConsoleRenderer.FormatBattleResourceSummary(characterResults, cycles);
        if (_locationId == DeveloperBattleTestLocationId)
            _developerBattleLog.CompleteBattle(battle, victory ? "victory" : "defeat");
        ResetBattleMovement();

        _activeBattle = null;
        _battleStarted = false;
        _preparedBattleTurnId = 0;

        _session.EndBattle(battle.Id);
        if (!victory)
        {
            _saveAfterBattle = false;
            _renderer.DrawGameOver(PartyLeader.Name);
            _gameOver = true;
            _session.SetPhase(GameSessionPhase.GameOver);
            return;
        }

        PlayBattleVictorySound();
        foreach (var character in battle.Characters.Where(character => character.IsAlive))
        {
            DrainNeedsAfterBattle(character, cycles);
            if (character != PartyLeader) TryNpcUseConsumables(character);
        }
        var message = ConsoleRenderer.FormatBattleVictorySummary(wasQuickBattle, cycles,
            battle.ActionNumber, battle.Kills);
        _renderer.DrawInventoryMessage(message, ConsoleColor.Green);
        RecordSessionActivity(SessionActivityKind.Battle, message, ConsoleColor.Green);
        var details = $"Eredmény: {resourceSummary}";
        _renderer.DrawInventoryMessage(details, ConsoleColor.Cyan);
        RecordSessionActivity(SessionActivityKind.Battle, details, ConsoleColor.Cyan);
        TryLogPartyComments(PartySituationIds.BattleWon);
        _renderer.RefreshCharacterSheet(PartyLeader);
        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
        foreach (var (character, result) in _pendingLevelUps.ToArray())
            ResolvePerkOffers(character, result);
        _renderer.RestoreAfterBattle();
        _pendingLevelUps.Clear();
        if (_saveAfterBattle)
        {
            _saveAfterBattle = false;
            SaveGame();
        }
        InitializeEnemyMoveSchedule(DateTime.UtcNow);
        foreach (var member in _maze.PartyMembers) ScheduleNextPartyMove(member, DateTime.UtcNow);
        _session.SetPhase(GameSessionPhase.Exploration);
        _nextNeedsDrain = DateTime.UtcNow + TimeSpan.FromMinutes(1);
        RequestCoopSnapshotPublish();
    }

    /// <summary>
    /// A coopban megnyitott, játékot szüneteltető személyes ablakok központi nyilvántartója.
    /// Nyilvántartja a megnyitott ablakot, elindítja vagy lezárja a közös játék szünetét,
    /// frissíti a felső státuszcsíkot, snapshotot küld, naplóüzenetet ír, és hangot is lejátszhat.
    /// Ez az igazság forrása arra nézve, hogy jelenleg mely játékosok tartják szüneteltetve a közös játékot valamilyen személyes ablakkal.
    /// </summary>
    /// <param name="playerId">melyik játékos</param>
    /// <param name="characterId"> melyik karakterével</param>
    /// <param name="kind">milyen ablakot nyitott (súgó, inventory stb.)</param>
    /// <param name="windowId">az adott konkrét ablakpéldány azonosítója</param>
    /// <param name="isOpen">megnyitotta vagy bezárta</param>
}
