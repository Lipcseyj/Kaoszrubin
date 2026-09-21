internal static partial class Program
{
    public static int Main(string[] args)
    {
        #region test cases
        var tests = new (string Name, Action Run)[]
        {
    ("A questláda CSV-je célzott objective-ot és ellenőrzött tartalmat ad", QuestChestTests.CsvResolvesChestAndObjective),
    ("Csak az első ládanyitás ad progresst, a kipakolás nem", QuestChestTests.FirstOpeningCountsButEmptyingDoesNot),
    ("A teli hátizsák mellett a zsákmány a questládában marad", QuestChestTests.FullBackpackKeepsContentInChest),
    ("A részlegesen kiürített questláda mentése és replikációja veszteségmentes", QuestChestTests.PartialContentSurvivesSaveAndReplication),
    ("A questláda megadott szobába és egyszer kerül", QuestChestTests.PlacementUsesNamedRoomAndRejectsDuplicates),
    ("A questajtó a pontos futást követi és megőrzi a megszerzett hozzáférést", QuestDoorTests.AccessFollowsExactQuestAndRemainsGranted),
    ("A tiltott questajtó-próba nem fogyaszt erőforrást", QuestDoorTests.DeniedInteractionSpendsNothing),
    ("A questajtó mentése és hálózati deltája megőrzi a feloldást", QuestDoorTests.SaveAndWorldDeltaPreserveGate),
    ("A jelvényes szoba egyetlen lezárt questajtót kap", QuestDoorTests.GeneratedRoomHasOneSealedQuestDoor),
    ("Roderic és a lezárható mellékszobák 80 seeddel is elérhetők", RodericRoomPlacementTests.PlacementSurvivesMultipleSeeds),
    ("A szobagenerálás reprodukálható és elutasítja a hibás konfigurációt", RodericRoomPlacementTests.SeedAndConfigurationAreValidated),
    ("Mind a 41 quest és 21 NPC típusos importja megőrzi a CSV-adatokat", QuestCatalogImportTests.AllDefinitionsPreserveCsvData),
    ("A hibás questdefiníció már CSV-betöltéskor meghiúsul", QuestCatalogImportTests.InvalidDefinitionsFailDuringLoading),
    ("A világpillanatkép típusos questállapotot és stabil kulcsot visz át", QuestReplicationTests.WorldUsesTypedStatesAndStableKeys),
    ("A questdelta csak a ténylegesen módosult NPC-t tartalmazza", QuestReplicationTests.DeltaChangesOnlyTheAffectedNpc),
    ("A questprojekció megtartja a láthatóságot és a követő aktuális pozícióját", QuestReplicationTests.VisibilityAndFollowerPositionRemainAuthoritative),
    ("A questreplikáció és resync nem ismétli a jutalmat vagy értesítést", QuestReplicationTests.ReplicationAndReconnectNeverReplayCompletion),
    ("A vendég questértesítése példányonként egyszer jelenik meg", QuestReplicationTests.NotificationsDistinguishInstancesAndSkipHistoricalBaseline),
    ("A questmegjelenítés változatlan típusos adatot kap", QuestPresentationTests.PresentationIsAnImmutableTypedSnapshot),
    ("Az elhalasztott questleadás nem fogyaszt és nem jutalmaz", QuestPresentationTests.DeferredTurnInNeverConsumesOrRewards),
    ("A leadási megerősítés után friss készletellenőrzés történik", QuestPresentationTests.ConfirmationDoesNotFreezeInventoryEligibility),
    ("Elira kijárati döntése tényleges questlezárást igényel", QuestPresentationTests.EliraDepartureRequiresActualCompletion),
    ("A questnapló külön sorokat és célzott feladást használ", QuestJournalTests.SeparateRowsAndTargetedAbandon),
    ("A gyorsutazás konkrét questadót és friss útvonalat használ", QuestJournalTests.TravelTargetsOneInstanceAndRechecksWorld),
    ("A gyorsutazás újraellenőrzi a collect készletét", QuestJournalTests.TravelRechecksCollectInventory),
    ("A globális quest utazása is konkrét karakterhez kötött", QuestJournalTests.GlobalTravelRetainsConcreteGiver),
    ("A külön questtörténetek NPC nélkül is körbefordulnak a mentésben", QuestJournalTests.SeparateHistorySurvivesSaveWithoutNpc),
    ("A questkulcs túléli a teljes és delta snapshotot és az újracsatlakozást", QuestJournalTests.JournalKeysSurviveFullDeltaAndReconnect),
    ("A questállapot mentési köre nem játszik vissza játékmeneti műveleteket", QuestPersistenceTests.RuntimeRoundTripDoesNotReplayGameplay),
    ("A hibás questállapot-import nem módosít részleges állapotot", QuestPersistenceTests.InvalidRuntimeImportIsAtomic),
    ("A collect betöltése a visszaállított inventoryt használja", QuestPersistenceTests.CollectRestoreUsesLoadedInventory),
    ("A questadó azonossága megmarad új világobjektum és registry-import esetén", QuestPersistenceTests.RegistryRetainsIdentityAcrossWorldObjects),
    ("A régi questmentés külön NPC-állapotokkal és archívummal fordul körbe", QuestPersistenceTests.LegacySaveRoundTripsSeparateNpcStatesAndArchive),
    ("A régi terminális questütközések nem aktiválnak újra", QuestPersistenceTests.LegacyTerminalConflictsNeverReactivate),
    ("A hibás típusos mentés nem esik vissza legacy állapotokra", QuestPersistenceTests.TypedSaveRejectsCorruptionWithoutLegacyFallback),
    ("A felfüggesztett világ megtartja a friss questállapotot és követőazonosságot", QuestPersistenceTests.SuspendedWorldRetainsCurrentQuestStateAndFollowerIdentity),
    ("A mentett NPC karakterazonosítója túléli a roster átrendeződését", QuestPersistenceTests.PersistedCharacterIdSurvivesRosterReordering),
    ("A questmentés verziómigrációja a felfüggesztett kampányt is kezeli", QuestPersistenceTests.VersionMigrationIncludesSuspendedCampaign),
    ("A visszaállított világ kizárólag típusos questprogresst tükröz", QuestPersistenceTests.WorldRestoreUsesOnlyTypedProgress),
    ("A régi NPC rekordja elsőbbséget élvez a példányhoz nem kötött naplóval szemben", QuestPersistenceTests.LegacyNpcRecordTakesPrecedenceOverUnboundJournal),
    ("A halottűzés az első körtől karakterenként tíz kör után újul meg", TurnUndeadRefreshesAfterTenRounds),
    ("A halottűzés mindkét kasztnál két mezőre hat alakzat nélkül is", TurnUndeadHasTwoCellRange),
    ("A buff és gyógyítás hangját a varázslótól és célponttól eltérő játékos is hallja", DefensiveSpellSoundIsShared),
    ("A becsapódások CSV-színe, ideje, alapértéke és validációja működik", SpellImpactTests.CsvSettings),
    ("A becsapódások területe, tölcsére, lánca és színhulláma pontos", SpellImpactTests.FootprintsAndAnimation),
    ("A támadó becsapódás a sebzés előtt az összes lánccélpontot megkapja", SpellImpactTests.ImpactPrecedesDamage),
    ("A terminál méretőre pontosan a teljes játékképernyőt követeli meg", TerminalViewportRequiresCompleteGameScreen),
    ("A főmenü rubintüze teljes szélességben terjed és korlátos marad", RubyFireSpreadsAcrossMenuWidth),
    ("A képernyő alulról felfelé ég el", ScreenBurnRisesAndConsumesTheWholeScreen),
    ("A főmenü rubinja félpercenként egy gyors magentahullámmal pulzál", RubyPulseUsesSlowSineWave),
    ("A főmenü sárkányszemének fénye 45 másodpercenként felizzik", DragonEyesFlashEveryFortyFiveSeconds),
    ("A lénymondatok osztályhoz vagy szörnyhöz és főmenüportréhoz oldódnak", CreatureQuotesLoadAndResolveForMainMenu),
    ("A Windows Terminal újraindítás debuggerben és gyermekfolyamatban kimarad", WindowsTerminalRelaunchGuardsAreStable),
    ("A Windows Terminal gyermek-kézfogás argumentuma szigorúan validált", WindowsTerminalHandshakeArgumentIsValidated),
    ("A hiányzó háttérzene callbackje kontextusonként egyszer jelez", BackgroundMusicMissingTrackReportingIsBounded),
    ("A többsoros fogadói pletyka minden sora a kereten belül marad", MultilineInnRumorStaysInsideFrame),
    ("A fejlesztői fegyvercsomag követi a kategóriákat és a hátizsák kapacitását", DevelopmentWeaponsRespectCapacity),
    ("A harci tesztpálya a kért csoportokat, jelölőládákat és középső folyosót építi", DeveloperBattleTestScenarioHasRequestedLayout),
    ("A harci teszt-NPC-k pontos szinttel és véletlenül memorizált elérhető varázslatokkal készülnek", CombatTestCharactersMatchRequestedLevelAndSpells),
    ("A mentésből indított tesztcsata önállóan helyreállítja a csatalogot", LoadedDeveloperBattleCreatesRecoveryLog),
    ("A lovag harci fegyvercsere-parancsa átjut a session ellenőrzésén", KnightBattleWeaponSwapCommandIsAccepted),
    ("A széles csapás csak kölcsönösen szomszédos célpontokat ér", WeaponSweepRequiresMutualAdjacency),
    ("A taktikai fegyverjártasságok módosítják a söprést, fedezetet és varázslást", TacticalWeaponMasteriesHaveDistinctRoles),
    ("A pajzstier közösen vezérli a kritikus blokkot és a pajzslökést", ShieldTierDrivesBlockAndBash),
    ("A kritikus pajzsblokk és a lovagi közbelépés minden harci naplóban látszik", DefensiveInterventionsReachBattleLogs),
    ("A pajzs CSV-validációja elutasítja a hibás tiert és besorolást", ShieldCsvValidationRejectsInvalidDefinitions),
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
    ("A vendég revízióhelyesen nyugtázhatja a közös ablakot", RemotePlayerCanAcknowledgeSharedWindow),
    ("A vendég elküldheti a saját memorizált varázslatait", RemotePlayerCanPrepareSpells),
    ("A vendég válaszolhat a saját szintlépési promptjára", RemotePlayerCanResolveLevelUpPrompt),
    ("A vendég értesítést kap a más által kezelt blokkoló ablakokról", GuestSeesOtherPlayersBlockingWindows),
    ("Az oldalsó inventory nem állítja meg a coop játékot", SideInventoryDoesNotPauseCoop),
    ("A vendég nem adhat leader-parancsot", RemotePlayerCannotIssueLeaderAction),
    ("A host és a vendég közös billentyűkiosztást használ", HostAndGuestUseSharedInputBindings),
    ("A faji tulajdonságokat az adatfájl tölti be", RaceTraitsAreLoadedFromData),
    ("A mágus első szintjén a Fényvarázslat a hatodik varázslat", SpellSchoolsIncludeMageLightSpell),
    ("A buff varázslatok időtartama CSV-ből, harci körökben érkezik", SpellBuffDurationLoadsAsRounds),
    ("Az öt új varázslat hatásai és célpontszabályai működnek", NewSpellEffectsAreSupported),
    ("A varázsmemória osztályonként eltérően fejlődik", SpellMemorizationCapacityUsesClassFormula),
    ("A kasztok CSV-ből módosítják a HP- és mannanövekedést", ClassResourceGrowthLoadsFromCsv),
    ("Az NPC-k és első küldetéseik CSV-ből töltődnek", NpcDefinitionsLoadFromCsv),
    ("Az NPC-életciklus működik saját questállapot nélkül", NpcLifecycleHasNoQuestState),
    ("A típusos quest aktiválását a történeti kapu szabályozza", QuestManagerTests.ActivationHonorsStoryGate),
    ("A típusos quest tömeges felvétele megőrzi a zárolt és lezárt állapotot", QuestManagerTests.BulkActivationLeavesLockedAndResolvedQuestsAlone),
    ("A típusos quest csak megfelelő aktív eseményre halad és nem lépi túl a célt", QuestManagerTests.ProgressOnlyUsesMatchingActiveObjectives),
    ("A típusos collect quest a készlet növekedését és visszaesését is jelzi", QuestManagerTests.CollectTracksInventoryInBothDirections),
    ("A típusos quest leadása pontosan egyszer fogyaszt és jutalmaz", QuestManagerTests.CompletionGrantsRewardsOnlyOnce),
    ("A típusos quest leadáskor újraellenőrzi a megváltozott készletet", QuestManagerTests.CompletionRechecksChangedInventory),
    ("A típusos quest sikertelen tárgyelvételnél nem ad jutalmat", QuestManagerTests.FailedConsumptionDoesNotGrantRewards),
    ("A típusos quest feladása azonnal megállítja a haladást", QuestManagerTests.AbandonImmediatelyStopsProgress),
    ("A típusos quest két NPC-példányának külön életciklusa van", QuestManagerTests.NpcInstancesHaveIndependentLifecycles),
    ("A típusos quest globális azonossága és NPC-ellenőrzése működik", QuestManagerTests.GlobalIdentityAndNpcValidation),
    ("A típusos quest beszélgetése megőrzi az NPC-példány azonosságát", QuestManagerTests.ConversationKeepsNpcInstanceIdentity),
    ("Roderic öt küldetése sorrendben, teli hátizsákkal is teljesíthető", RodericReworkTests.FiveQuestsFollowTheStory),
    ("Roderic ellenfelei és a három questkapu pontosan konfiguráltak", RodericReworkTests.EncountersAndGatesAreExact),
    ("Roderic régi mentése jutalom és pályaépítés nélkül továbblép", RodericReworkTests.OldCampaignAdvancesWithoutRewardsOrRebuilding),
    ("A típusos follower quest célpontot, részvételt és történetállapotot ellenőriz", QuestManagerTests.FollowerKillHonorsEnemyParticipationAndStory),
    ("Roderic története csak az aktuális típusos küldetést indítja", QuestMigrationTests.RodericStoryStartsOnlyTheChosenQuest),
    ("A kijárat felfedezése nem teljesíti a kísérő küldetését", QuestMigrationTests.DiscoveryNeverCompletesEscort),
    ("Az utólag felvett explore quest az aktuális pálya felfedezését használja", QuestMigrationTests.LateExploreActivationUsesCurrentWorldDiscovery),
    ("A kísérő célba érkezése konkrét NPC-példányhoz kötött", QuestMigrationTests.EscortArrivalIsBoundToNpcInstance),
    ("A világadapter tényleges távolságot és élő kísérőt ellenőriz", QuestMigrationTests.MazeEscortChecksActualDistanceAndLife),
    ("A questnapló NPC nélkül is azonnali és egyirányú projekció", QuestMigrationTests.JournalProjectsChangesWithoutNpcOrReverseWrites),
    ("A leadás fogyasztása és jutalma frissíti a többi collect projekcióját", QuestMigrationTests.CompletionRefreshesOtherCollectProjections),
    ("A közös questfrissítés követi az inventoryt és a partitagságot", QuestMigrationTests.InventoryBoundaryTracksMutationsAndPartyMembership),
    ("Elira és Roderic ad-hoc beszélgetései egyszer használható szálakat alkotnak", AdHocFollowerConversationsAreConfigured),
    ("A quest roomok kizárják a véletlen térképtartalmat", QuestRoomsReserveTheirContent),
    ("A három Skeleton Knight példányhoz kötött jelvényt őriz", RodericInsigniaGuardiansAreConfigured),
    ("Sir Malrec önálló 5-ös küldetéshelyszínen vár", RodericMalrecQuestLocationIsConfigured),
    ("Roderic rögzített egyedi karakterlapból készül", RodericUsesDefinedCharacterBuild),
    ("A parti megjegyzései CSV-ből, teljes idézőjeles szöveggel töltődnek", PartyRemarksLoadFromCsv),
    ("A parti megjegyzéseinek esélyei és beszélőszámai követik a szabályt", PartyRemarkProbabilitiesFollowRules),
    ("A world-NPC generálás kizárja a fehér karakterszínt", WorldNpcGenerationExcludesWhiteColor),
    ("A karaktergenerátor felszerelési profiljai szinthez kötöttek és konfigurálhatók", GeneratedCharacterEquipmentProfilesAreLevelBounded),
    ("Az 5. pálya utáni zsoldos legalább egy szinttel gyengébb és fizetős", LateInnRecruitsAreLowerLevelAndStillCostGold),
    ("Az ideiglenes követő megtartja a world-NPC inverz térképszíneit", TemporaryFollowerKeepsWorldNpcMapColors),
    ("A hosszú NPC-párbeszéd az ablakon belül sortörést kap", NpcDialogueWrapsInsideRecruitmentWindow),
    ("A közös küldetésnapló elkülöníti az aktív és teljesített küldetéseket", QuestJournalBuildsSharedHistory),
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
    ("A harcban az átlós ellenfél is közelharci távolságban van", DiagonalEnemyIsMeleeAdjacent),
    ("A harc váza kezeli a belépési kört és a kezdeményezési sorrendet", TacticalBattleStateOrdersEligibleParticipants),
    ("A nyitó ütésváltást a kezdeményezés dönti el, a rajtaütést kivéve", BattleOpeningOrderUsesInitiative),
    ("Az Első csapás a nyitásban +10, a rendes sorrendben +2 kezdeményezést ad", FirstStrikeUsesSeparateOpeningInitiative),
    ("A gyorsítás és lassítás a következő kör elején rendezi át a kezdeményezést", SpellEffectsReorderInitiativeAtCycleBoundary),
    ("Az időzített állapot kezdeményezés-büntetése körhatáron rendezi át a sorrendet", StatusPenaltyReordersInitiativeAtCycleBoundary),
    ("A kezdeményezési holtverseny sorrendje körönként stabil marad", InitiativeTiesRemainStable),
    ("A zárt út mögötti ellenfél nem érkezhet meg néhány harci kör alatt", TacticalArrivalRequiresWalkableRoute),
    ("A harc az inaktivitási küszöb után áll le", BattleDetectsInactiveSide),
    ("A harc ugyanazt a támadási szabálymotort használja", BattleAttackUsesExistingCombatRules),
    ("A szörny Ereje találat után lökési vagy tántorítási próbát ad", MonsterStrengthCreatesTacticalPressure),
    ("A vendég csak élő, pozícióval rendelkező partiavatárt rajzol", GuestDrawsOnlyLivingPartyAvatars),
    ("A közelharci támadás az ellenfél haláláig leköti a karaktert", BattleEngagementLastsUntilEnemyDeath),
    ("A zárt alakzat első sora védi a mögötte álló társat", BattleFormationProtectsRearRow),
    ("A vezér külön harcra készítheti a hátsó sor két oldalát", RearCombatPreparationIsLeaderControlled),
    ("A harci AI a gyógyital erejét a megengedett HP-veszteséghez igazítja", BattleAiHealingPotionAvoidsWaste),
    ("A zárt libasor együtt mozog, de nem kap hátsósori védelmet", BattleSingleFileHasNoRearProtection),
    ("Harcban csak szabad hátsó sori karakter használhat CSV-ben engedélyezett italt", BattleItemUseRequiresFreeRearPosition),
    ("A hátsó sor szálfegyverrel eléri az első társ lekötött ellenfelét", BattleRearPolearmReachUsesFrontEngagement),
    ("A hátsó sori pap elűzheti az első sor által lekötött élőholtat", RearPriestCanTurnFrontEngagedUndead),
    ("A hátráló varázshasználó távolságcélja repülő ellenfélnél nagyobb", SpellcasterRetreatDistanceIsCapped),
    ("Az ellenfél nézésiránya oldal- és hátbatámadási bónuszt ad", TacticalAttackArcsUseEnemyFacing),
    ("A tolvaj tőrrel a zárt alakzat hátsó sorából is orvtámad", ThiefCanBackstabFromRearFormation),
    ("A Hátra! helycsere átadja az első sori lekötéseket", BattleSwapToRearTransfersEngagements),
    ("Az alakzat csak a fennálló lekötéseket megtartva mozdulhat", BattleFormationMovementPreservesEngagements),
    ("A harc célpontja akcióvesztés nélkül váltható", BattleTargetCanBeChanged),
    ("Az NPC varázslási szabálya tartalékolja a mannát és csak egycélú támadást választ", NpcSpellcastingPolicyPreservesMana),
    ("Az Átoktörés csak ténylegesen tisztítható csapattársra használható", BreakCurseRequiresUsefulPartyTarget),
    ("A Megtisztítás nem használható egyszerű gyógyításként", CleansingHealRequiresRemovableStatus),
    ("A lekötött pap és lovag ritkítja a rutinvarázslást, de a sürgős segítséget nem", EngagedSupportSpellcastingIsThrottled),
    ("Az NPC támadóvarázslási összerőhatárai inkluzívak", NpcOffensiveSpellStrengthThresholdsAreInclusive),
    ("Az önbuff és közelharc profil haszon, mannaköltség és véletlen alapján buffol", NpcSelfBuffUsesValueManaAndChance),
    ("Az NPC varázspontozása csoport ellen területi, gyenge célra takarékos támadást kedvel", NpcSpellUtilityValuesTargetsAndOverkill),
    ("Az NPC varázsmemóriája váltogatja a repertoárt, de nem ír felül nagy erőkülönbséget", NpcSpellMemoryBalancesVarietyAndUtility),
    ("Az NPC varázsterv hiszterézise megtartja a közeli tervet és elengedi az összeomlottat", NpcSpellPlanRetentionIsStable),
    ("Az NPC varázsterv érvényét veszti halott célnál, elfogyott manánál és sikertelen tervnél", NpcSpellPlanInvalidationCoversFailureModes),
    ("Csak a szabad, alakzaton kívüli nem-lovag mozoghat varázslási pozícióba", NpcSpellPlanMovementHonorsClassAndBattleState),
    ("Az NPC tüzelőállás-pontozása körökkel és közelharci veszéllyel számol", NpcSpellPositionPenaltyIncludesTravelAndDanger),
    ("A veszélyben tüzelőállást kereső mágus a teljes mozgást részesíti előnyben", NpcCasterPrefersFullSafeCastingMove),
    ("A harc varázsmemóriája sorrendben őrzi a megkísérelt terveket", BattleStoresNpcSpellMemory),
    ("A szabad és lekötött varázslás eltérően módosítja a harci hibakockázatot", EngagementAdjustsSpellFailureChance),
    ("A harcba hívott erősítés a következő körben lép be", BattleReinforcementJoinsNextCycle),
    ("A coop session validálja a harci mozgást, tárgyhasználatot és passzt", BattleCommandsAreValidated),
    ("A fenyegetésbecslés felismeri az elszigetelt gyenge ellenfelet", EncounterThreatAssessmentRecognizesSafeFight),
    ("A gyorsharc legfeljebb három jelentéktelen ellenfelet enged át", QuickCombatAllowsUpToThreeSafeEnemies),
    ("A gyorsharc beállítása normalizálható és menthető", QuickCombatSettingPersists),
    ("A csatarészlet panel lapozható és mutatja a kritikus esélyt", BattleDetailsPanelPagesCalculation),
    ("A gyorsharc összesítője ölőnként csoportosítja az ellenfeleket és az XP-t", QuickCombatSummaryListsKillsAndExperience),
    ("A taktikai és gyorsharc összesítője kiírja a HP- és mannafogyást", BattleSummaryListsResourceUse),
    ("A felszerelés súlya leterheltséget és mozgási hátrányt okoz", EquipmentWeightAffectsMobility),
    ("A karakterlap és a tárgyvizsgálat előre jelzi a harci terhelést", MobilityPreviewIsVisible),
    ("A harcos taktikai találati esélyei a valódi képletet követik", FighterTacticHitChancesUseCombatFormula),
    ("A győzelmi üzenet nem ismétli meg az utolsó támadást", VictoryMessageIsConcise),
    ("A győzelmi összegzés egyetlen kompakt sor", VictorySummaryIsCompact),
    ("Csak az aktív BattleId és TurnId parancsa fogadható el", BattleCommandRequiresCurrentPrompt),
    ("A függő harci parancs kiszűri a gyors ismételt bemenetet", BattleCommandGateIgnoresBufferedInput),
    ("Az azonos harci prompt idempotens, a tartalmi változás újrapublikál", BattlePromptIsIdempotent),
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
    ("A széles pályatípus hárommezős folyosókat és külön konfigurációt használ", WideMazeUsesThreeCellCorridors),
    ("A kijelölt széles szintek több területre elég változatos hordát konfigurálnak", WideLevelsHaveBalancedDiverseHordes),
    ("A képernyőátjáró a mentésben és a világmodellben is megmarad", MazePassageSurvivesSaveRoundTrip),
    ("A mentés visszaállítja a szörny alatt fekvő csapdát", SavedTrapCanShareEnemyPosition),
    ("A tárgyátok esélye pályánként konfigurálható", CursedLootChanceIsConfiguredPerMazeLevel),
    ("A karakter kasztja, faja és átmeneti hatásai módosítják a látótávot", CharacterVisionRangeUsesClassRaceAndEffects),
    ("A szörnyek látótávja CSV-ből érkezik", EnemyVisionRangesLoadFromCsv),
    ("A felfedés változó látótávot és látóvonalat használ", FogRevealUsesVariableRangeAndLineOfSight),
    ("A szörnyek ébersége, felderítése és alvásképessége adatvezérelt", EnemyAwarenessAndSearchAreDataDriven),
    ("A falka közös keresési pont körül felderítőkre és biztosítókra oszlik", EnemyPackSearchStaysCoordinated),
    ("A felderítő a még be nem járt folyosóágakat választja és együtt marad", EnemySearchExploresCorridorFrontiers),
    ("Csak az explicit Horde találkozás válik vezérrel mozgó hordává", CorridorGroupsBecomeLedHordes),
    ("A horda vándorlási és táborozási állapota menthető és üldözéskor megszakad", HordeRoamingStatePersistsAndYieldsToPursuit),
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
    ("A felszerelészsákmány véletlen, de nem törött állapotban érkezik", EquipmentLootStartsWithRandomWear),
    ("A kereskedő a kopott felszerelésért tartósságarányosan kevesebbet fizet", WornEquipmentSellsForLess),
    ("A tárgybővítések és kaszttehetségek javítják a tartósságot", UpgradesAndClassPerksImproveDurability),
    ("A megfelelő tárgy a megfelelő hatásra kopik", EquipmentWearCauseMatchesEquipmentType),
    ("A legendás felszerelések egyedi tartósságúak és a javítókészlet csak terepi szintig javít", LegendaryDurabilityAndFieldRepairKit),
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
    ("A tesztfuttató argumentumai szigorúan validáltak", TestRunnerOptionsAreValidated),
    ("A tesztfuttató névszűrése kis- és nagybetűtől független", TestRunnerFilterIsCaseInsensitive),
    ("A SignalR LAN host elindítható és leállítható", () =>
        SignalRServerStartsAndStops().GetAwaiter().GetResult()),
    ("A SignalR kliens végigviszi a LAN coop kapcsolatot", () =>
        SignalRClientRunsLanProtocolFlow().GetAwaiter().GetResult()),
    ("Az in-memory transport végigviszi a coop protokollfolyamot", () =>
        InMemoryTransportRunsProtocolFlow().GetAwaiter().GetResult())
        };
        #endregion

        // ================================ Main method ================================
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        if (CoopHarnessOptions.TryParse(args, out var harnessOptions))
        {
            CoopSimulationHarness.Run(harnessOptions);
            return Environment.ExitCode;
        }

        if (!TryParseTestRunnerOptions(args, out var filter, out var justFail, out var showHelp))
        {
            PrintUsage();
            return 2;
        }

        if (showHelp)
        {
            PrintUsage();
            return 0;
        }
        var passed = 0;
        var failures = 0;
        double totalElapsed = 0;
        foreach (var test in tests)
        {
            if (!MatchesTestFilter(test.Name, test.Run, filter))
            {
                continue;
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                test.Run();
                stopwatch.Stop();
                if (!justFail)
                {
                    Console.WriteLine($"PASS  {stopwatch.Elapsed.TotalMilliseconds,9:F1} ms  {test.Name}");
                }
                passed++;
                totalElapsed += stopwatch.Elapsed.TotalMilliseconds;
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                failures++;
                totalElapsed += stopwatch.Elapsed.TotalMilliseconds;
                Console.WriteLine($"FAIL  {stopwatch.Elapsed.TotalMilliseconds,9:F1} ms  {test.Name}: {exception.Message}");
            }
        }

        Console.WriteLine($"Passed: {passed}, Failed: {failures} TotalElapsed: {totalElapsed,9:F1} ms");

        return failures == 0 ? 0 : 1;

        // ============================== END Main method ===============================
    }

    static bool TryParseTestRunnerOptions(
        string[] args,
        out string? filter,
        out bool justFail,
        out bool showHelp)
    {
        filter = null;
        justFail = false;
        showHelp = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--filter":
                    if (i + 1 >= args.Length || args[i + 1].StartsWith("-", StringComparison.Ordinal))
                    {
                        return false;
                    }

                    filter = args[++i];
                    break;
                case "--only-failed":
                    justFail = true;
                    break;
                case "-h":
                    showHelp = true;
                    break;
                default:
                    return false;
            }
        }

        return true;
    }

    static void PrintUsage()
    {
        Console.WriteLine("Usage: Tests [-h] [--filter <filterstring>] [--only-failed]");
        Console.WriteLine("       Tests --coop-sim [--scenario <name>] [--port <port>] [--workspace <path>] [--settings <path>]");
        Console.WriteLine("       Tests --coop-role <host|guest> [--scenario <name>] [--port <port>] [--workspace <path>] [--settings <path>]");
        Console.WriteLine("  -h                         Show this usage information.");
        Console.WriteLine("  --filter <filterstring>    Run tests whose display or method name contains the filter.");
        Console.WriteLine("  --only-failed              Write failed result lines only.");
        Console.WriteLine("  --coop-sim                 Run the coop simulation harness.");
        Console.WriteLine("  --coop-role <host|guest>   Run one coop harness role.");
        Console.WriteLine("  --scenario <name>          Select an optional coop scenario.");
        Console.WriteLine($"  --port <port>               Set the coop port (default: {CoopHarnessOptions.DefaultPort}).");
        Console.WriteLine("  --workspace <path>         Set the coop harness workspace.");
        Console.WriteLine("  --settings <path>          Load game settings for both coop roles from this JSON file.");
    }

    static bool MatchesTestFilter(string testName, Action run, string? filter) =>
        filter is null ||
        testName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
        run.Method.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);

    static void TestRunnerOptionsAreValidated()
    {
        Assert(TryParseTestRunnerOptions(
                   ["--only-failed", "--filter", "inventory"],
                   out var filter,
                   out var justFail,
                   out var showHelp) &&
               filter == "inventory" && justFail && !showHelp,
            "Az összetett tesztfuttató argumentumok feldolgozása hibás.");
        Assert(TryParseTestRunnerOptions(["-h"], out _, out _, out showHelp) && showHelp,
            "A tesztfuttató súgókapcsolója nem működik.");
        Assert(!TryParseTestRunnerOptions(["--filter"], out _, out _, out _) &&
               !TryParseTestRunnerOptions(["--unknown"], out _, out _, out _),
            "A tesztfuttató elfogadott egy hiányos vagy ismeretlen argumentumot.");
        Assert(CoopHarnessOptions.TryParse(
                   ["--coop-sim", "--scenario", "join", "--settings", "C:\\teszt\\beallitasok.json"],
                   out var coopOptions) &&
               coopOptions.Mode == CoopHarnessMode.Simulation && coopOptions.Scenario == "join" &&
               coopOptions.SettingsPath == "C:\\teszt\\beallitasok.json",
            "A coop harness nem dolgozta fel a külön beállításfájlt.");
    }

    static void TestRunnerFilterIsCaseInsensitive()
    {
        Assert(MatchesTestFilter("Inventory snapshot", TestRunnerFilterIsCaseInsensitive, "INVENTORY") &&
               MatchesTestFilter("Unrelated display name", TestRunnerFilterIsCaseInsensitive, "RUNNERFILTER") &&
               MatchesTestFilter("Inventory snapshot", TestRunnerFilterIsCaseInsensitive, null) &&
               !MatchesTestFilter("Inventory snapshot", TestRunnerFilterIsCaseInsensitive, "combat"),
            "A tesztfuttató név- és metódusszűrése nem case-insensitive részszövegkeresést használ.");
    }
}
