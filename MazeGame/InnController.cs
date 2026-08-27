using MazeGame.Data;
using MazeGame.Domain.Characters;
using MazeGame.Domain.Combat;
using MazeGame.Domain.Inventory;
using MazeGame.Domain.Magic;
using MazeGame.Application;

namespace MazeGame;

internal sealed class InnController
{
    private const int SecretStashLevelAdvance = 4;
    private static readonly HashSet<string> MerchantExcludedItemIds = ["W001", "W005", "A001", "A002",
        "T011", "T012", "T013", "T014", "T015", "T016", "T017", "T018", "T019", "T020", "T023", "T024"];
    private static readonly HashSet<string> WitcherOnlyItemIds = ["T011", "T012", "T013", "T014", "T015", "T016", "T017", "T018", "T019", "T020"];

    private readonly GameDataCatalog _gameData;
    private readonly CharacterRoster _characterRoster;
    private readonly LiveCharacter _selectedCharacter;
    private readonly ConsoleRenderer _renderer;
    private readonly Action<SoundEffect> _playGlobalSound;
    private readonly Random _random;
    private readonly Func<LiveCharacter, int, LevelUpResult> _awardExperience;
    private readonly Action<LiveCharacter, LevelUpResult> _resolvePerkOffers;
    private readonly Action _preparePartySpells;
    private readonly Func<ConsoleKeyInfo> _readKey;
    private readonly Action<PartyRestSnapshot> _reportRest;
    private readonly Dictionary<InnVendorKind, List<InnStockOffer>> _vendorStocks = [];
    private readonly List<InnRumor> _rumors = [];
    private readonly Queue<InnTransactionSnapshot> _transactions = new();
    private readonly Dictionary<string, int> _buybackPrices = new(StringComparer.OrdinalIgnoreCase);
    private long _transactionSequence;
    private long _revision;
    private bool _active;
    private bool _hasRestedAtInn;
    private int _secretStashAccessCost;

    public InnController(GameDataCatalog gameData, CharacterRoster characterRoster, LiveCharacter selectedCharacter,
        ConsoleRenderer renderer, Action<SoundEffect> playGlobalSound, Random random,
        Func<LiveCharacter, int, LevelUpResult> awardExperience,
        Action<LiveCharacter, LevelUpResult> resolvePerkOffers,
        Action preparePartySpells, Func<ConsoleKeyInfo>? readKey = null,
        Action<PartyRestSnapshot>? reportRest = null)
    {
        _gameData = gameData;
        _characterRoster = characterRoster;
        _selectedCharacter = selectedCharacter;
        _renderer = renderer;
        _playGlobalSound = playGlobalSound;
        _random = random;
        _awardExperience = awardExperience;
        _resolvePerkOffers = resolvePerkOffers;
        _preparePartySpells = preparePartySpells;
        _readKey = readKey ?? (() => Console.ReadKey(intercept: true));
        _reportRest = reportRest ?? (_ => { });
    }

    public InnSnapshot? CreateSnapshot()
    {
        if (!_active) return null;
        var vendors = _vendorStocks.Select(pair => new InnVendorSnapshot(pair.Key, VendorName(pair.Key),
            pair.Value.Select((offer, index) => new InnOfferSnapshot(index, ToSnapshot(offer.Item), offer.Price)).ToArray()))
            .ToArray();
        return new InnSnapshot(_revision, _selectedCharacter.Gold, vendors,
            _rumors.Select(rumor => new InnRumorSnapshot(rumor.Title, rumor.Lines, rumor.Color)).ToArray(),
            _transactions.ToArray(),
            _buybackPrices.Select(pair => new InnSellPriceSnapshot(pair.Key, pair.Value)).ToArray());
    }

    public bool TryPurchase(InnVendorKind vendor, int offerIndex, long expectedRevision,
        LiveCharacter recipient, out string message)
    {
        if (!_active) { message = "A parti jelenleg nincs a fogadóban."; return false; }
        if (expectedRevision != _revision) { message = "A készlet időközben megváltozott; válassz újra."; return false; }
        if (!_vendorStocks.TryGetValue(vendor, out var stock)) { message = "Ez a kereskedő most nincs jelen."; return false; }
        if (offerIndex < 0 || offerIndex >= stock.Count) { message = "Az ajánlat már nem érhető el."; return false; }
        var backpackIndex = recipient.Backpack.ToList().FindIndex(item => item is null);
        if (backpackIndex < 0) { message = $"{recipient.Name} hátizsákja tele van."; return false; }
        var offer = stock[offerIndex];
        if (!_selectedCharacter.SpendGold(offer.Price))
        { message = $"Nincs elég közös arany: még {offer.Price - _selectedCharacter.Gold} hiányzik."; return false; }
        recipient.SetInventoryItem(InventorySlotKind.Backpack, backpackIndex, offer.Item);
        stock.RemoveAt(offerIndex);
        _revision++;
        message = $"Megvetted: {offer.Item.Name} ({offer.Price} arany).";
        RecordTransaction(InnTransactionKind.Purchase, recipient.Name, offer.Item.Name, offer.Price,
            recipient.Name, announceOnHost: true);
        return true;
    }

    public bool TrySell(long expectedInnRevision, long expectedInventoryRevision, int backpackIndex,
        LiveCharacter seller, out string message)
    {
        if (!_active) { message = "A parti jelenleg nincs a fogadóban."; return false; }
        if (expectedInnRevision != _revision)
        { message = "A fogadói készlet időközben megváltozott; válassz újra."; return false; }
        if (seller.InventoryRevision != expectedInventoryRevision)
        { message = "Az inventory időközben megváltozott; válassz újra."; return false; }
        if (backpackIndex < 0 || backpackIndex >= seller.Backpack.Count || seller.Backpack[backpackIndex] is not { } item)
        { message = "A kijelölt hátizsákhely üres vagy érvénytelen."; return false; }
        if (!_buybackPrices.TryGetValue(item.Id, out var price))
        { message = "Ezt a tárgyat a kereskedő nem veszi meg."; return false; }
        if (!seller.SetInventoryItem(InventorySlotKind.Backpack, backpackIndex, null))
        { message = "Az eladás most nem hajtható végre."; return false; }
        _selectedCharacter.AddGold(price);
        _revision++;
        message = $"Eladtad: {item.Name} ({price} arany).";
        RecordTransaction(InnTransactionKind.Sale, seller.Name, item.Name, price, seller.Name,
            announceOnHost: true);
        return true;
    }

    private static InventoryItemSnapshot ToSnapshot(IItemDefinition item) => new(item.Id, item.Name, item.Category,
        item.Rarity, item is MagicItemDefinition magic ? magic.MaximumCharges : 0,
        item is MagicItemDefinition magicItem ? magicItem.MaximumCharges : 0,
        item is WeaponDefinition { IsTwoHanded: true }, item.Description, item.BasePrice, item.MagicPower);

