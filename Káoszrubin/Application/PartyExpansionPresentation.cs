using KaoszRubin.Domain.Characters;

namespace KaoszRubin.Application;

internal static class PartyExpansionPresentation
{
    public static IReadOnlyList<PartyExpansionPresentationSnapshot> Pending(Party party) =>
        party.PendingUnlockPresentations.Select(Create).ToArray();

    private static PartyExpansionPresentationSnapshot Create(PartyExpansionMilestone milestone) => milestone switch
    {
        PartyExpansionMilestone.FifthMember => new(milestone, 5, "Megerősített expedíció",
        [
            "A katakombák után Aurelios hálózata elismeri a Kulcshordozók helytállását.",
            "Új partihely! Mostantól legfeljebb 5 fővel indulhattok.",
            "Egyszeri toborzási támogatás: három különböző kasztú zsoldosból egyet ingyen felvehettek.",
            "A választás elhalasztható; a támogatás a következő fogadóban is megmarad."
        ]),
        PartyExpansionMilestone.SixthMember => new(milestone, 6, "Felkészülés a haditáborra",
        [
            "Aurelios ügynöke szervezett ork csapatokra figyelmeztet. Nagyobb kíséretre lesz szükségetek.",
            "Új partihely! A Kulcshordozók csapata mostantól 6 fős lehet.",
            "Elérhető a széles harcrend és egy újabb egyszeri toborzási támogatás."
        ]),
        _ => throw new ArgumentOutOfRangeException(nameof(milestone))
    };
}