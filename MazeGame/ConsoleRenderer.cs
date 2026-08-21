using System.Text;

namespace MazeGame;

public sealed class ConsoleRenderer
{
    public const int PlayfieldWidth = 170;
    public const int PlayfieldHeight = 44;
    private const int RightBorderX = PlayfieldWidth;
    private const int BottomBorderY = PlayfieldHeight;
    private static readonly Rune FogSymbol = new('░');
    private static readonly Rune PlayerSymbol = new('☻');

    public void DrawInitialState(Maze maze, Player player, FogOfWar fogOfWar)
    {
        Console.Clear();
        DrawPlayfield(maze, fogOfWar);
        DrawFrame();
        DrawCharacterSheet();
        DrawBattleMessage("Találd meg a kijáratot: ⌂");
        DrawPlayer(player.Position);
    }

    public void DrawMovement(Maze maze, FogOfWar fogOfWar, Position previousPosition, Position currentPosition, IReadOnlyList<Position> newlyRevealed, bool hasWon)
    {
        foreach (var position in newlyRevealed) DrawMapCell(maze, fogOfWar, position);
        DrawMapCell(maze, fogOfWar, previousPosition);
        DrawPlayer(currentPosition);
        if (hasWon) DrawBattleMessage("Célba értél! R: új labirintus, Esc: kilépés.");
    }

    public void DrawEnemyMovement(Maze maze, FogOfWar fogOfWar, Position previousPosition, Position currentPosition, Position playerPosition)
    {
        if (previousPosition != playerPosition) DrawMapCell(maze, fogOfWar, previousPosition);
        if (currentPosition != playerPosition) DrawMapCell(maze, fogOfWar, currentPosition);
    }

    public void DrawBattleStarted(Enemy enemy) => DrawBattleMessage($"Csata kezdődik! Ellenfél: {enemy.Name}");

    private static void DrawPlayfield(Maze maze, FogOfWar fogOfWar)
    {
        for (var y = 0; y < maze.Height; y++)
        {
            Console.SetCursorPosition(0, y);
            for (var x = 0; x < maze.Width; x++) DrawMapRune(maze, fogOfWar, new Position(x, y));
        }
    }

    private static void DrawFrame()
    {
        for (var y = 0; y < PlayfieldHeight; y++) WriteAt(RightBorderX, y, "│");
        Console.SetCursorPosition(0, BottomBorderY);
        Console.Write(new string('─', PlayfieldWidth));
        Console.Write('┘');
    }

    private static void DrawCharacterSheet()
    {
        WriteAt(173, 2, "KARAKTERLAP");
        WriteAt(173, 4, "Játékos: ☻");
        WriteAt(173, 6, "Mozgás: nyilak");
        WriteAt(173, 7, "Új pálya: R");
        WriteAt(173, 8, "Kilépés: Esc");
        WriteAt(173, 10, "Ajtó: ╬");
        WriteAt(173, 12, "Láda: ▣");
        WriteAt(173, 13, "Ellenség: ♟");
    }

    private static void DrawBattleMessage(string message)
    {
        WriteAt(2, BottomBorderY + 2, "ÜZENET: " + message.PadRight(160));
        WriteAt(2, BottomBorderY + 3, new string(' ', 166));
    }

    private static void DrawMapCell(Maze maze, FogOfWar fogOfWar, Position position)
    {
        Console.SetCursorPosition(position.X, position.Y);
        DrawMapRune(maze, fogOfWar, position);
    }

    private static void DrawMapRune(Maze maze, FogOfWar fogOfWar, Position position) =>
        WriteRune(fogOfWar.IsRevealed(position) ? maze.GetObjectAt(position)?.Symbol ?? maze.Tiles[position.X, position.Y] : FogSymbol);

    private static void DrawPlayer(Position position)
    {
        Console.SetCursorPosition(position.X, position.Y);
        WriteRune(PlayerSymbol);
    }

    private static void WriteAt(int x, int y, string text)
    {
        Console.SetCursorPosition(x, y);
        Console.Write(text);
    }

    private static void WriteRune(Rune rune) => Console.Write(rune.ToString());
}
