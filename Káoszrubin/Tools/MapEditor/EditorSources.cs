using System.Text;
using System.Text.RegularExpressions;

namespace KaoszRubin.MapEditor;

internal static class EditorSources
{
    public static string Root
    {
        get
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "World", "MazeLevelConfiguration.cs")))
                    return directory.FullName;
            throw new DirectoryNotFoundException("A játék forráskönyvtára nem található.");
        }
    }

    public static string PathFor(string relative) => Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

    public static string LevelBlock(string source, int level)
    {
        var match = Regex.Match(source, @"(?m)^\s*\[" + level + @"\]\s*=\s*new\s*\(\s*\)\s*\{");
        if (!match.Success) throw new InvalidOperationException($"A(z) {level}. pálya nincs kézzel definiálva.");
        var opening = source.IndexOf('{', match.Index);
        return source.Substring(match.Index, MatchingEnd(source, opening) - match.Index + 1);
    }

    public static string ReadLevel(int level) => LevelBlock(File.ReadAllText(PathFor("World/MazeLevelConfiguration.cs")), level);

    public static void SaveLevel(int level, IReadOnlyDictionary<string, string> properties)
    {
        var path = PathFor("World/MazeLevelConfiguration.cs");
        var source = File.ReadAllText(path);
        var updatedSource = UpdateLevelSource(source, level, properties);
        if (updatedSource != source) File.WriteAllText(path, updatedSource);
    }

    public static string UpdateLevelSource(string source, int level, IReadOnlyDictionary<string, string> properties)
    {
        var original = LevelBlock(source, level);
        var updated = original;
        foreach (var (name, value) in properties)
            if (Property(updated, name) != value.Trim())
                updated = ReplaceProperty(updated, name, value);
        return updated == original ? source : source.Replace(original, updated);
    }

    public static string? Property(string block, string name)
    {
        var match = Regex.Match(block, @"(?m)^\s*" + Regex.Escape(name) + @"\s*=");
        if (!match.Success) return null;
        var start = match.Index + match.Length;
        var end = TopLevelComma(block, start);
        return block.Substring(start, end - start).Trim();
    }

    private static string ReplaceProperty(string block, string name, string value)
    {
        var match = Regex.Match(block, @"(?m)^(?<indent>\s*)" + Regex.Escape(name) + @"\s*=");
        if (!match.Success)
        {
            var indent = "                ";
            var close = block.LastIndexOf('}');
            return block.Insert(close, indent + name + " = " + value + "," + Environment.NewLine);
        }
        var start = match.Index + match.Length;
        var end = TopLevelComma(block, start);
        return block[..start] + " " + value + block[end..];
    }

    public static IReadOnlyList<string> CollectionItems(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) return [];
        var open = expression.IndexOf('[');
        if (open < 0) return [];
        var close = MatchingEnd(expression, open);
        var inner = expression[(open + 1)..close];
        var result = new List<string>();
        var start = 0;
        while (start < inner.Length)
        {
            var comma = TopLevelComma(inner, start);
            var item = inner[start..comma].Trim();
            if (item.Length > 0) result.Add(item);
            start = comma + 1;
        }
        return result;
    }

    public static IReadOnlyList<KeyValuePair<string, string>> DictionaryEntries(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) return [];
        var open = expression.IndexOf('{');
        if (open < 0) return [];
        var close = MatchingEnd(expression, open);
        var inner = expression[(open + 1)..close];
        var entries = new List<KeyValuePair<string, string>>();
        var start = 0;
        while (start < inner.Length)
        {
            var comma = TopLevelComma(inner, start);
            var item = inner[start..comma].Trim();
            if (item.Length > 0)
            {
                var match = Regex.Match(item, "^\\[\\s*\\\"(?<key>(?:\\\\.|[^\\\"])*)\\\"\\s*\\]\\s*=\\s*(?<value>.+)$",
                    RegexOptions.Singleline);
                if (!match.Success) throw new FormatException($"Nem értelmezhető szótárbejegyzés: {item}");
                entries.Add(new(Regex.Unescape(match.Groups["key"].Value), match.Groups["value"].Value.Trim()));
            }
            start = comma + 1;
        }
        return entries;
    }

    public static int MatchingEnd(string source, int opening)
    {
        var open = source[opening];
        var close = open switch { '{' => '}', '[' => ']', '(' => ')', _ => throw new ArgumentException("Ismeretlen nyitójel.") };
        var depth = 0;
        var quoted = false;
        var character = false;
        for (var i = opening; i < source.Length; i++)
        {
            var c = source[i];
            if ((quoted || character) && c == '\\') { i++; continue; }
            if (!character && c == '"') { quoted = !quoted; continue; }
            if (!quoted && c == '\'') { character = !character; continue; }
            if (quoted || character) continue;
            if (c == open) depth++;
            if (c == close && --depth == 0) return i;
        }
        throw new FormatException("Lezáratlan C# kifejezés.");
    }

    private static int TopLevelComma(string source, int start)
    {
        var round = 0; var square = 0; var curly = 0; var angle = 0; var quoted = false; var character = false;
        for (var i = start; i < source.Length; i++)
        {
            var c = source[i];
            if ((quoted || character) && c == '\\') { i++; continue; }
            if (!character && c == '"') { quoted = !quoted; continue; }
            if (!quoted && c == '\'') { character = !character; continue; }
            if (quoted || character) continue;
            switch (c)
            {
                case '(': round++; break;
                case ')': round--; break;
                case '[': square++; break;
                case ']': square--; break;
                case '{': curly++; break;
                case '}': curly--; break;
                case '<': angle++; break;
                case '>' when angle > 0: angle--; break;
                case ',' when round == 0 && square == 0 && curly == 0 && angle == 0: return i;
            }
        }
        return source.Length;
    }
}

