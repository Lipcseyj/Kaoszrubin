namespace KaoszRubin.Domain;

public static class PartySituationIds
{
    public const string EnemySpotted = "S001";
    public const string BattleStarted = "S002";
    public const string BattleWon = "S003";
    public const string PartyMemberDied = "S004";
    public const string Hungry = "S005";
    public const string Injured = "S006";
    public const string TreasureChestFound = "S007";
    public const string Resting = "S008";
    public const string Thirsty = "S009";
}

public sealed record PartySituationDefinition(string Id, string Name) : IGameDefinition;

public sealed record PartyRemarkDefinition(string SituationId, string Id, string RaceId,
    string CharacterClassId, string Text) : IGameDefinition
{
    public string Name => Id;
}
