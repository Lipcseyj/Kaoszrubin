# Quest rendszer – architektúra és használat

Ez a dokumentum a Káoszrubin új quest-rétegének felépítését, felelősségi köreit és használatát írja le.

A rendszer fő célja, hogy:

- a questek állapota és progressze egyetlen helyen legyen kezelve;
- a játékkód ne dolgozzon string alapú quest- és NPC-azonosítókkal;
- a `Game` ne tartalmazzon szétszórt quest-logikát;
- a `WorldNpc` ne legyen quest-state tároló;
- a questek használata a játék felől egyszerű, típusos és nehezen elrontható API-n keresztül történjen;
- a Domain ne függjön UI-tól, fájlformátumtól vagy a régi CSV/string reprezentációktól.

---

# 1. Magas szintű felépítés

A quest rendszer három fő rétegre oszlik:

```text
Game / UI
    │
    ▼
Application
    │
    ▼
Domain
    ▲
    │
Infrastructure
```

A tényleges függési szabály ennél pontosabban:

```text
Application ─────► Domain
Infrastructure ─► Domain

Game ────────────► Application
Game ────────────► Infrastructure   // composition root / bekötés miatt

Domain ─X─► Application
Domain ─X─► Infrastructure
Domain ─X─► UI
Infrastructure ─X─► Game
```

A Domain a központ.

Az Application a játék számára használható quest-viselkedést szervezi össze.

Az Infrastructure a régi/adatforrás-specifikus világot fordítja át a typed quest-rendszer nyelvére.

---

# 2. Domain réteg

## Feladata

A `Domain.Quests` tartalmazza a quest-rendszer üzleti fogalmait.

Itt vannak többek között:

- `QuestId`
- `QuestNpcId`
- `QuestNpcInstanceId`
- `QuestState`
- `QuestScope`
- `QuestRepeatPolicy`
- `QuestStoryState`
- `QuestDefinition`
- `QuestRuntimeState`
- `QuestObjective`
- `QuestFollowerRequirement`
- `QuestActivationRequirement`
- `QuestEvent`
- `QuestProgressChange`

A Domain azt írja le, hogy **mi egy quest**, milyen állapotai vannak, milyen objective-je lehet, hogyan változhat a progressze.

## Mit láthat?

A Domain láthat:

- saját Domain típusokat;
- más domain objektumokat, ha üzletileg indokolt, például:
  - `Enemy`
  - `EnemyDefinition`
  - `IItemDefinition`

## Mit nem láthat?

A Domain nem láthat:

- `Game`
- `ConsoleRenderer`
- `MazeQuestWorldContext`
- CSV ID-ket, például `"NPCQ039"`
- `GameDataCatalog`
- fájlmentést
- UI ablakokat
- coop snapshotot
- hangokat
- konzolszíneket
- legacy `NpcQuestState` / `NpcQuestProgress` típusokat

Például ez rossz lenne:

```csharp
public sealed class QuestRuntimeState
{
    private readonly ConsoleRenderer _renderer; // TILOS
}
```

vagy:

```csharp
if (questId == "NPCQ039") // TILOS
{
}
```

A Domainben typed értékekkel dolgozunk:

```csharp
if (QuestId == QuestId.RodericOathbreakerKnight)
{
}
```

---

# 3. Infrastructure réteg

## Feladata

Az Infrastructure a quest rendszer és a játék meglévő technikai/adatforrás rétege közti adapter.

Tipikus osztályok:

- `QuestCatalogBuilder`
- `LegacyQuestIdMap`
- `LegacyNpcIdMap`
- `LegacyQuestStoryStateMap`
- `QuestNpcInstanceRegistry`
- `MazeQuestWorldContext`
- `LegacyQuestRewardContext`
- `LegacyQuestNpcConversationService`

## Fő szerepe

### 1. Legacy string ID-k fordítása typed ID-kre

Példa:

```csharp
LegacyQuestIdMap.ToQuestId("NPCQ039");
```

eredménye:

```csharp
QuestId.RodericOathbreakerKnight
```

Ugyanez NPC-kre:

```csharp
LegacyNpcIdMap.ToQuestNpcId("NPC021");
```

eredménye:

```csharp
QuestNpcId.SirRoderic
```

### 2. CSV / GameData definíciók átfordítása Domain objektumokká

Ezt végzi a:

```csharp
QuestCatalogBuilder
```

A builderből egy typed:

```csharp
QuestCatalog
```

jön létre.

