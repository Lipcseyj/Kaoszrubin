using System.Text.Json;
using System.Text.Json.Serialization;

namespace KaoszRubin.World;

/// <summary>A gráfszerkesztő és a játék közös, verziózott erdőkonfigurációs dokumentuma.</summary>
public sealed record ForestLevelGraphDocument(
    int SchemaVersion,
    int? Level,
    ExplicitForestAreaGraphConfiguration Graph)
{
    public const int CurrentSchemaVersion = 1;
}

public static class ForestConfigurationJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize(ExplicitForestAreaGraphConfiguration graph)
    {
        graph.Validate();
        return JsonSerializer.Serialize(new ForestLevelGraphDocument(
            ForestLevelGraphDocument.CurrentSchemaVersion, null, graph), Options);
    }

    public static string Serialize(int level, ExplicitForestAreaGraphConfiguration graph)
    {
        if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
        graph.Validate();
        return JsonSerializer.Serialize(new ForestLevelGraphDocument(
            ForestLevelGraphDocument.CurrentSchemaVersion, level, graph), Options);
    }

    public static ForestLevelGraphDocument DeserializeDocument(string json)
    {
        var document = JsonSerializer.Deserialize<ForestLevelGraphDocument>(json, Options) ??
                       throw new InvalidDataException("Az erdőgráf dokumentuma üres.");
        if (document.SchemaVersion != ForestLevelGraphDocument.CurrentSchemaVersion)
            throw new InvalidDataException($"Nem támogatott erdőgráf-verzió: {document.SchemaVersion}.");
        document.Graph.Validate();
        return document;
    }

    public static ExplicitForestAreaGraphConfiguration Deserialize(string json)
    {
        return DeserializeDocument(json).Graph;
    }
}
