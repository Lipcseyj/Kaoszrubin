using KaoszRubin.Domain.Combat;

namespace KaoszRubin.World;

/// <summary>
/// Összefüggő tisztásokat, kanyargó ösvényeket, változatos növényzetet és tavakat készítő
/// erdei képernyőgenerátor. A szobaként nyilvántartott tisztásokra a közös encounter- és
/// kincselhelyezés változtatás nélkül ráépül.
/// </summary>
public sealed class ForestMazeGenerator : MazeGenerator
{
    private static readonly Direction[] Directions = Enum.GetValues<Direction>();
    private readonly ForestGenerationConfiguration _forest;

    public ForestMazeGenerator(MazeGenerationSettings settings, ForestGenerationConfiguration forest,
        IReadOnlyList<ResolvedEnemyEncounter> clearingEncounters,
        IReadOnlyList<ResolvedEnemyEncounter> trailEncounters, Random? random = null,
        EnemyMagicWeaponContext? enemyMagicWeaponContext = null)
        : base(settings, clearingEncounters, trailEncounters, random, enemyMagicWeaponContext)
    {
        _forest = forest ?? throw new ArgumentNullException(nameof(forest));
        ValidateForestConfiguration(forest);
    }

    protected override Maze CreateLayout(int width, int height)
    {
        var palette = _forest.Palette;
        var maze = new Maze(width, height, palette.Tree.Rune, palette.Tree.ForegroundColor, Settings.LevelName);
        foreach (var style in palette.All) maze.RegisterTerrainStyle(style);
        FillForest(maze);
        PlaceLakes(maze);

        var startingRoom = new Room(new Position(1, 1), 3, 3);
        CarveRoom(maze, startingRoom);
        maze.SetStartingRoom(startingRoom);

        var clearings = PlaceClearings(maze);
        var connected = new List<Position> { maze.Entrance };
        foreach (var clearing in clearings.OrderBy(_ => Random.Next()))
        {
            var center = Center(clearing);
            var anchor = connected.OrderBy(position => Manhattan(position, center)).First();
            CarveMeanderingTrail(maze, anchor, center);
            connected.Add(center);
        }

        var exit = maze.Exit;
        var exitAnchor = connected.OrderBy(position => Manhattan(position, exit)).First();
        CarveMeanderingTrail(maze, exitAnchor, exit);
        maze.Carve(exit);
        ExpandForestEdges(maze);
        DecorateWalkableTerrain(maze);
        maze.PlaceExit(exit);
        var report = maze.EnsureFullAccessibility(() => DoorState.Open);
        if (!report.IsFullyAccessible)
            throw new InvalidOperationException("Az erdei képernyő járathálózata nem lett teljesen bejárható.");
        return maze;
    }

    private void FillForest(Maze maze)
    {
        var palette = _forest.Palette;
        for (var y = 0; y < maze.Height; y++)
        for (var x = 0; x < maze.Width; x++)
        {
            var position = new Position(x, y);
            maze.SetTerrain(position, RollObstacle(palette));
        }
    }

    private MazeTerrainStyle RollObstacle(ForestTerrainPalette palette)
    {
        var roll = Random.NextDouble();
        if ((roll -= _forest.PineChance) < 0) return palette.Pine;
        if ((roll -= _forest.BushChance) < 0) return palette.Bush;
        if ((roll -= _forest.FlowerBushChance) < 0) return palette.FlowerBush;
        if ((roll -= _forest.ThicketChance) < 0) return palette.Thicket;
        return palette.Tree;
    }

    private void PlaceLakes(Maze maze)
    {
        for (var index = 0; index < _forest.LakeCount.Roll(Random); index++)
        {
            var radiusX = _forest.LakeRadius.Roll(Random);
            var radiusY = Math.Max(2, radiusX + Random.Next(-1, 2));
            if (maze.Width <= radiusX * 2 + 6 || maze.Height <= radiusY * 2 + 6) continue;
            var center = new Position(Random.Next(radiusX + 3, maze.Width - radiusX - 3),
                Random.Next(radiusY + 3, maze.Height - radiusY - 3));
            for (var y = center.Y - radiusY; y <= center.Y + radiusY; y++)
            for (var x = center.X - radiusX; x <= center.X + radiusX; x++)
            {
                var normalized = Math.Pow((x - center.X) / (double)radiusX, 2) +
                                 Math.Pow((y - center.Y) / (double)radiusY, 2);
                if (normalized <= 1 + Random.NextDouble() * 0.16)
                    maze.SetTerrain(new Position(x, y), _forest.Palette.Water);
            }
        }
    }

    private List<Room> PlaceClearings(Maze maze)
    {
        var result = new List<Room>();
        var attempts = Math.Max(100, Settings.RoomCount * 300);
        for (var attempt = 0; attempt < attempts && result.Count < Settings.RoomCount; attempt++)
        {
            var width = Random.Next(Settings.MinimumRoomSize, Settings.MaximumRoomSize + 1);
            var height = Random.Next(Settings.MinimumRoomSize, Settings.MaximumRoomSize + 1);
            if (width >= maze.Width - 5 || height >= maze.Height - 5) continue;
            var room = new Room(new Position(Random.Next(2, maze.Width - width - 2),
                Random.Next(2, maze.Height - height - 2)), width, height);
            if (room.Contains(maze.Entrance) || room.Contains(maze.Exit) || result.Any(other => Overlaps(room, other)))
                continue;
            CarveRoom(maze, room);
            maze.AddRoom(room);
            result.Add(room);
        }
        if (result.Count < Settings.RoomCount)
            throw new InvalidOperationException(
                $"Az erdei képernyőn csak {result.Count}/{Settings.RoomCount} tisztás fért el.");
        return result;
    }

