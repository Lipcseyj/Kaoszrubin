using System.Reflection;

internal static partial class Program
{
    private sealed class ForestInnEconomyFixture
    {
        public GameDataCatalog Data { get; } = ForestSupplyData();
        public CharacterRoster Roster { get; } = new();
        public LiveCharacter Leader { get; } = CreateCharacter("Erdei vezér");
        public InnController Controller { get; }
        public GameTimeClock Clock { get; } = new();
        public long Now
        {
            get => Clock.Snapshot.TotalMinutes;
            set => Clock.Restore(Clock.Snapshot with { TotalMinutes = value });
        }
        public int RestCount { get; private set; }
        public int SpecialVisitCount { get; private set; }
        public List<LiveCharacter> SpecialCandidates { get; } = [];

        public ForestInnEconomyFixture(int seed = 42)
        {
            Now = 3 * 1440 + 8 * 60 + 17;
            Roster.Add(Leader); Roster.Select(Leader);
            Leader.AddGold(2_000_000);
            Controller = new(Data, Roster, Leader, new ConsoleRenderer(Data, Roster.Party),
                _ => { }, new Random(seed),
                (_, _) => throw new Exception("Erdei fogadó pályateljesítést indított."),
                (_, _) => throw new Exception("Erdei fogadó szintlépést indított."), () => { },
                reportRest: rest => { Clock.AdvanceRest(rest.RestId, rest.AtInn); RestCount++; },
                specialRecruitCandidates: () => { SpecialVisitCount++; return SpecialCandidates; },
                gameTimeMinutes: () => Now, currentSpecialRecruitCandidates: () => SpecialCandidates,
                gameTimeState: () => Clock.Snapshot, reportFeast: Clock.AdvanceFeast);
        }

        public ForestInn Enter(string id = "TEST_INN", int level = 13)
        {
            var inn = new ForestInn(new(3, 3), id, "Próba " + id);
            Controller.PrepareForestStop(level, inn);
            return inn;
        }

        public InnSnapshot Snapshot => Controller.CreateSnapshot()!;
        public ForestInnVendorState Market(ForestInn inn) =>
            inn.Services!.Vendors.Single(vendor => vendor.Kind == InnVendorKind.Market);

        public void BuyMarketOffer(int index = 0)
        {
            var snapshot = Snapshot;
            var offer = snapshot.Vendors.Single(vendor => vendor.Kind == InnVendorKind.Market).Offers[index];
            Assert(Controller.TryPurchase(InnVendorKind.Market, offer.Index, snapshot.Revision, Leader, out var message),
                "Nem sikerült a tesztvásárlás: " + message);
            for (var count = 0; count < offer.Item.Quantity; count++)
                Assert(Leader.RemoveFromBackpack(offer.Item.DefinitionId), "Hiányzó megvett tárgy.");
        }

        public void EmptyMarket()
        {
            while (Snapshot.Vendors.Single(vendor => vendor.Kind == InnVendorKind.Market).Offers.Count > 0)
                BuyMarketOffer();
        }
    }

    private static string ForestInnStateJson(ForestInn inn) => JsonSerializer.Serialize(inn.Services);
    private static int ForestStockCount(ForestInnVendorState vendor) => vendor.Stock.Sum(offer => offer.StockCount);
    private static HashSet<InnVendorKind> ForestVendorKinds(ForestInn inn) =>
        inn.Services!.Vendors.Select(vendor => vendor.Kind).ToHashSet();
    private static HashSet<CharacterId> ForestRecruitIds(InnController controller) =>
        controller.RecruitmentOffers().Select(offer => offer.CharacterId).ToHashSet();

    private static object? InvokeForestInn(InnController controller, string method, params object[] arguments) =>
        typeof(InnController).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(controller, arguments);

