using KaoszRubin.Data;
using KaoszRubin.Domain.Characters;

namespace KaoszRubin.Application;

internal sealed partial class InnController
{
    /// <summary>Csak új fogadólátogatáskor hívjuk; menüváltás vagy expedíciós visszatérés nem sorsol újra.</summary>
    internal void InitializeRecruitment(int completedLevel)
    {
        _innLevel = completedLevel;
        _recruitCandidates = [];
        _recruitmentPrices = [];
        _normalRecruitIds.Clear();
        var supported = _characterRoster.Party.AvailableRecruitmentGrants.Count > 0;
        var count = supported ? 3 : _random.Next(1, 4);
        var classes = _gameData.CharacterClasses.OrderBy(_ => _random.Next()).ToList();
        if (supported)
        {
            var existing = _characterRoster.Party.Members.Select(member => member.CharacterClass.Id).ToHashSet();
            var missing = classes.FirstOrDefault(characterClass => !existing.Contains(characterClass.Id));
            if (missing is not null) { classes.Remove(missing); classes.Insert(0, missing); }
        }
        var generator = new RandomCharacterGenerator(_gameData, _random);
        var usedNames = _characterRoster.Characters.Select(character => character.Name).ToList();
        foreach (var characterClass in classes.Take(count))
        {
            var candidate = supported
                ? generator.GenerateSupportedMercenary(characterClass, _partyLeader.Level, usedNames)
                : generator.GenerateMercenary(characterClass, _partyLeader.Level, usedNames, completedLevel);
            _recruitCandidates.Add(candidate);
            _normalRecruitIds.Add(candidate.Id);
            usedNames.Add(candidate.Name);
            _recruitmentPrices[candidate] = RecruitmentPrice(candidate, completedLevel);
        }
        foreach (var candidate in _specialRecruitCandidates().Where(candidate =>
                     !_characterRoster.Party.Members.Any(member => member.Id == candidate.Id) &&
                     !_recruitCandidates.Any(existing => existing.Id == candidate.Id)))
        {
            _recruitCandidates.Add(candidate);
            _recruitmentPrices[candidate] = RecruitmentPrice(candidate, completedLevel);
        }
        _revision++;
    }

    private PartyExpansionMilestone? RecruitmentGrantFor(LiveCharacter candidate) =>
        !_characterRoster.Party.IsFull && _normalRecruitIds.Contains(candidate.Id) && candidate.SourceNpcDefinitionId is null
            ? _characterRoster.Party.AvailableRecruitmentGrants.Select(value => (PartyExpansionMilestone?)value).FirstOrDefault()
            : null;

    internal IReadOnlyList<InnRecruitSnapshot> RecruitmentOffers() => (_recruitCandidates ?? [])
        .Select(candidate => new InnRecruitSnapshot(candidate.Id, candidate.Name, candidate.CharacterClass.Name,
            candidate.Level, candidate.Color, RecruitmentGrantFor(candidate) is not null ? 0 : _recruitmentPrices![candidate],
            RecruitmentGrantFor(candidate))).ToArray();

    private InnMenuOptionSnapshot RecruitmentMenuOption() => new(InnMenuOptionKind.Recruit,
        _characterRoster.Party.AvailableRecruitmentGrants.Count > 0
            ? "⚔️ Zsoldosok toborzása — támogatással" : "⚔️ Zsoldosok toborzása",
        RecruitmentStatus(), LeaderOnly: true);

    private string RecruitmentStatus() => _characterRoster.Party.AvailableRecruitmentGrants.Count == 0
        ? "Új partitagok felfogadása a meglévő árakon."
        : _characterRoster.Party.IsFull
            ? "A parti tele van. A támogatás megmarad; társcsere a normál felvételi áron lehetséges."
            : "Egy normál zsoldos toborzási támogatással ingyen felvehető. Esc: halasztás, a támogatás megmarad.";

    /// <summary>A host egy kijelölt fogadói ajánlatát hajtja végre; elavult kérés nem költ támogatást vagy aranyat.</summary>
    internal bool TryRecruit(CharacterId candidateId, long expectedRevision, CharacterId? replacedId, out string message)
    {
        if (!_active) { message = "A parti jelenleg nincs a fogadóban."; return false; }
        if (_revision != expectedRevision) { message = "A fogadói ajánlat időközben megváltozott; válassz újra."; return false; }
        var recruit = _recruitCandidates?.FirstOrDefault(candidate => candidate.Id == candidateId);
        if (recruit is null || _recruitmentPrices is null)
        { message = "Ez a jelölt már nem érhető el."; return false; }
        if (_characterRoster.Party.Members.Any(member => member.Id == recruit.Id))
        { message = "Ez a karakter már a parti tagja."; return false; }
        var grant = RecruitmentGrantFor(recruit);
        var price = grant is not null ? 0 : _recruitmentPrices[recruit];
        var replaced = replacedId is { } id
            ? _characterRoster.Party.Members.Skip(1).FirstOrDefault(member => member.Id == id) : null;
        if (replacedId is not null && (replaced is null || !_characterRoster.Party.IsFull))
        { message = "A lecserélendő társ vagy a parti időközben megváltozott."; return false; }
        if (_partyLeader.Gold < price)
        { message = $"Nincs elég közös arany: még {price - _partyLeader.Gold} hiányzik."; return false; }
        var joined = grant is { } milestone
            ? _characterRoster.Party.TryAddWithRecruitmentGrant(recruit, milestone)
            : replaced is null ? _characterRoster.Party.Add(recruit)
                : _characterRoster.Party.TryReplaceCompanion(replaced, recruit);
        if (!joined) { message = "A parti megtelt vagy időközben megváltozott; a támogatás és az arany megmaradt."; return false; }
        if (replaced is not null) _characterRoster.Remove(replaced);
        _partyLeader.SpendGold(price);
        if (!_characterRoster.Characters.Any(member => member.Id == recruit.Id)) _characterRoster.Add(recruit);
        recruit.SetNpcJoinOrigin(_innLevel, _innName);
        _specialRecruitAccepted(recruit);
        _recruitCandidates!.Remove(recruit);
        _recruitmentPrices.Remove(recruit);
        _normalRecruitIds.Remove(recruit.Id);
        _revision++;
        _menuOptions = _menuOptions.Select(option => option.Kind == InnMenuOptionKind.Recruit ? RecruitmentMenuOption() : option).ToArray();
        message = replaced is null
            ? $"✅ {recruit.Name} csatlakozott a partihoz{(grant is not null ? " toborzási támogatással ingyen" : FormatRecruitmentPricePaid(price))}."
            : $"✅ {recruit.Name} átvette {replaced.Name} helyét{FormatRecruitmentPricePaid(price)}; a régi társ végleg távozott.";
        RecordTransaction(InnTransactionKind.Recruitment, _partyLeader.Name,
            grant is not null ? $"{recruit.Name} — toborzási támogatással" : recruit.Name, price, recruit.Name);
        return true;
    }
}