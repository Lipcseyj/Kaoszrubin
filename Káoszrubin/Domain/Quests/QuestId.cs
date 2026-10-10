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
    // A megőrzött azonosító ma a pátriárkák feladatát jelöli.
    RodericSharedBladeTrial,
    RodericOathbreakerKnight,
    // A megőrzött azonosító ma a nyolc élőholt elleni bizonyítást jelöli.
    RodericTheDeadAreNotPrey,
    RodericOrderRelics,
    
    // NPC022 - Aurelios küldötte
    AureliosEmissaryChest,

    // NPC002 - Szörnyvadász
    GoblinChiefHunt,

    // NPC001 - Vándor füvesasszony
    VillagerMeat,

    // NPC002 - Szörnyvadász
    MonsterHunterOrcTrail,

    // NPC007 - Elf kósza
    Az_erdő_vasfogai,

    // NPC007 - Elf kósza
    Halál_a_zöldbőrűekre,
    // NPC002 - Szörnyvadász
    Prémvadászat,
    // NPC002 - Szörnyvadász
    Sámánorr_gyűjtés,
    // NPC024 - Renegát orgyilkos
    Csökkentsük_a_konkurenciát,
    // NPC025 - Inkvizítor
    Tisztítótűz,
    // NPC014 - Mocsári révész
    Biztonságos_ösvény,
    // NPC025 - Inkvizítor
    A_káosz_szolgái,
    RavensStolenSupplies,
    OrcTribeSupplyCache,
    SluiceRoyalProvisions,
    SunkenCourtProvisions,

    // A 7–23. pálya új, találkozáshoz kötött feladatai. Új ID csak a lista végére kerülhet.
    MonsterHunterHallOgres, // NPCQ058
    RuneBreakerBlindingSeals, // NPCQ059
    RuneBreakerHallAcolytes, // NPCQ060
    HerbalistCavePlague, // NPCQ061
    HerbalistCaveDressings, // NPCQ062
    OrcDeserterBloodPriests, // NPCQ063
    OrcDeserterArcherScreen, // NPCQ064
    PrisonerScoutCampExit, // NPCQ065
    SpiritSeerTombRestlessDead, // NPCQ066
    InquisitorTombChaosPriests, // NPCQ067
    GiantHunterTwoHeads, // NPCQ068
    SurveyorFortressExit, // NPCQ069
    SurveyorForkedLightning, // NPCQ070
    CultistTemplePriests, // NPCQ071
    CultistTempleFireSeals, // NPCQ072
    PrisonerScoutTempleMarauders, // NPCQ073
    RangerMarshRaiders, // NPCQ074
    RangerMarshDruids, // NPCQ075
    ArchivistMarshRegisters, // NPCQ076
    FerrymanMarshCrocodiles, // NPCQ077
    FerrymanMarshPriests, // NPCQ078
    AlchemistMarshVipers, // NPCQ079
    AlchemistMarshMedicine, // NPCQ080
    RuneBreakerNightmareSeals, // NPCQ081
    SurveyorDeepPassageExit, // NPCQ082
    RenegadeDeepAssassins, // NPCQ083
    RenegadeDeepAcolytes, // NPCQ084
    CrystalEngineerGolemWardens, // NPCQ085
    CrystalEngineerStormCircuits, // NPCQ086
    TreasureHunterCrystalCaches, // NPCQ087
    TreasureHunterCrystalGargoyles, // NPCQ088
    ChainbreakerCrystalArmors, // NPCQ089
    HerbalistFrozenBandages, // NPCQ090
    FrostGuideOgrePorters, // NPCQ091
    FrostGuideIceStormRunes, // NPCQ092
    NightRefugeeVampirePursuers, // NPCQ093
    NightRefugeeFortressInheritance, // NPCQ094
    SpiritSeerFortressChoir, // NPCQ095
    SpiritSeerFortressNightmares, // NPCQ096
    DragonResearcherGraveyardWyverns, // NPCQ097
    DragonResearcherMeteorMarks, // NPCQ098
    ArchivistGraveyardRoad, // NPCQ099
    ArchivistGraveyardNecromancers, // NPCQ100
    CultistAshPriests, // NPCQ101
    CultistAshEscapeRoute, // NPCQ102
    DemonHunterBloodSpiders, // NPCQ103
    DemonHunterBloodKnights, // NPCQ104
    ChainbreakerBloodMeteorChains, // NPCQ105
    ChainbreakerBloodRepairSupplies, // NPCQ106
    RuneBreakerSoulStorm, // NPCQ107
    InquisitorChaosHighPriests, // NPCQ108
    ChaosPilgrimFinalMages, // NPCQ109
    ChaosPilgrimFinalPath, // NPCQ110
    TreasureHunterFinalTreasury, // NPCQ111
    TreasureHunterFinalRansom, // NPCQ112
    ChainbreakerFinalArmors, // NPCQ113
    OrcDeserterFortressShamans, // NPCQ114
    OrcDeserterFortressBodyguards, // NPCQ115
    AlchemistDeepTrolls, // NPCQ116
    AlchemistDeepTea, // NPCQ117
    PrisonerScoutTempleGargoyles, // NPCQ118
    PrisonerScoutFortressBloodMages, // NPCQ119
    PrisonerScoutFortressExit, // NPCQ120
    DragonResearcherFinalChimeras, // NPCQ121
    DragonResearcherFinalArchives, // NPCQ122
    CrystalEngineerChaosGolems, // NPCQ123
    CrystalEngineerChaosStorms, // NPCQ124
    NightRefugeeBloodGuards, // NPCQ125
    NightRefugeeBloodExit, // NPCQ126
    DemonHunterChaosKnights, // NPCQ127
    DemonHunterChaosSpiders, // NPCQ128
    ChaosPilgrimAshAcolytes, // NPCQ129
    ChaosPilgrimAshTea, // NPCQ130
    RenegadeCampBloodContract, // NPCQ131
    RenegadeCampSupplyRecords, // NPCQ132
    SurveyorDeepMinotaurs, // NPCQ133
    SurveyorFrozenLight, // NPCQ134
    SurveyorFrozenBindings, // NPCQ135
    ArchivistMarshGhosts, // NPCQ136
    ArchivistFortressGraveGuards, // NPCQ137
    ArchivistFortressChestMarks, // NPCQ138
    CultistBloodHighPriests, // NPCQ139
    CultistBloodMages, // NPCQ140
    RuneBreakerMarshMana, // NPCQ141
    RuneBreakerChaosMages, // NPCQ142
    ChainbreakerFortressArrowGuards, // NPCQ143
    ChainbreakerFortressStores, // NPCQ144
    MonsterHunterFortressEttin, // NPCQ145
    OrcDeserterMarshOgres, // NPCQ146
    DemonHunterFinalOverlord, // NPCQ147
    RenegadeFortressSwordmaster, // NPCQ148
    SpiritSeerGraveyardLich, // NPCQ149
    InquisitorAshBloodMage, // NPCQ150
    MonsterHunterFrozenWolves, // NPCQ151
    MonsterHunterFrozenCyclops, // NPCQ152
    InquisitorTombMummies, // NPCQ153

    // A hüllőbirodalom két új dungeonjának egyszeri, találkozáshoz kötött feladatai.
    FerrymanPalaceBeastmasters, // NPCQ154
    FerrymanPalaceWater, // NPCQ155
    FerrymanPalaceHunters, // NPCQ156
    ArchivistScaledKing, // NPCQ157
    ArchivistPalaceTreasures, // NPCQ158
    ArchivistPalaceShields, // NPCQ159
    AlchemistTempleVenomMages, // NPCQ160
    AlchemistTempleAntidotes, // NPCQ161
    RuneBreakerTempleNightmares, // NPCQ162
    RuneBreakerTempleExit, // NPCQ163
    FugitiveSnakeHighPriest, // NPCQ164
    FugitiveTemplePriests, // NPCQ165

    // A tizenkét kulcsőrző személyes megbízásai.
    OrcDeserterWarchief, // NPCQ166
    GiantHunterHrold, // NPCQ167
    DragonResearcherAzrakar, // NPCQ168
    FerrymanAncientHydra, // NPCQ169
    CrystalEngineerXyrax, // NPCQ170
    FrostGuideOssyra, // NPCQ171
    SpiritSeerVelkhar, // NPCQ172
    DragonResearcherNharaz, // NPCQ173
    DemonHunterAshkaroth, // NPCQ174
    ChaosPilgrimKaelZhur, // NPCQ175
}
