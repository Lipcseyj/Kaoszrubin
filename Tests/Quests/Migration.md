# Questmigráció – végrehajtási napló

## 2026-09-13: az 1. lépés átvezetése elkészült

Kiinduló commit: `e0a913f` (`Added tests for QuestManager`), tiszta munkafa.
A 0. lépés alább történeti feljegyzés; az aktuális következő lépés a 2., a tartós
questazonosság és a mentés/betöltés átvezetése.

### Módosítások

- Az `ActivateNpcQuest`, `AbandonQuest`, `AbandonActiveQuestsFromNpc` és
  `ResolveTemporaryFollowerAtExit` már a `QuestManager`/handle API-t használja.
  A feladás azonnal Failed állapotot és friss naplót eredményez, élő NPC hiányában is.
- A `QuestManager.QuestChanged` szinkron értesíti a projekciókat aktiválás,
  progress, feladás és leadás után. Az aktiválás, `CanComplete`, illetve leadás
  belső collect-szinkronja is eljut a naplóhoz. A leadás fogyasztása és jutalma
  a többi aktív collect küldetést is frissíti.
- A `NpcQuestCoordinator` egyirányú naplóprojekció: sem a régi naplóállapot,
  sem egy kívülről megadott progress nem írhatja át a typed állapotot.
  A régi `WorldNpc`-tároló írása a külön `LegacyQuestProgressProjection` adapterbe került.
- A `QuestActivationKind.Story` elkülöníti a konkrét történeti indítást az
  általános NPC-ajánlattól. Roderic proof és insignia küldetése a választás
  **utáni** állapotban aktiválható; a közös pengepróba FOLLOWING állapotban
  ajánlható fel, Malrec küldetése a MALREC_APPROACH állapotban, induláskor nyílik.
  A proof/insignia történeti továbblépés tényleges Completed állapotot ellenőriz,
  ezért a leadás elhalasztása nem viszi tovább a párbeszédet.
- A felfedezés `LocationDiscoveredEvent`, a konkrét kísérő célba érkezése
  `NpcReachedLocationEvent`. A korábbi `LocationReachedEvent` csak explore
  célhoz számít be. A kíséréshez a megfelelő NPC-példány, élő követő és a
  kijárattól legfeljebb három mezős Manhattan-távolság szükséges; az eseményt
  a tényleges kijárathasználat küldi. Ez megőrzi a Game meglévő közelségi szabályát.
- A már felfedezett helyre utólag felvett explore küldetés azonnal leadható.
  A felfedezettséget az aktuális világadapter adja, így új pályán nem öröklődik
  automatikusan a korábbi kijárat ismerete.
- A `QuestInventorySynchronizer` a parti tagjait és inventory-revízióit figyeli.
  A Game parancsfeldolgozás után, snapshot/mentés/napló készítése előtt és a
  frissítést kérő műveletek végén egyeztet. Az összetett inventoryművelet végső
  állapota számít, így partin belüli áthelyezés nem jelent hamis visszaesést.
  A progress visszajelzése tájékoztató; önmagában nem nyit leadási ablakot.
- A valódi világadapter tesztje feltárt egy korábbi `InvalidCastException` hibát:
  a `Distinct(ReferenceEqualityComparer.Instance)` eredménye `object` típusra
  következtetett. Az explicit `Distinct<WorldNpc>` javítja az NPC-feloldást.

### Ellenőrzés

Nyolc új regressziós teszt ellenőrzi a valódi CSV-ből felépített Roderic-kapukat,
az explore/escort különbséget és utólagos felvételt, a kísérő példányát,
a valódi világadapter közelségi/életfeltételét, a visszaírásmentes naplót,
a leadás más collect küldetésekre gyakorolt hatását és a valódi karakter-inventoryk
változásait, beleértve a partitagságot és az áthelyezést.

| Ellenőrzés | Előtte | Utána |
|---|---:|---:|
| Fordítási hiba | 0 | 0 |
| CS0618 warning | 43 | 35 |
| Sikeres teszt | 248 | 256 |
| Hibás teszt | 0 | 0 |

