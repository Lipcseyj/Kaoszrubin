namespace KaoszRubin.World;

public sealed class FileForestLevelGraphSource(string directoryPath) : IForestLevelGraphSource
{
    private readonly string _directoryPath = string.IsNullOrWhiteSpace(directoryPath)
        ? throw new ArgumentException("A könyvtár neve nem lehet üres.", nameof(directoryPath))
        : directoryPath;

    public bool TryLoad(int level, out ForestLevelGraphDocument? document, out string? warning)
    {
        if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
        var fileName = Path.Combine(_directoryPath, $"level-{level}.json");
        document = null;
        warning = null;
        if (!File.Exists(fileName)) return false;

        try
        {
            var loaded = ForestConfigurationJson.DeserializeDocument(File.ReadAllText(fileName));
            if (loaded.Level is { } storedLevel && storedLevel != level)
            {
                warning = $"Az erdőgráf-fájl szintje ({storedLevel}) eltér a kért {level}. pályától: {fileName}.";
                return false;
            }
            document = loaded with { Level = loaded.Level ?? level };
            return true;
        }
        catch (Exception exception)
        {
            warning = $"A(z) {fileName} erdőgráf betöltése sikertelen: {exception.Message}";
            return false;
        }
    }
}
