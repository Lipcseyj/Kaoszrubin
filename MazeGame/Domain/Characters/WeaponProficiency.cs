using MazeGame.Domain.Combat;

namespace MazeGame.Domain.Characters;

public enum WeaponProficiencyRank { Trained = 1, Master = 2 }

public sealed record WeaponFamilyDefinition(string Id, string Name, string Icon, string TrainedDescription,
    string MasterDescription);

public sealed record WeaponProficiencyState(string FamilyId, WeaponProficiencyRank Rank);

public static class WeaponFamilies
{
    public const string Dagger = "DAGGER";
    public const string Sword = "SWORD";
    public const string Axe = "AXE";
    public const string Blunt = "BLUNT";
    public const string Polearm = "POLEARM";
    public const string Shield = "SHIELD";

    public static readonly IReadOnlyList<WeaponFamilyDefinition> All =
    [
        new(Dagger, "Tőr", "🗡️", "+2 kezdeményezés és +1 sebzés.", "A természetes 19 is kétszeres kritikus találat."),
        new(Sword, "Kard", "⚔️", "+1 fegyveres találat.", "Felszerelt karddal +1 védelem."),
        new(Axe, "Bárd", "🪓", "+2 fizikai sebzés.", "A természetes 20 háromszoros kritikus sebzés."),
        new(Blunt, "Zúzófegyver", "🔨", "Az ellenfél páncéljából 2 pontot figyelmen kívül hagy.", "Összesen 4 pont páncélt hagy figyelmen kívül."),
        new(Polearm, "Szálfegyver", "🔱", "+3 kezdeményezés.", "A csata első sikeres találata ×1,5 sebzés."),
        new(Shield, "Pajzs", "🛡️", "Felszerelt pajzzsal +1 védelem.", "A pajzs védelmi dobását kétszer dobja, és a jobb eredmény számít.")
    ];

    public static WeaponFamilyDefinition? Find(string id) => All.FirstOrDefault(family =>
        string.Equals(family.Id, id, StringComparison.OrdinalIgnoreCase));

    public static string? ForWeapon(WeaponDefinition? weapon)
    {
        var id = weapon?.BaseWeaponId ?? weapon?.Id;
        return id switch
        {
            "W001" => Dagger,
            "W002" or "W003" or "W004" or "W009" => Sword,
            "W010" => Axe,
            "W005" or "W006" or "W007" or "W008" or "W013" => Blunt,
            "W011" or "W012" => Polearm,
            "W014" or "W015" or "W016" => Shield,
            _ => null
        };
    }

    public static IReadOnlyList<WeaponFamilyDefinition> AvailableFor(string characterClassId,
        IEnumerable<WeaponDefinition> weapons) => weapons
        .Where(weapon => weapon.AllowedClassIds.Contains(characterClassId))
        .Select(ForWeapon).Where(id => id is not null).Distinct(StringComparer.OrdinalIgnoreCase)
        .Select(id => Find(id!)!).OrderBy(family => family.Name).ToArray();
}

public static class WeaponProficiencyProgression
{
    private static readonly int[] MartialMilestones = [1, 7, 17, 27];
    private static readonly int[] OtherMilestones = [7, 17];

    public static IReadOnlyList<int> MilestonesFor(string characterClassId) =>
        CharacterClassRules.IsMartial(characterClassId) ? MartialMilestones : OtherMilestones;

    public static int EarnedAdvances(string characterClassId, int level) =>
        MilestonesFor(characterClassId).Count(milestone => level >= milestone);
}
