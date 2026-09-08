using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Domain.Characters;

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
    public const string Staff = "STAFF";

    public static readonly IReadOnlyList<WeaponFamilyDefinition> All =
    [
        new(Dagger, "Tőr", "🗡️", "+2 kezdeményezés és +1 sebzés.", "A természetes 19 is kritikus; sikeres oldal- vagy hátbatámadás után kilép a lekötésből; tolvajként a zárt alakzat hátsó sorából is támadhatja az első társ lekötött ellenfelét."),
        new(Sword, "Kard", "⚔️", "+1 fegyveres találat.", "Felszerelt karddal +1 védelem, és +1 fedezetet ad a szomszédos társaknak."),
        new(Axe, "Bárd", "🪓", "+2 fizikai sebzés.", "A természetes 20 háromszoros kritikus; az íves söprés mellékcélpontjai teljes sebzést kapnak és -2 páncélt szenvednek a csata végéig."),
        new(Blunt, "Zúzófegyver", "🔨", "Az ellenfél páncéljából 2 pontot figyelmen kívül hagy.", "Összesen 4 pont páncélt hagy figyelmen kívül; találattal megszakítja az előkészített szörnyfegyvert, máskülönben megtorpasztja a következő közeledését."),
        new(Polearm, "Szálfegyver", "🔱", "+3 kezdeményezés; vonalban a cél mögötti ellenfelet is eléri.", "A csata első sikeres találata ×1,5 sebzés, és feltartóztatja a fegyver hatókörébe belépő ellenfelet."),
        new(Shield, "Pajzs", "🛡️", "Felszerelt pajzzsal +1 védelem, és +1 társi fedezetet ad a szomszédnak.", "A pajzsdobás kétszer történik; a társi fedezet +2, lovagnál +3."),
        new(Staff, "Harci bot", "🦯", "Felszerelve +1 védelem és -5% harci varázskudarc.", "Felszerelve összesen +2 védelem és -10% harci varázskudarc.")
    ];

    public static WeaponFamilyDefinition? Find(string id) => All.FirstOrDefault(family =>
        string.Equals(family.Id, id, StringComparison.OrdinalIgnoreCase));

    public static string? ForWeapon(WeaponDefinition? weapon)
    {
        if (weapon?.FamilyId is { Length: > 0 } family) return family;
        var id = weapon?.BaseWeaponId ?? weapon?.Id;
        return id switch
        {
            "W001" => Dagger,
            "W002" or "W003" or "W004" or "W009" => Sword,
            "W010" or "W017" => Axe,
            "W005" or "W006" or "W007" or "W008" or "W013" => Blunt,
            "W011" or "W012" => Polearm,
            "W014" or "W015" or "W016" => Shield,
            "W018" => Staff,
            _ => null
        };
    }

    public static IReadOnlyList<WeaponFamilyDefinition> AvailableFor(string characterClassId,
        IEnumerable<WeaponDefinition> weapons) => weapons
        .Where(weapon => !weapon.IsMonsterOnly && weapon.AllowedClassIds.Contains(characterClassId))
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

public static class DualWieldingRules
{
    public const int OffhandDamagePercent = 60;
    public const string ElvenDaggerId = "W022";

    public static bool TryGetWeapons(LiveCharacter character, out WeaponDefinition? mainHand,
        out WeaponDefinition? offhand)
    {
        mainHand = character.IsInventoryItemOperational(InventorySlotKind.Weapon, 0)
            ? character.WeaponSlots[0] : null;
        offhand = character.IsInventoryItemOperational(InventorySlotKind.Weapon, 1)
            ? character.WeaponSlots[1] : null;
        return offhand is not null && WeaponFamilies.ForWeapon(offhand) != WeaponFamilies.Shield &&
               CanEquipOffhand(character, mainHand, offhand);
    }

    public static bool CanEquipOffhand(LiveCharacter character, WeaponDefinition? mainHand,
        WeaponDefinition offhand)
    {
        if (WeaponFamilies.ForWeapon(offhand) == WeaponFamilies.Shield) return true;
        if (!character.HasTacticalDiscipline(TacticalDisciplines.DualWield) || mainHand is null ||
            mainHand.IsTwoHanded || offhand.IsTwoHanded || !IsSupportedPair(mainHand, offhand)) return false;
        var families = new[] { WeaponFamilies.ForWeapon(mainHand), WeaponFamilies.ForWeapon(offhand) };
        return families.All(family => character.WeaponProficiencyRankFor(family) is not null);
    }

    public static bool IsSupportedPair(WeaponDefinition first, WeaponDefinition second)
    {
        var firstFamily = WeaponFamilies.ForWeapon(first);
        var secondFamily = WeaponFamilies.ForWeapon(second);
        return firstFamily is WeaponFamilies.Dagger or WeaponFamilies.Sword &&
               secondFamily is WeaponFamilies.Dagger or WeaponFamilies.Sword;
    }

    public static bool HasPairedElvenDaggers(LiveCharacter character) =>
        TryGetWeapons(character, out var mainHand, out var offhand) &&
        string.Equals(mainHand?.Id, ElvenDaggerId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(offhand?.Id, ElvenDaggerId, StringComparison.OrdinalIgnoreCase);
}
