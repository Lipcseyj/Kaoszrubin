using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AsciiArtEditor.Services;

public enum PortraitDictionary
{
    CharacterClasses,
    Enemies
}

public sealed record PortraitSourceEntry(string KeyExpression, string Content);

public sealed record PortraitSaveResult(bool Success, bool Inserted, string? Error = null);

public sealed class AsciiPortraitSource
{
    private const string TypeDeclaration = "private static readonly IReadOnlyDictionary<string, AsciiPortrait>";
    private static readonly Regex EntryRegex = new(
        "(?m)^(?<indent>[ \\t]*)\\[(?<key>[^\\]\\r\\n]+)\\]\\s*=\\s*Portrait\\(\\s*\"\"\"(?<inner>.*?)\"\"\"\\s*\\)(?<comma>,?)",
        RegexOptions.Singleline | RegexOptions.Compiled);

    public Dictionary<string, string> ParsePortraits(string sourceContent, int set = 1)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (PortraitDictionary dictionary in Enum.GetValues<PortraitDictionary>())
        {
            foreach (var entry in ParseDictionary(sourceContent, dictionary, set))
                result[entry.KeyExpression] = entry.Content;
        }

        return result;
    }

    public IReadOnlyList<PortraitSourceEntry> ParseDictionary(string sourceContent, PortraitDictionary dictionary, int set = 1)
    {
        var range = FindDictionary(sourceContent, dictionary, set);
        var body = sourceContent[range.BodyStart..range.CloseBrace];
        var entries = new List<PortraitSourceEntry>();
        foreach (Match match in EntryRegex.Matches(body))
        {
            entries.Add(new PortraitSourceEntry(
                match.Groups["key"].Value.Trim(),
                DecodeRawString(match.Groups["inner"].Value)));
        }

        return entries;
    }

    public Dictionary<string, string> LoadPortraitsFromFile(string filePath, int set = 1) => ParsePortraits(File.ReadAllText(filePath), set);

    public bool UpdatePortraitInFile(string filePath, string id, string newInner, int set = 1)
    {
        var source = File.ReadAllText(filePath);
        var matches = Enum.GetValues<PortraitDictionary>()
            .SelectMany(dictionary => FindEntries(source, dictionary, set)
                .Where(entry => string.Equals(entry.KeyExpression, id, StringComparison.OrdinalIgnoreCase))
                .Select(entry => (dictionary, entry)))
            .ToArray();
        if (matches.Length != 1)
            return false;

        return SavePortraitInFile(filePath, matches[0].dictionary, matches[0].entry.KeyExpression, newInner, set).Success;
    }

    public PortraitSaveResult SavePortraitInFile(
        string filePath,
        PortraitDictionary dictionary,
        string keyExpression,
        string content,
        int set = 1)
    {
        if (!IsAllowedKeyExpression(dictionary, keyExpression))
            return new PortraitSaveResult(false, false, "The key must be a matching domain ID or a quoted string literal.");

        var source = File.ReadAllText(filePath);
        DictionaryRange range;
        try
        {
            range = FindDictionary(source, dictionary, set);
        }
        catch (InvalidDataException ex)
        {
            return new PortraitSaveResult(false, false, ex.Message);
        }

        var existing = FindEntries(source, dictionary, set);
        var matching = existing.Where(entry => KeysEqual(entry.KeyExpression, keyExpression)).ToArray();
        if (matching.Length > 1)
            return new PortraitSaveResult(false, false, "The key is duplicated in the selected dictionary.");

        var newline = source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var changed = source;
        if (matching.Length == 1)
        {
            var inner = matching[0].Match.Groups["inner"];
            var absoluteIndex = matching[0].Offset + inner.Index;
            var indent = GetRawIndent(inner.Value);
            string rawContent;
            try
            {
                rawContent = EncodeRawString(content, indent, newline);
            }
            catch (InvalidDataException ex)
            {
                return new PortraitSaveResult(false, false, ex.Message);
            }

            changed = source[..absoluteIndex] + rawContent + source[(absoluteIndex + inner.Length)..];
            WriteAtomically(filePath, changed);
            return new PortraitSaveResult(true, false);
        }

        var keyToken = keyExpression.Trim();
        var entryIndent = existing.Count > 0 ? existing[0].Match.Groups["indent"].Value : GetEntryIndent(source, range);
        var rawIndent = entryIndent + "    ";
        string encodedContent;
        try
        {
            encodedContent = EncodeRawString(content, rawIndent, newline);
        }
        catch (InvalidDataException ex)
        {
            return new PortraitSaveResult(false, false, ex.Message);
        }

        var entryText = $"{entryIndent}[{keyToken}] = Portrait({newline}{rawIndent}\"\"\"{encodedContent}\"\"\"),{newline}";
        var insertAt = source.LastIndexOf('\n', range.CloseBrace);
        insertAt = insertAt < range.BodyStart ? range.CloseBrace : insertAt + 1;

        var lastEntry = existing.LastOrDefault();
        if (lastEntry is not null && lastEntry.Match.Groups["comma"].Length == 0)
        {
            var end = lastEntry.Offset + lastEntry.Match.Index + lastEntry.Match.Length;
            changed = source[..end] + "," + source[end..];
            if (insertAt >= end)
                insertAt++;
        }

        changed = changed.Insert(insertAt, entryText);
        WriteAtomically(filePath, changed);
        return new PortraitSaveResult(true, true);
    }

    private static List<EntryMatch> FindEntries(string source, PortraitDictionary dictionary, int set)
    {
        var range = FindDictionary(source, dictionary, set);
        var body = source[range.BodyStart..range.CloseBrace];
        return EntryRegex.Matches(body)
            .Select(match => new EntryMatch(match.Groups["key"].Value.Trim(), match, range.BodyStart))
            .ToList();
    }

    private static DictionaryRange FindDictionary(string source, PortraitDictionary dictionary, int set)
    {
        if (set is not (1 or 2))
            throw new ArgumentOutOfRangeException(nameof(set));
        var name = dictionary + (set == 2 ? "Set2" : "");
        var declaration = source.IndexOf($"{TypeDeclaration} {name} =", StringComparison.Ordinal);
        if (declaration < 0)
            throw new InvalidDataException($"Could not find the {name} portrait dictionary.");

        var initializer = source.IndexOf("new Dictionary<string, AsciiPortrait>", declaration, StringComparison.Ordinal);
        if (initializer < 0)
            throw new InvalidDataException($"Could not find the {name} dictionary initializer.");

        var openBrace = source.IndexOf('{', initializer);
        if (openBrace < 0)
            throw new InvalidDataException($"The {name} dictionary has no initializer brace.");

        var closeBrace = FindMatchingBrace(source, openBrace);
        if (closeBrace < 0)
            throw new InvalidDataException($"The {name} dictionary braces are unbalanced.");

        return new DictionaryRange(openBrace + 1, closeBrace);
    }

    private static int FindMatchingBrace(string source, int openBrace)
    {
        var depth = 0;
        for (var index = openBrace; index < source.Length; index++)
        {
            if (source.AsSpan(index).StartsWith("\"\"\"", StringComparison.Ordinal))
            {
                var endRawString = source.IndexOf("\"\"\"", index + 3, StringComparison.Ordinal);
                if (endRawString < 0)
                    return -1;
                index = endRawString + 2;
            }
            else if (source.AsSpan(index).StartsWith("//", StringComparison.Ordinal))
            {
                var endComment = source.IndexOf('\n', index);
                if (endComment < 0)
                    return -1;
                index = endComment;
            }
            else if (source.AsSpan(index).StartsWith("/*", StringComparison.Ordinal))
            {
                var endComment = source.IndexOf("*/", index + 2, StringComparison.Ordinal);
                if (endComment < 0)
                    return -1;
                index = endComment + 1;
            }
            else if (source[index] == '"' || source[index] == '\'')
            {
                var endLiteral = FindLiteralEnd(source, index, source[index]);
                if (endLiteral < 0)
                    return -1;
                index = endLiteral;
            }
            else if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}' && --depth == 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindLiteralEnd(string source, int start, char quote)
    {
        for (var index = start + 1; index < source.Length; index++)
        {
            if (source[index] == '\\')
                index++;
            else if (source[index] == quote)
                return index;
        }

        return -1;
    }

    private static string DecodeRawString(string rawContent)
    {
        var normalized = rawContent.Replace("\r\n", "\n");
        if (normalized.StartsWith('\n'))
            normalized = normalized[1..];

        var lastLineBreak = normalized.LastIndexOf('\n');
        if (lastLineBreak >= 0 && normalized[(lastLineBreak + 1)..].All(char.IsWhiteSpace))
            normalized = normalized[..lastLineBreak];

        var closeIndent = GetRawIndent(rawContent);
        var lines = normalized.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            if (lines[index].StartsWith(closeIndent, StringComparison.Ordinal))
                lines[index] = lines[index][closeIndent.Length..];
        }

        return string.Join('\n', lines);
    }

    private static string GetRawIndent(string rawContent)
    {
        var normalized = rawContent.Replace("\r\n", "\n");
        var lastLineBreak = normalized.LastIndexOf('\n');
        if (lastLineBreak < 0)
            return "";

        var indent = normalized[(lastLineBreak + 1)..];
        return indent.All(character => character is ' ' or '\t') ? indent : "";
    }

    private static string EncodeRawString(string content, string indent, string newline)
    {
        var normalized = content.Replace("\r\n", "\n").Replace('\r', '\n').Trim('\n');
        if (normalized.Contains("\"\"\"", StringComparison.Ordinal))
            throw new InvalidDataException("Portrait content containing triple quotes cannot be saved as a raw string.");

        var lines = normalized.Split('\n');
        return newline + string.Join(newline, lines.Select(line => indent + line)) + newline + indent;
    }

    private static string GetEntryIndent(string source, DictionaryRange range)
    {
        var lineStart = source.LastIndexOf('\n', Math.Max(0, range.BodyStart - 1)) + 1;
        var dictionaryIndent = source[lineStart..range.BodyStart];
        var leadingWhitespace = new string(dictionaryIndent.TakeWhile(char.IsWhiteSpace).ToArray());
        return leadingWhitespace + "    ";
    }

    private static bool IsAllowedKeyExpression(PortraitDictionary dictionary, string keyExpression)
    {
        var expectedType = dictionary == PortraitDictionary.CharacterClasses ? "CharacterClassIds" : "MonsterIds";
        var symbolPattern = $"^{Regex.Escape(expectedType)}\\.[\\p{{L}}_][\\p{{L}}\\p{{Nd}}_]*$";
        if (Regex.IsMatch(keyExpression.Trim(), symbolPattern, RegexOptions.CultureInvariant))
            return true;

        try
        {
            return JsonSerializer.Deserialize<string>(keyExpression) is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool KeysEqual(string first, string second)
    {
        var firstValue = TryGetStringLiteral(first);
        var secondValue = TryGetStringLiteral(second);
        return firstValue is not null && secondValue is not null
            ? string.Equals(firstValue, secondValue, StringComparison.OrdinalIgnoreCase)
            : string.Equals(first.Trim(), second.Trim(), StringComparison.Ordinal);
    }

    private static string? TryGetStringLiteral(string expression)
    {
        try
        {
            return JsonSerializer.Deserialize<string>(expression);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void WriteAtomically(string filePath, string content)
    {
        var temporaryPath = filePath + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, content, new UTF8Encoding(false));
            File.Move(temporaryPath, filePath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private sealed record DictionaryRange(int BodyStart, int CloseBrace);

    private sealed record EntryMatch(string KeyExpression, Match Match, int Offset);
}