A játék többi része lehetőleg már ne a `NpcQuestDefinition` CSV rekordok alapján döntsön quest-logikáról.

### 3. A játékvilág leképezése a quest engine számára

A Domain/Application nem akarja tudni, hogy az NPC:

- `Maze.WorldNpcs` között van-e;
- temporary followerként a `PartyMembers` között van-e;
- milyen string story state-et használ;
- hogyan számoljuk a Manhattan távolságot.

Ezt rejti el a:

```csharp
IQuestWorldContext
```

implementációja:

```csharp
MazeQuestWorldContext
```

Példa:

```csharp
_world.IsNpcParticipatingInCombat(
    QuestNpcId.SirRoderic,
    instanceId,
    defeatedEnemy,
    maximumDistance: 6);
```

A quest engine nem tudja, hogyan találjuk meg Rodericet a Maze-ben.

Ez szándékos.

## Mit láthat?

Az Infrastructure láthat:

- Domaint;
- `GameDataCatalog`-ot;
- `Maze`-t;
- `WorldNpc`-t;
- inventory infrastruktúrát;
- legacy string ID-ket.

## Mit nem csinálhat?

Az Infrastructure ne tartalmazzon gameplay orchestrationt.

Például ne itt legyen:

```csharp
_renderer.DrawInventoryMessage(...);
RequestCoopSnapshotPublish();
QuestCompletionWindow.Show(...);
```

Az már Application/Game/UI feladat.

---

# 4. Application réteg

## Feladata

Az Application összerakja a Domain quest-mechanikáit használható szolgáltatásokká.

A legfontosabb elemek:

```text
QuestManager
QuestCatalog
QuestStateStore
QuestAvailabilityService
QuestProgressEngine
QuestCompletionProcessor
QuestRewardService
QuestHandle
QuestNpcHandle
RodericNpcApi
EliraNpcApi
NpcQuestCoordinator
```

---

# 5. QuestManager – a publikus façade

A játék quest-rendszer felé néző fő API-ja:

```csharp
QuestManager
```

A gameplay kód lehetőleg **ezen keresztül kommunikáljon a quest-rendszerrel**.

A `Game` ne közvetlenül a `QuestStateStore`, `QuestProgressEngine` vagy `QuestAvailabilityService` példányokat használja.

Jó:

```csharp
_questManager.RegisterQuestKill(defeatedEnemy);
```

Rossz:

```csharp
_progressEngine.Process(new EnemyKilledEvent(defeatedEnemy));
```

A második működhetne, de megkerüli a façade-ot, ezért később nehezebb karbantartani.

---

# 6. QuestHandle

Egy konkrét quest játék felől használható nézete.

Példa:

```csharp
var quest =
    _questManager.Roderic.Quests.OathbreakerKnight;
```

Hasznos propertyk:

```csharp
quest.Id
quest.Name
quest.Title
quest.Description
quest.State
quest.Progress
quest.RequiredCount
quest.ExperienceReward

quest.IsLocked
quest.IsAvailable
quest.IsActive
quest.IsReadyToTurnIn
quest.IsCompleted
quest.IsFailed
quest.IsInProgress
quest.IsResolved
```

Műveletek:

```csharp
quest.Activate();
quest.Complete();
quest.Abandon();
```

A handle nem külön state-t tartalmaz.

A mögötte levő `QuestRuntimeState` aktuális állapotát teszi kényelmesen elérhetővé.

---

# 7. QuestNpcHandle

Egy quest-giver NPC quest API-ja.

Példa:

```csharp
var roderic =
    _questManager.Roderic;
```

vagy általánosan:

```csharp
var npc =
    _questManager.For(
        QuestNpcId.MonsterHunter,
        instanceId);
```

Használható például:

```csharp
npc.GetQuests();
npc.GetActiveQuests();
npc.GetAvailableQuests();

npc.GetQuest(QuestId.MonsterHunterGoblinHunt);

npc.ActivateAvailableQuests();

npc.AreAllQuestsResolved;
npc.HasUnresolvedQuests;

npc.StartConversation();
```

A `GetQuest()` ellenőrzi, hogy a kért quest valóban az adott NPC-hez tartozik.

Ezért NPC-specifikus kódban előnyösebb:

```csharp
questNpc.GetQuest(questId);
```

mint egy teljesen általános manager lookup.

---

# 8. Egyedi NPC API-k

Az egyedi történeti NPC-k saját kényelmi API-t kaphatnak.

## Roderic

