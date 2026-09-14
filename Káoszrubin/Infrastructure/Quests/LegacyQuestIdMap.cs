using KaoszRubin.Domain.Quests;

namespace KaoszRubin.Infrastructure.Quests;

/// <summary>
/// A legacy NPCQxxx azonosítókat fordítja át
/// erősen tipizált QuestId értékekre.
///
/// String quest-ID ezen a rétegen túl nem kerülhet.
/// </summary>
public static class LegacyQuestIdMap
{
    public static QuestId ToQuestId(string legacyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyId);

        return legacyId.ToUpperInvariant() switch
        {
            "NPCQ001" => QuestId.HerbalistHealingSupplies,
            "NPCQ002" => QuestId.MonsterHunterGoblinHunt,
            "NPCQ003" => QuestId.TreasureHunterDeepJourneySupplies,
            "NPCQ004" => QuestId.BardSongOfBones,
            "NPCQ005" => QuestId.HerbalistCleanBandages,
            "NPCQ006" => QuestId.MonsterHunterGoblinBounty,
            "NPCQ007" => QuestId.TreasureHunterSealedPath,
            "NPCQ008" => QuestId.BardRestoreTheVoice,
            "NPCQ009" => QuestId.RatHunterClearTheTunnels,
            "NPCQ010" => QuestId.KoboldFugitiveNoWayBack,
            "NPCQ011" => QuestId.BorderGuardLostPatrol,
            "NPCQ012" => QuestId.SpiritSeerSilenceInTheGraves,
            "NPCQ013" => QuestId.SpiritSeerRestlessBodies,
            "NPCQ014" => QuestId.OrcDeserterBrokenTusk,
            "NPCQ015" => QuestId.OrcDeserterWarlordsGuards,
            "NPCQ016" => QuestId.CaveAlchemistVenomGlands,
            "NPCQ017" => QuestId.CaveAlchemistReliableAntidote,
            "NPCQ018" => QuestId.PrisonerScoutEyesOfTheCamp,
            "NPCQ019" => QuestId.GraveKeeperLostGraveMarker,
            "NPCQ020" => QuestId.GraveKeeperDesecratedSeals,
            "NPCQ021" => QuestId.DragonResearcherScaledLocks,
            "NPCQ022" => QuestId.DragonResearcherPathOfAsh,
            "NPCQ023" => QuestId.SwampFerrymanDryPath,
            "NPCQ024" => QuestId.SwampFerrymanSunkenTraps,
            "NPCQ025" => QuestId.CrystalEngineerCrystalLockSecret,
            "NPCQ026" => QuestId.CrystalEngineerFaultyMechanisms,
            "NPCQ027" => QuestId.NightRefugeeEscapeFromEternalNight,
            "NPCQ028" => QuestId.NightRefugeeConfiscatedInheritance,
            "NPCQ029" => QuestId.DemonHunterBurnTheWebs,
            "NPCQ030" => QuestId.DemonHunterKnightsOfHell,
            "NPCQ031" => QuestId.ChaosPilgrimImpossiblePath,
            "NPCQ032" => QuestId.ChaosPilgrimRubyEchoes,
            "NPCQ033" => QuestId.GiantHunterBigGame,
            "NPCQ034" => QuestId.EliraRescue,
            "NPCQ035" => QuestId.EliraTornBandage,
            "NPCQ036" => QuestId.EliraOnOurTrail,
            "NPCQ037" => QuestId.RodericFallenComradesInsignia,
            "NPCQ038" => QuestId.RodericSharedBladeTrial,
            "NPCQ039" => QuestId.RodericOathbreakerKnight,
            "NPCQ040" => QuestId.RodericTheDeadAreNotPrey,
            "NPCQ041" => QuestId.RodericOrderRelics,

            _ => throw new InvalidDataException(
                $"Ismeretlen legacy quest-azonosító: '{legacyId}'.")
        };
    }
    public static string ToExternalId(QuestId id) => id switch
    {
        QuestId.HerbalistHealingSupplies => "NPCQ001",
        QuestId.MonsterHunterGoblinHunt => "NPCQ002",
        QuestId.TreasureHunterDeepJourneySupplies => "NPCQ003",
        QuestId.BardSongOfBones => "NPCQ004",
        QuestId.HerbalistCleanBandages => "NPCQ005",
        QuestId.MonsterHunterGoblinBounty => "NPCQ006",
        QuestId.TreasureHunterSealedPath => "NPCQ007",
        QuestId.BardRestoreTheVoice => "NPCQ008",
        QuestId.RatHunterClearTheTunnels => "NPCQ009",
        QuestId.KoboldFugitiveNoWayBack => "NPCQ010",
        QuestId.BorderGuardLostPatrol => "NPCQ011",
        QuestId.SpiritSeerSilenceInTheGraves => "NPCQ012",
        QuestId.SpiritSeerRestlessBodies => "NPCQ013",
        QuestId.OrcDeserterBrokenTusk => "NPCQ014",
        QuestId.OrcDeserterWarlordsGuards => "NPCQ015",
        QuestId.CaveAlchemistVenomGlands => "NPCQ016",
        QuestId.CaveAlchemistReliableAntidote => "NPCQ017",
        QuestId.PrisonerScoutEyesOfTheCamp => "NPCQ018",
        QuestId.GraveKeeperLostGraveMarker => "NPCQ019",
        QuestId.GraveKeeperDesecratedSeals => "NPCQ020",
        QuestId.DragonResearcherScaledLocks => "NPCQ021",
        QuestId.DragonResearcherPathOfAsh => "NPCQ022",
        QuestId.SwampFerrymanDryPath => "NPCQ023",
        QuestId.SwampFerrymanSunkenTraps => "NPCQ024",
        QuestId.CrystalEngineerCrystalLockSecret => "NPCQ025",
        QuestId.CrystalEngineerFaultyMechanisms => "NPCQ026",
        QuestId.NightRefugeeEscapeFromEternalNight => "NPCQ027",
        QuestId.NightRefugeeConfiscatedInheritance => "NPCQ028",
        QuestId.DemonHunterBurnTheWebs => "NPCQ029",
        QuestId.DemonHunterKnightsOfHell => "NPCQ030",
        QuestId.ChaosPilgrimImpossiblePath => "NPCQ031",
        QuestId.ChaosPilgrimRubyEchoes => "NPCQ032",
        QuestId.GiantHunterBigGame => "NPCQ033",
        QuestId.EliraRescue => "NPCQ034",
        QuestId.EliraTornBandage => "NPCQ035",
        QuestId.EliraOnOurTrail => "NPCQ036",
        QuestId.RodericFallenComradesInsignia => "NPCQ037",
        QuestId.RodericSharedBladeTrial => "NPCQ038",
        QuestId.RodericOathbreakerKnight => "NPCQ039",
        QuestId.RodericTheDeadAreNotPrey => "NPCQ040",
        QuestId.RodericOrderRelics => "NPCQ041",
        _ => throw new InvalidDataException("Ismeretlen típusos questazonosító.")
    };
}
