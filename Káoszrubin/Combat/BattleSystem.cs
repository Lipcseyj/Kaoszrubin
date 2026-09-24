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

    public CombatantPreparation PrepareCharacter(LiveCharacter character)
    {
        ArgumentNullException.ThrowIfNull(character);
        var entries = new List<BattleLogEntry>();
        var runtime = new CharacterBattleChoices(character);
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

        var initiative = RollCharacterInitiative(character, includeTacticalDiscipline: true);
        entries.Add(new BattleLogEntry(
            $"⚡ {character.Name} kezdeményezése: {initiative.NormalBase} {initiative.Roll.ModifierText} = {initiative.Roll.Total}." +
            (initiative.DisciplineBonus > 0 ? $" [🏃 Portyázó +{initiative.DisciplineBonus}]" : string.Empty) +
            (initiative.FirstStrikeOpeningBonus > 0
                ? $" [⚔ Első csapás: nyitó +{initiative.FirstStrikeOpeningBonus} = {initiative.OpeningTotal}; " +
                  $"rendes +{initiative.FirstStrikeNormalBonus}]"
                : string.Empty),
            BattleLogKind.Information));
        return new CombatantPreparation(runtime, initiative.Roll.Total, initiative.OpeningTotal, entries);
    }

    private CharacterInitiativeRoll RollCharacterInitiative(LiveCharacter character,
        bool includeTacticalDiscipline)
    {
        var weapon = character.OperationalWeapons.FirstOrDefault(item =>
            item is not null && item.WeaponTypeId != DefenseWeaponTypeId);
        var family = WeaponFamilies.ForWeapon(weapon);
        var proficiencyBonus = family switch
        {
            WeaponFamilies.Dagger when character.WeaponProficiencyRankFor(family) is not null => 2,
            WeaponFamilies.Polearm when character.WeaponProficiencyRankFor(family) is not null => 3,
            _ => 0
        };
        var mobility = CharacterMobilityRules.Evaluate(character);
        var hasFirstStrike = character.HasPerk(PerkIds.FighterFirstStrike);
        var firstStrikeNormalBonus = hasFirstStrike ? 2 : 0;
        var firstStrikeOpeningBonus = hasFirstStrike ? 10 : 0;
        var disciplineBonus = includeTacticalDiscipline &&
                              character.HasTacticalDiscipline(TacticalDisciplines.Skirmisher) ? 2 : 0;
        var magicItemBonus = character.GetMagicItemBonus(MagicItemEffect.Initiative);
        var spellBonus = character.SpellEffectValue(ActiveSpellEffectType.InitiativeBonus);
        var normalBase = mobility.InitiativeBase + firstStrikeNormalBonus + proficiencyBonus + disciplineBonus +
                        magicItemBonus + spellBonus - character.StatusInitiativePenalty;
        var roll = RollInitiative(normalBase);
        var openingTotal = roll.Total - firstStrikeNormalBonus + firstStrikeOpeningBonus;
        return new CharacterInitiativeRoll(firstStrikeNormalBonus, firstStrikeOpeningBonus,
            disciplineBonus, normalBase, roll, openingTotal);
    }

    public int RollEnemyInitiative(Enemy enemy)
    {
        ArgumentNullException.ThrowIfNull(enemy);
        return RollInitiative(enemy.EffectiveSpeed + enemy.SpellEffectValue(ActiveSpellEffectType.InitiativeBonus) +
            MonsterAbilityValue(enemy.Definition, MonsterAbilityEffect.InitiativeBonus)).Total;
    }

    public void PrepareEnemyForBattle(Enemy enemy)
    {
        var abilities = enemy.Definition.AbilityIds.Where(_monsterAbilities.ContainsKey)
            .Select(id => _monsterAbilities[id]);
        enemy.PrepareAbilityCharges(abilities);
        enemy.ClearPreparedWeapon();
    }

    public void BeginCharacterTurn(LiveCharacter character)
    {
        ArgumentNullException.ThrowIfNull(character);
        character.AdvanceSpellEffects();
    }

    public string FinishCharacterAction(LiveCharacter character, CharacterBattleChoices runtime)
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

    public BattleLogEntry ResolveCharacterAttack(LiveCharacter attacker,
        CharacterBattleChoices runtime, Enemy defender, bool finishAction = true,
        int damagePercent = 100, int positionalHitBonus = 0, string? positionalAdvantage = null,
        bool tacticalBackstab = false, WeaponDefinition? attackWeapon = null,
        bool allowTriggeredExtraAttacks = true, bool allowAmbush = true,
        int armorPenalty = 0, string damageScaleName = "Söprési mellékcélpont",
        int? attackWeaponSlotIndex = null, int rangedHitModifier = 0)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(defender);
        var resolvedWeapon = attackWeapon ?? attacker.AttackWeapon;
        var target = EnemyDefenseSnapshot.From(defender, armorPenalty,
            MonsterAbilityValue(defender.Definition, MonsterAbilityEffect.ArmorBonus) +
            defender.SpellEffectValue(ActiveSpellEffectType.DefenseBonus));
        var attackOptions = new PlayerAttackOptions(
            PositionalHitBonus: positionalHitBonus,
            PositionalAdvantage: positionalAdvantage,
            TacticalBackstab: tacticalBackstab,
            AttackWeapon: resolvedWeapon,
            AllowAmbush: allowAmbush,
            AttackWeaponSlotIndex: attackWeaponSlotIndex,
            WoundedTarget: target.IsWounded,
            RangedHitModifier: rangedHitModifier);
        var count = allowTriggeredExtraAttacks && attacker.HasPerk(PerkIds.BarbarianBerserkerRage) &&
                    attacker.CurrentVitality * 2 < attacker.MaximumVitality ? 2 : 1;
        var attacks = new List<AttackResult>();
        var critical = false;
        for (var index = 0; index < count && target.CurrentHitPoints > 0; index++)
        {
            if (!RangedWeaponRules.TryConsumeAmmunition(attacker, resolvedWeapon)) break;
            var attack = ResolveCharacterWeaponAttack(attacker, target, runtime.Context, attackOptions);
            if (attack.Hit && damagePercent != 100)
            {
                var scaledDamage = attack.Damage == 0 ? 0 :
                    Math.Max(1, attack.Damage * Math.Clamp(damagePercent, 1, 100) / 100);
                attack = attack with
                {
                    Damage = scaledDamage,
                    Message = $"{attack.Message} {damageScaleName}: ×{damagePercent / 100d:0.##}.",
                    Details = attack.Details is { } detail
                        ? detail with { Damage = scaledDamage,
                            Calculation = detail.Calculation.Append(
                                $"🌀 {damageScaleName}: ×{damagePercent / 100d:0.##}").ToArray() }
                        : null
                };
            }
            critical |= attack.Critical;
            target = target.Apply(attack);
            attacks.Add(attack);
            if (allowTriggeredExtraAttacks && index == 0 && attack.Hit && target.CurrentHitPoints > 0 &&
                attacker.HasPerk(PerkIds.FighterSteelStorm) && _random.NextDouble() < 0.35 &&
                RangedWeaponRules.TryConsumeAmmunition(attacker, resolvedWeapon))
            {
                var extra = ResolveCharacterWeaponAttack(attacker, target, runtime.Context,
                    attackOptions with { WoundedTarget = target.IsWounded });
                critical |= extra.Critical;
                target = target.Apply(extra);
                attacks.Add(extra with { Message = $"Acélvihar: {extra.Message}" });
            }
        }
        if (attacks.Count == 0)
            return new BattleLogEntry($"{attacker.Name} nem tud lőni: elfogyott a lőszere.",
                BattleLogKind.Information);
        defender.SetCurrentHitPoints(target.CurrentHitPoints);
        var statusText = finishAction ? FinishCharacterAction(attacker, runtime) : string.Empty;
            return new BattleLogEntry(
                $"{FormatAttackSummary(attacker.Name, defender.Name, attacks,
                    defender.CurrentHitPoints, defender.MaximumHitPoints)}{statusText}",
            critical ? BattleLogKind.CriticalHit : BattleLogKind.PlayerAttack,
            DescribeAction(attacker.Name, defender.Name, attacks, statusText),
            attacks.SelectMany(attack => attack.DurabilityNotices).ToArray(),
            attacks.Where(attack => attack.ShieldBlock.Attempted)
                .Select(attack => attack.ShieldBlock).ToArray());
    }

    public WeaponDefinition? SelectEnemyAttackWeapon(Enemy attacker)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        if (attacker.PreparedWeaponId is { } preparedId)
        {
            var prepared = attacker.AttackWeapons.FirstOrDefault(weapon =>
                string.Equals(weapon.Id, preparedId, StringComparison.OrdinalIgnoreCase));
            if (prepared is not null) return prepared;
            attacker.ClearPreparedWeapon();
        }
        if (attacker.EquippedWeapon is { } selectedWeapon)
            return attacker.IsWeaponReady(selectedWeapon.Id) ? selectedWeapon : null;
        var ready = attacker.AttackWeapons.Where(weapon => attacker.IsWeaponReady(weapon.Id)).ToArray();
        return ready.Length > 0 ? ready[_random.Next(ready.Length)] : null;
    }

    public WeaponDefinition? SelectEnemyAttackWeapon(Enemy attacker, Func<WeaponDefinition, int> targetCount)
    {
        ArgumentNullException.ThrowIfNull(targetCount);
        if (attacker.PreparedWeaponId is not null) return SelectEnemyAttackWeapon(attacker);
        if (attacker.EquippedWeapon is not null) return SelectEnemyAttackWeapon(attacker);
        var ready = attacker.AttackWeapons.Where(weapon => attacker.IsWeaponReady(weapon.Id)).ToArray();
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
                    $"({enemy.CurrentHitPoints}/{enemy.MaximumHitPoints}).", BattleLogKind.Information));
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

    public BattleLogEntry ResolveEnemyAbility(Enemy attacker, LiveCharacter defender,
        CharacterBattleChoices defenderRuntime, MonsterAbilityDefinition ability,
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
                var armorCondition = defender.InventoryItemCondition(InventorySlotKind.Armor, 0);
                var resistance = defender.OperationalArmor?.Resistances?.Against(type) ?? 0;
                var damage = Math.Max(0, component.Value -
                    EquipmentDurabilityRules.ScaleDefense(resistance, armorCondition));
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

    public BattleLogEntry ResolveEnemyAction(Enemy attacker, LiveCharacter defender,
        CharacterBattleChoices defenderRuntime, WeaponDefinition? attackWeapon = null,
        bool advanceAttackerEffects = true, int alliedGuardDefense = 0)
        => ResolveEnemyActionDetailed(attacker, defender, defenderRuntime, attackWeapon,
            advanceAttackerEffects, alliedGuardDefense).Entry;

    public EnemyAttackResolution ResolveEnemyActionDetailed(Enemy attacker, LiveCharacter defender,
        CharacterBattleChoices defenderRuntime, WeaponDefinition? attackWeapon = null,
        bool advanceAttackerEffects = true, int alliedGuardDefense = 0)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(defender);
        ArgumentNullException.ThrowIfNull(defenderRuntime);
        attackWeapon ??= SelectEnemyAttackWeapon(attacker);
        var attack = ResolveEnemyWeaponAttack(attacker, defender, defenderRuntime.Context,
            new EnemyAttackOptions(AttackWeapon: attackWeapon, AllowWeaponFallback: false,
                AlliedGuardDefense: alliedGuardDefense));
        var vitalityBefore = defender.CurrentVitality;
        var survival = attack.Hit ? ApplyEnemyDamage(defender, attack.Damage, defenderRuntime.Context) : DamageApplicationResult.Empty;
        var entry = new BattleLogEntry(
            $"{FormatAttackSummary(attacker.Name, defender.Name, [attack],
                defender.CurrentVitality, defender.MaximumVitality)} {survival.ShortLog}",
            attack.Critical ? BattleLogKind.CriticalHit : BattleLogKind.EnemyAttack,
            DescribeAction(attacker.Name, defender.Name, [attack], survival.Details),
            attack.DurabilityNotices,
            attack.ShieldBlock.Attempted ? [attack.ShieldBlock] : []);
        return new EnemyAttackResolution(entry, attack.Hit,
            Math.Max(0, vitalityBefore - defender.CurrentVitality));
    }

    public MonsterStrengthContestResult ResolveMonsterStrengthContest(Enemy attacker, LiveCharacter defender,
        CharacterBattleChoices defenderRuntime)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(defender);
        ArgumentNullException.ThrowIfNull(defenderRuntime);
        var strength = attacker.EffectiveStrength;
        var strengthPressure = (strength + 1) / 2;
        var roll = _random.Next(1, 11);
        var resistanceRoll = _random.Next(1, 11);
        var defenderShield = defender.OperationalWeapons.FirstOrDefault(ShieldRules.IsShield);
        var shieldBonus = ShieldRules.StaggerStabilityBonus(defenderShield);
        var defensiveBonus = defenderRuntime.Tactic == BattleTactic.FighterDefensive ? 2 : 0;
        var resistance = resistanceRoll + defender.EffectiveAbilities.Health + shieldBonus + defensiveBonus;
        var total = strengthPressure + roll;
        var margin = total - resistance;
        var outcome = margin >= 5 ? MonsterStrengthContestOutcome.Push :
            margin >= 1 ? MonsterStrengthContestOutcome.Stagger : MonsterStrengthContestOutcome.Resisted;
        return new MonsterStrengthContestResult(strength, strengthPressure, roll, total,
            defender.EffectiveAbilities.Health, resistanceRoll, shieldBonus, defensiveBonus, resistance,
            margin, outcome);
    }

    public ShieldBashContestResult ResolvePlayerShieldBash(LiveCharacter attacker, Enemy defender,
        WeaponDefinition shield)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(defender);
        ArgumentNullException.ThrowIfNull(shield);
        var rank = attacker.WeaponProficiencyRankFor(WeaponFamilies.Shield);
        var shieldPower = ShieldRules.BashPower(shield, rank, attacker.HasPerk(PerkIds.KnightShieldWall));
        var shieldWeightBonus = ShieldRules.StaggerWeightBonus(shield);
        var strength = attacker.EffectiveAbilities.Strength;
        var defenderStability = Math.Max(1, (defender.EffectiveStrength + 1) / 2) +
                                Math.Max(1, defender.Definition.StrengthTier);
        var defenderShieldBonus = ShieldRules.StaggerStabilityBonus(defender.EquippedShield);
        var result = ResolveShieldBash(strength, shieldPower, shieldWeightBonus,
            defenderStability, defenderShieldBonus);
        var shieldSlot = Enumerable.Range(0, 2).FirstOrDefault(index =>
            ReferenceEquals(attacker.GetInventoryItem(InventorySlotKind.Weapon, index), shield), -1);
        if (shieldSlot >= 0)
            attacker.ApplyInventoryItemWear(InventorySlotKind.Weapon, shieldSlot,
                EquipmentWearCause.BeingAttacked, 1);
        return result;
    }

    public ShieldBashContestResult ResolveEnemyShieldBash(Enemy attacker, LiveCharacter defender)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(defender);
        var shield = attacker.EquippedShield ??
                     throw new InvalidOperationException("A pajzslökéshez pajzs szükséges.");
        var strength = attacker.EffectiveStrength;
        var defenderShield = defender.OperationalWeapons.FirstOrDefault(ShieldRules.IsShield);
        var defenderShieldBonus = ShieldRules.StaggerStabilityBonus(defenderShield);
        return ResolveShieldBash(strength, ShieldRules.BashPower(shield),
            ShieldRules.StaggerWeightBonus(shield), defender.EffectiveAbilities.Health, defenderShieldBonus);
    }

    private ShieldBashContestResult ResolveShieldBash(int strength, int shieldPower, int shieldWeightBonus,
        int defenderStability, int defenderShieldBonus)
    {
        var attackerRoll = _random.Next(1, 11);
        var defenderRoll = _random.Next(1, 11);
        var strengthPressure = (Math.Max(1, strength) + 1) / 2;
        var attackTotal = attackerRoll + strengthPressure + Math.Max(0, shieldPower) +
                          Math.Max(0, shieldWeightBonus);
        var defenseTotal = defenderRoll + Math.Max(0, defenderStability) + Math.Max(0, defenderShieldBonus);
        var margin = attackTotal - defenseTotal;
        var outcome = margin >= 5 ? MonsterStrengthContestOutcome.Push :
            margin >= 1 ? MonsterStrengthContestOutcome.Stagger : MonsterStrengthContestOutcome.Resisted;
        var damage = outcome == MonsterStrengthContestOutcome.Resisted
            ? 0
            : Math.Max(1, shieldPower + AbilityDamageBonus(strength) / 2);
        return new ShieldBashContestResult(attackerRoll, strength, strengthPressure, shieldPower,
            shieldWeightBonus, attackTotal, defenderRoll, defenderStability, defenderShieldBonus,
            defenseTotal, margin, outcome, damage);
    }

    public static BattleActionDetails DescribeShieldBash(string attackerName, string defenderName,
        ShieldBashContestResult result, MonsterStrengthContestOutcome actualOutcome, bool pushBlocked = false)
    {
        var outcome = actualOutcome switch
        {
            MonsterStrengthContestOutcome.Push => "LÖKÉS",
            MonsterStrengthContestOutcome.Stagger when pushBlocked => "LÖKÉS BLOKKOLVA → MEGINGÁS",
            MonsterStrengthContestOutcome.Stagger => "MEGINGÁS",
            _ => "ELLENÁLLVA"
        };
        return new BattleActionDetails(Guid.NewGuid(), attackerName, defenderName,
            [$"🛡️ Pajzslökés: {outcome}", $"📏 Különbség: {result.Margin:+#;-#;0}"],
            [
                $"🎲 Támadó: d10 {result.AttackerRoll} + Erőhatás {result.StrengthPressure} + pajzserő {result.ShieldPower} + pajzssúly {result.ShieldWeightBonus} = {result.AttackTotal}",
                $"🛡️ Stabilitás: d10 {result.DefenderRoll} + alap {result.DefenderStability} + pajzs {result.DefenderShieldBonus} = {result.DefenseTotal}",
                $"📐 Eredmény: 1–4 megingás; 5+ lökés; sebzés {result.Damage}"
            ]);
    }

    public static BattleActionDetails DescribeMonsterStrengthContest(string attackerName, string defenderName,
        MonsterStrengthContestResult result, MonsterStrengthContestOutcome actualOutcome, bool pushBlocked = false)
    {
        var defenseModifiers = new List<string>();
        if (result.ShieldBonus > 0) defenseModifiers.Add($"pajzs {result.ShieldBonus:+#;-#;0}");
        if (result.DefensiveBonus > 0)
            defenseModifiers.Add($"védekező állás {result.DefensiveBonus:+#;-#;0}");
        var modifiers = defenseModifiers.Count == 0 ? string.Empty : $" + {string.Join(" + ", defenseModifiers)}";
        var outcome = actualOutcome switch
        {
            MonsterStrengthContestOutcome.Push => "LÖKÉS",
            MonsterStrengthContestOutcome.Stagger when pushBlocked => "LÖKÉS BLOKKOLVA → SÚLYOS MEGINGÁS",
            MonsterStrengthContestOutcome.Stagger => "MEGINGÁS",
            _ => "ELLENÁLLVA"
        };
        return new BattleActionDetails(Guid.NewGuid(), attackerName, defenderName,
            [$"💪 Erőpróba: {outcome}", $"📏 Különbség: {result.Margin:+#;-#;0}"],
            [
                $"🎲 Támadó: d10 {result.Roll} + Erőhatás {result.StrengthPressure} = {result.Total} (Erő {result.Strength})",
                $"🛡️ Ellenállás: d10 {result.ResistanceRoll} + Egészség {result.Health}{modifiers} = {result.Resistance}",
                "📐 Eredmény: 1–4 megingás; 5+ lökés"
            ]);
    }

    public static string? MonsterStrengthCombatLogMessage(string defenderName,
        MonsterStrengthContestOutcome actualOutcome, bool pushedFormation = false, bool pushBlocked = false) =>
        actualOutcome switch
        {
            MonsterStrengthContestOutcome.Push when pushedFormation =>
                "💥 A csapás egy mezővel hátratolja az egész alakzatot.",
            MonsterStrengthContestOutcome.Push => $"💥 {defenderName} egy mezővel hátralökődik.",
            MonsterStrengthContestOutcome.Stagger when pushBlocked =>
                $"💫 Nincs hely a hátralökéshez, ezért {defenderName} súlyosan meginog.",
            MonsterStrengthContestOutcome.Stagger =>
                $"💫 {defenderName} meginog.",
            _ => null
        };

    public BattleLogEntry ResolveEnemyAttackOnRetreatingCharacter(Enemy attacker, LiveCharacter defender,
        CharacterBattleChoices defenderRuntime)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(defender);
        ArgumentNullException.ThrowIfNull(defenderRuntime);
        var weapons = attacker.AttackWeapons.Where(weapon =>
            weapon.MaximumTargets == 1 && attacker.IsWeaponReady(weapon.Id)).ToArray();
        var opportunityWeapon = attacker.EquippedWeapon is { MaximumTargets: 1 } selected &&
                                attacker.IsWeaponReady(selected.Id)
            ? selected
            : weapons.Length > 0 ? weapons[_random.Next(weapons.Length)] : null;
        var attack = ResolveEnemyWeaponAttack(attacker, defender, defenderRuntime.Context,
            new EnemyAttackOptions(AttackWeapon: opportunityWeapon, AllowWeaponFallback: false));
        var survival = attack.Hit ? ApplyEnemyDamage(defender, attack.Damage, defenderRuntime.Context) : DamageApplicationResult.Empty;
        return new BattleLogEntry(
            $"↪️ {FormatAttackSummary(attacker.Name, defender.Name, [attack],
                defender.CurrentVitality, defender.MaximumVitality)} {survival.ShortLog}",
            attack.Critical ? BattleLogKind.CriticalHit : BattleLogKind.EnemyAttack,
            DescribeAction(attacker.Name, defender.Name, [attack], survival.Details),
            attack.DurabilityNotices);
    }

    public void SetKnightProtection(CharacterBattleChoices runtime, LiveCharacter knight)
    {
        runtime.Context.KnightProtector = knight;
        runtime.Context.KnightProtectionAvailable = true;
    }

    public static string PadRightDisplay(string text, int width)
    {
        var padding = width - BattleCommandPanel.DisplayWidth(text);

        return padding > 0
            ? text + new string(' ', padding)
            : text;
    }

    private static string FormatAttackSummary(
     string attackerName,
     string defenderName,
     IReadOnlyList<AttackResult> attacks,
     int currentHitPoints,
     int maximumHitPoints)
    {
        const int AttackerWidth = BattleLogFormatter.ActorColumnWidth;
        const int DefenderWidth = 23;
        const int OutcomeWidth = 16;
        const int DamageWidth = 14;

        var successful = attacks.Where(attack => attack.Hit).ToArray();
        var critical = attacks.Any(attack => attack.Critical);

        var outcome = successful.Length == 0
            ? "💨 MELLÉ"
            : critical
                ? "💥 KRITIKUS!"
                : "🎯 TALÁLAT";

        var damage = successful.Length > 0
            ? $"💥 {successful.Sum(attack => attack.Damage)}"
            : string.Empty;

        var hitPoints = successful.Length > 0
            ? $"{defenderName} ❤️ {currentHitPoints}/{maximumHitPoints}"
            : string.Empty;

        var summary =
            $"{PadRightDisplay(attackerName, AttackerWidth)} → " +
            $"{PadRightDisplay(defenderName, DefenderWidth)} " +
            $"{PadRightDisplay(outcome, OutcomeWidth)} " +
            $"{PadRightDisplay(damage, DamageWidth)}" +
            hitPoints;

        if (attacks.Any(attack => attack.ShieldBlock.IsCriticalBlock))
            summary += $". 🛡️ {defenderName} PAJZSBLOKK";

        return summary;
    }

    private static BattleActionDetails DescribeAction(string actor, string target,
        IReadOnlyList<AttackResult> attacks, string effects)
    {
        var calculations = new List<string>();
        foreach (var attack in attacks)
        {
            calculations.AddRange(attack.Details?.Calculation ?? []);
        }
        if (!string.IsNullOrWhiteSpace(effects)) calculations.Add(effects);
        return new(Guid.NewGuid(), actor, target, [], calculations);
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

    private AttackResult ResolveCharacterWeaponAttack(LiveCharacter player, EnemyDefenseSnapshot defender,
        BattleRuntimeContext context, PlayerAttackOptions options)
    {
        var defenderSpeed = defender.EffectiveSpeed;
        var woundedTarget = options.WoundedTarget;
        var positionalHitBonus = options.PositionalHitBonus;
        var positionalAdvantage = options.PositionalAdvantage;
        var tacticalBackstab = options.TacticalBackstab;
        var attackWeapon = options.AttackWeapon;
        var allowAmbush = options.AllowAmbush;
        var attackWeaponSlotIndex = options.AttackWeaponSlotIndex;
        var rangedHitModifier = options.RangedHitModifier;
        // ============================================================
        // RÉSZLETES HARCI INFORMÁCIÓK GYŰJTŐI
        // ============================================================

        // Ez kerül mindig legelőre. Találat esetén pontosan 6 sor.
        var calculationSummary = new List<string>();

        // Az összefoglaló után következő részletes blokkok.
        var hitCalculations = new List<string>();
        var criticalCalculations = new List<string>();
        var defenseCalculations = new List<string>();
        var damageCalculations = new List<string>();
        var otherCalculations = new List<string>();

        // Modifier metódus
        void Modifier(List<string> target, string name, int value)
        {
            if (value != 0)
                target.Add($"{name}: {value:+#;-#;0}");
        }

        List<string> BuildCalculation()
        {
            var result = new List<string>();

            result.AddRange(calculationSummary);

            void AddSection(List<string> items)
            {
                if (items.Count == 0)
                    return;

                result.AddRange(items);
            }

            AddSection(hitCalculations);
            AddSection(criticalCalculations);
            AddSection(defenseCalculations);
            AddSection(damageCalculations);
            AddSection(otherCalculations);

            return result;
        }
        // ============================================================


        // ============================================================
        // ADATGYŰJTÉS
        // ============================================================

        player.BreakSanctuary();
        var forcedHit = context.ShadowStepReady;
        context.ShadowStepReady = false;
        var weapon = attackWeapon ?? player.AttackWeapon;
        var weaponSlot = ResolveWeaponSlot(player, weapon, attackWeaponSlotIndex);
        if (weaponSlot >= 0 && !player.IsInventoryItemOperational(InventorySlotKind.Weapon, weaponSlot))
        {
            weapon = null;
            weaponSlot = -1;
        }
        var weaponCondition = weaponSlot >= 0
            ? player.InventoryItemCondition(InventorySlotKind.Weapon, weaponSlot)
            : EquipmentCondition.NotApplicable;
        var durabilityHitPenalty = EquipmentDurabilityRules.WeaponHitPenalty(weaponCondition);
        var durabilityDamagePenalty = EquipmentDurabilityRules.WeaponDamagePenalty(weaponCondition);
        var blessedWeaponBonus = player.HasPerk(PerkIds.PriestBlessedWeapon) && defender.IsUndead ? 2 : 0;
        var invisibilityBonus = player.SpellEffectValue(ActiveSpellEffectType.Invisibility);
        var strengthHitBonus = StrengthHitBonus(player);
        var classHitBonus = ClassHitBonus(player);
        var weaponFamily = WeaponFamilies.ForWeapon(weapon);
        var weaponRank = player.WeaponProficiencyRankFor(weaponFamily);
        var rangedFamilyHitBonus = weaponFamily is WeaponFamilies.Bow or WeaponFamilies.Crossbow &&
                                   weaponRank is not null ? 1 : 0;
        var oathbladeBonus = UsesRodericOathblade(player, weapon) ? 1 : 0;
        var retaliation = context.KnightRetaliationReady;
        context.KnightRetaliationReady = false;
        var finisherBonus = woundedTarget && player.HasTacticalDiscipline(TacticalDisciplines.Finisher) ? 2 : 0;
        var hitBonus = CharacterHitBonus(player, context.Tactic, weapon is not null, invisibilityBonus,
            strengthHitBonus, blessedWeaponBonus) + (weapon?.MagicPower ?? 0) + (retaliation ? 2 : 0) +
                       (weaponFamily == WeaponFamilies.Sword && weaponRank is not null ? 1 : 0) + finisherBonus +
                       rangedFamilyHitBonus + Math.Max(0, positionalHitBonus) + rangedHitModifier;
        hitBonus += oathbladeBonus - durabilityHitPenalty;
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
        var positionalHitText = positionalHitBonus > 0
            ? $" [{positionalAdvantage ?? "Pozíció"} +{positionalHitBonus} találat]"
            : string.Empty;
        var rangedHitText = rangedHitModifier == 0
            ? string.Empty
            : $" [Közeli lövés {rangedHitModifier:+#;-#;0} találat]";
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
        // ============================================================


        // ============================================================
        // TALÁLATI FÁZIS INFORMÁCIÓI
        // ============================================================

        var hitTarget = 11 + defenderSpeed;
        var totalHitModifier = hitBonus - player.StatusHitPenalty;
        var totalHitRoll =
            hit.NaturalRoll +
            player.EffectiveAbilities.Dexterity +
            totalHitModifier;

        hitCalculations.Add(
            $"🎯 d20={hit.NaturalRoll}; ügyesség {player.EffectiveAbilities.Dexterity}");

        hitCalculations.Add(
            $"🎯 Cél: 11 + gyorsaság {defenderSpeed} = {hitTarget}");

        hitCalculations.Add(
            $"🎯 Összes módosító: {totalHitModifier:+#;-#;0}");

        Modifier(hitCalculations, "🎯 Erő", strengthHitBonus);
        Modifier(hitCalculations, "🎯 Osztályjártasság", classHitBonus);

        Modifier(
            hitCalculations,
            "🎯 Fegyvermester",
            weapon is not null && player.HasPerk(PerkIds.FighterWeaponMaster) ? 2 : 0);

        Modifier(
            hitCalculations,
            "🎯 Varázstárgy",
            player.GetMagicItemBonus(MagicItemEffect.Hit));

        Modifier(
            hitCalculations,
            "🎯 Varázshatás",
            player.SpellEffectValue(ActiveSpellEffectType.HitBonus));

        Modifier(hitCalculations, "🎯 Láthatatlanság", invisibilityBonus);
        Modifier(hitCalculations, "🎯 Áldott fegyver", blessedWeaponBonus);
        Modifier(hitCalculations, "🎯 Mágikus fegyver", weapon?.MagicPower ?? 0);

        Modifier(
            hitCalculations,
            "🎯 Kardjártasság",
            weaponFamily == WeaponFamilies.Sword && weaponRank is not null ? 1 : 0);
        Modifier(hitCalculations, "🎯 Távolsági fegyverjártasság", rangedFamilyHitBonus);
        Modifier(hitCalculations, "🎯 Közeli lövés", rangedHitModifier);

        Modifier(hitCalculations, "🎯 Esküpenge", oathbladeBonus);
        Modifier(hitCalculations, "🎯 Megtorlás", retaliation ? 2 : 0);
        Modifier(hitCalculations, "🎯 Kivégző", finisherBonus);

        Modifier(
            hitCalculations,
            string.IsNullOrWhiteSpace(positionalAdvantage)
                ? "🎯 Pozíció"
                : $"🎯 {positionalAdvantage}",
            Math.Max(0, positionalHitBonus));

        Modifier(
            hitCalculations,
            "🎯 Taktika",
            context.Tactic is BattleTactic.FighterPrecise or BattleTactic.ThiefObserve
                ? 2
                : context.Tactic == BattleTactic.FighterPowerful
                    ? -1
                    : 0);

        Modifier(
            hitCalculations,
            player.HasStatus(CharacterStatusIds.Thirsty)
                ? "💧 Szomjúság: találat"
                : "🎯 Állapotbüntetés",
            -player.StatusHitPenalty);

        Modifier(
            hitCalculations,
            "☠ Bizonytalan kéz",
            -player.GetActiveCurseValue(ItemCurseEffect.HitPenalty));

        Modifier(
            hitCalculations,
            "🛠️ Sérült fegyver: találat",
            -durabilityHitPenalty);        // Natural critical range is not sufficient: the attack must also hit.
        // ============================================================


        // ============================================================
        // KRITIKUS TALÁLAT INFORMÁCIÓI
        // ============================================================

        Modifier(
            criticalCalculations,
            "🎲 Mágikus fegyver (%)",
            weapon?.MagicPower >= 3
                ? 10
                : weapon?.MagicPower == 2
                    ? 5
                    : 0);

        Modifier(
            criticalCalculations,
            "🎲 Halálos pontosság (%)",
            player.HasPerk(PerkIds.ThiefDeadlyAccuracy) ? 10 : 0);

        Modifier(
            criticalCalculations,
            "🎲 Tőrmester (%)",
            weaponFamily == WeaponFamilies.Dagger &&
            weaponRank == WeaponProficiencyRank.Master
                ? 5
                : 0);

        Modifier(
            criticalCalculations,
            "🎲 Megfigyelés (%)",
            context.Tactic == BattleTactic.ThiefObserve &&
            player.HasClassFeatureUpgrade(ClassFeatureUpgrades.ThiefObserve)
                ? 5
                : 0);

        // A természetes kritikus tartomány önmagában nem elég:
        // a támadásnak el is kell találnia az ellenfelet.
        var criticalChance = Enumerable.Range(1, 20).Count(roll =>
            (forcedHit ||
             roll != 1 &&
             (roll == 20 ||
              roll +
              player.EffectiveAbilities.Dexterity +
              hitBonus -
              player.StatusHitPenalty >= 11 + defenderSpeed)) &&
            roll >= criticalNaturalRollMinimum) * 5d;

        criticalCalculations.Add(
            $"🎲 Kritikus alap 5%; bónusz +{criticalChanceBonusPercent}%");

        criticalCalculations.Add(
            $"🎲 Kritikus küszöb: {criticalNaturalRollMinimum}–20");

        criticalCalculations.Add(
            $"🎲 Tényleges kritikus esély: {criticalChance:0.##}%");
        // ============================================================
        

        // Detailed metódus
        AttackResult Detailed(AttackResult result) => result with
        {
            Details = new AttackDetails(
                hit.Description,
                result.Damage,
                criticalChance,
                result.Critical ? criticalMultiplier : 1,
                BuildCalculation().ToArray())
        };


        calculationSummary.Add(
            $"🎯 {totalHitRoll} vs {hitTarget} " +
            $"({totalHitModifier:+#;-#;0} módosító)");
        // ============================================================

        // ========================================================
        // SIKERTELEN TÁMADÁS
        // ========================================================
        if (!hit.Hit)
        {
            context.ConsecutivePlayerHits = 0;
            if (!hit.Hit)
            {
                calculationSummary.Add(
                    $"🎲 Kritikus: {criticalChance:0.##}% → nem");

                context.ConsecutivePlayerHits = 0;

                return Detailed(
                    AttackResult.Miss(
                        $"találat: {hit.Description}" +
                        $"{thirstHitText}" +
                        $"{magicWeaponHitText}" +
                        $"{magicWeaponCriticalText} → 💨." +
                        $"{strengthHitText}" +
                        $"{classHitText}" +
                        $"{positionalHitText}{rangedHitText}"));
            }
            return Detailed(AttackResult.Miss($"találat: {hit.Description}{thirstHitText}{magicWeaponHitText}{magicWeaponCriticalText} → 💨.{strengthHitText}{classHitText}{positionalHitText}{rangedHitText}"));
        }

        // ============================================================
        // SEBZÉSI FÁZIS – ALAPSEBZÉS ÉS BÓNUSZOK
        // ============================================================

        var baseDamage = weapon?.Damage is { } range ? Roll(range) : Roll(new ValueRange(1, 2));
        var usesDexterity = weapon is not null && string.Equals(weapon.WeaponTypeId, DexterityWeaponTypeId, StringComparison.OrdinalIgnoreCase);
        var ability = usesDexterity ? player.EffectiveAbilities.Dexterity : player.EffectiveAbilities.Strength;
        var abilityBonus = AbilityDamageBonus(ability);
        var randomBonus = Roll(new ValueRange(0, 2));
        var perkBonus = player.GetMagicItemBonus(MagicItemEffect.Damage) + blessedWeaponBonus +
                        player.SpellEffectValue(ActiveSpellEffectType.DamageBonus);
        var notes = new List<string>();
        Modifier(damageCalculations, "💥 Varázstárgy", player.GetMagicItemBonus(MagicItemEffect.Damage));
        Modifier(damageCalculations, "💥 Varázshatás", player.SpellEffectValue(ActiveSpellEffectType.DamageBonus));
        if (oathbladeBonus > 0)
        {
            perkBonus += 2;
            notes.Add("ℹ️⚔ Esküpenge +1 találat és +2 sebzés");
        }
        if (weaponFamily == WeaponFamilies.Sword && weaponRank is not null)
            notes.Add("ℹ️⚔ Kardjártasság +1 találat");
        if (weaponFamily == WeaponFamilies.Dagger && weaponRank is not null)
        { perkBonus += 1; notes.Add("ℹ️🗡️ Tőrjártasság +1 sebzés"); }
        if (weaponFamily == WeaponFamilies.Axe && weaponRank is not null)
        { perkBonus += 2; notes.Add("ℹ️🪓 Bárdjártasság +2 sebzés"); }
        if (string.Equals(weapon?.Id, DualWieldingRules.ElvenDaggerId, StringComparison.OrdinalIgnoreCase) &&
            DualWieldingRules.HasPairedElvenDaggers(player))
        {
            perkBonus++;
            notes.Add("ℹ️🧝 Páros elf tőr +1 sebzés");
            Modifier(damageCalculations, "💥 Páros elf tőr", 1);
        }
        if (blessedWeaponBonus > 0) notes.Add("ℹ️ Áldott fegyver +2");
        if (player.HasPerk(PerkIds.BarbarianBloodlust) && player.CurrentVitality * 2 < player.MaximumVitality) { perkBonus += 3; notes.Add("ℹ️ Vérszomj +3"); }
        if (player.HasPerk(PerkIds.BarbarianPrimalStrength)) { perkBonus += 5; notes.Add("ℹ️ Őserő +5"); }
        if (player.HasPerk(PerkIds.BarbarianRage))
        {
            perkBonus += context.ConsecutivePlayerHits;
            if (context.ConsecutivePlayerHits > 0) notes.Add($"ℹ️ Őrjöngés +{context.ConsecutivePlayerHits}");
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
            notes.Add($"ℹ️🔥 Düh +{rageBonus}");
        }
        if (retaliation) { perkBonus += 4; notes.Add("ℹ️⚔️ Megtorlás: +2 találat, +4 sebzés"); }

        // ============================================================
        // VÉDELMI FÁZIS – SEBZÉSTÍPUS, PÁNCÉL ÉS PAJZS
        // ============================================================

        var damageType = weapon?.DamageType ?? DamageType.Bludgeoning;
        var typeDefense = defender.Resistances?.Against(damageType) ?? 0;
        defenseCalculations.Add($"🛡️ Sebzéstípus: {damageType.Name()}");
        defenseCalculations.Add($"🛡️ Típusvédelem {typeDefense:+#;-#;0}");
        var armorRoll = Roll(defender.Armor);
        defenseCalculations.Add($"🛡️ Páncél {defender.Armor}: {armorRoll}");
        var armor = Math.Max(0, armorRoll + defender.ArmorAbilityBonus + typeDefense);
        var powerfulMastery = context.Tactic == BattleTactic.FighterPowerful &&
                              player.HasClassFeatureUpgrade(ClassFeatureUpgrades.FighterPowerful);
        var tacticArmorPenetration = powerfulMastery ? 75 :
            context.Tactic == BattleTactic.FighterPowerful ? 50 : 0;
        var armorPenetration = Math.Max(weapon?.EffectiveArmorPenetrationPercent ?? 0, tacticArmorPenetration);
        var armorPiercing = armorPenetration > 0;
        var armorAfterPiercing = (armor * (100 - armorPenetration) + 99) / 100;
        var bluntArmorIgnored = weaponFamily == WeaponFamilies.Blunt ? weaponRank switch
        {
            WeaponProficiencyRank.Master => 4,
            WeaponProficiencyRank.Trained => 2,
            _ => 0
        } : 0;
        var effectiveArmor = Math.Max(0, armorAfterPiercing - bluntArmorIgnored);
        var damageMultiplierPercent = 100;
        if (allowAmbush && (context.AmbushAvailable || tacticalBackstab))
        {
            damageMultiplierPercent = player.HasClassFeatureUpgrade(ClassFeatureUpgrades.ThiefAmbush) ? 250 : 200;
            context.AmbushAvailable = false;
            notes.Add($"{(tacticalBackstab ? "ℹ️ Hátbatámadás: " : string.Empty)}Orvtámadás ×{damageMultiplierPercent / 100d:0.##}");
        }
        if (criticalMultiplier > 1)
            notes.Add(criticalMultiplier == 3
                ? weaponFamily == WeaponFamilies.Axe && weaponRank == WeaponProficiencyRank.Master && hit.NaturalRoll == 20 &&
                  !player.HasPerk(PerkIds.ThiefDeadlyAccuracy)
                    ? "ℹ️\U0001fa93 Bárdmester kritikus sebzés ×3"
                    : "ℹ️ Halálos pontosság kritikus sebzés ×3"
                : "ℹ️ Kritikus sebzés ×2");
        if (magicWeaponCriticalText.Length > 0)
            notes.Add($"ℹ️ {magicWeaponCriticalText.Trim()}");
        damageMultiplierPercent *= criticalMultiplier;
        if (weaponFamily == WeaponFamilies.Polearm && weaponRank == WeaponProficiencyRank.Master &&
            context.PolearmMasterOpeningAvailable)
        {
            damageMultiplierPercent = damageMultiplierPercent * 150 / 100;
            context.PolearmMasterOpeningAvailable = false;
            notes.Add("ℹ️🔱 Szálfegyver-mester: első találat ×1,5");
        }
        var rawDamage = baseDamage + abilityBonus + randomBonus + perkBonus;
        var shieldRoll = defender.Shield?.Shield.Damage is { } shieldDefense ? Roll(shieldDefense) : 0;

        var damage = ApplyDefense((rawDamage * damageMultiplierPercent + 99) / 100, effectiveArmor + shieldRoll);
        if (damageType.IsPhysical() && defender.PhysicalReduction > 0)
            damage = Math.Max(1, damage * (100 - Math.Clamp(defender.PhysicalReduction, 0, 100)) / 100);

        var statusDamagePenalty = player.StatusPhysicalDamagePenalty;
        Modifier(damageCalculations, player.HasStatus(CharacterStatusIds.Hungry) ? "🍖 Éhség: fizikai sebzés" : "💥 Állapotbüntetés",
            -statusDamagePenalty);
        damage = Math.Max(1, damage - statusDamagePenalty);
        if (statusDamagePenalty > 0)
            notes.Add(player.HasStatus(CharacterStatusIds.Hungry)
                ? $"ℹ️🍖 éhség -{statusDamagePenalty} fizikai sebzés"
                : $"ℹ️💥 állapot -{statusDamagePenalty} fizikai sebzés");
        switch (context.Tactic)
        {
            case BattleTactic.FighterPrecise:
                var precisePercent = player.HasClassFeatureUpgrade(ClassFeatureUpgrades.FighterPrecise) ? 85 : 75;
                damage = Math.Max(1, damage * precisePercent / 100);
                notes.Add($"ℹ️ Pontos: +2 találat, ×0,{precisePercent} sebzés");
                break;
            case BattleTactic.FighterPowerful:
                damage = Math.Max(1, (damage * 125 + 99) / 100);
                notes.Add($"ℹ️💥 Erőteljes: -1 találat, ×1,25 sebzés, {(powerfulMastery ? 75 : 50)}% páncéltörés");
                break;
            case BattleTactic.FighterDefensive:
                damage = Math.Max(1, damage * 75 / 100);
                notes.Add($"ℹ️ Védekező: ×0,75 sebzés, +{(player.HasClassFeatureUpgrade(ClassFeatureUpgrades.FighterDefensive) ? 4 : 3)} védelem");
                break;
        }
        if (durabilityDamagePenalty > 0)
        {
            damage = Math.Max(1, damage - durabilityDamagePenalty);
            notes.Add($"ℹ️🛠️ sérült fegyver -{durabilityDamagePenalty} sebzés");
            Modifier(damageCalculations, "🛠️ Sérült fegyver: sebzés", -durabilityDamagePenalty);
        }
        var shieldBlock = ResolveCriticalBlock(defender.Shield, damageType);
        if (shieldBlock.IsCriticalBlock)
        {
            damage = 0;
            notes.Add($"🛡️ KRITIKUS BLOKK ({shieldBlock.Roll})");
            defenseCalculations.Add($"🛡️ Kritikus blokk: d20={shieldBlock.Roll}, érték {shieldBlock.BlockRating} → teljes fizikai sebzés kivédve");
        }
        else if (shieldBlock.Attempted)
            defenseCalculations.Add($"🛡️ Kritikus blokk: d20={shieldBlock.Roll}, érték {shieldBlock.BlockRating} → nem");
        if (player.HasPerk(PerkIds.ThiefPoisoner))
        {
            var poison = Roll(new ValueRange(1, 6));
            damage += poison;
            notes.Add($"ℹ️☠️ Méreg +{poison}");
        }
        if (context.Tactic == BattleTactic.ThiefPoison)
        {
            var poison = Roll(player.HasClassFeatureUpgrade(ClassFeatureUpgrades.ThiefPoison)
                ? new ValueRange(2, 6) : new ValueRange(1, 4));
            damage += poison;
            notes.Add($"ℹ️☠️ Mérgezett penge +{poison}");
        }
        if (context.BarbarianRageActionsRemaining > 0 &&
            player.HasClassFeatureUpgrade(ClassFeatureUpgrades.BarbarianBloodRage))
        {
            var before = player.CurrentVitality;
            player.RestoreVitality(Roll(new ValueRange(1, 3)));
            var restored = player.CurrentVitality - before;
            if (restored > 0) notes.Add($"ℹ️❤️‍🔥 Vérdüh +{restored} HP");
        }
        context.ConsecutivePlayerHits++;
        var noteText = notes.Count == 0 ? string.Empty : $" [{string.Join(", ", notes)}]";
        var perkBonusText = perkBonus == 0 ? string.Empty : $" + bónusz {perkBonus}";
        var armorText = armorPiercing
            ? $"páncél {armor} → {armorAfterPiercing} ({armorPenetration}% páncéltörés)"
            : $"páncél {armor}";
        if (bluntArmorIgnored > 0)
            armorText += $" → {effectiveArmor} (🔨 jártasság -{bluntArmorIgnored})";

        defenseCalculations.Add(
            $"🛡️ {armorText}; effektív {effectiveArmor}");

        if (defender.Shield is not null)
        {
            defenseCalculations.Add(
                $"🛡️ {defender.Shield.Shield.Name}: dobás {shieldRoll}");
        }

        var damageText = damage > 0 ? $"💥 {damage}" : "0";


        // ============================================================
        // SEBZÉSI FÁZIS – VÉGSŐ SZÁMÍTÁS INFORMÁCIÓI
        // ============================================================

        var damageAbilityName =
            usesDexterity ? "Ügyesség" : "Erő";

        damageCalculations.Add(
            $"💥 Fegyver alapsebzése: " +
            $"{weapon?.Name ?? "Puszta kéz"} → {baseDamage}");

        damageCalculations.Add(
            $"💥 {damageAbilityName}bónusz: " +
            $"{damageAbilityName} {ability} → +{abilityBonus}");

        damageCalculations.Add(
            $"🎲 Véletlen sebzésbónusz (0–2): +{randomBonus}");

        damageCalculations.Add(
            $"💥 Nyers sebzés: fegyver {baseDamage} + " +
            $"{damageAbilityName.ToLowerInvariant()} {abilityBonus} + " +
            $"véletlen {randomBonus} + egyéb {perkBonus} = {rawDamage}");

        var multipliedDamage =
            rawDamage * damageMultiplierPercent / 100d;

        var roundedMultipliedDamage =
            (rawDamage * damageMultiplierPercent + 99) / 100;

        damageCalculations.Add(
            damageMultiplierPercent == 100
                ? $"💥 Sebzésszorzó: ×1 → {rawDamage}"
                : multipliedDamage == roundedMultipliedDamage
                    ? $"💥 Sebzésszorzó: " +
                      $"×{damageMultiplierPercent / 100d:0.##} → " +
                      $"{roundedMultipliedDamage}"
                    : $"💥 Sebzésszorzó: " +
                      $"×{damageMultiplierPercent / 100d:0.##}; " +
                      $"{multipliedDamage:0.##} → " +
                      $"{roundedMultipliedDamage} " +
                      $"(felfelé kerekítve)");

        damageCalculations.Add("💥 Páncél és éhség után min. 1");
        damageCalculations.Add("💥 Majd taktika és méreg");
        // ============================================================

        otherCalculations.AddRange(notes);

        // we consider the enemy which does e.g. chaos damage has a chaos aura as well which can harm player weapons
        var wearCause = defender.Weapon?.DamageType switch
        {
            DamageType.Acid => EquipmentWearCause.Acid,
            DamageType.Chaos => EquipmentWearCause.Chaos,
            _ => EquipmentWearCause.Attack
        };
        var weaponWear = ApplyWeaponWear(player, weapon, weaponSlot, criticalMultiplier > 1, wearCause);
        if (weaponWear.Changed)
            otherCalculations.Add(DurabilityCalculation("ℹ️🛠️ Fegyverkopás", weapon!.Name, weaponWear));

        // ============================================================
        // ÖSSZEFOGLALÓ – 3–6. SOR
        // ============================================================

        var shieldSummary =
            defender.Shield is not null
                ? $"; {defender.Shield.Shield.Name} -{shieldRoll}"
                : string.Empty;

        calculationSummary.Add(
            $"🛡️ Páncél -{effectiveArmor}{shieldSummary}");

        var weaponName = weapon?.Name ?? "puszta kéz";

        var weaponDamageRange =
            weapon?.Damage?.ToString() ?? "1-2";

        var directDamageBonus =
            abilityBonus +
            randomBonus +
            perkBonus;

        var weaponIcon = weapon?.GetIcon() ?? "👊";

        calculationSummary.Add(
            $"💥 {weaponIcon}{weaponName} [{weaponDamageRange}] → " +
            $"{baseDamage} {directDamageBonus:+#;-#;0}");

        calculationSummary.Add(
            $"🎲 Kritikus: {criticalChance:0.##}% → " +
            $"{(criticalMultiplier > 1 ? "IGEN" : "nem")}");

        calculationSummary.Add(
            $"💥 {damage} ({damageType.Name()})");

        return Detailed(AttackResult.HitFor(damage,
            $"találat: {hit.Description}{thirstHitText} → 🎯;{strengthHitText}{classHitText}{positionalHitText}{rangedHitText} sebzés: (alap {baseDamage} + képesség {abilityBonus} + dobás {randomBonus}{perkBonusText}) ×{damageMultiplierPercent / 100d:0.##} - {armorText} = {damageText}.{noteText}",
            criticalMultiplier > 1,
            DurabilityNotice(player.Name, weapon?.Name, "fegyvere", weaponWear, defensive: false),
            shieldBlock));
    }

    public int EstimateCharacterHitChance(LiveCharacter character, Enemy enemy, BattleTactic tactic)
    {
        var defender = enemy.Definition;
        var weapon = character.AttackWeapon;
        var weaponSlot = ResolveWeaponSlot(character, weapon, null);
        var durabilityHitPenalty = weaponSlot < 0 ? 0 : EquipmentDurabilityRules.WeaponHitPenalty(
            character.InventoryItemCondition(InventorySlotKind.Weapon, weaponSlot));
        var weaponEquipped = weapon is not null;
        var blessedWeaponBonus = character.HasPerk(PerkIds.PriestBlessedWeapon) &&
                                 defender.HasTrait(EnemyTraits.Undead) ? 2 : 0;
        var bonus = CharacterHitBonus(character, tactic, weaponEquipped,
            character.SpellEffectValue(ActiveSpellEffectType.Invisibility), StrengthHitBonus(character), blessedWeaponBonus) -
                    character.StatusHitPenalty +
                    (weapon?.MagicPower ?? 0) +
                    (WeaponFamilies.ForWeapon(weapon) == WeaponFamilies.Sword &&
                     character.WeaponProficiencyRankFor(WeaponFamilies.Sword) is not null ? 1 : 0) +
                    (UsesRodericOathblade(character, weapon) ? 1 : 0) - durabilityHitPenalty +
                    (enemy.CurrentHitPoints * 2 <= Math.Max(1, enemy.MaximumHitPoints) &&
                     character.HasTacticalDiscipline(TacticalDisciplines.Finisher) ? 2 : 0);
        var target = 11 + enemy.EffectiveSpeed;
        var successfulRolls = Enumerable.Range(1, 20).Count(roll =>
            roll != 1 && (roll == 20 || roll + character.EffectiveAbilities.Dexterity + bonus >= target));
        return successfulRolls * 5;
    }

    private int CharacterHitBonus(LiveCharacter character, BattleTactic? tactic,
        bool weaponEquipped, int invisibilityBonus, int strengthHitBonus, int blessedWeaponBonus)
    {
        var tacticHitBonus = tactic switch
        {
            BattleTactic.FighterPrecise => 2,
            BattleTactic.FighterPowerful => -1,
            BattleTactic.ThiefObserve => 2,
            _ => 0
        };
        return (weaponEquipped && character.HasPerk(PerkIds.FighterWeaponMaster) ? 2 : 0) +
               character.GetMagicItemBonus(MagicItemEffect.Hit) + blessedWeaponBonus + invisibilityBonus +
               strengthHitBonus + character.SpellEffectValue(ActiveSpellEffectType.HitBonus) +
               ClassHitBonus(character) + tacticHitBonus - character.GetActiveCurseValue(ItemCurseEffect.HitPenalty);
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

    private AttackResult ResolveEnemyWeaponAttack(Enemy attacker, LiveCharacter defender, BattleRuntimeContext context,
        EnemyAttackOptions options)
    {
        var definition = attacker.Definition;
        var attackerSpeed = attacker.EffectiveSpeed;
        var attackWeapon = options.AttackWeapon;
        var allowWeaponFallback = options.AllowWeaponFallback;
        var alliedGuardDefense = options.AlliedGuardDefense;
        // ============================================================
        // RÉSZLETES HARCI INFORMÁCIÓK GYŰJTŐI
        // ============================================================

        // Ez kerül mindig legelőre. Találat esetén pontosan 6 sor.
        var calculationSummary = new List<string>();

        // Az összefoglaló után következő részletes blokkok.
        var hitCalculations = new List<string>();
        var criticalCalculations = new List<string>();
        var defenseCalculations = new List<string>();
        var damageCalculations = new List<string>();
        var otherCalculations = new List<string>();

        // Modifier metódus
        void Modifier(List<string> target, string name, int value)
        {
            if (value != 0)
                target.Add($"{name}: {value:+#;-#;0}");
        }

        List<string> BuildCalculation()
        {
            var result = new List<string>();

            result.AddRange(calculationSummary);

            void AddSection(List<string> items)
            {
                if (items.Count == 0)
                    return;

                result.AddRange(items);
            }

            AddSection(hitCalculations);
            AddSection(criticalCalculations);
            AddSection(defenseCalculations);
            AddSection(damageCalculations);
            AddSection(otherCalculations);

            return result;
        }
        // ============================================================


        // ============================================================
        // ADATGYŰJTÉS
        // ============================================================

        var criticalChance = 0d;
        var hitDescription = "Automatikus elkerülés";


        // ============================================================
        // Detailed metódus
        // ============================================================
        AttackResult Detailed(AttackResult result, int criticalMultiplier = 1) => result with
        {
            Details = new AttackDetails(
                hitDescription,
                result.Damage,
                criticalChance,
                result.Critical ? criticalMultiplier : 1,
                BuildCalculation().Append(result.Message).ToArray())
        };
        // ============================================================

        if (context.ChallengeAvailable)
        {
            context.ChallengeAvailable = false;
            calculationSummary.Add("🎯 Automatikus elkerülés (Kihívás)");
            return Detailed(AttackResult.Miss("💨 Kihívás: az első támadás automatikusan elhibázza."));
        }

        if (defender.HasPerk(PerkIds.PriestSanctuary) && _random.NextDouble() < 0.20)
        {
            calculationSummary.Add("🎯 Automatikus elkerülés (Szentély)");
            return Detailed(AttackResult.Miss("💨 Szentély: az ellenfél elveszíti a támadását."));
        }

        if (defender.HasSpellEffect(ActiveSpellEffectType.Invisibility))
        {
            calculationSummary.Add("🎯 Automatikus elkerülés (Láthatatlanság)");
            return Detailed(AttackResult.Miss("💨 Láthatatlanság: az ellenfél nem talál célpontot."));
        }

        var spellHitModifier = attacker.SpellEffectValue(ActiveSpellEffectType.HitBonus);
        var hit = HitRoll(attackerSpeed, defender.EffectiveAbilities.Dexterity, spellHitModifier, false);
        hitDescription = hit.Description;

        var hitTarget = 11 + defender.EffectiveAbilities.Dexterity;
        var totalHitModifier = spellHitModifier;
        var totalHitRoll = hit.NaturalRoll + attackerSpeed + totalHitModifier;

        criticalChance = Enumerable.Range(1, 20).Count(roll =>
            roll != 1 &&
            (roll == 20 || roll + attackerSpeed + spellHitModifier >= hitTarget) &&
            roll == 20) * 5d;

        var criticalMultiplier = hit.NaturalRoll == 20 ? 2 : 1;
        // ============================================================


        // ============================================================
        // TALÁLATI FÁZIS INFORMÁCIÓI
        // ============================================================

        hitCalculations.Add($"🎯 d20={hit.NaturalRoll}; gyorsaság {attackerSpeed}");
        hitCalculations.Add($"🎯 Cél: 11 + ügyesség {defender.EffectiveAbilities.Dexterity} = {hitTarget}");
        hitCalculations.Add($"🎯 Összes módosító: {totalHitModifier:+#;-#;0}");
        Modifier(hitCalculations, "🎯 Varázshatás", spellHitModifier);
        // ============================================================


        // ============================================================
        // KRITIKUS TALÁLAT INFORMÁCIÓI
        // ============================================================

        criticalCalculations.Add("🎲 Kritikus alap 5%; bónusz +0%");
        criticalCalculations.Add("🎲 Kritikus küszöb: 20–20");
        criticalCalculations.Add($"🎲 Tényleges kritikus esély: {criticalChance:0.##}%");
        // ============================================================



        // ============================================================
        // ÖSSZEFOGLALÓ – 1–2. SOR
        // ============================================================

        calculationSummary.Add($"🎯 {totalHitRoll} vs {hitTarget} ({totalHitModifier:+#;-#;0} módosító)");
        // ============================================================


        // ========================================================
        // SIKERTELEN TÁMADÁS
        // ========================================================

        if (!hit.Hit)
        {
            calculationSummary.Add($"🎲 Kritikus: {criticalChance:0.##}% → nem");
            return Detailed(AttackResult.Miss($"találat: {hit.Description} → 💨."));
        }

        if (criticalMultiplier == 1 && defender.HasPerk(PerkIds.ThiefEvasion) && _random.NextDouble() < 0.15)
        {
            context.ShadowStepReady = defender.HasPerk(PerkIds.ThiefShadowStep);
            calculationSummary.Add($"🎲 Kritikus: {criticalChance:0.##}% → nem");
            return Detailed(AttackResult.Miss("💨 Kitérés: a találat elkerülve." +
                                              (context.ShadowStepReady
                                                  ? " Árnyéklépés aktiválva."
                                                  : string.Empty)));
        }


        // ============================================================
        // SEBZÉSI FÁZIS – ALAPSEBZÉS ÉS BÓNUSZOK
        // ============================================================

        var strength = definition.Strength ?? 1;
        var enemyWeapon = attackWeapon ?? (allowWeaponFallback ? SelectEnemyAttackWeapon(attacker) : null);
        var baseDamage = Roll(enemyWeapon?.Damage ?? new ValueRange(1, 2));
        var strengthBonus = AbilityDamageBonus(strength);
        var damageType = enemyWeapon?.DamageType ?? DamageType.Bludgeoning;

        damageCalculations.Add($"💥 Fegyver alapsebzése: {enemyWeapon?.Name ?? "Puszta kéz"} → {baseDamage}");
        damageCalculations.Add($"💥 Erőbónusz: Erő {strength} → +{strengthBonus}");

        // ============================================================
        // VÉDELMI FÁZIS – SEBZÉSTÍPUS, PÁNCÉL ÉS PAJZS
        // ============================================================

        var armorCondition = defender.InventoryItemCondition(InventorySlotKind.Armor, 0);
        var armorRoll = RollArmor(defender);
        var typeDefense = EquipmentDurabilityRules.ScaleDefense(
            defender.OperationalArmor?.Resistances?.Against(damageType) ?? 0, armorCondition);
        var armor = Math.Max(0, armorRoll + typeDefense);

        defenseCalculations.Add($"🛡️ Sebzéstípus: {damageType.Name()}");
        defenseCalculations.Add($"🛡️ Típusvédelem: {typeDefense:+#;-#;0}");

        var shieldSlot = Enumerable.Range(0, 2).FirstOrDefault(index =>
            defender.IsInventoryItemOperational(InventorySlotKind.Weapon, index) &&
            defender.GetInventoryItem(InventorySlotKind.Weapon, index) is WeaponDefinition shieldCandidate &&
            shieldCandidate.WeaponTypeId == DefenseWeaponTypeId, -1);
        var shieldWeapon = shieldSlot >= 0
            ? (WeaponDefinition)defender.GetInventoryItem(InventorySlotKind.Weapon, shieldSlot)!
            : null;
        var shieldEquipped = shieldWeapon is not null;
        var shield = shieldWeapon?.Damage is { } shieldRange ? Roll(shieldRange) : 0;
        var shieldRank = defender.WeaponProficiencyRankFor(WeaponFamilies.Shield);
        var shieldCondition = shieldSlot >= 0
            ? defender.InventoryItemCondition(InventorySlotKind.Weapon, shieldSlot)
            : EquipmentCondition.NotApplicable;
        var shieldDefense = shieldWeapon is null ? null : new ShieldDefenseSnapshot(shieldWeapon, shieldRank,
            defender.HasPerk(PerkIds.KnightShieldWall), shieldCondition);
        var staffEquipped = defender.OperationalWeapons.Any(item =>
            WeaponFamilies.ForWeapon(item) == WeaponFamilies.Staff);
        var staffRank = defender.WeaponProficiencyRankFor(WeaponFamilies.Staff);
        var staffDefense = staffEquipped ? staffRank switch
        {
            WeaponProficiencyRank.Master => 2,
            WeaponProficiencyRank.Trained => 1,
            _ => 0
        } : 0;
        if (shieldRank == WeaponProficiencyRank.Master && shieldWeapon?.Damage is { } masterShieldRange)
            shield = Math.Max(shield, Roll(masterShieldRange));
        if (shieldEquipped)
            shield = EquipmentDurabilityRules.ScaleDefense(shield, shieldCondition);

        var evilWard = IsUnholy(definition)
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
                          Math.Max(0, alliedGuardDefense) +
                          (shieldEquipped && shieldRank is not null ? 1 : 0) +
                          staffDefense +
                          (defender.OperationalWeapons.Any(item => WeaponFamilies.ForWeapon(item) == WeaponFamilies.Sword) &&
                           defender.WeaponProficiencyRankFor(WeaponFamilies.Sword) == WeaponProficiencyRank.Master ? 1 : 0) +
                          tacticDefense - rageDefensePenalty;
        var curseDefensePenalty = defender.GetActiveCurseValue(ItemCurseEffect.DefensePenalty);
        perkDefense -= curseDefensePenalty;

        Modifier(defenseCalculations, "🛡️ Vastag bőr", defender.HasPerk(PerkIds.BarbarianThickSkin) ? 1 : 0);
        Modifier(defenseCalculations, "🛡️ Pajzsfal", shieldEquipped && defender.HasPerk(PerkIds.KnightShieldWall) ? 2 : 0);
        Modifier(defenseCalculations, "🛡️ Varázstárgy", defender.GetMagicItemBonus(MagicItemEffect.Defense));
        Modifier(defenseCalculations, "🛡️ Varázshatás", defender.SpellEffectValue(ActiveSpellEffectType.DefenseBonus));
        Modifier(defenseCalculations, "🛡️ Gonosz elleni védelem", evilWardDefense);
        Modifier(defenseCalculations, "🛡️ Társi fedezet", Math.Max(0, alliedGuardDefense));
        Modifier(defenseCalculations, "🛡️ Pajzsjártasság", shieldEquipped && shieldRank is not null ? 1 : 0);
        Modifier(defenseCalculations, "🦯 Botjártasság", staffDefense);
        Modifier(defenseCalculations, "🛡️ Kardmester", defender.OperationalWeapons.Any(item => WeaponFamilies.ForWeapon(item) == WeaponFamilies.Sword) &&
                                                defender.WeaponProficiencyRankFor(WeaponFamilies.Sword) == WeaponProficiencyRank.Master ? 1 : 0);
        Modifier(defenseCalculations, "🛡️ Védekező taktika", tacticDefense);
        Modifier(defenseCalculations, "🛡️ Düh", -rageDefensePenalty);
        Modifier(defenseCalculations, "☠ Repedt oltalom", -curseDefensePenalty);

        if (shieldRank == WeaponProficiencyRank.Master && shieldEquipped)
            defenseCalculations.Add($"🛡️ Pajzsmester: két dobás maximuma = {shield}");
        if (defender.HasPerk(PerkIds.KnightArmorMaster))
            defenseCalculations.Add($"🛡️ Páncélmester: legalább a felfelé kerekített átlag = {armor}");

        Modifier(defenseCalculations, "🛡️ Megtörhetetlen", defender.HasPerk(PerkIds.FighterUnbreakable) ? 2 : 0);
        Modifier(defenseCalculations, "🛡️ Legyőzhetetlen", defender.HasPerk(PerkIds.KnightInvincible) ? 4 : 0);
        var reduction = (defender.HasPerk(PerkIds.FighterUnbreakable) ? 2 : 0) +
                        (defender.HasPerk(PerkIds.KnightInvincible) ? 4 : 0);

        var onHit = ResolveMonsterOnHitAbilities(attacker, defender, enemyWeapon, otherCalculations);
        var monsterBonusRolls = onHit.Damage;
        var matchingBonusDamage = monsterBonusRolls.Where(bonus =>
            bonus.DamageType is null || bonus.DamageType == damageType).Sum(bonus => bonus.Value);

        var rawDamage = strengthBonus + baseDamage + matchingBonusDamage +
                        attacker.SpellEffectValue(ActiveSpellEffectType.DamageBonus);
        var damage = Math.Max(0, ApplyDefense(rawDamage * criticalMultiplier, armor + shield + perkDefense) - reduction);

        var physicalReduction = damageType.IsPhysical()
            ? defender.SpellEffectValue(ActiveSpellEffectType.PhysicalReduction)
            : 0;
        var percentageReduction = Math.Clamp(physicalReduction +
                                             defender.SpellEffectValue(ActiveSpellEffectType.Sanctuary) + evilWard.Sum(effect => effect.Value), 0, 100);
        Modifier(defenseCalculations, "🛡️ Fizikai csökkentés (%)", physicalReduction);
        Modifier(defenseCalculations, "🛡️ Szentély (%)", defender.SpellEffectValue(ActiveSpellEffectType.Sanctuary));
        Modifier(defenseCalculations, "🛡️ Gonosz elleni csökkentés (%)", evilWard.Sum(effect => effect.Value));
        defenseCalculations.Add($"🛡️ Összes sebzéscsökkentés: {percentageReduction}% (0–100%)");
        if (percentageReduction > 0)
            damage = damage * (100 - percentageReduction) / 100;

        var shieldBlock = ResolveCriticalBlock(shieldDefense, damageType);
        if (shieldBlock.IsCriticalBlock)
        {
            damage = 0;
            defenseCalculations.Add($"🛡️ KRITIKUS BLOKK: d20={shieldBlock.Roll}, érték {shieldBlock.BlockRating} → teljes fizikai sebzés kivédve");
            otherCalculations.Add($"🛡️ {defender.Name} teljesen kivédi a fizikai csapást.");
        }
        else if (shieldBlock.Attempted)
            defenseCalculations.Add($"🛡️ Kritikus blokk: d20={shieldBlock.Roll}, érték {shieldBlock.BlockRating} → nem");

        foreach (var group in monsterBonusRolls.Where(bonus => bonus.DamageType is not null && bonus.DamageType != damageType)
                     .GroupBy(bonus => bonus.DamageType!.Value))
        {
            var bonusType = group.Key;
            var bonusRaw = group.Sum(bonus => bonus.Value) * criticalMultiplier;
            var bonusTypeDefense = EquipmentDurabilityRules.ScaleDefense(
                defender.OperationalArmor?.Resistances?.Against(bonusType) ?? 0, armorCondition);
            var bonusArmor = Math.Max(0, armorRoll + bonusTypeDefense);
            var bonusDamage = Math.Max(0, ApplyDefense(bonusRaw, bonusArmor + shield + perkDefense) - reduction);
            var bonusReduction = Math.Clamp((bonusType.IsPhysical() ? physicalReduction : 0) +
                                            defender.SpellEffectValue(ActiveSpellEffectType.Sanctuary) + evilWard.Sum(effect => effect.Value), 0, 100);
            bonusDamage = bonusDamage * (100 - bonusReduction) / 100;
            damage += bonusDamage;
            damageCalculations.Add($"💥 {bonusType.Name()} képességsebzés: {bonusRaw} − védelem {bonusArmor + shield + perkDefense + reduction}, −{bonusReduction}% = {bonusDamage}");
        }

        if (defender.HasPerk(PerkIds.BarbarianPainTolerance) && damage < 3)
            damage = 0;

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

        damageCalculations.Add($"💥 Nyers sebzés: erő {strengthBonus} + fegyver {baseDamage} + azonos típusú szörnybónusz {matchingBonusDamage} = {rawDamage}");
        damageCalculations.Add($"💥 Sebzésszorzó: ×{criticalMultiplier} → {rawDamage * criticalMultiplier}");
        damageCalculations.Add($"💥 Védelem: páncél {armor} + pajzs {shield} + bónusz {perkDefense} + fix {reduction}");
        damageCalculations.Add($"💥 Sebzéscsökkentés: −{percentageReduction}%");
        if (absorbed > 0)
            damageCalculations.Add($"🔷 Mannapajzs: −{absorbed} sebzés / manna");

        var damageText = damage > 0 ? $"💥 {damage}" : "0";
        var durabilityNotices = new List<BattleLogNotice>();
        void ApplyDefensiveWear(InventorySlotKind kind, int slot, string label, string equipmentName,
            IItemDefinition item, int amount)
        {
            var wearCause = enemyWeapon?.DamageType switch
            {
                DamageType.Acid => EquipmentWearCause.Acid,
                DamageType.Chaos => EquipmentWearCause.Chaos,
                _ => EquipmentWearCause.BeingAttacked
            };
            var wear = defender.ApplyInventoryItemWear(kind, slot, wearCause, amount);
            if (!wear.Changed) return;
            otherCalculations.Add(DurabilityCalculation(label, item.Name, wear));
            AddDurabilityNotice(durabilityNotices, defender.Name, item.Name, equipmentName, wear,
                defensive: kind == InventorySlotKind.Armor || item is WeaponDefinition
                    { WeaponTypeId: DefenseWeaponTypeId });
        }

        if (damageType.IsPhysical())
        {
            var wearAmount = criticalMultiplier > 1 ? 2 : 1;
            if (defender.Armor is { } wornArmor)
                ApplyDefensiveWear(InventorySlotKind.Armor, 0, "🛡️🛠️ Páncélkopás", "páncélja",
                    wornArmor, wearAmount);
            if (shieldSlot >= 0)
                ApplyDefensiveWear(InventorySlotKind.Weapon, shieldSlot, "🛡️🛠️ Pajzskopás", "pajzsa",
                    shieldWeapon!, shieldBlock.IsCriticalBlock ? (criticalMultiplier > 1 ? 3 : 2) : wearAmount);
        }

        var damageTypes = monsterBonusRolls
            .Where(bonus => bonus.DamageType is not null && bonus.Value > 0)
            .Select(bonus => bonus.DamageType!.Value)
            .Append(damageType)
            .ToHashSet();
        if (damageTypes.Contains(DamageType.Acid))
        {
            var acidWear = criticalMultiplier > 1 ? 4 : 2;
            if (defender.Armor is { } corrodedArmor)
                ApplyDefensiveWear(InventorySlotKind.Armor, 0, "🧪 Savmarás", "páncélja",
                    corrodedArmor, acidWear);
            if (shieldSlot >= 0)
                ApplyDefensiveWear(InventorySlotKind.Weapon, shieldSlot, "🧪 Savmarás", "pajzsa",
                    shieldWeapon!, acidWear);
        }
        if (damageTypes.Contains(DamageType.Chaos))
        {
            var chaosTargets = new List<(InventorySlotKind Kind, int Slot, IItemDefinition Item,
                string EquipmentName)>();
            if (defender.Armor is { } chaosArmor &&
                EquipmentDurabilityRules.MaximumDurability(chaosArmor) > 0 &&
                defender.InventoryItemCondition(InventorySlotKind.Armor, 0) != EquipmentCondition.Broken)
                chaosTargets.Add((InventorySlotKind.Armor, 0, chaosArmor, "páncélja"));
            foreach (var slot in Enumerable.Range(0, 2))
                if (defender.GetInventoryItem(InventorySlotKind.Weapon, slot) is WeaponDefinition chaosWeapon &&
                    EquipmentDurabilityRules.MaximumDurability(chaosWeapon) > 0 &&
                    defender.InventoryItemCondition(InventorySlotKind.Weapon, slot) != EquipmentCondition.Broken)
                    chaosTargets.Add((InventorySlotKind.Weapon, slot, chaosWeapon,
                        chaosWeapon.WeaponTypeId == DefenseWeaponTypeId ? "pajzsa" : "fegyvere"));
            if (chaosTargets.Count > 0)
            {
                var target = chaosTargets[_random.Next(chaosTargets.Count)];
                var chaosWear = _random.Next(1, 4) * criticalMultiplier;
                ApplyDefensiveWear(target.Kind, target.Slot, "✹ Káoszmarás", target.EquipmentName,
                    target.Item, chaosWear);
            }
        }

        // ============================================================
        // ÖSSZEFOGLALÓ – 3–6. SOR
        // ============================================================

        var shieldSummary = shieldEquipped
            ? $"; {shieldWeapon!.Name} -{shield}"
            : string.Empty;

        calculationSummary.Add($"🛡️ Páncél -{armor}{shieldSummary}");

        var weaponName = enemyWeapon?.Name ?? "puszta kéz";
        var weaponDamageRange = enemyWeapon?.Damage?.ToString() ?? "1-2";
        var directDamageBonus = strengthBonus + matchingBonusDamage;
        var weaponIcon = enemyWeapon?.GetIcon() ?? "👊";

        calculationSummary.Add($"💥 {weaponIcon}{weaponName} [{weaponDamageRange}] → {baseDamage} {directDamageBonus:+#;-#;0}");
        calculationSummary.Add($"🎲 Kritikus: {criticalChance:0.##}% → {(criticalMultiplier > 1 ? "IGEN" : "nem")}");
        calculationSummary.Add($"💥 {damage} ({damageType.Name()})");

        return Detailed(AttackResult.HitFor(damage,
            $"találat: {hit.Description} → 🎯; sebzés: (Erőbónusz {strengthBonus} + fegyver {baseDamage}{monsterBonusText}) ×{criticalMultiplier} - páncél {armor} - pajzs {shield}{perkDefenseText}{reductionText}{manaShieldText} = {damageText}.{statusText}",
            criticalMultiplier > 1, durabilityNotices, shieldBlock));
    }

    private static EquipmentWearResult ApplyWeaponWear(LiveCharacter character, WeaponDefinition? weapon,
        int? preferredSlotIndex, bool critical, EquipmentWearCause wearCause)
    {
        if (weapon is null) return EquipmentWearResult.None;
        var slot = ResolveWeaponSlot(character, weapon, preferredSlotIndex);
        return slot < 0
            ? EquipmentWearResult.None
            : character.ApplyInventoryItemWear(InventorySlotKind.Weapon, slot, wearCause, critical ? 2 : 1);
    }

    private static int ResolveWeaponSlot(LiveCharacter character, WeaponDefinition? weapon,
        int? preferredSlotIndex)
    {
        if (weapon is null) return -1;
        return preferredSlotIndex is >= 0 and < 2 &&
                   character.GetInventoryItem(InventorySlotKind.Weapon, preferredSlotIndex.Value) is WeaponDefinition preferred &&
                   (ReferenceEquals(preferred, weapon) || preferred == weapon)
            ? preferredSlotIndex.Value
            : Enumerable.Range(0, 2).FirstOrDefault(index =>
                character.GetInventoryItem(InventorySlotKind.Weapon, index) is WeaponDefinition equipped &&
                (ReferenceEquals(equipped, weapon) || equipped == weapon), -1);
    }

    private static string DurabilityCalculation(string label, string itemName, EquipmentWearResult wear)
    {
        var transition = wear.ConditionChanged
            ? $"; {ConditionName(wear.PreviousCondition)} → {ConditionName(wear.CurrentCondition)}"
            : string.Empty;
        return $"{label}: {itemName} {wear.PreviousDurability} → {wear.CurrentDurability}/" +
               $"{wear.MaximumDurability}{transition}";
    }

    private static string ConditionName(EquipmentCondition condition) => condition switch
    {
        EquipmentCondition.Broken => "törött",
        EquipmentCondition.Damaged => "sérült",
        EquipmentCondition.Worn => "kopott",
        EquipmentCondition.Intact => "ép",
        _ => "nem kopó"
    };

    private static IReadOnlyList<BattleLogNotice> DurabilityNotice(string ownerName, string? itemName,
        string equipmentName, EquipmentWearResult wear, bool defensive)
    {
        if (!wear.ConditionChanged || string.IsNullOrWhiteSpace(itemName)) return [];
        var state = wear.CurrentCondition switch
        {
            EquipmentCondition.Worn => "🟡 kopottá vált",
            EquipmentCondition.Damaged => "🔴 megsérült",
            EquipmentCondition.Broken => "💥 eltört",
            _ => $"állapota megváltozott: {ConditionName(wear.CurrentCondition)}"
        };
        var consequence = wear.CurrentCondition switch
        {
            EquipmentCondition.Damaged when defensive => " A védelme a felére csökkent.",
            EquipmentCondition.Damaged => " Mostantól −1 találatot és −1 sebzést okoz.",
            EquipmentCondition.Broken when defensive => " Már nem ad védelmet.",
            EquipmentCondition.Broken => " Már nem használható fegyverként.",
            _ => string.Empty
        };
        return [new BattleLogNotice(
            $"{state}: {ownerName} {equipmentName}, {itemName} ({wear.CurrentDurability}/{wear.MaximumDurability}).{consequence}")];
    }

    private static void AddDurabilityNotice(ICollection<BattleLogNotice> notices, string ownerName,
        string itemName, string equipmentName, EquipmentWearResult wear, bool defensive)
    {
        foreach (var notice in DurabilityNotice(ownerName, itemName, equipmentName, wear, defensive))
            notices.Add(notice);
    }

    private int MonsterAbilityValue(EnemyDefinition enemy, MonsterAbilityEffect effect) => enemy.AbilityIds
        .Where(_monsterAbilities.ContainsKey)
        .Select(abilityId => _monsterAbilities[abilityId])
        .SelectMany(ability => ability.Effects)
        .Where(component => component.Effect == effect)
        .Sum(component => component.Value);

    private MonsterOnHitResult ResolveMonsterOnHitAbilities(Enemy enemyInstance,
        LiveCharacter defender, WeaponDefinition? weapon, ICollection<string>? calculation = null)
    {
        var enemy = enemyInstance.Definition;
        var rolls = new List<MonsterBonusDamageRoll>();
        var statuses = new List<string>();
        foreach (var ability in enemy.AbilityIds.Where(_monsterAbilities.ContainsKey)
                     .Select(abilityId => _monsterAbilities[abilityId])
                     .Where(ability => ability.Trigger == MonsterAbilityTrigger.OnHit &&
                                       AppliesToWeapon(ability, weapon) &&
                                       enemyInstance.IsAbilityReady(ability.Id) &&
                                       enemyInstance.HasAbilityCharge(ability)))
        {
            var roll = _random.Next(100);
            if (roll >= ability.ChancePercent) continue;
            enemyInstance.ConsumeAbilityCharge(ability);
            enemyInstance.StartAbilityCooldown(ability.Id, ability.Cooldown);
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
        if (defender.OperationalArmor?.Defense is not { } range) return 0;
        var rolled = Roll(range);
        var defense = defender.HasPerk(PerkIds.KnightArmorMaster)
            ? Math.Max(rolled, (int)Math.Ceiling((range.Minimum + range.Maximum) / 2.0))
            : rolled;
        return EquipmentDurabilityRules.ScaleDefense(defense,
            defender.InventoryItemCondition(InventorySlotKind.Armor, 0));
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
    private ShieldBlockResult ResolveCriticalBlock(ShieldDefenseSnapshot? shield, DamageType damageType)
    {
        if (shield is null || !damageType.IsPhysical() || shield.BlockRating <= 0)
            return ShieldBlockResult.NotAttempted;
        return ShieldRules.ResolveCriticalBlock(shield, damageType, _random.Next(1, 21));
    }

    private int Roll(ValueRange range) => _random.Next(range.Minimum, range.Maximum + 1);
    private static int ApplyDefense(int rawDamage, int defense) => Math.Max(1, rawDamage - defense);

    private sealed record DamageApplicationResult(string ShortLog, string Details)
    {
        public static DamageApplicationResult Empty { get; } = new(string.Empty, string.Empty);
    }

    private sealed record CharacterInitiativeRoll(
        int FirstStrikeNormalBonus,
        int FirstStrikeOpeningBonus,
        int DisciplineBonus,
        int NormalBase,
        InitiativeRoll Roll,
        int OpeningTotal);

    private sealed record EnemyDefenseSnapshot(
        string Name,
        int CurrentHitPoints,
        int MaximumHitPoints,
        int EffectiveSpeed,
        ValueRange Armor,
        int ArmorAbilityBonus,
        bool IsUndead,
        DamageResistance? Resistances,
        int PhysicalReduction,
        ShieldDefenseSnapshot? Shield,
        WeaponDefinition? Weapon)
    {
        public bool IsWounded => CurrentHitPoints * 2 <= Math.Max(1, MaximumHitPoints);

        public EnemyDefenseSnapshot Apply(AttackResult attack) => !attack.Hit
            ? this
            : this with { CurrentHitPoints = Math.Max(0, CurrentHitPoints - attack.Damage) };

        public static EnemyDefenseSnapshot From(Enemy enemy, int armorPenalty, int armorAbilityBonus)
        {
            var maximumHitPoints = enemy.MaximumHitPoints;
            return new EnemyDefenseSnapshot(
                enemy.Name,
                enemy.CurrentHitPoints,
                maximumHitPoints,
                enemy.EffectiveSpeed,
                enemy.Definition.Armor is { } armor
                    ? new ValueRange(Math.Max(0, armor.Minimum - Math.Max(0, armorPenalty)),
                        Math.Max(0, armor.Maximum - Math.Max(0, armorPenalty)))
                    : new ValueRange(0, 0),
                armorAbilityBonus,
                enemy.Definition.HasTrait(EnemyTraits.Undead),
                enemy.Definition.Resistances,
                enemy.SpellEffectValue(ActiveSpellEffectType.PhysicalReduction),
                enemy.EquippedShield is { } shield ? new ShieldDefenseSnapshot(shield) : null,
                enemy.EquippedWeapon);
        }
    }

    private sealed record PlayerAttackOptions(
        int PositionalHitBonus = 0,
        string? PositionalAdvantage = null,
        bool TacticalBackstab = false,
        WeaponDefinition? AttackWeapon = null,
        bool AllowAmbush = true,
        int? AttackWeaponSlotIndex = null,
        bool WoundedTarget = false,
        int RangedHitModifier = 0);

    private sealed record EnemyAttackOptions(
        WeaponDefinition? AttackWeapon = null,
        bool AllowWeaponFallback = true,
        int AlliedGuardDefense = 0);

    private sealed record InitiativeRoll(int Total, string ModifierText);
    private sealed record HitRollResult(bool Hit, int NaturalRoll, string Description);
    private sealed record MonsterBonusDamageRoll(int Value, DamageType? DamageType);
    private sealed record MonsterOnHitResult(IReadOnlyList<MonsterBonusDamageRoll> Damage, string StatusText);
    private sealed record AttackResult(bool Hit, int Damage, string Message, bool Critical,
        AttackDetails? Details = null, IReadOnlyList<BattleLogNotice>? WearNotices = null,
        ShieldBlockResult? BlockResult = null)
    {
        public IReadOnlyList<BattleLogNotice> DurabilityNotices => WearNotices ?? [];
        public ShieldBlockResult ShieldBlock => BlockResult ?? ShieldBlockResult.NotAttempted;
        public static AttackResult Miss(string message) => new(false, 0, message, false);
        public static AttackResult HitFor(int damage, string message, bool critical = false,
            IReadOnlyList<BattleLogNotice>? durabilityNotices = null,
            ShieldBlockResult? shieldBlock = null) =>
            new(true, damage, message, critical, WearNotices: durabilityNotices, BlockResult: shieldBlock);
    }
}

public sealed record BattleResult(bool PlayerWon, int Rounds, IReadOnlyList<string> Events);
public sealed record BattleLogNotice(string Message, BattleLogKind Kind = BattleLogKind.Information);
public sealed record BattleLogEntry(string Message, BattleLogKind Kind, BattleActionDetails? Details = null,
    IReadOnlyList<BattleLogNotice>? FollowUps = null,
    IReadOnlyList<ShieldBlockResult>? ShieldBlocks = null);
public sealed record EnemyAttackResolution(BattleLogEntry Entry, bool Hit, int DamageDealt);
public enum MonsterStrengthContestOutcome { Resisted, Stagger, Push }
public sealed record MonsterStrengthContestResult(int Strength, int StrengthPressure, int Roll, int Total,
    int Health, int ResistanceRoll, int ShieldBonus, int DefensiveBonus, int Resistance, int Margin,
    MonsterStrengthContestOutcome Outcome);
public sealed record ShieldBashContestResult(int AttackerRoll, int AttackerStrength, int StrengthPressure,
    int ShieldPower, int ShieldWeightBonus, int AttackTotal, int DefenderRoll, int DefenderStability,
    int DefenderShieldBonus, int DefenseTotal, int Margin, MonsterStrengthContestOutcome Outcome, int Damage);
public sealed record EnemyTurnStartResult(bool CanAct, IReadOnlyList<BattleLogEntry> Entries);
public sealed record BattlePlayerAction(string Message, BattleLogKind Kind = BattleLogKind.PlayerAttack,
    int DamageToEnemy = 0, int ExtraPlayerActions = 0);
public enum BattleLogKind { Information, PlayerAttack, EnemyAttack, CriticalHit }
