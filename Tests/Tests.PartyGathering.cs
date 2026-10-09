internal static partial class Program
{
    static void PartyGatheringAllowsLargerTravelRadius()
    {
        var origin = new Position(10, 10);
        var members = new[]
        {
            new PartyMemberAvatar(new(10, 11), CreateCharacter("Társ 1")),
            new PartyMemberAvatar(new(9, 10), CreateCharacter("Társ 2")),
            new PartyMemberAvatar(new(11, 10), CreateCharacter("Társ 3")),
            new PartyMemberAvatar(new(10, 3), CreateCharacter("Társ 4")),
            new PartyMemberAvatar(new(16, 12), CreateCharacter("Társ 5"))
        };
        Assert(PartyGatheringRules.TransitionMaximumDistance == 8 &&
            PartyGatheringRules.ExitEscortMaximumDistance == 7 &&
            PartyGatheringRules.FirstDistantLivingMember(members, origin) is null,
            "A hatfős parti nyolcmezős gyülekezési határa nem érvényesül.");
        members[4].MoveTo(new(16, 13));
        Assert(PartyGatheringRules.FirstDistantLivingMember(members, origin) == members[4],
            "A kilenc mezőre maradt társ is átkelhet.");
        members[4].Character.ReceiveDamage(1000);
        Assert(PartyGatheringRules.FirstDistantLivingMember(members, origin) is null,
            "A halott társ megakadályozza az együtt utazást.");
        Assert(PartyGatheringRules.FirstDistantLivingMember([members[3]], origin,
            PartyGatheringRules.ExitEscortMaximumDistance) is null, "A kísérő hétmezős határa túl szigorú.");
        members[3].MoveTo(new(10, 2));
        Assert(PartyGatheringRules.FirstDistantLivingMember([members[3]], origin,
            PartyGatheringRules.ExitEscortMaximumDistance) == members[3], "A nyolcmezős kísérő is célba ért.");
    }

    static void RegroupingCanFillLastLeaderNeighbor()
    {
        var maze = new Maze(9, 9);
        var leader = new Player(new(4, 4), CreateCharacter("Vezér"));
        foreach (var position in new[] { leader.Position, new Position(3, 4), new Position(3, 5) })
            maze.Carve(position);
        var member = new PartyMemberAvatar(new(3, 5), CreateCharacter("Gyűlő társ"));
        maze.AddPartyMember(member);
        var trail = new[] { member.Position, new Position(3, 4), leader.Position };
        Assert(!PartyMovementController.PreservesLeaderExit(member, new(3, 4), maze, leader),
            "A normál követés nem tartja szabadon az utolsó kijáratot.");
        var next = PartyMovementController.ChooseRegroupStep(member, maze, leader, trail);
        Assert(next == new Position(3, 4) && maze.TryMovePartyMember(member, next.Value, leader.Position),
            "A kért gyülekező továbbra is kihagy egy helyet a vezértől.");
        Assert(PartyMovementController.ChooseRegroupStep(member, maze, leader, trail) is null,
            "A gyülekező társ nem maradt a vezér mellett.");
        Assert(maze.TrySwapLeaderAndPartyMember(leader, member), "A szoros gyülekezőből nem lehet helycserével kilépni.");

        var crowded = new Maze(21, 21);
        for (var y = 1; y < 20; y++)
        for (var x = 1; x < 20; x++) crowded.Carve(new(x, y));
        var host = new Player(new(10, 10), CreateCharacter("Hatos vezér"));
        var avatars = new[]
        {
            new PartyMemberAvatar(new(10, 11), CreateCharacter("Egy")),
            new PartyMemberAvatar(new(9, 10), CreateCharacter("Kettő")),
            new PartyMemberAvatar(new(11, 10), CreateCharacter("Három")),
            new PartyMemberAvatar(new(10, 8), CreateCharacter("Négy")),
            new PartyMemberAvatar(new(9, 11), CreateCharacter("Öt"))
        };
        foreach (var avatar in avatars) crowded.AddPartyMember(avatar);
        var step = PartyMovementController.ChooseRegroupStep(avatars[3], crowded, host,
            [new(10, 8), new(10, 9), host.Position]);
        Assert(step == new Position(10, 9) && crowded.TryMovePartyMember(avatars[3], step.Value, host.Position),
            "A negyedik társ nem töltheti ki az utolsó szomszédos helyet.");
        Assert(PartyMovementController.ChooseRegroupStep(avatars[4], crowded, host,
            [new(9, 12), avatars[4].Position, host.Position]) is null &&
            PartyGatheringRules.FirstDistantLivingMember(avatars, host.Position) is null,
            "A hatfős gyülekező foglalt helyre lépne vagy nem utazhat együtt.");
    }
}
