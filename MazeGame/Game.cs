using MazeGame.Data;
using MazeGame.Domain.Characters;
using MazeGame.Combat;
using MazeGame.Domain.Inventory;
using MazeGame.Domain.Combat;
using MazeGame.Domain.Magic;
using MazeGame.UI;

namespace MazeGame;

/// <summary>A játék futását és felhasználói bemenetét koordinálja.</summary>
public sealed class Game
{
    private const int ZombieSpeed = 2;
    private const int ZombieMoveIntervalMilliseconds = 700;
    private const int MinimumPartyMoveDelayMilliseconds = 250;
    private const int MaximumPartyMoveDelayMilliseconds = 300;
    private const int CatchUpMoveDelayMilliseconds = 90;
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
    private readonly GameSaveService _gameSaveService;
    private readonly GameSaveData? _loadedState;
    private bool _battleStarted;
    private bool _gameOver;
    private bool _characterSheetFocused;
    private HeldInventoryItem? _heldInventoryItem;
    private DateTime _nextNeedsDrain;
    private readonly Dictionary<Enemy, DateTime> _nextEnemyMoves = [];
    private readonly Dictionary<PartyMemberAvatar, DateTime> _nextPartyMoves = [];
    private readonly List<Position> _leaderTrail = [];
    private bool _partyHoldingPosition;
    private bool _saveAfterBattle;
    private DateTime? _partyScatterUntil;
    private Direction _leaderFacing = Direction.Right;
    private int _mazeLevel = 1;
    private bool _hasRestedThisLevel;
    public CharacterRoster CharacterRoster { get; }
    public LiveCharacter SelectedCharacter { get; }

    public Game(GameDataCatalog gameData, CharacterRoster characterRoster, LiveCharacter selectedCharacter,
        GameSaveService gameSaveService, GameSaveData? loadedState = null)
    {
        CharacterRoster = characterRoster;
        SelectedCharacter = selectedCharacter;
        _gameData = gameData;
        _gameSaveService = gameSaveService;
        _loadedState = loadedState;
        _renderer = new ConsoleRenderer(gameData, characterRoster.Party);
        _battleSystem = new BattleSystem(_random, gameData.MonsterAbilities, gameData.Statuses);
    }

