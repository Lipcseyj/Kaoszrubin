namespace KaoszRubin.Domain.Characters;

public sealed record TacticalDisciplineDefinition(string Id, string Name, string Description);

/// <summary>Univerzális, csapatharcban érvényes fejlődési irányok.</summary>
public static class TacticalDisciplines
{
    public const string Finisher = "DISC-FINISHER";
    public const string Skirmisher = "DISC-SKIRMISHER";
    public const string Guardian = "DISC-GUARDIAN";
    public const string DualWield = "DISC-DUAL-WIELD";

    public static readonly IReadOnlyList<TacticalDisciplineDefinition> All =
    [
        new(Finisher, "🎯 Kivégző", "A fél HP alá sebesült ellenfelek elleni fegyveres támadás +2 találatot kap."),
        new(Skirmisher, "🏃 Portyázó", "Csapatharcban +2 kezdeményezést és +1 mező harci mozgást ad."),
        new(Guardian, "🤝 Bajtársi őrség", "Szomszédos élő társ mellett +1 saját védelmet ad, és +1 fedezetet nyújt a szomszédos társaknak."),
        new(DualWield, "⚔️ Kétfegyveres harc", "Két Jártas egykezes tőrrel vagy karddal a főkéz teljes támadása után a mellékkéz külön dobással 60% sebzést okoz.")
    ];

    public static TacticalDisciplineDefinition? Find(string id) => All.FirstOrDefault(definition =>
        string.Equals(definition.Id, id, StringComparison.OrdinalIgnoreCase));
}

public static class TacticalDisciplineProgression
{
    public static readonly IReadOnlyList<int> Milestones = [8, 18];

    public static int EarnedChoices(int level) => Milestones.Count(milestone => level >= milestone);
}

public enum ProgressionRetrainingKind { ClassFeatures, TacticalDisciplines, WeaponProficiencies }

public static class ProgressionRetrainingRules
{
    public static int Cost(LiveCharacter character, ProgressionRetrainingKind kind) =>
        Math.Max(300, character.Level * (kind == ProgressionRetrainingKind.WeaponProficiencies ? 75 : 100));

    public static string Name(ProgressionRetrainingKind kind) => kind switch
    {
        ProgressionRetrainingKind.ClassFeatures => "🌟 Osztályképességek",
        ProgressionRetrainingKind.TacticalDisciplines => "⚔️ Taktikai diszciplínák",
        ProgressionRetrainingKind.WeaponProficiencies => "🗡️ Fegyverjártasságok",
        _ => kind.ToString()
    };
}
