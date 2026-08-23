using MazeGame.Domain;

namespace MazeGame.Domain.Magic;

public enum SpellEffectType
{
    Damage,
    Burning,
    SpeedPenalty,
    SkipAlternate,
    Invisibility,
    DefenseBonus,
    TeleportSelf,
    Dispel,
    Storm,
    ChainDamage,
    PhysicalReduction,
    BleedingImmunity,
    ExtraActions,
    Execute,
    TeleportParty,
    RandomElement
}

public enum SpellResolution
{
    Auto,
    Attack,
    SaveHalf,
    SaveNegates
}

public readonly record struct DiceExpression(int Count, int Sides)
{
    public int Roll(Random random)
    {
        var total = 0;
        for (var index = 0; index < Count; index++) total += random.Next(1, Sides + 1);
        return total;
    }

    public override string ToString() => $"{Count}d{Sides}";

    public static bool TryParse(string value, out DiceExpression dice)
    {
        dice = default;
        var parts = value.Split('d', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 && int.TryParse(parts[0], out var count) && int.TryParse(parts[1], out var sides) &&
               count > 0 && sides > 1 && (dice = new DiceExpression(count, sides)) != default;
    }
}

public sealed record SpellEffectDefinition(string Id, string SpellId, int Order, SpellEffectType Type,
    DiceExpression? Dice, double IntelligenceMultiplier, int LevelMultiplier, int Value, int Duration,
    int ChancePercent, SpellResolution Resolution, string? Parameter, string Description) : IGameDefinition
{
    public string Name => Id;
}

public enum ActiveSpellEffectType
{
    Burning,
    Storm,
    SpeedPenalty,
    SkipAlternate,
    Invisibility,
    DefenseBonus,
    PhysicalReduction,
    BleedingImmunity,
    Frost
}

public sealed record ActiveSpellEffect(string SourceSpellId, ActiveSpellEffectType Type, int Value,
    int RemainingActions, DiceExpression? PeriodicDamage = null, int IntelligenceBonus = 0,
    bool Beneficial = false, int DamageMultiplierPercent = 100);

public sealed record SpellEffectTickResult(int Damage, bool SkipAction, IReadOnlyList<string> Notes);
