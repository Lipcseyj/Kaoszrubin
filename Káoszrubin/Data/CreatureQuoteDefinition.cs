namespace KaoszRubin.Data;

public enum CreatureQuoteKind { CharacterClass, Enemy }

public sealed record CreatureQuoteDefinition(string Id, CreatureQuoteKind Kind, string CreatureId,
    IReadOnlyList<string> Quotes);
