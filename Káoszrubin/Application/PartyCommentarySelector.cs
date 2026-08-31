using KaoszRubin.Data;
using KaoszRubin.Domain;
using KaoszRubin.Domain.Characters;

namespace KaoszRubin.Application;

public sealed record PartyCommentSelection(LiveCharacter Speaker, PartyRemarkDefinition Remark);

public static class PartyCommentarySelector
{
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

    public static IReadOnlyList<PartyCommentSelection> Select(GameDataCatalog data, string situationId,
        IReadOnlyCollection<LiveCharacter> partyMembers, Random random)
    {
        if (!ShouldComment(random.Next(100))) return [];
        var eligible = partyMembers.Where(character => character.IsAlive &&
                data.GetPartyRemarks(situationId, character).Count > 0)
            .OrderBy(_ => random.Next()).ToArray();
        var count = SpeakerCount(eligible.Length, random.Next(100));
        return eligible.Take(count).Select(speaker =>
        {
            var remarks = data.GetPartyRemarks(situationId, speaker);
            return new PartyCommentSelection(speaker, remarks[random.Next(remarks.Count)]);
        }).ToArray();
    }

    public static PartyCommentSelection? SelectFor(GameDataCatalog data, string situationId,
        LiveCharacter speaker, Random random)
    {
        var remarks = data.GetPartyRemarks(situationId, speaker);
        return remarks.Count == 0 ? null : new PartyCommentSelection(speaker, remarks[random.Next(remarks.Count)]);
    }
}
