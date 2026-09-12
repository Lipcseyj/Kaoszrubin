namespace KaoszRubin.Domain.Quests;

/// <summary>
/// A játék NPC-khez kapcsolódó küldetéseinek
/// erősen tipizált azonosítója.
/// </summary>
public enum QuestId
{
    None = 0,

    // NPC001 - Vándor füvesasszony
    HerbalistHealingSupplies,
    HerbalistCleanBandages,

    // NPC002 - Szörnyvadász
    MonsterHunterGoblinHunt,
    MonsterHunterGoblinBounty,

    // NPC003 - Kincskereső
    TreasureHunterDeepJourneySupplies,
    TreasureHunterSealedPath,

    // NPC004 - Vándor dalnok
    BardSongOfBones,
    BardRestoreTheVoice,

    // NPC005 - Patkányvadász
    RatHunterClearTheTunnels,

    // NPC006 - Kobold szökevény
    KoboldFugitiveNoWayBack,

    // NPC007 - Sebesült határőr
    BorderGuardLostPatrol,

    // NPC008 - Halottlátó remete
    SpiritSeerSilenceInTheGraves,
    SpiritSeerRestlessBodies,

    // NPC009 - Ork dezertőr
    OrcDeserterBrokenTusk,
    OrcDeserterWarlordsGuards,

    // NPC010 - Barlangi alkimista
    CaveAlchemistVenomGlands,
    CaveAlchemistReliableAntidote,

    // NPC011 - Hadifogoly felderítő
    PrisonerScoutEyesOfTheCamp,

    // NPC012 - Sírok őrzője
    GraveKeeperLostGraveMarker,
    GraveKeeperDesecratedSeals,

    // NPC013 - Sárkánykutató
    DragonResearcherScaledLocks,
    DragonResearcherPathOfAsh,

    // NPC014 - Mocsári révész
    SwampFerrymanDryPath,
    SwampFerrymanSunkenTraps,

    // NPC015 - Kristálymérnök
    CrystalEngineerCrystalLockSecret,
    CrystalEngineerFaultyMechanisms,

    // NPC016 - Éji menekült
    NightRefugeeEscapeFromEternalNight,
    NightRefugeeConfiscatedInheritance,

    // NPC017 - Láncait vesztett démonvadász
    DemonHunterBurnTheWebs,
    DemonHunterKnightsOfHell,

    // NPC018 - Káoszzarándok
    ChaosPilgrimImpossiblePath,
    ChaosPilgrimRubyEchoes,

    // NPC019 - Óriásvadász
    GiantHunterBigGame,

    // NPC020 - Elira Ezüstág
    EliraRescue,
    EliraTornBandage,
    EliraOnOurTrail,

    // NPC021 - Sir Roderic
    RodericFallenComradesInsignia,
    RodericSharedBladeTrial,
    RodericOathbreakerKnight,
    RodericTheDeadAreNotPrey
}