```csharp
_questManager.Roderic
```

Questjei:

```csharp
_questManager.Roderic.Quests.FallenComradesInsignia
_questManager.Roderic.Quests.SharedBladeTrial
_questManager.Roderic.Quests.OathbreakerKnight
_questManager.Roderic.Quests.TheDeadAreNotPrey
```

Példa:

```csharp
if (_questManager.Roderic.Quests.OathbreakerKnight.IsCompleted)
{
    // történeti logika
}
```

## Elira

```csharp
_questManager.Elira
```

Questjei:

```csharp
_questManager.Elira.Quests.Rescue
_questManager.Elira.Quests.TornBandage
_questManager.Elira.Quests.OnOurTrail
```

Ezek az API-k azért hasznosak, mert történeti kódban nincs szükség enum lookupokra sem.

---

# 9. QuestStateStore

A questek runtime állapotának egyetlen igazságforrása.

Tárolja például:

```text
QuestId
GiverInstanceId
State
Progress
CompletionCount
```

Fontos:

> A `WorldNpc.Quests` NEM lehet párhuzamos igazságforrás.

A migráció végén a runtime quest állapot csak a `QuestStateStore`-ban legyen.

---

# 10. QuestScope

Két scope van:

```csharp
QuestScope.Global
QuestScope.PerNpcInstance
```

## Global

Egy quest a teljes játékban egyetlen példány.

Tipikusan unique story NPC-knél:

```text
Sir Roderic
Elira
```

Ilyenkor az instance ID normalizálva:

```csharp
QuestNpcInstanceId.None
```

## PerNpcInstance

Ugyanabból az NPC típusból több példány is lehet.

Például két külön Monster Hunter saját quest-state-et kaphat.

Ilyenkor szükséges:

```csharp
QuestNpcInstanceId
```

---

# 11. QuestNpcInstanceRegistry

A runtime `WorldNpc` objektumokat stabil typed instance ID-hez köti.

Példa:

```csharp
var instanceId =
    _questWorldContext.GetInstanceId(npc);
```

Ezt ne kézzel generálja a gameplay kód.

A registry később a save/load rendszer része is.

---

# 12. QuestState lifecycle

Az alap állapotgép:

```text
Locked
   │
   ▼
Available
   │ Activate
   ▼
Active
   │ progress kész
   ▼
ReadyToTurnIn
   │ Complete
   ▼
Completed
```

Elhagyás esetén:

```text
Active ─────► Failed
ReadyToTurnIn ─► Failed
```

`Available` jelentése:

> a quest feltételei teljesülnek, felvehető.

`Active`:

> fel lett véve, de még nincs kész.

`ReadyToTurnIn`:

> objective kész, leadható.

`Completed`:

> jutalom kiosztva és végleg lezárt.

`Failed`:

> elhagyott / sikertelen quest.

---

# 13. Aktiválás

Az aktiválhatóságot a:

```csharp
QuestAvailabilityService
```

kezeli.

Nem a `Game` ellenőrzi kézzel a story state-et.

Rossz:

```csharp
if (npc.StoryStateId == "TRUSTED")
    npc.ActivateQuest("NPCQ039");
```

Jó:

```csharp
var activated =
    _questManager
        .Roderic
        .ActivateAvailableQuests();
```

A rendszer maga ellenőrzi az activation requirementeket.

---

# 14. Story state követelmények

Példa:

```csharp
new QuestActivationRequirement.StoryStateEquals(
    QuestStoryState.Trusted);
```

Ez csak az aktiválást szabályozza.

Fontos:

> Ha a quest már `Active`, a story state későbbi megváltozása nem kapcsolja ki.

---

# 15. Progress események

A világ tényeket közöl.

A quest rendszer dönti el, melyik quest számára relevánsak.

Ez kulcsfontosságú elv.

A `Game` NE ilyen logikát írjon:

```csharp
if (quest.Type == Kill &&
    enemy.Id == quest.TargetId)
{
    quest.Progress++;
}
```

Hanem:

```csharp
_questManager.RegisterQuestKill(
    defeatedEnemy);
```

A `QuestProgressEngine` eldönti:

- melyik aktív questet érinti;
- jó-e az enemy;
- rendelkezik-e a szükséges traittel;
- jelen van-e a required follower;
- megfelelő-e a story state;
- megfelelő-e a távolság.

---

# 16. Publikus progress API

## Enemy kill

```csharp
var changes =
    _questManager.RegisterQuestKill(
        defeatedEnemy);
```

