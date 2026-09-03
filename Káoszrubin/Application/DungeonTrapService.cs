using KaoszRubin.Data;
using KaoszRubin.Domain;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Magic;

namespace KaoszRubin.Application;

public sealed class DungeonTrapService
{
    private readonly GameDataCatalog _gameData;
    private readonly Random _random;

    public DungeonTrapService(GameDataCatalog gameData, Random random)
    {
        _gameData = gameData;
        _random = random;
    }

    public static int TrapDetectionChance(LiveCharacter character, TrapDefinition definition) => Math.Clamp(
        35 + (character.EffectiveAbilities.Intelligence + character.EffectiveAbilities.Dexterity) * 3 -
        definition.DetectionDifficulty * 5 +
        (CharacterClassRules.IsThief(character.CharacterClass.Id) ? 30 : 0), 15, 95);

    public static int TrapDisarmChance(LiveCharacter character, TrapDefinition definition) => Math.Clamp(
        30 + character.EffectiveAbilities.Dexterity * 5 - definition.DisarmDifficulty * 6 +
        (CharacterClassRules.IsThief(character.CharacterClass.Id) ? 30 : 0), 10, 95);

    public void ApplyTrap(
        LiveCharacter character,
        MazeTrap trap,
        int difficultyLevel,
        Maze maze,
        Action<LiveCharacter, int> onAlertNearbyEnemies,
        Action<string, ConsoleColor, LiveCharacter> onShowTrapMessage,
        Action<LiveCharacter> onRefreshCharacterSheet,
        Action onDrawMapVisibilityChanged)
    {
        trap.Trigger();
        var scaledDamage = trap.Definition.MaximumDamage == 0 ? 0 :
            _random.Next(trap.Definition.MinimumDamage, trap.Definition.MaximumDamage + 1) + (difficultyLevel - 1) / 3;
        var maximumAllowed = Math.Max(1, character.MaximumVitality / (difficultyLevel <= 4 ? 7 : 4));
        var damage = Math.Min(Math.Min(scaledDamage, maximumAllowed), Math.Max(0, character.CurrentVitality - 1));
        character.ReceiveDamage(damage);
        var extra = string.Empty;
        if (trap.Definition.Effect == TrapEffect.Poison && character.IsAlive &&
            _random.Next(100) < trap.Definition.StatusChancePercent)
        {
            character.AddStatus(_gameData.GetStatus(CharacterStatusIds.Poisoned));
            extra = " Megmérgeződött.";
        }
        else if (trap.Definition.Effect == TrapEffect.Alert)
        {
            onAlertNearbyEnemies(character, 12);
            extra = " A közeli szörnyek felfigyeltek a zajra.";
        }
        else if (trap.Definition.Effect == TrapEffect.Darkness && character.IsAlive)
        {
            character.ApplySpellEffect(new ActiveSpellEffect(trap.Definition.Id,
                ActiveSpellEffectType.VisionBonus, -2, 6));
            extra = " A koromfelhő 6 akcióra 2-vel csökkentette a látótávját.";
        }
        onRefreshCharacterSheet(character);
        onDrawMapVisibilityChanged();
        var damageText = damage > 0 ? $" {character.Name} {damage} sebzést szenvedett." : string.Empty;
        onShowTrapMessage($"💥 Elsült: {trap.Definition.Name}.{damageText}{extra}",
            ConsoleColor.Red, character);
    }
}
