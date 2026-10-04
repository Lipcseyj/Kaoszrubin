using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;

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
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(), new TerrainRuneJsonConverter() }
    };

    public static string Serialize(ExplicitForestAreaGraphConfiguration graph)
    {
        graph.Validate();
        return JsonSerializer.Serialize(new ForestLevelGraphDocument(
            ForestLevelGraphDocument.CurrentSchemaVersion, null, Compact(graph)), Options);
    }

    public static string Serialize(int level, ExplicitForestAreaGraphConfiguration graph)
    {
        if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
        graph.Validate();
        return JsonSerializer.Serialize(new ForestLevelGraphDocument(
            ForestLevelGraphDocument.CurrentSchemaVersion, level, Compact(graph)), Options);
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

    private static ExplicitForestAreaGraphConfiguration Compact(ExplicitForestAreaGraphConfiguration graph)
    {
        var empty = new ForestGenerationConfigurationPatch();
        return graph with
        {
            Areas = graph.Areas.Select(area => area.Overrides == empty
                ? area with { Overrides = null } : area).ToArray()
        };
    }

    private sealed class TerrainRuneJsonConverter : JsonConverter<Rune>
    {
        public override Rune Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var text = reader.GetString()!;
                if (Rune.TryGetRuneAt(text, 0, out var rune) && rune.Utf16SequenceLength == text.Length)
                    return rune;
            }
            else if (reader.TokenType == JsonTokenType.StartObject)
            {
                // Korábbi mentésekben a Rune objektumként, Value mezővel szerepelhetett.
                using var document = JsonDocument.ParseValue(ref reader);
                if (document.RootElement.TryGetProperty("Value", out var value) &&
                    value.TryGetInt32(out var codePoint) && Rune.IsValid(codePoint))
                    return new Rune(codePoint);
            }
            throw new JsonException("A tereprúna pontosan egy Unicode karakter legyen.");
        }

        public override void Write(Utf8JsonWriter writer, Rune value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString());
    }
}