    public void Run()
    {
        Console.CursorVisible = false;
        if (_loadedState is null) StartNewMaze();
        else RestoreGame(_loadedState);
        if (_loadedState is null) _nextNeedsDrain = DateTime.UtcNow + TimeSpan.FromMinutes(1);
        try
        {
            while (!_gameOver)
            {
                if (Console.KeyAvailable)
                {
                    var keyInfo = Console.ReadKey(intercept: true);
                    if (IsHelpShortcut(keyInfo))
                    {
                        ShowInGameHelp();
                        continue;
                    }
                    if (IsSaveGameShortcut(keyInfo))
                    {
                        SaveGame();
                        continue;
                    }
                    if (_renderer.IsSpellInfoPageOpen)
                    {
                        if (keyInfo.Key == ConsoleKey.Escape) _renderer.CloseSpellInfoPage();
                        else if (keyInfo.Key == ConsoleKey.UpArrow) _renderer.MoveSpellInfoSelection(-1);
                        else if (keyInfo.Key == ConsoleKey.DownArrow) _renderer.MoveSpellInfoSelection(1);
                        else if (TryGetQuickSpellIndex(keyInfo, out var spellSlot)) AssignSelectedSpellQuickSlot(spellSlot);
                        else if (keyInfo.Key == ConsoleKey.Enter) CastSelectedSpellInfo();
                        continue;
                    }
                    if (keyInfo.Key == ConsoleKey.V)
                    {
                        BeginExplorationSpellCasting();
                        continue;
                    }
                    if (TryGetQuickSpellIndex(keyInfo, out var quickSpellSlot))
                    {
                        var quickSpell = SelectedCharacter.QuickSpells[quickSpellSlot];
                        if (quickSpell is null)
                            _renderer.DrawInventoryMessage("Ez a varázslat-gyorshely üres.", ConsoleColor.DarkYellow);
                        else
                            BeginExplorationSpellCasting(quickSpell);
                        continue;
                    }
                    if (keyInfo.Key == ConsoleKey.Tab)
                    {
                        if (_characterSheetFocused) CancelHeldInventoryItem();
                        _characterSheetFocused = !_characterSheetFocused;
                        _renderer.SetCharacterSheetFocused(_characterSheetFocused);
                        continue;
                    }
                    if (_characterSheetFocused)
                    {
                        if (keyInfo.Key == ConsoleKey.Escape) { CancelHeldInventoryItem(); return; }
                        if (keyInfo.Key == ConsoleKey.UpArrow) _renderer.MoveCharacterSheetSelection(-1);
                        else if (keyInfo.Key == ConsoleKey.DownArrow) _renderer.MoveCharacterSheetSelection(1);
                        else if (keyInfo.Key == ConsoleKey.LeftArrow) _renderer.MoveDisplayedPartyMember(-1);
                        else if (keyInfo.Key == ConsoleKey.RightArrow) _renderer.MoveDisplayedPartyMember(1);
                        else if (keyInfo.Key == ConsoleKey.D) DropSelectedInventoryItem();
                        else if (keyInfo.Key == ConsoleKey.I) InspectSelectedInventoryItem();
                        else if (keyInfo.Key == ConsoleKey.Enter) UseSelectedInventoryItem();
                        else if (keyInfo.Key == ConsoleKey.Spacebar) GrabOrPlaceInventoryItem();
                        continue;
                    }
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
                        continue;
                    }
                    if (IsTeleportToExitShortcut(keyInfo))
                    {
                        TeleportLeaderNearExit();
                        _player.Character.AddGold(1000);
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
                    if (IsAddLevelOnePartyMemberShortcut(keyInfo))
                    {
                        AddLevelOnePartyMemberForDevelopment();
                        continue;
                    }

                    var key = keyInfo.Key;
                    if (key == ConsoleKey.Escape) return;
                    if (key == ConsoleKey.N) { TryOpenAdjacentDoor(); continue; }
                    if (key == ConsoleKey.Z) { TryCloseAdjacentDoor(); continue; }
                    if (key == ConsoleKey.K) { TryLockAdjacentDoor(); continue; }
                    if (key == ConsoleKey.H) { TogglePartyHoldPosition(); continue; }
                    if (key == ConsoleKey.M) { ScatterPartyTemporarily(); continue; }
                    if (key == ConsoleKey.P) { TryRestParty(); continue; }
                    MovePlayer(key);
                }

                if (!_battleStarted) MoveEnemies();

                if (!_battleStarted) MovePartyMembers();

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
        _hasRestedThisLevel = false;
        var configuration = MazeLevelConfigurations.Get(_mazeLevel);
        ResolvedEnemyEncounter ResolveEncounter(EnemyEncounterConfiguration encounter) => new(
            encounter.GroupCount,
            encounter.Members.Select(member => new ResolvedEnemyGroupMember(
                _gameData.GetEnemy(member.EnemyId), member.Count, member.Role)).ToList(),
            encounter.MovementProfile);
        _generator = new MazeGenerator(configuration.CreateGenerationSettings(_random),
            configuration.RoomEncounters.Select(ResolveEncounter).ToList(),
            configuration.CorridorEncounters.Select(ResolveEncounter).ToList());
        _maze = _generator.Create(MazeWidth, MazeHeight);
        _player = new Player(_maze.Entrance, SelectedCharacter);
        _leaderTrail.Clear();
        _leaderTrail.Add(_player.Position);
        _nextPartyMoves.Clear();
        PlacePartyMembersNear(_player.Position);
        _fogOfWar = new FogOfWar(_maze.Width, _maze.Height, VisionRange);
        _fogOfWar.RevealFrom(_maze, _player.Position);
        foreach (var member in _maze.PartyMembers) _fogOfWar.RevealFrom(_maze, member.Position);
        _battleStarted = false;
        _gameOver = false;
        InitializeEnemyMoveSchedule(DateTime.UtcNow);
        _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
    }

    private void ShowInGameHelp()
    {
        MainMenu.ShowHelp();
        _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
        _renderer.SetCharacterSheetFocused(_characterSheetFocused);
    }

    private void AssignSelectedSpellQuickSlot(int slotIndex)
    {
        var character = _renderer.SpellInfoCharacter;
        var spell = _renderer.GetSelectedSpellInfo();
        if (character is null || spell is null) return;
        if (!character.AssignQuickSpell(slotIndex, spell))
        {
            _renderer.DrawInventoryMessage("Csak memorizált varázslat tehető gyorshelyre.", ConsoleColor.Red);
            return;
        }
        _renderer.RefreshSpellInfoPage();
        _renderer.DrawInventoryMessage($"{spell.Name} hozzárendelve: F{slotIndex + 1}.", ConsoleColor.Cyan);
    }

    private void CastSelectedSpellInfo()
    {
        var character = _renderer.SpellInfoCharacter;
        var spell = _renderer.GetSelectedSpellInfo();
        if (character != SelectedCharacter || spell is null ||
            character.MemorizedSpells.All(candidate => !string.Equals(candidate.Id, spell.Id, StringComparison.OrdinalIgnoreCase)))
        {
            _renderer.DrawInventoryMessage("Csak a partivezér memorizált varázslata süthető el.", ConsoleColor.DarkYellow);
            return;
        }
        _renderer.CloseSpellInfoPage();
        BeginExplorationSpellCasting(spell);
    }

    private void BeginExplorationSpellCasting(SpellDefinition? quickSpell = null)
    {
        var spell = quickSpell;
        if (spell is null)
        {
            spell = _renderer.DrawSpellCastingScreen(SelectedCharacter, inCombat: false);
            RestorePlayfieldAfterSpellModal();
        }
        if (spell is null) return;
        var result = TryCastSpell(spell, inCombat: false, currentEnemy: null);
        if (result is not null)
        {
            _renderer.RefreshBattleStatusRows();
            _renderer.DrawInventoryMessage(result.Message, result.Kind == BattleLogKind.Information ? ConsoleColor.Red : ConsoleColor.Magenta);
        }
    }

    private void RestorePlayfieldAfterSpellModal(Enemy? battleEnemy = null)
    {
        _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
        _renderer.SetCharacterSheetFocused(_characterSheetFocused);
        if (battleEnemy is not null) _renderer.DrawBattleStarted(battleEnemy);
    }

    private void SaveGame()
    {
        CancelHeldInventoryItem();
        try
        {
            var path = _gameSaveService.Save(CreateGameSaveData(), CharacterRoster);
            _renderer.DrawDeveloperMessage($"Játék elmentve: {Path.GetFileName(path)}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _renderer.DrawDeveloperMessage($"A mentés sikertelen: {exception.Message}");
        }
    }

    private GameSaveData CreateGameSaveData()
    {
        var now = DateTime.UtcNow;
        int CharacterIndex(LiveCharacter character) => Enumerable.Range(0, CharacterRoster.Characters.Count)
            .First(index => CharacterRoster.Characters[index] == character);
        var mazeData = new MazeSaveData
        {
            Width = _maze.Width,
            Height = _maze.Height,
            WallCodePoint = _maze.WallRune.Value,
            WallColor = _maze.WallColor,
            LevelName = _maze.LevelName,
            Exit = _maze.Exit,
            StartingRoom = _maze.StartingRoom,
            Rooms = _maze.Rooms.Where(room => room != _maze.StartingRoom).ToList(),
            Doors = _maze.Doors.Select(door => new DoorSaveData(door.Position, door.State)).ToList(),
            Chests = _maze.TreasureChests.Select(chest => new ChestSaveData(chest.Position, chest.GoldAmount)).ToList(),
            Enemies = _maze.Enemies.Select(enemy => new EnemySaveData(enemy.Position, enemy.Definition.Id,
                enemy.CurrentHitPoints, enemy.MovementProfile, enemy.PatrolDirection, enemy.PursuitState,
                Math.Max(0, (int)(_nextEnemyMoves.GetValueOrDefault(enemy, now) - now).TotalMilliseconds),
                enemy.GroupId, enemy.GroupRole)).ToList(),
            Corpses = _maze.Corpses.Select(corpse => new CorpseSaveData(corpse.Position, corpse.FormerName,
                corpse is PartyMemberCorpse partyCorpse ? CharacterIndex(partyCorpse.Character) : null)).ToList(),
            PartyAvatars = _maze.PartyMembers.Select(member => new PartyAvatarSaveData(member.Position, CharacterIndex(member.Character))).ToList(),
            GroundPiles = _maze.GroundItemPiles.Select(pile => new GroundPileSaveData(pile.Position,
                pile.Items.Select(item => new SavedItemReference(item.Category.ToString(), item.Id)).ToList())).ToList()
        };
        for (var y = 0; y < _maze.Height; y++)
        for (var x = 0; x < _maze.Width; x++) mazeData.TileCodePoints.Add(_maze.Tiles[x, y].Value);
        return new GameSaveData
        {
            MainCharacterName = SelectedCharacter.Name,
            MazeLevel = _mazeLevel,
            PlayerPosition = _player.Position,
            LeaderFacing = _leaderFacing,
            LeaderTrail = _leaderTrail.ToList(),
            PartyHoldingPosition = _partyHoldingPosition,
            HasRestedThisLevel = _hasRestedThisLevel,
            ScatterRemainingMilliseconds = _partyScatterUntil is { } scatter
                ? Math.Max(0, (int)(scatter - now).TotalMilliseconds) : 0,
            NeedsDrainRemainingMilliseconds = Math.Max(0, (int)(_nextNeedsDrain - now).TotalMilliseconds),
            EnemyMoveRemainingMilliseconds = _maze.Enemies.Count == 0 ? 0 : _maze.Enemies.Min(enemy =>
                Math.Max(0, (int)(_nextEnemyMoves.GetValueOrDefault(enemy, now) - now).TotalMilliseconds)),
            Maze = mazeData,
            Fog = new FogSaveData
            {
                RevealedPositions = _fogOfWar.GetRevealedPositions().ToList(),
                DeveloperRevealActive = _fogOfWar.IsDeveloperRevealActive
            }
        };
    }

    private void RestoreGame(GameSaveData state)
    {
        if (state.Maze.TileCodePoints.Count != state.Maze.Width * state.Maze.Height)
            throw new InvalidOperationException("A mentett térképrács mérete érvénytelen.");
        _mazeLevel = Math.Max(1, state.MazeLevel);
        var wallRune = state.Maze.WallCodePoint > 0
            ? new System.Text.Rune(state.Maze.WallCodePoint)
            : Maze.Wall;
        _maze = new Maze(state.Maze.Width, state.Maze.Height, wallRune,
            state.Maze.WallColor, state.Maze.LevelName);
        var tileIndex = 0;
        for (var y = 0; y < _maze.Height; y++)
        for (var x = 0; x < _maze.Width; x++) _maze.SetTile(new Position(x, y), new System.Text.Rune(state.Maze.TileCodePoints[tileIndex++]));
        if (state.Maze.StartingRoom is { } startingRoom) _maze.SetStartingRoom(startingRoom);
        foreach (var room in state.Maze.Rooms) _maze.AddRoom(room);
        foreach (var door in state.Maze.Doors) _maze.PlaceDoor(door.Position, door.State);
        _maze.PlaceExit(state.Maze.Exit);
        foreach (var chest in state.Maze.Chests) _maze.AddTreasureChest(new TreasureChest(chest.Position, chest.GoldAmount));
        _nextEnemyMoves.Clear();
        foreach (var savedEnemy in state.Maze.Enemies)
        {
            var enemy = new ConfiguredEnemy(savedEnemy.Position, _gameData.GetEnemy(savedEnemy.DefinitionId));
            enemy.SetCurrentHitPoints(savedEnemy.CurrentHitPoints);
            enemy.ConfigureMovement(savedEnemy.MovementProfile, savedEnemy.PatrolDirection, savedEnemy.PursuitState);
            enemy.ConfigureGroup(savedEnemy.GroupId, savedEnemy.GroupRole);
            _maze.AddEnemy(enemy);
            var remaining = savedEnemy.NextMoveRemainingMilliseconds >= 0
                ? savedEnemy.NextMoveRemainingMilliseconds
                : Math.Max(0, state.EnemyMoveRemainingMilliseconds);
            _nextEnemyMoves[enemy] = DateTime.UtcNow + TimeSpan.FromMilliseconds(remaining);
        }
        foreach (var corpse in state.Maze.Corpses)
        {
            var restored = corpse.PartyCharacterIndex is >= 0 and var characterIndex && characterIndex < CharacterRoster.Characters.Count
                ? new PartyMemberCorpse(corpse.Position, CharacterRoster.Characters[characterIndex])
                : new Corpse(corpse.Position, corpse.FormerName);
            _maze.AddCorpse(restored);
        }
        foreach (var avatar in state.Maze.PartyAvatars)
            if (avatar.CharacterIndex >= 0 && avatar.CharacterIndex < CharacterRoster.Characters.Count)
                _maze.AddPartyMember(new PartyMemberAvatar(avatar.Position, CharacterRoster.Characters[avatar.CharacterIndex]));
        foreach (var pile in state.Maze.GroundPiles)
            foreach (var item in pile.Items) _maze.DropItem(pile.Position, ResolveSavedItem(item));
        _player = new Player(state.PlayerPosition, SelectedCharacter);
        _fogOfWar = new FogOfWar(_maze.Width, _maze.Height, VisionRange);
        _fogOfWar.Restore(state.Fog.RevealedPositions, state.Fog.DeveloperRevealActive);
        _leaderFacing = state.LeaderFacing;
        _leaderTrail.Clear();
        _leaderTrail.AddRange(state.LeaderTrail.Count > 0 ? state.LeaderTrail : [state.PlayerPosition]);
        _partyHoldingPosition = state.PartyHoldingPosition;
        _hasRestedThisLevel = state.HasRestedThisLevel;
        _partyScatterUntil = state.ScatterRemainingMilliseconds > 0
            ? DateTime.UtcNow + TimeSpan.FromMilliseconds(state.ScatterRemainingMilliseconds) : null;
        _nextNeedsDrain = DateTime.UtcNow + TimeSpan.FromMilliseconds(Math.Max(0, state.NeedsDrainRemainingMilliseconds));
        _nextPartyMoves.Clear();
        foreach (var member in _maze.PartyMembers) ScheduleNextPartyMove(member, DateTime.UtcNow);
        _battleStarted = false;
        _gameOver = false;
        _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
        _renderer.DrawDeveloperMessage($"Mentés betöltve: {state.MainCharacterName}, {_mazeLevel}. pálya.");
    }

    private IItemDefinition ResolveSavedItem(SavedItemReference item) => item.Category switch
    {
        nameof(ItemCategory.Weapon) => _gameData.GetWeapon(item.Id),
        nameof(ItemCategory.Armor) => _gameData.GetArmor(item.Id),
        nameof(ItemCategory.MagicItem) => _gameData.GetMagicItem(item.Id),
        nameof(ItemCategory.Miscellaneous) => _gameData.GetItem(item.Id),
        _ => throw new InvalidOperationException($"Ismeretlen mentett tárgykategória: {item.Category}")
    };

    private void TryRestParty()
    {
        if (_hasRestedThisLevel)
        {
            _renderer.DrawDeveloperMessage("Ezen a pályán már pihentetek egyszer.");
            return;
        }
        var room = _maze.Rooms.FirstOrDefault(candidate => candidate.Contains(_player.Position));
        if (room is null)
        {
            _renderer.DrawDeveloperMessage("Pihenni csak egy szoba belsejében lehet.");
            return;
        }
        var livingParty = CharacterRoster.Party.Members.Where(character => character.IsAlive).ToList();
        var everyoneInside = livingParty.All(character => character == SelectedCharacter
            ? room.Contains(_player.Position)
            : _maze.PartyMembers.Any(avatar => avatar.Character == character && room.Contains(avatar.Position)));
        if (!everyoneInside)
        {
            _renderer.DrawDeveloperMessage("Pihenéshez minden élő partitag ugyanabban a szobában legyen.");
            return;
        }
        if (_maze.Enemies.Any(enemy => room.Contains(enemy.Position)))
        {
            _renderer.DrawDeveloperMessage("Ellenség van a szobában; itt nem lehet pihenni.");
            return;
        }
        var roomDoors = _maze.Doors.Where(door => room.InteriorPositions()
            .Any(position => Manhattan(position, door.Position) == 1)).ToList();
        if (roomDoors.Count == 0 || roomDoors.Any(door => door.State != DoorState.Locked))
        {
            _renderer.DrawDeveloperMessage("Pihenéshez a szoba minden ajtaját kulcsra kell zárni.");
            return;
        }

        var summaries = new List<string>();
        foreach (var character in livingParty)
        {
            var before = character.CurrentVitality;
            character.RestoreVitality(_random.Next(1, 11));
            character.SetCurrentResources(character.CurrentVitality, character.MaximumMana);
            var cured = new List<string>();
            var cureChance = Math.Clamp(30 + character.Abilities.Health * 2, 0, 100);
            foreach (var (statusId, name) in new[]
                     {
                         (CharacterStatusIds.Diseased, "betegség"),
                         (CharacterStatusIds.Poisoned, "mérgezés"),
                         (CharacterStatusIds.Bleeding, "vérzés")
                     })
                if (character.HasStatus(statusId) && _random.Next(100) < cureChance && character.RemoveStatus(statusId))
                    cured.Add(name);
            character.ConsumeFood(10);
            character.ConsumeWater(10);
            character.SynchronizeNeedStatuses(_gameData.GetStatus(CharacterStatusIds.Hungry),
                _gameData.GetStatus(CharacterStatusIds.Thirsty));
            summaries.Add($"{character.Name}: +{character.CurrentVitality - before} HP" +
                (cured.Count > 0 ? $", elmúlt: {string.Join(", ", cured)}" : string.Empty));
        }
        _hasRestedThisLevel = true;
        PreparePartySpells();
        foreach (var door in roomDoors) _maze.SetDoorState(door, DoorState.Closed);
        _nextNeedsDrain = DateTime.UtcNow + TimeSpan.FromMinutes(1);
        InitializeEnemyMoveSchedule(DateTime.UtcNow);
        foreach (var member in _maze.PartyMembers) ScheduleNextPartyMove(member, DateTime.UtcNow);
        _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
        _renderer.DrawDeveloperMessage("A parti kipihente magát. " + string.Join("; ", summaries));
    }

    private void PreparePartySpells()
    {
        foreach (var character in CharacterRoster.Party.Members.Where(character => character.IsAlive && character.IsSpellcaster))
            character.SetMemorizedSpells(_renderer.DrawSpellPreparationScreen(character));
    }

    private void MovePlayer(ConsoleKey key)
    {
        if (_player.Position == _maze.Exit || !TryGetDirection(key, out var direction)) return;

        var previousPosition = _player.Position;
        if (!_player.TryMove(direction, _maze)) return;
        _leaderFacing = direction;
        if (_leaderTrail[^1] != _player.Position) _leaderTrail.Add(_player.Position);
        if (_leaderTrail.Count > 256) _leaderTrail.RemoveRange(0, _leaderTrail.Count - 256);

        var newlyRevealed = _fogOfWar.RevealFrom(_maze, _player.Position);
        _renderer.DrawMovement(_maze, _fogOfWar, previousPosition, _player.Position, newlyRevealed, _player.Position == _maze.Exit);
        if (_player.Position == _maze.Exit)
        {
            var completedLevel = _mazeLevel;
            var completion = CompleteLevelAtInn(completedLevel);
            _renderer.DrawLevelCompletionScreen(completedLevel, _gameData.BaseLevelCompletionExperience,
                completion.Results, completion.FallenCharacters);
            var leaderResult = completion.Results.First(result => result.Character == SelectedCharacter).Experience;
            if (leaderResult.LeveledUp) ResolvePerkOffers(leaderResult);
            RunInnMarket(completedLevel);
            RunInnRecruitment();
            PreparePartySpells();
            RunInnRumors(completedLevel);
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

    private void DropSelectedInventoryItem()
    {
        var slot = _renderer.GetSelectedInventorySlot();
        if (slot is null) { _renderer.DrawInventoryMessage("Itt nincs ledobható tárgy.", ConsoleColor.DarkYellow); return; }
        var item = slot.Value.Character.GetInventoryItem(slot.Value.Kind, slot.Value.Index);
        if (item is null) { _renderer.DrawInventoryMessage("A kijelölt hely üres.", ConsoleColor.DarkYellow); return; }
        if (SpellcastingRules.IsSpellcastingFocus(item))
        { _renderer.DrawInventoryMessage($"A(z) {item.Name} a karakterhez kötött varázsfókusz, ezért nem dobható el.", ConsoleColor.Red); return; }
        if (!slot.Value.Character.SetInventoryItem(slot.Value.Kind, slot.Value.Index, null))
        { _renderer.DrawInventoryMessage("A kijelölt tárgy nem távolítható el erről a helyről.", ConsoleColor.Red); return; }
        _maze.DropItem(_player.Position, item);
        _renderer.RefreshCharacterSheet(SelectedCharacter);
        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
        var pileCount = _maze.GetGroundItemPileAt(_player.Position)?.Items.Count ?? 1;
        _renderer.DrawInventoryMessage($"Ledobtad: {item.Name}. A mezőn {pileCount} tárgy van.", ConsoleColor.Cyan);
    }

    private void InspectSelectedInventoryItem()
    {
        var slot = _renderer.GetSelectedInventorySlot();
        if (slot is null && _renderer.GetSelectedPartyMember() is { } partyMember)
        {
            _renderer.DrawInventoryMessage($"{partyMember.Name} — mozgásprofil: {NpcBehaviorName(partyMember.NpcBehavior)}.", partyMember.Color);
            return;
        }
        var item = slot is { } selected ? selected.Character.GetInventoryItem(selected.Kind, selected.Index) : null;
        if (item is null) { _renderer.DrawInventoryMessage("A kijelölt helyen nincs megvizsgálható tárgy.", ConsoleColor.DarkYellow); return; }

        var details = item switch
        {
            Domain.Combat.WeaponDefinition weapon =>
                $"Fegyver | típus: {(weapon.WeaponTypeId is { } typeId ? _gameData.GetWeaponType(typeId).Name : "nincs")} | sebzés: {weapon.Damage?.ToString() ?? "nincs"} | " +
                $"{(weapon.IsTwoHanded ? "kétkezes" : "egykezes")} | kasztok: {AllowedClassNames(weapon.AllowedClassIds)}",
            Domain.Combat.ArmorDefinition armor => $"Páncél | védelem: {armor.Defense?.ToString() ?? "nincs"} | kasztok: {AllowedClassNames(armor.AllowedClassIds)}",
            Domain.Magic.MagicItemDefinition magic =>
                $"Varázstárgy | típus: {MagicItemKindName(magic.Kind)} | hatás: {MagicItemEffectName(magic.Effect)} {magic.EffectValue}" +
                (magic.SpellId is null ? string.Empty : $" | varázslat: {_gameData.Spells.First(spell => spell.Id == magic.SpellId).Name}") +
                (magic.MaximumCharges > 0 ? $" | töltet: {magic.MaximumCharges}" : string.Empty) +
                $" | kasztok: {AllowedClassNames(magic.AllowedClassIds)}",
            MiscItemDefinition misc when SpellcastingRules.IsSpellcastingFocus(misc) =>
                "Karakterhez kötött varázsfókusz | nem mozgatható, nem dobható el és nem kereskedhető",
            MiscItemDefinition misc when misc.Effect != ConsumableEffect.None => $"Használati tárgy | hatás: {ConsumableEffectName(misc.Effect)} {misc.EffectValue}",
            _ => "Általános tárgy"
        };
        var description = string.IsNullOrWhiteSpace(item.Description) ? "Nincs jellemzés." : item.Description;
        _renderer.DrawInventoryMessage($"{item.Name} [{item.Id}] — {details}. Ritkaság: {ItemRarityName(item.Rarity)}; mágikus erő: {item.MagicPower}; alapár: {item.BasePrice} arany. Jellemzés: {description}", RarityColor(item.Rarity));
    }

    private void UseSelectedInventoryItem()
    {
        var slot = _renderer.GetSelectedInventorySlot();
        if (slot is null || slot.Value.Kind != InventorySlotKind.Backpack)
        { _renderer.DrawInventoryMessage("Használható tárgyat a hátizsákban jelölj ki.", ConsoleColor.DarkYellow); return; }
        var selectedItem = slot.Value.Character.GetInventoryItem(slot.Value.Kind, slot.Value.Index);
        if (SpellcastingRules.IsSpellcastingFocus(selectedItem))
        {
            _renderer.DrawSpellInfoPage(slot.Value.Character, 0);
            return;
        }
        if (selectedItem is not MiscItemDefinition item || item.Effect == ConsumableEffect.None)
        { _renderer.DrawInventoryMessage("A kijelölt tárgy közvetlenül nem használható.", ConsoleColor.DarkYellow); return; }

        var character = slot.Value.Character;
        var used = true;
        var result = item.Effect switch
        {
            ConsumableEffect.Food when character.FoodLevel < 100 => UseFood(character, item.EffectValue),
            ConsumableEffect.Water when character.WaterLevel < 100 => UseWater(character, item.EffectValue),
            ConsumableEffect.Heal when character.IsAlive && character.CurrentVitality < character.MaximumVitality => UseHealing(character, item.EffectValue),
            ConsumableEffect.RestoreMana when character.IsAlive && character.UsesMana && character.CurrentMana < character.MaximumMana => UseManaPotion(character, item.EffectValue),
            ConsumableEffect.CurePoison when character.RemoveStatus(CharacterStatusIds.Poisoned) => "a mérgezés megszűnt",
            ConsumableEffect.CureDisease when character.RemoveStatus(CharacterStatusIds.Diseased) => "a betegség megszűnt",
            ConsumableEffect.StopBleeding when character.RemoveStatus(CharacterStatusIds.Bleeding) => "a vérzés elállt",
            _ => string.Empty
        };
        if (string.IsNullOrEmpty(result)) used = false;
        if (!used) { _renderer.DrawInventoryMessage("A tárgy hatására most nincs szükség vagy nem alkalmazható.", ConsoleColor.DarkYellow); return; }

        character.SetInventoryItem(InventorySlotKind.Backpack, slot.Value.Index, null);
        character.SynchronizeNeedStatuses(_gameData.GetStatus(CharacterStatusIds.Hungry), _gameData.GetStatus(CharacterStatusIds.Thirsty));
        _renderer.RefreshCharacterSheet(SelectedCharacter);
        _renderer.DrawInventoryMessage($"{character.Name} használta: {item.Name} — {result}.", ConsoleColor.Green);
    }

    private static string UseFood(LiveCharacter character, int amount)
    {
        var before = character.FoodLevel;
        character.RestoreFood(amount);
        return $"élelem +{character.FoodLevel - before}";
    }

    private static string UseWater(LiveCharacter character, int amount)
    {
        var before = character.WaterLevel;
        character.RestoreWater(amount);
        return $"víz +{character.WaterLevel - before}";
    }

    private static string UseHealing(LiveCharacter character, int amount)
    {
        var before = character.CurrentVitality;
        character.RestoreVitality(amount);
        return $"HP +{character.CurrentVitality - before}";
    }

    private static string UseManaPotion(LiveCharacter character, int amount)
    {
        var before = character.CurrentMana;
        character.RestoreMana(amount);
        return $"manna +{character.CurrentMana - before}";
    }

    private static string ItemRarityName(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Magic => "Varázs",
        ItemRarity.Legendary => "Legendás",
        _ => "Sima"
    };

    private static ConsoleColor RarityColor(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Magic => ConsoleColor.Cyan,
        ItemRarity.Legendary => ConsoleColor.Yellow,
        _ => ConsoleColor.Gray
    };

    private static string ConsumableEffectName(ConsumableEffect effect) => effect switch
    {
        ConsumableEffect.Food => "élelem",
        ConsumableEffect.Water => "víz",
        ConsumableEffect.Heal => "HP",
        ConsumableEffect.RestoreMana => "manna",
        ConsumableEffect.CurePoison => "mérgezés gyógyítása",
        ConsumableEffect.CureDisease => "betegség gyógyítása",
        ConsumableEffect.StopBleeding => "vérzés elállítása",
        _ => "nincs"
    };

    private static string MagicItemKindName(Domain.Magic.MagicItemKind kind) => kind switch
    {
        Domain.Magic.MagicItemKind.Amulet => "amulett",
        Domain.Magic.MagicItemKind.Wand => "varázspálca",
        Domain.Magic.MagicItemKind.Scroll => "varázstekercs",
        _ => "varázsgyűrű"
    };

    private static string MagicItemEffectName(Domain.Magic.MagicItemEffect effect) => effect switch
    {
        Domain.Magic.MagicItemEffect.Initiative => "kezdeményezés",
        Domain.Magic.MagicItemEffect.Hit => "találati próba",
        Domain.Magic.MagicItemEffect.Damage => "sebzés",
        Domain.Magic.MagicItemEffect.Defense => "védelem",
        Domain.Magic.MagicItemEffect.BattleHeal => "csata eleji HP",
        Domain.Magic.MagicItemEffect.BattleMana => "csata eleji manna",
        _ => "varázslattároló"
    };

    private static string NpcBehaviorName(NpcBehavior? behavior) => behavior switch
    {
        NpcBehavior.Defensive => "Defenzív",
        NpcBehavior.Aggressive => "Aggresszív",
        NpcBehavior.Scout => "Felderítő",
        NpcBehavior.Cautious => "Óvatos",
        _ => "inaktív"
    };

    private string AllowedClassNames(IReadOnlySet<string> classIds) => string.Join(", ",
        _gameData.CharacterClasses.Where(characterClass => classIds.Contains(characterClass.Id)).Select(characterClass => characterClass.Name));

    private void GrabOrPlaceInventoryItem()
    {
        var slot = _renderer.GetSelectedInventorySlot();
        if (slot is null) { _renderer.DrawInventoryMessage("Válassz egy felszerelés- vagy hátizsákhelyet.", ConsoleColor.DarkYellow); return; }
        var target = slot.Value;
        if (_heldInventoryItem is null)
        {
            var item = target.Character.GetInventoryItem(target.Kind, target.Index);
            if (item is null) { _renderer.DrawInventoryMessage("A kijelölt hely üres.", ConsoleColor.DarkYellow); return; }
            if (SpellcastingRules.IsSpellcastingFocus(item))
            { _renderer.DrawInventoryMessage($"A(z) {item.Name} a hátizsák első helyéhez kötött, ezért nem mozgatható.", ConsoleColor.Red); return; }
            if (!target.Character.SetInventoryItem(target.Kind, target.Index, null))
            { _renderer.DrawInventoryMessage("A kijelölt tárgy nem mozgatható.", ConsoleColor.Red); return; }
            _heldInventoryItem = new HeldInventoryItem(item, target);
            _renderer.RefreshInventoryRows();
            _renderer.DrawInventoryMessage($"Kézben: {item.Name}. Válassz célhelyet, majd nyomj Space-t.", ConsoleColor.Yellow);
            return;
        }

        var held = _heldInventoryItem;
        if (target == held.Source)
        {
            held.Source.Character.SetInventoryItem(held.Source.Kind, held.Source.Index, held.Item);
            _heldInventoryItem = null;
            _renderer.RefreshCharacterSheet(SelectedCharacter);
            _renderer.DrawInventoryMessage($"A(z) {held.Item.Name} visszakerült az eredeti helyére.", ConsoleColor.DarkYellow);
            return;
        }
        var displaced = target.Character.GetInventoryItem(target.Kind, target.Index);
        var changesByCharacter = new Dictionary<LiveCharacter, List<InventorySlotChange>>();
        AddInventoryChange(changesByCharacter, target.Character, new(target.Kind, target.Index, held.Item));
        AddInventoryChange(changesByCharacter, held.Source.Character, new(held.Source.Kind, held.Source.Index, displaced));
        if (changesByCharacter.Any(entry => !entry.Key.CanApplyInventoryChanges(entry.Value.ToArray())))
        {
            _renderer.DrawInventoryMessage("A felszerelés nem használható ezen a helyen vagy ezzel a kaszttal. A kétkezes fegyver csak az első, üres második fegyverhely mellett viselhető.", ConsoleColor.Red);
            return;
        }
        foreach (var entry in changesByCharacter) entry.Key.ApplyInventoryChanges(entry.Value.ToArray());
        _heldInventoryItem = null;
        _renderer.RefreshCharacterSheet(SelectedCharacter);
        _renderer.DrawInventoryMessage(displaced is null
            ? $"Áthelyezted: {held.Item.Name}."
            : $"Felcserélted: {held.Item.Name} ↔ {displaced.Name}.", ConsoleColor.Green);
    }

    private static void AddInventoryChange(Dictionary<LiveCharacter, List<InventorySlotChange>> changes,
        LiveCharacter character, InventorySlotChange change)
    {
        if (!changes.TryGetValue(character, out var characterChanges))
            changes[character] = characterChanges = [];
        characterChanges.Add(change);
    }

    private void CancelHeldInventoryItem()
    {
        if (_heldInventoryItem is not { } held) return;
        held.Source.Character.SetInventoryItem(held.Source.Kind, held.Source.Index, held.Item);
        _heldInventoryItem = null;
        _renderer.RefreshCharacterSheet(SelectedCharacter);
        _renderer.DrawInventoryMessage($"A(z) {held.Item.Name} visszakerült az eredeti helyére.", ConsoleColor.DarkYellow);
    }

    private void MoveEnemies()
    {
        var now = DateTime.UtcNow;
        foreach (var enemy in _maze.Enemies.Where(enemy => _nextEnemyMoves.GetValueOrDefault(enemy) <= now)
                     .OrderBy(_ => _random.Next()).ToArray())
        {
            ScheduleNextEnemyMove(enemy, now);
            if (enemy.PursuitState == EnemyPursuitState.Undecided &&
                FogOfWar.CanSee(_maze, enemy.Position, _player.Position, VisionRange))
                ResolveEnemyPursuit(enemy);

            Direction? direction = enemy.PursuitState == EnemyPursuitState.Pursuing
                ? FindEnemyStepToward(enemy, _player.Position)
                : enemy.MovementProfile switch
                {
                    EnemyMovementProfile.Stationary => null,
                    EnemyMovementProfile.Patrol => enemy.PatrolDirection,
                    _ => Directions[_random.Next(Directions.Length)]
                };
            if (direction is null) continue;
            if (TryMoveEnemy(enemy, direction.Value))
            {
                if (_battleStarted) return;
                continue;
            }
            if (enemy.PursuitState != EnemyPursuitState.Pursuing && enemy.MovementProfile == EnemyMovementProfile.Patrol)
            {
                enemy.ReversePatrolDirection();
                if (TryMoveEnemy(enemy, enemy.PatrolDirection) && _battleStarted) return;
            }
        }
    }

    private void ResolveEnemyPursuit(Enemy observer)
    {
        var pursue = _random.Next(100) < 60;
        var group = observer.GroupId is null
            ? [observer]
            : _maze.Enemies.Where(enemy => string.Equals(enemy.GroupId, observer.GroupId,
                StringComparison.Ordinal)).ToList();
        foreach (var enemy in group.Where(enemy => enemy.PursuitState == EnemyPursuitState.Undecided))
            enemy.ResolvePursuit(pursue);
    }

    private void InitializeEnemyMoveSchedule(DateTime from)
    {
        _nextEnemyMoves.Clear();
        foreach (var enemy in _maze.Enemies) ScheduleNextEnemyMove(enemy, from);
    }

    private void ScheduleNextEnemyMove(Enemy enemy, DateTime from) =>
        _nextEnemyMoves[enemy] = from + EnemyMoveInterval(enemy);

    private static TimeSpan EnemyMoveInterval(Enemy enemy)
    {
        var speed = Math.Max(1, enemy.Definition.Speed ?? ZombieSpeed);
        return TimeSpan.FromMilliseconds((double)ZombieMoveIntervalMilliseconds * ZombieSpeed / speed);
    }

    private bool TryMoveEnemy(Enemy enemy, Direction direction)
    {
        var previousPosition = enemy.Position;
        var destination = previousPosition + direction;
        if (_maze.GetPartyMemberAt(destination) is { } encounteredMember)
        {
            ResolveNpcBattle(encounteredMember, enemy);
            return true;
        }
        if (!_maze.TryMoveEnemy(enemy, destination)) return false;
        _renderer.DrawEnemyMovement(_maze, _fogOfWar, previousPosition, enemy.Position, _player.Position);
        if (enemy.Position == _player.Position) StartBattle(enemy);
        return true;
    }

    private Direction? FindEnemyStepToward(Enemy enemy, Position target)
    {
        var queue = new Queue<Position>();
        var previous = new Dictionary<Position, Position>();
        queue.Enqueue(enemy.Position);
        previous[enemy.Position] = enemy.Position;
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == target) break;
            foreach (var direction in Directions)
            {
                var next = current + direction;
                if (previous.ContainsKey(next) || !CanEnemyPathThrough(next, target)) continue;
                previous[next] = current;
                queue.Enqueue(next);
            }
        }
        if (!previous.ContainsKey(target)) return null;
        var step = target;
        while (previous[step] != enemy.Position) step = previous[step];
        return Directions.First(direction => enemy.Position + direction == step);
    }

    private bool CanEnemyPathThrough(Position position, Position target)
    {
        if (position == target) return _maze.IsWalkable(position);
        if (!_maze.IsWalkable(position) || position == _maze.Entrance || position == _maze.Exit) return false;
        return _maze.GetObjectAt(position) is null or GroundItemPile or PartyMemberAvatar;
    }

    private void MovePartyMembers()
    {
        var now = DateTime.UtcNow;
        if (_partyScatterUntil is { } scatterUntil && now >= scatterUntil)
        {
            _partyScatterUntil = null;
            _renderer.DrawDeveloperMessage(_partyHoldingPosition
                ? "A szétszóródás véget ért; a parti ismét helyben marad."
                : "A szétszóródás véget ért; a parti folytatja korábbi viselkedését.");
        }
        var isScattering = _partyScatterUntil is not null;
        if (_partyHoldingPosition && !isScattering) return;
        foreach (var member in _maze.PartyMembers.ToArray())
        {
            if (_nextPartyMoves.GetValueOrDefault(member) > now) continue;
            ScheduleNextPartyMove(member, now);
            if (isScattering)
            {
                MovePartyMemberAwayFromLeader(member);
                continue;
            }
            if (CanActivelyAttack(member) && TryResolveAdjacentNpcBattle(member)) continue;
            var previous = member.Position;
            var next = ChoosePartyMemberStep(member);
            if (next is null || !_maze.TryMovePartyMember(member, next.Value, _player.Position)) continue;
            var newlyRevealed = _fogOfWar.RevealFrom(_maze, member.Position);
            _renderer.DrawPartyMemberMovement(_maze, _fogOfWar, previous, member.Position, newlyRevealed, _player.Position);
            if (CanActivelyAttack(member)) TryResolveAdjacentNpcBattle(member);
        }
    }

    private void TogglePartyHoldPosition()
    {
        _partyHoldingPosition = !_partyHoldingPosition;
        if (!_partyHoldingPosition)
            foreach (var member in _maze.PartyMembers) _nextPartyMoves[member] = DateTime.UtcNow;
        _renderer.DrawDeveloperMessage(_partyHoldingPosition
            ? "Partiparancs: minden társ tartsa a helyét."
            : "Partiparancs: a társak folytatják korábbi viselkedésüket.");
    }

    private void ScatterPartyTemporarily()
    {
        _partyScatterUntil = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        foreach (var member in _maze.PartyMembers)
            _nextPartyMoves[member] = DateTime.UtcNow + TimeSpan.FromMilliseconds(_random.Next(0, 100));
        _renderer.DrawDeveloperMessage("Partiparancs: szétszóródás 10 másodpercig; a társak 10 mező távolságra húzódnak.");
    }

    private void MovePartyMemberAwayFromLeader(PartyMemberAvatar member)
    {
        if (Manhattan(member.Position, _player.Position) >= 10) return;
        var target = FindReachablePositions(member, 12)
            .Where(entry => Manhattan(entry.Position, _player.Position) <= 10)
            .OrderByDescending(entry => Manhattan(entry.Position, _player.Position))
            .ThenBy(entry => entry.Distance)
            .FirstOrDefault();
        if (target == default) return;
        var next = FindNextStep(member, [target.Position]);
        if (next is null) return;
        var previous = member.Position;
        if (!_maze.TryMovePartyMember(member, next.Value, _player.Position)) return;
        var newlyRevealed = _fogOfWar.RevealFrom(_maze, member.Position);
        _renderer.DrawPartyMemberMovement(_maze, _fogOfWar, previous, member.Position, newlyRevealed, _player.Position);
    }

    private static bool CanActivelyAttack(PartyMemberAvatar member) =>
        member.Character.NpcBehavior is NpcBehavior.Defensive or NpcBehavior.Aggressive;

    private bool TryResolveAdjacentNpcBattle(PartyMemberAvatar member)
    {
        var enemy = Directions.Select(direction => _maze.GetEnemyAt(member.Position + direction))
            .FirstOrDefault(candidate => candidate is not null);
        if (enemy is null) return false;
        ResolveNpcBattle(member, enemy);
        return true;
    }

    private void ResolveNpcBattle(PartyMemberAvatar member, Enemy enemy)
    {
        if (_battleStarted || !member.Character.IsAlive || !_maze.Enemies.Contains(enemy)) return;
        _battleStarted = true;
        var startingNpcHp = member.Character.CurrentVitality;
        var startingEnemyHp = enemy.CurrentHitPoints;
        var startingStatusIds = member.Character.Statuses.Select(status => status.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = _battleSystem.Resolve(member.Character, enemy, _ => { });
        var needLoss = DrainNeedsAfterBattle(member.Character, enemy.Definition.StrengthTier);
        var newStatusText = member.Character.Statuses
            .Where(status => !startingStatusIds.Contains(status.Id))
            .Select(status => $"{status.Icon} {status.Name}")
            .ToList() is { Count: > 0 } newStatuses
            ? $" Új állapot: {string.Join(", ", newStatuses)}."
            : string.Empty;
        LevelUpResult? leaderLevelUp = null;
        if (result.PlayerWon)
        {
            var experienceAwards = DistributeExperience(member.Character, enemy.Definition.ExperienceReward);
            leaderLevelUp = experienceAwards.FirstOrDefault(award => award.Character == SelectedCharacter)?.Result;
            var experienceResult = experienceAwards.First(award => award.Character == member.Character).Result;
            _maze.ReplaceEnemyWithCorpse(enemy);
            var levelText = experienceResult.LeveledUp
                ? $" Szint: {experienceResult.PreviousLevel}→{experienceResult.CurrentLevel}; +{experienceResult.VitalityGained} max HP" +
                  (experienceResult.ManaGained > 0 ? $"; +{experienceResult.ManaGained} max manna." : ".")
                : string.Empty;
            _renderer.DrawNpcBattleSummary(
                $"{member.Character.Name} automatikus csatában legyőzte {enemy.Name} ellenfelet {result.Rounds} kör alatt. " +
                $"HP: {startingNpcHp}→{member.Character.CurrentVitality}; ellenfél HP: {startingEnemyHp}→0; " +
                $"XP: {FormatExperienceAwards(experienceAwards)}.{levelText} 🍖💧 -{needLoss}.{newStatusText}",
                ConsoleColor.Green);
        }
        else
        {
            _maze.ReplacePartyMemberWithCorpse(member);
            _nextPartyMoves.Remove(member);
            _renderer.DrawNpcBattleSummary(
                $"{member.Character.Name} elesett a(z) {enemy.Name} elleni automatikus csatában {result.Rounds} kör után. " +
                $"HP: {startingNpcHp}→0; ellenfél HP: {startingEnemyHp}→{enemy.CurrentHitPoints}; 🍖💧 -{needLoss}.{newStatusText}",
                ConsoleColor.Red);
        }
        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
        _renderer.RefreshCharacterSheet(SelectedCharacter);
        _battleStarted = false;
        if (leaderLevelUp?.LeveledUp == true)
        {
            ResolvePerkOffers(leaderLevelUp);
            _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
        }
    }

    private void ScheduleNextPartyMove(PartyMemberAvatar member, DateTime from)
    {
        var distance = Manhattan(member.Position, _player.Position);
        var minimumDelay = distance >= 8 ? CatchUpMoveDelayMilliseconds :
            distance >= 5 ? 130 : MinimumPartyMoveDelayMilliseconds;
        var maximumDelay = distance >= 8 ? CatchUpMoveDelayMilliseconds + 30 :
            distance >= 5 ? 170 : MaximumPartyMoveDelayMilliseconds;
        _nextPartyMoves[member] = from + TimeSpan.FromMilliseconds(_random.Next(minimumDelay, maximumDelay + 1));
    }

    private Position? ChoosePartyMemberStep(PartyMemberAvatar member)
    {
        var behavior = member.Character.NpcBehavior ?? NpcBehavior.Defensive;
        var visibleEnemy = _maze.Enemies
            .Where(enemy => FogOfWar.CanSee(_maze, member.Position, enemy.Position, VisionRange))
            .OrderBy(enemy => Manhattan(member.Position, enemy.Position))
            .FirstOrDefault();

        if (behavior == NpcBehavior.Aggressive && visibleEnemy is not null)
        {
            if (Manhattan(member.Position, visibleEnemy.Position) == 1) return null;
            return FindNextStep(member, FreeNeighborsOf(visibleEnemy.Position));
        }

        if (behavior == NpcBehavior.Defensive && visibleEnemy is not null)
        {
            if (Manhattan(member.Position, visibleEnemy.Position) == 1) return null;
            return FindNextStep(member, FreeNeighborsOf(visibleEnemy.Position));
        }

        if (behavior == NpcBehavior.Scout)
        {
            if (visibleEnemy is not null)
                return FollowLeaderTrail(member, minimumLag: 2);
            return ChooseForwardStep(member, maximumLeaderDistance: 10, maximumSearchDistance: 10, avoidNarrowFront: false)
                ?? FollowLeaderTrail(member, minimumLag: 2);
        }

        if (behavior == NpcBehavior.Cautious)
            return FollowLeaderTrail(member, minimumLag: 2);

        if (behavior == NpcBehavior.Aggressive)
            return ChooseForwardStep(member, maximumLeaderDistance: 3, maximumSearchDistance: 4, avoidNarrowFront: true)
                ?? FollowLeaderTrail(member, minimumLag: 2);

        return FollowLeaderTrail(member, minimumLag: 2);
    }

    private Position? FollowLeaderTrail(PartyMemberAvatar member, int minimumLag)
    {
        if (_leaderTrail.Count == 0) return null;
        var partyOrder = Enumerable.Range(0, _maze.PartyMembers.Count)
            .FirstOrDefault(index => _maze.PartyMembers[index] == member);
        var formationLag = Math.Min(1, partyOrder);
        var targetIndex = Math.Max(0, _leaderTrail.Count - 1 - minimumLag - formationLag);
        for (var index = targetIndex; index >= 0; index--)
        {
            var target = _leaderTrail[index];
            if (target == member.Position) return null;
            if (!CanPartyTraverse(member, target)) continue;
            return FindNextStep(member, [target]);
        }
        return null;
    }

    private Position? ChooseForwardStep(PartyMemberAvatar member, int maximumLeaderDistance, int maximumSearchDistance, bool avoidNarrowFront)
    {
        var forward = DirectionOffset(_leaderFacing);
        var reachable = FindReachablePositions(member, maximumSearchDistance)
            .Where(entry => Manhattan(entry.Position, _player.Position) <= maximumLeaderDistance)
            .Select(entry => new
            {
                entry.Position,
                entry.Distance,
                Progress = (entry.Position.X - _player.Position.X) * forward.X + (entry.Position.Y - _player.Position.Y) * forward.Y
            })
            .Where(entry => entry.Progress > 0)
            .Where(entry => !avoidNarrowFront || CountWalkableNeighbors(entry.Position) >= 3)
            .OrderByDescending(entry => entry.Progress)
            .ThenBy(entry => entry.Distance)
            .FirstOrDefault();
        if (reachable is null) return null;
        var step = FindNextStep(member, [reachable.Position]);
        if (avoidNarrowFront && step is { } narrowStep && IsAheadOfLeader(narrowStep) && CountWalkableNeighbors(narrowStep) <= 2)
            return null;
        return step;
    }

    private Position? FindNextStep(PartyMemberAvatar member, IEnumerable<Position> targetPositions)
    {
        var targets = targetPositions.Where(position => CanPartyTraverse(member, position)).ToHashSet();
        if (targets.Count == 0 || targets.Contains(member.Position)) return null;
        var visited = new HashSet<Position> { member.Position };
        var queue = new Queue<(Position Position, Position FirstStep)>();
        foreach (var direction in Directions)
        {
            var next = member.Position + direction;
            if (!CanPartyTraverse(member, next) || !visited.Add(next)) continue;
            queue.Enqueue((next, next));
        }
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (targets.Contains(current.Position)) return current.FirstStep;
            foreach (var direction in Directions)
            {
                var next = current.Position + direction;
                if (!CanPartyTraverse(member, next) || !visited.Add(next)) continue;
                queue.Enqueue((next, current.FirstStep));
            }
        }
        return null;
    }

    private IReadOnlyList<(Position Position, int Distance)> FindReachablePositions(PartyMemberAvatar member, int maximumDistance)
    {
        var result = new List<(Position, int)> { (member.Position, 0) };
        var visited = new HashSet<Position> { member.Position };
        var queue = new Queue<(Position Position, int Distance)>();
        queue.Enqueue((member.Position, 0));
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.Distance >= maximumDistance) continue;
            foreach (var direction in Directions)
            {
                var next = current.Position + direction;
                if (!CanPartyTraverse(member, next) || !visited.Add(next)) continue;
                var distance = current.Distance + 1;
                result.Add((next, distance));
                queue.Enqueue((next, distance));
            }
        }
        return result;
    }

    private IEnumerable<Position> FreeNeighborsOf(Position origin) => Directions
        .Select(direction => origin + direction)
        .Where(position => _maze.IsWalkable(position) && position != _player.Position &&
                           (_maze.GetObjectAt(position) is null or GroundItemPile));

    private bool CanPartyTraverse(PartyMemberAvatar member, Position position)
    {
        if (!_maze.IsWalkable(position) || position == _player.Position) return false;
        var occupant = _maze.GetObjectAt(position);
        return occupant is null or GroundItemPile || occupant == member;
    }

    private int CountWalkableNeighbors(Position position) => Directions.Count(direction => _maze.IsWalkable(position + direction));
    private bool IsAheadOfLeader(Position position)
    {
        var forward = DirectionOffset(_leaderFacing);
        return (position.X - _player.Position.X) * forward.X + (position.Y - _player.Position.Y) * forward.Y > 0;
    }
    private static int Manhattan(Position first, Position second) => Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y);
    private static (int X, int Y) DirectionOffset(Direction direction) => direction switch
    {
        Direction.Up => (0, -1), Direction.Down => (0, 1), Direction.Left => (-1, 0), _ => (1, 0)
    };

