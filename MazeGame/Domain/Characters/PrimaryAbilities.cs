namespace MazeGame.Domain.Characters;

/// <summary>A karaktergenerálás négy elsődleges, dobható képességértéke.</summary>
public readonly record struct PrimaryAbilities(int Strength, int Dexterity, int Health, int Intelligence)
{
    public static PrimaryAbilities Zero => new();

    public static PrimaryAbilities operator +(PrimaryAbilities left, PrimaryAbilities right) => new(
        left.Strength + right.Strength,
        left.Dexterity + right.Dexterity,
        left.Health + right.Health,
        left.Intelligence + right.Intelligence);

    public bool MeetsMinimum(PrimaryAbilities minimum) =>
        Strength >= minimum.Strength && Dexterity >= minimum.Dexterity &&
        Health >= minimum.Health && Intelligence >= minimum.Intelligence;

    public PrimaryAbilities Clamp(int minimum, int maximum) => new(
        Math.Clamp(Strength, minimum, maximum),
        Math.Clamp(Dexterity, minimum, maximum),
        Math.Clamp(Health, minimum, maximum),
        Math.Clamp(Intelligence, minimum, maximum));
}
