using KaoszRubin.Domain;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Quests;
using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Application.Quests;

/// <summary>
/// A quest-rendszer számára szükséges, játékvilágból származó információk.
/// Elrejti a Maze, PartyMembers, TemporaryFollower stb. konkrét szerkezetét.
/// </summary>
public interface IQuestWorldContext
{
    /// <summary>
    /// Igaz, ha az adott quest NPC él és ténylegesen részt vesz
    /// abban a harcban, amelyben az ellenfél vereséget szenvedett.
    /// </summary>
    bool IsNpcParticipatingInCombat(
        QuestNpcId npcId,
        QuestNpcInstanceId instanceId,
        Enemy defeatedEnemy,
        int maximumDistance);

    /// <summary>
    /// Igaz, ha az NPC él és jelenleg a partit követi.
    /// </summary>
    bool IsNpcAliveAndFollowing(
        QuestNpcId npcId,
        QuestNpcInstanceId instanceId);

    /// <summary>
    /// Visszaadja az adott NPC aktuális, quest-rendszer számára
    /// értelmezett történetállapotát.
    /// </summary>
    QuestStoryState GetNpcStoryState(
        QuestNpcId npcId,
        QuestNpcInstanceId instanceId);

    int CountPartyItem(IItemDefinition item);
}