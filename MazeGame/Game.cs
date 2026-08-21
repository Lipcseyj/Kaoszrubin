using MazeGame.Data;
using MazeGame.Domain.Characters;
using MazeGame.Combat;

namespace MazeGame;

/// <summary>A játék futását és felhasználói bemenetét koordinálja.</summary>
public sealed class Game
{
    private static readonly TimeSpan EnemyMoveInterval = TimeSpan.FromMilliseconds(700);
    private const int VisionRange = 5;
    private static readonly Direction[] Directions = Enum.GetValues<Direction>();
    private const int MazeWidth = ConsoleRenderer.PlayfieldWidth;
    private const int MazeHeight = ConsoleRenderer.PlayfieldHeight;
    private readonly MazeGenerator _generator;
    private readonly ConsoleRenderer _renderer = new();
    private Maze _maze = null!;
    private Player _player = null!;
    private FogOfWar _fogOfWar = null!;
    private readonly Random _random = new();
    private readonly BattleSystem _battleSystem;
    private bool _battleStarted;
    public CharacterRoster CharacterRoster { get; }
    public LiveCharacter SelectedCharacter { get; }

    public Game(GameDataCatalog gameData, CharacterRoster characterRoster, LiveCharacter selectedCharacter)
    {
        CharacterRoster = characterRoster;
        SelectedCharacter = selectedCharacter;
        _generator = new MazeGenerator(new MazeGenerationSettings
        {
            DoubleWidthCorridorChance = 0.80,
            RoomCount = 5,
            MinimumRoomSize = 2,
            MaximumRoomSize = 6,
            TreasureChestCount = 5,
            RoomEnemyCount = 5,
            OutdoorEnemyCount = 10
        }, gameData.GetEnemy("E004"));
        _battleSystem = new BattleSystem(_random);
    }

    public void Run()
    {
        Console.CursorVisible = false;
        StartNewMaze();
        var nextEnemyMove = DateTime.UtcNow + EnemyMoveInterval;
        try
        {
            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var keyInfo = Console.ReadKey(intercept: true);
                    if (IsRevealMapShortcut(keyInfo))
                    {
                        var isMapRevealed = _fogOfWar.ToggleDeveloperReveal();
                        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
                        _renderer.DrawDeveloperMessage(isMapRevealed
                            ? "Fejlesztői mód: teljes térkép felfedve."
                            : "Fejlesztői mód: köd visszaállítva.");
                        continue;
                    }

                    var key = keyInfo.Key;
                    if (key == ConsoleKey.Escape) return;
                    if (key == ConsoleKey.R)
                    {
                        StartNewMaze();
                        nextEnemyMove = DateTime.UtcNow + EnemyMoveInterval;
                        continue;
                    }

                    MovePlayer(key);
                }

                if (!_battleStarted && DateTime.UtcNow >= nextEnemyMove)
                {
                    MoveEnemies();
                    nextEnemyMove = DateTime.UtcNow + EnemyMoveInterval;
                }

                Thread.Sleep(20);
            }
        }
        finally
        {
            Console.CursorVisible = true;
            Console.SetCursorPosition(0, ConsoleRenderer.PlayfieldHeight + 5);
        }
    }

    private void StartNewMaze()
    {
        _maze = _generator.Create(MazeWidth, MazeHeight);
        _player = new Player(_maze.Entrance, SelectedCharacter);
        _fogOfWar = new FogOfWar(_maze.Width, _maze.Height, VisionRange);
        _fogOfWar.RevealFrom(_maze, _player.Position);
        _battleStarted = false;
        _renderer.DrawInitialState(_maze, _player, _fogOfWar);
    }

    private void MovePlayer(ConsoleKey key)
    {
        if (_player.Position == _maze.Exit || !TryGetDirection(key, out var direction)) return;

        var previousPosition = _player.Position;
        if (!_player.TryMove(direction, _maze)) return;

        var newlyRevealed = _fogOfWar.RevealFrom(_maze, _player.Position);
        _renderer.DrawMovement(_maze, _fogOfWar, previousPosition, _player.Position, newlyRevealed, _player.Position == _maze.Exit);
        var enemy = _maze.GetEnemyAt(_player.Position);
        if (enemy is not null) StartBattle(enemy);
    }

    private void MoveEnemies()
    {
        foreach (var enemy in _maze.Enemies.OrderBy(_ => _random.Next()).ToArray())
        {
            var previousPosition = enemy.Position;
            var direction = Directions[_random.Next(Directions.Length)];
            if (!_maze.TryMoveEnemy(enemy, previousPosition + direction)) continue;

            _renderer.DrawEnemyMovement(_maze, _fogOfWar, previousPosition, enemy.Position, _player.Position);
            if (enemy.Position == _player.Position)
            {
                StartBattle(enemy);
                return;
            }
        }
    }

    private void StartBattle(Enemy enemy)
    {
        if (_battleStarted) return;
        _battleStarted = true;
        _renderer.DrawBattleStarted(enemy);
        var result = _battleSystem.Resolve(SelectedCharacter, enemy);
        _renderer.RefreshCharacterSheet(SelectedCharacter);

        if (result.PlayerWon)
        {
            _maze.ReplaceEnemyWithCorpse(enemy);
            _renderer.DrawMapCellAfterBattle(_maze, _fogOfWar, enemy.Position, _player.Position);
            _renderer.DrawBattleResult(result, enemy);
            _battleStarted = false;
            return;
        }

        _renderer.DrawBattleResult(result, enemy);
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

    private static bool IsRevealMapShortcut(ConsoleKeyInfo keyInfo) =>
        keyInfo.Key == ConsoleKey.U &&
        (keyInfo.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Shift)) == (ConsoleModifiers.Control | ConsoleModifiers.Shift);
}
