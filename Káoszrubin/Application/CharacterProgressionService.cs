using KaoszRubin.Data;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;

namespace KaoszRubin.Application;

public sealed record ExperienceAward(LiveCharacter Character, LevelUpResult Result);

public sealed class CharacterProgressionService
{
    private readonly GameDataCatalog _gameData;
    private readonly Random _random;

    public CharacterProgressionService(GameDataCatalog gameData, Random random)
    {
        _gameData = gameData;
        _random = random;
    }

    public ExperienceAward AwardExperience(LiveCharacter character, int amount) => new(character,
        character.AddExperience(amount, _gameData.ExperienceByLevel,
            _gameData.GetVitalityGrowth(character.Abilities.Health),
            _gameData.GetManaGrowth(character.Abilities.Intelligence),
            _gameData.GetCharacterResourceGrowth(character.CharacterClass.Id), _random));

    public LevelUpResult AwardExperienceResult(LiveCharacter character, int amount) =>
        AwardExperience(character, amount).Result;

    public IReadOnlyList<ExperienceAward> DistributeExperience(
        LiveCharacter winner,
        int totalExperience,
        IEnumerable<LiveCharacter> livingParty)
    {
        var total = Math.Max(0, totalExperience);
        var others = livingParty
            .Where(character => character != winner && character.IsAlive)
            .ToList();
        if (others.Count == 0)
            return [AwardExperience(winner, total)];

        var winnerShare = total * 60 / 100;
        var remainder = total - winnerShare;
        var sharedBase = remainder / others.Count;
        var sharedRemainder = remainder % others.Count;
        var awards = new List<ExperienceAward> { AwardExperience(winner, winnerShare) };
        for (var index = 0; index < others.Count; index++)
            awards.Add(AwardExperience(others[index], sharedBase + (index < sharedRemainder ? 1 : 0)));
        return awards;
    }

    public static string FormatExperienceAwards(IEnumerable<ExperienceAward> awards) => string.Join("; ", awards.Select(award =>
        $"{award.Character.Name} +{award.Result.GainedExperience}" +
        (award.Result.LeveledUp ? $" (L{award.Result.PreviousLevel}→L{award.Result.CurrentLevel})" : string.Empty)));

    public IReadOnlyList<PerkOffer> CreatePerkOffers(LiveCharacter character, LevelUpResult result)
    {
        var offers = new List<PerkOffer>();
        for (var tier = 1; tier <= 3; tier++)
        {
            if (character.Perks.Any(perk => perk.Tier == tier)) continue;
            var milestone = PerkProgressionRules.TriggerLevel(character.Race, tier);
            if (result.CurrentLevel < milestone) continue;

            var triggerLevel = result.PreviousLevel < milestone ? milestone : result.CurrentLevel;
            offers.Add(new PerkOffer(tier, triggerLevel, _gameData.GetPerkChoices(character.CharacterClass.Id, tier)));
        }
        return offers;
    }

    public static bool ShouldChooseSpecialization(LiveCharacter character, IReadOnlyList<PerkOffer> offers) =>
        character.SpecializationId is null && ClassSpecializations.ForClass(character.CharacterClass.Id).Count > 0 &&
        (offers.Any(offer => offer.Tier == 1) || character.Perks.Any(perk => perk.Tier == 1));

    public static IEnumerable<int> PendingClassFeatureMilestones(LiveCharacter character, LevelUpResult result)
    {
        var acquired = character.ClassFeatureUpgrades.Count;
        foreach (var milestone in new[] { 10, 20 })
        {
            if (result.CurrentLevel < milestone || acquired >= milestone / 10) continue;
            acquired++;
            yield return milestone;
        }
    }

    public static IReadOnlyList<(string Id, string Name, string Description)> AbilityIncreaseChoices(
        LiveCharacter character)
    {
        var choices = new List<(string, string, string)>();
        if (character.Abilities.Strength < 13)
            choices.Add(("STR", $"💪 Erő: {character.Abilities.Strength} → {character.Abilities.Strength + 1}",
                "Növeli a közelharci sebzést, és a harci osztályok találati bónuszát is elérheti."));
        if (character.Abilities.Dexterity < 13)
            choices.Add(("DEX", $"🏹 Ügyesség: {character.Abilities.Dexterity} → {character.Abilities.Dexterity + 1}",
                "Javítja a fegyveres találatot, a kezdeményezést és az ellenséges támadások elleni védelmet."));
        if (character.Abilities.Health < 13)
            choices.Add(("HEA", $"❤️ Egészség: {character.Abilities.Health} → {character.Abilities.Health + 1}",
                "Azonnal növelheti az alap HP-t, és a következő szintlépések HP-növekedését is javítja."));
        if (character.Abilities.Intelligence < 13)
            choices.Add(("INT", $"🧠 Intelligencia: {character.Abilities.Intelligence} → {character.Abilities.Intelligence + 1}",
                "Erősíti a varázslatokat, csökkenti a kudarcot, és azonnal növelheti az alap mannát is."));
        return choices;
    }

    public bool ApplyAbilityIncrease(LiveCharacter character, string abilityId)
    {
        var oldVitalityBase = _gameData.GetMinimumVitality(character.Abilities.Health);
        var oldManaBase = character.UsesMana
            ? CharacterClassRules.AdjustStartingMana(character.CharacterClass.Id,
                _gameData.GetMinimumMana(character.Abilities.Intelligence) + character.ManaBonus)
            : 0;
        if (!character.TryIncreaseAbility(abilityId)) return false;
        var newVitalityBase = _gameData.GetMinimumVitality(character.Abilities.Health);
        var newManaBase = character.UsesMana
            ? CharacterClassRules.AdjustStartingMana(character.CharacterClass.Id,
                _gameData.GetMinimumMana(character.Abilities.Intelligence) + character.ManaBonus)
            : 0;
        character.ApplyAbilityResourceIncrease(newVitalityBase - oldVitalityBase, newManaBase - oldManaBase);
        return true;
    }

    public static int EarnedWeaponProficiencyAdvances(LiveCharacter character, int level) =>
        WeaponProficiencyProgression.EarnedAdvances(character.CharacterClass.Id, level);

    public IReadOnlyList<(string Id, string Name, string Description)> WeaponProficiencyChoices(
        LiveCharacter character) => WeaponFamilies.AvailableFor(character.CharacterClass.Id, _gameData.Weapons)
        .Where(family => character.WeaponProficiencyRankFor(family.Id) != WeaponProficiencyRank.Master &&
                         (character.WeaponProficiencies.Count < 2 ||
                          character.WeaponProficiencyRankFor(family.Id) is not null))
        .Select(family => character.WeaponProficiencyRankFor(family.Id) is null
            ? (family.Id, $"{family.Icon} {family.Name} — Jártas", family.TrainedDescription)
            : (family.Id, $"{family.Icon} {family.Name} — Mester", family.MasterDescription))
        .ToArray();

    public static int NextWeaponProficiencyMilestone(LiveCharacter character)
    {
        var index = character.WeaponProficiencyAdvances;
        return WeaponProficiencyProgression.MilestonesFor(character.CharacterClass.Id).ElementAtOrDefault(index);
    }
}
