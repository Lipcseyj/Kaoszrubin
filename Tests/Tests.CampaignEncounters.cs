using System.Reflection;
using System.Runtime.CompilerServices;

internal static partial class Program
{
    static void CampaignEncounterGenerationFitsRoomsAndKeepsCasters()
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var report = new List<string>
        {
            "| Szint | Ellenfelek min–max | Varázshasználók min–max | Típusok min–max | Ládák min–max |",
            "|---|---:|---:|---:|---:|"
        };
        for (var number = 8; number <= 22; number++)
        {
            var configuration = MazeLevelConfigurations.Get(number);
            var encounters = configuration.RoomEncounters.Concat(configuration.CorridorEncounters).ToArray();
            var expectedIds = encounters.SelectMany(encounter => encounter.Members)
                .Select(member => member.EnemyId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var id in expectedIds) _ = data.GetEnemy(id);
            var minimumEnemies = encounters.Sum(encounter =>
                encounter.GroupCount.Minimum * encounter.Members.Sum(member => member.Count.Minimum));
            var minimumCasters = encounters.Sum(encounter => encounter.GroupCount.Minimum *
                encounter.Members.Where(member => data.GetEnemy(member.EnemyId).SpellcasterProfile is not null)
                    .Sum(member => member.Count.Minimum));
            var samples = new List<(int Enemies, int Casters, int Types, int Chests)>();
            foreach (var seed in Enumerable.Range(1, 5))
            {
                var game = (Game)RuntimeHelpers.GetUninitializedObject(typeof(Game));
                foreach (var (name, value) in new (string, object)[]
                    { ("_random", new Random(number * 1000 + seed)), ("_gameData", data), ("_difficultyLevel", number) })
                    typeof(Game).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(game, value);
                DungeonLevel level;
                try
                {
                    level = (DungeonLevel)typeof(Game).GetMethod("GenerateDungeonLevel",
                        BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(game, [configuration, null])!;
                }
                catch (TargetInvocationException exception) when (exception.InnerException is not null)
                { throw exception.InnerException; }
                var enemies = level.Areas.SelectMany(area => area.Maze.Enemies).ToArray();
                var casters = enemies.Count(enemy => enemy.Definition.SpellcasterProfile is not null);
                var chests = level.Areas.Sum(area => area.Maze.TreasureChests.Count);
                Assert(level.Areas.All(area => area.Maze.CheckFullAccessibility().IsFullyAccessible),
                    $"A(z) {number}. szint nem bejárható (seed {seed}).");
                Assert(expectedIds.SetEquals(enemies.Select(enemy => enemy.Definition.Id)) &&
                       enemies.Length >= minimumEnemies * 0.9,
                    $"A(z) {number}. szint túl sok konfigurált ellenfelet hagyott ki: {enemies.Length}/{minimumEnemies} (seed {seed}).");
                Assert(casters >= Math.Max(2, Math.Ceiling(minimumCasters * 0.6)) &&
                       chests >= configuration.TreasureChestCount.Minimum,
                    $"A(z) {number}. szinten eltűntek a mágusok vagy a kincsek (seed {seed}).");
                foreach (var encounter in configuration.RoomEncounters.Where(encounter =>
                    encounter.ScreenNumber is not null &&
                    encounter.Members.Any(member => member.Role == EnemyGroupRole.Leader)))
                {
                    var target = level.Areas[encounter.ScreenNumber!.Value - 1].Maze;
                    foreach (var leader in encounter.Members.Where(member => member.Role == EnemyGroupRole.Leader))
                        Assert(target.Enemies.Any(enemy => enemy.Definition.Id == leader.EnemyId),
                            $"A(z) {number}. szint {encounter.ScreenNumber}. képernyőjéről hiányzik {leader.EnemyId} (seed {seed}).");
                }
                var chiefCount = enemies.Count(enemy => enemy.Definition.Id == MonsterIds.OrkTörzsfő);
                if (number == 9)
                    Assert(chiefCount == 1, $"Az ork haditáborban {chiefCount} törzsfő jött létre (seed {seed}).");
                samples.Add((enemies.Length, casters,
                    enemies.Select(enemy => enemy.Definition.Id).Distinct().Count(), chests));
            }
            string Bounds(Func<(int Enemies, int Casters, int Types, int Chests), int> select) =>
                $"{samples.Min(select)}–{samples.Max(select)}";
            var row = $"| {number} | {Bounds(sample => sample.Enemies)} | {Bounds(sample => sample.Casters)} | " +
                $"{Bounds(sample => sample.Types)} | {Bounds(sample => sample.Chests)} |";
            report.Add(row);
            Console.WriteLine(row);
        }
        if (Environment.GetEnvironmentVariable("KAOSZRUBIN_BALANCE_REPORT") is { Length: > 0 } path)
            File.WriteAllLines(path, report);
    }
}
