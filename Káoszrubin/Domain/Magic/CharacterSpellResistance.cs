using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;

namespace KaoszRubin.Domain.Magic;

/// <summary>Felszerelt tárgyak és aktív varázslatok százalékos védelme a bejövő varázssebzés ellen.</summary>
public static class CharacterSpellResistance
{
    public static int Percent(LiveCharacter character, DamageType? type = null)
    {
        var (item, spell) = type switch
        {
            DamageType.Fire => (MagicItemEffect.FireResistance, ActiveSpellEffectType.FireResistance),
            DamageType.Acid => (MagicItemEffect.AcidResistance, ActiveSpellEffectType.AcidResistance),
            DamageType.Necrotic => (MagicItemEffect.NecroticResistance, ActiveSpellEffectType.NecroticResistance),
            DamageType.Frost => (MagicItemEffect.FrostResistance, ActiveSpellEffectType.FrostResistance),
            DamageType.Lightning => (MagicItemEffect.LightningResistance, ActiveSpellEffectType.LightningResistance),
            _ => (MagicItemEffect.None, default)
        };
        return item == MagicItemEffect.None ? 0 :
            Math.Clamp(character.GetMagicItemBonus(item) + character.SpellEffectValue(spell), 0, 100);
    }

    public static int MagicPercent(LiveCharacter character) => Math.Clamp(
        character.GetMagicItemBonus(MagicItemEffect.MagicResistance) +
        character.SpellEffectValue(ActiveSpellEffectType.MagicResistance), 0, 100);

    public static int Apply(LiveCharacter character, int damage, DamageType? type,
        ICollection<string>? modifiers = null)
    {
        damage = Math.Max(0, damage);
        if (damage == 0) return 0;
        var typed = Percent(character, type);
        if (typed > 0)
        {
            modifiers?.Add($"🛡️ {type!.Value.Name()} ellenállás {typed}%");
            damage = damage * (100 - typed) / 100;
        }
        var magic = MagicPercent(character);
        if (magic > 0)
        {
            modifiers?.Add($"🔮 varázsvédelem {magic}%");
            damage = damage * (100 - magic) / 100;
        }
        return damage;
    }
}