    private static string VendorName(InnVendorKind vendor) => vendor switch
    {
        InnVendorKind.Market => "Kereskedő",
        InnVendorKind.Witcher => "Vajákos",
        InnVendorKind.Blacksmith => "Kovácsmester",
        InnVendorKind.Armorer => "Páncélmíves",
        InnVendorKind.WanderingMage => "Vándormágus portéka",
        _ => vendor.ToString()
    };

    public void Run(int completedLevel)
    {
        _hasRestedAtInn = false;
        _secretStashAccessCost = _random.Next(50, 101) + completedLevel * 50;
        var completion = CompleteLevelAtInn(completedLevel);
        _renderer.DrawLevelCompletionScreen(completedLevel, _gameData.BaseLevelCompletionExperience,
            completion.Results, completion.FallenCharacters);
        foreach (var levelResult in completion.Results.Where(result => result.Experience.LeveledUp))
            _resolvePerkOffers(levelResult.Character, levelResult.Experience);

        var blacksmithPresent = _random.Next(2) == 0;
        var armorerPresent = _random.Next(2) == 0;
        var wanderingMagePresent = _random.Next(100) < 30;
        var blacksmithStock = blacksmithPresent ? CreateSpecialistStock(completedLevel, ItemCategory.Weapon) : [];
        var armorerStock = armorerPresent ? CreateSpecialistStock(completedLevel, ItemCategory.Armor) : [];
        var wanderingMageStock = wanderingMagePresent ? CreateWanderingMageStock() : [];
        _vendorStocks.Clear();
        _vendorStocks[InnVendorKind.Market] = CreateInnStock(completedLevel).ToList();
        _vendorStocks[InnVendorKind.Witcher] = CreateWitcherStock(completedLevel).ToList();
        if (blacksmithPresent) _vendorStocks[InnVendorKind.Blacksmith] = blacksmithStock;
        if (armorerPresent) _vendorStocks[InnVendorKind.Armorer] = armorerStock;
        if (wanderingMagePresent) _vendorStocks[InnVendorKind.WanderingMage] = wanderingMageStock;
        _buybackPrices.Clear();
        foreach (var item in AllTradableItems())
            _buybackPrices[item.Id] = Math.Max(1, item.BasePrice * _random.Next(40, 71) / 100);
        _rumors.Clear();
        _transactions.Clear();
        var shownRumors = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < 4; index++)
            _rumors.Add(CreateUniqueInnRumor(completedLevel, shownRumors));
        _revision++;
        _active = true;
        var presentVisitors = new List<string>();
        if (blacksmithPresent) presentVisitors.Add("a Kovácsmester");
        if (armorerPresent) presentVisitors.Add("a Páncélmíves");
        if (wanderingMagePresent) presentVisitors.Add("a Vándormágus");
        var artisanNotice = presentVisitors.Count == 0
            ? "A fogadós jelzi: ma egyik vándormester sincs jelen."
            : $"A fogadós jelzi: ma {HungarianList(presentVisitors)} van jelen.";
        var options = new List<(InnMenuOption Option, string Label, string Description)>
        {
            (InnMenuOption.Rest, "🛏️ Pihenés", "HP és manna feltöltése, majd varázslatok memorizálása minden partitag számára."),
            (InnMenuOption.Market, "🛒 Kereskedő", "Felszerelés vétele és eladása."),
            (InnMenuOption.Witcher, "⚗️ Vajákos", "Gyógy- és varázsitalok, kötés és gyógyfüves készítmények."),
            (InnMenuOption.SecretStash, $"🗝️ Titkos raktár ({_secretStashAccessCost} {ConsoleRenderer.MoneyIcon})", "Fejlettebb, drágább különleges készlet a kereskedő pultja mögött.")
        };
        if (blacksmithPresent) options.Add((InnMenuOption.Blacksmith, "🔨 Kovácsmester", "Kizárólag fegyvereket kínál, csak vásárlásra."));
        if (armorerPresent) options.Add((InnMenuOption.Armorer, "🛡️ Páncélmíves", "Kizárólag páncélokat kínál, csak vásárlásra."));
        if (wanderingMagePresent) options.Add((InnMenuOption.WanderingMage, "🧙 Vándormágus", "Varázspálcák feltöltése, különleges portéka és varázstárgy-azonosítás."));
        options.Add((InnMenuOption.Recruit, "⚔️ Zsoldosok toborzása", "Új partitagok felfogadása."));
        options.Add((InnMenuOption.Rumors, "👂 Pletykák", "Hírek a következő pályáról és a környékbeli szörnyekről."));
        options.Add((InnMenuOption.Leave, "🚪 Indulás a következő pályára", "A parti elhagyja a fogadót."));
        var selectedIndex = 0;
        var redraw = true;
        while (true)
        {
            var displayOptions = options.Select(option => (option.Label, option.Description)).ToList();
            if (redraw)
            {
                _renderer.DrawInnMenuScreen(_selectedCharacter, _characterRoster.Party.Members.Count, selectedIndex, displayOptions, artisanNotice);
                redraw = false;
            }
            var key = _readKey().Key;
            if (key == ConsoleKey.UpArrow)
            {
                var previousIndex = selectedIndex;
                selectedIndex = (selectedIndex - 1 + options.Count) % options.Count;
                _renderer.UpdateInnMenuSelection(displayOptions, previousIndex, selectedIndex);
                continue;
            }
            if (key == ConsoleKey.DownArrow)
            {
                var previousIndex = selectedIndex;
                selectedIndex = (selectedIndex + 1) % options.Count;
                _renderer.UpdateInnMenuSelection(displayOptions, previousIndex, selectedIndex);
                continue;
            }
            if (key != ConsoleKey.Enter) continue;
            redraw = true;

            switch (options[selectedIndex].Option)
            {
                case InnMenuOption.Rest: RestPartyAtInn(); break;
                case InnMenuOption.Market: RunInnMarket(completedLevel); break;
                case InnMenuOption.Witcher: RunWitcherMarket(completedLevel); break;
                case InnMenuOption.SecretStash: RunInnSecretStash(completedLevel); break;
                case InnMenuOption.Blacksmith: RunSpecialistMarket("🔨 KOVÁCSMESTER", blacksmithStock); break;
                case InnMenuOption.Armorer: RunSpecialistMarket("🛡️ PÁNCÉLMÍVES", armorerStock); break;
                case InnMenuOption.WanderingMage: RunWanderingMage(wanderingMageStock); break;
                case InnMenuOption.Recruit: RunInnRecruitment(); break;
                case InnMenuOption.Rumors: RunInnRumors(); break;
                case InnMenuOption.Leave: _active = false; return;
            }
        }
    }

    private LevelCompletionOutcome CompleteLevelAtInn(int completedLevel)
    {
        var reward = checked(_gameData.BaseLevelCompletionExperience * completedLevel);
        var fallenCharacters = _characterRoster.Party.Members.Where(character => !character.IsAlive).ToList();
        foreach (var fallen in fallenCharacters) _characterRoster.Remove(fallen);
        var results = _characterRoster.Party.Members
            .Select(character => new LevelCompletionResult(character, _awardExperience(character, reward)))
            .ToList();
        return new LevelCompletionOutcome(results, fallenCharacters);
    }

    private void RestPartyAtInn()
    {
        if (_hasRestedAtInn)
        {
            _renderer.DrawInnRestUnavailableScreen();
            return;
        }
        var summaries = new List<(LiveCharacter Character, int HealedAmount)>();
        foreach (var character in _characterRoster.Party.Members.Where(character => character.IsAlive))
        {
            var before = character.CurrentVitality;
            character.RestoreVitality(_random.Next(20, 41));
            character.SetCurrentResources(character.CurrentVitality, character.MaximumMana);
            character.ClearTemporarySpellEffects();
            summaries.Add((character, character.CurrentVitality - before));
        }
        _hasRestedAtInn = true;
        _reportRest(new PartyRestSnapshot(Guid.NewGuid(), true, summaries.Select(summary =>
            new CharacterRestSnapshot(summary.Character.Id, summary.Character.Name, summary.HealedAmount, [])).ToArray()));
        _renderer.DrawInnRestScreen(summaries);
        _playGlobalSound(SoundEffect.Rest);
        _preparePartySpells();
    }

    private void RunInnMarket(int completedLevel)
    {
        var stock = _vendorStocks[InnVendorKind.Market];
        var mode = InnMarketMode.Buy;
        var selectedIndex = 0;
        var message = "A kereskedő rád kacsint: „Nézz körül, kalandozó!”";
        var redraw = true;

        while (true)
        {
            var sellOffers = CreateSellOffers(_buybackPrices);
            var entryCount = mode == InnMarketMode.Buy ? stock.Count : sellOffers.Count;
            selectedIndex = entryCount == 0 ? 0 : Math.Clamp(selectedIndex, 0, entryCount - 1);
            if (redraw)
            {
                _renderer.DrawInnMarketScreen(_selectedCharacter, mode, stock, sellOffers, selectedIndex,
                    _characterRoster.Party.Members.Sum(character => character.Backpack.Count(item => item is null)), message);
                redraw = false;
            }

            var key = _readKey().Key;
            if (key == ConsoleKey.Escape) return;
            if (key is ConsoleKey.LeftArrow or ConsoleKey.RightArrow or ConsoleKey.Tab)
            {
                mode = mode == InnMarketMode.Buy ? InnMarketMode.Sell : InnMarketMode.Buy;
                selectedIndex = 0;
                message = mode == InnMarketMode.Buy ? "A kereskedő kínálata." : "Csak a hátizsákok tárgyai adhatók el.";
                redraw = true;
                continue;
            }
            if (key == ConsoleKey.UpArrow && entryCount > 0)
            {
                var previousIndex = selectedIndex;
                selectedIndex = (selectedIndex - 1 + entryCount) % entryCount;
                _renderer.UpdateInnMarketSelection(mode, stock, sellOffers, previousIndex, selectedIndex);
                redraw = false;
                continue;
            }
            if (key == ConsoleKey.DownArrow && entryCount > 0)
            {
                var previousIndex = selectedIndex;
                selectedIndex = (selectedIndex + 1) % entryCount;
                _renderer.UpdateInnMarketSelection(mode, stock, sellOffers, previousIndex, selectedIndex);
                redraw = false;
                continue;
            }
            if (key != ConsoleKey.Enter || entryCount == 0) continue;
            redraw = true;

            if (mode == InnMarketMode.Buy)
            {
                if (stock.Count == 0) { selectedIndex = 0; continue; }
                selectedIndex = Math.Clamp(selectedIndex, 0, stock.Count - 1);
                var offer = stock[selectedIndex];
                var recipient = _characterRoster.Party.Members.FirstOrDefault(character => character.Backpack.Any(item => item is null));
                if (recipient is null) { message = "🎒 A parti összes hátizsákja tele van."; continue; }
                if (!_selectedCharacter.SpendGold(offer.Price)) { message = $"{ConsoleRenderer.MoneyIcon} Nincs elég aranyad: még {offer.Price - _selectedCharacter.Gold} hiányzik."; continue; }
                recipient.AddToBackpack(offer.Item);
                stock.RemoveAt(selectedIndex);
                _revision++;
                message = $"✅ Megvetted: {offer.Item.Name} → {recipient.Name} hátizsákja ({offer.Price} arany).";
                RecordTransaction(InnTransactionKind.Purchase, _selectedCharacter.Name, offer.Item.Name,
                    offer.Price, recipient.Name);
            }
            else
            {
                var offer = sellOffers[selectedIndex];
                if (!offer.Owner.SetInventoryItem(InventorySlotKind.Backpack, offer.BackpackIndex, null))
                { message = "Az üzlet most nem hajtható végre."; continue; }
                _selectedCharacter.AddGold(offer.Price);
                _revision++;
                message = $"✅ Eladtad: {offer.Item.Name} {offer.Price} aranyért ({offer.Owner.Name} hátizsákjából).";
                RecordTransaction(InnTransactionKind.Sale, _selectedCharacter.Name, offer.Item.Name,
                    offer.Price, offer.Owner.Name);
            }
        }
    }

    private IReadOnlyList<InnStockOffer> CreateInnStock(int completedLevel) =>
        CreateInnStock(completedLevel, completedLevel, 1.0, includePremiumStock: true,
            includeRandomLegendary: true, includePremiumSupplies: false);

    private void RunInnSecretStash(int completedLevel)
    {
        if (_selectedCharacter.Gold < _secretStashAccessCost)
        {
            _renderer.DrawDeveloperMessage($"🗝️ A kereskedő titkos készletéhez {_secretStashAccessCost} arany kell; még {_secretStashAccessCost - _selectedCharacter.Gold} hiányzik.");
            return;
        }
        if (!_renderer.ConfirmInnSecretStashAccess(_secretStashAccessCost)) return;
        _selectedCharacter.SpendGold(_secretStashAccessCost);
        var secretLevel = completedLevel + SecretStashLevelAdvance;
        var stock = CreateInnStock(completedLevel, secretLevel, _random.Next(105, 121) / 100.0,
            includePremiumStock: false, includeRandomLegendary: false, includePremiumSupplies: true).ToList();
        AddSecretStashSpecialOffer(stock, completedLevel, secretLevel);
        stock.Sort((left, right) => left.Price.CompareTo(right.Price));
        var selectedIndex = 0;
        var message = $"🗝️ {_secretStashAccessCost} aranyért a kereskedő megmutatta titkos, fejlettebb készletét.";
        var redraw = true;
        while (true)
        {
            var entryCount = stock.Count;
            selectedIndex = entryCount == 0 ? 0 : Math.Clamp(selectedIndex, 0, entryCount - 1);
            if (redraw)
            {
                _renderer.DrawInnSecretStashScreen(_selectedCharacter, stock, selectedIndex,
                    _characterRoster.Party.Members.Sum(character => character.Backpack.Count(item => item is null)), message);
                redraw = false;
            }

            var key = _readKey().Key;
            if (key == ConsoleKey.Escape) return;
            if (key == ConsoleKey.UpArrow && entryCount > 0)
            {
                var previousIndex = selectedIndex;
                selectedIndex = (selectedIndex - 1 + entryCount) % entryCount;
                _renderer.UpdateInnBuyOnlySelection(stock, previousIndex, selectedIndex);
                redraw = false;
                continue;
            }
            if (key == ConsoleKey.DownArrow && entryCount > 0)
            {
                var previousIndex = selectedIndex;
                selectedIndex = (selectedIndex + 1) % entryCount;
                _renderer.UpdateInnBuyOnlySelection(stock, previousIndex, selectedIndex);
                redraw = false;
                continue;
            }
            if (key != ConsoleKey.Enter || entryCount == 0) continue;
            redraw = true;

            var offer = stock[selectedIndex];
            var recipient = _characterRoster.Party.Members.FirstOrDefault(character => character.Backpack.Any(item => item is null));
            if (recipient is null) { message = "🎒 A parti összes hátizsákja tele van."; continue; }
            if (!_selectedCharacter.SpendGold(offer.Price)) { message = $"{ConsoleRenderer.MoneyIcon} Nincs elég aranyad: még {offer.Price - _selectedCharacter.Gold} hiányzik."; continue; }
            recipient.AddToBackpack(offer.Item);
            stock.RemoveAt(selectedIndex);
            _revision++;
            message = $"✅ Megvetted: {offer.Item.Name} → {recipient.Name} hátizsákja ({offer.Price} arany).";
            RecordTransaction(InnTransactionKind.Purchase, _selectedCharacter.Name, offer.Item.Name,
                offer.Price, recipient.Name);
        }
    }

    private IReadOnlyList<InnStockOffer> CreateInnStock(int completedLevel, int unlockLevel, double priceMultiplier,
        bool includePremiumStock, bool includeRandomLegendary, bool includePremiumSupplies)
    {
        var allItems = AllTradableItems().Where(item => item.Rarity != ItemRarity.Legendary).OrderBy(item => item.BasePrice).ToList();
        var normalUnlockedCount = Math.Min(allItems.Count, 8 + unlockLevel * 8);
        var normalPool = allItems.Take(normalUnlockedCount).ToList();
        var baseStockCount = Math.Min(normalPool.Count, Math.Min(12, 5 + completedLevel));
        var stockCount = Math.Min(allItems.Count, Math.Min(18, (int)Math.Ceiling(baseStockCount * 1.5))) + 2;
        var premiumUnlockedCount = Math.Min(allItems.Count, 40 + unlockLevel * 19);
        var premiumPool = allItems.Skip(normalUnlockedCount).Take(premiumUnlockedCount - normalUnlockedCount).ToList();
        var premiumStockCount = includePremiumStock
            ? Math.Min(premiumPool.Count, Math.Max(1, stockCount / 3))
            : 0;
        var normalStockCount = stockCount - premiumStockCount;
        var offers = new List<InnStockOffer>(stockCount);
        while (offers.Count < normalStockCount && normalPool.Count > 0)
        {
            var equipmentPool = completedLevel < 5 && offers.Count >= baseStockCount * 2 / 3
                ? normalPool.Where(item => item.Category is ItemCategory.Weapon or ItemCategory.Armor && item.Rarity == ItemRarity.Normal).ToList()
                : [];
            var item = SelectWeightedStockItem(equipmentPool.Count > 0 ? equipmentPool : normalPool, unlockLevel);
            normalPool.Remove(item);
            offers.Add(CreateInnStockOffer(item, priceMultiplier, completedLevel));
        }
        while (offers.Count < stockCount && premiumPool.Count > 0)
        {
            var item = SelectWeightedStockItem(premiumPool, unlockLevel);
            premiumPool.Remove(item);
            offers.Add(CreateInnStockOffer(item, priceMultiplier, completedLevel));
        }
        while (offers.Count < stockCount && normalPool.Count > 0)
        {
            var item = SelectWeightedStockItem(normalPool, unlockLevel);
            normalPool.Remove(item);
            offers.Add(CreateInnStockOffer(item, priceMultiplier, completedLevel));
        }
        var legendaryChance = Math.Min(0.08, 0.01 + unlockLevel * 0.005);
        if (includeRandomLegendary && _random.NextDouble() < legendaryChance)
        {
            var legendaryPool = AllTradableItems().Where(item => item.Rarity == ItemRarity.Legendary)
                .OrderBy(item => item.BasePrice).Take(Math.Min(40, Math.Max(4, unlockLevel * 4))).ToList();
            if (legendaryPool.Count > 0)
            {
                if (offers.Count >= stockCount) offers.RemoveAt(_random.Next(offers.Count));
                var legendary = legendaryPool[_random.Next(legendaryPool.Count)];
                var price = (int)Math.Round(legendary.BasePrice * _random.Next(125, 181) / 100.0 * priceMultiplier);
                offers.Add(new InnStockOffer(legendary, price));
            }
        }

        var fixedExtras = includePremiumSupplies
            ? CreateSecretStashSupplies()
            : new[] { "T001", "T001", "T001", "T001", "T002", "T002", "T002", "T002" };

        foreach (var itemId in fixedExtras)
        {
            var fixedItem = _gameData.Items.FirstOrDefault(i => string.Equals(i.Id, itemId, StringComparison.OrdinalIgnoreCase));
            if (fixedItem is null) continue;
            offers.Add(CreateInnStockOffer(fixedItem, priceMultiplier, completedLevel));
        }

        return offers.OrderBy(offer => offer.Price).ToList();
    }

    private IReadOnlyList<string> CreateSecretStashSupplies()
    {
        var firstDrink = _random.Next(2) == 0 ? MiscItemIds.Mead : MiscItemIds.SpicedWine;
        var secondDrink = firstDrink == MiscItemIds.Mead ? MiscItemIds.SpicedWine : MiscItemIds.Mead;
        return ["T008", "T007", "T010", firstDrink, firstDrink, secondDrink];
    }

    private void AddSecretStashSpecialOffer(ICollection<InnStockOffer> stock, int completedLevel, int secretLevel)
    {
        var specialPool = completedLevel <= 5
            ? AllTradableItems().Where(item => item.Rarity == ItemRarity.Magic && item.MagicPower == 3 &&
                    item.Category is ItemCategory.Weapon or ItemCategory.Armor)
                .OrderBy(item => item.BasePrice).Take(12).ToList()
            : AllTradableItems().Where(item => item.Rarity == ItemRarity.Legendary)
                .OrderBy(item => item.BasePrice).Take(Math.Min(12, Math.Max(4, secretLevel * 2))).ToList();
        if (specialPool.Count == 0) return;
        if (stock.Count > 0) stock.Remove(stock.OrderBy(offer => offer.Price).First());
        var special = specialPool[_random.Next(specialPool.Count)];
        stock.Add(CreateInnStockOffer(special, 1.0, completedLevel));
    }

    private IItemDefinition SelectWeightedStockItem(IReadOnlyList<IItemDefinition> candidates, int unlockLevel)
    {
        var totalWeight = candidates.Select((_, index) => 1 + index * Math.Max(1, unlockLevel)).Sum();
        var roll = _random.Next(totalWeight);
        for (var index = 0; index < candidates.Count - 1; index++)
        {
            roll -= 1 + index * Math.Max(1, unlockLevel);
            if (roll < 0) return candidates[index];
        }
        return candidates[^1];
    }

    private InnStockOffer CreateInnStockOffer(IItemDefinition item, double priceMultiplier, int completedLevel)
    {
        var expensiveProbability = Math.Min(0.80, 0.20 + 0.07 * (completedLevel - 1));
        var percentage = _random.NextDouble() < expensiveProbability ? _random.Next(105, 151) : _random.Next(85, 101);
        var price = Math.Max(1, (int)Math.Round(item.BasePrice * percentage / 100.0 * priceMultiplier));
        return new InnStockOffer(item, price);
    }

    private IReadOnlyList<InnSellOffer> CreateSellOffers(IReadOnlyDictionary<string, int> buybackPrices) =>
        _characterRoster.Party.Members.SelectMany(character => character.Backpack
            .Select((item, index) => item is null || !buybackPrices.TryGetValue(item.Id, out var price)
                ? null
                : new InnSellOffer(character, index, item, price)))
            .Where(offer => offer is not null).Cast<InnSellOffer>()
            .OrderBy(offer => offer.Price).ToList();

    private void RunInnRecruitment()
    {
        var generator = new RandomCharacterGenerator(_gameData, _random);
        var candidateCount = _random.Next(1, 4);
        var classes = _gameData.CharacterClasses.OrderBy(_ => _random.Next()).Take(candidateCount).ToList();
        var usedNames = _characterRoster.Characters.Select(character => character.Name).ToList();
        var candidates = new List<LiveCharacter>();
        foreach (var characterClass in classes)
        {
            var candidate = generator.CreateRecruit(characterClass, _selectedCharacter.Level,
                usedNames.Concat(candidates.Select(character => character.Name)).ToList());
            candidates.Add(candidate);
        }
        var recruitmentPrices = candidates.ToDictionary(candidate => candidate,
            candidate => candidate.Level < _selectedCharacter.Level
                ? 0
                : Math.Max(1, candidate.Level * 100 * _random.Next(50, 151) / 100));

        var selectedIndex = 0;
        var message = "A fogadós bemutatja az utazásra kész zsoldosokat.";
        var redraw = true;
        while (candidates.Count > 0)
        {
            selectedIndex = Math.Clamp(selectedIndex, 0, candidates.Count - 1);
            if (redraw)
            {
                _renderer.DrawInnRecruitmentScreen(candidates, recruitmentPrices, selectedIndex,
                    _characterRoster.Party.Members, _selectedCharacter.Gold, message);
                redraw = false;
            }
            var key = _readKey().Key;
            if (key == ConsoleKey.Escape) return;
            if (key == ConsoleKey.UpArrow)
            {
                var previousIndex = selectedIndex;
                selectedIndex = (selectedIndex - 1 + candidates.Count) % candidates.Count;
                _renderer.UpdateInnRecruitmentSelection(candidates, recruitmentPrices, previousIndex, selectedIndex);
                continue;
            }
            if (key == ConsoleKey.DownArrow)
            {
                var previousIndex = selectedIndex;
                selectedIndex = (selectedIndex + 1) % candidates.Count;
                _renderer.UpdateInnRecruitmentSelection(candidates, recruitmentPrices, previousIndex, selectedIndex);
                continue;
            }
            if (key != ConsoleKey.Enter) continue;
            redraw = true;

            var recruit = candidates[selectedIndex];
            var price = recruitmentPrices[recruit];
            if (_selectedCharacter.Gold < price)
            {
                message = $"{ConsoleRenderer.MoneyIcon} Nincs elég aranyad: {price - _selectedCharacter.Gold} arany hiányzik {recruit.Name} felbérléséhez.";
                continue;
            }
            LiveCharacter? replaced = null;
            if (_characterRoster.Party.Members.Count >= Party.MaximumSize)
            {
                var replaceable = _characterRoster.Party.Members.Skip(1).ToList();
                var replacementIndex = ChoosePartyMemberToReplace(recruit, replaceable);
                if (replacementIndex is null)
                {
                    message = "A toborzást megszakítottad; választhatsz másik jelöltet.";
                    continue;
                }
                replaced = replaceable[replacementIndex.Value];
                _characterRoster.Remove(replaced);
            }

            _selectedCharacter.SpendGold(price);
            _characterRoster.Add(recruit);
            _characterRoster.Party.Add(recruit);
            candidates.RemoveAt(selectedIndex);
            recruitmentPrices.Remove(recruit);
            message = replaced is null
                ? $"✅ {recruit.Name} csatlakozott a partihoz{FormatRecruitmentPricePaid(price)}."
                : $"✅ {recruit.Name} átvette {replaced.Name} helyét{FormatRecruitmentPricePaid(price)}; a régi társ végleg távozott.";
        }
    }

    private static string FormatRecruitmentPricePaid(int price) => price == 0
        ? " ingyen"
        : $" {price} aranyért";

    private int? ChoosePartyMemberToReplace(LiveCharacter recruit, IReadOnlyList<LiveCharacter> replaceable)
    {
        var selectedIndex = 0;
        var redraw = true;
        while (true)
        {
            if (redraw)
            {
                _renderer.DrawInnReplacementScreen(recruit, replaceable, selectedIndex);
                redraw = false;
            }
            var key = _readKey().Key;
            if (key == ConsoleKey.Escape) return null;
            if (key == ConsoleKey.UpArrow)
            {
                var previousIndex = selectedIndex;
                selectedIndex = (selectedIndex - 1 + replaceable.Count) % replaceable.Count;
                _renderer.UpdateInnReplacementSelection(replaceable, previousIndex, selectedIndex);
            }
            else if (key == ConsoleKey.DownArrow)
            {
                var previousIndex = selectedIndex;
                selectedIndex = (selectedIndex + 1) % replaceable.Count;
                _renderer.UpdateInnReplacementSelection(replaceable, previousIndex, selectedIndex);
            }
            else if (key == ConsoleKey.Enter) return selectedIndex;
        }
    }

    private void RunInnRumors()
    {
        var selectedIndex = 0;
        while (true)
        {
            if (_rumors.Count == 0) return;
            _renderer.DrawInnRumorScreen(_rumors[selectedIndex], selectedIndex, _rumors.Count);
            var key = _readKey().Key;
            if (key is ConsoleKey.Enter or ConsoleKey.Escape) return;
            if (key is ConsoleKey.N or ConsoleKey.RightArrow or ConsoleKey.DownArrow)
                selectedIndex = (selectedIndex + 1) % _rumors.Count;
            else if (key is ConsoleKey.LeftArrow or ConsoleKey.UpArrow)
                selectedIndex = (selectedIndex - 1 + _rumors.Count) % _rumors.Count;
        }
    }

    private InnRumor CreateUniqueInnRumor(int completedLevel, ISet<string> shownRumors)
    {
        InnRumor rumor;
        var attempts = 0;
        do rumor = CreateInnRumor(completedLevel);
        while (!shownRumors.Add(rumor.Title + "\n" + string.Join('\n', rumor.Lines)) && ++attempts < 30);
        return rumor;
    }

    private InnRumor CreateInnRumor(int completedLevel) => _random.Next(2) == 0
        ? CreateNextLevelRumor(completedLevel + 1)
        : CreateMonsterRumor(completedLevel);

    private InnRumor CreateNextLevelRumor(int level)
    {
        var configuration = MazeLevelConfigurations.Get(level);
        var enemyIds = configuration.RoomEncounters.Concat(configuration.CorridorEncounters)
            .SelectMany(encounter => encounter.Members).Select(member => member.EnemyId)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var enemyNames = enemyIds.Select(id => _gameData.GetEnemy(id).Name).ToList();
        var leaders = configuration.RoomEncounters.SelectMany(encounter => encounter.Members)
            .Where(member => member.Role == EnemyGroupRole.Leader)
            .Select(member => _gameData.GetEnemy(member.EnemyId).Name).Distinct().ToList();
        var corridorText = configuration.DoubleWidthCorridorChance switch
        {
            >= 0.9 => "szinte mindenütt széles, páros folyosók",
            >= 0.7 => "többnyire széles folyosók",
            <= 0.25 => "szűk, egycellás átjárók a nagyobb terek között",
            _ => "változó szélességű folyosók"
        };
        var wall = $"{configuration.WallRune} ({configuration.WallColor})";
        return new InnRumor($"Úti pletyka: {configuration.Name}",
        [
            $"A következő út a(z) {level}. szintre vezet: {configuration.Name}.",
            $"Terep: {configuration.RoomCount.Minimum}–{configuration.RoomCount.Maximum} szoba, " +
            $"{configuration.RoomSize.Minimum}–{configuration.RoomSize.Maximum} mezős oldalakkal; {corridorText}.",
            $"A falazat jele és színe: {wall}.",
            $"Várható ellenfelek: {string.Join(", ", enemyNames)}.",
            leaders.Count == 0
                ? "Külön vezéralakú szörnycsoportról nem érkezett biztos hír."
                : $"Vezérrel felvonuló csoportra is számíts: {string.Join(", ", leaders)}.",
            "A fogadós tanácsa: töltsd fel az ellátmányt, és a felsorolt ellenfelek képességeihez igazítsd a felszerelést."
        ], ConsoleColor.Yellow);
    }

    private InnRumor CreateMonsterRumor(int completedLevel)
    {
        var nearbyLevels = Enumerable.Range(Math.Max(1, completedLevel - 1), 3)
            .Where(level => level <= completedLevel + 1).ToList();
        var candidates = nearbyLevels.SelectMany(level =>
                MazeLevelConfigurations.Get(level).RoomEncounters
                    .Concat(MazeLevelConfigurations.Get(level).CorridorEncounters)
                    .SelectMany(encounter => encounter.Members)
                    .Select(member => (Level: level, Enemy: _gameData.GetEnemy(member.EnemyId))))
            .GroupBy(entry => entry.Enemy.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First()).ToList();
        var selected = candidates[_random.Next(candidates.Count)];
        var enemy = selected.Enemy;
        var abilities = enemy.AbilityIds.Select(_gameData.GetMonsterAbility).ToList();
        var lines = new List<string>
        {
            $"A(z) {selected.Level}. szint környékén látták. Jel a térképen: {enemy.Appearance}.",
            $"Erősség: {enemy.StrengthTier}/5; HP {enemy.HitPoints ?? 0}; Erő {enemy.Strength ?? 0}; " +
            $"Páncél {enemy.Armor ?? 0}; Gyorsaság {enemy.Speed ?? 0}; jutalom {enemy.ExperienceReward} XP."
        };
        if (abilities.Count == 0) lines.Add("Nincs ismert különleges képessége.");
        else
            foreach (var ability in abilities)
            {
                var activation = ability.Effect == MonsterAbilityEffect.Trait
                    ? "állandó tulajdonság"
                    : $"{ability.ChancePercent}% aktiválási esély, érték {ability.Value}";
                lines.Add($"{ability.Name} — {activation}. {ability.Description}");
            }
        lines.Add($"Mozgástempója a Gyorsasága alapján körülbelül {1400 / Math.Max(1, enemy.Speed ?? 2)} ms lépésenként.");
        return new InnRumor($"Szörnypletyka: {enemy.Name}", lines, ConsoleColor.Cyan);
    }

    private IReadOnlyList<IItemDefinition> AllTradableItems() => _gameData.Items.Cast<IItemDefinition>()
        .Concat(_gameData.Weapons).Concat(_gameData.Armors).Concat(_gameData.MagicItems)
        .Where(item => !SpellcastingRules.IsRestrictedFromTradingAndGeneration(item))
        .Where(item => !MerchantExcludedItemIds.Contains(item.Id)).ToList();

    private IReadOnlyList<InnStockOffer> CreateWitcherStock(int completedLevel)
    {
        var allowedItems = _gameData.Items.Where(item => WitcherOnlyItemIds.Contains(item.Id)).OrderBy(item => item.BasePrice).ToList();
        var stock = new List<InnStockOffer>();
        if (allowedItems.Count == 0) return stock;

        var minQuantities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["T011"] = 3,
            ["T012"] = 1,
            ["T014"] = 1,
            ["T018"] = 1,
            ["T019"] = 1
        };

        var priceMultiplier = 1.0;
        foreach (var kv in minQuantities)
        {
            var item = allowedItems.FirstOrDefault(i => string.Equals(i.Id, kv.Key, StringComparison.OrdinalIgnoreCase));
            if (item is null) continue;
            for (var i = 0; i < kv.Value; i++) stock.Add(CreateInnStockOffer(item, priceMultiplier, completedLevel));
        }

        var baseCount = Math.Min(allowedItems.Count * 3, Math.Max(8, 7 + completedLevel));
        while (stock.Count < baseCount)
        {
            var item = allowedItems[_random.Next(allowedItems.Count)];
            stock.Add(CreateInnStockOffer(item, priceMultiplier, completedLevel));
        }

        AddWitcherExtraStock(allowedItems, stock, completedLevel, 3, "T012");
        AddWitcherExtraStock(allowedItems, stock, completedLevel, 6, "T012");
        AddWitcherExtraStock(allowedItems, stock, completedLevel, 8, "T013");
        AddWitcherExtraStock(allowedItems, stock, completedLevel, 10, "T013");

        return stock.OrderBy(offer => offer.Price).ToList();
    }

    private void AddWitcherExtraStock(IReadOnlyList<MiscItemDefinition> allowedItems, ICollection<InnStockOffer> stock,
        int completedLevel, int minimumLevel, string itemId)
    {
        if (completedLevel < minimumLevel) return;
        var extraCount = _random.Next(1, 2);
        for (var i = 0; i < extraCount; i++)
        {
            var item = allowedItems.FirstOrDefault(i => string.Equals(i.Id, itemId, StringComparison.OrdinalIgnoreCase));
            if (item is not null) stock.Add(CreateInnStockOffer(item, 1.0, completedLevel));
        }
    }

    private void RunWitcherMarket(int completedLevel)
    {
        var stock = _vendorStocks[InnVendorKind.Witcher];
        var mode = InnMarketMode.Buy;
        var selectedIndex = 0;
        var message = "A vajákos bólint: 'Gyógyitalok és orvosságok, kigyógyítom a sebet.'";
        var redraw = true;

        while (true)
        {
            var sellOffers = new List<InnSellOffer>();
            var entryCount = stock.Count;
            selectedIndex = entryCount == 0 ? 0 : Math.Clamp(selectedIndex, 0, entryCount - 1);
            if (redraw)
            {
                _renderer.DrawInnMarketScreen(_selectedCharacter, mode, stock, sellOffers, selectedIndex,
                    _characterRoster.Party.Members.Sum(character => character.Backpack.Count(item => item is null)), message);
                redraw = false;
            }

            var key = _readKey().Key;
            if (key == ConsoleKey.Escape) return;
            if (key == ConsoleKey.UpArrow && entryCount > 0)
            {
                var previousIndex = selectedIndex;
                selectedIndex = (selectedIndex - 1 + entryCount) % entryCount;
                _renderer.UpdateInnMarketSelection(mode, stock, sellOffers, previousIndex, selectedIndex);
                redraw = false;
                continue;
            }
            if (key == ConsoleKey.DownArrow && entryCount > 0)
            {
                var previousIndex = selectedIndex;
                selectedIndex = (selectedIndex + 1) % entryCount;
                _renderer.UpdateInnMarketSelection(mode, stock, sellOffers, previousIndex, selectedIndex);
                redraw = false;
                continue;
            }
            if (key != ConsoleKey.Enter || entryCount == 0) continue;
            redraw = true;
            if (stock.Count == 0) { selectedIndex = 0; continue; }
            selectedIndex = Math.Clamp(selectedIndex, 0, stock.Count - 1);
            var offer = stock[selectedIndex];
            var recipient = _characterRoster.Party.Members.FirstOrDefault(character => character.Backpack.Any(item => item is null));
            if (recipient is null) { message = "🎒 A parti összes hátizsákja tele van."; continue; }
            if (!_selectedCharacter.SpendGold(offer.Price)) { message = $"{ConsoleRenderer.MoneyIcon} Nincs elég aranyad: még {offer.Price - _selectedCharacter.Gold} hiányzik."; continue; }
            recipient.AddToBackpack(offer.Item);
            stock.RemoveAt(selectedIndex);
            _revision++;
            message = $"✅ Megvetted: {offer.Item.Name} → {recipient.Name} hátizsákja ({offer.Price} arany).";
            RecordTransaction(InnTransactionKind.Purchase, _selectedCharacter.Name, offer.Item.Name,
                offer.Price, recipient.Name);
        }
    }

    private List<InnStockOffer> CreateSpecialistStock(int completedLevel, ItemCategory category)
    {
        var totalCount = _random.Next(2, 5) + completedLevel / 3;
        var magicCount = completedLevel switch { >= 15 => 4, >= 10 => 3, >= 5 => 2, >= 4 => 1, _ => 0 };
        magicCount = Math.Min(magicCount, totalCount);
        var magicPower = completedLevel switch { >= 12 => 3, >= 8 => 2, >= 4 => 1, _ => 0 };
        var source = category == ItemCategory.Weapon
            ? _gameData.Weapons.Cast<IItemDefinition>()
            : _gameData.Armors.Cast<IItemDefinition>();
        var normalPool = source.Where(item => item.Rarity == ItemRarity.Normal).OrderBy(_ => _random.Next()).ToList();
        var magicPool = source.Where(item => item.Rarity == ItemRarity.Magic && item.MagicPower == magicPower)
            .OrderBy(_ => _random.Next()).ToList();
        var selected = normalPool.Take(Math.Max(0, totalCount - magicCount)).ToList();
        selected.AddRange(magicPool.Take(magicCount));
        if (completedLevel >= 10 && magicCount > 0 && _random.Next(2) == 0)
        {
            var legendary = source.Where(item => item.Rarity == ItemRarity.Legendary).OrderBy(_ => _random.Next()).FirstOrDefault();
            var replaceIndex = selected.FindIndex(item => item.Rarity == ItemRarity.Magic);
            if (legendary is not null && replaceIndex >= 0) selected[replaceIndex] = legendary;
        }
        return selected.Select(item => new InnStockOffer(item,
                Math.Max(1, (int)Math.Round(item.BasePrice * _random.Next(90, 151) / 100.0))))
            .OrderBy(offer => offer.Price).ToList();
    }

    private List<InnStockOffer> CreateWanderingMageStock()
    {
        var stock = new List<InnStockOffer>();
        AddRandomMagicItemOffer(stock, MagicItemKind.Wand);
        AddRandomMagicItemOffer(stock, MagicItemKind.Scroll);
        return stock;
    }

    private void AddRandomMagicItemOffer(ICollection<InnStockOffer> stock, MagicItemKind kind)
    {
        var candidates = _gameData.MagicItems.Where(item => item.Kind == kind &&
            !SpellcastingRules.IsRestrictedFromTradingAndGeneration(item)).ToList();
        if (candidates.Count == 0) return;
        var item = candidates[_random.Next(candidates.Count)];
        var price = Math.Max(1, (int)Math.Round(item.BasePrice * _random.Next(90, 151) / 100.0));
        stock.Add(new InnStockOffer(item, price));
    }

    private void RunWanderingMage(List<InnStockOffer> stock)
    {
        var options = new List<(string Label, string Description)>
        {
            ($"{ConsoleRenderer.WandIcon} Kiürült varázspálcák feltöltése", "Teljes feltöltés a pálca eredeti árának kétharmadáért."),
            ("📜 Varázsportékák", "Egy véletlen varázspálca és egy véletlen tekercs, egyszeri készletről."),
            ("🔮 Varázstárgy azonosítása", "Az azonosítás szolgáltatása hamarosan elérhető lesz."),
            ("🚪 Vissza", "Visszatérés a fogadó főtermébe.")
        };
        var selectedIndex = 0;
        var message = "A vándormágus köpenye alól halk, kékes fény szűrődik ki.";
        while (true)
        {
            _renderer.DrawWanderingMageMenu(_selectedCharacter, options, selectedIndex, message);
            var key = _readKey().Key;
            if (key == ConsoleKey.Escape) return;
            if (key == ConsoleKey.UpArrow) selectedIndex = (selectedIndex - 1 + options.Count) % options.Count;
            else if (key == ConsoleKey.DownArrow) selectedIndex = (selectedIndex + 1) % options.Count;
            else if (key == ConsoleKey.Enter)
            {
                switch (selectedIndex)
                {
                    case 0: RunWandRecharging(); break;
                    case 1: RunSpecialistMarket("🧙 VÁNDORMÁGUS PORTÉKÁI", stock); break;
                    case 2: message = "🔮 A varázstárgy-azonosítás még nem használható; a szolgáltatás helye már elő van készítve."; break;
                    case 3: return;
                }
            }
        }
    }

    private void RunWandRecharging()
    {
        var selectedIndex = 0;
        var message = "Csak a teljesen kiürült varázspálcák tölthetők fel.";
        while (true)
        {
            var wands = EmptyWands().ToList();
            selectedIndex = wands.Count == 0 ? 0 : Math.Clamp(selectedIndex, 0, wands.Count - 1);
            _renderer.DrawWandRechargeScreen(_selectedCharacter, wands.Select(wand =>
                (wand.Character.Name, wand.Item.Name, WandRechargePrice(wand.Item), wand.Item.MaximumCharges)).ToList(), selectedIndex, message);
            var key = _readKey().Key;
            if (key == ConsoleKey.Escape) return;
            if (key == ConsoleKey.UpArrow && wands.Count > 0) selectedIndex = (selectedIndex - 1 + wands.Count) % wands.Count;
            else if (key == ConsoleKey.DownArrow && wands.Count > 0) selectedIndex = (selectedIndex + 1) % wands.Count;
            else if (key == ConsoleKey.Enter && wands.Count > 0)
            {
                var wand = wands[selectedIndex];
                var price = WandRechargePrice(wand.Item);
                if (!_selectedCharacter.SpendGold(price))
                {
                    message = $"{ConsoleRenderer.MoneyIcon} Nincs elég aranyad: még {price - _selectedCharacter.Gold} hiányzik.";
                    continue;
                }
                wand.Character.ApplyInventoryChanges(new InventorySlotChange(wand.Kind, wand.Index, wand.Item, wand.Item.MaximumCharges));
                message = $"✅ {wand.Character.Name} {wand.Item.Name} pálcája feltöltve: {wand.Item.MaximumCharges}/{wand.Item.MaximumCharges} töltet ({price} arany).";
            }
        }
    }

    private IEnumerable<RechargeableWand> EmptyWands()
    {
        foreach (var character in _characterRoster.Party.Members)
        {
            for (var index = 0; index < character.MagicItems.Count; index++)
                if (character.MagicItems[index] is { Kind: MagicItemKind.Wand } equipped && character.MagicItemCharges[index] == 0)
                    yield return new RechargeableWand(character, InventorySlotKind.MagicItem, index, equipped);
            for (var index = 0; index < character.Backpack.Count; index++)
                if (character.Backpack[index] is MagicItemDefinition { Kind: MagicItemKind.Wand } packed &&
                    character.GetInventoryItemCharges(InventorySlotKind.Backpack, index) == 0)
                    yield return new RechargeableWand(character, InventorySlotKind.Backpack, index, packed);
        }
    }

    private static int WandRechargePrice(MagicItemDefinition item) => Math.Max(1, (int)Math.Ceiling(item.BasePrice * 2 / 3.0));

    private static string HungarianList(IReadOnlyList<string> items) => items.Count switch
    {
        0 => string.Empty,
        1 => items[0],
        2 => $"{items[0]} és {items[1]}",
        _ => $"{string.Join(", ", items.Take(items.Count - 1))} és {items[^1]}"
    };

    private void RunSpecialistMarket(string title, List<InnStockOffer> stock)
    {
        var selectedIndex = 0;
        var message = "A mester készlete és egyedi árai erre a fogadólátogatásra rögzítve vannak.";
        var redraw = true;
        while (true)
        {
            selectedIndex = stock.Count == 0 ? 0 : Math.Clamp(selectedIndex, 0, stock.Count - 1);
            if (redraw)
            {
                _renderer.DrawInnSpecialistScreen(title, _selectedCharacter, stock, selectedIndex,
                    _characterRoster.Party.Members.Sum(character => character.Backpack.Count(item => item is null)), message);
                redraw = false;
            }
            var key = _readKey().Key;
            if (key == ConsoleKey.Escape) return;
            if (key is ConsoleKey.UpArrow or ConsoleKey.DownArrow && stock.Count > 0)
            {
                var previousIndex = selectedIndex;
                selectedIndex = key == ConsoleKey.UpArrow
                    ? (selectedIndex - 1 + stock.Count) % stock.Count
                    : (selectedIndex + 1) % stock.Count;
                _renderer.UpdateInnBuyOnlySelection(stock, previousIndex, selectedIndex);
                continue;
            }
            if (key != ConsoleKey.Enter || stock.Count == 0) continue;
            selectedIndex = Math.Clamp(selectedIndex, 0, stock.Count - 1);
            var offer = stock[selectedIndex];
            var recipient = _characterRoster.Party.Members.FirstOrDefault(character => character.Backpack.Any(item => item is null));
            if (recipient is null) { message = "🎒 A parti összes hátizsákja tele van."; redraw = true; continue; }
            if (!_selectedCharacter.SpendGold(offer.Price)) { message = $"{ConsoleRenderer.MoneyIcon} Nincs elég aranyad: még {offer.Price - _selectedCharacter.Gold} hiányzik."; redraw = true; continue; }
            recipient.AddToBackpack(offer.Item);
            stock.RemoveAt(selectedIndex);
            _revision++;
            message = $"✅ Megvetted: {offer.Item.Name} → {recipient.Name} hátizsákja ({offer.Price} arany).";
            RecordTransaction(InnTransactionKind.Purchase, _selectedCharacter.Name, offer.Item.Name,
                offer.Price, recipient.Name);
            redraw = true;
        }
    }

    private void RecordTransaction(InnTransactionKind kind, string actorName, string itemName, int price,
        string inventoryOwnerName, bool announceOnHost = false)
    {
        var transaction = new InnTransactionSnapshot(++_transactionSequence, kind, actorName, itemName, price,
            inventoryOwnerName);
        _transactions.Enqueue(transaction);
        while (_transactions.Count > 8) _transactions.Dequeue();
        if (announceOnHost)
            _renderer.DrawInventoryMessage(FormatTransaction(transaction), ConsoleColor.Yellow);
    }

    private static string FormatTransaction(InnTransactionSnapshot transaction) => transaction.Kind switch
    {
        InnTransactionKind.Purchase => $"🏰 {transaction.ActorName} megvette: {transaction.ItemName} " +
                                       $"({transaction.Price} arany) → {transaction.InventoryOwnerName}",
        InnTransactionKind.Sale => $"🏰 {transaction.ActorName} eladta: {transaction.ItemName} " +
                                   $"({transaction.Price} arany) ← {transaction.InventoryOwnerName}",
        _ => $"🏰 {transaction.ActorName}: {transaction.ItemName}"
    };

    private enum InnMenuOption { Rest, Market, Witcher, SecretStash, Blacksmith, Armorer, WanderingMage, Recruit, Rumors, Leave }

    private sealed record RechargeableWand(LiveCharacter Character, InventorySlotKind Kind, int Index, MagicItemDefinition Item);

    private sealed record LevelCompletionOutcome(IReadOnlyList<LevelCompletionResult> Results,
        IReadOnlyList<LiveCharacter> FallenCharacters);
}
