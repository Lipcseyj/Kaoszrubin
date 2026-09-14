using KaoszRubin.Domain.Quests;

namespace KaoszRubin.Infrastructure.Quests;

/// <summary>
/// A legacy adatfájl NPC-azonosítóit fordítja át
/// az új, erősen tipizált quest NPC-azonosítókra.
///
/// String NPC-ID ezen a rétegen túl nem kerülhet.
/// </summary>
public static class LegacyNpcIdMap
{
    public static QuestNpcId ToQuestNpcId(string legacyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyId);

        return legacyId.ToUpperInvariant() switch
        {
            "NPC001" => QuestNpcId.WanderingHerbalist,
            "NPC002" => QuestNpcId.MonsterHunter,
            "NPC003" => QuestNpcId.TreasureHunter,
            "NPC004" => QuestNpcId.WanderingBard,
            "NPC005" => QuestNpcId.RatHunter,
            "NPC006" => QuestNpcId.KoboldFugitive,
            "NPC007" => QuestNpcId.WoundedBorderGuard,
            "NPC008" => QuestNpcId.SpiritSeerHermit,
            "NPC009" => QuestNpcId.OrcDeserter,
            "NPC010" => QuestNpcId.CaveAlchemist,
            "NPC011" => QuestNpcId.PrisonerScout,
            "NPC012" => QuestNpcId.GraveKeeper,
            "NPC013" => QuestNpcId.DragonResearcher,
            "NPC014" => QuestNpcId.SwampFerryman,
            "NPC015" => QuestNpcId.CrystalEngineer,
            "NPC016" => QuestNpcId.NightRefugee,
            "NPC017" => QuestNpcId.UnchainedDemonHunter,
            "NPC018" => QuestNpcId.ChaosPilgrim,
            "NPC019" => QuestNpcId.GiantHunter,
            "NPC020" => QuestNpcId.EliraSilverbranch,
            "NPC021" => QuestNpcId.SirRoderic,

            _ => throw new InvalidDataException(
                $"Ismeretlen legacy quest NPC-azonosító: '{legacyId}'.")
        };
    }
    public static string ToExternalId(QuestNpcId id) => id switch
    {
        QuestNpcId.WanderingHerbalist => "NPC001",
        QuestNpcId.MonsterHunter => "NPC002",
        QuestNpcId.TreasureHunter => "NPC003",
        QuestNpcId.WanderingBard => "NPC004",
        QuestNpcId.RatHunter => "NPC005",
        QuestNpcId.KoboldFugitive => "NPC006",
        QuestNpcId.WoundedBorderGuard => "NPC007",
        QuestNpcId.SpiritSeerHermit => "NPC008",
        QuestNpcId.OrcDeserter => "NPC009",
        QuestNpcId.CaveAlchemist => "NPC010",
        QuestNpcId.PrisonerScout => "NPC011",
        QuestNpcId.GraveKeeper => "NPC012",
        QuestNpcId.DragonResearcher => "NPC013",
        QuestNpcId.SwampFerryman => "NPC014",
        QuestNpcId.CrystalEngineer => "NPC015",
        QuestNpcId.NightRefugee => "NPC016",
        QuestNpcId.UnchainedDemonHunter => "NPC017",
        QuestNpcId.ChaosPilgrim => "NPC018",
        QuestNpcId.GiantHunter => "NPC019",
        QuestNpcId.EliraSilverbranch => "NPC020",
        QuestNpcId.SirRoderic => "NPC021",
        _ => throw new InvalidDataException("Ismeretlen típusos questadó-azonosító.")
    };
}