A teljes solution nem inkrementális fordítása és a teljes tesztprogram futott,
a 0. lépésnél leírt parancsokkal. A `git diff --check` hibamentes.

A maradék warningok: Game 12, WorldSnapshots 3, GameStateMapper 3,
GameSaveService 1, WorldNpc 2, Program tesztek 8, a külön kompatibilitási
projekció 6. Nincs warningelnyomás. A forráskeresés szerint az alkalmazásban
legacy questmutátor-hívás már csak a `LegacyQuestProgressProjection` adapterben
és a `GameStateMapper` betöltőjében maradt.

### Megmaradt határok és következő munka

Ez még nem az első kiadható mérföldkő: a typed állapot tartós mentése,
importja és az NPC-identitás megőrzése továbbra is a **2. lépés** feladata.
A régi napló visszaírásának eltávolítása után különösen fontos, hogy a régi
mentések lezárt/feladott állapotát az importer rekonstruálja. Jutalmazó adapter
kivételénél az atomi leadás továbbra sincs megoldva.

A napló/kiválasztás még QuestId-alapú. Feladáskor a kiválasztott közös sorhoz
tartozó aktív futások mind lezárulnak; a külön NPC-példány célzott kiválasztása
a 3. lépésben, a napló kulcsának átvezetésével készül el. A korábbi pályák aktív
küldetéseinek progressz-szabálya itt nem változott.

A teljes történeti és követői UI-folyamat nem kapott interaktív végigjátszást.
A forrásvizsgálat a követői leadási értesítés mellett további régi hiányt talált:
a közös pengepróba utáni TRUSTED és a Malrec utáni MALREC_DEFEATED átmenet
korábbi kódja már az `e45642c` migrációs commitban eltűnt; a jelenlegi pending
questfeldolgozási halmaznak sincs hozzáadó hívása. Ezeket a történeti/UI
átvezetésben (4. lépés) a tényleges typed lezáráshoz kell kötni; a 7. lépésben
a szükségtelenné váló pending út törölhető. Az új tesztek a kapukat és a questmagot
igazolják, nem a teljes Roderic-történet végigjátszhatóságát.

## 2026-09-13: a 0. lépés elkészült

Terv: `C:/Users/rockm/Documents/Codex/2026-09-12/h/outputs/quest-migracios-terv.md`.
Kiinduló commit: `0913a64` (`Introducing QuestManager to Game`), tiszta munkafa.

Ez a változtatás a tesztalapot készíti elő. Az alkalmazás futásidejű működését,
a mentés formátumát és a kompatibilitási bridge-eket még nem módosítja.
Az 1–7. lépés továbbra is nyitott; ez önmagában nem a terv első kiadható migrációs mérföldköve.

### Ellenőrzött tesztalap

A `QuestTestFixture` kis, memóriabeli világ-, inventory-, jutalom- és beszélgetési
adapterekkel állítja össze a valódi `QuestManager`, `QuestStateStore`,
`QuestAvailabilityService`, `QuestProgressEngine`, `QuestCompletionProcessor` és
`QuestRewardService` szolgáltatásokat. A fixture nem tölt CSV-t, nem indít Game-et
vagy UI-t, és nem használ legacy quest API-t. Minden teszt saját állapotot kap.

A `QuestManagerTests` 12 külön regisztrált tesztje a publikus manager/handle API-kon ellenőrzi:

- a történeti kapu megnyílását, visszazárását és az aktív állapot megőrzését;
- a zárolt és feladott küldetések kizárását a tömeges aktiválásból;
- az objective-hoz tartozó eseményeket, az aktív állapotot és a progress felső határát;
- a collect kezdeti inventoryját és a 2/3 → 3/3 → 2/3 változásjelzéseket;
- a mellékhatásmentes leadhatóság-ellenőrzést, az egyszeri fogyasztást,
  XP-elosztást és fix/véletlen tárgyjutalmat, telt hátizsák esetén is;
