using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Application;

internal sealed partial class InnController
{
    internal const int ForestRestCooldownMinutes = 16 * 60;
    private readonly Func<GameTimeSnapshot> _gameTimeState;
    private readonly Action<bool> _reportFeast;
    private long? _localLastInnRestCompletedMinutes;
    private long? _localLastForestFeastDay;
    private int _roomPricePerPerson;

    internal static int RoomPricePerPerson(int level, bool forestInn, Random random)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        var basePrice = Math.Max(1L, (level * (long)level + 3) / 4);
        if (forestInn) basePrice = Math.Max(12, basePrice * 3);
        return Math.Max(1, checked((int)Math.Round(basePrice * random.Next(80, 121) / 100.0)));
    }

    private int LodgingPersonCount() => _characterRoster.Party.Members.Concat(_temporaryFollowers())
        .Where(character => character.IsAlive).DistinctBy(character => character.Id).Count();

    private string? RestUnavailableReason()
    {
        if (!_forestStop)
            return _hasRestedAtInn ? "A parti már kipihente magát ebben a fogadóban; tovább kell indulni." : null;
        var time = _gameTimeState();
        if (time.LastInnRestCompletedMinutes is not { } last) return null;
        var remaining = ForestRestCooldownMinutes - (time.TotalMinutes - last);
        return remaining > 0
            ? $"A parti még nem fáradt. A következő fogadói pihenésig {remaining / 60} óra {remaining % 60} perc van hátra."
            : null;
    }

    private string? FeastUnavailableReason()
    {
        if (!_forestStop) return null;
        var time = _gameTimeState();
        return time.LastForestFeastDay == time.Day
            ? "A parti ma már lakomázott erdei fogadóban. Legközelebb a következő napon lehet."
            : null;
    }

    private InnMenuOptionSnapshot RestMenuOption()
    {
        var total = checked(_roomPricePerPerson * LodgingPersonCount());
        var availability = RestUnavailableReason() ?? (_forestStop
            ? "Az utolsó fogadói pihenés végétől legalább 16 órának kell eltelnie."
            : "Ebben a fogadóban egyszer lehet pihenni.");
        return new(InnMenuOptionKind.Rest, $"🛏️ Pihenés ({_roomPricePerPerson} {ConsoleRenderer.MoneyIcon}/fő)",
            $"8 óra pihenés és varázslatmemorizálás; szobadíj összesen {total} arany. {availability}", LeaderOnly: true);
    }

    private InnMenuOptionSnapshot FeastMenuOption() => new(InnMenuOptionKind.Feast,
        $"🍽️ Lakomázás ({_feastPrice} {ConsoleRenderer.MoneyIcon}/fő)",
        "1 óra; élelem és víz feltöltése a parti és követői számára." +
        (_forestStop ? " " + (FeastUnavailableReason() ?? "Erdei fogadóban naptári naponként egyszer.") : ""),
        LeaderOnly: true);

    private void RefreshInnServiceMenu() => _menuOptions = _menuOptions.Select(option => option.Kind switch
    {
        InnMenuOptionKind.Rest => RestMenuOption(),
        InnMenuOptionKind.Feast => FeastMenuOption(),
        _ => option
    }).ToArray();

    private void RestPartyAtInn()
    {
        if (RestUnavailableReason() is { } reason)
        {
            _renderer.DrawInnRestUnavailableScreen(reason);
            return;
        }
        var count = LodgingPersonCount();
        var total = checked(_roomPricePerPerson * count);
        if (_partyLeader.Gold < total)
        {
            _renderer.DrawDeveloperMessage($"A szobákhoz még {total - _partyLeader.Gold} arany hiányzik.");
            return;
        }
        var revision = _revision;
        _runHostWindow("Fogadói szobafoglalás", "A vezető a parti szobáit foglalja le…", () =>
        {
            if (!_renderer.ConfirmInnRest(_roomPricePerPerson, count, total)) return;
            if (!TryRestAtInn(revision, out var message)) _renderer.DrawDeveloperMessage(message);
        });
    }

    internal bool TryRestAtInn(long expectedRevision, out string message)
    {
        if (!_active) { message = "A parti jelenleg nincs a fogadóban."; return false; }
        if (expectedRevision != _revision) { message = "A fogadói ajánlat megváltozott; válassz újra."; return false; }
        if (RestUnavailableReason() is { } reason) { message = reason; return false; }
        var count = LodgingPersonCount();
        if (count == 0) { message = "Nincs élő személy a partihoz."; return false; }
        var total = checked(_roomPricePerPerson * count);
        if (!_partyLeader.SpendGold(total))
        { message = $"A szobákhoz még {total - _partyLeader.Gold} arany hiányzik."; return false; }

        var restEndsAt = checked(_gameTimeMinutes() + GameTimeClock.RestHours * 60);
        var summaries = new List<CharacterRestSnapshot>();
        foreach (var character in _characterRoster.Party.Members.Where(character => character.IsAlive))
        {
            var beforeVitality = character.CurrentVitality;
            var beforeMana = character.CurrentMana;
            character.RestoreVitality(_random.Next(20, 41));
            character.SetCurrentResources(character.CurrentVitality, character.MaximumMana);
            character.ClearTemporarySpellEffects();
            summaries.Add(new CharacterRestSnapshot(character.Id, character.Name, character.Color,
                character.CurrentVitality - beforeVitality, character.CurrentMana - beforeMana,
                character.CurrentVitality, character.MaximumVitality, character.CurrentMana, character.MaximumMana,
                character.UsesMana, []));
        }
        var cookedMeatCount = RestProvisionService.CookRawMeat(_characterRoster.Party.Members,
            _gameData.GetItem(MiscItemIds.CookedMeat));
        _hasRestedAtInn = true;
        _localLastInnRestCompletedMinutes = restEndsAt;
        _revision++;
        message = $"Szobadíj: {total} arany ({count} fő).";
        var provisionMessage = RestProvisionService.CookingMessage(cookedMeatCount);
        _reportRest(new PartyRestSnapshot(Guid.NewGuid(), true, summaries, [],
            string.IsNullOrEmpty(provisionMessage) ? message : message + " " + provisionMessage));
        RefreshInnServiceMenu();
        RecordTransaction(InnTransactionKind.Service, _partyLeader.Name, "Fogadói pihenés", total, _partyLeader.Name);
        _playGlobalSound(SoundEffect.Rest);
        _preparePartySpells();
        return true;
    }

    private void RunInnFeast()
    {
        if (FeastUnavailableReason() is { } reason)
        {
            _renderer.DrawDeveloperMessage(reason);
            return;
        }
        var personCount = FeastParticipants().Count;
        var total = checked(_feastPrice * personCount);
        if (_partyLeader.Gold < total)
        {
            _renderer.DrawDeveloperMessage($"A lakomához még {total - _partyLeader.Gold} arany hiányzik.");
            return;
        }
        var revision = _revision;
        _runHostWindow("Fogadói lakomázás", "A vezető a fogadói lakomázást intézi…", () =>
        {
            if (!_renderer.ConfirmInnFeast(_feastPrice, personCount, total)) return;
            if (!TryFeastAtInn(revision, out var message))
            {
                _renderer.DrawDeveloperMessage(message);
                return;
            }
            _renderer.RefreshCharacterSheet(_partyLeader);
            _renderer.DrawFeastWindow(_characterRoster.Party.Members.Select(member => member.Name).ToList(), total);
        });
    }

    private List<LiveCharacter> FeastParticipants() => _characterRoster.Party.Members.Concat(_temporaryFollowers())
        .DistinctBy(character => character.Id).ToList();

    internal bool TryFeastAtInn(long expectedRevision, out string message)
    {
        if (!_active) { message = "A parti jelenleg nincs a fogadóban."; return false; }
        if (expectedRevision != _revision) { message = "A fogadói ajánlat megváltozott; válassz újra."; return false; }
        if (FeastUnavailableReason() is { } reason) { message = reason; return false; }
        var participants = FeastParticipants();
        if (participants.Count == 0) { message = "Nincsenek személyek a partihoz."; return false; }
        var total = checked(_feastPrice * participants.Count);
        if (!_partyLeader.SpendGold(total))
        { message = $"A lakomához még {total - _partyLeader.Gold} arany hiányzik."; return false; }
        foreach (var character in participants)
        {
            character.RestoreFood(100);
            character.RestoreWater(100);
            character.SynchronizeNeedStatuses(_gameData.GetStatus(CharacterStatusIds.Hungry),
                _gameData.GetStatus(CharacterStatusIds.Thirsty));
        }
        if (_forestStop) _localLastForestFeastDay = _gameTimeState().Day;
        _revision++;
        _reportFeast(_forestStop);
        RefreshInnServiceMenu();
        RecordTransaction(InnTransactionKind.Service, _partyLeader.Name, "Lakomázás", total,
            _partyLeader.Name, announceOnHost: true);
        message = $"A lakoma {total} aranyba került, és egy órán át tartott.";
        return true;
    }
}
