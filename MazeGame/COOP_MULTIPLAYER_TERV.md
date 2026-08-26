# Coop multiplayer műszaki és játékrendszer-terv

## 1. Rövid döntési összefoglaló

A játék első coop változata két játékost támogasson:

- az első játékos a **host és party-leader**;
- a második játékos egy, a host mentésében már létező, nem-vezér partitag irányítását veszi át;
- a host futtatja az egyetlen hiteles játékszimulációt; a kliens csak parancsokat küld;
- a világ minden résztvevő számára azonos, nincs kliensoldali találgatás vagy külön világpéldány;
- a host ment, tölt vissza és birtokolja a kampányt; a vendég karaktere is ebben a mentésben él;
- kapcsolatvesztéskor a vendég karakterét az NPC-AI átveszi, visszacsatlakozáskor ugyanaz a játékos ugyanazt a karaktert kapja vissza;
- első szállítható változatként LAN/direct-IP kapcsolat készüljön, de ugyanazzal a protokollal;
- a végleges internetes meghívás egy kis nyilvános **rendezvous/relay** szolgáltatást használjon, amelyhez mindkét gép kifelé nyit kapcsolatot.

Ajánlott kommunikáció: **ASP.NET Core SignalR WebSocket transporttal**, kezdetben JSON, később szükség esetén MessagePack. A gameplayhez nem javasolt külön REST, gRPC vagy kézzel írt TCP protokoll.

## 2. Mire építhetünk a jelenlegi kódban

A meglévő modell meglepően sok coop-alapot már tartalmaz:

- a `Party` legfeljebb négy külön `LiveCharacter` példányt kezel;
- minden társ saját `PartyMemberAvatar`, pozíció, mozgási időzítő, HP, manna, szükséglet, inventory és aktív hatás birtokosa;
- az NPC-k külön is találkozhatnak ellenféllel, és saját csatát futtathatnak;
- a ködöt minden partitag látótere felfedi;
- a teljes party, a karakterpozíciók, az ellenfelek és a legtöbb időzített állapot menthető;
- az inventory már támogatja a karakterek közötti atomi tárgycserét.

A fő akadályok:

1. A `Game` egyszerre szimuláció, inputkezelő, use-case réteg és UI-koordinátor. Sok állapotot közvetlenül módosít.
2. Számos képernyő és harci lépés blokkoló `Console.ReadKey` ciklust futtat. Ilyenkor nem dolgozható fel hálózati üzenet, timeout vagy disconnect.
3. A `BattleSystem.Resolve` egy teljes csatát szinkron módon futtat végig callbackekkel. Nincs tartós, lépésenként folytatható `BattleState`.
4. A renderer nem pusztán megjelenít: több helyen választást kér és eredményt ad vissza.
5. A futásidejű entitásokat többnyire objektumhivatkozás vagy roster-index azonosítja. Hálózaton stabil `EntityId` szükséges.
6. Nincs automatikus tesztprojekt. Egy hálózati refaktor előtt ez nagy regressziós kockázat.
7. A véletlen dobások egy közös `Random` példányból származnak. Ez host-authoritative működésben helyes, de csak a host hívhatja.

Következmény: a hálózati kódot nem közvetlenül a jelenlegi `Game.Run` ciklusba kell belefűzni. Előbb egy inputtól és renderertől elválasztott, parancsokat fogadó `GameSession` szükséges.

## 3. Játékos- és karaktertulajdon

Minden partitaghoz tartozzon vezérlési állapot:

```text
CharacterControl
  CharacterId
  ControllerKind = HostPlayer | RemotePlayer | Npc
  PlayerId?       = csak emberi vezérlésnél
  ConnectionState = Connected | Reconnecting | Disconnected
```

Szabályok:

- A party első tagja mindig a hosthoz tartozik, ezt a szerepet játék közben nem lehet átadni.
- A vendég egy élő, nem-vezér NPC-t vehet át. Saját gépről hozott karakter az első verzióban ne legyen: ez duplikációs, balansz- és mentéstulajdonlási problémákat nyitna.
- A host választja ki vagy hagyja jóvá, melyik NPC-t veszi át a vendég.
- Ember által vezérelt karaktert nem lehet kirúgni, lecserélni vagy toborzáskor felülírni.
- A karakter NPC-profilja megmarad, de emberi vezérlés alatt inaktív. Disconnectkor ez a profil újra aktiválódik.
- A vendég csatlakozhat menet közben, de az irányításátadás csak **biztonságos ponton** történjen: felfedezés közben, amikor a karakter nem csatázik és nincs globális döntés. Addig a vendég megfigyelő képernyőt kap.
- Halott vagy pálya végén végleg elvesztett vendégkarakter esetén a vendég megfigyelő lesz; új karaktert a következő fogadóban kaphat.

