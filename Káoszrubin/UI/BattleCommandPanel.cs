using KaoszRubin.Application;

namespace KaoszRubin.UI;

/// <summary>
/// Represents a segment of text with an associated console color for rendering hotkeys with distinct styling.
/// </summary>
public sealed record TextSegment(string Text, ConsoleColor? Color = null);

public sealed class BattleCommandPanel
{
    public const int Width = 170;
    public const int Row = 44;
    private const string LeftDecoration = "♦▓▒ ";
    private const string RightDecoration = " ▒▓♦";
    private readonly ConsoleColor _foreground;
    private readonly ConsoleColor _background;
    private readonly ConsoleColor _hotkeyColor;
    private readonly string _closedLine;
    private string _line;
    private IReadOnlyList<TextSegment> _segments = [];

    public BattleCommandPanel(ConsoleColor foreground = ConsoleColor.DarkYellow,
        ConsoleColor background = ConsoleColor.Black, string closedLine = "",
        ConsoleColor hotkeyColor = ConsoleColor.Cyan)
    {
        _foreground = foreground;
        _background = background;
        _hotkeyColor = hotkeyColor;
        _closedLine = closedLine;
        _line = closedLine;
    }

    public bool IsOpen { get; private set; }
    public ConsoleColor Foreground => _foreground;
    public ConsoleColor Background => _background;
    public ConsoleColor HotkeyColor => _hotkeyColor;
    public string Line => _line;
    public IReadOnlyList<TextSegment> Segments => _segments;

    public string Open(string text)
    {
        IsOpen = true;
        _line = FitCentered(text);
        _segments = [new TextSegment(_line)];
        return _line;
    }

    public string OpenWithHighlighting(IReadOnlyList<TextSegment> segments)
    {
        IsOpen = true;
        var combined = string.Concat(segments.Select(s => s.Text));
        _line = FitCentered(combined);
        _segments = segments;
        return _line;
    }

    public string Close()
    {
        IsOpen = false;
        _line = FitCentered(_closedLine);
        _segments = [new TextSegment(_line)];
        return _line;
    }

    public static string Format(IEnumerable<BattleActionKind> actions,
        IReadOnlyList<BattleTacticOptionSnapshot>? tactics = null, bool enemyTurn = false)
    {
        if (enemyTurn) return Decorate("Space: végrehajtja az ellenfél akcióját.");
        if (tactics is { Count: > 0 })
            return Decorate("Taktika: " + string.Join(" | ", tactics.Select((tactic, index) =>
                $"{index + 1}: {tactic.Name}")));

        var actionSet = actions.ToHashSet();
        if (actionSet.SetEquals([BattleActionKind.Move, BattleActionKind.Pass]))
            return Decorate("Akció: nyilak: további lépés | Space/P: mozgás vége");

        var commands = new List<string>();
        if (actionSet.Contains(BattleActionKind.PhysicalAttack)) commands.Add("Space: támadás");
        if (actionSet.Contains(BattleActionKind.Move) || actionSet.Contains(BattleActionKind.MoveFormation))
            commands.Add("nyilak: mozgás");
        if (actionSet.Contains(BattleActionKind.CastSpell)) commands.Add("V/F1-F8: varázslat");
        if (actionSet.Contains(BattleActionKind.SelectTarget)) commands.Add("Tab: célpont");
        if (actionSet.Contains(BattleActionKind.UseItem)) commands.Add("U: tárgy");
        if (actionSet.Contains(BattleActionKind.TurnUndead)) commands.Add("T: halottűzés");
        if (actionSet.Contains(BattleActionKind.Retreat)) commands.Add("R: visszavonulás");
        if (actionSet.Contains(BattleActionKind.Pass)) commands.Add("P: passz");
        return commands.Count == 0 ? string.Empty : Decorate("Akció: " + string.Join(" | ", commands));
    }

    /// <summary>
    /// Parses a format string with hotkeys (e.g., "Space: attack | Tab: target") and returns segments
    /// where hotkeys are highlighted separately. Hotkeys are identified by text before colons.
    /// </summary>
    public static IReadOnlyList<TextSegment> FormatWithHighlighting(IEnumerable<BattleActionKind> actions,
        IReadOnlyList<BattleTacticOptionSnapshot>? tactics = null, bool enemyTurn = false,
        ConsoleColor? hotkeyColor = null)
    {
        var plainText = Format(actions, tactics, enemyTurn);
        if (string.IsNullOrEmpty(plainText)) return [];

        return ParseHotkeysPublic(plainText, hotkeyColor);
    }

    /// <summary>
    /// Parses a text string containing hotkeys (identified by text before colons) and creates
    /// colored segments where hotkey portions use the specified highlight color.
    /// Only highlights recognized battle command hotkeys to avoid false positives.
    /// </summary>
    public static IReadOnlyList<TextSegment> ParseHotkeysPublic(string text, ConsoleColor? hotkeyColor)
    {
        var knownHotkeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Space", "nyilak", "V", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8",
            "Tab", "U", "T", "R", "P", "Space/P", "V/F1-F8", "1", "2", "3", "4", "5", "6", "7", "8", "9"
        };

        var segments = new List<TextSegment>();
        void AddPlainText(string value)
        {
            var start = 0;
            while (start < value.Length)
            {
                var separator = value.IndexOf('|', start);
                if (separator < 0)
                {
                    segments.Add(new TextSegment(value[start..]));
                    return;
                }
                if (separator > start) segments.Add(new TextSegment(value[start..separator]));
                segments.Add(new TextSegment("|", ConsoleColor.Red));
                start = separator + 1;
            }
        }
        var segmentStart = 0;
        var searchStart = 0;
        while (searchStart < text.Length)
        {
            var colonIndex = text.IndexOf(':', searchStart);
            if (colonIndex == -1)
                break;

            var tokenStart = colonIndex - 1;
            while (tokenStart >= 0 && text[tokenStart] != '|' && text[tokenStart] != ' ')
                tokenStart--;
            tokenStart++;
            var potentialHotkey = text[tokenStart..colonIndex].Trim();
            if (!knownHotkeys.Contains(potentialHotkey))
            {
                searchStart = colonIndex + 1;
                continue;
            }

            if (tokenStart > segmentStart)
                AddPlainText(text[segmentStart..tokenStart]);
            segments.Add(new TextSegment(text[tokenStart..(colonIndex + 1)], hotkeyColor));
            segmentStart = colonIndex + 1;
            searchStart = segmentStart;
        }

        if (segmentStart < text.Length) AddPlainText(text[segmentStart..]);
        return segments;
    }

    private static string Decorate(string text) => LeftDecoration + text + RightDecoration;

    /// <summary>
    /// Centers text within the panel width. If text exceeds width, it is truncated.
    /// </summary>
    private string FitCentered(string text)
    {
        if (text.Length > Width)
            text = text[..Width];

        if (text.Length == Width)
            return text;

        var padding = Width - text.Length;
        var leftPadding = padding / 2;
        var rightPadding = padding - leftPadding;

        return new string(' ', leftPadding) + text + new string(' ', rightPadding);
    }
}