    private BattlePlayerAction? ChooseBattlePlayerAction(Enemy enemy)
    {
        while (true)
        {
            _renderer.DrawInventoryMessage("Akció: Space — fegyveres támadás | V — varázslat | F1-F8 — gyorsvarázslat", ConsoleColor.Yellow);
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Spacebar) return null;
            if (IsSaveGameShortcut(key))
            {
                _saveAfterBattle = true;
                _renderer.DrawInventoryMessage("Mentés kérve: a csata lezárása után automatikusan elkészül.", ConsoleColor.Yellow);
                continue;
            }
            if (IsHelpShortcut(key))
            {
                ShowInGameHelp();
                _renderer.DrawBattleStarted(enemy);
                _renderer.RefreshBattleStatusRows();
                continue;
            }

            SpellDefinition? spell = null;
            if (key.Key == ConsoleKey.V)
            {
                spell = _renderer.DrawSpellCastingScreen(SelectedCharacter, inCombat: true);
                RestorePlayfieldAfterSpellModal(enemy);
            }
            else if (TryGetQuickSpellIndex(key, out var slotIndex))
                spell = SelectedCharacter.QuickSpells[slotIndex];
            else
                continue;

            if (spell is null)
            {
                if (key.Key != ConsoleKey.V)
                    _renderer.DrawInventoryMessage("Ez a varázslat-gyorshely üres.", ConsoleColor.DarkYellow);
                continue;
            }
            var attempt = TryCastSpell(spell, inCombat: true, enemy);
            if (attempt is null) continue;
            if (attempt.ConsumesTurn) return new BattlePlayerAction(attempt.Message, attempt.Kind);
            _renderer.DrawInventoryMessage(attempt.Message, ConsoleColor.Red);
        }
    }

    private SpellCastAttempt? TryCastSpell(SpellDefinition spell, bool inCombat, Enemy? currentEnemy)
    {
        if (!SelectedCharacter.IsAlive)
            return new SpellCastAttempt(false, $"{SelectedCharacter.Name} nem képes varázsolni.", BattleLogKind.Information);
        if (!SelectedCharacter.IsSpellcaster)
            return new SpellCastAttempt(false, "Ez az osztály nem használ varázslatokat.", BattleLogKind.Information);
        if (!SpellcastingRules.HasRequiredFocus(SelectedCharacter))
            return new SpellCastAttempt(false, "A varázsláshoz hiányzik a megfelelő fókusztárgy.", BattleLogKind.Information);
        if (SelectedCharacter.MemorizedSpells.All(candidate =>
                !string.Equals(candidate.Id, spell.Id, StringComparison.OrdinalIgnoreCase)))
            return new SpellCastAttempt(false, $"A(z) {spell.Name} nincs memorizálva.", BattleLogKind.Information);
        if (inCombat ? !spell.CanUseInCombat : !spell.CanUseDuringExploration)
            return new SpellCastAttempt(false, $"A(z) {spell.Name} ebben a helyzetben nem használható.", BattleLogKind.Information);
        if (SelectedCharacter.CurrentMana < spell.ManaCost)
            return new SpellCastAttempt(false, $"Nincs elég manna: {spell.Name} {spell.ManaCost} mannát igényel.", BattleLogKind.Information);

        var target = SelectSpellTarget(spell, currentEnemy);
        if (target is null) return null;
        SelectedCharacter.SpendMana(spell.ManaCost);
        _renderer.RefreshBattleStatusRows();

        if (inCombat)
        {
            var failureChance = Math.Clamp(30 - SelectedCharacter.Abilities.Intelligence - SelectedCharacter.Abilities.Dexterity, 0, 100);
            var roll = _random.Next(1, 101);
            if (roll <= failureChance)
                return new SpellCastAttempt(true,
                    $"{SelectedCharacter.Name} varázslata meghiúsul: {spell.Name} — kockázat {failureChance}%, dobás {roll}. -{spell.ManaCost} manna; az akció elveszett.",
                    BattleLogKind.Information);
        }

        var targetText = DescribeSpellTarget(spell, target.Value, currentEnemy);
        return new SpellCastAttempt(true,
            $"{SelectedCharacter.Name} sikeresen elsüti: {spell.Name} → {targetText}. -{spell.ManaCost} manna. A konkrét varázshatás végrehajtóhelye aktiválva.",
            BattleLogKind.PlayerAttack);
    }

    private Position? SelectSpellTarget(SpellDefinition spell, Enemy? currentEnemy)
    {
        if (spell.TargetType is SpellTargetType.Self or SpellTargetType.Party) return _player.Position;
        var candidates = GetValidSpellTargets(spell, currentEnemy).Distinct().ToList();
        var forward = DirectionOffset(_leaderFacing);
        var fallback = new Position(
            Math.Clamp(_player.Position.X + forward.X, 0, _maze.Width - 1),
            Math.Clamp(_player.Position.Y + forward.Y, 0, _maze.Height - 1));
        var cursor = candidates.OrderBy(position => Chebyshev(position, _player.Position)).FirstOrDefault(fallback);
        Position? previous = null;

        while (true)
        {
            var valid = IsValidSpellTarget(spell, cursor, currentEnemy);
            var prompt = $"✥ {spell.Name} — {ConsoleRenderer.SpellTargetName(spell.TargetType)}, táv {spell.Range}" +
                         (spell.AreaRadius > 0 ? $", sugár {spell.AreaRadius}" : string.Empty) +
                         $" | {(valid ? DescribeSpellTarget(spell, cursor, currentEnemy) : "érvénytelen cél")} | Enter: célzás, Tab: következő, Esc: mégse";
            _renderer.DrawSpellTargetCursor(_maze, _fogOfWar, previous, cursor, valid, prompt);
            previous = cursor;
            var key = Console.ReadKey(intercept: true);
            if (IsHelpShortcut(key))
            {
                ShowInGameHelp();
                if (currentEnemy is not null) _renderer.DrawBattleStarted(currentEnemy);
                previous = null;
                continue;
            }
            if (key.Key == ConsoleKey.Escape)
            {
                _renderer.FinishSpellTargeting(_maze, _fogOfWar, _player.Position);
                return null;
            }
            if (key.Key == ConsoleKey.Enter && valid)
            {
                _renderer.FinishSpellTargeting(_maze, _fogOfWar, _player.Position);
                return cursor;
            }
            if (key.Key == ConsoleKey.Tab && candidates.Count > 0)
            {
                var index = candidates.IndexOf(cursor);
                cursor = candidates[(index + 1 + candidates.Count) % candidates.Count];
                continue;
            }
            if (!TryGetDirection(key.Key, out var direction)) continue;
            cursor = spell.TargetType == SpellTargetType.Direction
                ? _player.Position + direction
                : cursor + direction;
            if (!_maze.IsInside(cursor)) cursor = previous.Value;
        }
    }

    private IEnumerable<Position> GetValidSpellTargets(SpellDefinition spell, Enemy? currentEnemy)
    {
        IEnumerable<Position> possible = spell.TargetType switch
        {
            SpellTargetType.Enemy when currentEnemy is not null => [currentEnemy.Position],
            SpellTargetType.Enemy => _maze.Enemies.Where(enemy => enemy.CurrentHitPoints > 0).Select(enemy => enemy.Position),
            SpellTargetType.PartyMember => new[] { _player.Position }.Concat(_maze.PartyMembers
                .Where(member => member.Character.IsAlive).Select(member => member.Position)),
            SpellTargetType.Corpse => _maze.Corpses.OfType<PartyMemberCorpse>().Select(corpse => corpse.Position),
            SpellTargetType.Direction => Directions.Select(direction => _player.Position + direction),
            SpellTargetType.Cell or SpellTargetType.Area =>
                from y in Enumerable.Range(Math.Max(0, _player.Position.Y - spell.Range),
                    Math.Min(_maze.Height - 1, _player.Position.Y + spell.Range) - Math.Max(0, _player.Position.Y - spell.Range) + 1)
                from x in Enumerable.Range(Math.Max(0, _player.Position.X - spell.Range),
                    Math.Min(_maze.Width - 1, _player.Position.X + spell.Range) - Math.Max(0, _player.Position.X - spell.Range) + 1)
                select new Position(x, y),
            _ => []
        };
        return possible.Where(position => IsValidSpellTarget(spell, position, currentEnemy));
    }

    private bool IsValidSpellTarget(SpellDefinition spell, Position position, Enemy? currentEnemy)
    {
        if (!_maze.IsInside(position) || !_fogOfWar.IsVisible(position)) return false;
        var inRange = Chebyshev(_player.Position, position) <= Math.Max(1, spell.Range);
        if (!inRange || spell.RequiresLineOfSight && !FogOfWar.CanSee(_maze, _player.Position, position, Math.Max(1, spell.Range))) return false;
        return spell.TargetType switch
        {
            SpellTargetType.Enemy => currentEnemy is not null
                ? currentEnemy.CurrentHitPoints > 0 && currentEnemy.Position == position
                : _maze.GetEnemyAt(position)?.CurrentHitPoints > 0,
            SpellTargetType.PartyMember => position == _player.Position && SelectedCharacter.IsAlive ||
                                           _maze.PartyMembers.Any(member => member.Position == position && member.Character.IsAlive),
            SpellTargetType.Corpse => _maze.Corpses.OfType<PartyMemberCorpse>().Any(corpse => corpse.Position == position),
            SpellTargetType.Direction => Manhattan(_player.Position, position) == 1,
            SpellTargetType.Cell or SpellTargetType.Area => true,
            _ => false
        };
    }

    private string DescribeSpellTarget(SpellDefinition spell, Position position, Enemy? currentEnemy) => spell.TargetType switch
    {
        SpellTargetType.Self => SelectedCharacter.Name,
        SpellTargetType.Party => "az egész parti",
        SpellTargetType.Enemy when currentEnemy is not null && currentEnemy.Position == position => currentEnemy.Name,
        SpellTargetType.Enemy => _maze.GetEnemyAt(position)?.Name ?? $"({position.X},{position.Y})",
        SpellTargetType.PartyMember when position == _player.Position => SelectedCharacter.Name,
        SpellTargetType.PartyMember => _maze.PartyMembers.FirstOrDefault(member => member.Position == position)?.Character.Name ?? $"({position.X},{position.Y})",
        SpellTargetType.Corpse => _maze.Corpses.OfType<PartyMemberCorpse>().FirstOrDefault(corpse => corpse.Position == position)?.Character.Name ?? $"({position.X},{position.Y})",
        SpellTargetType.Direction => $"{DirectionName(position)} irány",
        _ => $"({position.X},{position.Y})"
    };

    private string DirectionName(Position position) => position.X < _player.Position.X ? "bal" :
        position.X > _player.Position.X ? "jobb" : position.Y < _player.Position.Y ? "fel" : "le";

    private static int Chebyshev(Position first, Position second) =>
        Math.Max(Math.Abs(first.X - second.X), Math.Abs(first.Y - second.Y));

    private void StartBattle(Enemy enemy)
    {
        if (_battleStarted) return;
        if (_renderer.IsSpellInfoPageOpen) _renderer.CloseSpellInfoPage();
        _battleStarted = true;
        _renderer.DrawBattleStarted(enemy);
        var result = _battleSystem.Resolve(SelectedCharacter, enemy, entry =>
        {
            _renderer.DrawBattleRound(entry);
            _renderer.RefreshBattleStatusRows();
            WaitForBattleContinue(enemy);
        }, () => ChooseBattlePlayerAction(enemy));
        var needLoss = DrainNeedsAfterBattle(SelectedCharacter, enemy.Definition.StrengthTier);
        _renderer.RefreshCharacterSheet(SelectedCharacter);

        if (result.PlayerWon)
        {
            var experienceAwards = DistributeExperience(SelectedCharacter, enemy.Definition.ExperienceReward);
            var experienceResult = experienceAwards.First(award => award.Character == SelectedCharacter).Result;
            _maze.ReplaceEnemyWithCorpse(enemy);
            _renderer.DrawMapCellAfterBattle(_maze, _fogOfWar, enemy.Position, _player.Position);
            _renderer.DrawBattleResult(result, enemy);
            _renderer.DrawInventoryMessage($"A csata kifárasztott: 🍖 -{needLoss}, 💧 -{needLoss}.", ConsoleColor.DarkYellow);
            _renderer.DrawExperienceDistribution(FormatExperienceAwards(experienceAwards),
                experienceAwards.Any(award => award.Result.LeveledUp));
            _renderer.RefreshCharacterSheet(SelectedCharacter);
            if (experienceResult.LeveledUp)
            {
                ResolvePerkOffers(experienceResult);
                _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
            }
            if (_saveAfterBattle)
            {
                _saveAfterBattle = false;
                SaveGame();
            }
            InitializeEnemyMoveSchedule(DateTime.UtcNow);
            _battleStarted = false;
            _nextNeedsDrain = DateTime.UtcNow + TimeSpan.FromMinutes(1);
            return;
        }

        _renderer.DrawBattleResult(result, enemy);
        _saveAfterBattle = false;
        _renderer.DrawInventoryMessage($"A csata kifárasztott: 🍖 -{needLoss}, 💧 -{needLoss}.", ConsoleColor.DarkYellow);
        _renderer.DrawGameOver(SelectedCharacter.Name);
        _gameOver = true;
    }

    private void DrainNeeds()
    {
        var hungry = _gameData.GetStatus(CharacterStatusIds.Hungry);
        var thirsty = _gameData.GetStatus(CharacterStatusIds.Thirsty);
        foreach (var character in CharacterRoster.Party.Members.Where(character => character.IsAlive))
        {
            var foodLoss = 2 + character.MaximumVitality / 60;
            character.ConsumeFood(foodLoss);
            var waterLoss = 2;
            if (character.CurrentVitality < character.MaximumVitality) waterLoss++;
            if (character.CurrentVitality * 2 < character.MaximumVitality) waterLoss++;
            character.ConsumeWater(waterLoss);
            character.SynchronizeNeedStatuses(hungry, thirsty);
        }
        _renderer.RefreshCharacterSheet(SelectedCharacter);
    }

    private int DrainNeedsAfterBattle(LiveCharacter character, int monsterTier)
    {
        var loss = _random.Next(1, 6) + Math.Clamp(monsterTier, 1, 5);
        character.ConsumeFood(loss);
        character.ConsumeWater(loss);
        character.SynchronizeNeedStatuses(
            _gameData.GetStatus(CharacterStatusIds.Hungry),
            _gameData.GetStatus(CharacterStatusIds.Thirsty));
        return loss;
    }

    private MazeDoor? GetAdjacentDoor() => Directions
        .Select(direction => _maze.GetDoorAt(_player.Position + direction))
        .FirstOrDefault(door => door is not null);

    private void TryOpenAdjacentDoor()
    {
        var door = GetAdjacentDoor();
        if (door is null) { _renderer.DrawDoorMessage("Nincs ajtó melletted."); return; }
        if (door.State == DoorState.Open) { _renderer.DrawDoorMessage("Az ajtó már nyitva van."); return; }
        if (door.State == DoorState.Smashed) { _renderer.DrawDoorMessage("A bezúzott ajtónyílás már szabad."); return; }
        if (door.State == DoorState.Closed)
        {
            _maze.SetDoorState(door, DoorState.Open);
            RefreshAfterDoorChanged("Kinyitottad az ajtót.", ConsoleColor.Green);
            return;
        }

        if (SelectedCharacter.RemoveFromBackpack(MiscItemIds.Key))
        {
            _maze.SetDoorState(door, DoorState.Open);
            RefreshAfterDoorChanged("A kulcs kinyitotta a zárat és eltört a használat során.", ConsoleColor.Green);
            return;
        }

        if (CharacterClassRules.IsThief(SelectedCharacter.CharacterClass.Id))
        {
            var chance = LockpickChance(SelectedCharacter.Abilities.Dexterity);
            var roll = _random.Next(1, 101);
            if (roll <= chance)
            {
                _maze.SetDoorState(door, DoorState.Open);
                RefreshAfterDoorChanged($"Zárnyitás sikerült: Ügy {SelectedCharacter.Abilities.Dexterity}, esély {chance}%, dobás {roll}.", ConsoleColor.Green);
                return;
            }
            _renderer.DrawDoorMessage($"Zárnyitás sikertelen: Ügy {SelectedCharacter.Abilities.Dexterity}, esély {chance}%, dobás {roll}.", ConsoleColor.Red);
        }

        var strengthRoll = _random.Next(1, 21);
        if (strengthRoll <= SelectedCharacter.Abilities.Strength)
        {
            _maze.SetDoorState(door, DoorState.Smashed);
            RefreshAfterDoorChanged($"Erőpróba sikerült: 1d20({strengthRoll}) ≤ Erő {SelectedCharacter.Abilities.Strength}. Az ajtó bezúzva!", ConsoleColor.Green);
        }
        else
            _renderer.DrawDoorMessage($"Erőpróba sikertelen: 1d20({strengthRoll}) > Erő {SelectedCharacter.Abilities.Strength}. Az ajtó zárva marad.", ConsoleColor.Red);
    }

    private void TryCloseAdjacentDoor()
    {
        var door = GetAdjacentDoor();
        if (door is null) { _renderer.DrawDoorMessage("Nincs ajtó melletted."); return; }
        if (door.State == DoorState.Smashed) { _renderer.DrawDoorMessage("A bezúzott ajtó többé nem zárható be.", ConsoleColor.Red); return; }
        if (door.State == DoorState.Locked) { _renderer.DrawDoorMessage("Az ajtó már kulcsra van zárva."); return; }
        if (door.State == DoorState.Closed) { _renderer.DrawDoorMessage("Az ajtó már be van zárva."); return; }
        _maze.SetDoorState(door, DoorState.Closed);
        RefreshAfterDoorChanged("Bezártad az ajtót.", ConsoleColor.DarkYellow);
    }

    private void TryLockAdjacentDoor()
    {
        var door = GetAdjacentDoor();
        if (door is null) { _renderer.DrawDoorMessage("Nincs ajtó melletted."); return; }
        if (door.State == DoorState.Smashed) { _renderer.DrawDoorMessage("A bezúzott ajtó többé nem zárható kulcsra.", ConsoleColor.Red); return; }
        if (door.State == DoorState.Locked) { _renderer.DrawDoorMessage("Az ajtó már kulcsra van zárva."); return; }

        if (SelectedCharacter.RemoveFromBackpack(MiscItemIds.Key))
        {
            _maze.SetDoorState(door, DoorState.Locked);
            RefreshAfterDoorChanged("Kulccsal bezártad az ajtót. A kulcs elveszett.", ConsoleColor.DarkYellow);
            return;
        }
        if (CharacterClassRules.IsThief(SelectedCharacter.CharacterClass.Id))
        {
            _maze.SetDoorState(door, DoorState.Locked);
            RefreshAfterDoorChanged("Tolvajként kulcs nélkül is bezártad az ajtó zárját.", ConsoleColor.DarkYellow);
            return;
        }
        _renderer.DrawDoorMessage("Az ajtó kulcsra zárásához kulcs vagy tolvaj szükséges.", ConsoleColor.Red);
    }

    private void RefreshAfterDoorChanged(string message, ConsoleColor color)
    {
        _fogOfWar.RevealFrom(_maze, _player.Position);
        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
        _renderer.RefreshCharacterSheet(SelectedCharacter);
        _renderer.DrawDoorMessage(message, color);
    }

    private static int LockpickChance(int dexterity) => dexterity <= 10
        ? Math.Clamp(dexterity * 10 - 10, 0, 90)
        : Math.Clamp(90 + (dexterity - 10) * 10 / 3, 90, 100);

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

    private void WaitForBattleContinue(Enemy enemy)
    {
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Spacebar) return;
            if (IsSaveGameShortcut(key))
            {
                _saveAfterBattle = true;
                _renderer.DrawInventoryMessage("Mentés kérve: a csata lezárása után automatikusan elkészül.", ConsoleColor.Yellow);
                continue;
            }
            if (!IsHelpShortcut(key)) continue;
            ShowInGameHelp();
            _renderer.DrawBattleStarted(enemy);
            _renderer.RefreshBattleStatusRows();
        }
    }

    private static bool IsSaveGameShortcut(ConsoleKeyInfo keyInfo) =>
        keyInfo.Key == ConsoleKey.F9;

    private static bool IsHelpShortcut(ConsoleKeyInfo keyInfo) =>
        keyInfo.Key == ConsoleKey.F1 && (keyInfo.Modifiers & ConsoleModifiers.Shift) != 0;

    private static bool TryGetQuickSpellIndex(ConsoleKeyInfo keyInfo, out int slotIndex)
    {
        slotIndex = keyInfo.Key switch
        {
            ConsoleKey.F1 => 0,
            ConsoleKey.F2 => 1,
            ConsoleKey.F3 => 2,
            ConsoleKey.F4 => 3,
            ConsoleKey.F5 => 4,
            ConsoleKey.F6 => 5,
            ConsoleKey.F7 => 6,
            ConsoleKey.F8 => 7,
            _ => -1
        };
        return slotIndex >= 0 && (keyInfo.Modifiers & ConsoleModifiers.Shift) == 0;
    }

    private static bool IsRevealMapShortcut(ConsoleKeyInfo keyInfo) =>
        keyInfo.Key == ConsoleKey.U &&
        (keyInfo.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Shift)) == (ConsoleModifiers.Control | ConsoleModifiers.Shift);

    private static bool IsNewMazeShortcut(ConsoleKeyInfo keyInfo) =>
        keyInfo.Key == ConsoleKey.R &&
        (keyInfo.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Shift)) == (ConsoleModifiers.Control | ConsoleModifiers.Shift);

    private static bool IsTeleportToExitShortcut(ConsoleKeyInfo keyInfo) =>
        keyInfo.Key == ConsoleKey.E &&
        (keyInfo.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Shift)) == (ConsoleModifiers.Control | ConsoleModifiers.Shift);

    private static bool IsLevelUpShortcut(ConsoleKeyInfo keyInfo) =>
        keyInfo.Key == ConsoleKey.S &&
        (keyInfo.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Shift)) == (ConsoleModifiers.Control | ConsoleModifiers.Shift);

    private static bool IsFillPartyShortcut(ConsoleKeyInfo keyInfo) =>
        keyInfo.Key == ConsoleKey.Y &&
        (keyInfo.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Shift)) == (ConsoleModifiers.Control | ConsoleModifiers.Shift);

    private static bool IsAddLevelOnePartyMemberShortcut(ConsoleKeyInfo keyInfo) =>
        (keyInfo.Key is ConsoleKey.Oem102 or ConsoleKey.Oem8 || keyInfo.KeyChar is 'í' or 'Í') &&
        (keyInfo.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Shift)) == (ConsoleModifiers.Control | ConsoleModifiers.Shift);

    private void TeleportLeaderNearExit()
    {
        Position? destination = Directions
            .Select(direction => _maze.Exit + direction)
            .Where(position => _maze.IsWalkable(position) && _maze.GetObjectAt(position) is null)
            .OrderBy(position => Manhattan(position, _player.Position))
            .Select(position => (Position?)position)
            .FirstOrDefault();
        if (destination is null)
        {
            _renderer.DrawDeveloperMessage("Fejlesztői mód: nincs üres járható mező a kijárat mellett.");
            return;
        }

        _player.TeleportTo(destination.Value);
        _leaderTrail.Clear();
        _leaderTrail.Add(destination.Value);
        _fogOfWar.RevealFrom(_maze, destination.Value);
        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, destination.Value);
        _renderer.DrawDeveloperMessage("Fejlesztői mód: a partyvezér a kijárat mellé teleportált.");
    }

    private sealed record HeldInventoryItem(IItemDefinition Item, InventorySlotReference Source);
    private sealed record SpellCastAttempt(bool ConsumesTurn, string Message, BattleLogKind Kind);

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
        PlacePartyMembersNear(_player.Position);
        foreach (var member in _maze.PartyMembers) _fogOfWar.RevealFrom(_maze, member.Position);
        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
        _renderer.RefreshCharacterSheet(SelectedCharacter);
        _renderer.DrawDeveloperMessage("Fejlesztői mód: a parti véletlen társakkal feltöltve (4/4).");
    }

    private void AddLevelOnePartyMemberForDevelopment()
    {
        if (CharacterRoster.Party.Members.Count >= Party.MaximumSize)
        {
            _renderer.DrawDeveloperMessage("Fejlesztői mód: a parti már teljes (4/4). ");
            return;
        }

        var generator = new RandomCharacterGenerator(_gameData, _random);
        var member = generator.CreateLevelOne(CharacterRoster.Characters.Select(character => character.Name).ToList());
        CharacterRoster.Add(member);
        CharacterRoster.Party.Add(member);
        PlacePartyMembersNear(_player.Position);
        foreach (var avatar in _maze.PartyMembers) _fogOfWar.RevealFrom(_maze, avatar.Position);
        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
        _renderer.RefreshCharacterSheet(SelectedCharacter);
        _renderer.DrawDeveloperMessage($"Fejlesztői mód: {member.Name} ({member.CharacterClass.Name}) 1. szinten csatlakozott. Profil: {NpcBehaviorName(member.NpcBehavior)}.");
    }

    private void PlacePartyMembersNear(Position origin)
    {
        var alreadyPlaced = _maze.PartyMembers.Select(member => member.Character).ToHashSet();
        var companions = CharacterRoster.Party.Members.Where(member => member != SelectedCharacter && member.IsAlive && !alreadyPlaced.Contains(member)).ToList();
        if (companions.Count == 0) return;

        var positions = FindNearbyFreePositions(origin).Take(companions.Count).ToList();
        for (var index = 0; index < Math.Min(companions.Count, positions.Count); index++)
        {
            if (companions[index].NpcBehavior is null) companions[index].SetNpcBehavior(NpcBehavior.Defensive);
            var avatar = new PartyMemberAvatar(positions[index], companions[index]);
            _maze.AddPartyMember(avatar);
            _nextPartyMoves[avatar] = DateTime.UtcNow + TimeSpan.FromMilliseconds(_random.Next(80, MaximumPartyMoveDelayMilliseconds + 1));
        }
    }

    private IEnumerable<Position> FindNearbyFreePositions(Position origin)
    {
        var yielded = new HashSet<Position>();
        if (_maze.StartingRoom is { } startingRoom && startingRoom.Contains(origin))
        {
            foreach (var position in startingRoom.InteriorPositions()
                         .Where(position => position != origin && _maze.GetObjectAt(position) is null && !IsStartingRoomDoorApproach(startingRoom, position))
                         .OrderByDescending(position => Math.Abs(position.X - origin.X) + Math.Abs(position.Y - origin.Y)))
            {
                yielded.Add(position);
                yield return position;
            }
        }

        var visited = new HashSet<Position> { origin };
        var queue = new Queue<Position>();
        queue.Enqueue(origin);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var direction in Directions)
            {
                var next = current + direction;
                if (!visited.Add(next) || !_maze.IsWalkable(next)) continue;
                queue.Enqueue(next);
                if (!yielded.Contains(next) && next != _maze.Entrance && next != _maze.Exit && next != _player.Position && _maze.GetObjectAt(next) is null)
                {
                    yielded.Add(next);
                    yield return next;
                }
            }
        }
    }

    private bool IsStartingRoomDoorApproach(Room room, Position position)
    {
        var rightBoundary = new Position(room.TopLeft.X + room.Width, position.Y);
        if (position.X == room.TopLeft.X + room.Width - 1 && _maze.IsInside(rightBoundary) &&
            _maze.GetDoorAt(rightBoundary) is not null) return true;

        var bottomBoundary = new Position(position.X, room.TopLeft.Y + room.Height);
        return position.Y == room.TopLeft.Y + room.Height - 1 && _maze.IsInside(bottomBoundary) &&
            _maze.GetDoorAt(bottomBoundary) is not null;
    }

    private LevelUpResult AddExperience(int amount) => SelectedCharacter.AddExperience(
        amount,
        _gameData.ExperienceByLevel,
        _gameData.GetVitalityGrowth(SelectedCharacter.Abilities.Health),
        _gameData.GetManaGrowth(SelectedCharacter.Abilities.Intelligence),
        _random);

    private IReadOnlyList<ExperienceAward> DistributeExperience(LiveCharacter winner, int totalExperience)
    {
        var total = Math.Max(0, totalExperience);
        var others = CharacterRoster.Party.Members
            .Where(character => character != winner && character.IsAlive)
            .ToList();
        if (others.Count == 0)
            return [AwardExperience(winner, total)];

        var winnerShare = total * 60 / 100;
        var remainder = total - winnerShare;
        var sharedBase = remainder / others.Count;
        var sharedRemainder = remainder % others.Count;
        var awards = new List<ExperienceAward> { AwardExperience(winner, winnerShare) };
        for (var index = 0; index < others.Count; index++)
            awards.Add(AwardExperience(others[index], sharedBase + (index < sharedRemainder ? 1 : 0)));
        return awards;
    }

    private LevelCompletionOutcome CompleteLevelAtInn(int completedLevel)
    {
        var reward = checked(_gameData.BaseLevelCompletionExperience * completedLevel);
        var fallenCharacters = CharacterRoster.Party.Members.Where(character => !character.IsAlive).ToList();
        foreach (var fallen in fallenCharacters) CharacterRoster.Remove(fallen);
        var results = CharacterRoster.Party.Members
            .Select(character => new LevelCompletionResult(character, AwardExperience(character, reward).Result))
            .ToList();
        foreach (var character in CharacterRoster.Party.Members)
            character.SetCurrentResources(character.MaximumVitality, character.MaximumMana);
        return new LevelCompletionOutcome(results, fallenCharacters);
    }

    private void RunInnMarket(int completedLevel)
    {
        var stock = CreateInnStock(completedLevel).ToList();
        var buybackPrices = AllTradableItems().ToDictionary(item => item.Id,
            item => Math.Max(1, item.BasePrice * _random.Next(40, 71) / 100), StringComparer.OrdinalIgnoreCase);
        var mode = InnMarketMode.Buy;
        var selectedIndex = 0;
        var message = "A kereskedő rád kacsint: „Nézz körül, kalandozó!”";

        while (true)
        {
            var sellOffers = CreateSellOffers(buybackPrices);
            var entryCount = mode == InnMarketMode.Buy ? stock.Count : sellOffers.Count;
            selectedIndex = entryCount == 0 ? 0 : Math.Clamp(selectedIndex, 0, entryCount - 1);
            _renderer.DrawInnMarketScreen(SelectedCharacter, mode, stock, sellOffers, selectedIndex,
                CharacterRoster.Party.Members.Sum(character => character.Backpack.Count(item => item is null)), message);

            var key = Console.ReadKey(intercept: true).Key;
            if (key == ConsoleKey.Escape) return;
            if (key is ConsoleKey.LeftArrow or ConsoleKey.RightArrow or ConsoleKey.Tab)
            {
                mode = mode == InnMarketMode.Buy ? InnMarketMode.Sell : InnMarketMode.Buy;
                selectedIndex = 0;
                message = mode == InnMarketMode.Buy ? "A kereskedő kínálata." : "Csak a hátizsákok tárgyai adhatók el.";
                continue;
            }
            if (key == ConsoleKey.UpArrow && entryCount > 0) { selectedIndex = (selectedIndex - 1 + entryCount) % entryCount; continue; }
            if (key == ConsoleKey.DownArrow && entryCount > 0) { selectedIndex = (selectedIndex + 1) % entryCount; continue; }
            if (key != ConsoleKey.Enter || entryCount == 0) continue;

            if (mode == InnMarketMode.Buy)
            {
                var offer = stock[selectedIndex];
                var recipient = CharacterRoster.Party.Members.FirstOrDefault(character => character.Backpack.Any(item => item is null));
                if (recipient is null) { message = "🎒 A parti összes hátizsákja tele van."; continue; }
                if (!SelectedCharacter.SpendGold(offer.Price)) { message = $"💰 Nincs elég aranyad: még {offer.Price - SelectedCharacter.Gold} hiányzik."; continue; }
                recipient.AddToBackpack(offer.Item);
                stock.RemoveAt(selectedIndex);
                message = $"✅ Megvetted: {offer.Item.Name} → {recipient.Name} hátizsákja ({offer.Price} arany).";
            }
            else
            {
                var offer = sellOffers[selectedIndex];
                if (!offer.Owner.SetInventoryItem(InventorySlotKind.Backpack, offer.BackpackIndex, null))
                { message = "Az üzlet most nem hajtható végre."; continue; }
                SelectedCharacter.AddGold(offer.Price);
                message = $"✅ Eladtad: {offer.Item.Name} {offer.Price} aranyért ({offer.Owner.Name} hátizsákjából).";
            }
        }
    }

    private IReadOnlyList<InnStockOffer> CreateInnStock(int completedLevel)
    {
        var allItems = AllTradableItems().Where(item => item.Rarity != ItemRarity.Legendary).OrderBy(item => item.BasePrice).ToList();
        var unlocked = allItems.Take(Math.Min(allItems.Count, 8 + completedLevel * 8)).ToList();
        var stockCount = Math.Min(unlocked.Count, Math.Min(12, 5 + completedLevel));
        var offers = new List<InnStockOffer>(stockCount);
        while (offers.Count < stockCount && unlocked.Count > 0)
        {
            var totalWeight = unlocked.Select((_, index) => 1 + index * Math.Max(1, completedLevel)).Sum();
            var roll = _random.Next(totalWeight);
            var chosenIndex = 0;
            for (; chosenIndex < unlocked.Count - 1; chosenIndex++)
            {
                roll -= 1 + chosenIndex * Math.Max(1, completedLevel);
                if (roll < 0) break;
            }
            var item = unlocked[chosenIndex];
            unlocked.RemoveAt(chosenIndex);
            var percentage = _random.NextDouble() < 0.20 ? _random.Next(85, 101) : _random.Next(105, 151);
            offers.Add(new InnStockOffer(item, Math.Max(1, item.BasePrice * percentage / 100)));
        }
        var legendaryChance = Math.Min(0.08, 0.01 + completedLevel * 0.005);
        if (_random.NextDouble() < legendaryChance)
        {
            var legendaryPool = AllTradableItems().Where(item => item.Rarity == ItemRarity.Legendary)
                .OrderBy(item => item.BasePrice).Take(Math.Min(40, Math.Max(4, completedLevel * 4))).ToList();
            if (legendaryPool.Count > 0)
            {
                if (offers.Count >= 12) offers.RemoveAt(_random.Next(offers.Count));
                var legendary = legendaryPool[_random.Next(legendaryPool.Count)];
                offers.Add(new InnStockOffer(legendary, legendary.BasePrice * _random.Next(125, 181) / 100));
            }
        }
        return offers.OrderBy(offer => offer.Price).ToList();
    }

    private IReadOnlyList<InnSellOffer> CreateSellOffers(IReadOnlyDictionary<string, int> buybackPrices) =>
        CharacterRoster.Party.Members.SelectMany(character => character.Backpack
            .Select((item, index) => item is null || !buybackPrices.TryGetValue(item.Id, out var price)
                ? null
                : new InnSellOffer(character, index, item, price)))
            .Where(offer => offer is not null).Cast<InnSellOffer>().ToList();

    private void RunInnRecruitment()
    {
        var generator = new RandomCharacterGenerator(_gameData, _random);
        var candidateCount = _random.Next(1, 4);
        var classes = _gameData.CharacterClasses.OrderBy(_ => _random.Next()).Take(candidateCount).ToList();
        var usedNames = CharacterRoster.Characters.Select(character => character.Name).ToList();
        var candidates = new List<LiveCharacter>();
        foreach (var characterClass in classes)
        {
            var candidate = generator.CreateRecruit(characterClass, SelectedCharacter.Level,
                usedNames.Concat(candidates.Select(character => character.Name)).ToList());
            candidates.Add(candidate);
        }
        var recruitmentPrices = candidates.ToDictionary(candidate => candidate,
            candidate => candidate.Level < SelectedCharacter.Level
                ? 0
                : Math.Max(1, candidate.Level * 100 * _random.Next(50, 151) / 100));

        var selectedIndex = 0;
        var message = "A fogadós bemutatja az utazásra kész zsoldosokat.";
        while (candidates.Count > 0)
        {
            selectedIndex = Math.Clamp(selectedIndex, 0, candidates.Count - 1);
            _renderer.DrawInnRecruitmentScreen(candidates, recruitmentPrices, selectedIndex,
                CharacterRoster.Party.Members, SelectedCharacter.Gold, message);
            var key = Console.ReadKey(intercept: true).Key;
            if (key == ConsoleKey.Escape) return;
            if (key == ConsoleKey.UpArrow) { selectedIndex = (selectedIndex - 1 + candidates.Count) % candidates.Count; continue; }
            if (key == ConsoleKey.DownArrow) { selectedIndex = (selectedIndex + 1) % candidates.Count; continue; }
            if (key != ConsoleKey.Enter) continue;

            var recruit = candidates[selectedIndex];
            var price = recruitmentPrices[recruit];
            if (SelectedCharacter.Gold < price)
            {
                message = $"💰 Nincs elég aranyad: {price - SelectedCharacter.Gold} arany hiányzik {recruit.Name} felbérléséhez.";
                continue;
            }
            LiveCharacter? replaced = null;
            if (CharacterRoster.Party.Members.Count >= Party.MaximumSize)
            {
                var replaceable = CharacterRoster.Party.Members.Skip(1).ToList();
                var replacementIndex = ChoosePartyMemberToReplace(recruit, replaceable);
                if (replacementIndex is null)
                {
                    message = "A toborzást megszakítottad; választhatsz másik jelöltet.";
                    continue;
                }
                replaced = replaceable[replacementIndex.Value];
                CharacterRoster.Remove(replaced);
            }

            SelectedCharacter.SpendGold(price);
            CharacterRoster.Add(recruit);
            CharacterRoster.Party.Add(recruit);
            candidates.RemoveAt(selectedIndex);
            recruitmentPrices.Remove(recruit);
            message = replaced is null
                ? $"✅ {recruit.Name} csatlakozott a partihoz{FormatRecruitmentPricePaid(price)}."
                : $"✅ {recruit.Name} átvette {replaced.Name} helyét{FormatRecruitmentPricePaid(price)}; a régi társ végleg távozott.";
        }
    }

    private static string FormatRecruitmentPricePaid(int price) => price == 0
        ? " ingyen"
        : $" {price} aranyért";

    private int? ChoosePartyMemberToReplace(LiveCharacter recruit, IReadOnlyList<LiveCharacter> replaceable)
    {
        var selectedIndex = 0;
        while (true)
        {
            _renderer.DrawInnReplacementScreen(recruit, replaceable, selectedIndex);
            var key = Console.ReadKey(intercept: true).Key;
            if (key == ConsoleKey.Escape) return null;
            if (key == ConsoleKey.UpArrow) selectedIndex = (selectedIndex - 1 + replaceable.Count) % replaceable.Count;
            else if (key == ConsoleKey.DownArrow) selectedIndex = (selectedIndex + 1) % replaceable.Count;
            else if (key == ConsoleKey.Enter) return selectedIndex;
        }
    }

    private void RunInnRumors(int completedLevel)
    {
        const int maximumRefreshes = 3;
        var refreshesUsed = 0;
        var shownRumors = new HashSet<string>(StringComparer.Ordinal);
        var rumor = CreateUniqueInnRumor(completedLevel, shownRumors);
        while (true)
        {
            _renderer.DrawInnRumorScreen(rumor, maximumRefreshes - refreshesUsed);
            var key = Console.ReadKey(intercept: true).Key;
            if (key is ConsoleKey.Enter or ConsoleKey.Escape) return;
            if (key != ConsoleKey.N || refreshesUsed >= maximumRefreshes) continue;
            refreshesUsed++;
            rumor = CreateUniqueInnRumor(completedLevel, shownRumors);
        }
    }

    private InnRumor CreateUniqueInnRumor(int completedLevel, ISet<string> shownRumors)
    {
        InnRumor rumor;
        var attempts = 0;
        do rumor = CreateInnRumor(completedLevel);
        while (!shownRumors.Add(rumor.Title + "\n" + string.Join('\n', rumor.Lines)) && ++attempts < 30);
        return rumor;
    }

    private InnRumor CreateInnRumor(int completedLevel) => _random.Next(2) == 0
        ? CreateNextLevelRumor(completedLevel + 1)
        : CreateMonsterRumor(completedLevel);

    private InnRumor CreateNextLevelRumor(int level)
    {
        var configuration = MazeLevelConfigurations.Get(level);
        var enemyIds = configuration.RoomEncounters.Concat(configuration.CorridorEncounters)
            .SelectMany(encounter => encounter.Members).Select(member => member.EnemyId)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var enemyNames = enemyIds.Select(id => _gameData.GetEnemy(id).Name).ToList();
        var leaders = configuration.RoomEncounters.SelectMany(encounter => encounter.Members)
            .Where(member => member.Role == EnemyGroupRole.Leader)
            .Select(member => _gameData.GetEnemy(member.EnemyId).Name).Distinct().ToList();
        var corridorText = configuration.DoubleWidthCorridorChance switch
        {
            >= 0.9 => "szinte mindenütt széles, páros folyosók",
            >= 0.7 => "többnyire széles folyosók",
            <= 0.25 => "szűk, egycellás átjárók a nagyobb terek között",
            _ => "változó szélességű folyosók"
        };
        var wall = $"{configuration.WallRune} ({configuration.WallColor})";
        return new InnRumor($"Úti pletyka: {configuration.Name}",
        [
            $"A következő út a(z) {level}. szintre vezet: {configuration.Name}.",
            $"Terep: {configuration.RoomCount.Minimum}–{configuration.RoomCount.Maximum} szoba, " +
            $"{configuration.RoomSize.Minimum}–{configuration.RoomSize.Maximum} mezős oldalakkal; {corridorText}.",
            $"A falazat jele és színe: {wall}.",
            $"Várható ellenfelek: {string.Join(", ", enemyNames)}.",
            leaders.Count == 0
                ? "Külön vezéralakú szörnycsoportról nem érkezett biztos hír."
                : $"Vezérrel felvonuló csoportra is számíts: {string.Join(", ", leaders)}.",
            "A fogadós tanácsa: töltsd fel az ellátmányt, és a felsorolt ellenfelek képességeihez igazítsd a felszerelést."
        ], ConsoleColor.Yellow);
    }

    private InnRumor CreateMonsterRumor(int completedLevel)
    {
        var nearbyLevels = Enumerable.Range(Math.Max(1, completedLevel - 1), 3)
            .Where(level => level <= completedLevel + 1).ToList();
        var candidates = nearbyLevels.SelectMany(level =>
                MazeLevelConfigurations.Get(level).RoomEncounters
                    .Concat(MazeLevelConfigurations.Get(level).CorridorEncounters)
                    .SelectMany(encounter => encounter.Members)
                    .Select(member => (Level: level, Enemy: _gameData.GetEnemy(member.EnemyId))))
            .GroupBy(entry => entry.Enemy.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First()).ToList();
        var selected = candidates[_random.Next(candidates.Count)];
        var enemy = selected.Enemy;
        var abilities = enemy.AbilityIds.Select(_gameData.GetMonsterAbility).ToList();
        var lines = new List<string>
        {
            $"A(z) {selected.Level}. szint környékén látták. Jel a térképen: {enemy.Appearance}.",
            $"Erősség: {enemy.StrengthTier}/5; HP {enemy.HitPoints ?? 0}; Erő {enemy.Strength ?? 0}; " +
            $"Páncél {enemy.Armor ?? 0}; Gyorsaság {enemy.Speed ?? 0}; jutalom {enemy.ExperienceReward} XP."
        };
        if (abilities.Count == 0) lines.Add("Nincs ismert különleges képessége.");
        else
            foreach (var ability in abilities)
            {
                var activation = ability.Effect == MonsterAbilityEffect.Trait
                    ? "állandó tulajdonság"
                    : $"{ability.ChancePercent}% aktiválási esély, érték {ability.Value}";
                lines.Add($"{ability.Name} — {activation}. {ability.Description}");
            }
        lines.Add($"Mozgástempója a Gyorsasága alapján körülbelül {1400 / Math.Max(1, enemy.Speed ?? 2)} ms lépésenként.");
        return new InnRumor($"Szörnypletyka: {enemy.Name}", lines, ConsoleColor.Cyan);
    }

    private IReadOnlyList<IItemDefinition> AllTradableItems() => _gameData.Items.Cast<IItemDefinition>()
        .Concat(_gameData.Weapons).Concat(_gameData.Armors).Concat(_gameData.MagicItems)
        .Where(item => !SpellcastingRules.IsRestrictedFromTradingAndGeneration(item)).ToList();

    private ExperienceAward AwardExperience(LiveCharacter character, int amount) => new(character,
        AddExperienceAndLearnForNpc(character, amount));

    private LevelUpResult AddExperienceAndLearnForNpc(LiveCharacter character, int amount)
    {
        var result = character.AddExperience(
            amount,
            _gameData.ExperienceByLevel,
            _gameData.GetVitalityGrowth(character.Abilities.Health),
            _gameData.GetManaGrowth(character.Abilities.Intelligence),
            _random);
        if (character != SelectedCharacter)
            SpellcastingRules.LearnAutomaticSpells(character, _gameData, result.Bonuses, _random);
        return result;
    }

    private static string FormatExperienceAwards(IEnumerable<ExperienceAward> awards) => string.Join("; ", awards.Select(award =>
        $"{award.Character.Name} +{award.Result.GainedExperience}" +
        (award.Result.LeveledUp ? $" (L{award.Result.PreviousLevel}→L{award.Result.CurrentLevel})" : string.Empty)));

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
        ResolveSpellLearning(result);
    }

    private void ResolveSpellLearning(LevelUpResult result)
    {
        if (!SelectedCharacter.IsSpellcaster) return;
        var simulatedKnown = SelectedCharacter.KnownSpells.Select(spell => spell.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var learningCount = 0;
        foreach (var bonus in result.Bonuses)
        {
            if (!SpellcastingRules.TryGetSchool(SelectedCharacter.CharacterClass.Id, out var school)) break;
            var simulatedChoice = _gameData.Spells.FirstOrDefault(spell => spell.School == school &&
                spell.Level <= SpellcastingRules.MaximumSpellLevel(bonus.Level) && !simulatedKnown.Contains(spell.Id));
            if (simulatedChoice is null) continue;
            simulatedKnown.Add(simulatedChoice.Id);
            learningCount++;
        }
        var learnedNumber = 0;
        foreach (var bonus in result.Bonuses)
        {
            var choices = SpellcastingRules.AvailableUnknownSpells(SelectedCharacter, _gameData, bonus.Level);
            if (choices.Count > 0)
            {
                learnedNumber++;
                SelectedCharacter.LearnSpell(_renderer.DrawSpellLearningScreen(SelectedCharacter, choices,
                    learnedNumber, learningCount));
            }
        }
    }

    private sealed record ExperienceAward(LiveCharacter Character, LevelUpResult Result);
    public sealed record LevelCompletionResult(LiveCharacter Character, LevelUpResult Experience);
    private sealed record LevelCompletionOutcome(IReadOnlyList<LevelCompletionResult> Results,
        IReadOnlyList<LiveCharacter> FallenCharacters);

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
