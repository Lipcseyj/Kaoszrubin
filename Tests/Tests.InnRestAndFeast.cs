using System.Reflection;

internal static partial class Program
{
    private static void UseNormalInnForServices(ForestInnEconomyFixture fixture, int level = 6)
    {
        fixture.Enter(level: level);
        void Set(string field, object? value) => typeof(InnController)
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(fixture.Controller, value);
        Set("_forestInn", null);
        Set("_forestStop", false);
        Set("_roomPricePerPerson", InnController.RoomPricePerPerson(level, false, new Random(71)));
        InvokeForestInn(fixture.Controller, "RebuildInnMenu", "Visszatérő expedíció");
    }

    private static int InnRoomPrice(ForestInnEconomyFixture fixture) => (int)typeof(InnController)
        .GetField("_roomPricePerPerson", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Controller)!;

    static void InnServicesRoomPricesScaleAndRemainStable()
    {
        foreach (var level in Enumerable.Range(1, 23))
        {
            var normalBase = Math.Max(1, (level * level + 3) / 4);
            var forestBase = Math.Max(12, normalBase * 3);
            var normalPrices = new HashSet<int>();
            var forestPrices = new HashSet<int>();
            foreach (var seed in Enumerable.Range(1, 60))
            {
                var normal = InnController.RoomPricePerPerson(level, false, new Random(seed));
                var forest = InnController.RoomPricePerPerson(level, true, new Random(seed));
                normalPrices.Add(normal); forestPrices.Add(forest);
                Assert(normal >= Math.Max(1, (int)Math.Round(normalBase * 0.8)) &&
                    normal <= Math.Round(normalBase * 1.2) && forest >= Math.Round(forestBase * 0.8) &&
                    forest <= Math.Round(forestBase * 1.2) && forest > normal, "Hibás fogadói ársáv.");
            }
            Assert(forestPrices.Count > 1 && (level < 3 || normalPrices.Count > 1),
                "Hiányzik a szobaár véletlen változása.");
            if (level <= 2) Assert(normalPrices.SetEquals([1]), "Túl drága lett a kezdeti pihenés.");
        }
        var fixture = new ForestInnEconomyFixture();
        var first = fixture.Enter("FIRST", 6);
        var price = first.Services!.RoomPricePerPerson;
        fixture.Enter("SECOND", 13);
        fixture.Controller.PrepareForestStop(6, first);
        Assert(first.Services!.RoomPricePerPerson == price && InnRoomPrice(fixture) == price &&
            fixture.Snapshot.MenuOptions!.Single(option => option.Kind == InnMenuOptionKind.Rest)
                .Label.Contains($"{price}"), "A visszatérés új szobaárat sorsolt, vagy nem jelzi az árat.");
    }

    static void InnServicesRestChargesPartyAndNormalInnAllowsOnlyOnce()
    {
        var fixture = new ForestInnEconomyFixture();
        UseNormalInnForServices(fixture, 2);
        foreach (var name in new[] { "Társ egy", "Társ kettő" })
        {
            var companion = CreateCharacter(name);
            fixture.Roster.Add(companion);
            Assert(fixture.Roster.Party.Add(companion), "Nem fért be a társ.");
        }
        fixture.Leader.SetCurrentResources(1, 0);
        fixture.Leader.SetGold(0);
        var before = fixture.Clock.Snapshot;
        var revision = fixture.Snapshot.Revision;
        Assert(!fixture.Controller.TryRestAtInn(revision, out var denied) && denied.Contains("arany") &&
            fixture.Leader.CurrentVitality == 1 && fixture.Clock.Snapshot == before &&
            fixture.Snapshot.Revision == revision, "A fedezet nélküli pihenés megváltoztatta a játékot.");
        fixture.Leader.SetGold(1000);
        var gold = fixture.Leader.Gold;
        var count = fixture.Roster.Party.Members.Count;
        Assert(fixture.Controller.TryRestAtInn(revision, out _) &&
            fixture.Leader.Gold == gold - InnRoomPrice(fixture) * count &&
            fixture.Leader.CurrentVitality > 1 && fixture.Clock.Snapshot.TotalMinutes == before.TotalMinutes + 480 &&
            fixture.Clock.Snapshot.LastInnRestCompletedMinutes == fixture.Now,
            "A sikeres pihenés díja, regenerálása vagy ideje hibás.");
        Assert(fixture.Snapshot.MenuOptions!.Any(option => option.Kind == InnMenuOptionKind.ReturnExpedition),
            "A pihenés eltüntette az expedíciós menüpontot.");
        fixture.Now += 2 * 1440;
        gold = fixture.Leader.Gold;
        var time = fixture.Clock.Snapshot;
        Assert(!fixture.Controller.TryRestAtInn(fixture.Snapshot.Revision, out _) &&
            fixture.Leader.Gold == gold && fixture.Clock.Snapshot == time && fixture.RestCount == 1,
            "A pályavégi fogadó engedte a második pihenést.");
    }

