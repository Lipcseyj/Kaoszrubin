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

            _ => throw new InvalidDataException(
                $"Ismeretlen legacy quest-azonosító: '{legacyId}'.")
        };
    }
}