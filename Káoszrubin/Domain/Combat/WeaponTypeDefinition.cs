using KaoszRubin.Domain;

namespace KaoszRubin.Domain.Combat;

/// <summary>A fegyver hatását meghatározó típus, például Erő vagy Ügyesség.</summary>
public sealed record WeaponTypeDefinition(string Id, string Name) : IGameDefinition;
