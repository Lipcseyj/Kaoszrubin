using KaoszRubin.Domain;

namespace KaoszRubin.Domain.Inventory;

public sealed record ItemCurseDefinition(string Id, string Name, ItemCurseEffect Effect, int Value,
    int Strength, IReadOnlySet<ItemCategory> CompatibleCategories, string Description,
    string ActivationText, string RemovalText) : IGameDefinition
{
    public bool CanAffect(IItemDefinition item) => CompatibleCategories.Contains(item.Category);
}

public enum ItemCurseEffect
{
    None,
    HitPenalty,
    DefensePenalty,
    InitiativePenalty,
    MovementPenalty,
    CombatWeight,
    ManaCost,
    HealthPenalty,
    IntelligencePenalty
}