## 4. Host-authoritative szimuláció

A kliens soha ne küldjön új pozíciót, HP-t, aranyat vagy inventoryállapotot. Csak szándékot küld:

```text
Move(Direction.Left)
UseItem(CharacterId, Slot, ExpectedInventoryRevision)
MoveItem(Source, Target, ExpectedInventoryRevision)
ChooseBattleAction(BattleId, TurnId, Action)
ChooseDialogOption(PromptId, OptionId)
InnPurchase(OfferId, TargetCharacterId, ExpectedInnRevision)
SetReady(PhaseId, true)
```

A host sorba rendezi és validálja a parancsokat, módosítja az állapotot, majd eseményt vagy állapot-deltát küld mindkét félnek. Minden parancshoz tartozzon:

- `SessionId`;
- `PlayerId`;
- monoton növekvő `ClientCommandId` az ismételt küldés felismeréséhez;
- az érintett alrendszer revíziója, ha versenyhelyzet lehetséges;
- opcionális `PromptId`, `BattleId` vagy `PhaseId`, hogy egy későn érkező válasz ne hasson már lezárt állapotra.

A szerver eseményei kapjanak globális, monoton `EventSequence` értéket. Hézag észlelésekor a kliens ne próbálja kitalálni a hiányzó állapotot, hanem kérjen új snapshotot.

### Javasolt szimulációs ciklus

- A host belső tickje maradhat 20 ms, de `DateTime.UtcNow` közvetlen használata helyett legyen `IGameClock`.
- A konzolinput és a hálózat ugyanabba a `Channel<GameCommand>` sorba írjon.
- Egyetlen szimulációs szál olvassa a parancsokat és módosítsa a domént. Így a `LiveCharacter`, `Maze` és inventory nem igényel általános lockolást.
- A host 10–20 Hz-cel küldjön összevont világ-deltát, a fontos tranzakciókat és fázisváltásokat azonnal.
- A kliens saját mozgását vizuálisan előre jelezheti később, de az MVP-ben csak a host visszaigazolása után mozduljon. A játék jelenlegi 90–240 ms-os karaktermozgása mellett ez LAN-on megfelelő lesz.

## 5. Felfedezés és mozgás

- Mindkét ember a saját avatárját mozgatja.
- A host továbbra is partyparancsokat adhat az NPC-knek (`hold`, `regroup`, `scatter`), de ezek a vendég karakterét nem mozgatják automatikusan.
- A vendég szabadon eltávolodhat, de javasolt egy 12–15 mezős puha határ: ezen túl figyelmeztetés és romló közös együttműködés, nem láthatatlan fal.
- A kijáratot a leader aktiválja. Normál esetben minden élő emberi karakter legyen a kijárat 3 mezős környezetében és csatán kívül.
- Ha valaki nincs ott, a host választhat: vár, vagy megerősítéssel összegyűjti a partit. Az összegyűjtés ne működjön aktív csata közben.
- Ajtót, tetemet, földi tárgyat és ládát az a játékos kezel, akinek a karaktere a szükséges pozícióban van. A tolvajsegítség kérés legyen külön, célzott prompt a tolvaj irányítójának; NPC tolvajnál maradhat automatikus.
- A köd legyen közös party-köd, ahogy jelenleg is. Nem érdemes játékosonként eltérő térképet szinkronizálni.

## 6. Harc

### Ajánlott MVP-szabály: globális világstop, lokális akciógazda

Ha bármelyik karakter csatába kerül, az egész világ szimulációja megáll: nincs ellenfélmozgás, szükségletfogyás vagy második, párhuzamos csata. Ez megőrzi a jelenlegi szabályt, kizárja az egymásba futó módosításokat, és két játékosnál jól érthető.

A csata szerepei:

- az ütközést kezdeményező vagy elszenvedő karakter a **primary combatant**;
- a karakter emberi irányítású, akkor a saját játékosa választja a fő akcióját;
- ha NPC, az AI választ;
- az 5 mezőn belüli, rálátással rendelkező másik emberi karakter körönként egy támogatói akciót választhat;
- a távoli játékos nézőként látja a csatát és a naplót, de nem avatkozik be;
- a közelben levő NPC-k a jelenlegi támogatói AI-t használják.

Ez közvetlenül továbbviszi a jelenlegi modellt, amelyben a primary karakter 1v1 csatázik, a közeli társak pedig körönként támogatnak. Nem kell rögtön teljes, több célpontos taktikai csatarendszert tervezni.

Minden harci kör legyen explicit állapotgép:

```text
BattleStarted
  -> AwaitPrimaryAction (ha a primary köre jön)
  -> AwaitSupportActions (csak jogosult támogatók)
  -> ResolveActionsOnHost
  -> PublishBattleEvents
  -> AwaitAcknowledge vagy rövid automatikus késleltetés
  -> következő kör / BattleEnded
```

Fontos részletek:

- A `BattleSystem.Resolve` helyett `StartBattle` + `Advance(BattleCommand)` API kell, tartós `BattleState` objektummal.
- A host dob minden kockát és sorrendben publikálja az eredményt.
- Az akcióválasztás timeoutja például 30 másodperc. Timeoutnál fizikai támadás legyen az alapértelmezés, ne teljes session-stop.
- Disconnectkor az adott karakter akcióját az AI veszi át.
- A Space-szel léptetett harci napló csak lokális prezentáció legyen. Egy játékos visszaolvasása ne tartsa fel a másikat; a következő döntés előtt azonban mindkét kliens megkapja az összes eseményt.
- F9 csak a hostnak legyen engedélyezett, és továbbra is biztonságos harcvégi mentési kérést jelentsen.
- A host halála nem feltétlenül zárja le azonnal a kampányt: a jelenlegi game-over szabályt coop előtt termékdöntéssel tisztázni kell. Ajánlott MVP: primary karakter veresége a jelenlegi szabály szerint elesés, de ha van élő partitag, a futam folytatódik; a leader halála után a party csak feltámasztással vagy fogadóba jutással menthető. Ez külön játékszabály-fejlesztés, ne rejtett hálózati mellékhatás legyen.

## 7. Modális ablakok, dialógusok és szünet

Minden UI-ablak kapjon `PauseScope` besorolást:

| Típus | Példa | Világ állapota | Ki válaszol |
|---|---|---|---|
| `LocalOverlay` | súgó, tárgyleírás, karakterlap | fut tovább | csak a megnyitó játékos |
| `ActorPrompt` | varázscélzás, saját ajtópróba, saját harci akció | az érintett state machine vár; harcnál globális stop | az akció gazdája |
| `PartyPrompt` | pályaváltás, fontos történeti döntés | globális stop | leader vagy ready-check |
| `SystemPause` | host pause menü, mentésbetöltés, kapcsolat-helyreállítás | globális stop | host |

A történeti overlayt mindkét kliens kapja meg. Az első verzióban a leader lépteti tovább, a vendég képernyőjén „A party-leader döntésére várunk” jelenik meg.

Semmilyen domain- vagy renderer-metódus ne hívjon `Console.ReadKey`-t. A UI megnyit egy nézetet, majd később `GameCommand` formájában adja vissza a választ. Ettől a hálózati keepalive, disconnect és timeout modális képernyő alatt is működik.

## 8. Inventory, loot és pénz

### Tulajdon és jogosultság

- Minden játékos szabadon kezeli a saját karakterének inventoryját.
- A host szabadon kezeli az NPC-ket, de a vendég által vezérelt karakter inventoryját nem.
- Két emberi karakter közötti tárgyátadás kétlépcsős ajánlat legyen: `OfferItem` majd `Accept/Reject`. Az elfogadás a hoston egyetlen atomi tranzakció.
- A harcba belépés zárolja az érintett karakter inventory-módosítását; a már nyitott helyi inventory nézet bezárható vagy read-onlyvá válik.
- Minden karakter inventoryja kapjon `InventoryRevision` értéket. Régi revíziójú parancs elutasításakor a kliens friss állapotot kap.
- Tárgyakat továbbra is stabil definíció-ID és töltetszám azonosítson, de a konkrét példány kapjon `ItemInstanceId`-t is. Különben két azonos pálca vagy fogyóeszköz versenyhelyzetben nem különböztethető meg biztonságosan.

