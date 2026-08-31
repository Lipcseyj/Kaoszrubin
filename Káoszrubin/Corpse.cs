using System.Text;

namespace KaoszRubin;

/// <summary>Egy csatában elesett szereplő maradványa a pályán.</summary>
public class Corpse(Position position, string formerName) : WorldObject(position)
{
    public string FormerName { get; } = formerName;
    public override Rune Symbol { get; } = new('†');
}

/// <summary>Egy szörny egyszer átkutatható teteme, amely őrzi az eredeti definíció azonosítóját.</summary>
public sealed class MonsterCorpse(Position position, string formerName, string enemyDefinitionId,
    bool isSearched = false, IReadOnlyList<string>? guaranteedLootIds = null) : Corpse(position, formerName)
{
    public string EnemyDefinitionId { get; } = enemyDefinitionId;
    public bool IsSearched { get; private set; } = isSearched;
    public IReadOnlyList<string> GuaranteedLootIds { get; } = guaranteedLootIds ?? [];
    public void MarkSearched() => IsSearched = true;
}
