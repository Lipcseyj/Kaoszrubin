namespace KaoszRubin.Domain.Characters;

public static class RecruitmentRules
{
    public const int LowerLevelRecruitmentStartsAfterLevel = 5;

    public static bool UsesLowerLevelCandidates(int completedLevel) =>
        completedLevel >= LowerLevelRecruitmentStartsAfterLevel;

    public static int LowerRecruitLevel(int leaderLevel, int reductionPercent)
    {
        leaderLevel = Math.Max(1, leaderLevel);
        reductionPercent = Math.Clamp(reductionPercent, 20, 50);
        var levelReduction = Math.Max(1,
            (int)Math.Ceiling(leaderLevel * reductionPercent / 100.0));
        return Math.Max(1, leaderLevel - levelReduction);
    }

    public static int Price(int recruitLevel, int leaderLevel, int completedLevel, int pricePercent)
    {
        if (!UsesLowerLevelCandidates(completedLevel) && recruitLevel < leaderLevel) return 0;
        return Math.Max(1, Math.Max(1, recruitLevel) * 200 * Math.Clamp(pricePercent, 50, 150) / 100);
    }
}
