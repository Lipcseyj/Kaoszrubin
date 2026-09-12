using KaoszRubin.Domain.Quests;

namespace KaoszRubin.Application.Quests;

/// <summary>
/// A játék erősen tipizált quest-definícióinak központi,
/// csak olvasható katalógusa.
/// </summary>
public sealed class QuestCatalog
{
    private readonly IReadOnlyList<QuestDefinition> _all;
    private readonly IReadOnlyDictionary<QuestId, QuestDefinition> _byId;
    private readonly IReadOnlyDictionary<QuestNpcId, IReadOnlyList<QuestDefinition>> _byGiver;

    public IReadOnlyList<QuestDefinition> All => _all;

    public int Count => _all.Count;

    public QuestCatalog(IEnumerable<QuestDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var all = definitions.ToArray();

        ValidateDefinitions(all);

        _all = all;

        _byId = all.ToDictionary(
            quest => quest.Id);

        _byGiver = all
            .GroupBy(quest => quest.Giver)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<QuestDefinition>)group.ToArray());
    }

    /// <summary>
    /// Visszaadja a megadott quest definícióját.
    /// Hibát dob, ha az azonosító nem létezik.
    /// </summary>
    public QuestDefinition Get(QuestId id)
    {
        EnsureValidQuestId(id);

        return _byId.TryGetValue(id, out var quest)
            ? quest
            : throw new KeyNotFoundException(
                $"A(z) '{id}' quest nem található a QuestCatalogban.");
    }

    /// <summary>
    /// Megpróbálja lekérni a megadott quest definícióját.
    /// </summary>
    public bool TryGet(
        QuestId id,
        out QuestDefinition definition)
    {
        if (id == QuestId.None)
        {
            definition = null!;
            return false;
        }

        return _byId.TryGetValue(id, out definition!);
    }

    /// <summary>
    /// Visszaadja az adott NPC-hez tartozó összes questet.
    /// </summary>
    public IReadOnlyList<QuestDefinition> GetByGiver(
        QuestNpcId giver)
    {
        EnsureValidNpcId(giver);

        return _byGiver.TryGetValue(giver, out var quests)
            ? quests
            : Array.Empty<QuestDefinition>();
    }

    /// <summary>
    /// Igaz, ha a katalógus tartalmazza a megadott questet.
    /// </summary>
    public bool Contains(QuestId id) =>
        id != QuestId.None &&
        _byId.ContainsKey(id);

    private static void ValidateDefinitions(
        IReadOnlyList<QuestDefinition> definitions)
    {
        foreach (var quest in definitions)
        {
            if (quest.Id == QuestId.None)
                throw new InvalidOperationException(
                    "QuestDefinition nem használhat QuestId.None azonosítót.");

            if (quest.Giver == QuestNpcId.None)
                throw new InvalidOperationException(
                    $"A(z) '{quest.Id}' questhez nincs érvényes questadó NPC.");

            if (string.IsNullOrWhiteSpace(quest.Title))
                throw new InvalidOperationException(
                    $"A(z) '{quest.Id}' quest címe üres.");

            if (string.IsNullOrWhiteSpace(quest.Description))
                throw new InvalidOperationException(
                    $"A(z) '{quest.Id}' quest leírása üres.");

            if (quest.Objective is null)
                throw new InvalidOperationException(
                    $"A(z) '{quest.Id}' questhez nincs objective.");

            if (quest.Objective.RequiredCount <= 0)
                throw new InvalidOperationException(
                    $"A(z) '{quest.Id}' quest RequiredCount értéke nem lehet nulla vagy negatív.");

            if (quest.ExperienceReward < 0)
                throw new InvalidOperationException(
                    $"A(z) '{quest.Id}' quest XP jutalma nem lehet negatív.");

            if (quest.FixedRewardItem is null &&
                quest.FixedRewardItemCount != 0)
            {
                throw new InvalidOperationException(
                    $"A(z) '{quest.Id}' questhez nincs fix jutalomtárgy, " +
                    "de a jutalom darabszáma nem nulla.");
            }

            if (quest.FixedRewardItem is not null &&
                quest.FixedRewardItemCount <= 0)
            {
                throw new InvalidOperationException(
                    $"A(z) '{quest.Id}' quest fix jutalomtárgyához " +
                    "pozitív darabszám szükséges.");
            }

            if (quest.RandomRewardCount < 0)
                throw new InvalidOperationException(
                    $"A(z) '{quest.Id}' véletlen jutalmainak száma nem lehet negatív.");
        }

        var duplicateIds = definitions
            .GroupBy(quest => quest.Id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicateIds.Length > 0)
        {
            throw new InvalidOperationException(
                "Többször szereplő QuestId a QuestCatalogban: " +
                string.Join(", ", duplicateIds));
        }
    }

    private static void EnsureValidQuestId(QuestId id)
    {
        if (id == QuestId.None)
            throw new ArgumentException(
                "QuestId.None nem használható quest lekéréséhez.",
                nameof(id));
    }

    private static void EnsureValidNpcId(QuestNpcId id)
    {
        if (id == QuestNpcId.None)
            throw new ArgumentException(
                "QuestNpcId.None nem használható questek lekéréséhez.",
                nameof(id));
    }
}