using KaoszRubin.Domain;
using KaoszRubin.Domain.Characters;

namespace KaoszRubin.Application;

public sealed class PartyAiController
{
    private const int MinimumPartyMoveDelayMilliseconds = 250;
    private const int MaximumPartyMoveDelayMilliseconds = 300;
    private const int CatchUpMoveDelayMilliseconds = 90;
    private const int ControlledMoveDelayMilliseconds = 85;

    private readonly Random _random;

    public PartyAiController(Random random)
    {
        _random = random;
    }

    public bool CanActivelyAttack(bool partyAttackMode, PartyMemberAvatar member) =>
        partyAttackMode || member.Character.NpcBehavior is NpcBehavior.Defensive or NpcBehavior.Aggressive;

    public bool TryResolveAdjacentNpcBattle(Maze maze, PartyMemberAvatar member, Action<PartyMemberAvatar, Enemy> startBattle)
    {
        var enemy = Enum.GetValues<Direction>()
            .Select(direction => maze.GetEnemyAt(member.Position + direction))
            .FirstOrDefault(candidate => candidate is not null);
        if (enemy is null) return false;
        startBattle(member, enemy);
        return true;
    }

    public void ScheduleNextPartyMove(PartyMemberAvatar member, DateTime from, Player player,
        IDictionary<PartyMemberAvatar, DateTime> nextPartyMoves)
    {
        var distance = PartyMovementController.Manhattan(member.Position, player.Position);
        var minimumDelay = distance >= 8 ? CatchUpMoveDelayMilliseconds :
            distance >= 5 ? 130 : MinimumPartyMoveDelayMilliseconds;
        var maximumDelay = distance >= 8 ? CatchUpMoveDelayMilliseconds + 30 :
            distance >= 5 ? 170 : MaximumPartyMoveDelayMilliseconds;
        (minimumDelay, maximumDelay) = CharacterMobilityRules.ScaleExplorationDelay(member.Character,
            minimumDelay, maximumDelay);
        nextPartyMoves[member] = from + TimeSpan.FromMilliseconds(_random.Next(minimumDelay, maximumDelay + 1));
    }

    public bool CanControlledCharacterMove(LiveCharacter character, IReadOnlyDictionary<CharacterId, DateTime> nextControlledMoves) =>
        nextControlledMoves.GetValueOrDefault(character.Id) <= DateTime.UtcNow;

    public void ScheduleNextControlledMove(LiveCharacter character, IDictionary<CharacterId, DateTime> nextControlledMoves)
    {
        var delay = Math.Max(35, (int)Math.Round(ControlledMoveDelayMilliseconds *
            CharacterMobilityRules.Evaluate(character).ExplorationDelayMultiplier));
        nextControlledMoves[character.Id] = DateTime.UtcNow + TimeSpan.FromMilliseconds(delay);
    }
}