    static void InnServicesForestRestCooldownIsGlobalAndIgnoresCampTimestamp()
    {
        var fixture = new ForestInnEconomyFixture();
        UseNormalInnForServices(fixture);
        Assert(fixture.Controller.TryRestAtInn(fixture.Snapshot.Revision, out _), "Nem sikerült az első pihenés.");
        var lastInnRest = fixture.Now;
        var inn = fixture.Enter("FIRST");
        var gold = fixture.Leader.Gold;
        Assert(!fixture.Controller.TryRestAtInn(fixture.Snapshot.Revision, out var reason) &&
            reason.Contains("nem fáradt") && fixture.Leader.Gold == gold, "Az erdőbe lépés nullázta a korlátot.");
        var camp = Guid.NewGuid();
        fixture.Clock.AdvanceRest(camp, atInn: false);
        fixture.Clock.AdvanceRest(camp, atInn: false);
        Assert(fixture.Now == lastInnRest + 480 && fixture.Clock.Snapshot.LastInnRestCompletedMinutes == lastInnRest,
            "A tábori pihenés felülírta a fogadói időpontot vagy kétszer számított.");
        Assert(!fixture.Controller.TryRestAtInn(fixture.Snapshot.Revision, out _), "Nyolc óra után már pihenhetett a parti.");
        fixture.Now = lastInnRest + 959;
        fixture.Controller.PrepareForestStop(13, inn);
        Assert(!fixture.Controller.TryRestAtInn(fixture.Snapshot.Revision, out reason) && reason.Contains("1 perc"),
            "A tizenhatórás határ előtt pihenhetett a parti.");
        fixture.Now++;
        Assert(fixture.Controller.TryRestAtInn(fixture.Snapshot.Revision, out _) &&
            fixture.Now == lastInnRest + 960 + 480, "Pontosan tizenhat óra után nem lehetett pihenni.");
        var endsAt = fixture.Now;
        fixture.Enter("SECOND");
        Assert(!fixture.Controller.TryRestAtInn(fixture.Snapshot.Revision, out _) &&
            fixture.Clock.Snapshot.LastInnRestCompletedMinutes == endsAt,
            "A másik erdei fogadó megkerülte a korlátot.");
        fixture.Now += 960;
        Assert(fixture.Controller.TryRestAtInn(fixture.Snapshot.Revision, out _),
            "Az erdei fogadó örökre megőrizte az egyszeri pihenés korlátját.");

        var campOnly = new ForestInnEconomyFixture();
        campOnly.Clock.AdvanceRest(Guid.NewGuid(), atInn: false);
        campOnly.Enter();
        Assert(campOnly.Clock.Snapshot.LastInnRestCompletedMinutes is null &&
            campOnly.Controller.TryRestAtInn(campOnly.Snapshot.Revision, out _),
            "A táborozás fogadói pihenésként számított.");
    }

    static void InnServicesForestFeastsAreDailyAcrossInnsAndAdvanceOneHour()
    {
        var fixture = new ForestInnEconomyFixture();
        var inn = fixture.Enter();
        fixture.BuyMarketOffer();
        var shortage = ForestStockCount(fixture.Market(inn));
        fixture.Now += 119;
        fixture.Leader.SetNeedLevels(10, 20);
        var before = fixture.Clock.Snapshot;
        var gold = fixture.Leader.Gold;
        var revision = fixture.Snapshot.Revision;
        var price = inn.Services!.FeastPrice;
        Assert(fixture.Controller.TryFeastAtInn(revision, out _) && fixture.Now == before.TotalMinutes + 60 &&
            fixture.Clock.Snapshot.LastForestFeastDay == before.Day &&
            fixture.Leader.Gold == gold - price && fixture.Leader.FoodLevel == 100 && fixture.Leader.WaterLevel == 100,
            "Hibás lakoma, ár vagy egyórás időnövelés.");
        Assert(fixture.Controller.AdvanceForestInnTime() && ForestStockCount(fixture.Market(inn)) == shortage + 1,
            "A lakoma órája nem számított az utánpótlásba.");
        gold = fixture.Leader.Gold;
        before = fixture.Clock.Snapshot;
        Assert(!fixture.Controller.TryFeastAtInn(revision, out _) &&
            !fixture.Controller.TryFeastAtInn(fixture.Snapshot.Revision, out var reason) &&
            reason.Contains("ma már") && fixture.Leader.Gold == gold && fixture.Clock.Snapshot == before,
            "Ismételt vagy elavult lakomakérés költött vagy időt adott.");
        fixture.Enter("SECOND");
        Assert(!fixture.Controller.TryFeastAtInn(fixture.Snapshot.Revision, out _), "A másik fogadó második lakomát adott.");
        fixture.Now = before.Day * 1440;
        Assert(fixture.Controller.TryFeastAtInn(fixture.Snapshot.Revision, out _) &&
            fixture.Clock.Snapshot.LastForestFeastDay == before.Day + 1, "Napváltás után nem lehetett lakomázni.");

        UseNormalInnForServices(fixture);
        var forestFeastDay = fixture.Clock.Snapshot.LastForestFeastDay;
        var normalStart = fixture.Now;
        Assert(fixture.Controller.TryFeastAtInn(fixture.Snapshot.Revision, out _) &&
            fixture.Controller.TryFeastAtInn(fixture.Snapshot.Revision, out _) &&
            fixture.Now == normalStart + 120 && fixture.Clock.Snapshot.LastForestFeastDay == forestFeastDay,
            "A normál fogadó napi korlátot kapott, vagy nem számolta a lakoma idejét.");
        fixture.Leader.SetGold(0);
        before = fixture.Clock.Snapshot;
        Assert(!fixture.Controller.TryFeastAtInn(fixture.Snapshot.Revision, out _) &&
            fixture.Clock.Snapshot == before, "A fedezet nélküli lakoma időt növelt.");

        var midnight = new ForestInnEconomyFixture();
        midnight.Now = 1430;
        midnight.Enter();
        Assert(midnight.Controller.TryFeastAtInn(midnight.Snapshot.Revision, out _) &&
            midnight.Clock.Snapshot is { Day: 2, LastForestFeastDay: 1 } &&
            midnight.Controller.TryFeastAtInn(midnight.Snapshot.Revision, out _),
            "Az éjfélen átnyúló lakoma nem a kezdőnapjához tartozott.");
    }

