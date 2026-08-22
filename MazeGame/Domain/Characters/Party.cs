namespace MazeGame.Domain.Characters;

/// <summary>Az aktív karakterből és legfeljebb három társából álló kalandozócsapat.</summary>
public sealed class Party
{
    public const int MaximumSize = 4;
    private readonly List<LiveCharacter> _members = [];
    public IReadOnlyList<LiveCharacter> Members => _members;
    public LiveCharacter? Leader => _members.FirstOrDefault();

    public void SetLeader(LiveCharacter leader)
    {
        _members.Clear();
        _members.Add(leader);
    }

    public bool Add(LiveCharacter character)
    {
        if (_members.Count >= MaximumSize || _members.Contains(character)) return false;
        _members.Add(character);
        return true;
    }

    public bool Remove(LiveCharacter character) => _members.Remove(character);

    public void Restore(LiveCharacter leader, IEnumerable<LiveCharacter> members)
    {
        SetLeader(leader);
        foreach (var member in members.Where(member => member != leader).Take(MaximumSize - 1)) Add(member);
    }

    public void Clear() => _members.Clear();
}
