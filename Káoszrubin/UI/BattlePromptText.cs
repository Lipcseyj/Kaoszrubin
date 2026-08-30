using KaoszRubin.Application;
using KaoszRubin.Domain.Characters;

namespace KaoszRubin.UI;

/// <summary>A host és a vendég közös rövid harci vezérlési feliratai.</summary>
public static class BattlePromptText
{
    public const string EnemyTurn = "Space — ellenfél köre";

    public static string Tactic(string characterClassId,
        IReadOnlyList<BattleTacticOptionSnapshot>? options)
    {
        if (characterClassId == CharacterClassIds.Harcos && options is { Count: > 0 })
            return "Válassz harci állást: " + string.Join(" | ", options.Select((option, index) =>
                $"{index + 1} — {option.Name} {option.HitChancePercent}% ({option.Effect})"));
        return "Válassz megközelítést: 1 — Orvtámadás | 2 — Megfigyelés | 3 — Mérgezett penge";
    }

    public static string PlayerAction(bool canCastSpell, bool canTurnUndead) =>
        "Akció: Space — fegyveres támadás" +
        (canCastSpell ? " | V — varázslat | F1-F8 — gyorsvarázslat" : string.Empty) +
        (canTurnUndead ? " | T — halottűzés" : string.Empty) +
        " | ellenfél körében Space — tovább";
}
