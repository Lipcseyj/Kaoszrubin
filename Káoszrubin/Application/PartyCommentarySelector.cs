using KaoszRubin.Data;
using KaoszRubin.Domain;
using KaoszRubin.Domain.Characters;

namespace KaoszRubin.Application;

public sealed record PartyCommentSelection(LiveCharacter Speaker, PartyRemarkDefinition Remark);

public static class PartyCommentarySelector
{
    private const string DwarfRaceId = "R002";

    public static string Format(LiveCharacter speaker, string comment, string? level = null) =>
        $"[{speaker.Name}]" + (level is null ? " " : $"({level}) ") + comment;

    public static bool ShouldComment(int percentageRoll) => percentageRoll is >= 0 and < 40;

    public static int SpeakerCount(int availableSpeakers, int distributionRoll) => availableSpeakers <= 0 ? 0 :
        Math.Min(availableSpeakers, distributionRoll switch
        {
            < 60 => 1,
            < 80 => 2,
            _ => 3
        });

    public static int SpeakerWeight(string situationId, LiveCharacter character) =>
        !string.Equals(character.Race.Id, DwarfRaceId, StringComparison.OrdinalIgnoreCase) ? 100 :
        IsCombatSituation(situationId) ? 140 : 120;

    public static IReadOnlyList<PartyCommentSelection> Select(GameDataCatalog data, string situationId,
        IReadOnlyCollection<LiveCharacter> partyMembers, Random random)
    {
        if (!ShouldComment(random.Next(100))) return [];
        var eligible = partyMembers.Where(character => character.IsAlive &&
                data.GetPartyRemarks(situationId, character).Count > 0)
            .ToList();
        var count = SpeakerCount(eligible.Count, random.Next(100));
        var selected = new List<PartyCommentSelection>(count);
        while (selected.Count < count)
        {
            var totalWeight = eligible.Sum(character => SpeakerWeight(situationId, character));
            var roll = random.Next(totalWeight);
            var speakerIndex = 0;
            for (; speakerIndex < eligible.Count - 1; speakerIndex++)
            {
                roll -= SpeakerWeight(situationId, eligible[speakerIndex]);
                if (roll < 0) break;
            }
            var speaker = eligible[speakerIndex];
            eligible.RemoveAt(speakerIndex);
            var remarks = data.GetPartyRemarks(situationId, speaker);
            selected.Add(new PartyCommentSelection(speaker, remarks[random.Next(remarks.Count)]));
        }
        return selected;
    }

    public static PartyCommentSelection? SelectFor(GameDataCatalog data, string situationId,
        LiveCharacter speaker, Random random)
    {
        var remarks = data.GetPartyRemarks(situationId, speaker);
        return remarks.Count == 0 ? null : new PartyCommentSelection(speaker, remarks[random.Next(remarks.Count)]);
    }

    private static bool IsCombatSituation(string situationId) => situationId is
        PartySituationIds.EnemySpotted or PartySituationIds.BattleStarted or PartySituationIds.BattleWon or
        PartySituationIds.PartyMemberDied;
}
