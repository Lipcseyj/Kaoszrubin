namespace MazeGame.Domain.Characters;

/// <summary>Egy osztály CSV-ben megadott kezdő felszerelése.</summary>
public sealed record StartingEquipmentDefinition(
    string CharacterClassId,
    string? FirstWeaponId,
    string? SecondWeaponId,
    string? ArmorId,
    string? MagicItemId,
    IReadOnlyList<string> BackpackItemIds);
