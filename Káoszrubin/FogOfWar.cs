namespace KaoszRubin;

/// <summary>A játékos által már felderített pályacellákat tartja nyilván.</summary>
public sealed class FogOfWar
{
    private const int MaximumBridgedFogGapLength = 3;
    // A konzol karaktercellája hozzávetőleg kétszer olyan magas, mint széles. A képarány
    // korrekció nélkül az azonos rácstávolság vízszintesen feleakkorának látszana.
    public const int HorizontalCellsPerVisionUnit = 2;
    private readonly bool[,] _revealed;
    private readonly bool[,] _currentlyVisible;
    private readonly Dictionary<WorldEntityId, EnemySightMemory> _enemyMemories = [];
    private Dictionary<WorldEntityId, Position> _visibleEnemyPositions = [];
    private bool _hasPartyPerceptionState;
    public int VisionRange { get; }
    public bool IsDeveloperRevealActive { get; private set; }

    public FogOfWar(int width, int height, int visionRange)
    {
        if (visionRange < 0) throw new ArgumentOutOfRangeException(nameof(visionRange));
        _revealed = new bool[width, height];
        _currentlyVisible = new bool[width, height];
        VisionRange = visionRange;
    }

    public bool IsRevealed(Position position) =>
        position.X >= 0 && position.X < _revealed.GetLength(0) &&
        position.Y >= 0 && position.Y < _revealed.GetLength(1) && _revealed[position.X, position.Y];

    public bool IsVisible(Position position) => IsDeveloperRevealActive || IsRevealed(position);
    public bool IsCurrentlyVisible(Position position, bool includeDeveloperReveal = true) =>
        includeDeveloperReveal && IsDeveloperRevealActive ||
        position.X >= 0 && position.X < _currentlyVisible.GetLength(0) &&
        position.Y >= 0 && position.Y < _currentlyVisible.GetLength(1) &&
        _currentlyVisible[position.X, position.Y];
    public IReadOnlyDictionary<WorldEntityId, EnemySightMemory> EnemyMemories => _enemyMemories;
    public bool IsEnemyVisible(WorldEntityId id, Position position) => _hasPartyPerceptionState
        ? _visibleEnemyPositions.ContainsKey(id)
        : IsCurrentlyVisible(position, includeDeveloperReveal: false);

    public IReadOnlyList<Position> RevealFrom(Maze maze, Position origin, int? visionRange = null)
    {
        var effectiveRange = visionRange ?? VisionRange;
        if (effectiveRange < 0) throw new ArgumentOutOfRangeException(nameof(visionRange));
        var newlyRevealed = new List<Position>();
        for (var y = origin.Y - effectiveRange; y <= origin.Y + effectiveRange; y++)
        for (var x = origin.X - effectiveRange * HorizontalCellsPerVisionUnit;
             x <= origin.X + effectiveRange * HorizontalCellsPerVisionUnit; x++)
        {
            var target = new Position(x, y);
            if (!maze.IsInside(target) || !IsWithinVisionRange(origin, target, effectiveRange)) continue;
            if (!HasLineOfSight(maze, origin, target) || _revealed[x, y]) continue;
            _revealed[x, y] = true;
            _currentlyVisible[x, y] = true;
            newlyRevealed.Add(target);
        }
        BridgeShortFogGaps(maze, newlyRevealed);
        return newlyRevealed;
    }

    public IReadOnlyList<Position> UpdatePartyVisibility(Maze maze,
        IEnumerable<(Position Origin, int Range)> sources, bool advanceEnemyMemory)
        => UpdatePartyVisibility(maze, sources.Select(source =>
            new PartyPerceptionSource(source.Origin, source.Range, 0, 0)), advanceEnemyMemory);

