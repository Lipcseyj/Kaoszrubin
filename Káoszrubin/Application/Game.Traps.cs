using KaoszRubin.Application;
using KaoszRubin.Application.Quests;
using KaoszRubin.Combat;
using KaoszRubin.Data;
using KaoszRubin.Domain;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Magic;
using KaoszRubin.Domain.Quests;
using KaoszRubin.Infrastructure;
using KaoszRubin.Infrastructure.Quests;
using KaoszRubin.UI;
using System.Runtime;
using System.Security.Cryptography.Xml;
using static KaoszRubin.UI.GameInput;
using MainMenu = KaoszRubin.UI.MainMenu;

namespace KaoszRubin.Application;

public sealed partial class Game
{
    private bool CanEnterTrap(LiveCharacter character, Position destination)
    {
        var trap = _maze.GetTrapAt(destination);
        if (trap is null || !trap.IsActive) return true;
        if (trap.State == TrapState.Detected)
        {
            ShowTrapMessage($"⚠️ {trap.Definition.Name} zárja el az utat. A mellette álló karakter K-val megpróbálhatja hatástalanítani.",
                ConsoleColor.Yellow, character);
            return false;
        }
        if (!trap.DetectionAttempted)
        {
            trap.MarkDetectionAttempted();
            var chance = TrapDetectionChance(character, trap.Definition);
            if (_random.Next(100) < chance)
            {
                trap.Detect();
                _renderer.DrawMapCellsChanged(_maze, _fogOfWar, _player.Position, [trap.Position]);
                RewardTrapSuccess(character, trap.Definition.DetectionExperience,
                    $"👁️ {character.Name} időben felfedezte: {trap.Definition.Name} ({chance}% esély).",
                    ConsoleColor.Cyan);
                return false;
            }
        }
        return true;
    }

    private static int TrapDetectionChance(LiveCharacter character, TrapDefinition definition) =>
        DungeonTrapService.TrapDetectionChance(character, definition);

    private static int TrapDisarmChance(LiveCharacter character, TrapDefinition definition) =>
        DungeonTrapService.TrapDisarmChance(character, definition);

    private bool TryDisarmAdjacentTrap(LiveCharacter character, Position position)
    {
        var traps = Directions.Select(direction => _maze.GetTrapAt(position + direction))
            .Where(trap => trap is { State: TrapState.Detected }).Cast<MazeTrap>().ToArray();
        if (traps.Length == 0) return false;
        var trap = traps[0];
        var chance = TrapDisarmChance(character, trap.Definition);
        if (_random.Next(100) < chance)
        {
            trap.Disarm();
            ProcessQuestProgressChanges(_questManager.RegisterTrapDisarmed());
            _renderer.DrawMapCellsChanged(_maze, _fogOfWar, _player.Position, [trap.Position]);
            RewardTrapSuccess(character, trap.Definition.DisarmExperience,
                $"🧰 {character.Name} hatástalanította: {trap.Definition.Name} ({chance}% esély).",
                ConsoleColor.Green);
            return true;
        }
        trap.RecordFailedDisarm();
        ShowTrapMessage($"⚠️ {character.Name} nem tudta hatástalanítani: {trap.Definition.Name} ({chance}% esély)." +
                        (trap.FailedDisarmAttempts == 1 ? " A csapda még nem sült el." : string.Empty),
            ConsoleColor.DarkYellow, character);
        if (trap.FailedDisarmAttempts >= 2 && _random.Next(2) == 0) ApplyTrap(character, trap);
        return true;
    }

    private void TriggerTrapAt(LiveCharacter character, Position position)
    {
        if (_maze.GetTrapAt(position) is { IsActive: true } trap) ApplyTrap(character, trap);
    }

    private void ApplyTrap(LiveCharacter character, MazeTrap trap) =>
        _dungeonTrapService.ApplyTrap(character, trap, _difficultyLevel, _maze,
            (c, radius) =>
            {
                foreach (var enemy in _maze.Enemies.Where(enemy => Manhattan(enemy.Position, trap.Position) <= radius))
                    enemy.ConfigureMovement(enemy.MovementProfile, enemy.PatrolDirection, EnemyPursuitState.Pursuing);
            },
            ShowTrapMessage, c => _renderer.RefreshCharacterSheet(c),
            () => _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position));

    private void ShowTrapMessage(string message, ConsoleColor color, LiveCharacter character)
    {
        var now = DateTime.UtcNow;
        if (now < _nextTrapMessageUtc) return;
        _nextTrapMessageUtc = now + TimeSpan.FromSeconds(2);
        _renderer.DrawInventoryMessage(message, color);
        RecordSessionActivity(SessionActivityKind.System, message, color, [character.Id]);
    }

    private void RewardTrapSuccess(LiveCharacter character, int experience, string message, ConsoleColor color)
    {
        var award = AwardExperience(character, experience);
        var levelText = award.Result.LeveledUp
            ? $" Szint: {award.Result.PreviousLevel}→{award.Result.CurrentLevel}."
            : string.Empty;
        ShowTrapMessage($"{message} +{award.Result.GainedExperience} XP.{levelText}", color, character);
        _renderer.RefreshCharacterSheet(character);
        if (!award.Result.LeveledUp || !character.IsAlive) return;
        ResolvePerkOffers(character, award.Result);
        _renderer.RefreshCharacterSheet(character);
    }
}
