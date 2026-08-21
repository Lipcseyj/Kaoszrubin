using MazeGame.Domain.Combat;

namespace MazeGame;

/// <summary>2×2-es folyosókból, szűkületekből és ajtós szobákból álló labirintust készít.</summary>
public sealed class MazeGenerator
{
    private const int CorridorWidth = 2;
    // A folyosóblokkok között három falcella marad: ezekben kapnak helyet a szobák.
    private const int GridStep = 5;
    private static readonly Direction[] Directions = Enum.GetValues<Direction>();
    private readonly Random _random = new();
    private readonly MazeGenerationSettings _settings;
    private readonly IReadOnlyList<ResolvedEnemySpawn> _enemySpawns;

    public MazeGenerator(MazeGenerationSettings settings, IReadOnlyList<ResolvedEnemySpawn> enemySpawns)
    {
        _settings = settings;
        _enemySpawns = enemySpawns;
        ValidateSettings(_settings);
    }

    public Maze Create(int width, int height)
    {
        var maze = new Maze(width, height);
        var gridWidth = (width - 3) / GridStep + 1;
        var gridHeight = (height - 3) / GridStep + 1;
        var visited = new bool[gridWidth, gridHeight];

        CarveFrom(maze, new Position(0, 0), visited, gridWidth, gridHeight);
        PlaceRooms(maze);
        maze.PlaceExit(ToMazePosition(new Position(gridWidth - 1, gridHeight - 1)));
        PlaceMapObjects(maze);
        return maze;
    }

    private void CarveFrom(Maze maze, Position gridPosition, bool[,] visited, int gridWidth, int gridHeight)
    {
        visited[gridPosition.X, gridPosition.Y] = true;
        CarveNode(maze, gridPosition);

        foreach (var direction in Directions.OrderBy(_ => _random.Next()))
        {
            var next = gridPosition + direction;
            if (!IsInsideGrid(next, gridWidth, gridHeight) || visited[next.X, next.Y]) continue;

            var isWide = _random.NextDouble() < _settings.DoubleWidthCorridorChance;
            CarveConnection(maze, gridPosition, direction, isWide);
            CarveFrom(maze, next, visited, gridWidth, gridHeight);
        }
    }

    private static void CarveNode(Maze maze, Position gridPosition)
    {
        var topLeft = ToMazePosition(gridPosition);
        for (var y = topLeft.Y; y < topLeft.Y + CorridorWidth; y++)
        for (var x = topLeft.X; x < topLeft.X + CorridorWidth; x++)
            maze.Carve(new Position(x, y));
    }

    private void CarveConnection(Maze maze, Position gridPosition, Direction direction, bool isWide)
    {
        var topLeft = ToMazePosition(gridPosition);
        var connectionLength = GridStep - CorridorWidth;
        if (direction is Direction.Left or Direction.Right)
        {
            var wallX = direction == Direction.Right ? topLeft.X + CorridorWidth : topLeft.X - 1;
            CarveHorizontalConnection(maze, wallX, topLeft.Y, direction == Direction.Right ? 1 : -1, connectionLength, isWide);
            return;
        }

        var wallY = direction == Direction.Down ? topLeft.Y + CorridorWidth : topLeft.Y - 1;
        CarveVerticalConnection(maze, topLeft.X, wallY, direction == Direction.Down ? 1 : -1, connectionLength, isWide);
    }

    private static void CarveHorizontalConnection(Maze maze, int startX, int y, int step, int length, bool isWide)
    {
        for (var offset = 0; offset < length; offset++)
        {
            maze.Carve(new Position(startX + offset * step, y));
            if (isWide) maze.Carve(new Position(startX + offset * step, y + 1));
        }
    }

    private static void CarveVerticalConnection(Maze maze, int x, int startY, int step, int length, bool isWide)
    {
        for (var offset = 0; offset < length; offset++)
        {
            maze.Carve(new Position(x, startY + offset * step));
            if (isWide) maze.Carve(new Position(x + 1, startY + offset * step));
        }
    }

    private void PlaceRooms(Maze maze)
    {
        var placedRooms = 0;
        var attempts = _settings.RoomCount * 400;
        for (var attempt = 0; attempt < attempts && placedRooms < _settings.RoomCount; attempt++)
        {
            var width = _random.Next(_settings.MinimumRoomSize, _settings.MaximumRoomSize + 1);
            var height = _random.Next(_settings.MinimumRoomSize, _settings.MaximumRoomSize + 1);
            var topLeft = new Position(_random.Next(2, maze.Width - width), _random.Next(2, maze.Height - height));
            if (TryPlaceRoom(maze, topLeft, width, height)) placedRooms++;
        }
    }

    private bool TryPlaceRoom(Maze maze, Position topLeft, int width, int height)
    {
        if (ContainsDoor(maze, topLeft, width, height)) return false;
        var doors = GetPossibleDoors(maze, topLeft, width, height).ToArray();
        if (doors.Length == 0) return false;

        var originalTiles = (System.Text.Rune[,])maze.Tiles.Clone();
        BuildRoomShell(maze, topLeft, width, height);
        for (var y = topLeft.Y; y < topLeft.Y + height; y++)
        for (var x = topLeft.X; x < topLeft.X + width; x++) maze.Carve(new Position(x, y));
        maze.SetTile(doors[_random.Next(doors.Length)], Maze.Door);

        if (HasPath(maze, maze.Entrance, maze.Exit))
        {
            maze.AddRoom(new Room(topLeft, width, height));
            return true;
        }

        Array.Copy(originalTiles, maze.Tiles, originalTiles.Length);
        return false;
    }

