using KaoszRubin.Data;
using KaoszRubin.Domain;
using KaoszRubin.Domain.Characters;

namespace KaoszRubin.Application;

public sealed class StoryConversationCoordinator
{
    public const string RodericStoryId = "RODERIC_OATH";
    public const string EliraStoryId = "ELIRA_RESCUE";

    private readonly GameDataCatalog _gameData;
    private readonly Random _random;

    public StoryConversationCoordinator(GameDataCatalog gameData, Random random)
    {
        _gameData = gameData;
        _random = random;
    }

    public static bool IsAdHocConversationStory(string? storyId) =>
        string.Equals(storyId, EliraStoryId, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(storyId, RodericStoryId, StringComparison.OrdinalIgnoreCase);

    public static string AdHocConversationId(WorldNpc npc, string startState) =>
        $"{npc.StoryId}:{startState}";

    public IReadOnlyList<WorldNpc> GetAdHocConversationCandidates(
        Maze maze,
        Player player,
        CharacterRoster roster,
        LiveCharacter selectedCharacter)
    {
        var candidates = maze.PartyMembers
            .Where(member => member.TemporaryFollower is { } follower && IsAdHocConversationStory(follower.StoryId))
            .Select(member => member.TemporaryFollower!)
            .Where(npc => npc.Character.IsAlive)
            .ToList();
        var temporaryCharacterIds = candidates.Select(npc => npc.Character.Id).ToHashSet();
        var definitions = _gameData.Npcs.Where(definition => IsAdHocConversationStory(definition.StoryId))
            .ToDictionary(definition => definition.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var character in roster.Party.Members.Where(character => character.IsAlive &&
                     !temporaryCharacterIds.Contains(character.Id)))
        {
            if (!definitions.TryGetValue(character.Name, out var definition)) continue;
            var position = character == selectedCharacter
                ? player.Position
                : maze.PartyMembers.FirstOrDefault(member => member.Character == character)?.Position ??
                  player.Position;
            candidates.Add(new WorldNpc(position, definition.Id, character, definition.Disposition,
                definition.Recruitable, false, string.Empty, friendliness: 10, behavior: definition.Behavior,
                storyId: definition.StoryId));
        }
        return candidates;
    }
}
