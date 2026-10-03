namespace KaoszRubin.World;

/// <summary>A tulajdonságrács által módosítható, az örökölt profiloktól független szerkesztői állapot.</summary>
public static class ForestConfigurationEditing
{
    public static ForestGenerationConfiguration Snapshot(ForestGenerationConfiguration source)
    {
        var copy = new ForestGenerationConfiguration();
        foreach (var property in typeof(ForestGenerationConfiguration).GetProperties())
            property.SetValue(copy, CopyValue(property.GetValue(source)));
        return copy;
    }

    public static ForestGenerationConfigurationPatch? Difference(ForestGenerationConfiguration edited,
        ForestGenerationConfiguration inherited)
    {
        var patch = new ForestGenerationConfigurationPatch();
        var changed = false;
        foreach (var property in typeof(ForestGenerationConfigurationPatch).GetProperties())
        {
            var sourceProperty = typeof(ForestGenerationConfiguration).GetProperty(property.Name)!;
            var value = sourceProperty.GetValue(edited);
            if (SameValue(value, sourceProperty.GetValue(inherited))) continue;
            // A mentett felülírást a tulajdonságrács későbbi szerkesztése sem módosíthatja.
            property.SetValue(patch, CopyValue(value));
            changed = true;
        }
        return changed ? patch : null;
    }

    private static object? CopyValue(object? value) => value switch
    {
        IntRange range => range with { },
        ForestTerrainPalette palette => CopyPalette(palette),
        IReadOnlyList<ForestBuildingStyleDefinition> styles => styles.Select(style => style with
        {
            Wall = style.Wall with { },
            AllowedLayouts = style.AllowedLayouts?.ToHashSet()
        }).ToArray(),
        _ => value
    };

    private static ForestTerrainPalette CopyPalette(ForestTerrainPalette source)
    {
        var copy = new ForestTerrainPalette();
        foreach (var property in typeof(ForestTerrainPalette).GetProperties()
                     .Where(property => property.PropertyType == typeof(MazeTerrainStyle)))
            property.SetValue(copy, ((MazeTerrainStyle)property.GetValue(source)!) with { });
        return copy;
    }

    private static bool SameValue(object? first, object? second) => (first, second) switch
    {
        (ForestTerrainPalette a, ForestTerrainPalette b) => a.All.SequenceEqual(b.All),
        (IReadOnlyList<ForestBuildingStyleDefinition> a, IReadOnlyList<ForestBuildingStyleDefinition> b) =>
            a.Count == b.Count && a.Zip(b).All(pair =>
                pair.First.Id == pair.Second.Id && pair.First.Wall == pair.Second.Wall &&
                pair.First.Weight == pair.Second.Weight &&
                (pair.First.AllowedLayouts is null ? pair.Second.AllowedLayouts is null :
                    pair.Second.AllowedLayouts is not null &&
                    pair.First.AllowedLayouts.SetEquals(pair.Second.AllowedLayouts))),
        _ => Equals(first, second)
    };
}
