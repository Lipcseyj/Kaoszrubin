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
}