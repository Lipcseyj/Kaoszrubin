using MazeGame.Domain.Characters;
using MazeGame.Domain.Combat;

namespace MazeGame.Combat;

/// <summary>A közelharc teljes, automatikusan lefutó körökre osztott szabályrendszere.</summary>
public sealed class BattleSystem(Random random)
{
    private const string StrengthWeaponTypeId = "WT001";
    private const string DexterityWeaponTypeId = "WT002";
    private const string DefenseWeaponTypeId = "WT003";
    private readonly Random _random = random;

    public BattleResult Resolve(LiveCharacter player, Enemy enemy)
    {
        var definition = ((Skeleton)enemy).Definition;
        var playerStarts = Initiative(player.Abilities.Dexterity) >= Initiative(definition.Speed ?? 1);
        var playerAttacks = playerStarts;
        var round = 0;
        var events = new List<string>();

        while (player.CurrentVitality > 0 && definition.HitPoints is > 0)
        {
            round++;
            if (playerAttacks)
            {
                var attack = PlayerAttack(player, definition);
                if (attack.Hit) definition = definition with { HitPoints = Math.Max(0, definition.HitPoints!.Value - attack.Damage) };
                events.Add($"{round}. kör: {player.Name} {attack.Message}");
            }
            else
            {
                var attack = EnemyAttack(definition, player);
                if (attack.Hit) player.ReceiveDamage(attack.Damage);
                events.Add($"{round}. kör: {enemy.Name} {attack.Message}");
            }

            playerAttacks = !playerAttacks;
        }

        return new BattleResult(player.CurrentVitality > 0, round, events);
    }

    private int Initiative(int speed) => speed + SignedD2();

    private AttackResult PlayerAttack(LiveCharacter player, EnemyDefinition defender)
    {
        if (!Hits(player.Abilities.Dexterity, defender.Speed ?? 1, out var roll))
            return AttackResult.Miss($"nem talál (dobás: {roll}).");

        var weapon = player.WeaponSlots.FirstOrDefault(item => item?.WeaponTypeId != DefenseWeaponTypeId);
        var baseDamage = weapon?.Damage is { } range ? Roll(range) : Roll(new ValueRange(1, 2));
        var abilityBonus = weapon?.WeaponTypeId switch
        {
            DexterityWeaponTypeId => AbilityDamageBonus(player.Abilities.Dexterity),
            _ => AbilityDamageBonus(player.Abilities.Strength)
        };
        var damage = ApplyDefense(baseDamage + abilityBonus + Roll(new ValueRange(0, 2)), defender.Armor ?? 0);
        return AttackResult.HitFor(damage, $"talál: {damage} sebzés.");
    }

    private AttackResult EnemyAttack(EnemyDefinition attacker, LiveCharacter defender)
    {
        if (!Hits(attacker.Speed ?? 1, defender.Abilities.Dexterity, out var roll))
            return AttackResult.Miss($"nem talál (dobás: {roll}).");

        var strength = attacker.Strength ?? 1;
        var rawDamage = strength + Roll(new ValueRange(1, Math.Max(2, strength)));
        var damage = ApplyDefense(rawDamage, PlayerDefense(defender));
        return AttackResult.HitFor(damage, $"talál: {damage} sebzés.");
    }

    private bool Hits(int attackerSpeed, int defenderSpeed, out int roll)
    {
        roll = Roll(new ValueRange(1, 20));
        return roll + attackerSpeed >= 11 + defenderSpeed;
    }

    private int PlayerDefense(LiveCharacter character)
    {
        var armor = character.Armor?.Defense is { } armorRange ? Roll(armorRange) : 0;
        var shield = character.WeaponSlots.FirstOrDefault(item => item?.WeaponTypeId == DefenseWeaponTypeId)?.Damage is { } shieldRange
            ? Roll(shieldRange)
            : 0;
        return armor + shield;
    }

    private static int AbilityDamageBonus(int ability) => Math.Max(0, (ability - 1) / 2);
    private int Roll(ValueRange range) => _random.Next(range.Minimum, range.Maximum + 1);
    private int SignedD2() => _random.Next(2) == 0 ? -Roll(new ValueRange(1, 2)) : Roll(new ValueRange(1, 2));
    private static int ApplyDefense(int rawDamage, int defense) => Math.Max(1, rawDamage - defense);

    private sealed record AttackResult(bool Hit, int Damage, string Message)
    {
        public static AttackResult Miss(string message) => new(false, 0, message);
        public static AttackResult HitFor(int damage, string message) => new(true, damage, message);
    }
}

public sealed record BattleResult(bool PlayerWon, int Rounds, IReadOnlyList<string> Events);
