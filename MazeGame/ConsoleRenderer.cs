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
    private ConsoleColor? _currentForegroundColor;
    private ConsoleColor? _currentBackgroundColor;

    public void DrawInitialState(Maze maze, Player player, FogOfWar fogOfWar)
    {
        ResetColorCache();
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

    public void DrawMapVisibilityChanged(Maze maze, FogOfWar fogOfWar, Position playerPosition)
    {
        for (var y = 0; y < maze.Height; y++)
        for (var x = 0; x < maze.Width; x++) DrawMapCell(maze, fogOfWar, new Position(x, y));
        DrawPlayer(playerPosition);
    }

    public void DrawBattleStarted(Enemy enemy) => DrawBattleMessage($"Csata kezdődik! Ellenfél: {enemy.Name}");
    public void DrawDeveloperMessage(string message) => DrawBattleMessage(message);

    private void DrawPlayfield(Maze maze, FogOfWar fogOfWar)
    {
        for (var y = 0; y < maze.Height; y++)
        {
            Console.SetCursorPosition(0, y);
            for (var x = 0; x < maze.Width; x++) DrawMapRune(maze, fogOfWar, new Position(x, y));
        }
    }

    private void DrawFrame()
    {
        SetColors(ConsoleColor.DarkCyan, ConsoleColor.Black);
        for (var y = 0; y < PlayfieldHeight; y++) WriteAt(RightBorderX, y, "│");
        Console.SetCursorPosition(0, BottomBorderY);
        Console.Write(new string('─', PlayfieldWidth));
        Console.Write('┘');
    }

    private void DrawCharacterSheet()
    {
        SetColors(ConsoleColor.Gray, ConsoleColor.Black);
        WriteAt(173, 2, "KARAKTERLAP");
        WriteAt(173, 4, "Játékos: ☻");
        WriteAt(173, 6, "Mozgás: nyilak");
        WriteAt(173, 7, "Új pálya: R");
        WriteAt(173, 8, "Kilépés: Esc");
        WriteAt(173, 10, "Ajtó: ╬");
        WriteAt(173, 12, "Láda: ▣");
        WriteAt(173, 13, "Ellenség: ♟");
    }

    private void DrawBattleMessage(string message)
    {
        SetColors(ConsoleColor.Gray, ConsoleColor.Black);
        WriteAt(2, BottomBorderY + 2, "ÜZENET: " + message.PadRight(160));
        WriteAt(2, BottomBorderY + 3, new string(' ', 166));
    }

    private void DrawMapCell(Maze maze, FogOfWar fogOfWar, Position position)
    {
        Console.SetCursorPosition(position.X, position.Y);
        DrawMapRune(maze, fogOfWar, position);
    }

    private void DrawMapRune(Maze maze, FogOfWar fogOfWar, Position position) =>
        WriteRuneWithColor(
            fogOfWar.IsVisible(position) ? maze.GetObjectAt(position)?.Symbol ?? maze.Tiles[position.X, position.Y] : FogSymbol,
            fogOfWar.IsVisible(position) ? GetForegroundColor(maze, position) : ConsoleColor.Black,
            fogOfWar.IsVisible(position) ? ConsoleColor.Black : ConsoleColor.DarkBlue);

    private void DrawPlayer(Position position)
    {
        Console.SetCursorPosition(position.X, position.Y);
        WriteRuneWithColor(PlayerSymbol, ConsoleColor.Cyan, ConsoleColor.Black);
    }

    private static void WriteAt(int x, int y, string text)
    {
        Console.SetCursorPosition(x, y);
        Console.Write(text);
    }

    private static ConsoleColor GetForegroundColor(Maze maze, Position position)
    {
        var mapObject = maze.GetObjectAt(position);
        if (mapObject is TreasureChest) return ConsoleColor.Yellow;
        if (mapObject is Enemy) return ConsoleColor.Red;

        return maze.Tiles[position.X, position.Y] switch
        {
            var tile when tile == Maze.Wall => ConsoleColor.DarkGray,
            var tile when tile == Maze.Door => ConsoleColor.DarkYellow,
            var tile when tile == Maze.ExitMarker => ConsoleColor.Green,
            _ => ConsoleColor.Black
        };
    }

    private void WriteRuneWithColor(Rune rune, ConsoleColor foregroundColor, ConsoleColor backgroundColor)
    {
        SetColors(foregroundColor, backgroundColor);
        Console.Write(rune.ToString());
    }

    private void SetColors(ConsoleColor foregroundColor, ConsoleColor backgroundColor)
    {
        if (_currentForegroundColor != foregroundColor)
        {
            Console.ForegroundColor = foregroundColor;
            _currentForegroundColor = foregroundColor;
        }

        if (_currentBackgroundColor != backgroundColor)
        {
            Console.BackgroundColor = backgroundColor;
            _currentBackgroundColor = backgroundColor;
        }
    }

    private void ResetColorCache()
    {
        Console.ResetColor();
        _currentForegroundColor = null;
        _currentBackgroundColor = null;
    }
}
