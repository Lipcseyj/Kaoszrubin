using KaoszRubin.Domain.Combat;

namespace KaoszRubin.Domain.Characters;

public enum EncumbranceLevel { Light, Medium, Heavy }

public sealed record CharacterMobilityProfile(
    int EquippedWeight,
    int CarryingCapacity,
    EncumbranceLevel Encumbrance,
    int EncumbranceInitiativePenalty,
    int ClassInitiativeModifier,
    int InitiativeBase,
    int CombatMovementAllowance,
    double ExplorationDelayMultiplier);

/// <summary>A felszerelésből számított, dobástól független kezdeményezési és mozgási profil.</summary>
public static class CharacterMobilityRules
{
    public const int BaselineMovementAllowance = 3;

    public static CharacterMobilityProfile Evaluate(LiveCharacter character)
    {
        ArgumentNullException.ThrowIfNull(character);
        var abilities = character.EffectiveAbilities;
        var weight = character.WeaponSlots.Where(weapon => weapon is not null).Sum(weapon => weapon!.Weight) +
                     (character.Armor?.Weight ?? 0);
        var capacity = Math.Max(6, abilities.Strength * 3);
        var encumbrance = weight * 100 <= capacity * 50 ? EncumbranceLevel.Light :
            weight * 100 <= capacity * 80 ? EncumbranceLevel.Medium : EncumbranceLevel.Heavy;
        var armorMaster = character.HasPerk(PerkIds.KnightArmorMaster);
        var initiativePenalty = encumbrance switch
        {
            EncumbranceLevel.Medium => 1,
            EncumbranceLevel.Heavy => 3,
            _ => 0
        };
        var movementPenalty = encumbrance switch
        {
            EncumbranceLevel.Medium => 1,
            EncumbranceLevel.Heavy => 2,
            _ => 0
        };
        if (armorMaster)
        {
            initiativePenalty = Math.Max(0, initiativePenalty - 1);
            movementPenalty = Math.Max(0, movementPenalty - 1);
        }
        var classInitiative = character.CharacterClass.Id switch
        {
            CharacterClassIds.Tolvaj => 2,
            CharacterClassIds.Harcos or CharacterClassIds.Barbár => 1,
            _ => 0
        };
        var classMovement = character.CharacterClass.Id switch
        {
            CharacterClassIds.Tolvaj => 1,
            CharacterClassIds.Barbár => 1,
            _ => 0
        };
        var dexterityMovement = Math.DivRem(Math.Max(0, abilities.Dexterity - 4), 3, out _);
        var movement = Math.Clamp(BaselineMovementAllowance + dexterityMovement + classMovement -
                                  movementPenalty, 1, 6);
        return new CharacterMobilityProfile(weight, capacity, encumbrance, initiativePenalty,
            classInitiative, abilities.Dexterity + classInitiative - initiativePenalty,
            movement, (double)BaselineMovementAllowance / movement);
    }

    public static (int Minimum, int Maximum) ScaleExplorationDelay(LiveCharacter character,
        int minimumMilliseconds, int maximumMilliseconds)
    {
        if (minimumMilliseconds <= 0 || maximumMilliseconds < minimumMilliseconds)
            throw new ArgumentOutOfRangeException(nameof(minimumMilliseconds));
        var multiplier = Evaluate(character).ExplorationDelayMultiplier;
        return (Math.Max(1, (int)Math.Round(minimumMilliseconds * multiplier)),
            Math.Max(1, (int)Math.Round(maximumMilliseconds * multiplier)));
    }
}
