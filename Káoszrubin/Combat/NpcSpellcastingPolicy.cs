using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Magic;

namespace KaoszRubin.Combat;

public static class NpcSpellcastingPolicy
{
    public const int ManaReservePercent = 20;
    public const int HealThresholdPercent = 35;
    public const int EmergencyHealThresholdPercent = 10;
    public const int EngagedSupportCasterRoutineSpellInterval = 3;

    public static bool NeedsHealing(LiveCharacter character) =>
        ResourcePercent(character.CurrentVitality, character.MaximumVitality) <= HealThresholdPercent;

    public static bool IsEmergency(LiveCharacter character) =>
        ResourcePercent(character.CurrentVitality, character.MaximumVitality) <= EmergencyHealThresholdPercent;

    public static bool HasCurableStatus(LiveCharacter character,
        IEnumerable<SpellEffectDefinition> effects) => effects
        .Where(effect => effect.Type == SpellEffectType.CureStatus)
        .SelectMany(effect => SpellExecutionService.ParseEffectParameters(effect.Parameter))
        .Any(character.HasStatus);

    public static bool NeedsCleansing(LiveCharacter character,
        IEnumerable<SpellEffectDefinition> effects)
    {
        var effectList = effects as IReadOnlyCollection<SpellEffectDefinition> ?? effects.ToArray();
        return HasCurableStatus(character, effectList) ||
               effectList.Any(effect => effect.Type == SpellEffectType.Dispel &&
                   string.Equals(effect.Parameter, "HarmfulOnly", StringComparison.OrdinalIgnoreCase)) &&
               character.ActiveSpellEffects.Any(active => !active.Beneficial) ||
               effectList.Any(effect => effect.Type == SpellEffectType.BreakItemCurse) &&
               character.HasActiveCurse;
    }

    public static bool UsesEngagedSpellCadence(string characterClassId) =>
        characterClassId is CharacterClassIds.Pap or CharacterClassIds.Lovag;

    public static bool CanCastWhileEngaged(int battleCycle, bool urgent) =>
        urgent || Math.Max(1, battleCycle) % EngagedSupportCasterRoutineSpellInterval == 0;

    public static bool CanSpendMana(LiveCharacter caster, int manaCost, bool emergency = false)
    {
        if (manaCost < 0 || caster.CurrentMana < manaCost) return false;
        var reserve = caster.MaximumMana * ManaReservePercent / 100;
        return emergency || caster.CurrentMana - manaCost >= reserve;
    }

    public static int BuffCastChancePercent(int manaCost, int currentMana, int enemyStrength,
        int enemyCount, int beneficiaryCount, bool casterIsEngaged, bool casterIsFrontRow,
        IEnumerable<SpellEffectDefinition> effects)
    {
        if (manaCost < 0 || currentMana < manaCost || enemyCount <= 0 || beneficiaryCount <= 0) return 0;
        var buffEffects = effects.Where(effect => IsBuffEffect(effect.Type)).ToArray();
        if (buffEffects.Length == 0) return 0;

        var effectValue = buffEffects.Sum(effect => effect.Type switch
        {
            SpellEffectType.DefenseBonus => Math.Max(0, effect.Value) * 4,
            SpellEffectType.PhysicalReduction => Math.Max(0, effect.Value) / 5,
            SpellEffectType.HitBonus or SpellEffectType.DamageBonus => Math.Max(0, effect.Value) * 5,
            SpellEffectType.InitiativeBonus => Math.Max(0, effect.Value) * 2,
            SpellEffectType.ProtectionFromEvil => Math.Max(0, effect.Value) / 4,
            SpellEffectType.GuardianAngel => 20,
            SpellEffectType.Sanctuary => Math.Max(0, effect.Value) / 5,
            SpellEffectType.WeaponDamageType => 15,
            SpellEffectType.Invisibility => 12,
            SpellEffectType.BleedingImmunity => 4,
            _ => 0
        });
        var longestDuration = buffEffects.Max(effect => Math.Max(1, effect.Duration));
        effectValue = effectValue * Math.Clamp(50 + longestDuration * 10, 60, 120) / 100;
        effectValue = effectValue * (10 + Math.Min(5, beneficiaryCount - 1) * 6) / 10;

        var threatValue = Math.Clamp(enemyStrength, 0, 40) + Math.Min(12, enemyCount * 2);
        var exposureValue = (casterIsEngaged ? 10 : 0) + (casterIsFrontRow ? 5 : 0);
        var manaBurden = manaCost == 0 ? 0 : (manaCost * 100 + Math.Max(1, currentMana) - 1) /
            Math.Max(1, currentMana);
        var value = effectValue + threatValue + exposureValue - manaBurden;
        return value < 20 ? 0 : Math.Clamp(value, 15, 85);
    }

    public static bool ShouldCastBuff(int chancePercent, int roll) =>
        chancePercent > 0 && Math.Clamp(roll, 0, 99) < Math.Clamp(chancePercent, 0, 100);

    public static bool IsBuffEffect(SpellEffectType type) => type is
        SpellEffectType.Invisibility or SpellEffectType.DefenseBonus or
        SpellEffectType.PhysicalReduction or SpellEffectType.BleedingImmunity or
        SpellEffectType.HitBonus or SpellEffectType.DamageBonus or
        SpellEffectType.InitiativeBonus or SpellEffectType.ProtectionFromEvil or
        SpellEffectType.GuardianAngel or SpellEffectType.Sanctuary or
        SpellEffectType.WeaponDamageType;

    public static bool IsSingleTargetOffensive(SpellDefinition spell,
        IEnumerable<SpellEffectDefinition> effects)
    {
        var classification = NpcSpellTacticalClassifier.Classify(spell, effects);
        return classification.IsOffensive &&
               classification.AttackPattern == NpcSpellAttackPattern.SingleTarget;
    }

    public static bool IsOffensive(SpellDefinition spell, IEnumerable<SpellEffectDefinition> effects) =>
        NpcSpellTacticalClassifier.Classify(spell, effects).IsOffensive;

    public static ActiveSpellEffectType? ActiveTypeFor(SpellEffectType type) => type switch
    {
        SpellEffectType.Invisibility => ActiveSpellEffectType.Invisibility,
        SpellEffectType.DefenseBonus => ActiveSpellEffectType.DefenseBonus,
        SpellEffectType.PhysicalReduction => ActiveSpellEffectType.PhysicalReduction,
        SpellEffectType.BleedingImmunity => ActiveSpellEffectType.BleedingImmunity,
        SpellEffectType.HitBonus => ActiveSpellEffectType.HitBonus,
        SpellEffectType.DamageBonus => ActiveSpellEffectType.DamageBonus,
        SpellEffectType.InitiativeBonus => ActiveSpellEffectType.InitiativeBonus,
        SpellEffectType.ProtectionFromEvil => ActiveSpellEffectType.ProtectionFromEvil,
        SpellEffectType.GuardianAngel => ActiveSpellEffectType.GuardianAngel,
        SpellEffectType.Sanctuary => ActiveSpellEffectType.Sanctuary,
        SpellEffectType.WeaponDamageType => ActiveSpellEffectType.WeaponDamageType,
        _ => null
    };

    private static int ResourcePercent(int current, int maximum) =>
        maximum <= 0 ? 100 : Math.Clamp(current * 100 / maximum, 0, 100);
}
