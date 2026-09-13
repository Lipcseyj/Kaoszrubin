using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Quests;

namespace KaoszRubin.Application.Quests;

/// <summary>
/// API: YES
/// Egy konkrét quest kényelmes, biztonságos publikus nézete.
///
/// A handle összekapcsolja a változatlan <see cref="QuestDefinition"/> definíciót
/// az aktuális <see cref="QuestRuntimeState"/> futásidejű állapottal.
///
/// A gameplay kód quest-specifikus lekérdezésekhez és műveletekhez ezt a típust
/// használja a belső state- és service-objektumok közvetlen elérése helyett.
/// A handle maga nem külön quest-state másolatot tárol.
/// </summary>
public sealed class QuestHandle
{
    private readonly QuestManager _manager;
    private readonly QuestDefinition _definition;
    private readonly QuestRuntimeState _state;

    /// <summary>
    /// API: NO – belső konstruktor.
    /// A handle-t kizárólag a <see cref="QuestManager"/> hozza létre, hogy mindig
    /// konzisztensen ugyanahhoz a quest-definícióhoz és runtime state-hez kapcsolódjon.
    /// </summary>
    /// <param name="manager">A publikus quest façade, amelyen keresztül a módosító műveletek futnak.</param>
    /// <param name="definition">A quest változatlan definíciója.</param>
    /// <param name="state">A quest aktuális futásidejű állapota.</param>
    internal QuestHandle(
        QuestManager manager,
        QuestDefinition definition,
        QuestRuntimeState state)
    {
        _manager = manager;
        _definition = definition;
        _state = state;
    }

    /// <summary>
    /// API: YES
    /// A quest stabil, typed azonosítója.
    /// A definícióból származik, és runtime közben nem változik.
    /// </summary>
    public QuestId Id =>
        _definition.Id;

    /// <summary>
    /// API: YES
    /// A questet adó NPC típusszintű typed azonosítója.
    /// A konkrét runtime NPC-példány azonosítója külön a
    /// <see cref="GiverInstanceId"/> propertyn érhető el.
    /// </summary>
    public QuestNpcId Giver =>
        _definition.Giver;

    /// <summary>
    /// API: YES
    /// A questet adó konkrét runtime NPC-példány azonosítója.
    ///
    /// Globális scope-ú questnél ez tipikusan
    /// <see cref="QuestNpcInstanceId.None"/>.
    /// </summary>
    public QuestNpcInstanceId GiverInstanceId =>
        _state.GiverInstanceId;

    /// <summary>
    /// API: YES
    /// Kényelmi alias a quest címére.
    /// Ugyanazt az értéket adja vissza, mint a <see cref="Title"/>.
    /// </summary>
    public string Name =>
        _definition.Title;

    /// <summary>
    /// API: YES
    /// A quest játékosnak megjeleníthető címe.
    /// A változatlan quest-definícióból származik.
    /// </summary>
    public string Title =>
        _definition.Title;

    /// <summary>
    /// API: YES
    /// A quest játékosnak megjeleníthető leírása.
    /// A változatlan quest-definícióból származik.
    /// </summary>
    public string Description =>
        _definition.Description;

    /// <summary>
    /// API: YES
    /// A quest typed objective-definíciója.
    ///
    /// Lekérdezésre használható, de gameplay kód ne módosítson közvetlenül
    /// quest progresst az objective alapján; erre a <see cref="QuestManager"/>
    /// gameplay esemény API-jai szolgálnak.
    /// </summary>
    public QuestObjective Objective =>
        _definition.Objective;

    /// <summary>
    /// API: YES
    /// A quest teljesítéséért járó összes tapasztalati pont.
    /// A tényleges szétosztást a reward/completion réteg végzi.
    /// </summary>
    public int ExperienceReward =>
        _definition.ExperienceReward;

    /// <summary>
    /// API: YES
    /// Meghatározza, hogy a quest globális vagy konkrét NPC-példányhoz kötött.
    /// </summary>
    public QuestScope Scope =>
        _definition.Scope;

    /// <summary>
    /// API: YES
    /// A quest ismételhetőségi szabálya.
    /// A hívó ezt információként olvashatja; az ismétlés állapotgépét nem itt kell kezelni.
    /// </summary>
    public QuestRepeatPolicy RepeatPolicy =>
        _definition.RepeatPolicy;

    /// <summary>
    /// API: YES
    /// A quest aktuális futásidejű állapota.
    ///
    /// Az érték közvetlenül a központi <see cref="QuestRuntimeState"/> objektumból
    /// származik, ezért mindig az aktuális state-et tükrözi.
    /// </summary>
    public QuestState State =>
        _state.State;

    /// <summary>
    /// API: YES
    /// A quest aktuális progresszértéke.
    ///
    /// Az érték a runtime state-ből származik. A hívó csak olvassa;
    /// módosítása a quest progress engine feladata.
    /// </summary>
    public int Progress =>
        _state.Progress;

    /// <summary>
    /// API: YES
    /// Az objective teljesítéséhez szükséges célérték.
    /// A quest definíciójából származik.
    /// </summary>
    public int RequiredCount =>
        _definition.Objective.RequiredCount;

    /// <summary>
    /// API: YES
    /// Megmutatja, hogy a quest runtime state-je szerint hányszor lett sikeresen lezárva.
    ///
    /// Egyszer teljesíthető questnél tipikusan 0 vagy 1.
    /// </summary>
    public int CompletionCount =>
        _state.CompletionCount;

