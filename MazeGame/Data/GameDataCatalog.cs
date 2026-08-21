using MazeGame.Domain.Characters;
using MazeGame.Domain.Combat;
using MazeGame.Domain.Magic;

namespace MazeGame.Data;

/// <summary>A játék definícióinak központi, csak olvasható gyűjteménye.</summary>
public sealed class GameDataCatalog
{
    public IReadOnlyList<RaceDefinition> Races { get; init; } = [];
    public IReadOnlyList<CharacterClassDefinition> CharacterClasses { get; init; } = [];
    public IReadOnlyList<EnemyDefinition> Enemies { get; init; } = [];
    public IReadOnlyList<WeaponDefinition> Weapons { get; init; } = [];
    public IReadOnlyList<ArmorDefinition> Armors { get; init; } = [];
    public IReadOnlyList<AbilityDefinition> Abilities { get; init; } = [];
    public IReadOnlyList<MagicItemDefinition> MagicItems { get; init; } = [];
    public IReadOnlyList<SpellDefinition> Spells { get; init; } = [];

    public EnemyDefinition GetEnemy(string name) =>
        Enemies.FirstOrDefault(enemy => string.Equals(enemy.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"A(z) '{name}' nevű ellenfél nem található az adatok.csv fájlban.");
}
