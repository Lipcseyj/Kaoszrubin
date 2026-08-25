using MazeGame.Data;
using MazeGame.Domain.Characters;
using MazeGame.Domain.Inventory;

namespace MazeGame;

internal sealed class DoorInteractionController
{
    private static readonly Direction[] Directions = Enum.GetValues<Direction>();
    private readonly GameDataCatalog _gameData;
    private readonly ConsoleRenderer _renderer;
    private readonly SoundEffects _soundEffects;
    private readonly Random _random;

    public DoorInteractionController(GameDataCatalog gameData, ConsoleRenderer renderer,
        SoundEffects soundEffects, Random random)
    {
        _gameData = gameData;
        _renderer = renderer;
        _soundEffects = soundEffects;
        _random = random;
    }

    public void TryOpenAdjacentDoor(Maze maze, FogOfWar fogOfWar, Player player, LiveCharacter selectedCharacter)
    {
        var door = GetAdjacentDoor(maze, player.Position);
        if (door is null) { _renderer.DrawDoorMessage("Nincs ajtó melletted."); return; }
        if (door.State == DoorState.Open) { _renderer.DrawDoorMessage("Az ajtó már nyitva van."); return; }
        if (door.State == DoorState.Smashed) { _renderer.DrawDoorMessage("A bezúzott ajtónyílás már szabad."); return; }
        if (door.State == DoorState.Closed)
        {
            maze.SetDoorState(door, DoorState.Open);
            RefreshAfterDoorChanged(maze, fogOfWar, player, selectedCharacter, "Kinyitottad az ajtót.", ConsoleColor.Green);
            return;
        }

        if (selectedCharacter.RemoveFromBackpack(MiscItemIds.Key))
        {
            maze.SetDoorState(door, DoorState.Open);
            RefreshAfterDoorChanged(maze, fogOfWar, player, selectedCharacter,
                "A kulcs kinyitotta a zárat és eltört a használat során.", ConsoleColor.Green);
            return;
        }

        var attemptCost = ConsumeLockedDoorAttemptNeeds(selectedCharacter);
        var costMessage = $" Próba ára: 🍖 -{attemptCost.Food}, 💧 -{attemptCost.Water}.";

        if (CharacterClassRules.IsThief(selectedCharacter.CharacterClass.Id))
        {
            var chance = LockpickChance(selectedCharacter.Abilities.Dexterity);
            var roll = _random.Next(1, 101);
            if (roll <= chance)
            {
                maze.SetDoorState(door, DoorState.Open);
                RefreshAfterDoorChanged(maze, fogOfWar, player, selectedCharacter,
                    $"Zárnyitás sikerült: Ügy {selectedCharacter.Abilities.Dexterity}, esély {chance}%, dobás {roll}." + costMessage,
                    ConsoleColor.Green);
                return;
            }
            _renderer.DrawDoorMessage($"Zárnyitás sikertelen: Ügy {selectedCharacter.Abilities.Dexterity}, esély {chance}%, dobás {roll}.", ConsoleColor.Red);
        }

        var strengthRoll = _random.Next(1, 21);
        if (strengthRoll <= selectedCharacter.Abilities.Strength)
        {
            maze.SetDoorState(door, DoorState.Smashed);
            RefreshAfterDoorChanged(maze, fogOfWar, player, selectedCharacter,
                $"Erőpróba sikerült: 1d20({strengthRoll}) ≤ Erő {selectedCharacter.Abilities.Strength}. Az ajtó bezúzva!" + costMessage,
                ConsoleColor.Green);
        }
        else
        {
            _renderer.RefreshCharacterSheet(selectedCharacter);
            _renderer.DrawDoorMessage(
                $"Erőpróba sikertelen: 1d20({strengthRoll}) > Erő {selectedCharacter.Abilities.Strength}. Az ajtó zárva marad." + costMessage,
                ConsoleColor.Red);
        }
    }

