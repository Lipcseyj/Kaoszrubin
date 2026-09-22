namespace KaoszRubin.World;

/// <summary>A kulcsboss példányonkénti HP-bónusza és ettől független vizuális besorolása.</summary>
public static class BossTierRules
{
    public static int TierForBonus(int hitPointBonusPercent)
    {
        if (hitPointBonusPercent is < 10 or > 50)
            throw new ArgumentOutOfRangeException(nameof(hitPointBonusPercent));
        return Math.Min(5, (hitPointBonusPercent - 10) / 8 + 1);
    }

    public static ConsoleColor Background(int tier) => tier switch
    {
        1 => ConsoleColor.Gray,
        2 => ConsoleColor.Cyan,
        3 => ConsoleColor.Yellow,
        4 => ConsoleColor.Red,
        5 => ConsoleColor.Magenta,
        _ => ConsoleColor.Black
    };
}