    static void ForestInnRepeatVisitsKeepPurchasedStockAndIndependentInns()
    {
        var fixture = new ForestInnEconomyFixture();
        var first = fixture.Enter("FERRY_INN");
        var firstVisit = fixture.Now;
        var initialCount = ForestStockCount(fixture.Market(first));
        var initialRecruits = ForestRecruitIds(fixture.Controller);
        fixture.BuyMarketOffer();
        var changed = ForestInnStateJson(first);
        Assert(ForestStockCount(fixture.Market(first)) == initialCount - 1, "Nem fogyott a készlet.");

        var second = fixture.Enter("COURT_INN");
        var secondState = ForestInnStateJson(second);
        fixture.Controller.PrepareForestStop(13, first);
        Assert(ForestInnStateJson(first) == changed && initialRecruits.SetEquals(ForestRecruitIds(fixture.Controller)) &&
            first.FirstVisitMinutes == firstVisit && fixture.Snapshot.FirstVisitMinutes == firstVisit &&
            fixture.Snapshot.ArtisanNotice.Contains(new GameTimeSnapshot(firstVisit).DisplayText),
            "A visszalépés újrasorsolta a készletet, az árakat, a zsoldosokat vagy az első betérést.");
        fixture.Controller.PrepareForestStop(13, second);
        Assert(ForestInnStateJson(second) == secondState, "Két fogadó készlete összekeveredett.");

        fixture.Controller.PrepareForestStop(13, first);
        var food = fixture.Data.GetItem("T001");
        Assert(fixture.Leader.AddToBackpack(food), "Nem fért el az eladandó zsákmány.");
        var slot = Enumerable.Range(0, fixture.Leader.Backpack.Count)
            .First(index => fixture.Leader.Backpack[index]?.Id == food.Id);
        var gold = fixture.Leader.Gold;
        Assert(fixture.Controller.TrySell(fixture.Snapshot.Revision, fixture.Leader.InventoryRevision,
            slot, fixture.Leader, out _) && fixture.Leader.Gold > gold,
            "Visszatéréskor nem lehetett zsákmányt eladni.");
        Assert(ForestStockCount(fixture.Market(first)) == initialCount - 1,
            "Az eladás ingyen újratöltötte a készletet.");
    }

    static void ForestInnRestockRespectsElapsedTimeAndOriginalCaps()
    {
        var fixture = new ForestInnEconomyFixture();
        var inn = fixture.Enter();
        var original = fixture.Market(inn).TargetStock.ToArray();
        var firstVisit = fixture.Now;
        fixture.EmptyMarket();
        Assert(fixture.Market(inn).Stock.Count == 0, "Nem ürült ki a kereskedő.");
        fixture.Now = firstVisit + 119;
        fixture.Controller.PrepareForestStop(13, inn);
        Assert(fixture.Market(inn).Stock.Count == 0, "Két óra előtt újratöltött a készlet.");
        fixture.Now++;
        Assert(fixture.Controller.AdvanceForestInnTime() && ForestStockCount(fixture.Market(inn)) == 1,
            "Két óra után nem pontosan egy ajánlat érkezett.");
        var firstOffer = fixture.Market(inn).Stock.Single();
        Assert(original.Any(offer => offer.ItemId == firstOffer.ItemId && offer.Price == firstOffer.Price &&
            offer.Quantity == firstOffer.Quantity), "Megváltozott a pótlás ára vagy csomagmérete.");
        Assert(!fixture.Controller.AdvanceForestInnTime(), "Ugyanaz az eltelt idő kétszer töltött.");
        fixture.Now = firstVisit + 239;
        Assert(!fixture.Controller.AdvanceForestInnTime(), "Elveszett a megkezdett utánpótlási időszak.");
        fixture.Now++;
        fixture.Controller.AdvanceForestInnTime();
        Assert(ForestStockCount(fixture.Market(inn)) == 2, "Négy óra után nem két ajánlat érkezett.");
        fixture.Now = firstVisit + 120L * (original.Sum(offer => offer.StockCount) + 5);
        fixture.Controller.AdvanceForestInnTime();
        Assert(ForestStockCount(fixture.Market(inn)) == original.Sum(offer => offer.StockCount) &&
            fixture.Market(inn).Stock.All(offer => original.Where(target => target.ItemId == offer.ItemId &&
                target.Price == offer.Price && target.Quantity == offer.Quantity)
                .Sum(target => target.StockCount) == offer.StockCount),
            "A hosszú időkihagyás túltöltötte vagy megváltoztatta az alapkészletet.");
        fixture.BuyMarketOffer();
        var shortage = ForestStockCount(fixture.Market(inn));
        Assert(!fixture.Controller.AdvanceForestInnTime() &&
            ForestStockCount(fixture.Market(inn)) == shortage, "A teli készlet felhalmozott ingyenes pótlást.");
        fixture.Now += 120;
        fixture.Controller.AdvanceForestInnTime();
        Assert(ForestStockCount(fixture.Market(inn)) == shortage + 1, "A következő pótlás elmaradt.");
        Assert(inn.FirstVisitMinutes == firstVisit, "Időfrissítés átírta az első betérést.");
    }