internal sealed class CsvSectionEditor(string path)
{
    private readonly string[] _lines = File.ReadAllLines(path);

    public string[] Headers(string section)
    {
        var (first, _) = Bounds(section);
        return Parse(_lines[first]).ToArray();
    }

    public List<string[]> Rows(string section)
    {
        var (first, end) = Bounds(section);
        return _lines[(first + 1)..end].Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => Parse(line).ToArray()).ToList();
    }

    public void Save(string section, IEnumerable<string[]> rows)
    {
        var (first, end) = Bounds(section);
        var incoming = rows.ToDictionary(row => row[0], StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var width = Parse(_lines[first]).Count;
        var edited = new List<string>();
        foreach (var line in _lines[(first + 1)..end])
        {
            if (string.IsNullOrWhiteSpace(line)) { edited.Add(line); continue; }
            var original = Parse(line);
            if (original.Count == 0 || !incoming.TryGetValue(original[0], out var row)) continue;
            seen.Add(original[0]);
            edited.Add(Enumerable.Range(0, width).All(index =>
                (index < original.Count ? original[index] : "") ==
                (index < row.Length ? row[index] : ""))
                ? line : string.Join(';', row.Select(Escape)));
        }
        foreach (var (id, row) in incoming)
            if (!seen.Contains(id)) edited.Add(string.Join(';', row.Select(Escape)));
        var result = _lines[..(first + 1)].Concat(edited)
            .Concat(_lines[end..]).ToArray();
        File.WriteAllLines(path, result, new UTF8Encoding(false));
    }

    private (int First, int End) Bounds(string section)
    {
        var heading = "#" + section;
        var index = Array.FindIndex(_lines, line => line.Trim() == heading);
        if (index < 0) throw new InvalidOperationException($"Hiányzó CSV szekció: {section}");
        var end = index + 2;
        while (end < _lines.Length && !_lines[end].StartsWith('#')) end++;
        while (end > index + 2 && string.IsNullOrWhiteSpace(_lines[end - 1])) end--;
        return (index + 1, end);
    }

    private static List<string> Parse(string line)
    {
        var cells = new List<string>(); var cell = new StringBuilder(); var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"' && quoted && i + 1 < line.Length && line[i + 1] == '"') { cell.Append('"'); i++; }
            else if (c == '"') quoted = !quoted;
            else if (c == ';' && !quoted) { cells.Add(cell.ToString()); cell.Clear(); }
            else cell.Append(c);
        }
        cells.Add(cell.ToString()); return cells;
    }

    private static string Escape(string cell) => cell.IndexOfAny([';', '"', '\n', '\r']) >= 0
        ? "\"" + cell.Replace("\"", "\"\"") + "\"" : cell;
}
