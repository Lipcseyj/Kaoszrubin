namespace KaoszRubin.Domain.Combat;

public enum DamageType { Slashing, Piercing, Bludgeoning, Fire, Acid, Necrotic, Chaos }

/// <summary>Signed armor adjustments: positive protects, negative exposes a weakness.</summary>
public sealed record DamageResistance(int Slashing = 0, int Piercing = 0, int Bludgeoning = 0,
    int Fire = 0, int Acid = 0, int Necrotic = 0, int Chaos = 0)
{
    // Spells use ten-percent steps; weapon hits use the same rating as a flat armor adjustment.
    public static int ApplySpellPercent(int damage, int rating) =>
        (int)Math.Min(int.MaxValue, (long)Math.Max(0, damage) *
            (10 - Math.Clamp(rating, -10, 10)) / 10);

    public int Against(DamageType type) => type switch
    {
        DamageType.Slashing => Slashing,
        DamageType.Piercing => Piercing,
        DamageType.Bludgeoning => Bludgeoning,
        DamageType.Fire => Fire,
        DamageType.Acid => Acid,
        DamageType.Necrotic => Necrotic,
        DamageType.Chaos => Chaos,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    public override string ToString()
    {
        var values = new[]
        {
            (DamageType.Slashing, Slashing), (DamageType.Piercing, Piercing),
            (DamageType.Bludgeoning, Bludgeoning), (DamageType.Fire, Fire),
            (DamageType.Acid, Acid), (DamageType.Necrotic, Necrotic), (DamageType.Chaos, Chaos)
        };
        var configured = values.Where(value => value.Item2 != 0)
            .Select(value => $"{value.Item1.Name()} {value.Item2:+#;-#;0}").ToArray();
        return configured.Length == 0 ? "nincs" : string.Join(", ", configured);
    }

    public string ToSpellPercentString()
    {
        var values = new[]
        {
            (DamageType.Slashing, Slashing), (DamageType.Piercing, Piercing),
            (DamageType.Bludgeoning, Bludgeoning), (DamageType.Fire, Fire),
            (DamageType.Acid, Acid), (DamageType.Necrotic, Necrotic), (DamageType.Chaos, Chaos)
        };
        var configured = values.Where(value => value.Item2 != 0)
            .Select(value => $"{value.Item1.Name()} {value.Item2 * 10:+#;-#;0}%").ToArray();
        return configured.Length == 0 ? "nincs" : string.Join(", ", configured);
    }
}

public static class PhysicalDamage
{
    public static string Name(this DamageType type) => type switch
    {
        DamageType.Slashing => "vágás",
        DamageType.Piercing => "szúrás",
        DamageType.Bludgeoning => "zúzás",
        DamageType.Fire => "tűz",
        DamageType.Acid => "sav",
        DamageType.Necrotic => "nekrotikus",
        DamageType.Chaos => "káosz",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    public static bool IsPhysical(this DamageType type) => type is
        DamageType.Slashing or DamageType.Piercing or DamageType.Bludgeoning;
}
