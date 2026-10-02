using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Domain.Magic;

public static class LightSourceRules
{
    public const string LightSpellId = "S026";
    public const int TrapDetectionBonus = 15;

    private static readonly ConsoleColor[] SpellAuraColors =
        [ConsoleColor.Yellow, ConsoleColor.Magenta, ConsoleColor.Cyan];

    public static bool IsActiveLight(ActiveSpellEffect effect) =>
        effect.Type == ActiveSpellEffectType.VisionBonus && effect.Value > 0 &&
        effect.SourceSpellId is LightSpellId or MiscItemIds.Torch;

    public static bool HasActiveLight(IEnumerable<ActiveSpellEffect> effects) => effects.Any(IsActiveLight);

    public static ConsoleColor RandomSpellAuraColor(Random random) =>
        SpellAuraColors[random.Next(SpellAuraColors.Length)];

    public static ConsoleColor AuraColor(ActiveSpellEffect effect)
    {
        if (effect.SourceSpellId == MiscItemIds.Torch) return ConsoleColor.Yellow;
        return Enum.TryParse<ConsoleColor>(effect.Parameter, true, out var color) &&
               SpellAuraColors.Contains(color)
            ? color
            : ConsoleColor.Yellow;
    }
}
