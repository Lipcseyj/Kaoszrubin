using KaoszRubin.Application;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Magic;

namespace KaoszRubin.UI;

/// <summary>Közös host–vendég varázslatinformációs karakterlap.</summary>
public static class SpellInfoPanel
{
    private const int VisibleSpellRows = 11;
    private const int DescriptionRows = 3;
    private const int EffectRows = 9;

    public static IReadOnlyList<CharacterSheetPanelLine> Build(string characterName, string characterClassId,
        int characterLevel, SpellInfoSnapshot info, int selectedIndex, bool focused = false,
        int width = CharacterSheetPanel.Width)
    {
        var effectiveWidth = Math.Max(CharacterSheetPanel.Width, width);
        var spells = info.KnownSpells;
        selectedIndex = spells.Count == 0 ? 0 : Math.Clamp(selectedIndex, 0, spells.Count - 1);
        var lines = new List<CharacterSheetPanelLine>
        {
            new(0, $"VARÁZSLATOK - {characterName}", ConsoleColor.Yellow, Background:
                focused ? ConsoleColor.Green : ConsoleColor.Black),
            new(1, $"Fókusz: {info.FocusName}", info.FocusName == "HIÁNYZIK" ? ConsoleColor.Red : ConsoleColor.Cyan),
            new(2, $"Memória: {spells.Count(spell => spell.IsMemorized)}/{info.MemorizationCapacity}", ConsoleColor.Magenta),
            new(3, "[M] memorizált  [F#] gyors", ConsoleColor.DarkCyan),
            new(4, "ISMERT VARÁZSLATOK", ConsoleColor.White)
        };
        var start = Math.Clamp(selectedIndex - VisibleSpellRows / 2, 0,
            Math.Max(0, spells.Count - VisibleSpellRows));
        for (var row = 0; row < VisibleSpellRows; row++)
        {
            var index = start + row;
            if (index >= spells.Count) { lines.Add(new(5 + row, string.Empty, ConsoleColor.Gray)); continue; }
            var spell = spells[index];
            var quick = spell.QuickSlot is { } slot ? $"F{slot + 1}" : "  ";
            var selected = index == selectedIndex;
            lines.Add(new(5 + row, $"{(selected ? ">" : " ")}[{(spell.IsMemorized ? "M" : " ")}][{quick}] " +
                $"{spell.Level}. {spell.Name}", selected ? ConsoleColor.Yellow :
                    spell.IsMemorized ? ConsoleColor.Cyan : ConsoleColor.Gray,
                Background: selected ? ConsoleColor.DarkCyan : ConsoleColor.Black));
        }
        if (spells.Count > 0)
        {
            var selected = spells[selectedIndex];
            lines.Add(new(17, "KIJELÖLT VARÁZSLAT", ConsoleColor.White));
            lines.Add(new(18, selected.Name, ConsoleColor.Yellow));
            lines.Add(new(19, $"L{selected.Level} | {selected.ManaCost} manna | {SchoolName(selected.School)}",
                ConsoleColor.Blue));
            lines.Add(new(20, $"Cél: {ConsoleRenderer.SpellTargetName(selected.TargetType)}", ConsoleColor.Cyan));
            lines.Add(new(21, $"Hatótáv: {(selected.Range == 0 ? "helyi" : $"{selected.Range} mező")} | " +
                $"Sugár: {(selected.AreaRadius == 0 ? "nincs" : $"{selected.AreaRadius} mező")}", ConsoleColor.Cyan));
            lines.Add(new(22, $"Látóvonal: {(selected.RequiresLineOfSight ? "szükséges" : "nem szükséges")}",
                selected.RequiresLineOfSight ? ConsoleColor.DarkYellow : ConsoleColor.Green));
            lines.Add(new(23, $"Használat: {UsageName(selected.UsageMode)}", ConsoleColor.Cyan));
            lines.Add(new(24, $"Vizuál: {PaletteName(selected.ImpactPalette)}, " +
                $"{selected.ImpactDurationMilliseconds / 1000d:0.##} mp", ConsoleColor.DarkMagenta));
            lines.Add(new(25, (selected.IsMemorized
                    ? $"Memorizált{(selected.QuickSlot is { } slot ? $", F{slot + 1}" : string.Empty)}"
                    : "Csak ismert") + (selected.EnemyOnly ? " | csak ellenfél" : string.Empty),
                ConsoleColor.Magenta));
            lines.Add(new(26, "LEÍRÁS", ConsoleColor.White));
            var description = Wrap(selected.Description, effectiveWidth).Take(DescriptionRows).ToArray();
            for (var row = 0; row < DescriptionRows; row++)
                lines.Add(new(27 + row, row < description.Length ? description[row] : string.Empty, ConsoleColor.Gray));
            lines.Add(new(30, "HATÁSOK", ConsoleColor.White));
            var effects = (selected.Effects ?? []).Select(DescribeEffect)
                .SelectMany(effect => Wrap(effect, effectiveWidth)).Take(EffectRows).ToArray();
            if (effects.Length == 0) effects = ["Nincs konfigurált hatás."];
            for (var row = 0; row < EffectRows; row++)
                lines.Add(new(31 + row, row < effects.Length ? effects[row] : string.Empty,
                    row < effects.Length ? ConsoleColor.DarkCyan : ConsoleColor.Gray));
        }
        lines.Add(new(40, "VARÁZSLATSZINTEK", ConsoleColor.White));
        var unlocks = characterClassId == CharacterClassIds.Lovag ? new[] { 1, 8 } : new[] { 1, 5, 10, 15, 20 };
        lines.Add(new(41, $"Feloldva: {unlocks.Count(level => characterLevel >= level)}/{unlocks.Length}",
            ConsoleColor.Green));
        var nextUnlock = unlocks.FirstOrDefault(level => level > characterLevel);
        lines.Add(new(42, nextUnlock == 0 ? "Minden szint feloldva." : $"Következő feloldás: L{nextUnlock}", ConsoleColor.Cyan));
        lines.Add(new(45, "Fel/le | F1-F8 gyors", ConsoleColor.Green));
        lines.Add(new(46, "Enter elsüt | Esc vissza", ConsoleColor.DarkYellow));
        return lines;
    }

