namespace MazeGame.Domain.Characters;

/// <summary>Egy osztály CSV-ben megadott kezdő felszerelése.</summary>
public sealed record StartingEquipmentDefinition(
    string CharacterClassName,
    string? FirstWeaponName,
    string? SecondWeaponName,
    string? ArmorName,
    string? MagicItemName,
    IReadOnlyList<string> BackpackItemNames);
