namespace MazeGame.Domain.Combat;

/// <summary>Egy osztály Erő-alapú fegyveres találati bónuszának CSV-ben hangolható küszöbe.</summary>
public sealed record StrengthHitBonusDefinition(string CharacterClassId, int MinimumStrength, int Bonus);