    private static string DescribeEffect(KnownSpellEffectSnapshot effect)
    {
        var mechanics = new List<string>();
        if (effect.Dice is { } dice) mechanics.Add(dice.ToString());
        if (effect.IntelligenceMultiplier != 0) mechanics.Add($"INT×{effect.IntelligenceMultiplier:0.##}");
        if (effect.LevelMultiplier != 0) mechanics.Add($"szint×{effect.LevelMultiplier}");
        if (effect.Value != 0) mechanics.Add($"érték {effect.Value}");
        if (effect.Duration > 0) mechanics.Add($"{effect.Duration} kör");
        if (effect.ChancePercent is > 0 and < 100) mechanics.Add($"{effect.ChancePercent}% esély");
        if (effect.Resolution != SpellResolution.Auto) mechanics.Add(ResolutionName(effect.Resolution));
        if (!string.IsNullOrWhiteSpace(effect.Parameter)) mechanics.Add(effect.Parameter!);
        var detail = mechanics.Count == 0 ? string.Empty : $" ({string.Join(", ", mechanics)})";
        var description = string.IsNullOrWhiteSpace(effect.Description) ? string.Empty : $": {effect.Description}";
        return $"• {EffectName(effect.Type)}{detail}{description}";
    }

    private static string SchoolName(SpellSchool school) => school switch
    {
        SpellSchool.Divine => "Isteni",
        _ => "Arkán"
    };

    private static string UsageName(SpellUsageMode usage) => usage switch
    {
        SpellUsageMode.Exploration => "térképen",
        SpellUsageMode.Combat => "csatában",
        _ => "térképen és csatában"
    };

    private static string PaletteName(SpellImpactPalette palette) => palette switch
    {
        SpellImpactPalette.Red => "vörös",
        SpellImpactPalette.YellowBrown => "aranybarna",
        SpellImpactPalette.Purple => "lila",
        SpellImpactPalette.SicklyGreen => "betegzöld",
        SpellImpactPalette.Shadow => "árny",
        SpellImpactPalette.BloodRed => "vérvörös",
        _ => "kék"
    };

    private static string ResolutionName(SpellResolution resolution) => resolution switch
    {
        SpellResolution.Attack => "támadópróba",
        SpellResolution.SaveHalf => "mentő: felez",
        SpellResolution.SaveNegates => "mentő: kivéd",
        _ => "automatikus"
    };

