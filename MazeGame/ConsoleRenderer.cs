namespace MazeGame;

public sealed class ConsoleRenderer
{
    public void Draw(Maze maze, Player player, bool hasWon)
    {
        Console.SetCursorPosition(0, 0);
        Console.WriteLine("LABIRINTUS  |  Mozgás: nyílbillentyűk  |  Új pálya: R  |  Kilépés: Esc");
        Console.WriteLine();
        for (var y = 0; y < maze.Height; y++)
        {
            for (var x = 0; x < maze.Width; x++)
            {
                var position = new Position(x, y);
                Console.Write(position == player.Position ? '@' : position == maze.Exit ? 'X' : maze.IsWalkable(position) ? ' ' : '#');
            }
            Console.WriteLine();
        }
        Console.WriteLine();
        Console.Write(hasWon ? "Célba értél! Nyomj R-t egy új labirintushoz, vagy Esc-et a kilépéshez." : "Találd meg az X-szel jelölt kijáratot.");
    }
}
