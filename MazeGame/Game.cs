using MazeGame.Data;
using MazeGame.Domain.Characters;
using MazeGame.Combat;
using MazeGame.Domain.Inventory;

namespace MazeGame;

/// <summary>A játék futását és felhasználói bemenetét koordinálja.</summary>
public sealed class Game
{
    private static readonly TimeSpan EnemyMoveInterval = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan PartyMoveInterval = TimeSpan.FromMilliseconds(1500);
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
    private bool _characterSheetFocused;
    private HeldInventoryItem? _heldInventoryItem;
    private DateTime _nextNeedsDrain;
    private DateTime _nextPartyMove;
    private Direction _leaderFacing = Direction.Right;
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
                    if (key == ConsoleKey.N) { TryOpenAdjacentDoor(); continue; }
                    if (key == ConsoleKey.Z) { TryCloseAdjacentDoor(); continue; }
                    if (key == ConsoleKey.K) { TryLockAdjacentDoor(); continue; }
                    MovePlayer(key);
                }

                if (!_battleStarted && DateTime.UtcNow >= nextEnemyMove)
                {
                    MoveEnemies();
                    nextEnemyMove = DateTime.UtcNow + EnemyMoveInterval;
                }

                if (!_battleStarted && DateTime.UtcNow >= _nextPartyMove)
                {
                    MovePartyMembers();
                    _nextPartyMove = DateTime.UtcNow + PartyMoveInterval;
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
        PlacePartyMembersNear(_player.Position);
        _fogOfWar = new FogOfWar(_maze.Width, _maze.Height, VisionRange);
        _fogOfWar.RevealFrom(_maze, _player.Position);
        foreach (var member in _maze.PartyMembers) _fogOfWar.RevealFrom(_maze, member.Position);
        _nextPartyMove = DateTime.UtcNow + PartyMoveInterval;
        _battleStarted = false;
        _gameOver = false;
        _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
    }

    private void MovePlayer(ConsoleKey key)
    {
        if (_player.Position == _maze.Exit || !TryGetDirection(key, out var direction)) return;

        var previousPosition = _player.Position;
        if (!_player.TryMove(direction, _maze)) return;
        _leaderFacing = direction;

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

    private void DropSelectedInventoryItem()
    {
        var slot = _renderer.GetSelectedInventorySlot();
        if (slot is null) { _renderer.DrawInventoryMessage("Itt nincs ledobható tárgy.", ConsoleColor.DarkYellow); return; }
        var item = slot.Value.Character.GetInventoryItem(slot.Value.Kind, slot.Value.Index);
        if (item is null) { _renderer.DrawInventoryMessage("A kijelölt hely üres.", ConsoleColor.DarkYellow); return; }
        slot.Value.Character.SetInventoryItem(slot.Value.Kind, slot.Value.Index, null);
        _maze.DropItem(_player.Position, item);
        _renderer.RefreshCharacterSheet(SelectedCharacter);
        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
        var pileCount = _maze.GetGroundItemPileAt(_player.Position)?.Items.Count ?? 1;
        _renderer.DrawInventoryMessage($"Ledobtad: {item.Name}. A mezőn {pileCount} tárgy van.", ConsoleColor.Cyan);
    }

    private void InspectSelectedInventoryItem()
    {
        var slot = _renderer.GetSelectedInventorySlot();
        var item = slot is { } selected ? selected.Character.GetInventoryItem(selected.Kind, selected.Index) : null;
        if (item is null) { _renderer.DrawInventoryMessage("A kijelölt helyen nincs megvizsgálható tárgy.", ConsoleColor.DarkYellow); return; }

        var details = item switch
        {
            Domain.Combat.WeaponDefinition weapon =>
                $"Fegyver | típus: {(weapon.WeaponTypeId is { } typeId ? _gameData.GetWeaponType(typeId).Name : "nincs")} | sebzés: {weapon.Damage?.ToString() ?? "nincs"}",
            Domain.Combat.ArmorDefinition armor => $"Páncél | védelem: {armor.Defense?.ToString() ?? "nincs"}",
            Domain.Magic.MagicItemDefinition => "Varázstárgy | mágikus hatása még nincs bevezetve",
            _ => "Általános tárgy"
        };
        var description = string.IsNullOrWhiteSpace(item.Description) ? "Nincs jellemzés." : item.Description;
        _renderer.DrawInventoryMessage($"{item.Name} [{item.Id}] — {details}. Jellemzés: {description}", ConsoleColor.Cyan);
    }

    private void GrabOrPlaceInventoryItem()
    {
        var slot = _renderer.GetSelectedInventorySlot();
        if (slot is null) { _renderer.DrawInventoryMessage("Válassz egy felszerelés- vagy hátizsákhelyet.", ConsoleColor.DarkYellow); return; }
        var target = slot.Value;
        if (_heldInventoryItem is null)
        {
            var item = target.Character.GetInventoryItem(target.Kind, target.Index);
            if (item is null) { _renderer.DrawInventoryMessage("A kijelölt hely üres.", ConsoleColor.DarkYellow); return; }
            target.Character.SetInventoryItem(target.Kind, target.Index, null);
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
        if (!LiveCharacter.CanPlaceInventoryItem(target.Kind, held.Item))
        {
            _renderer.DrawInventoryMessage($"A(z) {held.Item.Name} nem tehető ebbe a helybe.", ConsoleColor.Red);
            return;
        }
        var displaced = target.Character.GetInventoryItem(target.Kind, target.Index);
        if (displaced is not null && !LiveCharacter.CanPlaceInventoryItem(held.Source.Kind, displaced))
        {
            _renderer.DrawInventoryMessage($"A csere nem lehetséges: a(z) {displaced.Name} nem fér a forráshelyre.", ConsoleColor.Red);
            return;
        }
        target.Character.SetInventoryItem(target.Kind, target.Index, held.Item);
        held.Source.Character.SetInventoryItem(held.Source.Kind, held.Source.Index, displaced);
        _heldInventoryItem = null;
        _renderer.RefreshCharacterSheet(SelectedCharacter);
        _renderer.DrawInventoryMessage(displaced is null
            ? $"Áthelyezted: {held.Item.Name}."
            : $"Felcserélted: {held.Item.Name} ↔ {displaced.Name}.", ConsoleColor.Green);
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

    private void MovePartyMembers()
    {
        foreach (var member in _maze.PartyMembers.ToArray())
        {
            var previous = member.Position;
            var next = ChoosePartyMemberStep(member);
            if (next is null || !_maze.TryMovePartyMember(member, next.Value, _player.Position)) continue;
            var newlyRevealed = _fogOfWar.RevealFrom(_maze, member.Position);
            _renderer.DrawPartyMemberMovement(_maze, _fogOfWar, previous, member.Position, newlyRevealed, _player.Position);
        }
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

        if (behavior == NpcBehavior.Scout)
        {
            if (visibleEnemy is not null)
                return FindNextStep(member, FreePositionsNear(_player.Position, 2));
            return ChooseForwardStep(member, maximumLeaderDistance: 10, maximumSearchDistance: 10, avoidNarrowFront: false)
                ?? FollowLeader(member, stopDistance: 2);
        }

        if (behavior == NpcBehavior.Aggressive)
            return ChooseForwardStep(member, maximumLeaderDistance: 3, maximumSearchDistance: 4, avoidNarrowFront: true)
                ?? FollowLeader(member, stopDistance: 1);

        return FollowLeader(member, stopDistance: 1);
    }

    private Position? FollowLeader(PartyMemberAvatar member, int stopDistance)
    {
        if (Manhattan(member.Position, _player.Position) <= stopDistance) return null;
        return FindNextStep(member, FreePositionsNear(_player.Position, stopDistance));
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

    private IEnumerable<Position> FreePositionsNear(Position origin, int distance) =>
        Enumerable.Range(-distance, distance * 2 + 1)
            .SelectMany(dx => Enumerable.Range(-distance, distance * 2 + 1).Select(dy => new Position(origin.X + dx, origin.Y + dy)))
            .Where(position => Manhattan(position, origin) > 0 && Manhattan(position, origin) <= distance)
            .Where(position => _maze.IsWalkable(position) && position != _player.Position &&
                               (_maze.GetObjectAt(position) is null or GroundItemPile or PartyMemberAvatar));

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

    private sealed record HeldInventoryItem(IItemDefinition Item, InventorySlotReference Source);

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

    private void PlacePartyMembersNear(Position origin)
    {
        var alreadyPlaced = _maze.PartyMembers.Select(member => member.Character).ToHashSet();
        var companions = CharacterRoster.Party.Members.Where(member => member != SelectedCharacter && !alreadyPlaced.Contains(member)).ToList();
        if (companions.Count == 0) return;

        var positions = FindNearbyFreePositions(origin).Take(companions.Count).ToList();
        for (var index = 0; index < Math.Min(companions.Count, positions.Count); index++)
        {
            if (companions[index].NpcBehavior is null) companions[index].SetNpcBehavior(NpcBehavior.Defensive);
            _maze.AddPartyMember(new PartyMemberAvatar(positions[index], companions[index]));
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