    static void ForestInnTravelersAndRecruitsUseSeparateSchedules()
    {
        var fixture = new ForestInnEconomyFixture();
        var inn = fixture.Enter();
        var firstVisit = fixture.Now;
        var vendors = ForestVendorKinds(inn);
        var recruits = ForestRecruitIds(fixture.Controller);
        fixture.Now += 239;
        fixture.Controller.PrepareForestStop(13, inn);
        Assert(vendors.SetEquals(ForestVendorKinds(inn)) && recruits.SetEquals(ForestRecruitIds(fixture.Controller)),
            "Túl korán távozott kereskedő vagy zsoldos.");
        fixture.Now++;
        fixture.Controller.AdvanceForestInnTime();
        var vendorsAfter = ForestVendorKinds(inn);
        Assert(vendors.Except(vendorsAfter).Count() + vendorsAfter.Except(vendors).Count() == 1 &&
            vendorsAfter.Contains(InnVendorKind.Market) && vendorsAfter.Contains(InnVendorKind.Witcher) &&
            recruits.SetEquals(ForestRecruitIds(fixture.Controller)), "Nem egy vándormester változott négy óra után.");
        foreach (var option in fixture.Snapshot.MenuOptions!.Where(option =>
                     option.Kind is InnMenuOptionKind.Blacksmith or InnMenuOptionKind.Armorer or
                         InnMenuOptionKind.WanderingMage or InnMenuOptionKind.Bowyer))
            Assert(vendorsAfter.Contains(option.Vendor!.Value), "Távozott kereskedő maradt a menüben.");
        var unchanged = ForestInnStateJson(inn);
        fixture.Controller.PrepareForestStop(13, inn);
        Assert(ForestInnStateJson(inn) == unchanged, "A belépés megismételte a vendégcserét.");
        fixture.Now = firstVisit + 359;
        fixture.Controller.AdvanceForestInnTime();
        Assert(recruits.SetEquals(ForestRecruitIds(fixture.Controller)), "Hat óra előtt cserélődött zsoldos.");
        fixture.Now++;
        fixture.Controller.AdvanceForestInnTime();
        var recruitsAfter = ForestRecruitIds(fixture.Controller);
        Assert(recruitsAfter.Count == recruits.Count && recruits.Except(recruitsAfter).Count() == 1 &&
            recruitsAfter.Except(recruits).Count() == 1 && vendorsAfter.SetEquals(ForestVendorKinds(inn)),
            "Hat óra után nem egy zsoldos cserélődött.");
        Assert(inn.Services!.VisitorsThroughMinutes == firstVisit + 240 &&
            inn.Services.RecruitsThroughMinutes == firstVisit + 360, "Az időszakok elcsúsztak.");
    }

    static void ForestInnHiredMercenariesStayHiredAndSpecialCandidatesRemain()
    {
        var fixture = new ForestInnEconomyFixture();
        var special = CreateCharacter("Quest társ");
        fixture.SpecialCandidates.Add(special);
        var inn = fixture.Enter();
        var capacity = inn.Services!.RecruitCapacity;
        var specialPrice = fixture.Controller.RecruitmentOffers().Single(offer => offer.CharacterId == special.Id).Price;
        var hired = fixture.Controller.RecruitmentOffers().First(offer => offer.CharacterId != special.Id);
        Assert(fixture.Controller.TryRecruit(hired.CharacterId, fixture.Snapshot.Revision, null, out _),
            "Nem sikerült zsoldost felvenni.");
        fixture.Controller.PrepareForestStop(13, inn);
        Assert(!ForestRecruitIds(fixture.Controller).Contains(hired.CharacterId) &&
            inn.Services!.Recruits.Count == capacity - 1, "Visszajött azonnal a felvett zsoldos.");
        fixture.Now += 360;
        fixture.Controller.AdvanceForestInnTime();
        Assert(inn.Services!.Recruits.Count == capacity &&
            !ForestRecruitIds(fixture.Controller).Contains(hired.CharacterId) &&
            ForestRecruitIds(fixture.Controller).Contains(special.Id),
            "Nem töltődött a toborzási hely, vagy eltűnt a questhez kötött jelölt.");
        var json = ForestInnStateJson(inn);
        fixture.Controller.PrepareForestStop(13, inn);
        Assert(ForestInnStateJson(inn) == json && ForestRecruitIds(fixture.Controller).Contains(special.Id) &&
            fixture.Controller.RecruitmentOffers().Single(offer => offer.CharacterId == special.Id).Price == specialPrice &&
            fixture.SpecialVisitCount == 1,
            "A visszatérés újrasorsolta a toborzást vagy fogyasztotta a különleges társ várakozási idejét.");
    }

