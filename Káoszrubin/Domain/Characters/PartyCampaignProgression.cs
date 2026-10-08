namespace KaoszRubin.Domain.Characters;

/// <summary>Stabil azonosítók a két egyszeri partihely-jutalomhoz.</summary>
public enum PartyExpansionMilestone { FifthMember, SixthMember }

public sealed record PartyCampaignProgressionSnapshot(
    int HighestCompletedCampaignLevel = 0,
    IReadOnlyList<PartyExpansionMilestone>? ConsumedRecruitmentGrants = null,
    IReadOnlyList<PartyExpansionMilestone>? PresentedUnlocks = null)
{
    public static PartyCampaignProgressionSnapshot Merge(PartyCampaignProgressionSnapshot? current,
        PartyCampaignProgressionSnapshot? incoming) => new(
        Math.Max(0, Math.Max(current?.HighestCompletedCampaignLevel ?? 0,
            incoming?.HighestCompletedCampaignLevel ?? 0)),
        (current?.ConsumedRecruitmentGrants ?? []).Concat(incoming?.ConsumedRecruitmentGrants ?? [])
            .Where(value => Enum.IsDefined(value)).Distinct().Order().ToArray(),
        (current?.PresentedUnlocks ?? []).Concat(incoming?.PresentedUnlocks ?? [])
            .Where(value => Enum.IsDefined(value)).Distinct().Order().ToArray());
}

/// <summary>A küszöbök kampánypályákra vonatkoznak, nem karakterszintekre.</summary>
public sealed record PartyCapacityRules
{
    public const int InitialCapacity = 4;
    // Az 5. és 8. sikeresen lezárt főpálya oldja fel az ötödik és hatodik helyet.
    public static PartyCapacityRules Current { get; } = new() { ExpandedPartyEnabled = true };
    public bool ExpandedPartyEnabled { get; init; }
    public int MaximumEnabledCapacity { get; init; } = Party.MaximumSize;
    public int FifthMemberCompletedLevel { get; init; } = 5;
    public int SixthMemberCompletedLevel { get; init; } = 8;

    public void Validate()
    {
        if (MaximumEnabledCapacity < InitialCapacity || MaximumEnabledCapacity > Party.MaximumSize)
            throw new ArgumentOutOfRangeException(nameof(MaximumEnabledCapacity));
        if (FifthMemberCompletedLevel < 1 || SixthMemberCompletedLevel <= FifthMemberCompletedLevel)
            throw new ArgumentException("A partihelyek kampányküszöbei pozitívak és növekvők legyenek.");
    }

    public int UnlockedCapacity(int highestCompletedCampaignLevel) =>
        highestCompletedCampaignLevel >= SixthMemberCompletedLevel ? Party.MaximumSize :
        highestCompletedCampaignLevel >= FifthMemberCompletedLevel ? 5 : InitialCapacity;

    public int Capacity(int highestCompletedCampaignLevel) => ExpandedPartyEnabled
        ? Math.Min(MaximumEnabledCapacity, UnlockedCapacity(highestCompletedCampaignLevel)) : InitialCapacity;

    public bool IsEnabled(PartyExpansionMilestone milestone) => ExpandedPartyEnabled && milestone switch
    {
        PartyExpansionMilestone.FifthMember => MaximumEnabledCapacity >= 5,
        PartyExpansionMilestone.SixthMember => MaximumEnabledCapacity >= 6,
        _ => false
    };

    public bool IsUnlocked(PartyExpansionMilestone milestone, int highestCompletedCampaignLevel) =>
        milestone switch
        {
            PartyExpansionMilestone.FifthMember => highestCompletedCampaignLevel >= FifthMemberCompletedLevel,
            PartyExpansionMilestone.SixthMember => highestCompletedCampaignLevel >= SixthMemberCompletedLevel,
            _ => false
        };
}
