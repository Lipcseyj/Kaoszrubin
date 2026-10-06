using System.Text.RegularExpressions;

namespace KaoszRubin.MapEditor;

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
        var npcNames = npc ? null : new CsvSectionEditor(EditorSources.PathFor("Data/game-data.csv"))
            .Rows("NPC-k").ToDictionary(row => row[0], row => row[1], StringComparer.OrdinalIgnoreCase);
        var changed = false;
        foreach (var row in rows)
        {
            var id = row[0].Trim().ToUpperInvariant();
            if (!Regex.IsMatch(id, npc ? @"^NPC\d{3,}$" : @"^NPCQ\d{3,}$"))
                throw new InvalidDataException($"Érvénytelen azonosító: {id}");
            if (mapSource.Contains($"\"{id}\"", StringComparison.OrdinalIgnoreCase)) continue;
            var member = npc
                ? "Npc" + Regex.Match(id, @"\d+$").Value
                : ToQuestIdMember(row);
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
            var comment = npc ? "" : $"    // {QuestNpcDescription(row, npcNames!)}\r\n";
            enumSource = enumSource.Insert(close, $"{comment}    {member},\r\n");
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

    private static string ToQuestIdMember(string[] row)
    {
        const int titleColumn = 6;
        if (row.Length <= titleColumn || string.IsNullOrWhiteSpace(row[titleColumn]))
            throw new InvalidDataException($"A(z) {row[0]} küldetés címe hiányzik.");

        var member = Regex.Replace(row[titleColumn].Trim(), @"[^\p{L}\p{M}\p{Nd}_]+", "_").Trim('_');
        if (member.Length == 0)
            throw new InvalidDataException($"A(z) {row[0]} küldetés címéből nem képezhető azonosító.");
        if (!Regex.IsMatch(member, @"^[\p{L}_]")) member = "_" + member;
        return member;
    }

    private static string QuestNpcDescription(string[] row, IReadOnlyDictionary<string, string> npcNames)
    {
        const int npcIdColumn = 1;
        if (row.Length <= npcIdColumn || string.IsNullOrWhiteSpace(row[npcIdColumn]))
            throw new InvalidDataException($"A(z) {row[0]} küldetés NpcId mezője hiányzik.");

        var npcId = row[npcIdColumn].Trim().ToUpperInvariant();
        if (!npcNames.TryGetValue(npcId, out var npcName))
            throw new InvalidDataException($"A(z) {row[0]} küldetés ismeretlen NPC-re hivatkozik: {npcId}.");
        return $"{npcId} - {npcName.Replace('\r', ' ').Replace('\n', ' ').Trim()}";
    }
}
