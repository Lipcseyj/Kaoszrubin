using KaoszRubin.Domain.Quests;

namespace KaoszRubin.Application.Quests;

/// <summary>
/// API: YES
/// Egy questadó NPC kényelmes, biztonságos publikus quest API-ja.
///
/// A handle egy konkrét questadó NPC-t képvisel a <see cref="QuestManager"/> fölött.
/// Nem tárol saját quest-state-et; minden lekérdezést és módosító műveletet
/// visszadelegál a központi managerhez.
///
/// Egyedi NPC-knél az <see cref="InstanceId"/> jellemzően
/// <see cref="QuestNpcInstanceId.None"/>, nem egyedi NPC-knél viszont a konkrét
/// runtime példányazonosító különíti el ugyanazon NPC-típus questjeit.
/// </summary>
public class QuestNpcHandle
{
    /// <summary>
    /// API: NO – leszármazott quest-NPC API-k belső hozzáférési pontja.
    ///
    /// A property azért <c>protected</c>, hogy az olyan erősen típusos API-k,
    /// mint a <c>RodericNpcApi</c> vagy az <c>EliraNpcApi</c>, ugyanazt a központi
    /// <see cref="QuestManager"/> példányt használhassák.
    /// </summary>
    protected QuestManager Manager { get; }

    /// <summary>
    /// API: YES
    /// A questadó NPC típusszintű, typed azonosítója.
    ///
    /// Ez azt mondja meg, milyen NPC-ről van szó; a konkrét runtime példányt
    /// az <see cref="InstanceId"/> azonosítja.
    /// </summary>
    public QuestNpcId Id { get; }

    /// <summary>
    /// API: YES
    /// A questadó konkrét runtime példányazonosítója.
    ///
    /// Globális vagy egyedi NPC-k esetén lehet
    /// <see cref="QuestNpcInstanceId.None"/>, per-instance NPC-knél viszont
    /// ezzel különülnek el az azonos típusú NPC-k quest-state-jei.
    /// </summary>
    public QuestNpcInstanceId InstanceId { get; }

    /// <summary>
    /// API: NO – belső konstruktor.
    /// A handle-t a <see cref="QuestManager"/> vagy egy erősen típusos
    /// NPC API hozza létre.
    ///
    /// A konstruktor megakadályozza a <see cref="QuestNpcId.None"/> használatát,
    /// mert egy NPC-handle mindig konkrét questadó típust reprezentál.
    /// </summary>
    /// <param name="manager">A központi quest façade.</param>
    /// <param name="id">A questadó typed NPC-azonosítója.</param>
    /// <param name="instanceId">A konkrét runtime NPC-példány azonosítója.</param>
    /// <exception cref="ArgumentException">
    /// Akkor keletkezik, ha <paramref name="id"/> értéke <see cref="QuestNpcId.None"/>.
    /// </exception>
    internal QuestNpcHandle(
        QuestManager manager,
        QuestNpcId id,
        QuestNpcInstanceId instanceId = default)
    {
        ArgumentNullException.ThrowIfNull(manager);

        if (id == QuestNpcId.None)
        {
            throw new ArgumentException(
                "QuestNpcId.None nem használható.",
                nameof(id));
        }

        Manager = manager;
        Id = id;
        InstanceId = instanceId;
    }

    /// <summary>
    /// API: YES
    /// Visszaadja az NPC-hez tartozó összes questet, állapottól függetlenül.
    ///
    /// A visszakapott <see cref="QuestHandle"/> objektumok a központi runtime
    /// state-et reprezentálják; nem különálló quest-másolatok.
    /// </summary>
    /// <returns>Az NPC összes definiált questjének handle-je.</returns>
    public IReadOnlyList<QuestHandle> GetQuests()
    {
        return Manager.GetQuestsForNpc(
            Id,
            InstanceId);
    }

    /// <summary>
    /// API: YES
    /// <c>true</c>, ha az NPC minden questje végleges állapotban van.
    ///
    /// Egy quest akkor számít lezártnak, ha
    /// <see cref="QuestHandle.IsResolved"/> igaz, vagyis Completed vagy Failed.
    /// Tipikus használat: recruitolhatóság vagy történeti továbblépés vizsgálata.
    /// </summary>
    public bool AreAllQuestsResolved =>
        GetQuests()
            .All(quest => quest.IsResolved);

