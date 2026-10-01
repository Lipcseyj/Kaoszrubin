using System.Text.RegularExpressions;

namespace KaoszRubin.ForestMapEditor;

internal static class LegacyMappingWriter
{
    public static void EnsureMappings(string section, IReadOnlyList<string[]> rows)
    {
        var npc = section == "NPC-k";
        var enumName = npc ? "QuestNpcId" : "QuestId";
        var mapName = npc ? "LegacyNpcIdMap" : "LegacyQuestIdMap";
        var fromName = npc ? "ToQuestNpcId" : "ToQuestId";
        var enumPath = EditorSources.PathFor($"Domain/Quests/{enumName}.cs");
        var mapPath = EditorSources.PathFor($"Infrastructure/Quests/{mapName}.cs");
        var enumSource = File.ReadAllText(enumPath);
        var mapSource = File.ReadAllText(mapPath);
        var changed = false;
        foreach (var row in rows)
        {
            var id = row[0].Trim().ToUpperInvariant();
            if (!Regex.IsMatch(id, npc ? @"^NPC\d{3,}$" : @"^NPCQ\d{3,}$"))
                throw new InvalidDataException($"Érvénytelen azonosító: {id}");
            if (mapSource.Contains($"\"{id}\"", StringComparison.OrdinalIgnoreCase)) continue;
            var member = (npc ? "Npc" : "Quest") + Regex.Match(id, @"\d+$").Value;
            if (Regex.IsMatch(enumSource, @"\b" + member + @"\b"))
                throw new InvalidDataException($"Már létező típusos azonosító: {member}");
            var close = enumSource.LastIndexOf('}');
            var previous = close - 1;
            while (previous >= 0 && char.IsWhiteSpace(enumSource[previous])) previous--;
            if (previous >= 0 && enumSource[previous] != ',')
            {
                enumSource = enumSource.Insert(previous + 1, ",");
                close++;
            }
            enumSource = enumSource.Insert(close, $"    {member},\r\n");
            var fromAnchor = mapSource.IndexOf("public static " + enumName + " " + fromName, StringComparison.Ordinal);
            var fromFallback = mapSource.IndexOf("_ =>", fromAnchor, StringComparison.Ordinal);
            if (fromAnchor < 0 || fromFallback < 0) throw new FormatException("A legacy azonosító térkép formátuma ismeretlen.");
            mapSource = mapSource.Insert(fromFallback, $"\"{id}\" => {enumName}.{member},\r\n            ");
            var toAnchor = mapSource.IndexOf("public static string ToExternalId", StringComparison.Ordinal);
            var toFallback = mapSource.IndexOf("_ =>", toAnchor, StringComparison.Ordinal);
            if (toAnchor < 0 || toFallback < 0) throw new FormatException("A visszafelé azonosító térkép formátuma ismeretlen.");
            mapSource = mapSource.Insert(toFallback, $"{enumName}.{member} => \"{id}\",\r\n        ");
            changed = true;
        }
        if (!changed) return;
        File.WriteAllText(enumPath, enumSource);
        File.WriteAllText(mapPath, mapSource);
    }
}
