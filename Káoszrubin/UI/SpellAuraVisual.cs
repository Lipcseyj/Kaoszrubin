using KaoszRubin.Data;
using KaoszRubin.Domain.Magic;

namespace KaoszRubin.UI;

internal static class SpellAuraVisual
{
    public static ConsoleColor? Background(IEnumerable<string> activeSpellIds, GameDataCatalog gameData)
    {
        foreach (var spellId in activeSpellIds)
        {
            var spell = gameData.Spells.FirstOrDefault(candidate => candidate.Id == spellId);
            if (spell?.ImpactPattern is not (SpellImpactPattern.Ward or SpellImpactPattern.Halo))
                continue;
            return SpellVisualPalette.Colors(spell.StormPalette ?? spell.ImpactPalette).Dark;
        }
        return null;
    }
}
