using KaoszRubin.Domain.Characters;
using KaoszRubin.World;

namespace KaoszRubin.UI;

public interface IDoorInteractionRenderer
{
    bool DrawDoorSmashChoice(LiveCharacter leader, LiveCharacter thief, Maze maze,
        FogOfWar fogOfWar, Position playerPosition);

    void DrawMapCellsChanged(Maze maze, FogOfWar fogOfWar, Position playerPosition,
        IEnumerable<Position> changedPositions);

    void DrawDoorMessage(string message, ConsoleColor color = ConsoleColor.DarkYellow);

    void RefreshCharacterSheet(LiveCharacter character);
}
