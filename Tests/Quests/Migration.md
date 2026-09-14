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

## 2026-09-13: a 2. lépés – tartós questazonosság és mentés/betöltés

A megvalósítás a `doc/quest-readme.md` réteghatárait követi: a Game a
QuestManager export/restore API-ját használja, a mentési séma és a régi adatok
értelmezése az infrastruktúrában lévő QuestSaveAdapter feladata.

### Elkészült változtatások

- `QuestKey` és változatlan `QuestStateSnapshot`; a teljes bemenet ellenőrzése
  után végrehajtott állapotcsere. Hibás kulcs, scope, progress, állapot,
  completion count és duplikált futás nem okoz részleges állapotbetöltést.
- 22-es mentési verzió, külön `Quests` blokk stabil szöveges quest/NPC ID-kkal
  és állapotnevekkel, normalizált instance ID-val és completion counttal.
  A kijelzési adatok és a tényleges jutalmazás szöveges összegzése futásonként
  megmarad akkor is, ha az NPC már nincs a világban.
- Az NPC registry a tartós CharacterId-hoz rendel questpéldány-azonosítót.
  A mentés a világ NPC-inél mindkettőt tárolja; a korábban eltávozott NPC-k
  registry-bejegyzései megmaradnak, az ID-k nem használódnak fel újra.
- Betöltéskor az adapter előkészíti és ellenőrzi az identitásokat és állapotokat,
  majd a mapper felépíti a világot. A Game ezután állítja vissza a típusos
  állapotot, egyezteti az aktív collect objective-okat a betöltött inventoryval,
  és állítja elő a naplót/kompatibilitási projekciókat. Nincs aktiválás,
  tárgyfogyasztás vagy jutalmazás a restore során.
- A GameStateMapper többé nem olvassa vissza az NPC-k flat questprogressét.
  A régi adat csak a migrációs adapter bemenete; a WorldNpc-nézetet a manager
  állapotváltozásai töltik fel.
- A felfüggesztett kampány visszaállítása a világot cseréli; az aktuális
  questállapotot nem tölti vissza a régebbi snapshotból. Az új WorldNpc objektum
  ugyanahhoz a karakterhez ugyanazt az instance ID-t kapja. Egy közben végleg
  eltávozott követőt a régi pillanatkép nem hoz vissza.

### Régi mentések egyeztetési szabályai

A hiányzó/null `Quests` blokk egyszeri legacy importot jelöl. Ez megmarad akkor
is, ha a save editor a mentést már 22-es verzióval írja vissza. Jelen lévő, de
érvénytelen típusos blokk nem esik vissza legacy adatokra.

Offered → Locked/Available, Active → Active/ReadyToTurnIn, Completed →
Completed és completion count 1, Abandoned → Failed. Collect esetén a betöltött
készlet határozza meg az aktív futás tényleges progressét.

Ugyanazon NPC több világpillanatképének, valamint a globális questek régi
naplóadatainak egyeztetésében: Completed > Failed > Active/Ready > Offered;
azonos rangnál a nagyobb progress marad. Az eltérések migrációs megjegyzést kapnak.
PerNpcInstance questnél több vagy nulla lehetséges NPC esetén a régi napló
archívumba kerül. Egyetlen lehetséges NPC mellett is annak saját állapota és
progresse az elsődleges; eltérő állapotú naplótörténetet archiválunk. Egyező
állapotnál a napló kijelzési/jutalomtörténeti adatai átvehetők. Ismeretlen régi
naplóazonosító szintén megmarad az archívumban. Hibás NPC-azonosság vagy ismeretlen
NPC-hez kötött questrekord esetén az import hibát jelez.

### Ellenőrzés és a következő lépés

A 12 új, külön regisztrált QuestPersistenceTests teszt az állapotimportot,
a karakter- és JSON-mentési kört, a collect-egyeztetést, a jutalommentes
betöltést, a külön NPC-példányokat, a legacy ütközéseket/archívumot, a hibás
típusos adatok elutasítását, a felfüggesztett világot és a flat kimeneti projekciót
ellenőrzi. Az új karakterlista valóban deszerializált LiveCharacter objektumokat
használ. A Malrec-visszatérési teszt a valódi mapper/registry/manager/adapter
láncot vizsgálja; interaktív Game/UI- és teljes történeti végigjátszás nem történt.