Tipikus használat:

```csharp
ProcessQuestProgressChanges(
    _questManager.RegisterQuestKill(
        defeatedEnemy));
```

## Trap

```csharp
ProcessQuestProgressChanges(
    _questManager.RegisterQuestTrapDisarmed());
```

## Chest

```csharp
ProcessQuestProgressChanges(
    _questManager.RegisterQuestChestOpened());
```

## Location

```csharp
ProcessQuestProgressChanges(
    _questManager.RegisterQuestLocationReached(
        QuestLocation.Exit));
```

## Inventory változás

```csharp
ProcessQuestProgressChanges(
    _questManager.RegisterQuestInventoryChanged(
        item));
```

---

# 17. Collect questek külön szabálya

A collect quest progress nem azt jelenti:

> ennyi tárgyat szedtél fel a quest kezdete óta

hanem:

> jelenleg ennyi megfelelő tárgy van a party inventoryban

Ezért:

```csharp
_questManager.SynchronizeCollectQuests();
```

újraszámolja az aktuális progresszt.

Példa:

```text
3/3 → ReadyToTurnIn

a játékos elhasznál egy tárgyat

2/3 → Active
```

A leadáskor a szükséges tárgyakat a completion processor fogyasztja el.

---

# 18. QuestProgressChange

A progress API-k visszaadják a változásokat.

Példa:

```csharp
foreach (var change in changes)
{
    if (change.BecameReadyToTurnIn)
    {
        // UI értesítés
    }
}
```

Hasznos propertyk:

```csharp
change.QuestId
change.GiverInstanceId
change.PreviousProgress
change.CurrentProgress
change.ProgressDelta
change.PreviousState
change.CurrentState
change.BecameReadyToTurnIn
change.LostReadyToTurnIn
```

A Domain/Application csak a változást adja vissza.

A UI megjelenítés a `Game` feladata.

---

# 19. Quest completion

Egy quest csak:

```csharp
QuestState.ReadyToTurnIn
```

állapotból completelhető.

Példa:

```csharp
if (quest.CanComplete)
{
    var result =
        quest.Complete();
}
```

vagy:

```csharp
var result =
    _questManager.Complete(
        quest.Id,
        quest.GiverInstanceId);
```

A completion során történik:

1. objective végső ellenőrzése;
2. collect itemek elfogyasztása;
3. XP kiosztás;
4. item reward kiosztás;
5. state `Completed`-re állítása.

---

# 20. QuestCompletionResult

A completion nem rajzol UI-t.

Egy eredményt ad vissza:

```csharp
QuestCompletionResult
```

A benne levő:

```csharp
Rewards
```

tartalmazza például:

```csharp
ExperienceAwards
ItemRewards
LevelUpAwards
DroppedItemCount
HasItemRewards
HasLevelUps
```

A `Game` ebből dönt például:

```csharp
foreach (var award in completion.Rewards.LevelUpAwards)
{
    ResolvePerkOffers(
        award.Character,
        award.Result);
}

if (completion.Rewards.HasItemRewards)
{
    PlaySessionSound(
        SoundEffect.Item);
}
```

Ez helyes.

A `QuestRewardService` NE:

- rajzoljon;
- játsszon hangot;
- nyisson perk ablakot.

---

# 21. Quest reward

A:

```csharp
QuestRewardService
```

feladata:

- quest XP szétosztása;
- fix reward itemek kiosztása;
- random rewardok generálása;
- hátizsákba helyezés;
- ha nincs hely, földre ejtés;
- eredmény visszaadása.

A konkrét játékvilág műveleteket az:

```csharp
IQuestRewardContext
```

rejti el.

---

# 22. NPC conversation

A publikus API:

```csharp
_questManager.Roderic.StartConversation();
```

vagy:

```csharp
_questManager
    .For(npcId, instanceId)
    .StartConversation();
```

A `QuestManager` nem rajzol dialógus UI-t.

Ezt az:

```csharp
IQuestNpcConversationService
```

adapteren keresztül továbbítja a meglévő Game conversation rendszernek.

---

# 23. NPC csatlakozhatóság

A quest-rendszer csak azt mondja meg:

```csharp
questNpc.AreAllQuestsResolved
```

A `WorldNpc.Recruitable` továbbra is world/NPC adat.

Ezért a játékoldali döntés például:

