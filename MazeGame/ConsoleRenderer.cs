using System.Text;

namespace MazeGame;

public sealed class ConsoleRenderer
{
    public const int PlayfieldWidth = 170;
    public const int PlayfieldHeight = 44;
    private const int RightBorderX = PlayfieldWidth;
    private const int BottomBorderY = PlayfieldHeight;

    // Az emoji (például 🧙) két konzolcellát foglalhat. Ez a rúna pontosan egyet,
    // ezért nem írja felül a szomszédos pályacellát.
    private static readonly Rune PlayerSymbol = new('☻');

    public void DrawInitialState(Maze maze, Player player)
    {
        Console.Clear();
        DrawPlayfield(maze);
        DrawFrame();
        DrawCharacterSheet();
        DrawBattleMessage("Találd meg a kijáratot: ⌂");
        DrawPlayer(player.Position);
    }

    public void DrawMovement(Maze maze, Position previousPosition, Position currentPosition, bool hasWon)
    {
        DrawTile(maze, previousPosition);
        DrawPlayer(currentPosition);

        if (hasWon)
            DrawBattleMessage("Célba értél! R: új labirintus, Esc: kilépés.");
    }

    private static void DrawPlayfield(Maze maze)
    {
        for (var y = 0; y < maze.Height; y++)
        {
            Console.SetCursorPosition(0, y);
            for (var x = 0; x < maze.Width; x++)
                WriteRune(maze.Tiles[x, y]);
        }
    }

    private static void DrawFrame()
    {
        for (var y = 0; y < PlayfieldHeight; y++)
            WriteAt(RightBorderX, y, "│");

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
    }

    private static void DrawBattleMessage(string message)
    {
        WriteAt(2, BottomBorderY + 2, "ÜZENET: " + message.PadRight(160));
        WriteAt(2, BottomBorderY + 3, new string(' ', 166));
    }

    private static void DrawTile(Maze maze, Position position)
    {
        Console.SetCursorPosition(position.X, position.Y);
        WriteRune(maze.Tiles[position.X, position.Y]);
    }

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
