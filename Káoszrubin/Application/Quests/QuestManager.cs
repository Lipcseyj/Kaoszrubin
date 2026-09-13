using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Quests;

namespace KaoszRubin.Application.Quests;

/// <summary>
/// API: YES
/// A quest-rendszer központi publikus belépési pontja.
///
/// A játék többi része lehetőség szerint kizárólag ezen az osztályon,
/// illetve az innen elérhető <see cref="QuestHandle"/> és
/// <see cref="QuestNpcHandle"/> objektumokon keresztül dolgozik questekkel.
///
/// A manager façade-ként fogja össze a questek lekérdezését, aktiválását,
/// progressz-eseményeit, leadását, elhagyását és az NPC-specifikus API-kat.
/// A belső quest service-ek közvetlen használatát gameplay kódból kerülni kell.
/// </summary>
public sealed class QuestManager
{
    private readonly QuestCatalog _catalog;
    private readonly QuestStateStore _stateStore;
    private readonly QuestAvailabilityService _availability;
    private readonly QuestProgressEngine _progressEngine;
    private readonly QuestCompletionProcessor _completionProcessor;
    private readonly IQuestNpcConversationService _conversationService;

    /// <summary>
    /// API: YES
    /// Értesítés arról, hogy egy quest futásidejű állapota vagy progressze megváltozott.
    ///
    /// Elsősorban journal-, UI- és egyéb csak olvasható projekciók szinkronizálására
    /// szolgál. A feliratkozó a kapott handle-t olvassa; lehetőség szerint ne indítson
    /// az eseménykezelőből újabb quest-műveletet.
    /// </summary>
    public event Action<QuestHandle>? QuestChanged;

    /// <summary>
    /// API: YES
    /// Létrehozza a quest-rendszer publikus façade-ját a szükséges belső
    /// szolgáltatásokkal.
    ///
    /// A példányosítás helye a composition root, tipikusan a <c>Game</c>
    /// inicializálása. Gameplay közben a hívók már ezt a manager példányt használják.
    /// </summary>
    /// <param name="catalog">A typed quest-definíciók katalógusa.</param>
    /// <param name="stateStore">A questek központi futásidejű state-tárolója.</param>
    /// <param name="availability">A questek elérhetőségét és aktiválását kezelő service.</param>
    /// <param name="progressEngine">A világ eseményeit quest-progresszé alakító motor.</param>
    /// <param name="completionProcessor">A questek leadását és lezárását végző komponens.</param>
    /// <param name="conversationService">A questadó NPC-beszélgetést a játék felé továbbító adapter.</param>
    public QuestManager(
        QuestCatalog catalog,
        QuestStateStore stateStore,
        QuestAvailabilityService availability,
        QuestProgressEngine progressEngine,
        QuestCompletionProcessor completionProcessor,
        IQuestNpcConversationService conversationService)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(stateStore);
        ArgumentNullException.ThrowIfNull(availability);
        ArgumentNullException.ThrowIfNull(progressEngine);
        ArgumentNullException.ThrowIfNull(completionProcessor);
        ArgumentNullException.ThrowIfNull(conversationService);

        _catalog = catalog;
        _stateStore = stateStore;
        _availability = availability;
        _progressEngine = progressEngine;
        _completionProcessor = completionProcessor;
        _conversationService = conversationService;
        _progressEngine.ProgressChanged += change => QuestChanged?.Invoke(
            CreateHandle(change.QuestId, change.GiverInstanceId));

        Roderic = new RodericNpcApi(this);

