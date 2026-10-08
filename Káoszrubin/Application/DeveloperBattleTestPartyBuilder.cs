using KaoszRubin.Data;
using KaoszRubin.Domain.Characters;

namespace KaoszRubin.Application;

internal static class DeveloperBattleTestPartyBuilder
{
    public static IReadOnlyList<LiveCharacter> CreateCompanions(GameDataCatalog data, DeveloperBattleTestOptions options,
        LiveCharacter leader, IEnumerable<string> existingNames, Random random)
    {
        options.Validate(Math.Max(leader.Level, data.ExperienceByLevel.Keys.DefaultIfEmpty(options.PartyLevel).Max()));
        var generator = new RandomCharacterGenerator(data, random);
        var equipment = RandomCharacterGenerator.EquipmentOptions.Scaled(tierVariance: 0);
        var usedNames = existingNames.ToList();
        var companions = new List<LiveCharacter>();
        foreach (var classId in new[] { CharacterClassIds.Mágus, CharacterClassIds.Pap, CharacterClassIds.Lovag,
                     CharacterClassIds.Barbár, CharacterClassIds.Tolvaj, CharacterClassIds.Harcos }
                     .Where(classId => classId != leader.CharacterClass.Id).Take(options.PartySize - 1))
        {
            var companion = generator.GenerateCombatTestCharacter(data.GetCharacterClass(classId),
                options.PartyLevel, usedNames, equipment);
            companions.Add(companion);
            usedNames.Add(companion.Name);
        }
        return companions;
    }
}