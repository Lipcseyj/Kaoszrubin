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
    private readonly GameDataCatalog _gameData;
    private MazeGenerator _generator = null!;
    private readonly ConsoleRenderer _renderer;
    private Maze _maze = null!;
    private Player _player = null!;
    private FogOfWar _fogOfWar = null!;
    private readonly Random _random = new();
    private readonly BattleSystem _battleSystem;
    private bool _battleStarted;
    private bool _gameOver;
    private DateTime _nextNeedsDrain;
    private int _mazeLevel = 1;
    public CharacterRoster CharacterRoster { get; }
    public LiveCharacter SelectedCharacter { get; }

    public Game(GameDataCatalog gameData, CharacterRoster characterRoster, LiveCharacter selectedCharacter)
    {
        CharacterRoster = characterRoster;
        SelectedCharacter = selectedCharacter;
        _gameData = gameData;
        _renderer = new ConsoleRenderer(gameData, characterRoster.Party);
        _battleSystem = new BattleSystem(_random);
    }

    public void Run()
    {
        Console.CursorVisible = false;
        StartNewMaze();
        var nextEnemyMove = DateTime.UtcNow + EnemyMoveInterval;
        _nextNeedsDrain = DateTime.UtcNow + TimeSpan.FromMinutes(1);
        try
        {
            while (!_gameOver)
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
                    if (IsNewMazeShortcut(keyInfo))
                    {
                        StartNewMaze();
                        nextEnemyMove = DateTime.UtcNow + EnemyMoveInterval;
                        continue;
                    }
                    if (IsLevelUpShortcut(keyInfo))
                    {
                        TriggerDeveloperLevelUp();
                        continue;
                    }
                    if (IsFillPartyShortcut(keyInfo))
                    {
                        FillPartyForDevelopment();
                        continue;
                    }

                    var key = keyInfo.Key;
                    if (key == ConsoleKey.Escape) return;
                    MovePlayer(key);
                }

                if (!_battleStarted && DateTime.UtcNow >= nextEnemyMove)
                {
                    MoveEnemies();
                    nextEnemyMove = DateTime.UtcNow + EnemyMoveInterval;
                }

                if (!_battleStarted && DateTime.UtcNow >= _nextNeedsDrain)
                {
                    DrainNeeds();
                    _nextNeedsDrain = DateTime.UtcNow + TimeSpan.FromMinutes(1);
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
        var configuration = MazeLevelConfigurations.Get(_mazeLevel);
        var enemySpawns = configuration.EnemySpawns
            .Select(spawn => new ResolvedEnemySpawn(_gameData.GetEnemy(spawn.EnemyId), spawn.Count.Roll(_random)))
            .ToList();
        _generator = new MazeGenerator(configuration.CreateGenerationSettings(_random), enemySpawns);
        _maze = _generator.Create(MazeWidth, MazeHeight);
        _player = new Player(_maze.Entrance, SelectedCharacter);
        _fogOfWar = new FogOfWar(_maze.Width, _maze.Height, VisionRange);
        _fogOfWar.RevealFrom(_maze, _player.Position);
        _battleStarted = false;
        _gameOver = false;
        _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
    }

    private void MovePlayer(ConsoleKey key)
    {
        if (_player.Position == _maze.Exit || !TryGetDirection(key, out var direction)) return;

        var previousPosition = _player.Position;
        if (!_player.TryMove(direction, _maze)) return;

        var newlyRevealed = _fogOfWar.RevealFrom(_maze, _player.Position);
        _renderer.DrawMovement(_maze, _fogOfWar, previousPosition, _player.Position, newlyRevealed, _player.Position == _maze.Exit);
        if (_player.Position == _maze.Exit)
        {
            _mazeLevel++;
            StartNewMaze();
            return;
        }
        var chest = _maze.GetTreasureChestAt(_player.Position);
        if (chest is not null)
        {
            var goldAmount = SelectedCharacter.HasPerk(PerkIds.ThiefMasterThief) ? chest.GoldAmount * 2 : chest.GoldAmount;
            SelectedCharacter.AddGold(goldAmount);
            _maze.RemoveTreasureChest(chest);
            _renderer.RefreshCharacterSheet(SelectedCharacter);
            _renderer.DrawTreasureCollected(goldAmount);
        }
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
        var result = _battleSystem.Resolve(SelectedCharacter, enemy, entry =>
        {
            _renderer.DrawBattleRound(entry);
            WaitForBattleContinue();
        });
        _renderer.RefreshCharacterSheet(SelectedCharacter);

        if (result.PlayerWon)
        {
            var experienceResult = AddExperience(enemy.Definition.ExperienceReward);
            _maze.ReplaceEnemyWithCorpse(enemy);
            _renderer.DrawMapCellAfterBattle(_maze, _fogOfWar, enemy.Position, _player.Position);
            _renderer.DrawBattleResult(result, enemy);
            _renderer.DrawExperienceGained(experienceResult);
            _renderer.RefreshCharacterSheet(SelectedCharacter);
            if (experienceResult.LeveledUp)
            {
                ResolvePerkOffers(experienceResult);
                _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
            }
            _battleStarted = false;
            _nextNeedsDrain = DateTime.UtcNow + TimeSpan.FromMinutes(1);
            return;
        }

        _renderer.DrawBattleResult(result, enemy);
        _renderer.DrawGameOver(SelectedCharacter.Name);
        _gameOver = true;
    }

    private void DrainNeeds()
    {
        var foodLoss = 2 + SelectedCharacter.MaximumVitality / 60;
        SelectedCharacter.ConsumeFood(foodLoss);
        var waterLoss = 2;
        if (SelectedCharacter.CurrentVitality < SelectedCharacter.MaximumVitality) waterLoss++;
        if (SelectedCharacter.CurrentVitality * 2 < SelectedCharacter.MaximumVitality) waterLoss++;
        SelectedCharacter.ConsumeWater(waterLoss);
        SelectedCharacter.SynchronizeNeedStatuses(
            _gameData.GetStatus(CharacterStatusIds.Hungry),
            _gameData.GetStatus(CharacterStatusIds.Thirsty));
        _renderer.RefreshCharacterSheet(SelectedCharacter);
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

    private static void WaitForBattleContinue()
    {
        while (Console.ReadKey(intercept: true).Key != ConsoleKey.Spacebar) { }
    }

    private static bool IsRevealMapShortcut(ConsoleKeyInfo keyInfo) =>
        keyInfo.Key == ConsoleKey.U &&
        (keyInfo.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Shift)) == (ConsoleModifiers.Control | ConsoleModifiers.Shift);

    private static bool IsNewMazeShortcut(ConsoleKeyInfo keyInfo) =>
        keyInfo.Key == ConsoleKey.R &&
        (keyInfo.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Shift)) == (ConsoleModifiers.Control | ConsoleModifiers.Shift);

    private static bool IsLevelUpShortcut(ConsoleKeyInfo keyInfo) =>
        keyInfo.Key == ConsoleKey.L &&
        (keyInfo.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Shift)) == (ConsoleModifiers.Control | ConsoleModifiers.Shift);

    private static bool IsFillPartyShortcut(ConsoleKeyInfo keyInfo) =>
        keyInfo.Key == ConsoleKey.Y &&
        (keyInfo.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Shift)) == (ConsoleModifiers.Control | ConsoleModifiers.Shift);

    private void FillPartyForDevelopment()
    {
        if (CharacterRoster.Party.Members.Count >= Party.MaximumSize)
        {
            _renderer.DrawDeveloperMessage("Fejlesztői mód: a parti már teljes (4/4). ");
            return;
        }

        var generator = new RandomCharacterGenerator(_gameData, _random);
        while (CharacterRoster.Party.Members.Count < Party.MaximumSize)
        {
            var member = generator.Create(CharacterRoster.Characters.Select(character => character.Name).ToList());
            CharacterRoster.Add(member);
            CharacterRoster.Party.Add(member);
        }
        _renderer.RefreshCharacterSheet(SelectedCharacter);
        _renderer.DrawDeveloperMessage("Fejlesztői mód: a parti véletlen társakkal feltöltve (4/4).");
    }

    private LevelUpResult AddExperience(int amount) => SelectedCharacter.AddExperience(
        amount,
        _gameData.ExperienceByLevel,
        _gameData.GetVitalityGrowth(SelectedCharacter.Abilities.Health),
        _gameData.GetManaGrowth(SelectedCharacter.Abilities.Intelligence),
        _random);

    private void TriggerDeveloperLevelUp()
    {
        var neededExperience = SelectedCharacter.GetExperienceNeededForNextLevel(_gameData.ExperienceByLevel);
        if (neededExperience <= 0)
        {
            _renderer.DrawDeveloperMessage("Fejlesztői mód: a karakter már elérte a maximális szintet.");
            return;
        }

        var result = AddExperience(neededExperience);
        ResolvePerkOffers(result);
        _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
    }

    private void ResolvePerkOffers(LevelUpResult result)
    {
        var offers = CreatePerkOffers(result);
        var selectedPerks = _renderer.DrawLevelUpScreen(SelectedCharacter, result, offers);
        foreach (var perk in selectedPerks)
            if (SelectedCharacter.AddPerk(perk)) SelectedCharacter.ApplyPerkAcquisitionBonus(perk);
    }

    private IReadOnlyList<PerkOffer> CreatePerkOffers(LevelUpResult result)
    {
        var offers = new List<PerkOffer>();
        var milestones = new[] { 5, 15, 25 };
        for (var tier = 1; tier <= milestones.Length; tier++)
        {
            if (SelectedCharacter.Perks.Any(perk => perk.Tier == tier)) continue;
            var firstLevel = milestones[tier - 1] - 2;
            var lastLevel = milestones[tier - 1] + 2;
            if (result.CurrentLevel < firstLevel) continue;

            int? triggerLevel = null;
            for (var level = Math.Max(result.PreviousLevel + 1, firstLevel); level <= Math.Min(result.CurrentLevel, lastLevel); level++)
            {
                if (level == lastLevel || _random.NextDouble() < 0.40)
                {
                    triggerLevel = level;
                    break;
                }
            }

            // A funkció bevezetése előtt az ablakon túljutott mentések a következő szintlépéskor megkapják a kimaradt választást.
            if (triggerLevel is null && result.CurrentLevel >= lastLevel) triggerLevel = result.CurrentLevel;
            if (triggerLevel is not null)
                offers.Add(new PerkOffer(tier, triggerLevel.Value, _gameData.GetPerkChoices(SelectedCharacter.CharacterClass.Id, tier)));
        }
        return offers;
    }
}
