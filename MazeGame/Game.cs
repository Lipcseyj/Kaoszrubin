using MazeGame.Data;
using MazeGame.Domain.Characters;
using MazeGame.Combat;
using MazeGame.Domain.Inventory;

namespace MazeGame;

/// <summary>A játék futását és felhasználói bemenetét koordinálja.</summary>
public sealed class Game
{
    private static readonly TimeSpan EnemyMoveInterval = TimeSpan.FromMilliseconds(700);
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
    private bool _battleStarted;
    private bool _gameOver;
    private bool _characterSheetFocused;
    private HeldInventoryItem? _heldInventoryItem;
    private DateTime _nextNeedsDrain;
    private readonly Dictionary<PartyMemberAvatar, DateTime> _nextPartyMoves = [];
    private readonly List<Position> _leaderTrail = [];
    private bool _partyHoldingPosition;
    private DateTime? _partyScatterUntil;
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
                    if (IsTeleportToExitShortcut(keyInfo))
                    {
                        TeleportLeaderNearExit();
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
                    MovePlayer(key);
                }

                if (!_battleStarted && DateTime.UtcNow >= nextEnemyMove)
                {
                    MoveEnemies();
                    nextEnemyMove = DateTime.UtcNow + EnemyMoveInterval;
                }

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
        var configuration = MazeLevelConfigurations.Get(_mazeLevel);
        var enemySpawns = configuration.EnemySpawns
            .Select(spawn => new ResolvedEnemySpawn(_gameData.GetEnemy(spawn.EnemyId), spawn.Count.Roll(_random)))
            .ToList();
        _generator = new MazeGenerator(configuration.CreateGenerationSettings(_random), enemySpawns);
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
        _renderer.DrawInitialState(_maze, _player, _fogOfWar, _mazeLevel);
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
                $"Fegyver | típus: {(weapon.WeaponTypeId is { } typeId ? _gameData.GetWeaponType(typeId).Name : "nincs")} | sebzés: {weapon.Damage?.ToString() ?? "nincs"}",
            Domain.Combat.ArmorDefinition armor => $"Páncél | védelem: {armor.Defense?.ToString() ?? "nincs"}",
            Domain.Magic.MagicItemDefinition => "Varázstárgy | mágikus hatása még nincs bevezetve",
            _ => "Általános tárgy"
        };
        var description = string.IsNullOrWhiteSpace(item.Description) ? "Nincs jellemzés." : item.Description;
        _renderer.DrawInventoryMessage($"{item.Name} [{item.Id}] — {details}. Jellemzés: {description}", ConsoleColor.Cyan);
    }

    private static string NpcBehaviorName(NpcBehavior? behavior) => behavior switch
    {
        NpcBehavior.Defensive => "Defenzív",
        NpcBehavior.Aggressive => "Aggresszív",
        NpcBehavior.Scout => "Felderítő",
        NpcBehavior.Cautious => "Óvatos",
        _ => "inaktív"
    };

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
            var destination = previousPosition + direction;
            if (_maze.GetPartyMemberAt(destination) is { } encounteredMember)
            {
                ResolveNpcBattle(encounteredMember, enemy);
                continue;
            }
            if (!_maze.TryMoveEnemy(enemy, destination)) continue;

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
        var result = _battleSystem.Resolve(member.Character, enemy, _ => { });
        if (result.PlayerWon)
        {
            var experienceAwards = DistributeExperience(member.Character, enemy.Definition.ExperienceReward);
            var experienceResult = experienceAwards.First(award => award.Character == member.Character).Result;
            _maze.ReplaceEnemyWithCorpse(enemy);
            var levelText = experienceResult.LeveledUp
                ? $" Szint: {experienceResult.PreviousLevel}→{experienceResult.CurrentLevel}; +{experienceResult.VitalityGained} max HP" +
                  (experienceResult.ManaGained > 0 ? $"; +{experienceResult.ManaGained} max manna." : ".")
                : string.Empty;
            _renderer.DrawNpcBattleSummary(
                $"{member.Character.Name} automatikus csatában legyőzte {enemy.Name} ellenfelet {result.Rounds} kör alatt. " +
                $"HP: {startingNpcHp}→{member.Character.CurrentVitality}; ellenfél HP: {startingEnemyHp}→0; " +
                $"XP: {FormatExperienceAwards(experienceAwards)}.{levelText}",
                ConsoleColor.Green);
        }
        else
        {
            _maze.ReplacePartyMemberWithCorpse(member);
            _nextPartyMoves.Remove(member);
            _renderer.DrawNpcBattleSummary(
                $"{member.Character.Name} elesett a(z) {enemy.Name} elleni automatikus csatában {result.Rounds} kör után. " +
                $"HP: {startingNpcHp}→0; ellenfél HP: {startingEnemyHp}→{enemy.CurrentHitPoints}.",
                ConsoleColor.Red);
        }
        _renderer.DrawMapVisibilityChanged(_maze, _fogOfWar, _player.Position);
        _renderer.RefreshCharacterSheet(SelectedCharacter);
        _battleStarted = false;
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
            var experienceAwards = DistributeExperience(SelectedCharacter, enemy.Definition.ExperienceReward);
            var experienceResult = experienceAwards.First(award => award.Character == SelectedCharacter).Result;
            _maze.ReplaceEnemyWithCorpse(enemy);
            _renderer.DrawMapCellAfterBattle(_maze, _fogOfWar, enemy.Position, _player.Position);
            _renderer.DrawBattleResult(result, enemy);
            _renderer.DrawExperienceDistribution(FormatExperienceAwards(experienceAwards),
                experienceAwards.Any(award => award.Result.LeveledUp));
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

    private static bool IsTeleportToExitShortcut(ConsoleKeyInfo keyInfo) =>
        keyInfo.Key == ConsoleKey.E &&
        (keyInfo.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Shift)) == (ConsoleModifiers.Control | ConsoleModifiers.Shift);

    private static bool IsLevelUpShortcut(ConsoleKeyInfo keyInfo) =>
        keyInfo.Key == ConsoleKey.L &&
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
        {
            character.RestoreVitality(character.MaximumVitality);
            character.RestoreMana(character.MaximumMana);
        }
        return new LevelCompletionOutcome(results, fallenCharacters);
    }

    private ExperienceAward AwardExperience(LiveCharacter character, int amount) => new(character,
        character.AddExperience(
            amount,
            _gameData.ExperienceByLevel,
            _gameData.GetVitalityGrowth(character.Abilities.Health),
            _gameData.GetManaGrowth(character.Abilities.Intelligence),
            _random));

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