- Teljes solution build: 0 hiba, 33 CS0618 figyelmeztetés (kiindulás: 35).
- Teljes tesztkészlet: 268 PASS, 0 FAIL (kiindulás: 256 PASS).
- `git diff --check`: nincs whitespace-hiba.
- Nincs új figyelmeztetés-elnyomás; négy legacy enumhivatkozás az importerben
  marad, a Game figyelmeztetései 12-ről 7-re, a mapperéi 3-ról 2-re csökkentek.

Következő: a terv 3. lépése, a napló, UI-kiválasztás és gyorsutazás átállítása
QuestKey-re. A mostani mentés külön tárolja a futásokat, de a régi stringkulcsú
UI-napló még összevonhat azonos QuestId-jú sorokat. A coop szerződés és a
Roderic/Elira történeti lezárások teljes átvezetése a terv későbbi lépéseiben marad.

## 2026-09-13: a 3. lépés – napló, kiválasztás és gyorsutazás

A közös questfolyamatok átálltak QuestKey-re. A felhasználó kérésére Roderic
küldetéseinek történetspecifikus átvezetése alacsonyabb prioritású, mert ezek a
küldetések át lesznek dolgozva. Ebben a lépésben nem változtattunk Roderic történetén.

### Elkészült működés

- A napló és a naplóablak kiválasztási eredménye pontos futáskulcsot használ.
  Két azonos QuestId-jú, külön NPC-hez tartozó quest külön sor és külön feladás.
  A manager TryGetQuest lekérdezése hiányzó kulcshoz nem hoz létre új állapotot.
- A naplóprojekció csak a típusos állapotból dolgozik. A korábbi stringkulcsú
  segédútvonalakat eltávolítottuk; a jutalomösszegzések is futásonként maradnak meg.
  A mentés a naplóból kizárólag kijelzési metaadatot vesz át, állapotot/progresst nem.
- A QuestTravelService a típusos leadhatóságból készít opciókat. Az opció a
  QuestKey mellett a konkrét cél-NPC azonosítóját is őrzi, globális questnél is.
  Végrehajtás előtt újra ellenőrzi a készletet/állapotot, az élő és jelen lévő
  questadót, az útvonalat és annak aktuális költségét. A Game csak a kiválasztott
  questet ajánlja fel leadásra, nem az NPC összes kész küldetését.
- A fogadói visszatérési indok tényleges aktív futást és a jelenlegi pályán
  elérhető konkrét questadót keres, a régi napló/CSV archetípus-egyeztetése helyett.
- A hátrahagyott aktív questek továbbra is megkapják a rájuk illeszkedő globális
  eseményeket és inventoryváltozásokat; ez a meglévő engine viselkedése maradt.
  Távoli, nem jelen lévő questadóhoz nincs gyorsutazási opció. A naplótörténet
  NPC hiányában is megmarad.
- A közös session-napló kulcsát saját JSON-konverter viszi át stabil szöveges
  külső quest-ID-val és numerikus instance ID-val. A vendég ismert/teljesített
  questhalmazai is QuestKey-t használnak. A journal DTO változásakor a protokoll
  79-ről 80-ra lépett; a közben érkezett zenei módosításokkal jelenleg 81.
  A WorldNpc wire-progress teljes kiváltása továbbra is az 5. lépés része.

### Tesztek és közben érkezett commitok

A hat új QuestJournalTests teszt: két sor és célzott feladás; konkrét NPC és
friss utazási költség; collect-visszaellenőrzés; globális quest konkrét fizikai
célpontja; külön jutalomtörténetek mentése NPC nélkül; teljes/delta/reconnect
session-napló és a webes JSON-beállítások kulcsmegőrzése. Hibás, hiányzó,
ismeretlen és nem numerikus wire-kulcsadatok elutasítását is ellenőrizzük.