        Elira = new EliraNpcApi(this);
    }


    // ------------------------------------------------------------
    // Egyedi Npc támogatás
    // ------------------------------------------------------------

    /// <summary>
    /// API: YES
    /// Sir Roderic név szerinti, erősen típusos quest- és NPC API-ja.
    ///
    /// Story kódban ezt érdemes használni általános enum-alapú lookup helyett,
    /// például: <c>_questManager.Roderic.Quests.OathbreakerKnight</c>.
    /// </summary>
    public RodericNpcApi Roderic { get; }

    /// <summary>
    /// API: YES
    /// Elira Ezüstág név szerinti, erősen típusos quest- és NPC API-ja.
    /// Story kódban ezt érdemes használni általános enum-alapú lookup helyett.
    /// </summary>
    public EliraNpcApi Elira { get; }

    /// <summary>A teljes hiteles állapot másolata, a lezárt és már nem jelen lévő NPC-k futásaival együtt.</summary>
    public IReadOnlyList<QuestStateSnapshot> ExportState() => _stateStore.Export();

    /// <summary>
    /// Visszaállítja a validált állapotot, majd a betöltött világ és inventory alapján egyeztet.
    /// Nem aktivál vagy jutalmaz; csak az egyeztetett állapotból küld projekciós értesítést.
    /// </summary>
    public void RestoreState(IEnumerable<QuestStateSnapshot> snapshots)
    {
        _stateStore.Restore(snapshots);
        _availability.RefreshRestoredStates();
        _progressEngine.SynchronizeCollectObjectives();
        PublishState();
    }

    /// <summary>Világváltás után újraküldi a projekciókat a jelenlegi állapot visszatekerése nélkül.</summary>
    public void PublishState()
    {
        foreach (var state in _stateStore.All) QuestChanged?.Invoke(CreateHandle(state));
    }

    // ------------------------------------------------------------
    // NEM egyedi Npc támogatás
    // ------------------------------------------------------------

    /// <summary>
    /// API: YES
    /// Létrehoz egy NPC-központú quest handle-t a megadott questadóhoz.
    ///
    /// Ezen keresztül kérdezhetők le az NPC questjei, aktiválhatók az elérhető
    /// küldetések és indítható beszélgetés. Nem egyedi NPC-nél a konkrét
    /// <paramref name="instanceId"/> megadása szükséges.
    /// </summary>
    /// <param name="npcId">A questadó typed NPC-azonosítója.</param>
    /// <param name="instanceId">A konkrét runtime NPC-példány azonosítója.</param>
    /// <returns>Az adott questadóhoz kötött <see cref="QuestNpcHandle"/>.</returns>
    public QuestNpcHandle For(QuestNpcId npcId, QuestNpcInstanceId instanceId = default)
    {
        return new QuestNpcHandle(
            this,
            npcId,
            instanceId);
    }

    // ------------------------------------------------------------
    // Quest lekérdezések
    // ------------------------------------------------------------

    /// <summary>
    /// API: YES
    /// Visszaadja egy NPC jelenleg folyamatban lévő questjeit.
    ///
    /// Az <see cref="QuestState.Active"/> és
    /// <see cref="QuestState.ReadyToTurnIn"/> állapotú questeket egyaránt tartalmazza.
    /// </summary>
    /// <param name="npcId">A questadó typed NPC-azonosítója.</param>
    /// <param name="instanceId">A konkrét runtime NPC-példány azonosítója.</param>
    /// <returns>Az NPC aktív vagy leadásra kész questjei.</returns>
    public IReadOnlyList<QuestHandle> GetActiveQuestsForNpc(QuestNpcId npcId, QuestNpcInstanceId instanceId = default)
    {
        return GetQuestsForNpc(
                npcId,
                instanceId)
            .Where(quest =>
                quest.IsInProgress)
            .ToArray();
    }

    /// <summary>
    /// API: YES
    /// Lekér egy globális scope-ú questet.
    ///
    /// <see cref="QuestScope.PerNpcInstance"/> quest esetén a példányazonosítót
    /// fogadó overloadot kell használni.
    /// </summary>
    /// <param name="questId">A lekérendő typed quest-azonosító.</param>
    /// <returns>A quest aktuális definícióját és runtime állapotát reprezentáló handle.</returns>
    public QuestHandle GetQuest(QuestId questId)
    {
        return CreateHandle(
            questId,
            QuestNpcInstanceId.None);
    }

    /// <summary>
    /// API: YES
    /// Lekér egy konkrét NPC-példányhoz tartozó questet.
    /// </summary>
    /// <param name="questId">A lekérendő typed quest-azonosító.</param>
    /// <param name="giverInstanceId">A questadó konkrét runtime példányazonosítója.</param>
    /// <returns>A quest aktuális definícióját és runtime állapotát reprezentáló handle.</returns>
    public QuestHandle GetQuest(QuestId questId, QuestNpcInstanceId giverInstanceId)
    {
        return CreateHandle(
            questId,
            giverInstanceId);
    }

    /// <summary>
    /// API: YES
    /// Visszaadja a játék összes jelenleg folyamatban lévő questjét.
    ///
    /// Az <see cref="QuestState.Active"/> és
    /// <see cref="QuestState.ReadyToTurnIn"/> állapotokat egyaránt tartalmazza.
    /// </summary>
    /// <returns>Az összes folyamatban lévő quest handle-je.</returns>
    public IReadOnlyList<QuestHandle> GetActiveQuests()
    {
        return _stateStore
            .GetInProgress()
            .Select(CreateHandle)
            .ToArray();
    }

    /// <summary>
    /// API: YES
    /// Visszaadja az összes olyan questet, amelynek objective-je teljesült
    /// és jelenleg leadható.
    /// </summary>
    /// <returns>A <see cref="QuestState.ReadyToTurnIn"/> állapotú questek.</returns>
    public IReadOnlyList<QuestHandle> GetReadyToTurnInQuests()
    {
        return _stateStore
            .GetReadyToTurnIn()
            .Select(CreateHandle)
            .ToArray();
    }

    /// <summary>
    /// API: YES
    /// Visszaadja az adott questadóhoz tartozó összes questet, állapottól függetlenül.
    ///
    /// A visszaadott handle-ek a központi runtime state-et reprezentálják,
    /// nem különálló quest-másolatok.
    /// </summary>
    /// <param name="npcId">A questadó typed NPC-azonosítója.</param>
    /// <param name="instanceId">A konkrét runtime NPC-példány azonosítója.</param>
    /// <returns>Az NPC-hez definiált questek handle-jei.</returns>
    public IReadOnlyList<QuestHandle> GetQuestsForNpc(QuestNpcId npcId, QuestNpcInstanceId instanceId = default)
    {
        var definitions =
            _catalog.GetByGiver(npcId);

        return definitions
            .Select(definition =>
                CreateHandle(
                    definition.Id,
                    instanceId))
            .ToArray();
    }

    /// <summary>
    /// API: YES
    /// Visszaadja egy NPC jelenleg általánosan felajánlható questjeit.
    ///
    /// Az availability réteg végzi az állapot- és aktiválási feltételek ellenőrzését;
    /// ezeket a hívónak nem kell kézzel újraimplementálnia.
    /// </summary>
    /// <param name="npcId">A questadó typed NPC-azonosítója.</param>
    /// <param name="instanceId">A konkrét runtime NPC-példány azonosítója.</param>
    /// <returns>Az aktuálisan felajánlható questek.</returns>
    public IReadOnlyList<QuestHandle> GetAvailableQuestsForNpc(QuestNpcId npcId, QuestNpcInstanceId instanceId = default)
    {
        return _availability
            .GetAvailableForNpc(
                npcId,
                instanceId)
            .Select(CreateHandle)
            .ToArray();
    }

    /// <summary>
    /// API: YES
    /// Megvizsgálja, hogy a quest jelenlegi állapotában szabályosan leadható-e.
    ///
    /// UI-döntéshez használható; a tényleges lezárást mindig a
    /// <see cref="Complete(QuestId, QuestNpcInstanceId)"/> végezze.
    /// </summary>
    /// <param name="questId">A vizsgált quest azonosítója.</param>
    /// <param name="giverInstanceId">A questadó runtime példányazonosítója.</param>
    /// <returns><c>true</c>, ha a quest jelenleg completelhető.</returns>
    public bool CanComplete(QuestId questId, QuestNpcInstanceId giverInstanceId = default)
    {
        return _completionProcessor.CanComplete(
            questId,
            giverInstanceId);
    }

    /// <summary>
    /// API: YES
    /// Leadja és lezárja a questet, és végrehajtja a completionhez tartozó
    /// reward-kezelést.
    ///
    /// A művelet után <see cref="QuestChanged"/> értesítés történik, majd a rendszer
    /// újraszinkronizálja az aktív collect questeket, mert a leadáskor elfogyasztott
    /// vagy jutalomként kapott tárgyak más questek progresszét is befolyásolhatják.
    /// </summary>
    /// <param name="questId">A leadandó quest azonosítója.</param>
    /// <param name="giverInstanceId">A questadó runtime példányazonosítója.</param>
    /// <returns>A tényleges completion és reward eredménye.</returns>
    public QuestCompletionResult Complete(QuestId questId, QuestNpcInstanceId giverInstanceId = default)
    {
        var result = _completionProcessor.Complete(
            questId,
            giverInstanceId);
        QuestChanged?.Invoke(CreateHandle(questId, giverInstanceId));
        // A fogyasztás és a jutalom más aktív collect küldetést is érinthet.
        _progressEngine.SynchronizeCollectObjectives();
        return result;
    }

    /// <summary>
    /// API: YES
    /// Visszaadja azokat a questeket, amelyeket a completion rendszer
    /// jelenleg leadásra késznek tekint.
    ///
    /// Tipikus használat: NPC-interakciónál vagy turn-in UI megnyitásakor.
    /// </summary>
    /// <returns>A jelenleg leadható questek handle-jei.</returns>
    public IReadOnlyList<QuestHandle> GetPendingQuestCompletions()
    {
        return _completionProcessor
            .GetPendingCompletions()
            .Select(CreateHandle)
            .ToArray();
    }

    // ------------------------------------------------------------
    // Aktiválás
    // ------------------------------------------------------------

    /// <summary>
    /// API: YES
    /// Aktivál egy konkrét, jelenleg felvehető questet.
    ///
    /// Aktiválás után a rendszer szinkronizálja a releváns objective-eket.
    /// Collect quest esetén a party inventoryban már meglévő tárgyak azonnal
    /// beleszámítanak a progresszbe.
    ///
    /// Az aktiválhatósági feltételeket nem a hívó ellenőrzi, hanem az availability réteg.
    /// </summary>
    /// <param name="questId">Az aktiválandó quest azonosítója.</param>
    /// <param name="giverInstanceId">A questadó runtime példányazonosítója.</param>
    /// <returns>Az aktivált quest handle-je.</returns>
    public QuestHandle Activate(QuestId questId, QuestNpcInstanceId giverInstanceId = default)
    {
        var state = _availability.Activate(
            questId,
            giverInstanceId);
        QuestChanged?.Invoke(CreateHandle(state));
        _progressEngine.SynchronizeExplorationObjective(state);

        // Collect quest esetén azonnal vegyük figyelembe
        // a már meglévő inventory tartalmát.
        if (_catalog.Get(questId).Objective
            is QuestObjective.CollectItem)
        {
            _progressEngine
                .SynchronizeCollectObjectives();
        }

        return CreateHandle(state);
    }

    /// <summary>
    /// API: YES
    /// Aktiválja az adott NPC összes jelenleg általánosan felajánlható questjét.
    ///
    /// Az aktivált questekhez projekciós értesítés készül, az exploration objective-ek
    /// szinkronizálódnak, collect quest esetén pedig a meglévő inventory azonnal
    /// beleszámít a progresszbe.
    /// </summary>
    /// <param name="npcId">A questadó typed NPC-azonosítója.</param>
    /// <param name="instanceId">A konkrét runtime NPC-példány azonosítója.</param>
    /// <returns>Az ebben a hívásban ténylegesen aktivált questek.</returns>
    public IReadOnlyList<QuestHandle> ActivateAvailableForNpc(QuestNpcId npcId, QuestNpcInstanceId instanceId = default)
    {
        var states = _availability
            .ActivateAvailableForNpc(
                npcId,
                instanceId);

        foreach (var state in states)
        {
            QuestChanged?.Invoke(CreateHandle(state));
            _progressEngine.SynchronizeExplorationObjective(state);
        }

        if (states.Any(state =>
                _catalog.Get(state.QuestId).Objective
                    is QuestObjective.CollectItem))
        {
            _progressEngine
                .SynchronizeCollectObjectives();
        }

        return states
            .Select(CreateHandle)
            .ToArray();
    }

    /// <summary>
    /// API: YES
    /// Elhagy egy folyamatban lévő questet.
    ///
    /// A runtime state-et a domain szabályai szerint módosítja, majd
    /// <see cref="QuestChanged"/> értesítést küld. A hívó ne módosítsa
    /// közvetlenül a <see cref="QuestRuntimeState"/> állapotát.
    /// </summary>
    /// <param name="questId">Az elhagyandó quest azonosítója.</param>
    /// <param name="giverInstanceId">A questadó runtime példányazonosítója.</param>
    /// <returns>Az elhagyás utáni quest handle.</returns>
    public QuestHandle Abandon(QuestId questId, QuestNpcInstanceId giverInstanceId = default)
    {
        var state =
            _stateStore.Get(
                questId,
                giverInstanceId);

        state.Abandon();
        QuestChanged?.Invoke(CreateHandle(state));

        return CreateHandle(state);
    }

    // ------------------------------------------------------------
    // Gameplay események
    // ------------------------------------------------------------

    /// <summary>
    /// API: YES
    /// Jelenti a quest-rendszernek, hogy egy ellenfél meghalt.
    ///
    /// A hívó csak a világban megtörtént tényt közli. A progress engine dönti el,
    /// melyik aktív questet érinti az esemény, beleértve az enemy-, trait- és
    /// follower-feltételeket is.
    /// </summary>
    /// <param name="defeatedEnemy">A ténylegesen legyőzött runtime ellenfél.</param>
    /// <returns>Az esemény hatására megváltozott questek listája.</returns>
    public IReadOnlyList<QuestProgressChange> RegisterKill(Enemy defeatedEnemy)
    {
        ArgumentNullException.ThrowIfNull(defeatedEnemy);

        return _progressEngine.Process(
            new EnemyKilledEvent(defeatedEnemy));
    }

    /// <summary>
    /// API: YES
    /// Jelenti, hogy egy quest szempontjából releváns tárgy partybeli darabszáma
    /// megváltozott.
    ///
    /// Elsősorban collect questek újraszámítására szolgál. Ezek progressze nem
    /// egyszerű növekmény, hanem az aktuális party inventoryból származtatott érték.
    /// </summary>
    /// <param name="item">A megváltozott darabszámú tárgy definíciója.</param>
    /// <returns>Az inventory-változás által érintett quest progress-változások.</returns>
    public IReadOnlyList<QuestProgressChange> RegisterInventoryChanged(IItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return _progressEngine.Process(
            new InventoryItemCountChangedEvent(item));
    }

    /// <summary>
    /// API: YES
    /// Jelenti a quest-rendszernek egy csapda sikeres hatástalanítását.
    /// A quest-relevanciát a progress engine határozza meg.
    /// </summary>
    /// <returns>Az esemény által módosított questek progress-változásai.</returns>
    public IReadOnlyList<QuestProgressChange> RegisterTrapDisarmed()
    {
        return _progressEngine.Process(
            new TrapDisarmedEvent());
    }

    /// <summary>
    /// API: YES
    /// Jelenti a quest-rendszernek egy kincsesláda kinyitását.
    /// A hívó csak a világban megtörtént eseményt közli; nem keres questeket kézzel.
    /// </summary>
    /// <returns>Az esemény által módosított questek progress-változásai.</returns>
    public IReadOnlyList<QuestProgressChange> RegisterChestOpened()
    {
        return _progressEngine.Process(
            new ChestOpenedEvent());
    }

    /// <summary>
    /// API: YES
    /// Jelenti, hogy a játékos vagy a party elért egy typed quest-helyszínt.
    ///
    /// Olyan objective-ekhez használható, amelyek a hely tényleges elérésére reagálnak.
    /// </summary>
    /// <param name="location">Az elért typed quest-helyszín.</param>
    /// <returns>Az esemény által módosított questek progress-változásai.</returns>
    public IReadOnlyList<QuestProgressChange> RegisterLocationReached(QuestLocation location)
    {
        return _progressEngine.Process(
            new LocationReachedEvent(location));
    }

    /// <summary>
    /// API: YES
    /// Jelenti, hogy a játékos vagy a party felfedezett egy typed quest-helyszínt.
    ///
    /// A felfedezés külön esemény a hely tényleges elérésétől; discovery objective
    /// esetén ezt a metódust kell használni.
    /// </summary>
    /// <param name="location">A felfedezett typed quest-helyszín.</param>
    /// <returns>Az esemény által módosított questek progress-változásai.</returns>
    public IReadOnlyList<QuestProgressChange> RegisterLocationDiscovered(QuestLocation location) =>
        _progressEngine.Process(new LocationDiscoveredEvent(location));

    /// <summary>
    /// API: YES
    /// Jelenti, hogy egy konkrét quest-NPC elért egy meghatározott quest-helyszínt.
    ///
    /// Tipikusan escort objective-ekhez használható. Itt kötelező a konkrét
    /// runtime instance ID, hogy a rendszer ugyanazt az NPC-példányt kövesse.
    /// </summary>
    /// <param name="npcId">Az escortált quest-NPC typed azonosítója.</param>
    /// <param name="instanceId">Az escortált konkrét runtime NPC-példány azonosítója.</param>
    /// <param name="location">Az NPC által elért typed quest-helyszín.</param>
    /// <returns>Az esemény által módosított questek progress-változásai.</returns>
    /// <exception cref="ArgumentException">
    /// Akkor keletkezik, ha az NPC-azonosító vagy az instance ID nincs megadva.
    /// </exception>
    public IReadOnlyList<QuestProgressChange> RegisterNpcReachedLocation(
        QuestNpcId npcId, QuestNpcInstanceId instanceId, QuestLocation location)
    {
        if (npcId == QuestNpcId.None) throw new ArgumentException("A kísérő NPC azonosítója kötelező.", nameof(npcId));
        if (instanceId.IsNone) throw new ArgumentException("A kísérő példányazonosítója kötelező.", nameof(instanceId));
        return _progressEngine.Process(new NpcReachedLocationEvent(npcId, instanceId, location));
    }

    /// <summary>
    /// API: YES
    /// Minden aktív collect quest progresszét újraszámolja az aktuális party
    /// inventory alapján.
    ///
    /// Akkor hasznos, ha több inventory-változás történt egyszerre, vagy nincs egyetlen
    /// konkrét megváltozott tárgy. A progress visszafelé is változhat: például egy
    /// <see cref="QuestState.ReadyToTurnIn"/> collect quest újra
    /// <see cref="QuestState.Active"/> lehet, ha elfogy egy szükséges tárgy.
    /// </summary>
    /// <returns>Az újraszámítás során ténylegesen megváltozott questek listája.</returns>
    public IReadOnlyList<QuestProgressChange> SynchronizeCollectQuests()
    {
        return _progressEngine
            .SynchronizeCollectObjectives();
    }

    /// <summary>
    /// API: NO – belső QuestNpcHandle infrastruktúra.
    /// Elindítja a megadott questadó NPC beszélgetését a konfigurált conversation
    /// adapteren keresztül.
    ///
    /// Gameplay kódból lehetőség szerint a <see cref="QuestNpcHandle.StartConversation"/>
    /// vagy az egyedi NPC API megfelelő metódusát kell használni.
    /// </summary>
    /// <param name="npcId">A megszólítandó quest-NPC typed azonosítója.</param>
    /// <param name="instanceId">A konkrét runtime NPC-példány azonosítója.</param>
    internal void StartConversation(QuestNpcId npcId, QuestNpcInstanceId instanceId = default)
    {
        _conversationService.StartConversation(
            npcId,
            instanceId);
    }

    // ------------------------------------------------------------
    // Belső handle létrehozás
    // ------------------------------------------------------------

    /// <summary>
    /// API: NO – belső segédmetódus.
    /// A katalógus definícióját és a központi runtime state-et egy
    /// <see cref="QuestHandle"/> objektumba csomagolja.
    /// </summary>
    private QuestHandle CreateHandle(
        QuestId questId,
        QuestNpcInstanceId instanceId)
    {
        var definition =
            _catalog.Get(questId);

        var state =
            _stateStore.GetOrCreate(
                questId,
                instanceId);

        return new QuestHandle(
            this,
            definition,
            state);
    }

    /// <summary>
    /// API: NO – belső segédmetódus.
    /// Egy már ismert runtime state-hez létrehozza a megfelelő
    /// <see cref="QuestHandle"/> objektumot.
    /// </summary>
    private QuestHandle CreateHandle(
        QuestRuntimeState state)
    {
        return new QuestHandle(
            this,
            _catalog.Get(state.QuestId),
            state);
    }
}
