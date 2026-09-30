namespace KaoszRubin.World;

public static class ForestLevelGraphOverrideBridge
{
    public static MazeLayoutConfiguration? Apply(MazeLevelConfiguration configuration,
        IForestLevelGraphSource source, Action<string>? warningSink = null, Action<string>? infoSink = null)
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

        if (!source.TryLoad(configuration.Level, out var document, out var sourceDescription, out var warning))
        {
            if (!string.IsNullOrWhiteSpace(warning)) warningSink?.Invoke(warning);
            return layout;
        }
        if (document?.Graph is null) return layout;
        var graph = document.Graph;
        infoSink?.Invoke(
            $"level={configuration.Level}; source={sourceDescription ?? "ismeretlen"}; areas={graph.Areas.Count}; " +
            $"connections={graph.Connections.Count}; templates={graph.Templates?.Count ?? 0}; " +
            $"entrance={graph.EntranceAreaId}; exit={graph.ExitAreaId}");
        return forestLayout with { ExplicitGraph = graph };
    }
}