A felhasználó munka közben a 476d48f commitba mentette a questmódosításokat,
és CSV/zenei módosítások is érkeztek. A végső ellenőrzés a 41ab24d állapotra
épülő munkafán történt. Az öt kezdeti új teszthibát a lefoglalt bejáratra
helyezett teszt-NPC okozta; a fixture szabad cellára került. Két meglévő CSV-teszt
vesszős sorokra keresett a már pontosvesszős fájlban, ezért nem módosított tesztadatot.
A tesztek most a tényleges elválasztót használják; a játékadatokat nem írtuk át.

- Teljes solution build: 0 hiba, 28 CS0618 (a 3. lépés előtt 33).
- Teljes tesztkészlet: 274 PASS, 0 FAIL (előtte 268 PASS).
- `git diff --check`: nincs whitespace-hiba.
- Friss futási naplók: `Káoszrubin/quest-step3-build.log`, `Káoszrubin/quest-step3-tests.log`.
- Interaktív Game/UI-végigjátszás nem történt; a célzott műveleteket és adatcserét
  komponens- és integrációs tesztek fedik.

Következő a 4. lépés közös megjelenítési/leadási rétege és Elira átvezetése.
Roderic speciális történeti részeit az átdolgozás miatt későbbre soroljuk.

## 2026-09-13: a 4. lépés közös megjelenítési/leadási rétege és Elira

- A QuestPresentationSnapshot változatlan, futáskulcsos megjelenítési adatot ad
  az ajánlati és leadási ablakoknak. A renderer és a QuestTurnInWindow nem kap
  NpcQuestDefinition objektumot vagy élő quest handle-t.
- Elira és az általános egyedi NPC ajánlati ablaka csak a ténylegesen aktivált
  questeket mutatja. Az Elira-szöveg többé nem állít rögzített három ajánlatot.
- A QuestTurnInService a megerősítést és a tényleges leadást választja szét.
  Elutasításkor nincs fogyasztás/jutalom; elfogadás után a handle ellenőrzi a
  friss készletet és állapotot. A host közös ablaka, coop értesítései, jutalmazás
  utáni level-up kezelése és összegzése megmaradt.
- Elira CanResolveDeparture feltétele a Rescue Completed állapota. Ready,
  elhalasztott vagy feladott állapot nem nyitja meg a kijárati búcsúzást vagy
  végleges csatlakozást, és nem adja meg az ehhez kapcsolódó barátságbónuszt.
  A pályaváltás meglévő követőátviteli útvonala ilyenkor is továbbviszi Elirát.
- A Game utolsó WorldNpc.Quests-olvasása (Roderic régi Offered ellenőrzése)
  mechanikusan a típusos Locked/Available állapotokra került. Roderic történetét
  nem dolgoztuk át, speciális történeti végigjátszása továbbra is későbbi feladat.
- Törölve a GetLegacyQuestDefinition és a hívó nélküli flat jutalmazó/leíró
  segédek. A NPC-spawn CSV/definíciós határa a 6. lépésre marad.

Négy új QuestPresentationTests teszt ellenőrzi a snapshot változatlanságát és
ablaktartalmát, a leadás elhalasztását/elfogadását, a megerősítés alatt változó
készletet, valamint Elira célba érés → elhalasztás → tényleges leadás állapotsorát.
A feladás sem számít sikeres mentésnek. Teljes build: 0 hiba, 26 CS0618
(kiindulás 28). Teljes tesztkészlet: 278 PASS, 0 FAIL (kiindulás 274).
`git diff --check`: nincs whitespace-hiba. Interaktív UI-végigjátszás nem történt.

A felhasználó által hátrébb sorolt Roderic-specifikus munka nyitott marad.
Következő közös migrációs feladat az 5. lépés: a WorldSnapshots flat NPC
questprogressének kiváltása; a session-napló kulcsosítása már a 3. lépésben elkészült.

## 2026-09-13: az 5. lépés – típusos NPC-questadatok a replikációban

- A WorldNpcSnapshot flat QuestIds/NpcQuestProgress mezőit a managerből készített
  WorldQuestSnapshot váltotta fel: stabil QuestKey, típusos állapot, haladás,
  szükséges mennyiség és teljesítésszám. A fizikai questadó példányazonosítója
  külön is szerepel. A projekció nem aktivál és nem jutalmaz.