    static void ForestInnEconomySurvivesSaveAndLegacyInnsReopen()
    {
        var fixture = new ForestInnEconomyFixture();
        var inn = fixture.Enter();
        fixture.BuyMarketOffer();
        var expected = ForestInnStateJson(inn);
        var maze = ForestInnTestMaze();
        maze.AddForestInn(inn);
        var fog = new FogOfWar(11, 11, 0);
        fog.Restore([inn.Position], false);
        var mapper = new GameStateMapper(fixture.Data, fixture.Roster, fixture.Leader);
        var saved = mapper.Create(13, maze, new Player(inn.Position, fixture.Leader), fog,
            Direction.Right, [], false, false, false, false, null, DateTime.UtcNow,
            new Dictionary<Enemy, DateTime>(), [], []);
        saved.GameTime = new(fixture.Now);
        saved = JsonSerializer.Deserialize<GameSaveData>(JsonSerializer.Serialize(saved))!;
        var restored = mapper.Restore(saved).Maze.ForestInns.Single();
        Assert(ForestInnStateJson(restored) == expected && restored.TryVisit(), "Elveszett a mentett fogadókészlet.");
        var controllerAfterLoad = new InnController(fixture.Data, fixture.Roster, fixture.Leader,
            new ConsoleRenderer(fixture.Data, fixture.Roster.Party), _ => { }, new Random(999),
            (_, _) => throw new Exception("Betöltéskor XP."), (_, _) => { }, () => { },
            gameTimeMinutes: () => saved.GameTime.TotalMinutes);
        controllerAfterLoad.PrepareForestStop(13, restored);
        Assert(ForestInnStateJson(restored) == expected, "A betöltés új készletet vagy zsoldosokat sorsolt.");
        saved.GameTime = new(saved.GameTime.TotalMinutes + 120);
        controllerAfterLoad.PrepareForestStop(13, restored);
        Assert(ForestStockCount(fixture.Market(restored)) == ForestStockCount(fixture.Market(inn)) + 1,
            "A betöltés után nem folytatódott az utánpótlás.");

        var initialWorld = WorldSnapshotProjector.Create(maze, fog);
        inn.Services = inn.Services! with { FirstVisitMinutes = fixture.Now + 1 };
        var changedWorld = WorldSnapshotProjector.Create(maze, fog);
        var delta = JsonSerializer.Deserialize<WorldDelta>(JsonSerializer.Serialize(
            WorldDeltaProjector.Create(1, initialWorld, 2, changedWorld)))!;
        Assert(WorldDeltaReducer.Apply(initialWorld, delta).ForestInns!.Single().FirstVisitMinutes == fixture.Now + 1 &&
            initialWorld.ForestInns!.Single().FirstVisitMinutes == fixture.Now,
            "A kliens elvesztette az első betérés idejét.");

        var legacy = new GameSaveData { Version = 40, MazeLevel = 13, GameTime = new(fixture.Now),
            Maze = new() { LevelName = "Régi erdő", ForestInns = [new("OLD_INN", "Régi fogadó", new(3, 3), true)] },
            SuspendedCampaign = new() { Version = 40, GameTime = new(fixture.Now),
                Maze = new() { ForestInns = [new("OLD_INN", "Régi fogadó", new(3, 3), true)] } } };
        GameSaveFormat.MigrateToCurrent(legacy);
        Assert(legacy.Version == GameSaveFormat.CurrentVersion &&
            legacy.SuspendedCampaign!.Version == GameSaveFormat.CurrentVersion &&
            legacy.GameTime.TotalMinutes == fixture.Now && legacy.Maze.ForestInns.Single().Visited &&
            legacy.Maze.ForestInns.Single().Services is null, "A migráció átírta a meglévő világot vagy időt.");
        var oldInn = new ForestInn(new(3, 3), "OLD_INN", "Régi fogadó", visited: true);
        Assert(oldInn.TryVisit() && oldInn.FirstVisitMinutes is null, "A régi fogadó lezárt maradt.");
        fixture.Controller.PrepareForestStop(13, oldInn);
        Assert(oldInn.FirstVisitMinutes == fixture.Now, "A régi fogadó első új betérése nem kapott időbélyeget.");
    }