    static void InnServicesRestrictionsAndRoomPricesSurviveSaveAndMigration()
    {
        var fixture = new ForestInnEconomyFixture();
        var inn = fixture.Enter();
        Assert(fixture.Controller.TryRestAtInn(fixture.Snapshot.Revision, out _) &&
            fixture.Controller.TryFeastAtInn(fixture.Snapshot.Revision, out _), "Nem sikerült a mentési helyzet.");
        var save = new GameSaveData { GameTime = fixture.Clock.Snapshot,
            Maze = new() { ForestInns = [new(inn.RoomId, inn.Name, inn.Position, true, inn.Services)] } };
        save = GameSaveFormat.MigrateToCurrent(JsonSerializer.Deserialize<GameSaveData>(JsonSerializer.Serialize(save))!);
        var loaded = new ForestInnEconomyFixture(999);
        loaded.Clock.Restore(save.GameTime);
        var savedInn = save.Maze.ForestInns.Single();
        var restored = new ForestInn(savedInn.Position, savedInn.RoomId, savedInn.Name, savedInn.Visited)
            { Services = savedInn.Services };
        loaded.Controller.PrepareForestStop(13, restored);
        Assert(loaded.Clock.Snapshot == fixture.Clock.Snapshot &&
            restored.Services!.RoomPricePerPerson == inn.Services!.RoomPricePerPerson &&
            !loaded.Controller.TryRestAtInn(loaded.Snapshot.Revision, out _) &&
            !loaded.Controller.TryFeastAtInn(loaded.Snapshot.Revision, out _),
            "A betöltés új szobaárat sorsolt vagy eltüntette a korlátot.");

        var oldServices = inn.Services! with { RoomPricePerPerson = 0 };
        var legacy = new GameSaveData { Version = 41, GameTime = new(fixture.Now),
            Maze = new() { ForestInns = [new(inn.RoomId, inn.Name, inn.Position, true, oldServices)] },
            SuspendedCampaign = new() { Version = 41, GameTime = new(fixture.Now - 100) } };
        GameSaveFormat.MigrateToCurrent(legacy);
        Assert(legacy.Version == GameSaveFormat.CurrentVersion &&
            legacy.SuspendedCampaign!.Version == GameSaveFormat.CurrentVersion &&
            legacy.GameTime.TotalMinutes == fixture.Now &&
            legacy.GameTime.LastInnRestCompletedMinutes is null && legacy.GameTime.LastForestFeastDay is null &&
            legacy.Maze.ForestInns.Single().Services!.FirstVisitMinutes == inn.FirstVisitMinutes,
            "A migráció korábbi pihenést vagy lakomát talált ki, vagy elvesztette a fogadót.");
        var legacyInn = new ForestInn(inn.Position, inn.RoomId, inn.Name, true) { Services = oldServices };
        loaded.Controller.PrepareForestStop(13, legacyInn);
        var legacyPrice = legacyInn.Services!.RoomPricePerPerson;
        loaded.Controller.PrepareForestStop(13, legacyInn);
        Assert(legacyPrice > 0 && legacyInn.Services!.RoomPricePerPerson == legacyPrice,
            "A régi fogadó új ára nem maradt meg.");
    }
}
