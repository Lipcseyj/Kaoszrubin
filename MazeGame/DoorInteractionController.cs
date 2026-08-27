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

    public void TryOpenAdjacentDoor(Maze maze, FogOfWar fogOfWar, Position actorPosition, Position leaderPosition,
        LiveCharacter selectedCharacter, bool allowPartyAssistanceAndPrompts)
    {
        var door = GetAdjacentDoor(maze, actorPosition);
        if (door is null) { _renderer.DrawDoorMessage("Nincs ajtó melletted."); return; }
        if (door.State == DoorState.Open) { _renderer.DrawDoorMessage("Az ajtó már nyitva van."); return; }
        if (door.State == DoorState.Smashed) { _renderer.DrawDoorMessage("A bezúzott ajtónyílás már szabad."); return; }
        if (door.State == DoorState.Closed)
        {
            maze.SetDoorState(door, DoorState.Open);
            RefreshAfterDoorChanged(maze, fogOfWar, actorPosition, leaderPosition, selectedCharacter,
                "Kinyitottad az ajtót.", ConsoleColor.Green);
            return;
        }

        var assistingThief = !allowPartyAssistanceAndPrompts || CharacterClassRules.IsThief(selectedCharacter.CharacterClass.Id)
            ? null
            : FindNearbyNpcThief(maze, actorPosition);
        var lockHandler = assistingThief?.Character ?? selectedCharacter;
        var isThief = CharacterClassRules.IsThief(lockHandler.CharacterClass.Id);
        var hasKey = lockHandler.Backpack.Any(item =>
            string.Equals(item?.Id, MiscItemIds.Key, StringComparison.OrdinalIgnoreCase));
        var useKey = hasKey && (!isThief || allowPartyAssistanceAndPrompts &&
            _renderer.DrawThiefKeyChoice(lockHandler, maze, fogOfWar, actorPosition));
        if (useKey && lockHandler.RemoveFromBackpack(MiscItemIds.Key))
        {
            maze.SetDoorState(door, DoorState.Open);
            RefreshAfterDoorChanged(maze, fogOfWar, actorPosition, leaderPosition, selectedCharacter,
                assistingThief is null
                    ? "A kulcs kinyitotta a zárat és eltört a használat során."
                    : $"{lockHandler.Name} kulcsa kinyitotta a zárat és eltört a használat során.",
                ConsoleColor.Green);
            return;
        }

        var attemptCost = ConsumeLockedDoorAttemptNeeds(lockHandler);
        var costMessage = $" Próba ára: 🍖 -{attemptCost.Food}, 💧 -{attemptCost.Water}.";

        if (isThief)
        {
            var chance = LockpickChance(lockHandler.Abilities.Dexterity);
            var roll = _random.Next(1, 101);
            if (roll <= chance)
            {
                maze.SetDoorState(door, DoorState.Open);
                RefreshAfterDoorChanged(maze, fogOfWar, actorPosition, leaderPosition, selectedCharacter,
                    $"{(assistingThief is null ? string.Empty : lockHandler.Name + " előrelép. ")}Zárnyitás sikerült: " +
                    $"Ügy {lockHandler.Abilities.Dexterity}, esély {chance}%, dobás {roll}." + costMessage,
                    ConsoleColor.Green);
                return;
            }
            _renderer.DrawDoorMessage($"{(assistingThief is null ? string.Empty : lockHandler.Name + ": ")}Zárnyitás sikertelen: " +
                $"Ügy {lockHandler.Abilities.Dexterity}, esély {chance}%, dobás {roll}." + costMessage,
                ConsoleColor.Red);
            if (assistingThief is not null && !_renderer.DrawDoorSmashChoice(selectedCharacter, lockHandler,
                    maze, fogOfWar, actorPosition))
            {
                _renderer.DrawDoorMessage($"{selectedCharacter.Name} nem próbálta betörni az ajtót. Az ajtó zárva marad.",
                    ConsoleColor.DarkYellow);
                return;
            }
        }

        var strengthRoll = _random.Next(1, 21);
        if (strengthRoll <= selectedCharacter.Abilities.Strength)
        {
            maze.SetDoorState(door, DoorState.Smashed);
            RefreshAfterDoorChanged(maze, fogOfWar, actorPosition, leaderPosition, selectedCharacter,
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

    public void TryCloseAdjacentDoor(Maze maze, FogOfWar fogOfWar, Position actorPosition,
        Position leaderPosition, LiveCharacter selectedCharacter)
    {
        var door = GetAdjacentDoor(maze, actorPosition);
        if (door is null) { _renderer.DrawDoorMessage("Nincs ajtó melletted."); return; }
        if (door.State == DoorState.Smashed) { _renderer.DrawDoorMessage("A bezúzott ajtó többé nem zárható be.", ConsoleColor.Red); return; }
        if (door.State == DoorState.Locked) { _renderer.DrawDoorMessage("Az ajtó már kulcsra van zárva."); return; }
        if (door.State == DoorState.Closed) { _renderer.DrawDoorMessage("Az ajtó már be van zárva."); return; }
        maze.SetDoorState(door, DoorState.Closed);
        RefreshAfterDoorChanged(maze, fogOfWar, actorPosition, leaderPosition, selectedCharacter,
            "Bezártad az ajtót.", ConsoleColor.DarkYellow);
    }

    public void TryCloseOrLockAdjacentDoor(Maze maze, FogOfWar fogOfWar, Position actorPosition,
        Position leaderPosition, LiveCharacter selectedCharacter)
    {
        var door = GetAdjacentDoor(maze, actorPosition);
        if (door is null) { _renderer.DrawDoorMessage("Nincs ajtó melletted."); return; }
        if (door.State == DoorState.Open)
        {
            TryCloseAdjacentDoor(maze, fogOfWar, actorPosition, leaderPosition, selectedCharacter);
            return;
        }
        TryLockAdjacentDoor(maze, fogOfWar, actorPosition, leaderPosition, selectedCharacter);
    }

    public void TryLockAdjacentDoor(Maze maze, FogOfWar fogOfWar, Position actorPosition,
        Position leaderPosition, LiveCharacter selectedCharacter)
    {
        var door = GetAdjacentDoor(maze, actorPosition);
        if (door is null) { _renderer.DrawDoorMessage("Nincs ajtó melletted."); return; }
        if (door.State == DoorState.Smashed) { _renderer.DrawDoorMessage("A bezúzott ajtó többé nem zárható kulcsra.", ConsoleColor.Red); return; }
        if (door.State == DoorState.Locked) { _renderer.DrawDoorMessage("Az ajtó már kulcsra van zárva."); return; }

        if (selectedCharacter.RemoveFromBackpack(MiscItemIds.Key))
        {
            maze.SetDoorState(door, DoorState.Locked);
            RefreshAfterDoorChanged(maze, fogOfWar, actorPosition, leaderPosition, selectedCharacter,
                "Kulccsal bezártad az ajtót. A kulcs elveszett.", ConsoleColor.DarkYellow);
            return;
        }
        if (CharacterClassRules.IsThief(selectedCharacter.CharacterClass.Id))
        {
            maze.SetDoorState(door, DoorState.Locked);
            RefreshAfterDoorChanged(maze, fogOfWar, actorPosition, leaderPosition, selectedCharacter,
                "Tolvajként kulcs nélkül is bezártad az ajtó zárját.", ConsoleColor.DarkYellow);
            return;
        }
        _renderer.DrawDoorMessage("Az ajtó kulcsra zárásához kulcs vagy tolvaj szükséges.", ConsoleColor.Red);
    }

    private static MazeDoor? GetAdjacentDoor(Maze maze, Position playerPosition) => Directions
        .Select(direction => maze.GetDoorAt(playerPosition + direction))
        .FirstOrDefault(door => door is not null);

    private static PartyMemberAvatar? FindNearbyNpcThief(Maze maze, Position leaderPosition) =>
        maze.PartyMembers.Where(member => member.Character.IsAlive &&
                CharacterClassRules.IsThief(member.Character.CharacterClass.Id) &&
                Chebyshev(member.Position, leaderPosition) <= 2)
            .OrderByDescending(member => member.Character.Abilities.Dexterity)
            .FirstOrDefault();

    private static int Chebyshev(Position first, Position second) =>
        Math.Max(Math.Abs(first.X - second.X), Math.Abs(first.Y - second.Y));

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

    private void RefreshAfterDoorChanged(Maze maze, FogOfWar fogOfWar, Position actorPosition,
        Position leaderPosition, LiveCharacter selectedCharacter, string message, ConsoleColor color)
    {
        fogOfWar.RevealFrom(maze, actorPosition);
        _renderer.DrawMapVisibilityChanged(maze, fogOfWar, leaderPosition);
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
