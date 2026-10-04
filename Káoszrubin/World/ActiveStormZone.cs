using KaoszRubin.Domain.Magic;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;

namespace KaoszRubin.World;

/// <summary>A pályán maradó vihar. A sérülést mindig az aktuális helyzet alapján kapják a szereplők.</summary>
public sealed record ActiveStormZone(Guid Id, string SpellId, Position Origin,
    IReadOnlyList<Position> Cells, int RemainingRounds, DiceExpression DamageDice,
    int DamageBonus, int SaveDifficulty, SpellResolution Resolution,
    CharacterId? CharacterCasterId = null, WorldEntityId? EnemyCasterId = null,
    int DamageMultiplierPercent = 100, DamageType? DamageType = null)
{
    public bool Contains(Position position) => Cells.Contains(position);

    public ActiveStormZone AfterTick() => this with { RemainingRounds = RemainingRounds - 1 };

    public int RollDamage(Random random, int saveBonus, int magicResistancePercent,
        int typedResistance = 0)
    {
        var damage = Math.Max(0, DamageDice.Roll(random) + DamageBonus);
        var saved = Resolution is SpellResolution.SaveHalf or SpellResolution.SaveNegates &&
                    random.Next(1, 21) + saveBonus >= SaveDifficulty;
        if (saved && Resolution == SpellResolution.SaveNegates) return 0;
        if (saved && Resolution == SpellResolution.SaveHalf) damage /= 2;
        return DamageResistance.ApplySpellPercent(
                   damage * Math.Max(0, DamageMultiplierPercent) / 100, typedResistance) *
               (100 - Math.Clamp(magicResistancePercent, 0, 100)) / 100;
    }
}
