namespace MazeGame;

/// <summary>A játék futását és felhasználói bemenetét koordinálja.</summary>
public sealed class Game
{
    private const int MazeWidth = 31;
    private const int MazeHeight = 17;
    private readonly MazeGenerator _generator = new();
    private readonly ConsoleRenderer _renderer = new();
    private Maze _maze = null!;
    private Player _player = null!;

    public void Run()
    {
        Console.CursorVisible = false;
        Console.Clear();
        StartNewMaze();
        try
        {
            while (true)
            {
                _renderer.Draw(_maze, _player, _player.Position == _maze.Exit);
                var key = Console.ReadKey(intercept: true).Key;
                if (key == ConsoleKey.Escape) return;
                if (key == ConsoleKey.R) { StartNewMaze(); continue; }
                if (_player.Position != _maze.Exit && TryGetDirection(key, out var direction)) _player.TryMove(direction, _maze);
            }
        }
        finally
        {
            Console.CursorVisible = true;
            Console.SetCursorPosition(0, MazeHeight + 5);
        }
    }

    private void StartNewMaze()
    {
        _maze = _generator.Create(MazeWidth, MazeHeight);
        _player = new Player(_maze.Entrance);
    }

    private static bool TryGetDirection(ConsoleKey key, out Direction direction)
    {
        direction = key switch
        {
            ConsoleKey.UpArrow => Direction.Up,
            ConsoleKey.DownArrow => Direction.Down,
            ConsoleKey.LeftArrow => Direction.Left,
            ConsoleKey.RightArrow => Direction.Right,
            _ => default
        };
        return key is ConsoleKey.UpArrow or ConsoleKey.DownArrow or ConsoleKey.LeftArrow or ConsoleKey.RightArrow;
    }
}