```csharp
private bool CanNpcJoin(
    WorldNpc npc)
{
    if (!npc.Recruitable)
        return false;

    var npcId =
        LegacyNpcIdMap.ToQuestNpcId(
            npc.DefinitionId);

    var instanceId =
        _questWorldContext.GetInstanceId(
            npc);

    return _questManager
        .For(npcId, instanceId)
        .AreAllQuestsResolved;
}
```

A `WorldNpc` ne kapjon `QuestManager` függőséget.

---

# 24. UI modell

A renderer ne olvassa:

```csharp
npc.Quests
```

és ne ismerje:

```csharp
NpcQuestState
```

A Game készítsen egyszerű UI projectiont:

```csharp
public sealed record NpcQuestUiEntry(
    string Title,
    QuestState State,
    int Progress,
    int RequiredCount);
```

és ezt adja át:

```csharp
_renderer.DrawWorldNpcRecruitment(
    npc,
    CanNpcJoin(npc),
    GetNpcQuestUiEntries(npc));
```

A renderer csak megjelenít.

---

# 25. NpcQuestCoordinator

A `NpcQuestCoordinator` már nem lehet második quest engine.

Jelenlegi szerepe főleg journal-koordináció.

Nem használhatja:

```text
WorldNpc.Quests
NpcQuestState
NpcQuestProgress
WorldNpc.ActivateQuest()
WorldNpc.CompleteQuest()
WorldNpc.AbandonQuest()
```

A quest állapotot a `QuestManager` / `QuestHandle` felől olvassa.

---

# 26. Quest journal

A journal UI/presentation projection.

Nem elsődleges quest-state tároló.

A helyes irány:

```text
QuestStateStore
    ↓
QuestHandle
    ↓
NpcQuestCoordinator
    ↓
QuestJournalEntrySnapshot
```

Nem:

```text
QuestJournal
    ↓
quest engine
```

Átmenetileg régi mentések miatt még lehet visszafelé bridge, de ezt később el kell távolítani.

---

# 27. Tipikus inicializálás

A `Game` composition rootjában:

```csharp
var questCatalog =
    new QuestCatalogBuilder(
        gameData)
    .Build();

var questStateStore =
    new QuestStateStore(
        questCatalog);

_questWorldContext =
    new MazeQuestWorldContext(
        getMaze: () => _maze,
        countPartyItem: item =>
            CountPartyBackpackItems(item.Id),
        tryConsumePartyItem: (item, amount) =>
        {
            if (CountPartyBackpackItems(item.Id) < amount)
                return false;

            RemovePartyBackpackItems(
                item.Id,
                amount);

            return true;
        },
        instanceRegistry:
            _questNpcInstanceRegistry);

var availabilityService =
    new QuestAvailabilityService(
        questCatalog,
        questStateStore,
        _questWorldContext);

var progressEngine =
    new QuestProgressEngine(
        questCatalog,
        questStateStore,
        _questWorldContext);

var rewardService =
    new QuestRewardService(
        _progressionService,
        rewardContext);

var completionProcessor =
    new QuestCompletionProcessor(
        questCatalog,
        questStateStore,
        progressEngine,
        _questWorldContext,
        rewardService);

_questManager =
    new QuestManager(
        questCatalog,
        questStateStore,
        availabilityService,
        progressEngine,
        completionProcessor,
        conversationService);
```

A konkrét wiring változhat.

A fontos szabály:

> A Game építi össze az objektumgráfot, de gameplay közben a QuestManager façade-ot használja.

---

# 28. Példák a Game felől

## Egy enemy meghal

```csharp
private void RegisterNpcQuestKill(
    Enemy defeatedEnemy)
{
    ProcessQuestProgressChanges(
        _questManager.RegisterQuestKill(
            defeatedEnemy));
}
```

## Aktív Roderic questek

```csharp
var quests =
    _questManager
        .Roderic
        .GetActiveQuests();
```

## Egy konkrét quest

```csharp
var quest =
    _questManager
        .Roderic
        .Quests
        .OathbreakerKnight;
```

## Quest név

```csharp
var title =
    _questManager
        .Roderic
        .Quests
        .OathbreakerKnight
        .Title;
```

## Quest státusz

```csharp
if (_questManager
    .Roderic
    .Quests
    .OathbreakerKnight
    .IsReadyToTurnIn)
{
}
```

## NPC összes questje kész?

```csharp
if (_questManager
    .Roderic
    .AreAllQuestsResolved)
{
}
```

## Általános NPC

```csharp
var npcHandle =
    _questManager.For(
        npcId,
        instanceId);

var active =
    npcHandle.GetActiveQuests();
```

