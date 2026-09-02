using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Magic;

namespace KaoszRubin.Combat;

public static class NpcSpellcastingPolicy
{
    public const int ManaReservePercent = 20;
    public const int HealThresholdPercent = 35;
    public const int EmergencyHealThresholdPercent = 10;

    public static bool NeedsHealing(LiveCharacter character) =>
        ResourcePercent(character.CurrentVitality, character.MaximumVitality) <= HealThresholdPercent;

    public static bool IsEmergency(LiveCharacter character) =>
        ResourcePercent(character.CurrentVitality, character.MaximumVitality) <= EmergencyHealThresholdPercent;

    public static bool CanSpendMana(LiveCharacter caster, int manaCost, bool emergency = false)
    {
        if (manaCost < 0 || caster.CurrentMana < manaCost) return false;
        var reserve = caster.MaximumMana * ManaReservePercent / 100;
        return emergency || caster.CurrentMana - manaCost >= reserve;
    }

    public static bool IsBuffEffect(SpellEffectType type) => type is
        SpellEffectType.Invisibility or SpellEffectType.DefenseBonus or
        SpellEffectType.PhysicalReduction or SpellEffectType.BleedingImmunity or
        SpellEffectType.HitBonus or SpellEffectType.DamageBonus or
        SpellEffectType.InitiativeBonus or SpellEffectType.ProtectionFromEvil or
        SpellEffectType.GuardianAngel or SpellEffectType.Sanctuary;

    public static bool IsSingleTargetOffensive(SpellDefinition spell,
        IEnumerable<SpellEffectDefinition> effects)
    {
        var spellEffects = effects as IReadOnlyCollection<SpellEffectDefinition> ?? effects.ToArray();
        return spell.TargetType == SpellTargetType.Enemy && spell.AreaRadius == 0 &&
               spellEffects.Any(effect => effect.Type == SpellEffectType.Damage) &&
               spellEffects.All(effect => effect.Type != SpellEffectType.ChainDamage);
    }

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
        _ => null
    };

    private static int ResourcePercent(int current, int maximum) =>
        maximum <= 0 ? 100 : Math.Clamp(current * 100 / maximum, 0, 100);
}
