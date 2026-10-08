namespace KaoszRubin.Domain.Characters;

/// <summary>Az aktív karakterből és a kampányban feloldott számú társából álló kalandozócsapat.</summary>
public sealed class Party
{
    public const int MaximumSize = 6;
    private readonly object _gate = new();
    private readonly List<LiveCharacter> _members = [];
    private readonly PartyCapacityRules _capacityRules;
    private PartyCampaignProgressionSnapshot _campaignProgression = new();

    public Party(PartyCapacityRules? capacityRules = null)
    {
        _capacityRules = capacityRules ?? PartyCapacityRules.Current;
        _capacityRules.Validate();
    }

    public IReadOnlyList<LiveCharacter> Members { get { lock (_gate) return _members.ToArray(); } }
    public LiveCharacter? Leader { get { lock (_gate) return _members.FirstOrDefault(); } }
    public int UnlockedCapacity
    {
        get { lock (_gate) return _capacityRules.UnlockedCapacity(_campaignProgression.HighestCompletedCampaignLevel); }
    }
    public int Capacity { get { lock (_gate) return CurrentCapacity(); } }
    public bool IsFull { get { lock (_gate) return _members.Count >= CurrentCapacity(); } }
    public PartyCampaignProgressionSnapshot CampaignProgression
    {
        get { lock (_gate) return PartyCampaignProgressionSnapshot.Merge(null, _campaignProgression); }
    }
    public IReadOnlyList<PartyExpansionMilestone> AvailableRecruitmentGrants
    {
        get { lock (_gate) return AvailableMilestones(_campaignProgression.ConsumedRecruitmentGrants); }
    }
    public IReadOnlyList<PartyExpansionMilestone> PendingUnlockPresentations
    {
        get { lock (_gate) return AvailableMilestones(_campaignProgression.PresentedUnlocks); }
    }

    public void StartNewCampaign()
    {
        lock (_gate)
        {
            if (_members.Count > PartyCapacityRules.InitialCapacity)
                throw new InvalidOperationException("Új kampány legfeljebb négyfős csapattal indítható.");
            _campaignProgression = new();
        }
    }

    public void MergeCampaignProgression(PartyCampaignProgressionSnapshot? progression)
    {
        lock (_gate)
            _campaignProgression = PartyCampaignProgressionSnapshot.Merge(_campaignProgression, progression);
    }

    /// <summary>Csak sikeresen lezárt fő kampánypályával hívható; a pályára belépés nem teljesítés.</summary>
    public void RecordCampaignLevelCompletion(int completedLevel)
    {
        if (completedLevel < 1) throw new ArgumentOutOfRangeException(nameof(completedLevel));
        MergeCampaignProgression(new(completedLevel));
    }

    public bool TryMarkUnlockPresented(PartyExpansionMilestone milestone)
    {
        lock (_gate)
        {
            if (!AvailableMilestones(_campaignProgression.PresentedUnlocks).Contains(milestone)) return false;
            _campaignProgression = _campaignProgression with
            {
                PresentedUnlocks = (_campaignProgression.PresentedUnlocks ?? []).Append(milestone).ToArray()
            };
            return true;
        }
    }

    /// <summary>A normál zsoldos támogatott felvétele és a támogatás elfogyasztása egyetlen művelet.</summary>
    public bool TryAddWithRecruitmentGrant(LiveCharacter character, PartyExpansionMilestone milestone)
    {
        ArgumentNullException.ThrowIfNull(character);
        lock (_gate)
        {
            if (character.SourceNpcDefinitionId is not null ||
                !AvailableMilestones(_campaignProgression.ConsumedRecruitmentGrants).Contains(milestone) ||
                !TryAdd(character)) return false;
            _campaignProgression = _campaignProgression with
            {
                ConsumedRecruitmentGrants = (_campaignProgression.ConsumedRecruitmentGrants ?? [])
                    .Append(milestone).ToArray()
            };
            return true;
        }
    }

    public void SetLeader(LiveCharacter leader)
    {
        ArgumentNullException.ThrowIfNull(leader);
        lock (_gate)
        {
            _members.Clear();
            _members.Add(leader);
        }
    }

    public bool Add(LiveCharacter character)
    {
        ArgumentNullException.ThrowIfNull(character);
        lock (_gate) return TryAdd(character);
    }

    /// <summary>Csak meglévő társat cserélhet; a vezetőt vagy a kapacitást nem kerüli meg.</summary>
    public bool TryReplaceCompanion(LiveCharacter companion, LiveCharacter replacement)
    {
        ArgumentNullException.ThrowIfNull(companion);
        ArgumentNullException.ThrowIfNull(replacement);
        lock (_gate)
        {
            var index = _members.IndexOf(companion);
            if (index <= 0 || _members.Any(member => member.Id == replacement.Id)) return false;
            _members[index] = replacement;
            return true;
        }
    }

    public bool Remove(LiveCharacter character) { lock (_gate) return _members.Remove(character); }

    public void Restore(LiveCharacter leader, IEnumerable<LiveCharacter> members)
    {
        ArgumentNullException.ThrowIfNull(leader);
        ArgumentNullException.ThrowIfNull(members);
        lock (_gate)
        {
            var restored = members.Prepend(leader).DistinctBy(member => member.Id)
                .Take(CurrentCapacity()).ToArray();
            _members.Clear();
            _members.AddRange(restored);
        }
    }

    public void Clear() { lock (_gate) _members.Clear(); }

    private int CurrentCapacity() => _capacityRules.Capacity(_campaignProgression.HighestCompletedCampaignLevel);

    private bool TryAdd(LiveCharacter character)
    {
        if (_members.Count >= CurrentCapacity() || _members.Any(member => member.Id == character.Id)) return false;
        _members.Add(character);
        return true;
    }

    private PartyExpansionMilestone[] AvailableMilestones(IReadOnlyList<PartyExpansionMilestone>? used) =>
        _capacityRules.ExpandedPartyEnabled
            ? Enum.GetValues<PartyExpansionMilestone>().Where(milestone =>
                _capacityRules.IsEnabled(milestone) && _capacityRules.IsUnlocked(milestone, _campaignProgression.HighestCompletedCampaignLevel) &&
                !(used ?? []).Contains(milestone)).ToArray()
            : [];
}