---

# 29. Amit a Game NE csináljon

Ne módosítson közvetlenül quest state-et:

```csharp
runtimeState.State =
    QuestState.Completed; // TILOS
```

Ne dolgozzon legacy string quest ID-val:

```csharp
if (quest.Id == "NPCQ039") // TILOS
```

Ne számolja maga a progresszt:

```csharp
quest.Progress++; // TILOS
```

Ne kérdezze a `WorldNpc`-t quest state-ről:

```csharp
npc.Quests // KIVEZETENDŐ / TILOS
```

Ne hívjon legacy quest state metódusokat:

```csharp
npc.ActivateQuest(...)
npc.AddQuestProgress(...)
npc.CompleteQuest(...)
npc.AbandonQuest(...)
```

Ne használja közvetlenül a belső service-eket gameplayből, ha ugyanaz elérhető a façade-on:

```csharp
_progressEngine.Process(...) // kerülendő
```

helyette:

```csharp
_questManager.RegisterQuestKill(...)
```

---

# 30. Amit az UI NE csináljon

A renderer ne:

- keressen quest definíciókat;
- mapeljen string ID-ket;
- olvassa a `WorldNpc.Quests`-ot;
- döntsön aktiválhatóságról;
- számoljon progresszt;
- completeljen questet;
- osszon jutalmat.

Az UI kész modellt kapjon.

Példa:

```csharp
NpcQuestUiEntry
QuestJournalEntrySnapshot
QuestCompletionResult
```

---

# 31. Amit az Infrastructure NE csináljon

Ne legyen benne:

```text
Console.Write
Renderer
QuestCompletionWindow
perk ablak
coop publish
játéküzenet
```

Az Infrastructure adapter, nem játékvezérlő.

---

# 32. Függési összefoglaló

## Domain

Láthat:

```text
Domain
```

Nem láthat:

```text
Application
Infrastructure
Game
UI
CSV
legacy string ID
```

## Infrastructure

Láthat:

```text
Domain
GameData
World
legacy formátumok
```

Feladata:

```text
mapping
adapter
context
catalog build
legacy integration
```

## Application

Láthat:

```text
Domain
absztrakciók
```

Feladata:

```text
quest lifecycle
availability
progress
completion
reward orchestration
publikus façade
```

## Game

Láthat:

```text
QuestManager
QuestHandle
QuestNpcHandle
eredmény DTO-k
UI projection
```

Feladata:

```text
input
UI
hang
ablakok
perk választás
coop publish
játékfolyam orchestration
```

---

# 33. Migrációs állapot

A régi rendszerből jelenleg fokozatosan kivezetendő:

```text
WorldNpc.Quests
NpcQuestProgress
NpcQuestState

WorldNpc.ActivateQuest()
WorldNpc.AddQuestProgress()
WorldNpc.CompleteQuest()
WorldNpc.AbandonQuest()
WorldNpc.RestoreQuests()
```

Átmeneti bridge-ek még létezhetnek:

```text
SynchronizeLegacyQuestProgress()
legacy save/load
legacy coop snapshot
legacy journal kompatibilitás
```

A végállapot:

```text
QuestStateStore = egyetlen quest runtime igazságforrás
```

---

# 34. Következő architekturális célok

A jelenlegi migráció után:

1. save/load átállítása `QuestStateStore` export/restore-ra;
2. `QuestNpcInstanceRegistry` mentése;
3. coop snapshot typed quest state-re átállítása;
4. `SynchronizeLegacyQuestProgress()` eltávolítása;
5. `WorldNpc.Quests` teljes törlése;
6. `NpcQuestState` és `NpcQuestProgress` törlése;
7. legacy quest metódusok törlése a `WorldNpc`-ból.

---

# 35. Rövid szabály

Ha questtel kapcsolatos gameplay kódot írsz, először ezt kérdezd:

> Meg tudom ezt csinálni a `QuestManager`, `QuestNpcHandle` vagy `QuestHandle` segítségével?

Ha igen, azt használd.

Ha nem, akkor inkább a `QuestManager` publikus API-ját bővítsd, mintsem megkerüld a quest-réteget.

A cél:

```text
Game
  ↓
QuestManager
  ↓
Quest services
  ↓
Domain
```

és ne:

```text
Game
  ├─ QuestStateStore
  ├─ QuestProgressEngine
  ├─ WorldNpc.Quests
  ├─ CSV quest ID-k
  └─ saját quest logika
```
