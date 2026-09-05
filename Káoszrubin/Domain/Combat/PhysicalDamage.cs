namespace KaoszRubin.Domain.Combat;

public enum DamageType { Slashing, Piercing, Bludgeoning }

/// <summary>Signed armor adjustments: positive protects, negative exposes a weakness.</summary>
public sealed record DamageResistance(int Slashing = 0, int Piercing = 0, int Bludgeoning = 0)
{
    public int Against(DamageType type) => type switch
    {
        DamageType.Slashing => Slashing,
        DamageType.Piercing => Piercing,
        _ => Bludgeoning
    };

    public override string ToString() => $"vágás {Slashing:+#;-#;0}, szúrás {Piercing:+#;-#;0}, zúzás {Bludgeoning:+#;-#;0}";
}

public static class PhysicalDamage
{
    public static string Name(this DamageType type) => type switch
    {
        DamageType.Slashing => "vágás",
        DamageType.Piercing => "szúrás",
        _ => "zúzás"
    };
}