    /// <summary>
    /// API: YES
    /// <c>true</c>, ha az NPC-hez tartozik legalább egy még nem lezárt quest.
    /// A <see cref="AreAllQuestsResolved"/> kényelmi negáltja.
    /// </summary>
    public bool HasUnresolvedQuests =>
        !AreAllQuestsResolved;

    /// <summary>
    /// API: YES
    /// Visszaadja az NPC jelenleg folyamatban lévő questjeit.
    ///
    /// Az Active és ReadyToTurnIn állapotú questek egyaránt ide tartoznak.
    /// </summary>
    /// <returns>Az NPC folyamatban lévő questjei.</returns>
    public IReadOnlyList<QuestHandle> GetActiveQuests()
    {
        return Manager.GetActiveQuestsForNpc(
            Id,
            InstanceId);
    }

    /// <summary>
    /// API: YES
    /// Visszaadja az NPC jelenleg általánosan felajánlható questjeit.
    ///
    /// Az availability feltételeket a <see cref="QuestManager"/> mögötti
    /// quest service-ek ellenőrzik; ezeket a hívónak nem kell kézzel újraimplementálnia.
    /// </summary>
    /// <returns>Az NPC jelenleg felvehető questjei.</returns>
    public IReadOnlyList<QuestHandle> GetAvailableQuests()
    {
        return Manager.GetAvailableQuestsForNpc(
            Id,
            InstanceId);
    }

    /// <summary>
    /// API: YES
    /// Lekér egy konkrét questet, és ellenőrzi, hogy valóban ehhez az NPC-hez tartozik.
    ///
    /// NPC-specifikus kódban ezt érdemes használni a közvetlen
    /// <see cref="QuestManager.GetQuest(QuestId, QuestNpcInstanceId)"/> hívás helyett,
    /// mert plusz giver-validációt biztosít.
    /// </summary>
    /// <param name="questId">A lekérendő typed quest-azonosító.</param>
    /// <returns>Az NPC-hez tartozó quest handle-je.</returns>
    /// <exception cref="InvalidOperationException">
    /// Akkor keletkezik, ha a kért quest nem ehhez a questadó NPC-hez tartozik.
    /// </exception>
    public QuestHandle GetQuest(
        QuestId questId)
    {
        var quest =
            Manager.GetQuest(
                questId,
                InstanceId);

        if (quest.Giver != Id)
        {
            throw new InvalidOperationException(
                $"A(z) '{questId}' quest nem a(z) " +
                $"'{Id}' NPC-hez tartozik.");
        }

        return quest;
    }

    /// <summary>
    /// API: YES
    /// Aktiválja az NPC összes jelenleg általánosan felajánlható questjét.
    ///
    /// Az aktiválási feltételek, projekciós értesítések és az aktiválás utáni
    /// collect/exploration szinkronizálás a <see cref="QuestManager"/> feladata.
    /// </summary>
    /// <returns>Az ebben a hívásban ténylegesen aktivált questek.</returns>
    public IReadOnlyList<QuestHandle>
        ActivateAvailableQuests()
    {
        return Manager.ActivateAvailableForNpc(
            Id,
            InstanceId);
    }

    /// <summary>
    /// API: YES
    /// Elindítja a questadó NPC beszélgetését a központi conversation adapteren keresztül.
    ///
    /// A handle nem jelenít meg UI-t közvetlenül; a <see cref="QuestManager"/>
    /// a konfigurált conversation service felé delegál.
    /// </summary>
    public void StartConversation()
    {
        Manager.StartConversation(
            Id,
            InstanceId);
    }

    /// <summary>
    /// API: YES
    /// Rövid diagnosztikai reprezentációt ad az NPC-handle-ről.
    ///
    /// Egyedi/globális NPC-nél csak az NPC ID-t, per-instance NPC-nél az
    /// instance ID-t is tartalmazza. Elsősorban logoláshoz és hibakereséshez való.
    /// </summary>
    public override string ToString() =>
        InstanceId.IsNone
            ? Id.ToString()
            : $"{Id} [{InstanceId}]";
}
