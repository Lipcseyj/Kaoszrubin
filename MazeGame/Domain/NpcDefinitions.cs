using MazeGame.Domain.Characters;

namespace MazeGame.Domain;

public enum NpcWorldBehavior { Friendly, Guarded, Opportunistic, Aggressive }
public enum NpcQuestType { Collect, Kill, Explore, Disarm, OpenChest }

public sealed record NpcDefinition(string Id, string Name, string CharacterClassId,
    NpcDisposition Disposition, NpcWorldBehavior Behavior, bool Recruitable, bool Unique) : IGameDefinition;

public sealed record NpcEncounterDefinition(string Id, string NpcId, int MazeLevel,
    int MinimumDistance, int MaximumDistance) : IGameDefinition
{
    public string Name => Id;
}

public sealed record NpcDialogueDefinition(string Id, string NpcId, int MinimumFriendliness,
    int MaximumFriendliness, string Text) : IGameDefinition
{
    public string Name => Id;
}

public sealed record NpcQuestDefinition(string Id, string NpcId, NpcQuestType Type, string TargetId,
    int RequiredCount, int ExperienceReward, string Title, string Description) : IGameDefinition
{
    public string Name => Title;
}
