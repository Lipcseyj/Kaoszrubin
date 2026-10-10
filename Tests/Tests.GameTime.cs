internal static partial class Program
{
    static void GameTimeRoundsRestAndDaylight()
    {
        var clock = new GameTimeClock();
        Assert(clock.Snapshot is { Day: 1, Hour: 8, Minute: 0, IsDaytime: true },
            "A kampány nem az első nap reggelén kezdődik.");
        for (var round = 0; round < 59; round++) clock.AdvanceExplorationRound();
        Assert(clock.Snapshot is { Hour: 8, Minute: 59 }, "A térképi kör nem egy játékperc.");
        clock.AdvanceExplorationRound();
        Assert(clock.Snapshot is { Hour: 9, Minute: 0 }, "A hatvanadik kör nem váltott órát.");
        var firstRest = Guid.NewGuid();
        clock.AdvanceRest(firstRest);
        clock.AdvanceRest(firstRest);
        Assert(clock.Snapshot is { Day: 1, Hour: 17, IsDaytime: true },
            "A pihenés nem nyolc óra, vagy a megismételt értesítés újra növelte az időt.");
        clock.AdvanceRest(Guid.NewGuid());
        Assert(clock.Snapshot is { Day: 2, Hour: 1, IsDaytime: false },
            "A pihenés nem lépte át az éjfélt.");
        foreach (var (minute, daylight) in new[] { (359L, false), (360L, true), (1079L, true), (1080L, false), (1439L, false) })
        {
            clock.Restore(new GameTimeSnapshot(minute));
            Assert(clock.Snapshot.IsDaytime == daylight &&
                clock.Snapshot.DayNightIcon == (daylight ? "☀️" : "🌙"), "Hibás a napszakváltás.");
        }
        clock.AdvanceExplorationRound();
        Assert(clock.Snapshot is { Day: 2, Hour: 0, Minute: 0 }, "Az éjfél nem váltott napot.");
    }

    static void GameTimeBattleRoundsCountOnce()
    {
        var clock = new GameTimeClock();
        var battle = new BattleId(Guid.NewGuid());
        for (var participant = 0; participant < 30; participant++) clock.AccountBattleRounds(battle, 1);
        Assert(clock.Snapshot.TotalMinutes == 481, "Minden résztvevő külön játékpercet kapott.");
        clock.AccountBattleRounds(battle, 20);
        clock.AccountBattleRounds(battle, 20);
        clock.AccountBattleRounds(battle, 19);
        Assert(clock.Snapshot.TotalMinutes == 500, "A gyorsharc kihagyott vagy újraszámolt köröket.");
        clock.AccountBattleRounds(new BattleId(Guid.NewGuid()), 1);
        Assert(clock.Snapshot.TotalMinutes == 501, "Az új csata első köre nem számított.");
        clock.Restore(new GameTimeSnapshot(2000));
        clock.AccountBattleRounds(battle, 1);
        Assert(clock.Snapshot.TotalMinutes == 2001, "A betöltés régi csatakört tartott számon.");
    }

    static void GameTimeSaveMigrationAndRoundRemainder()
    {
        var now = new DateTime(2026, 10, 9, 10, 0, 0, DateTimeKind.Utc);
        var deadline = now.AddSeconds(13);
        Assert(Game.ExplorationRoundRemainingMilliseconds(deadline, now) == 13_000 &&
            Game.ExplorationRoundRemainingMilliseconds(deadline.AddMinutes(5), now.AddMinutes(5)) == 13_000 &&
            Game.ExplorationRoundRemainingMilliseconds(DateTime.MinValue, now) == 30_000 &&
            Game.ExplorationRoundRemainingMilliseconds(DateTime.MaxValue, now) == 30_000,
            "A szünet nem őrizte meg a térképi kör hátralévő idejét.");
        var save = new GameSaveData
        {
            GameTime = new GameTimeSnapshot(3 * 1440 + 23 * 60 + 55),
            ExplorationRoundRemainingMilliseconds = 13_000,
            SuspendedCampaign = new GameSaveData { GameTime = new GameTimeSnapshot(480) }
        };
        var restored = GameSaveFormat.MigrateToCurrent(
            JsonSerializer.Deserialize<GameSaveData>(JsonSerializer.Serialize(save))!);
        Assert(restored.GameTime == save.GameTime && restored.ExplorationRoundRemainingMilliseconds == 13_000 &&
            restored.GameTime is { Day: 4, Hour: 23, Minute: 55 },
            "A mentés elvesztette a játékidőt vagy visszatekerte a felfüggesztett kampányhoz.");
        var legacy = JsonSerializer.Deserialize<GameSaveData>(
            "{\"Version\":39,\"MazeLevel\":13,\"SuspendedCampaign\":{\"Version\":39}}")!;
        GameSaveFormat.MigrateToCurrent(legacy);
        Assert(legacy.Version == GameSaveFormat.CurrentVersion && legacy.MazeLevel == 13 &&
            legacy.GameTime is { Day: 1, Hour: 8 } &&
            legacy.SuspendedCampaign!.Version == GameSaveFormat.CurrentVersion &&
            legacy.ExplorationRoundRemainingMilliseconds == 30_000, "A régi mentés időmigrációja hibás.");
    }

    static void GameTimePausesPreservePartialRound()
    {
        var game = (Game)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(Game));
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic;
        var deadlineField = typeof(Game).GetField("_nextExplorationStatusTickUtc", flags)!;
        deadlineField.SetValue(game, DateTime.UtcNow.AddSeconds(13));
        typeof(Game).GetMethod("PauseExplorationRound", flags)!.Invoke(game, null);
        var remainder = (int)typeof(Game).GetMethod("RemainingExplorationRoundMilliseconds", flags)!
            .Invoke(game, [DateTime.UtcNow.AddDays(2)])!;
        Assert(remainder > 12_000 && remainder <= 13_000 &&
            (DateTime)deadlineField.GetValue(game)! == DateTime.MaxValue,
            "A fogadói vagy harci szünet elfogyasztotta a megkezdett térképi kört.");
        typeof(Game).GetMethod("PauseExplorationRound", flags)!.Invoke(game, null);
        Assert((int)typeof(Game).GetMethod("RemainingExplorationRoundMilliseconds", flags)!
            .Invoke(game, [DateTime.UtcNow.AddDays(3)])! == remainder,
            "Az egymásba ágyazott szünet újraindította a térképi kört.");
        var before = DateTime.UtcNow;
        typeof(Game).GetMethod("ResumeExplorationRound", flags)!.Invoke(game, null);
        var after = DateTime.UtcNow;
        var resumed = (DateTime)deadlineField.GetValue(game)!;
        Assert(resumed >= before.AddMilliseconds(remainder) && resumed <= after.AddMilliseconds(remainder),
            "A folytatás nem a megmaradt köridővel indult.");
    }

    static void GameTimeReplicatesThroughFullAndDelta()
    {
        var (session, leader, _) = CreateSession();
        var maze = new Maze(7, 7);
        var fog = new FogOfWar(7, 7, 0);
        var snapshot = session.CreateSnapshot(new SessionSnapshotContext(6, "Órapróba",
            new Dictionary<CharacterId, Position> { [leader.Id] = maze.Entrance }))
            with { World = WorldSnapshotProjector.Create(maze, fog), GameTime = new GameTimeSnapshot() };
        var publisher = new SessionReplicationPublisher();
        var store = new ClientSessionStore(session.HostPlayerId);
        var full = publisher.CreateFrame(session.HostPlayerId, snapshot);
        full = JsonSerializer.Deserialize<SessionReplicationFrame>(JsonSerializer.Serialize(full))!;
        Assert(store.Apply(full).Status == ClientFrameApplyStatus.Applied &&
            store.CurrentSnapshot!.GameTime == snapshot.GameTime, "A teljes snapshot elvesztette a játékidőt.");
        Assert(publisher.TryAcknowledge(session.HostPlayerId, snapshot.SnapshotSequence, out var error), error);
        var later = snapshot with { SnapshotSequence = snapshot.SnapshotSequence + 1,
            GameTime = new GameTimeSnapshot(1440 + 2 * 60, 960, 1) };
        var delta = publisher.CreateFrame(session.HostPlayerId, later);
        delta = JsonSerializer.Deserialize<SessionReplicationFrame>(JsonSerializer.Serialize(delta))!;
        Assert(delta.Kind == SessionReplicationFrameKind.Delta &&
            store.Apply(delta).Status == ClientFrameApplyStatus.Applied &&
            store.CurrentSnapshot!.GameTime is { Day: 2, Hour: 2, IsDaytime: false,
                LastInnRestCompletedMinutes: 960, LastForestFeastDay: 1 },
            "A világ nélküli delta elvesztette a napváltást.");
    }

    static void GameTimeHeaderAndGoldColumnFit()
    {
        foreach (var width in new[] { 27, 34, 50 })
        foreach (var day in new[] { 1, 10, 9999 })
        {
            var time = new GameTimeSnapshot((day - 1L) * 1440 + 23 * 60 + 59);
            string Header(string indicator) => CharacterSheetPanel.WithFocusMarker(
                CharacterSheetPanel.BuildWorldHeaderLine(23, 7, 12, indicator, width - 1, time), false, width).Text;
            var running = Header("⌛");
            var paused = Header("⌛⏸");
            Assert(BattleCommandPanel.DisplayWidth(paused) <= width &&
                paused.Contains("23:59") && paused.Contains("🌙") && !paused.Contains("🔑"),
                "A játékidő vagy az éjszaka nem fér el a fejlécben.");
            var update = CoopGuestScreen.ClockOnlyUpdate(running, paused, width);
            Assert(update is not null && update.Value.Column + 4 == width,
                "A homokóra nem kapott fix helyet a fejléc jobb szélén.");
        }
        var character = CreateCharacter("Időteszt");
        character.AddGold(5000);
        var gold = CharacterSheetPanel.BuildGoldLine(character, 7, 12, 27).Text;
        Assert(gold.StartsWith("Arany: 5000") && gold.Contains("🔑 7/12") &&
            BattleCommandPanel.DisplayWidth(gold) <= 27 &&
            BattleCommandPanel.DisplayWidth(gold[..gold.IndexOf("🔑", StringComparison.Ordinal)]) == 18,
            "Az aranykulcsok nem az arany sorának második oszlopában vannak.");
        character.AddGold(int.MaxValue - character.Gold);
        gold = CharacterSheetPanel.BuildGoldLine(character, 12, 12, 27).Text;
        Assert(gold.Contains(int.MaxValue.ToString()) && gold.Contains("🔑 12/12") &&
            BattleCommandPanel.DisplayWidth(gold) <= 27, "Nagy aranyösszeg mellett eltűnik a kulcskijelzés.");
    }
}
