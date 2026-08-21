using MazeGame.Domain.Characters;
using MazeGame.Domain.Combat;

namespace MazeGame.Combat;

/// <summary>A közelharc teljes, felhasználó által léptetett körökre osztott szabályrendszere.</summary>
public sealed class BattleSystem(Random random)
{
    private const string DexterityWeaponTypeId = "WT002";
    private const string DefenseWeaponTypeId = "WT003";
    private readonly Random _random = random;

    public BattleResult Resolve(LiveCharacter player, Enemy enemy, Action<BattleLogEntry> onRound)
    {
        var defender = ((Skeleton)enemy).Definition;
        var playerInitiative = RollInitiative(player.Abilities.Dexterity);
        var enemyInitiative = RollInitiative(defender.Speed ?? 1);
        var playerAttacks = playerInitiative.Total >= enemyInitiative.Total;
        var events = new List<string>
        {
            $"Kezdeményezés: {player.Name} Ügy {player.Abilities.Dexterity} {playerInitiative.ModifierText} = {playerInitiative.Total}; " +
            $"{enemy.Name} Gy {defender.Speed ?? 1} {enemyInitiative.ModifierText} = {enemyInitiative.Total}. " +
            $"{(playerAttacks ? player.Name : enemy.Name)} kezd."
        };
        onRound(new BattleLogEntry(events[0], BattleLogKind.Information));
        var round = 0;

        while (player.CurrentVitality > 0 && defender.HitPoints is > 0)
        {
            round++;
            string message;
            BattleLogKind logKind;
            if (playerAttacks)
            {
                var attack = PlayerAttack(player, defender);
                if (attack.Hit) defender = defender with { HitPoints = Math.Max(0, defender.HitPoints!.Value - attack.Damage) };
                message = $"{round}. kör — {player.Name} támadja {enemy.Name}-t. {attack.Message} {enemy.Name} HP: {defender.HitPoints}/" + ((Skeleton)enemy).Definition.HitPoints + ".";
                logKind = BattleLogKind.PlayerAttack;
            }
            else
            {
                var attack = EnemyAttack(defender, player);
                if (attack.Hit) player.ReceiveDamage(attack.Damage);
                message = $"{round}. kör — {enemy.Name} támadja {player.Name}-t. {attack.Message} {player.Name} HP: {player.CurrentVitality}/{player.MaximumVitality}.";
                logKind = BattleLogKind.EnemyAttack;
            }

            events.Add(message);
            onRound(new BattleLogEntry(message, logKind));
            playerAttacks = !playerAttacks;
        }

        return new BattleResult(player.CurrentVitality > 0, round, events);
    }

    private InitiativeRoll RollInitiative(int speed)
    {
        var modifier = _random.Next(2) == 0 ? -Roll(new ValueRange(1, 2)) : Roll(new ValueRange(1, 2));
        return new InitiativeRoll(speed + modifier, $"{(modifier >= 0 ? "+" : string.Empty)}1d2({modifier})");
    }

    private AttackResult PlayerAttack(LiveCharacter player, EnemyDefinition defender)
    {
        var hit = HitRoll(player.Abilities.Dexterity, defender.Speed ?? 1, "Ügy", "Gy");
        if (!hit.Hit) return AttackResult.Miss($"találat: {hit.Description} → NEM TALÁL.");

        var weapon = player.WeaponSlots.FirstOrDefault(item => item?.WeaponTypeId != DefenseWeaponTypeId);
        var baseDamage = weapon?.Damage is { } range ? Roll(range) : Roll(new ValueRange(1, 2));
        var usesDexterity = weapon is not null && string.Equals(weapon.WeaponTypeId, DexterityWeaponTypeId, StringComparison.OrdinalIgnoreCase);
        var abilityName = usesDexterity ? "Ügy" : "Erő";
        var ability = usesDexterity ? player.Abilities.Dexterity : player.Abilities.Strength;
        var abilityBonus = AbilityDamageBonus(ability);
        var randomBonus = Roll(new ValueRange(0, 2));
        var rawDamage = baseDamage + abilityBonus + randomBonus;
        var armor = defender.Armor ?? 0;
        var damage = ApplyDefense(rawDamage, armor);
        return AttackResult.HitFor(damage,
            $"találat: {hit.Description} → TALÁL; sebzés: alap {baseDamage} + {abilityName}-bónusz {abilityBonus} + dobás {randomBonus} - páncél {armor} = {damage}.");
    }

    private AttackResult EnemyAttack(EnemyDefinition attacker, LiveCharacter defender)
    {
        var hit = HitRoll(attacker.Speed ?? 1, defender.Abilities.Dexterity, "Gy", "Ügy");
        if (!hit.Hit) return AttackResult.Miss($"találat: {hit.Description} → NEM TALÁL.");

        var strength = attacker.Strength ?? 1;
        var randomDamage = Roll(new ValueRange(1, Math.Max(2, strength)));
        var armor = defender.Armor?.Defense is { } armorRange ? Roll(armorRange) : 0;
        var shield = defender.WeaponSlots.FirstOrDefault(item => item?.WeaponTypeId == DefenseWeaponTypeId)?.Damage is { } shieldRange ? Roll(shieldRange) : 0;
        var rawDamage = strength + randomDamage;
        var damage = ApplyDefense(rawDamage, armor + shield);
        return AttackResult.HitFor(damage,
            $"találat: {hit.Description} → TALÁL; sebzés: Erő {strength} + dobás {randomDamage} - páncél {armor} - pajzs {shield} = {damage}.");
    }

    private HitRollResult HitRoll(int attackerSpeed, int defenderSpeed, string attackerLabel, string defenderLabel)
    {
        var roll = Roll(new ValueRange(1, 20));
        var attackTotal = roll + attackerSpeed;
        var defenseTarget = 11 + defenderSpeed;
        return new HitRollResult(attackTotal >= defenseTarget,
            $"{attackTotal} vs {defenseTarget}");
    }

    private static int AbilityDamageBonus(int ability) => Math.Max(0, (ability - 1) / 2);
    private int Roll(ValueRange range) => _random.Next(range.Minimum, range.Maximum + 1);
    private static int ApplyDefense(int rawDamage, int defense) => Math.Max(1, rawDamage - defense);

    private sealed record InitiativeRoll(int Total, string ModifierText);
    private sealed record HitRollResult(bool Hit, string Description);
    private sealed record AttackResult(bool Hit, int Damage, string Message)
    {
        public static AttackResult Miss(string message) => new(false, 0, message);
        public static AttackResult HitFor(int damage, string message) => new(true, damage, message);
    }
}

public sealed record BattleResult(bool PlayerWon, int Rounds, IReadOnlyList<string> Events);
public sealed record BattleLogEntry(string Message, BattleLogKind Kind);
public enum BattleLogKind { Information, PlayerAttack, EnemyAttack }