### Loot

Az első változatban megőrizhető a jelenlegi „első szabad party-hátizsák” kiosztás, de az esemény mindkét játékosnak mondja meg, ki kapta a tárgyat. Később érdemes közös, korlátozott party-stash irányába menni; ez nem szükséges a hálózati MVP-hez.

### Arany

A jelenlegi arany ténylegesen a leader karakterén van, miközben a teljes party vásárlásait finanszírozza. Coop előtt ezt érdemes explicit `PartyTreasury` állapottá refaktorálni. Mindkét játékos költhet belőle a fogadóban; a host sorosítja a tranzakciókat. Ha termékoldalról leader-jóváhagyás kívánatos, az később külön opcionális lobbybeállítás lehet, ne minden apró vásárlás kötelező promptja.

## 9. Pályavégi fogadó

A fogadó közös session-fázis, de nem közös, blokkoló képernyősorozat:

1. A host egyszer kisorsolja és eltárolja a jutalmakat, piacokat, mestereket, zsoldosokat és pletykákat.
2. Mindkét játékos megkapja a pályavégi összesítőt.
3. Minden játékos párhuzamosan kezelheti a saját karakterét: inventory, tárgyhasználat, varázsmemorizálás és saját level-up/perk választás.
4. A kereskedő készlete közös és verziózott. Limitált ajánlatnál a hosthoz előbb beérkező érvényes vásárlás nyer; a másik kliens azonnal `OfferSold` eseményt kap.
5. Eladni csak saját karakter vagy host esetén NPC hátizsákjából lehet.
6. Toborzás és NPC-csere leader-jogosultság. Ember által foglalt partyhely nem cserélhető le.
7. Pihenés egyszeri, globális művelet, amely minden élő karaktert érint.
8. A játékosok `Ready` állapotot jelölnek. A leader akkor indítja a következő pályát, amikor mindenki kész; hosszú várakozásnál megerősítéssel kényszerítheti az indulást, de aktív perk-/varázslatválasztást nem szakíthat félbe.

A fogadói véletlen készletet a session állapotába és a teljes mentésbe is bele kell venni, ha fogadó közben engedélyezünk mentést vagy reconnectet. Egyszeri újragenerálás reconnectkor kihasználható hiba lenne.

## 10. Kapcsolódás, meghívás és session-életciklus

### LAN/direct-IP MVP

1. Host: „Coop játék indítása”.
2. A játék elindít egy lokális Kestrel/SignalR végpontot egy választott porton.
3. Megjelenik a LAN IP, port és egy rövid session-kód.
4. A vendég beírja a címet és a kódot.
5. Verzió- és adatkatalógus-egyezés után snapshotot kap, majd a leader jóváhagyja a karakterátvételt.

### Internetes végleges változat

A NAT miatt a host gépe általában nem érhető el megbízhatóan egy meghívókóddal. A javasolt felépítés:

```text
Host game ── kimenő TLS/WebSocket ── Relay/Lobby service ── kimenő TLS/WebSocket ── Guest
                    |                       |
                    +── session regisztráció, invite-kód, rövid reconnect-token
```

A relay ne futtassa a játékszabályokat; csak hitelesít, session-csoportba rendez és továbbít. A host marad authoritative. Kétjátékos, körökhöz közeli konzoljáték esetén az extra relay-latencia és sávszélesség elhanyagolható a fejlesztési és üzemeltetési egyszerűséghez képest.

Fejlesztői internetes teszthez használható VPN-overlay vagy kézi port-forward, de ez ne legyen a végleges játékosélmény. Saját ICE/STUN/TURN hole-punch rendszer bevezetése ehhez a projekthez aránytalanul nagy feladat.

### Session-szabályok

- Meghívókód mögött legalább 128 bit véletlen titok legyen; a rövid, beírható kód csak szerveroldali, gyorsan lejáró hivatkozás.
- A csatlakozás egyeztesse: protokollverzió, játékverzió, save-schema és `adatok.csv` tartalmi hash.
- 5–10 másodperces kapcsolatvesztési türelmi idő után az NPC-AI vegye át a karaktert.
- A reconnect-token ugyanabba a `PlayerId`-ba és karakterbe engedjen vissza, ne új játékost hozzon létre.
- Host disconnect esetén a session megszűnik. Host migration későbbi, külön feature; az authoritative állapot és a mentéstulajdon miatt nem olcsó kiegészítés.
- A host kilépés előtt menthet. A vendég helyi fájlba ne írhassa a kampány hiteles mentését.