- a leadás előtt megváltozott inventory újraellenőrzését;
- a sikertelen tárgyelvétel jutalom nélküli elutasítását és az újrapróbálást;
- az Active és ReadyToTurnIn állapotból történő azonnali feladást;
- két azonos NPC-típus külön progressét, leadását és feladását;
- a globális quest azonosságát és a hibás NPC/hiányzó instance elutasítását;
- a beszélgetés NPC-példányazonosítójának továbbítását;
- a follower kill célpontját, részvételi és történeti feltételét, valamint az adapternek átadott távolsághatárt.

A follower teszt az adapter felé továbbított szerződést vizsgálja; a tényleges
térképi távolság és az élő Game integrációjának ellenőrzése későbbi lépés.
A tömeges aktiválás tesztje meglévő aktiválási feltételekre vonatkozik: a CSV-ben
feltétel nélkül szereplő Roderic questek történeti indítása még nincs rendezve.

### A korábbi két teszthiba oka

1. Az NPC-párbeszédek tesztje 73 bejegyzést várt, de az `81becb5`
   (`Improved npc conversations`) commit eltávolította az NPCD070–NPCD073 sorokat.
   A helyes jelenlegi darabszám 69. A párbeszédek, történeti választások, questek
   és az egyes questtípusok darabszáma külön, várt/tényleges értéket tartalmazó
   ellenőrzést kapott. A legacy NPC-életciklus külön tesztként fut, ezért egy
   CSV-eltérés többé nem akadályozza meg a végrehajtását.
2. Külön, questtől független tesztkarbantartás: a `6aa359a` commit szándékosan
   növelte a csapdaszámokat. Az 1., 10. és utolsó szint tesztelvárása ehhez
   igazodott: 3–7, 5–10, illetve 6–13. A játékmeneti konfiguráció változatlan.

### Fordítási és teszteredmények

Parancsok a repository gyökeréből:

```powershell
dotnet build 'KáoszrubinApps.slnx' --no-restore --no-incremental -v:minimal
& '.\Tests\bin\Debug\net10.0-windows\KaoszrubinTests.exe'
```

| Ellenőrzés | Előtte | Utána |
|---|---:|---:|
| Fordítási hiba | 0 | 0 |
| CS0618 warning | 43 | 43 |
| Sikeres teszt | 233 | 248 |
| Hibás teszt | 2 | 0 |

A 13 új regisztráció: 12 típusos teszt és a különválasztott legacy életciklusteszt.
Az összes figyelmeztetés meglévő legacy használatból ered; nincs új `NoWarn`
vagy figyelmeztetést elnyomó pragma. A build összegzésében ismétlődő sorok
duplikáció nélkül számolandók.

| Fájl | CS0618 kiindulópont és jelenlegi érték |
|---|---:|
| `Káoszrubin/Application/Game.cs` | 26 |
| `Káoszrubin/Application/WorldSnapshots.cs` | 3 |
| `Káoszrubin/Data/GameStateMapper.cs` | 3 |
| `Káoszrubin/Data/GameSaveService.cs` | 1 |
| `Káoszrubin/World/WorldNpc.cs` | 2 |
| `Tests/Program.cs` | 8 |

### Következő lépés

Az 1. lépésben az `ActivateNpcQuest`, `AbandonQuest`,
`AbandonActiveQuestsFromNpc` és `ResolveTemporaryFollowerAtExit` útvonalakat
kell az új API-ra vezetni, a történeti aktiválás és az automatikus felvétel
szétválasztásával. Az explore/escort események, a már felfedezett kijáratra
utólag felvett quest és az inventoryváltozások projekciója szintén ide tartozik.

Az egyszeri jutalmazás tesztje a sikeres leadás megismétlését védi. A jutalmazó
adapter által dobott kivétel esetén a fogyasztás/XP/tárgyjutalom még nem atomi;
ezt a mostani csomag nem javította, és nem rögzíti helyes viselkedésként.

Az első kiadható mérföldkő továbbra is az 1–2. lépés együtt: a típusos állapot
legyen az egyetlen játékmeneti állapotforrás, stabil NPC-azonossággal és működő
régi/új mentésbetöltéssel. A jelenlegi zöld tesztek ezt még nem igazolják.
