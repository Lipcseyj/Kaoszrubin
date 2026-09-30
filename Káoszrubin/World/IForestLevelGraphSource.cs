namespace KaoszRubin.World;

public interface IForestLevelGraphSource
{
    bool TryLoad(int level, out ForestLevelGraphDocument? document, out string? warning);
}
