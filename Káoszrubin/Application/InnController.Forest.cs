using KaoszRubin.Data;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.World;

namespace KaoszRubin.Application;

internal sealed partial class InnController
{
    internal const int ForestRestockMinutes = 2 * 60;
    internal const int ForestVisitorMinutes = 4 * 60;
    internal const int ForestRecruitMinutes = 6 * 60;
    private ForestInn? _forestInn;
    private List<InnStockOffer>? _forestSecretStash;
    private List<InnStockOffer>? _forestSecretStashTargets;
    private int _forestSecretStashCursor;
    private readonly Dictionary<InnVendorKind, List<InnStockOffer>> _forestStockTargets = [];
    private readonly Dictionary<InnVendorKind, int> _forestRestockCursors = [];
    private static readonly InnVendorKind[] ForestTravelers =
        [InnVendorKind.Blacksmith, InnVendorKind.Armorer, InnVendorKind.WanderingMage, InnVendorKind.Bowyer];

    public void RunForestStop(int level, ForestInn inn, Action? onReady = null)
    {
        // Egy visszatérő expedíció fogadóját az erdei kitérő nem írhatja felül.
        var prior = !_forestStop && !string.IsNullOrWhiteSpace(_innName)
            ? new SuspendedInnVisit(ExportInnState(new()), _innName, _innLevel, _hasRestedAtInn, _menuOptions, _artisanNotice)
            : null;
        try
        {
            PrepareForestStop(level, inn);
            onReady?.Invoke();
            RunMenuLoop(level);
        }
        finally
        {
            CaptureForestStop();
            _forestInn = null;
            _active = false;
            if (prior is not null)
            {
                ImportInnState(prior.State, prior.Name, prior.Level);
                _forestStop = false;
                _hasRestedAtInn = prior.HasRested;
                _menuOptions = prior.MenuOptions;
                _artisanNotice = prior.ArtisanNotice;
                _active = false;
            }
        }
    }