    public IReadOnlyList<Position> UpdatePartyVisibility(Maze maze,
        IEnumerable<PartyPerceptionSource> sources, bool advanceEnemyMemory)
    {
        var perceptionSources = sources.ToArray();
        _hasPartyPerceptionState = true;
        var previousVisible = (bool[,])_currentlyVisible.Clone();
        var previousMemoryPositions = _enemyMemories.Values.Select(memory => memory.Position).ToHashSet();
        Array.Clear(_currentlyVisible);
        foreach (var source in perceptionSources)
        {
            var origin = source.Origin;
            var range = source.VisionRange;
            for (var y = origin.Y - range; y <= origin.Y + range; y++)
            for (var x = origin.X - range * HorizontalCellsPerVisionUnit;
                 x <= origin.X + range * HorizontalCellsPerVisionUnit; x++)
            {
                var target = new Position(x, y);
                if (!maze.IsInside(target) || !IsWithinVisionRange(origin, target, range) ||
                    !HasLineOfSight(maze, origin, target)) continue;
                _currentlyVisible[x, y] = true;
                _revealed[x, y] = true;
            }
        }

        if (advanceEnemyMemory)
            foreach (var (id, memory) in _enemyMemories.ToArray())
                if (memory.RemainingPartyMoves <= 1) _enemyMemories.Remove(id);
                else _enemyMemories[id] = memory with { RemainingPartyMoves = memory.RemainingPartyMoves - 1 };

        var livingEnemyIds = maze.Enemies.Select(enemy => enemy.Id).ToHashSet();
        var nowVisible = maze.Enemies.Where(enemy => perceptionSources.Any(source =>
                CanDetectEnemy(maze, source, enemy)))
            .ToDictionary(enemy => enemy.Id, enemy => enemy.Position);
        foreach (var (id, position) in _visibleEnemyPositions)
            if (!nowVisible.ContainsKey(id) && livingEnemyIds.Contains(id))
                _enemyMemories[id] = new EnemySightMemory(position, 3);
        foreach (var id in nowVisible.Keys) _enemyMemories.Remove(id);
        foreach (var enemy in maze.Enemies.Where(enemy => !nowVisible.ContainsKey(enemy.Id) &&
                     (!_enemyMemories.TryGetValue(enemy.Id, out var memory) || memory.IsSoundCue)))
        {
            if (!perceptionSources.Any(source => CanHearEnemy(source, enemy))) continue;
            _enemyMemories[enemy.Id] = new EnemySightMemory(ApproximateSoundPosition(maze, enemy), 2, true);
        }
        foreach (var id in _enemyMemories.Keys.Where(id => !livingEnemyIds.Contains(id)).ToArray())
            _enemyMemories.Remove(id);
        _visibleEnemyPositions = nowVisible;
        foreach (var enemy in maze.Enemies) enemy.ClearPerceptibleActivity();

        var changed = new List<Position>();
        for (var y = 0; y < maze.Height; y++)
        for (var x = 0; x < maze.Width; x++)
            if (previousVisible[x, y] != _currentlyVisible[x, y]) changed.Add(new Position(x, y));
        var currentMemoryPositions = _enemyMemories.Values.Select(memory => memory.Position).ToHashSet();
        changed.AddRange(previousMemoryPositions.Where(position => !currentMemoryPositions.Contains(position)));
        changed.AddRange(currentMemoryPositions.Where(position => !previousMemoryPositions.Contains(position)));
        return changed.Distinct().ToArray();
    }

    public bool HasEnemyMemoryAt(Position position) => _enemyMemories.Values.Any(memory => memory.Position == position);
    public EnemySightMemory? EnemyMemoryAt(Position position) =>
        _enemyMemories.Values.FirstOrDefault(memory => memory.Position == position);

    private bool CanDetectEnemy(Maze maze, PartyPerceptionSource source, Enemy enemy)
    {
        var effectiveStealth = enemy.IsPerceptiblyActive
            ? 0
            : Math.Max(0, enemy.Definition.Stealth - source.DetectionBonus);
        var detectionRange = Math.Max(1, source.VisionRange - effectiveStealth);
        return IsCurrentlyVisible(enemy.Position, includeDeveloperReveal: false) &&
               CanSee(maze, source.Origin, enemy.Position, detectionRange);
    }

    private static bool CanHearEnemy(PartyPerceptionSource source, Enemy enemy)
    {
        if (enemy.Definition.Noise <= 0 ||
            !IsWithinVisionRange(source.Origin, enemy.Position, source.HearingRange + enemy.Definition.Noise))
            return false;
        if (enemy.Definition.Noise >= 4) return true;
        var roll = (HashCode.Combine(enemy.Id.Value, enemy.Position, source.Origin) & int.MaxValue) % 100;
        return roll < enemy.Definition.Noise * 25;
    }

    private static Position ApproximateSoundPosition(Maze maze, Enemy enemy)
    {
        var offsets = new[]
        {
            new Position(-2, 0), new Position(2, 0), new Position(0, -1), new Position(0, 1),
            new Position(-1, -1), new Position(1, 1), new Position(-1, 1), new Position(1, -1)
        };
        var start = (enemy.Id.Value.GetHashCode() & int.MaxValue) % offsets.Length;
        for (var index = 0; index < offsets.Length; index++)
        {
            var offset = offsets[(start + index) % offsets.Length];
            var candidate = new Position(enemy.Position.X + offset.X, enemy.Position.Y + offset.Y);
            if (maze.IsInside(candidate) && maze.IsWalkable(candidate)) return candidate;
        }
        return enemy.Position;
    }

