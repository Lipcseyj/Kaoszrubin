namespace KaoszRubin.World;

public static class ForestLevelGraphOverrideBridge
{
    public static MazeLayoutConfiguration? Apply(MazeLevelConfiguration configuration,
        IForestLevelGraphSource source, Action<string>? warningSink = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(source);

        var layout = configuration.Layout;
        if (!configuration.ForestGraphJsonOverrideEnabled) return layout;
        if (layout is not ForestMazeLayoutConfiguration forestLayout)
        {
            warningSink?.Invoke($"A(z) {configuration.Level}. pálya JSON erdőgráf-felülírásra engedélyezett, de nem erdei layoutot használ.");
            return layout;
        }

        if (!source.TryLoad(configuration.Level, out var document, out var warning))
        {
            if (!string.IsNullOrWhiteSpace(warning)) warningSink?.Invoke(warning);
            return layout;
        }
        if (document?.Graph is null) return layout;
        return forestLayout with { ExplicitGraph = document.Graph };
    }
}
