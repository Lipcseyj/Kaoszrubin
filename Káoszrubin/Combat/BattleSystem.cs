using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Magic;

namespace KaoszRubin.Combat;

/// <summary>A közelharc egy akciónként léptethető, megjelenítéstől és inputforrástól független szabályrendszere.</summary>
public sealed class BattleSystem(Random random, IEnumerable<MonsterAbilityDefinition> monsterAbilities,
    IEnumerable<StatusDefinition> statuses, IEnumerable<StrengthHitBonusDefinition> strengthHitBonuses)
{
    private const string DexterityWeaponTypeId = "WT002";
    private const string DefenseWeaponTypeId = "WT003";
    private readonly Random _random = random;
    private readonly IReadOnlyDictionary<string, MonsterAbilityDefinition> _monsterAbilities =
        monsterAbilities.ToDictionary(ability => ability.Id, StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyDictionary<string, StatusDefinition> _statuses =
        statuses.ToDictionary(status => status.Id, StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyList<StrengthHitBonusDefinition> _strengthHitBonuses = strengthHitBonuses.ToList();

    public BattleResult Resolve(LiveCharacter player, Enemy enemy, Action<BattleLogEntry> onRound,
        Func<BattlePlayerAction?>? choosePlayerAction = null, Func<int>? partyMemberDamage = null,
        LiveCharacter? knightProtector = null)
    {
        var started = StartBattle(player, enemy);
        foreach (var entry in started.Entries) onRound(entry);
        var state = started.State;
        if (knightProtector is not null)
            state.SetKnightProtection(knightProtector);
        if (state.RequiresTacticSelection)
        {
            var tactics = player.CharacterClass.Id == CharacterClassIds.Harcos
                ? new[] { BattleTactic.FighterPrecise, BattleTactic.FighterPowerful, BattleTactic.FighterDefensive }
                : new[] { BattleTactic.ThiefAmbush, BattleTactic.ThiefObserve, BattleTactic.ThiefPoison };
            state.TryChooseTactic(tactics[_random.Next(tactics.Length)]);
        }
        while (!state.IsCompleted)
        {
            var supportDamage = partyMemberDamage?.Invoke() ?? 0;
            var action = state.IsPlayerTurn && supportDamage < state.CurrentEnemyHitPoints
                ? choosePlayerAction?.Invoke()
                : null;
            var step = Advance(state, action, supportDamage);
            foreach (var entry in step.Entries) onRound(entry);
        }
        return state.Result!;
    }

    public BattleStartResult StartBattle(LiveCharacter player, Enemy enemy)
    {
        PrepareEnemyForBattle(enemy);
        var defender = enemy.Definition with { HitPoints = enemy.CurrentHitPoints };
        var context = new BattleRuntimeContext(player);
        var entries = new List<BattleLogEntry>();
        ApplyBattleStartPerks(player, entries.Add);
        var statusCosts = player.ApplyBattleStartStatusEffects();
        if (statusCosts.VitalityLost > 0 || statusCosts.ManaLost > 0)
        {
            var costs = new List<string>();
            if (statusCosts.VitalityLost > 0)
                costs.Add($"🍖 nulla élelem: ❤️ -{statusCosts.VitalityLost} HP");
            if (statusCosts.ManaLost > 0)
                costs.Add($"💧 szomjúság: 🔷 -{statusCosts.ManaLost} manna");
            entries.Add(new BattleLogEntry($"Csatakezdő állapothatás — {string.Join("; ", costs)}.", BattleLogKind.Information));
        }
        var attackWeapon = player.ActiveWeapons.FirstOrDefault(item =>
            item is not null && item.WeaponTypeId != DefenseWeaponTypeId);
        var initiativeFamily = WeaponFamilies.ForWeapon(attackWeapon);
        var proficiencyInitiativeBonus = initiativeFamily switch
        {
            WeaponFamilies.Dagger when player.WeaponProficiencyRankFor(initiativeFamily) is not null => 2,
            WeaponFamilies.Polearm when player.WeaponProficiencyRankFor(initiativeFamily) is not null => 3,
            _ => 0
        };
        var perkInitiativeBonus = player.HasPerk(PerkIds.FighterFirstStrike) ? 10 : 0;
        var magicInitiativeBonus = player.GetMagicItemBonus(MagicItemEffect.Initiative);
        var spellInitiativeBonus = player.SpellEffectValue(ActiveSpellEffectType.InitiativeBonus);
        var mobility = CharacterMobilityRules.Evaluate(player);
        var initiativeBonus = perkInitiativeBonus + magicInitiativeBonus + spellInitiativeBonus + proficiencyInitiativeBonus;
        var playerInitiative = RollInitiative(mobility.InitiativeBase + initiativeBonus - player.StatusInitiativePenalty);
        var enemyInitiativeBonus = MonsterAbilityValue(defender, MonsterAbilityEffect.InitiativeBonus);
        var enemyInitiative = RollInitiative(enemy.EffectiveSpeed + enemyInitiativeBonus);
        var playerAttacks = playerInitiative.Total >= enemyInitiative.Total;
        var initiativeNotes = new List<string>();
        if (perkInitiativeBonus > 0) initiativeNotes.Add($"Első csapás +{perkInitiativeBonus}");
        if (magicInitiativeBonus > 0) initiativeNotes.Add($"varázstárgy +{magicInitiativeBonus}");
        if (spellInitiativeBonus > 0) initiativeNotes.Add($"áldás +{spellInitiativeBonus}");
        if (proficiencyInitiativeBonus > 0)
            initiativeNotes.Add($"{WeaponFamilies.Find(initiativeFamily!)!.Icon} jártasság +{proficiencyInitiativeBonus}");
        var perkText = initiativeNotes.Count > 0 ? $" [{string.Join(", ", initiativeNotes)}]" : string.Empty;
        var loadText = mobility.EncumbranceInitiativePenalty > 0
            ? $" - felszerelés {mobility.EncumbranceInitiativePenalty} ({mobility.EquippedWeight}/{mobility.CarryingCapacity})"
            : string.Empty;
        var initiativeMessage = $"Kezdeményezés: {player.Name} Ügy {player.EffectiveAbilities.Dexterity}" +
            (mobility.ClassInitiativeModifier > 0 ? $" + osztály {mobility.ClassInitiativeModifier}" : string.Empty) +
            loadText + perkText +
            (player.StatusInitiativePenalty > 0
                ? $" - {(player.HasStatus(CharacterStatusIds.Thirsty) ? "💧 szomjúság" : "állapot")} {player.StatusInitiativePenalty}"
                : string.Empty) +
            $" {playerInitiative.ModifierText} = {playerInitiative.Total}; " +
            $"{enemy.Name} Gy {defender.Speed ?? 1}" + (enemyInitiativeBonus > 0 ? $" + képesség {enemyInitiativeBonus}" : string.Empty) +
            $" {enemyInitiative.ModifierText} = {enemyInitiative.Total}. {(playerAttacks ? player.Name : enemy.Name)} kezd.";
        entries.Add(new BattleLogEntry(initiativeMessage, BattleLogKind.Information));
        var state = new BattleState(player, enemy, defender, context, playerAttacks, [initiativeMessage]);
        if (player.CurrentVitality <= 0 || state.CurrentEnemyHitPoints <= 0) Complete(state);
        return new BattleStartResult(state, entries);
    }

    public TeamCombatantPreparation PrepareTeamCharacter(LiveCharacter character)
    {
        ArgumentNullException.ThrowIfNull(character);
        var entries = new List<BattleLogEntry>();
        var runtime = new TeamCharacterBattleRuntime(character);
        ApplyBattleStartPerks(character, entries.Add);
        var statusCosts = character.ApplyBattleStartStatusEffects();
        if (statusCosts.VitalityLost > 0 || statusCosts.ManaLost > 0)
        {
            var costs = new List<string>();
            if (statusCosts.VitalityLost > 0) costs.Add($"🍖 nulla élelem: ❤️ -{statusCosts.VitalityLost} HP");
            if (statusCosts.ManaLost > 0) costs.Add($"💧 szomjúság: 🔷 -{statusCosts.ManaLost} manna");
            entries.Add(new BattleLogEntry($"{character.Name} csatakezdő állapothatása — {string.Join("; ", costs)}.",
                BattleLogKind.Information));
        }

        var weapon = character.ActiveWeapons.FirstOrDefault(item =>
            item is not null && item.WeaponTypeId != DefenseWeaponTypeId);
        var family = WeaponFamilies.ForWeapon(weapon);
        var proficiencyBonus = family switch
        {
            WeaponFamilies.Dagger when character.WeaponProficiencyRankFor(family) is not null => 2,
            WeaponFamilies.Polearm when character.WeaponProficiencyRankFor(family) is not null => 3,
            _ => 0
        };
        var mobility = CharacterMobilityRules.Evaluate(character);
        var perkBonus = character.HasPerk(PerkIds.FighterFirstStrike) ? 10 : 0;
        var totalBase = mobility.InitiativeBase + perkBonus + proficiencyBonus +
                        character.GetMagicItemBonus(MagicItemEffect.Initiative) +
                        character.SpellEffectValue(ActiveSpellEffectType.InitiativeBonus) -
                        character.StatusInitiativePenalty;
        var roll = RollInitiative(totalBase);
        entries.Add(new BattleLogEntry(
            $"⚡ {character.Name} kezdeményezése: {totalBase} {roll.ModifierText} = {roll.Total}.",
            BattleLogKind.Information));
        return new TeamCombatantPreparation(runtime, roll.Total, entries);
    }

    public int RollTeamEnemyInitiative(Enemy enemy)
    {
        ArgumentNullException.ThrowIfNull(enemy);
        return RollInitiative(enemy.EffectiveSpeed +
            MonsterAbilityValue(enemy.Definition, MonsterAbilityEffect.InitiativeBonus)).Total;
    }

    public void PrepareEnemyForBattle(Enemy enemy)
    {
        var abilities = enemy.Definition.AbilityIds.Where(_monsterAbilities.ContainsKey)
            .Select(id => _monsterAbilities[id]);
        enemy.PrepareAbilityCharges(abilities);
        enemy.ClearPreparedWeapon();
    }

    public void BeginTeamCharacterTurn(LiveCharacter character)
    {
        ArgumentNullException.ThrowIfNull(character);
        character.AdvanceSpellEffects();
    }

    public string FinishTeamCharacterAction(LiveCharacter character, TeamCharacterBattleRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(runtime);
        var ticks = character.ApplyTurnEndStatusEffects(_random);
        if (runtime.Context.BarbarianRageActionsRemaining > 0)
            runtime.Context.BarbarianRageActionsRemaining--;
        return ticks.Count == 0 ? string.Empty :
            $" Állapothatások: {string.Join(", ", ticks.Select(tick => $"{tick.Icon} {tick.Name}" +
                (tick.Damage > 0 ? $" -{tick.Damage} HP" : string.Empty) +
                (tick.Expired ? " (elmúlt)" : string.Empty)))}.";
    }

    public BattleLogEntry ResolveTeamCharacterAttack(LiveCharacter attacker,
        TeamCharacterBattleRuntime runtime, Enemy defender, bool finishAction = true)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(defender);
        var definition = defender.Definition with { HitPoints = defender.CurrentHitPoints };
        var count = attacker.HasPerk(PerkIds.BarbarianBerserkerRage) &&
                    attacker.CurrentVitality * 2 < attacker.MaximumVitality ? 2 : 1;
        var attacks = new List<AttackResult>();
        var critical = false;
        for (var index = 0; index < count && definition.HitPoints is > 0; index++)
        {
            var attack = PlayerAttack(attacker, definition, runtime.Context, defender.EffectiveSpeed);
            critical |= attack.Critical;
            definition = ApplyAttack(definition, attack);
            attacks.Add(attack);
            if (index == 0 && attack.Hit && definition.HitPoints is > 0 &&
                attacker.HasPerk(PerkIds.FighterSteelStorm) && _random.NextDouble() < 0.35)
            {
                var extra = PlayerAttack(attacker, definition, runtime.Context, defender.EffectiveSpeed);
                critical |= extra.Critical;
                definition = ApplyAttack(definition, extra);
                attacks.Add(extra with { Message = $"Acélvihar: {extra.Message}" });
            }
        }
        defender.SetCurrentHitPoints(definition.HitPoints ?? 0);
        var statusText = finishAction ? FinishTeamCharacterAction(attacker, runtime) : string.Empty;
            return new BattleLogEntry(
                $"{FormatAttackSummary(attacker.Name, defender.Name, attacks,
                    defender.CurrentHitPoints, defender.Definition.HitPoints ?? defender.CurrentHitPoints)}{statusText}",
            critical ? BattleLogKind.CriticalHit : BattleLogKind.PlayerAttack,
            DescribeAction(attacker.Name, defender.Name, attacks, statusText));
    }

    public WeaponDefinition? SelectEnemyAttackWeapon(Enemy attacker)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        if (attacker.PreparedWeaponId is { } preparedId)
        {
            var prepared = (attacker.Definition.Weapons ?? []).FirstOrDefault(weapon =>
                string.Equals(weapon.Id, preparedId, StringComparison.OrdinalIgnoreCase));
            if (prepared is not null) return prepared;
            attacker.ClearPreparedWeapon();
        }
        if (attacker.Definition.Weapon is { } selectedWeapon)
            return attacker.IsWeaponReady(selectedWeapon.Id) ? selectedWeapon : null;
        var ready = (attacker.Definition.Weapons ?? []).Where(weapon => attacker.IsWeaponReady(weapon.Id)).ToArray();
        return ready.Length > 0 ? ready[_random.Next(ready.Length)] : null;
    }

    public WeaponDefinition? SelectEnemyAttackWeapon(Enemy attacker, Func<WeaponDefinition, int> targetCount)
    {
        ArgumentNullException.ThrowIfNull(targetCount);
        if (attacker.PreparedWeaponId is not null) return SelectEnemyAttackWeapon(attacker);
        if (attacker.Definition.Weapon is not null) return SelectEnemyAttackWeapon(attacker);
        var ready = (attacker.Definition.Weapons ?? []).Where(weapon => attacker.IsWeaponReady(weapon.Id)).ToArray();
        if (ready.Length == 0) return null;
        var breaths = ready.Where(IsTelegraphedWeapon).Select(weapon => (Weapon: weapon, Targets: targetCount(weapon)))
            .Where(candidate => candidate.Targets > 0).ToArray();
        var groupBreath = breaths.Where(candidate => candidate.Targets >= 2)
            .OrderByDescending(candidate => candidate.Targets)
            .ThenByDescending(candidate => candidate.Weapon.Damage?.Maximum ?? 0).FirstOrDefault();
        if (groupBreath.Weapon is not null) return groupBreath.Weapon;
        if (breaths.Length > 0 && _random.Next(100) < 35)
            return breaths[_random.Next(breaths.Length)].Weapon;
        var ordinary = ready.Where(weapon => !IsTelegraphedWeapon(weapon) && targetCount(weapon) > 0).ToArray();
        return ordinary.Length > 0 ? ordinary[_random.Next(ordinary.Length)] :
            breaths.Length > 0 ? breaths[_random.Next(breaths.Length)].Weapon : ready[_random.Next(ready.Length)];
    }

    public static bool IsTelegraphedWeapon(WeaponDefinition? weapon) =>
        weapon is { MaximumTargets: > 1 } && !weapon.DamageType.IsPhysical();

    public BattleLogEntry PrepareEnemyWeapon(Enemy enemy, WeaponDefinition weapon)
    {
        enemy.PrepareWeapon(weapon.Id);
        return new BattleLogEntry($"⚠️ {enemy.Name} előkészíti: {weapon.Name}. A következő saját körében elsüti!",
            BattleLogKind.Information);
    }

    public EnemyTurnStartResult BeginEnemyTurn(Enemy enemy)
    {
        ArgumentNullException.ThrowIfNull(enemy);
        enemy.AdvanceCombatCooldowns();
        var entries = new List<BattleLogEntry>();
        var spellTick = enemy.AdvanceSpellEffects(_random);
        if (spellTick.Damage > 0) enemy.ReceiveSpellDamage(spellTick.Damage);
        if (spellTick.Notes.Count > 0)
            entries.Add(new BattleLogEntry($"{enemy.Name}: {string.Join(", ", spellTick.Notes)}.", BattleLogKind.Information));
        if (enemy.CurrentHitPoints > 0)
        {
            var regeneration = MonsterAbilityValue(enemy.Definition, MonsterAbilityEffect.Regeneration);
            var restored = enemy.RestoreHitPoints(regeneration);
            if (restored > 0)
                entries.Add(new BattleLogEntry($"♻️ {enemy.Name} regenerálódik: +{restored} HP " +
                    $"({enemy.CurrentHitPoints}/{enemy.Definition.HitPoints}).", BattleLogKind.Information));
        }
        if (enemy.CurrentHitPoints <= 0) return new EnemyTurnStartResult(false, entries);
        if (spellTick.SkipAction)
        {
            entries.Add(new BattleLogEntry($"{enemy.Name} varázshatás miatt kihagyja az akcióját.", BattleLogKind.Information));
            return new EnemyTurnStartResult(false, entries);
        }
        return new EnemyTurnStartResult(true, entries);
    }

    public MonsterAbilityDefinition? SelectEnemyActiveAbility(Enemy enemy, int targetDistance)
    {
        var candidates = enemy.Definition.AbilityIds.Where(_monsterAbilities.ContainsKey)
            .Select(id => _monsterAbilities[id])
            .Where(ability => ability.Trigger == MonsterAbilityTrigger.Active &&
                              enemy.IsAbilityReady(ability.Id) && enemy.HasAbilityCharge(ability) &&
                              targetDistance <= ability.Range)
            .ToArray();
        if (candidates.Length == 0) return null;
        var selected = candidates[_random.Next(candidates.Length)];
        return _random.Next(100) < selected.AiWeight ? selected : null;
    }

    public BattleLogEntry ResolveTeamEnemyAbility(Enemy attacker, LiveCharacter defender,
        TeamCharacterBattleRuntime defenderRuntime, MonsterAbilityDefinition ability,
        bool consumeResources = true) =>
        ResolveEnemyAbility(attacker, defender, defenderRuntime.Context, ability, consumeResources);

    private BattleLogEntry ResolveEnemyAbility(Enemy attacker, LiveCharacter defender,
        BattleRuntimeContext defenderContext, MonsterAbilityDefinition ability, bool consumeResources = true)
    {
        if (consumeResources)
        {
            attacker.StartAbilityCooldown(ability.Id, ability.Cooldown);
            attacker.ConsumeAbilityCharge(ability);
        }
        if (_random.Next(100) >= ability.ChancePercent)
            return new BattleLogEntry($"👁️ {attacker.Name} használja: {ability.Name}, de {defender.Name} ellenáll.",
                BattleLogKind.Information);
        var effects = new List<string>();
        foreach (var component in ability.Effects)
        {
            if (IsStatusEffect(component.Effect))
            {
                var status = ApplyMonsterStatusAbility(attacker.Definition, defender, component);
                if (!string.IsNullOrWhiteSpace(status)) effects.Add(status.Trim());
            }
        }
        var damageComponents = ability.Effects.Where(component => component.Effect == MonsterAbilityEffect.ExtraDamage)
            .Select(component =>
            {
                var type = component.DamageType ?? DamageType.Bludgeoning;
                var damage = Math.Max(0, component.Value - (defender.Armor?.Resistances?.Against(type) ?? 0));
                return (Type: type, Damage: damage);
            }).ToArray();
        var abilityDamage = damageComponents.Sum(component => component.Damage);
        var survival = abilityDamage > 0
            ? ApplyEnemyDamage(defender, abilityDamage, defenderContext)
            : DamageApplicationResult.Empty;
        if (abilityDamage > 0)
            effects.Add($"💥 {string.Join(" + ", damageComponents.Where(component => component.Damage > 0)
                .Select(component => $"{component.Damage} {component.Type.Name()}"))} sebzés, " +
                        $"{defender.Name} ❤️ {defender.CurrentVitality}/{defender.MaximumVitality} " +
                        survival.ShortLog);
        return new BattleLogEntry($"👁️ {attacker.Name} használja: {ability.Name} → {defender.Name}. " +
            (effects.Count == 0 ? "A hatás elmarad." : string.Join("; ", effects)), BattleLogKind.EnemyAttack);
    }

    public void MarkEnemyWeaponUsed(Enemy enemy, WeaponDefinition? weapon)
    {
        if (IsTelegraphedWeapon(weapon))
        {
            enemy.StartWeaponCooldown(weapon!.Id, 3);
            enemy.ClearPreparedWeapon();
        }
    }

    public BattleLogEntry ResolveTeamEnemyAction(Enemy attacker, LiveCharacter defender,
        TeamCharacterBattleRuntime defenderRuntime, WeaponDefinition? attackWeapon = null,
        bool advanceAttackerEffects = true)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(defender);
        ArgumentNullException.ThrowIfNull(defenderRuntime);
        attackWeapon ??= SelectEnemyAttackWeapon(attacker);
        var attack = EnemyAttack(attacker.Definition, defender, defenderRuntime.Context, attacker.EffectiveSpeed,
            attackWeapon, allowWeaponFallback: false, attackerInstance: attacker);
        var survival = attack.Hit ? ApplyEnemyDamage(defender, attack.Damage, defenderRuntime.Context) : DamageApplicationResult.Empty;
        return new BattleLogEntry(
            $"{FormatAttackSummary(attacker.Name, defender.Name, [attack],
                defender.CurrentVitality, defender.MaximumVitality)} {survival.ShortLog}",
            attack.Critical ? BattleLogKind.CriticalHit : BattleLogKind.EnemyAttack,
            DescribeAction(attacker.Name, defender.Name, [attack], survival.Details));
    }

    public BattleLogEntry ResolveTeamOpportunityAttack(Enemy attacker, LiveCharacter defender,
        TeamCharacterBattleRuntime defenderRuntime)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(defender);
        ArgumentNullException.ThrowIfNull(defenderRuntime);
        var weapons = (attacker.Definition.Weapons ?? []).Where(weapon =>
            weapon.MaximumTargets == 1 && attacker.IsWeaponReady(weapon.Id)).ToArray();
        var opportunityWeapon = attacker.Definition.Weapon is { MaximumTargets: 1 } selected &&
                                attacker.IsWeaponReady(selected.Id)
            ? selected
            : weapons.Length > 0 ? weapons[_random.Next(weapons.Length)] : null;
        var attack = EnemyAttack(attacker.Definition, defender, defenderRuntime.Context, attacker.EffectiveSpeed,
            opportunityWeapon, allowWeaponFallback: false, attackerInstance: attacker);
        var survival = attack.Hit ? ApplyEnemyDamage(defender, attack.Damage, defenderRuntime.Context) : DamageApplicationResult.Empty;
        return new BattleLogEntry(
            $"↪️ {FormatAttackSummary(attacker.Name, defender.Name, [attack],
                defender.CurrentVitality, defender.MaximumVitality)} {survival.ShortLog}",
            attack.Critical ? BattleLogKind.CriticalHit : BattleLogKind.EnemyAttack,
            DescribeAction(attacker.Name, defender.Name, [attack], survival.Details));
    }

    public void SetTeamKnightProtection(TeamCharacterBattleRuntime runtime, LiveCharacter knight)
    {
        runtime.Context.KnightProtector = knight;
        runtime.Context.KnightProtectionAvailable = true;
    }

    /// <summary>
    /// Pontosan egy harci akciót old fel. Játékoskörben a null akció fizikai támadás; ellenfélkörben
    /// játékosakció nem adható. A supportDamage ugyanúgy az akció előtt érvényesül, mint a régi ciklusban.
    /// A BattlePlayerAction már hostoldalon feloldott eredmény; hálózatról érkező nyers DTO-t nem szabad ide átadni.
    /// </summary>
    public BattleStepResult Advance(BattleState state, BattlePlayerAction? playerAction = null, int supportDamage = 0)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.IsCompleted) throw new InvalidOperationException("A lezárt csata nem folytatható.");
        if (!state.IsPlayerTurn && playerAction is not null)
            throw new InvalidOperationException("Az ellenfél körében nem adható játékosakció.");
        if (supportDamage < 0) throw new ArgumentOutOfRangeException(nameof(supportDamage));

        var player = state.Player;
        var enemy = state.Enemy;
        var defender = state.Defender;
        var context = state.Context;
        var entries = new List<BattleLogEntry>();
        state.Round++;
        if (supportDamage > 0)
        {
            defender = defender with { HitPoints = Math.Max(0, defender.HitPoints!.Value - supportDamage) };
            if (defender.HitPoints <= 0)
            {
                var finishMessage = $"✨ A parti támogató varázslatai végeztek {enemy.Name}-vel.";
                state.Events.Add(finishMessage);
                entries.Add(new BattleLogEntry(finishMessage, BattleLogKind.PlayerAttack));
                state.Defender = defender;
                state.TurnId++;
                Complete(state);
                return new BattleStepResult(state, entries);
            }
        }
        string message;
        BattleLogKind kind;
        BattleActionDetails? actionDetails = null;
        if (state.IsPlayerTurn)
        {
            player.AdvanceSpellEffects();
            if (playerAction is not null)
            {
                if (playerAction.DamageToEnemy > 0)
                    defender = defender with { HitPoints = Math.Max(0, defender.HitPoints!.Value - playerAction.DamageToEnemy) };
                state.QueuedPlayerActions += playerAction.ExtraPlayerActions;
                var statusTicks = player.ApplyTurnEndStatusEffects(_random);
                var statusText = statusTicks.Count == 0 ? string.Empty :
                    $" Állapothatások: {string.Join(", ", statusTicks.Select(tick => $"{tick.Icon} {tick.Name}" + (tick.Damage > 0 ? $" -{tick.Damage} HP" : string.Empty) + (tick.Expired ? " (elmúlt)" : string.Empty)))}.";
                message = playerAction.Message + statusText;
                kind = playerAction.Kind;
            }
            else
            {
                var count = player.HasPerk(PerkIds.BarbarianBerserkerRage) && player.CurrentVitality * 2 < player.MaximumVitality ? 2 : 1;
                var attacks = new List<AttackResult>();
                var criticalHit = false;
                for (var index = 0; index < count && defender.HitPoints is > 0; index++)
                {
                    var attack = PlayerAttack(player, defender, context, enemy.EffectiveSpeed);
                    criticalHit |= attack.Critical;
                    defender = ApplyAttack(defender, attack);
                    attacks.Add(attack);
                    if (index == 0 && attack.Hit && defender.HitPoints is > 0 && player.HasPerk(PerkIds.FighterSteelStorm) && _random.NextDouble() < 0.35)
                    {
                        var extra = PlayerAttack(player, defender, context, enemy.EffectiveSpeed);
                        criticalHit |= extra.Critical;
                        defender = ApplyAttack(defender, extra);
                        attacks.Add(extra with { Message = $"Acélvihar: {extra.Message}" });
                    }
                }
                var statusTicks = player.ApplyTurnEndStatusEffects(_random);
                var statusText = statusTicks.Count == 0 ? string.Empty :
                    $" Állapothatások: {string.Join(", ", statusTicks.Select(tick => $"{tick.Icon} {tick.Name}" + (tick.Damage > 0 ? $" -{tick.Damage} HP" : string.Empty) + (tick.Expired ? " (elmúlt)" : string.Empty)))}.";
                message = FormatAttackSummary(player.Name, enemy.Name, attacks,
                    defender.HitPoints!.Value, enemy.Definition.HitPoints!.Value) + statusText;
                kind = criticalHit ? BattleLogKind.CriticalHit : BattleLogKind.PlayerAttack;
                actionDetails = DescribeAction(player.Name, enemy.Name, attacks, statusText);
            }
        }
        else
        {
            enemy.SetCurrentHitPoints(defender.HitPoints ?? 0);
            var turnStart = BeginEnemyTurn(enemy);
            defender = defender with { HitPoints = enemy.CurrentHitPoints };
            var effectText = turnStart.Entries.Count == 0 ? string.Empty :
                $" {string.Join(" ", turnStart.Entries.Select(entry => entry.Message))}";
            if (defender.HitPoints <= 0)
            {
                message = $"{enemy.Name} elbukik a varázshatásoktól.{effectText}";
                kind = BattleLogKind.PlayerAttack;
            }
            else if (!turnStart.CanAct)
            {
                message = $"{enemy.Name} varázshatás miatt kihagyja az akcióját.{effectText}";
                kind = BattleLogKind.Information;
            }
            else if (enemy.PreparedWeaponId is null && SelectEnemyActiveAbility(enemy, 1) is { } activeAbility)
            {
                var activeEntry = ResolveEnemyAbility(enemy, player, context, activeAbility);
                message = activeEntry.Message + effectText;
                kind = activeEntry.Kind;
                actionDetails = activeEntry.Details;
            }
            else
            {
                var weapon = SelectEnemyAttackWeapon(enemy);
                if (IsTelegraphedWeapon(weapon) && !enemy.IsWeaponPrepared(weapon!.Id))
                {
                    var preparation = PrepareEnemyWeapon(enemy, weapon);
                    message = preparation.Message + effectText;
                    kind = preparation.Kind;
                }
                else
                {
                    var attack = EnemyAttack(defender, player, context, enemy.EffectiveSpeed, weapon,
                        allowWeaponFallback: false, attackerInstance: enemy);
                    MarkEnemyWeaponUsed(enemy, weapon);
                    var survival = attack.Hit ? ApplyEnemyDamage(player, attack.Damage, context) : DamageApplicationResult.Empty;
                    message = $"{FormatAttackSummary(enemy.Name, player.Name, [attack],
                        player.CurrentVitality, player.MaximumVitality)} {survival.ShortLog}{effectText}";
                    kind = attack.Critical ? BattleLogKind.CriticalHit : BattleLogKind.EnemyAttack;
                    actionDetails = DescribeAction(enemy.Name, player.Name, [attack], survival.Details + effectText);
                }
            }
        }
        state.Defender = defender;
        state.Events.Add(message);
        entries.Add(new BattleLogEntry(message, kind, actionDetails));
        if (state.IsPlayerTurn && context.BarbarianRageActionsRemaining > 0)
            context.BarbarianRageActionsRemaining--;
        if (state.IsPlayerTurn && state.QueuedPlayerActions > 0)
            state.QueuedPlayerActions--;
        else
            state.IsPlayerTurn = !state.IsPlayerTurn;
        state.TurnId++;
        if (player.CurrentVitality <= 0 || state.CurrentEnemyHitPoints <= 0) Complete(state);
        return new BattleStepResult(state, entries);
    }

    private static void Complete(BattleState state)
    {
        state.Enemy.SetCurrentHitPoints(state.CurrentEnemyHitPoints);
        state.IsCompleted = true;
        state.Result = new BattleResult(state.Player.CurrentVitality > 0, state.Round, state.Events.ToList());
    }

    private static EnemyDefinition ApplyAttack(EnemyDefinition defender, AttackResult attack) => attack.Hit
        ? defender with { HitPoints = Math.Max(0, defender.HitPoints!.Value - attack.Damage) }
        : defender;

    private static string FormatAttackSummary(string attackerName, string defenderName,
        IReadOnlyList<AttackResult> attacks, int currentHitPoints, int maximumHitPoints)
    {
        var successful = attacks.Where(attack => attack.Hit).ToArray();
        var critical = attacks.Any(attack => attack.Critical);
        var outcome = successful.Length == 0
            ? "💨 MELLÉ"
            : critical ? "💥 KRITIKUS!" : "🎯 TALÁLAT";
        var summary = $"{attackerName}\t→ {defenderName}\t{outcome}";
        if (successful.Length > 0)
            summary += $"\t💥 {successful.Sum(attack => attack.Damage)}\t{defenderName} ❤️ {currentHitPoints}/{maximumHitPoints}";
        return summary;
    }

    private static BattleActionDetails DescribeAction(string actor, string target,
        IReadOnlyList<AttackResult> attacks, string effects)
    {
        var summary = new List<string>
        {
            $"🎯 {attacks.Count(attack => attack.Hit)}/{attacks.Count} találat",
            $"💥 Sebzés: {attacks.Sum(attack => attack.Damage)}",
            $"🎲 Kritikus: {attacks.FirstOrDefault()?.Details?.CriticalChancePercent ?? 0:0.##}%" +
                (attacks.Any(attack => attack.Critical) ? " KRITIKUS!" : " — nem")
        };
        var calculation = new List<string>();
        for (var i = 0; i < attacks.Count; i++)
        {
            var attack = attacks[i];
            calculation.Add($"⚔ {i + 1}. ütés: {(attack.Hit ? "talált" : "mellé")}");
            if (attack.Details is { } detail)
            {
                calculation.Add($"🎯 {detail.Hit}");
                calculation.Add($"🎲 {detail.CriticalChancePercent:0.##}%; kritikus ×{detail.CriticalMultiplier}");
                calculation.AddRange(detail.Calculation);
                calculation.Add($"💥 Végső sebzés: {detail.Damage}");
            }
            else calculation.Add(attack.Message);
        }
        if (!string.IsNullOrWhiteSpace(effects)) calculation.Add(effects);
        return new(Guid.NewGuid(), actor, target, summary, calculation);
    }

    private static void ApplyBattleStartPerks(LiveCharacter player, Action<BattleLogEntry> onRound)
    {
        if (player.HasPerk(PerkIds.KnightHolyOath))
        {
            var before = player.CurrentVitality;
            player.RestoreVitality(10);
            var restored = player.CurrentVitality - before;
            if (restored > 0) onRound(new BattleLogEntry($"Szent eskü: +{restored} HP.", BattleLogKind.Information));
        }
        if (player.HasPerk(PerkIds.PriestFaithSource))
        {
            var before = player.CurrentMana;
            player.RestoreMana(5);
            var restored = player.CurrentMana - before;
            if (restored > 0) onRound(new BattleLogEntry($"Hitforrás: +{restored} manna.", BattleLogKind.Information));
        }
        var magicHealing = player.GetMagicItemBonus(MagicItemEffect.BattleHeal);
        if (magicHealing > 0)
        {
            var before = player.CurrentVitality;
            player.RestoreVitality(magicHealing);
            var restored = player.CurrentVitality - before;
            if (restored > 0) onRound(new BattleLogEntry($"Varázstárgy: +{restored} HP.", BattleLogKind.Information));
        }
        var magicMana = player.GetMagicItemBonus(MagicItemEffect.BattleMana);
        if (magicMana > 0 && player.UsesMana)
        {
            var before = player.CurrentMana;
            player.RestoreMana(magicMana);
            var restored = player.CurrentMana - before;
            if (restored > 0) onRound(new BattleLogEntry($"Varázstárgy: +{restored} manna.", BattleLogKind.Information));
        }
    }

    private InitiativeRoll RollInitiative(int speed)
    {
        var modifier = _random.Next(2) == 0 ? -Roll(new ValueRange(1, 2)) : Roll(new ValueRange(1, 2));
        return new InitiativeRoll(speed + modifier, $"±1d2({modifier:+#;-#;0})");
    }

    private AttackResult PlayerAttack(LiveCharacter player, EnemyDefinition defender, BattleRuntimeContext context, int defenderSpeed)
    {
        player.BreakSanctuary();
        var forcedHit = context.ShadowStepReady;
        context.ShadowStepReady = false;
        var weapon = player.ActiveWeapons.FirstOrDefault(item =>
            item is not null && item.WeaponTypeId != DefenseWeaponTypeId);
        var blessedWeaponBonus = player.HasPerk(PerkIds.PriestBlessedWeapon) &&
                                 defender.HasTrait(EnemyTraits.Undead) ? 2 : 0;
        var invisibilityBonus = player.SpellEffectValue(ActiveSpellEffectType.Invisibility);
        var strengthHitBonus = StrengthHitBonus(player);
        var classHitBonus = ClassHitBonus(player);
        var weaponFamily = WeaponFamilies.ForWeapon(weapon);
        var weaponRank = player.WeaponProficiencyRankFor(weaponFamily);
        var oathbladeBonus = UsesRodericOathblade(player, weapon) ? 1 : 0;
        var retaliation = context.KnightRetaliationReady;
        context.KnightRetaliationReady = false;
        var hitBonus = PlayerHitBonus(player, context.Tactic, weapon is not null, invisibilityBonus,
            strengthHitBonus, blessedWeaponBonus) + (weapon?.MagicPower ?? 0) + (retaliation ? 2 : 0) +
                       (weaponFamily == WeaponFamilies.Sword && weaponRank is not null ? 1 : 0);
        hitBonus += oathbladeBonus;
        var hit = HitRoll(player.EffectiveAbilities.Dexterity, defenderSpeed, hitBonus - player.StatusHitPenalty, forcedHit);
        if (invisibilityBonus > 0) player.BreakInvisibility();
        var strengthHitText = strengthHitBonus > 0 ? $" [Erő-találat +{strengthHitBonus}]" : string.Empty;
        var classHitText = classHitBonus > 0 ? $" [Osztályjártasság +{classHitBonus}]" : string.Empty;
        var magicWeaponHitText = weapon?.MagicPower > 0 ? $" [Mágikus fegyver +{weapon.MagicPower} találat]" : string.Empty;
        var magicWeaponCriticalText = weapon?.MagicPower switch
        {
            2 => " [Mágikus fegyver +5% kritikus esély, természetes 19–20]",
            >= 3 => " [Mágikus fegyver +10% kritikus esély, természetes 18–20]",
            _ => string.Empty
        };
        var thirstHitText = player.StatusHitPenalty > 0 && player.HasStatus(CharacterStatusIds.Thirsty)
            ? $" [💧 szomjúság -{player.StatusHitPenalty} találat]"
            : string.Empty;
        var criticalChanceBonusPercent = weapon?.MagicPower switch
        {
            2 => 5,
            >= 3 => 10,
            _ => 0
        };
        if (player.HasPerk(PerkIds.ThiefDeadlyAccuracy)) criticalChanceBonusPercent += 10;
        if (weaponFamily == WeaponFamilies.Dagger && weaponRank == WeaponProficiencyRank.Master)
            criticalChanceBonusPercent += 5;
        if (context.Tactic == BattleTactic.ThiefObserve &&
            player.HasClassFeatureUpgrade(ClassFeatureUpgrades.ThiefObserve))
            criticalChanceBonusPercent += 5;
        var criticalNaturalRollMinimum = Math.Max(1, 20 - criticalChanceBonusPercent / 5);
        var criticalMultiplier = player.HasPerk(PerkIds.ThiefDeadlyAccuracy) && hit.NaturalRoll >= 18
            ? 3
            : weaponFamily == WeaponFamilies.Axe && weaponRank == WeaponProficiencyRank.Master && hit.NaturalRoll == 20
                ? 3
            : weaponFamily == WeaponFamilies.Dagger && weaponRank == WeaponProficiencyRank.Master && hit.NaturalRoll >= 19
                ? 2
            : hit.NaturalRoll >= criticalNaturalRollMinimum || context.Tactic == BattleTactic.ThiefObserve && hit.NaturalRoll == 19 &&
              player.HasClassFeatureUpgrade(ClassFeatureUpgrades.ThiefObserve) ? 2 : 1;
        var calculation = new List<string>
        {
            $"🎯 d20={hit.NaturalRoll}; ügyesség {player.EffectiveAbilities.Dexterity}",
            $"🎯 Cél: 11 + gyorsaság {defenderSpeed} = {11 + defenderSpeed}",
            $"🎯 Összes módosító: {hitBonus - player.StatusHitPenalty:+#;-#;0}",
            $"🎲 Kritikus alap 5%; bónusz +{criticalChanceBonusPercent}%",
            $"🎲 Kritikus küszöb: {criticalNaturalRollMinimum}–20"
        };
        void Modifier(string name, int value) { if (value != 0) calculation.Add($"{name}: {value:+#;-#;0}"); }
        Modifier("🎯 Erő", strengthHitBonus);
        Modifier("🎯 Osztályjártasság", classHitBonus);
        Modifier("🎯 Fegyvermester", weapon is not null && player.HasPerk(PerkIds.FighterWeaponMaster) ? 1 : 0);
        Modifier("🎯 Varázstárgy", player.GetMagicItemBonus(MagicItemEffect.Hit));
        Modifier("🎯 Varázshatás", player.SpellEffectValue(ActiveSpellEffectType.HitBonus));
        Modifier("🎯 Láthatatlanság", invisibilityBonus);
        Modifier("🎯 Áldott fegyver", blessedWeaponBonus);
        Modifier("🎯 Mágikus fegyver", weapon?.MagicPower ?? 0);
        Modifier("🎯 Kardjártasság", weaponFamily == WeaponFamilies.Sword && weaponRank is not null ? 1 : 0);
        Modifier("🎯 Esküpenge", oathbladeBonus);
        Modifier("🎯 Megtorlás", retaliation ? 2 : 0);
        Modifier("🎯 Taktika", context.Tactic is BattleTactic.FighterPrecise or BattleTactic.ThiefObserve ? 2 :
            context.Tactic == BattleTactic.FighterPowerful ? -1 : 0);
        Modifier(player.HasStatus(CharacterStatusIds.Thirsty) ? "💧 Szomjúság: találat" : "🎯 Állapotbüntetés",
            -player.StatusHitPenalty);
        Modifier("🎲 Mágikus fegyver (%)", weapon?.MagicPower >= 3 ? 10 : weapon?.MagicPower == 2 ? 5 : 0);
        Modifier("🎲 Halálos pontosság (%)", player.HasPerk(PerkIds.ThiefDeadlyAccuracy) ? 10 : 0);
        Modifier("🎲 Tőrmester (%)", weaponFamily == WeaponFamilies.Dagger && weaponRank == WeaponProficiencyRank.Master ? 5 : 0);
        Modifier("🎲 Megfigyelés (%)", context.Tactic == BattleTactic.ThiefObserve &&
            player.HasClassFeatureUpgrade(ClassFeatureUpgrades.ThiefObserve) ? 5 : 0);
        // Natural critical range is not sufficient: the attack must also hit.
        var criticalChance = Enumerable.Range(1, 20).Count(roll =>
            (forcedHit || roll != 1 && (roll == 20 || roll + player.EffectiveAbilities.Dexterity +
                hitBonus - player.StatusHitPenalty >= 11 + defenderSpeed)) &&
            roll >= criticalNaturalRollMinimum) * 5d;
        calculation.Add($"🎲 Tényleges kritikus esély: {criticalChance:0.##}%");
        AttackResult Detailed(AttackResult result) => result with
        {
            Details = new AttackDetails(hit.Description, result.Damage, criticalChance,
                result.Critical ? criticalMultiplier : 1, calculation.ToArray())
        };
        if (!hit.Hit)
        {
            context.ConsecutivePlayerHits = 0;
            return Detailed(AttackResult.Miss($"találat: {hit.Description}{thirstHitText}{magicWeaponHitText}{magicWeaponCriticalText} → 💨.{strengthHitText}{classHitText}"));
        }

        var baseDamage = weapon?.Damage is { } range ? Roll(range) : Roll(new ValueRange(1, 2));
        var usesDexterity = weapon is not null && string.Equals(weapon.WeaponTypeId, DexterityWeaponTypeId, StringComparison.OrdinalIgnoreCase);
        var ability = usesDexterity ? player.EffectiveAbilities.Dexterity : player.EffectiveAbilities.Strength;
        var abilityBonus = AbilityDamageBonus(ability);
        var randomBonus = Roll(new ValueRange(0, 2));
        var perkBonus = player.GetMagicItemBonus(MagicItemEffect.Damage) + blessedWeaponBonus +
                        player.SpellEffectValue(ActiveSpellEffectType.DamageBonus);
        var notes = new List<string>();
        Modifier("💥 Varázstárgy", player.GetMagicItemBonus(MagicItemEffect.Damage));
        Modifier("💥 Varázshatás", player.SpellEffectValue(ActiveSpellEffectType.DamageBonus));
        if (oathbladeBonus > 0)
        {
            perkBonus += 2;
            notes.Add("Esküpenge +1 találat és +2 sebzés");
        }
        if (weaponFamily == WeaponFamilies.Sword && weaponRank is not null)
            notes.Add("⚔️ Kardjártasság +1 találat");
        if (weaponFamily == WeaponFamilies.Dagger && weaponRank is not null)
        { perkBonus += 1; notes.Add("🗡️ Tőrjártasság +1 sebzés"); }
        if (weaponFamily == WeaponFamilies.Axe && weaponRank is not null)
        { perkBonus += 2; notes.Add("🪓 Bárdjártasság +2 sebzés"); }
        if (blessedWeaponBonus > 0) notes.Add("Áldott fegyver +2");
        if (player.HasPerk(PerkIds.BarbarianBloodlust) && player.CurrentVitality * 2 < player.MaximumVitality) { perkBonus += 3; notes.Add("Vérszomj +3"); }
        if (player.HasPerk(PerkIds.BarbarianPrimalStrength)) { perkBonus += 5; notes.Add("Őserő +5"); }
        if (player.HasPerk(PerkIds.BarbarianRage))
        {
            perkBonus += context.ConsecutivePlayerHits;
            if (context.ConsecutivePlayerHits > 0) notes.Add($"Őrjöngés +{context.ConsecutivePlayerHits}");
        }
        if (context.BarbarianRageActionsRemaining > 0)
        {
            var rageRange = player.HasClassFeatureUpgrade(ClassFeatureUpgrades.BarbarianWildRage)
                ? new ValueRange(7, 12)
                : player.HasClassFeatureUpgrade(ClassFeatureUpgrades.BarbarianEnduringRage)
                    ? new ValueRange(4, 7)
                    : new ValueRange(5, 10);
            var rageBonus = Roll(rageRange);
            perkBonus += rageBonus;
            notes.Add($"🔥 Düh +{rageBonus}");
        }
        if (retaliation) { perkBonus += 4; notes.Add("⚔️ Megtorlás: +2 találat, +4 sebzés"); }
        var damageType = weapon?.DamageType ?? DamageType.Bludgeoning;
        var typeDefense = defender.Resistances?.Against(damageType) ?? 0;
        calculation.Add($"💥 Sebzéstípus: {damageType.Name()}; típusvédelem {typeDefense:+#;-#;0}");
        var armor = Math.Max(0, (defender.Armor ?? 0) + MonsterAbilityValue(defender, MonsterAbilityEffect.ArmorBonus) + typeDefense);
        var powerfulMastery = context.Tactic == BattleTactic.FighterPowerful &&
                              player.HasClassFeatureUpgrade(ClassFeatureUpgrades.FighterPowerful);
        var armorPiercing = weapon?.IsTwoHanded == true || context.Tactic == BattleTactic.FighterPowerful;
        var armorAfterPiercing = powerfulMastery ? (armor + 3) / 4 : armorPiercing ? (armor + 1) / 2 : armor;
        var bluntArmorIgnored = weaponFamily == WeaponFamilies.Blunt ? weaponRank switch
        {
            WeaponProficiencyRank.Master => 4,
            WeaponProficiencyRank.Trained => 2,
            _ => 0
        } : 0;
        var effectiveArmor = Math.Max(0, armorAfterPiercing - bluntArmorIgnored);
        var damageMultiplierPercent = 100;
        if (context.AmbushAvailable)
        {
            damageMultiplierPercent = player.HasClassFeatureUpgrade(ClassFeatureUpgrades.ThiefAmbush) ? 250 : 200;
            context.AmbushAvailable = false;
            notes.Add($"Orvtámadás ×{damageMultiplierPercent / 100d:0.##}");
        }
        if (criticalMultiplier > 1)
            notes.Add(criticalMultiplier == 3
                ? weaponFamily == WeaponFamilies.Axe && weaponRank == WeaponProficiencyRank.Master && hit.NaturalRoll == 20 &&
                  !player.HasPerk(PerkIds.ThiefDeadlyAccuracy)
                    ? "Bárdmester kritikus sebzés ×3"
                    : "Halálos pontosság kritikus sebzés ×3"
                : "Kritikus sebzés ×2");
        if (magicWeaponCriticalText.Length > 0)
            notes.Add(magicWeaponCriticalText.Trim());
        damageMultiplierPercent *= criticalMultiplier;
        if (weaponFamily == WeaponFamilies.Polearm && weaponRank == WeaponProficiencyRank.Master &&
            context.PolearmMasterOpeningAvailable)
        {
            damageMultiplierPercent = damageMultiplierPercent * 150 / 100;
            context.PolearmMasterOpeningAvailable = false;
            notes.Add("🔱 Szálfegyver-mester: első találat ×1,5");
        }
        var rawDamage = baseDamage + abilityBonus + randomBonus + perkBonus;
        var damage = ApplyDefense((rawDamage * damageMultiplierPercent + 99) / 100, effectiveArmor);
        var statusDamagePenalty = player.StatusPhysicalDamagePenalty;
        Modifier(player.HasStatus(CharacterStatusIds.Hungry) ? "🍖 Éhség: fizikai sebzés" : "💥 Állapotbüntetés",
            -statusDamagePenalty);
        damage = Math.Max(1, damage - statusDamagePenalty);
        if (statusDamagePenalty > 0)
            notes.Add(player.HasStatus(CharacterStatusIds.Hungry)
                ? $"🍖 éhség -{statusDamagePenalty} fizikai sebzés"
                : $"állapot -{statusDamagePenalty} fizikai sebzés");
        switch (context.Tactic)
        {
            case BattleTactic.FighterPrecise:
                var precisePercent = player.HasClassFeatureUpgrade(ClassFeatureUpgrades.FighterPrecise) ? 85 : 75;
                damage = Math.Max(1, damage * precisePercent / 100);
                notes.Add($"Pontos: +2 találat, ×0,{precisePercent} sebzés");
                break;
            case BattleTactic.FighterPowerful:
                damage = Math.Max(1, (damage * 125 + 99) / 100);
                notes.Add($"Erőteljes: -1 találat, ×1,25 sebzés, {(powerfulMastery ? 75 : 50)}% páncéltörés");
                break;
            case BattleTactic.FighterDefensive:
                damage = Math.Max(1, damage * 75 / 100);
                notes.Add($"Védekező: ×0,75 sebzés, +{(player.HasClassFeatureUpgrade(ClassFeatureUpgrades.FighterDefensive) ? 4 : 3)} védelem");
                break;
        }
        if (player.HasPerk(PerkIds.ThiefPoisoner))
        {
            var poison = Roll(new ValueRange(1, 6));
            damage += poison;
            notes.Add($"Méreg +{poison}");
        }
        if (context.Tactic == BattleTactic.ThiefPoison)
        {
            var poison = Roll(player.HasClassFeatureUpgrade(ClassFeatureUpgrades.ThiefPoison)
                ? new ValueRange(2, 6) : new ValueRange(1, 4));
            damage += poison;
            notes.Add($"Mérgezett penge +{poison}");
        }
        if (context.BarbarianRageActionsRemaining > 0 &&
            player.HasClassFeatureUpgrade(ClassFeatureUpgrades.BarbarianBloodRage))
        {
            var before = player.CurrentVitality;
            player.RestoreVitality(Roll(new ValueRange(1, 3)));
            var restored = player.CurrentVitality - before;
            if (restored > 0) notes.Add($"❤️‍🔥 Vérdüh +{restored} HP");
        }
        context.ConsecutivePlayerHits++;
        var noteText = notes.Count == 0 ? string.Empty : $" [{string.Join(", ", notes)}]";
        var perkBonusText = perkBonus == 0 ? string.Empty : $" + bónusz {perkBonus}";
        var armorText = armorPiercing
            ? $"páncél {armor} → {armorAfterPiercing} ({(context.Tactic == BattleTactic.FighterPowerful ? "💥 erőteljes páncéltörés" : "⚒️ páncéltörés")})"
            : $"páncél {armor}";
        if (bluntArmorIgnored > 0)
            armorText += $" → {effectiveArmor} (🔨 jártasság -{bluntArmorIgnored})";
        var damageText = damage > 0 ? $"💥 {damage}" : "0";
        var damageAbilityName = usesDexterity ? "Ügyesség" : "Erő";
        calculation.Add($"💥 Fegyver alapsebzése: {weapon?.Name ?? "Puszta kéz"} → {baseDamage}");
        calculation.Add($"💥 {damageAbilityName}bónusz: {damageAbilityName} {ability} → +{abilityBonus}");
        calculation.Add($"🎲 Véletlen sebzésbónusz (0–2): +{randomBonus}");
        calculation.Add($"💥 Nyers sebzés: fegyver {baseDamage} + {damageAbilityName.ToLowerInvariant()} {abilityBonus} + véletlen {randomBonus} + egyéb {perkBonus} = {rawDamage}");
        var multipliedDamage = rawDamage * damageMultiplierPercent / 100d;
        var roundedMultipliedDamage = (rawDamage * damageMultiplierPercent + 99) / 100;
        calculation.Add(damageMultiplierPercent == 100
            ? $"💥 Sebzésszorzó: ×1 → {rawDamage}"
            : multipliedDamage == roundedMultipliedDamage
                ? $"💥 Sebzésszorzó: ×{damageMultiplierPercent / 100d:0.##} → {roundedMultipliedDamage}"
                : $"💥 Sebzésszorzó: ×{damageMultiplierPercent / 100d:0.##}; {multipliedDamage:0.##} → {roundedMultipliedDamage} (felfelé kerekítve)");
        calculation.Add($"🛡️ {armorText}; effektív {effectiveArmor}");
        calculation.AddRange(notes);
        calculation.Add("💥 Páncél után min. 1; éhség után min. 1; majd taktika és méreg.");
        return Detailed(AttackResult.HitFor(damage,
            $"találat: {hit.Description}{thirstHitText} → 🎯;{strengthHitText}{classHitText} sebzés: (alap {baseDamage} + képesség {abilityBonus} + dobás {randomBonus}{perkBonusText}) ×{damageMultiplierPercent / 100d:0.##} - {armorText} = {damageText}.{noteText}",
            criticalMultiplier > 1));
    }

    public int EstimatePlayerHitChance(LiveCharacter player, Enemy enemy, BattleTactic tactic)
    {
        var defender = enemy.Definition with { HitPoints = enemy.CurrentHitPoints };
        var weapon = player.ActiveWeapons.FirstOrDefault(item => item is not null && item.WeaponTypeId != DefenseWeaponTypeId);
        var weaponEquipped = weapon is not null;
        var blessedWeaponBonus = player.HasPerk(PerkIds.PriestBlessedWeapon) &&
                                 defender.HasTrait(EnemyTraits.Undead) ? 2 : 0;
        var bonus = PlayerHitBonus(player, tactic, weaponEquipped,
            player.SpellEffectValue(ActiveSpellEffectType.Invisibility), StrengthHitBonus(player), blessedWeaponBonus) -
                    player.StatusHitPenalty +
                    (weapon?.MagicPower ?? 0) +
                    (WeaponFamilies.ForWeapon(weapon) == WeaponFamilies.Sword &&
                     player.WeaponProficiencyRankFor(WeaponFamilies.Sword) is not null ? 1 : 0) +
                    (UsesRodericOathblade(player, weapon) ? 1 : 0);
        var target = 11 + enemy.EffectiveSpeed;
        var successfulRolls = Enumerable.Range(1, 20).Count(roll =>
            roll != 1 && (roll == 20 || roll + player.EffectiveAbilities.Dexterity + bonus >= target));
        return successfulRolls * 5;
    }

    private int PlayerHitBonus(LiveCharacter player, BattleTactic? tactic,
        bool weaponEquipped, int invisibilityBonus, int strengthHitBonus, int blessedWeaponBonus)
    {
        var tacticHitBonus = tactic switch
        {
            BattleTactic.FighterPrecise => 2,
            BattleTactic.FighterPowerful => -1,
            BattleTactic.ThiefObserve => 2,
            _ => 0
        };
        return (weaponEquipped && player.HasPerk(PerkIds.FighterWeaponMaster) ? 1 : 0) +
               player.GetMagicItemBonus(MagicItemEffect.Hit) + blessedWeaponBonus + invisibilityBonus +
               strengthHitBonus + player.SpellEffectValue(ActiveSpellEffectType.HitBonus) +
               ClassHitBonus(player) + tacticHitBonus;
    }

    private static bool UsesRodericOathblade(LiveCharacter player, WeaponDefinition? weapon) =>
        player.HasPerk(PerkIds.RodericOathblade) && weapon is not null &&
        string.Equals(weapon.Id, CharacterBoundItemRules.RodericGreatswordId,
            StringComparison.OrdinalIgnoreCase);

    private static int ClassHitBonus(LiveCharacter player) => player.CharacterClass.Id switch
    {
        CharacterClassIds.Harcos => Math.Min(4, 1 + player.Level / 5),
        CharacterClassIds.Barbár or CharacterClassIds.Lovag => Math.Min(3, player.Level / 5),
        _ => 0
    };

    private int StrengthHitBonus(LiveCharacter character) => _strengthHitBonuses
        .Where(bonus => string.Equals(bonus.CharacterClassId, character.CharacterClass.Id,
            StringComparison.OrdinalIgnoreCase) && bonus.MinimumStrength <= character.EffectiveAbilities.Strength)
        .OrderByDescending(bonus => bonus.MinimumStrength)
        .Select(bonus => bonus.Bonus)
        .FirstOrDefault();

    private AttackResult EnemyAttack(EnemyDefinition attacker, LiveCharacter defender, BattleRuntimeContext context,
        int attackerSpeed, WeaponDefinition? attackWeapon = null, bool allowWeaponFallback = true,
        Enemy? attackerInstance = null)
    {
        var calculation = new List<string>();
        var criticalChance = 0d;
        var hitDescription = "Automatikus elkerülés";
        AttackResult Detailed(AttackResult result) => result with
        {
            Details = new AttackDetails(hitDescription, result.Damage, criticalChance,
                result.Critical ? 2 : 1, calculation.Append(result.Message).ToArray())
        };
        void Modifier(string label, int value) { if (value != 0) calculation.Add($"{label}: {value:+#;-#;0}"); }
        if (context.ChallengeAvailable) { context.ChallengeAvailable = false; return Detailed(AttackResult.Miss("💨 Kihívás: az első támadás automatikusan elhibázza.")); }
        if (defender.HasPerk(PerkIds.PriestSanctuary) && _random.NextDouble() < 0.20) return Detailed(AttackResult.Miss("💨 Szentély: az ellenfél elveszíti a támadását."));
        if (defender.HasSpellEffect(ActiveSpellEffectType.Invisibility)) return Detailed(AttackResult.Miss("💨 Láthatatlanság: az ellenfél nem talál célpontot."));
        var hit = HitRoll(attackerSpeed, defender.EffectiveAbilities.Dexterity, 0, false);
        criticalChance = 5;
        hitDescription = hit.Description;
        calculation.Add($"🎯 d20={hit.NaturalRoll}; gyorsaság {attackerSpeed}");
        calculation.Add($"🎯 Cél: 11 + ügyesség {defender.EffectiveAbilities.Dexterity}");
        calculation.Add("🎲 Kritikus: természetes 20, 5%, ×2");
        if (!hit.Hit) return Detailed(AttackResult.Miss($"találat: {hit.Description} → 💨."));
        var criticalMultiplier = hit.NaturalRoll == 20 ? 2 : 1;
        if (criticalMultiplier == 1 && defender.HasPerk(PerkIds.ThiefEvasion) && _random.NextDouble() < 0.15)
        {
            context.ShadowStepReady = defender.HasPerk(PerkIds.ThiefShadowStep);
            return Detailed(AttackResult.Miss("💨 Kitérés: a találat elkerülve." + (context.ShadowStepReady ? " Árnyéklépés aktiválva." : string.Empty)));
        }

        var strength = attacker.Strength ?? 1;
        var enemyWeapon = attackWeapon ?? (allowWeaponFallback ? SelectEnemyAttackWeapon(attacker) : null);
        var randomDamage = Roll(enemyWeapon?.Damage ?? new ValueRange(1, 2));
        var strengthBonus = AbilityDamageBonus(strength);
        var damageType = enemyWeapon?.DamageType ?? DamageType.Bludgeoning;
        calculation.Add($"💥 Fegyver: {enemyWeapon?.Name ?? "Puszta kéz"}; {damageType.Name()}; alapsebzés {randomDamage}; erőbónusz {strengthBonus}");
        var armorRoll = RollArmor(defender);
        var typeDefense = defender.Armor?.Resistances?.Against(damageType) ?? 0;
        var armor = Math.Max(0, armorRoll + typeDefense);
        calculation.Add($"🛡️ Típusvédelem: {typeDefense:+#;-#;0}");
        var shieldEquipped = defender.ActiveWeapons.Any(item => item?.WeaponTypeId == DefenseWeaponTypeId);
        var shield = defender.ActiveWeapons.FirstOrDefault(item => item?.WeaponTypeId == DefenseWeaponTypeId)?.Damage is { } shieldRange ? Roll(shieldRange) : 0;
        var shieldRank = defender.WeaponProficiencyRankFor(WeaponFamilies.Shield);
        var staffEquipped = defender.ActiveWeapons.Any(item =>
            WeaponFamilies.ForWeapon(item) == WeaponFamilies.Staff);
        var staffRank = defender.WeaponProficiencyRankFor(WeaponFamilies.Staff);
        var staffDefense = staffEquipped ? staffRank switch
        {
            WeaponProficiencyRank.Master => 2,
            WeaponProficiencyRank.Trained => 1,
            _ => 0
        } : 0;
        if (shieldRank == WeaponProficiencyRank.Master && shieldEquipped &&
            defender.ActiveWeapons.FirstOrDefault(item => item?.WeaponTypeId == DefenseWeaponTypeId)?.Damage is { } masterShieldRange)
            shield = Math.Max(shield, Roll(masterShieldRange));
        var evilWard = IsUnholy(attacker)
            ? defender.ActiveSpellEffects.Where(effect => effect.Type == ActiveSpellEffectType.ProtectionFromEvil).ToList()
            : [];
        var evilWardDefense = evilWard.Sum(effect => ParseInt(effect.Parameter));
        var tacticDefense = context.Tactic == BattleTactic.FighterDefensive
            ? defender.HasClassFeatureUpgrade(ClassFeatureUpgrades.FighterDefensive) ? 4 : 3
            : 0;
        var rageDefensePenalty = context.BarbarianRageActionsRemaining > 0
            ? defender.HasClassFeatureUpgrade(ClassFeatureUpgrades.BarbarianWildRage) ? 3 : 2
            : 0;
        var perkDefense = (defender.HasPerk(PerkIds.BarbarianThickSkin) ? 1 : 0) +
                          (shieldEquipped && defender.HasPerk(PerkIds.KnightShieldWall) ? 2 : 0) +
                          defender.GetMagicItemBonus(MagicItemEffect.Defense) +
                          defender.SpellEffectValue(ActiveSpellEffectType.DefenseBonus) + evilWardDefense +
                          (shieldEquipped && shieldRank is not null ? 1 : 0) +
                          staffDefense +
                          (defender.ActiveWeapons.Any(item => WeaponFamilies.ForWeapon(item) == WeaponFamilies.Sword) &&
                           defender.WeaponProficiencyRankFor(WeaponFamilies.Sword) == WeaponProficiencyRank.Master ? 1 : 0) +
                          tacticDefense - rageDefensePenalty;
        Modifier("🛡️ Vastag bőr", defender.HasPerk(PerkIds.BarbarianThickSkin) ? 1 : 0);
        Modifier("🛡️ Pajzsfal", shieldEquipped && defender.HasPerk(PerkIds.KnightShieldWall) ? 2 : 0);
        Modifier("🛡️ Varázstárgy", defender.GetMagicItemBonus(MagicItemEffect.Defense));
        Modifier("🛡️ Varázshatás", defender.SpellEffectValue(ActiveSpellEffectType.DefenseBonus));
        Modifier("🛡️ Gonosz elleni védelem", evilWardDefense);
        Modifier("🛡️ Pajzsjártasság", shieldEquipped && shieldRank is not null ? 1 : 0);
        Modifier("🦯 Botjártasság", staffDefense);
        Modifier("🛡️ Kardmester", defender.ActiveWeapons.Any(item => WeaponFamilies.ForWeapon(item) == WeaponFamilies.Sword) &&
            defender.WeaponProficiencyRankFor(WeaponFamilies.Sword) == WeaponProficiencyRank.Master ? 1 : 0);
        Modifier("🛡️ Védekező taktika", tacticDefense);
        Modifier("🛡️ Düh", -rageDefensePenalty);
        if (shieldRank == WeaponProficiencyRank.Master && shieldEquipped)
            calculation.Add($"🛡️ Pajzsmester: két dobás maximuma = {shield}");
        if (defender.HasPerk(PerkIds.KnightArmorMaster))
            calculation.Add($"🛡️ Páncélmester: legalább a felfelé kerekített átlag = {armor}");
        Modifier("🛡️ Megtörhetetlen", defender.HasPerk(PerkIds.FighterUnbreakable) ? 2 : 0);
        Modifier("🛡️ Legyőzhetetlen", defender.HasPerk(PerkIds.KnightInvincible) ? 4 : 0);
        var reduction = (defender.HasPerk(PerkIds.FighterUnbreakable) ? 2 : 0) + (defender.HasPerk(PerkIds.KnightInvincible) ? 4 : 0);
        var onHit = ResolveMonsterOnHitAbilities(attacker, attackerInstance, defender, enemyWeapon, calculation);
        var monsterBonusRolls = onHit.Damage;
        var matchingBonusDamage = monsterBonusRolls.Where(bonus =>
            bonus.DamageType is null || bonus.DamageType == damageType).Sum(bonus => bonus.Value);
        var rawDamage = strengthBonus + randomDamage + matchingBonusDamage;
        var damage = Math.Max(0, ApplyDefense(rawDamage * criticalMultiplier, armor + shield + perkDefense) - reduction);
        var physicalReduction = damageType.IsPhysical()
            ? defender.SpellEffectValue(ActiveSpellEffectType.PhysicalReduction)
            : 0;
        var percentageReduction = Math.Clamp(physicalReduction +
            defender.SpellEffectValue(ActiveSpellEffectType.Sanctuary) + evilWard.Sum(effect => effect.Value), 0, 100);
        Modifier("🛡️ Fizikai csökkentés (%)", physicalReduction);
        Modifier("🛡️ Szentély (%)", defender.SpellEffectValue(ActiveSpellEffectType.Sanctuary));
        Modifier("🛡️ Gonosz elleni csökkentés (%)", evilWard.Sum(effect => effect.Value));
        calculation.Add($"🛡️ Összes sebzéscsökkentés: {percentageReduction}% (0–100%)");
        if (percentageReduction > 0) damage = damage * (100 - percentageReduction) / 100;
        foreach (var group in monsterBonusRolls.Where(bonus => bonus.DamageType is not null && bonus.DamageType != damageType)
                     .GroupBy(bonus => bonus.DamageType!.Value))
        {
            var bonusType = group.Key;
            var bonusRaw = group.Sum(bonus => bonus.Value) * criticalMultiplier;
            var bonusTypeDefense = defender.Armor?.Resistances?.Against(bonusType) ?? 0;
            var bonusArmor = Math.Max(0, armorRoll + bonusTypeDefense);
            var bonusDamage = Math.Max(0, ApplyDefense(bonusRaw, bonusArmor + shield + perkDefense) - reduction);
            var bonusReduction = Math.Clamp((bonusType.IsPhysical() ? physicalReduction : 0) +
                defender.SpellEffectValue(ActiveSpellEffectType.Sanctuary) + evilWard.Sum(effect => effect.Value), 0, 100);
            bonusDamage = bonusDamage * (100 - bonusReduction) / 100;
            damage += bonusDamage;
            calculation.Add($"💥 {bonusType.Name()} képességsebzés: {bonusRaw} − védelem " +
                            $"{bonusArmor + shield + perkDefense + reduction}, −{bonusReduction}% = {bonusDamage}");
        }
        if (defender.HasPerk(PerkIds.BarbarianPainTolerance) && damage < 3) damage = 0;
        var absorbed = 0;
        if (damage > 0 && defender.HasPerk(PerkIds.MageMagicShield) && defender.CurrentMana > 0)
        {
            absorbed = Math.Min(defender.CurrentMana, (damage + 3) / 4);
            defender.SpendMana(absorbed);
            damage -= absorbed;
        }
        var perkDefenseText = perkDefense == 0 ? string.Empty : $" - bónuszvédelem {perkDefense}";
        var reductionText = reduction == 0 ? string.Empty : $" - csökkentés {reduction}";
        var manaShieldText = absorbed == 0 ? string.Empty : $" - mannapajzs {absorbed}";
        var monsterBonusText = matchingBonusDamage == 0 ? string.Empty : $" + szörnyképesség {matchingBonusDamage}";
        var statusText = onHit.StatusText;
        calculation.Add($"💥 Erőbónusz {strengthBonus} + fegyver {randomDamage} + azonos típusú szörnybónusz {matchingBonusDamage} = {rawDamage}");
        calculation.Add($"💥 ×{criticalMultiplier} − páncél {armor} − pajzs {shield} − bónuszvédelem {perkDefense}");
        calculation.Add($"💥 Minimum 1, majd fix csökkentés −{reduction}, majd −{percentageReduction}%, lefelé kerekítve");
        if (absorbed > 0) calculation.Add($"🔷 Mannapajzs: −{absorbed} sebzés / manna");
        var damageText = damage > 0 ? $"💥 {damage}" : "0";
        return Detailed(AttackResult.HitFor(damage,
            $"találat: {hit.Description} → 🎯; sebzés: (Erőbónusz {strengthBonus} + fegyver {randomDamage}{monsterBonusText}) ×{criticalMultiplier} - páncél {armor} - pajzs {shield}{perkDefenseText}{reductionText}{manaShieldText} = {damageText}.{statusText}",
            criticalMultiplier > 1));
    }

    private WeaponDefinition? SelectEnemyAttackWeapon(EnemyDefinition attacker)
    {
        if (attacker.Weapon is { } selectedWeapon) return selectedWeapon;
        return attacker.Weapons is { Count: > 0 } weapons ? weapons[_random.Next(weapons.Count)] : null;
    }

    private int MonsterAbilityValue(EnemyDefinition enemy, MonsterAbilityEffect effect) => enemy.AbilityIds
        .Where(_monsterAbilities.ContainsKey)
        .Select(abilityId => _monsterAbilities[abilityId])
        .SelectMany(ability => ability.Effects)
        .Where(component => component.Effect == effect)
        .Sum(component => component.Value);

    private MonsterOnHitResult ResolveMonsterOnHitAbilities(EnemyDefinition enemy, Enemy? enemyInstance,
        LiveCharacter defender, WeaponDefinition? weapon, ICollection<string>? calculation = null)
    {
        var rolls = new List<MonsterBonusDamageRoll>();
        var statuses = new List<string>();
        foreach (var ability in enemy.AbilityIds.Where(_monsterAbilities.ContainsKey)
                     .Select(abilityId => _monsterAbilities[abilityId])
                     .Where(ability => ability.Trigger == MonsterAbilityTrigger.OnHit &&
                                       AppliesToWeapon(ability, weapon) &&
                                       (enemyInstance?.IsAbilityReady(ability.Id) ?? true) &&
                                       (enemyInstance?.HasAbilityCharge(ability) ?? ability.ChargesPerBattle <= 0)))
        {
            var roll = _random.Next(100);
            if (roll >= ability.ChancePercent) continue;
            enemyInstance?.ConsumeAbilityCharge(ability);
            enemyInstance?.StartAbilityCooldown(ability.Id, ability.Cooldown);
            calculation?.Add($"✨ {ability.Name} aktiválódott ({ability.ChancePercent}%, dobás {roll + 1})");
            foreach (var component in ability.Effects)
            {
                if (component.Effect == MonsterAbilityEffect.ExtraDamage)
                {
                    rolls.Add(new MonsterBonusDamageRoll(component.Value, component.DamageType));
                    calculation?.Add($"💥 {ability.Name}: +{component.Value}" +
                                     (component.DamageType is { } type ? $" {type.Name()}" : string.Empty));
                }
                else if (IsStatusEffect(component.Effect))
                {
                    var status = ApplyMonsterStatusAbility(enemy, defender, component);
                    if (!string.IsNullOrWhiteSpace(status)) statuses.Add(status.Trim());
                }
            }
        }
        var statusText = statuses.Count == 0 ? string.Empty : $" ⚠️ ÁLLAPOT: {string.Join(", ", statuses)}!";
        return new MonsterOnHitResult(rolls, statusText);
    }

    private static bool AppliesToWeapon(MonsterAbilityDefinition ability, WeaponDefinition? weapon) =>
        ability.WeaponIds is not { Count: > 0 } || weapon is not null &&
        ability.WeaponIds.Contains(weapon.Id, StringComparer.OrdinalIgnoreCase);

    private static bool IsStatusEffect(MonsterAbilityEffect effect) => effect is
        MonsterAbilityEffect.Poison or MonsterAbilityEffect.Disease or MonsterAbilityEffect.Bleeding or
        MonsterAbilityEffect.ApplyStatus;

    private string ApplyMonsterStatusAbility(EnemyDefinition enemy, LiveCharacter defender,
        MonsterAbilityComponent component)
    {
        var statusId = component.Effect switch
        {
            MonsterAbilityEffect.Poison => CharacterStatusIds.Poisoned,
            MonsterAbilityEffect.Disease => CharacterStatusIds.Diseased,
            MonsterAbilityEffect.Bleeding => CharacterStatusIds.Bleeding,
            MonsterAbilityEffect.ApplyStatus => component.StatusId,
            _ => null
        };
            var sanctuaryImmunity = defender.HasSpellEffect(ActiveSpellEffectType.Sanctuary) &&
                                    statusId is CharacterStatusIds.Poisoned or CharacterStatusIds.Diseased or CharacterStatusIds.Bleeding;
            var evilWardImmunity = IsUnholy(enemy) && defender.HasSpellEffect(ActiveSpellEffectType.ProtectionFromEvil) &&
                                   statusId is CharacterStatusIds.Poisoned or CharacterStatusIds.Diseased;
            var racialResistance = defender.Race.HasTrait(RaceTraits.Resilient) &&
                                   statusId is CharacterStatusIds.Poisoned or CharacterStatusIds.Diseased &&
                                   _random.Next(100) < 50;
            if (statusId is null || statusId == CharacterStatusIds.Bleeding &&
                defender.HasSpellEffect(ActiveSpellEffectType.BleedingImmunity) || sanctuaryImmunity || evilWardImmunity ||
                !_statuses.TryGetValue(statusId, out var status)) return string.Empty;
            if (racialResistance)
            {
                return $" ⛰️ {defender.Race.Name} ellenállt: {status.Name}";
            }
            var wasActive = defender.HasStatus(statusId);
            var maximumVitalityBefore = defender.MaximumVitality;
            defender.AddStatus(status);
            var maximumVitalityChange = !wasActive && statusId == CharacterStatusIds.Diseased &&
                                        defender.MaximumVitality != maximumVitalityBefore
                ? $" (max ❤️ {maximumVitalityBefore}→{defender.MaximumVitality} HP)"
                : string.Empty;
            return $" {status.Icon} {status.Name}" +
                   (wasActive ? " időtartama újraindult" : " felkerült") + maximumVitalityChange;
    }

    private int RollArmor(LiveCharacter defender)
    {
        if (defender.Armor?.Defense is not { } range) return 0;
        var rolled = Roll(range);
        return defender.HasPerk(PerkIds.KnightArmorMaster) ? Math.Max(rolled, (int)Math.Ceiling((range.Minimum + range.Maximum) / 2.0)) : rolled;
    }

    private DamageApplicationResult ApplyEnemyDamage(LiveCharacter player, int damage, BattleRuntimeContext context)
    {
        var shortNotes = new List<string>();
        var details = new List<string>();
        if (damage > 0 && context.KnightProtectionAvailable)
        {
            context.KnightProtectionAvailable = false;
            var protector = context.KnightProtector;
            if (protector is not null && protector.IsAlive)
            {
                var divisor = protector.HasClassFeatureUpgrade(ClassFeatureUpgrades.KnightMarbleWall) ? 4 : 3;
                var transferredDamage = Math.Max(1, (damage + divisor - 1) / divisor);
                protector.ReceiveDamage(transferredDamage);
                if (protector.HasClassFeatureUpgrade(ClassFeatureUpgrades.KnightRetaliation))
                    protector.ReadyKnightRetaliation();
                shortNotes.Add($"🛡️ {protector.Name} közbelépett");
                details.Add($"🛡️ {protector.Name} közbelépett: a teljes {damage} sebzést kivédte, " +
                            $"és 💥 {transferredDamage} sebzést kapott (❤️ {protector.CurrentVitality}/{protector.MaximumVitality})." +
                            (divisor == 4 ? " Márványfal: a sebzés negyede." : " A sebzés harmada."));
                damage = 0;
            }
        }
        DamageApplicationResult Result(string shortMessage, string detail) =>
            new(string.Join(". ", shortNotes.Append(shortMessage).Where(value => value.Length > 0)),
                string.Join(". ", details.Append(detail).Where(value => value.Length > 0)));

        if (damage >= player.CurrentVitality && player.TakeSpellEffect(ActiveSpellEffectType.GuardianAngel) is { } angel)
        {
            player.ReceiveDamage(Math.Max(0, player.CurrentVitality - 1));
            var healing = ((angel.PeriodicDamage?.Roll(_random) ?? 0) + angel.IntelligenceBonus) *
                          angel.DamageMultiplierPercent / 100;
            var beforeHealing = player.CurrentVitality;
            player.RestoreVitality(healing);
            return Result($"👼 Őrangyal megóvta {player.Name}-t", $"👼 Őrangyal: a halálos csapás kivédve és +{player.CurrentVitality - beforeHealing} HP.");
        }
        if (damage >= player.CurrentVitality && context.GuardianAngelAvailable)
        {
            context.GuardianAngelAvailable = false;
            player.RestoreVitality(25);
            return Result($"👼 Őrangyal megóvta {player.Name}-t", "👼 Őrangyal: a halálos csapás kivédve és +25 HP.");
        }
        if (damage >= player.CurrentVitality && context.LastFortressAvailable)
        {
            context.LastFortressAvailable = false;
            player.ReceiveDamage(Math.Max(0, player.CurrentVitality - 1));
            return Result($"🏰 {player.Name} talpon maradt", "🏰 Utolsó erőd: 1 HP-n talpon marad.");
        }
        if (damage >= player.CurrentVitality && player.Race.HasTrait(RaceTraits.Relentless) &&
            !player.WasRelentlessUsedThisLevel)
        {
            player.ReceiveDamage(Math.Max(0, player.CurrentVitality - 1));
            player.MarkRelentlessUsedThisLevel();
            return Result($"🔥 {player.Name} túlélte a halálos csapást", $"🔥 Könyörtelen: {player.Name} 1 HP-n túléli a halálos csapást.");
        }
        if (damage >= player.CurrentVitality && player.HasPerk(PerkIds.PriestResurrection) &&
            !player.WasResurrectedThisLevel)
        {
            player.SetCurrentResources(player.MaximumVitality, player.CurrentMana);
            player.MarkResurrectedThisLevel();
            return Result($"✨ {player.Name} feltámadt", $"✨ Feltámadás: {player.Name} teljes HP-val visszatér a halálból.");
        }
        player.ReceiveDamage(damage);
        if (damage >= 5 && player.CharacterClass.Id == CharacterClassIds.Barbár &&
            !context.BarbarianRageTriggered)
        {
            context.BarbarianRageTriggered = true;
            context.BarbarianRageActionsRemaining = player.HasClassFeatureUpgrade(ClassFeatureUpgrades.BarbarianEnduringRage) ? 5 : 3;
            var rageText = player.HasClassFeatureUpgrade(ClassFeatureUpgrades.BarbarianWildRage)
                ? "+7–12 sebzés és -3 védelem"
                : player.HasClassFeatureUpgrade(ClassFeatureUpgrades.BarbarianEnduringRage)
                    ? "+4–7 sebzés és -2 védelem"
                    : "+5–10 sebzés és -2 védelem";
            shortNotes.Add($"🔥 {player.Name} Dühbe gurult");
            details.Add($"🔥 Düh: {context.BarbarianRageActionsRemaining} akcióig {rageText}");
        }
        return new(string.Join(". ", shortNotes), string.Join(". ", details));
    }

    private static bool IsUnholy(EnemyDefinition enemy) =>
        enemy.HasTrait(EnemyTraits.Undead) || enemy.HasTrait(EnemyTraits.Demonic);

    private static int ParseInt(string? value) => int.TryParse(value, out var parsed) ? parsed : 0;

    private HitRollResult HitRoll(int attackerSpeed, int defenderSpeed, int attackerBonus, bool forcedHit)
    {
        var roll = Roll(new ValueRange(1, 20));
        if (forcedHit) return new HitRollResult(true, roll, $"Árnyéklépés → automatikus találat ({roll})");
        var total = roll + attackerSpeed + attackerBonus;
        var target = 11 + defenderSpeed;
        var hit = roll != 1 && (roll == 20 || total >= target);
        var description = roll switch
        {
            1 => $"természetes 1 ({total} vs {target})",
            20 => $"természetes 20 ({total} vs {target})",
            _ => $"{total} vs {target}" + (attackerBonus != 0 ? $" ({attackerBonus:+#;-#;0} módosító)" : string.Empty)
        };
        return new HitRollResult(hit, roll, description);
    }

    private static int AbilityDamageBonus(int ability) => Math.Max(0, (ability - 1) / 2);
    private int Roll(ValueRange range) => _random.Next(range.Minimum, range.Maximum + 1);
    private static int ApplyDefense(int rawDamage, int defense) => Math.Max(1, rawDamage - defense);

    private sealed record DamageApplicationResult(string ShortLog, string Details)
    {
        public static DamageApplicationResult Empty { get; } = new(string.Empty, string.Empty);
    }

    private sealed record InitiativeRoll(int Total, string ModifierText);
    private sealed record HitRollResult(bool Hit, int NaturalRoll, string Description);
    private sealed record MonsterBonusDamageRoll(int Value, DamageType? DamageType);
    private sealed record MonsterOnHitResult(IReadOnlyList<MonsterBonusDamageRoll> Damage, string StatusText);
    private sealed record AttackResult(bool Hit, int Damage, string Message, bool Critical, AttackDetails? Details = null)
    {
        public static AttackResult Miss(string message) => new(false, 0, message, false);
        public static AttackResult HitFor(int damage, string message, bool critical = false) => new(true, damage, message, critical);
    }
}

public sealed record BattleResult(bool PlayerWon, int Rounds, IReadOnlyList<string> Events);
public sealed record BattleLogEntry(string Message, BattleLogKind Kind, BattleActionDetails? Details = null);
public sealed record EnemyTurnStartResult(bool CanAct, IReadOnlyList<BattleLogEntry> Entries);
public sealed record BattlePlayerAction(string Message, BattleLogKind Kind = BattleLogKind.PlayerAttack,
    int DamageToEnemy = 0, int ExtraPlayerActions = 0);
public enum BattleLogKind { Information, PlayerAttack, EnemyAttack, CriticalHit }
