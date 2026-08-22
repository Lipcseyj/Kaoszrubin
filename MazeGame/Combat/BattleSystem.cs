using MazeGame.Domain.Characters;
using MazeGame.Domain.Combat;
using MazeGame.Domain.Magic;

namespace MazeGame.Combat;

/// <summary>A közelharc teljes, felhasználó által léptetett körökre osztott szabályrendszere.</summary>
public sealed class BattleSystem(Random random)
{
    private const string DexterityWeaponTypeId = "WT002";
    private const string DefenseWeaponTypeId = "WT003";
    private readonly Random _random = random;

    public BattleResult Resolve(LiveCharacter player, Enemy enemy, Action<BattleLogEntry> onRound)
    {
        var defender = enemy.Definition with { HitPoints = enemy.CurrentHitPoints };
        var context = new BattleContext(player);
        ApplyBattleStartPerks(player, onRound);
        var perkInitiativeBonus = player.HasPerk(PerkIds.FighterFirstStrike) ? 10 : 0;
        var magicInitiativeBonus = player.GetMagicItemBonus(MagicItemEffect.Initiative);
        var initiativeBonus = perkInitiativeBonus + magicInitiativeBonus;
        var playerInitiative = RollInitiative(player.Abilities.Dexterity + initiativeBonus);
        var enemyInitiative = RollInitiative(defender.Speed ?? 1);
        var playerAttacks = playerInitiative.Total >= enemyInitiative.Total;
        var initiativeNotes = new List<string>();
        if (perkInitiativeBonus > 0) initiativeNotes.Add($"Első csapás +{perkInitiativeBonus}");
        if (magicInitiativeBonus > 0) initiativeNotes.Add($"varázstárgy +{magicInitiativeBonus}");
        var perkText = initiativeNotes.Count > 0 ? $" [{string.Join(", ", initiativeNotes)}]" : string.Empty;
        var events = new List<string>
        {
            $"Kezdeményezés: {player.Name} Ügy {player.Abilities.Dexterity}{perkText} {playerInitiative.ModifierText} = {playerInitiative.Total}; " +
            $"{enemy.Name} Gy {defender.Speed ?? 1} {enemyInitiative.ModifierText} = {enemyInitiative.Total}. {(playerAttacks ? player.Name : enemy.Name)} kezd."
        };
        onRound(new BattleLogEntry(events[0], BattleLogKind.Information));
        var round = 0;

        while (player.CurrentVitality > 0 && defender.HitPoints is > 0)
        {
            round++;
            string message;
            BattleLogKind kind;
            if (playerAttacks)
            {
                var count = player.HasPerk(PerkIds.BarbarianBerserkerRage) && player.CurrentVitality * 2 < player.MaximumVitality ? 2 : 1;
                var messages = new List<string>();
                for (var index = 0; index < count && defender.HitPoints is > 0; index++)
                {
                    var attack = PlayerAttack(player, defender, context);
                    defender = ApplyAttack(defender, attack);
                    messages.Add(attack.Message);
                    if (index == 0 && attack.Hit && defender.HitPoints is > 0 && player.HasPerk(PerkIds.FighterSteelStorm) && _random.NextDouble() < 0.35)
                    {
                        var extra = PlayerAttack(player, defender, context);
                        defender = ApplyAttack(defender, extra);
                        messages.Add($"Acélvihar: {extra.Message}");
                    }
                }
                message = $"{round}. kör — {player.Name} támadja {enemy.Name}-t. {string.Join(" ", messages)} {enemy.Name} HP: {defender.HitPoints}/{enemy.Definition.HitPoints}.";
                kind = BattleLogKind.PlayerAttack;
            }
            else
            {
                var attack = EnemyAttack(defender, player, context);
                var survival = attack.Hit ? ApplyEnemyDamage(player, attack.Damage, context) : string.Empty;
                message = $"{round}. kör — {enemy.Name} támadja {player.Name}-t. {attack.Message} {survival} {player.Name} HP: {player.CurrentVitality}/{player.MaximumVitality}.";
                kind = BattleLogKind.EnemyAttack;
            }
            events.Add(message);
            onRound(new BattleLogEntry(message, kind));
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

    private AttackResult PlayerAttack(LiveCharacter player, EnemyDefinition defender, BattleContext context)
    {
        var forcedHit = context.ShadowStepReady;
        context.ShadowStepReady = false;
        var weapon = player.WeaponSlots.FirstOrDefault(item => item?.WeaponTypeId != DefenseWeaponTypeId);
        var hitBonus = (weapon is not null && player.HasPerk(PerkIds.FighterWeaponMaster) ? 2 : 0) +
                       player.GetMagicItemBonus(MagicItemEffect.Hit);
        var hit = HitRoll(player.Abilities.Dexterity, defender.Speed ?? 1, hitBonus, forcedHit);
        if (!hit.Hit)
        {
            context.ConsecutivePlayerHits = 0;
            return AttackResult.Miss($"találat: {hit.Description} → NEM TALÁL.");
        }

        var baseDamage = weapon?.Damage is { } range ? Roll(range) : Roll(new ValueRange(1, 2));
        var usesDexterity = weapon is not null && string.Equals(weapon.WeaponTypeId, DexterityWeaponTypeId, StringComparison.OrdinalIgnoreCase);
        var ability = usesDexterity ? player.Abilities.Dexterity : player.Abilities.Strength;
        var abilityBonus = AbilityDamageBonus(ability);
        var randomBonus = Roll(new ValueRange(0, 2));
        var perkBonus = player.GetMagicItemBonus(MagicItemEffect.Damage);
        var notes = new List<string>();
        if (player.HasPerk(PerkIds.BarbarianBloodlust) && player.CurrentVitality * 2 < player.MaximumVitality) { perkBonus += 3; notes.Add("Vérszomj +3"); }
        if (player.HasPerk(PerkIds.BarbarianPrimalStrength)) { perkBonus += 5; notes.Add("Őserő +5"); }
        if (player.HasPerk(PerkIds.BarbarianRage))
        {
            perkBonus += context.ConsecutivePlayerHits;
            if (context.ConsecutivePlayerHits > 0) notes.Add($"Őrjöngés +{context.ConsecutivePlayerHits}");
        }
        var armor = defender.Armor ?? 0;
        var damage = ApplyDefense(baseDamage + abilityBonus + randomBonus + perkBonus, armor);
        var multiplier = 1;
        if (context.AmbushAvailable) { multiplier *= 2; context.AmbushAvailable = false; notes.Add("Orvtámadás ×2"); }
        if (player.HasPerk(PerkIds.ThiefDeadlyAccuracy) && hit.NaturalRoll >= 18) { multiplier *= 3; notes.Add("Halálos pontosság ×3"); }
        damage *= multiplier;
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
            $"találat: {hit.Description} → TALÁL; sebzés: alap {baseDamage} + képesség {abilityBonus} + dobás {randomBonus}{perkBonusText} - páncél {armor}, ×{multiplier} = {damage}.{noteText}");
    }

    private AttackResult EnemyAttack(EnemyDefinition attacker, LiveCharacter defender, BattleContext context)
    {
        if (context.ChallengeAvailable) { context.ChallengeAvailable = false; return AttackResult.Miss("Kihívás: az első támadás automatikusan elhibázza."); }
        if (defender.HasPerk(PerkIds.PriestSanctuary) && _random.NextDouble() < 0.20) return AttackResult.Miss("Szentély: az ellenfél elveszíti a támadását.");
        var hit = HitRoll(attacker.Speed ?? 1, defender.Abilities.Dexterity, 0, false);
        if (!hit.Hit) return AttackResult.Miss($"találat: {hit.Description} → NEM TALÁL.");
        if (defender.HasPerk(PerkIds.ThiefEvasion) && _random.NextDouble() < 0.15)
        {
            context.ShadowStepReady = defender.HasPerk(PerkIds.ThiefShadowStep);
            return AttackResult.Miss("Kitérés: a találat elkerülve." + (context.ShadowStepReady ? " Árnyéklépés aktiválva." : string.Empty));
        }

        var strength = attacker.Strength ?? 1;
        var randomDamage = Roll(new ValueRange(1, Math.Max(2, strength)));
        var armor = RollArmor(defender);
        var shieldEquipped = defender.WeaponSlots.Any(item => item?.WeaponTypeId == DefenseWeaponTypeId);
        var shield = defender.WeaponSlots.FirstOrDefault(item => item?.WeaponTypeId == DefenseWeaponTypeId)?.Damage is { } shieldRange ? Roll(shieldRange) : 0;
        var perkDefense = (defender.HasPerk(PerkIds.BarbarianThickSkin) ? 1 : 0) +
                          (shieldEquipped && defender.HasPerk(PerkIds.KnightShieldWall) ? 2 : 0) +
                          defender.GetMagicItemBonus(MagicItemEffect.Defense);
        var reduction = (defender.HasPerk(PerkIds.FighterUnbreakable) ? 2 : 0) + (defender.HasPerk(PerkIds.KnightInvincible) ? 4 : 0);
        var damage = Math.Max(0, ApplyDefense(strength + randomDamage, armor + shield + perkDefense) - reduction);
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
        return AttackResult.HitFor(damage,
            $"találat: {hit.Description} → TALÁL; sebzés: Erő {strength} + dobás {randomDamage} - páncél {armor} - pajzs {shield}{perkDefenseText}{reductionText}{manaShieldText} = {damage}.");
    }

    private int RollArmor(LiveCharacter defender)
    {
        if (defender.Armor?.Defense is not { } range) return 0;
        var rolled = Roll(range);
        return defender.HasPerk(PerkIds.KnightArmorMaster) ? Math.Max(rolled, (int)Math.Ceiling((range.Minimum + range.Maximum) / 2.0)) : rolled;
    }

    private static string ApplyEnemyDamage(LiveCharacter player, int damage, BattleContext context)
    {
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

    private HitRollResult HitRoll(int attackerSpeed, int defenderSpeed, int attackerBonus, bool forcedHit)
    {
        var roll = Roll(new ValueRange(1, 20));
        if (forcedHit) return new HitRollResult(true, roll, $"Árnyéklépés → automatikus találat ({roll})");
        var total = roll + attackerSpeed + attackerBonus;
        var target = 11 + defenderSpeed;
        return new HitRollResult(total >= target, roll, $"{total} vs {target}" + (attackerBonus > 0 ? $" (+{attackerBonus} bónusz)" : string.Empty));
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
    private sealed record AttackResult(bool Hit, int Damage, string Message)
    {
        public static AttackResult Miss(string message) => new(false, 0, message);
        public static AttackResult HitFor(int damage, string message) => new(true, damage, message);
    }
}

public sealed record BattleResult(bool PlayerWon, int Rounds, IReadOnlyList<string> Events);
public sealed record BattleLogEntry(string Message, BattleLogKind Kind);
public enum BattleLogKind { Information, PlayerAttack, EnemyAttack }