    private static string EffectName(SpellEffectType type) => type switch
    {
        SpellEffectType.Damage => "Sebzés",
        SpellEffectType.Burning => "Égés",
        SpellEffectType.SpeedPenalty => "Lassítás",
        SpellEffectType.SkipAlternate => "Akcióvesztés",
        SpellEffectType.Invisibility => "Láthatatlanság",
        SpellEffectType.DefenseBonus => "Védelem",
        SpellEffectType.TeleportSelf => "Teleportálás",
        SpellEffectType.Dispel => "Mágiatörés",
        SpellEffectType.Storm => "Vihar",
        SpellEffectType.ChainDamage => "Láncsebzés",
        SpellEffectType.PhysicalReduction => "Fizikai védelem",
        SpellEffectType.BleedingImmunity => "Vérzésimmunitás",
        SpellEffectType.ExtraActions => "Extra akció",
        SpellEffectType.Execute => "Kivégzés",
        SpellEffectType.TeleportParty => "Partiteleport",
        SpellEffectType.RandomElement => "Véletlen elem",
        SpellEffectType.Heal => "Gyógyítás",
        SpellEffectType.CureStatus => "Állapotgyógyítás",
        SpellEffectType.HitBonus => "Találati bónusz",
        SpellEffectType.DamageBonus => "Sebzésbónusz",
        SpellEffectType.InitiativeBonus => "Kezdeményezés",
        SpellEffectType.ProtectionFromEvil => "Gonosz elleni védelem",
        SpellEffectType.GuardianAngel => "Őrangyal",
        SpellEffectType.Sanctuary => "Szentély",
        SpellEffectType.Resurrect => "Feltámasztás",
        SpellEffectType.DispelBeneficial => "Erősítés törlése",
        SpellEffectType.RestoreNeeds => "Szükségletek helyreállítása",
        SpellEffectType.VisionBonus => "Látásmódosítás",
        SpellEffectType.BreakItemCurse => "Tárgyátok megtörése",
        SpellEffectType.WeaponDamageType => "Fegyversebzés-típus",
        _ => type.ToString()
    };

    private static IEnumerable<string> Wrap(string text, int width)
    {
        var remaining = text;
        while (remaining.Length > width)
        {
            var split = remaining.LastIndexOf(' ', width);
            if (split <= 0) split = width;
            yield return remaining[..split];
            remaining = remaining[split..].TrimStart();
        }
        yield return remaining;
    }
}

public sealed record SpellSelectorOption(string Name, int Level, int ManaCost, SpellTargetType TargetType,
    string QuickLabel, bool Affordable);

/// <summary>Közös host–vendég varázslatválasztó ablak tartalma.</summary>
public static class SpellSelectorWindow
{
    public const int Width = 76;
    public const int PageSize = 12;

    public static IReadOnlyList<(string Text, ConsoleColor Color)> Build(string characterName, int currentMana,
        int maximumMana, bool inCombat, IReadOnlyList<SpellSelectorOption> options, int selectedIndex,
        int firstVisibleIndex, int casterIndex = 0, int casterCount = 1)
    {
        var switchHint = casterCount > 1 ? "  ◄► váltás" : string.Empty;
        var casterHint = casterCount > 1 ? $"   ({casterIndex + 1}/{casterCount})" : string.Empty;
        var visible = options.Skip(firstVisibleIndex).Take(PageSize).ToArray();
        var lines = new List<(string Text, ConsoleColor Color)>
        {
            (inCombat ? "⚔️ HARCI VARÁZSLÁS" : "🔮 VARÁZSLÁS", ConsoleColor.Magenta),
            ($"{characterName}  ◆ {currentMana}/{maximumMana} manna{casterHint}", ConsoleColor.Cyan),
            ("↑↓ választ  Enter célzás  Esc bezár" + switchHint, ConsoleColor.Green),
            (new string('─', 68), ConsoleColor.DarkMagenta)
        };
        if (options.Count == 0)
        {
            lines.Add(("Ebben a helyzetben nincs használható memorizált", ConsoleColor.DarkYellow));
            lines.Add(("vagy tárgyban tárolt varázslat.", ConsoleColor.DarkYellow));
        }
        else
        {
            lines.AddRange(visible.Select((spell, visibleIndex) =>
            {
                var index = firstVisibleIndex + visibleIndex;
                return ($"{(index == selectedIndex ? "▶" : " ")} [{spell.QuickLabel}] L{spell.Level}  " +
                        $"{spell.Name,-24} {spell.ManaCost}M  {ConsoleRenderer.SpellTargetName(spell.TargetType)}",
                    !spell.Affordable ? ConsoleColor.DarkRed :
                    index == selectedIndex ? ConsoleColor.Yellow : ConsoleColor.Gray);
            }));
            if (options.Count > PageSize)
                lines.Add(($"{firstVisibleIndex + 1}–{firstVisibleIndex + visible.Length} / {options.Count}",
                    ConsoleColor.DarkCyan));
        }
        return lines;
    }
}
