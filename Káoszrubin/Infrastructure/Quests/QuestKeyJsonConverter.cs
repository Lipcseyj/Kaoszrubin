using System.Text.Json;
using System.Text.Json.Serialization;
using KaoszRubin.Domain.Quests;

namespace KaoszRubin.Infrastructure.Quests;

/// <summary>A napló wire-kulcsában stabil külső quest-ID szerepel, nem az enum sorszáma.</summary>
public sealed class QuestKeyJsonConverter : JsonConverter<QuestKey>
{
    public override QuestKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var json = JsonDocument.ParseValue(ref reader);
        var value = json.RootElement;
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty("QuestId", out var id) ||
            id.ValueKind != JsonValueKind.String || !value.TryGetProperty("GiverInstanceId", out var giver) ||
            giver.ValueKind != JsonValueKind.Number || !giver.TryGetInt32(out var instance) || instance < 0)
            throw new JsonException("Érvénytelen questkulcs.");
        try
        {
            return new(LegacyQuestIdMap.ToQuestId(id.GetString()!),
                instance == 0 ? default : new QuestNpcInstanceId(instance));
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentException)
        {
            throw new JsonException("Ismeretlen questazonosító.", exception);
        }
    }

    public override void Write(Utf8JsonWriter writer, QuestKey value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("QuestId", LegacyQuestIdMap.ToExternalId(value.QuestId));
        writer.WriteNumber("GiverInstanceId", value.GiverInstanceId.Value);
        writer.WriteEndObject();
    }
}
