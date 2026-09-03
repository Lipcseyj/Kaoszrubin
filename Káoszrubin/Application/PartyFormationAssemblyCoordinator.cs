using KaoszRubin.Domain;
using KaoszRubin.Domain.Characters;

namespace KaoszRubin.Application;

public sealed class PartyFormationAssemblyCoordinator
{
    public sealed record Result(
        bool AllInPlace,
        bool MadeProgress,
        bool ObstacleReported,
        bool BattleStarted);

    public static Result Advance(
        DateTime now,
        Maze maze,
        Player player,
        CharacterId selectedCharacterId,
        IReadOnlyDictionary<CharacterId, Position> targets,
        IEnumerable<PartyMemberAvatar> members,
        IDictionary<PartyMemberAvatar, DateTime> nextPartyMoves,
        Func<PartyMemberAvatar, Position, IReadOnlyDictionary<CharacterId, Position>, Position?> findNextStep,
        Func<PartyMemberAvatar, Position, bool> canEnterTrap,
        Func<PartyMemberAvatar, Position, PartyMemberAvatar?> getPartyMemberAt,
        Func<PartyMemberAvatar, Position, bool> tryMovePartyMember,
        Func<PartyMemberAvatar, PartyMemberAvatar, Position, bool> trySwapPartyMembers,
        Action<PartyMemberAvatar, Position> registerFormationMove,
        Action<PartyMemberAvatar, DateTime> scheduleNextMove,
        Func<PartyMemberAvatar, Position, Enemy?> getEnemyAt,
        Action<PartyMemberAvatar, Enemy> startBattle,
        Func<CharacterId, PartyMemberAvatar?> getFormationAvatar)
    {
        var allInPlace = true;
        var madeProgress = false;

        foreach (var member in members.Where(member => !member.IsTemporaryFollower).ToArray())
        {
            if (!targets.TryGetValue(member.Character.Id, out var target) || member.Position == target) continue;
            allInPlace = false;
            if (nextPartyMoves.TryGetValue(member, out var nextMove) && nextMove > now) continue;

            scheduleNextMove(member, now);
            var next = findNextStep(member, target, targets);
            var previous = member.Position;
            if (next is { } nextPosition && getEnemyAt(member, nextPosition) is { } enemy)
            {
                startBattle(member, enemy);
                return new Result(false, false, false, true);
            }

            if (next is null || !canEnterTrap(member, next.Value)) continue;
            if (getPartyMemberAt(member, next.Value) is { } blockingFriend && blockingFriend != member)
            {
                var blockerAtOwnTarget = targets.TryGetValue(blockingFriend.Character.Id, out var blockerTarget) &&
                                         blockerTarget == blockingFriend.Position;
                if (blockerAtOwnTarget || !trySwapPartyMembers(member, blockingFriend, next.Value))
                    continue;
                madeProgress = true;
                registerFormationMove(member, previous);
                registerFormationMove(blockingFriend, next.Value);
                scheduleNextMove(blockingFriend, now);
                continue;
            }

            if (!tryMovePartyMember(member, next.Value)) continue;
            madeProgress = true;
            registerFormationMove(member, previous);
        }

        allInPlace = allInPlace || targets.All(pair =>
            pair.Key == selectedCharacterId
                ? player.Position == pair.Value
                : getFormationAvatar(pair.Key)?.Position == pair.Value);

        if (allInPlace)
            return new Result(true, madeProgress, false, false);

        var obstacleReported = !madeProgress && targets.Values.Any(position =>
            !maze.IsWalkable(position) ||
            (maze.GetObjectAt(position) is { } occupant && occupant is not PartyMemberAvatar &&
             occupant is not (GroundItemPile or Corpse or TreasureChest) &&
             !Maze.IsPassableNeutralNpc(occupant)));

        return new Result(false, madeProgress, obstacleReported, false);
    }
}
