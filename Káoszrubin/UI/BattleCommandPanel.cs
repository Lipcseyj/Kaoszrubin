using KaoszRubin.Application;

namespace KaoszRubin.UI;

public sealed class BattleCommandPanel
{
    public const int Width = 170;
    public const int Row = 44;
    private readonly ConsoleColor _foreground;
    private readonly ConsoleColor _background;
    private readonly string _closedLine;
    private string _line;

    public BattleCommandPanel(ConsoleColor foreground = ConsoleColor.DarkYellow,
        ConsoleColor background = ConsoleColor.Black, string closedLine = "")
    {
        _foreground = foreground;
        _background = background;
        _closedLine = closedLine;
        _line = closedLine;
    }

    public bool IsOpen { get; private set; }
    public ConsoleColor Foreground => _foreground;
    public ConsoleColor Background => _background;
    public string Line => _line;

    public string Open(string text)
    {
        IsOpen = true;
        _line = Fit(text);
        return _line;
    }

    public string Close()
    {
        IsOpen = false;
        _line = Fit(_closedLine);
        return _line;
    }

    public static string Format(IEnumerable<BattleActionKind> actions,
        IReadOnlyList<BattleTacticOptionSnapshot>? tactics = null, bool enemyTurn = false)
    {
        if (enemyTurn) return "Space: végrehajtja az ellenfél akcióját.";
        if (tactics is { Count: > 0 })
            return "Taktika: " + string.Join(" | ", tactics.Select((tactic, index) =>
                $"{index + 1}: {tactic.Name}"));

        var commands = new List<string>();
        if (actions.Contains(BattleActionKind.PhysicalAttack)) commands.Add("Space: támadás");
        if (actions.Contains(BattleActionKind.Move) || actions.Contains(BattleActionKind.MoveFormation))
            commands.Add("nyilak: mozgás");
        if (actions.Contains(BattleActionKind.CastSpell)) commands.Add("V/F1-F8: varázslat");
        if (actions.Contains(BattleActionKind.SelectTarget)) commands.Add("Tab: célpont");
        if (actions.Contains(BattleActionKind.UseItem)) commands.Add("U: tárgy");
        if (actions.Contains(BattleActionKind.TurnUndead)) commands.Add("T: halottűzés");
        if (actions.Contains(BattleActionKind.Retreat)) commands.Add("R: visszavonulás");
        if (actions.Contains(BattleActionKind.Pass)) commands.Add("P: passz");
        return commands.Count == 0 ? string.Empty : "Akció: " + string.Join(" | ", commands);
    }

    private string Fit(string text)
    {
        if (text.Length > Width) text = text[..Width];
        return text.PadRight(Width);
    }
}