    public void TryCloseAdjacentDoor(Maze maze, FogOfWar fogOfWar, Player player, LiveCharacter selectedCharacter)
    {
        var door = GetAdjacentDoor(maze, player.Position);
        if (door is null) { _renderer.DrawDoorMessage("Nincs ajtó melletted."); return; }
        if (door.State == DoorState.Smashed) { _renderer.DrawDoorMessage("A bezúzott ajtó többé nem zárható be.", ConsoleColor.Red); return; }
        if (door.State == DoorState.Locked) { _renderer.DrawDoorMessage("Az ajtó már kulcsra van zárva."); return; }
        if (door.State == DoorState.Closed) { _renderer.DrawDoorMessage("Az ajtó már be van zárva."); return; }
        maze.SetDoorState(door, DoorState.Closed);
        RefreshAfterDoorChanged(maze, fogOfWar, player, selectedCharacter, "Bezártad az ajtót.", ConsoleColor.DarkYellow);
    }

    public void TryLockAdjacentDoor(Maze maze, FogOfWar fogOfWar, Player player, LiveCharacter selectedCharacter)
    {
        var door = GetAdjacentDoor(maze, player.Position);
        if (door is null) { _renderer.DrawDoorMessage("Nincs ajtó melletted."); return; }
        if (door.State == DoorState.Smashed) { _renderer.DrawDoorMessage("A bezúzott ajtó többé nem zárható kulcsra.", ConsoleColor.Red); return; }
        if (door.State == DoorState.Locked) { _renderer.DrawDoorMessage("Az ajtó már kulcsra van zárva."); return; }

        if (selectedCharacter.RemoveFromBackpack(MiscItemIds.Key))
        {
            maze.SetDoorState(door, DoorState.Locked);
            RefreshAfterDoorChanged(maze, fogOfWar, player, selectedCharacter,
                "Kulccsal bezártad az ajtót. A kulcs elveszett.", ConsoleColor.DarkYellow);
            return;
        }
        if (CharacterClassRules.IsThief(selectedCharacter.CharacterClass.Id))
        {
            maze.SetDoorState(door, DoorState.Locked);
            RefreshAfterDoorChanged(maze, fogOfWar, player, selectedCharacter,
                "Tolvajként kulcs nélkül is bezártad az ajtó zárját.", ConsoleColor.DarkYellow);
            return;
        }
        _renderer.DrawDoorMessage("Az ajtó kulcsra zárásához kulcs vagy tolvaj szükséges.", ConsoleColor.Red);
    }

    private static MazeDoor? GetAdjacentDoor(Maze maze, Position playerPosition) => Directions
        .Select(direction => maze.GetDoorAt(playerPosition + direction))
        .FirstOrDefault(door => door is not null);

    private (int Food, int Water) ConsumeLockedDoorAttemptNeeds(LiveCharacter selectedCharacter)
    {
        var rules = _gameData.DoorAttemptRules;
        var food = _random.Next(rules.FoodMinimum, rules.FoodMaximum + 1);
        var water = _random.Next(rules.WaterMinimum, rules.WaterMaximum + 1);
        selectedCharacter.ConsumeFood(food);
        selectedCharacter.ConsumeWater(water);
        selectedCharacter.SynchronizeNeedStatuses(
            _gameData.GetStatus(CharacterStatusIds.Hungry),
            _gameData.GetStatus(CharacterStatusIds.Thirsty));
        return (food, water);
    }

    private void RefreshAfterDoorChanged(Maze maze, FogOfWar fogOfWar, Player player,
        LiveCharacter selectedCharacter, string message, ConsoleColor color)
    {
        fogOfWar.RevealFrom(maze, player.Position);
        _renderer.DrawMapVisibilityChanged(maze, fogOfWar, player.Position);
        _renderer.RefreshCharacterSheet(selectedCharacter);
        _renderer.DrawDoorMessage(message, color);
        _soundEffects.Play(message.Contains("Bezártad", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("bezártad", StringComparison.OrdinalIgnoreCase)
            ? SoundEffect.DoorClose
            : SoundEffect.DoorOpen);
    }

    private static int LockpickChance(int dexterity) => dexterity <= 10
        ? Math.Clamp(dexterity * 10 - 10, 0, 90)
        : Math.Clamp(90 + (dexterity - 10) * 10 / 3, 90, 100);
}