    public static bool CanSee(Maze maze, Position origin, Position target, int range) =>
        IsWithinVisionRange(origin, target, range) &&
        HasLineOfSight(maze, origin, target);

    public static bool IsWithinVisionRange(Position origin, Position target, int range)
    {
        if (range < 0) return false;
        var horizontalUnits = (Math.Abs(target.X - origin.X) + HorizontalCellsPerVisionUnit - 1) /
                              HorizontalCellsPerVisionUnit;
        return Math.Max(horizontalUnits, Math.Abs(target.Y - origin.Y)) <= range;
    }

    /// <summary>Fejlesztői módban ideiglenesen felfedi, majd visszakapcsolva újra elfedi a térképet.</summary>
    public bool ToggleDeveloperReveal()
    {
        IsDeveloperRevealActive = !IsDeveloperRevealActive;
        return IsDeveloperRevealActive;
    }

    public IReadOnlyList<Position> GetRevealedPositions()
    {
        var positions = new List<Position>();
        for (var y = 0; y < _revealed.GetLength(1); y++)
        for (var x = 0; x < _revealed.GetLength(0); x++)
            if (_revealed[x, y]) positions.Add(new Position(x, y));
        return positions;
    }

    public void Restore(IEnumerable<Position> revealedPositions, bool developerRevealActive)
    {
        foreach (var position in revealedPositions)
            if (position.X >= 0 && position.X < _revealed.GetLength(0) && position.Y >= 0 && position.Y < _revealed.GetLength(1))
                _revealed[position.X, position.Y] = true;
        IsDeveloperRevealActive = developerRevealActive;
    }

    private static bool HasLineOfSight(Maze maze, Position origin, Position target)
    {
        var x = origin.X;
        var y = origin.Y;
        var deltaX = Math.Abs(target.X - origin.X);
        var deltaY = Math.Abs(target.Y - origin.Y);
        var stepX = origin.X < target.X ? 1 : -1;
        var stepY = origin.Y < target.Y ? 1 : -1;
        var error = deltaX - deltaY;

        while (true)
        {
            if (x == target.X && y == target.Y) return true;
            if ((x != origin.X || y != origin.Y) && maze.BlocksSight(new Position(x, y))) return false;
            var doubleError = 2 * error;
            if (doubleError > -deltaY) { error -= deltaY; x += stepX; }
            if (doubleError < deltaX) { error += deltaX; y += stepY; }
        }
    }

    /// <summary>
    /// Ha egy rövid (legfeljebb három cellás) ködcsík két végét a játékos már
    /// felfedezte, a köztes cellák is ténylegesen felderítődnek. Ez lehet fal vagy járat.
    /// </summary>
    private void BridgeShortFogGaps(Maze maze, ICollection<Position> newlyRevealed)
    {
        var bridgedPositions = new HashSet<Position>();
        for (var y = 0; y < maze.Height; y++)
            FindBridgedGaps(maze, maze.Width, x => new Position(x, y), bridgedPositions);

        for (var x = 0; x < maze.Width; x++)
            FindBridgedGaps(maze, maze.Height, y => new Position(x, y), bridgedPositions);

        foreach (var position in bridgedPositions)
        {
            _revealed[position.X, position.Y] = true;
            newlyRevealed.Add(position);
        }
    }

    private void FindBridgedGaps(Maze maze, int lineLength, Func<int, Position> positionAt, ISet<Position> bridgedPositions)
    {
        var index = 0;
        while (index < lineLength)
        {
            if (IsVisible(positionAt(index)))
            {
                index++;
                continue;
            }

            var start = index;
            while (index < lineLength && !IsVisible(positionAt(index))) index++;
            var gapLength = index - start;
            var hasExploredEnds = start > 0 && index < lineLength && IsRevealed(positionAt(start - 1)) && IsRevealed(positionAt(index));
            var containsDoor = Enumerable.Range(start, gapLength).Any(gapIndex => maze.GetDoorAt(positionAt(gapIndex)) is not null);
            if (!hasExploredEnds || gapLength > MaximumBridgedFogGapLength || containsDoor) continue;

            for (var gapIndex = start; gapIndex < index; gapIndex++)
                bridgedPositions.Add(positionAt(gapIndex));
        }
    }

}

public sealed record PartyPerceptionSource(Position Origin, int VisionRange, int HearingRange, int DetectionBonus);
public sealed record EnemySightMemory(Position Position, int RemainingPartyMoves, bool IsSoundCue = false);
