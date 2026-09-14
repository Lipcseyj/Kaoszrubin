using KaoszRubin.Domain.Characters;

namespace KaoszRubin.Domain;

public enum NpcWorldBehavior { Friendly, Guarded, Opportunistic, Aggressive }

public sealed record NpcDefinition(string Id, string Name, string CharacterClassId,
    NpcDisposition Disposition, NpcWorldBehavior Behavior, bool Recruitable, bool Unique,
    string? RaceId = null, string? StoryId = null) : IGameDefinition;

public sealed record UniqueNpcCharacterDefinition(string NpcId, int Level, PrimaryAbilities RolledAbilities,
    PrimaryAbilities AdaptableAbilityBonus, int VitalityBonus, int ManaBonus, ConsoleColor Color,
    NpcBehavior Behavior, string? SpecializationId, IReadOnlyList<string> PerkIds,
    string? FirstWeaponId, string? SecondWeaponId, string? ArmorId,
    IReadOnlyList<string> MagicItemIds, IReadOnlyList<string> BackpackItemIds,
    IReadOnlyList<string> WeaponProficiencyIds) : IGameDefinition
{
    public string Id => NpcId;
    public string Name => NpcId;
}

public sealed record NpcEncounterDefinition(string Id, string NpcId, int MazeLevel,
    int MinimumDistance, int MaximumDistance, string? QuestRoomId = null) : IGameDefinition
{
    public string Name => Id;
}

public sealed record NpcDialogueDefinition(string Id, string NpcId, int MinimumFriendliness,
    int MaximumFriendliness, string Text) : IGameDefinition
{
    public string Name => Id;
}

public sealed record NpcStoryChoiceDefinition(string Id, string StoryId, string StateId, string Prompt,
    int Order, string Text, int FriendlinessChange, string NextStateId,
    string Response = "", NpcStoryAction Action = NpcStoryAction.None,
    string? ActionParameter = null, bool ContinueConversation = false,
    int MinimumFriendliness = 0, int MaximumFriendliness = 10) : IGameDefinition
{
    public string Name => Id;
    public bool IsAvailableAt(int friendliness) =>
        friendliness >= MinimumFriendliness && friendliness <= MaximumFriendliness;
}

public enum NpcStoryAction
{
    None,
    ActivateQuest,
    BeginFollowing,
    TravelToLocation,
    RequestPermanentJoin
}