    internal void PrepareForestStop(int level, ForestInn inn)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        ArgumentNullException.ThrowIfNull(inn);
        CaptureForestStop();
        _forestInn = null;
        _forestStockTargets.Clear();
        _forestRestockCursors.Clear();
        inn.TryVisit();
        if (inn.Services is null)
        {
            InitializeVisit(level, null, null, inn.Name);
            var now = Math.Max(0, _gameTimeMinutes());
            foreach (var (kind, stock) in _vendorStocks)
                _forestStockTargets[kind] = stock.ToList();
            inn.Services = new()
            {
                FirstVisitMinutes = now, RestockThroughMinutes = now,
                VisitorsThroughMinutes = now, RecruitsThroughMinutes = now,
                RecruitCapacity = _normalRecruitIds.Count
            };
        }
        else ImportInnState(inn.Services, inn.Name, level);
        _forestInn = inn;
        _forestStop = true;
        _hasRestedAtInn = false;
        _levelCompletion = null;
        _active = true;
        CaptureForestStop();
        AdvanceForestInnTime();
        RebuildInnMenu();
        AddForestVisitNotice();
        _revision++;
    }

    internal void CaptureForestStop()
    {
        if (_forestInn?.Services is { } state)
            _forestInn.Services = ExportInnState(state);
    }

    private ForestInnServicesState ExportInnState(ForestInnServicesState state)
    {
        var serializer = new CharacterSaveService(string.Empty, _gameData);
        return state with
        {
            SecretStashAccessCost = _secretStashAccessCost,
            FeastPrice = _feastPrice,
            RoomPricePerPerson = _roomPricePerPerson,
            Vendors = _vendorStocks.Select(pair => new ForestInnVendorState(pair.Key,
                pair.Value.Select(SaveForestOffer).ToList(),
                (_forestStockTargets.GetValueOrDefault(pair.Key) ?? pair.Value).Select(SaveForestOffer).ToList(),
                _forestRestockCursors.GetValueOrDefault(pair.Key))).ToList(),
            BuybackPrices = new(_buybackPrices, StringComparer.OrdinalIgnoreCase),
            Recruits = (_recruitCandidates ?? []).Where(candidate => _normalRecruitIds.Contains(candidate.Id))
                .Select(candidate => new ForestInnRecruitState(serializer.SerializeCharacter(candidate),
                    _recruitmentPrices![candidate])).ToList(),
            SpecialRecruitPrices = (_recruitCandidates ?? []).Where(candidate => !_normalRecruitIds.Contains(candidate.Id))
                .ToDictionary(candidate => candidate.Id.Value, candidate => _recruitmentPrices![candidate]),
            Rumors = _rumors.ToList(),
            SecretStash = _forestSecretStash?.Select(SaveForestOffer).ToList(),
            SecretStashTarget = _forestSecretStashTargets?.Select(SaveForestOffer).ToList(),
            SecretStashRestockCursor = _forestSecretStashCursor
        };
    }

    private static ForestInnOfferState SaveForestOffer(InnStockOffer offer) =>
        new(offer.Item.Id, offer.Price, offer.Quantity, offer.StockCount);

    private void ImportInnState(ForestInnServicesState state, string name, int level)
    {
        _innName = name;
        _innLevel = level;
        _secretStashAccessCost = state.SecretStashAccessCost;
        _feastPrice = state.FeastPrice;
        _roomPricePerPerson = state.RoomPricePerPerson > 0 ? state.RoomPricePerPerson :
            RoomPricePerPerson(level, true, _random);
        _levelCompletion = null;
        _transactions.Clear();
        _pendingHostTransactionMessages.Clear();
        var items = AllGameItems().DistinctBy(item => item.Id)
            .ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        InnStockOffer RestoreOffer(ForestInnOfferState offer)
        {
            if (!items.TryGetValue(offer.ItemId, out var item) ||
                offer.Price < 1 || offer.Quantity < 1 || offer.StockCount < 1)
                throw new InvalidOperationException($"Hibás fogadói készlet: {offer.ItemId}.");
            return new(item, offer.Price, offer.Quantity, offer.StockCount);
        }
        _vendorStocks.Clear();
        _forestStockTargets.Clear();
        _forestRestockCursors.Clear();
        foreach (var vendor in state.Vendors)
        {
            _vendorStocks[vendor.Kind] = vendor.Stock.Select(RestoreOffer).ToList();
            _forestStockTargets[vendor.Kind] = vendor.TargetStock.Select(RestoreOffer).ToList();
            _forestRestockCursors[vendor.Kind] = vendor.RestockCursor;
        }
        _forestSecretStash = state.SecretStash?.Select(RestoreOffer).ToList();
        _forestSecretStashTargets = state.SecretStashTarget?.Select(RestoreOffer).ToList();
        _forestSecretStashCursor = state.SecretStashRestockCursor;
        _buybackPrices.Clear();
        foreach (var (itemId, price) in state.BuybackPrices) _buybackPrices[itemId] = price;
        _rumors.Clear();
        _rumors.AddRange(state.Rumors);
        _recruitCandidates = [];
        _recruitmentPrices = [];
        _normalRecruitIds.Clear();
        var serializer = new CharacterSaveService(string.Empty, _gameData);
        foreach (var offer in state.Recruits)
        {
            var candidate = serializer.DeserializeCharacter(offer.CharacterJson);
            if (_characterRoster.Party.Members.Any(member => member.Id == candidate.Id)) continue;
            _recruitCandidates.Add(candidate);
            _normalRecruitIds.Add(candidate.Id);
            _recruitmentPrices[candidate] = offer.Price;
        }
        AppendSpecialRecruitCandidates(state.SpecialRecruitPrices);
        _revision++;
    }

    private void AppendSpecialRecruitCandidates(IReadOnlyDictionary<Guid, int>? savedPrices = null)
    {
        // A visszaállítás csak lekérdez; nem fogyaszt új fogadólátogatást az NPC-k várakozásából.
        var candidates = savedPrices is null ? _specialRecruitCandidates() : _currentSpecialRecruitCandidates();
        foreach (var candidate in candidates.Where(candidate =>
                     !_characterRoster.Party.Members.Any(member => member.Id == candidate.Id) &&
                     !_recruitCandidates!.Any(existing => existing.Id == candidate.Id)))
        {
            _recruitCandidates!.Add(candidate);
            _recruitmentPrices![candidate] = savedPrices is not null && savedPrices.TryGetValue(candidate.Id.Value, out var price)
                ? price : RecruitmentPrice(candidate, _innLevel);
        }
    }

    internal bool AdvanceForestInnTime()
    {
        if (_forestInn?.Services is not { } state) return false;
        var now = Math.Max(state.FirstVisitMinutes, _gameTimeMinutes());
        var restocks = Math.Max(0, (now - state.RestockThroughMinutes) / ForestRestockMinutes);
        var visitors = Math.Max(0, (now - state.VisitorsThroughMinutes) / ForestVisitorMinutes);
        var recruits = Math.Max(0, (now - state.RecruitsThroughMinutes) / ForestRecruitMinutes);
        if (restocks == 0 && visitors == 0 && recruits == 0) return false;

        foreach (var (kind, stock) in _vendorStocks)
        {
            if (!_forestStockTargets.TryGetValue(kind, out var targets)) continue;
            var cursor = _forestRestockCursors.GetValueOrDefault(kind);
            ReplenishForestStock(stock, targets, restocks, ref cursor);
            _forestRestockCursors[kind] = cursor;
        }
        if (_forestSecretStash is not null && _forestSecretStashTargets is not null)
            ReplenishForestStock(_forestSecretStash, _forestSecretStashTargets, restocks,
                ref _forestSecretStashCursor);

        // Hosszú kihagyáskor a köztes vendégjárásból csak a legutóbbi állapotot kell előállítani.
        for (var index = 0L; index < Math.Min(visitors, 64); index++)
        {
            var kind = ForestTravelers[_random.Next(ForestTravelers.Length)];
            if (_vendorStocks.Remove(kind))
            {
                _forestStockTargets.Remove(kind);
                _forestRestockCursors.Remove(kind);
                continue;
            }
            var stock = kind switch
            {
                InnVendorKind.Blacksmith => CreateSpecialistStock(_innLevel, ItemCategory.Weapon),
                InnVendorKind.Armorer => CreateSpecialistStock(_innLevel, ItemCategory.Armor),
                InnVendorKind.WanderingMage => CreateWanderingMageStock(),
                _ => CreateBowyerStock(_innLevel)
            };
            if (kind is InnVendorKind.Blacksmith or InnVendorKind.Armorer)
                AddRepairKitStock(stock, _innLevel);
            _vendorStocks[kind] = stock;
            _forestStockTargets[kind] = stock.ToList();
            _forestRestockCursors[kind] = 0;
        }
        for (var index = 0L; index < Math.Min(recruits, Math.Max(1, state.RecruitCapacity)); index++)
            RotateForestRecruit(Math.Clamp(state.RecruitCapacity, 1, 3));

        _forestInn.Services = state with
        {
            RestockThroughMinutes = state.RestockThroughMinutes + restocks * ForestRestockMinutes,
            VisitorsThroughMinutes = state.VisitorsThroughMinutes + visitors * ForestVisitorMinutes,
            RecruitsThroughMinutes = state.RecruitsThroughMinutes + recruits * ForestRecruitMinutes
        };
        CaptureForestStop();
        RebuildInnMenu();
        AddForestVisitNotice();
        _revision++;
        return true;
    }

    private static void ReplenishForestStock(List<InnStockOffer> stock, List<InnStockOffer> originalTargets,
        long rounds, ref int cursor)
    {
        if (rounds <= 0 || originalTargets.Count == 0) return;
        var targets = originalTargets.GroupBy(offer => (offer.Item.Id, offer.Price, offer.Quantity))
            .Select(group => group.First() with { StockCount = group.Sum(offer => offer.StockCount) }).ToList();
        static bool SameOffer(InnStockOffer left, InnStockOffer right) =>
            left.Item.Id == right.Item.Id && left.Price == right.Price && left.Quantity == right.Quantity;
        var limit = Math.Min(rounds, targets.Sum(target => (long)target.StockCount));
        for (var round = 0L; round < limit; round++)
        {
            var found = false;
            for (var offset = 0; offset < targets.Count; offset++)
            {
                var index = (Math.Max(0, cursor) + offset) % targets.Count;
                var target = targets[index];
                if (stock.Where(offer => SameOffer(offer, target)).Sum(offer => offer.StockCount) >= target.StockCount)
                    continue;
                var current = stock.FindIndex(offer => SameOffer(offer, target));
                if (current < 0) stock.Add(target with { StockCount = 1 });
                else stock[current] = stock[current] with { StockCount = stock[current].StockCount + 1 };
                cursor = (index + 1) % targets.Count;
                found = true;
                break;
            }
            if (!found) break;
        }
        stock.Sort((left, right) => left.Price.CompareTo(right.Price));
    }

    private void RotateForestRecruit(int capacity)
    {
        var candidates = _recruitCandidates ?? throw new InvalidOperationException("Hiányzó fogadói toborzási lista.");
        var prices = _recruitmentPrices ?? throw new InvalidOperationException("Hiányzó fogadói toborzási árak.");
        var normals = candidates.Where(candidate => _normalRecruitIds.Contains(candidate.Id)).ToList();
        if (normals.Count >= capacity)
        {
            var departing = normals[_random.Next(normals.Count)];
            candidates.Remove(departing);
            _normalRecruitIds.Remove(departing.Id);
            prices.Remove(departing);
        }
        var usedClasses = candidates.Select(candidate => candidate.CharacterClass.Id).ToHashSet();
        var classes = _gameData.CharacterClasses.Where(characterClass => !usedClasses.Contains(characterClass.Id)).ToList();
        if (classes.Count == 0) classes = _gameData.CharacterClasses.ToList();
        var chosenClass = classes[_random.Next(classes.Count)];
        var usedNames = _characterRoster.Characters.Select(character => character.Name)
            .Concat(candidates.Select(character => character.Name)).ToList();
        var generator = new RandomCharacterGenerator(_gameData, _random);
        var candidate = _characterRoster.Party.AvailableRecruitmentGrants.Count > 0
            ? generator.GenerateSupportedMercenary(chosenClass, _partyLeader.Level, usedNames)
            : generator.GenerateMercenary(chosenClass, _partyLeader.Level, usedNames, _innLevel);
        candidates.Add(candidate);
        _normalRecruitIds.Add(candidate.Id);
        prices[candidate] = RecruitmentPrice(candidate, _innLevel);
    }

    private List<InnStockOffer> SecretStashStock(int completedLevel)
    {
        if (_forestStop && _forestSecretStash is not null) return _forestSecretStash;
        var secretLevel = completedLevel + SecretStashLevelAdvance;
        var stock = CreateMerchantStock(completedLevel, secretLevel, _random.Next(105, 121) / 100.0,
            includePremiumStock: false, includeRandomLegendary: false, includePremiumSupplies: true).ToList();
        AddSecretStashSpecialOffer(stock, completedLevel, secretLevel);
        stock.Sort((left, right) => left.Price.CompareTo(right.Price));
        if (_forestStop)
        {
            _forestSecretStash = stock;
            _forestSecretStashTargets = stock.ToList();
            _forestSecretStashCursor = 0;
            CaptureForestStop();
        }
        return stock;
    }

    private void AddForestVisitNotice()
    {
        if (_forestInn?.FirstVisitMinutes is { } first)
            _artisanNotice = $"Első betérés: {new GameTimeSnapshot(first).DisplayText}. " + _artisanNotice;
    }

    private sealed record SuspendedInnVisit(ForestInnServicesState State, string Name, int Level,
        bool HasRested, IReadOnlyList<InnMenuOptionSnapshot> MenuOptions, string ArtisanNotice);
}
