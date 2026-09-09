using KaoszRubin;
using KaoszRubin.Application;
using KaoszRubin.Audio;
using KaoszRubin.Combat;
using KaoszRubin.Data;
using KaoszRubin.Domain;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Magic;
using KaoszRubin.Infrastructure;
using KaoszRubin.Transport.SignalR;
using KaoszRubin.UI;
using KaoszRubin.World;
using System.Text.Json;
using System.Text;

var tests = new (string Name, Action Run)[]
{
    ("A terminál méretőre pontosan a teljes játékképernyőt követeli meg", TerminalViewportRequiresCompleteGameScreen),
    ("A Windows Terminal újraindítás debuggerben és gyermekfolyamatban kimarad", WindowsTerminalRelaunchGuardsAreStable),
    ("A többsoros fogadói pletyka minden sora a kereten belül marad", MultilineInnRumorStaysInsideFrame),
    ("A fejlesztői fegyvercsomag követi a kategóriákat és a hátizsák kapacitását", DevelopmentWeaponsRespectCapacity),
    ("A harci tesztpálya a kért csoportokat, jelölőládákat és középső folyosót építi", DeveloperBattleTestScenarioHasRequestedLayout),
    ("A harci teszt-NPC-k pontos szinttel és véletlenül memorizált elérhető varázslatokkal készülnek", CombatTestCharactersMatchRequestedLevelAndSpells),
    ("A mentésből indított tesztcsata önállóan helyreállítja a csatalogot", LoadedDeveloperBattleCreatesRecoveryLog),
    ("A lovag harci fegyvercsere-parancsa átjut a session ellenőrzésén", KnightBattleWeaponSwapCommandIsAccepted),
    ("A széles csapás csak kölcsönösen szomszédos célpontokat ér", WeaponSweepRequiresMutualAdjacency),
    ("A taktikai fegyverjártasságok módosítják a söprést, fedezetet és varázslást", TacticalWeaponMasteriesHaveDistinctRoles),
    ("A többcélú fegyverek ívben, vonalban és kis területen hatnak", WeaponFamiliesUseDistinctAttackPatterns),
    ("A kétfegyveres harc csak képzett tőr- és kardpárokkal működik", DualWieldingRequiresDisciplineAndProficiencies),
    ("Az elf tőr ügyességi vágófegyver és párban sebzésbónuszt ad", ElvenDaggersGainPairedDamage),
    ("A taktikai diszciplínák a 8. és 18. szinten választhatók és menthetők", TacticalDisciplinesProgressAndPersist),
    ("A fogadói átképzés csoportonként őrzi meg a fejlődési lépéseket", ProgressionRetrainingPreservesAdvances),
    ("A tartalékfegyver passzív és veszteség nélkül menthető, cserélhető", ReserveWeaponIsPassiveAndPersistent),
    ("Az NPC eltört aktív fegyver helyett működő tartalékra vált", NpcSwapsBrokenWeaponForOperationalReserve),
    ("A kétkezes tartalékfegyver atomian elteszi a pajzsot", ReserveTwoHandedSwapStowsShield),
    ("A sebzéstípusok és a szörnyfegyverek módosítják a valódi sebzést", PhysicalDamageUsesTypesAndWeapons),
    ("A CSV új fegyverei adatvezéreltek és örökítik a harci tulajdonságokat", WeaponCsvPropertiesAreInherited),
    ("A host mozgási parancsa átmegy", HostMovementIsAccepted),
    ("A vendég átvehet egy NPC-t", RemotePlayerCanTakeNpcControl),
    ("A vendég saját karakterrel beléphet a host partijába", RemotePlayerCanJoinOwnCharacter),
    ("Az emberi vendég ráléphet a kincsesláda mezőjére", RemotePlayerCanStepOntoTreasureChest),
    ("A bejáraton vagy kijáraton mentett partitárs visszaállítható", PartyMemberCanBeRestoredOnEntranceOrExit),
    ("A semleges NPC nem állja el a mozgó szereplők útját", NeutralWorldNpcIsPassable),
    ("Alakzat-összeálláskor két barátságos avatar atomian helyet cserél", FormationAssemblySwapsFriendlyAvatars),
    ("A visszatérő expedíció harminc százalékos szörnyállományt céloz", ReturnExpeditionPopulationIsLimited),
    ("A vendég visszaveheti a coop mentésben foglalt karakterét", RemotePlayerCanReclaimSavedCharacter),
    ("A vendég saját ajtó- és keresési akciót küldhet", RemotePlayerCanIssueCharacterAction),
    ("A vendég térképről varázslási parancsot küldhet", RemotePlayerCanCastExplorationSpell),
    ("A vendég saját karakterével fogadói vásárlást küldhet", RemotePlayerCanPurchaseAtInn),
    ("A vendég saját hátizsákjából fogadói eladást küldhet", RemotePlayerCanSellAtInn),
    ("A vendég nyugtázhatja a közös történeti ablakot", RemotePlayerCanAcknowledgeNarrative),
    ("A vendég nyugtázhatja a közös pályaképet", RemotePlayerCanAcknowledgeLevelImage),
    ("A vendég nyugtázhatja a közös pihenési összegzőt", RemotePlayerCanAcknowledgeRest),
    ("A vendég elküldheti a saját memorizált varázslatait", RemotePlayerCanPrepareSpells),
    ("A vendég válaszolhat a saját szintlépési promptjára", RemotePlayerCanResolveLevelUpPrompt),
    ("A vendég értesítést kap a más által kezelt blokkoló ablakokról", GuestSeesOtherPlayersBlockingWindows),
    ("A vendég nem adhat leader-parancsot", RemotePlayerCannotIssueLeaderAction),
    ("A host és a vendég közös billentyűkiosztást használ", HostAndGuestUseSharedInputBindings),
    ("A faji tulajdonságokat az adatfájl tölti be", RaceTraitsAreLoadedFromData),
    ("A mágus első szintjén a Fényvarázslat a hatodik varázslat", SpellSchoolsIncludeMageLightSpell),
    ("A buff varázslatok időtartama CSV-ből, harci körökben érkezik", SpellBuffDurationLoadsAsRounds),
    ("Az öt új varázslat hatásai és célpontszabályai működnek", NewSpellEffectsAreSupported),
    ("A varázsmemória osztályonként eltérően fejlődik", SpellMemorizationCapacityUsesClassFormula),
    ("A kasztok CSV-ből módosítják a HP- és mannanövekedést", ClassResourceGrowthLoadsFromCsv),
    ("Az NPC-k és első küldetéseik CSV-ből töltődnek", NpcDefinitionsLoadFromCsv),
    ("Elira és Roderic ad-hoc beszélgetései egyszer használható szálakat alkotnak", AdHocFollowerConversationsAreConfigured),
    ("A quest roomok kizárják a véletlen térképtartalmat", QuestRoomsReserveTheirContent),
    ("A három Skeleton Knight példányhoz kötött jelvényt őriz", RodericInsigniaGuardiansAreConfigured),
    ("Sir Malrec önálló 5-ös küldetéshelyszínen vár", RodericMalrecQuestLocationIsConfigured),
    ("Roderic rögzített egyedi karakterlapból készül", RodericUsesDefinedCharacterBuild),
    ("A parti megjegyzései CSV-ből, teljes idézőjeles szöveggel töltődnek", PartyRemarksLoadFromCsv),
    ("A parti megjegyzéseinek esélyei és beszélőszámai követik a szabályt", PartyRemarkProbabilitiesFollowRules),
    ("A world-NPC generálás kizárja a fehér karakterszínt", WorldNpcGenerationExcludesWhiteColor),
    ("Az ideiglenes követő megtartja a world-NPC inverz térképszíneit", TemporaryFollowerKeepsWorldNpcMapColors),
    ("A hosszú NPC-párbeszéd az ablakon belül sortörést kap", NpcDialogueWrapsInsideRecruitmentWindow),
    ("A közös küldetésnapló elkülöníti az aktív és teljesített küldetéseket", QuestJournalBuildsSharedHistory),
    ("A feladott NPC-küldetés menthető és nem aktiválható újra", AbandonedNpcQuestRemainsResolved),
    ("Az ismeretlen CSV-fejezet sorszámos hibát ad", UnknownCsvSectionIsRejectedWithLineNumber),
    ("A hiányzó kötelező CSV-mező sorszámos hibát ad", MissingRequiredCsvFieldIsRejectedWithLineNumber),
    ("Az alkalmazkodó ember választott képességbónuszt kap", AdaptableRaceGainsChosenAbility),
    ("A duplikált parancs elutasításra kerül", DuplicateCommandIsRejected),
    ("Harc közben nem futhat felfedezési parancs", ExplorationCommandIsRejectedDuringBattle),
    ("A CharacterId mentés után is stabil", CharacterIdSurvivesSerialization),
    ("Az ölési statisztika és az NPC csatlakozási helye menthető", CharacterHistorySurvivesSerialization),
    ("A régi játékmentések az aktuális formátumra migrálódnak", LegacyGameSavesMigrateToCurrentVersion),
    ("A mentésszerkesztő biztonsági másolattal írja felül az állást", SaveEditorOverwritesWithBackup),
    ("Az ismeretlen és verzió nélküli játékmentések elutasításra kerülnek", InvalidGameSaveVersionsAreRejected),
    ("Az osztályspecializáció mentés után is megmarad", ClassSpecializationSurvivesSerialization),
    ("A 10. és 20. szintű osztályfejlesztések mentődnek és megjelennek", ClassFeatureUpgradesPersistAndAppearOnSheet),
    ("A képességpontok 13-nál megállnak és mentődnek", AbilityIncreasesAreCappedAndPersisted),
    ("A fegyverjártasság két családra korlátozott, hat és menthető", WeaponProficienciesAreLimitedEffectiveAndPersisted),
    ("Disconnectkor AI veszi át, reconnectkor visszakapja", DisconnectAndReconnectRestoreControl),
    ("A taktikai távolság követi a konzolcellák kettő az egyhez arányát", TacticalDistanceUsesConsoleAspectRatio),
    ("A 2x2-es alakzat minden irányban a vezér slotjához igazodik", PartyFormationPositionsFollowFacing),
    ("A zárt 2x2-es alakzat a saját mezőin fordul meg", PartyFormationTurnsInPlace),
    ("A feloszlatott parti az alakzatslotok sorrendjében követ", FormationSlotsControlFreeFollowOrder),
    ("A zárt alakzat a szűkületben állapotvesztés nélkül libasorra vált", LockedFormationUsesSingleFileLayout),
    ("A követő kísérőhelyei az alakzat hátsó éle mögött vannak", FormationEscortPositionsFollowRearEdge),
    ("A követő kitérhet a zárt alakzat célmezőjéről", TemporaryFollowerCanYieldToFormation),
    ("Zárt alakzatban minden slot pozíciója ajtó-interakciós eredőpont", LockedFormationSharesDoorInteractionOrigins),
    ("Az alakzatos zárnyitás a kiválasztott partitag kulcsát fogyasztja", FormationDoorKeyOwnerTakesPriority),
    ("Zárt alakzatból a coop vendég nem léphet ki", LockedFormationRejectsRemoteMovement),
    ("A csapatharcban az átlós ellenfél is közelharci távolságban van", DiagonalEnemyIsMeleeAdjacent),
    ("A csapatharc váza kezeli a belépési kört és a kezdeményezési sorrendet", TacticalBattleStateOrdersEligibleParticipants),
    ("A nyitó ütésváltást a kezdeményezés dönti el, a rajtaütést kivéve", TeamBattleOpeningOrderUsesInitiative),
    ("Az Első csapás a nyitásban +10, a rendes sorrendben +2 kezdeményezést ad", FirstStrikeUsesSeparateOpeningInitiative),
    ("A gyorsítás és lassítás a következő kör elején rendezi át a kezdeményezést", SpellEffectsReorderInitiativeAtCycleBoundary),
    ("Az időzített állapot kezdeményezés-büntetése körhatáron rendezi át a sorrendet", StatusPenaltyReordersInitiativeAtCycleBoundary),
    ("A kezdeményezési holtverseny sorrendje körönként stabil marad", InitiativeTiesRemainStable),
    ("A zárt út mögötti ellenfél nem érkezhet meg néhány harci kör alatt", TacticalArrivalRequiresWalkableRoute),
    ("A csapatharc két egymást követő tétlen kör után áll le", TeamBattleDetectsInactiveSide),
    ("A csapatharc ugyanazt a támadási szabálymotort használja", TeamBattleAttackUsesExistingCombatRules),
    ("A szörny Ereje találat után lökési vagy tántorítási próbát ad", MonsterStrengthCreatesTacticalPressure),
    ("A közelharci támadás az ellenfél haláláig leköti a karaktert", TeamBattleEngagementLastsUntilEnemyDeath),
    ("A zárt alakzat első sora védi a mögötte álló társat", TeamBattleFormationProtectsRearRow),
    ("A vezér külön harcra készítheti a hátsó sor két oldalát", RearCombatPreparationIsLeaderControlled),
    ("A harci AI a gyógyital erejét a megengedett HP-veszteséghez igazítja", TeamBattleAiHealingPotionAvoidsWaste),
    ("A zárt libasor együtt mozog, de nem kap hátsósori védelmet", TeamBattleSingleFileHasNoRearProtection),
    ("Harcban csak szabad hátsó sori karakter használhat CSV-ben engedélyezett italt", TeamBattleItemUseRequiresFreeRearPosition),
    ("A hátsó sor szálfegyverrel eléri az első társ lekötött ellenfelét", TeamBattleRearPolearmReachUsesFrontEngagement),
    ("A hátsó sori pap elűzheti az első sor által lekötött élőholtat", RearPriestCanTurnFrontEngagedUndead),
    ("A hátráló varázshasználó távolságcélja repülő ellenfélnél nagyobb", SpellcasterRetreatDistanceIsCapped),
    ("Az ellenfél nézésiránya oldal- és hátbatámadási bónuszt ad", TacticalAttackArcsUseEnemyFacing),
    ("A tolvaj tőrrel a zárt alakzat hátsó sorából is orvtámad", ThiefCanBackstabFromRearFormation),
    ("A Hátra! helycsere átadja az első sori lekötéseket", TeamBattleSwapToRearTransfersEngagements),
    ("Az alakzat csak a fennálló lekötéseket megtartva mozdulhat", TeamBattleFormationMovementPreservesEngagements),
    ("A csapatharc célpontja akcióvesztés nélkül váltható", TeamBattleTargetCanBeChanged),
    ("Az NPC varázslási szabálya tartalékolja a mannát és csak egycélú támadást választ", NpcSpellcastingPolicyPreservesMana),
    ("Az Átoktörés csak ténylegesen tisztítható csapattársra használható", BreakCurseRequiresUsefulPartyTarget),
    ("A Megtisztítás nem használható egyszerű gyógyításként", CleansingHealRequiresRemovableStatus),
    ("A lekötött pap és lovag ritkítja a rutinvarázslást, de a sürgős segítséget nem", EngagedSupportSpellcastingIsThrottled),
    ("Az NPC támadóvarázslási összerőhatárai inkluzívak", NpcOffensiveSpellStrengthThresholdsAreInclusive),
    ("Az NPC varázspontozása csoport ellen területi, gyenge célra takarékos támadást kedvel", NpcSpellUtilityValuesTargetsAndOverkill),
    ("Az NPC varázsmemóriája váltogatja a repertoárt, de nem ír felül nagy erőkülönbséget", NpcSpellMemoryBalancesVarietyAndUtility),
    ("Az NPC varázsterv hiszterézise megtartja a közeli tervet és elengedi az összeomlottat", NpcSpellPlanRetentionIsStable),
    ("Az NPC varázsterv érvényét veszti halott célnál, elfogyott manánál és sikertelen tervnél", NpcSpellPlanInvalidationCoversFailureModes),
    ("Csak a szabad, alakzaton kívüli nem-lovag mozoghat varázslási pozícióba", NpcSpellPlanMovementHonorsClassAndBattleState),
    ("Az NPC tüzelőállás-pontozása körökkel és közelharci veszéllyel számol", NpcSpellPositionPenaltyIncludesTravelAndDanger),
    ("A veszélyben tüzelőállást kereső mágus a teljes mozgást részesíti előnyben", NpcCasterPrefersFullSafeCastingMove),
    ("A csapatharc varázsmemóriája sorrendben őrzi a megkísérelt terveket", TeamBattleStoresNpcSpellMemory),
    ("A szabad és lekötött varázslás eltérően módosítja a harci hibakockázatot", EngagementAdjustsSpellFailureChance),
    ("A harcba hívott erősítés a következő körben lép be", TeamBattleReinforcementJoinsNextCycle),
    ("A coop session validálja a csapatharcos mozgást, tárgyhasználatot és passzt", TeamBattleCommandsAreValidated),
    ("A fenyegetésbecslés felismeri az elszigetelt gyenge ellenfelet", EncounterThreatAssessmentRecognizesSafeFight),
    ("A gyorsharc legfeljebb három jelentéktelen ellenfelet enged át", QuickCombatAllowsUpToThreeSafeEnemies),
    ("A gyorsharc beállítása normalizálható és menthető", QuickCombatSettingPersists),
    ("A csatarészlet panel lapozható és mutatja a kritikus esélyt", BattleDetailsPanelPagesCalculation),
    ("A gyorsharc összesítője ölőnként csoportosítja az ellenfeleket és az XP-t", QuickCombatSummaryListsKillsAndExperience),
    ("A taktikai és gyorsharc összesítője kiírja a HP- és mannafogyást", TeamBattleSummaryListsResourceUse),
    ("A felszerelés súlya leterheltséget és mozgási hátrányt okoz", EquipmentWeightAffectsMobility),
    ("A karakterlap és a tárgyvizsgálat előre jelzi a harci terhelést", MobilityPreviewIsVisible),
    ("A harcos taktikai találati esélyei a valódi képletet követik", FighterTacticHitChancesUseCombatFormula),
    ("A győzelmi üzenet nem ismétli meg az utolsó támadást", VictoryMessageIsConcise),
    ("A győzelmi összegzés egyetlen kompakt sor", VictorySummaryIsCompact),
    ("Csak az aktív BattleId és TurnId parancsa fogadható el", BattleCommandRequiresCurrentPrompt),
    ("Az ellenfél köre külön Space-paranccsal léptethető", EnemyTurnAdvanceCommandIsAccepted),
    ("A távoli harci promptot csak a karakter gazdája oldhatja fel", RemoteBattlePromptRequiresCharacterOwner),
    ("A varázslat command csak szemantikus választást hordoz", SpellBattleCommandIsAccepted),
    ("Hiányos varázslat command nem juthat át", MalformedSpellBattleCommandIsRejected),
    ("A promptban nem engedélyezett harci akció elutasításra kerül", DisallowedBattleActionIsRejected),
    ("A csatakezdő taktikai parancs átjut a session-validáción", BattleTacticCommandIsAccepted),
    ("A session snapshot JSON-on körbeírható", SessionSnapshotRoundTripsThroughJson),
    ("A session-aktivitás karakterhez címezhető", SessionActivityCanTargetCharacter),
    ("A fogadó snapshotja közös pletykákat továbbít", InnSnapshotCarriesSharedRumors),
    ("A fogadónevek és hangulatpletykák CSV-ből töltődnek", InnNamesAndRumorsLoadFromCsv),
    ("A snapshot csak az aktív harci promptot fogadja el", SnapshotRequiresCurrentBattlePrompt),
    ("A world snapshot nem szivárogtat rejtett entitást", WorldSnapshotOnlyContainsRevealedState),
    ("A közös látótér elrejti és rövid ideig megjegyzi az eltűnt szörnyet", PartyVisionTracksLastKnownEnemy),
    ("A közös észlelés felfedi a lopakodót és pontatlan hangjelet ad", PartyPerceptionDetectsStealthAndSound),
    ("A rejtett csapda nem szivárog ki, a felfedezett pedig replikálódik", TrapVisibilityFollowsDiscoveryState),
    ("A csapdakészlet és darabszám a labirintusszinttel nehezedik", TrapConfigurationScalesByMazeLevel),
    ("A mentés visszaállítja a szörny alatt fekvő csapdát", SavedTrapCanShareEnemyPosition),
    ("A tárgyátok esélye pályánként konfigurálható", CursedLootChanceIsConfiguredPerMazeLevel),
    ("A karakter kasztja, faja és átmeneti hatásai módosítják a látótávot", CharacterVisionRangeUsesClassRaceAndEffects),
    ("A szörnyek látótávja CSV-ből érkezik", EnemyVisionRangesLoadFromCsv),
    ("A felfedés változó látótávot és látóvonalat használ", FogRevealUsesVariableRangeAndLineOfSight),
    ("A szörnyek ébersége, felderítése és alvásképessége adatvezérelt", EnemyAwarenessAndSearchAreDataDriven),
    ("A falka közös keresési pont körül felderítőkre és biztosítókra oszlik", EnemyPackSearchStaysCoordinated),
    ("A felderítő a még be nem járt folyosóágakat választja és együtt marad", EnemySearchExploresCorridorFrontiers),
    ("A szaglás és hatodik érzék CSV-ből, útvonaltávolsággal működik", EnemyTrackingSenseIsDataDriven),
    ("A szörnyjellemzők és képességparaméterek külön töltődnek", MonsterTraitsAndAbilitiesAreDataDriven),
    ("A regeneráció és a leheletlehűlés példányonként működik", MonsterRegenerationAndBreathCooldownWork),
    ("A sebzés nélküli, időzített állapot is lejár", TimedNonDamageStatusExpires),
    ("Az összetett szörnyképesség egy aktiválással sebez és állapotot okoz", CompositeMonsterAbilityAppliesAllEffects),
    ("A szörnyképesség csak a CSV-ben kötött fegyverrel aktiválódik", MonsterAbilityRespectsWeaponBinding),
    ("A pályanevekből szabályos képfájlnév készül", LevelImageFileNamesAreNormalized),
    ("Az ellenség a legközelebbi látható csapattagot célozza", EnemyTargetsNearestVisiblePartyMember),
    ("A mozgó world entity azonosítója stabil", WorldEntityIdSurvivesMovement),
    ("Az azonos mezőn lévő holttestek egy halomba kerülnek", CorpsesStackOnOneCell),
    ("A world delta minden lényeges változást leír", WorldDeltaCapturesChanges),
    ("Eltérő pályák között nem készülhet delta", WorldDeltaRejectsDifferentWorld),
    ("A publisher teljes snapshot után ACK-alapú deltát küld", ReplicationPublisherUsesAcknowledgedBaseline),
    ("Ismeretlen ACK teljes resyncet kényszerít", UnknownReplicationAckForcesResync),
    ("Pályaváltáskor a publisher teljes snapshotra vált", ReplicationPublisherUsesFullSnapshotForNewWorld),
    ("A kliens store teljes snapshotot és régi baseline-ról érkező deltát alkalmaz", ClientStoreAppliesReplicationFrames),
    ("Hiányzó delta-baseline esetén a kliens resyncet kér", ClientStoreRequestsResyncForMissingBaseline),
    ("Az inventory snapshot explicit slotokat és revíziót tartalmaz", InventorySnapshotHasSlotsAndRevision),
    ("A hátizsák 12 helyes és kilences kötegeket képez", BackpackStacksIdenticalItemsUpToNine),
    ("Az azonosítatlan varázstárgy példányállapota mentés és mozgatás közben megmarad", MagicItemIdentificationStatePersists),
    ("A felszerelés tartóssága adatvezérelt és menthető", EquipmentDurabilityDataAndStatePersist),
    ("A tárgyvizsgálat és a részletes karakterinfó mutatja a felszerelés állapotát", EquipmentDurabilityIsVisible),
    ("A közös harci motor koptatja a használt fegyvert és a találatot fogó vértezetet", CombatAppliesEquipmentWear),
    ("A sav és a káosz különleges módon koptatja a felszerelést", AcidAndChaosCauseSpecialEquipmentWear),
    ("A sérült és törött felszerelés fokozatos harci hátrányt okoz", DamagedAndBrokenEquipmentAffectsCombat),
    ("A fogadói javítás ára ritkaság- és kopásarányos, az állapotot pedig megőrzi", EquipmentRepairRestoresDurability),
    ("A legintelligensebb élő mágus egyszer megpróbálja azonosítani a friss zsákmányt", MageIdentifiesFreshMagicLoot),
    ("Az átkozott tárgy aktiválódik, megköt és alkalmazza az adatvezérelt hátrányokat", CursedItemsActivateBindAndApplyEffects),
    ("Az Átoktörés és a Vándormágus végleg megtisztítja és feloldja a tárgyat", ItemCursePurificationIsPermanent),
    ("A host és a vendég ugyanazt a karakterlap-layoutot használja", CharacterSheetLayoutIsShared),
    ("A részletes karakterlap közösen mutatja a látásmódosítókat és ölési statisztikát", CharacterDetailsAreShared),
    ("A karakterlap külön színezi az alacsony HP-t és a mannát", CharacterSheetColorsHealthAndManaSeparately),
    ("A host és a vendég közös varázslat-UI modelleket használ", SpellUiModelsAreShared),
    ("A host és a vendég közös pihenési összegzőt használ", RestSummaryUiIsShared),
    ("A vendég tárgyvizsgálata nem vágja le a sebzésértéket", GuestItemInspectionKeepsDamageValue),
    ("A boss-ablak és a harci promptok közös UI-modellt használnak", BossAndBattlePromptsAreShared),
    ("A kompakt party státusz HP-t és manát százalékosan mutat", CompactPartyStatusShowsResources),
    ("Az ablakkeret-katalógus méretezhető és konfigurálható", WindowFrameCatalogIsResizableAndConfigured),
    ("A vendég snapshot kasztbetűt és karakterszínt őriz", GuestAvatarUsesClassGlyphAndCharacterColor),
    ("A vendég nem rajzol újra puszta snapshot-sorszám változásra", GuestRedrawIgnoresReplicationSequences),
    ("A vendég a teljes party inventory read modeljét megkapja", ReplicationPublisherSharesPartyInventories),
    ("Az inventory transfer atomi és megőrzi a töltetet", InventoryTransferIsAtomicAndPreservesCharges),
    ("A hátizsákköteg felezése atomi és tele hátizsáknál figyelmeztet", InventoryStackSplitIsAtomicAndRequiresSpace),
    ("Az elfogyasztható köteg egyenletesen és veszteségmentesen oszlik szét", ConsumableStackDistributesEvenly),
    ("Az elfogyasztható köteg fele átadható a követőnek", ConsumableStackHalfTransfersToFollower),
    ("A képességnövelő varázstárgyak minden kasztnál 13-ig hatnak", AbilityMagicItemsAreUniversalAndCapped),
    ("Az elavult inventory-revízió elutasításra kerül", StaleInventoryRevisionIsRejected),
    ("A vendég hátizsákok között mozgathat, felszerelést nem", RemoteInventoryTransferCanCrossBackpacksOnly),
    ("A használat, eldobás és pickup command alakja validált", InventoryActionCommandsAreValidated),
    ("Nem fogyasztható tárgy használata elutasításra kerül", NonConsumableUseIsRejected),
    ("A földi loot megőrzi a töltetet és revíziózott", GroundPilePreservesChargesAndRevision),
    ("A katalógus fingerprint determinisztikus", CatalogFingerprintIsDeterministic),
    ("A handshake verziót és katalógushasht ellenőriz", HandshakeValidatesProtocolAndCatalog),
    ("A reconnect-token ugyanazt a PlayerId-t állítja vissza", HandshakeReconnectRestoresPlayer),
    ("A JSON wire codec allowlistelt commandot ír körbe", ProtocolCodecRoundTripsCommand),
    ("A host gateway kapcsolathoz köti a PlayerId-t", HostGatewayBindsAuthenticatedPlayer),
    ("A host gateway kezeli a control-, replikáció- és disconnect-folyamot", HostGatewayRunsConnectionLifecycle),
    ("A hálózati lifecycle és a szimulációs esemény nem deadlockol", GatewayAndSimulationDoNotDeadlock),
    ("A SignalR LAN host elindítható és leállítható", () =>
        SignalRServerStartsAndStops().GetAwaiter().GetResult()),
    ("A SignalR kliens végigviszi a LAN coop kapcsolatot", () =>
        SignalRClientRunsLanProtocolFlow().GetAwaiter().GetResult()),
    ("Az in-memory transport végigviszi a coop protokollfolyamot", () =>
        InMemoryTransportRunsProtocolFlow().GetAwaiter().GetResult())
};

static void TerminalViewportRequiresCompleteGameScreen()
{
    var minimumWidth = ConsoleRenderer.PlayfieldWidth + 1;
    var minimumHeight = ConsoleRenderer.ScreenRowCount;
    Assert(!new TerminalViewport.Size(minimumWidth - 1, minimumHeight).CanFit(minimumWidth, minimumHeight),
        "Egy hiányzó oszlopnál a játéknak várakoznia kell.");
    Assert(!new TerminalViewport.Size(minimumWidth, minimumHeight - 1).CanFit(minimumWidth, minimumHeight),
        "Egy hiányzó sornál a játéknak várakoznia kell.");
    Assert(new TerminalViewport.Size(minimumWidth, minimumHeight).CanFit(minimumWidth, minimumHeight),
        "A pontos minimális méretnek már használhatónak kell lennie.");
}

static void WindowsTerminalRelaunchGuardsAreStable()
{
    Assert(SystemHelpers.ShouldRelaunchInWindowsTerminal(
            isWindows: true, hasWindowsTerminalSession: false, hasChildMarker: false, debuggerAttached: false),
        "A közvetlen Windows-indítás nem kérte a Windows Terminalt.");
    Assert(!SystemHelpers.ShouldRelaunchInWindowsTerminal(
            isWindows: true, hasWindowsTerminalSession: true, hasChildMarker: false, debuggerAttached: false) &&
           !SystemHelpers.ShouldRelaunchInWindowsTerminal(
               isWindows: true, hasWindowsTerminalSession: false, hasChildMarker: true, debuggerAttached: false) &&
           !SystemHelpers.ShouldRelaunchInWindowsTerminal(
               isWindows: true, hasWindowsTerminalSession: false, hasChildMarker: false, debuggerAttached: true) &&
           !SystemHelpers.ShouldRelaunchInWindowsTerminal(
               isWindows: false, hasWindowsTerminalSession: false, hasChildMarker: false, debuggerAttached: false),
        "A Windows Terminal újraindítási őrfeltételei ciklust vagy debuggerleválást engednek.");
}

var failures = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS  {test.Name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.WriteLine($"FAIL  {test.Name}: {exception.Message}");
    }
}

return failures == 0 ? 0 : 1;

static void HostMovementIsAccepted()
{
    var (session, leader, _) = CreateSession();
    Assert(session.Submit(new MoveCharacterCommand(session.HostPlayerId, 1, leader.Id, Direction.Right)),
        "A parancs nem került a sorba.");
    Assert(session.TryReadCommand(out var command) && command is MoveCharacterCommand,
        "A host érvényes mozgási parancsa nem olvasható ki.");
}

static void RemotePlayerCanTakeNpcControl()
{
    var (session, _, companion) = CreateSession();
    var remote = session.RegisterRemotePlayer();
    Assert(session.TryAssignRemoteControl(remote, companion.Id, out var error), error);
    Assert(session.Submit(new MoveCharacterCommand(remote, 1, companion.Id, Direction.Left)),
        "A vendég parancsa nem került a sorba.");
    Assert(session.TryReadCommand(out var command) && command.SenderId == remote,
        "A vendég saját karakterének parancsát elutasította a session.");
}

static void RemotePlayerCanJoinOwnCharacter()
{
    var leader = CreateCharacter("Host");
    var party = new Party();
    party.SetLeader(leader);
    var session = new GameSession(party, leader);
    var joined = CreateCharacter("Vendég");
    LiveCharacter? registered = null;
    var hash = CatalogFingerprint.Compute(Encoding.UTF8.GetBytes("catalog"));
    var gateway = new CoopHostGateway(session, new SessionHandshakeService(session, "1.0.0", hash),
        new SessionReplicationPublisher(), _ => joined, character => registered = character);
    var helloMessages = gateway.HandleIncoming("guest", CoopProtocolJson.Encode(
        new ClientHello(SessionProtocol.Version, "1.0.0", hash, "Vendég")));
    var hello = (ServerHello)CoopProtocolJson.Decode(helloMessages.Single().WireMessage);
    var responseMessages = gateway.HandleIncoming("guest", CoopProtocolJson.Encode(
        new JoinCharacterRequest(hello.PlayerId!.Value, "character-data")));
    var response = (CharacterControlResult)CoopProtocolJson.Decode(responseMessages.Single().WireMessage);

    Assert(response.Accepted && response.CharacterId == joined.Id && registered == joined &&
           party.Members.Contains(joined) && session.IsHumanControlled(joined.Id),
        "A host nem vette fel és nem rendelte a távoli játékoshoz a kliens karakterét.");
}

static void RemotePlayerCanReclaimSavedCharacter()
{
    var (session, _, companion) = CreateSession();
    var hash = CatalogFingerprint.Compute(Encoding.UTF8.GetBytes("catalog"));
    var registered = false;
    var gateway = new CoopHostGateway(session, new SessionHandshakeService(session, "1.0.0", hash),
        new SessionReplicationPublisher(), _ => companion, _ => registered = true, companion.Id);
    var helloMessage = gateway.HandleIncoming("saved-guest", CoopProtocolJson.Encode(
        new ClientHello(SessionProtocol.Version, "1.0.0", hash, "Vendég"))).Single();
    var hello = (ServerHello)CoopProtocolJson.Decode(helloMessage.WireMessage);
    var resultMessage = gateway.HandleIncoming("saved-guest", CoopProtocolJson.Encode(
        new JoinCharacterRequest(hello.PlayerId!.Value, "saved-character"))).Single();
    var result = (CharacterControlResult)CoopProtocolJson.Decode(resultMessage.WireMessage);
    Assert(result.Accepted && result.CharacterId == companion.Id && session.IsHumanControlled(companion.Id) &&
           !registered, "A mentett vendégslot nem a host meglévő karakterpéldányához lett rendelve.");
    Assert(gateway.QueueCharacterState(companion.Id, "authoritative-character", CharacterSyncReason.CharacterDied),
        "A host nem tudta sorba állítani a vendég célzott halálállapotát.");
    var sync = (CharacterStateSync)CoopProtocolJson.Decode(gateway.DrainPendingMessages().Single().WireMessage);
    Assert(sync.PlayerId == hello.PlayerId && sync.CharacterId == companion.Id &&
           sync.CharacterData == "authoritative-character" && sync.Reason == CharacterSyncReason.CharacterDied,
        "A karakter-visszaszinkronizálás nem a megfelelő vendéghez került.");
}

static void RemotePlayerCanIssueCharacterAction()
{
    var (session, _, companion) = CreateSession();
    var remote = session.RegisterRemotePlayer();
    Assert(session.TryAssignRemoteControl(remote, companion.Id, out var error), error);
    var command = new CharacterActionCommand(remote, 1, companion.Id, CharacterAction.OpenDoor,
        new Position(4, 3));
    session.Submit(command);
    Assert(session.TryReadCommand(out var accepted) && accepted == command,
        "A session elutasította a vendég saját karakterhez kötött ajtóakcióját.");
}

static void RemotePlayerCanCastExplorationSpell()
{
    var (session, _, companion) = CreateSession();
    var remote = session.RegisterRemotePlayer();
    Assert(session.TryAssignRemoteControl(remote, companion.Id, out var assignmentError), assignmentError);
    var command = new CastExplorationSpellCommand(remote, 1, companion.Id, "S-TEST", null,
        new Position(3, 2));
    session.Submit(command);
    Assert(session.TryReadCommand(out var accepted) && accepted == command,
        "A session nem fogadta el a vendég saját, térképi varázslási parancsát.");
}

static void RemotePlayerCanPurchaseAtInn()
{
    var (session, _, companion) = CreateSession();
    var remote = session.RegisterRemotePlayer();
    Assert(session.TryAssignRemoteControl(remote, companion.Id, out var assignmentError), assignmentError);
    session.SetPhase(GameSessionPhase.Inn);
    var command = new InnPurchaseCommand(remote, 1, companion.Id, 3, InnVendorKind.Market, 0);
    session.Submit(command);
    Assert(session.TryReadCommand(out var accepted) && accepted == command,
        "A session elutasította a vendég saját fogadói vásárlását.");
}

static void RemotePlayerCanSellAtInn()
{
    var (session, _, companion) = CreateSession();
    var remote = session.RegisterRemotePlayer();
    Assert(session.TryAssignRemoteControl(remote, companion.Id, out var assignmentError), assignmentError);
    companion.AddToBackpack(new MiscItemDefinition("I-SELL", "Eladó tárgy", "Teszt", 10));
    session.SetPhase(GameSessionPhase.Inn);
    var command = new InnSaleCommand(remote, 1, companion.Id, 3, companion.InventoryRevision, 0);
    Assert(session.Submit(command) && session.TryReadCommand(out var accepted) && accepted == command,
        "A session elutasította a vendég saját fogadói eladását.");
}

static void RemotePlayerCanAcknowledgeNarrative()
{
    var (session, _, companion) = CreateSession();
    var remote = session.RegisterRemotePlayer();
    Assert(session.TryAssignRemoteControl(remote, companion.Id, out var assignmentError), assignmentError);
    session.SetPhase(GameSessionPhase.Paused);
    var command = new AcknowledgeNarrativeCommand(remote, 1, companion.Id, Guid.NewGuid());
    session.Submit(command);
    Assert(session.TryReadCommand(out var accepted) && accepted == command,
        "A session elutasította a vendég történeti nyugtázását.");
}

static void RemotePlayerCanPrepareSpells()
{
    var (session, _, companion) = CreateSession();
    var remote = session.RegisterRemotePlayer();
    Assert(session.TryAssignRemoteControl(remote, companion.Id, out var assignmentError), assignmentError);
    session.SetPhase(GameSessionPhase.Paused);
    var command = new PrepareSpellsCommand(remote, 1, companion.Id, Guid.NewGuid(), ["S001", "S002"]);
    session.Submit(command);
    Assert(session.TryReadCommand(out var accepted) && accepted == command,
        "A session elutasította a vendég memorizálási választását.");
}

static void RemotePlayerCanResolveLevelUpPrompt()
{
    var (session, _, companion) = CreateSession();
    var remote = session.RegisterRemotePlayer();
    Assert(session.TryAssignRemoteControl(remote, companion.Id, out var assignmentError), assignmentError);
    session.SetPhase(GameSessionPhase.Paused);
    var command = new ResolveLevelUpPromptCommand(remote, 1, companion.Id, Guid.NewGuid(), "PERK-TEST");
    session.Submit(command);
    Assert(session.TryReadCommand(out var accepted) && accepted == command,
        "A session elutasította a vendég szintlépési választását.");
}

static void HostAndGuestUseSharedInputBindings()
{
    Assert(GameInputBindings.IsCharacterSheetToggle(ConsoleKey.Tab), "A Tab nem vált karakterlapfókuszt.");
    Assert(GameInputBindings.InventoryAction(ConsoleKey.Enter) == InventoryInputAction.Use &&
           GameInputBindings.InventoryAction(ConsoleKey.D) == InventoryInputAction.Drop &&
           GameInputBindings.InventoryAction(ConsoleKey.Spacebar) == InventoryInputAction.MoveItem &&
           GameInputBindings.InventoryAction(ConsoleKey.F) == InventoryInputAction.SplitStack &&
           GameInputBindings.InventoryAction(ConsoleKey.S) == InventoryInputAction.DistributeStack &&
           GameInputBindings.InventoryAction(ConsoleKey.K) == InventoryInputAction.GiveFollowerStack &&
           GameInputBindings.InventoryAction(ConsoleKey.I) == InventoryInputAction.Inspect,
        "Az inventory közös billentyűkiosztása eltér a host vezérlésétől.");
    Assert(GameInputBindings.CharacterAction(ConsoleKey.N) == CharacterAction.OpenDoor &&
           GameInputBindings.CharacterAction(ConsoleKey.Z) == CharacterAction.CloseOrLockDoor &&
           GameInputBindings.CharacterAction(ConsoleKey.K) == CharacterAction.SearchCurrentPosition,
        "Az N/Z/K karakterakciók nincsenek a közös keymapben.");
    Assert(GameInputBindings.LeaderAction(ConsoleKey.P, false) == LeaderAction.Rest &&
           GameInputBindings.LeaderAction(ConsoleKey.G, false) == LeaderAction.ToggleRegrouping &&
           GameInputBindings.LeaderAction(ConsoleKey.H, false) == LeaderAction.ToggleHoldPosition &&
           GameInputBindings.LeaderAction(ConsoleKey.T, false) == LeaderAction.ToggleAttackMode &&
           GameInputBindings.LeaderAction(ConsoleKey.Enter, false) is null &&
           GameInputBindings.LeaderAction(ConsoleKey.Enter, true) == LeaderAction.ActivateExit,
        "A leader-only billentyűkiosztás hibás.");
    Assert(GameInputBindings.PreserveFormationFacing(ConsoleModifiers.Shift) &&
           GameInputBindings.PreserveFormationFacing(ConsoleModifiers.Shift | ConsoleModifiers.Alt) &&
           !GameInputBindings.PreserveFormationFacing(ConsoleModifiers.Control),
        "A Shift+nyíl alakzati oldalazás módosítója nem közös a host és a vendég között.");
}

static void RemotePlayerCannotIssueLeaderAction()
{
    var (session, _, companion) = CreateSession();
    var remote = session.RegisterRemotePlayer();
    Assert(session.TryAssignRemoteControl(remote, companion.Id, out var error), error);
    var events = CollectEvents(session);
    session.Submit(new LeaderActionCommand(remote, 1, companion.Id, LeaderAction.Rest));
    Assert(!session.TryReadCommand(out _), "A vendég leader-parancsa átjutott.");
    Assert(events.OfType<GameCommandRejectedEvent>().Any(),
        "Az elutasított parancsról nem keletkezett esemény.");
}

static void DuplicateCommandIsRejected()
{
    var (session, leader, _) = CreateSession();
    var events = CollectEvents(session);
    session.Submit(new MoveCharacterCommand(session.HostPlayerId, 1, leader.Id, Direction.Right));
    Assert(session.TryReadCommand(out _), "Az első parancsot is elutasította a session.");
    session.Submit(new MoveCharacterCommand(session.HostPlayerId, 1, leader.Id, Direction.Left));
    Assert(!session.TryReadCommand(out _), "A duplikált parancs átjutott.");
    Assert(events.OfType<GameCommandRejectedEvent>()
        .Any(rejected => rejected.Reason.Contains("Ismételt", StringComparison.Ordinal)),
        "A duplikáció oka nem jelent meg az eseményben.");
}

static void ExplorationCommandIsRejectedDuringBattle()
{
    var (session, leader, _) = CreateSession();
    var events = CollectEvents(session);
    session.SetPhase(GameSessionPhase.Battle);
    session.Submit(new MoveCharacterCommand(session.HostPlayerId, 1, leader.Id, Direction.Right));
    Assert(!session.TryReadCommand(out _), "Harc közben átjutott egy mozgási parancs.");
    Assert(events.OfType<GameCommandRejectedEvent>()
        .Any(rejected => rejected.Reason.Contains("felfedezés", StringComparison.Ordinal)),
        "A hibás session-fázis nem jelent meg az elutasításban.");
}

static void CharacterIdSurvivesSerialization()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var roster = new CharacterRoster();
    var character = CreateCharacter("Persistent");
    roster.Add(character);
    roster.Select(character);
    var service = new CharacterSaveService(Path.Combine(Path.GetTempPath(), "unused-character-save.json"), data);
    var restored = service.Deserialize(service.Serialize(roster));
    Assert(restored.SelectedCharacter?.Id == character.Id, "A karakter stabil azonosítója megváltozott mentéskor.");
}

static void CharacterHistorySurvivesSerialization()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var character = CreateCharacter("Krónikás");
    character.SetNpcBehavior(NpcBehavior.Defensive);
    character.SetNpcJoinOrigin(4, "A Kormos Griff");
    character.RecordMonsterKill(data.Enemies[0].Id, 3);
    var service = new CharacterSaveService(Path.Combine(Path.GetTempPath(), "unused-history-save.json"), data);
    var restored = service.DeserializeCharacter(service.SerializeCharacter(character));
    Assert(restored.NpcJoinedMazeLevel == 4 && restored.NpcJoinedLocation == "A Kormos Griff" &&
           restored.MonsterKills.GetValueOrDefault(data.Enemies[0].Id) == 3,
        "A karakter történeti adatai nem élték túl a mentési körutat.");
}

static void LegacyGameSavesMigrateToCurrentVersion()
{
    foreach (var version in new[] { 1, 2, 3, 18 })
    {
        var state = new GameSaveData { Version = version, MazeLevel = 6 };
        var migrated = GameSaveFormat.MigrateToCurrent(state);
        Assert(ReferenceEquals(state, migrated) && migrated.Version == GameSaveFormat.CurrentVersion &&
               migrated.MazeLevel == 6,
            $"A(z) {version}. mentésverzió migrációja hibás vagy megváltoztatta a pályaszintet.");
    }

    var current = new GameSaveData
    {
        UsedAdHocConversationIds = ["ELIRA_RESCUE:ADHOC_1_START"],
        LastAdHocConversationUtc = new DateTimeOffset(2026, 8, 31, 12, 0, 0, TimeSpan.Zero),
        AdHocConversationMazeLevel = 4,
        EliraInnCharacterIndex = 2,
        EliraInnVisitsRemaining = 3,
        Formation = new PartyFormationSnapshot(CharacterId.New(), null, null, null,
            Direction.Left, PartyFormationState.Locked, PartyFormationLayout.SingleFile)
    };
    var restored = JsonSerializer.Deserialize<GameSaveData>(JsonSerializer.Serialize(current));
    Assert(restored is { UsedAdHocConversationIds: ["ELIRA_RESCUE:ADHOC_1_START"],
               AdHocConversationMazeLevel: 4 } &&
           restored.LastAdHocConversationUtc == current.LastAdHocConversationUtc &&
           restored.EliraInnCharacterIndex == 2 && restored.EliraInnVisitsRemaining == 3 &&
           restored.Formation is { State: PartyFormationState.Locked, Layout: PartyFormationLayout.SingleFile },
        "Az egyszer már elindított ad-hoc párbeszéd vagy a korlátozásai elvesztek mentéskor.");

    var old = GameSaveFormat.MigrateToCurrent(new GameSaveData { Version = 13 });
    Assert(old.Formation is { State: PartyFormationState.Disbanded },
        "A 13-as mentés nem kapott biztonságosan feloszlatott alap-alakzatot.");
}

static void SaveEditorOverwritesWithBackup()
{
    var directory = Path.Combine(Path.GetTempPath(), $"kaoszrubin-save-editor-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
        var characterService = new CharacterSaveService(Path.Combine(directory, "characters.json"), data);
        var saveService = new GameSaveService(directory, characterService);
        var roster = new CharacterRoster();
        var leader = CreateCharacter("Szerkesztett");
        leader.SetGold(125);
        roster.Add(leader);
        roster.Select(leader);
        roster.Party.SetLeader(leader);
        var path = saveService.Save(new GameSaveData { MainCharacterName = leader.Name }, roster);
        var loaded = saveService.Load(path);
        loaded.Roster.SelectedCharacter!.SetGold(999);
        Assert(InventoryBundleGrantService.TryGrant([loaded.Roster.SelectedCharacter],
                [new InventoryBundleEntry(data.GetItem("T001"), 9),
                 new InventoryBundleEntry(data.GetItem("T002"), 9)], out _),
            "A szerkesztő tesztellátmánya nem fért el.");
        var backup = saveService.Overwrite(loaded);
        var restored = saveService.Load(path);
        Assert(File.Exists(backup) && backup.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) &&
               restored.State.Version == GameSaveFormat.CurrentVersion &&
               restored.Roster.SelectedCharacter is { Gold: 999 } selected &&
               CountBackpack(selected, "T001") == 9 && CountBackpack(selected, "T002") == 9,
            "A szerkesztett mentés, a két kilences köteg vagy a biztonsági másolat hibás.");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }

    static int CountBackpack(LiveCharacter character, string itemId) =>
        Enumerable.Range(0, LiveCharacter.MaximumBackpackItemCount)
            .Where(index => string.Equals(character.Backpack[index]?.Id, itemId,
                StringComparison.OrdinalIgnoreCase))
            .Sum(index => character.GetInventoryItemQuantity(InventorySlotKind.Backpack, index));
}

static void RemotePlayerCanAcknowledgeLevelImage()
{
    var (session, _, companion) = CreateSession();
    var remote = session.RegisterRemotePlayer();
    Assert(session.TryAssignRemoteControl(remote, companion.Id, out var assignmentError), assignmentError);
    session.SetPhase(GameSessionPhase.Paused);
    var command = new AcknowledgeLevelImageCommand(remote, 1, companion.Id, Guid.NewGuid());
    session.Submit(command);
    Assert(session.TryReadCommand(out var accepted) && accepted == command,
        "A session elutasította a vendég pályakép-nyugtázását.");
}

static void InvalidGameSaveVersionsAreRejected()
{
    try
    {
        GameSaveFormat.MigrateToCurrent(new GameSaveData { Version = GameSaveFormat.CurrentVersion + 1 });
        throw new InvalidOperationException("A jövőbeli mentésverzió betöltődött.");
    }
    catch (InvalidOperationException exception)
    {
        Assert(exception.Message.Contains("Nem támogatott mentésverzió", StringComparison.Ordinal),
            $"A jövőbeli mentésverzió hibaüzenete pontatlan: {exception.Message}");
    }

    try
    {
        JsonSerializer.Deserialize<GameSaveData>("{}");
        throw new InvalidOperationException("A verzió nélküli mentés betöltődött.");
    }
    catch (JsonException)
    {
    }
}

static void ClassSpecializationSurvivesSerialization()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var race = data.GetRace("R003");
    var mageClass = data.CharacterClasses.Single(characterClass => characterClass.Id == CharacterClassIds.Mágus);
    var character = new LiveCharacter("Specialista", race, mageClass,
        new PrimaryAbilities(1, 3, 3, 10), 30, 60, 1, 1);
    Assert(character.ChooseSpecialization(ClassSpecializations.MageIllusionist),
        "A mágus nem tudta kiválasztani az Illuzionista specializációt.");
    var roster = new CharacterRoster();
    roster.Add(character);
    var service = new CharacterSaveService(Path.Combine(Path.GetTempPath(), "unused-specialization-save.json"), data);
    var restored = service.Deserialize(service.Serialize(roster)).Characters.Single();
    Assert(restored.SpecializationId == ClassSpecializations.MageIllusionist,
        "A specializáció elveszett a mentési kör után.");
}

static void DisconnectAndReconnectRestoreControl()
{
    var (session, _, companion) = CreateSession();
    var remote = session.RegisterRemotePlayer();
    Assert(session.TryAssignRemoteControl(remote, companion.Id, out var error), error);
    session.MarkPlayerDisconnected(remote);
    Assert(!session.IsHumanControlled(companion.Id), "Disconnect után emberi maradt a vezérlés.");
    session.Submit(new MoveCharacterCommand(remote, 1, companion.Id, Direction.Right));
    Assert(!session.TryReadCommand(out _), "Disconnect után átjutott a vendég parancsa.");
    Assert(session.TryReconnectPlayer(remote), "A reconnect nem találta meg a foglalt karaktert.");
    Assert(session.IsHumanControlled(companion.Id), "Reconnect után nem állt vissza az emberi vezérlés.");
    session.Submit(new MoveCharacterCommand(remote, 2, companion.Id, Direction.Right));
    Assert(session.TryReadCommand(out _), "Reconnect után elutasította az új parancsot.");
}

#if false // A megszüntetett párbaj-állapotgép tesztjei; a csapatharcos lefedettség váltja fel őket.
static void BattleAdvanceRunsOneAction()
{
    var system = CreateBattleSystem(11);
    var player = CreateCharacter("Fighter", vitality: 500);
    var enemy = CreateEnemy(hitPoints: 500, strength: 1);
    var started = system.StartBattle(player, enemy);
    var previousTurnId = started.State.TurnId;
    var step = system.Advance(started.State);
    Assert(step.State.Round == 1, "Egy Advance nem pontosan egy akciót futtatott.");
    Assert(step.State.TurnId == previousTurnId + 1, "A harci turn ID nem növekedett.");
    Assert(step.Entries.Count == 1, "Egy akció nem pontosan egy naplóbejegyzést adott.");
    Assert(!step.IsCompleted, "A nagy HP-jú tesztcsata váratlanul lezárult.");
}

static void PhysicalClassesChooseBattleTactic()
{
    var system = CreateBattleSystem(71);
    var fighter = CreateCharacter("Harcos", characterClassId: CharacterClassIds.Harcos);
    var fighterState = system.StartBattle(fighter, CreateEnemy(100, 1)).State;
    Assert(fighterState.RequiresTacticSelection, "A harcos nem kapott csatakezdő állásválasztást.");
    Assert(!fighterState.TryChooseTactic(BattleTactic.ThiefPoison), "A harcos elfogadott egy tolvaj taktikát.");
    Assert(fighterState.TryChooseTactic(BattleTactic.FighterDefensive) && !fighterState.RequiresTacticSelection,
        "A harcos érvényes állása nem oldotta fel a választást.");

    var thief = CreateCharacter("Tolvaj", characterClassId: CharacterClassIds.Tolvaj);
    var thiefState = system.StartBattle(thief, CreateEnemy(100, 1)).State;
    Assert(thiefState.RequiresTacticSelection && thiefState.TryChooseTactic(BattleTactic.ThiefObserve),
        "A tolvaj nem tudta kiválasztani a csatakezdő megközelítését.");
}

static void BarbarianRageTriggersAfterFiveDamage()
{
    var system = CreateBattleSystem(72);
    var race = new RaceDefinition("R001", "Ember", PrimaryAbilities.Zero);
    var barbarianClass = new CharacterClassDefinition(CharacterClassIds.Barbár, "Barbár",
        PrimaryAbilities.Zero, false, 1.0);
    var barbarian = new LiveCharacter("Barbár", race, barbarianClass,
        new PrimaryAbilities(5, 100, 5, 5), 500, 0, 1, 0);
    var state = system.StartBattle(barbarian, CreateEnemy(1000, 20)).State;
    for (var step = 0; step < 100 && !state.IsBarbarianRaging; step++)
        system.Advance(state);
    Assert(state.IsBarbarianRaging, "A barbár legalább 5 tényleges sebzés után sem került Dühbe.");
    var rageLogs = new List<string>();
    for (var step = 0; step < 6 && state.IsBarbarianRaging; step++)
        rageLogs.AddRange(system.Advance(state).Entries.SelectMany(entry =>
            entry.Details?.Calculation ?? [entry.Message]));
    Assert(rageLogs.Any(log => Enumerable.Range(5, 6).Any(bonus =>
            log.Contains($"🔥 Düh +{bonus}", StringComparison.Ordinal))),
        "A barbár Düh támadása nem kapott 5–10 közötti sebzésbónuszt.");
}

#endif

static void ClassFeatureUpgradesPersistAndAppearOnSheet()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    foreach (var characterClass in data.CharacterClasses)
        Assert(ClassFeatureUpgrades.ForClass(characterClass.Id).Count == 3,
            $"{characterClass.Name} nem pontosan három osztályfejlesztést kapott.");

    var race = data.GetRace("R001");
    var fighterClass = data.CharacterClasses.Single(characterClass => characterClass.Id == CharacterClassIds.Harcos);
    var character = new LiveCharacter("Fejlesztett", race, fighterClass,
        new PrimaryAbilities(7, 8, 9, 10), 40, 0, 1, 0);
    Assert(character.ChooseClassFeatureUpgrade(ClassFeatureUpgrades.FighterPrecise) &&
           character.ChooseClassFeatureUpgrade(ClassFeatureUpgrades.FighterDefensive) &&
           !character.ChooseClassFeatureUpgrade(ClassFeatureUpgrades.FighterPowerful) &&
           !character.ChooseClassFeatureUpgrade(ClassFeatureUpgrades.BarbarianWildRage),
        "Az osztályfejlesztések darabszám- vagy osztálykorlátozása hibás.");

    var service = new CharacterSaveService(Path.Combine(Path.GetTempPath(), "unused-upgrade-save.json"), data);
    var restored = service.DeserializeCharacter(service.SerializeCharacter(character));
    Assert(restored.ClassFeatureUpgrades.Select(upgrade => upgrade.Id).SequenceEqual(
            new[] { ClassFeatureUpgrades.FighterPrecise, ClassFeatureUpgrades.FighterDefensive }),
        "Az osztályfejlesztések elvesztek a mentési kör után.");

    var lines = CharacterSheetPanel.Build(restored, data.ExperienceByLevel, 1, 0, 12);
    Assert(lines.Single(line => line.Row == 4).Text.Contains("💪7", StringComparison.Ordinal) &&
           lines.Single(line => line.Row == 4).Text.Contains("💖9", StringComparison.Ordinal) &&
           lines.Single(line => line.Row == 4).Text.Contains("🧠10", StringComparison.Ordinal) &&
           lines.Single(line => line.Row == 13).Text == "OSZTÁLYFEJLESZTÉSEK" &&
           lines.Single(line => line.Row == 14).Text.Contains("Kimért pontosság", StringComparison.Ordinal) &&
           lines.Single(line => line.Row == 15).Text.Contains("Áthatolhatatlan", StringComparison.Ordinal),
        "A tömör képességsor vagy az osztályfejlesztések karakterlap-blokkja hibás.");
}

static void AbilityIncreasesAreCappedAndPersisted()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var character = new LiveCharacter("Képességes", data.GetRace("R001"),
        data.CharacterClasses.Single(characterClass => characterClass.Id == CharacterClassIds.Harcos),
        new PrimaryAbilities(12, 13, 8, 9), data.GetMinimumVitality(8) + 1, 0, 1, 0);
    Assert(character.TryIncreaseAbility("STR") && character.Abilities.Strength == 13,
        "Az Erő képességpontja nem növelte 13-ra az értéket.");
    Assert(!character.TryIncreaseAbility("STR") && !character.TryIncreaseAbility("DEX") &&
           character.Abilities.Strength == 13 && character.Abilities.Dexterity == 13,
        "A képességpont túllépte a 13-as maximumot.");
    var oldVitalityBase = data.GetMinimumVitality(character.Abilities.Health);
    Assert(character.TryIncreaseAbility("HEA") && character.AbilityIncreasesClaimed == 2,
        "A képességpontok elköltött számlálója hibás.");
    character.ApplyAbilityResourceIncrease(data.GetMinimumVitality(character.Abilities.Health) - oldVitalityBase, 0);

    var service = new CharacterSaveService(Path.Combine(Path.GetTempPath(), "unused-ability-save.json"), data);
    var restored = service.DeserializeCharacter(service.SerializeCharacter(character));
    Assert(restored.Abilities == character.Abilities && restored.AbilityIncreasesClaimed == 2 &&
           restored.MaximumVitality == character.MaximumVitality,
        "A képességnövelések vagy az elköltött pontok száma elveszett a mentési kör után.");
}

static void WeaponProficienciesAreLimitedEffectiveAndPersisted()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    Assert(WeaponProficiencyProgression.MilestonesFor(CharacterClassIds.Harcos).SequenceEqual(new[] { 1, 7, 17, 27 }) &&
           WeaponProficiencyProgression.MilestonesFor(CharacterClassIds.Tolvaj).SequenceEqual(new[] { 7, 17 }),
        "A harci és nem harci osztályok fegyverjártassági mérföldkövei hibásak.");
    var fighter = CreateCharacter("Fegyvermester", characterClassId: CharacterClassIds.Harcos);
    var sword = data.GetWeapon("W004");
    Assert(WeaponFamilies.ForWeapon(sword) == WeaponFamilies.Sword &&
           WeaponFamilies.ForWeapon(data.GetWeapon("LW004")) == WeaponFamilies.Sword &&
           WeaponFamilies.All.Count == 7,
        "A normál vagy legendás fegyver családbesorolása hibás.");
    Assert(fighter.EquipWeapon(0, sword), "A tesztkarakter nem tudta felszerelni a hosszú kardot.");
    var system = CreateBattleSystem(2201);
    var enemy = CreateEnemy(100, 1, speed: 8);
    var before = system.EstimatePlayerHitChance(fighter, enemy, BattleTactic.FighterDefensive);
    Assert(fighter.TryAdvanceWeaponProficiency(WeaponFamilies.Sword), "A Kard Jártas fok nem volt választható.");
    var after = system.EstimatePlayerHitChance(fighter, enemy, BattleTactic.FighterDefensive);
    Assert(after == before + 5, "A Kard Jártas fok nem adott +1, azaz 5 százalékpont találati esélyt.");
    Assert(fighter.TryAdvanceWeaponProficiency(WeaponFamilies.Sword) &&
           fighter.TryAdvanceWeaponProficiency(WeaponFamilies.Shield) &&
           !fighter.TryAdvanceWeaponProficiency(WeaponFamilies.Dagger) &&
           fighter.TryAdvanceWeaponProficiency(WeaponFamilies.Shield) &&
           fighter.WeaponProficiencyAdvances == 4,
        "A két családos vagy kétfokozatú fegyverjártassági korlát hibás.");

    var service = new CharacterSaveService(Path.Combine(Path.GetTempPath(), "unused-proficiency-save.json"), data);
    var restored = service.DeserializeCharacter(service.SerializeCharacter(fighter));
    Assert(restored.WeaponProficiencyRankFor(WeaponFamilies.Sword) == WeaponProficiencyRank.Master &&
           restored.WeaponProficiencyRankFor(WeaponFamilies.Shield) == WeaponProficiencyRank.Master,
        "A fegyverjártasságok elvesztek a mentési kör után.");
    var lines = CharacterSheetPanel.Build(restored, data.ExperienceByLevel, 1, 0, 12);
    Assert(lines.Single(line => line.Row == 10).Text.Contains("⚔️M", StringComparison.Ordinal) &&
           lines.Single(line => line.Row == 10).Text.Contains("🛡️M", StringComparison.Ordinal) &&
           lines.Single(line => line.Row == 5).Text.Contains("❤️", StringComparison.Ordinal) &&
           !lines.Single(line => line.Row == 5).Text.Contains("🔷", StringComparison.Ordinal),
        "A fegyverjártasság- vagy az összevont erőforrássor hibás a karakterlapon.");
    var inspection = ItemInspectionFormatter.Format(sword, data, weaponProficiencies:
        restored.WeaponProficiencies.ToDictionary(proficiency => proficiency.FamilyId,
            proficiency => (int)proficiency.Rank));
    Assert(inspection.Text.Contains("Kard", StringComparison.Ordinal) &&
           inspection.Text.Contains("Mester", StringComparison.Ordinal),
        "A fegyver részletes nézete nem mutatja a családot és a jártassági fokot.");
}

#if false // A megszüntetett párbaj-állapotgép tesztjei.
static void KnightProtectionTransfersThirdOfFirstHit()
{
    var unprotectedSystem = CreateBattleSystem(73);
    var protectedSystem = CreateBattleSystem(73);
    var unprotected = CreateCharacter("Védtelen", vitality: 500, characterClassId: CharacterClassIds.Pap);
    var protectedCharacter = CreateCharacter("Védett", vitality: 500, characterClassId: CharacterClassIds.Pap);
    var protector = CreateCharacter("Őrszem", vitality: 500, characterClassId: CharacterClassIds.Lovag);
    var unprotectedState = unprotectedSystem.StartBattle(unprotected, CreateEnemy(1000, 20)).State;
    var protectedState = protectedSystem.StartBattle(protectedCharacter, CreateEnemy(1000, 20)).State;
    protectedState.SetKnightProtection(protector);
    var protectedEntries = new List<BattleLogEntry>();
    for (var step = 0; step < 100 && unprotected.CurrentVitality == 500; step++)
    {
        unprotectedSystem.Advance(unprotectedState);
        protectedEntries.AddRange(protectedSystem.Advance(protectedState).Entries);
    }
    var fullDamage = 500 - unprotected.CurrentVitality;
    var protectedDamage = 500 - protectedCharacter.CurrentVitality;
    var protectorDamage = 500 - protector.CurrentVitality;
    Assert(fullDamage > 0 && protectedDamage == 0 && protectorDamage == (fullDamage + 2) / 3,
        $"A lovagi védelem hibásan osztotta el a sebzést: társ {protectedDamage}, lovag {protectorDamage}, eredeti {fullDamage}.");
    var protection = protectedEntries.FirstOrDefault(entry =>
        entry.Message.Contains("🛡️ Őrszem közbelépett", StringComparison.Ordinal));
    Assert(protection is not null &&
           !protection.Message.Contains("teljes", StringComparison.OrdinalIgnoreCase) &&
           protection.Details?.Calculation.Any(line =>
               line.Contains("teljes", StringComparison.OrdinalIgnoreCase) &&
               line.Contains("harmada", StringComparison.OrdinalIgnoreCase)) == true,
        "A lovagi közbelépés rövid naplója vagy részletes paneladata hibás.");
}

static void BattleCanWaitForPlayerAction()
{
    var system = CreateBattleSystem(22);
    var player = CreateCharacter("Fighter", vitality: 500);
    var enemy = CreateEnemy(hitPoints: 100, strength: 1);
    var state = system.StartBattle(player, enemy).State;
    while (!state.IsPlayerTurn && !state.IsCompleted) system.Advance(state);
    Assert(!state.IsCompleted && state.IsPlayerTurn, "A teszt nem jutott el játékosakcióig.");
    var turnId = state.TurnId;
    Assert(state.Round <= 1, "A csata input nélkül túlhaladt a játékos körén.");
    var step = system.Advance(state, new BattlePlayerAction("Hálózatról érkezett varázslat.",
        DamageToEnemy: 100));
    Assert(step.IsCompleted && step.Result?.PlayerWon == true, "A beadott játékosakció nem zárta le a csatát.");
    Assert(state.TurnId == turnId + 1, "Nem a várt hálózati turn oldódott fel.");
}

static void SupportCanFinishBattleBeforePlayerAction()
{
    var system = CreateBattleSystem(33);
    var player = CreateCharacter("Fighter", vitality: 100);
    var enemy = CreateEnemy(hitPoints: 20, strength: 1);
    var state = system.StartBattle(player, enemy).State;
    var step = system.Advance(state, supportDamage: 20);
    Assert(step.IsCompleted && enemy.CurrentHitPoints == 0, "A támogatói sebzés nem zárta le a csatát.");
    Assert(step.Entries.Single().Message.Contains("támogató", StringComparison.Ordinal),
        "A támogatói győzelem nem kapott saját eseményt.");
}

static void ResolveUsesStateMachineAdapter()
{
    var system = CreateBattleSystem(44);
    var player = CreateCharacter("Fighter", vitality: 100);
    var enemy = CreateEnemy(hitPoints: 50, strength: 1);
    var entries = new List<BattleLogEntry>();
    var actionRequests = 0;
    var result = system.Resolve(player, enemy, entries.Add, () =>
    {
        actionRequests++;
        return new BattlePlayerAction("Adapter-akció.", DamageToEnemy: 50);
    });
    Assert(result.PlayerWon && enemy.CurrentHitPoints == 0, "A kompatibilitási Resolve nem fejezte be a csatát.");
    Assert(actionRequests == 1, "A Resolve nem egyszer kérte be a győztes játékosakciót.");
    Assert(entries.Count >= 2, "A kezdő- és akcióesemények nem jutottak el a callbackhez.");
}

static void ResolveSkipsActionAfterSupportVictory()
{
    var system = CreateBattleSystem(55);
    var player = CreateCharacter("Fighter", vitality: 100);
    var enemy = CreateEnemy(hitPoints: 10, strength: 1);
    var actionRequests = 0;
    var result = system.Resolve(player, enemy, _ => { }, () =>
    {
        actionRequests++;
        return null;
    }, () => 10);
    Assert(result.PlayerWon && actionRequests == 0,
        "A támogatói győzelem után a kompatibilitási adapter még játékosakciót kért.");
}

#endif

static void BattleCommandRequiresCurrentPrompt()
{
    var (session, leader, _) = CreateSession();
    var battleId = BattleId.New();
    var events = CollectEvents(session);
    session.SetBattlePrompt(battleId, 7, leader.Id);
    Assert(events.OfType<BattlePromptEvent>().Any(prompt => prompt.BattleId == battleId && prompt.TurnId == 7),
        "A session nem publikálta a harci promptot.");
    session.Submit(new BattleActionCommand(session.HostPlayerId, 1, leader.Id, battleId, 6,
        BattleActionKind.PhysicalAttack));
    Assert(!session.TryReadCommand(out _), "A lejárt TurnId harci parancsa átjutott.");
    session.Submit(new BattleActionCommand(session.HostPlayerId, 2, leader.Id, battleId, 7,
        BattleActionKind.PhysicalAttack));
    Assert(session.TryReadCommand(out var command) && command is BattleActionCommand,
        "Az aktív BattleId/TurnId érvényes parancsát elutasította a session.");
    session.EndBattle(battleId);
    Assert(events.OfType<BattleEndedEvent>().Any(ended => ended.BattleId == battleId),
        "A session nem publikálta a csata végét.");
}

static void RemoteBattlePromptRequiresCharacterOwner()
{
    var (session, leader, companion) = CreateSession();
    var events = CollectEvents(session);
    var remote = session.RegisterRemotePlayer();
    Assert(session.TryAssignRemoteControl(remote, companion.Id, out var assignmentError), assignmentError);
    var battleId = BattleId.New();
    session.SetBattlePrompt(battleId, 1, companion.Id, [BattleActionKind.PhysicalAttack]);
    session.Submit(new BattleActionCommand(session.HostPlayerId, 1, companion.Id, battleId, 1,
        BattleActionKind.PhysicalAttack));
    Assert(!session.TryReadCommand(out _), "A host feloldhatta a távoli karakter harci promptját.");
    session.Submit(new BattleActionCommand(remote, 1, companion.Id, battleId, 1,
        BattleActionKind.PhysicalAttack));
    Assert(session.TryReadCommand(out var accepted) && accepted.SenderId == remote,
        "A távoli karakter gazdájának érvényes harci akcióját elutasította a session.");
    session.RejectExecutedCommand(accepted, "Szemantikai próbahiba.");
    Assert(events.OfType<GameCommandRejectedEvent>().Any(rejected => rejected.PlayerId == remote &&
            rejected.CommandId == accepted.CommandId && rejected.Reason == "Szemantikai próbahiba."),
        "A végrehajtási réteg szemantikai elutasítása nem került vissza a parancs gazdájához.");

    companion.ReceiveDamage(companion.CurrentVitality);
    session.ReleaseCharacterControl(companion.Id);
    Assert(!session.IsHumanControlled(companion.Id) && session.GetAvailableRemoteCharacters()
            .All(option => option.CharacterId != companion.Id),
        "A halott távoli karakter vezérlése nem szűnt meg, vagy újra kiválasztható maradt.");
    Assert(session.TryReconnectPlayer(remote),
        "A karakterét elvesztő megfigyelő reconnect-tokenje nem maradt érvényes.");
    session.EndBattle(battleId);
}

static void EnemyTurnAdvanceCommandIsAccepted()
{
    var (session, leader, _) = CreateSession();
    var battleId = BattleId.New();
    session.SetBattlePrompt(battleId, 4, leader.Id, [BattleActionKind.AdvanceEnemyTurn]);
    Assert(session.Submit(new BattleActionCommand(session.HostPlayerId, 1, leader.Id, battleId, 4,
            BattleActionKind.AdvanceEnemyTurn)),
        "Az ellenfél körét léptető Space-parancsot elutasította a session.");
    Assert(session.TryReadCommand(out var command) && command is BattleActionCommand
        { Action: BattleActionKind.AdvanceEnemyTurn },
        "Az ellenfél körét léptető parancs nem került a végrehajtási sorba.");
}

static void SpellBattleCommandIsAccepted()
{
    var (session, leader, _) = CreateSession();
    var battleId = BattleId.New();
    session.SetBattlePrompt(battleId, 3, leader.Id,
        [BattleActionKind.PhysicalAttack, BattleActionKind.CastSpell]);
    var target = new Position(4, 2);
    session.Submit(new BattleActionCommand(session.HostPlayerId, 1, leader.Id, battleId, 3,
        BattleActionKind.CastSpell, "SP-TEST", 1, target));
    Assert(session.TryReadCommand(out var command) && command is BattleActionCommand
        {
            Action: BattleActionKind.CastSpell,
            SpellId: "SP-TEST",
            CastingItemSlotIndex: 1,
            Target: { } acceptedTarget
        } && acceptedTarget == target, "A teljes szemantikus varázslat-commandot elutasította a session.");
}

static void MalformedSpellBattleCommandIsRejected()
{
    var (session, leader, _) = CreateSession();
    var battleId = BattleId.New();
    var events = CollectEvents(session);
    session.SetBattlePrompt(battleId, 1, leader.Id, [BattleActionKind.CastSpell]);
    session.Submit(new BattleActionCommand(session.HostPlayerId, 1, leader.Id, battleId, 1,
        BattleActionKind.CastSpell, SpellId: "SP-TEST"));
    Assert(!session.TryReadCommand(out _), "A célpont nélküli varázslat-command átjutott.");
    Assert(events.OfType<GameCommandRejectedEvent>().Any(rejected => rejected.CommandId == 1),
        "A hiányos varázslat-command elutasításáról nem keletkezett esemény.");
}

static void DisallowedBattleActionIsRejected()
{
    var (session, leader, _) = CreateSession();
    var battleId = BattleId.New();
    session.SetBattlePrompt(battleId, 1, leader.Id, [BattleActionKind.PhysicalAttack]);
    session.Submit(new BattleActionCommand(session.HostPlayerId, 1, leader.Id, battleId, 1,
        BattleActionKind.TurnUndead));
    Assert(!session.TryReadCommand(out _), "A promptban nem szereplő halottűzés átjutott.");
}

static void BattleTacticCommandIsAccepted()
{
    var (session, leader, _) = CreateSession();
    var battleId = BattleId.New();
    session.SetBattlePrompt(battleId, 1, leader.Id,
        [BattleActionKind.FighterPrecise, BattleActionKind.FighterPowerful, BattleActionKind.FighterDefensive]);
    Assert(session.Submit(new BattleActionCommand(session.HostPlayerId, 1, leader.Id, battleId, 1,
            BattleActionKind.FighterPowerful)),
        "A session elutasította az engedélyezett harcos taktikát.");
    Assert(session.TryReadCommand(out var command) && command is BattleActionCommand
        { Action: BattleActionKind.FighterPowerful }, "A taktikai parancs nem került a feldolgozási sorba.");
}
static void SessionSnapshotRoundTripsThroughJson()
{
    var (session, leader, companion) = CreateSession();
    leader.SetGold(777);
    companion.SetGold(999);
    var positions = new Dictionary<CharacterId, Position>
    {
        [leader.Id] = new Position(2, 2),
        [companion.Id] = new Position(3, 2)
    };
    var snapshot = session.CreateSnapshot(new SessionSnapshotContext(4, "Tesztlabirintus", positions)) with
    {
        Activities = [new SessionActivitySnapshot(1, SessionActivityKind.Spell,
            "A host térképi varázslatot használt.", ConsoleColor.Magenta)],
        Sounds = [new SessionSoundSnapshot(1, SoundEffect.OffensiveSpell, [companion.Id])],
        LevelImage = new LevelImageSnapshot(Guid.NewGuid(), "Tesztlabirintus", "teszt.png",
            [session.HostPlayerId]),
        InnDeparture = new InnDepartureSnapshot("A csapat elhagyja a fogadót."),
        AdHocConversation = new AdHocConversationSnapshot(Guid.NewGuid(), "Elira", "Elf", "Tolvaj",
            ["Elira: Emlékszem az erdőre."], "Hiányzik az otthonod?", ["Igen.", "Beszélj másról."]),
        Formation = PartyFormationRules.CreateDefault([leader.Id, companion.Id], leader.Id,
            Direction.Down, PartyFormationState.Locked),
        LeaderDecisionMessage = "Várunk a vezető döntéseire…",
        LeaderDecisionTitle = "Alakzatszerkesztő"
    };
    var json = JsonSerializer.Serialize(snapshot);
    var restored = JsonSerializer.Deserialize<SessionSnapshot>(json);
    Assert(restored is not null && restored.ProtocolVersion == SessionProtocol.Version &&
           restored.Phase == GameSessionPhase.Exploration && restored.Party.Count == 2 &&
           restored.Activities is [{ Kind: SessionActivityKind.Spell }] &&
           restored.LevelImage is { FileName: "teszt.png", AcknowledgedPlayerIds.Count: 1 } &&
           restored.InnDeparture is { Message: "A csapat elhagyja a fogadót." } &&
           restored.AdHocConversation is { CharacterName: "Elira", Choices.Count: 2 } &&
           restored.Formation is { Facing: Direction.Down, State: PartyFormationState.Locked } &&
           restored.LeaderDecisionMessage == "Várunk a vezető döntéseire…" &&
           restored.LeaderDecisionTitle == "Alakzatszerkesztő" &&
           restored.Sounds is [{ Sequence: 1, Effect: SoundEffect.OffensiveSpell,
               ListenerCharacterIds: [{ } listener] }] && listener == companion.Id &&
           restored.PartyGold == 777 && restored.Party.All(character => character.Gold == 777) &&
           restored.Sounds[0].IsAudibleTo(companion.Id) && !restored.Sounds[0].IsAudibleTo(leader.Id) &&
           restored.Party.Single(character => character.CharacterId == companion.Id).Position == new Position(3, 2),
        "A session snapshot JSON round-trip közben megváltozott.");
    var next = session.CreateSnapshot(new SessionSnapshotContext(4, "Tesztlabirintus", positions));
    Assert(next.SnapshotSequence == snapshot.SnapshotSequence + 1,
        "A publikált snapshot sorszáma nem monoton nő.");
}

static void RemotePlayerCanStepOntoTreasureChest()
{
    var maze = new Maze(7, 7);
    var start = new Position(2, 3);
    var chestPosition = new Position(3, 3);
    maze.Carve(start);
    maze.Carve(chestPosition);
    var member = new PartyMemberAvatar(start, CreateCharacter("Vendég"));
    var chest = new TreasureChest(chestPosition, 25);
    maze.AddPartyMember(member);
    maze.AddTreasureChest(chest);

    Assert(!maze.TryMovePartyMember(member, chestPosition, maze.Entrance),
        "Az NPC partitárs önállóan felvehetne kincsesládát.");
    Assert(maze.TryMovePartyMember(member, chestPosition, maze.Entrance, allowTreasureChest: true) &&
           member.Position == chestPosition && maze.GetTreasureChestAt(chestPosition) == chest,
        "Az ember által vezérelt vendéget a láda mezője blokkolta.");
}

static void GuestSeesOtherPlayersBlockingWindows()
{
    var (session, leader, companion) = CreateSession();
    var remote = session.RegisterRemotePlayer();
    Assert(session.TryAssignRemoteControl(remote, companion.Id, out var assignmentError), assignmentError);
    var maze = new Maze(7, 7);
    maze.Carve(maze.Entrance);
    var fog = new FogOfWar(7, 7, 0);
    fog.RevealFrom(maze, maze.Entrance);
    var snapshot = session.CreateSnapshot(new SessionSnapshotContext(1, "Várakozási próba",
        new Dictionary<CharacterId, Position>
        {
            [leader.Id] = maze.Entrance,
            [companion.Id] = maze.Entrance
        }, World: WorldSnapshotProjector.Create(maze, fog))) with
    {
        Phase = GameSessionPhase.Paused,
        LevelUpPrompt = new LevelUpPromptSnapshot(Guid.NewGuid(), leader.Id, leader.Name,
            LevelUpPromptKind.PerkChoice, 1, 2, 5, 0, [], "Válassz tehetséget.")
    };
    var personalized = new SessionReplicationPublisher().CreateFrame(remote, snapshot).Session;
    Assert(personalized.LevelUpPrompt is null &&
           personalized.LeaderDecisionTitle == $"Szintlépés — {leader.Name}" &&
           personalized.LeaderDecisionMessage?.Contains(leader.Name, StringComparison.Ordinal) == true &&
           CoopGuestScreen.BuildHostWindowWaitingLines(personalized.LeaderDecisionTitle,
               personalized.LeaderDecisionMessage).Any(line =>
                   line.Text == "❖  Várakozás a másik játékosra…  ❖"),
        "A más karakter szintlépési ablaka eltűnt a vendég elől várakozási értesítés nélkül.");
}

static void PartyMemberCanBeRestoredOnEntranceOrExit()
{
    var maze = new Maze(7, 7);
    maze.Carve(maze.Entrance);
    maze.Carve(maze.Exit);

    var entranceMember = new PartyMemberAvatar(maze.Entrance, CreateCharacter("Bejárati társ"));
    var exitMember = new PartyMemberAvatar(maze.Exit, CreateCharacter("Kijárati társ"));
    maze.AddPartyMember(entranceMember);
    maze.AddPartyMember(exitMember);

    Assert(maze.GetPartyMemberAt(maze.Entrance) == entranceMember &&
           maze.GetPartyMemberAt(maze.Exit) == exitMember,
        "A mentésből visszaállított partitársat a bejárat vagy a kijárat elutasította.");
}

static void SessionActivityCanTargetCharacter()
{
    var first = new CharacterId(Guid.NewGuid());
    var second = new CharacterId(Guid.NewGuid());
    var targeted = new SessionActivitySnapshot(1, SessionActivityKind.System, "Keresési eredmény",
        ConsoleColor.Yellow, [first]);
    var shared = new SessionActivitySnapshot(2, SessionActivityKind.Battle, "Közös esemény", ConsoleColor.Red);
    Assert(targeted.IsVisibleTo(first) && !targeted.IsVisibleTo(second) &&
           shared.IsVisibleTo(first) && shared.IsVisibleTo(second),
        "A karakterhez címzett session-aktivitás láthatósága hibás.");
}

static void InnSnapshotCarriesSharedRumors()
{
    var completionId = Guid.NewGuid();
    var snapshot = new InnSnapshot(3, 120, [],
        [new InnRumorSnapshot("Úti hír", ["Ugyanazt hallja a host és a vendég."], ConsoleColor.Yellow)],
        [new InnTransactionSnapshot(1, InnTransactionKind.Purchase, "Vendég", "Kard", 50, "Vendég")],
        [new InnSellPriceSnapshot("W-TEST", 25)],
        [new InnMenuOptionSnapshot(InnMenuOptionKind.Rest, "Pihenés", "Közös pihenés", LeaderOnly: true),
         new InnMenuOptionSnapshot(InnMenuOptionKind.Market, "Kereskedő", "Vétel és eladás", InnVendorKind.Market)],
        "A kovácsmester ma jelen van.", 2, 7,
        new LevelCompletionSnapshot(completionId, 2, 100,
            [new LevelCompletionCharacterSnapshot("Host", ConsoleColor.Green, 200, 1, 2, 12, 15, 0, 0, false)],
            [new LevelCompletionFallenSnapshot("Elesett", "Harcos")]), "A Törött Kard", 2);
    var restored = JsonSerializer.Deserialize<InnSnapshot>(JsonSerializer.Serialize(snapshot));
    Assert(restored is { Rumors.Count: 1 } && restored.Rumors[0].Title == "Úti hír" &&
           restored.Rumors[0].Lines.SequenceEqual(snapshot.Rumors[0].Lines) &&
           restored.Rumors[0].Color == ConsoleColor.Yellow && restored.Transactions is [{ ActorName: "Vendég" }] &&
           restored.SellPrices is [{ Price: 25 }] && restored.MenuOptions is [{ LeaderOnly: true }, ..] &&
           restored.MenuOptions[1].Vendor == InnVendorKind.Market && restored.PartyCount == 2 &&
           restored.PartyFreeBackpackSlots == 7 && restored.LevelCompletion?.CompletionId == completionId &&
           restored.LevelCompletion.FallenCharacters is [{ Name: "Elesett" }] &&
           restored.InnName == "A Törött Kard" && restored.MazeLevel == 2,
        "A fogadó közös menü- vagy pályavégi állapota nem maradt meg a snapshot JSON round-trip során.");
}

static void NeutralWorldNpcIsPassable()
{
    static (Maze Maze, Position Start, Position NpcPosition) CreateMazeWithNeutralNpc()
    {
        var maze = new Maze(7, 7);
        var start = new Position(2, 3);
        var npcPosition = new Position(3, 3);
        maze.Carve(start);
        maze.Carve(npcPosition);
        maze.AddWorldNpc(new WorldNpc(npcPosition, "NPC-PASSABLE", CreateCharacter("Semleges"),
            NpcDisposition.Neutral, false, false, "Utad engedem."));
        return (maze, start, npcPosition);
    }

    var enemySetup = CreateMazeWithNeutralNpc();
    var enemy = CreateEnemyAt(enemySetup.Start, "PASSABLE-ENEMY");
    enemySetup.Maze.AddEnemy(enemy);
    Assert(enemySetup.Maze.TryMoveEnemy(enemy, enemySetup.NpcPosition) &&
           enemy.Position == enemySetup.NpcPosition,
        "A szörnyet blokkolta a semleges NPC.");

    var partySetup = CreateMazeWithNeutralNpc();
    var member = new PartyMemberAvatar(partySetup.Start, CreateCharacter("Mozgó NPC"));
    partySetup.Maze.AddPartyMember(member);
    Assert(partySetup.Maze.TryMovePartyMember(member, partySetup.NpcPosition, partySetup.Maze.Entrance) &&
           member.Position == partySetup.NpcPosition,
        "A mozgó partitársat blokkolta a semleges NPC.");
}

static void ReturnExpeditionPopulationIsLimited()
{
    Assert(ReturnExpeditionRules.TargetNormalEnemyCount(10) == 3 &&
           ReturnExpeditionRules.TargetNormalEnemyCount(11) == 4 &&
           ReturnExpeditionRules.TargetNormalEnemyCount(1) == 1 &&
           ReturnExpeditionRules.AdditionalEnemiesNeeded(10, 1) == 2 &&
           ReturnExpeditionRules.AdditionalEnemiesNeeded(10, 4) == 0,
        "A visszatérő expedíció nem az eredeti normál szörnyállomány 30%-ára tölt vissza.");
}

static void RemotePlayerCanAcknowledgeRest()
{
    var (session, _, companion) = CreateSession();
    var remote = session.RegisterRemotePlayer();
    Assert(session.TryAssignRemoteControl(remote, companion.Id, out var assignmentError), assignmentError);
    session.SetPhase(GameSessionPhase.Paused);
    var command = new AcknowledgeRestCommand(remote, 1, companion.Id, Guid.NewGuid());
    session.Submit(command);
    Assert(session.TryReadCommand(out var accepted) && accepted == command,
        "A session elutasította a vendég pihenési nyugtázását.");
    Assert(CoopProtocolJson.Decode(CoopProtocolJson.Encode(command)) is AcknowledgeRestCommand decoded &&
           decoded == command, "A pihenési nyugtázás nem írható körbe a hálózati protokollon.");
}

static void InnNamesAndRumorsLoadFromCsv()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    Assert(data.InnNames.Count == 26 && data.InnNames.Contains("A Törött Kard") &&
           data.InnNames.Contains("A Vándor Pihenője"),
        "A fogadónév-halmaz nem megfelelően töltődött be a CSV-ből.");
    Assert(data.InnRumors.Count == 50 &&
           data.InnRumors.Single(rumor => rumor.Id == "PL001").Name.Contains(
               "Aki válaszol neki, azt többé nem látják.", StringComparison.Ordinal),
        "A hangulatpletykák vagy a szövegükben lévő vesszők nem megfelelően töltődtek be a CSV-ből.");
    Assert(data.Traps.Count == 8 && data.GetTrap("TR001").Effect == TrapEffect.Damage &&
           data.GetTrap("TR001").DetectionExperience == 25 &&
           data.GetTrap("TR001").DisarmExperience == 75 &&
           data.GetTrap("TR002").Effect == TrapEffect.Poison && data.GetTrap("TR003").Effect == TrapEffect.Alert &&
           data.GetTrap("TR007").MinimumLevel == 18 && data.GetTrap("TR007").DisarmDifficulty == 15 &&
           data.GetTrap("TR007").DetectionExperience == 200 &&
           data.GetTrap("TR007").DisarmExperience == 600 &&
           data.GetTrap("TR008").Effect == TrapEffect.Darkness &&
           data.GetItem(MiscItemIds.Torch) is { Effect: ConsumableEffect.Vision, EffectValue: 2 },
        "A csapdadefiníciók nem megfelelően töltődtek be a CSV-ből.");
}

static void TrapConfigurationScalesByMazeLevel()
{
    var first = MazeLevelConfigurations.Get(1);
    var middle = MazeLevelConfigurations.Get(10);
    var final = MazeLevelConfigurations.Get(MazeLevelConfigurations.FinalLevel);
    Assert(first.TrapCount == new IntRange(2, 5) && first.TrapIds.SequenceEqual(["TR001"]),
        "Az első szint csapdakonfigurációja nem kezdőbarát.");
    Assert(middle.TrapCount == new IntRange(3, 6) && middle.TrapIds.Contains("TR005") &&
           middle.TrapIds.Contains("TR008") && !middle.TrapIds.Contains("TR006"),
        "A középső szintek csapdakonfigurációja nem megfelelően nehezedik.");
    Assert(final.TrapCount == new IntRange(4, 8) && final.TrapIds.Contains("TR007") &&
           !final.TrapIds.Contains("TR001"),
        "A végső szintek nem a legnehezebb csapdakészletet használják.");
    Assert(first.VisionModifier == 0 && MazeLevelConfigurations.Get(5).VisionModifier == -1 &&
           MazeLevelConfigurations.Get(9).VisionModifier == -2,
        "Az extra sötét pályák látótávmódosítója hibás.");
}

static void SavedTrapCanShareEnemyPosition()
{
    const int size = 7;
    var overlap = new Position(3, 2);
    var entrance = new Position(2, 2);
    var exit = new Position(5, 5);
    var tiles = Enumerable.Repeat(Maze.Wall.Value, size * size).ToList();
    void SetTile(Position position, Rune tile) => tiles[position.Y * size + position.X] = tile.Value;
    SetTile(entrance, Maze.Floor);
    SetTile(overlap, Maze.Floor);
    SetTile(exit, Maze.Floor);

    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var character = CreateCharacter("Betöltő");
    var roster = new CharacterRoster();
    roster.Add(character);
    roster.Select(character);
    var enemy = data.Enemies[0];
    var trap = data.Traps[0];
    var state = new GameSaveData
    {
        MazeLevel = 1,
        PlayerPosition = entrance,
        Maze = new MazeSaveData
        {
            Width = size,
            Height = size,
            WallCodePoint = Maze.Wall.Value,
            TileCodePoints = tiles,
            Exit = exit,
            Enemies = [new EnemySaveData(overlap, enemy.Id, enemy.HitPoints ?? 1)],
            Traps = [new TrapSaveData(overlap, trap.Id, TrapState.Hidden, false, 0)]
        }
    };

    var restored = new GameStateMapper(data, roster, character).Restore(state);
    Assert(restored.Maze.GetEnemyAt(overlap) is not null &&
           restored.Maze.GetTrapAt(overlap)?.Definition.Id == trap.Id,
        "A betöltés nem őrizte meg az egy mezőn álló szörnyet és csapdát.");
}

static void CursedLootChanceIsConfiguredPerMazeLevel()
{
    Assert(MazeLevelConfigurations.Get(1).ItemCurseChancePercent == 8 &&
           MazeLevelConfigurations.Get(9).ItemCurseChancePercent == 30,
        "Az alapértelmezett vagy a korábbi elátkozott sírkamra-esély megváltozott.");
    Assert(MazeLevelConfigurations.Get(7).ItemCurseChancePercent == 15 &&
           MazeLevelConfigurations.Get(12).ItemCurseChancePercent == 15 &&
           MazeLevelConfigurations.Get(16).ItemCurseChancePercent == 20 &&
           MazeLevelConfigurations.Get(18).ItemCurseChancePercent == 25 &&
           MazeLevelConfigurations.Get(19).ItemCurseChancePercent == 25,
        "A kiemelten veszélyes pályák tárgyátok-esélyei nem a konfigurációból érkeznek.");
    Assert(QuestLocationConfigurations.Get(QuestLocationConfigurations.RodericMalrec)
               .ItemCurseChancePercent == 8,
        "A küldetéshelyszínek nem öröklik az alapértelmezett tárgyátok-esélyt.");
}

static void LevelImageFileNamesAreNormalized()
{
    Assert(ImageViewer.FileNameForLevel("Patkányjáratok") == "patkanyjaratok.png" &&
           ImageViewer.FileNameForLevel("A holtak katakombái") == "aholtakkatakombai.png" &&
           ImageViewer.FileNameForLevel("A démoni sík: Vértrónus") == "ademonisikvertronus.png" &&
           ImageViewer.FileNameForLevel("A Káoszrubin rejtekhelye") == "akaoszrubinrejtekhelye.png",
        "A pályakép fájlneve nem kisbetűs, egybeírt és ékezetmentes.");
}

static void TrapVisibilityFollowsDiscoveryState()
{
    var maze = new Maze(7, 7);
    var position = new Position(3, 2);
    maze.Carve(position);
    var definition = new TrapDefinition("TR-TEST", "Tesztcsapda", new Rune('⌄'), TrapEffect.Damage,
        1, 7, 7, 3, 7, 0, 25, 75, "Teszt.");
    var trap = new MazeTrap(position, definition);
    maze.AddTrap(trap);
    var fog = new FogOfWar(maze.Width, maze.Height, 2);
    fog.RevealFrom(maze, maze.Entrance);

    var hidden = WorldSnapshotProjector.Create(maze, fog).RevealedCells.Single(cell => cell.Position == position);
    Assert(hidden.TileCodePoint == Maze.Floor.Value,
        "A rejtett csapda kiszivárgott a coop world snapshotba.");
    trap.Detect();
    var detected = WorldSnapshotProjector.Create(maze, fog).RevealedCells.Single(cell => cell.Position == position);
    Assert(detected.TileCodePoint == definition.Symbol.Value && detected.ForegroundColor == ConsoleColor.Yellow,
        "A felfedezett csapda nem jelent meg a coop world snapshotban.");
    trap.Disarm();
    var disarmed = WorldSnapshotProjector.Create(maze, fog).RevealedCells.Single(cell => cell.Position == position);
    Assert(disarmed.TileCodePoint == new Rune('·').Value && disarmed.ForegroundColor == ConsoleColor.DarkGray,
        "A hatástalanított csapda állapota nem replikálódott.");
}

static void EnemyTargetsNearestVisiblePartyMember()
{
    var host = CreateCharacter("Host");
    var guest = CreateCharacter("Vendég");
    var npc = CreateCharacter("NPC");
    var candidates = new[]
    {
        (host, new Position(8, 8)),
        (guest, new Position(3, 2)),
        (npc, new Position(4, 2))
    };
    var target = EnemyTargeting.ChooseNearestVisible(new Position(2, 2), candidates,
        position => position != new Position(3, 2), new Random(1));
    Assert(target?.Character == npc,
        "Az ellenség nem a legközelebbi látható NPC-/vendégpozíciót választotta a host helyett.");

    target = EnemyTargeting.ChooseNearestVisible(new Position(2, 2), candidates,
        _ => true, new Random(1));
    Assert(target?.Character == guest,
        "Az ellenség figyelmen kívül hagyta a hostnál közelebbi vendégkaraktert.");
}

static void FighterTacticHitChancesUseCombatFormula()
{
    var system = CreateBattleSystem(1701);
    var fighter = CreateCharacter("Harcos", characterClassId: CharacterClassIds.Harcos);
    var enemy = CreateEnemy(100, 1, speed: 8);
    var precise = system.EstimatePlayerHitChance(fighter, enemy, BattleTactic.FighterPrecise);
    var powerful = system.EstimatePlayerHitChance(fighter, enemy, BattleTactic.FighterPowerful);
    var defensive = system.EstimatePlayerHitChance(fighter, enemy, BattleTactic.FighterDefensive);
    Assert(precise == defensive + 10 && defensive == powerful + 5,
        $"A taktikai módosítók nem +2/0/-1 arányban változtatják az esélyt: {precise}/{defensive}/{powerful}%.");

    var race = new RaceDefinition("R001", "Ember", PrimaryAbilities.Zero);
    var fighterClass = new CharacterClassDefinition(CharacterClassIds.Harcos, "Harcos", PrimaryAbilities.Zero,
        false, 1.0);
    var nearlyCertain = new LiveCharacter("Biztos", race, fighterClass,
        new PrimaryAbilities(5, 100, 5, 5), 20, 0, 1, 0);
    var nearlyImpossible = new LiveCharacter("Esélytelen", race, fighterClass,
        new PrimaryAbilities(5, -100, 5, 5), 20, 0, 1, 0);
    Assert(system.EstimatePlayerHitChance(nearlyCertain, enemy, BattleTactic.FighterPrecise) == 95,
        "A természetes 1 nem korlátozza 95%-ra a találati esélyt.");
    Assert(system.EstimatePlayerHitChance(nearlyImpossible, enemy, BattleTactic.FighterPowerful) == 5,
        "A természetes 20 nem biztosít legalább 5% találati esélyt.");
}

#if false // A megszüntetett párbaj-állapotgép tesztjei.
static void EnemyInitiativeDelaysTacticPrompt()
{
    var system = CreateBattleSystem(710);
    var fighter = CreateCharacter("Harcos", vitality: 100, characterClassId: CharacterClassIds.Harcos);
    var state = system.StartBattle(fighter, CreateEnemy(100, 1, speed: 100)).State;
    Assert(state.IsOpeningEnemyTurn && state.RequiresTacticSelection && !state.IsAwaitingTacticSelection,
        "Az ellenséges nyitókör előtt a rendszer már taktikai inputot várt.");
    system.Advance(state);
    Assert(state.IsPlayerTurn && state.IsAwaitingTacticSelection,
        "Az első ellenséges támadás után nem jelent meg az első saját kör taktikai választása.");
}

static void InitiativeLogShowsSignedDie()
{
    var started = CreateBattleSystem(711).StartBattle(CreateCharacter("Kezdeményező"),
        CreateEnemy(100, 1));
    var message = started.Entries.Single(entry => entry.Message.StartsWith("Kezdeményezés:",
        StringComparison.Ordinal)).Message;
    Assert(message.Split("±1d2(", StringSplitOptions.None).Length == 3 &&
           !message.Contains(" +1d2(", StringComparison.Ordinal),
        "A kezdeményezési napló nem mindkét félnél ±1d2 formában mutatja a dobást.");
}

static void NeedStatusEffectsAreVisible()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var race = new RaceDefinition("R001", "Ember", PrimaryAbilities.Zero);
    var priestClass = new CharacterClassDefinition(CharacterClassIds.Pap, "Pap", PrimaryAbilities.Zero, true, 1.0);
    var player = new LiveCharacter("Éhező", race, priestClass, new PrimaryAbilities(5, 5, 5, 5),
        200, 100, 1, 1);
    player.ConsumeFood(100);
    player.ConsumeWater(100);
    player.AddStatus(data.GetStatus(CharacterStatusIds.Hungry));
    player.AddStatus(data.GetStatus(CharacterStatusIds.Thirsty));
    player.AddStatus(data.GetStatus(CharacterStatusIds.Diseased));
    player.ReceiveDamage(100);

    Assert(player.PreviewVitalityRecovery(100) == 37,
        "Az éhség és betegség kombinált gyógyításcsökkentése hibás.");
    var started = CreateBattleSystem(713).StartBattle(player, CreateEnemy(1000, 1));
    var startLog = string.Join(" ", started.Entries.Select(entry => entry.Message));
    Assert(startLog.Contains("🍖 nulla élelem: ❤️ -", StringComparison.Ordinal) &&
           startLog.Contains("💧 szomjúság: 🔷 -", StringComparison.Ordinal) &&
           startLog.Contains("💧 szomjúság 6", StringComparison.Ordinal),
        "A csatakezdő szükséglethatások vagy a szomjúság kezdeményezés-büntetése nem látható.");

    var attackLogs = new List<string>();
    for (var index = 0; index < 100 && !started.State.IsCompleted; index++)
    {
        var entry = CreateBattleSystem(713 + index).Advance(started.State).Entries.Single();
        if (entry.Details?.Actor == "Éhező") attackLogs.AddRange(entry.Details.Calculation);
        if (attackLogs.Any(log => log.Contains("éhség", StringComparison.OrdinalIgnoreCase))) break;
    }
    Assert(attackLogs.Any(log => log.Contains("szomj", StringComparison.OrdinalIgnoreCase)) &&
           attackLogs.Any(log => log.Contains("éhség", StringComparison.OrdinalIgnoreCase)),
        "A találat- vagy fizikai sebzésbüntetés oka nem látható a támadás naplójában.");
}

static void BattleHitHighlightsDamageAndHealth()
{
    var system = CreateBattleSystem(712);
    var player = CreateCharacter("Sebző", vitality: 500, characterClassId: CharacterClassIds.Pap);
    var state = system.StartBattle(player, CreateEnemy(1000, 1)).State;
    BattleActionDetails? playerHit = null;
    BattleActionDetails? enemyHit = null;
    for (var index = 0; index < 100 && (playerHit is null || enemyHit is null) && !state.IsCompleted; index++)
    {
        var entry = system.Advance(state).Entries.Single();
        if (entry.Details?.Actor == "Sebző" && entry.Details.Summary.Any(text => text.Contains("1/1")))
            playerHit = entry.Details;
        if (entry.Details?.Target == "Sebző" && entry.Details.Summary.Any(text => text.Contains("1/1")))
            enemyHit = entry.Details;
    }
    Assert(playerHit?.Summary.Any(text => text.StartsWith("💥")) == true &&
           playerHit.Target == "Tesztellenfél" &&
           enemyHit?.Summary.Any(text => text.StartsWith("💥")) == true &&
           enemyHit.Target == "Sebző",
        "A sikeres támadásból hiányzik a sebzés- vagy a megmaradt HP ikonja.");
}

#endif

static void VictoryMessageIsConcise()
{
    var enemy = CreateEnemy(1, 1);
    var result = new BattleResult(true, 7, ["7. kör — hosszú és redundáns utolsó támadás."]);
    Assert(ConsoleRenderer.FormatBattleResultMessage(result, enemy) ==
           "GYŐZELEM 🏆: Tesztellenfél elesett.",
        "A győzelmi üzenet továbbra is megismétli az utolsó támadás részleteit.");
}

static void SnapshotRequiresCurrentBattlePrompt()
{
    var (session, leader, _) = CreateSession();
    var battleId = BattleId.New();
    session.SetBattlePrompt(battleId, 2, leader.Id,
        [BattleActionKind.PhysicalAttack, BattleActionKind.TurnUndead]);
    var battle = new BattleSnapshot(battleId, 2, 1, true, leader.Id,
        new SessionEnemySnapshot("E-TEST", "Tesztellenfél", new Position(3, 2), 8, 10),
        [BattleActionKind.PhysicalAttack, BattleActionKind.TurnUndead],
        [new BattleSpellOption("S-TEST", "Tesztvarázs", 1, 3, SpellTargetType.Enemy, 5, 0,
            null, null, 0, 0, [new Position(3, 2)])],
        [new BattleTacticOptionSnapshot(BattleActionKind.FighterPrecise, "🎯 Pontos", "sebzés ×0,75", 55)]);
    var snapshot = session.CreateSnapshot(new SessionSnapshotContext(1, "Tesztlabirintus",
        new Dictionary<CharacterId, Position> { [leader.Id] = new Position(2, 2) }, battle));
    Assert(snapshot.Battle?.BattleId == battleId && snapshot.Battle.AllowedActions.Count == 2 &&
           snapshot.Battle.SpellOptions?.Single().ValidTargets.Single() == new Position(3, 2) &&
           snapshot.Battle.TacticOptions?.Single().HitChancePercent == 55,
        "Az aktív harci prompt nem került a snapshotba.");

    var stale = battle with { TurnId = 1 };
    var rejected = false;
    try
    {
        session.CreateSnapshot(new SessionSnapshotContext(1, "Tesztlabirintus",
            new Dictionary<CharacterId, Position> { [leader.Id] = new Position(2, 2) }, stale));
    }
    catch (ArgumentException)
    {
        rejected = true;
    }
    Assert(rejected, "A lejárt körhöz tartozó harci snapshotot elfogadta a session.");
}

static void WorldSnapshotOnlyContainsRevealedState()
{
    var maze = new Maze(7, 7);
    var visibleEnemy = CreateEnemyAt(new Position(3, 2), "E-VISIBLE", "r");
    var hiddenEnemy = CreateEnemyAt(new Position(4, 5), "E-HIDDEN");
    var visibleNpc = new WorldNpc(new Position(1, 1), "NPC-VISIBLE", CreateCharacter("Segítő"),
        NpcDisposition.Friendly, true, false, "Veletek tartok.");
    var hiddenNpc = new WorldNpc(new Position(5, 4), "NPC-HIDDEN", CreateCharacter("Rejtett"),
        NpcDisposition.Neutral, false, true, "Titok.");
    foreach (var position in new[] { visibleEnemy.Position, hiddenEnemy.Position, new Position(1, 2),
                 visibleNpc.Position, hiddenNpc.Position })
        maze.Carve(position);
    maze.AddEnemy(visibleEnemy);
    maze.AddEnemy(hiddenEnemy);
    maze.AddTreasureChest(new TreasureChest(new Position(1, 2), 99));
    maze.AddWorldNpc(visibleNpc);
    maze.AddWorldNpc(hiddenNpc);
    maze.PlaceDoor(new Position(2, 3), DoorState.Closed);
    var fog = new FogOfWar(maze.Width, maze.Height, 1);
    fog.RevealFrom(maze, maze.Entrance);
    fog.ToggleDeveloperReveal();

    var world = WorldSnapshotProjector.Create(maze, fog);
    Assert(world.Enemies.Count == 1 && world.Enemies[0].EntityId == visibleEnemy.Id,
        "A world snapshot rejtett ellenfelet is publikált, vagy kihagyta a láthatót.");
    Assert(world.Chests.Count == 1 && world.Doors.Count == 1,
        "A felfedett statikus entitások hiányoznak a world snapshotból.");
    Assert(world.Npcs?.Single().EntityId == visibleNpc.Id && world.Npcs.Single().Name == "Segítő",
        "A world snapshot rejtett NPC-t publikált, vagy kihagyta a láthatót.");
    Assert(world.Chests.Single().SymbolCodePoint == new Rune('▣').Value &&
           world.Chests.Single().ForegroundColor == ConsoleColor.Yellow,
        "A world snapshot nem őrizte meg a láda hostoldali megjelenését.");
    Assert(world.Exit is null && world.RevealedCells.All(cell => fog.IsRevealed(cell.Position)),
        "A world snapshot rejtett kijáratot vagy cellát publikált.");
    Assert(world.RevealedCells.Single(cell => cell.Position == new Position(2, 1)).ForegroundColor == maze.WallColor,
        "A world snapshot nem őrizte meg a host falszínét.");
    Assert(world.Doors.Single().ForegroundColor == ConsoleColor.DarkYellow &&
           world.Doors.Single().SymbolCodePoint == new Rune('╬').Value,
        "A world snapshot nem őrizte meg a host ajtószínét vagy ajtójelét.");
    Assert(world.Enemies.Single().Color != ConsoleColor.Red,
        "A world snapshot a host erősségfüggő színe helyett fix kliensszínt adott az ellenfélnek.");
    Assert(world.Enemies.Single().SymbolCodePoint == new Rune('r').Value,
        "A world snapshot nem őrizte meg az ellenfél katalógusban megadott jelét.");
    var restored = JsonSerializer.Deserialize<WorldSnapshot>(JsonSerializer.Serialize(world));
    Assert(restored?.Enemies.Single().DefinitionId == "E-VISIBLE",
        "A world snapshot JSON round-trip közben megváltozott.");
}

static void WorldEntityIdSurvivesMovement()
{
    var maze = new Maze(7, 7);
    var enemy = CreateEnemyAt(new Position(2, 3), "E-MOVING");
    var destination = new Position(3, 3);
    maze.Carve(enemy.Position);
    maze.Carve(destination);
    maze.AddEnemy(enemy);
    var entityId = enemy.Id;
    Assert(maze.TryMoveEnemy(enemy, destination), "A tesztellenfél nem tudott elmozdulni.");
    Assert(enemy.Id == entityId, "A world entity azonosítója mozgáskor megváltozott.");
}

static void CorpsesStackOnOneCell()
{
    var maze = new Maze(7, 7);
    var position = new Position(3, 3);
    var first = new MonsterCorpse(position, "Első", "E001");
    var second = new MonsterCorpse(position, "Második", "E002");
    maze.AddCorpse(first);
    maze.AddCorpse(second);

    var stack = maze.GetCorpsesAt(position);
    Assert(stack.Count == 2 && stack.Contains(first) && stack.Contains(second) &&
           maze.GetObjectAt(position) is Corpse,
        "Az egy mezőre kerülő holttestek nem maradtak meg közös halomban.");
    first.MarkSearched();
    Assert(maze.GetUnsearchedMonsterCorpsesAt(position).SequenceEqual([second]),
        "A keresés nem pontosan a halom még át nem kutatott holttesteit választja ki.");
}

static void WorldDeltaCapturesChanges()
{
    var (session, leader, _) = CreateSession();
    var characterPositions = new Dictionary<CharacterId, Position> { [leader.Id] = new Position(2, 2) };
    var maze = new Maze(7, 7);
    var enemy = CreateEnemyAt(new Position(3, 2), "E-DELTA");
    var chest = new TreasureChest(new Position(1, 2), 10);
    foreach (var position in new[] { enemy.Position, new Position(3, 3), chest.Position }) maze.Carve(position);
    maze.AddEnemy(enemy);
    maze.AddTreasureChest(chest);
    maze.PlaceDoor(new Position(2, 3), DoorState.Closed);
    var fog = new FogOfWar(maze.Width, maze.Height, 1);
    fog.RevealFrom(maze, maze.Entrance);
    var previous = WorldSnapshotProjector.Create(maze, fog);
    var previousSession = session.CreateSnapshot(new SessionSnapshotContext(1, "Delta-labirintus",
        characterPositions, World: previous));

    enemy.SetCurrentHitPoints(6);
    Assert(maze.TryMoveEnemy(enemy, new Position(3, 3)), "A delta tesztellenfele nem tudott mozogni.");
    maze.SetDoorState(maze.GetDoorAt(new Position(2, 3))!, DoorState.Open);
    maze.RemoveTreasureChest(chest);
    var corpse = new MonsterCorpse(new Position(1, 2), "Elesett", "E-DEAD");
    maze.AddCorpse(corpse);
    fog.RevealFrom(maze, new Position(3, 3));
    var current = WorldSnapshotProjector.Create(maze, fog);
    var currentSession = session.CreateSnapshot(new SessionSnapshotContext(1, "Delta-labirintus",
        characterPositions, World: current));

    var delta = WorldDeltaProjector.Create(previousSession, currentSession);
    Assert(delta.EnemyUpserts.Single() is { CurrentHitPoints: 6, Position: { X: 3, Y: 3 } },
        "Az ellenfél mozgása vagy HP-változása hiányzik a deltából.");
    Assert(delta.DoorUpserts.Single().State == DoorState.Open && delta.CorpseUpserts.Single().EntityId == corpse.Id,
        "Az ajtóállapot vagy az új tetem hiányzik a deltából.");
    Assert(delta.RemovedEntityIds.Contains(chest.Id) && delta.RevealedOrChangedCells.Count > 0,
        "Az entitáseltávolítás vagy cellafelfedés hiányzik a deltából.");
    var restored = JsonSerializer.Deserialize<WorldDelta>(JsonSerializer.Serialize(delta));
    Assert(restored?.ToSnapshotSequence == currentSession.SnapshotSequence && restored.EnemyUpserts.Count == 1,
        "A world delta JSON round-trip közben megváltozott.");
}

static void WorldDeltaRejectsDifferentWorld()
{
    var firstMaze = new Maze(7, 7);
    var secondMaze = new Maze(7, 7);
    var first = WorldSnapshotProjector.Create(firstMaze, new FogOfWar(7, 7, 0));
    var second = WorldSnapshotProjector.Create(secondMaze, new FogOfWar(7, 7, 0));
    var rejected = false;
    try
    {
        WorldDeltaProjector.Create(1, first, 2, second);
    }
    catch (ArgumentException)
    {
        rejected = true;
    }
    Assert(rejected, "Különböző WorldId értékek között is elkészült a delta.");
}

static void ReplicationPublisherUsesAcknowledgedBaseline()
{
    var (session, leader, _) = CreateSession();
    var positions = new Dictionary<CharacterId, Position> { [leader.Id] = new Position(2, 2) };
    var maze = new Maze(7, 7);
    maze.Carve(new Position(3, 2));
    var fog = new FogOfWar(7, 7, 1);
    fog.RevealFrom(maze, maze.Entrance);
    var first = session.CreateSnapshot(new SessionSnapshotContext(1, "Replikációs pálya", positions,
        World: WorldSnapshotProjector.Create(maze, fog)));
    var playerId = PlayerId.New();
    var publisher = new SessionReplicationPublisher();

    var full = publisher.CreateFrame(playerId, first);
    Assert(full.Kind == SessionReplicationFrameKind.FullSnapshot && full.Session.World is not null &&
           full.WorldDelta is null && full.BaseSnapshotSequence is null,
        "Az első replikációs frame nem teljes snapshot.");
    Assert(publisher.TryAcknowledge(playerId, first.SnapshotSequence, out var error), error);

    var enemy = CreateEnemyAt(new Position(3, 2), "E-REPLICATION");
    maze.AddEnemy(enemy);
    var second = session.CreateSnapshot(new SessionSnapshotContext(1, "Replikációs pálya", positions,
        World: WorldSnapshotProjector.Create(maze, fog)));
    var delta = publisher.CreateFrame(playerId, second);
    Assert(delta.Kind == SessionReplicationFrameKind.Delta && delta.Session.World is null &&
           delta.BaseSnapshotSequence == first.SnapshotSequence &&
           delta.WorldDelta?.EnemyUpserts.Single().EntityId == enemy.Id,
        "A nyugtázott baseline után nem megfelelő world delta készült.");
    var restored = JsonSerializer.Deserialize<SessionReplicationFrame>(JsonSerializer.Serialize(delta));
    Assert(restored?.WorldDelta?.ToSnapshotSequence == second.SnapshotSequence,
        "A replikációs frame JSON round-trip közben megváltozott.");
}

static void UnknownReplicationAckForcesResync()
{
    var (session, leader, _) = CreateSession();
    var maze = new Maze(7, 7);
    var fog = new FogOfWar(7, 7, 0);
    fog.RevealFrom(maze, maze.Entrance);
    var positions = new Dictionary<CharacterId, Position> { [leader.Id] = maze.Entrance };
    var publisher = new SessionReplicationPublisher();
    var playerId = PlayerId.New();
    var first = session.CreateSnapshot(new SessionSnapshotContext(1, "ACK pálya", positions,
        World: WorldSnapshotProjector.Create(maze, fog)));
    publisher.CreateFrame(playerId, first);
    Assert(!publisher.TryAcknowledge(playerId, first.SnapshotSequence + 99, out _),
        "Az ismeretlen snapshot ACK-ot elfogadta a publisher.");
    var second = session.CreateSnapshot(new SessionSnapshotContext(1, "ACK pálya", positions,
        World: WorldSnapshotProjector.Create(maze, fog)));
    Assert(publisher.CreateFrame(playerId, second).Kind == SessionReplicationFrameKind.FullSnapshot,
        "Ismeretlen ACK után nem történt teljes resync.");
}

static void ReplicationPublisherUsesFullSnapshotForNewWorld()
{
    var (session, leader, _) = CreateSession();
    var positions = new Dictionary<CharacterId, Position> { [leader.Id] = new Position(2, 2) };
    var firstMaze = new Maze(7, 7);
    var firstFog = new FogOfWar(7, 7, 0);
    firstFog.RevealFrom(firstMaze, firstMaze.Entrance);
    var publisher = new SessionReplicationPublisher();
    var playerId = PlayerId.New();
    var first = session.CreateSnapshot(new SessionSnapshotContext(1, "Első pálya", positions,
        World: WorldSnapshotProjector.Create(firstMaze, firstFog)));
    publisher.CreateFrame(playerId, first);
    Assert(publisher.TryAcknowledge(playerId, first.SnapshotSequence, out var error), error);

    var secondMaze = new Maze(7, 7);
    var secondFog = new FogOfWar(7, 7, 0);
    secondFog.RevealFrom(secondMaze, secondMaze.Entrance);
    var second = session.CreateSnapshot(new SessionSnapshotContext(2, "Második pálya", positions,
        World: WorldSnapshotProjector.Create(secondMaze, secondFog)));
    Assert(publisher.CreateFrame(playerId, second).Kind == SessionReplicationFrameKind.FullSnapshot,
        "Pályaváltáskor a publisher deltát próbált küldeni.");
}

static void ClientStoreAppliesReplicationFrames()
{
    var (session, leader, _) = CreateSession();
    var playerId = PlayerId.New();
    var publisher = new SessionReplicationPublisher();
    var store = new ClientSessionStore(playerId);
    var changedSnapshots = new List<SessionSnapshot>();
    store.SnapshotChanged += changedSnapshots.Add;
    var maze = new Maze(7, 7);
    var enemyPosition = new Position(3, 2);
    maze.Carve(maze.Entrance);
    maze.Carve(enemyPosition);
    var fog = new FogOfWar(7, 7, 1);
    fog.RevealFrom(maze, maze.Entrance);
    var positions = new Dictionary<CharacterId, Position> { [leader.Id] = maze.Entrance };

    var first = session.CreateSnapshot(new SessionSnapshotContext(1, "Kliens pálya", positions,
        World: WorldSnapshotProjector.Create(maze, fog)));
    var full = publisher.CreateFrame(playerId, first);
    var fullResult = store.Apply(full);
    Assert(fullResult is { Status: ClientFrameApplyStatus.Applied, Response: SnapshotAck } &&
           store.CurrentSnapshot?.World is not null, "A kliens store nem alkalmazta a teljes snapshotot.");
    Assert(publisher.TryAcknowledge(playerId, first.SnapshotSequence, out var ackError), ackError);

    var enemy = CreateEnemyAt(enemyPosition, "E-CLIENT-DELTA");
    maze.AddEnemy(enemy);
    var second = session.CreateSnapshot(new SessionSnapshotContext(1, "Kliens pálya", positions,
        World: WorldSnapshotProjector.Create(maze, fog)));
    var firstDelta = publisher.CreateFrame(playerId, second);
    Assert(store.Apply(firstDelta).Status == ClientFrameApplyStatus.Applied &&
           store.CurrentSnapshot!.World!.Enemies.Single().EntityId == enemy.Id,
        "A kliens store nem alkalmazta az entitás-upsertet.");

    // Az ACK még nem ért vissza a hosthoz: a következő delta továbbra is az első snapshotból indul.
    maze.ReplaceEnemyWithCorpse(enemy);
    var third = session.CreateSnapshot(new SessionSnapshotContext(1, "Kliens pálya", positions,
        World: WorldSnapshotProjector.Create(maze, fog)));
    var oldBaselineDelta = publisher.CreateFrame(playerId, third);
    Assert(oldBaselineDelta.BaseSnapshotSequence == first.SnapshotSequence,
        "A tesztframe nem a várt régi ACK-baseline-ról indult.");
    Assert(store.Apply(oldBaselineDelta).Status == ClientFrameApplyStatus.Applied &&
           store.CurrentSnapshot!.World!.Enemies.Count == 0 &&
           store.CurrentSnapshot.World.Corpses.Count == 1 && changedSnapshots.Count == 3,
        "A kliens nem a deklarált baseline-ra alkalmazta a deltát, vagy nem publikálta az új read modelt.");
}

static void ClientStoreRequestsResyncForMissingBaseline()
{
    var playerId = PlayerId.New();
    var world = new WorldSnapshot(WorldId.New(), 7, 7, null, null, [], [], [], [], [], []);
    var delta = new WorldDelta(10, 11, null, null, [], [], [], [], [], [], [], []);
    var session = new SessionSnapshot(SessionProtocol.Version, 11, 0, GameSessionPhase.Exploration,
        PlayerId.New(), CharacterId.New(), 1, "Hiányzó baseline", [], [], null);
    var frame = new SessionReplicationFrame(SessionReplicationFrameKind.Delta, playerId, 10, session, delta);
    var store = new ClientSessionStore(playerId);
    var result = store.Apply(frame);
    Assert(result is { Status: ClientFrameApplyStatus.ResyncRequired, Response: SnapshotResyncRequest } &&
           store.CurrentSnapshot is null,
        "A kliens hiányzó baseline esetén nem kért teljes resyncet.");

    var wrongRecipient = new SessionReplicationFrame(SessionReplicationFrameKind.FullSnapshot, PlayerId.New(),
        null, session with { SnapshotSequence = 1, World = world }, null);
    Assert(store.Apply(wrongRecipient).Status == ClientFrameApplyStatus.Rejected,
        "A kliens elfogadta a másik játékosnak címzett snapshotot.");
}

static void InventorySnapshotHasSlotsAndRevision()
{
    var character = CreateCharacter("Inventory");
    var ration = new MiscItemDefinition("I-FOOD", "Útravaló", "Tesztélelem", 2, ConsumableEffect.Food, 10);
    Assert(character.AddToBackpack(ration), "A teszttárgy nem került a hátizsákba.");
    var first = InventorySnapshotProjector.Create(character);
    Assert(first.Revision == 1 && first.Slots.Count == 19 &&
           first.Slots.Single(slot => slot.Kind == InventorySlotKind.Backpack && slot.Index == 0)
               .Item?.DefinitionId == ration.Id,
        "Az inventory snapshot slotjai vagy revíziója hibás.");
    Assert(character.SetInventoryItem(InventorySlotKind.Backpack, 0, null), "A teszttárgy nem távolítható el.");
    var second = InventorySnapshotProjector.Create(character);
    Assert(second.Revision == first.Revision + 1 &&
           second.Slots.Single(slot => slot.Kind == InventorySlotKind.Backpack && slot.Index == 0).Item is null,
        "Az inventory mutáció nem növelte pontosan egyszer a revíziót.");
}

static void PartyVisionTracksLastKnownEnemy()
{
    var maze = new Maze(15, 9);
    for (var x = 2; x <= 8; x++) maze.Carve(new Position(x, 2));
    maze.Carve(new Position(2, 6));
    var enemy = CreateEnemyAt(new Position(7, 2), "E-MEMORY");
    maze.AddEnemy(enemy);
    var fog = new FogOfWar(maze.Width, maze.Height, 5);
    fog.UpdatePartyVisibility(maze, [(maze.Entrance, 3)], advanceEnemyMemory: false);
    Assert(WorldSnapshotProjector.Create(maze, fog).Enemies.Single().EntityId == enemy.Id,
        "A partitag által látott szörny nem jelent meg a közös world snapshotban.");

    fog.UpdatePartyVisibility(maze, [(new Position(2, 6), 1)], advanceEnemyMemory: true);
    var hidden = WorldSnapshotProjector.Create(maze, fog);
    Assert(hidden.Enemies.Count == 0 && hidden.LastKnownEnemies is
               [{ EntityId: var rememberedId, Position: var rememberedPosition, RemainingPartyMoves: 3 }] &&
           rememberedId == enemy.Id && rememberedPosition == enemy.Position,
        "A látótérből kikerült szörny nem tűnt el, vagy nem maradt meg az utolsó ismert helye.");

    for (var step = 0; step < 3; step++)
        fog.UpdatePartyVisibility(maze, [(new Position(2, 6), 1)], advanceEnemyMemory: true);
    Assert(fog.EnemyMemories.Count == 0,
        "Az utolsó ismert szörnyhely három partimozgás után sem tűnt el.");
}

static void PartyPerceptionDetectsStealthAndSound()
{
    var maze = new Maze(11, 7);
    var origin = new Position(2, 3);
    var enemyPosition = new Position(8, 3);
    for (var x = 1; x <= 9; x++)
        for (var y = 2; y <= 4; y++) maze.Carve(new Position(x, y));
    var stealthy = new ConfiguredEnemy(enemyPosition,
        new EnemyDefinition("E-STEALTH", "Árnyjáró", "a", 1, 10, 0, 1, 10, 1, [],
            VisionRange: 5, Stealth: 2, Noise: 0));
    maze.AddEnemy(stealthy);
    var fog = new FogOfWar(maze.Width, maze.Height, 5);

    fog.UpdatePartyVisibility(maze, [new PartyPerceptionSource(origin, 4, 0, 0)], false);
    Assert(WorldSnapshotProjector.Create(maze, fog).Enemies.Count == 0,
        "A lopakodó ellenfél felderítési próba nélkül kiszivárgott.");
    stealthy.MoveTo(new Position(7, 3));
    fog.UpdatePartyVisibility(maze, [new PartyPerceptionSource(origin, 4, 0, 0)], false);
    Assert(WorldSnapshotProjector.Create(maze, fog).Enemies.Single().EntityId == stealthy.Id,
        "A lopakodó ellenfél mozgása nem fedte fel átmenetileg.");
    fog.UpdatePartyVisibility(maze, [new PartyPerceptionSource(origin, 4, 0, 0)], false);
    Assert(WorldSnapshotProjector.Create(maze, fog).Enemies.Count == 0,
        "A lopakodó ellenfél nem tudott újra elrejtőzni a mozgása után.");
    fog.UpdatePartyVisibility(maze, [new PartyPerceptionSource(origin, 4, 0, 2)], false);
    Assert(WorldSnapshotProjector.Create(maze, fog).Enemies.Single().EntityId == stealthy.Id,
        "A jobb közös észlelés nem fedte fel a lopakodó ellenfelet.");

    var noisyMaze = new Maze(11, 7);
    for (var x = 1; x <= 9; x++)
        for (var y = 2; y <= 4; y++) noisyMaze.Carve(new Position(x, y));
    var noisy = new ConfiguredEnemy(enemyPosition,
        new EnemyDefinition("E-NOISY", "Csörtető", "c", 1, 10, 0, 1, 10, 1, [],
            VisionRange: 5, Stealth: 0, Noise: 4));
    noisyMaze.AddEnemy(noisy);
    var hearingFog = new FogOfWar(noisyMaze.Width, noisyMaze.Height, 1);
    hearingFog.UpdatePartyVisibility(noisyMaze,
        [new PartyPerceptionSource(origin, 1, 4, 0)], false);
    var heard = WorldSnapshotProjector.Create(noisyMaze, hearingFog);
    Assert(heard.Enemies.Count == 0 && heard.LastKnownEnemies is
               [{ IsSoundCue: true, Position: var heardPosition }] && heardPosition != enemyPosition,
        "A hallott ellenfél hangjele pontos helyet vagy teljes szörnyadatot árult el.");
}

static void VictorySummaryIsCompact()
{
    var enemy = CreateEnemy(1, 1);
    var result = new BattleResult(true, 4, []);
    Assert(ConsoleRenderer.FormatBattleVictorySummary(result, enemy, 11, 8, ["🤒"], 4) ==
           "GYŐZELEM 🏆: Tesztellenfél elesett. ⌛4❤️-11🔷-8🤒🍖-4💧-4",
        "A győzelmi összegzés nem a várt kompakt formátumot használja.");
    Assert(ConsoleRenderer.FormatBattleVictorySummary(result, enemy, 0, 0, [], 2) ==
           "GYŐZELEM 🏆: Tesztellenfél elesett. ⌛4🍖-2💧-2",
        "A nulla HP-/manaveszteséget nem szabad megjeleníteni.");
    Assert(ConsoleRenderer.FormatAutoBattleVictorySummary(result, "Borin", enemy, 11, 8, ["🤒"], 4, 2, 120) ==
           "AUTOCSATA 🏆: Borin → Tesztellenfél elesett. ⌛4❤️-11🔷-8🤒🍖-4💧-4📜2✨120XP",
        "A győztes autocsata összegzése nem kompakt vagy nem nevezi meg a harcoló társat.");
    Assert(ConsoleRenderer.FormatAutoBattleDefeatSummary(result with { PlayerWon = false }, "Borin", enemy,
               20, 3, [], 4, 0) ==
           "AUTOCSATA 💀: Borin elesett → Tesztellenfél. ⌛4❤️-20🔷-3🍖-4💧-4👹1HP",
        "A vesztes autocsata összegzése nem kompakt vagy nem mutatja az ellenfél megmaradt HP-ját.");
}

static void BackpackStacksIdenticalItemsUpToNine()
{
    var character = CreateCharacter("Kötegteszt");
    var ration = new MiscItemDefinition("I-STACK", "Útravaló", "Tesztélelem", 2,
        ConsumableEffect.Food, 10);
    for (var count = 0; count < 10; count++)
        Assert(character.AddToBackpack(ration), "Az azonos tárgy nem fért be a hátizsákba.");
    Assert(character.Backpack.Count == 12 &&
           character.GetInventoryItemQuantity(InventorySlotKind.Backpack, 0) == 9 &&
           character.GetInventoryItemQuantity(InventorySlotKind.Backpack, 1) == 1,
        "A hátizsák nem kilences kötegre és új slotra bontotta a tíz azonos tárgyat.");
    Assert(character.RemoveOneInventoryItem(InventorySlotKind.Backpack, 0) &&
           character.GetInventoryItemQuantity(InventorySlotKind.Backpack, 0) == 8,
        "Egy tárgy elvétele nem pontosan eggyel csökkentette a köteget.");
    var snapshot = InventorySnapshotProjector.Create(character);
    Assert(snapshot.Slots.Single(slot => slot.Kind == InventorySlotKind.Backpack && slot.Index == 0)
               .Item?.Quantity == 8,
        "A coop inventory snapshot nem továbbította a köteg darabszámát.");
}

static void CharacterSheetLayoutIsShared()
{
    var character = CreateCharacter("Közös lap");
    var experienceByLevel = new Dictionary<int, int> { [2] = 100 };
    var inventory = InventorySnapshotProjector.Create(character);
    var snapshot = new SessionCharacterSnapshot(character.Id, character.Name, character.Race.Id,
        character.CharacterClass.Id, character.Level, character.CurrentVitality, character.MaximumVitality,
        character.CurrentMana, character.MaximumMana, character.FoodLevel, character.WaterLevel, character.Gold,
        character.IsAlive, null, [], inventory,
        CharacterSheetSnapshotProjector.Create(character, experienceByLevel));

    var hostLines = CharacterSheetPanel.Build(character, experienceByLevel, 3, 1, 4);
    var guestLines = CharacterSheetPanel.Build(snapshot, 3, 1, 4);
    Assert(hostLines.SequenceEqual(guestLines),
        "A doménkarakterből és a hálózati snapshotból felépített karakterlap eltér.");
    var abilityLine = hostLines.Single(line => line.Row == 4);
    Assert(abilityLine.Text.Contains("💖", StringComparison.Ordinal) &&
           abilityLine.Text.EndsWith("👁️", StringComparison.Ordinal) && abilityLine.ColoredSuffix == "5" &&
           abilityLine.ColoredSuffixColor == ConsoleColor.White &&
           abilityLine.Text.Length + abilityLine.ColoredSuffix.Length <= CharacterSheetPanel.Width,
        $"A karakterlap képességsora nem mutatja szabályosan a látótávot: '{abilityLine.Text}{abilityLine.ColoredSuffix}'.");
    var restored = JsonSerializer.Deserialize<SessionCharacterSnapshot>(JsonSerializer.Serialize(snapshot));
    Assert(restored is not null && CharacterSheetPanel.Build(restored, 3, 1, 4).SequenceEqual(hostLines),
        "A közös karakterlap read modelje nem élte túl a JSON wire-körutat.");
    var hostLeaderLine = CharacterSheetPanel.Build(character, experienceByLevel, 3, 1, 4, true)
        .Single(line => line.Row == 2);
    var guestLeaderLine = CharacterSheetPanel.Build(snapshot, 3, 1, 4, true)
        .Single(line => line.Row == 2);
    Assert(hostLeaderLine == guestLeaderLine && hostLeaderLine.Text.Contains("👑 VEZÉR", StringComparison.Ordinal) &&
           hostLeaderLine.Color == ConsoleColor.Yellow,
        "A host és a vendég karakterlapján nem azonos a feltűnő vezérjelzés.");
    var followerSnapshot = snapshot with { IsTemporaryFollower = true };
    var hostFollowerLine = CharacterSheetPanel.Build(character, experienceByLevel, 3, 1, 4,
        isTemporaryFollower: true).Single(line => line.Row == 2);
    var guestFollowerLine = CharacterSheetPanel.Build(followerSnapshot, 3, 1, 4)
        .Single(line => line.Row == 2);
    Assert(hostFollowerLine == guestFollowerLine &&
           hostFollowerLine.Text.Contains("👤 KÖVETŐ", StringComparison.Ordinal) &&
           hostFollowerLine.Color == ConsoleColor.Black &&
           hostFollowerLine.Background == ConsoleColor.Yellow,
        "A host és a vendég karakterlapján nem azonos a követőjelzés szövege vagy színe.");
}

static void CharacterDetailsAreShared()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var character = CreateCharacter("Dosszié", characterClassId: CharacterClassIds.Tolvaj);
    character.SetNpcBehavior(NpcBehavior.Defensive);
    character.SetNpcJoinOrigin(3, "A Rézcsengő");
    character.RecordMonsterKill(data.Enemies[0].Id, 2);
    var visionItem = data.GetItem(MiscItemIds.Torch);
    character.ApplySpellEffect(new ActiveSpellEffect(visionItem.Id, ActiveSpellEffectType.VisionBonus, 2, 12));
    Assert(character.TryAdvanceWeaponProficiency(WeaponFamilies.Dagger),
        "A részletes karakterlap fegyverjártassági tesztje nem készíthető elő.");
    var sheet = CharacterSheetSnapshotProjector.Create(character, data.ExperienceByLevel, -2);
    var snapshot = new SessionCharacterSnapshot(character.Id, character.Name, character.Race.Id,
        character.CharacterClass.Id, character.Level, character.CurrentVitality, character.MaximumVitality,
        character.CurrentMana, character.MaximumMana, character.FoodLevel, character.WaterLevel, character.Gold,
        character.IsAlive, null, [], InventorySnapshotProjector.Create(character), sheet, character.Color,
        History: new CharacterHistorySnapshot([new MonsterKillSnapshot(data.Enemies[0].Id, 2)], 3,
            "A Rézcsengő", NpcBehavior.Defensive.ToString()));
    var lines = CharacterDetailsWindow.Build(snapshot, data);
    Assert(GameInputBindings.InventoryAction(ConsoleKey.R) == InventoryInputAction.CharacterDetails &&
           WindowFrameConfiguration.For(FramedWindow.CharacterDetails) == WindowFrameStyle.Stone,
        "Az R billentyű vagy a stone keret nincs bekötve.");
    Assert(lines.Any(line => line.Text.Contains("Tolvaj osztály", StringComparison.Ordinal)) &&
           lines.Any(line => line.Text.Contains("Pálya/környezet", StringComparison.Ordinal) && line.Color == ConsoleColor.Red) &&
           lines.Any(line => line.Text.Contains(visionItem.Name, StringComparison.Ordinal) &&
                             !line.Text.Contains(visionItem.Id, StringComparison.Ordinal)) &&
           lines.Any(line => line.Text.Contains("🗡️ Tőr — Jártas", StringComparison.Ordinal)) &&
           lines.Any(line => line.Text.Contains("A Rézcsengő", StringComparison.Ordinal)) &&
           lines.Any(line => line.Text.Contains(data.Enemies[0].Name, StringComparison.Ordinal) && line.Text.Contains("2", StringComparison.Ordinal)),
        "A közös részletes karakterlapból hiányzik egy látás-, NPC- vagy ölési adat.");
}

static void GuestAvatarUsesClassGlyphAndCharacterColor()
{
    Assert(CharacterSheetPanel.CharacterClassGlyph(CharacterClassIds.Harcos) == "H" &&
           CharacterSheetPanel.CharacterClassGlyph(CharacterClassIds.Barbár) == "B" &&
           CharacterSheetPanel.CharacterClassGlyph(CharacterClassIds.Lovag) == "L" &&
           CharacterSheetPanel.CharacterClassGlyph(CharacterClassIds.Tolvaj) == "T" &&
           CharacterSheetPanel.CharacterClassGlyph(CharacterClassIds.Pap) == "P" &&
           CharacterSheetPanel.CharacterClassGlyph(CharacterClassIds.Mágus) == "M",
        "A kasztazonosítók nem a megfelelő térképi betűre képződnek.");
    var (session, leader, companion) = CreateSession();
    var snapshot = session.CreateSnapshot(new SessionSnapshotContext(1, "Színteszt",
        new Dictionary<CharacterId, Position>
        {
            [leader.Id] = new Position(1, 1),
            [companion.Id] = new Position(2, 1)
        }));
    Assert(snapshot.Party.Single(character => character.CharacterId == leader.Id).Color == leader.Color &&
           snapshot.Party.Single(character => character.CharacterId == companion.Id).Color == companion.Color,
        "A session snapshot nem őrizte meg a karakterhez rendelt konzolszínt.");
}

static void GuestRedrawIgnoresReplicationSequences()
{
    var (session, leader, companion) = CreateSession();
    var positions = new Dictionary<CharacterId, Position>
    {
        [leader.Id] = new Position(1, 1),
        [companion.Id] = new Position(2, 1)
    };
    var first = session.CreateSnapshot(new SessionSnapshotContext(1, "Render", positions));
    var sequenceOnly = first with
    {
        SnapshotSequence = first.SnapshotSequence + 10,
        LastEventSequence = first.LastEventSequence + 5
    };
    var changed = first with { MazeLevel = first.MazeLevel + 1 };
    Assert(CoopGuestRenderFingerprint.Compute(first) == CoopGuestRenderFingerprint.Compute(sequenceOnly),
        "A render fingerprint puszta replikációs sorszámra megváltozott.");
    Assert(CoopGuestRenderFingerprint.Compute(first) != CoopGuestRenderFingerprint.Compute(changed),
        "A render fingerprint valódi látható állapotváltozást nem érzékelt.");
}

static void ReplicationPublisherSharesPartyInventories()
{
    var (session, leader, companion) = CreateSession();
    var remote = session.RegisterRemotePlayer();
    Assert(session.TryAssignRemoteControl(remote, companion.Id, out var assignmentError), assignmentError);
    leader.AddToBackpack(new MiscItemDefinition("I-LEADER", "Leader tárgy", "Teszt", 1));
    companion.AddToBackpack(new MiscItemDefinition("I-GUEST", "Vendég tárgy", "Teszt", 1));
    var maze = new Maze(7, 7);
    var fog = new FogOfWar(7, 7, 0);
    fog.RevealFrom(maze, maze.Entrance);
    var positions = new Dictionary<CharacterId, Position>
    {
        [leader.Id] = maze.Entrance,
        [companion.Id] = new Position(3, 2)
    };
    var snapshot = session.CreateSnapshot(new SessionSnapshotContext(1, "Inventory pálya", positions,
        World: WorldSnapshotProjector.Create(maze, fog)));
    var experienceByLevel = new Dictionary<int, int> { [2] = 100 };
    snapshot = snapshot with
    {
        Party = snapshot.Party.Select(character => character with
        {
            CharacterSheet = CharacterSheetSnapshotProjector.Create(
                character.CharacterId == leader.Id ? leader : companion, experienceByLevel)
        }).ToArray()
    };
    var publisher = new SessionReplicationPublisher();
    var hostFrame = publisher.CreateFrame(session.HostPlayerId, snapshot);
    var guestFrame = publisher.CreateFrame(remote, snapshot);

    Assert(hostFrame.Session.Party.All(character => character.Inventory is not null),
        "A host nem kapta meg a teljes parti inventory read modelt.");
    Assert(guestFrame.Session.Party.All(character => character.Inventory is not null),
        "A vendég nem kapta meg a party hátizsákok közötti mozgatáshoz szükséges inventory read modelleket.");
    Assert(guestFrame.Session.Party.All(character => character.CharacterSheet is not null),
        "A vendég nem kapta meg a lapozható party-karakterlapokat.");
}

static void InventoryTransferIsAtomicAndPreservesCharges()
{
    var party = new Party();
    var leader = CreateCharacter("InvLeader");
    party.SetLeader(leader);
    var session = new GameSession(party, leader);
    var wand = new MagicItemDefinition("MI-TEST", "Tesztpálca", MagicItemKind.Wand, ItemRarity.Magic,
        10, 5, null, MagicItemEffect.None, 0, new HashSet<string> { leader.CharacterClass.Id }, "Teszt", 1);
    Assert(leader.AddMagicItem(wand), "A tesztpálca nem került a varázstárgyslotba.");
    var revision = leader.InventoryRevision;
    var command = new InventoryTransferCommand(session.HostPlayerId, 1, leader.Id, revision,
        InventorySlotKind.MagicItem, 0, leader.Id, revision, InventorySlotKind.Backpack, 1);
    session.Submit(command);
    Assert(session.TryReadCommand(out var accepted) && accepted == command,
        "Az érvényes inventory transfer commandot elutasította a session.");
    Assert(InventoryTransferService.TryExecute(party, command, out _, out var error), error);
    Assert(leader.GetInventoryItem(InventorySlotKind.MagicItem, 0) is null &&
           leader.GetInventoryItem(InventorySlotKind.Backpack, 1)?.Id == wand.Id &&
           leader.GetInventoryItemCharges(InventorySlotKind.Backpack, 1) == wand.MaximumCharges &&
           leader.InventoryRevision == revision + 1,
        "Az atomi transfer elvesztette a tárgyat, töltetet vagy hibásan növelte a revíziót.");
}

static void StaleInventoryRevisionIsRejected()
{
    var (session, leader, _) = CreateSession();
    leader.AddToBackpack(new MiscItemDefinition("I-SOURCE", "Forrás", "Teszt", 1));
    var staleRevision = leader.InventoryRevision;
    var command = new InventoryTransferCommand(session.HostPlayerId, 1, leader.Id, staleRevision,
        InventorySlotKind.Backpack, 0, leader.Id, staleRevision, InventorySlotKind.Backpack, 1);
    leader.AddToBackpack(new MiscItemDefinition("I-CHANGE", "Változás", "Teszt", 1));
    var events = CollectEvents(session);
    session.Submit(command);
    Assert(!session.TryReadCommand(out _) && events.OfType<GameCommandRejectedEvent>().Any(rejected =>
            rejected.Reason.Contains("megváltozott", StringComparison.OrdinalIgnoreCase)),
        "Az elavult inventory-revíziójú command átjutott.");
}

static void RemoteInventoryTransferCanCrossBackpacksOnly()
{
    var (session, leader, companion) = CreateSession();
    var remote = session.RegisterRemotePlayer();
    Assert(session.TryAssignRemoteControl(remote, companion.Id, out var assignmentError), assignmentError);
    companion.AddToBackpack(new MiscItemDefinition("I-REMOTE", "Vendégtárgy", "Teszt", 1));
    var command = new InventoryTransferCommand(remote, 1, companion.Id, companion.InventoryRevision,
        InventorySlotKind.Backpack, 0, leader.Id, leader.InventoryRevision, InventorySlotKind.Backpack, 1);
    session.Submit(command);
    Assert(session.TryReadCommand(out var accepted) && accepted == command,
        "A vendég nem mozgathatott tárgyat a saját és a host hátizsákja között.");
    var weapon = new WeaponDefinition("W-REMOTE", "Vendégfegyver", "Kard", new ValueRange(1, 4), 0, false,
        new HashSet<string> { companion.CharacterClass.Id }, "Teszt", 1);
    Assert(companion.SetInventoryItem(InventorySlotKind.Weapon, 0, weapon),
        "A vendég felszereléskorlátozási tesztje nem készíthető elő.");
    var forbidden = new InventoryTransferCommand(remote, 2, companion.Id, companion.InventoryRevision,
        InventorySlotKind.Weapon, 0, leader.Id, leader.InventoryRevision, InventorySlotKind.Backpack, 2);
    session.Submit(forbidden);
    Assert(!session.TryReadCommand(out _),
        "A vendég másik karakterhez felszerelést is mozgathatott.");
}

static void InventoryActionCommandsAreValidated()
{
    var (session, leader, _) = CreateSession();
    leader.AddToBackpack(new MiscItemDefinition("I-USE", "Gyógyital", "Teszt", 1, ConsumableEffect.Heal, 5));
    var revision = leader.InventoryRevision;
    session.Submit(new UseInventoryItemCommand(session.HostPlayerId, 1, leader.Id, revision, 0));
    Assert(session.TryReadCommand(out var use) && use is UseInventoryItemCommand,
        "Az érvényes használati commandot elutasította a session.");
    session.Submit(new DropInventoryItemCommand(session.HostPlayerId, 2, leader.Id, revision,
        InventorySlotKind.Backpack, 0));
    Assert(session.TryReadCommand(out var drop) && drop is DropInventoryItemCommand,
        "Az érvényes eldobási commandot elutasította a session.");
    session.Submit(new PickUpGroundItemCommand(session.HostPlayerId, 3, leader.Id, revision,
        WorldEntityId.New(), 1, 0, 1));
    Assert(session.TryReadCommand(out var pickup) && pickup is PickUpGroundItemCommand,
        "Az érvényes pickup command alakját elutasította a session.");
}

static void NonConsumableUseIsRejected()
{
    var (session, leader, _) = CreateSession();
    leader.AddToBackpack(new MiscItemDefinition("I-NOUSE", "Dísztárgy", "Teszt", 1));
    session.Submit(new UseInventoryItemCommand(session.HostPlayerId, 1, leader.Id,
        leader.InventoryRevision, 0));
    Assert(!session.TryReadCommand(out _), "A nem fogyasztható tárgy használati commandja átjutott.");
}

static void GroundPilePreservesChargesAndRevision()
{
    var character = CreateCharacter("PileTest");
    var wand = new MagicItemDefinition("MI-PILE", "Földi pálca", MagicItemKind.Wand, ItemRarity.Magic,
        10, 5, null, MagicItemEffect.None, 0, new HashSet<string> { character.CharacterClass.Id }, "Teszt", 1);
    var pile = new GroundItemPile(new Position(2, 2), wand, 3);
    Assert(pile.Revision == 1 && pile.Entries.Single().Charges == 3,
        "A földi kupac nem őrizte meg a kezdeti töltetet vagy revíziót.");
    Assert(!pile.TryTake(0, 2, out _), "A kupac elfogadta az elavult revíziót.");
    Assert(pile.TryTake(0, 1, out var entry) && entry.Charges == 3 && pile.Revision == 2,
        "A revíziózott pickup elvesztette a töltetet vagy nem növelte a kupacrevíziót.");
    var maze = new Maze(7, 7);
    maze.Carve(maze.Entrance);
    maze.DropItem(maze.Entrance, wand, 3);
    var fog = new FogOfWar(7, 7, 0);
    fog.RevealFrom(maze, maze.Entrance);
    var worldPile = WorldSnapshotProjector.Create(maze, fog).GroundPiles.Single();
    Assert(worldPile.Revision == 1 && worldPile.Items.Single().Charges == 3,
        "A world snapshot nem publikálta a kupac revízióját vagy töltetszámát.");
}

static void CatalogFingerprintIsDeterministic()
{
    var content = Encoding.UTF8.GetBytes("azonos katalógus\nR001;Ember");
    var first = CatalogFingerprint.Compute(content);
    var second = CatalogFingerprint.Compute(content);
    var changed = CatalogFingerprint.Compute(Encoding.UTF8.GetBytes("más katalógus"));
    Assert(first == second && first.Length == 64 && first != changed,
        "A katalógus SHA-256 fingerprint nem determinisztikus vagy nem érzékeli a változást.");
}

static void HandshakeValidatesProtocolAndCatalog()
{
    var (session, _, _) = CreateSession();
    var hash = CatalogFingerprint.Compute(Encoding.UTF8.GetBytes("catalog"));
    var handshake = new SessionHandshakeService(session, "1.0.0", hash);
    var wrongProtocol = handshake.Handle(new ClientHello(SessionProtocol.Version + 1, "1.0.0", hash, "Vendég"));
    var wrongCatalog = handshake.Handle(new ClientHello(SessionProtocol.Version, "1.0.0",
        CatalogFingerprint.Compute(Encoding.UTF8.GetBytes("other")), "Vendég"));
    var accepted = handshake.Handle(new ClientHello(SessionProtocol.Version, "1.0.0", hash, "Vendég"));
    Assert(!wrongProtocol.Accepted && !wrongCatalog.Accepted && accepted.Accepted &&
           accepted.PlayerId is not null && accepted.ReconnectToken?.Length == 64 &&
           accepted.AvailableCharacters?.Single().Name == "Companion",
        "A handshake verzió-/katalógusellenőrzése vagy elfogadott válasza hibás.");
}

static void HandshakeReconnectRestoresPlayer()
{
    var (session, _, companion) = CreateSession();
    var hash = CatalogFingerprint.Compute(Encoding.UTF8.GetBytes("catalog"));
    var handshake = new SessionHandshakeService(session, "1.0.0", hash);
    var first = handshake.Handle(new ClientHello(SessionProtocol.Version, "1.0.0", hash, "Vendég"));
    Assert(first is { Accepted: true, PlayerId: not null, ReconnectToken: not null },
        "Az első handshake sikertelen.");
    var playerId = first.PlayerId!.Value;
    var token = first.ReconnectToken!;
    Assert(session.TryAssignRemoteControl(playerId, companion.Id, out var error), error);
    session.MarkPlayerDisconnected(playerId);
    var reconnected = handshake.Handle(new ClientHello(SessionProtocol.Version, "1.0.0", hash,
        "Vendég", token));
    Assert(reconnected.Accepted && reconnected.PlayerId == playerId,
        "A reconnect-token nem az eredeti PlayerId-t állította vissza.");
}

static void ProtocolCodecRoundTripsCommand()
{
    var command = new BattleActionCommand(PlayerId.New(), 7, CharacterId.New(), BattleId.New(), 3,
        BattleActionKind.CastSpell, "SP-TEST", 1, new Position(4, 5));
    var restored = CoopProtocolJson.Decode(CoopProtocolJson.Encode(command));
    Assert(restored is BattleActionCommand decoded && decoded == command,
        "A JSON wire codec megváltoztatta a battle commandot.");
    var strafe = new MoveCharacterCommand(PlayerId.New(), 8, CharacterId.New(), Direction.Left,
        PreserveFormationFacing: true);
    Assert(CoopProtocolJson.Decode(CoopProtocolJson.Encode(strafe)) is MoveCharacterCommand decodedStrafe &&
           decodedStrafe == strafe && decodedStrafe.PreserveFormationFacing,
        "A JSON wire codec elvesztette a Shift+nyíl nézésiirány-megőrzését.");
    var keyOwnerId = CharacterId.New();
    var characterAction = new CharacterActionCommand(PlayerId.New(), 8, CharacterId.New(),
        CharacterAction.CloseOrLockDoor, new Position(7, 9), UseKey: true,
        KeyOwnerCharacterId: keyOwnerId);
    Assert(CoopProtocolJson.Decode(CoopProtocolJson.Encode(characterAction)) is CharacterActionCommand decodedAction &&
           decodedAction == characterAction && decodedAction.UseKey == true &&
           decodedAction.KeyOwnerCharacterId == keyOwnerId,
        "A JSON wire codec megváltoztatta a karakterhez kötött akciót.");
    var attackOrder = new LeaderActionCommand(PlayerId.New(), 9, CharacterId.New(),
        LeaderAction.ToggleAttackMode);
    Assert(CoopProtocolJson.Decode(CoopProtocolJson.Encode(attackOrder)) is LeaderActionCommand decodedOrder &&
           decodedOrder == attackOrder,
        "A JSON wire codec megváltoztatta a Támadás leader-parancsot.");
    var sale = new InnSaleCommand(PlayerId.New(), 10, CharacterId.New(), 4, 7, 2);
    Assert(CoopProtocolJson.Decode(CoopProtocolJson.Encode(sale)) is InnSaleCommand decodedSale &&
           decodedSale == sale, "A JSON wire codec megváltoztatta a fogadói eladást.");
    var helpVisibility = new SetHelpVisibilityCommand(PlayerId.New(), 11, CharacterId.New(), true);
    Assert(CoopProtocolJson.Decode(CoopProtocolJson.Encode(helpVisibility)) is SetHelpVisibilityCommand decodedHelp &&
           decodedHelp == helpVisibility, "A JSON wire codec megváltoztatta a súgó láthatósági parancsát.");
    var distribution = new DistributeInventoryStackCommand(PlayerId.New(), 12, CharacterId.New(), 4, 2);
    Assert(CoopProtocolJson.Decode(CoopProtocolJson.Encode(distribution)) is
               DistributeInventoryStackCommand decodedDistribution && decodedDistribution == distribution,
        "A JSON wire codec megváltoztatta az inventory-szétosztási parancsot.");
    var followerTransfer = new GiveFollowerStackCommand(PlayerId.New(), 13, CharacterId.New(), 4, 2,
        CharacterId.New(), 7);
    Assert(CoopProtocolJson.Decode(CoopProtocolJson.Encode(followerTransfer)) is
               GiveFollowerStackCommand decodedFollowerTransfer && decodedFollowerTransfer == followerTransfer,
        "A JSON wire codec megváltoztatta a követőnek átadási parancsot.");
    var imageAcknowledgement = new AcknowledgeLevelImageCommand(PlayerId.New(), 13, CharacterId.New(),
        Guid.NewGuid());
    Assert(CoopProtocolJson.Decode(CoopProtocolJson.Encode(imageAcknowledgement)) is
               AcknowledgeLevelImageCommand decodedImageAcknowledgement &&
           decodedImageAcknowledgement == imageAcknowledgement,
        "A JSON wire codec megváltoztatta a pályakép-nyugtázást.");
    var characterState = new CharacterStateSync(PlayerId.New(), CharacterId.New(), "character-json",
        CharacterSyncReason.CharacterDied);
    Assert(CoopProtocolJson.Decode(CoopProtocolJson.Encode(characterState)) is CharacterStateSync decodedState &&
           decodedState == characterState, "A JSON wire codec megváltoztatta a karakter-visszaszinkronizálást.");
    var rejected = false;
    try
    {
        CoopProtocolJson.Decode("{\"Type\":\"command.unknown\",\"Payload\":{}}");
    }
    catch (JsonException)
    {
        rejected = true;
    }
    Assert(rejected, "A codec elfogadott egy nem allowlistelt üzenettípust.");
}

static void HostGatewayBindsAuthenticatedPlayer()
{
    var (session, _, companion) = CreateSession();
    var hash = CatalogFingerprint.Compute(Encoding.UTF8.GetBytes("catalog"));
    var gateway = new CoopHostGateway(session, new SessionHandshakeService(session, "1.0.0", hash),
        new SessionReplicationPublisher());
    var helloMessages = gateway.HandleIncoming("connection-1", CoopProtocolJson.Encode(
        new ClientHello(SessionProtocol.Version, "1.0.0", hash, "Vendég")));
    var hello = (ServerHello)CoopProtocolJson.Decode(helloMessages.Single().WireMessage);
    Assert(hello is { Accepted: true, PlayerId: { } }, "A gateway handshake sikertelen.");
    var playerId = hello.PlayerId!.Value;

    var assignmentMessages = gateway.HandleIncoming("connection-1", CoopProtocolJson.Encode(
        new CharacterControlRequest(playerId, companion.Id)));
    var assignment = (CharacterControlResult)CoopProtocolJson.Decode(assignmentMessages.Single().WireMessage);
    Assert(assignment.Accepted && session.IsHumanControlled(companion.Id),
        "A gateway nem adta át a kiválasztott NPC vezérlését.");

    var impostor = new MoveCharacterCommand(PlayerId.New(), 1, companion.Id, Direction.Right);
    var rejectionMessages = gateway.HandleIncoming("connection-1", CoopProtocolJson.Encode(impostor));
    var rejection = (CoopProtocolError)CoopProtocolJson.Decode(rejectionMessages.Single().WireMessage);
    Assert(rejection.Code == "sender-mismatch" && !session.TryReadCommand(out _),
        "A gateway elfogadta a kapcsolattól eltérő PlayerId-jú commandot.");
}

static void HostGatewayRunsConnectionLifecycle()
{
    var (session, leader, companion) = CreateSession();
    var hash = CatalogFingerprint.Compute(Encoding.UTF8.GetBytes("catalog"));
    var gateway = new CoopHostGateway(session, new SessionHandshakeService(session, "1.0.0", hash),
        new SessionReplicationPublisher());
    var helloMessage = gateway.HandleIncoming("connection-2", CoopProtocolJson.Encode(
        new ClientHello(SessionProtocol.Version, "1.0.0", hash, "Vendég"))).Single();
    var playerId = ((ServerHello)CoopProtocolJson.Decode(helloMessage.WireMessage)).PlayerId!.Value;
    gateway.HandleIncoming("connection-2", CoopProtocolJson.Encode(
        new CharacterControlRequest(playerId, companion.Id)));

    var move = new MoveCharacterCommand(playerId, 1, companion.Id, Direction.Left);
    Assert(gateway.HandleIncoming("connection-2", CoopProtocolJson.Encode(move)).Count == 0 &&
           session.TryReadCommand(out var accepted) && accepted == move,
        "A hitelesített gateway-command nem jutott el a session queue-ba.");

    var maze = new Maze(7, 7);
    maze.Carve(maze.Entrance);
    var fog = new FogOfWar(7, 7, 0);
    fog.RevealFrom(maze, maze.Entrance);
    var snapshot = session.CreateSnapshot(new SessionSnapshotContext(1, "Gateway pálya",
        new Dictionary<CharacterId, Position>
        {
            [leader.Id] = maze.Entrance,
            [companion.Id] = new Position(3, 2)
        }, World: WorldSnapshotProjector.Create(maze, fog)));
    var replication = gateway.CreateReplicationMessages(snapshot);
    var frame = (SessionReplicationFrame)CoopProtocolJson.Decode(replication.Single().WireMessage);
    Assert(frame.Kind == SessionReplicationFrameKind.FullSnapshot && frame.RecipientPlayerId == playerId,
        "A gateway nem a csatlakozott játékosnak készítette a replikációs frame-et.");
    Assert(gateway.HandleIncoming("connection-2", CoopProtocolJson.Encode(
        new SnapshotAck(playerId, frame.Session.SnapshotSequence))).Count == 0,
        "A gateway nem fogadta el a snapshot ACK-ot.");

    gateway.Disconnect("connection-2");
    Assert(!session.IsHumanControlled(companion.Id) && gateway.CreateReplicationMessages(snapshot).Count == 0,
        "Disconnect után nem állt vissza az NPC-vezérlés vagy megmaradt a címzett kapcsolat.");
}

static void GatewayAndSimulationDoNotDeadlock()
{
    var (session, _, companion) = CreateSession();
    var hash = CatalogFingerprint.Compute(Encoding.UTF8.GetBytes("catalog"));
    var gateway = new CoopHostGateway(session, new SessionHandshakeService(session, "1.0.0", hash),
        new SessionReplicationPublisher());
    var hello = (ServerHello)CoopProtocolJson.Decode(gateway.HandleIncoming("deadlock-connection",
        CoopProtocolJson.Encode(new ClientHello(SessionProtocol.Version, "1.0.0", hash, "Vendég")))
        .Single().WireMessage);
    var playerId = hello.PlayerId!.Value;
    gateway.HandleIncoming("deadlock-connection",
        CoopProtocolJson.Encode(new CharacterControlRequest(playerId, companion.Id)));

    var simulation = Task.Run(() =>
    {
        for (var index = 1; index <= 200; index++)
        {
            gateway.HandleIncoming("deadlock-connection", CoopProtocolJson.Encode(
                new MoveCharacterCommand(playerId, index, companion.Id, Direction.Right)));
            session.TryReadCommand(out _);
            gateway.HandleIncoming("deadlock-connection", CoopProtocolJson.Encode(
                new MoveCharacterCommand(playerId, index, companion.Id, Direction.Left)));
            session.TryReadCommand(out _);
        }
    });
    var lifecycle = Task.Run(() =>
    {
        for (var index = 0; index < 200; index++)
            gateway.HandleIncoming("deadlock-connection",
                CoopProtocolJson.Encode(new CharacterControlRequest(playerId, companion.Id)));
    });
    Assert(Task.WaitAll([simulation, lifecycle], TimeSpan.FromSeconds(5)),
        "A session-event és a gateway lifecycle egymás zárolására várt.");
}

static async Task SignalRServerStartsAndStops()
{
    var (session, _, _) = CreateSession();
    var hash = CatalogFingerprint.Compute(Encoding.UTF8.GetBytes("catalog"));
    var gateway = new CoopHostGateway(session, new SessionHandshakeService(session, "1.0.0", hash),
        new SessionReplicationPublisher());
    await using var server = await CoopSignalRServer.StartAsync(gateway, "http://127.0.0.1:0");
}

static async Task SignalRClientRunsLanProtocolFlow()
{
    var (session, leader, companion) = CreateSession();
    var hash = CatalogFingerprint.Compute(Encoding.UTF8.GetBytes("catalog"));
    var publisher = new SessionReplicationPublisher();
    var gateway = new CoopHostGateway(session, new SessionHandshakeService(session, "1.0.0", hash), publisher);
    await using var server = await CoopSignalRServer.StartAsync(gateway, "http://127.0.0.1:0");
    var address = server.Addresses.Single();
    Assert(!address.EndsWith(":0", StringComparison.Ordinal),
        "A Kestrel nem publikálta a dinamikusan választott portot.");

    await using var client = new CoopSignalRClient(address, "1.0.0", hash, "LAN vendég");
    var receivedSnapshot = new TaskCompletionSource<SessionSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
    client.SnapshotChanged += snapshot => receivedSnapshot.TrySetResult(snapshot);
    var protocolErrors = new List<CoopProtocolError>();
    client.ProtocolErrorReceived += protocolErrors.Add;
    var commandRejected = new TaskCompletionSource<GameCommandRejectedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
    client.CommandRejected += rejected => commandRejected.TrySetResult(rejected);
    var hello = await client.ConnectAsync();
    Assert(hello is { Accepted: true, PlayerId: { } } && client.State == CoopClientConnectionState.Connected,
        "A valódi SignalR kliens handshake-je sikertelen.");

    var control = await client.RequestCharacterControlAsync(companion.Id);
    Assert(control.Accepted && session.IsHumanControlled(companion.Id),
        "A SignalR kliens nem tudta átvenni az NPC irányítását.");

    var maze = new Maze(7, 7);
    maze.Carve(maze.Entrance);
    var fog = new FogOfWar(7, 7, 0);
    fog.RevealFrom(maze, maze.Entrance);
    var snapshot = session.CreateSnapshot(new SessionSnapshotContext(1, "SignalR pálya",
        new Dictionary<CharacterId, Position>
        {
            [leader.Id] = maze.Entrance,
            [companion.Id] = new Position(3, 2)
        }, World: WorldSnapshotProjector.Create(maze, fog)));
    await server.PublishSnapshotAsync(snapshot);
    var applied = await receivedSnapshot.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Assert(applied.SnapshotSequence == snapshot.SnapshotSequence && client.CurrentSnapshot?.World is not null,
        "A SignalR kliens nem alkalmazta a host teljes snapshotját.");

    var move = new MoveCharacterCommand(client.PlayerId!.Value, client.NextCommandId(), companion.Id,
        Direction.Right);
    await client.SendCommandAsync(move);
    GameCommand? accepted = null;
    for (var attempt = 0; attempt < 100 && accepted is null; attempt++)
    {
        if (session.TryReadCommand(out var queued)) accepted = queued;
        else await Task.Delay(10);
    }
    Assert(accepted == move,
        "A SignalR kliens commandja nem jutott el a host session queue-jáig.");
    await client.SendCommandAsync(move);
    var duplicateAccepted = false;
    for (var attempt = 0; attempt < 100 && !commandRejected.Task.IsCompleted; attempt++)
    {
        duplicateAccepted |= session.TryReadCommand(out _);
        if (!commandRejected.Task.IsCompleted) await Task.Delay(10);
    }
    Assert(!duplicateAccepted, "A host session elfogadta a duplikált hálózati commandot.");
    var rejectionSnapshot = session.CreateSnapshot(new SessionSnapshotContext(1, "SignalR pálya",
        new Dictionary<CharacterId, Position>
        {
            [leader.Id] = maze.Entrance,
            [companion.Id] = new Position(3, 2)
        }, World: WorldSnapshotProjector.Create(maze, fog)));
    await server.PublishSnapshotAsync(rejectionSnapshot);
    Assert((await commandRejected.Task.WaitAsync(TimeSpan.FromSeconds(5))).CommandId == move.CommandId,
        "A szimulációs szál command-elutasítása nem jutott vissza a SignalR klienshez.");
    Assert(protocolErrors.Count == 0, "A hibamentes SignalR folyamat közben protokollhiba érkezett.");
}

static async Task InMemoryTransportRunsProtocolFlow()
{
    var (session, leader, companion) = CreateSession();
    var hash = CatalogFingerprint.Compute(Encoding.UTF8.GetBytes("catalog"));
    var handshake = new SessionHandshakeService(session, "1.0.0", hash);
    var (host, client) = InMemoryCoopTransport.CreatePair();

    await client.SendAsync(CoopProtocolJson.Encode(new ClientHello(SessionProtocol.Version, "1.0.0", hash, "Vendég")));
    var hello = (ClientHello)CoopProtocolJson.Decode(await host.ReceiveAsync());
    var serverHello = handshake.Handle(hello);
    await host.SendAsync(CoopProtocolJson.Encode(serverHello));
    var accepted = (ServerHello)CoopProtocolJson.Decode(await client.ReceiveAsync());
    Assert(accepted is { Accepted: true, PlayerId: { } }, "Az in-memory handshake sikertelen.");
    var remote = accepted.PlayerId!.Value;
    Assert(session.TryAssignRemoteControl(remote, companion.Id, out var assignmentError), assignmentError);

    var move = new MoveCharacterCommand(remote, 1, companion.Id, Direction.Right);
    await client.SendAsync(CoopProtocolJson.Encode(move));
    var decodedMove = (MoveCharacterCommand)CoopProtocolJson.Decode(await host.ReceiveAsync());
    session.Submit(decodedMove);
    Assert(session.TryReadCommand(out var acceptedCommand) && acceptedCommand == move,
        "Az in-memory transporton érkezett commandot nem fogadta el a session.");

    var maze = new Maze(7, 7);
    maze.Carve(maze.Entrance);
    var fog = new FogOfWar(7, 7, 0);
    fog.RevealFrom(maze, maze.Entrance);
    var positions = new Dictionary<CharacterId, Position>
    {
        [leader.Id] = maze.Entrance,
        [companion.Id] = new Position(3, 2)
    };
    var snapshot = session.CreateSnapshot(new SessionSnapshotContext(1, "Wire pálya", positions,
        World: WorldSnapshotProjector.Create(maze, fog)));
    var publisher = new SessionReplicationPublisher();
    await host.SendAsync(CoopProtocolJson.Encode(publisher.CreateFrame(remote, snapshot)));
    var frame = (SessionReplicationFrame)CoopProtocolJson.Decode(await client.ReceiveAsync());
    Assert(frame.Kind == SessionReplicationFrameKind.FullSnapshot && frame.Session.World is not null,
        "Az első replikációs frame nem jutott át az in-memory transporton.");
    await client.SendAsync(CoopProtocolJson.Encode(new SnapshotAck(remote, frame.Session.SnapshotSequence)));
    var ack = (SnapshotAck)CoopProtocolJson.Decode(await host.ReceiveAsync());
    Assert(publisher.TryAcknowledge(ack.PlayerId, ack.SnapshotSequence, out var ackError), ackError);
}

static (GameSession Session, LiveCharacter Leader, LiveCharacter Companion) CreateSession()
{
    var party = new Party();
    var leader = CreateCharacter("Leader");
    var companion = CreateCharacter("Companion");
    party.SetLeader(leader);
    party.Add(companion);
    return (new GameSession(party, leader), leader, companion);
}

static LiveCharacter CreateCharacter(string name, int vitality = 20,
    string characterClassId = CharacterClassIds.Harcos)
{
    var abilities = new PrimaryAbilities(5, 5, 5, 5);
    var race = new RaceDefinition("R001", "Ember", PrimaryAbilities.Zero);
    var characterClass = new CharacterClassDefinition(characterClassId, characterClassId, PrimaryAbilities.Zero, false, 1.0);
    return new LiveCharacter(name, race, characterClass, abilities, vitality, 0, 1, 0);
}

static void MagicItemIdentificationStatePersists()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var race = data.Races[0];
    var characterClass = data.CharacterClasses.First(value => value.Id == CharacterClassIds.Harcos);
    var character = new LiveCharacter("Azonosító", race, characterClass,
        new PrimaryAbilities(8, 8, 8, 8), 30, 0, 0, 0);
    var item = data.MagicItems.First(value => value.Rarity == ItemRarity.Magic);
    var instanceId = Guid.NewGuid();
    var curse = data.ItemCurses.First(value => value.CanAffect(item));
    var statefulItem = new InventoryItemInstanceState(instanceId, false, curse.Id, curse.Effect,
        curse.Value, curse.Strength);
    Assert(character.AddToBackpack(item, identified: false, instanceId, statefulItem),
        "Az azonosítatlan tárgy nem került a hátizsákba.");

    var hidden = InventorySnapshotProjector.Create(character).Slots.Single(slot =>
        slot.Kind == InventorySlotKind.Backpack && slot.Index == 0).Item!;
    Assert(!hidden.IsIdentified && hidden.InstanceId == instanceId && hidden.DefinitionId.Length == 0 &&
           hidden.Name.Contains("Ismeretlen", StringComparison.OrdinalIgnoreCase) && hidden.BasePrice == 0 &&
           hidden.MagicPower == 0 && hidden.MaximumCharges == 0,
        "A snapshot kiszivárogtatta az azonosítatlan tárgy tulajdonságait.");

    var charges = character.GetInventoryItemCharges(InventorySlotKind.Backpack, 0);
    var state = character.GetInventoryItemState(InventorySlotKind.Backpack, 0);
    character.ApplyInventoryChanges(
        new InventorySlotChange(InventorySlotKind.Backpack, 0, null),
        new InventorySlotChange(InventorySlotKind.Backpack, 1, item, charges, 1, state));
    Assert(character.GetInventoryItemState(InventorySlotKind.Backpack, 1)?.InstanceId == instanceId &&
           !character.IsInventoryItemIdentified(InventorySlotKind.Backpack, 1),
        "A mozgatás nem őrizte meg a tárgypéldány állapotát.");

    var saves = new CharacterSaveService(Path.Combine(Path.GetTempPath(), "unused-identification-save.json"), data);
    var restored = saves.DeserializeCharacter(saves.SerializeCharacter(character));
    Assert(restored.GetInventoryItemState(InventorySlotKind.Backpack, 1)?.InstanceId == instanceId &&
           !restored.IsInventoryItemIdentified(InventorySlotKind.Backpack, 1) &&
           restored.GetInventoryItemState(InventorySlotKind.Backpack, 1)?.CurseId == curse.Id,
        "A mentés nem őrizte meg az azonosítási állapotot.");
    Assert(restored.IdentifyInventoryItem(InventorySlotKind.Backpack, 1),
        "A tárgy nem volt azonosítható.");
    var revealed = InventorySnapshotProjector.Create(restored).Slots.Single(slot =>
        slot.Kind == InventorySlotKind.Backpack && slot.Index == 1).Item!;
    Assert(revealed.IsIdentified && revealed.DefinitionId == item.Id &&
           revealed.Name.StartsWith(item.Name, StringComparison.Ordinal) &&
           revealed.CurseId == curse.Id && revealed.InstanceId == instanceId,
        "Az azonosítás nem fedte fel a valódi tárgyat vagy lecserélte a példányazonosítót.");
}

static void EquipmentDurabilityDataAndStatePersist()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var dagger = data.GetWeapon("W001");
    var greatsword = data.GetWeapon("W009");
    var naturalWeapon = data.GetWeapon("WN001");
    var clothArmor = data.GetArmor("A001");
    var plateArmor = data.GetArmor("A006");
    Assert(dagger.MaximumDurability == 80 && greatsword.MaximumDurability == 120 &&
           naturalWeapon.MaximumDurability == 0 && clothArmor.MaximumDurability == 90 &&
           plateArmor.MaximumDurability == 150,
        "A felszerelések alap-tartóssága nem a súlyuk és típusuk szerinti adatból érkezik.");
    Assert(data.GetWeapon("LW014").MaximumDurability == 0,
        "A soha meg nem repedő Tölgykirály pajzsa kopó tárggyá vált.");

    var original = InventoryItemInstanceState.Create();
    var worn = EquipmentDurabilityRules.ApplyWear(dagger, original, 35);
    var broken = EquipmentDurabilityRules.ApplyWear(dagger, worn, 1000);
    var repaired = EquipmentDurabilityRules.Repair(dagger, broken, 20);
    Assert(worn.DurabilityDamage == 35 && EquipmentDurabilityRules.CurrentDurability(dagger, worn) == 45 &&
           broken.DurabilityDamage == dagger.MaximumDurability &&
           EquipmentDurabilityRules.CurrentDurability(dagger, broken) == 0 &&
           EquipmentDurabilityRules.CurrentDurability(dagger, repaired) == 20,
        "A kopás, törés vagy javítás nem marad a tartóssági határok között.");
    Assert(EquipmentDurabilityRules.ApplyWear(naturalWeapon, original, 10) == original,
        "A természetes szörnyfegyver példánykopást kapott.");

    var character = CreateCharacter("Kopásteszt");
    Assert(character.SetInventoryItem(InventorySlotKind.Weapon, 0, dagger, null, 1, worn),
        "A kopott tesztfegyvert nem lehetett felszerelni.");
    var saves = new CharacterSaveService(Path.Combine(Path.GetTempPath(), "unused-durability-save.json"), data);
    var restored = saves.DeserializeCharacter(saves.SerializeCharacter(character));
    var restoredState = restored.GetInventoryItemState(InventorySlotKind.Weapon, 0);
    Assert(restoredState?.DurabilityDamage == 35 &&
           InventorySnapshotProjector.Create(restored).Slots.Single(slot =>
               slot.Kind == InventorySlotKind.Weapon && slot.Index == 0).Item is
               { MaximumDurability: 80, DurabilityDamage: 35 },
        "A fegyver kopása nem élte túl a mentési vagy coop-pillanatkép körutat.");

    var legacy = GameSaveFormat.MigrateToCurrent(new GameSaveData { Version = 20 });
    Assert(legacy.Version == GameSaveFormat.CurrentVersion,
        "A kopás előtti játékmentés nem migrálódott az aktuális formátumra.");
}

static void EquipmentDurabilityIsVisible()
{
    Assert(EquipmentDurabilityRules.Condition(100, 49) == EquipmentCondition.Intact &&
           EquipmentDurabilityRules.Condition(100, 50) == EquipmentCondition.Worn &&
           EquipmentDurabilityRules.Condition(100, 75) == EquipmentCondition.Damaged &&
           EquipmentDurabilityRules.Condition(100, 100) == EquipmentCondition.Broken &&
           EquipmentDurabilityRules.Condition(120, 119) == EquipmentCondition.Damaged &&
           EquipmentDurabilityRules.Condition(0, 0) == EquipmentCondition.NotApplicable,
        "A tartóssági állapotok határértékei nem 51–100 / 26–50 / 1–25 / 0 százaléknál vannak.");

    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var dagger = data.GetWeapon("W001");
    var wornState = InventoryItemInstanceState.Create() with { DurabilityDamage = 40 };
    var inspection = ItemInspectionFormatter.Format(dagger, data, instanceState: wornState);
    Assert(inspection.Text.Contains("Tartósság: 40/80 (50%)", StringComparison.Ordinal) &&
           inspection.Text.Contains("kopott", StringComparison.OrdinalIgnoreCase),
        "A tárgyvizsgálat nem mutatja a kopott fegyver pontos tartósságát és állapotát.");

    var character = CreateCharacter("Állapotjelző");
    Assert(character.SetInventoryItem(InventorySlotKind.Weapon, 0, dagger, null, 1, wornState),
        "A kopott tesztfegyvert nem lehetett felszerelni.");
    var inventory = InventorySnapshotProjector.Create(character);
    var sheet = CharacterSheetSnapshotProjector.Create(character, data.ExperienceByLevel, 0);
    var snapshot = new SessionCharacterSnapshot(character.Id, character.Name, character.Race.Id,
        character.CharacterClass.Id, character.Level, character.CurrentVitality, character.MaximumVitality,
        character.CurrentMana, character.MaximumMana, character.FoodLevel, character.WaterLevel, character.Gold,
        character.IsAlive, null, [], inventory, sheet, character.Color);
    var detailsLine = CharacterDetailsWindow.Build(snapshot, data)
        .Single(line => line.Text.Contains($"Fegyver 1: {dagger.Name}", StringComparison.Ordinal));
    Assert(detailsLine.Text.Contains("Tartósság: 40/80 (50%)", StringComparison.Ordinal) &&
           detailsLine.Color == ConsoleColor.Yellow,
        "A részletes karakterinfó nem mutatja vagy nem színezi a kopott felszerelést.");
    var compactLine = CharacterSheetPanel.Build(snapshot, 0, 0, 0)
        .Single(line => line.Row == 18);
    Assert(!compactLine.Text.Contains("Tartósság", StringComparison.Ordinal) &&
           !compactLine.Text.Contains("🟡", StringComparison.Ordinal) &&
           compactLine.ColoredTextStart == "1: ".Length &&
           compactLine.ColoredTextColor == ConsoleColor.Yellow,
        "A kompakt karakterlap nem helytakarékosan, a kopott fegyver nevét sárgítva jelez.");

    var unidentified = new InventoryItemSnapshot(string.Empty, "❓ Azonosítatlan mágikus fegyver",
        ItemCategory.Weapon, ItemRarity.Magic, 0, 0, Description: "erős mágikus aura",
        IsIdentified: false, MaximumDurability: 120, DurabilityDamage: 90);
    Assert(ItemInspectionFormatter.FormatUnidentified(unidentified).Text.Contains(
            "Tartósság: 30/120 (25%)", StringComparison.Ordinal),
        "Az azonosítatlan felszerelés szemmel látható fizikai állapota rejtve maradt.");
}

static void CombatAppliesEquipmentWear()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var attacker = CreateCharacter("Koptató", 1000);
    var weapon = data.GetWeapon("W001");
    Assert(attacker.EquipWeapon(0, weapon), "A kopási teszt fegyvere nem volt felszerelhető.");
    var enemy = CreateEnemy(10000, 1);
    var attackSystem = CreateBattleSystem(17);
    var attackerRuntime = attackSystem.PrepareTeamCharacter(attacker).Runtime;
    var observedWeaponHits = 0;
    for (var attempt = 0; attempt < 40 && observedWeaponHits < 3; attempt++)
    {
        var before = attacker.GetInventoryItemState(InventorySlotKind.Weapon, 0)!.Value.DurabilityDamage;
        var enemyVitalityBefore = enemy.CurrentHitPoints;
        var entry = attackSystem.ResolveTeamCharacterAttack(attacker, attackerRuntime, enemy,
            finishAction: false);
        var after = attacker.GetInventoryItemState(InventorySlotKind.Weapon, 0)!.Value.DurabilityDamage;
        var hit = enemy.CurrentHitPoints < enemyVitalityBefore;
        var expectedWear = hit ? entry.Kind == BattleLogKind.CriticalHit ? 2 : 1 : 0;
        Assert(after - before == expectedWear,
            "A sikeres, kritikus vagy elhibázott fegyvertámadás nem a megfelelő kopást okozta.");
        if (!hit) continue;
        observedWeaponHits++;
        Assert(entry.Details?.Calculation.Any(line => line.Contains("Fegyverkopás", StringComparison.Ordinal)) == true,
            "A fegyverkopás nem került be a csatarészletek közé.");
    }
    Assert(observedWeaponHits >= 3 && attacker.InventoryRevision > 1,
        "Nem sikerült több fegyverkopást megfigyelni, vagy az inventory revízió nem változott.");

    Assert(attacker.SetInventoryItem(InventorySlotKind.Weapon, 0, weapon, null, 1,
            InventoryItemInstanceState.Create() with { DurabilityDamage = weapon.MaximumDurability - 1 }),
        "A majdnem törött tesztfegyvert nem lehetett felszerelni.");
    BattleLogEntry? weaponBreak = null;
    for (var attempt = 0; attempt < 40 && weaponBreak is null; attempt++)
    {
        var entry = attackSystem.ResolveTeamCharacterAttack(attacker, attackerRuntime, enemy,
            finishAction: false);
        if (entry.FollowUps?.Any(notice => notice.Message.Contains("eltört", StringComparison.OrdinalIgnoreCase)) == true)
            weaponBreak = entry;
    }
    Assert(weaponBreak?.FollowUps?.Any(notice =>
               notice.Message.Contains(attacker.Name, StringComparison.Ordinal) &&
               notice.Message.Contains(weapon.Name, StringComparison.Ordinal) &&
               notice.Message.Contains("nem használható", StringComparison.OrdinalIgnoreCase)) == true,
        "A fegyver törése nem adott külön, következményt is leíró csatalog-üzenetet.");

    var defender = CreateCharacter("Vértvizsgáló", 1000);
    var armor = data.GetArmor("A001");
    var shield = data.GetWeapon("W014");
    Assert(defender.EquipWeapon(1, shield) && defender.EquipArmor(armor),
        "A kopási teszt páncélja vagy pajzsa nem volt felszerelhető.");
    var armoredEnemy = CreateEnemy(10000, 5);
    var defenseSystem = CreateBattleSystem(29);
    var defenderRuntime = defenseSystem.PrepareTeamCharacter(defender).Runtime;
    var observedArmorHits = 0;
    for (var attempt = 0; attempt < 40 && observedArmorHits < 3; attempt++)
    {
        var armorBefore = defender.GetInventoryItemState(InventorySlotKind.Armor, 0)!.Value.DurabilityDamage;
        var shieldBefore = defender.GetInventoryItemState(InventorySlotKind.Weapon, 1)!.Value.DurabilityDamage;
        var resolution = defenseSystem.ResolveTeamEnemyActionDetailed(armoredEnemy, defender, defenderRuntime);
        var armorAfter = defender.GetInventoryItemState(InventorySlotKind.Armor, 0)!.Value.DurabilityDamage;
        var shieldAfter = defender.GetInventoryItemState(InventorySlotKind.Weapon, 1)!.Value.DurabilityDamage;
        var expectedWear = resolution.Hit ? resolution.Entry.Kind == BattleLogKind.CriticalHit ? 2 : 1 : 0;
        Assert(armorAfter - armorBefore == expectedWear && shieldAfter - shieldBefore == expectedWear,
            "A fizikai találat nem egyformán és a kritikus szabály szerint koptatta a páncélt és pajzsot.");
        if (!resolution.Hit) continue;
        observedArmorHits++;
        Assert(resolution.Entry.Details?.Calculation.Any(line => line.Contains("Páncélkopás", StringComparison.Ordinal)) == true &&
               resolution.Entry.Details.Calculation.Any(line => line.Contains("Pajzskopás", StringComparison.Ordinal)),
            "A páncél- vagy pajzskopás nem került be a csatarészletek közé.");
    }
    Assert(observedArmorHits >= 3, "Nem sikerült több páncélt érő találatot megfigyelni.");

    var breakingDefender = CreateCharacter("Törő vértes", 1000);
    Assert(breakingDefender.SetInventoryItem(InventorySlotKind.Armor, 0, armor, null, 1,
               InventoryItemInstanceState.Create() with { DurabilityDamage = armor.MaximumDurability - 1 }) &&
           breakingDefender.SetInventoryItem(InventorySlotKind.Weapon, 1, shield, null, 1,
               InventoryItemInstanceState.Create() with { DurabilityDamage = shield.MaximumDurability - 1 }),
        "A majdnem törött páncélt vagy pajzsot nem lehetett felszerelni.");
    var breakingEnemyWeapon = weapon with { Id = "W-BREAK-NOTICE", Damage = new ValueRange(1, 1) };
    var breakingEnemy = new ConfiguredEnemy(new Position(1, 1),
        CreateEnemy(10000, 5, speed: 100).Definition with { Weapon = breakingEnemyWeapon });
    var breakingSystem = CreateBattleSystem(31);
    var breakingRuntime = breakingSystem.PrepareTeamCharacter(breakingDefender).Runtime;
    var defensiveBreakNotices = new List<BattleLogNotice>();
    for (var attempt = 0; attempt < 40 && defensiveBreakNotices.Count < 2; attempt++)
    {
        var resolution = breakingSystem.ResolveTeamEnemyActionDetailed(
            breakingEnemy, breakingDefender, breakingRuntime, breakingEnemyWeapon);
        if (resolution.Entry.FollowUps is { } followUps) defensiveBreakNotices.AddRange(followUps);
    }
    Assert(defensiveBreakNotices.Count(notice =>
               notice.Message.Contains("eltört", StringComparison.OrdinalIgnoreCase) &&
               notice.Message.Contains("nem ad védelmet", StringComparison.OrdinalIgnoreCase)) == 2,
        "A páncél és a pajzs törése nem adott külön, következményt is leíró csatalog-üzenetet. " +
        string.Join(" | ", defensiveBreakNotices.Select(notice => notice.Message)));

    var indestructible = shield with { Id = "W-INDESTRUCTIBLE-TEST", MaximumDurability = 0 };
    Assert(defender.SetInventoryItem(InventorySlotKind.Weapon, 1, indestructible, null, 1),
        "A törhetetlen pajzsot nem lehetett felszerelni.");
    Assert(!defender.ApplyInventoryItemWear(InventorySlotKind.Weapon, 1, 100).Changed &&
           defender.GetInventoryItemState(InventorySlotKind.Weapon, 1)!.Value.DurabilityDamage == 0,
        "A nulla maximális tartósságú, törhetetlen felszerelés kopást kapott.");
}

static void AcidAndChaosCauseSpecialEquipmentWear()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var armor = data.GetArmor("A001");
    var shield = data.GetWeapon("W014");
    var weapon = data.GetWeapon("W001");

    (TeamEnemyAttackResolution Resolution, int ArmorWear, int ShieldWear, int WeaponWear) ResolveHit(
        DamageType damageType, int seed)
    {
        var defender = CreateCharacter($"{damageType.Name()} célpont", 1000);
        Assert(defender.EquipWeapon(0, weapon) && defender.EquipWeapon(1, shield) && defender.EquipArmor(armor),
            "A különleges kopási teszt felszerelése nem volt feladható.");
        var attackWeapon = data.GetWeapon("WN003") with
        {
            Id = $"W-{damageType}-WEAR-TEST",
            Damage = new ValueRange(1, 1),
            DamageType = damageType
        };
        var enemy = new ConfiguredEnemy(new Position(1, 1),
            CreateEnemy(10000, 5, speed: 100).Definition with { Weapon = attackWeapon });
        var system = CreateBattleSystem(seed);
        var runtime = system.PrepareTeamCharacter(defender).Runtime;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            var resolution = system.ResolveTeamEnemyActionDetailed(enemy, defender, runtime, attackWeapon);
            if (!resolution.Hit) continue;
            return (resolution,
                defender.GetInventoryItemState(InventorySlotKind.Armor, 0)!.Value.DurabilityDamage,
                defender.GetInventoryItemState(InventorySlotKind.Weapon, 1)!.Value.DurabilityDamage,
                defender.GetInventoryItemState(InventorySlotKind.Weapon, 0)!.Value.DurabilityDamage);
        }
        throw new InvalidOperationException("A különleges kopási próba negyven támadásból sem talált.");
    }

    var acid = ResolveHit(DamageType.Acid, 71);
    var expectedAcidWear = acid.Resolution.Entry.Kind == BattleLogKind.CriticalHit ? 4 : 2;
    Assert(acid.ArmorWear == expectedAcidWear && acid.ShieldWear == expectedAcidWear && acid.WeaponWear == 0 &&
           acid.Resolution.Entry.Details?.Calculation.Any(line =>
               line.Contains("Savmarás", StringComparison.Ordinal)) == true,
        "A sav nem kétszeres alapkopással marta a páncélt és a pajzsot.");

    var chaos = ResolveHit(DamageType.Chaos, 73);
    var chaosWear = chaos.ArmorWear + chaos.ShieldWear + chaos.WeaponWear;
    var chaosMaximum = chaos.Resolution.Entry.Kind == BattleLogKind.CriticalHit ? 6 : 3;
    Assert(chaosWear >= 1 && chaosWear <= chaosMaximum &&
           new[] { chaos.ArmorWear, chaos.ShieldWear, chaos.WeaponWear }.Count(value => value > 0) == 1 &&
           chaos.Resolution.Entry.Details?.Calculation.Any(line =>
               line.Contains("Káoszmarás", StringComparison.Ordinal)) == true,
        "A káoszsebzés nem egyetlen véletlen aktív felszerelést koptatott 1–3 ponttal.");
}

static void DamagedAndBrokenEquipmentAffectsCombat()
{
    Assert(EquipmentDurabilityRules.WeaponHitPenalty(EquipmentCondition.Worn) == 0 &&
           EquipmentDurabilityRules.WeaponDamagePenalty(EquipmentCondition.Damaged) == 1 &&
           EquipmentDurabilityRules.ScaleDefense(9, EquipmentCondition.Damaged) == 5 &&
           EquipmentDurabilityRules.ScaleDefense(-3, EquipmentCondition.Intact) == -3 &&
           EquipmentDurabilityRules.ScaleDefense(-3, EquipmentCondition.Damaged) == -2 &&
           EquipmentDurabilityRules.ScaleDefense(9, EquipmentCondition.Broken) == 0,
        "A kopott, sérült vagy törött felszerelés alapvető harci módosítói hibásak.");

    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { CharacterClassIds.Harcos };
    var weapon = data.GetWeapon("W001") with
    {
        Id = "W-CONDITION-TEST", Damage = new ValueRange(5, 5), MaximumDurability = 10000,
        AllowedClassIds = allowed
    };

    (int Damage, BattleLogEntry? DamagedEntry) AttackSeries(int durabilityDamage)
    {
        var character = CreateCharacter("Kopott támadó", 1000);
        Assert(character.SetInventoryItem(InventorySlotKind.Weapon, 0, weapon, null, 1,
                InventoryItemInstanceState.Create() with { DurabilityDamage = durabilityDamage }),
            "Az állapotteszt fegyvere nem volt felszerelhető.");
        var system = CreateBattleSystem(41);
        var runtime = system.PrepareTeamCharacter(character).Runtime;
        var enemy = CreateEnemy(100000, 1);
        BattleLogEntry? firstHit = null;
        for (var attack = 0; attack < 100; attack++)
        {
            var before = enemy.CurrentHitPoints;
            var entry = system.ResolveTeamCharacterAttack(character, runtime, enemy, finishAction: false);
            if (enemy.CurrentHitPoints < before && firstHit is null) firstHit = entry;
        }
        return (100000 - enemy.CurrentHitPoints, firstHit);
    }

    var intactAttack = AttackSeries(0);
    var damagedAttack = AttackSeries(7500);
    Assert(damagedAttack.Damage < intactAttack.Damage &&
           damagedAttack.DamagedEntry?.Details?.Calculation.Any(line =>
               line.Contains("Sérült fegyver: találat", StringComparison.Ordinal) ||
               line.Contains("sérült fegyver -1 sebzés", StringComparison.OrdinalIgnoreCase)) == true,
        "A sérült fegyver nem csökkentette a találati esélyt és a sebzést a közös harci motorban.");

    var brokenAttacker = CreateCharacter("Töröttkezű");
    Assert(brokenAttacker.SetInventoryItem(InventorySlotKind.Weapon, 0, weapon, null, 1,
            InventoryItemInstanceState.Create() with { DurabilityDamage = weapon.MaximumDurability }),
        "A törött tesztfegyvert nem lehetett felszerelve tárolni.");
    Assert(brokenAttacker.WeaponSlots[0] == weapon && brokenAttacker.AttackWeapon is null &&
           !brokenAttacker.IsInventoryItemOperational(InventorySlotKind.Weapon, 0),
        "A törött fegyver eltűnt a slotból vagy továbbra is használható maradt.");
    Assert(ItemInspectionFormatter.Format(weapon, data,
               instanceState: brokenAttacker.GetInventoryItemState(InventorySlotKind.Weapon, 0)).Text
            .Contains("nem használható fegyverként", StringComparison.OrdinalIgnoreCase),
        "A tárgyvizsgálat nem magyarázza el a törött fegyver következményét.");

    var armor = data.GetArmor("A001") with
    {
        Id = "A-CONDITION-TEST", Defense = new ValueRange(10, 10), MaximumDurability = 10000,
        AllowedClassIds = allowed
    };
    int DamageReceived(int durabilityDamage)
    {
        var defender = CreateCharacter("Kopott védő", 100000);
        Assert(defender.SetInventoryItem(InventorySlotKind.Armor, 0, armor, null, 1,
                InventoryItemInstanceState.Create() with { DurabilityDamage = durabilityDamage }),
            "Az állapotteszt páncélja nem volt felszerelhető.");
        var enemyWeapon = weapon with { Id = "W-ENEMY-CONDITION", Damage = new ValueRange(20, 20) };
        var enemyDefinition = CreateEnemy(1000, 5).Definition with { Weapon = enemyWeapon };
        var enemy = new ConfiguredEnemy(new Position(1, 1), enemyDefinition);
        var system = CreateBattleSystem(67);
        var runtime = system.PrepareTeamCharacter(defender).Runtime;
        for (var attack = 0; attack < 100; attack++)
            system.ResolveTeamEnemyAction(enemy, defender, runtime, enemyWeapon);
        return 100000 - defender.CurrentVitality;
    }

    var intactArmorDamage = DamageReceived(0);
    var damagedArmorDamage = DamageReceived(7500);
    var brokenArmorDamage = DamageReceived(10000);
    Assert(intactArmorDamage < damagedArmorDamage && damagedArmorDamage < brokenArmorDamage,
        "A sérült páncél nem fél védelemmel, vagy a törött páncél nem védelem nélkül működött.");
    Assert(ItemInspectionFormatter.Format(armor, data,
               instanceState: InventoryItemInstanceState.Create() with { DurabilityDamage = 7500 }).Text
            .Contains("védelem 50%-a", StringComparison.OrdinalIgnoreCase),
        "A tárgyvizsgálat nem magyarázza el a sérült páncél következményét.");
}

static void EquipmentRepairRestoresDurability()
{
    var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { CharacterClassIds.Harcos };
    var normal = new WeaponDefinition("W-REPAIR-N", "Javítandó kard", "WT001", new ValueRange(2, 4), 1,
        false, allowed, "", 1000, Rarity: ItemRarity.Normal, MaximumDurability: 100);
    var magic = normal with { Id = "W-REPAIR-M", Rarity = ItemRarity.Magic };
    var legendary = normal with { Id = "W-REPAIR-L", Rarity = ItemRarity.Legendary };
    var state = new InventoryItemInstanceState(Guid.NewGuid(), false, "CURSE-TEST",
        ItemCurseEffect.HitPenalty, 1, 2, true, CharacterId.New(), DurabilityDamage: 60);
    Assert(EquipmentDurabilityRules.FullRepairCost(normal, state) == 150 &&
           EquipmentDurabilityRules.FullRepairCost(magic, state) == 210 &&
           EquipmentDurabilityRules.FullRepairCost(legendary, state) == 300 &&
           EquipmentDurabilityRules.FullRepairCost(normal, state with { DurabilityDamage = 0 }) == 0,
        "A teljes javítás díja nem a hiányzó tartósság 25/35/50%-os árhányadát követi.");

    var character = CreateCharacter("Javítás");
    var boundState = state with { BoundCharacterId = character.Id };
    Assert(character.SetInventoryItem(InventorySlotKind.Weapon, 0, magic, null, 1, boundState),
        "A javítási tesztfegyvert nem lehetett felszerelni.");
    var revisionBefore = character.InventoryRevision;
    Assert(character.RepairInventoryItemFully(InventorySlotKind.Weapon, 0),
        "A kopott felszerelés teljes javítása sikertelen volt.");
    var repaired = character.GetInventoryItemState(InventorySlotKind.Weapon, 0)!.Value;
    Assert(repaired.DurabilityDamage == 0 && repaired.InstanceId == boundState.InstanceId &&
           repaired.IsIdentified == boundState.IsIdentified && repaired.CurseId == boundState.CurseId &&
           repaired.IsCurseActivated && repaired.BoundCharacterId == character.Id &&
           character.InventoryRevision == revisionBefore + 1,
        "A javítás lecserélte a példányt, elvesztette az azonosítás/átok állapotát vagy nem frissített revíziót.");
    Assert(!character.RepairInventoryItemFully(InventorySlotKind.Weapon, 0),
        "A teljesen ép felszerelést ismét meg lehetett javítani.");

    var repairItem = new InventoryItemSnapshot(magic.Id, $"{character.Name}: {magic.Name}",
        ItemCategory.Weapon, magic.Rarity, 0, 0, Description: "Teljes javítás: 40/100 → 100/100 tartósság.",
        BasePrice: magic.BasePrice, MaximumDurability: 100, DurabilityDamage: 60);
    var repairVendor = new InnVendorSnapshot(InnVendorKind.BlacksmithRepair, "Javítóműhely",
        [new InnOfferSnapshot(0, repairItem, 210)]);
    var lines = ConsoleRenderer.BuildInnVendorLines(repairVendor, InnMarketMode.Buy, [], 0,
        500, 0, "Válassz javítást.", "Tesztfogadó");
    Assert(lines.Any(line => line.Text.Contains("FEGYVERJAVÍTÁS", StringComparison.Ordinal)) &&
           lines.Any(line => line.Text.Contains("40/100 → 100/100", StringComparison.Ordinal)) &&
           lines.Any(line => line.Text.Contains("Enter javítás", StringComparison.Ordinal)) &&
           lines.Any(line => line.Text.Contains("210", StringComparison.Ordinal)),
        "A közös host/vendég javítóképernyő nem mutatja az állapotváltozást, árat vagy vezérlést.");
}

static void MageIdentifiesFreshMagicLoot()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var race = data.Races[0];
    var mageClass = data.CharacterClasses.First(value => value.Id == CharacterClassIds.Mágus);
    var weakerMage = new LiveCharacter("Tanonc", race, mageClass, new PrimaryAbilities(5, 5, 5, 7),
        20, 30, 0, 0);
    var strongerMage = new LiveCharacter("Tudós", race, mageClass, new PrimaryAbilities(5, 5, 5, 12),
        20, 30, 0, 0);
    var fighter = CreateCharacter("Harcos");
    var item = data.MagicItems.Where(value => value.Rarity == ItemRarity.Magic)
        .OrderBy(value => value.MagicPower).First();
    var state = InventoryItemInstanceState.Create(identified: false);

    var result = ItemIdentificationRules.AttemptByBestMage(item, state,
        [fighter, weakerMage, strongerMage], new Random(1));

    Assert(result.Attempted && result.Mage == strongerMage,
        "Nem a legmagasabb effektív Intelligenciájú élő Mágus végezte a próbát.");
    Assert(result.ChancePercent == ItemIdentificationRules.MageIdentificationChance(strongerMage, item) &&
           result.Succeeded == (result.Roll <= result.ChancePercent) &&
           result.State.IsIdentified == result.Succeeded && result.State.InstanceId == state.InstanceId,
        "A mágusi azonosítás nem a dokumentált esély vagy dobás szerint módosította a példányállapotot.");

    var withoutMage = ItemIdentificationRules.AttemptByBestMage(item, state, [fighter], new Random(1));
    Assert(!withoutMage.Attempted && !withoutMage.State.IsIdentified,
        "Mágus nélkül is történt automatikus tárgyazonosítás.");
}

static void CursedItemsActivateBindAndApplyEffects()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    Assert(data.ItemCurses.Count == 8 && data.ItemCurses.Select(curse => curse.Effect).Distinct().Count() == 8,
        "A nyolc adatvezérelt átok nem töltődött be.");
    var character = CreateCharacter("Átokpróba", characterClassId: CharacterClassIds.Mágus);
    var item = new MagicItemDefinition("MI-CURSE", "Próbagyűrű", MagicItemKind.Ring, ItemRarity.Magic,
        1000, 0, null, MagicItemEffect.None, 0, new HashSet<string> { CharacterClassIds.Mágus },
        "Átokpróba", 3);
    var curse = data.ItemCurses.First(value => value.Effect == ItemCurseEffect.ManaCost);
    Assert(ItemIdentificationRules.CreateLootState(item, data.ItemCurses, new Random(1), 100).HasCurse,
        "A garantált átokdobás nem rendelt kompatibilis átkot a varázstárgyhoz.");
    var state = new InventoryItemInstanceState(Guid.NewGuid(), false, curse.Id, curse.Effect,
        curse.Value, curse.Strength);
    Assert(character.AddToBackpack(item, identified: false, state.InstanceId, state),
        "Az átkozott példány nem került a hátizsákba.");
    var party = new Party();
    party.SetLeader(character);
    var command = new InventoryTransferCommand(PlayerId.New(), 1, character.Id, character.InventoryRevision,
        InventorySlotKind.Backpack, 0, character.Id, character.InventoryRevision,
        InventorySlotKind.MagicItem, 0);
    Assert(InventoryTransferService.TryExecute(party, command, out var result, out var error), error);
    var activated = character.GetInventoryItemState(InventorySlotKind.MagicItem, 0);
    Assert(activated is { IsCurseActivated: true, HasCurse: true } &&
           activated.Value.BoundCharacterId == character.Id && result.CurseActivations?.Count == 1,
        "A felszerelt átok nem aktiválódott vagy nem kötődött a viselőhöz.");

    var remove = new InventoryTransferCommand(PlayerId.New(), 2, character.Id, character.InventoryRevision,
        InventorySlotKind.MagicItem, 0, character.Id, character.InventoryRevision,
        InventorySlotKind.Backpack, 1);
    Assert(!InventoryTransferService.TryExecute(party, remove, out _, out _),
        "Az aktív átkozott tárgy levehető volt.");
    Assert(character.GetActiveCurseValue(ItemCurseEffect.ManaCost) == curse.Value,
        "Az aktív átok értéke nem került be a karakter szabályaiba.");
    var spell = data.Spells.First(value => value.ManaCost > 0);
    Assert(SpellcastingRules.EffectiveManaCost(character, spell) == spell.ManaCost + curse.Value,
        "A Manafaló nem növelte a varázslat mannaköltségét.");

    Assert(character.IdentifyInventoryItem(InventorySlotKind.MagicItem, 0),
        "Az aktív átkozott tárgy nem volt azonosítható.");
    var snapshot = InventorySnapshotProjector.Create(character).Slots.Single(slot =>
        slot.Kind == InventorySlotKind.MagicItem && slot.Index == 0).Item!;
    Assert(snapshot.CurseId == curse.Id && snapshot.IsCurseActivated && snapshot.Name.Contains('☠'),
        "Az azonosítás nem fedte fel az aktív átkot.");
}

static void ItemCursePurificationIsPermanent()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var character = CreateCharacter("Tisztító", characterClassId: CharacterClassIds.Mágus);
    var item = new MagicItemDefinition("MI-PURIFY", "Próbagyűrű", MagicItemKind.Ring, ItemRarity.Magic,
        1000, 0, null, MagicItemEffect.None, 0, new HashSet<string> { CharacterClassIds.Mágus },
        "Átoktörési próba", 3);
    var weak = data.ItemCurses.First(curse => curse.Strength == 1 && curse.CanAffect(item));
    var strong = data.ItemCurses.First(curse => curse.Strength == 3 && curse.CanAffect(item));
    var weakState = new InventoryItemInstanceState(Guid.NewGuid(), true, weak.Id, weak.Effect,
        weak.Value, weak.Strength);
    var strongState = new InventoryItemInstanceState(Guid.NewGuid(), true, strong.Id, strong.Effect,
        strong.Value, strong.Strength);
    Assert(character.SetInventoryItem(InventorySlotKind.MagicItem, 0, item, 0, 1, weakState) &&
           character.SetInventoryItem(InventorySlotKind.MagicItem, 1, item, 0, 1, strongState),
        "Az átkozott próbatárgyak nem voltak felszerelhetők.");

    Assert(character.PurifyStrongestActiveCurse()?.Id == item.Id,
        "Az Átoktörés nem a legerősebb aktív tárgyátkot választotta.");
    var purified = character.GetInventoryItemState(InventorySlotKind.MagicItem, 1);
    Assert(purified is { IsPurified: true, IsCurseActivated: false, BoundCharacterId: null } &&
           !purified.Value.HasCurse && purified.Value.CurseId == strong.Id,
        "A megtisztítás nem őrizte meg az átok előéletét vagy nem oldotta fel a kötést.");
    Assert(character.SetInventoryItem(InventorySlotKind.MagicItem, 1, null, 0, 0),
        "A megtisztított tárgy továbbra sem volt levehető.");
    Assert(character.HasActiveCurse && character.PurifyInventoryItem(InventorySlotKind.MagicItem, 0) &&
           !character.HasActiveCurse,
        "A Vándormágus-jellegű célzott megtisztítás nem szüntette meg az aktív hátrányt.");
    Assert(ItemIdentificationRules.CurseRemovalPrice(item, weakState) == 50 + 120 + 75 + 100,
        "A Vándormágus átoktörési díja nem a dokumentált képletet követi.");
    Assert(data.GetSpellEffects("P027").Any(effect => effect.Type == SpellEffectType.BreakItemCurse),
        "Az Átoktörés varázslathoz nincs tárgyátok-tisztítás rendelve.");
}

static void CompactPartyStatusShowsResources()
{
    var race = new RaceDefinition("R001", "Ember", PrimaryAbilities.Zero);
    var mageClass = new CharacterClassDefinition(CharacterClassIds.Mágus, "Mágus", PrimaryAbilities.Zero,
        true, 1.0);
    var mage = new LiveCharacter("Hosszúnevű", race, mageClass, new PrimaryAbilities(5, 5, 5, 5),
        40, 20, 1, 0);
    mage.SetCurrentResources(10, 12);
    var status = CharacterSheetPanel.BuildPartyStatus(mage, true, isLeader: true);
    Assert(status.Text.Length <= CharacterSheetPanel.Width, "A party státusz túllóg a jobb panelen.");
    Assert(!status.Identity.Contains("👑", StringComparison.Ordinal) && status.InvertedNameStart >= 0,
        "A vezér neve nem inverz jelölést kapott a party státuszban.");
    Assert(status.Text.Contains("❤️25%", StringComparison.Ordinal) &&
           status.Text.Contains("🔷60%", StringComparison.Ordinal),
        "A party státusz nem százalékosan mutatja a HP-t és a manát.");
    Assert(status.VitalityColor == ConsoleColor.Red && status.ManaColor == ConsoleColor.Cyan,
        "A party státusz erőforrásszínei nem követik a százalékos küszöböket.");

    var fighter = CreateCharacter("Hosszú Harcos", vitality: 40,
        characterClassId: CharacterClassIds.Harcos);
    var fighterStatus = CharacterSheetPanel.BuildPartyStatus(fighter, false);
    Assert(string.IsNullOrEmpty(fighterStatus.Mana) &&
           !fighterStatus.Text.Contains("🔷", StringComparison.Ordinal) &&
           fighterStatus.Identity.Contains("Hosszú Harcos", StringComparison.Ordinal),
        "A manát nem használó karakter party státusza helyet foglal a manna számára.");
}

static void WindowFrameCatalogIsResizableAndConfigured()
{
    Assert(ConsoleRenderer.MessageLogLineCountForMonitorHeight(1080) == 7 &&
           ConsoleRenderer.MessageLogLineCountForMonitorHeight(1199) == 7 &&
           ConsoleRenderer.MessageLogLineCountForMonitorHeight(1200) == 11 &&
           ConsoleRenderer.MessageLogLineCountForMonitorHeight(2160) == 11 &&
           ConsoleRenderer.MessageLogBufferLineCount == ConsoleRenderer.MessageLogLineCount * 3 &&
           ConsoleRenderer.ScreenRowCount == ConsoleRenderer.PlayfieldHeight +
               ConsoleRenderer.MessageLogLineCount + 1,
        "A fő játékfelület 1200p-s négy extra logsora vagy a hozzá igazodó magassága hibás.");
    foreach (var style in Enum.GetValues<WindowFrameStyle>())
    {
        Assert(WindowFrameCatalog.Horizontal(style, 52).Length == 52,
            $"A(z) {style} keret felső sora nem tartja a kért szélességet.");
        Assert(WindowFrameCatalog.Horizontal(style, 27, bottom: true).Length == 27,
            $"A(z) {style} keret alsó sora nem tartja a kért szélességet.");
    }
    Assert(WindowFrameCatalog.Horizontal(WindowFrameStyle.Scroll2, 6) == "╭≈≈≈≈╮" &&
           WindowFrameCatalog.Horizontal(WindowFrameStyle.Scroll2, 6, bottom: true) == "╰≈≈≈≈╯" &&
           WindowFrameCatalog.Sides(WindowFrameStyle.Scroll2, 0, 3) == new WindowFrameRow(" )", "( ") &&
           WindowFrameCatalog.Sides(WindowFrameStyle.Scroll2, 1, 3) == new WindowFrameRow("( ", " )"),
        "A scroll2 keretsablon nem az előírt váltakozó tekercsformát adja.");
    Assert(WindowFrameCatalog.Adornment(WindowFrameStyle.Sword, 12) == "   ▲    ▲   " &&
           WindowFrameCatalog.Horizontal(WindowFrameStyle.Sword, 12) == "═══╪════╪═══" &&
           WindowFrameCatalog.Sides(WindowFrameStyle.Sword, 0, 3) == new WindowFrameRow("   │", "│   ") &&
           WindowFrameCatalog.Adornment(WindowFrameStyle.Sword, 12, bottom: true) == "   ▼    ▼   ",
        "A sword keretsablon kardjai és függőleges élei nem igazodnak egymáshoz.");
    Assert(WindowFrameCatalog.Horizontal(WindowFrameStyle.Magic2, 37) ==
           "· ✦ ─────── ◆ ───────── ◆ ─────── ✦ ·" &&
           WindowFrameCatalog.Horizontal(WindowFrameStyle.Magic2, 37, bottom: true) ==
           "· ✦ ─────── ◆ ───────── ◆ ─────── ✦ ·" &&
           WindowFrameCatalog.Sides(WindowFrameStyle.Magic2, 0, 3) == new WindowFrameRow("│", "│"),
        "A magic2 keretsablon nem az előírt szimmetrikus mágikus díszsort adja.");
    Assert(WindowFrameConfiguration.For(FramedWindow.MainMenu) == WindowFrameStyle.Ruby &&
           WindowFrameConfiguration.For(FramedWindow.Help) == WindowFrameStyle.Ruby &&
           WindowFrameConfiguration.For(FramedWindow.SpellSelector) == WindowFrameStyle.Magic &&
           WindowFrameConfiguration.For(FramedWindow.CreaturePortrait) == WindowFrameStyle.Stone &&
           WindowFrameConfiguration.For(FramedWindow.Storyline) == WindowFrameStyle.Stone &&
           WindowFrameConfiguration.For(FramedWindow.LevelUp) == WindowFrameStyle.Scroll2 &&
           WindowFrameConfiguration.For(FramedWindow.LevelUpChoice) == WindowFrameStyle.Sword &&
           WindowFrameConfiguration.For(FramedWindow.SpellLearning) == WindowFrameStyle.Magic2 &&
           WindowFrameConfiguration.For(FramedWindow.SpellPreparation) == WindowFrameStyle.Magic2 &&
           WindowFrameConfiguration.For(FramedWindow.Inn) == WindowFrameStyle.Ruby,
        "Az első körös ablak–keret alapbeállítások hibásak.");
}

static void RaceTraitsAreLoadedFromData()
{
    var dataPath = Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName);
    var catalog = CsvGameDataLoader.Load(dataPath);
    Assert(catalog.GetRace("R001").HasTrait(RaceTraits.Adaptable), "Az ember Alkalmazkodó tulajdonsága hiányzik.");
    Assert(catalog.GetRace("R002").HasTrait(RaceTraits.Resilient), "A törp Rendíthetetlen tulajdonsága hiányzik.");
    Assert(catalog.GetRace("R003").HasTrait(RaceTraits.KeenSenses), "Az elf Éles érzékek tulajdonsága hiányzik.");
    Assert(catalog.GetRace("R004").HasTrait(RaceTraits.Relentless), "A félork Könyörtelen tulajdonsága hiányzik.");
}

static void CharacterSheetColorsHealthAndManaSeparately()
{
    var race = new RaceDefinition("R001", "Ember", PrimaryAbilities.Zero);
    var mageClass = new CharacterClassDefinition(CharacterClassIds.Mágus, "Mágus", PrimaryAbilities.Zero,
        true, 1.0);
    var mage = new LiveCharacter("Színpróba", race, mageClass, new PrimaryAbilities(5, 5, 5, 8),
        20, 20, 1, 1);

    var full = CharacterSheetPanel.BuildResourceLine(mage);
    Assert(full.VitalityColor == ConsoleColor.Green && full.ManaColor == ConsoleColor.Cyan,
        "A teljes HP vagy a manna színe hibás.");
    mage.SetCurrentResources(10, 10);
    Assert(CharacterSheetPanel.BuildResourceLine(mage).VitalityColor == ConsoleColor.Green,
        "A pontosan fél HP tévesen piros.");
    mage.SetCurrentResources(9, 10);
    var low = CharacterSheetPanel.BuildResourceLine(mage);
    Assert(low.VitalityColor == ConsoleColor.Red && low.ManaColor == ConsoleColor.Cyan,
        "A fél HP alatti érték nem piros, vagy a manna nem maradt külön színű.");
}

static void SpellSchoolsIncludeMageLightSpell()
{
    var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    foreach (var school in Enum.GetValues<SpellSchool>())
        for (var level = 1; level <= 5; level++)
            Assert(catalog.GetSpells(school, level).Count == (level <= 3 ? 6 : 5),
                $"A(z) {school} iskola {level}. szintjén hibás a varázslatok száma.");

    var light = catalog.GetSpell("S026");
    Assert(light.Name == "Fényvarázslat" && light.Level == 1 &&
           light.UsageMode == SpellUsageMode.Exploration && light.TargetType == SpellTargetType.Self &&
           catalog.GetSpellEffects(light.Id).Single() is
               { Type: SpellEffectType.VisionBonus, Value: 2, Duration: 12 },
        "A Fényvarázslat adatai vagy látótávhatása hibás.");

    var creation = catalog.GetSpell("P021");
    Assert(creation.Name == "Étel és ital teremtése" && creation.Level == 1 &&
           creation.UsageMode == SpellUsageMode.Exploration &&
           catalog.GetSpellEffects(creation.Id).Single() is { Type: SpellEffectType.RestoreNeeds, Value: 35 },
        "Az első szintű Étel és ital teremtése varázslat adatai vagy hatása hibás.");
}

static void SpellMemorizationCapacityUsesClassFormula()
{
    static LiveCharacter Caster(string classId, int level)
    {
        var race = new RaceDefinition("R-MEM", "Teszt", PrimaryAbilities.Zero);
        var characterClass = new CharacterClassDefinition(classId, classId, PrimaryAbilities.Zero, true, 1.0);
        var character = new LiveCharacter("Memória", race, characterClass,
            new PrimaryAbilities(5, 5, 5, 8), 20, 20, 1, 1);
        character.SetProgress(level, 0);
        return character;
    }

    Assert(Caster(CharacterClassIds.Mágus, 1).MemorizationCapacity == 4 &&
           Caster(CharacterClassIds.Mágus, 30).MemorizationCapacity == 10,
        "A mágus memóriaképlete hibás.");
    Assert(Caster(CharacterClassIds.Pap, 1).MemorizationCapacity == 4 &&
           Caster(CharacterClassIds.Pap, 30).MemorizationCapacity == 10,
        "A pap memóriaképlete hibás.");
    Assert(Caster(CharacterClassIds.Lovag, 1).MemorizationCapacity == 2 &&
           Caster(CharacterClassIds.Lovag, 10).MemorizationCapacity == 3 &&
           Caster(CharacterClassIds.Lovag, 20).MemorizationCapacity == 4 &&
           Caster(CharacterClassIds.Lovag, 30).MemorizationCapacity == 4,
        "A lovag memóriaképlete vagy négyhelyes korlátja hibás.");
}

static void InventoryStackSplitIsAtomicAndRequiresSpace()
{
    var party = new Party();
    var character = CreateCharacter("StackSplit");
    party.SetLeader(character);
    var item = new MiscItemDefinition("I-STACK", "Dobókés", "Teszt", 1);
    for (var index = 0; index < 5; index++)
        Assert(character.AddToBackpack(item), "A tesztköteg nem fért a hátizsákba.");
    var revision = character.InventoryRevision;
    var command = new SplitInventoryStackCommand(PlayerId.New(), 1, character.Id, revision, 0);

    Assert(InventoryStackService.TryExecute(party, command, out var result, out var error), error);
    Assert(character.GetInventoryItemQuantity(InventorySlotKind.Backpack, 0) == 3 &&
           character.GetInventoryItemQuantity(InventorySlotKind.Backpack, result.DestinationIndex) == 2 &&
           character.InventoryRevision == revision + 1,
        "Az 5 darabos köteg nem atomi 3+2 kötegre vált szét.");

    for (var index = 0; index < LiveCharacter.MaximumBackpackItemCount; index++)
    {
        if (character.GetInventoryItem(InventorySlotKind.Backpack, index) is not null) continue;
        Assert(character.SetInventoryItem(InventorySlotKind.Backpack, index,
            new MiscItemDefinition($"I-FILL-{index}", $"Töltelék {index}", "Teszt", 1)),
            "A tele hátizsák tesztjének előkészítése sikertelen.");
    }
    var fullCommand = command with { CommandId = 2, ExpectedInventoryRevision = character.InventoryRevision };
    Assert(!InventoryStackService.TryExecute(party, fullCommand, out _, out error) &&
           error.Contains("nincs üres hely", StringComparison.OrdinalIgnoreCase),
        "A tele hátizsák felezése nem adott egyértelmű figyelmeztetést.");
}

static void ConsumableStackDistributesEvenly()
{
    var party = new Party();
    var source = CreateCharacter("Forrás");
    var second = CreateCharacter("Második");
    var third = CreateCharacter("Harmadik");
    party.SetLeader(source);
    party.Add(second);
    party.Add(third);
    var ration = new MiscItemDefinition("I-DISTRIBUTE", "Útravaló", "Teszt", 1,
        ConsumableEffect.Food, 25);
    for (var index = 0; index < 9; index++)
        Assert(source.AddToBackpack(ration), "A szétosztási teszt forráskötege nem fért el.");
    var sourceRevision = source.InventoryRevision;
    var secondRevision = second.InventoryRevision;
    var thirdRevision = third.InventoryRevision;
    var command = new DistributeInventoryStackCommand(PlayerId.New(), 1, source.Id, sourceRevision, 0);

    Assert(InventoryDistributionService.TryExecute(party, command, out var result, out var error), error);
    Assert(source.GetInventoryItemQuantity(InventorySlotKind.Backpack, 0) == 3 &&
           second.GetInventoryItemQuantity(InventorySlotKind.Backpack, 0) == 3 &&
           third.GetInventoryItemQuantity(InventorySlotKind.Backpack, 0) == 3 &&
           result.DistributedQuantity == 6 && result.RemainingSourceQuantity == 3 &&
           source.InventoryRevision == sourceRevision + 1 &&
           second.InventoryRevision == secondRevision + 1 && third.InventoryRevision == thirdRevision + 1,
        "A kilences fogyóeszközköteg nem 3–3–3 arányban, atomi revíziónöveléssel oszlott szét.");

    var nonConsumable = new MiscItemDefinition("I-NONDISTRIBUTE", "Dísztárgy", "Teszt", 1);
    Assert(source.AddToBackpack(nonConsumable), "A nem fogyasztható teszttárgy nem fért el.");
    var nonConsumableIndex = Enumerable.Range(0, LiveCharacter.MaximumBackpackItemCount)
        .Single(index => source.GetInventoryItem(InventorySlotKind.Backpack, index)?.Id == nonConsumable.Id);
    var invalid = command with { CommandId = 2, ExpectedInventoryRevision = source.InventoryRevision,
        BackpackIndex = nonConsumableIndex };
    Assert(!InventoryDistributionService.TryExecute(party, invalid, out _, out error) &&
           error.Contains("elfogyasztható", StringComparison.OrdinalIgnoreCase),
        "A szétosztás elfogadott egy nem elfogyasztható tárgyat.");
}

static void ConsumableStackHalfTransfersToFollower()
{
    var source = CreateCharacter("Átadó");
    var follower = CreateCharacter("Követő");
    var ration = new MiscItemDefinition("I-FOLLOWER-GIFT", "Útravaló", "Teszt", 1,
        ConsumableEffect.Food, 25);
    for (var index = 0; index < 5; index++)
        Assert(source.AddToBackpack(ration), "A követőnek szánt tesztköteg nem fért el.");
    Assert(follower.AddToBackpack(ration), "A követő célstackje nem hozható létre.");
    var command = new GiveFollowerStackCommand(PlayerId.New(), 1, source.Id, source.InventoryRevision, 0,
        follower.Id, follower.InventoryRevision);
    Assert(FollowerStackTransferService.TryExecute(source, follower, command, out var result, out var error), error);
    Assert(source.GetInventoryItemQuantity(InventorySlotKind.Backpack, 0) == 3 &&
           follower.GetInventoryItemQuantity(InventorySlotKind.Backpack, 0) == 3 &&
           result.TransferredQuantity == 2 && result.RemainingQuantity == 3,
        "Az ötdarabos köteg kisebb fele nem 3/2 arányban került a követőhöz vagy nem stackelődött.");

    var trinket = new MiscItemDefinition("I-FOLLOWER-NONCONSUMABLE", "Dísztárgy", "Teszt", 1);
    Assert(source.AddToBackpack(trinket) && source.AddToBackpack(trinket),
        "A nem fogyasztható tesztköteg nem fért el.");
    var trinketIndex = Enumerable.Range(0, LiveCharacter.MaximumBackpackItemCount)
        .Single(index => source.GetInventoryItem(InventorySlotKind.Backpack, index)?.Id == trinket.Id);
    var invalid = command with { CommandId = 2, ExpectedInventoryRevision = source.InventoryRevision,
        BackpackIndex = trinketIndex, ExpectedFollowerInventoryRevision = follower.InventoryRevision };
    Assert(!FollowerStackTransferService.TryExecute(source, follower, invalid, out _, out error) &&
           error.Contains("elfogyasztható", StringComparison.OrdinalIgnoreCase),
        "A követőnek átadás elfogadott egy nem fogyasztható tárgyat.");
}

static void ClassResourceGrowthLoadsFromCsv()
{
    var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    Assert(catalog.GetCharacterResourceGrowth(CharacterClassIds.Barbár).AdjustVitality(5) == 10 &&
           catalog.GetCharacterResourceGrowth(CharacterClassIds.Harcos).AdjustVitality(5) == 8 &&
           catalog.GetCharacterResourceGrowth(CharacterClassIds.Tolvaj).AdjustVitality(5) == 5 &&
           catalog.GetCharacterResourceGrowth(CharacterClassIds.Mágus).AdjustVitality(1) == 1,
        "A barbár/harcos/tolvaj/mágus HP-növekedési módosítója hibás.");
    Assert(catalog.GetCharacterResourceGrowth(CharacterClassIds.Mágus).AdjustMana(5) == 6 &&
           catalog.GetCharacterResourceGrowth(CharacterClassIds.Pap).AdjustMana(5) == 5 &&
           catalog.GetCharacterResourceGrowth(CharacterClassIds.Lovag).AdjustMana(5) == 3,
        "A mágus/pap/lovag mannanövekedési módosítója hibás.");
}

static void NpcDefinitionsLoadFromCsv()
{
    var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    Assert(catalog.Npcs.Count == 21 && catalog.NpcEncounters.Count == 29 &&
           Enumerable.Range(1, MazeLevelConfigurations.FinalLevel).All(level =>
               catalog.NpcEncounters.Any(encounter => encounter.MazeLevel == level)),
        "Az NPC-definíciók vagy valamelyik pálya találkozása hiányzik.");
    Assert(catalog.NpcDialogues.Count == 73 && catalog.NpcStoryChoices.Count == 70 &&
           catalog.NpcQuests.Count == 40 &&
           catalog.NpcQuests.Count(quest => quest.Type == NpcQuestType.Collect) == 8 &&
           catalog.NpcQuests.Count(quest => quest.Type == NpcQuestType.Kill) == 18 &&
           catalog.NpcQuests.Count(quest => quest.Type == NpcQuestType.KillWithFollower) == 1 &&
           catalog.NpcQuests.Count(quest => quest.Type == NpcQuestType.Explore) == 5 &&
           catalog.NpcQuests.Count(quest => quest.Type == NpcQuestType.Disarm) == 3 &&
           catalog.NpcQuests.Count(quest => quest.Type == NpcQuestType.OpenChest) == 4 &&
           catalog.NpcQuests.Count(quest => quest.Type == NpcQuestType.Escort) == 1,
        "Az NPC-párbeszédek vagy a küldetéstípusok hibásan töltődtek.");
    Assert(catalog.GetNpc("NPC001") is { Disposition: NpcDisposition.Neutral, Unique: false } &&
           catalog.GetNpcQuests("NPC002").Any(quest =>
               quest is { TargetId: "E003", ExperienceReward: 260 }) &&
           catalog.GetNpcQuests("NPC001").Any(quest => quest is
               { Id: "NPCQ001", RewardItemId: "T018", RewardItemCount: 2, RandomRewardCount: 0 }) &&
           catalog.GetNpc("NPC020") is { Unique: true, Recruitable: true, RaceId: "R003" } &&
           catalog.GetNpc("NPC021") is { Unique: true, RaceId: "R001", StoryId: "RODERIC_OATH" } &&
           catalog.NpcEncounters.Single(encounter => encounter.NpcId == "NPC021").QuestRoomId == "RODERIC_MEETING" &&
           catalog.GetNpcStoryChoices("RODERIC_OATH", "INITIAL") is
               [{ FriendlinessChange: 2 }, { FriendlinessChange: 0 }, { FriendlinessChange: -3 }] &&
           catalog.GetNpcQuests("NPC020").Select(quest => quest.Type).ToHashSet().SetEquals(
               [NpcQuestType.Escort, NpcQuestType.Collect, NpcQuestType.Kill]) &&
           catalog.NpcQuests.All(quest => quest.RandomRewardCount > 0 || quest.RewardItemCount > 0),
        "A semleges nem egyedi NPC vagy a hozzá kapcsolt küldetés hibás.");

    var npc = new WorldNpc(new Position(1, 1), "NPC002", CreateCharacter("Küldetésadó"),
        NpcDisposition.Neutral, false, true, "Próba", questIds: ["NPCQ002"]);
    Assert(npc.ActivateQuest("NPCQ002") && npc.AddQuestProgress("NPCQ002", 3, 4) &&
           npc.Quests.Single() is { State: NpcQuestState.Active, Progress: 3 } &&
           npc.AddQuestProgress("NPCQ002", 1, 4) && npc.CompleteQuest("NPCQ002") &&
           npc.Quests.Single().State == NpcQuestState.Completed,
        "Az NPC-küldetés felvétele, haladása vagy egyszeri lezárása hibás.");

    npc.AdjustFriendliness(20);
    npc.SetStoryState("TEST_STATE");
    npc.BeginFollowing();
    npc.AdvanceConversation();
    var follower = new PartyMemberAvatar(new Position(1, 1), npc.Character, npc);
    follower.MoveTo(new Position(2, 1));
    Assert(npc.Friendliness == 10 && npc.State == WorldNpcState.Following && !npc.CanStartConversation &&
           npc.ConversationStage == 1 && npc.StoryStateId == "TEST_STATE" && follower.IsTemporaryFollower &&
           npc.Position == follower.Position,
        "Az egyedi NPC viszonya, követőállapota vagy párbeszédtiltása hibás.");
    follower.MakePermanent();
    Assert(!follower.IsTemporaryFollower,
        "Az ideiglenes követő nem alakítható végleges partitaggá.");
}

static void AdHocFollowerConversationsAreConfigured()
{
    var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    foreach (var storyId in new[] { "ELIRA_RESCUE", "RODERIC_OATH" })
    {
        for (var index = 1; index <= 5; index++)
        {
            var startState = $"ADHOC_{index}_START";
            var followupState = $"ADHOC_{index}_FOLLOWUP";
            var start = catalog.GetNpcStoryChoices(storyId, startState);
            var followup = catalog.GetNpcStoryChoices(storyId, followupState);
            Assert(start.Count == 2 && start.All(choice => choice.ContinueConversation &&
                       choice.NextStateId == followupState) &&
                   followup.Count == 2 && followup.All(choice => !choice.ContinueConversation),
                $"A(z) {storyId}/{startState} ad-hoc beszélgetésszál szerkezete hibás.");
        }
    }

    var snapshot = new AdHocConversationSnapshot(Guid.NewGuid(), "Roderic", "Ember", "Lovag",
        ["Roderic: Az esküm még köt."], "Bízol bennünk?", ["Igen.", "Még nem."]);
    var window = AdHocConversationWindow.Build(snapshot);
    Assert(window.Any(line => line.Text.Contains("1) Igen.", StringComparison.Ordinal)) &&
           window.Any(line => line.Text.Contains("host választja", StringComparison.OrdinalIgnoreCase)),
        "A vendég ad-hoc párbeszédablaka nem mutatja a két választ vagy a host vezérlését.");
}

static void RodericInsigniaGuardiansAreConfigured()
{
    var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var encounter = MazeLevelConfigurations.Get(5).QuestRoomEnemyEncounters.Single();
    Assert(encounter is { RoomId: "RODERIC_INSIGNIA", EnemyId: "E052", Count: 3,
               GuaranteedItemId: "T026" } &&
           catalog.GetEnemy(MonsterIds.CsontvázLovag) is { Strength: 6, HitPoints: 105, Armor: 5 } &&
           catalog.GetItem(MiscItemIds.FallenKnightInsignia).BasePrice == 1 &&
           SpellcastingRules.IsRestrictedFromTradingAndGeneration(
               catalog.GetItem(MiscItemIds.FallenKnightInsignia)),
        "A jelvényes terem, a Skeleton Knight vagy a questtárgy adatai hibásak.");

    var maze = new Maze(7, 7);
    maze.Carve(new Position(3, 3));
    var enemy = new ConfiguredEnemy(new Position(3, 3), catalog.GetEnemy(MonsterIds.CsontvázLovag));
    enemy.ConfigureGuaranteedLoot([MiscItemIds.FallenKnightInsignia]);
    maze.AddEnemy(enemy);
    maze.ReplaceEnemyWithCorpse(enemy);
    var corpse = maze.Corpses.OfType<MonsterCorpse>().Single();
    Assert(corpse.GuaranteedLootIds.SequenceEqual([MiscItemIds.FallenKnightInsignia]),
        "A példányhoz kötött jelvény nem került át a Skeleton Knight tetemére.");
    var restored = JsonSerializer.Deserialize<CorpseSaveData>(JsonSerializer.Serialize(new CorpseSaveData(
        corpse.Position, corpse.FormerName, null, corpse.EnemyDefinitionId, corpse.IsSearched,
        corpse.GuaranteedLootIds.ToList())));
    Assert(restored?.GuaranteedLootIds?.SequenceEqual([MiscItemIds.FallenKnightInsignia]) == true,
        "A garantált jelvény nem élte túl a mentési JSON-körutat.");
}

static void RodericMalrecQuestLocationIsConfigured()
{
    var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var configuration = QuestLocationConfigurations.Get(QuestLocationConfigurations.RodericMalrec);
    var malrecEncounter = configuration.QuestRoomEnemyEncounters.Single(value =>
        value.EnemyId == MonsterIds.SirMalrec);
    var guards = configuration.QuestRoomEnemyEncounters.Single(value =>
        value.EnemyId == MonsterIds.CsontvázLovag);
    var quest = catalog.NpcQuests.Single(value => value.Id == "NPCQ039");
    var proofQuest = catalog.NpcQuests.Single(value => value.Id == "NPCQ040");
    Assert(configuration.Level == 5 && configuration.BossRoomIds.SequenceEqual(["MALREC_CHAMBER"]) &&
           malrecEncounter is { RoomId: "MALREC_CHAMBER", EnemyId: "E053", Count: 1 } &&
           guards is { RoomId: "MALREC_CHAMBER", EnemyId: "E052", Count: 4,
               GuaranteedItemId: null } &&
           catalog.GetEnemy(MonsterIds.SirMalrec) is
               { Rank: EnemyRank.MiniBoss, IsBoss: false, HitPoints: 420, Armor: 9 } &&
           quest is { Type: NpcQuestType.Kill, TargetId: "E053", RequiredStoryStateId: "TRUSTED" },
        "Sir Malrec rangja, küldetése vagy az 5-ös nehézségű küldetéshelyszíne hibás.");
    Assert(quest is { RewardItemId: "T027", RewardItemCount: 1 } &&
           proofQuest is { Type: NpcQuestType.Kill, TargetId: "E004", RequiredCount: 4 } &&
           catalog.GetNpcStoryChoices("RODERIC_OATH", "PROOF_OFFER").Single() is
               { Action: NpcStoryAction.ActivateQuest, ActionParameter: "NPCQ040",
                   NextStateId: "PROOF_ACTIVE" } &&
           catalog.GetItemDefinition(MiscItemIds.SilverOathSeal).Id == MiscItemIds.SilverOathSeal &&
           catalog.GetItem(MiscItemIds.SilverOathSeal).BasePrice == 1 &&
           SpellcastingRules.IsRestrictedFromTradingAndGeneration(
               catalog.GetItem(MiscItemIds.SilverOathSeal)),
        "Az Ezüst Eskü nagypecsétje nem történeti jutalomtárgyként szerepel.");

    var initialChoices = catalog.GetNpcStoryChoices("RODERIC_OATH", "INITIAL");
    var trustedChoices = catalog.GetNpcStoryChoices("RODERIC_OATH", "TRUSTED");
    var finalChoices = catalog.GetNpcStoryChoices("RODERIC_OATH", "MALREC_DEFEATED");
    var lowTrustVerdict = catalog.GetNpcStoryChoices("RODERIC_OATH", "JOIN_VERDICT", 7);
    var highTrustVerdict = catalog.GetNpcStoryChoices("RODERIC_OATH", "JOIN_VERDICT", 8);
    Assert(initialChoices.Count == 3 && initialChoices.All(value => value.ContinueConversation) &&
           trustedChoices.Single() is { NextStateId: "MALREC_STORY", ContinueConversation: true } &&
           catalog.GetNpcStoryChoices("RODERIC_OATH", "MALREC_STORY").All(value =>
               value.NextStateId == "CACHE_DECISION" && value.ContinueConversation) &&
           catalog.GetNpcStoryChoices("RODERIC_OATH", "CACHE_DECISION").Count == 3 &&
           catalog.GetNpcStoryChoices("RODERIC_OATH", "CACHE_DECISION").Count(value =>
               value.Action == NpcStoryAction.GrantEmergencySupplies) == 2 &&
           catalog.GetNpcStoryChoices("RODERIC_OATH", "CACHE_BLOCKED").Single().Action ==
               NpcStoryAction.GrantEmergencySupplies &&
           finalChoices.Count == 3 && finalChoices.All(value => value.ContinueConversation) &&
           catalog.GetNpcStoryChoices("RODERIC_OATH", "SECOND_CHANCE").Count == 2 &&
           catalog.GetNpcStoryChoices("RODERIC_OATH", "SECOND_CHANCE").Any(value =>
               value.NextStateId == "OATH_BROKEN" && value.FriendlinessChange == -3) &&
           lowTrustVerdict.Single() is { NextStateId: "JOIN_REFUSED",
               MaximumFriendliness: 7 } &&
           highTrustVerdict.Single() is { NextStateId: "JOIN_ACCEPTED",
               MinimumFriendliness: 8, Action: NpcStoryAction.RequestPermanentJoin },
        "Roderic többforduló párbeszédgráfja vagy második csatlakozási esélye hibás.");

    var maze = new MazeGenerator(configuration.CreateGenerationSettings(new Random(17)), [], [])
        .Create(55, 31);
    Assert(maze.Rooms.SingleOrDefault(room => room.ContentId == "MALREC_CHAMBER") is
               { Purpose: RoomPurpose.Boss },
        "Sir Malrec szobája nem elkülönített boss roomként jött létre.");

    var suspended = new GameSaveData { MazeLevel = 5, LocationId = "CAMPAIGN_05" };
    var active = new GameSaveData
    {
        MazeLevel = 5,
        LocationKind = AdventureLocationKind.Quest,
        LocationId = QuestLocationConfigurations.RodericMalrec,
        DifficultyLevel = 5,
        SuspendedCampaign = suspended
    };
    var restored = JsonSerializer.Deserialize<GameSaveData>(JsonSerializer.Serialize(active));
    Assert(restored is { LocationKind: AdventureLocationKind.Quest, DifficultyLevel: 5,
               SuspendedCampaign.LocationId: "CAMPAIGN_05" },
        "A küldetéshelyszín vagy a felfüggesztett kampánypálya nem menthető.");
}

static void QuestRoomsReserveTheirContent()
{
    var settings = new MazeGenerationSettings
    {
        RoomCount = 8,
        MinimumRoomSize = 4,
        MaximumRoomSize = 6,
        TreasureChestCount = 20,
        QuestRoomIds = ["RODERIC_MEETING", "RODERIC_INSIGNIA"]
    };
    var maze = new MazeGenerator(settings, [], []).Create(55, 31);
    var questRooms = maze.Rooms.Where(room => room.Purpose == RoomPurpose.Quest).ToArray();
    Assert(questRooms.Length == 2 && questRooms.Select(room => room.ContentId).ToHashSet().SetEquals(
               ["RODERIC_MEETING", "RODERIC_INSIGNIA"]),
        "A két Roderic-quest room nem jött létre stabil tartalomazonosítóval.");
    Assert(questRooms.All(room => maze.TreasureChests.All(chest => !room.Contains(chest.Position)) &&
                                  maze.Enemies.All(enemy => !room.Contains(enemy.Position))),
        "Véletlen kincs vagy ellenfél került egy quest roomba.");
    var restored = JsonSerializer.Deserialize<Room>(JsonSerializer.Serialize(questRooms[0]));
    Assert(restored is { Purpose: RoomPurpose.Quest, ContentId: not null },
        "A quest room szerepe vagy azonosítója nem menthető.");
}

static void RodericUsesDefinedCharacterBuild()
{
    var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var first = new UniqueNpcCharacterFactory(catalog).Create(catalog.GetNpc("NPC021"));
    var repeated = new UniqueNpcCharacterFactory(catalog).Create(catalog.GetNpc("NPC021"));
    var scaled = new UniqueNpcCharacterFactory(catalog).Create(catalog.GetNpc("NPC021"), 9);
    var veteran = new UniqueNpcCharacterFactory(catalog).Create(catalog.GetNpc("NPC021"), 15);
    Assert(first.Name == "Sir Roderic" && first.Level == 7 && scaled.Level == 9 &&
           first.Color == ConsoleColor.DarkCyan && first.NpcBehavior == NpcBehavior.Aggressive &&
           first.Abilities == new PrimaryAbilities(12, 5, 9, 6) &&
           scaled.Abilities.Strength == 13 && veteran.HasPerk(PerkIds.KnightHolyOath) &&
           veteran.HasClassFeatureUpgrade(ClassFeatureUpgrades.KnightRetaliation) &&
           first.Perks.Select(perk => perk.Id).SequenceEqual(["PERK-C003-1B", PerkIds.RodericOathblade]) &&
           first.WeaponProficiencyRankFor(WeaponFamilies.Sword) == WeaponProficiencyRank.Master &&
           first.WeaponSlots[0] is { Id: CharacterBoundItemRules.RodericGreatswordId,
               Rarity: ItemRarity.Magic, MagicPower: 1, BaseWeaponId: "W009" } &&
           first.WeaponSlots[1] is null &&
           first.Armor is { Id: CharacterBoundItemRules.RodericPlateArmorId,
               Rarity: ItemRarity.Magic, MagicPower: 1, BaseArmorId: "A006" } &&
           first.Color != ConsoleColor.White,
        "Roderic karakterlapja nem a CSV-ben rögzített buildet használja.");
    Assert(first.Abilities == repeated.Abilities && first.MaximumVitality == repeated.MaximumVitality &&
           first.WeaponSlots.Select(item => item?.Id).SequenceEqual(repeated.WeaponSlots.Select(item => item?.Id)),
        "Roderic újbóli létrehozása nem determinisztikus.");

    var outsider = CreateCharacter("Ereklyepróba");
    Assert(!outsider.SetInventoryItem(InventorySlotKind.Backpack, 0, first.WeaponSlots[0]) &&
           !outsider.SetInventoryItem(InventorySlotKind.Armor, 0, first.Armor) &&
           SpellcastingRules.IsRestrictedFromTradingAndGeneration(first.WeaponSlots[0]!) &&
           SpellcastingRules.IsRestrictedFromTradingAndGeneration(first.Armor!),
        "Roderic családi ereklyéit más karakter használhatja vagy kereskedelmi lootként kaphatja.");

    var recipient = CreateCharacter("Ellátmány");
    var bundle = new[]
    {
        new InventoryBundleEntry(catalog.GetItem("T012"), 4),
        new InventoryBundleEntry(catalog.GetItem("T004"), 2),
        new InventoryBundleEntry(catalog.GetItem("T006"), 2),
        new InventoryBundleEntry(catalog.GetItem("T002"), 4)
    };
    Assert(InventoryBundleGrantService.TryGrant([recipient], bundle, out var lacking) && lacking.Count == 0 &&
           CountBackpack(recipient, "T012") == 4 && CountBackpack(recipient, "T004") == 2 &&
           CountBackpack(recipient, "T006") == 2 && CountBackpack(recipient, "T002") == 4,
        "Az Ezüst Eskü vésztartaléka nem a kért mennyiséget osztotta ki.");

    var fullRecipient = CreateCharacter("Telezsák");
    for (var index = 0; index < LiveCharacter.MaximumBackpackItemCount; index++)
        Assert(fullRecipient.AddToBackpack(new MiscItemDefinition($"FULL-{index}", $"Tárgy {index}", "Teszt", 1)),
            "A tele hátizsákos előfeltétel nem jött létre.");
    Assert(!InventoryBundleGrantService.TryGrant([recipient, fullRecipient], bundle, out lacking) &&
           lacking.SequenceEqual([fullRecipient.Name]) && CountBackpack(recipient, "T012") == 4,
        "A vésztartalék részlegesen kiosztódott annak ellenére hogy egy partitag hátizsákja tele volt.");

    static int CountBackpack(LiveCharacter character, string itemId) =>
        Enumerable.Range(0, LiveCharacter.MaximumBackpackItemCount)
            .Where(index => string.Equals(character.Backpack[index]?.Id, itemId, StringComparison.OrdinalIgnoreCase))
            .Sum(index => character.GetInventoryItemQuantity(InventorySlotKind.Backpack, index));
}

static void PartyRemarksLoadFromCsv()
{
    var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var combinations = catalog.Races.SelectMany(race => catalog.CharacterClasses.Select(characterClass =>
        (RaceId: race.Id, ClassId: characterClass.Id))).ToArray();

    Assert(catalog.PartySituations.Count == 9 && catalog.PartyRemarks.Count == 729 &&
           combinations.All(pair => catalog.PartyRemarks.Count(remark =>
               remark.SituationId == PartySituationIds.Thirsty && remark.RaceId == pair.RaceId &&
               remark.CharacterClassId == pair.ClassId) ==
               (pair.RaceId == "R002" && pair.ClassId == CharacterClassIds.Harcos ? 6 : 3)),
        "A kilenc szituáció vagy a faj–osztály páronkénti három szomjúsági megjegyzés hiányzik.");
    var dwarfFighterRemarks = catalog.PartyRemarks.Where(remark => remark.RaceId == "R002" &&
        remark.CharacterClassId == CharacterClassIds.Harcos).ToArray();
    Assert(catalog.PartySituations.All(situation => dwarfFighterRemarks.Count(remark =>
               remark.SituationId == situation.Id) == 6) &&
           new[] { "üllő", "tárna", "szakáll", "pöröly", "lakoma", "győzel" }.All(topic =>
               dwarfFighterRemarks.Any(remark => remark.Text.Contains(topic, StringComparison.OrdinalIgnoreCase))),
        "A törpe harcosok megduplázott vagy tematikus megjegyzései hiányoznak.");
    Assert(catalog.PartyRemarks.Count(remark => remark.CharacterName == "Sir Roderic" &&
               remark.TemporaryFollower) == 27 &&
           catalog.PartyRemarks.Count(remark => remark.CharacterName == "Sir Roderic" &&
               !remark.TemporaryFollower) == 27 &&
           catalog.PartyRemarks.Count(remark => remark.CharacterName == "Sir Roderic" &&
               remark.Text.Contains("Ezüst Eskü", StringComparison.Ordinal)) >= 18,
        "Roderic követői vagy végleges partitagként használt egyedi megjegyzései hiányoznak.");
    Assert(catalog.PartyRemarks.Single(remark => remark.Id == "PM001").Text.Contains(
               "Maradjatok mögöttem, felmérem", StringComparison.Ordinal) &&
           catalog.PartyRemarks.Single(remark => remark.Id == "PM648").Text.Contains(
               "a víz még nagyobb", StringComparison.Ordinal),
        "Az idézőjeles, vesszőt tartalmazó megjegyzésszöveg csonkolódott.");
}

static void PartyRemarkProbabilitiesFollowRules()
{
    Assert(PartyCommentarySelector.ShouldComment(0) && PartyCommentarySelector.ShouldComment(39) &&
           !PartyCommentarySelector.ShouldComment(40) && !PartyCommentarySelector.ShouldComment(99),
        "A szituációs kommentár esélye nem pontosan 40 százalék.");
    Assert(PartyCommentarySelector.SpeakerCount(4, 0) == 1 &&
           PartyCommentarySelector.SpeakerCount(4, 49) == 1 &&
           PartyCommentarySelector.SpeakerCount(4, 59) == 1 &&
           PartyCommentarySelector.SpeakerCount(4, 60) == 2 &&
           PartyCommentarySelector.SpeakerCount(4, 79) == 2 &&
           PartyCommentarySelector.SpeakerCount(4, 80) == 3 &&
           PartyCommentarySelector.SpeakerCount(2, 99) == 2,
        "A beszélők 60/20/20 százalékos eloszlása vagy partilétszám-korlátja hibás.");
    var dwarf = new LiveCharacter("Törpe", new RaceDefinition("R002", "Törpe", PrimaryAbilities.Zero),
        new CharacterClassDefinition(CharacterClassIds.Harcos, "Harcos", PrimaryAbilities.Zero, false, 1.0),
        new PrimaryAbilities(5, 5, 5, 5), 20, 0, 1, 0);
    Assert(PartyCommentarySelector.SpeakerWeight(PartySituationIds.Resting, dwarf) == 120 &&
           PartyCommentarySelector.SpeakerWeight(PartySituationIds.EnemySpotted, dwarf) == 140 &&
           PartyCommentarySelector.SpeakerWeight(PartySituationIds.BattleStarted, dwarf) == 140 &&
           PartyCommentarySelector.SpeakerWeight(PartySituationIds.BattleWon, dwarf) == 140 &&
           PartyCommentarySelector.SpeakerWeight(PartySituationIds.PartyMemberDied, dwarf) == 140 &&
           PartyCommentarySelector.SpeakerWeight(PartySituationIds.BattleStarted,
               CreateCharacter("Ember")) == 100,
        "A törpe beszélők 20/40 százalékos súlytöbblete hibás.");
    var speaker = CreateCharacter("Kommentelő");
    Assert(PartyCommentarySelector.Format(speaker, "Próba.") == "[Kommentelő] Próba." &&
           PartyCommentarySelector.Format(speaker, "Éhes vagyok.", "17") ==
           "[Kommentelő](17) Éhes vagyok.",
        "A parti megjegyzésének név- vagy állapotszint-formátuma hibás.");
}

static void WorldNpcGenerationExcludesWhiteColor()
{
    Assert(CharacterColors.Selectable.Contains(ConsoleColor.White) &&
           !CharacterColors.WorldNpcSelectable.Contains(ConsoleColor.White) &&
           CharacterColors.WorldNpcSelectable.Count == CharacterColors.Selectable.Count - 1,
        "A world-NPC színpaletta nem pontosan a fehér karakterszínt zárja ki.");

    var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var generator = new RandomCharacterGenerator(catalog, new Random(7281));
    var characterClass = catalog.GetCharacterClass(CharacterClassIds.Harcos);
    var recruits = Enumerable.Range(0, 30)
        .Select(index => generator.CreateRecruit(characterClass, 5, [$"WorldNpc{index}"]))
        .ToArray();
    Assert(recruits.All(recruit => recruit.Color != ConsoleColor.White),
        "A world-NPC generátor fehér karakterszínt választott.");
}

static void TemporaryFollowerKeepsWorldNpcMapColors()
{
    var character = CreateCharacter("Elira");
    var npc = new WorldNpc(new Position(1, 1), "NPC020", character,
        NpcDisposition.Neutral, true, true, "Próba");
    var follower = new PartyMemberAvatar(npc.Position, character, npc);

    Assert(follower.ForegroundColor == ConsoleColor.White && follower.BackgroundColor == character.Color,
        "Az ideiglenes követő nem a world-NPC inverz térképszíneit kapta.");
    follower.MakePermanent();
    Assert(follower.ForegroundColor == character.Color && follower.BackgroundColor == ConsoleColor.Black,
        "A végleges partitaggá vált követő nem kapta vissza a normál térképszíneit.");
}

static void NpcDialogueWrapsInsideRecruitmentWindow()
{
    var dialogue = "„A csontok emlékeznek azokra akik felébresztették őket. " +
                   "Segítsetek újra elcsendesíteni a sírokat.”";
    var lines = MessageTextLayout.Wrap(dialogue, ConsoleRenderer.WorldNpcRecruitmentTextWidth).ToArray();

    Assert(lines.Length > 1 && lines.All(line =>
               line.Length <= ConsoleRenderer.WorldNpcRecruitmentTextWidth) &&
           string.Join(' ', lines).Replace("  ", " ", StringComparison.Ordinal) == dialogue,
        "A hosszú NPC-párbeszéd kilóg a találkozási ablakból vagy szöveg veszett el.");
}

static void QuestJournalBuildsSharedHistory()
{
    var entries = new QuestJournalEntrySnapshot[]
    {
        new("Q-A", "Folyamatban", "Tedd meg.", "Elira", QuestJournalStatus.Active, 2, 4, 240),
        new("Q-B", "Befejezve", "Megtetted.", "Elira", QuestJournalStatus.Completed, 1, 1, 420),
        new("Q-C", "Feladva", "Nem folytatod.", "Elira", QuestJournalStatus.Abandoned, 1, 3, 180)
    };
    var lines = QuestJournalWindow.Build(entries);
    var restoration = QuestJournalWindow.CalculateRestorationRegion(entries, 0, 200, 50);
    var readOnlyRestoration = QuestJournalWindow.CalculateRestorationRegion(entries, 0, 200, 50,
        allowAbandon: false);
    Assert(WindowFrameConfiguration.For(FramedWindow.QuestOffer) == WindowFrameStyle.Stone &&
           WindowFrameConfiguration.For(FramedWindow.QuestJournal) == WindowFrameStyle.Scroll2 &&
           restoration.Width == QuestJournalWindow.Width && restoration.Height < 50 &&
           restoration.Height == readOnlyRestoration.Height + 1 &&
           restoration.Left > 0 && restoration.Left + restoration.Width < 200 &&
           lines.Any(line => line.Text.Contains("Folyamatban — 2/4  Elira (240 XP)", StringComparison.Ordinal)) &&
           lines.Any(line => line.Text.Contains("Befejezve — Elira (+420 XP)", StringComparison.Ordinal)) &&
           lines.Any(line => line.Text.Contains("× Feladva — Elira", StringComparison.Ordinal)),
        "A küldetésnapló kerete vagy aktív/teljesített/feladott tartalma hibás.");
}

static void AbandonedNpcQuestRemainsResolved()
{
    var character = CreateCharacter("Elira-próba");
    var npc = new WorldNpc(new Position(1, 1), "NPC020", character, NpcDisposition.Neutral,
        true, true, "Próba", questIds: ["Q-ELIRA"], storyId: "ELIRA_RESCUE");
    Assert(npc.ActivateQuest("Q-ELIRA") && npc.AbandonQuest("Q-ELIRA") &&
           npc.Quests.Single().State == NpcQuestState.Abandoned && npc.CanJoin,
        "A feladott küldetés nem maradt lezárt NPC-állapotban.");
    Assert(!npc.AddQuestProgress("Q-ELIRA", 1, 3) && !npc.CompleteQuest("Q-ELIRA") &&
           !npc.ActivateQuest("Q-ELIRA"),
        "A feladott küldetés újra aktiválható vagy tovább teljesíthető volt.");

    var saved = new QuestJournalSaveData("Q-ELIRA", QuestJournalStatus.Abandoned, 1, 180);
    var restored = JsonSerializer.Deserialize<QuestJournalSaveData>(JsonSerializer.Serialize(saved));
    Assert(restored?.Status == QuestJournalStatus.Abandoned && restored.Progress == 1,
        "A feladott naplóállapot nem élte túl a mentési körutat.");
}

static void SpellUiModelsAreShared()
{
    var spell = new KnownSpellSnapshot("spell-test", "Próbaláng", 2, 7, SpellTargetType.Enemy,
        "Egy próbaként használt varázslat.", true, 0);
    var infoLines = SpellInfoPanel.Build("Rubin", CharacterClassIds.Mágus, 6,
        new SpellInfoSnapshot("Kristálygömb", 3, [spell]), 0);
    Assert(infoLines.Any(line => line.Row == 5 && line.Text.Contains("[M][F1]", StringComparison.Ordinal)) &&
           infoLines.Any(line => line.Row == 43 && line.Text == "Következő feloldás: L10") &&
           infoLines.Single(line => line.Row == 5).Background == ConsoleColor.DarkCyan,
        "A közös varázslatinformációs panel elvesztette a gyorshelyet, feloldást vagy kijelölést.");

    var selectorLines = SpellSelectorWindow.Build("Rubin", 5, 12, true,
        [new SpellSelectorOption("Próbaláng", 2, 7, SpellTargetType.Enemy, "F1", false)], 0, 0);
    Assert(selectorLines[0].Text == "⚔️ HARCI VARÁZSLÁS" &&
           selectorLines.Any(line => line.Text.Contains("[F1] L2", StringComparison.Ordinal) &&
                                     line.Color == ConsoleColor.DarkRed),
        "A közös varázslatválasztó elvesztette a harci címet, gyorshelyet vagy mannafigyelmeztetést.");
}

static void RestSummaryUiIsShared()
{
    var characterId = CharacterId.New();
    var rest = new PartyRestSnapshot(Guid.NewGuid(), false,
        [new CharacterRestSnapshot(characterId, "Rubin", ConsoleColor.Cyan,
            7, 12, 28, 35, 20, 20, true, ["🤒 betegség", "🩸 vérzés"])], []);
    var lines = RestSummaryWindow.Build(rest, "❖  Nyomj Entert a folytatáshoz...  ❖");
    Assert(WindowFrameConfiguration.For(FramedWindow.Inn) == WindowFrameStyle.Ruby &&
           lines.Any(line => line.Text.Contains("❤️ Rubin", StringComparison.Ordinal) &&
                             line.Text.Contains("+7", StringComparison.Ordinal) &&
                             line.Text.Contains("🔷+12", StringComparison.Ordinal)) &&
           lines.Any(line => line.Text.Contains("🤒 betegség", StringComparison.Ordinal) &&
                             line.Text.Contains("🩸 vérzés", StringComparison.Ordinal)),
        "A közös Ruby pihenési összegzőből hiányzik a HP, manna vagy megszűnt állapot.");
}

static void GuestItemInspectionKeepsDamageValue()
{
    var dataPath = Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName);
    var data = CsvGameDataLoader.Load(dataPath);
    var weapon = data.Weapons.First(candidate => candidate.Damage is not null);
    var inspection = ItemInspectionFormatter.Format(weapon, data);
    var lines = MessageTextLayout.Wrap(inspection.Text, 48).ToArray();
    Assert(lines.All(line => line.Length <= 48) &&
           string.Join(' ', lines).Contains($"sebzés: {weapon.Damage}", StringComparison.Ordinal),
        "A vendég tényleges panelszélességű tördelése levágta a fegyver sebzésértékét.");
}

static void BossAndBattlePromptsAreShared()
{
    var boss = new BossPresentationSnapshot("Káoszúr", "🐉 Fekete sárkány", 5, "🔑 Aranykulcs");
    var lines = NarrativeWindow.Build("BOSS KÖZELEG", "X. fejezet", ["Nincs menekvés."],
        "❖  Tovább  ❖", kind: NarrativeKind.BossIntroduction, boss: boss);
    Assert(lines[0] == ("⚔️👑  BOSS KÖZELEG  👑⚔️", ConsoleColor.Red) &&
           lines.Any(line => line.Text.Contains("Káoszúr", StringComparison.Ordinal)) &&
           lines.Any(line => line.Text.Contains("Erősség: 5/5", StringComparison.Ordinal) &&
                             line.Text.Contains("Aranykulcs", StringComparison.Ordinal)),
        "A közös boss-ablak elvesztette a boss azonosságát, erősségét vagy jutalmát.");

    var tactics = new[]
    {
        new BattleTacticOptionSnapshot(BattleActionKind.FighterPrecise, "🎯 Pontos", "sebzés ×0,75", 65)
    };
    var thiefTactics = new[]
    {
        new BattleTacticOptionSnapshot(BattleActionKind.ThiefAmbush, "🗡️ Orvtámadás", "első találat ×2", 60),
        new BattleTacticOptionSnapshot(BattleActionKind.ThiefObserve, "👁️ Megfigyelés", "+2 találat", 70),
        new BattleTacticOptionSnapshot(BattleActionKind.ThiefPoison, "☠️ Mérgezett penge", "+1–4 sebzés", 60)
    };
    Assert(BattlePromptText.Tactic(CharacterClassIds.Harcos, tactics).Contains("65%", StringComparison.Ordinal) &&
           BattlePromptText.Tactic(CharacterClassIds.Tolvaj, thiefTactics).Contains("Megfigyelés 70%", StringComparison.Ordinal) &&
           BattleCommandPanel.Format(thiefTactics.Select(option => option.Action), thiefTactics)
               .Contains("3: ☠️ Mérgezett penge", StringComparison.Ordinal) &&
           BattleCommandPanel.DisplayWidth("🗡️ Orvtámadás | 👁️ Megfigyelés") == 30 &&
           BattlePromptText.EnemyTurn == "Space — ellenfél köre" &&
           BattlePromptText.PlayerAction(true, true).Contains("halottűzés", StringComparison.Ordinal),
        "A közös harci prompt elvesztette a taktikai esélyt vagy valamelyik vezérlést.");
}

static void AbilityMagicItemsAreUniversalAndCapped()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var expected = new Dictionary<string, (MagicItemEffect Effect, int Value, int Price)>
    {
        ["M017"] = (MagicItemEffect.Strength, 1, 1200),
        ["M018"] = (MagicItemEffect.Strength, 2, 3000),
        ["M019"] = (MagicItemEffect.Dexterity, 1, 1200),
        ["M020"] = (MagicItemEffect.Dexterity, 2, 3000),
        ["M021"] = (MagicItemEffect.Health, 1, 1200),
        ["M022"] = (MagicItemEffect.Health, 2, 3000),
        ["M023"] = (MagicItemEffect.Intelligence, 1, 1200),
        ["M024"] = (MagicItemEffect.Intelligence, 2, 3000)
    };
    foreach (var (id, definition) in expected)
    {
        var item = data.GetMagicItem(id);
        Assert(item.Effect == definition.Effect && item.EffectValue == definition.Value &&
               item.BasePrice == definition.Price &&
               data.CharacterClasses.All(characterClass => item.CanBeEquippedBy(characterClass.Id)),
            $"A(z) {id} képességtárgy adatai vagy kasztengedélyei hibásak.");
    }

    var race = data.GetRace("R001");
    var characterClass = data.CharacterClasses.First();
    var character = new LiveCharacter("Ékszerteszt", race, characterClass,
        new PrimaryAbilities(12, 12, 12, 12), 100, 100, 1, 1);
    Assert(character.AddMagicItem(data.GetMagicItem("M018")) &&
           character.AddMagicItem(data.GetMagicItem("M019")) &&
           character.AddMagicItem(data.GetMagicItem("M022")),
        "A képességtárgyak nem voltak felszerelhetők.");
    Assert(character.EffectiveAbilities == new PrimaryAbilities(13, 13, 13, 12) &&
           character.Abilities == new PrimaryAbilities(12, 12, 12, 12),
        "A felszerelt képességbónusz átlépte a 13-at vagy módosította az alapértéket.");
    var snapshot = CharacterSheetSnapshotProjector.Create(character, data.ExperienceByLevel);
    Assert(snapshot.Abilities == character.EffectiveAbilities,
        "A karakterlap és a coop snapshot nem az effektív képességeket mutatja.");
    Assert(character.SetInventoryItem(InventorySlotKind.MagicItem, 0, null) &&
           character.EffectiveAbilities.Strength == 12 && character.Abilities.Strength == 12,
        "A varázstárgy levétele után nem szűnt meg a képességbónusz.");
}

static void UnknownCsvSectionIsRejectedWithLineNumber()
{
    var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var invalid = source.Replace("#Képességek", "#Elgépelt képességek", StringComparison.Ordinal);
    AssertCsvLoadFails(invalid, "Ismeretlen fejezetcím", "sorában");
}

static void MissingRequiredCsvFieldIsRejectedWithLineNumber()
{
    var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var invalid = source.Replace("R001,Ember,Adaptable", "R001,Ember", StringComparison.Ordinal);
    AssertCsvLoadFails(invalid, "Tulajdonság", "sorában");
}

static void AssertCsvLoadFails(string content, params string[] expectedMessageParts)
{
    var path = Path.Combine(Path.GetTempPath(), $"kaoszrubin-invalid-{Guid.NewGuid():N}.csv");
    try
    {
        File.WriteAllText(path, content, new UTF8Encoding(false));
        try
        {
            CsvGameDataLoader.Load(path);
            throw new InvalidOperationException("A hibás CSV betöltése nem dobott kivételt.");
        }
        catch (InvalidDataException exception)
        {
            Assert(expectedMessageParts.All(part => exception.Message.Contains(part,
                    StringComparison.OrdinalIgnoreCase)),
                $"A CSV-hibaüzenet nem elég részletes: {exception.Message}");
        }
    }
    finally
    {
        if (File.Exists(path)) File.Delete(path);
    }
}

static void AdaptableRaceGainsChosenAbility()
{
    var race = new RaceDefinition("R001", "Ember", PrimaryAbilities.Zero, RaceTraits.Adaptable);
    var characterClass = new CharacterClassDefinition("C001", "Harcos", PrimaryAbilities.Zero, false, 1.0);
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var character = LiveCharacterFactory.Create("Ember", race, characterClass,
        new PrimaryAbilities(5, 5, 5, 5), 1, 1, data, ConsoleColor.Cyan,
        new PrimaryAbilities(0, 0, 0, 1));
    Assert(character.Abilities == new PrimaryAbilities(5, 5, 5, 6),
        "Az Alkalmazkodó tulajdonság nem a kiválasztott képességre adta a +1-et.");
    Assert(PerkProgressionRules.TriggerLevel(race, 1) == 4 &&
           PerkProgressionRules.TriggerLevel(race, 2) == 15,
        "Az Alkalmazkodó ember tehetségszintjei hibásak.");
}

static void CharacterVisionRangeUsesClassRaceAndEffects()
{
    var abilities = new PrimaryAbilities(5, 5, 5, 5);
    var human = new RaceDefinition("R-HUMAN", "Ember", PrimaryAbilities.Zero);
    var elf = new RaceDefinition("R-ELF", "Elf", PrimaryAbilities.Zero, RaceTraits.KeenSenses);
    var fighterClass = new CharacterClassDefinition(CharacterClassIds.Harcos, "Harcos", PrimaryAbilities.Zero,
        false, 1.0);
    var thiefClass = new CharacterClassDefinition(CharacterClassIds.Tolvaj, "Tolvaj", PrimaryAbilities.Zero,
        false, 1.0);
    var fighter = new LiveCharacter("Harcos", human, fighterClass, abilities, 20, 0, 1, 0);
    var thief = new LiveCharacter("Tolvaj", human, thiefClass, abilities, 20, 0, 1, 0);
    var elfThief = new LiveCharacter("Elf tolvaj", elf, thiefClass, abilities, 20, 0, 1, 0);
    Assert(CharacterClassRules.VisionRange(fighter) == 5 &&
           CharacterClassRules.VisionRange(thief) == 7 &&
           CharacterClassRules.VisionRange(elfThief) == 8,
        "A karakter 5/7/8-as alap-, tolvaj- vagy elf látótávja hibás.");

    var darkLevelLine = CharacterSheetPanel.Build(fighter, new Dictionary<int, int> { [2] = 100 },
        9, 0, 12).Single(line => line.Row == 4);
    Assert(CharacterClassRules.VisionRange(fighter, -2) == 3 && darkLevelLine.ColoredSuffix == "3" &&
           darkLevelLine.ColoredSuffixColor == ConsoleColor.Red,
        "Az extra sötét pálya nem csökkenti vagy nem pirosítja a látótávot.");

    fighter.ApplySpellEffect(new ActiveSpellEffect("LIGHT", ActiveSpellEffectType.VisionBonus, 2, 12, Beneficial: true));
    Assert(CharacterClassRules.NaturalVisionRange(fighter) == 5 &&
           CharacterClassRules.VisionRange(fighter) == 7,
        "A pozitív látótávhatás nem különül el a természetes látótávtól.");
    var increasedLine = CharacterSheetPanel.Build(fighter, new Dictionary<int, int> { [2] = 100 },
        1, 0, 12).Single(line => line.Row == 4);
    Assert(increasedLine.ColoredSuffix == "7" && increasedLine.ColoredSuffixColor == ConsoleColor.Green,
        "A növelt látótáv száma nem zöld a karakterlapon.");

    fighter.ApplySpellEffect(new ActiveSpellEffect("DARKNESS", ActiveSpellEffectType.VisionBonus, -4, 12));
    var decreasedLine = CharacterSheetPanel.Build(fighter, new Dictionary<int, int> { [2] = 100 },
        1, 0, 12).Single(line => line.Row == 4);
    Assert(CharacterClassRules.VisionRange(fighter) == 3 && decreasedLine.ColoredSuffix == "3" &&
           decreasedLine.ColoredSuffixColor == ConsoleColor.Red,
        "A csökkentett látótáv értéke vagy piros kijelzése hibás.");
}

static void EnemyVisionRangesLoadFromCsv()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    Assert(data.GetEnemy("E001").VisionRange == 3 && data.GetEnemy("E001").Stealth == 1 &&
           data.GetEnemy("E026").Noise == 4 && data.GetEnemy("E045").Stealth == 3 &&
           data.GetEnemy("E003").VisionRange == 4 &&
           data.GetEnemy("E019").VisionRange == 7 && data.GetEnemy("E050").VisionRange == 8,
        "A patkány, goblin, vámpír vagy káoszsárkány CSV-látótávja hibás.");
}

static void FogRevealUsesVariableRangeAndLineOfSight()
{
    var maze = new Maze(22, 13);
    for (var x = 2; x <= 20; x++) maze.Carve(new Position(x, 2));
    for (var y = 2; y <= 11; y++) maze.Carve(new Position(2, y));
    var origin = maze.Entrance;
    var normalFog = new FogOfWar(maze.Width, maze.Height, 5);
    normalFog.RevealFrom(maze, origin, 5);
    var horizontallyFar = new Position(13, 2);
    var verticallyFar = new Position(2, 8);
    Assert(!normalFog.IsRevealed(horizontallyFar) && !normalFog.IsRevealed(verticallyFar),
        "Az ötrácsos látótáv túl messzire fedett fel.");

    var scoutFog = new FogOfWar(maze.Width, maze.Height, 5);
    scoutFog.RevealFrom(maze, origin, 8);
    Assert(scoutFog.IsRevealed(horizontallyFar) && scoutFog.IsRevealed(verticallyFar) &&
           FogOfWar.IsWithinVisionRange(origin, new Position(18, 2), 8) &&
           !FogOfWar.IsWithinVisionRange(origin, new Position(19, 2), 8),
        "A nyolcas látótáv nem alkalmazza a vízszintes 2:1 képarány-korrekciót.");

    maze.PlaceDoor(new Position(4, 2), DoorState.Closed);
    var blockedFog = new FogOfWar(maze.Width, maze.Height, 5);
    blockedFog.RevealFrom(maze, origin, 8);
    Assert(!blockedFog.IsRevealed(new Position(5, 2)), "A zárt ajtó mögé átlátott a felfedés.");
}

static void MonsterTraitsAndAbilitiesAreDataDriven()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var medusa = data.GetEnemy(MonsterIds.Medúza);
    var troll = data.GetEnemy("E013");
    var dragon = data.GetEnemy("E021");
    var gaze = data.GetMonsterAbility("MA011");
    Assert(medusa.AbilityIds.Contains("MA011") &&
           gaze.Trigger == MonsterAbilityTrigger.Active && gaze.Cooldown == 3 &&
           gaze.Range == 3 && gaze.StatusId == "STATUS006" &&
           troll.AbilityIds.Contains("MA008") &&
           dragon.HasTrait(EnemyTraits.Flying) &&
           data.GetEnemy("E004").HasTrait(EnemyTraits.Undead) &&
           !data.GetEnemy("E004").AbilityIds.Contains(MonsterAbilityIds.Undead) &&
           data.GetEnemy("E018").AbilityIds.Contains("MA012") &&
           data.GetEnemy("E022").AbilityIds.Contains("MA013") &&
           data.GetMonsterAbility("MA013").MaximumTargets == 2,
        "A jellemzők és a paraméterezett képességek szétválasztása hibás.");
}

static void SpellBuffDurationLoadsAsRounds()
{
    var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var blessing = catalog.GetSpellEffects("P003");
    Assert(blessing.Count == 3 && blessing.All(effect => effect.Duration == 4) &&
           blessing.All(effect => effect.Description.Contains("kör", StringComparison.OrdinalIgnoreCase)),
        "Az Áldás CSV-ben megadott négykörös időtartama vagy leírása nem töltődött be.");

    var active = new ActiveSpellEffect("P003", ActiveSpellEffectType.HitBonus, 1, 4, Beneficial: true);
    var json = JsonSerializer.Serialize(active);
    var restored = JsonSerializer.Deserialize<ActiveSpellEffect>(json);
    Assert(json.Contains("RemainingActions", StringComparison.Ordinal) && restored?.RemainingRounds == 4,
        "A köralapú varázshatás nem kompatibilis a korábbi mentések RemainingActions mezőjével.");
}

static void NewSpellEffectsAreSupported()
{
    var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    foreach (var id in new[] { "S027", "S028", "P026", "P027", "P028" })
        Assert(catalog.GetSpellEffects(id).Count > 0, $"A(z) {id} varázslat hatásai hiányoznak.");

    var enemy = new ConfiguredEnemy(new Position(3, 3), catalog.GetEnemy("E001"));
    enemy.ApplySpellEffect(new ActiveSpellEffect("S027", ActiveSpellEffectType.HitBonus, -4, 4));
    enemy.ApplySpellEffect(new ActiveSpellEffect("S027", ActiveSpellEffectType.VisionBonus, -3, 4));
    Assert(enemy.SpellEffectValue(ActiveSpellEffectType.HitBonus) == -4 &&
           enemy.EffectiveVisionRange == Math.Max(1, enemy.Definition.VisionRange - 3),
        "A Vakítás nem rontja az ellenfél találatát és látótávját.");

    var ally = CreateCharacter("Átok sújtott");
    ally.ApplySpellEffect(new ActiveSpellEffect("P003", ActiveSpellEffectType.DefenseBonus, 2, 4,
        Beneficial: true));
    ally.ApplySpellEffect(new ActiveSpellEffect("S027", ActiveSpellEffectType.HitBonus, -4, 4));
    var service = new SpellExecutionService(catalog, new Random(1));
    var maze = new Maze(7, 7);
    service.DispelAt(new Position(2, 2), 0, maze, [(ally, new Position(2, 2))], "HarmfulOnly");
    Assert(ally.HasSpellEffect(ActiveSpellEffectType.DefenseBonus) &&
           !ally.HasSpellEffect(ActiveSpellEffectType.HitBonus),
        "Az Átoktörés a káros hatás helyett a hasznos buffot is eltávolította.");

    Assert(service.IsOffensiveSpell(catalog.GetSpell("S027")) &&
           service.IsOffensiveSpell(catalog.GetSpell("S028")) &&
           service.IsOffensiveSpell(catalog.GetSpell("P028")),
        "Az új támadó vagy kontrollvarázslatok nem minősülnek támadónak.");
}

static void MonsterRegenerationAndBreathCooldownWork()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var troll = new ConfiguredEnemy(new Position(1, 1), data.GetEnemy("E013"));
    troll.SetCurrentHitPoints(400);
    var battle = new BattleSystem(new Random(7), data.MonsterAbilities, data.Statuses, data.StrengthHitBonuses);
    var start = battle.BeginEnemyTurn(troll);
    Assert(troll.CurrentHitPoints == 405 && start.Entries.Any(entry => entry.Message.Contains("regenerálódik")),
        "A Troll kör eleji regenerációja nem működik.");

    var dragon = new ConfiguredEnemy(new Position(1, 1), data.GetEnemy("E021"));
    var breath = data.GetWeapon("WN006");
    var preparation = battle.PrepareEnemyWeapon(dragon, breath);
    Assert(dragon.IsWeaponPrepared(breath.Id) &&
           battle.SelectEnemyAttackWeapon(dragon)?.Id == breath.Id &&
           preparation.Message.Contains("következő saját körében"),
        "A lehelet előkészítése vagy előrejelzése hibás.");
    battle.MarkEnemyWeaponUsed(dragon, breath);
    Assert(!dragon.IsWeaponPrepared(breath.Id) && !dragon.IsWeaponReady(breath.Id),
        "A lehelet elsütése nem törölte az előkészítést vagy nem indította el a lehűlést.");
    battle.BeginEnemyTurn(dragon);
    battle.BeginEnemyTurn(dragon);
    Assert(!dragon.IsWeaponReady(breath.Id), "A lehelet túl korán vált újra használhatóvá.");
    battle.BeginEnemyTurn(dragon);
    Assert(dragon.IsWeaponReady(breath.Id), "A lehelet nem vált használhatóvá három saját kör után.");
}

static void TimedNonDamageStatusExpires()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var character = CreateCharacter("Dermedt");
    character.AddStatus(data.GetStatus("STATUS006"));
    var first = character.ApplyTurnEndStatusEffects(new Random(1));
    var second = character.ApplyTurnEndStatusEffects(new Random(1));
    Assert(first.Count == 1 && first[0].Damage == 0 && !first[0].Expired &&
           second.Count == 1 && second[0].Expired && !character.HasStatus("STATUS006"),
        "A sebzés nélküli időzített állapot nem két saját akció után járt le.");
}
static void CompositeMonsterAbilityAppliesAllEffects()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var source = data.GetMonsterAbility("MA012");
    var ability = source with { ChancePercent = 100 };
    var definition = data.GetEnemy("E018") with { AbilityIds = ["MA012"] };
    var enemy = new ConfiguredEnemy(new Position(1, 1), definition);
    var target = CreateCharacter("Sugárcél", vitality: 100);
    var battle = new BattleSystem(new Random(1), [ability], data.Statuses, data.StrengthHitBonuses);
    var runtime = battle.PrepareTeamCharacter(target).Runtime;
    battle.PrepareEnemyForBattle(enemy);

    var before = target.CurrentVitality;
    var result = battle.ResolveTeamEnemyAbility(enemy, target, runtime, ability);

    Assert(target.HasStatus("STATUS006") && target.CurrentVitality == before - 4 &&
           enemy.RemainingAbilityCharges.GetValueOrDefault("MA012") == 1 &&
           result.Message.Contains("nekrotikus", StringComparison.OrdinalIgnoreCase) &&
           result.Message.Contains("4 nekrotikus", StringComparison.Ordinal),
        "Az összetett Bénító sugár nem alkalmazta együtt az állapotot, sebzést és töltetfogyást.");
}

static void MonsterAbilityRespectsWeaponBinding()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var poison = data.GetMonsterAbility("MA002") with { ChancePercent = 100, WeaponIds = ["WN018"] };
    var dagger = data.GetWeapon("W001");
    var fangs = data.GetWeapon("WN018");
    var enemy = new ConfiguredEnemy(new Position(1, 1), new EnemyDefinition(
        "E-BIND", "Kötési próba", "k", 1, 100, 0, 100, 1, 1, ["MA002"],
        Weapons: [dagger, fangs]));
    var target = CreateCharacter("Kötési cél", vitality: 500);
    var battle = new BattleSystem(new Random(2), [poison], data.Statuses, data.StrengthHitBonuses);
    var runtime = battle.PrepareTeamCharacter(target).Runtime;

    for (var i = 0; i < 20; i++) battle.ResolveTeamEnemyAction(enemy, target, runtime, dagger);
    Assert(!target.HasStatus(CharacterStatusIds.Poisoned),
        "A mérgezés a hozzá nem kötött tőrrel is aktiválódott.");

    for (var i = 0; i < 20 && !target.HasStatus(CharacterStatusIds.Poisoned); i++)
        battle.ResolveTeamEnemyAction(enemy, target, runtime, fangs);
    Assert(target.HasStatus(CharacterStatusIds.Poisoned),
        "A mérgezés a hozzá kötött méregfogakkal sem aktiválódott.");
}
static void EnemyAwarenessAndSearchAreDataDriven()
{
    var definition = new EnemyDefinition("E-SLEEP", "Alvó őr", "e", 1, 10, 0, 1,
        1, 1, Array.Empty<string>(), VisionRange: 6, CanSleep: true);
    var enemy = new ConfiguredEnemy(new Position(4, 5), definition);
    enemy.ConfigureMovement(EnemyMovementProfile.Stationary, Direction.Left);
    enemy.ConfigureAwareness(EnemyAlertness.Sleeping, new Position(4, 5));
    Assert(enemy.MovementProfile == EnemyMovementProfile.Stationary &&
           enemy.Alertness == EnemyAlertness.Sleeping && enemy.EffectiveVisionRange == 1,
        "Az alvó álló ellenfél profilja vagy csökkentett észlelése hibás.");

    var target = CharacterId.New();
    enemy.BeginPursuit(target, new Position(9, 5), 4);
    enemy.RefreshKnownTarget(new Position(10, 5));
    Assert(enemy.Alertness == EnemyAlertness.Alert && enemy.PursuitState == EnemyPursuitState.Pursuing &&
           enemy.EffectiveVisionRange == 6 && enemy.ConsumeReactionDelay() &&
           enemy.ReactionDelayMovesRemaining == 3,
        "Az észlelés nem ébresztette fel késleltetve az álló ellenfelet.");
    enemy.BeginSearch(1, enemy.LastKnownTargetPosition ?? enemy.Position, EnemySearchRole.Scout);
    enemy.RecordSearchVisit(new Position(9, 5));
    Assert(enemy.SearchRole == EnemySearchRole.Scout &&
           enemy.SearchMovesRemaining == Enemy.MinimumSearchMoves,
        "A felderítés nem tartja be a harminclépéses minimumot.");

    var undead = new ConfiguredEnemy(new Position(1, 1), new EnemyDefinition(
        "E-UNDEAD", "Élőholt", "u", 1, 10, 0, 1, 1, 1, [MonsterAbilityIds.Undead], CanSleep: false));
    undead.ConfigureAwareness(EnemyAlertness.Sleeping);
    Assert(undead.Alertness == EnemyAlertness.Alert,
        "Az alvásra képtelen ellenfél alvó állapotba került.");

    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    Assert(data.GetEnemy("E003").CanSleep && data.GetEnemy("E007").CanSleep &&
           !data.GetEnemy("E004").CanSleep && !data.GetEnemy("E006").CanSleep,
        "A goblin, ork vagy élőholt alvásképessége hibásan töltődött be a CSV-ből.");

    var saved = new EnemySaveData(enemy.Position, enemy.Definition.Id, enemy.CurrentHitPoints,
        Alertness: enemy.Alertness, SearchRole: enemy.SearchRole, HomePosition: enemy.HomePosition,
        LastKnownTargetPosition: enemy.LastKnownTargetPosition,
        ReactionDelayMovesRemaining: enemy.ReactionDelayMovesRemaining,
        SearchMovesRemaining: enemy.SearchMovesRemaining,
        LastKnownTargetDirection: enemy.LastKnownTargetDirection,
        SearchAnchorPosition: enemy.SearchAnchorPosition,
        SearchVisitedPositions: enemy.SearchVisitedPositions.ToList());
    var restored = JsonSerializer.Deserialize<EnemySaveData>(JsonSerializer.Serialize(saved));
    Assert(restored?.SearchRole == EnemySearchRole.Scout &&
           restored.SearchMovesRemaining == Enemy.MinimumSearchMoves &&
           restored.HomePosition == new Position(4, 5) &&
           restored.SearchAnchorPosition == new Position(10, 5) &&
           restored.SearchVisitedPositions?.Contains(new Position(9, 5)) == true,
        "Az éberségi és felderítési állapot nem élte túl a mentési JSON-körutat.");
}

static void EnemyPackSearchStaysCoordinated()
{
    var target = CharacterId.New();
    var anchor = new Position(8, 6);
    var group = Enumerable.Range(0, 5).Select(index =>
    {
        var definition = new EnemyDefinition($"E-PACK-{index}", $"Falkatag {index}", "f", 1, 20, 0,
            index + 1, 1, 1, []);
        var enemy = new ConfiguredEnemy(new Position(4 + index, 4), definition);
        enemy.BeginPursuit(target, anchor, 0, 10);
        return (Enemy)enemy;
    }).ToArray();

    EnemySearchCoordinator.BeginCoordinatedSearch(group, group[0], new Random(7));

    Assert(group.Count(enemy => enemy.SearchRole == EnemySearchRole.Scout) == 2 &&
           group.Count(enemy => enemy.SearchRole == EnemySearchRole.Guarding) == 3 &&
           group.All(enemy => enemy.SearchAnchorPosition == anchor) &&
           group.Select(enemy => enemy.SearchMovesRemaining).Distinct().Count() == 1 &&
           group.Where(enemy => enemy.SearchRole == EnemySearchRole.Scout)
               .All(enemy => group.All(member => enemy.SearchVisitedPositions.Contains(member.Position))) &&
           group.All(enemy => enemy.PursuitState == EnemyPursuitState.Undecided),
        "A falka nem közös pont körül, összehangolt szerepekkel kezdte meg a keresést.");
}

static void EnemySearchExploresCorridorFrontiers()
{
    var directions = Enum.GetValues<Direction>();
    var anchor = new Position(2, 2);
    var junction = new Position(3, 2);
    var corridor = new HashSet<Position>
    {
        anchor, junction, new(4, 2), new(3, 1), new(3, 3)
    };
    var searched = new HashSet<Position> { anchor, junction, new(4, 2) };
    var branch = EnemySearchNavigator.ChooseScoutDirection(junction, anchor, Direction.Right,
        Enemy.SearchCohesionRadius, searched, directions, corridor.Contains, new Random(2));
    Assert(branch is Direction.Up or Direction.Down,
        "A felderítő a már bejárt folyosó helyett nem választott új elágazást.");

    var radiusAnchor = new Position(10, 10);
    var boundary = new Position(16, 10);
    var boundaryCorridor = new HashSet<Position> { new(15, 10), boundary, new(17, 10) };
    var inward = EnemySearchNavigator.ChooseScoutDirection(boundary, radiusAnchor, Direction.Right,
        Enemy.SearchCohesionRadius, [boundary, new Position(15, 10)], directions,
        boundaryCorridor.Contains, new Random(3));
    Assert(inward == Direction.Left,
        "A felderítő elhagyhatta a falka hatmezős keresési körzetét.");
}

static void EnemyTrackingSenseIsDataDriven()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    Assert(data.GetEnemy("E001").TrackingSense == 6 && data.GetEnemy("E005").TrackingSense == 8 &&
           data.GetEnemy("E004").TrackingSense == 5 && data.GetEnemy("E022").TrackingSense == 8 &&
           data.GetEnemy("E028").TrackingSense == 1,
        "A patkányok, farkasok, élőholtak vagy emberek nyomérzéke hibásan töltődött be.");

    var nearer = CreateCharacter("Közelebbi");
    var preferred = CreateCharacter("Üldözött");
    var positions = new[]
    {
        (nearer, new Position(2, 1)),
        (preferred, new Position(4, 1))
    };
    int? Distance(Position position) => position.X - 1;
    var sensed = EnemyTargeting.ChooseNearestSensed(new Position(1, 1), positions, 4, Distance,
        new Random(1), preferred.Id);
    Assert(sensed?.Character == preferred &&
           EnemyTargeting.ChooseNearestSensed(new Position(1, 1), positions, 0, Distance,
               new Random(1)) is null &&
           EnemyTargeting.ChooseNearestSensed(new Position(1, 1), positions, 1, Distance,
               new Random(1))?.Character == nearer,
        "A nyomérzék hatótávja vagy a már üldözött célpont elsőbbsége hibás.");
}

static void PartyFormationPositionsFollowFacing()
{
    var leader = CreateCharacter("Alakzatvezer");
    var right = CreateCharacter("Jobbszel");
    var rearLeft = CreateCharacter("Hatso bal");
    var rearRight = CreateCharacter("Hatso jobb");
    var formation = new PartyFormationSnapshot(leader.Id, right.Id, rearLeft.Id, rearRight.Id,
        Direction.Up, PartyFormationState.Locked);
    var up = PartyFormationRules.Positions(formation, leader.Id, new Position(10, 10));
    var turned = PartyFormationRules.Rotate(formation, clockwise: true);
    var facingRight = PartyFormationRules.Positions(turned, leader.Id, new Position(10, 10));
    Assert(up[leader.Id] == new Position(10, 10) && up[right.Id] == new Position(11, 10) &&
           up[rearLeft.Id] == new Position(10, 11) && up[rearRight.Id] == new Position(11, 11) &&
           facingRight[leader.Id] == new Position(10, 10) &&
           facingRight[right.Id] == new Position(10, 11) &&
           facingRight[rearLeft.Id] == new Position(9, 10) &&
           facingRight[rearRight.Id] == new Position(9, 11),
        "Az alakzat slotjai nem fordultak el helyesen a vezér körül.");
}

static void PartyFormationTurnsInPlace()
{
    var leader = CreateCharacter("Fordulo");
    var right = CreateCharacter("Jobb");
    var rearLeft = CreateCharacter("HatsoBal");
    var rearRight = CreateCharacter("HatsoJobb");
    var formation = new PartyFormationSnapshot(leader.Id, right.Id, rearLeft.Id, rearRight.Id,
        Direction.Up, PartyFormationState.Locked);
    var before = PartyFormationRules.Positions(formation, leader.Id, new Position(10, 10));
    var clockwise = PartyFormationRules.RotateInPlace(formation, clockwise: true);
    var afterClockwise = PartyFormationRules.PositionsInSameFootprint(formation, leader.Id,
        new Position(10, 10), clockwise.Facing);
    var facingLeft = PartyFormationRules.FaceInPlace(formation, Direction.Left);
    var afterFacingLeft = PartyFormationRules.PositionsInSameFootprint(formation, leader.Id,
        new Position(10, 10), facingLeft.Facing);

    Assert(clockwise.Facing == Direction.Right &&
           clockwise.Slots.SequenceEqual(formation.Slots) &&
           facingLeft.Facing == Direction.Left &&
           before.Values.ToHashSet().SetEquals(afterClockwise.Values) &&
           before.Values.ToHashSet().SetEquals(afterFacingLeft.Values) &&
           afterClockwise[leader.Id] == new Position(11, 10) &&
           afterClockwise[right.Id] == new Position(11, 11) &&
           formation.Facing == Direction.Up,
        "A helyben fordulas kilépett a 2x2-es területből, átírta a slotokat vagy idő előtt módosította az állapotot.");
}

static void FormationSlotsControlFreeFollowOrder()
{
    var leader = CreateCharacter("Vezer");
    var frontLeft = CreateCharacter("BalElso");
    var frontRight = CreateCharacter("JobbElso");
    var rearLeft = CreateCharacter("BalHatso");
    var formation = new PartyFormationSnapshot(frontLeft.Id, frontRight.Id, rearLeft.Id, leader.Id,
        Direction.Up, PartyFormationState.Disbanded);
    var followOrder = PartyFormationRules.FollowOrder(formation, leader.Id,
        [leader.Id, rearLeft.Id, frontRight.Id, frontLeft.Id]);

    var maze = new Maze(11, 11);
    var memberPosition = new Position(5, 5);
    var leftTarget = new Position(4, 5);
    var upTarget = new Position(5, 4);
    var rightTarget = new Position(6, 5);
    foreach (var position in new[] { memberPosition, leftTarget, upTarget, rightTarget }) maze.Carve(position);
    var member = new PartyMemberAvatar(memberPosition, frontLeft);
    var player = new Player(new Position(5, 6), leader);
    IReadOnlyList<Position> trail =
    [
        new Position(1, 1), new Position(1, 2), leftTarget, upTarget, rightTarget,
        new Position(6, 6), player.Position
    ];
    var firstStep = PartyMovementController.FollowLeaderTrail(member, 2, maze, player, trail, followOrder: 0);
    var secondStep = PartyMovementController.FollowLeaderTrail(member, 2, maze, player, trail, followOrder: 1);
    var thirdStep = PartyMovementController.FollowLeaderTrail(member, 2, maze, player, trail, followOrder: 2);

    Assert(followOrder.SequenceEqual([frontLeft.Id, frontRight.Id, rearLeft.Id]) &&
           firstStep == rightTarget && secondStep == upTarget && thirdStep == leftTarget,
        "A szabad követés továbbra is a csatlakozási sorrendet vagy közös nyompontot használt a slotsorrend helyett.");
}

static void LockedFormationUsesSingleFileLayout()
{
    var leader = CreateCharacter("Libasorvezér");
    var second = CreateCharacter("Libasor ketto");
    var third = CreateCharacter("Libasor harom");
    var fourth = CreateCharacter("Libasor negy");
    var formation = new PartyFormationSnapshot(leader.Id, second.Id, third.Id, fourth.Id,
        Direction.Up, PartyFormationState.Locked, PartyFormationLayout.SingleFile);
    var positions = PartyFormationRules.Positions(formation, leader.Id, new Position(10, 10));
    var maze = new Maze(17, 17);
    var blockFormation = formation with { Layout = PartyFormationLayout.Block };
    var currentBlock = PartyFormationRules.Positions(blockFormation, leader.Id, new Position(10, 10));
    foreach (var position in currentBlock.Values) maze.Carve(position);
    maze.Carve(new Position(10, 9));
    maze.Carve(new Position(9, 9));
    var blockDestinations = currentBlock.ToDictionary(pair => pair.Key, pair => pair.Value + Direction.Up);
    var shifted = PartyFormationController.SingleFileDestinations(formation, currentBlock, leader.Id,
        new Position(10, 9));
    var turned = PartyFormationController.SingleFileDestinations(formation, shifted, leader.Id,
        new Position(9, 9));

    Assert(formation.State == PartyFormationState.Locked &&
           positions[leader.Id] == new Position(10, 10) &&
           positions[second.Id] == new Position(10, 11) &&
           positions[third.Id] == new Position(10, 12) &&
           positions[fourth.Id] == new Position(10, 13) &&
           PartyFormationController.IsSingleFilePassage(blockDestinations, shifted, maze) &&
           shifted[leader.Id] == new Position(10, 9) &&
           shifted[second.Id] == new Position(10, 10) &&
           shifted[fourth.Id] == new Position(11, 10) &&
           shifted[third.Id] == new Position(11, 11) &&
           turned[leader.Id] == new Position(9, 9) &&
           turned[second.Id] == new Position(10, 9) &&
           turned[fourth.Id] == new Position(10, 10) &&
           turned[third.Id] == new Position(11, 10) &&
           ConsoleRenderer.FormationStatusText(formation).Contains("zárt · libasor", StringComparison.Ordinal),
        "A libasor nem maradt zárt, nem fűződött ki a szobából vagy nem követte a folyosó kanyarját.");
}

static void FormationEscortPositionsFollowRearEdge()
{
    var leader = CreateCharacter("Kísérővezér");
    var second = CreateCharacter("Kísérőtárs");
    var block = new PartyFormationSnapshot(leader.Id, second.Id, null, null,
        Direction.Up, PartyFormationState.Locked);
    var blockPositions = PartyFormationRules.Positions(block, leader.Id, new Position(10, 10));
    var blockEscorts = PartyFormationController.EscortPositions(blockPositions, block.Facing);
    var singleFile = block with { Layout = PartyFormationLayout.SingleFile };
    var filePositions = PartyFormationRules.Positions(singleFile, leader.Id, new Position(10, 10));
    var fileEscorts = PartyFormationController.EscortPositions(filePositions, singleFile.Facing);

    Assert(blockEscorts.Take(2).ToHashSet().SetEquals([new Position(10, 11), new Position(11, 11)]) &&
           fileEscorts.First() == new Position(10, 12),
        "A követő elsődleges kísérőhelye nem az alakzat hátsó éle mögé került.");
}

static void TemporaryFollowerCanYieldToFormation()
{
    var maze = new Maze(7, 7);
    var destination = new Position(3, 3);
    maze.Carve(destination);
    var followerCharacter = CreateCharacter("Kitérő követő");
    var followerNpc = new WorldNpc(destination, "NPC-YIELD", followerCharacter,
        NpcDisposition.Friendly, false, false, string.Empty, WorldNpcState.Following);
    var follower = new PartyMemberAvatar(destination, followerCharacter, followerNpc);
    maze.AddPartyMember(follower);
    var positions = new Dictionary<CharacterId, Position> { [CharacterId.New()] = destination };

    Assert(!PartyFormationController.CanFormationOccupy(positions, maze, _ => null) &&
           PartyFormationController.CanFormationOccupy(positions, maze, _ => null,
               avatar => avatar.IsTemporaryFollower),
        "A követő nem különbözik meg az alakzat elől kitérni képtelen akadálytól.");
}

static void LockedFormationSharesDoorInteractionOrigins()
{
    var leader = CreateCharacter("Ajtóvezér");
    var rear = CreateCharacter("Ajtótárs");
    var leaderPosition = new Position(5, 5);
    var rearPosition = new Position(5, 6);
    var positions = new Dictionary<CharacterId, Position>
    {
        [leader.Id] = leaderPosition,
        [rear.Id] = rearPosition
    };
    var locked = new PartyFormationSnapshot(leader.Id, null, rear.Id, null,
        Direction.Up, PartyFormationState.Locked);
    var disbanded = locked with { State = PartyFormationState.Disbanded };

    Assert(PartyFormationRules.InteractionOrigins(locked, leader.Id, leaderPosition, positions)
               .ToHashSet().SetEquals([leaderPosition, rearPosition]) &&
           PartyFormationRules.InteractionOrigins(disbanded, leader.Id, leaderPosition, positions)
               .SequenceEqual([leaderPosition]),
        "Az ajtó-interakció hatósugara nem csak zárt alakzatban terjed ki a többi slot pozíciójára.");
}

static void FormationDoorKeyOwnerTakesPriority()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var leader = CreateCharacter("Tolvajvezér", characterClassId: CharacterClassIds.Tolvaj);
    var secondThief = CreateCharacter("Másik tolvaj", characterClassId: CharacterClassIds.Tolvaj);
    var keyOwner = CreateCharacter("Kulcstartó");
    var otherOwner = CreateCharacter("Másik kulcs");
    Assert(secondThief.AddToBackpack(data.GetItem(MiscItemIds.Key)) &&
           keyOwner.AddToBackpack(data.GetItem(MiscItemIds.Key)) &&
           otherOwner.AddToBackpack(data.GetItem(MiscItemIds.Key)),
        "A tesztparti nem kapta meg a három kulcsot.");
    LiveCharacter[] owners = [leader, secondThief, keyOwner, otherOwner];

    var selectedOwner = DoorInteractionRules.SelectKeyOwner(leader, owners, useKeyChoice: true, keyOwner.Id);
    Assert(selectedOwner == keyOwner && selectedOwner.RemoveFromBackpack(MiscItemIds.Key) &&
           !DoorInteractionRules.HasKey(keyOwner) && DoorInteractionRules.HasKey(secondThief) &&
           DoorInteractionRules.HasKey(otherOwner),
        "Az alakzatos zárnyitás nem pontosan a kiválasztott partitag kulcsát fogyasztotta el.");
    Assert(DoorInteractionRules.SelectKeyOwner(leader, owners, useKeyChoice: false, otherOwner.Id) is null,
        "A visszautasított kulcshasználat mégis kiválasztott egy kulcstulajdonost.");
}

static void FormationAssemblySwapsFriendlyAvatars()
{
    var maze = new Maze(7, 7);
    var firstPosition = new Position(2, 3);
    var secondPosition = new Position(3, 3);
    maze.Carve(firstPosition);
    maze.Carve(secondPosition);
    var member = new PartyMemberAvatar(firstPosition, CreateCharacter("Alakzattag"));
    var followerCharacter = CreateCharacter("Koveto");
    var followerNpc = new WorldNpc(secondPosition, "NPC-SWAP", followerCharacter,
        NpcDisposition.Friendly, false, false, string.Empty, WorldNpcState.Following);
    var follower = new PartyMemberAvatar(secondPosition, followerCharacter, followerNpc);
    maze.AddPartyMember(member);
    maze.AddPartyMember(follower);

    Assert(maze.TrySwapPartyMembers(member, follower, maze.Entrance) &&
           member.Position == secondPosition && follower.Position == firstPosition &&
           followerNpc.Position == firstPosition && maze.GetPartyMemberAt(firstPosition) == follower &&
           maze.GetPartyMemberAt(secondPosition) == member,
        "A baratsagos helycsere atfedest hagyott vagy nem mozgatta a koveto world-NPC allapotat.");
}

static void LockedFormationRejectsRemoteMovement()
{
    var (session, _, companion) = CreateSession();
    var remote = session.RegisterRemotePlayer();
    Assert(session.TryAssignRemoteControl(remote, companion.Id, out var error), error);
    session.SetFormationMovementLocked(true);
    var rejectedEvents = CollectEvents(session);
    session.Submit(new MoveCharacterCommand(remote, 1, companion.Id, Direction.Right));
    Assert(!session.TryReadCommand(out _) && rejectedEvents.OfType<GameCommandRejectedEvent>().Any(entry =>
               entry.Reason.Contains("alakzat", StringComparison.OrdinalIgnoreCase)),
        "A zart alakzatbol erkezo vendegmozgas atjutott a host validaciojan.");
    session.SetFormationMovementLocked(false);
    var move = new MoveCharacterCommand(remote, 2, companion.Id, Direction.Right);
    session.Submit(move);
    Assert(session.TryReadCommand(out var accepted) && accepted == move,
        "Feloszlatott alakzat utan sem kapta vissza a vendeg a mozgast.");
}

static void TacticalDistanceUsesConsoleAspectRatio()
{
    var origin = new Position(10, 10);
    Assert(TacticalDistance.Between(origin, new Position(20, 10)) == 5 &&
           TacticalDistance.Between(origin, new Position(10, 15)) == 5 &&
           TacticalDistance.IsWithin(origin, new Position(18, 11), 5) &&
           !TacticalDistance.IsWithin(origin, new Position(20, 11), 5),
        "A taktikai távolság nem azonos léptékben kezeli a vízszintes és függőleges irányt.");
}

static void DiagonalEnemyIsMeleeAdjacent()
{
    var origin = new Position(10, 10);
    Assert(TacticalDistance.IsMeleeAdjacent(origin, new Position(9, 9)) &&
           TacticalDistance.IsMeleeAdjacent(origin, new Position(11, 11)) &&
           TacticalDistance.IsMeleeAdjacent(origin, new Position(10, 11)) &&
           !TacticalDistance.IsMeleeAdjacent(origin, origin) &&
           !TacticalDistance.IsMeleeAdjacent(origin, new Position(12, 10)),
        "A közelharci szomszédság nem pontosan a nyolc környező mezőt fogadja el.");
}

static void TacticalBattleStateOrdersEligibleParticipants()
{
    var first = new TacticalBattleParticipant(new CombatantId("character:first"), BattleSide.Friendly,
        TacticalParticipantKind.PartyMember, new Position(10, 10), 8, 3);
    var second = new TacticalBattleParticipant(new CombatantId("enemy:first"), BattleSide.Hostile,
        TacticalParticipantKind.Enemy, new Position(11, 10), 6, 2);
    var late = new TacticalBattleParticipant(new CombatantId("character:late"), BattleSide.Friendly,
        TacticalParticipantKind.PartyMember, new Position(20, 10), 12, 5, EligibleFromCycle: 2);
    var state = new TacticalBattleState(BattleId.New(), new Position(10, 10), [first, second, late],
        openingOrder: [second.Id, first.Id]);

    Assert(state.InitiativeOrder.Select(value => value.Id).SequenceEqual([first.Id, second.Id]) &&
           state.IsInsideBattleArea(new Position(20, 10)),
        "A nyitószakaszban nem csak az azonnal jogosult résztvevők kerültek sorra.");
    Assert(state.StartTurns().Id == second.Id && state.AdvanceTurn().Id == first.Id &&
           state.AdvanceTurn().Id == late.Id,
        "A nyitó ütésváltást nem azonnal követte a teljes kezdeményezési sor.");
    Assert(state.InitiativeOrder.Select(value => value.Id).SequenceEqual([late.Id, first.Id, second.Id]),
        "A harmadik körben nem lépett be vagy nem kezdeményezés szerint rendeződött a távoli résztvevő.");
}

static void TeamBattleOpeningOrderUsesInitiative()
{
    var system = CreateBattleSystem(1720);
    var slower = CreateCharacter("Lassabb");
    var fasterEnemy = CreateEnemyAt(new Position(2, 1), "OPENING-FAST-ENEMY");
    var preparation = system.PrepareTeamCharacter(slower);
    var normal = new TeamBattleEncounter(new Position(1, 1),
        [new TeamCharacterParticipant(slower, new Position(1, 1), TacticalParticipantKind.PartyMember,
            4, 3, 1, preparation.Runtime)],
        [new TeamEnemyParticipant(fasterEnemy, 9, 3, 1)], slower.Id, fasterEnemy.Id);
    Assert(normal.OpeningOrder.SequenceEqual(
            [CombatantId.ForEnemy(fasterEnemy.Id), CombatantId.ForCharacter(slower.Id)]) &&
           normal.Turns.StartTurns().Id == CombatantId.ForEnemy(fasterEnemy.Id),
        "Normál találkozáskor nem a magasabb kezdeményezésű fél kezdte a nyitó ütésváltást.");

    var faster = CreateCharacter("Gyorsabb");
    var ambusher = CreateEnemyAt(new Position(4, 3), "OPENING-AMBUSHER");
    var ambushPreparation = system.PrepareTeamCharacter(faster);
    var ambush = new TeamBattleEncounter(new Position(3, 3),
        [new TeamCharacterParticipant(faster, new Position(3, 3), TacticalParticipantKind.PartyMember,
            20, 3, 1, ambushPreparation.Runtime)],
        [new TeamEnemyParticipant(ambusher, 1, 3, 1)], faster.Id, ambusher.Id,
        enemyStrikesFirst: true);
    Assert(ambush.OpeningOrder.SequenceEqual(
            [CombatantId.ForEnemy(ambusher.Id), CombatantId.ForCharacter(faster.Id)]) &&
           ambush.Turns.StartTurns().Id == CombatantId.ForEnemy(ambusher.Id),
        "Az ellenséges rajtaütés nem őrizte meg a szörny nyitó elsőbbségét.");
}

static void FirstStrikeUsesSeparateOpeningInitiative()
{
    var normalCharacter = CreateCharacter("Normál");
    var firstStrikeCharacter = CreateCharacter("Elsőcsapás");
    Assert(firstStrikeCharacter.AddPerk(new PerkDefinition(PerkIds.FighterFirstStrike, "Első csapás",
            "Teszt", CharacterClassIds.Harcos, 1)),
        "A tesztkarakter nem kapta meg az Első csapás tehetséget.");

    var normalPreparation = CreateBattleSystem(1721).PrepareTeamCharacter(normalCharacter);
    var firstStrikePreparation = CreateBattleSystem(1721).PrepareTeamCharacter(firstStrikeCharacter);
    Assert(firstStrikePreparation.Initiative == normalPreparation.Initiative + 2 &&
           firstStrikePreparation.OpeningInitiative == normalPreparation.OpeningInitiative + 10 &&
           firstStrikePreparation.OpeningInitiative == firstStrikePreparation.Initiative + 8,
        "Az Első csapás nem ugyanarra a dobásra adta a nyitó +10 és a rendes +2 bónuszt.");

    var enemy = CreateEnemyAt(new Position(2, 1), "FIRST-STRIKE-ENEMY");
    var enemyInitiative = firstStrikePreparation.Initiative + 5;
    var encounter = new TeamBattleEncounter(new Position(1, 1),
        [new TeamCharacterParticipant(firstStrikeCharacter, new Position(1, 1),
            TacticalParticipantKind.PartyMember, firstStrikePreparation.Initiative, 3, 1,
            firstStrikePreparation.Runtime, firstStrikePreparation.OpeningInitiative)],
        [new TeamEnemyParticipant(enemy, enemyInitiative, 3, 1)], firstStrikeCharacter.Id, enemy.Id);
    var characterId = CombatantId.ForCharacter(firstStrikeCharacter.Id);
    var enemyId = CombatantId.ForEnemy(enemy.Id);

    Assert(encounter.OpeningOrder.SequenceEqual([characterId, enemyId]) &&
           encounter.Turns.StartTurns().Id == characterId &&
           encounter.Turns.AdvanceTurn().Id == enemyId &&
           encounter.Turns.AdvanceTurn().Id == enemyId,
        "Az Első csapás nyitó bónusza nem csak az első ütésváltást rendezte át.");
}

static void SpellEffectsReorderInitiativeAtCycleBoundary()
{
    var system = CreateBattleSystem(1722);
    var character = CreateCharacter("Gyorsított");
    var enemy = CreateEnemy(20, 2, speed: 7);
    var preparation = system.PrepareTeamCharacter(character);
    var encounter = new TeamBattleEncounter(new Position(1, 1),
        [new TeamCharacterParticipant(character, new Position(1, 1), TacticalParticipantKind.PartyMember,
            5, 3, 1, preparation.Runtime)],
        [new TeamEnemyParticipant(enemy, 7, 3, 1)], character.Id, enemy.Id);
    var characterId = CombatantId.ForCharacter(character.Id);
    var enemyId = CombatantId.ForEnemy(enemy.Id);

    Assert(encounter.Turns.StartTurns().Id == enemyId,
        "A teszt kezdeti kezdeményezési sorrendje hibás.");
    character.ApplySpellEffect(new ActiveSpellEffect("HASTE-TEST", ActiveSpellEffectType.InitiativeBonus,
        5, 3, Beneficial: true));
    enemy.ApplySpellEffect(new ActiveSpellEffect("SLOW-TEST", ActiveSpellEffectType.SpeedPenalty, 3, 3));
    Assert(encounter.Turns.CurrentParticipant?.CurrentInitiative == 7,
        "A varázshatás kör közben megváltoztatta az aktuális sorrendet.");

    encounter.AdvanceTurn();
    var secondCycleFirst = encounter.AdvanceTurn();
    Assert(encounter.Turns.Cycle == 2 && secondCycleFirst.Id == characterId &&
           encounter.Turns.Find(characterId)?.CurrentInitiative == 10 &&
           encounter.Turns.Find(enemyId)?.CurrentInitiative == 4 &&
           encounter.InitiativeChangesAtCycleStart.Count == 2,
        "A gyorsítás és lassítás nem a következő kör kezdeményezését rendezte át.");

    character.RemoveSpellEffects(effect => effect.SourceSpellId == "HASTE-TEST");
    enemy.RemoveSpellEffects(effect => effect.SourceSpellId == "SLOW-TEST");
    encounter.AdvanceTurn();
    var thirdCycleFirst = encounter.AdvanceTurn();
    Assert(encounter.Turns.Cycle == 3 && thirdCycleFirst.Id == enemyId &&
           encounter.Turns.Find(characterId)?.CurrentInitiative == 5 &&
           encounter.Turns.Find(enemyId)?.CurrentInitiative == 7 &&
           encounter.InitiativeChangesAtCycleStart.Count == 2,
        "A lejárt gyorsítás és lassítás nem állította vissza a következő kör sorrendjét.");

    encounter.AdvanceTurn();
    encounter.AdvanceTurn();
    Assert(encounter.Turns.Cycle == 4 && encounter.InitiativeChangesAtCycleStart.Count == 0,
        "A rendszer változatlan kezdeményezés mellett is körönkénti naplóeseményt készítene.");
}

static void StatusPenaltyReordersInitiativeAtCycleBoundary()
{
    var system = CreateBattleSystem(1723);
    var character = CreateCharacter("Rémült");
    var enemy = CreateEnemy(20, 2, speed: 7);
    var preparation = system.PrepareTeamCharacter(character);
    var encounter = new TeamBattleEncounter(new Position(1, 1),
        [new TeamCharacterParticipant(character, new Position(1, 1), TacticalParticipantKind.PartyMember,
            8, 3, 1, preparation.Runtime)],
        [new TeamEnemyParticipant(enemy, 7, 3, 1)], character.Id, enemy.Id);
    var characterId = CombatantId.ForCharacter(character.Id);
    var enemyId = CombatantId.ForEnemy(enemy.Id);
    var fear = new StatusDefinition("FEAR-TEST", "Rettegés", "😱", 2,
        0, 0, 0, 2, 0, 100, 100, 100, 100, 0, 0, 1, "Teszt");

    Assert(encounter.Turns.StartTurns().Id == characterId,
        "A státuszteszt kezdeti kezdeményezési sorrendje hibás.");
    character.AddStatus(fear);
    encounter.AdvanceTurn();
    var penalizedCycleFirst = encounter.AdvanceTurn();
    Assert(encounter.Turns.Cycle == 2 && penalizedCycleFirst.Id == enemyId &&
           encounter.Turns.Find(characterId)?.CurrentInitiative == 6 &&
           encounter.InitiativeChangesAtCycleStart is [{ PreviousInitiative: 8, CurrentInitiative: 6 }],
        "Az időzített kezdeményezés-büntetés nem rendezte át a következő kört.");

    character.RemoveStatus(fear.Id);
    encounter.AdvanceTurn();
    var restoredCycleFirst = encounter.AdvanceTurn();
    Assert(encounter.Turns.Cycle == 3 && restoredCycleFirst.Id == characterId &&
           encounter.Turns.Find(characterId)?.CurrentInitiative == 8 &&
           encounter.InitiativeChangesAtCycleStart is [{ PreviousInitiative: 6, CurrentInitiative: 8 }],
        "A megszűnt kezdeményezés-büntetés nem állította vissza a következő kör sorrendjét.");
}

static void InitiativeTiesRemainStable()
{
    var first = new TacticalBattleParticipant(new CombatantId("character:a"), BattleSide.Friendly,
        TacticalParticipantKind.PartyMember, new Position(1, 1), 5, 3);
    var second = new TacticalBattleParticipant(new CombatantId("character:b"), BattleSide.Friendly,
        TacticalParticipantKind.PartyMember, new Position(2, 1), 5, 3);
    var state = new TacticalBattleState(BattleId.New(), new Position(1, 1), [second, first]);

    Assert(state.InitiativeOrder.Select(participant => participant.Id).SequenceEqual([first.Id, second.Id]) &&
           state.StartTurns().Id == first.Id && state.AdvanceTurn().Id == second.Id &&
           state.AdvanceTurn().Id == first.Id &&
           state.InitiativeOrder.Select(participant => participant.Id).SequenceEqual([first.Id, second.Id]),
        "Az azonos kezdeményezésű résztvevők sorrendje megváltozott a körhatáron.");
}

static void TacticalArrivalRequiresWalkableRoute()
{
    var origin = new Position(0, 0);
    var closed = new Position(2, 0);
    bool IsCorridorOpen(Position position) => position.Y == 0 && position.X is >= 0 and <= 4 && position != closed;
    bool IsOpenCorridor(Position position) => position.Y == 0 && position.X is >= 0 and <= 4;
    bool HasArrived(Position position) => position == new Position(4, 0);

    Assert(!TacticalArrivalRules.CanReachWithin(origin, 6, IsCorridorOpen, HasArrived) &&
           TacticalArrivalRules.CanReachWithin(origin, 6, IsOpenCorridor, HasArrived),
        "A taktikai érkezés nem különítette el a zárt és a járható útvonalat.");
}

static void TeamBattleDetectsInactiveSide()
{
    var system = CreateBattleSystem(1710);
    var character = CreateCharacter("Aktivitás");
    var enemy = CreateEnemy(20, 2);
    var preparation = system.PrepareTeamCharacter(character);
    var encounter = new TeamBattleEncounter(new Position(1, 1),
        [new TeamCharacterParticipant(character, new Position(1, 2), TacticalParticipantKind.PartyMember,
            preparation.Initiative, 3, 1, preparation.Runtime)],
        [new TeamEnemyParticipant(enemy, 5, 2, 1)], character.Id, enemy.Id);
    encounter.Turns.StartTurns();
    encounter.RecordAttack(BattleSide.Friendly);
    encounter.AdvanceTurn();
    encounter.AdvanceTurn();

    Assert(encounter.InactiveSidesLastCompletedCycle.Count == 0,
        "A rendszer már az első tétlen kör után lezárná a csatát.");

    encounter.RecordAttack(BattleSide.Friendly);
    encounter.AdvanceTurn();
    encounter.AdvanceTurn();

    Assert(encounter.InactiveSidesLastCompletedCycle.SetEquals([BattleSide.Hostile]),
        "A rendszer nem azonosította a két körön át mozdulatlan és támadás nélküli oldalt.");
}

static void EncounterThreatAssessmentRecognizesSafeFight()
{
    var party = Enumerable.Range(0, 4).Select(index => CreateCharacter($"Hős{index}", 30)).ToArray();
    var weakEnemy = new EnemyDefinition("E-WEAK", "Gyenge ellenfél", "e", 1, 2, 0, 1,
        1, 1, []);
    var boss = weakEnemy with { HitPoints = 100, Strength = 12, Armor = 8, Speed = 8,
        StrengthTier = 10, Rank = EnemyRank.Boss };
    var safe = EncounterThreatEvaluator.Assess(party, [weakEnemy]);
    var dangerous = EncounterThreatEvaluator.Assess(party, [boss]);
    Assert(safe.IsOverwhelminglySafe && safe.HostileToFriendlyRatio <= 0.25 &&
           !dangerous.IsOverwhelminglySafe && dangerous.HostilePower > safe.HostilePower,
        "A fenyegetésbecslés nem különíti el a jelentéktelen ellenfelet a bosstól.");
}

static void QuickCombatAllowsUpToThreeSafeEnemies()
{
    var party = Enumerable.Range(0, 4).Select(index => CreateCharacter($"Gyorshős{index}", 30)).ToArray();
    var weak = new EnemyDefinition("E-QUICK", "Jelentéktelen ellenfél", "e", 1, 2, 0, 1,
        1, 1, []);
    var giantRat = new EnemyDefinition("E001", "Óriáspatkány", "r", 2, 20, 0, 5,
        100, 1, ["MA003"]);
    var safe = QuickCombatRules.Assess(party, [weak]);
    var safeGroup = QuickCombatRules.Assess(party, [weak, weak, weak]);
    var giantRatGroup = QuickCombatRules.Assess(party, [giantRat, giantRat, giantRat]);
    var fragileParty = party.ToArray();
    fragileParty[0].ReceiveDamage(29);
    var injuredGroup = QuickCombatRules.Assess(fragileParty, [giantRat, giantRat, giantRat]);

    Assert(safe.IsEligible && safeGroup.IsEligible && giantRatGroup.IsEligible && injuredGroup.IsEligible &&
           safeGroup.PredictedInjuryRatio <= QuickCombatRules.MaximumPredictedInjuryRatio,
        "Az egy-három jelentéktelen ellenfélből álló csoport egy sérült csapattal sem lett gyorsharcra alkalmas.");
    Assert(!QuickCombatRules.Assess(party, [weak, weak, weak, weak]).IsEligible &&
           !QuickCombatRules.Assess(party, [weak with { Rank = EnemyRank.Elite }]).IsEligible &&
           !QuickCombatRules.Assess(party, [weak], hasAvailableReinforcements: true).IsEligible &&
           !QuickCombatRules.Assess(party, [weak], hasActiveFormation: true).IsEligible &&
           !QuickCombatRules.Assess(party, [weak], isQuestImportant: true).IsEligible &&
           !QuickCombatRules.Assess(party, [weak], enemyStrikesFirst: true).IsEligible,
        "A gyorsharc valamelyik taktikai vagy halálkockázatos helyzetet tévesen átengedte.");
}

static void QuickCombatSettingPersists()
{
    var path = Path.Combine(Path.GetTempPath(), $"kaoszrubin-settings-{Guid.NewGuid():N}.json");
    try
    {
        var service = new GameSettingsService(path);
        service.Settings.QuickCombat = QuickCombatMode.Automatic;
        service.Save();
        var loaded = new GameSettingsService(path);
        var invalid = new GameSettings
        {
            QuickCombat = (QuickCombatMode)999,
            VolumePercent = 150,
            SoundEffectsVolumePercent = -10
        };
        invalid.Normalize();

        Assert(loaded.Settings.QuickCombat == QuickCombatMode.Automatic &&
               invalid.QuickCombat == QuickCombatMode.Ask && invalid.VolumePercent == 100 &&
               invalid.SoundEffectsVolumePercent == 0,
            "A gyorsharc módja nem maradt meg vagy az érvénytelen beállítás nem normalizálódott.");
    }
    finally
    {
        if (File.Exists(path)) File.Delete(path);
    }
}

static void BattleDetailsPanelPagesCalculation()
{
    var details = new BattleActionDetails(Guid.NewGuid(), "Grok", "Kobold",
        ["🎯 1/1 találat", "💥 Sebzés: 12", "🎲 Kritikus: 15% — nem"],
        Enumerable.Range(1, 12).Select(index => $"🎯 {index}. módosító: +{index}").ToArray());
    var first = BattleDetailsPanel.Build(details, 0);
    var pages = BattleDetailsPanel.PageCount(details);
    var last = BattleDetailsPanel.Build(details, pages - 1);

    Assert(first.Count == BattleDetailsPanel.Height && pages > 1 &&
           first.Any(line => line.Text.Contains("15%")) &&
           first[0].Text.StartsWith("├") && first[0].Text.Length == BattleDetailsPanel.ExtendedWidth &&
           first[0].ExtendsToDivider && first[^1].ExtendsToDivider &&
           first[^1].Text.EndsWith("┤") && first[^1].Text.Contains("−/+") &&
           first[^1].Segments?.Any(segment => segment.Text == "−/+" &&
               segment.Color == ConsoleColor.Yellow) == true &&
           last[^1].Text.Contains($"{pages}/{pages}"),
        "A csatarészlet panel mérete, kritikus esélye vagy lapozása hibás.");
}

static void QuickCombatSummaryListsKillsAndExperience()
{
    var iskra = CharacterId.New();
    var yorgrim = CharacterId.New();
    TeamBattleKill[] kills =
    [
        new(iskra, "Iskra", "E001", "Óriáspatkány", 100),
        new(iskra, "Iskra", "E001", "Óriáspatkány", 100),
        new(yorgrim, "Yorgrim", "E002", "Kobold", 150)
    ];

    var summary = ConsoleRenderer.FormatQuickBattleKillSummary(kills);
    Assert(summary == "Szerzett XP: 350. Iskra legyőzött 2 ellenfelet: 2× Óriáspatkány; " +
           "Yorgrim legyőzött 1 ellenfelet: 1× Kobold.",
        "A gyorsharc összesítője nem a tényleges ölőket, ellenféltípusokat és XP-t írta ki.");
}

static void TeamBattleSummaryListsResourceUse()
{
    var summary = ConsoleRenderer.FormatTeamBattleResourceSummary(
    [
        new TeamBattleCharacterResult("Iskra", 0, 0, false, ["🤒"], 0),
        new TeamBattleCharacterResult("Yorgrim", 0, 0, false, ["🤒"], 0),
        new TeamBattleCharacterResult("Fürge", 0, 0, false, [], 0),
        new TeamBattleCharacterResult("Pál", 0, 20, false, [], 3)
    ], 7);
    Assert(summary == "Iskra: ❤️-0 🤒; Yorgrim: ❤️-0 🤒; Fürge: ❤️-0; " +
           "Pál: ❤️-0🔷-20 ✨3 Mindenki 🍖-7 💧-7",
        "A csapatharc erőforrás-összesítője nem személyenként és tömör emoji-formában jelenik meg.");

    TeamBattleKill[] kills =
    [
        new(CharacterId.New(), "Iskra", "E001", "Óriáspatkány", 100),
        new(CharacterId.New(), "Pál", "E001", "Óriáspatkány", 100),
        new(CharacterId.New(), "Yorgrim", "E001", "Óriáspatkány", 100)
    ];
    var victorySummary = ConsoleRenderer.FormatTeamBattleVictorySummary(true, 7, 17, kills);
    Assert(victorySummary ==
           "🏆🤖 CSAPATHARC GYŐZELEM — ⌛7 🕧17 ☠ 3 🎖 300. " +
           "Iskra ☠ 1: 1× Óriáspatkány; Pál ☠ 1: 1× Óriáspatkány; Yorgrim ☠ 1: 1× Óriáspatkány.",
        $"Az autoharc győzelmi sora hibás: {victorySummary}");

    var retreatSummary = ConsoleRenderer.FormatTeamBattleRetreatSummary(7, 17, kills);
    Assert(retreatSummary == "🏃 CSAPATHARC VISSZAVONULÁS — ⌛7 🕧17 ☠ 3 🎖 300 XP.",
        $"A visszavonulási összefoglaló nem jelzi a megtartott öléseket és XP-t: {retreatSummary}");

    var (encounter, front, _, _) = CreateFormationEncounter();
    var poisoned = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName))
        .GetStatus(CharacterStatusIds.Poisoned);
    front.AddStatus(poisoned);
    encounter.CaptureNewStatuses();
    front.RemoveStatus(poisoned.Id);
    encounter.RecordSpellCast(front);
    encounter.RecordSpellCast(front);
    var recorded = encounter.ResultFor(front);
    Assert(recorded.GainedStatusIcons.SequenceEqual([poisoned.Icon]) && recorded.SpellsCast == 2,
        "A csata közben megszűnt állapot vagy a karakter varázslatszáma elveszett az összesítőből.");
}

static void TeamBattleAttackUsesExistingCombatRules()
{
    var system = CreateBattleSystem(1701);
    var fighter = CreateCharacter("Csapatharcos", 30);
    var preparation = system.PrepareTeamCharacter(fighter);
    Assert(preparation.Runtime.TryChooseTactic(fighter, BattleTactic.FighterPrecise),
        "A harcos nem tudta kiválasztani a meglévő pontos taktikát.");
    var enemy = CreateEnemy(30, 2, 1);
    system.BeginTeamCharacterTurn(fighter);
    var entry = system.ResolveTeamCharacterAttack(fighter, preparation.Runtime, enemy);
    Assert(entry.Message.Contains(fighter.Name, StringComparison.Ordinal) &&
           entry.Message.Contains(enemy.Name, StringComparison.Ordinal) &&
           !entry.Message.Contains("1. akció", StringComparison.Ordinal) && enemy.CurrentHitPoints <= 30 &&
           entry.Details is { } details && details.Summary.Any(line => line.Contains("Kritikus")) &&
           details.Calculation.Any(line => line.StartsWith("🎯")) &&
           details.Calculation.Any(line => line.StartsWith("💥")),
        "A csapatharcos támadás nem a meglévő találat/sebzés naplóformátumot és HP-kezelést használja.");
}

static void TeamBattleEngagementLastsUntilEnemyDeath()
{
    var system = CreateBattleSystem(1702);
    var character = CreateCharacter("Lekötött hős");
    var enemy = CreateEnemy(20, 2);
    var preparation = system.PrepareTeamCharacter(character);
    var encounter = new TeamBattleEncounter(new Position(1, 1),
        [new TeamCharacterParticipant(character, new Position(1, 2), TacticalParticipantKind.PartyMember,
            preparation.Initiative, 3, 1, preparation.Runtime)],
        [new TeamEnemyParticipant(enemy, 5, 2, 1)], character.Id, enemy.Id);
    encounter.Engage(character, enemy);
    Assert(encounter.IsEngaged(character) && encounter.IsEngaged(enemy),
        "A közelharci páros nem került lekötött állapotba.");
    enemy.SetCurrentHitPoints(0);
    Assert(!encounter.IsEngaged(character),
        "A karaktert a legyőzött ellenfél továbbra is lekötve tartja.");
}

static void TeamBattleFormationProtectsRearRow()
{
    var (encounter, front, rear, _) = CreateFormationEncounter();
    Assert(encounter.HasActiveFormation && encounter.IsFrontRow(front) && encounter.IsRearRow(rear) &&
           encounter.RearPartnerOf(front) == rear && encounter.FrontPartnerOf(rear) == front,
        "A harc nem őrizte meg az alakzat sorait és oszloppárját.");
    Assert(encounter.IsProtectedRearTarget(rear, new Position(3, 2)) &&
           !encounter.IsProtectedRearTarget(rear, new Position(2, 4)) &&
           !encounter.IsProtectedRearTarget(rear, new Position(3, 5)),
        "Az első sor nem csak az alakzat eleje felől védi a hátsó társat.");
}

static void MonsterStrengthCreatesTacticalPressure()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var attacker = CreateEnemy(100, 13);
    var plainDefender = CreateCharacter("Támasz nélkül", 100);
    var bracedDefender = CreateCharacter("Pajzsos", 100);
    Assert(bracedDefender.EquipWeapon(0, data.GetWeapon("W004")) &&
           bracedDefender.EquipWeapon(1, data.GetWeapon("W014")),
        "A pajzsos Erőpróba-teszt felszerelése sikertelen.");
    var plainSystem = CreateBattleSystem(1712);
    var bracedSystem = CreateBattleSystem(1712);
    var plainRuntime = plainSystem.PrepareTeamCharacter(plainDefender).Runtime;
    var bracedRuntime = bracedSystem.PrepareTeamCharacter(bracedDefender).Runtime;
    Assert(bracedRuntime.TryChooseTactic(bracedDefender, BattleTactic.FighterDefensive),
        "A védekező állás nem volt kiválasztható az Erőpróba tesztjében.");
    var plain = plainSystem.ResolveMonsterStrengthContest(attacker, plainDefender, plainRuntime);
    var braced = bracedSystem.ResolveMonsterStrengthContest(attacker, bracedDefender, bracedRuntime);
    Assert(plain.Roll == braced.Roll && plain.ResistanceRoll == braced.ResistanceRoll &&
           plain.Total == braced.Total && braced.StrengthPressure == (braced.Strength + 1) / 2 &&
           braced.ShieldBonus == 2 && braced.DefensiveBonus == 2 &&
           braced.Resistance == plain.Resistance + 4,
        "A pajzs vagy a védekező állás nem növelte helyesen az Erőpróba ellenállását.");
    Assert(plain.Outcome == (plain.Margin >= 5 ? MonsterStrengthContestOutcome.Push :
               plain.Margin >= 1 ? MonsterStrengthContestOutcome.Stagger : MonsterStrengthContestOutcome.Resisted),
        "Az Erőpróba különbsége nem a megfelelő taktikai hatást választotta.");

    var race = new RaceDefinition("R-STRENGTH", "Ember", PrimaryAbilities.Zero);
    var fighterClass = new CharacterClassDefinition(CharacterClassIds.Harcos, "Harcos",
        PrimaryAbilities.Zero, false, 1.0);
    var sturdyDefender = new LiveCharacter("Szívós", race, fighterClass,
        new PrimaryAbilities(5, 5, 10, 5), 100, 0, 1, 0);
    Assert(sturdyDefender.EquipWeapon(0, data.GetWeapon("W004")) &&
           sturdyDefender.EquipWeapon(1, data.GetWeapon("W014")),
        "A szívós pajzsos tesztkarakter felszerelése sikertelen.");
    var sturdyRuntime = CreateBattleSystem(0).PrepareTeamCharacter(sturdyDefender).Runtime;
    var kobold = CreateEnemy(30, 3);
    Assert(Enumerable.Range(0, 200).All(seed =>
            CreateBattleSystem(seed).ResolveMonsterStrengthContest(kobold, sturdyDefender, sturdyRuntime).Outcome ==
            MonsterStrengthContestOutcome.Resisted),
        "A 3-as Erővel rendelkező kobold szerencsével megtántoríthatta a 10-es Egészségű pajzsost.");

    var ordinaryDefender = CreateCharacter("Átlagos", 100);
    var ordinaryRuntime = CreateBattleSystem(0).PrepareTeamCharacter(ordinaryDefender).Runtime;
    var strongMonster = CreateEnemy(100, 16);
    var strongOutcomes = Enumerable.Range(0, 200).Select(seed =>
        CreateBattleSystem(seed).ResolveMonsterStrengthContest(strongMonster, ordinaryDefender,
            ordinaryRuntime).Outcome).ToArray();
    Assert(strongOutcomes.Contains(MonsterStrengthContestOutcome.Resisted) &&
           strongOutcomes.Any(outcome => outcome is MonsterStrengthContestOutcome.Stagger or
               MonsterStrengthContestOutcome.Push),
        "A 16-os szörnyerő próbája garantálttá vagy hatástalanná vált az átlagos célpont ellen.");

    var resistedDetails = BattleSystem.DescribeMonsterStrengthContest("Ogre", "Harcos", braced,
        MonsterStrengthContestOutcome.Resisted);
    var resistedLog = BattleSystem.MonsterStrengthCombatLogMessage("Harcos",
        MonsterStrengthContestOutcome.Resisted);
    var staggerLog = BattleSystem.MonsterStrengthCombatLogMessage("Harcos",
        MonsterStrengthContestOutcome.Stagger);
    var pushLog = BattleSystem.MonsterStrengthCombatLogMessage("Harcos",
        MonsterStrengthContestOutcome.Push);
    Assert(resistedLog is null && resistedDetails.Summary.Any(line => line.Contains("Erőpróba")) &&
           resistedDetails.Calculation.Any(line => line.Contains("Erőhatás")),
        "Az ellenállt Erőpróba nem csak a csatarészletek paneljére került.");
    Assert(staggerLog is not null && pushLog is not null &&
           !staggerLog.Contains("Erőpróba") && !pushLog.Contains("Erőpróba"),
        "A tényleges Erőhatás naplóbejegyzése még mindig kiírja a próba részleteit.");

    var character = CreateCharacter("Tántorgó", characterClassId: CharacterClassIds.Barbár);
    var enemy = CreateEnemyAt(new Position(8, 8), "E-STRENGTH");
    var system = CreateBattleSystem(1713);
    var preparation = system.PrepareTeamCharacter(character);
    var encounter = new TeamBattleEncounter(new Position(3, 3),
        [new TeamCharacterParticipant(character, new Position(3, 3), TacticalParticipantKind.PartyMember,
            preparation.Initiative, 3, 1, preparation.Runtime)],
        [new TeamEnemyParticipant(enemy, 5, 2, 1)], character.Id, enemy.Id);
    Assert(encounter.TryBeginStrengthContest(enemy) && !encounter.TryBeginStrengthContest(enemy),
        "Ugyanaz a szörny egy körben többször kezdhetett Erőpróbát.");
    Assert(encounter.StaggerCharacter(character), "A karakter nem kapta meg a tántorodást.");
    var coordinator = new TacticalTeamBattleCoordinator(data, system, new Random(1713));
    var actions = coordinator.GetTeamAllowedBattleActions(encounter, character, enemy, character,
        encounter.PositionOf(character), false, []);
    Assert(!actions.Contains(BattleActionKind.Move) && actions.Contains(BattleActionKind.Pass),
        "A megtántorított karakter továbbra is mozoghatott, vagy más akcióit is elvesztette.");
    encounter.Turns.StartTurns();
    encounter.AdvanceTurn();
    encounter.AdvanceTurn();
    Assert(!encounter.IsCharacterStaggered(character) && encounter.TryBeginStrengthContest(enemy),
        "A tántorodás nem a következő saját kör végén múlt el, vagy az Erőpróba nem újult meg körváltáskor.");
}

static void RearCombatPreparationIsLeaderControlled()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var system = CreateBattleSystem(1710);
    var leader = CreateCharacter("Vezér", characterClassId: CharacterClassIds.Barbár);
    var rearLeft = CreateCharacter("Bal hátul", characterClassId: CharacterClassIds.Mágus);
    var rearRight = CreateCharacter("Jobb hátul", characterClassId: CharacterClassIds.Pap);
    var enemy = CreateEnemyAt(new Position(3, 2), "E-PREPARE");
    TeamCharacterParticipant Participant(LiveCharacter member, Position position)
    {
        var prepared = system.PrepareTeamCharacter(member);
        return new TeamCharacterParticipant(member, position, TacticalParticipantKind.PartyMember,
            prepared.Initiative, 3, 1, prepared.Runtime);
    }

    var formation = new PartyFormationSnapshot(leader.Id, null, rearLeft.Id, rearRight.Id,
        Direction.Up, PartyFormationState.Locked);
    var encounter = new TeamBattleEncounter(new Position(3, 3),
        [Participant(leader, new Position(3, 3)), Participant(rearLeft, new Position(3, 4)),
            Participant(rearRight, new Position(4, 4))],
        [new TeamEnemyParticipant(enemy, 5, 2, 1)], leader.Id, enemy.Id, formation: formation);
    var coordinator = new TacticalTeamBattleCoordinator(data, system, new Random(1710));
    var actions = coordinator.GetTeamAllowedBattleActions(encounter, leader, enemy, leader,
        new Position(3, 3), false, []);
    Assert(actions.Contains(BattleActionKind.PrepareRearLeft) &&
           actions.Contains(BattleActionKind.PrepareRearRight),
        "A vezér nem kapta meg mindkét hátsó alakzathely felkészítő akcióját.");

    Assert(encounter.TryOrderRearCombatPreparation(FormationSlot.RearLeft, out var ordered) &&
           ordered == rearLeft && encounter.ShouldPrioritizeRearSelfBuff(rearLeft) &&
           !encounter.ShouldPrioritizeRearSelfBuff(rearRight),
        "A bal és jobb hátsó felkészítési utasítás nem maradt elkülönítve.");

    var selfBuff = new SpellDefinition("TEST-SELF-BUFF", "Próbavédelem", SpellSchool.Arcane, 1, 1, "",
        SpellTargetType.Self, 0, 0, false, SpellUsageMode.Combat);
    var defense = new SpellEffectDefinition("TEST-DEFENSE", selfBuff.Id, 1, SpellEffectType.DefenseBonus,
        null, 1, 0, 0, 0, 100, SpellResolution.Auto, null, "");
    var casterPosition = encounter.PositionOf(rearLeft);
    Assert(coordinator.ChooseNpcBuffTarget(encounter, rearLeft, casterPosition, selfBuff, [defense],
               [leader, rearLeft, rearRight], encounter.PositionOf, (_, _, _, _, _) => true,
               allowSelfBuff: false) is null &&
           coordinator.ChooseNpcBuffTarget(encounter, rearLeft, casterPosition, selfBuff, [defense],
               [leader, rearLeft, rearRight], encounter.PositionOf, (_, _, _, _, _) => true) == casterPosition,
        "A hátsó sori önbuff tiltása vagy vezetői engedélyezése nem működik.");

    Assert(encounter.TrySwapToRear(leader, out _, out _, out _, out _) &&
           !encounter.ShouldPrioritizeRearSelfBuff(rearLeft),
        "Az előresorolt tag megtartotta a csak hátsó sorban érvényes felkészítési utasítást.");
    var panel = BattleCommandPanel.Format([BattleActionKind.PrepareRearLeft, BattleActionKind.PrepareRearRight]);
    Assert(panel.Contains("B: bal hátul", StringComparison.Ordinal) &&
           panel.Contains("J: jobb hátul", StringComparison.Ordinal),
        "A két hátsó felkészítő parancs nem jelent meg külön a csatapanelen.");
}

static void TeamBattleAiHealingPotionAvoidsWaste()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var system = CreateBattleSystem(1711);
    var character = CreateCharacter("Sebesült", vitality: 200, characterClassId: CharacterClassIds.Barbár);
    Assert(character.AddToBackpack(data.GetItem("T011")) &&
           character.AddToBackpack(data.GetItem("T012")) &&
           character.AddToBackpack(data.GetItem("T013")),
        "A gyógyital-választási teszt készlete nem fért el a hátizsákban.");
    character.SetCurrentResources(95, 0);
    var enemy = CreateEnemyAt(new Position(8, 8), "E-POTION");
    var prepared = system.PrepareTeamCharacter(character);
    var encounter = new TeamBattleEncounter(new Position(3, 3),
        [new TeamCharacterParticipant(character, new Position(3, 3), TacticalParticipantKind.PartyMember,
            prepared.Initiative, 3, 1, prepared.Runtime)],
        [new TeamEnemyParticipant(enemy, 5, 2, 1)], character.Id, enemy.Id);

    Assert(TacticalTeamBattleCoordinator.ChooseNpcHealingPotionIndex(encounter, character, allowedWaste: 0) == 1,
        "A normál harci AI olyan gyógyitalt választott, amely HP-t pazarolna.");
    Assert(TacticalTeamBattleCoordinator.ChooseNpcHealingPotionIndex(encounter, character, allowedWaste: 15) == 2,
        "A felkészített hátsó tag nem a legerősebb, legfeljebb 15 HP-t pazarló gyógyitalt választotta.");
    character.SetCurrentResources(196, 0);
    Assert(TacticalTeamBattleCoordinator.ChooseNpcHealingPotionIndex(encounter, character, allowedWaste: 15) is null,
        "Az AI a 15 HP-s pazarlási határt meghaladó gyógyitalt választott.");
}

static void TeamBattleSingleFileHasNoRearProtection()
{
    var (encounter, front, rear, enemy) = CreateFormationEncounter(PartyFormationLayout.SingleFile);
    encounter.Engage(front, enemy);
    Assert(encounter.HasActiveFormation && !encounter.HasProtectiveFormation &&
           !encounter.IsProtectedRearTarget(rear, new Position(3, 2)) &&
           encounter.RearFormationEngagedEnemies(rear).Count == 0 &&
           !encounter.TrySwapToRear(front, out _, out _, out _, out _) &&
           encounter.FormationDestinations(Direction.Up).Count == 2,
        "A libasor felbomlott, vagy tévesen megkapta a 2×2-es alakzat harci előnyeit.");
}

static void TeamBattleItemUseRequiresFreeRearPosition()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var combatItemIds = data.Items.Where(item => item.UsableInCombat).Select(item => item.Id).ToArray();
    Assert(combatItemIds.SequenceEqual(["T011", "T012", "T013", "T014", "T015", "T016"]),
        "Nem kizárólag a gyógy- és varázsitalok használhatók harcban a CSV szerint.");

    var (encounter, front, rear, enemy) = CreateFormationEncounter();
    var healingPotion = data.GetItem("T011");
    var food = data.GetItem("T004");
    Assert(!encounter.CanUseItem(front, healingPotion) && encounter.CanUseItem(rear, healingPotion) &&
           !encounter.CanUseItem(rear, food),
        "Az alakzat első és hátsó sorának tárgyhasználati szabálya hibás.");
    encounter.Engage(rear, enemy);
    Assert(!encounter.CanUseItem(rear, healingPotion),
        "A lekötött hátsó sori karakter tárgyat használhatott.");
    Assert(data.GetMagicItem("M004").Kind == MagicItemKind.Wand &&
           data.GetMagicItem("M002").Kind == MagicItemKind.Scroll,
        "A pálca vagy tekercs kikerült a külön varázslási tárgykategóriából.");
}

static void TeamBattleRearPolearmReachUsesFrontEngagement()
{
    var (encounter, front, rear, enemy) = CreateFormationEncounter();
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    Assert(rear.EquipWeapon(0, data.GetWeapon("W011")),
        "A hátsó sori tesztkarakter nem tudta felszerelni a szálfegyvert.");
    encounter.Engage(front, enemy);

    Assert(encounter.RearFormationEnemiesInReach(rear).SequenceEqual([enemy]),
        "A hátsó sori szálfegyver nem érte el az előtte álló társ lekötött ellenfelét.");
}

static void TeamBattleSwapToRearTransfersEngagements()
{
    var (encounter, front, rear, enemy) = CreateFormationEncounter();
    encounter.Engage(front, enemy);

    Assert(encounter.TrySwapToRear(front, out var swappedRear, out var oldFrontPosition,
               out var oldRearPosition, out var transferred) && swappedRear == rear && transferred == 1,
        "A Hátra! nem hajtotta végre az első és hátsó társ helycseréjét.");
    Assert(encounter.FormationSlotFor(front) == FormationSlot.RearLeft &&
           encounter.FormationSlotFor(rear) == FormationSlot.FrontLeft &&
           encounter.Turns.Find(CombatantId.ForCharacter(front.Id))?.Position == oldRearPosition &&
           encounter.Turns.Find(CombatantId.ForCharacter(rear.Id))?.Position == oldFrontPosition &&
           !encounter.IsEngaged(front) && encounter.IsEngaged(rear),
        "A Hátra! nem cserélte fel atomian a slotokat, pozíciókat és lekötéseket.");
}

static void TeamBattleFormationMovementPreservesEngagements()
{
    var (encounter, front, _, enemy) = CreateFormationEncounter();
    encounter.Engage(front, enemy);
    var sideways = encounter.FormationDestinations(Direction.Right);
    var backward = encounter.FormationDestinations(Direction.Down);

    Assert(sideways.Count == 2 && encounter.PreservesEngagements(sideways) &&
           !encounter.PreservesEngagements(backward),
        "Az alakzatmozgás nem a fennálló közelharci lekötés megtartását követeli meg.");
}

static (TeamBattleEncounter Encounter, LiveCharacter Front, LiveCharacter Rear, ConfiguredEnemy Enemy)
    CreateFormationEncounter(PartyFormationLayout layout = PartyFormationLayout.Block)
{
    var system = CreateBattleSystem(1705);
    var front = CreateCharacter("Első sor");
    var rear = CreateCharacter("Hátsó sor");
    var enemy = CreateEnemyAt(new Position(3, 2), "E-FORMATION");
    var frontPreparation = system.PrepareTeamCharacter(front);
    var rearPreparation = system.PrepareTeamCharacter(rear);
    var formation = new PartyFormationSnapshot(front.Id, null, rear.Id, null,
        Direction.Up, PartyFormationState.Locked, layout);
    var encounter = new TeamBattleEncounter(new Position(3, 3),
        [
            new TeamCharacterParticipant(front, new Position(3, 3), TacticalParticipantKind.PartyMember,
                frontPreparation.Initiative, 3, 1, frontPreparation.Runtime),
            new TeamCharacterParticipant(rear, new Position(3, 4), TacticalParticipantKind.PartyMember,
                rearPreparation.Initiative, 3, 1, rearPreparation.Runtime)
        ],
        [new TeamEnemyParticipant(enemy, 5, 2, 1)], front.Id, enemy.Id, formation: formation);
    return (encounter, front, rear, enemy);
}

static void TeamBattleTargetCanBeChanged()
{
    var system = CreateBattleSystem(1703);
    var character = CreateCharacter("Célpontváltó");
    var firstEnemy = CreateEnemy(20, 2);
    var secondEnemy = CreateEnemy(20, 2);
    var preparation = system.PrepareTeamCharacter(character);
    var encounter = new TeamBattleEncounter(new Position(1, 1),
        [new TeamCharacterParticipant(character, new Position(1, 2), TacticalParticipantKind.PartyMember,
            preparation.Initiative, 3, 1, preparation.Runtime)],
        [new TeamEnemyParticipant(firstEnemy, 5, 2, 1), new TeamEnemyParticipant(secondEnemy, 4, 2, 2)],
        character.Id, firstEnemy.Id);
    encounter.Turns.StartTurns();
    var turnId = encounter.Turns.TurnId;
    Assert(encounter.TrySelectTarget(secondEnemy) && encounter.SelectedTargetEnemy() == secondEnemy &&
           encounter.Turns.TurnId == turnId,
        "A célpontváltás előreléptette a körsorrendet vagy nem őrizte meg a célpontot.");
}

static void NpcSpellcastingPolicyPreservesMana()
{
    var race = new RaceDefinition("R001", "Ember", PrimaryAbilities.Zero);
    var mageClass = new CharacterClassDefinition(CharacterClassIds.Mágus, "Mágus", PrimaryAbilities.Zero,
        true, 1.0);
    var mage = new LiveCharacter("Taktikus", race, mageClass, new PrimaryAbilities(5, 5, 5, 5),
        40, 20, 1, 0);
    mage.SetCurrentResources(14, 8);

    Assert(NpcSpellcastingPolicy.NeedsHealing(mage) && !NpcSpellcastingPolicy.IsEmergency(mage),
        "Az NPC gyógyítási küszöbe nem 35 százalék.");
    Assert(NpcSpellcastingPolicy.CanSpendMana(mage, 4) &&
           !NpcSpellcastingPolicy.CanSpendMana(mage, 5) &&
           NpcSpellcastingPolicy.CanSpendMana(mage, 5, emergency: true),
        "Az NPC nem tartja meg a 20 százalékos mannatartalékot, vagy vészhelyzetben sem oldja fel.");
    mage.SetCurrentResources(4, 8);
    Assert(NpcSpellcastingPolicy.IsEmergency(mage),
        "Az NPC nem ismeri fel a 10 százalékos gyógyítási vészhelyzetet.");

    var bolt = new SpellDefinition("TEST-BOLT", "Próbalövedék", SpellSchool.Arcane, 1, 2, "",
        SpellTargetType.Enemy, 6, 0, true, SpellUsageMode.Combat);
    var blast = bolt with { Id = "TEST-BLAST", AreaRadius = 1 };
    var damage = new SpellEffectDefinition("TEST-DAMAGE", bolt.Id, 1, SpellEffectType.Damage,
        new DiceExpression(1, 4), 0, 0, 0, 0, 100, SpellResolution.Auto, null, "");
    var chain = damage with { Id = "TEST-CHAIN", Type = SpellEffectType.ChainDamage };
    Assert(NpcSpellcastingPolicy.IsSingleTargetOffensive(bolt, [damage]) &&
           !NpcSpellcastingPolicy.IsSingleTargetOffensive(blast, [damage]) &&
           !NpcSpellcastingPolicy.IsSingleTargetOffensive(bolt, [damage, chain]) &&
           NpcSpellcastingPolicy.ActiveTypeFor(SpellEffectType.DefenseBonus) ==
           ActiveSpellEffectType.DefenseBonus,
        "Az NPC támadó- vagy buffvarázslat-besorolása hibás.");
}

static void BreakCurseRequiresUsefulPartyTarget()
{
    var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory,
        CsvGameDataLoader.GameDataFileName));
    var spell = catalog.GetSpell("P027");
    var effects = catalog.GetSpellEffects(spell.Id);
    var service = new SpellExecutionService(catalog, new Random(1802));
    var ally = CreateCharacter("Tiszta");
    var classification = NpcSpellTacticalClassifier.Classify(spell, effects);

    Assert(spell.TargetType == SpellTargetType.PartyMember &&
           !classification.IsOffensive &&
           classification.AttackPattern == NpcSpellAttackPattern.None &&
           classification.Roles.HasFlag(NpcSpellTacticalRole.Cleanse),
        "Az Átoktörés célpontja vagy taktikai tisztító besorolása hibás.");
    Assert(!service.CanAffectCharacter(spell, ally),
        "Az Átoktörés tiszta csapattársat is érvényes célpontnak tekintett.");

    ally.ApplySpellEffect(new ActiveSpellEffect("TEST-CURSE", ActiveSpellEffectType.HitBonus,
        -2, 3, Beneficial: false));
    Assert(service.CanAffectCharacter(spell, ally),
        "Az Átoktörés nem ismerte fel a káros varázshatással sújtott csapattársat.");
    ally.RemoveSpellEffects(active => !active.Beneficial);
    Assert(!service.CanAffectCharacter(spell, ally),
        "Az Átoktörés a megtisztítás után továbbra is elsüthető maradt ugyanarra a csapattársra.");
}

static void CleansingHealRequiresRemovableStatus()
{
    var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory,
        CsvGameDataLoader.GameDataFileName));
    var effects = catalog.GetSpellEffects("P014");
    var ally = CreateCharacter("Tiszta", vitality: 40);
    ally.ReceiveDamage(30);

    Assert(NpcSpellcastingPolicy.NeedsHealing(ally) &&
           !NpcSpellcastingPolicy.NeedsCleansing(ally, effects),
        "A Megtisztítás a pusztán sérült, de tiszta csapattársat is tisztítandó célpontnak tekintette.");

    ally.AddStatus(catalog.GetStatus(CharacterStatusIds.Poisoned));
    Assert(NpcSpellcastingPolicy.NeedsCleansing(ally, effects),
        "A Megtisztítás nem ismerte fel a mérgezett csapattársat.");
    ally.RemoveStatus(CharacterStatusIds.Poisoned);
    Assert(!NpcSpellcastingPolicy.NeedsCleansing(ally, effects),
        "A Megtisztítás a méreg levétele után is indokoltnak látszik.");
}

static void EngagedSupportSpellcastingIsThrottled()
{
    Assert(NpcSpellcastingPolicy.UsesEngagedSpellCadence(CharacterClassIds.Pap) &&
           NpcSpellcastingPolicy.UsesEngagedSpellCadence(CharacterClassIds.Lovag) &&
           !NpcSpellcastingPolicy.UsesEngagedSpellCadence(CharacterClassIds.Mágus),
        "A lekötött varázslási ritkítás nem pontosan a papra és a lovagra vonatkozik.");
    Assert(!NpcSpellcastingPolicy.CanCastWhileEngaged(1, urgent: false) &&
           !NpcSpellcastingPolicy.CanCastWhileEngaged(2, urgent: false) &&
           NpcSpellcastingPolicy.CanCastWhileEngaged(3, urgent: false),
        "A lekötött pap vagy lovag rutinvarázslása nem minden harmadik csatakörre korlátozott.");
    Assert(NpcSpellcastingPolicy.CanCastWhileEngaged(1, urgent: true),
        "A lekötött pap vagy lovag sürgős gyógyítását vagy tisztítását is letiltotta a ritkítás.");
}

static void NpcOffensiveSpellStrengthThresholdsAreInclusive()
{
    var tactics = NpcSpellcasterTactics.Default.StandardProfile;
    var catalog = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory,
        CsvGameDataLoader.GameDataFileName));
    var koboldStrength = catalog.GetEnemy("E002").Strength ?? 1;

    Assert(koboldStrength == 3 &&
           NpcSpellPlanningPolicy.EnemyStrength(Enumerable.Repeat(koboldStrength, 3)) == 9 &&
           NpcSpellPlanningPolicy.EnemyStrength([0, -2, 3]) == 5,
        "Az ellenség-összerő nem a résztvevők tényleges Erő tulajdonságát összegzi (3 koboldnak 9-et kell adnia).");
    Assert(!NpcSpellPlanningPolicy.ShouldCastOffensively(tactics, 7, offensiveSpellsCast: 0) &&
           NpcSpellPlanningPolicy.ShouldCastOffensively(tactics, 8, offensiveSpellsCast: 0),
        "A mágus alsó, 8-as összerőhatára nem inkluzív.");
    Assert(!NpcSpellPlanningPolicy.ShouldCastOffensively(tactics, 8, offensiveSpellsCast: 2) &&
           NpcSpellPlanningPolicy.ShouldCastOffensively(tactics, 16, offensiveSpellsCast: 2),
        "A mágus teljes támadásra váltó, 16-os összerőhatára nem inkluzív, vagy nem kezeli a kvótát.");
}

static void NpcSpellUtilityValuesTargetsAndOverkill()
{
    var caster = CreateNpcSpellTestCaster();
    var spell = new SpellDefinition("TEST-UTILITY", "Próbavillám", SpellSchool.Arcane, 1, 6, "",
        SpellTargetType.Enemy, 8, 0, true, SpellUsageMode.Combat);
    var damage = new SpellEffectDefinition("FX-TEST-UTILITY", spell.Id, 1, SpellEffectType.Damage,
        new DiceExpression(2, 6), 1, 1, 0, 0, 100, SpellResolution.Auto, null, "");
    var targets = new[]
    {
        CreateNpcSpellTestEnemy("UTILITY-1", 40, 2, new Position(2, 2)),
        CreateNpcSpellTestEnemy("UTILITY-2", 40, 2, new Position(3, 2)),
        CreateNpcSpellTestEnemy("UTILITY-3", 40, 2, new Position(4, 2))
    };
    var single = NpcSpellPlanEvaluator.Evaluate(caster, spell, [damage],
        [new NpcSpellPlanTarget(targets[0])], spell.ManaCost);
    var area = NpcSpellPlanEvaluator.Evaluate(caster, spell with
        { Id = "TEST-AREA", TargetType = SpellTargetType.Area, AreaRadius = 1, ManaCost = 8 }, [damage],
        targets.Select(enemy => new NpcSpellPlanTarget(enemy)).ToArray(), 8);

    var weak = CreateNpcSpellTestEnemy("UTILITY-WEAK", 5, 1, new Position(2, 3));
    var cheapEffect = damage with
    {
        Id = "FX-TEST-CHEAP", Dice = new DiceExpression(1, 8),
        IntelligenceMultiplier = 0, LevelMultiplier = 0, Value = 2
    };
    var wastefulEffect = damage with
    {
        Id = "FX-TEST-WASTEFUL", Dice = new DiceExpression(20, 10),
        IntelligenceMultiplier = 0, LevelMultiplier = 0, Value = 0
    };
    var cheap = NpcSpellPlanEvaluator.Evaluate(caster, spell, [cheapEffect],
        [new NpcSpellPlanTarget(weak)], 1);
    var wasteful = NpcSpellPlanEvaluator.Evaluate(caster, spell with { ManaCost = 30 }, [wastefulEffect],
        [new NpcSpellPlanTarget(weak)], 30);

    Assert(area.Utility > single.Utility * 2 && area.UsefulDamage == single.UsefulDamage * 3,
        "A többcélú varázslat nem kapta meg a csoportsebzés hasznát.");
    Assert(cheap.Utility > wasteful.Utility && wasteful.Overkill > 100,
        "A pontozás nem büntette a gyenge célpontra pazarolt nagy varázslatot.");
}

static void NpcSpellMemoryBalancesVarietyAndUtility()
{
    NpcOffensiveSpellMemory[] memories =
    [
        new("MAGIC-MISSILE", NpcSpellPlanComplexity.Simple, NpcSpellAttackPattern.SingleTarget, 1),
        new("MAGIC-MISSILE", NpcSpellPlanComplexity.Simple, NpcSpellAttackPattern.SingleTarget, 2)
    ];
    var repeated = NpcSpellPlanningPolicy.AdjustUtilityForMemory(memories, "MAGIC-MISSILE",
        NpcSpellPlanComplexity.Simple, NpcSpellAttackPattern.SingleTarget, 100);
    var unusedSimple = NpcSpellPlanningPolicy.AdjustUtilityForMemory(memories, "FROST-BOLT",
        NpcSpellPlanComplexity.Simple, NpcSpellAttackPattern.SingleTarget, 100);
    var unusedComplex = NpcSpellPlanningPolicy.AdjustUtilityForMemory(memories, "FIREBALL",
        NpcSpellPlanComplexity.Complex, NpcSpellAttackPattern.Area, 100);
    var clearlySuperiorRepeat = NpcSpellPlanningPolicy.AdjustUtilityForMemory(memories, "MAGIC-MISSILE",
        NpcSpellPlanComplexity.Simple, NpcSpellAttackPattern.SingleTarget, 250);
    var weakNovelty = NpcSpellPlanningPolicy.AdjustUtilityForMemory(memories, "FIREBALL",
        NpcSpellPlanComplexity.Complex, NpcSpellAttackPattern.Area, 40);

    Assert(unusedComplex > unusedSimple && unusedSimple > repeated,
        "A memória nem jutalmazza az egyszerű–összetett vagy repertoárváltást.");
    Assert(clearlySuperiorRepeat > weakNovelty,
        "A változatossági bónusz felülírt egy lényegesen jobb ismételt varázslatot.");
}

static void NpcSpellPlanRetentionIsStable()
{
    Assert(NpcSpellPlanningPolicy.ShouldRetainPlan(100, 91, 100),
        "A 12 százalékon belüli aktív tervet nem tartotta meg a hiszterézis.");
    Assert(!NpcSpellPlanningPolicy.ShouldRetainPlan(100, 87, 100),
        "A megtartási tartományon kívüli tervet is megtartotta a hiszterézis.");
    Assert(!NpcSpellPlanningPolicy.ShouldRetainPlan(100, 50, 52),
        "Az eredeti hasznának 55 százaléka alá esett terv nem omlott össze.");
}

static void NpcSpellPlanInvalidationCoversFailureModes()
{
    Assert(NpcSpellPlanningPolicy.CanReevaluatePlan(NpcSpellPlanStatus.SeekingPosition,
            targetIsAlive: true, spellIsAvailable: true, canSpendMana: true),
        "Egy megvalósítható, pozíciót kereső tervet érvénytelenített.");
    Assert(!NpcSpellPlanningPolicy.CanReevaluatePlan(NpcSpellPlanStatus.SeekingPosition,
            targetIsAlive: false, spellIsAvailable: true, canSpendMana: true) &&
           !NpcSpellPlanningPolicy.CanReevaluatePlan(NpcSpellPlanStatus.SeekingPosition,
               targetIsAlive: true, spellIsAvailable: false, canSpendMana: true) &&
           !NpcSpellPlanningPolicy.CanReevaluatePlan(NpcSpellPlanStatus.SeekingPosition,
               targetIsAlive: true, spellIsAvailable: true, canSpendMana: false) &&
           !NpcSpellPlanningPolicy.CanReevaluatePlan(NpcSpellPlanStatus.Failed,
               targetIsAlive: true, spellIsAvailable: true, canSpendMana: true),
        "A halott célpont, elveszett varázslat, elfogyott mana vagy sikertelen terv nem érvénytelenítette a tervet.");
}

static void NpcSpellPlanMovementHonorsClassAndBattleState()
{
    Assert(NpcSpellPlanningPolicy.CanMoveForPlan(isKnight: false, hasActiveFormation: false,
            isEngaged: false, isStaggered: false),
        "A szabad mágus nem kereshet tüzelőállást.");
    Assert(!NpcSpellPlanningPolicy.CanMoveForPlan(isKnight: true, hasActiveFormation: false,
            isEngaged: false, isStaggered: false) &&
           !NpcSpellPlanningPolicy.CanMoveForPlan(isKnight: false, hasActiveFormation: true,
               isEngaged: false, isStaggered: false) &&
           !NpcSpellPlanningPolicy.CanMoveForPlan(isKnight: false, hasActiveFormation: false,
               isEngaged: true, isStaggered: false) &&
           !NpcSpellPlanningPolicy.CanMoveForPlan(isKnight: false, hasActiveFormation: false,
               isEngaged: false, isStaggered: true),
        "A lovag, alakzat, lekötés vagy tántorodás nem tiltotta le a tervhez mozgást.");
}

static void NpcSpellPositionPenaltyIncludesTravelAndDanger()
{
    var current = NpcSpellPlanningPolicy.PositionPenalty(0, 3, 0);
    var distant = NpcSpellPlanningPolicy.PositionPenalty(4, 3, 0);
    var dangerous = NpcSpellPlanningPolicy.PositionPenalty(4, 3, 3);
    Assert(current == 0 && distant == 7 && dangerous == 13,
        $"A mozgási vagy veszélybüntetés hibás: {current}/{distant}/{dangerous}.");
}

static void NpcCasterPrefersFullSafeCastingMove()
{
    Assert(NpcSpellPlanningPolicy.CanPreferSaferFullCastingMove(3, 4, 1, 100, 94),
        "A közeli ellenfélnél nem választható a közel azonos értékű, teljes mozgásnyi tüzelőállás.");
    Assert(!NpcSpellPlanningPolicy.CanPreferSaferFullCastingMove(6, 4, 1, 100, 100) &&
           !NpcSpellPlanningPolicy.CanPreferSaferFullCastingMove(3, 4, 0, 100, 100) &&
           !NpcSpellPlanningPolicy.CanPreferSaferFullCastingMove(3, 4, 1, 100, 80),
        "A teljes mozgás preferenciája biztonságos távolságban, mozgás nélkül vagy nagy hasznosságvesztéssel is aktiválódott.");
}

static void TeamBattleStoresNpcSpellMemory()
{
    var system = CreateBattleSystem(1801);
    var caster = CreateCharacter("Memóriamágus", characterClassId: CharacterClassIds.Mágus);
    var enemy = CreateNpcSpellTestEnemy("MEMORY-TARGET", 30, 2, new Position(2, 1));
    var preparation = system.PrepareTeamCharacter(caster);
    var battle = new TeamBattleEncounter(new Position(1, 1),
        [new TeamCharacterParticipant(caster, new Position(1, 1), TacticalParticipantKind.PartyMember,
            preparation.Initiative, 3, 1, preparation.Runtime)],
        [new TeamEnemyParticipant(enemy, 5, 2, 1)], caster.Id, enemy.Id);
    var first = new NpcSpellPlan(Guid.NewGuid(), "SPELL-SIMPLE", enemy.Id, enemy.Position,
        new Position(1, 1), NpcSpellPlanComplexity.Simple, NpcSpellAttackPattern.SingleTarget,
        NpcSpellTacticalRole.Damage, 1, 1, 20, NpcSpellPlanStatus.ReadyToCast);
    var second = first with
    {
        Id = Guid.NewGuid(), SpellId = "SPELL-AREA", Complexity = NpcSpellPlanComplexity.Complex,
        AttackPattern = NpcSpellAttackPattern.Area, ExpectedTargetCount = 3
    };
    battle.RecordNpcOffensiveSpellMemory(caster, first);
    battle.RecordNpcOffensiveSpellMemory(caster, second);
    var memories = battle.NpcOffensiveSpellMemoriesFor(caster);

    Assert(memories.Count == 2 && memories[0].SpellId == "SPELL-SIMPLE" &&
           memories[1].SpellId == "SPELL-AREA" &&
           memories.Select(memory => memory.Complexity).SequenceEqual(
               [NpcSpellPlanComplexity.Simple, NpcSpellPlanComplexity.Complex]),
        "A csatamemória elvesztette a varázslatok sorrendjét vagy tervtípusát.");
}

static LiveCharacter CreateNpcSpellTestCaster()
{
    var race = new RaceDefinition("R-NPC-SPELL", "Ember", PrimaryAbilities.Zero);
    var mageClass = new CharacterClassDefinition(CharacterClassIds.Mágus, "Mágus", PrimaryAbilities.Zero,
        true, 1.0);
    return new LiveCharacter("Tervmágus", race, mageClass, new PrimaryAbilities(5, 5, 5, 8),
        40, 100, 1, 0);
}

static ConfiguredEnemy CreateNpcSpellTestEnemy(string id, int hitPoints, int strengthTier,
    Position position) => new(position, new EnemyDefinition(id, id, "e", strengthTier, hitPoints,
        0, 1, 1, strengthTier, []));

static void EngagementAdjustsSpellFailureChance()
{
    var race = new RaceDefinition("R001", "Ember", PrimaryAbilities.Zero);
    var mageClass = new CharacterClassDefinition(CharacterClassIds.Mágus, "Mágus", PrimaryAbilities.Zero,
        true, 1.0);
    var mage = new LiveCharacter("Lekötött", race, mageClass, new PrimaryAbilities(5, 5, 5, 5),
        30, 30, 1, 0);
    Assert(SpellcastingRules.CombatFailureChance(mage, engaged: false) == 0 &&
           SpellcastingRules.CombatFailureChance(mage, engaged: true) == 35,
        "A szabad varázslásnak hibakockázata van, vagy a lekötött varázslás képlete hibás.");
}

static void TeamBattleReinforcementJoinsNextCycle()
{
    var system = CreateBattleSystem(1704);
    var character = CreateCharacter("Erősítéspróba");
    var enemy = CreateEnemy(20, 2);
    var reinforcement = CreateEnemy(20, 2);
    var preparation = system.PrepareTeamCharacter(character);
    var encounter = new TeamBattleEncounter(new Position(1, 1),
        [new TeamCharacterParticipant(character, new Position(1, 2), TacticalParticipantKind.PartyMember,
            preparation.Initiative, 3, 1, preparation.Runtime)],
        [new TeamEnemyParticipant(enemy, 5, 2, 1)], character.Id, enemy.Id);
    encounter.Turns.StartTurns();
    Assert(encounter.TryAddEnemy(new TeamEnemyParticipant(reinforcement, 99, 3, 2)) &&
           !encounter.Turns.InitiativeOrder.Any(participant =>
               participant.Id == CombatantId.ForEnemy(reinforcement.Id)),
        "Az erősítés már a nyitó ütésváltásba bekerült.");
    encounter.AdvanceTurn();
    var next = encounter.AdvanceTurn();
    Assert(encounter.Turns.Cycle == 2 && next.Id == CombatantId.ForEnemy(reinforcement.Id),
        "Az erősítés nem a következő kör kezdeményezési sorrendjébe került.");
}

static void TeamBattleCommandsAreValidated()
{
    var (session, leader, _) = CreateSession();
    var battleId = BattleId.New();
    session.SetBattlePrompt(battleId, 1, leader.Id, [BattleActionKind.Move]);
    var move = new BattleActionCommand(session.HostPlayerId, 1, leader.Id, battleId, 1,
        BattleActionKind.Move, Target: new Position(4, 3));
    Assert(session.Submit(move) && session.TryReadCommand(out var acceptedMove) && acceptedMove == move,
        "A szemantikus csapatharcos mozgási parancsot elutasította a session.");

    session.SetBattlePrompt(battleId, 2, leader.Id, [BattleActionKind.UseItem]);
    var use = new BattleActionCommand(session.HostPlayerId, 2, leader.Id, battleId, 2,
        BattleActionKind.UseItem, BackpackIndex: 3);
    Assert(session.Submit(use) && session.TryReadCommand(out var acceptedUse) && acceptedUse == use,
        "A csapatharcos tárgyhasználati parancsot elutasította a session.");

    var enemyId = WorldEntityId.New();
    session.SetBattlePrompt(battleId, 3, leader.Id,
        [BattleActionKind.SelectTarget, BattleActionKind.Retreat]);
    var select = new BattleActionCommand(session.HostPlayerId, 3, leader.Id, battleId, 3,
        BattleActionKind.SelectTarget, TargetEnemyId: enemyId);
    Assert(session.Submit(select) && session.TryReadCommand(out var acceptedSelect) && acceptedSelect == select,
        "A célpontváltó parancsot elutasította a session.");
    var retreat = new BattleActionCommand(session.HostPlayerId, 4, leader.Id, battleId, 3,
        BattleActionKind.Retreat);
    Assert(session.Submit(retreat) && session.TryReadCommand(out var acceptedRetreat) && acceptedRetreat == retreat,
        "A visszavonulási parancsot elutasította a session.");

    session.SetBattlePrompt(battleId, 4, leader.Id,
        [BattleActionKind.MoveFormation, BattleActionKind.SwapToRear, BattleActionKind.PrepareRearLeft,
            BattleActionKind.PrepareRearRight, BattleActionKind.Pass]);
    var formationMove = new BattleActionCommand(session.HostPlayerId, 5, leader.Id, battleId, 4,
        BattleActionKind.MoveFormation, Target: new Position(5, 3));
    Assert(session.Submit(formationMove) && session.TryReadCommand(out var acceptedFormationMove) &&
           acceptedFormationMove == formationMove,
        "Az alakzatmozgatási parancsot elutasította a session.");
    var swap = new BattleActionCommand(session.HostPlayerId, 6, leader.Id, battleId, 4,
        BattleActionKind.SwapToRear);
    Assert(session.Submit(swap) && session.TryReadCommand(out var acceptedSwap) && acceptedSwap == swap,
        "A Hátra! parancsot elutasította a session.");
    var pass = new BattleActionCommand(session.HostPlayerId, 7, leader.Id, battleId, 4,
        BattleActionKind.Pass);
    Assert(session.Submit(pass) && session.TryReadCommand(out var acceptedPass) && acceptedPass == pass,
        "A passz parancsot elutasította a session.");
    var prepareLeft = new BattleActionCommand(session.HostPlayerId, 8, leader.Id, battleId, 4,
        BattleActionKind.PrepareRearLeft);
    var prepareRight = prepareLeft with { CommandId = 9, Action = BattleActionKind.PrepareRearRight };
    Assert(session.Submit(prepareLeft) && session.TryReadCommand(out var acceptedPrepareLeft) &&
           acceptedPrepareLeft == prepareLeft && session.Submit(prepareRight) &&
           session.TryReadCommand(out var acceptedPrepareRight) && acceptedPrepareRight == prepareRight,
        "A két hátsó felkészítő parancsot elutasította a session.");
    var malformedPass = pass with { CommandId = 10, Target = new Position(9, 9) };
    session.Submit(malformedPass);
    Assert(!session.TryReadCommand(out _),
        "A célponttal meghamisított passz parancs átjutott a session-validáción.");
}

static void EquipmentWeightAffectsMobility()
{
    var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { CharacterClassIds.Harcos };
    var race = new RaceDefinition("R-TEST", "Ember", PrimaryAbilities.Zero);
    var fighterClass = new CharacterClassDefinition(CharacterClassIds.Harcos, "Harcos",
        PrimaryAbilities.Zero, false, 1.0);
    var character = new LiveCharacter("Teherpróba", race, fighterClass,
        new PrimaryAbilities(8, 7, 5, 5), 30, 0, 1, 0);
    var light = CharacterMobilityRules.Evaluate(character);
    var weapon = new WeaponDefinition("W-HEAVY", "Nehéz fegyver", "WT001", new ValueRange(2, 4),
        1, false, allowed, "", 1, Weight: 10);
    var shield = weapon with
    {
        Id = "W-SHIELD", Name = "Nehéz pajzs", WeaponTypeId = "WT003", FamilyId = WeaponFamilies.Shield
    };
    var reserve = weapon with { Id = "W-RESERVE", Name = "Nehéz tartalékfegyver" };
    var armor = new ArmorDefinition("A-HEAVY", "Nehéz vért", new ValueRange(2, 4), allowed,
        "", 1, Weight: 14);
    var magicItem = new MagicItemDefinition("M-HEAVY", "Nehéz amulett", MagicItemKind.Amulet,
        ItemRarity.Normal, 1, 0, null, MagicItemEffect.None, 0, allowed, "", 0, Weight: 6);
    Assert(character.EquipWeapon(0, weapon) && character.EquipWeapon(1, shield) &&
           character.EquipWeapon(2, reserve) && character.EquipArmor(armor) &&
           character.AddMagicItem(magicItem),
        "A tesztfelszerelés nem volt felvehető.");
    var heavy = CharacterMobilityRules.Evaluate(character);
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));

    Assert(light.Encumbrance == EncumbranceLevel.Light && heavy.EquippedWeight == 50 &&
           heavy.CarriedWeight == 50 && heavy.CarryingCapacity == 44 &&
           heavy.CombatCarryingCapacity == 33 &&
           heavy.Encumbrance == EncumbranceLevel.Heavy &&
           heavy.InitiativeBase < light.InitiativeBase &&
           heavy.CombatMovementAllowance < light.CombatMovementAllowance &&
           data.GetWeapon("W001").Weight == 1 && data.GetWeapon("W009").Weight == 7 &&
           data.GetArmor("A006").Weight == 12 && Math.Abs(data.GetItem("T001").Weight - 0.2) < 0.001 &&
           Math.Abs(data.GetMagicItem("M001").Weight - 0.1) < 0.001,
        "A súlyadatok vagy a leterheltségi mozgásprofil hibás.");
}

static void MobilityPreviewIsVisible()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var race = new RaceDefinition("R-TEST", "Ember", PrimaryAbilities.Zero);
    var fighterClass = new CharacterClassDefinition(CharacterClassIds.Harcos, "Harcos",
        PrimaryAbilities.Zero, false, 1.0);
    var character = new LiveCharacter("Előnézet", race, fighterClass,
        new PrimaryAbilities(8, 7, 5, 5), 30, 0, 1, 0);
    Assert(character.EquipWeapon(0, data.GetWeapon("W004")) &&
           character.EquipArmor(data.GetArmor("A003")) &&
           character.AddToBackpack(data.GetArmor("A006")),
        "A mobilitási előnézet tesztfelszerelése nem volt előkészíthető.");
    var supplies = new MiscItemDefinition("I-WEIGHT", "Teherpróba", "Teszt", 1, Weight: 2);
    Assert(character.AddToBackpack(supplies) && character.AddToBackpack(supplies) &&
           character.AddToBackpack(supplies), "A többdarabos súlyteszt nem volt előkészíthető.");

    var snapshot = new SessionCharacterSnapshot(character.Id, character.Name, character.Race.Id,
        character.CharacterClass.Id, character.Level, character.CurrentVitality, character.MaximumVitality,
        character.CurrentMana, character.MaximumMana, character.FoodLevel, character.WaterLevel,
        character.Gold, character.IsAlive, null, [], InventorySnapshotProjector.Create(character),
        CharacterSheetSnapshotProjector.Create(character, data.ExperienceByLevel));
    var panel = CharacterSheetPanel.Build(snapshot, 1, 0, 12);
    var armorIndex = character.Backpack.ToList().FindIndex(item => item?.Id == "A006");
    var inspection = ItemInspectionFormatter.Format(data.GetArmor("A006"), data,
        mobilityContext: new ItemInspectionMobilityContext(snapshot, InventorySlotKind.Backpack, armorIndex));

    var loadLine = panel.Single(line => line.Row == 17);
    Assert(loadLine.Text + loadLine.ColoredSuffix == "FEGYVEREK ⚔ ⚖ 9/33  ⚡ 8" &&
           panel.Single(line => line.Row == 21).InventorySlot?.Kind == InventorySlotKind.Armor &&
           panel.Single(line => line.Row == 26).Text == $"HÁTIZSÁK 2/12 ⚖ {27.0:F1}/44" &&
           snapshot.CharacterSheet!.CarriedWeight == 27 &&
           snapshot.CharacterSheet.ExplorationMovementAllowance == 3 &&
           inspection.Text.Contains("súly: 12", StringComparison.Ordinal) &&
           inspection.Text.Contains("⚔ ⚖ 9 → 15", StringComparison.Ordinal) &&
           inspection.Text.Contains("Könnyű → Könnyű", StringComparison.Ordinal) &&
           inspection.Text.Contains("👣 4 → 4", StringComparison.Ordinal) &&
           inspection.Text.Contains("⚡ 8 → 8", StringComparison.Ordinal),
        $"A kompakt harci terhelés vagy a felszerelési előnézet hibás. Sor='{loadLine.Text + loadLine.ColoredSuffix}', " +
        $"hátizsák='{panel.Single(line => line.Row == 26).Text}', súly={snapshot.CharacterSheet!.CarriedWeight}, " +
        $"mozgás={snapshot.CharacterSheet.ExplorationMovementAllowance}, vizsgálat='{inspection.Text}'.");
}

static BattleSystem CreateBattleSystem(int seed) => new(new Random(seed),
    Array.Empty<MonsterAbilityDefinition>(), Array.Empty<StatusDefinition>(),
    Array.Empty<StrengthHitBonusDefinition>());

static ConfiguredEnemy CreateEnemy(int hitPoints, int strength, int speed = 1) => new(new Position(1, 1),
    new EnemyDefinition("E-TEST", "Tesztellenfél", "e", strength, hitPoints, 0, speed,
        1, 1, Array.Empty<string>()));

static ConfiguredEnemy CreateEnemyAt(Position position, string id, string appearance = "e") => new(position,
    new EnemyDefinition(id, "Tesztellenfél", appearance, 1, 10, 0, 1,
        1, 1, Array.Empty<string>()));

static List<GameSessionEvent> CollectEvents(GameSession session)
{
    var events = new List<GameSessionEvent>();
    session.EventPublished += events.Add;
    return events;
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void RearPriestCanTurnFrontEngagedUndead()
{
    var system = CreateBattleSystem(1706);
    var front = CreateCharacter("Első sor", characterClassId: CharacterClassIds.Harcos);
    var priest = CreateCharacter("Hátsó pap", characterClassId: CharacterClassIds.Pap);
    var undead = CreateEnemyAt(new Position(3, 2), "E-REAR-UNDEAD");
    var undeadDefinition = undead.Definition with { Traits = EnemyTraits.Undead };
    undead = new ConfiguredEnemy(new Position(3, 2), undeadDefinition);
    var frontPreparation = system.PrepareTeamCharacter(front);
    var priestPreparation = system.PrepareTeamCharacter(priest);
    var formation = new PartyFormationSnapshot(front.Id, null, priest.Id, null,
        Direction.Up, PartyFormationState.Locked);
    var battle = new TeamBattleEncounter(new Position(3, 3),
        [
            new TeamCharacterParticipant(front, new Position(3, 3), TacticalParticipantKind.PartyMember,
                frontPreparation.Initiative, 3, 1, frontPreparation.Runtime),
            new TeamCharacterParticipant(priest, new Position(3, 4), TacticalParticipantKind.PartyMember,
                priestPreparation.Initiative, 3, 1, priestPreparation.Runtime)
        ],
        [new TeamEnemyParticipant(undead, 5, 2, 1)], front.Id, undead.Id, formation: formation);
    battle.Engage(front, undead);
    battle.Turns.StartTurns();
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var coordinator = new TacticalTeamBattleCoordinator(data, system, new Random(1706));
    var actions = coordinator.GetTeamAllowedBattleActions(battle, priest, undead, priest,
        new Position(3, 4), false, []);

    Assert(TacticalTeamBattleCoordinator.ReachableTeamEnemies(battle, priest, new Position(3, 4)).Count() == 0 &&
           battle.RearFormationEngagedEnemies(priest).SequenceEqual([undead]) &&
           BattleActionCoordinator.CanTurnUndead(priest, undead) &&
           actions.Contains(BattleActionKind.TurnUndead),
        "A fegyverével nem támadó hátsó pap nem érte el Halottűzéssel az első sor által lekötött élőholtat.");
}

static void SpellcasterRetreatDistanceIsCapped()
{
    var ground = CreateEnemyAt(new Position(1, 1), "E-GROUND");
    var flyingDefinition = ground.Definition with { Id = "E-FLYING", Traits = EnemyTraits.Flying };
    var flying = new ConfiguredEnemy(new Position(2, 2), flyingDefinition);
    Assert(TacticalTeamBattleCoordinator.PreferredSpellcasterRetreatDistance([ground]) == 6 &&
           TacticalTeamBattleCoordinator.PreferredSpellcasterRetreatDistance([ground, flying]) == 8,
        "A hátráló varázshasználó 6/8 mezős biztonsági távolsága hibás.");
}

static void TacticalAttackArcsUseEnemyFacing()
{
    var system = CreateBattleSystem(1801);
    var attackers = new[]
    {
        (Character: CreateCharacter("Bal elöl"), Position: new Position(2, 4), Arc: TacticalAttackArc.Front),
        (Character: CreateCharacter("Elöl"), Position: new Position(3, 4), Arc: TacticalAttackArc.Front),
        (Character: CreateCharacter("Jobb elöl"), Position: new Position(4, 4), Arc: TacticalAttackArc.Front),
        (Character: CreateCharacter("Balról"), Position: new Position(2, 3), Arc: TacticalAttackArc.Flank),
        (Character: CreateCharacter("Jobbról"), Position: new Position(4, 3), Arc: TacticalAttackArc.Flank),
        (Character: CreateCharacter("Bal hátul"), Position: new Position(2, 2), Arc: TacticalAttackArc.Rear),
        (Character: CreateCharacter("Hátul"), Position: new Position(3, 2), Arc: TacticalAttackArc.Rear),
        (Character: CreateCharacter("Jobb hátul"), Position: new Position(4, 2), Arc: TacticalAttackArc.Rear)
    };
    var enemy = CreateEnemyAt(new Position(3, 3), "E-FACING");
    var participants = attackers.Select(attacker =>
    {
        var preparation = system.PrepareTeamCharacter(attacker.Character);
        return new TeamCharacterParticipant(attacker.Character, attacker.Position,
            TacticalParticipantKind.PartyMember, preparation.Initiative, 3, 1, preparation.Runtime);
    }).ToArray();
    var front = attackers[1].Character;
    var battle = new TeamBattleEncounter(new Position(3, 3),
        participants,
        [new TeamEnemyParticipant(enemy, 5, 2, 1)], front.Id, enemy.Id);

    foreach (var attacker in attackers)
    {
        var advantage = TacticalTeamBattleCoordinator.AttackAdvantage(battle, attacker.Character, enemy);
        var expectedBonus = attacker.Arc switch
        {
            TacticalAttackArc.Flank => 1,
            TacticalAttackArc.Rear => 2,
            _ => 0
        };
        Assert(advantage.Arc == attacker.Arc && advantage.HitBonus == expectedBonus,
            $"A(z) {attacker.Character.Name} pozíció {attacker.Arc} ívének felismerése hibás.");
    }

    var flank = attackers[4].Character;
    battle.FaceEnemyToward(enemy, flank);
    Assert(TacticalTeamBattleCoordinator.AttackAdvantage(battle, flank, enemy).Arc == TacticalAttackArc.Front,
        "Az ellenfél nem fordult az új célpont felé.");
}

static void ThiefCanBackstabFromRearFormation()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var system = CreateBattleSystem(1802);
    var front = CreateCharacter("Fedező", characterClassId: CharacterClassIds.Harcos);
    var thief = CreateCharacter("Orvtámadó", characterClassId: CharacterClassIds.Tolvaj);
    Assert(thief.EquipWeapon(0, data.GetWeapon("W001")), "A tolvaj nem tudta felszerelni a tőrt.");
    var enemy = CreateEnemyAt(new Position(3, 2), "E-REAR-THIEF");
    var frontPreparation = system.PrepareTeamCharacter(front);
    var thiefPreparation = system.PrepareTeamCharacter(thief);
    var formation = new PartyFormationSnapshot(front.Id, null, thief.Id, null,
        Direction.Up, PartyFormationState.Locked);
    var battle = new TeamBattleEncounter(new Position(3, 3),
        [new TeamCharacterParticipant(front, new Position(3, 3), TacticalParticipantKind.PartyMember,
             frontPreparation.Initiative, 3, 1, frontPreparation.Runtime),
         new TeamCharacterParticipant(thief, new Position(3, 4), TacticalParticipantKind.PartyMember,
             thiefPreparation.Initiative, 3, 1, thiefPreparation.Runtime)],
        [new TeamEnemyParticipant(enemy, 5, 2, 1)], front.Id, enemy.Id, formation: formation);
    battle.Engage(front, enemy);
    Assert(battle.RearFormationEnemiesInReach(thief).Count == 0,
        "A képesség nélküli hátsó sori tolvaj elérte az ellenfelet.");
    Assert(thiefPreparation.Runtime.TryChooseTactic(thief, BattleTactic.ThiefAmbush) &&
           battle.RearFormationEnemiesInReach(thief).SequenceEqual([enemy]),
        "Az Orvtámadás nem nyitotta meg a hátsó sori tőrtámadást.");
    var entry = system.ResolveTeamCharacterAttack(thief, thiefPreparation.Runtime, enemy,
        tacticalBackstab: true);
    Assert(entry.Details?.Calculation.Any(line => line.Contains("Hátbatámadás: Orvtámadás",
               StringComparison.Ordinal)) == true,
        "A hátsó sori tőrtámadás nem aktiválta az Orvtámadás sebzésszorzóját.");
}

static void MultilineInnRumorStaysInsideFrame()
{
    var rumor = new InnRumorSnapshot("🗺️ Nyom az előző pályáról",
        ["Egy zilált vándor új mozgásról beszél a már elhagyott járatokban.\r\n" +
         "Nem a teljes szörnyhorda tért vissza, de valami érdemes lehet még odalent."],
        ConsoleColor.Yellow);
    var method = typeof(ConsoleRenderer).GetMethod("BuildInnRumorLines",
        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
    var lines = (IReadOnlyList<(string Text, ConsoleColor Color)>)method.Invoke(
        null, [rumor, 2, 5, "Tesztfogadó", null])!;

    Assert(lines.Any(line => line.Text.StartsWith("Egy zilált vándor", StringComparison.Ordinal)) &&
           lines.Any(line => line.Text.StartsWith("Nem a teljes szörnyhorda", StringComparison.Ordinal)) &&
           lines.All(line => !line.Text.Contains('\r') && !line.Text.Contains('\n') &&
                             line.Text.Length <= 104),
        "A beágyazott sortörés kijutott a keret rajzolásához átadott sorból.");
}

static void DevelopmentWeaponsRespectCapacity()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var character = CreateCharacter("Tesztcsomag");
    var granted = DevelopmentWeaponGrantService.Grant(character, data.Weapons, new Random(42));
    Assert(granted.Count == 6 && granted.Select(weapon => weapon.Id).Distinct().Count() == 6 &&
        granted.Count(weapon => weapon.Rarity == ItemRarity.Normal && !weapon.IsTwoHanded) == 2 &&
        granted.Count(weapon => weapon.Rarity == ItemRarity.Normal && weapon.IsTwoHanded) == 2 &&
        granted.Count(weapon => weapon.Rarity == ItemRarity.Magic) == 1 &&
        granted.Count(weapon => weapon.Rarity == ItemRarity.Legendary) == 1 &&
        granted.All(weapon => weapon.WeaponTypeId != "WT003" && !SpellcastingRules.IsRestrictedFromTradingAndGeneration(weapon)),
        "A fejlesztői csomag összetétele hibás.");
    Assert(character.Backpack.Count(item => item is WeaponDefinition) == 6 && character.WeaponSlots.All(item => item is null),
        "A csomag nem a hátizsákba került.");
    var limited = CreateCharacter("Kevéshely");
    for (var index = 0; index < LiveCharacter.MaximumBackpackItemCount - 1; index++)
        limited.SetInventoryItem(InventorySlotKind.Backpack, index, data.GetItem("T001"));
    var partial = DevelopmentWeaponGrantService.Grant(limited, data.Weapons, new Random(42));
    Assert(partial.Count == 1 && limited.Backpack.Take(11).All(item => item?.Id == "T001"),
        "A részleges csomag felülírta a hátizsák tartalmát.");
    var full = CreateCharacter("Telthátizsák");
    for (var index = 0; index < LiveCharacter.MaximumBackpackItemCount; index++)
        full.SetInventoryItem(InventorySlotKind.Backpack, index, data.GetItem("T001"));
    var revision = full.InventoryRevision;
    Assert(DevelopmentWeaponGrantService.Grant(full, data.Weapons, new Random(42)).Count == 0 && full.InventoryRevision == revision,
        "A telt hátizsák módosult a sikertelen kiosztástól.");
}

static void DeveloperBattleTestScenarioHasRequestedLayout()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var options = new DeveloperBattleTestOptions(12, 6, 12);
    var scenario = DeveloperBattleTestScenarioBuilder.Create(ConsoleRenderer.PlayfieldWidth,
        ConsoleRenderer.PlayfieldHeight, options, data.Enemies, new Random(4201), 30);

    Assert(scenario.EnemyGroups.Count == 6 && scenario.EnemyGroups.All(group => group.Count == 12) &&
           scenario.Maze.Enemies.Count == 72 && scenario.GroupMarkers.Count == 6 &&
           scenario.Maze.TreasureChests.Count == 6,
        "A tesztpálya nem a kért számú csoportot, ellenfelet vagy jelölőládát készítette.");
    Assert(scenario.EnemyGroups.Select(group => group[0].GroupId).Distinct().Count() == 6 &&
           scenario.EnemyGroups.All(group => group.Select(enemy => enemy.GroupId).Distinct().Count() == 1),
        "Az ellenfelek csoportazonosítói összekeveredtek.");
    Assert(scenario.EnemyGroups.SelectMany(group => group).All(enemy =>
        enemy.Position.Y < scenario.LeaderPosition.Y &&
        Math.Max(Math.Abs(enemy.Position.X - scenario.LeaderPosition.X),
            Math.Abs(enemy.Position.Y - scenario.LeaderPosition.Y)) is >=
                DeveloperBattleTestScenarioBuilder.MinimumEnemyDistance and <=
                DeveloperBattleTestScenarioBuilder.MaximumEnemyDistance),
        $"Egy ellenfél nem a felső térfélen vagy nem " +
        $"{DeveloperBattleTestScenarioBuilder.MinimumEnemyDistance}–" +
        $"{DeveloperBattleTestScenarioBuilder.MaximumEnemyDistance} mezős távolságban áll.");
    Assert(scenario.EnemyGroups.Zip(scenario.GroupMarkers).All(pair => pair.First.Any(enemy =>
        Math.Abs(enemy.Position.X - pair.Second.Position.X) +
        Math.Abs(enemy.Position.Y - pair.Second.Position.Y) == 1)),
        "Egy jelölőláda nem a saját ellenségcsoportja mellett áll.");

    var corridor = scenario.CorridorTopLeft;
    for (var offset = 0; offset < DeveloperBattleTestScenarioBuilder.CorridorLength; offset++)
    {
        Assert(scenario.Maze.IsWalkable(new Position(corridor.X, corridor.Y + offset)) &&
               scenario.Maze.IsWalkable(new Position(corridor.X + 1, corridor.Y + offset)) &&
               scenario.Maze.BlocksSight(new Position(corridor.X - 1, corridor.Y + offset)) &&
               scenario.Maze.BlocksSight(new Position(corridor.X + 2, corridor.Y + offset)),
            "A középső 2×8-as folyosó járható szélessége vagy oldalfala hibás.");
    }
}

static void LoadedDeveloperBattleCreatesRecoveryLog()
{
    var system = CreateBattleSystem(4210);
    var character = CreateCharacter("Loghős");
    var enemy = CreateNpcSpellTestEnemy("LOG-ENEMY", 30, 2, new Position(2, 1));
    var preparation = system.PrepareTeamCharacter(character);
    var battle = new TeamBattleEncounter(new Position(1, 1),
        [new TeamCharacterParticipant(character, new Position(1, 1), TacticalParticipantKind.PartyMember,
            preparation.Initiative, 3, 1, preparation.Runtime)],
        [new TeamEnemyParticipant(enemy, 5, 2, 1)], character.Id, enemy.Id);
    battle.Turns.StartTurns();

    var log = new DeveloperBattleLog();
    log.BeginBattle(battle);
    log.CompleteBattle(battle, "defeat");
    var path = log.FilePath;
    Assert(path is not null && File.Exists(path),
        "A scenario-inicializálás nélkül indított tesztcsata nem hozott létre naplófájlt.");
    using var stream = new FileStream(path!, FileMode.Open, FileAccess.Read,
        FileShare.ReadWrite | FileShare.Delete);
    using var reader = new StreamReader(stream, Encoding.UTF8);
    var text = reader.ReadToEnd();
    Assert(text.Contains("[LOG-START] reason=battle-recovery", StringComparison.Ordinal) &&
           text.Contains("[SCENARIO-RECOVERY]", StringComparison.Ordinal) &&
           text.Contains("[BATTLE-START]", StringComparison.Ordinal) &&
           text.Contains("[BATTLE-END]", StringComparison.Ordinal) &&
           text.Contains("outcome=defeat", StringComparison.Ordinal),
        "A helyreállított csatalogból hiányzik a kezdet, a csataállapot vagy a vereségi lezárás.");
    File.Delete(path!);
}

static void CombatTestCharactersMatchRequestedLevelAndSpells()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var generator = new RandomCharacterGenerator(data, new Random(4202));
    var characters = new[] { CharacterClassIds.Mágus, CharacterClassIds.Pap, CharacterClassIds.Lovag }
        .Select(classId => generator.CreateCombatTestCharacter(data.GetCharacterClass(classId), 12, []))
        .ToArray();

    Assert(characters.All(character => character.Level == 12 && character.IsAlive &&
                                     character.CurrentVitality == character.MaximumVitality &&
                                     character.CurrentMana == character.MaximumMana &&
                                     SpellcastingRules.HasRequiredFocus(character)),
        "A teszt-NPC szintje, erőforrása vagy varázsfókusza hibás.");
    Assert(characters.All(character => character.MemorizedSpells.Count ==
                                      Math.Min(character.KnownSpells.Count, character.MemorizationCapacity) &&
                                      character.MemorizedSpells.All(spell =>
                                          spell.Level <= SpellcastingRules.MaximumSpellLevel(character.Level) &&
                                          character.KnownSpells.Any(known => known.Id == spell.Id))),
        "A teszt-NPC nem az ismert, szintjén elérhető varázslataiból memorizált.");
    Assert(characters.All(character => character.ActiveWeapons.Any(weapon => weapon is not null)),
        "Egy teszt-NPC nem kapta meg a kaszt alapfegyverzetét.");
}

static void KnightBattleWeaponSwapCommandIsAccepted()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var knight = new LiveCharacter("Tesztlovag", data.GetRace("R001"),
        data.GetCharacterClass(CharacterClassIds.Lovag), new PrimaryAbilities(8, 5, 5, 5), 40, 0, 1, 0);
    Assert(knight.EquipWeapon(0, data.GetWeapon("W004")) &&
           knight.EquipWeapon(1, data.GetWeapon("W014")) &&
           knight.EquipWeapon(2, data.GetWeapon("W010")), "A kard–pajzs–csatabárd felszerelés nem állítható be.");
    var party = new Party(); party.SetLeader(knight);
    var session = new GameSession(party, knight);
    var system = CreateBattleSystem(42);
    var preparation = system.PrepareTeamCharacter(knight);
    var rat = new ConfiguredEnemy(new(3, 2), data.GetEnemy("E001"));
    var battle = new TeamBattleEncounter(new(3, 3),
        [new TeamCharacterParticipant(knight, new(3, 3), TacticalParticipantKind.PartyMember,
            preparation.Initiative, 3, 1, preparation.Runtime)],
        [new TeamEnemyParticipant(rat, 1, 1, 1)], knight.Id, rat.Id);
    var coordinator = new TacticalTeamBattleCoordinator(data, system, new Random(42));
    var allowed = coordinator.GetTeamAllowedBattleActions(battle, knight, rat, knight, new(3, 3), false, []);
    Assert(allowed.Contains(BattleActionKind.SwapWeapon), "A panel nem kínálja fel a fegyvercserét.");
    var battleId = BattleId.New();
    session.SetBattlePrompt(battleId, 1, knight.Id, allowed);
    var swap = new BattleActionCommand(session.HostPlayerId, 1, knight.Id, battleId, 1, BattleActionKind.SwapWeapon);
    Assert(session.Submit(swap) && session.TryReadCommand(out var accepted) && accepted == swap,
        "A panelen felkínált fegyvercsere-parancsot elutasította a session.");
    Assert(knight.TrySwapReserveWeapon() && knight.WeaponSlots[0]?.Id == "W010" &&
           knight.WeaponSlots[1]?.Id == "W014" && knight.WeaponSlots[2]?.Id == "W004",
        "A kard és csatabárd cseréje nem őrizte meg a pajzsot.");
    session.Submit(swap with { CommandId = 2, Target = new(9, 9) });
    Assert(!session.TryReadCommand(out _), "A célpontot tartalmazó hibás fegyvercsere átjutott.");
    session.SetBattlePrompt(battleId, 2, knight.Id, [BattleActionKind.Pass]);
    session.Submit(swap with { CommandId = 3, TurnId = 2 });
    Assert(!session.TryReadCommand(out _), "A fel nem kínált fegyvercsere átjutott.");
}

static void WeaponSweepRequiresMutualAdjacency()
{
    var (battle, front, _, primary) = CreateFormationEncounter();
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var weapon = data.GetWeapon("W009") with { MinimumStrength = 1 };
    Assert(front.EquipWeapon(0, weapon), "A pallos nem szerelhető fel.");
    var opposite = CreateEnemyAt(new Position(3, 4), "E-OPPOSITE");
    var distant = CreateEnemyAt(new Position(3, 1), "E-DISTANT");
    var side = CreateEnemyAt(new Position(4, 2), "E-SIDE");
    foreach (var enemy in new[] { opposite, distant, side })
        Assert(battle.TryAddEnemy(new(enemy, 1, 1, 1)), "Nem csatlakozott a célpont.");
    Assert(TacticalTeamBattleCoordinator.SweepTargets(battle, front, new(3, 3), primary)
        .SequenceEqual([primary, side]), "Távoli vagy átellenes célpont bekerült a csapásba.");
    side.SetCurrentHitPoints(0);
    Assert(TacticalTeamBattleCoordinator.SweepTargets(battle, front, new(3, 3), primary).Count == 1,
        "A halott célpontot vagy az átellenes ellenfelet elérte a csapás.");
    Assert(front.EquipWeapon(0, data.GetWeapon("W004")), "A kard nem szerelhető fel.");
    side.SetCurrentHitPoints(10);
    Assert(TacticalTeamBattleCoordinator.SweepTargets(battle, front, new(3, 3), primary).Count == 1,
        "Az egycélpontos kard több ellenfelet ért el.");
}

static void TacticalWeaponMasteriesHaveDistinctRoles()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var fighter = CreateCharacter("Taktikus", characterClassId: CharacterClassIds.Harcos);
    var system = CreateBattleSystem(7);
    var runtime = system.PrepareTeamCharacter(fighter).Runtime;
    Assert(runtime.TryChooseTactic(fighter, BattleTactic.FighterPowerful) &&
           TacticalTeamBattleCoordinator.SweepDamagePercent(fighter, runtime, true) == 100,
        "Az Erőteljes állás nem ad teljes erejű söprést.");

    var mage = CreateCharacter("Botharcos", characterClassId: CharacterClassIds.Mágus);
    var staff = data.GetWeapon("W018") with { MinimumStrength = 1 };
    Assert(mage.EquipWeapon(0, staff) && mage.TryAdvanceWeaponProficiency(WeaponFamilies.Staff) &&
           mage.TryAdvanceWeaponProficiency(WeaponFamilies.Staff), "A botmester tesztfelszerelése hibás.");
    var withoutStaff = CreateCharacter("Fókusz", characterClassId: CharacterClassIds.Mágus);
    Assert(SpellcastingRules.CombatFailureChance(mage, true) ==
           Math.Max(0, SpellcastingRules.CombatFailureChance(withoutStaff, true) - 10),
        "A botmester nem csökkenti tíz százalékponttal a harci varázskudarcot.");

    var swordMaster = CreateCharacter("Kardőr", characterClassId: CharacterClassIds.Harcos);
    var protectedAlly = CreateCharacter("Védett", characterClassId: CharacterClassIds.Harcos);
    Assert(swordMaster.EquipWeapon(0, data.GetWeapon("W004")) &&
           swordMaster.TryAdvanceWeaponProficiency(WeaponFamilies.Sword) &&
           swordMaster.TryAdvanceWeaponProficiency(WeaponFamilies.Sword),
        "A kardmester tesztfelszerelése hibás.");
    var swordPreparation = system.PrepareTeamCharacter(swordMaster);
    var allyPreparation = system.PrepareTeamCharacter(protectedAlly);
    var guardEnemy = CreateEnemyAt(new Position(2, 1), "SWORD-GUARD");
    var guardBattle = new TeamBattleEncounter(new Position(1, 1),
        [new TeamCharacterParticipant(swordMaster, new Position(1, 1), TacticalParticipantKind.PartyMember,
             swordPreparation.Initiative, 3, 1, swordPreparation.Runtime),
         new TeamCharacterParticipant(protectedAlly, new Position(1, 2), TacticalParticipantKind.PartyMember,
             allyPreparation.Initiative, 3, 1, allyPreparation.Runtime)],
        [new TeamEnemyParticipant(guardEnemy, 1, 2, 1)], protectedAlly.Id, guardEnemy.Id);
    Assert(TacticalTeamBattleCoordinator.AlliedGuardDefense(guardBattle, protectedAlly,
               candidate => guardBattle.PositionOf(candidate)) == 1,
        "A kardmester nem adott +1 fedezetet a szomszédos társának.");

    var milestones = CharacterProgressionService.UpcomingMilestones(fighter);
    Assert(milestones.Any(text => text.Contains("képességpont", StringComparison.OrdinalIgnoreCase)) &&
           milestones.Any(text => text.Contains("tehetség", StringComparison.OrdinalIgnoreCase)),
        "A szintlépési előnézetből hiányzik a következő fejlődés.");
}

static void WeaponFamiliesUseDistinctAttackPatterns()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));

    IReadOnlyList<Enemy> Targets(string weaponId, Position secondaryPosition)
    {
        var character = CreateCharacter($"Minta-{weaponId}", characterClassId: CharacterClassIds.Harcos);
        Assert(character.EquipWeapon(0, data.GetWeapon(weaponId) with { MinimumStrength = 1 }),
            $"A(z) {weaponId} tesztfegyver nem szerelhető fel.");
        var primary = CreateEnemyAt(new Position(3, 2), $"{weaponId}-PRIMARY");
        var secondary = CreateEnemyAt(secondaryPosition, $"{weaponId}-SECONDARY");
        var decoyPosition = weaponId == "W011" ? new Position(4, 2) : new Position(3, 4);
        var decoy = CreateEnemyAt(decoyPosition, $"{weaponId}-DECOY");
        var preparation = CreateBattleSystem(1803).PrepareTeamCharacter(character);
        var battle = new TeamBattleEncounter(new Position(3, 3),
            [new TeamCharacterParticipant(character, new Position(3, 3), TacticalParticipantKind.PartyMember,
                preparation.Initiative, 3, 1, preparation.Runtime)],
            [new TeamEnemyParticipant(primary, 3, 2, 1), new TeamEnemyParticipant(secondary, 2, 2, 1),
             new TeamEnemyParticipant(decoy, 1, 2, 1)], character.Id, primary.Id);
        return TacticalTeamBattleCoordinator.SweepTargets(battle, character, new Position(3, 3), primary);
    }

    var polearmTargets = Targets("W011", new Position(3, 1));
    Assert(polearmTargets.Count == 2 && polearmTargets[1].Position == new Position(3, 1) &&
           TacticalTeamBattleCoordinator.AttackPattern(data.GetWeapon("W011")) == WeaponAttackPattern.Line,
        "A szálfegyver nem egyenes vonalban érte el a cél mögötti mezőt.");
    var axeTargets = Targets("W017", new Position(4, 2));
    Assert(axeTargets.Count == 2 &&
           TacticalTeamBattleCoordinator.AttackPattern(data.GetWeapon("W017")) == WeaponAttackPattern.Arc,
        "A nagybalta nem ívesen söpört.");
    var hammerTargets = Targets("W013", new Position(4, 2));
    Assert(hammerTargets.Count == 2 &&
           TacticalTeamBattleCoordinator.AttackPattern(data.GetWeapon("W013")) == WeaponAttackPattern.Compact,
        "A kétkezes pöröly nem kis összefüggő területen hatott.");

    var sentinel = CreateCharacter("Feltartóztató", characterClassId: CharacterClassIds.Harcos);
    Assert(sentinel.EquipWeapon(0, data.GetWeapon("W011") with { MinimumStrength = 1 }) &&
           sentinel.TryAdvanceWeaponProficiency(WeaponFamilies.Polearm) &&
           sentinel.TryAdvanceWeaponProficiency(WeaponFamilies.Polearm),
        "A szálfegyver-mester tesztkarakter nem állítható elő.");
    var approaching = CreateEnemyAt(new Position(3, 1), "INTERCEPTED");
    var sentinelPreparation = CreateBattleSystem(1805).PrepareTeamCharacter(sentinel);
    var sentinelBattle = new TeamBattleEncounter(new Position(3, 3),
        [new TeamCharacterParticipant(sentinel, new Position(3, 3), TacticalParticipantKind.PartyMember,
            sentinelPreparation.Initiative, 3, 1, sentinelPreparation.Runtime)],
        [new TeamEnemyParticipant(approaching, 1, 2, 1)], sentinel.Id, approaching.Id);
    Assert(TacticalTeamBattleCoordinator.PolearmMasterControlling(sentinelBattle, new Position(3, 2)) == sentinel,
        "A szálfegyver-mester nem tartotta ellenőrzés alatt a belépő mezőt.");

    var stateEnemy = CreateEnemyAt(new Position(1, 1), "TACTICAL-STATE");
    var stateCharacter = CreateCharacter("Állapotteszt");
    var statePreparation = CreateBattleSystem(1804).PrepareTeamCharacter(stateCharacter);
    var stateBattle = new TeamBattleEncounter(new Position(1, 2),
        [new TeamCharacterParticipant(stateCharacter, new Position(1, 2), TacticalParticipantKind.PartyMember,
            statePreparation.Initiative, 3, 1, statePreparation.Runtime)],
        [new TeamEnemyParticipant(stateEnemy, 1, 2, 1)], stateCharacter.Id, stateEnemy.Id);
    Assert(stateBattle.ApplyArmorShred(stateEnemy, 2) && stateBattle.EnemyArmorPenalty(stateEnemy) == 2 &&
           !stateBattle.ApplyArmorShred(stateEnemy, 1) && stateBattle.StaggerEnemy(stateEnemy) &&
           stateBattle.ConsumeEnemyStagger(stateEnemy) && !stateBattle.ConsumeEnemyStagger(stateEnemy),
        "A páncélrepesztés vagy a megtorpanás harci állapota hibás.");
}

static void DualWieldingRequiresDisciplineAndProficiencies()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var shieldBearer = CreateCharacter("Pajzsos", characterClassId: CharacterClassIds.Harcos);
    Assert(shieldBearer.EquipWeapon(0, data.GetWeapon("W004")) &&
           shieldBearer.EquipWeapon(1, data.GetWeapon("W014")) &&
           !DualWieldingRules.TryGetWeapons(shieldBearer, out _, out _),
        "A rendszer a pajzsot támadó mellékkéz-fegyvernek tekintette.");

    var fighter = CreateCharacter("Kétpengés", characterClassId: CharacterClassIds.Harcos);
    Assert(fighter.EquipWeapon(0, data.GetWeapon("W001")) &&
           fighter.TryAdvanceWeaponProficiency(WeaponFamilies.Dagger) &&
           !fighter.EquipWeapon(1, data.GetWeapon("W001")),
        "A mellékkéz képesség nélkül elfogadta a második tőrt.");
    Assert(fighter.ChooseTacticalDiscipline(TacticalDisciplines.DualWield) &&
           fighter.EquipWeapon(1, data.GetWeapon("W001")) &&
           DualWieldingRules.TryGetWeapons(fighter, out var main, out var offhand) &&
           main?.Id == "W001" && offhand?.Id == "W001",
        "A két tőr a diszciplína és a jártasság után sem aktiválódott.");

    Assert(fighter.TryAdvanceWeaponProficiency(WeaponFamilies.Sword) &&
           fighter.EquipWeapon(0, data.GetWeapon("W004")) &&
           DualWieldingRules.TryGetWeapons(fighter, out _, out _),
        "A Jártas kard–tőr páros nem aktiválódott.");
    Assert(!fighter.EquipWeapon(1, data.GetWeapon("W005")) &&
           fighter.WeaponSlots[1]?.Id == "W001",
        "A mellékkéz elfogadta a nem támogatott zúzófegyvert.");

    Assert(fighter.EquipWeapon(1, data.GetWeapon("W001")), "A mellékkéz tőre nem szerelhető vissza.");
    var system = CreateBattleSystem(1806);
    var runtime = system.PrepareTeamCharacter(fighter).Runtime;
    BattleLogEntry? entry = null;
    for (var attempt = 0; attempt < 20; attempt++)
    {
        entry = system.ResolveTeamCharacterAttack(fighter, runtime, CreateEnemy(100, 0), finishAction: false,
            damagePercent: DualWieldingRules.OffhandDamagePercent, attackWeapon: fighter.WeaponSlots[1],
            allowTriggeredExtraAttacks: false, allowAmbush: false, damageScaleName: "Mellékkéz");
        if (entry.Details?.Calculation.Any(line => line.Contains("Fegyver alapsebzése: tőr") &&
                                                   line.Contains("tőr", StringComparison.OrdinalIgnoreCase)) == true)
            break;
    }
    Assert(entry?.Details?.Calculation.Any(line => line.Contains("Fegyver alapsebzése: tőr") ||
                                                   line.Contains("Mellékkéz")) == true,
        "A külön mellékkéz-támadás nem a második fegyvert vagy a 60%-os skálázást használta.");
}

static void ElvenDaggersGainPairedDamage()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var elvenDagger = data.GetWeapon(DualWieldingRules.ElvenDaggerId);
    Assert(elvenDagger.WeaponTypeId == "WT002" && elvenDagger.Damage == new ValueRange(3, 6) &&
           elvenDagger.DamageType == DamageType.Slashing &&
           WeaponFamilies.ForWeapon(elvenDagger) == WeaponFamilies.Dagger,
        "Az elf tőr nem a megadott ügyességi, vágó Tőr-profilt kapta.");

    var fighter = CreateCharacter("Elfkések", characterClassId: CharacterClassIds.Harcos);
    Assert(fighter.TryAdvanceWeaponProficiency(WeaponFamilies.Dagger) &&
           fighter.ChooseTacticalDiscipline(TacticalDisciplines.DualWield) &&
           fighter.EquipWeapon(0, elvenDagger) && fighter.EquipWeapon(1, elvenDagger) &&
           DualWieldingRules.HasPairedElvenDaggers(fighter),
        "A páros elf tőr nem szerelhető fel aktív kétfegyveres harccal.");
    var system = CreateBattleSystem(1807);
    var runtime = system.PrepareTeamCharacter(fighter).Runtime;
    BattleLogEntry? entry = null;
    for (var attempt = 0; attempt < 20; attempt++)
    {
        entry = system.ResolveTeamCharacterAttack(fighter, runtime, CreateEnemy(100, 0), finishAction: false,
            attackWeapon: elvenDagger, allowTriggeredExtraAttacks: false, allowAmbush: false);
        if (entry.Details?.Calculation.Any(line => line.Contains("Páros elf tőr", StringComparison.OrdinalIgnoreCase)) == true)
            break;
    }
    Assert(entry?.Details?.Calculation.Any(line => line.Contains("Páros elf tőr", StringComparison.OrdinalIgnoreCase)) == true,
        "A két elf tőrrel végrehajtott találat nem kapott +1 sebzést.");
}

static void TacticalDisciplinesProgressAndPersist()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var character = CreateCharacter("Diszciplína", characterClassId: CharacterClassIds.Harcos);
    var firstMilestones = CharacterProgressionService.PendingTacticalDisciplineMilestones(character,
        new LevelUpResult(0, 7, 8, [])).ToArray();
    Assert(firstMilestones.SequenceEqual(new[] { 8 }) &&
           CharacterProgressionService.TacticalDisciplineChoices(character).Count == 4,
        "A 8. szint nem nyitotta meg az első diszciplínát.");
    Assert(character.ChooseTacticalDiscipline(TacticalDisciplines.Skirmisher) &&
           !character.ChooseTacticalDiscipline(TacticalDisciplines.Skirmisher),
        "Ugyanaz a diszciplína többször kiválasztható.");
    var secondMilestones = CharacterProgressionService.PendingTacticalDisciplineMilestones(character,
        new LevelUpResult(0, 17, 18, [])).ToArray();
    Assert(secondMilestones.SequenceEqual(new[] { 18 }) &&
           character.ChooseTacticalDiscipline(TacticalDisciplines.Guardian) &&
           !character.ChooseTacticalDiscipline(TacticalDisciplines.Finisher),
        "A 18. szint vagy a kétdiszciplínás korlát hibás.");

    var baseCharacter = CreateCharacter("Alap", characterClassId: CharacterClassIds.Harcos);
    var baseInitiative = CreateBattleSystem(91).PrepareTeamCharacter(baseCharacter).Initiative;
    var disciplineInitiative = CreateBattleSystem(91).PrepareTeamCharacter(character).Initiative;
    Assert(disciplineInitiative == baseInitiative + 2,
        "A Portyázó nem adott +2 csapatharcos kezdeményezést.");

    var finisher = CreateCharacter("Kivégző", characterClassId: CharacterClassIds.Harcos);
    var woundedEnemy = CreateEnemy(20, 1, speed: 8);
    woundedEnemy.SetCurrentHitPoints(10);
    var chanceWithoutDiscipline = CreateBattleSystem(17)
        .EstimatePlayerHitChance(baseCharacter, woundedEnemy, BattleTactic.FighterPrecise);
    Assert(finisher.ChooseTacticalDiscipline(TacticalDisciplines.Finisher),
        "A Kivégző diszciplína nem választható.");
    var chanceWithDiscipline = CreateBattleSystem(17)
        .EstimatePlayerHitChance(finisher, woundedEnemy, BattleTactic.FighterPrecise);
    Assert(chanceWithDiscipline == chanceWithoutDiscipline + 10,
        "A Kivégző nem adott +2, azaz 10 százalékpontnyi találati előnyt a sebesült célpont ellen.");

    var protectedAlly = CreateCharacter("Védett", characterClassId: CharacterClassIds.Harcos);
    var guardian = CreateCharacter("Őrszem", characterClassId: CharacterClassIds.Harcos);
    Assert(guardian.ChooseTacticalDiscipline(TacticalDisciplines.Guardian),
        "A Bajtársi őrség nem választható.");
    var protectedPreparation = CreateBattleSystem(22).PrepareTeamCharacter(protectedAlly);
    var guardianPreparation = CreateBattleSystem(23).PrepareTeamCharacter(guardian);
    var guardEnemy = CreateEnemy(20, 2);
    var guardBattle = new TeamBattleEncounter(new(1, 1),
        [new TeamCharacterParticipant(protectedAlly, new(1, 1), TacticalParticipantKind.PartyMember,
             protectedPreparation.Initiative, 3, 1, protectedPreparation.Runtime),
         new TeamCharacterParticipant(guardian, new(1, 2), TacticalParticipantKind.PartyMember,
             guardianPreparation.Initiative, 3, 1, guardianPreparation.Runtime)],
        [new TeamEnemyParticipant(guardEnemy, 1, 2, 1)], protectedAlly.Id, guardEnemy.Id);
    Assert(TacticalTeamBattleCoordinator.AlliedGuardDefense(guardBattle, protectedAlly,
               candidate => candidate == protectedAlly ? new(1, 1) : new(1, 2)) == 1,
        "A Bajtársi őrség nem adott fedezetet a szomszédos társnak.");

    var service = new CharacterSaveService(Path.Combine(Path.GetTempPath(), "unused-discipline-save.json"), data);
    var restored = service.DeserializeCharacter(service.SerializeCharacter(character));
    Assert(restored.TacticalDisciplines.Select(discipline => discipline.Id).SequenceEqual(
            new[] { TacticalDisciplines.Skirmisher, TacticalDisciplines.Guardian }) &&
           CharacterSheetSnapshotProjector.Create(restored, data.ExperienceByLevel).ClassFeatureUpgradeNames!
               .Any(name => name.Contains("diszciplína", StringComparison.OrdinalIgnoreCase)),
        "A diszciplínák elvesztek a mentésben vagy nem jelennek meg a karakterlapon.");
}

static void ProgressionRetrainingPreservesAdvances()
{
    var character = CreateCharacter("Átképzett", characterClassId: CharacterClassIds.Harcos);
    Assert(character.ChooseClassFeatureUpgrade(ClassFeatureUpgrades.FighterPrecise) &&
           character.ChooseClassFeatureUpgrade(ClassFeatureUpgrades.FighterDefensive) &&
           character.ChooseTacticalDiscipline(TacticalDisciplines.Finisher) &&
           character.ChooseTacticalDiscipline(TacticalDisciplines.Guardian) &&
           character.TryAdvanceWeaponProficiency(WeaponFamilies.Sword) &&
           character.TryAdvanceWeaponProficiency(WeaponFamilies.Sword) &&
           character.TryAdvanceWeaponProficiency(WeaponFamilies.Shield),
        "Az átképzési teszt fejlődése nem állítható elő.");

    var classCount = character.ClassFeatureUpgrades.Count;
    var disciplineCount = character.TacticalDisciplines.Count;
    var weaponAdvances = character.WeaponProficiencyAdvances;
    character.ResetClassFeatureUpgrades();
    Assert(character.ChooseClassFeatureUpgrade(ClassFeatureUpgrades.FighterPowerful) &&
           character.ChooseClassFeatureUpgrade(ClassFeatureUpgrades.FighterDefensive) &&
           character.ClassFeatureUpgrades.Count == classCount &&
           character.TacticalDisciplines.Count == disciplineCount &&
           character.WeaponProficiencyAdvances == weaponAdvances,
        "Az osztályképesség-átképzés más csoportot módosított vagy lépést vesztett.");

    character.ResetWeaponProficiencies();
    Assert(character.TryAdvanceWeaponProficiency(WeaponFamilies.Axe) &&
           character.TryAdvanceWeaponProficiency(WeaponFamilies.Axe) &&
           character.TryAdvanceWeaponProficiency(WeaponFamilies.Blunt) &&
           character.WeaponProficiencyAdvances == weaponAdvances,
        "A fegyverjártasság-átképzés nem őrizte meg a lépések számát.");
    Assert(ProgressionRetrainingRules.Cost(character,
               ProgressionRetrainingKind.WeaponProficiencies) == 300 &&
           ProgressionRetrainingRules.Cost(character,
               ProgressionRetrainingKind.ClassFeatures) == 300,
        "Az átképzés minimumdíja hibás.");
}

static void ReserveWeaponIsPassiveAndPersistent()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var character = CreateCharacter("Tartalék");
    Assert(character.EquipWeapon(0, data.GetWeapon("W001")), "Hiányzik a tőr.");
    var weight = CharacterMobilityRules.Evaluate(character).EquippedWeight;
    Assert(character.EquipWeapon(2, data.GetWeapon("W004")), "Hiányzik a tartalék kard.");
    Assert(character.AttackWeapon?.Id == "W001" && character.ActiveWeapons.Count() == 2 &&
        CharacterMobilityRules.Evaluate(character).EquippedWeight == weight + 3 &&
        CharacterMobilityRules.Evaluate(character).CarriedWeight == weight + 3,
        "A tartalék aktívvá vált vagy hibásan számít a súlyba.");
    var revision = character.InventoryRevision;
    Assert(character.TrySwapReserveWeapon() && character.InventoryRevision == revision + 1 &&
        character.AttackWeapon?.Id == "W004" && character.WeaponSlots[2]?.Id == "W001", "Hibás csere.");
    var roster = new CharacterRoster(); roster.Add(character); roster.Select(character);
    var saves = new CharacterSaveService(Path.Combine(Path.GetTempPath(), "unused-reserve.json"), data);
    var restored = saves.Deserialize(saves.Serialize(roster)).SelectedCharacter!;
    Assert(restored.WeaponSlots.Select(value => value?.Id).SequenceEqual(character.WeaponSlots.Select(value => value?.Id)),
        "A tartalék fegyver elveszett a mentésben.");
}

static void NpcSwapsBrokenWeaponForOperationalReserve()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var character = CreateCharacter("Fegyverváltó");
    var brokenWeapon = data.GetWeapon("W001");
    var reserve = data.GetWeapon("W004");
    var reserveState = InventoryItemInstanceState.Create() with { DurabilityDamage = 7 };
    Assert(character.SetInventoryItem(InventorySlotKind.Weapon, 0, brokenWeapon, null, 1,
               InventoryItemInstanceState.Create() with { DurabilityDamage = brokenWeapon.MaximumDurability }) &&
           character.SetInventoryItem(InventorySlotKind.Weapon, 2, reserve, null, 1, reserveState),
        "Az NPC fegyvercsere-tesztje nem tudta előkészíteni a felszerelést.");
    Assert(TacticalTeamBattleCoordinator.ShouldNpcSwapToReserveWeapon(character),
        "Az NPC nem ismerte fel, hogy az eltört aktív fegyverét le kell cserélnie.");
    Assert(character.TrySwapReserveWeapon() && character.AttackWeapon?.Id == reserve.Id &&
           character.GetInventoryItemState(InventorySlotKind.Weapon, 0)?.DurabilityDamage == 7 &&
           character.WeaponSlots[2]?.Id == brokenWeapon.Id,
        "Az NPC tartalékfegyver-cseréje nem őrizte meg a tárgyállapotokat.");
    Assert(!TacticalTeamBattleCoordinator.ShouldNpcSwapToReserveWeapon(character),
        "Az NPC működő aktív fegyver mellett is újabb tartalékcserét kezdeményezne.");

    Assert(character.SetInventoryItem(InventorySlotKind.Weapon, 0, brokenWeapon, null, 1,
               InventoryItemInstanceState.Create() with { DurabilityDamage = brokenWeapon.MaximumDurability }) &&
           character.SetInventoryItem(InventorySlotKind.Weapon, 2, reserve, null, 1,
               InventoryItemInstanceState.Create() with { DurabilityDamage = reserve.MaximumDurability }),
        "A törött tartalékfegyveres esetet nem sikerült előkészíteni.");
    Assert(!TacticalTeamBattleCoordinator.ShouldNpcSwapToReserveWeapon(character),
        "Az NPC törött tartalékfegyvert próbálna kézbe venni.");
}

static void ReserveTwoHandedSwapStowsShield()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    var character = CreateCharacter("Pajzsos");
    Assert(character.EquipWeapon(0, data.GetWeapon("W004")) &&
        character.EquipWeapon(1, data.GetWeapon("W014")) && character.EquipWeapon(2, data.GetWeapon("W011")), "Hibás előkészítés.");
    for (var index = 0; index < LiveCharacter.MaximumBackpackItemCount; index++)
        character.SetInventoryItem(InventorySlotKind.Backpack, index, data.GetItem("T001"));
    var revision = character.InventoryRevision;
    Assert(!character.TrySwapReserveWeapon() && character.InventoryRevision == revision && character.WeaponSlots[1]?.Id == "W014",
        "Telt hátizsáknál részleges csere történt.");
    character.SetInventoryItem(InventorySlotKind.Backpack, 3, null);
    Assert(character.TrySwapReserveWeapon() && character.WeaponSlots[1] is null &&
        character.Backpack[3]?.Id == "W014" && character.WeaponSlots[2]?.Id == "W004", "A pajzs nem került biztonságba.");
}

static void PhysicalDamageUsesTypesAndWeapons()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    int PlayerDamage(DamageType type, DamageResistance? resistance = null)
    {
        var total = 0;
        for (var seed = 0; seed < 30; seed++)
        {
            var system = CreateBattleSystem(seed);
            var character = CreateCharacter("Támadó", 1000);
            character.EquipWeapon(0, data.GetWeapon("W005") with { Damage = new(20, 20), DamageType = type });
            var enemy = new ConfiguredEnemy(new(1, 1), new("E-TYPE", "Cél", "e", 1, 1000, 8, 1, 1, 1, [],
                Resistances: resistance ?? new(4, 0, -4)));
            system.ResolveTeamCharacterAttack(character, system.PrepareTeamCharacter(character).Runtime, enemy);
            total += 1000 - enemy.CurrentHitPoints;
        }
        return total;
    }
    Assert(PlayerDamage(DamageType.Bludgeoning) > PlayerDamage(DamageType.Piercing) &&
        PlayerDamage(DamageType.Piercing) > PlayerDamage(DamageType.Slashing), "A szörny típusvédelme nem számít.");
    Assert(PlayerDamage(DamageType.Acid, new(Acid: -4)) > PlayerDamage(DamageType.Acid, new(Acid: 4)) &&
           Enum.GetValues<DamageType>().Select(type => type.Name()).SequenceEqual(
               ["vágás", "szúrás", "zúzás", "tűz", "sav", "nekrotikus", "káosz"]),
        "Az elemi és természetfeletti sebzéstípusok vagy ellenállásaik hibásak.");
    int EnemyDamage(int weaponDamage, DamageResistance resistance)
    {
        var total = 0;
        for (var seed = 0; seed < 30; seed++)
        {
            var system = CreateBattleSystem(seed);
            var target = CreateCharacter("Védő", 1000);
            target.EquipArmor(data.GetArmor("A002") with { Defense = new(5, 5), Resistances = resistance });
            var enemy = new ConfiguredEnemy(new(1, 1), data.GetEnemy("E003") with
            { Weapon = data.GetWeapon("W005") with { Damage = new(weaponDamage, weaponDamage) } });
            system.ResolveTeamEnemyAction(enemy, target, system.PrepareTeamCharacter(target).Runtime);
            total += 1000 - target.CurrentVitality;
        }
        return total;
    }
    Assert(EnemyDamage(20, new()) > EnemyDamage(5, new()), "Az azonos erejű szörny fegyvere nem módosítja a sebzést.");
    Assert(EnemyDamage(20, new(0, 0, -3)) > EnemyDamage(20, new(0, 0, 3)), "A páncél típusvédelme nem számít.");
}

static void WeaponCsvPropertiesAreInherited()
{
    var data = CsvGameDataLoader.Load(Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName));
    Assert(data.Enemies.All(enemy => enemy.Weapons is { Count: > 0 } &&
            enemy.Weapons.Select(weapon => weapon.Id).SequenceEqual(enemy.WeaponIds ?? [])),
        "Fegyver nélküli vagy hibás fegyverlistájú szörny.");
    Assert(data.Enemies.All(enemy => enemy.Id is not ("E054" or "E055" or "E056" or "E057" or "E058" or "E059" or "E060")),
        "A régi goblin- vagy orkvariáns megmaradt.");
    var goblin = data.GetEnemy("E003");
    var firstGoblin = new ConfiguredEnemy(new(1, 1), goblin, new Random(7));
    var selectedGoblinWeapon = firstGoblin.Definition.Weapon ??
                               throw new InvalidOperationException("A goblin nem választott fegyvert.");
    var sameGoblin = new ConfiguredEnemy(new(1, 1), goblin, new Random(99), selectedGoblinWeapon.Id);
    Assert(goblin.Name == "Goblin" && goblin.ChoosesWeapon && goblin.WeaponIds!.Count == 3 &&
           firstGoblin.Name == $"Goblin ({selectedGoblinWeapon.Name})" &&
           sameGoblin.Definition.Weapon?.Id == selectedGoblinWeapon.Id,
        "A goblin példány nem választott és nem őrzött meg megjelenített fegyvert.");
    var savedGoblin = JsonSerializer.Deserialize<EnemySaveData>(JsonSerializer.Serialize(new EnemySaveData(
        firstGoblin.Position, firstGoblin.Definition.Id, firstGoblin.CurrentHitPoints,
        SelectedWeaponId: selectedGoblinWeapon.Id)))!;
    var restoredGoblin = new ConfiguredEnemy(savedGoblin.Position, goblin, new Random(99), savedGoblin.SelectedWeaponId);
    Assert(restoredGoblin.Name == firstGoblin.Name,
        "A példány fegyverválasztása nem élte túl a mentést.");
    var zombie = data.GetEnemy("E006");
    var zombieWeaponIds = zombie.WeaponIds ?? [];
    var zombieEnemy = new ConfiguredEnemy(new(1, 1), zombie, new Random(4));
    Assert(zombie.ChoosesWeapon && zombieWeaponIds.SequenceEqual(["WN003", "W005"]) &&
           zombieEnemy.Definition.Weapon is { } zombieWeapon &&
           zombieWeaponIds.Contains(zombieWeapon.Id) && zombieEnemy.Name.Contains($"({zombieWeapon.Name})"),
        "A zombi nem választ egyszer az ököl és a bunkó közül.");
    var armedZombie = new ConfiguredEnemy(new(1, 1), zombie, selectedWeaponId: "W005");
    var unarmedZombie = new ConfiguredEnemy(new(1, 1), zombie, selectedWeaponId: "WN003");
    var minotaurCorpseMaze = new Maze(7, 7);
    var minotaurForLoot = new ConfiguredEnemy(new(3, 3), data.GetEnemy("E014"));
    minotaurCorpseMaze.Carve(minotaurForLoot.Position);
    minotaurCorpseMaze.AddEnemy(minotaurForLoot);
    minotaurCorpseMaze.ReplaceEnemyWithCorpse(minotaurForLoot);
    var minotaurCorpse = minotaurCorpseMaze.Corpses.OfType<MonsterCorpse>().Single();
    var lootService = new LootAndInventoryService(data, new Random(1));
    Assert(data.LootRules.CarriedWeaponChancePercent == 30 &&
           armedZombie.CarriedWeaponIds.SequenceEqual(["W005"]) &&
           unarmedZombie.CarriedWeaponIds.Count == 0 &&
           minotaurCorpse.CarriedWeaponIds.SequenceEqual(["W017"]) &&
           lootService.RollCarriedWeapon(minotaurCorpse.CarriedWeaponIds, 100)?.Id == "W017" &&
           lootService.RollCarriedWeapon(["WN009"], 100) is null,
        "A humanoid saját fegyverének emelt zsákmányesélye vagy a természetes fegyver kizárása hibás.");
    var minotaur = data.GetEnemy("E014");
    var minotaurWeapons = minotaur.Weapons ?? [];
    Assert(!minotaur.ChoosesWeapon && (minotaur.WeaponIds ?? []).SequenceEqual(["W017", "WN009"]) &&
           minotaurWeapons.Any(weapon => weapon.IsMonsterOnly) &&
           minotaurWeapons.Any(weapon => !weapon.IsMonsterOnly),
        "A minotaurusz vegyes nagybalta–szarvöklelés listája hibás.");
    var dragon = data.GetEnemy("E021");
    var dragonWeaponIds = dragon.WeaponIds ?? [];
    var dragonWeapons = dragon.Weapons ?? [];
    Assert(!dragon.ChoosesWeapon && dragonWeaponIds.SequenceEqual(["WN004", "WN005", "WN006"]) &&
           dragonWeapons.Select(weapon => weapon.Name).SequenceEqual(["sárkányfogak", "farokcsapás", "tüzes lehelet"]),
        "A sárkány természetes támadáslistája hibás.");
    var dragonEnemy = new ConfiguredEnemy(new(1, 1), dragon, new Random(11));
    var dragonSystem = CreateBattleSystem(11);
    var defender = CreateCharacter("Sárkánycél", 10000);
    var usedWeapons = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    for (var attack = 0; attack < 100 && usedWeapons.Count < 3; attack++)
    {
        var entry = dragonSystem.ResolveTeamEnemyAction(dragonEnemy, defender,
            dragonSystem.PrepareTeamCharacter(defender).Runtime);
        foreach (var weapon in dragonWeapons)
            if (entry.Details?.Calculation.Any(line => line.Contains($"Fegyver: {weapon.Name}",
                    StringComparison.OrdinalIgnoreCase)) == true)
                usedWeapons.Add(weapon.Id);
    }
    Assert(usedWeapons.SetEquals(dragonWeaponIds),
        "A nem választó sárkány nem használta véletlenszerűen mindhárom támadását.");
    var lich = data.GetEnemy("E022");
    var lichEnemy = new ConfiguredEnemy(new(1, 1), lich);
    var lichStaff = data.GetWeapon("W021");
    Assert((lich.WeaponIds ?? []).SequenceEqual(["W021"]) &&
           dragonSystem.SelectEnemyAttackWeapon(lichEnemy)?.Id == lichStaff.Id &&
           lichStaff is { DamageType: DamageType.Necrotic, CanAttackFromRear: true, IsMonsterOnly: true } &&
           !lichStaff.CanBeEquippedBy(CharacterClassIds.Mágus, 13) &&
           SpellcastingRules.IsRestrictedFromTradingAndGeneration(lichStaff),
        "A lich varázsbotja nem szörnykizárólagos nekrotikus távolsági fegyverként működik.");
    var breath = data.GetWeapon("WN006");
    var chaosBreath = data.GetWeapon("WN017");
    var targetSystem = CreateBattleSystem(21);
    var breathTargets = Enumerable.Range(0, 4).Select(index => CreateCharacter($"Leheletcél {index}", 100)).ToArray();
    var targetPositions = new[] { new Position(3, 2), new Position(4, 3), new Position(3, 4), new Position(2, 3) };
    var targetPreparations = breathTargets.Select(targetSystem.PrepareTeamCharacter).ToArray();
    var breathEnemy = new ConfiguredEnemy(new(3, 3), data.GetEnemy("E050"));
    var breathBattle = new TeamBattleEncounter(new(3, 3), breathTargets.Select((character, index) =>
            new TeamCharacterParticipant(character, targetPositions[index], TacticalParticipantKind.PartyMember,
                targetPreparations[index].Initiative, 3, 1, targetPreparations[index].Runtime)),
        [new TeamEnemyParticipant(breathEnemy, 5, 2, 1)], breathTargets[0].Id, breathEnemy.Id);
    Assert(TacticalTeamBattleCoordinator.EnemyAttackTargets(breathBattle, breathEnemy, breath,
               character => targetPositions[Array.IndexOf(breathTargets, character)]).Count == 3 &&
           TacticalTeamBattleCoordinator.EnemyAttackTargets(breathBattle, breathEnemy, chaosBreath,
               character => targetPositions[Array.IndexOf(breathTargets, character)]).Count == 4,
        "A leheletek CSV szerinti többcélú támadása nem érvényesül.");
    Assert(data.GetWeapon("W009-PLUS1").MaximumTargets == 2 &&
        data.GetWeapon("W011-PLUS1").CanAttackFromRear && data.GetArmor("A003-PLUS1").Resistances == data.GetArmor("A003").Resistances,
        "A mágikus változat elvesztette a tulajdonságokat.");
    var (battle, front, rear, enemy) = CreateFormationEncounter();
    rear.EquipWeapon(0, data.GetWeapon("W011") with { Id = "CUSTOM-POLEARM", BaseWeaponId = null });
    battle.Engage(front, enemy);
    Assert(battle.RearFormationEnemiesInReach(rear).Count == 1, "Új azonosítóval nem működik a hátsó sor.");
    rear.EquipWeapon(0, data.GetWeapon("W001")); rear.EquipWeapon(2, data.GetWeapon("W011"));
    Assert(battle.RearFormationEnemiesInReach(rear).Count == 0, "A tartalék szálfegyver hátsó soros támadást adott.");
    Assert(SpellcastingRules.IsRestrictedFromTradingAndGeneration(data.GetWeapon("WN001")) &&
        !data.GetWeapon("WN001").CanBeEquippedBy(CharacterClassIds.Harcos, 13) &&
        SpellcastingRules.IsRestrictedFromTradingAndGeneration(data.GetWeapon("W019")) &&
        SpellcastingRules.IsRestrictedFromTradingAndGeneration(data.GetWeapon("W020")) &&
        !data.GetWeapon("W019").CanBeEquippedBy(CharacterClassIds.Harcos, 13) &&
        WeaponFamilies.Find(WeaponFamilies.Staff) is not null,
        "A természetes vagy nulla árú szörnyfegyver felszerelhető/árulható, vagy hiányzik a botcsalád.");
    var legendaryArmors = data.Armors.Where(armor => armor.Rarity == ItemRarity.Legendary).ToArray();
    int StrongDistinctBaseTypes(Func<DamageResistance, int> resistance) => legendaryArmors
        .Where(armor => armor.Resistances is { } values && resistance(values) >= 4)
        .Select(armor => armor.BaseArmorId).Where(id => id is not null)
        .Distinct(StringComparer.OrdinalIgnoreCase).Count();
    Assert(StrongDistinctBaseTypes(values => values.Fire) >= 2 &&
           StrongDistinctBaseTypes(values => values.Acid) >= 2 &&
           StrongDistinctBaseTypes(values => values.Necrotic) >= 2 &&
           StrongDistinctBaseTypes(values => values.Chaos) >= 2,
        "Nincs mind a négy új sebzéstípushoz két erős, eltérő alaptípusú legendás páncél.");
}
