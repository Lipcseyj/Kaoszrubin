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
