namespace MazeGame.Domain.Characters;

public sealed record ClassSpecializationDefinition(string Id, string Name, string Description,
    string CharacterClassId);

public static class ClassSpecializations
{
    public const string PriestLife = "SPEC-C005-LIFE";
    public const string PriestProtection = "SPEC-C005-PROTECTION";
    public const string PriestJudgment = "SPEC-C005-JUDGMENT";
    public const string MageElementalist = "SPEC-C006-ELEMENTALIST";
    public const string MageIllusionist = "SPEC-C006-ILLUSIONIST";
    public const string MageNecromancer = "SPEC-C006-NECROMANCER";

    private static readonly IReadOnlyList<ClassSpecializationDefinition> Definitions =
    [
        new(PriestLife, "Élet", "+25% minden gyógyításra.", CharacterClassIds.Pap),
        new(PriestProtection, "Védelem", "A védővarázslatok egy akcióval tovább tartanak.", CharacterClassIds.Pap),
        new(PriestJudgment, "Ítélet", "+20% minden papi sebző varázslatra.", CharacterClassIds.Pap),
        new(MageElementalist, "Elementalista", "+20% minden közvetlen arkán varázssebzésre.", CharacterClassIds.Mágus),
        new(MageIllusionist, "Illuzionista", "A kontroll- és védővarázslatok egy akcióval tovább tartanak.", CharacterClassIds.Mágus),
        new(MageNecromancer, "Nekromanta", "A közvetlen varázssebzés 10%-a gyógyítja a mágust.", CharacterClassIds.Mágus)
    ];

    public static IReadOnlyList<ClassSpecializationDefinition> ForClass(string characterClassId) => Definitions
        .Where(definition => string.Equals(definition.CharacterClassId, characterClassId,
            StringComparison.OrdinalIgnoreCase)).ToArray();

    public static ClassSpecializationDefinition? Find(string? id) => string.IsNullOrWhiteSpace(id)
        ? null
        : Definitions.FirstOrDefault(definition => string.Equals(definition.Id, id, StringComparison.OrdinalIgnoreCase));
}
