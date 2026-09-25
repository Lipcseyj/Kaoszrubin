namespace KaoszRubin.World;

/// <summary>Egy labirintusszint képernyőnkénti egyszeri pihenéseit tartja nyilván.</summary>
public sealed class DungeonRestState
{
    private readonly HashSet<string> _restedAreaIds = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> RestedAreaIds => _restedAreaIds;

    public bool HasRested(string areaId) => _restedAreaIds.Contains(areaId);

    public bool TryMarkRested(string areaId) => _restedAreaIds.Add(areaId);

    public void Reset() => _restedAreaIds.Clear();

    public void MarkAll(IEnumerable<string> areaIds)
    {
        foreach (var areaId in areaIds) _restedAreaIds.Add(areaId);
    }

    public void Restore(IEnumerable<string>? restedAreaIds, bool legacyHasRestedThisLevel,
        IEnumerable<string> validAreaIds)
    {
        Reset();
        var valid = validAreaIds.ToHashSet(StringComparer.Ordinal);
        var saved = (restedAreaIds ?? []).Distinct(StringComparer.Ordinal).ToArray();
        if (saved.Length == 0 && legacyHasRestedThisLevel)
        {
            MarkAll(valid);
            return;
        }
        if (saved.Any(areaId => !valid.Contains(areaId)))
            throw new InvalidDataException("A mentés ismeretlen pihenési képernyőazonosítót tartalmaz.");
        MarkAll(saved);
    }
}