    private static void CarveRoom(Maze maze, Room room)
    {
        foreach (var position in room.InteriorPositions()) maze.Carve(position);
    }

    private void CarveMeanderingTrail(Maze maze, Position start, Position destination)
    {
        var current = start;
        var safety = maze.Width * maze.Height;
        while (current != destination && safety-- > 0)
        {
            CarveTrailWidth(maze, current);
            var horizontalDistance = destination.X - current.X;
            var verticalDistance = destination.Y - current.Y;
            var horizontal = horizontalDistance != 0 &&
                             (verticalDistance == 0 || Random.Next(Math.Abs(horizontalDistance) +
                                 Math.Abs(verticalDistance)) < Math.Abs(horizontalDistance));
            current = horizontal
                ? current with { X = current.X + Math.Sign(horizontalDistance) }
                : current with { Y = current.Y + Math.Sign(verticalDistance) };
        }
        CarveTrailWidth(maze, destination);
    }

    private void CarveTrailWidth(Maze maze, Position center)
    {
        var left = (_forest.TrailWidth - 1) / 2;
        var right = _forest.TrailWidth / 2;
        for (var y = center.Y - left; y <= center.Y + right; y++)
        for (var x = center.X - left; x <= center.X + right; x++)
        {
            var position = new Position(x, y);
            if (position.X <= 0 || position.X >= maze.Width - 1 ||
                position.Y <= 0 || position.Y >= maze.Height - 1) continue;
            maze.Carve(position);
        }
    }

    private void ExpandForestEdges(Maze maze)
    {
        var expansionChance = (1 - _forest.ForestDensity) * 0.42;
        for (var pass = 0; pass < 3; pass++)
        {
            var candidates = new List<Position>();
            for (var y = 1; y < maze.Height - 1; y++)
            for (var x = 1; x < maze.Width - 1; x++)
            {
                var position = new Position(x, y);
                if (maze.IsWalkable(position) || maze.GetTerrainStyle(position)?.Id == _forest.Palette.Water.Id)
                    continue;
                if (Directions.Any(direction => maze.IsWalkable(position + direction)) &&
                    Random.NextDouble() < expansionChance) candidates.Add(position);
            }
            foreach (var position in candidates) maze.Carve(position);
        }
    }

    private void DecorateWalkableTerrain(Maze maze)
    {
        var walkable = new List<Position>();
        for (var y = 1; y < maze.Height - 1; y++)
        for (var x = 1; x < maze.Width - 1; x++)
        {
            var position = new Position(x, y);
            if (maze.Tiles[x, y] == Maze.Floor && position != maze.Entrance && position != maze.Exit)
                walkable.Add(position);
        }

        foreach (var position in walkable)
        {
            if (Directions.Any(direction =>
                    maze.GetTerrainStyle(position + direction)?.Id == _forest.Palette.Water.Id) &&
                Random.NextDouble() < _forest.MarshChance)
            {
                maze.SetTerrain(position, _forest.Palette.Marsh);
                continue;
            }
            var roll = Random.NextDouble();
            if (roll < _forest.DenseUndergrowthChance)
                maze.SetTerrain(position, _forest.Palette.DenseUndergrowth);
            else if (roll < _forest.DenseUndergrowthChance + _forest.UndergrowthChance)
                maze.SetTerrain(position, _forest.Palette.Undergrowth);
        }
    }

    private static Position Center(Room room) =>
        new(room.TopLeft.X + room.Width / 2, room.TopLeft.Y + room.Height / 2);

    private static int Manhattan(Position first, Position second) =>
        Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y);

    private static bool Overlaps(Room first, Room second) =>
        first.TopLeft.X - 2 <= second.TopLeft.X + second.Width + 1 &&
        first.TopLeft.X + first.Width + 1 >= second.TopLeft.X - 2 &&
        first.TopLeft.Y - 2 <= second.TopLeft.Y + second.Height + 1 &&
        first.TopLeft.Y + first.Height + 1 >= second.TopLeft.Y - 2;

    private static void ValidateForestConfiguration(ForestGenerationConfiguration configuration)
    {
        var probabilities = new[]
        {
            configuration.ForestDensity, configuration.PineChance, configuration.BushChance,
            configuration.FlowerBushChance, configuration.ThicketChance, configuration.UndergrowthChance,
            configuration.DenseUndergrowthChance, configuration.MarshChance
        };
        if (probabilities.Any(value => value is < 0 or > 1) ||
            configuration.PineChance + configuration.BushChance + configuration.FlowerBushChance +
            configuration.ThicketChance > 1 ||
            configuration.UndergrowthChance + configuration.DenseUndergrowthChance > 1)
            throw new ArgumentOutOfRangeException(nameof(configuration),
                "Az erdei gyakoriságoknak 0 és 1 közé kell esniük, a részarányok összege legfeljebb 1 lehet.");
        if (configuration.LakeCount.Minimum < 0 ||
            configuration.LakeCount.Maximum < configuration.LakeCount.Minimum ||
            configuration.LakeRadius.Minimum < 1 ||
            configuration.LakeRadius.Maximum < configuration.LakeRadius.Minimum ||
            configuration.TrailWidth is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(configuration), "Az erdei méretbeállítások érvénytelenek.");
        if (configuration.Palette.All.Select(style => style.Rune.Value).Distinct().Count() !=
            configuration.Palette.All.Count)
            throw new ArgumentException("Az erdei tereprúnáknak egyedinek kell lenniük.", nameof(configuration));
    }
}