## 11. Protokoll és adatirányok

### Vendég → host

- csatlakozás, authentikáció, verzió/catalógushash;
- mozgási és interakciós szándék;
- harci, célzási és dialógusválasztás;
- inventory- és kereskedelmi parancs;
- fogadói ready állapot;
- heartbeat, snapshot- vagy resend-kérés.

### Host → vendég

- teljes `SessionSnapshot` csatlakozáskor/reconnectkor;
- entitás-delták: pozíció, HP/manna/szükséglet, státusz, inventory-revízió;
- létrehozás/eltávolítás: ellenfél, tetem, loot, avatar;
- harci és narratív események;
- promptok és azok jogosult válaszadója;
- fogadói készlet és tranzakcióeredmény;
- pause/phase/ready/connection állapot;
- parancselfogadás vagy strukturált elutasítás.

Nem szükséges minden renderer-sort vagy teljes konzolképet hálózaton küldeni. A kliens ugyanabból a típusos állapotból saját `ConsoleRenderer`-rel rajzol.

Javasolt szerződésprojektek:

```text
MazeGame.Contracts
  Commands/*
  Events/*
  Snapshots/*
  ProtocolVersion.cs

MazeGame.Domain
  tiszta játékszabály és állapot

MazeGame.Application
  GameSession, command validation, fázis- és battle-state machine

MazeGame.Transport.SignalR
  host hub, klienskapcsolat, serialization

MazeGame.ConsoleClient
  input és renderer

MazeGame.Relay
  internetes lobby/invite/forwarding szolgáltatás
```

## 12. Miért SignalR/WebSocket

- A játék kétirányú, tartós, alacsony forgalmú eseménycsatornát igényel; ez a WebSocket természetes feladata.
- SignalR kezeli a kapcsolat-életciklus és reconnect jelentős részét, RPC-szerű típusos hívásokat ad, és szükség esetén transport fallbacket biztosít.
- A Microsoft a legtöbb ASP.NET Core alkalmazáshoz a SignalR-t ajánlja a kézzel kezelt WebSocket helyett; a SignalR lehetőség szerint WebSocketet használ.
- Kezdetben a JSON könnyen naplózható és hibakereshető. A szerződés stabilizálása után a SignalR MessagePack protokollra váltható, ha méréssel indokolt.

Alternatívák:

| Technológia | Döntés | Indok |
|---|---|---|
| REST/HTTP | csak lobby/admin célra | a gameplayhez sok polling vagy külön eseménycsatorna kellene |
| gRPC bidirectional streaming | most nem | technikailag alkalmas, de a protobuf/stream-életciklus plusz komplexitás; a játék forgalma nem teljesítménykritikus |
| raw TCP | nem | saját framing, verziózás, reconnect, TLS és backpressure kellene |
| raw WebSocket | csak akkor, ha SignalR akadály lesz | kisebb absztrakció, de sok kész kapcsolatkezelést újra kellene írni |
| UDP/QUIC | nem az MVP-ben | nincs szükség twitch-szerű, sokszor másodpercenkénti pozíciófrissítésre; a megbízható sorrend fontosabb |

Források:

- Microsoft: [WebSockets support in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-10.0)
- Microsoft: [ASP.NET Core SignalR configuration](https://learn.microsoft.com/en-us/aspnet/core/signalr/configuration?view=aspnetcore-10.0)
- Microsoft: [gRPC performance and bidirectional streaming](https://learn.microsoft.com/en-us/aspnet/core/grpc/performance?view=aspnetcore-10.0)
- IETF: [RFC 8656 — TURN és NAT mögötti relay](https://www.rfc-editor.org/rfc/rfc8656.html)

## 13. Biztonság és hibakezelés

- Interneten csak TLS (`wss://`). LAN fejlesztői módban engedhető konfigurálható plain kapcsolat.
- A host minden parancsnál ellenőrizze a karaktertulajdont, session-fázist, távolságot, erőforrást és revíziót.
- Legyen üzenetméret-korlát, sebességkorlát és ismeretlen message-type elutasítás.
- Soha ne deszerializáljunk futtatható típust vagy tetszőleges CLR type nevet. Csak zárt DTO-szerződések és stabil enum/string discriminatorok legyenek.
- A napló ne tartalmazzon invite-secretet vagy reconnect-tokent.
- A session-folyamat kezelje külön a `Rejected`, `OutOfDate`, `NotAuthorized`, `Busy`, `StaleRevision` és `InvalidPhase` hibákat.
- A kliens ne fagyjon meg hálózati várakozáskor: minden várás legyen cancellationtokennel és látható állapottal.

## 14. Bevezetési sorrend

### 0. Karakterizációs tesztek

- Új `MazeGame.Tests` projekt.
- Tesztek a mozgás/foglaltság, XP-elosztás, NPC-harc, inventorycsere, lootkiosztás, fogadói vásárlás és mentés-visszatöltés köré.
- Seedelhető `IRandomSource` és vezérelhető `IGameClock`.

**Kilépési feltétel:** a fontos jelenlegi szabályok renderer és valódi idő nélkül futtathatók.

### 1. Egygépes parancsalapú refaktor

- `GameSession`, `GameCommand`, `GameEvent` és explicit session-fázisok.
- A konzolinput parancsot küld; a renderer eseményt/állapotot kap.
- A blokkoló `ReadKey` ciklusok megszüntetése.
- Stabil `CharacterId`, `EntityId`, `ItemInstanceId` és revíziók.
- A `BattleSystem` léptethető state machine-né alakítása.

**Kilépési feltétel:** a single-player változat változatlanul játszható, de minden döntés ugyanazon a parancsútvonalon megy, amelyet később a hálózat használ.

### 2. In-process két klienses vertical slice

- Host session és két inputforrás ugyanabban a processben.
- Vendég átvesz egy NPC-t.
- Külön mozgás, közös köd, disconnect→AI és reconnect.
- Globális pause és egyszerű, fizikai támadásos vendégharc.

**Kilépési feltétel:** automatizált integrációs tesztben két vezérlő végig tud menni egy rövid pályán.

### 3. LAN SignalR

- Snapshot + delta sync, parancsazonosítás, keepalive, reconnect.
- Host/join menü és verzióellenőrzés.
- Inventory, varázslás, ajtók és teljes harci akciók.
- Két valódi gépes soak test mesterséges latency/loss mellett.

### 4. Fogadó és teljes kampányfolyam

- Párhuzamos fogadói UI, shared stock revision, ready-check.
- Perk- és varázsválasztás karaktertulajdon szerint.
- Halál, pályaváltás, mentés/betöltés és visszacsatlakozás.

### 5. Internet relay és meghívás

- Relay/lobby deployment, lejáró invite-code, TLS, authentikáció és rate limit.
- Telemetria: kapcsolatminőség, disconnect ok, snapshot méret, parancs-latencia; személyes adat nélkül.
- Publikus internetes teszt több NAT-típus és tűzfal mögül.

### 6. Hardening

- Hosszú session-, reconnect-, dupla parancs-, stale inventory- és egyidejű vásárlástesztek.
- Protokollkompatibilitási szabály és migráció.
- Terhelésmérés után döntés JSON vs. MessagePack ügyben.

Egy fejlesztő számára ez nagyjából **10–17 nettó fejlesztői hét** lehet, a teljes jelenlegi feature-készlet megtartásával. A legnagyobb bizonytalanság nem a hálózat, hanem a blokkoló UI és a szinkron harc állapotgéppé alakítása.

## 15. Első játszható scope és tudatosan halasztott elemek

Az első valóban tesztelhető coop slice tartalmazza:

- host + pontosan egy vendég;
- egy NPC átvétele;
- LAN kapcsolat;
- két független mozgó karakter;
- közös foglaltság és köd;
- egy egyszerű csata, amelyet bármelyik játékos kezdeményezhet;
- disconnectkor AI takeover;
- reconnect snapshotból.

Tudatosan későbbre marad:

- négynél több partyhely vagy kettőnél több ember;
- vendég által importált saját karakter;
- host migration;
- drop-in karaktercsere aktív harc közben;
- párhuzamos csaták és tovább futó világ;
- saját NAT hole punching;
- matchmaking, barátlista, voice chat;
- kliensoldali predikció és rollback.

## 16. Elfogadási kritériumok

- A vendég semmilyen paranccsal nem tud más karaktert, aranyat vagy világállapotot jogosulatlanul módosítani.
- Ugyanaz a seed és parancssor single-player és hálózati host módban ugyanazt a domain-eredményt adja.
- 500 ms késleltetett vagy duplán kézbesített parancs nem dupláz tárgyat, aranyat, XP-t vagy sebzést.
- Disconnect után a host 10 másodpercen belül folytathatja a játékot AI társsal.
- Reconnectkor a vendég teljes snapshot után ugyanazt a karaktert kapja, és nem marad el eseménye.
- Két egyidejű fogadói vásárlásból egy limitált ajánlat pontosan egyszer fogy el.
- Modális képernyő alatt is működik heartbeat, disconnect-észlelés és host pause.
- Csak a host készíthet vagy tölthet be hiteles kampánymentést.
- LAN-on legalább kétórás játék nem mutat állapotszétcsúszást; internetes relayen mesterséges 150 ms RTT mellett is végigjátszható a vertical slice.

## 17. Következő konkrét lépés

> Implementációs állapot (2026-08-26): az első session-alap elkészült. Van stabil, mentett `CharacterId`, `PlayerId`, karaktervezérlési tulajdon, fázis- és sorszám-validált command queue, sorrendezett session-eseményfolyam, remote karaktertulajdon, disconnect → AI és reconnect. A térképi, harci és inventory inputok szemantikus, host-validált commandként működnek. Elkészült a protokollverziózott `SessionSnapshot`, a felfedett world read model, a sorszámkötött `WorldDelta`, valamint a kliensenkénti teljes snapshot → ACK → delta/resync publisher. A transportfüggetlen protokollhatár verzió- és SHA-256 katalógushash-handshake-et, 256 bites reconnect-tokent, explicit allowlistes JSON codecet, klienskarakter-belépést és snapshot ACK/resync üzeneteket ad. A `CoopHostGateway` a transportkapcsolathoz köti a `PlayerId`-t, elutasítja a sender-hamisítást, kezeli a kapcsolat-életciklust, és visszaküldi a szimulációs szál command-elutasításait. A Kestrel/SignalR LAN hostot a `CoopHostRuntime` nem blokkoló latest-snapshot pumpája táplálja. A főmenüben a host várakozószobát nyit és csak a vendég karakterének belépése után indítja a játékot. A vendég a saját helyi karakterlistájából választ vagy új karaktert generál; a teljes mentési állapotot elküldi, a host pedig ezt a karaktert veszi fel party-tagnak. A session/party megosztott állapota többszálú olvasásra védett. A távoli karakter ellenségre lépési szándéka host-oldali `BattleState`-et indít, globális `Battle` fázisban megállítja a világot, és a promptot csak a karakter tulajdonosa oldhatja fel. A győzelem, XP, szükségletfogyás, tetemképzés és vereség utáni megfigyelő mód host-authoritatív. A protokoll 5-ös verziójában a vendég ugyanazt a közös karakterlap-layoutot és alap-keymapet használja, mint a host, karakterváltás nélkül. Az `N/Z/K` saját pozíciós karakterakció, a `P/G/H/M` és kijárati `Enter` leader-only. A vendég dirty-cell renderelést használ, a host által projektált terep-, ajtó- és ellenfélszíneket jeleníti meg, és csak a megváltozott térképcellákat vagy panelsorokat írja újra. A jobb panelen party-státusz és ASCII portré, a térkép alatt ötsoros helyi üzenetnapló látható. Snapshot- és kupacrevízióval védve tud inventoryslotot cserélni, fogyóeszközt használni, tárgyat eldobni és kereséssel felvenni. A csata akcióra várása nem használ blokkoló belső `ReadKey`-t; a helyi célzó overlay és több fogadói képernyő még modális UI.

A vendég és a host közös karakterlap-/inventory-layoutja elkészült; a vendég változatából szándékosan hiányzik a karakterváltás és más partitag kezelése. A következő konkrét lépés a távoli harci varázslás: a kliensnek a promptban engedélyezett varázslatok és varázstárgyak biztonságos read modelje szükséges, majd nem blokkoló varázslat-, eszközslot- és célpontválasztásból kell `BattleActionCommand`-ot képeznie. Ezt követően a távoli karakter szintlépési tehetség- és varázslatválasztását is a tulajdonoshoz címzett prompttá kell alakítani; az első vertical slice-ban ez még a host konzolján történik.
