using KaoszRubin.Domain;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Quests;

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
        Enemy defeatedEnemy);

    /// <summary>
    /// Igaz, ha az NPC él és jelenleg a partit követi.
    /// </summary>
    bool IsNpcAliveAndFollowing(
        QuestNpcId npcId,
        QuestNpcInstanceId instanceId);
}