    private void PlaceMapObjects(Maze maze)
    {
        PlaceObjects(maze, _settings.TreasureChestCount, GetRoomPositions(maze), position => new TreasureChest(position), maze.AddTreasureChest);
        var enemyPositions = GetRoomPositions(maze).Concat(GetOutdoorPositions(maze));
        foreach (var spawn in _enemySpawns)
            PlaceObjects(maze, spawn.Count, enemyPositions, position => new Skeleton(position, spawn.Definition), maze.AddEnemy);
    }

    private void PlaceObjects<T>(Maze maze, int requestedCount, IEnumerable<Position> candidates, Func<Position, T> factory, Action<T> add)
        where T : WorldObject
    {
        var available = candidates.Where(position => maze.GetObjectAt(position) is null && position != maze.Entrance && position != maze.Exit).ToList();
        for (var count = 0; count < requestedCount && available.Count > 0; count++)
        {
            var index = _random.Next(available.Count);
            var position = available[index];
            available.RemoveAt(index);
            add(factory(position));
        }
    }

    private static IEnumerable<Position> GetRoomPositions(Maze maze) =>
        maze.Rooms.SelectMany(room => room.InteriorPositions()).Where(maze.IsWalkable);

    private static IEnumerable<Position> GetOutdoorPositions(Maze maze)
    {
        for (var y = 1; y < maze.Height - 1; y++)
        for (var x = 1; x < maze.Width - 1; x++)
        {
            var position = new Position(x, y);
            if (maze.IsWalkable(position) && maze.Tiles[x, y] != Maze.Door && !maze.Rooms.Any(room => room.Contains(position)))
                yield return position;
        }
    }

    private static bool ContainsDoor(Maze maze, Position topLeft, int width, int height)
    {
        for (var y = topLeft.Y - 1; y <= topLeft.Y + height; y++)
        for (var x = topLeft.X - 1; x <= topLeft.X + width; x++)
            if (maze.Tiles[x, y] == Maze.Door) return true;
        return false;
    }

    private static void BuildRoomShell(Maze maze, Position topLeft, int width, int height)
    {
        for (var y = topLeft.Y - 1; y <= topLeft.Y + height; y++)
        for (var x = topLeft.X - 1; x <= topLeft.X + width; x++)
        {
            var isInterior = x >= topLeft.X && x < topLeft.X + width && y >= topLeft.Y && y < topLeft.Y + height;
            if (!isInterior) maze.SetTile(new Position(x, y), Maze.Wall);
        }
    }

    private static bool HasPath(Maze maze, Position start, Position destination)
    {
        var visited = new bool[maze.Width, maze.Height];
        var queue = new Queue<Position>();
        queue.Enqueue(start);
        visited[start.X, start.Y] = true;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == destination) return true;
            foreach (var direction in Directions)
            {
                var next = current + direction;
                if (!maze.IsWalkable(next) || visited[next.X, next.Y]) continue;
                visited[next.X, next.Y] = true;
                queue.Enqueue(next);
            }
        }

        return false;
    }

    private static IEnumerable<Position> GetPossibleDoors(Maze maze, Position topLeft, int width, int height)
    {
        for (var x = topLeft.X; x < topLeft.X + width; x++)
        {
            var topDoor = new Position(x, topLeft.Y - 1);
            if (maze.IsWalkable(topDoor + Direction.Up)) yield return topDoor;
            var bottomDoor = new Position(x, topLeft.Y + height);
            if (maze.IsWalkable(bottomDoor + Direction.Down)) yield return bottomDoor;
        }
        for (var y = topLeft.Y; y < topLeft.Y + height; y++)
        {
            var leftDoor = new Position(topLeft.X - 1, y);
            if (maze.IsWalkable(leftDoor + Direction.Left)) yield return leftDoor;
            var rightDoor = new Position(topLeft.X + width, y);
            if (maze.IsWalkable(rightDoor + Direction.Right)) yield return rightDoor;
        }
    }

    private static Position ToMazePosition(Position gridPosition) => new(1 + gridPosition.X * GridStep, 1 + gridPosition.Y * GridStep);
    private static bool IsInsideGrid(Position position, int width, int height) => position.X >= 0 && position.X < width && position.Y >= 0 && position.Y < height;

    private static void ValidateSettings(MazeGenerationSettings settings)
    {
        if (settings.DoubleWidthCorridorChance is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(settings.DoubleWidthCorridorChance));
        if (settings.RoomCount < 0) throw new ArgumentOutOfRangeException(nameof(settings.RoomCount));
        if (settings.MinimumRoomSize < 2 || settings.MaximumRoomSize < settings.MinimumRoomSize)
            throw new ArgumentException("A szobaméreteknek legalább 2-nek és növekvő sorrendűnek kell lenniük.");
        if (settings.TreasureChestCount < 0 || settings.RoomEnemyCount < 0 || settings.OutdoorEnemyCount < 0)
            throw new ArgumentOutOfRangeException(nameof(settings), "Az objektumok száma nem lehet negatív.");
    }
}
