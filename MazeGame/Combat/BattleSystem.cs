using MazeGame.Domain.Characters;
using MazeGame.Domain.Combat;
using MazeGame.Domain.Magic;

namespace MazeGame.Combat;

/// <summary>A közelharc teljes, felhasználó által léptetett körökre osztott szabályrendszere.</summary>
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
        Func<BattlePlayerAction?>? choosePlayerAction = null)
    {
        var defender = enemy.Definition with { HitPoints = enemy.CurrentHitPoints };
        var context = new BattleContext(player);
        ApplyBattleStartPerks(player, onRound);
        var statusCosts = player.ApplyBattleStartStatusEffects();
        if (statusCosts.VitalityLost > 0 || statusCosts.ManaLost > 0)
        {
            var costs = new List<string>();
            if (statusCosts.VitalityLost > 0) costs.Add($"-{statusCosts.VitalityLost} HP");
            if (statusCosts.ManaLost > 0) costs.Add($"-{statusCosts.ManaLost} manna");
            onRound(new BattleLogEntry($"Állapothatás a csata kezdetén: {string.Join(", ", costs)}.", BattleLogKind.Information));
        }
        var perkInitiativeBonus = player.HasPerk(PerkIds.FighterFirstStrike) ? 10 : 0;
        var magicInitiativeBonus = player.GetMagicItemBonus(MagicItemEffect.Initiative);
        var spellInitiativeBonus = player.SpellEffectValue(ActiveSpellEffectType.InitiativeBonus);
        var initiativeBonus = perkInitiativeBonus + magicInitiativeBonus + spellInitiativeBonus;
        var playerInitiative = RollInitiative(player.Abilities.Dexterity + initiativeBonus - player.StatusInitiativePenalty);
        var enemyInitiativeBonus = MonsterAbilityValue(defender, MonsterAbilityEffect.InitiativeBonus);
        var enemyInitiative = RollInitiative(enemy.EffectiveSpeed + enemyInitiativeBonus);
        var playerAttacks = playerInitiative.Total >= enemyInitiative.Total;
        var initiativeNotes = new List<string>();
        if (perkInitiativeBonus > 0) initiativeNotes.Add($"Első csapás +{perkInitiativeBonus}");
        if (magicInitiativeBonus > 0) initiativeNotes.Add($"varázstárgy +{magicInitiativeBonus}");
        if (spellInitiativeBonus > 0) initiativeNotes.Add($"áldás +{spellInitiativeBonus}");
        var perkText = initiativeNotes.Count > 0 ? $" [{string.Join(", ", initiativeNotes)}]" : string.Empty;
        var events = new List<string>
        {
            $"Kezdeményezés: {player.Name} Ügy {player.Abilities.Dexterity}{perkText}" +
            (player.StatusInitiativePenalty > 0 ? $" - állapot {player.StatusInitiativePenalty}" : string.Empty) +
            $" {playerInitiative.ModifierText} = {playerInitiative.Total}; " +
            $"{enemy.Name} Gy {defender.Speed ?? 1}" + (enemyInitiativeBonus > 0 ? $" + képesség {enemyInitiativeBonus}" : string.Empty) +
            $" {enemyInitiative.ModifierText} = {enemyInitiative.Total}. {(playerAttacks ? player.Name : enemy.Name)} kezd."
        };
        onRound(new BattleLogEntry(events[0], BattleLogKind.Information));
        var round = 0;
        var queuedPlayerActions = 0;

        while (player.CurrentVitality > 0 && defender.HitPoints is > 0)
        {
            round++;
            string message;
            BattleLogKind kind;
            if (playerAttacks)
            {
                player.AdvanceSpellEffects();
                var chosenAction = choosePlayerAction?.Invoke();
                if (chosenAction is not null)
                {
                    if (chosenAction.DamageToEnemy > 0)
                        defender = defender with { HitPoints = Math.Max(0, defender.HitPoints!.Value - chosenAction.DamageToEnemy) };
                    queuedPlayerActions += chosenAction.ExtraPlayerActions;
                    var statusTicks = player.ApplyTurnEndStatusEffects(_random);
                    var statusText = statusTicks.Count == 0 ? string.Empty :
                        $" Állapothatások: {string.Join(", ", statusTicks.Select(tick => $"{tick.Icon} {tick.Name} -{tick.Damage} HP" + (tick.Expired ? " (elmúlt)" : string.Empty)))}.";
                    message = $"{round}. kör — {chosenAction.Message} {enemy.Name} HP: {defender.HitPoints}/{enemy.Definition.HitPoints}.{statusText}";
                    kind = chosenAction.Kind;
                }
                else
                {
                    var count = player.HasPerk(PerkIds.BarbarianBerserkerRage) && player.CurrentVitality * 2 < player.MaximumVitality ? 2 : 1;
                    var messages = new List<string>();
                    var criticalHit = false;
                    for (var index = 0; index < count && defender.HitPoints is > 0; index++)
                    {
                        var attack = PlayerAttack(player, defender, context, enemy.EffectiveSpeed);
                        criticalHit |= attack.Critical;
                        defender = ApplyAttack(defender, attack);
                        messages.Add(attack.Message);
                        if (index == 0 && attack.Hit && defender.HitPoints is > 0 && player.HasPerk(PerkIds.FighterSteelStorm) && _random.NextDouble() < 0.35)
                        {
                            var extra = PlayerAttack(player, defender, context, enemy.EffectiveSpeed);
                            criticalHit |= extra.Critical;
                            defender = ApplyAttack(defender, extra);
                            messages.Add($"Acélvihar: {extra.Message}");
                        }
                    }
                    var statusTicks = player.ApplyTurnEndStatusEffects(_random);
                    var statusText = statusTicks.Count == 0 ? string.Empty :
                        $" Állapothatások: {string.Join(", ", statusTicks.Select(tick => $"{tick.Icon} {tick.Name} -{tick.Damage} HP" + (tick.Expired ? " (elmúlt)" : string.Empty)))}.";
                    message = $"{round}. kör — {player.Name} támadja {enemy.Name}-t. {string.Join(" ", messages)} {enemy.Name} HP: {defender.HitPoints}/{enemy.Definition.HitPoints}.{statusText}";
                    kind = criticalHit ? BattleLogKind.CriticalHit : BattleLogKind.PlayerAttack;
                }
            }
            else
            {
                var spellTick = enemy.AdvanceSpellEffects(_random);
                if (spellTick.Damage > 0)
                    defender = defender with { HitPoints = Math.Max(0, defender.HitPoints!.Value - spellTick.Damage) };
                var effectText = spellTick.Notes.Count == 0 ? string.Empty : $" {string.Join(", ", spellTick.Notes)}.";
                if (defender.HitPoints <= 0)
                {
                    message = $"{round}. kör — {enemy.Name} elbukik a varázshatásoktól.{effectText}";
                    kind = BattleLogKind.PlayerAttack;
                }
                else if (spellTick.SkipAction)
                {
                    message = $"{round}. kör — {enemy.Name} varázshatás miatt kihagyja az akcióját.{effectText}";
                    kind = BattleLogKind.Information;
                }
                else
                {
                    var attack = EnemyAttack(defender, player, context, enemy.EffectiveSpeed);
                    var survival = attack.Hit ? ApplyEnemyDamage(player, attack.Damage, context) : string.Empty;
                    message = $"{round}. kör — {enemy.Name} támadja {player.Name}-t. {attack.Message} {survival} {player.Name} HP: {player.CurrentVitality}/{player.MaximumVitality}.{effectText}";
                    kind = attack.Critical ? BattleLogKind.CriticalHit : BattleLogKind.EnemyAttack;
                }
            }
            events.Add(message);
            onRound(new BattleLogEntry(message, kind));
            if (playerAttacks && queuedPlayerActions > 0)
                queuedPlayerActions--;
            else
                playerAttacks = !playerAttacks;
        }
        enemy.SetCurrentHitPoints(defender.HitPoints ?? 0);
        return new BattleResult(player.CurrentVitality > 0, round, events);
    }

    private static EnemyDefinition ApplyAttack(EnemyDefinition defender, AttackResult attack) => attack.Hit
        ? defender with { HitPoints = Math.Max(0, defender.HitPoints!.Value - attack.Damage) }
        : defender;

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
        return new InitiativeRoll(speed + modifier, $"{(modifier >= 0 ? "+" : string.Empty)}1d2({modifier})");
    }

    private AttackResult PlayerAttack(LiveCharacter player, EnemyDefinition defender, BattleContext context, int defenderSpeed)
    {
        player.BreakSanctuary();
        var forcedHit = context.ShadowStepReady;
        context.ShadowStepReady = false;
        var weapon = player.WeaponSlots.FirstOrDefault(item => item?.WeaponTypeId != DefenseWeaponTypeId);
        var blessedWeaponBonus = player.HasPerk(PerkIds.PriestBlessedWeapon) &&
                                 defender.AbilityIds.Contains(MonsterAbilityIds.Undead, StringComparer.OrdinalIgnoreCase) ? 2 : 0;
        var invisibilityBonus = player.SpellEffectValue(ActiveSpellEffectType.Invisibility);
        var strengthHitBonus = StrengthHitBonus(player);
        var hitBonus = (weapon is not null && player.HasPerk(PerkIds.FighterWeaponMaster) ? 2 : 0) +
                       player.GetMagicItemBonus(MagicItemEffect.Hit) + blessedWeaponBonus;
        hitBonus += invisibilityBonus + strengthHitBonus + player.SpellEffectValue(ActiveSpellEffectType.HitBonus);
        var hit = HitRoll(player.Abilities.Dexterity, defenderSpeed, hitBonus - player.StatusHitPenalty, forcedHit);
        if (invisibilityBonus > 0) player.BreakInvisibility();
        var strengthHitText = strengthHitBonus > 0 ? $" [Erő-találat +{strengthHitBonus}]" : string.Empty;
        if (!hit.Hit)
        {
            context.ConsecutivePlayerHits = 0;
            return AttackResult.Miss($"találat: {hit.Description} → NEM TALÁL.{strengthHitText}");
        }

        var baseDamage = weapon?.Damage is { } range ? Roll(range) : Roll(new ValueRange(1, 2));
        var usesDexterity = weapon is not null && string.Equals(weapon.WeaponTypeId, DexterityWeaponTypeId, StringComparison.OrdinalIgnoreCase);
        var ability = usesDexterity ? player.Abilities.Dexterity : player.Abilities.Strength;
        var abilityBonus = AbilityDamageBonus(ability);
        var randomBonus = Roll(new ValueRange(0, 2));
        var perkBonus = player.GetMagicItemBonus(MagicItemEffect.Damage) + blessedWeaponBonus +
                        player.SpellEffectValue(ActiveSpellEffectType.DamageBonus);
        var notes = new List<string>();
        if (blessedWeaponBonus > 0) notes.Add("Áldott fegyver +2");
        if (player.HasPerk(PerkIds.BarbarianBloodlust) && player.CurrentVitality * 2 < player.MaximumVitality) { perkBonus += 3; notes.Add("Vérszomj +3"); }
        if (player.HasPerk(PerkIds.BarbarianPrimalStrength)) { perkBonus += 5; notes.Add("Őserő +5"); }
        if (player.HasPerk(PerkIds.BarbarianRage))
        {
            perkBonus += context.ConsecutivePlayerHits;
            if (context.ConsecutivePlayerHits > 0) notes.Add($"Őrjöngés +{context.ConsecutivePlayerHits}");
        }
        var armor = (defender.Armor ?? 0) + MonsterAbilityValue(defender, MonsterAbilityEffect.ArmorBonus);
        var damageMultiplier = 1;
        if (context.AmbushAvailable) { damageMultiplier *= 2; context.AmbushAvailable = false; notes.Add("Orvtámadás ×2"); }
        var criticalMultiplier = player.HasPerk(PerkIds.ThiefDeadlyAccuracy) && hit.NaturalRoll >= 18
            ? 3
            : hit.NaturalRoll == 20 ? 2 : 1;
        if (criticalMultiplier > 1)
            notes.Add(criticalMultiplier == 3 ? "💥 KRITIKUS — Halálos pontosság ×3" : "💥 KRITIKUS TALÁLAT ×2");
        damageMultiplier *= criticalMultiplier;
        var rawDamage = baseDamage + abilityBonus + randomBonus + perkBonus;
        var damage = ApplyDefense(rawDamage * damageMultiplier, armor);
        var statusDamagePenalty = player.StatusPhysicalDamagePenalty;
        damage = Math.Max(1, damage - statusDamagePenalty);
        if (statusDamagePenalty > 0) notes.Add($"állapot -{statusDamagePenalty}");
        if (player.HasPerk(PerkIds.ThiefPoisoner))
        {
            var poison = Roll(new ValueRange(1, 6));
            damage += poison;
            notes.Add($"Méreg +{poison}");
        }
        context.ConsecutivePlayerHits++;
        var noteText = notes.Count == 0 ? string.Empty : $" [{string.Join(", ", notes)}]";
        var perkBonusText = perkBonus == 0 ? string.Empty : $" + bónusz {perkBonus}";
        return AttackResult.HitFor(damage,
            $"{(criticalMultiplier > 1 ? "💥 KRITIKUS TALÁLAT! " : string.Empty)}találat: {hit.Description} → TALÁL;{strengthHitText} sebzés: (alap {baseDamage} + képesség {abilityBonus} + dobás {randomBonus}{perkBonusText}) ×{damageMultiplier} - páncél {armor} = {damage}.{noteText}",
            criticalMultiplier > 1);
    }

    private int StrengthHitBonus(LiveCharacter character) => _strengthHitBonuses
        .Where(bonus => string.Equals(bonus.CharacterClassId, character.CharacterClass.Id,
            StringComparison.OrdinalIgnoreCase) && bonus.MinimumStrength <= character.Abilities.Strength)
        .OrderByDescending(bonus => bonus.MinimumStrength)
        .Select(bonus => bonus.Bonus)
        .FirstOrDefault();

    private AttackResult EnemyAttack(EnemyDefinition attacker, LiveCharacter defender, BattleContext context, int attackerSpeed)
    {
        if (context.ChallengeAvailable) { context.ChallengeAvailable = false; return AttackResult.Miss("Kihívás: az első támadás automatikusan elhibázza."); }
        if (defender.HasPerk(PerkIds.PriestSanctuary) && _random.NextDouble() < 0.20) return AttackResult.Miss("Szentély: az ellenfél elveszíti a támadását.");
        if (defender.HasSpellEffect(ActiveSpellEffectType.Invisibility)) return AttackResult.Miss("Láthatatlanság: az ellenfél nem talál célpontot.");
        var hit = HitRoll(attackerSpeed, defender.Abilities.Dexterity, 0, false);
        if (!hit.Hit) return AttackResult.Miss($"találat: {hit.Description} → NEM TALÁL.");
        var criticalMultiplier = hit.NaturalRoll == 20 ? 2 : 1;
        if (criticalMultiplier == 1 && defender.HasPerk(PerkIds.ThiefEvasion) && _random.NextDouble() < 0.15)
        {
            context.ShadowStepReady = defender.HasPerk(PerkIds.ThiefShadowStep);
            return AttackResult.Miss("Kitérés: a találat elkerülve." + (context.ShadowStepReady ? " Árnyéklépés aktiválva." : string.Empty));
        }

        var strength = attacker.Strength ?? 1;
        var randomDamage = Roll(new ValueRange(1, Math.Max(2, strength)));
        var armor = RollArmor(defender);
        var shieldEquipped = defender.WeaponSlots.Any(item => item?.WeaponTypeId == DefenseWeaponTypeId);
        var shield = defender.WeaponSlots.FirstOrDefault(item => item?.WeaponTypeId == DefenseWeaponTypeId)?.Damage is { } shieldRange ? Roll(shieldRange) : 0;
        var evilWard = IsUnholy(attacker)
            ? defender.ActiveSpellEffects.Where(effect => effect.Type == ActiveSpellEffectType.ProtectionFromEvil).ToList()
            : [];
        var evilWardDefense = evilWard.Sum(effect => ParseInt(effect.Parameter));
        var perkDefense = (defender.HasPerk(PerkIds.BarbarianThickSkin) ? 1 : 0) +
                          (shieldEquipped && defender.HasPerk(PerkIds.KnightShieldWall) ? 2 : 0) +
                          defender.GetMagicItemBonus(MagicItemEffect.Defense) +
                          defender.SpellEffectValue(ActiveSpellEffectType.DefenseBonus) + evilWardDefense;
        var reduction = (defender.HasPerk(PerkIds.FighterUnbreakable) ? 2 : 0) + (defender.HasPerk(PerkIds.KnightInvincible) ? 4 : 0);
        var monsterBonusDamage = RollMonsterBonusDamage(attacker);
        var rawDamage = strength + randomDamage + monsterBonusDamage;
        var damage = Math.Max(0, ApplyDefense(rawDamage * criticalMultiplier, armor + shield + perkDefense) - reduction);
        var physicalReduction = Math.Clamp(defender.SpellEffectValue(ActiveSpellEffectType.PhysicalReduction) +
            defender.SpellEffectValue(ActiveSpellEffectType.Sanctuary) + evilWard.Sum(effect => effect.Value), 0, 100);
        if (physicalReduction > 0) damage = damage * (100 - physicalReduction) / 100;
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
        var monsterBonusText = monsterBonusDamage == 0 ? string.Empty : $" + szörnyképesség {monsterBonusDamage}";
        var statusText = ApplyMonsterStatusAbilities(attacker, defender);
        return AttackResult.HitFor(damage,
            $"{(criticalMultiplier > 1 ? "💥 KRITIKUS TALÁLAT! " : string.Empty)}találat: {hit.Description} → TALÁL; sebzés: (Erő {strength} + dobás {randomDamage}{monsterBonusText}) ×{criticalMultiplier} - páncél {armor} - pajzs {shield}{perkDefenseText}{reductionText}{manaShieldText} = {damage}.{statusText}",
            criticalMultiplier > 1);
    }

    private int MonsterAbilityValue(EnemyDefinition enemy, MonsterAbilityEffect effect) => enemy.AbilityIds
        .Where(_monsterAbilities.ContainsKey)
        .Select(abilityId => _monsterAbilities[abilityId])
        .Where(ability => ability.Effect == effect)
        .Sum(ability => ability.Value);

    private int RollMonsterBonusDamage(EnemyDefinition enemy) => enemy.AbilityIds
        .Where(_monsterAbilities.ContainsKey)
        .Select(abilityId => _monsterAbilities[abilityId])
        .Where(ability => ability.Effect == MonsterAbilityEffect.ExtraDamage &&
                          _random.Next(100) < ability.ChancePercent)
        .Sum(ability => ability.Value);

    private string ApplyMonsterStatusAbilities(EnemyDefinition enemy, LiveCharacter defender)
    {
        var applied = new List<string>();
        foreach (var ability in enemy.AbilityIds.Where(_monsterAbilities.ContainsKey)
                     .Select(abilityId => _monsterAbilities[abilityId]))
        {
            var statusId = ability.Effect switch
            {
                MonsterAbilityEffect.Poison => CharacterStatusIds.Poisoned,
                MonsterAbilityEffect.Disease => CharacterStatusIds.Diseased,
                MonsterAbilityEffect.Bleeding => CharacterStatusIds.Bleeding,
                _ => null
            };
            var sanctuaryImmunity = defender.HasSpellEffect(ActiveSpellEffectType.Sanctuary) &&
                                    statusId is CharacterStatusIds.Poisoned or CharacterStatusIds.Diseased or CharacterStatusIds.Bleeding;
            var evilWardImmunity = IsUnholy(enemy) && defender.HasSpellEffect(ActiveSpellEffectType.ProtectionFromEvil) &&
                                   statusId is CharacterStatusIds.Poisoned or CharacterStatusIds.Diseased;
            if (statusId is null || statusId == CharacterStatusIds.Bleeding &&
                defender.HasSpellEffect(ActiveSpellEffectType.BleedingImmunity) || sanctuaryImmunity || evilWardImmunity ||
                _random.Next(100) >= ability.ChancePercent ||
                !_statuses.TryGetValue(statusId, out var status)) continue;
            var wasActive = defender.HasStatus(statusId);
            defender.AddStatus(status);
            applied.Add($"{status.Icon} {status.Name}" + (wasActive ? " időtartama újraindult" : " felkerült"));
        }
        return applied.Count == 0 ? string.Empty : $" ⚠️ ÁLLAPOT: {string.Join(", ", applied)}!";
    }

    private int RollArmor(LiveCharacter defender)
    {
        if (defender.Armor?.Defense is not { } range) return 0;
        var rolled = Roll(range);
        return defender.HasPerk(PerkIds.KnightArmorMaster) ? Math.Max(rolled, (int)Math.Ceiling((range.Minimum + range.Maximum) / 2.0)) : rolled;
    }

    private string ApplyEnemyDamage(LiveCharacter player, int damage, BattleContext context)
    {
        if (damage >= player.CurrentVitality && player.TakeSpellEffect(ActiveSpellEffectType.GuardianAngel) is { } angel)
        {
            player.ReceiveDamage(Math.Max(0, player.CurrentVitality - 1));
            var healing = ((angel.PeriodicDamage?.Roll(_random) ?? 0) + angel.IntelligenceBonus) *
                          angel.DamageMultiplierPercent / 100;
            var beforeHealing = player.CurrentVitality;
            player.RestoreVitality(healing);
            return $"👼 Őrangyal: a halálos csapás kivédve és +{player.CurrentVitality - beforeHealing} HP.";
        }
        if (damage >= player.CurrentVitality && context.GuardianAngelAvailable)
        {
            context.GuardianAngelAvailable = false;
            player.RestoreVitality(25);
            return "Őrangyal: a halálos csapás kivédve és +25 HP.";
        }
        if (damage >= player.CurrentVitality && context.LastFortressAvailable)
        {
            context.LastFortressAvailable = false;
            player.ReceiveDamage(Math.Max(0, player.CurrentVitality - 1));
            return "Utolsó erőd: 1 HP-n talpon marad.";
        }
        player.ReceiveDamage(damage);
        return string.Empty;
    }

    private static bool IsUnholy(EnemyDefinition enemy) => enemy.AbilityIds.Any(abilityId =>
        string.Equals(abilityId, MonsterAbilityIds.Undead, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(abilityId, MonsterAbilityIds.Demonic, StringComparison.OrdinalIgnoreCase));

    private static int ParseInt(string? value) => int.TryParse(value, out var parsed) ? parsed : 0;

    private HitRollResult HitRoll(int attackerSpeed, int defenderSpeed, int attackerBonus, bool forcedHit)
    {
        var roll = Roll(new ValueRange(1, 20));
        if (forcedHit) return new HitRollResult(true, roll, $"Árnyéklépés → automatikus találat ({roll})");
        var total = roll + attackerSpeed + attackerBonus;
        var target = 11 + defenderSpeed;
        return new HitRollResult(roll == 20 || total >= target, roll,
            roll == 20 ? $"természetes 20 ({total} vs {target})" : $"{total} vs {target}" + (attackerBonus > 0 ? $" (+{attackerBonus} bónusz)" : string.Empty));
    }

    private static int AbilityDamageBonus(int ability) => Math.Max(0, (ability - 1) / 2);
    private int Roll(ValueRange range) => _random.Next(range.Minimum, range.Maximum + 1);
    private static int ApplyDefense(int rawDamage, int defense) => Math.Max(1, rawDamage - defense);

    private sealed class BattleContext
    {
        public BattleContext(LiveCharacter player)
        {
            ChallengeAvailable = player.HasPerk(PerkIds.KnightChallenge);
            GuardianAngelAvailable = player.HasPerk(PerkIds.KnightGuardianAngel);
            LastFortressAvailable = player.HasPerk(PerkIds.FighterLastFortress);
            AmbushAvailable = player.HasPerk(PerkIds.ThiefAmbush);
        }
        public bool ChallengeAvailable { get; set; }
        public bool GuardianAngelAvailable { get; set; }
        public bool LastFortressAvailable { get; set; }
        public bool AmbushAvailable { get; set; }
        public bool ShadowStepReady { get; set; }
        public int ConsecutivePlayerHits { get; set; }
    }

    private sealed record InitiativeRoll(int Total, string ModifierText);
    private sealed record HitRollResult(bool Hit, int NaturalRoll, string Description);
    private sealed record AttackResult(bool Hit, int Damage, string Message, bool Critical)
    {
        public static AttackResult Miss(string message) => new(false, 0, message, false);
        public static AttackResult HitFor(int damage, string message, bool critical = false) => new(true, damage, message, critical);
    }
}

public sealed record BattleResult(bool PlayerWon, int Rounds, IReadOnlyList<string> Events);
public sealed record BattleLogEntry(string Message, BattleLogKind Kind);
public sealed record BattlePlayerAction(string Message, BattleLogKind Kind = BattleLogKind.PlayerAttack,
    int DamageToEnemy = 0, int ExtraPlayerActions = 0);
public enum BattleLogKind { Information, PlayerAttack, EnemyAttack, CriticalHit }
