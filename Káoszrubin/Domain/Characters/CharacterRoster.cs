namespace KaoszRubin.Domain.Characters;

public enum CharacterCampaignStatus { Active, Completed }
public sealed record CharacterCampaignBinding(Guid CampaignId, CharacterCampaignStatus Status,
    int LastKnownLevel, DateTimeOffset LastPlayedAt);

/// <summary>A már generált, használható karakterek listája. Új játék kezdetén üres.</summary>
public sealed class CharacterRoster
{
    private readonly List<LiveCharacter> _characters = [];
    private readonly Dictionary<CharacterId, CharacterCampaignBinding> _campaigns = [];
    public IReadOnlyList<LiveCharacter> Characters => _characters;
    public CharacterRoster(PartyCapacityRules? capacityRules = null) => Party = new(capacityRules);
    public Party Party { get; }
    public LiveCharacter? SelectedCharacter { get; private set; }
    public CharacterCampaignBinding? CampaignOf(LiveCharacter character) => _campaigns.GetValueOrDefault(character.Id);
    public void BindCampaign(LiveCharacter character, Guid campaignId, int level,
        CharacterCampaignStatus status = CharacterCampaignStatus.Active) =>
        _campaigns[character.Id] = new(campaignId, status, level, DateTimeOffset.Now);
    public void Add(LiveCharacter character) => _characters.Add(character);
    public void Select(LiveCharacter character)
    {
        if (!_characters.Contains(character)) throw new ArgumentException("Csak a karakterlistában szereplő karakter választható.", nameof(character));
        SelectedCharacter = character;
        Party.SetLeader(character);
    }

    public bool Remove(LiveCharacter character)
    {
        if (!_characters.Remove(character)) return false;
        _campaigns.Remove(character.Id);
        if (SelectedCharacter == character)
        {
            SelectedCharacter = null;
            Party.Clear();
        }
        else Party.Remove(character);
        return true;
    }

    public bool Replace(LiveCharacter current, LiveCharacter replacement)
    {
        var index = _characters.IndexOf(current);
        if (index < 0 || replacement.Id != current.Id) return false;
        var partyMembers = Party.Members.Select(member => member == current ? replacement : member).ToArray();
        var leader = Party.Leader == current ? replacement : Party.Leader;
        _characters[index] = replacement;
        if (SelectedCharacter == current) SelectedCharacter = replacement;
        if (leader is not null) Party.Restore(leader, partyMembers);
        return true;
    }

    /// <summary>A régebbi játékmentés betöltése nem írhatja felül a többi helyi karaktert.</summary>
    public void PreserveCharactersOutsidePartyFrom(CharacterRoster localRoster)
    {
        ArgumentNullException.ThrowIfNull(localRoster);
        if (ReferenceEquals(this, localRoster)) return;
        var partyIds = Party.Members.Select(member => member.Id).ToHashSet();
        foreach (var localCharacter in localRoster.Characters)
        {
            if (partyIds.Contains(localCharacter.Id)) continue;
            var savedIndex = _characters.FindIndex(character => character.Id == localCharacter.Id);
            if (savedIndex < 0) _characters.Add(localCharacter);
            else _characters[savedIndex] = localCharacter;
            if (localRoster._campaigns.TryGetValue(localCharacter.Id, out var binding))
                _campaigns[localCharacter.Id] = binding;
            else
                _campaigns.Remove(localCharacter.Id);
        }
    }

    public void Clear()
    {
        _characters.Clear();
        _campaigns.Clear();
        SelectedCharacter = null;
        Party.Clear();
    }
}
