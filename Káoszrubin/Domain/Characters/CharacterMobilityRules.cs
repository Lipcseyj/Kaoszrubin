using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Domain.Characters;

public enum EncumbranceLevel { Light, Medium, Heavy }

public sealed record CharacterMobilityProfile(
    double EquippedWeight,
    double CarriedWeight,
    double CarryingCapacity,
    EncumbranceLevel Encumbrance,
    EncumbranceLevel CarriedEncumbrance,
    int EncumbranceInitiativePenalty,
    int ClassInitiativeModifier,
    int InitiativeBase,
    int CombatMovementAllowance,
    int ExplorationMovementAllowance,
    double ExplorationDelayMultiplier);

/// <summary>A felszerelésből számított, dobástól független kezdeményezési és mozgási profil.</summary>
public static class CharacterMobilityRules
{
    public const int BaselineMovementAllowance = 3;

    public static CharacterMobilityProfile Evaluate(LiveCharacter character)
    {
        ArgumentNullException.ThrowIfNull(character);
        var abilities = character.EffectiveAbilities;
        var weight = character.ActiveWeapons.Where(weapon => weapon is not null).Sum(weapon => weapon!.Weight) +
                     (character.Armor?.Weight ?? 0);
        var carriedWeight = weight + (character.WeaponSlots[2]?.Weight ?? 0) + character.MagicItems.Where(item => item is not null).Sum(item => item!.Weight) +
                            Enumerable.Range(0, LiveCharacter.MaximumBackpackItemCount).Sum(index =>
                                (character.GetInventoryItem(InventorySlotKind.Backpack, index)?.Weight ?? 0) *
                                character.GetInventoryItemQuantity(InventorySlotKind.Backpack, index));
        return EvaluateEquipment(abilities, character.CharacterClass.Id, weight,
            character.HasPerk(PerkIds.KnightArmorMaster), carriedWeight);
    }

    public static CharacterMobilityProfile EvaluateEquipment(PrimaryAbilities abilities, string characterClassId,
        double equippedWeight, bool hasArmorMaster, double? carriedWeight = null)
    {
        if (equippedWeight < 0) throw new ArgumentOutOfRangeException(nameof(equippedWeight));
        var totalWeight = carriedWeight ?? equippedWeight;
        if (totalWeight < equippedWeight) throw new ArgumentOutOfRangeException(nameof(carriedWeight));
        var capacity = Math.Max(6, abilities.Strength * 4) + abilities.Health + 3;
        var encumbrance = EncumbranceFor(equippedWeight, capacity);
        var carriedEncumbrance = EncumbranceFor(totalWeight, capacity);
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
        if (hasArmorMaster)
        {
            initiativePenalty = Math.Max(0, initiativePenalty - 1);
            movementPenalty = Math.Max(0, movementPenalty - 1);
        }
        var classInitiative = characterClassId switch
        {
            CharacterClassIds.Tolvaj => 2,
            CharacterClassIds.Harcos or CharacterClassIds.Barbár => 1,
            _ => 0
        };
        var classMovement = characterClassId switch
        {
            CharacterClassIds.Tolvaj => 1,
            CharacterClassIds.Barbár => 1,
            _ => 0
        };
        var dexterityMovement = Math.DivRem(Math.Max(0, abilities.Dexterity - 4), 3, out _);
        var movement = Math.Clamp(BaselineMovementAllowance + dexterityMovement + classMovement -
                                  movementPenalty, 1, 6);
        var additionalCarriedPenalty = Math.Max(0,
            MovementPenaltyFor(carriedEncumbrance) - MovementPenaltyFor(encumbrance));
        var explorationMovement = Math.Clamp(movement - additionalCarriedPenalty, 1, 6);
        return new CharacterMobilityProfile(equippedWeight, totalWeight, capacity, encumbrance, carriedEncumbrance,
            initiativePenalty,
            classInitiative, abilities.Dexterity + classInitiative - initiativePenalty,
            movement, explorationMovement, (double)BaselineMovementAllowance / explorationMovement);
    }

    private static EncumbranceLevel EncumbranceFor(double weight, double capacity) =>
        weight * 100 <= capacity * 50 ? EncumbranceLevel.Light :
        weight * 100 <= capacity * 80 ? EncumbranceLevel.Medium : EncumbranceLevel.Heavy;

    private static int MovementPenaltyFor(EncumbranceLevel encumbrance) => encumbrance switch
    {
        EncumbranceLevel.Medium => 1,
        EncumbranceLevel.Heavy => 2,
        _ => 0
    };

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
