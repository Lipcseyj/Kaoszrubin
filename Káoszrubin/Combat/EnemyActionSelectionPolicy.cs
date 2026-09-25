namespace KaoszRubin.Combat;

/// <summary>
/// A taktikai AI legjobb, egymashoz kozeli jeloltjei kozul valaszt. A kis szoras
/// valtozatossa teszi a dontest, de egy egyertelmuen gyenge akciot nem emelhet az elre.
/// </summary>
public static class EnemyActionSelectionPolicy
{
    public const int VariationPercent = 10;

    public static T? Select<T>(IReadOnlyList<T> candidates, Func<T, double> score, Random random)
        where T : class
    {
        if (candidates.Count == 0) return null;
        var best = candidates.Max(score);
        var tolerance = Math.Max(10d, Math.Abs(best) * VariationPercent / 100d);
        var shortlist = candidates.Where(candidate => score(candidate) >= best - tolerance).ToArray();
        return shortlist
            .Select(candidate => new
            {
                Candidate = candidate,
                Adjusted = score(candidate) * (100 + random.Next(-VariationPercent, VariationPercent + 1)) / 100d
            })
            .OrderByDescending(item => item.Adjusted)
            .First().Candidate;
    }
}