    static void ForestInnDetourPreservesTheExpeditionInn()
    {
        var fixture = new ForestInnEconomyFixture();
        fixture.Enter();
        var controller = fixture.Controller;
        void Set(string name, object? value) => typeof(InnController)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(controller, value);
        // Egy már előállított fogadói kínálatot használunk, pályateljesítési XP és UI nélkül.
        Set("_forestInn", null);
        Set("_forestStop", false);
        Set("_innName", "Pályavégi fogadó");
        Set("_hasRestedAtInn", true);
        InvokeForestInn(controller, "RebuildInnMenu", "Visszatérő expedíció");
        var prior = fixture.Snapshot;
        var detour = new ForestInn(new(3, 3), "DETOUR", "Erdei kitérő");
        try
        {
            controller.RunForestStop(6, detour, () => throw new InvalidOperationException("Tesztkilépés"));
            throw new Exception("A tesztkilépés nem történt meg.");
        }
        catch (InvalidOperationException error) when (error.Message == "Tesztkilépés") { }
        Set("_active", true); // A Run(resume: true) ugyanígy aktiválja a visszaállított fogadót.
        var resumed = fixture.Snapshot;
        Assert(resumed.InnName == prior.InnName && resumed.MazeLevel == prior.MazeLevel &&
            resumed.ArtisanNotice == prior.ArtisanNotice &&
            JsonSerializer.Serialize(resumed.Vendors) == JsonSerializer.Serialize(prior.Vendors) &&
            JsonSerializer.Serialize(resumed.Recruits) == JsonSerializer.Serialize(prior.Recruits) &&
            JsonSerializer.Serialize(resumed.MenuOptions) == JsonSerializer.Serialize(prior.MenuOptions) &&
            (bool)typeof(InnController).GetField("_hasRestedAtInn",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(controller)! &&
            resumed.FirstVisitMinutes is null && detour.Services is not null,
            "Az erdei kitérő felülírta az expedíciós fogadót.");
    }

    static void ForestInnRestAdvancesEconomyAndSecretStashPersists()
    {
        var fixture = new ForestInnEconomyFixture();
        var inn = fixture.Enter();
        var firstVisit = fixture.Now;
        var stash = (List<InnStockOffer>)InvokeForestInn(fixture.Controller, "SecretStashStock", 13)!;
        Assert(stash.Count > 0, "Üres kezdeti titkos raktár.");
        stash.Clear();
        fixture.Controller.CaptureForestStop();
        fixture.EmptyMarket();
        var saved = JsonSerializer.Serialize(inn.Services);
        inn.Services = JsonSerializer.Deserialize<ForestInnServicesState>(saved)!;
        fixture.Controller.PrepareForestStop(13, inn);
        Assert(((List<InnStockOffer>)InvokeForestInn(fixture.Controller, "SecretStashStock", 13)!).Count == 0,
            "A titkos raktár újranyitáskor feltöltődött.");
        Assert(fixture.Controller.TryRestAtInn(fixture.Snapshot.Revision, out _), "Nem sikerült fogadóban pihenni.");
        Assert(fixture.RestCount == 1 && fixture.Now == firstVisit + 480 &&
            fixture.Controller.AdvanceForestInnTime(), "A pihenés nem vitt előre nyolc órával.");
        Assert(ForestStockCount(fixture.Market(inn)) == 4 &&
            inn.Services!.SecretStash!.Sum(offer => offer.StockCount) == 4 &&
            inn.Services.RestockThroughMinutes == firstVisit + 480,
            "Nyolcórás pihenés után nem négy ajánlat pótlódott.");
        var afterRest = ForestInnStateJson(inn);
        fixture.Controller.PrepareForestStop(13, inn);
        Assert(ForestInnStateJson(inn) == afterRest, "A pihenés utáni új belépés újra töltött.");
        var nextRest = fixture.Now + InnController.ForestRestCooldownMinutes;
        fixture.Now = nextRest;
        Assert(fixture.Controller.TryRestAtInn(fixture.Snapshot.Revision, out _), "Nem sikerült fogadóban pihenni.");
        fixture.Controller.AdvanceForestInnTime();
        Assert(fixture.RestCount == 2 && fixture.Now == firstVisit + 960 + InnController.ForestRestCooldownMinutes && inn.FirstVisitMinutes == firstVisit,
            "Új látogatáskor nem lehetett újra pihenni.");
    }
}
