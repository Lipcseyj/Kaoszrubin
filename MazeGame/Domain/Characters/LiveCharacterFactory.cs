using MazeGame.Data;

namespace MazeGame.Domain.Characters;

public static class LiveCharacterFactory
{
    public static LiveCharacter Create(string name, RaceDefinition race, CharacterClassDefinition characterClass, PrimaryAbilities rolledAbilities, int vitalityBonus, int manaBonus, GameDataCatalog data)
    {
        if (vitalityBonus is < 1 or > 15 || manaBonus is < 1 or > 15)
            throw new ArgumentOutOfRangeException("A HP- és mannabónusznak 1 és 15 között kell lennie.");

        var finalAbilities = (rolledAbilities + race.AbilityBonuses).Clamp(1, 13);
        if (!finalAbilities.MeetsMinimum(characterClass.MinimumAbilities))
            throw new ArgumentException("A végső képességértékek nem érik el a választott osztály minimumait.", nameof(rolledAbilities));

        return new LiveCharacter(name, race, characterClass, finalAbilities,
            data.GetMinimumVitality(finalAbilities.Health) + vitalityBonus,
            data.GetMinimumMana(finalAbilities.Intelligence) + manaBonus,
            vitalityBonus, manaBonus);
    }
}
