internal static partial class Program
{
    static void RubyFireSpreadsAcrossMenuWidth()
    {
        var fire = new RubyFireEffect(ConsoleRenderer.PlayfieldWidth, 8, new Random(43050));
        for (var frame = 0; frame < 24; frame++) fire.Update();

        Assert(fire.Width == ConsoleRenderer.PlayfieldWidth && fire.Height == 8,
            "A rubintűz nem a főmenü képszélességét vagy a 43–50. sor nyolc sorát használja.");
        Assert(Enumerable.Range(0, fire.Width).All(x => fire.IntensityAt(x, fire.Height - 1) > 0),
            "A rubintűz forrássora nem ér végig a kép szélességén.");
        Assert(Enumerable.Range(0, fire.Width).Any(x => fire.IntensityAt(x, 0) > 0) &&
               Enumerable.Range(0, fire.Width).SelectMany(x => Enumerable.Range(0, fire.Height)
                   .Select(y => fire.IntensityAt(x, y))).All(value => value <= 9),
            "A láng nem terjed fel a teljes sávban, vagy kilépett az intenzitástartományból.");
    }

    static void BackgroundMusicMissingTrackReportingIsBounded()
    {
        var settings = new GameSettings { MusicEnabled = true };
        using var player = new BackgroundMusicPlayer(settings, trackResolver: (_, _) => null);

        // Callback nélkül a sikertelen próba nem számít jelentettnek: a később
        // bekötött UI-nak még meg kell kapnia a diagnosztikai üzenetet.
        player.EnterMap();
        var messages = new List<string>();
        player.SetReportCallback(messages.Add);
        player.EnterMap();
        player.EnterMap();
        player.EnterInn();
        player.EnterInn();
        player.EnterMap();

        Assert(messages.Count(message => message.Contains("Music\\Map", StringComparison.Ordinal)) == 1 &&
               messages.Count(message => message.Contains("Music\\Inn", StringComparison.Ordinal)) == 1 &&
               messages.All(message => message.Contains("nem található lejátszható MP3-fájl",
                   StringComparison.Ordinal)),
            "A hiányzó zene jelzése elveszett vagy ismét elárasztotta a callbacket.");
    }

    static void TerminalViewportRequiresCompleteGameScreen()
    {
        var minimumWidth = ConsoleRenderer.PlayfieldWidth + 1;
        var minimumHeight = ConsoleRenderer.ScreenRowCount;
        Assert(!new TerminalViewport.Size(minimumWidth - 1, minimumHeight).CanFit(minimumWidth, minimumHeight),
            "Egy hiányzó oszlopnál a játéknak várakoznia kell.");
        Assert(!new TerminalViewport.Size(minimumWidth, minimumHeight - 1).CanFit(minimumWidth, minimumHeight),
            "Egy hiányzó sornál a játéknak várakoznia kell.");
        Assert(new TerminalViewport.Size(minimumWidth, minimumHeight).CanFit(minimumWidth, minimumHeight),
            "A pontos minimális méretnek már használhatónak kell lennie.");
    }

    static void WindowsTerminalRelaunchGuardsAreStable()
    {
        Assert(SystemHelpers.ShouldRelaunchInWindowsTerminal(
                isWindows: true, hasChildMarker: false, debuggerAttached: false),
            "A közvetlen Windows-indítás nem kérte a Windows Terminalt.");
        Assert(!SystemHelpers.ShouldRelaunchInWindowsTerminal(
                   isWindows: true, hasChildMarker: true, debuggerAttached: false) &&
               !SystemHelpers.ShouldRelaunchInWindowsTerminal(
                   isWindows: true, hasChildMarker: false, debuggerAttached: true) &&
               !SystemHelpers.ShouldRelaunchInWindowsTerminal(
                   isWindows: false, hasChildMarker: false, debuggerAttached: false),
            "A Windows Terminal újraindítási őrfeltételei ciklust vagy debuggerleválást engednek.");
    }

    static void WindowsTerminalHandshakeArgumentIsValidated()
    {
        const string id = "0123456789abcdef0123456789abcdef";
        Assert(SystemHelpers.GetTerminalHandshakeId([$"{SystemHelpers.TerminalChildArgument}={id}"]) == id,
            "Az érvényes gyermek-kézfogás azonosítója nem olvasható vissza.");
        Assert(SystemHelpers.GetTerminalHandshakeId([SystemHelpers.TerminalChildArgument]) is null &&
               SystemHelpers.GetTerminalHandshakeId([$"{SystemHelpers.TerminalChildArgument}=hibás"]) is null &&
               SystemHelpers.GetTerminalHandshakeId(null) is null,
            "A hiányzó vagy hibás gyermek-kézfogás azonosítója elfogadásra került.");
    }

    static void DefensiveSpellSoundIsShared()
    {
        var leader = CreateCharacter("Hallgató");
        var caster = CreateCharacter("Varázsló");
        var target = CreateCharacter("Sérült társ");
        var party = new Party();
        party.SetLeader(leader);
        party.Add(caster);
        party.Add(target);
        using var audio = new SoundEffects(new GameSettings { SoundEffectsEnabled = false });
        var events = new SessionEventService(new ConsoleRenderer(new GameDataCatalog(), party), audio, new Random(1));

        // A map self-buff and healing another companion both excluded the leader before.
        foreach (var listeners in new CharacterId[][] { [caster.Id], [caster.Id, target.Id] })
        {
            events.PlaySessionSound(SoundEffect.DefensiveSpell, listeners, leader.Id);
            var sound = events.Sounds.Last();
            Assert(sound.Effect == SoundEffect.DefensiveSpell && party.Members.All(member => sound.IsAudibleTo(member.Id)),
                "A defenzív varázslathang csak a varázslóhoz vagy célpontjához jutott el.");
            var remoteSound = JsonSerializer.Deserialize<SessionSoundSnapshot>(JsonSerializer.Serialize(sound))!;
            Assert(remoteSound.IsAudibleTo(leader.Id) && remoteSound.IsAudibleTo(caster.Id),
                "A közös defenzív hang címzése nem maradt meg hálózati továbbításkor.");
        }

        events.PlaySessionSound(SoundEffect.Step1, [caster.Id], leader.Id);
        Assert(!events.Sounds.Last().IsAudibleTo(leader.Id),
            "A javítás egy karakterhez címzett lépéshangot is közössé tett.");
    }
}