- A megváltozott wire-séma miatt a session-protokoll 81-ről 82-re lépett.
  A questkulcs meglévő JSON-konvertere stabil külső quest-ID-t visz át;
  az állapot szöveges enumként kerül a hálózatra. A mentésverzió változatlan.
- A láthatósági szűrés a questadatok lekérdezése előtt történik. A követő
  helyét a party-avatar adja, az NPC és a questpéldány azonossága megmarad.
- A questlista rendezett, az NPC-delta tartalom szerint hasonlítja össze:
  változatlan adatok új listapéldánya nem eredményez felesleges upsertet.
- A vendég értesítéskövetése külön tesztelhető komponensbe került.
  A kezdeti történet néma; az ismételt frame, teljes resync és új kapcsolat
  nem játssza vissza a már látott lezárást. A jutalmazás a hoston marad.

Öt új QuestReplicationTests teszt fedi a JSON-körutat, a külön NPC-példányokat,
a változatlan és célzott deltát, a láthatóságot és követővé válást, valamint
a teljes/delta/resync/reconnect adatfolyamot és az értesítések ismétlésvédelmét.
A korábbi protokoll elutasítását is ellenőrizzük.

- Teljes solution build: 0 hiba, 23 CS0618 (kiindulás 26).
- Teljes tesztkészlet: 283 PASS, 0 FAIL (kiindulás 278).
- Interaktív UI- és külön gépes hálózati végigjátszás nem történt.

Következő a 6. lépés: a flat CSV/definíciós határ átvezetése.
Roderic történeti átdolgozása továbbra is későbbi prioritás.

## 2026-09-14: a 6. lépés – a flat definíciós köztes modell leválasztása

- A CsvGameDataLoader a betöltés végén egyszer felépíti és ellenőrzi a típusos
  GameDataCatalog.Quests katalógust. A célpontok és jutalmak feloldott definíciók.
- A publikus NpcQuestDefinition, NpcQuestType, NpcQuests és GetNpcQuests megszűnt.
  A QuestImportRow/QuestImportType belső, csak betöltés alatt élő köztes modell;
  a QuestCatalogBuilder szintén belső importadapter, nem játékmeneti API.
- A Game, a napló és a mentési adapter a kész típusos katalógust használja.
  A napló NPC nélkül a típusos giverből kér nevet. Az NPC-k külső azonosítóinak
  visszaalakítása a LegacyNpcIdMap része lett.
- A még létező WorldNpc-tükör inicializálásához szükséges stringes questlista
  előállítása a LegacyQuestProgressProjection kompatibilitási határába került.
  A tükör és mutátorai eltávolítása továbbra is a 7. lépés feladata.
- Roderic történeti működését nem dolgoztuk át. Az eddigi aktiválási és
  Malrec-követőfeltételek változatlanok; meglévő tesztjei típusos definíciót olvasnak.
- A README inicializálási példája a gameData.Quests belépést mutatja.
  Sem CSV-tartalom, sem mentés-/hálózati verzió nem változott.

Két új QuestCatalogImportTests teszt ellenőrzi mind a 40 quest és 21 NPC
teljességét és az ID-k körbefordulását, a scope-ot, minden objective célpontját,
darabszámot, címet, leírást és jutalmat; továbbá az ismeretlen ID/cél/jutalom,
hibás fix jutalommennyiség, üres cím és duplikált quest betöltéskori elutasítását.

- Teljes solution build: 0 hiba, 23 CS0618 (változatlan).
- Teljes tesztkészlet: 285 PASS, 0 FAIL (kiindulás 283).
- Az első, sandboxban futó tesztkörben a csatanapló-teszt nem tudott a Tests
  kimeneti könyvtárába írni. A szükséges jogosultsággal a teljes készlet sikeres;
  emiatt alkalmazáskódot nem módosítottunk.
- Interaktív végigjátszás nem történt.

Következő a 7. lépés: a runtime bridge-ek és a WorldNpc legacy questállapotának
eltávolítása, a régi mentések importkompatibilitásának megtartásával.
