using KaoszRubin.Domain;
using KaoszRubin.Domain.Characters;

namespace KaoszRubin.Application;

public sealed class PartyFormationController
{
    public static PartyFormationSnapshot Normalize(
        PartyFormationSnapshot formation,
        IEnumerable<CharacterId> livingMemberIds,
        CharacterId leaderId,
        out bool transitionedToAssembling)
    {
        var previousSlots = formation.Slots;
        var normalized = PartyFormationRules.Normalize(formation, livingMemberIds, leaderId);
        transitionedToAssembling = formation.State == PartyFormationState.Locked && !previousSlots.SequenceEqual(normalized.Slots);
        if (transitionedToAssembling)
        {
            normalized = PartyFormationRules.WithState(normalized, PartyFormationState.Assembling);
        }
        return normalized;
    }

    public static IReadOnlyDictionary<CharacterId, Position> Positions(
        PartyFormationSnapshot formation,
        CharacterId leaderId,
        Position leaderPosition) =>
        PartyFormationRules.Positions(formation, leaderId, leaderPosition);

    public static PartyFormationSnapshot Rotate(PartyFormationSnapshot formation, bool clockwise) =>
        PartyFormationRules.Rotate(formation, clockwise);

    public static int CalculateMoveDelay(IEnumerable<LiveCharacter> members, int controlledMoveDelayMilliseconds = 85)
    {
        var slowestMultiplier = members.Where(member => member.IsAlive)
            .Select(member => CharacterMobilityRules.Evaluate(member).ExplorationDelayMultiplier)
            .DefaultIfEmpty(1)
            .Max();
        return Math.Max(35, (int)Math.Round(controlledMoveDelayMilliseconds * slowestMultiplier * 1.35));
    }

    public static bool CanFormationOccupy(
        IReadOnlyDictionary<CharacterId, Position> positions,
        Maze maze,
        Func<CharacterId, PartyMemberAvatar?> getFormationAvatar)
    {
        if (positions.Values.Distinct().Count() != positions.Count) return false;
        var ownAvatars = positions.Keys.Select(getFormationAvatar).Where(avatar => avatar is not null).ToHashSet();
        foreach (var position in positions.Values)
        {
            if (!maze.IsWalkable(position) || maze.GetEnemyAt(position) is not null) return false;
            var occupant = maze.GetObjectAt(position);
            if (occupant is null or GroundItemPile or Corpse or TreasureChest || Maze.IsPassableNeutralNpc(occupant))
                continue;
            if (occupant is PartyMemberAvatar avatar && ownAvatars.Contains(avatar)) continue;
            return false;
        }
        return true;
    }
}
