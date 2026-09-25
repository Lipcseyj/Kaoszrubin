using KaoszRubin.Domain;

namespace KaoszRubin.Domain.Combat;

public sealed record MonsterSummonDefinition(string Id, string AbilityId,
    IReadOnlyList<string> EnemyIds, int MinimumCount, int MaximumCount, int SpawnRadius,
    int MaximumLivingSummons, bool GrantsRewardsAndLoot, bool Prepared) : IGameDefinition
{
    public string Name => Id;
}
