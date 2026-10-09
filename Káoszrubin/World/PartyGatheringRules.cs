namespace KaoszRubin.World;

/// <summary>Közös közelségi szabályok az együtt utazó, akár hatfős partihoz.</summary>
public static class PartyGatheringRules
{
    public const int TransitionMaximumDistance = 8;
    public const int ExitEscortMaximumDistance = 7;

    public static PartyMemberAvatar? FirstDistantLivingMember(IEnumerable<PartyMemberAvatar> members,
        Position origin, int maximumDistance = TransitionMaximumDistance) =>
        members.FirstOrDefault(member => member.Character.IsAlive &&
            Math.Abs(member.Position.X - origin.X) + Math.Abs(member.Position.Y - origin.Y) > maximumDistance);
}