    /// <summary>
    /// API: YES
    /// <c>true</c>, ha a quest még nem felajánlható, mert valamely aktiválási
    /// feltétele nem teljesül.
    /// </summary>
    public bool IsLocked =>
        State == QuestState.Locked;

    /// <summary>
    /// API: YES
    /// <c>true</c>, ha a quest jelenleg felvehető, de még nincs aktiválva.
    /// </summary>
    public bool IsAvailable =>
        State == QuestState.Available;

    /// <summary>
    /// API: YES
    /// <c>true</c>, ha a quest aktív és még nincs leadásra kész állapotban.
    /// </summary>
    public bool IsActive =>
        State == QuestState.Active;

    /// <summary>
    /// API: YES
    /// <c>true</c>, ha az objective teljesült, és a quest jelenleg leadható.
    /// </summary>
    public bool IsReadyToTurnIn =>
        State == QuestState.ReadyToTurnIn;

    /// <summary>
    /// API: YES
    /// <c>true</c>, ha a quest sikeresen le lett adva és végleg lezárult.
    /// </summary>
    public bool IsCompleted =>
        State == QuestState.Completed;

    /// <summary>
    /// API: YES
    /// <c>true</c>, ha a quest sikertelen vagy elhagyott állapotban van.
    /// A legacy rendszer <c>Abandoned</c> állapotának ez a typed megfelelője.
    /// </summary>
    public bool IsFailed =>
        State == QuestState.Failed;

    /// <summary>
    /// API: YES
    /// Megadja, hogy a quest a jelenlegi állapotában szabályosan completelhető-e.
    ///
    /// A döntést nem helyben számolja ki, hanem a <see cref="QuestManager"/>
    /// completion szabályain keresztül kérdezi le.
    /// </summary>
    public bool CanComplete =>
        _manager.CanComplete(
            Id,
            GiverInstanceId);

    /// <summary>
    /// API: YES
    /// <c>true</c>, ha a quest folyamatban van.
    ///
    /// Az <see cref="QuestState.Active"/> és
    /// <see cref="QuestState.ReadyToTurnIn"/> állapotokat egyaránt ide sorolja.
    /// </summary>
    public bool IsInProgress =>
        State is
            QuestState.Active or
            QuestState.ReadyToTurnIn;

    /// <summary>
    /// API: YES
    /// <c>true</c>, ha a quest végleges állapotba került.
    ///
    /// A <see cref="QuestState.Completed"/> és
    /// <see cref="QuestState.Failed"/> állapotokat tekinti lezártnak.
    /// Hasznos például annak eldöntésére, hogy egy questadó NPC összes questje lezárult-e.
    /// </summary>
    public bool IsResolved =>
        State is
            QuestState.Completed or
            QuestState.Failed;

    /// <summary>
    /// API: YES
    /// A quest fix tárgyjutalmának definíciója, ha van ilyen.
    ///
    /// Ez csak a jutalom definícióját jelzi; a tényleges kiosztást a
    /// quest completion/reward réteg végzi.
    /// </summary>
    public IItemDefinition? FixedRewardItem =>
    _definition.FixedRewardItem;

    /// <summary>
    /// API: YES
    /// A fix tárgyjutalomból kiosztandó darabszám.
    /// Ha nincs fix tárgyjutalom, az érték tipikusan 0.
    /// </summary>
    public int FixedRewardItemCount =>
        _definition.FixedRewardItemCount;

    /// <summary>
    /// API: YES
    /// A quest teljesítésekor kisorsolandó véletlen tárgyjutalmak száma.
    /// A konkrét tárgyakat a reward service választja ki.
    /// </summary>
    public int RandomRewardCount =>
        _definition.RandomRewardCount;

    /// <summary>
    /// API: YES
    /// Leadja és sikeresen lezárja a questet a
    /// <see cref="QuestManager"/> szabályain keresztül.
    ///
    /// A művelet végrehajtja a completion folyamatot és a tényleges reward-kezelést is.
    /// A hívó ne módosítsa közvetlenül a runtime state-et.
    /// </summary>
    /// <returns>A teljesítés során ténylegesen kiosztott jutalmakat és eredményeket tartalmazó objektum.</returns>
    public QuestCompletionResult Complete()
    {
        return _manager.Complete(
            Id,
            GiverInstanceId);
    }

    /// <summary>
    /// API: YES
    /// Elhagyja a questet a <see cref="QuestManager"/> szabályain keresztül.
    ///
    /// A művelet a quest runtime state-jét a megfelelő végleges sikertelen
    /// állapotba helyezi, és a manageren keresztül projekciós értesítést is kiválthat.
    /// </summary>
    public void Abandon()
    {
        _manager.Abandon(
            Id,
            GiverInstanceId);
    }

    /// <summary>
    /// API: YES
    /// Aktiválja a questet a <see cref="QuestManager"/> szabályain keresztül.
    ///
    /// Az availability feltételeket, valamint az aktiválás utáni collect/exploration
    /// szinkronizálást a manager és a mögöttes service-ek végzik.
    /// A hívó ne állítsa közvetlenül a quest state-et.
    /// </summary>
    public void Activate()
    {
        _manager.Activate(
            Id,
            GiverInstanceId);
    }

    /// <summary>
    /// API: YES
    /// Rövid, diagnosztikai szöveges reprezentációt ad a questről:
    /// cím, aktuális state és progressz.
    ///
    /// Elsősorban logoláshoz és hibakereséshez használható, nem játékosnak szánt
    /// lokalizált UI-szövegként.
    /// </summary>
    public override string ToString() =>
        $"{Title} [{State}] {Progress}/{RequiredCount}";
}
