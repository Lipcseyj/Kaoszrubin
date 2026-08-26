# MazeGame architektúra

## Ténylegesen működő fejlesztői funkciók

Az alábbi rejtett gyorsbillentyűk közvetlenül be vannak kötve a játék fő bemeneti ciklusába:

- `Ctrl+Shift+U`: a teljes térkép megjelenítésének be- és kikapcsolása a felfedezettségi adatok módosítása nélkül;
- `Ctrl+Shift+R`: az aktuális futam következő, újonnan generált labirintusának indítása;
- `Ctrl+Shift+E`: a partyvezér teleportálása a kijárathoz legközelebbi szabad mezőre;
- `Ctrl+Alt+S`: a partyvezér azonnali felléptetése a következő szintre a hiányzó XP megadásával;
- `Ctrl+Shift+Y`: a szabad partihelyek feltöltése Harcos–Mágus–Lovag sorrendben;
- `Ctrl+Alt+X`: a szabad partihelyek feltöltése Barbár–Tolvaj–Pap sorrendben;
- `Ctrl+Shift+Í`: egy véletlen osztályú, első szintű NPC hozzáadása, ha van szabad partihely;
- `Ctrl+Shift+I`: a falakon való áthaladás be- és kikapcsolása.

A két rögzített osztályszett magasabb szintű, véletlenül generált karakterei három felszerelt varázstárgyat és pontosan egy kulcsot kapnak. A kulcs számára telt hátizsáknál az utolsó véletlen tárgy helye szabadul fel. A Mágus, Pap és Lovag egy pálcát, egy számukra használható tekercset és egy passzív gyűrűt vagy amulettet visel. A Harcos, Barbár és Tolvaj tekercs helyett egy második pálcát kap, így a tekercsek normál kasztkorlátozása változatlan marad.

## Áttekintés

A MazeGame egy .NET 10 konzolos, egyjátékos labirintusjáték. Az alkalmazás adatvezérelt: a fajok, osztályok, ellenfelek, felszerelések, varázslatok és fejlődési küszöbök az `adatok.csv` fájlból töltődnek be. A karakterlista JSON-fájlban, a teljes futamok pedig időbélyeges `.save` állományokban maradnak meg.

### Halottűzés

Az `MA001` Élőholt tulajdonságú ellenfelek ellen a Pap és a Lovag csatánként egyszer külön kasztakciót használhat; a partyvezérnél ez a `T` billentyű, az NPC-k pedig automatikusan választják. A képesség nem varázslat, ezért nem fogyaszt mannát, nem igényel memorizálást vagy fókusztárgyat, de egy teljes harci akcióba kerül.

- Pap: `1d20 + Intelligencia + szint/2` a `10 + ellenfél-erősség×2` nehézség ellen. Sikerre az élőholt két akciót kihagy. Legalább 10 pontos túldobás az 1–2-es erősségű, nem vezér élőholtat azonnal megsemmisíti.
- Lovag: `1d20 + Erő + szint/3` ugyanilyen nehézség ellen. Sikerre `1d6 + szint/2` szent sebzést okoz, az ellenfél kihagyja következő akcióját, a Lovag pedig két akcióra +2 védelmet kap.
- Kudarc esetén csak az akció és az adott csatára szóló használat vész el. A 4–5-ös erősségű és vezér ellenfelek nem semmisíthetők meg azonnal.

### Bossok és aranykulcsok

Az `EnemyDefinition.IsBoss` jelöli a tizenkét bossfajt. Ezek: Patkányember, Ghoul, Ork sámán, Fagyóriás, Vörös sárkány, Hidra, Vén beholder, Csontsárkány, Ősvámpír, Drakolich, Balor démon és végül a Káoszsárkány. A kampány első célja mind a 12 aranykulcs összegyűjtése.

Egy bossfaj legyőzése pontosan egyszer ad aranykulcsot, ezért ismételt példány vagy mentés-visszatöltés nem sokszorozhatja a jutalmat. A kulcsok nem inventorytárgyak: a játékállás az összegyűjtött bossazonosítókat tárolja, a karakterlap pedig a labirintusszint mellett `🔑 n/12` formában mutatja az előrehaladást. A tizenkettedik kulcs megszerzése külön cél-teljesítési üzenetet ad.

Amikor egy boss mezője először ténylegesen láthatóvá válik, modális bossbemutató ablak jelenik meg. Az ablak a varázsválasztóhoz hasonlóan kizárólag a térkép fölé rajzolódik: előtte elmenti az érintett térképcellák vizuális állapotát, bezáráskor pedig célzottan csak ezeket állítja vissza. A karakterlap és a teljes konzol újrarajzolása nem szükséges. A már bemutatott bossazonosítók szintén a teljes játékmentés részei. Mind a 12 boss saját, E/1-ben elmondott történetet kapott a II–XIII. fejezetben. A korai őrzők csak töredékeket és szóbeszédeket ismernek, a későbbiek egyre pontosabban beszélnek a kulcsokról, a Káoszrubinról és az előttünk álló útról. A tizenkét kulcs megszerzése a XIV. fejezettel folytatja a történetet.

Új játék indításakor ugyanezzel a térképoverlay-mechanizmussal jelenik meg a Káoszrubin eredetét, Aurelios Máguskirály megbízását, Vhar-Zul fenyegetését és a kiválasztott Kulcshordozók küldetését elmesélő nyitófejezet. A tizenkettedik kulcs megszerzésekor külön második fejezet nyílik: a zárak feloldódnak, és feltárul a Káoszrubinhoz vezető huszonegy további szint. Ez mérföldkő, nem játékbefejezés.

A megoldás fő felelősségi területei:

- **indítás és menü:** az adatok betöltése, karakterkezelés és játékindítás;
- **adatmodell:** CSV-ből érkező, többnyire változatlan definíciók;
- **karakterállapot:** a játék során változó értékek és felszerelés;
- **világmodell:** labirintus, szobák, játékos és térképi objektumok;
- **játékmenet:** bemenet, időzített események, pályaváltás és találkozások;
- **harc:** körökre osztott közelharci szabályok;
- **megjelenítés:** közvetlen, részleges konzolfrissítés.

## Indítási és fő adatfolyam

```text
Program
  ├─ CsvGameDataLoader ── adatok.csv ──> GameDataCatalog
  └─ MainMenu
       ├─ CharacterSaveService <──> karakterek.json
       ├─ GameSaveService <──> mentések/*.save
       ├─ CharacterCreationScreen
       └─ Game
            ├─ MazeLevelConfigurations
            ├─ MazeGenerator ──> Maze
            ├─ FogOfWar
            ├─ BattleSystem
            └─ ConsoleRenderer
```

Az indítás menete:

1. A `Program.cs` UTF-8 konzolkódolást állít be.
2. Az alkalmazás kimeneti könyvtárából betölti az `adatok.csv` fájlt.
3. A `CsvGameDataLoader` létrehozza a `GameDataCatalog` katalógust.
4. A `MainMenu` betölti a `karakterek.json` állományt, ha létezik.
5. A felhasználó karaktert készíthet, választhat vagy törölhet, illetve játékot indíthat.
6. A `Game` minden labirintusszinthez új világot, játékospozíciót és ködállapotot hoz létre, de ugyanazt a `LiveCharacter` példányt használja tovább.

## Projektfelépítés

### Gyökérnévtér: világ és játékmenet

- `Game.cs`: a fő játékhurok és az alrendszerek koordinátora.
- `Maze.cs`: a pályarács, a szobák és a térképi objektumok tárolója.
- `MazeGenerator.cs`: labirintus, szobák, ládák és ellenfelek létrehozása.
- `MazeLevelConfiguration.cs`: szintenkénti nehézség és generálási tartományok.
- `MazeGenerationSettings.cs`: egy konkrét generálás már kisorsolt beállításai.
- `FogOfWar.cs`: felfedezett cellák, látóvonal és fejlesztői felfedés.
- `ConsoleRenderer.cs`: a teljes konzolos nézet és annak részleges frissítése.
- `Player.cs`, `Enemy.cs`: mozgó világobjektumok.
- `TreasureChest.cs`, `Corpse.cs`: felvehető vagy dekoratív világobjektumok.
- `WorldObject.cs`: minden, pályaborítástól független objektum alaptípusa.
- `Position.cs`, `Direction.cs`, `Room.cs`: alapvető térbeli értékobjektumok.
- `AsciiPortraits.cs`: a jobb alsó képpanel legfeljebb öt sor magas, osztály- és ellenfélazonosító alapján választott beépített ASCII-ábrái.

### `Data`: betöltés és mentés

- `CsvGameDataLoader`: szekciókra bontott CSV-feldolgozó. Elsődlegesen UTF-8-at olvas, hibás UTF-8 esetén Windows-1250-re vált.
- `GameDataCatalog`: központi, csak olvasható definíciógyűjtemény és azonosító alapú keresési felület.
- `CharacterSaveService`: a futó karakterek és az aktív kiválasztás JSON-szerializálása, illetve visszaépítése a katalógus definícióiból.
- `GameSaveService`: a teljes futam időbélyeges `.save` fájljainak létrehozása, listázása és betöltése.

### `Domain`: játékadatok és karakterállapot

- `IGameDefinition`: az azonosítóval és névvel rendelkező definíciók közös szerződése.
- `Domain/Characters`: fajok, osztályok, képességek, kezdőfelszerelés, karakterlista és `LiveCharacter`.
- `Domain/Characters/Party.cs`: az aktív vezetőből és legfeljebb három társából álló csapat.
- `Domain/Combat`: ellenfél-, fegyver-, fegyvertípus- és páncéldefiníciók, valamint zárt számtartományok.
- `Domain/Inventory`: általános tárgyfelület és hétköznapi tárgyak.
- `Domain/Magic`: varázstárgyak és varázslatok.

A `Definition` végű típusok az `adatok.csv` tartalmát képviselik. A `LiveCharacter` ezzel szemben változó futásidejű állapot: HP, manna, szükségletek, arany, XP, szint, felszerelés, hátizsák, valamint az ismert és memorizált varázslatok.

### `UI`: menük

- `MainMenu`: új játék, mentésbetöltő, karakterlista, kiválasztás, törlés, gyorsindítás és súgó.
- `CharacterCreationScreen`: név- és fajválasztás, tulajdonságdobás, osztályjogosultság és karakter létrehozása.

### `Combat`: harci szabályrendszer

- `BattleSystem`: a megjelenítéstől független harci algoritmus.
- `BattleResult`, `BattleLogEntry`: a csata végeredménye és megjeleníthető eseményei.
- `BattleState`: stabil `BattleId` és monoton `TurnId` mellett több inputcikluson át folytatható harci állapot. A `StartBattle` inicializál, az `Advance` pontosan egy akciót old fel; a régi `Resolve` kompatibilitási adapterként ugyanezt az állapotgépet hajtja.

### `Application`: session- és parancshatár

- `GameSession`: egy futó játék helyi vagy később hálózati parancsainak egyetlen belépési pontja. Tulajdonjog, session-fázis és monoton parancssorszám alapján validál, majd a `Game` egyetlen szimulációs szála olvassa ki az elfogadott parancsokat.
- `SessionContracts`: stabil `PlayerId`/`CharacterId` alapú commandok, vezérlési állapotok és sorrendezett session-eseményfolyam. Minden harci választás `BattleActionCommand`, amely az aktuális `BattleId` és `TurnId` mellett csak szemantikus inputot hordoz: akciótípust, varázslat-ID-t, opcionális varázstárgyslotot és célpozíciót.
- `SessionSnapshots`: a doménobjektumoktól leválasztott, JSON-nal körbeírható host read model. A `SessionSnapshot` protokollverziót, monoton snapshot-sorszámot, eseménykurzort, fázist, parti-erőforrásokat és -pozíciókat, vezérlési kiosztást, valamint opcionális aktív harci promptot tartalmaz. A snapshot harci része csak az aktuális session-prompt azonosítóival és akció-whitelistjével hozható létre.
- `WorldSnapshots`: a kliens által ismert pályarész read modelje. Kódpontként tárolt felfedett cellákat, ajtókat, ellenfeleket, ládákat, tetemeket és földi tárgykupacokat tartalmaz; köd mögötti geometriát vagy entitást nem publikál. A dinamikus térképi objektumok stabil futásidejű `WorldEntityId`-t kapnak, amelyre a későbbi deltaüzenetek hivatkozhatnak.
- `WorldDeltas`: két egymást követő, azonos `WorldId` értékű snapshot determinisztikus különbsége. Cellafelfedést vagy cellaváltozást, ajtó-upsertet és -eltávolítást, entitás-upsertet, illetve stabil ID-alapú entitáseltávolítást hordoz. A delta `FromSnapshotSequence`/`ToSnapshotSequence` párja megakadályozza, hogy a kliens rossz baseline-ra alkalmazza; pályaváltáskor teljes snapshot szükséges.
- `SessionReplicationPublisher`: transportfüggetlen, kliensenkénti host-publisher. Első kapcsolódáskor teljes snapshotot ad, majd kizárólag a kliens által ACK-olt snapshotból képez deltát. Korlátozott pending snapshot-ablakot tart; ismeretlen ACK, explicit resync vagy pályaváltás esetén teljes snapshotra vált vissza. A delta-frame a friss session/party állapotot world rész nélkül és mellette a world deltát hordozza.
- `InventorySnapshots`: minden felszerelés- és hátizsákhelyet — az üreseket is — explicit `(InventorySlotKind, index)` címmel publikáló read model. A slotban definíció-ID, megjelenítési név, kategória, ritkaság és töltetszám utazik; a részletes statisztikát az azonos verziójú helyi katalógus adja. A `LiveCharacter.InventoryRevision` minden sikeres inventory-mutációnál egyszer nő, hogy a későbbi commandok elavult kliensállapotot észlelhessenek.
- `InventoryTransferCommand` + `InventoryTransferService`: a kliens csak forrás- és célkaraktert/slotot, valamint az elvárt inventoryrevíziókat küldi. Itemdefiníció és töltetszám nem érkezhet a klienstől. A host ugyanazzal a közös szabálykészlettel validálja és hajtja végre az atomi slotcserét; a tárgyak és töltetek vagy mindkét oldalon együtt változnak, vagy semmi nem módosul. A vendég kizárólag a saját vezérelt karakterén belül mozgathat, a host party-karakterek között is rendezhet felszerelést.
- A konzolos leader mozgása, ajtókezelése, partiparancsai, pihenése és pályaváltása már ezen az útvonalon halad. Távoli vezérlő egy NPC-partitag mozgását veheti át; ilyenkor az automatikus NPC-mozgás leáll, disconnectkor visszaáll, reconnectkor pedig ugyanahhoz a karakterhez tér vissza.
- A leader tényleges harca már léptethető `BattleState`-et használ és az aktív `BattleId`/`TurnId` a `Game` felől megfigyelhető. A harc játékosakciónál visszatér a fő játékhurokba; `BattlePromptEvent` jelzi az adott körben engedélyezett akciókat. A fegyveres támadás, a varázslat és a halottűzés ugyanazon a validált command queue-n érkezik vissza, amelyet később a hálózat használ. A kliens nem küldhet dobás- vagy sebzéseredményt: a jogosultságot, célpontot, erőforrás-felhasználást és véletlent a host ellenőrzi és számolja. A fogadó egyelőre csak explicit session-fázist jelez.
- A `Game.CreateSessionSnapshot()` a futó host állapotából elkészíti a transport által közvetlenül publikálható session- és world read modelt. A world projekció csak a `FogOfWar` szerint ténylegesen felfedett adatokat adja át, így még a host fejlesztői reveal módja sem szivárogtatja ki a labirintust vagy a rejtett ellenfeleket. Két ilyen snapshotból a `WorldDeltaProjector` előállítja a sorrendhelyes world deltát, a `SessionReplicationPublisher` pedig ACK-alapú baseline-nal kiválasztja a teljes snapshot vagy delta frame-et. A publisher a hostnak minden party-inventoryt átad, a vendég frame-jében viszont csak a hozzá rendelt karakter inventoryja marad. A helyi inventory UI sem vesz ki többé ideiglenesen tárgyat „kézben tartáskor”; csak a végső célválasztás küld atomi commandot. A konkrét SignalR transport még külön rétegként következik.

## Adatmodell és `adatok.csv`

A CSV `#` karakterrel kezdődő szekciókból áll. A betöltő az ékezeteket és kis-/nagybetűket figyelmen kívül hagyva azonosítja a szekcióneveket. A jelenlegi szekciók:

- fajok és faji képességbónuszok;
- osztályok, képességminimumok és kezdőfelszerelések;
- osztályonkénti tehetségek és tehetségfokozatok;
- osztályonkénti karakter- és később NPC-ként is használható nevek;
- karakterállapotok;
- szintlépési XP-küszöbök;
- ellenfelek;
- fegyverek és fegyvertípusok;
- páncélok;
- képességek és tárgyak;
- varázstárgyak, mágikus és papi varázslatok;
- a fegyverek, páncélok, általános tárgyak és varázstárgyak pozitív egész alapára;
- felszerelésritkaságok, mágikus erő, alapfelszerelés-hivatkozások és CSV-vezérelt tárgybővítések;
- használati tárgyak hatástípusa és hatásértéke;
- egészségből számított minimum életerő;
- intelligenciából számított minimum manna.
- a pályavégi teljesítési jutalom konfigurálható `#Base XP pálya végén` alapértéke.

A sorok közötti kapcsolatok szöveges azonosítókon alapulnak, például `C001`, `W004` vagy `E001`. Új adat hozzáadásakor az azonosítóknak egyedieknek, a hivatkozásoknak pedig feloldhatóknak kell lenniük. A CSV egyszerű vessző menti darabolást használ, ezért idézőjeles, vesszőt tartalmazó mezőket jelenleg nem támogat.

A `#Base XP pálya végén` szekció egyetlen nemnegatív egész számot tartalmaz. A `GameDataCatalog.BaseLevelCompletionExperience` kötelező értékként kapja meg; hiánya vagy negatív értéke betöltési hibát okoz.

Az `adatok.csv` a projektfájl beállítása miatt fordításkor a kimeneti könyvtárba másolódik. A program futáskor ezt a másolatot olvassa, nem feltétlenül a forráskönyvtárban lévő fájlt.

## Karakter létrehozása és fejlődése

A karaktergenerálás négy elsődleges képességre egy véletlen méretű pontkészletet oszt el. Az alap 25 pont 15% eséllyel nem kap bónuszt, 50% eséllyel +1, 25% eséllyel +2, 10% eséllyel pedig +3 ponttal nő, így a tényleges készlet 25–28 pont. Mindegyik érték legalább 1 és legfeljebb 10 a dobás során. Ehhez adódnak hozzá a faj módosítói, majd a végeredmény 1 és 13 közé szorul. A kézi generálás azokat a dobásokat, amelyek a kiválasztott fajjal egyetlen osztály CSV-s minimumát sem teljesítik, megjelenítés nélkül automatikusan újradobja. A képességdobás képernyő már az elfogadás előtt felsorolja az eredményhez választható osztályokat, és az osztályválasztó ugyanezt az előre kiszámított listát használja. Ugyanez a pontkészletdobás érvényes a kézi, gyorsindításos és véletlen NPC-karaktergenerálásra.

A karakternév 1–13 karakter hosszú lehet. A korábbi mentésekből érkező hosszabb nevek betöltéskor 13 karakterre rövidülnek, hogy a karakterlap rögzített fejlécébe illeszkedjenek.

Minden `LiveCharacter` tartós `ConsoleColor` tulajdonsággal rendelkezik. Kézi karaktergeneráláskor a játékos egy jól látható színpalettáról választ; gyorsindításkor és fejlesztői társgeneráláskor a szín véletlen. Régi mentéseknél az alapértelmezés cián.

Az `adatok.csv` `#Karakternevek` szekciója osztályonként 20 `CharacterNameDefinition` rekordot tartalmaz. A gyorsindítás az elkészült karakter tényleges osztályának névkészletéből választ, és előnyben részesíti a karakterlistában még nem használt neveket. Ha egy osztály mind a 20 neve foglalt, az ismétlődés megengedett. A definíciók nem játékos karakterek későbbi elnevezésére is újrahasználhatók.

Csak olyan osztály választható, amelynek minden CSV-ben megadott képességminimumát teljesíti a karakter. A maximális HP és manna képlete:

```text
max HP    = egészséghez tartozó CSV-minimum + 1..15 életerőbónusz
max manna = intelligenciához tartozó CSV-minimum + 1..15 mannabónusz
```

Mannát csak a `CharacterClassRules` által varázshasználónak minősített osztályok kapnak. A kezdőfelszerelés az osztályazonosítóhoz tartozó CSV-sorból épül fel.

A CSV intelligenciaküszöbe és a generált 1–15 közötti mannabónusz először közös kezdő mannaösszeget képez. Ebből a Pap 90%-ot, a Lovag 50%-ot kap matematikai egész kerekítéssel; a Mágus teljes értéket kap. Szintlépéskor a Pap és Mágus a teljes CSV-s mannanövekedést, a Lovag minden külön növekedési dobás 50%-át kapja, szintén matematikai kerekítéssel és pozitív dobásnál legalább 1 ponttal. A mentési alapérték-számítás ugyanezt a kasztszabályt használja.

Győztes csata után az ellenfél teljes XP-jutalma a parti életben lévő tagjai között oszlik meg. A győztes 60%-ot kap; a fennmaradó 40% egyenlően jut a többi élő partitaghoz. Az egész számú osztás maradéka parti-sorrendben egyesével kerül kiosztásra ezért XP nem vész el. Ha nincs más élő partitag akkor a győztes kapja a teljes jutalmat. A szabály a vezér és az NPC által megnyert csatára is azonos.

Minden részesülő karakter saját osztálymódosítójával és fejlődési szabályaival dolgozza fel a kapott XP-t. A következő szint tényleges küszöbe:

```text
ceil(CSV XP-küszöb × osztály XP-módosító)
```

Egy XP-jóváírás egyszerre több szintlépést is eredményezhet.

Minden elért új szinthez külön HP- és – mannát használó osztálynál – mannadobás tartozik. A dobás zárt tartományát az `adatok.csv` `#Szintlépés életerő növekedés`, illetve `#Szintlépés manna növekedés` szekciója adja meg az Egészség és az Intelligencia alapján. A növekmény egyszerre emeli a maximális és az aktuális erőforrást, tehát a szintlépés részleges feltöltést is jelent. Több egyszerre elért szint minden bónusza külön kisorsolódik és összeadódik.

### Tehetségek

Minden osztályhoz hat `PerkDefinition` tartozik az `adatok.csv` `#Tehetségek` szekciójában. A mezők: stabil azonosító, név, leírás, osztályazonosító és fokozat. A hat tehetség három egymást kizáró párt alkot; fokozatonként pontosan két definíció szükséges.

A tehetségablakok az 5., 15. és 25. szint körüli ±2 szint:

```text
1. fokozat:  3–7. szint
2. fokozat: 13–17. szint
3. fokozat: 23–27. szint
```

Az ablak minden szintlépésénél 40% az esély a tehetségválasztás aktiválására. Ha addig nem történt meg, az ablak utolsó szintjén garantált. Aktiváláskor a szintlépési képernyőn a játékos kiválasztja a pár egyik tagját; a másik végleg kiesik. A kiválasztott tehetség azonosítója bekerül a karaktermentésbe. Egy fokozat lezárását az jelzi, hogy a karakter már rendelkezik az adott fokozat egyik tehetségével.

Ha egy régi mentésből származó karakter már túllépett egy tehetségablakon, de nincs abból a fokozatból tehetsége, a következő szintlépésekor garantáltan megkapja a kimaradt választást. Több szint egyidejű átlépése több tehetségválasztást is kiválthat.

Az állandó HP- és mannabónuszok a választás pillanatában az aktuális és maximális értéket is növelik. A mentés külön jelzi, hogy mely tehetségek egyszeri bónusza lett már alkalmazva; így a régi mentések megkapják a korábban még passzív tehetségeik bónuszát, de az újabb betöltések nem halmozzák azt többször.

#### Tehetségek implementációs állapota

| Osztály | Tehetség | Állapot | Megvalósított hatás |
|---|---|---:|---|
| Harcos | Első csapás | kész | +10 kezdeményezés |
| Harcos | Robusztusság | kész | +10 maximális és aktuális HP választáskor |
| Harcos | Fegyvermester | kész | +2 fegyveres találati próba |
| Harcos | Rendíthetetlen | kész | találatonként 2 sebzéscsökkentés |
| Harcos | Acélvihar | kész | sikeres első támadás után 35% eséllyel extra támadás |
| Harcos | Utolsó erőd | kész | csatánként egyszer 1 HP-n túléli a halálos csapást |
| Barbár | Vérszomj | kész | fél HP alatt +3 sebzés |
| Barbár | Vastag bőr | kész | +8 HP választáskor és +1 védelem |
| Barbár | Őrjöngés | kész | megszakítás nélküli találatonként halmozódó +1 sebzés |
| Barbár | Fájdalomtűrés | kész | a 3 alatti végső sebzést lenullázza |
| Barbár | Berserker düh | kész | fél HP alatt két támadás saját támadókörönként |
| Barbár | Őserő | kész | +20 HP választáskor és +5 közelharci sebzés |
| Lovag | Pajzsfal | kész | felszerelt pajzzsal további +2 védelem |
| Lovag | Kihívás | kész | az ellenfél első támadása automatikusan kimarad |
| Lovag | Páncélmester | kész | a páncéldobás legalább a tartomány felfelé kerekített átlaga |
| Lovag | Szent eskü | kész | csata elején legfeljebb 10 HP gyógyulás |
| Lovag | Őrangyal | kész | csatánként egyszer kivédi a halálos csapást és 25 HP-t gyógyít |
| Lovag | Legyőzhetetlen | kész | +15 HP választáskor és találatonként 4 sebzéscsökkentés |
| Tolvaj | Orvtámadás | kész | a csata első sikeres támadása kétszeres sebzésű |
| Tolvaj | Kitérés | kész | találat után 15% eséllyel teljes elkerülés |
| Tolvaj | Méregkeverő | kész | sikeres fegyveres támadáshoz +1d6 sebzés |
| Tolvaj | Árnyéklépés | kész | sikeres kitérés után a következő támadás automatikusan talál |
| Tolvaj | Halálos pontosság | kész | természetes 18–20 dobásnál háromszoros sebzés |
| Tolvaj | Mestertolvaj | kész | dupla ládaarany és ládánként 25% eséllyel egy véletlen mágikus ritkaságú tárgy; telt parti-inventorynál a tárgy a láda mezőjén marad |
| Pap | Gyógyító kegyelem | kész | minden papi HP-gyógyítást 25%-kal növel |
| Pap | Áldott fegyver | kész | élőholt (`MA001`) ellen +2 találat és +2 sebzés |
| Pap | Szentély | kész | ellenséges támadásonként 20% eséllyel kimarad a támadás |
| Pap | Hitforrás | kész | +12 manna választáskor és csata elején legfeljebb 5 manna visszatöltés |
| Pap | Feltámadás | kész | pályánként egyszer egy halálos csapás után automatikusan teljes HP-val visszatér; vezetői és NPC-csatában is működik |
| Pap | Isteni ítélet | kész | minden ötödik papi varázslat ingyenes; sebzése vagy gyógyítása kétszeres és az időtartama megduplázódik |
| Mágus | Arkán fókusz | kész | +2 a mágikus támadódobásokhoz |
| Mágus | Mannatartalék | kész | +15 maximális és aktuális manna választáskor |
| Mágus | Elemi mester | kész | minden sebző mágusvarázslat sebzésére +25% |
| Mágus | Mágikus pajzs | kész | a beérkező sebzés felfelé kerekített negyedét manna nyeli el |
| Mágus | Láncvarázslat | kész | 30% eséllyel ingyen megismétli a sebző varázslat sebzését |
| Mágus | Főmágus | kész | +25 manna választáskor és minden varázslat legalább 1-ig csökkentett, -2 mannaköltsége |

A csatában aktiválódó tehetségek bekerülnek a harci napló számításaiba és magyarázó szövegeibe. Az egyszer használható túlélési és első támadásos hatások minden csata elején új harci kontextust kapnak.

A Pap Feltámadás tehetségének „naponta egyszer” korlátját a jelenlegi időmodellben a két fogadólátogatás közötti pálya jelenti. Ugyanazt a mentett visszatérési jelzőt használja mint a papi feltámasztó varázslatok: ha a tehetség már aktiválódott akkor azon a pályán varázslattal sem hozható vissza újra a karakter és fordítva. Az Őrangyal vagy Utolsó erőd jellegű csatánkénti védelem előbb aktiválódik ezért a ritkább Feltámadás csak akkor fogy el ha más túlélési hatás már nem menti meg a karaktert.

### Karakterállapotok

Az állapotok az `adatok.csv` `#Állapotok` szekciójának `StatusDefinition` rekordjai. A CSV állapotonként tárolja az emojit, időtartamot, körsebzést, támadó- és kezdeményezésbüntetést, maximum-erőforrás és regeneráció százalékokat, csatakezdő veszteségeket és a nulla szükségletszint szorzóját. Hibás sebzéstartomány, százalék vagy üres emoji betöltési hibát okoz.

| Állapot | Aktív hatás |
|---|---|
| 🍖 Éhes | −2 fizikai sebzés és 75%-os HP-gyógyulás; nulla élelemnél csatakezdéskor a maximális HP 5%-a elveszik |
| 💧 Szomjas | −3 kezdeményezés, −1 találati próba és csatakezdéskor 5% maximálismanna-vesztés; nulla víznél minden büntetés kétszeres |
| ☠️ Mérgezés | saját támadási kör végén 1d4 közvetlen sebzés, hat aktiválódás után elmúlik |
| 🤒 Betegség | a maximális HP és manna 80%-os, minden HP-/mannavisszatöltés 50%-os; nem jár le magától |
| 🩸 Vérzés | saját támadási kör végén 1d3 közvetlen sebzés, négy aktiválódás után elmúlik |

Az Éhes és Szomjas állapot származtatott: 30 vagy alacsonyabb élelem-, illetve vízszintnél automatikusan aktív, magasabb értéknél megszűnik. A többi állapot hátralévő aktiválódásszámmal együtt mentődik. Az ismételt mérgezés vagy vérzés nem halmozódik, hanem visszaállítja az állapot teljes CSV-s időtartamát. Az ellenméreg, gyógyfüves orvosság és kötés továbbra is azonnal eltávolítja a megfelelő állapotot. A karakterlap az állapotok neve helyett a CSV-s emojikat mutatja.

## Játékhurok

A `Game.Run` egy körülbelül 20 ms-onként ismétlődő ciklus. Három eseményforrást kezel:

1. **Billentyűzet:** nyilakkal játékosmozgás, `P`-vel biztonságos pihenés, `Esc`-pel visszatérés, fejlesztői gyorsbillentyűk.
2. **Ellenfélmozgás:** csatán kívül minden ellenfél a saját Gyorsaságából számított időpontokban hajtja végre a profiljának megfelelő mozgást.
3. **Szükségletcsökkenés:** csatán kívül percenként csökken az élelem és a víz.

Az ellenfelek nem léphetnek falra, bejáratra, kijáratra vagy foglalt mezőre. Ha a játékos és egy ellenfél azonos cellára kerül, azonnal csata indul. Csata alatt a világ ideje és az ellenfelek mozgása megáll.

### Szörnymozgási profilok

Minden szörny a pályageneráláskor egyszer kap mozgási profilt és járőrirányt. A profil a szörny teljes pályabeli életére megmarad, és a teljes játékmentés része:

- `Stationary`: normál helyzetben egy helyben áll;
- `Wander`: minden mozgási időpontban véletlen szomszédos irányt választ;
- `Patrol`: egyenes vonalban halad, akadálynál megfordul, majd az ellenkező irányba folytatja útját.

Ha egy találkozás nem ír elő mozgást, a szobában generált szörny 80% eséllyel helyben áll; a fennmaradó 20% egyenlően oszlik meg a kóborló és járőr profil között. Folyosón a helyben állás esélye 10%, a maradék 90% fele-fele arányban kóborló vagy járőr. A jelenlegi szobai csoportkonfigurációk kifejezetten `Stationary` profilt kérnek, ezért ezek tagjai és vezérei észlelés előtt együtt, helyben várakoznak.

Profiltól függetlenül minden magányos szörny vagy szörnycsoport egyszer hoz üldözési döntést, amikor valamelyik tag először legfeljebb öt Chebyshev-távolságra, tiszta látóvonalban meglátja a partyvezért. 60% eséllyel az egész csoport üldözni kezd, 40% eséllyel minden tag végleg megtartja eredeti profilját. Az üldözők járható útvonalon, zárt ajtókat és foglalt mezőket kerülve közelítenek; partitársba ütközve vele kezdenek csatát. Az üldözési döntés, a csoportazonosító és -szerep, a profil és a járőr aktuális iránya mentéskor megmarad. Régi mentésből hiányzó profil alapértéke a korábbi működést megőrző `Wander`; a hiányzó csoportazonosító magányos ellenfelet jelent.

### Szörnycsoportok és találkozások

A pályakonfiguráció nem összesített ellenféldarabszámokat, hanem külön szobai és folyosói `EnemyEncounterConfiguration` találkozásokat ír le. Három rövid építő áll rendelkezésre:

- `Encounters.Same`: egyetlen fajból álló homogén csoport;
- `Encounters.Mixed`: két fajból álló, hasonló erejű vegyes csoport;
- `Encounters.LeaderGroup`: pontosan egy erősebb vezér és több gyengébb követő;
- `Encounters.Solo`: egyszemélyes találkozások, elsősorban folyosókhoz.

A mennyiségekhez az `Amount` jelzők használhatók: `One` = 1, `Few` = 1–2, `Several` = 3–5, `Band` = 6–9, `Many` = 10–14. A csoportok száma és az egy csoporton belüli létszám külön jelző, ezért például kevés nagy banda és sok kis csoport egymástól függetlenül konfigurálható. Az `IntRange` belső típusként és olyan értékeknél marad meg, ahol egyedi tartomány szükséges, például szobaméretnél, aranynál vagy későbbi szintek képletes skálázásánál.

A generátor előbb a szobai találkozásokat helyezi el, szobánként legfeljebb egy teljes csoporttal, majd csak ezután a folyosói találkozásokat. A vezércsoportok a szobai találkozások között elsőbbséget élveznek, így kevés szobás pályán sem szoríthatják ki őket a közönséges csoportok. A csoport vezére a szoba közepéhez legközelebbi alkalmas mezőt kapja, a követők köré rendeződnek. Ajtóval közvetlenül szomszédos belső mezőre nem kerül szörny. Ha egy csoport teljes kisorsolt létszáma nem fér el, a generátor másik szobát keres; ha nincs megfelelő szoba, kihagyja a csoportot, nem helyezi el töredékesen. A folyosói csoportok összefüggő járható mezőkön jelennek meg.

Az 1–2. pályán főként azonos, alacsony erősségű lények csoportjai jelennek meg, a 2. pályától vegyes találkozásokkal. A 3. pályától vezér és kíséret típusú csoport is lehet. A későbbi, automatikusan képzett konfigurációk három pályánként magasabb erősségi készletre váltanak, miközben a szobai csoportok maradnak túlsúlyban.

Minden szörny külön következő mozgási időponttal rendelkezik. A zombi (`E006`) Gyorsasága 2, ehhez tartozik a korábbi 700 ms-os alaptempó; más ellenfélnél a periódus fordítottan arányos a CSV-s Gyorsasággal:

```text
mozgási periódus = 700 ms × 2 / max(1, Gyorsaság)
```

Így a jelenlegi 2–10-es tartományban a periódus 700–140 ms. A gyorsabb ellenfelek gyakrabban kapnak mozgási lehetőséget, miközben a profiljuk szabályai változatlanok maradnak. A szörnyenként hátralévő mozgási idő mentésre kerül; régi mentésnél a korábbi közös időzítő értéke lesz minden ellenfél induló késleltetése. Vezéri csata után minden túlélő ellenfél friss, saját sebességének megfelelő teljes periódusról indul, ezért a csata alatt eltelt valós idő nem okoz torlódó azonnali lépéseket.

A szükségletek percenkénti csökkenése:

```text
élelemvesztés = 2 + max HP / 60        (egész osztás)
vízvesztés    = 2
               +1, ha a karakter sérült
               +1, ha a HP a maximum fele alatt van
```

A periódusos fogyás külön-külön lefut a parti minden élő tagjára, az adott karakter saját maximális és aktuális HP-ja alapján. A halott társak szükségletei már nem változnak.

Minden csata után kizárólag a ténylegesen harcoló karakter élelem- és vízszintje csökken, ugyanazzal a dobott értékkel:

```text
csata utáni élelemvesztés = 1d5 + szörny erősségi szintje
csata utáni vízvesztés    = 1d5 + szörny erősségi szintje
```

Ez a vezér és az automatikusan harcoló NPC-k csatáira is érvényes.

A rejtett `Ctrl+Shift+S` fejlesztői gyorsbillentyű pontosan a következő szinthez hiányzó XP-t adja a karakternek. Ugyanazt a fejlődési és bónuszdobási útvonalat használja, mint egy valódi csatagyőzelem.

## Parti

A `Party` 1–4 egyedi `LiveCharacter` objektumot tartalmaz. Első tagja mindig az aktív karakter és egyben a csapat vezetője. Új vezető kiválasztása új, egyszemélyes partit kezd; a társak normál felvételi folyamata későbbi fejlesztési pont. A parti tagjai a központi karakterlistában is szerepelnek, ezért ugyanazzal a karaktermentési modellel tárolódnak. A mentés a tagok karakterlistabeli indexeit őrzi.

A jelenlegi játékmenetben a vezető és a társak is harcolhatnak. A harci XP a 60/40-es parti szabály szerint minden élő taghoz eljuthat; minden részesülő ugyanazzal az osztálymódosítóval valamint HP-/mannanövekedési szabállyal lép szintet. Aranyat továbbra is csak a vezető kap. A periódusos szükségletfogyás minden élő tagra lefut, a csata utáni fogyás pedig kizárólag a ténylegesen harcoló karaktert érinti.

A vezető és a társak térképi jele az osztály magyar nevének nagy kezdőbetűje: `H`, `B`, `L`, `T`, `P` vagy `M`. A jel a karakter saját színével rajzolódik. A társak minden új pályán szélességi kereséssel a vezetőhöz legközelebbi üres, járható cellákra kerülnek. Foglalják a mezőjüket az ellenfelek és a vezető elől; egymásra vagy szörnyre nem lépnek és zárt ajtón nem haladnak át. A szörny- és partitárstetemek viszont nem blokkolják sem az NPC-k sem a szörnyek útkeresését vagy lépését; az élő szereplő ideiglenesen eltakarja a tetem jelét, amely a mező elhagyásakor újra láthatóvá válik.

Az NPC-ként vezérelt `LiveCharacter` nullable `NpcBehavior` tulajdonsága mentésre kerül. A vezetőnél inaktív. Generáláskor a barbár mindig `Aggressive`, a lovag mindig `Defensive`, a tolvaj mindig `Scout`, a pap és a mágus mindig `Cautious`; a harcos fele-fele eséllyel `Defensive` vagy `Aggressive`. Régi mentésből származó NPC alapértéke az első elhelyezéskor `Defensive`.

A partitársak mozgása a `Game` meglévő egyszálú eseményciklusában fut. Minden avatár saját következő mozgási időpontot kap: normál helyzetben 180–240 ms közötti kis véletlen eltéréssel lépnek; legalább öt mezős lemaradásnál 130–170 ms-ra és legalább nyolc mezőnél 90–120 ms-ra gyorsulnak. Az induló késleltetésük 80–240 ms között eltérő. Így nem egyszerre próbálják elfoglalni ugyanazokat a mezőket de nagy lemaradásból is gyorsan felzárkóznak.

A játék legfeljebb a vezér utolsó 256 sikeres pozícióját tartja nyilván. A vezetőt követő NPC-k nem annak pillanatnyi X/Y-koordinátája köré választanak célmezőt: a parti sorrendje szerint nyompontot céloznak és szélességi útkereséssel lépnek felé. A sorrend legfeljebb egy további lépésnyi formációs késést okoz ezért a hátsó társ sem marad látványosan messzebb. A speciális előremenő vagy ellenségre reagáló viselkedés után ugyanehhez a nyomvonalhoz térnek vissza.

Térképfókuszban három partiparancs írhatja felül a profilok mozgását:

- a `H` tartósan ki- és bekapcsolja a helyben maradást; aktív állapotban a társak nem kezdeményeznek mozgást vagy támadást de a rájuk lépni próbáló szörnnyel továbbra is automatikusan megküzdenek;
- a `Shift+H` tartósan ki- és bekapcsolja a szoros gyülekezőt: minden élő társ a vezér közvetlen szomszédos szabad mezőihez útvonalaz, majd a vezérrel együtt mozog, és közben nem tér el ellenfél vagy saját NPC-profilja miatt. Ha a közvetlen mezők megteltek, a többiek a lehető legközelebb zárkóznak fel. A parancs bekapcsolása megszünteti a helyben maradást és az aktív szétszóródást; aktív állapotát csak újabb `Shift+H` kapcsolja ki, ezért a `H` és `M` ilyenkor csak figyelmeztetést ad;
- az `M` 10 másodpercre szétszóródást rendel el: minden élő társ járható útvonalon legfeljebb tíz Manhattan-távolságra húzódik a vezértől és közben továbbra is felderít. Ez az időszak a `H` állapotát is ideiglenesen felülírja. Lejáratkor minden társ visszakapja a korábbi profilját; ha a `H` előtte aktív volt akkor ismét helyben marad.

- a defenzív társ legalább két vezérlépéssel korábbi nyompontot követ és így egy üres mezőt hagy közöttük; ötmezős rálátáson belüli szörny felé indul és mellé érve automatikusan megtámadja;
- az agresszív társ az előre eső tágas mezőket keresi és nem lép a vezető előtti szűk folyosóba; ötmezős rálátáson belüli szörny felé indul és mellé érve automatikusan megtámadja;
- a felderítő legfeljebb tíz mezőre halad a vezető előtt; ötmezős rálátáson belüli szörny észlelésekor visszatér a vezér nyomvonalára;
- az óvatos társ legalább két vezérlépéssel korábbi nyompontot követ; ellenség észlelésekor sem indul felé.

Minden társ ugyanazzal az ötmezős és falak/ajtók által takart látótérszámítással hívja a `FogOfWar.RevealFrom` műveletet. A társ induló környezete azonnal láthatóvá válik és minden sikeres NPC-lépés csak az újonnan felfedett valamint az elhagyott/elfoglalt térképcellákat rajzolja újra.

A karakterlap a jobb panel legfelső sorában kezdődik, ezért nem hagy kihasználatlan üres sort a fejléc felett. A faj és osztály alatt a megszerzett tehetségek két sorban jelennek meg; a neveket a renderer a két sor között osztja el és szükség esetén soronként egyenletesen rövidíti. A tíz hátizsáksor alatt három sort tart fenn a társaknak. Minden sor a karakter saját színével mutatja az osztály kezdőbetűjét, a nevet, a szintet és az aktuális/maximális HP-t. A vezető nem ismétlődik meg ezekben a sorokban.

A `Tab` vált a térkép- és karakterlapfókusz között. Karakterlapfókuszban a `KARAKTERLAP` cím zöld hátteret kap, a fel/le nyilak pedig minden felszereléshely, varázstárgyhely, hátizsákhely és partitárs között léptetik az aktív kijelölést; az üres helyek is célpontok. A kiválasztott sor DarkCyan hátteret kap, miközben megtartja saját előtérszínét; a renderer a logikai kategória és index alapján karakterenként külön megőrzi az utolsó kijelölést. Lépéskor csak a választható tárgy- és partisorok rajzolódnak újra, a teljes karakterlap és térkép nem.

Kijelölt partitársnál a `Del` megerősítést kér a végleges kirúgáshoz. Jóváhagyáskor a karakter felszerelésével együtt kikerül a rosterből és a partiból; élő avatárja vagy pályán maradt teteme eltűnik a térképről, mozgási időzítője pedig törlődik. Ha az ő karakterlapja volt megnyitva, a panel automatikusan visszavált a partivezérre.

Az `Esc` a térkép- és karakterlapfókuszból is megerősítést kér, mielőtt visszatér a főmenübe, mert a legutóbbi mentés utáni állapot elveszik. A célzó- és varázslatinformációs képernyőkön az `Esc` továbbra is csak az aktuális segédnézetet zárja be.

Karakterlapfókuszban a bal/jobb nyíl körkörösen vált a parti tagjainak teljes karakterlapja és inventoryja között. A három rögzített társsor mindig ugyanazokat a társakat mutatja; az éppen megtekintett társ sora `▶` jelölést kap. A vezető lapjának megtekintésekor nincs nyíl a társsorokban. A megtekintett lap nem változtatja meg a térképen irányított vezetőt.

Az inventory rögzített helyekből áll: két fegyverhely, egy páncélhely, három varázstárgyhely és tíz hátizsákhely. Minden `IItemDefinition` kategóriája fegyver, páncél, varázstárgy vagy általános tárgy. A felszereléshelyek csak a saját kategóriájukat fogadják el, a hátizsák bármelyiket. A `Space` kiemeli a kijelölt tárgyat, majd ugyanazon vagy másik partitag érvényes helyére teszi; foglalt cél esetén a két tárgy helyet cserél, ha a teljes csere után mindkét felszerelés érvényes. A fókusz elhagyása visszateszi a még kézben tartott tárgyat, így az nem veszhet el.

A fegyverek és páncélok ritkasága `Normal`, `Magic` vagy `Legendary`, a felületen Sima, Varázs és Legendás néven jelenik meg. A CSV-ben minden kézzel felvett felszerelés külön `Kategória`, opcionális `AlapId` és `MágikusErő` mezőt kap. A mágikus erő már adatként, menthető tárgydefiníció részeként rendelkezésre áll; a jelenlegi csatában a mágikus `+N` felszerelések megnövelt sebzés-/védelmi tartománya aktív, az általános mágikus-erő mechanika a későbbi varázsrendszer bővítési pontja.

A generált `+1/+2/+3` fegyverek és páncélok harci tartományának mindkét széle rendre 1/2/3 ponttal nő. Az eddigi 2×/4×/7× alapárhoz általában fix 500 arany mágikus felár adódik. A kámzsa (`A001`) és bőrvért (`A002`) kivétel: feláruk `MágikusErő × 500`, vagyis +1/+2/+3 fokozaton 500/1000/1500 arany. Egyedül a bunkónak (`W005`) nem készül generált mágikus változata; minden pajzs fejleszthető.

A `#Tárgybővítések` szekció határozza meg a név-utótagot, harci bónuszt, árszorzót és mágikus erőt. Betöltéskor minden Sima fegyverből és páncélból automatikusan létrejön a három Varázs változat. A `+1`, `+2`, `+3` bónusz a sebzés- vagy védelmi tartomány mindkét végére rákerül; az ár rendre az alapár 2×, 4× és 7× értéke. Így új alapfelszerelés vagy új bővítési fokozat hozzáadásához nem kell C# kódot módosítani.

A CSV ezen felül húsz egyedi nevű Legendás fegyvert és húsz Legendás páncélt tartalmaz. Ezek nem generált átnevezések: külön sebzésük/védelmük, kasztengedélyük, alapfelszerelés-hivatkozásuk, mágikus erejük, leírásuk és áruk van. A katalógus és a mentés már kezeli őket; későbbi pályatárgy-generálás közvetlenül ezekből a definíciókból válogathat majd.

### Varázstárgyak

A `#Varázstárgyak` szekcióban nincs Sima ritkaság: minden definíció legalább Varázs, az egyedi ereklyék Legendás kategóriájúak. A `MagicItemDefinition` mezői: altípus, ritkaság, alapár, maximális töltet, opcionális varázslat-ID, passzív hatás és érték, kasztengedély, jellemzés és mágikus erő. Négy altípus létezik: `Ring`, `Amulet`, `Wand`, `Scroll`.

A katalógus 57 varázstárgyat tartalmaz: 12 gyűrűt, 12 amulettet, 9 pálcát és 24 tekercset. A gyűrűk között pontosan öt, az amulettek között szintén öt egyedi Legendás darab van. A gyűrűk és amulettek felszerelve összeadódó passzív csatabónuszt adhatnak:

- `Initiative`: hozzáadódik a kezdeményezéshez;
- `Hit`: hozzáadódik minden fegyveres találati próbához;
- `Damage`: hozzáadódik a sikeres fegyveres támadás sebzéséhez;
- `Defense`: hozzáadódik az ellenfél támadásakor számított védelemhez;
- `BattleHeal`: minden csata kezdetén legfeljebb a maximumig HP-t tölt;
- `BattleMana`: minden csata kezdetén legfeljebb a maximumig mannát tölt.

A bónusz csak akkor él, ha a tárgy valamelyik varázstárgyhelyen van; hátizsákból nem hat. A felszerelési ellenőrzés a varázstárgy kasztengedélyét is ugyanabban az atomi inventory-ellenőrzésben vizsgálja, mint a fegyvereket és páncélokat.

Az eredeti 12 mágus- és 12 papi varázslathoz tartozik egy-egy tekercs. A további, varázsrendszert előkészítő definíciókhoz még nincs automatikusan varázstárgy. A tekercs pontosan egy töltetű: mágusiskolájú tekercset kizárólag Mágus, papi tekercset Pap vagy Lovag szerelhet fel és használhat; a Harcos, Barbár és Tolvaj nem használ tekercset. A kilenc pálca 3–8 töltetet és jelenleg mágusiskolájú varázslatot hivatkozik, de kaszttól és varázsiskolától függetlenül bárki felszerelheti és használhatja. A CSV-betöltő ellenőrzi a varázslat-ID létezését, a pálca több töltetét, valamint a tekercs pontosan egy töltetét.

A `SpellId` határozza meg a varázstárgyhoz kötött varázslatot. Ha használható tekercs vagy pálca van a karakter három varázstárgyhelyének egyikén, a `V` varázsválasztóban külön `📜 0M`, illetve `🪄 0M` sor jelenik meg. Ezekhez nem kell fókusztárgy, ismertség, memorizálás vagy manna. Varázshasználónál az eszközös varázslat a memorizált lista mellett jelenik meg; azonos varázslat esetén külön normál és eszközös sor választható. A célzás `Esc` megszakításakor semmi nem fogy. A célpont megerősítése után a tekercs eltűnik, a pálca aktuális töltete eggyel csökken; a harci koncentrációs kudarc is fogyaszt. A töltet az inventorymozgatáskor és karakterek közötti átadáskor a tárggyal mozog, a karaktermentés pedig slotonként tárolja. A 0 töltetű pálca megmarad, de nem jelenik meg a varázslistában. A gyűrűk és amulettek passzív hatásai továbbra is teljesen működnek.

Alapműködés
Minden karakternek:
- 3 felszerelt varázstárgyhelye van;
- 10 hátizsákhelye van;
- csak a felszerelt varázstárgyak fejtenek ki hatást;
- a hátizsákban lévő varázstárgy passzív bónuszt sem ad, és onnan pálca vagy tekercs sem süthető el.
A tárgyakat a karakterlapon, Space segítségével lehet mozgatni a hátizsák, a varázstárgyhelyek és a partitagok között. A kasztkorlátozást felszereléskor ellenőrzi a játék.
Jelenleg 57 varázstárgy van:
Típus	Darab	Működés
Varázsgyűrű	12	Állandó passzív bónusz
Amulett	12	Állandó vagy csata eleji passzív hatás
Varázspálca	9	Több töltetes, 0 mannába kerülő varázslás
Varázstekercs	24	Egyszer használható, 0 mannába kerülő varázslás


A kezdőfelszerelések között jelenleg nincs varázstárgy.
Gyűrűk és amulettek
Ezeknek nincs töltetük: amíg fel vannak szerelve, automatikusan működnek.
A lehetséges hatások:
- Initiative: hozzáadódik a harc eleji kezdeményezéshez.
- Hit: hozzáadódik a fizikai és a támadó varázslatok találati próbájához.
- Damage: hozzáadódik a fizikai támadás sebzéséhez.
- Defense: csökkenti a beérkező fizikai sebzést.
- BattleHeal: minden csata elején HP-t gyógyít.
- BattleMana: minden csata elején mannát tölt vissza, ha a karakter használ mannát.
A három felszerelt tárgy azonos típusú bónuszai összeadódnak. Nincs „csak egy gyűrű” vagy „csak egy azonos nevű tárgy” korlátozás, tehát akár három azonos bónuszú tárgy is viselhető egyszerre.
A gyógyítás és mannatöltés nem mehet a maximum fölé. Ha a karakter már tele van, a hatás gyakorlatilag elvész, és nem jelenik meg hozzá harci üzenet.
Varázspálcák
A pálca egy meghatározott varázslatot tárol, általában 3–8 töltettel.
Használata:
- fel kell szerelni valamelyik varázstárgyhelyre;
- a V varázslási menüben jelenik meg;
- minden kaszt használhatja;
- nem kell hozzá varázshasználó kaszt, varázskönyv, szent szimbólum, ismert vagy memorizált varázslat;
- nem fogyaszt mannát;
- elsütésenként egy töltetet fogyaszt.
A pálca ugyanazt a teljes varázslat-végrehajtást használja, mint a normál varázslás: célzás, hatótáv, támadó dobás, mentődobás és a varázslat összes hatása érvényesül. A hatás számításánál továbbra is a használó tulajdonságai – például az Intelligenciája – számítanak.
Az üres pálca:
- nem jelenik meg többé a varázslási menüben;
- nem tűnik el;
- továbbra is elfoglalja a varázstárgyhelyet;
- jelenleg nem tölthető újra.
Varázstekercsek
A tekercsek egyetlen töltettel rendelkeznek, és használatkor eltűnnek.
Kasztkorlátozás:
- mágusvarázslatot tartalmazó tekercs: csak mágus;
- papi varázslatot tartalmazó tekercs: csak pap vagy lovag.
A tekercs:
- nem igényli, hogy a karakter ismerje vagy memorizálja a varázslatot;
- nem fogyaszt mannát;
- a V menüből használható;
- a sikeres célpontválasztás után azonnal elhasználódik.
Fontos eltérés: a CSV-ben a papi tekercsek leírása még azt mondja, hogy „mágusok által használható másolat”. Ez már nem felel meg a tényleges működésnek: a kód papnak és lovagnak engedi őket felszerelni és használni.
Harci varázslási kudarc
Pálcára és tekercsre is vonatkozik a normál harci varázslási kudarc:
max(0, 30 − Intelligencia − Ügyesség)%
A töltet már a kudarc ellenőrzése előtt elfogy. Ez azt jelenti, hogy:
- sikertelen pálcahasználatnál elveszik egy töltet;
- sikertelen tekercshasználatnál a tekercs teljesen eltűnik;
- az akció is elvész.
Ha a játékos még a célzásnál visszalép, nem fogy töltet.
A papi „Isteni ítélet” és más, normál varázslásra kötött bónuszok tárgyból történő varázsláskor nem aktiválódnak. Az Időmegállítás csatánként egyszeri korlátozása viszont tekercsre és pálcára is érvényes.
Megszerzés és tárolás
Varázstárgyak jelenleg:
- szörnyzsákmányként eshetnek, ha az adott szörny zsákmánytáblája engedi;
- fogadói kereskedőnél megjelenhetnek;
- legendás tárgyként ritkábban kerülhetnek készletbe;
- generált karakterek felszerelésében vagy hátizsákjában is előfordulhatnak.
A zsákmány először hátizsákba kerül. Ha nincs szabad hátizsákhely a partiban, a földön marad.
A töltetszámot a játék:
- hátizsákba mozgatáskor;
- karakterek közötti átadáskor;
- mentéskor és visszatöltéskor
is megőrzi. Tehát egy félig elhasznált pálca mozgatással vagy mentés-visszatöltéssel nem töltődik újra.

### Varázslatdefiníciók és szintek

A `SpellDefinition` stabil azonosítót, nevet, `Arcane` vagy `Divine` iskolát, 1–5 közötti varázslatszintet, pozitív alap-mannaköltséget, leírást és célzási metaadatokat tartalmaz. Az `adatok.csv` `#Varázslatok` és `#Papi varázslatok` szekcióinak oszlopai: `Id`, `Név`, `Szint`, `Manna`, `Leírás`, `Célzás`, `Hatótáv`, `Terület`, `Látóvonal`, `HasználatiMód`. A célzás típusa `Self`, `Party`, `PartyMember`, `Enemy`, `Corpse`, `Cell`, `Area` vagy `Direction`; a használati mód `Exploration`, `Combat` vagy `Both`. Mindkét iskola mannaköltsége és leírása a tényleges CSV-s hatásokhoz van hangolva.

Mindkét iskolában pontosan 20 varázslat található, szintenként pontosan négy:

| Szint | Mágusvarázslatok | Papi varázslatok |
|---:|---|---|
| 1 | Mágikus lövedék; Fagyasztó érintés; Égő kéz; Lángoló nyíl | Gyógyító érintés; Szent fény; Áldás; Méregűzés |
| 2 | Villámcsapás; Láthatatlanság; Arkán páncél; Lassítás | Szent pajzs; Gyógyítás; Védelem a gonosztól; Betegségűzés |
| 3 | Tűzgolyó; Jégvihar; Teleportáció; Mágia szétoszlatása | Szent csapás; Isteni védelem; Megtisztítás; Bátorság imája |
| 4 | Villámvihar; Meteorzápor; Láncvillám; Kőbőr | Feltámasztás; Szent ítélet; Őrangyal; Tömeges gyógyítás |
| 5 | Időmegállítás; Dezintegráció; Dimenziókapu; Arkán kataklizma | Isteni csoda; Isteni harag; Szentély; Igazi feltámasztás |

A CSV-betöltő visszautasítja az 1–5 tartományon kívüli szintet, az ismeretlen célzás- vagy használatimód-nevet, a negatív hatótávot/területet, illetve azt az adatállományt, amelyben egy iskola nem pontosan 20 vagy valamely szint nem pontosan négy definíciót tartalmaz. A `GameDataCatalog.GetSpell` azonosító szerint, a `GetSpells(school, level)` pedig iskola és szint szerint szolgáltat definíciókat.

Az összetett működést az önálló `#Varázshatások` szekció írja le. Egy varázslathoz több, sorrendben végrehajtott `SpellEffectDefinition` tartozhat. A sor konfigurálja a hatástípust, kockát, intelligencia- és karakterszint-szorzót, állandó értéket, akciókban számolt időtartamot, esélyt, `Auto`/`Attack`/`SaveHalf`/`SaveNegates` feloldást és opcionális paramétert. A betöltő ellenőrzi az ID-ket, kockakifejezéseket, tartományokat és azt is, hogy mind a negyven varázslathoz legyen legalább egy hatás.

A mágikus támadás `d20 + Intelligencia + tárgyi találati bónusz` a szörny `11 + effektív Gyorsaság` értéke ellen; az Arkán fókusz további +2-t ad, a természetes 20 kritikus. Az ellenpróba célszáma `10 + floor(Intelligencia / 2) + varázslatszint`; siker esetén a `SaveHalf` felezi a sebzést, a `SaveNegates` teljesen kivédi a mellékhatást. Az Elemi mester a kiszámolt sebzést 25%-kal növeli.

Az implementált mágushatások lefedik az egycélpontos és területi sebzést, a kétmezős iránykúpot, égést és viharsebzést, sebességcsökkentést, minden második akció kihagyását, láthatatlanságot, arkán páncélt, kőbőrt és vérzésvédelmet, láncoló sebzést, varázshatás-szétoszlatást, ön- és partiteleportációt, csatánként egyszeri két extra akciót, kivégzési küszöböt és véletlen elemi mellékhatást. Az időzített hatások a karakter- és pályamentés részei; a fogadóban az élő karakterekről törlődnek.

Az implementált papi hatások gyógyítanak, Mérgezést/Betegséget/Vérzést tisztítanak, valamint találatot, fizikai sebzést, kezdeményezést, védelmet és sebzéscsökkentést adnak. A Szent fény, Szent csapás, Szent ítélet és Isteni harag élőholt (`MA001`) vagy démoni (`MA010`) célpont ellen 50%-kal nagyobbat sebez. A Védelem a gonosztól kizárólag ilyen támadó ellen ad +4 védelmet, 30% sebzéscsökkentést és mérgezés-/betegségvédelmet. Az Őrangyal az első halálos ellenséges csapást 1 HP-n kivédi és utólag gyógyít. A Szentély a varázslás pillanatában három mezőn belüli élő tagokra kerül; 50% sebzéscsökkentést és súlyosállapot-védelmet ad, de az adott karakter első fegyveres vagy támadó varázsakciójánál megszűnik.

### Aktív karakterbuffok és ikonjaik

A karakterlap `Áll:` sora a hagyományos állapotok mellett az aktív pozitív varázshatásokat is emojival jelzi. Az időtartam karakterenként, akciókban fogy: csatában az adott karakter saját harci akciója egy akció, felfedezéskor pedig ugyanazon karakter minden tizedik sikeres térképi lépése egy akció. A vezér és az automatikus partitagok külön lépésszámlálót használnak; a 0–9 közötti részszámláló a karaktermentés része. Sikertelen mozgás, inventorykezelés és információs képernyő nem fogyaszt időtartamot. Az Isteni ítélettel megerősített papi varázslat az alábbi alap-időtartamokat kétszerezi.

| Ikon | Aktív hatás | Forrás, érték és alap-időtartam |
|---|---|---|
| 👻 | Láthatatlanság | `Láthatatlanság`: 3 akció. Az ellenfél támadása automatikusan hibázik; az első saját támadás +5 találatot kap, majd a buff megszűnik. |
| 🛡️ | Védelmi bónusz | `Arkán páncél`: +5/5 akció; `Áldás`: +1/4; `Szent pajzs`: +5/4; `Isteni védelem`: +3/4. |
| 🪨 | Fizikai sebzéscsökkentés | `Kőbőr`: 50%/4 akció; `Isteni védelem`: 25%/4 akció. |
| 🩸🚫 | Vérzésimmunitás | `Kőbőr`: 4 akcióig megakadályozza a Vérzés felkerülését. |
| 🎯 | Találati bónusz | `Áldás`: +1/4 akció; `Bátorság imája`: +2/5; `Mézsör` vagy `Fűszeres bor`: +1/10. |
| ⚔️✨ | Fizikai sebzésbónusz | `Bátorság imája`: +2/5 akció. |
| ⚡ | Kezdeményezési bónusz | `Áldás`: +2/4 akció; `Bátorság imája`: +3/5; `Mézsör` vagy `Fűszeres bor`: +2/10. |
| ✝️🛡️ | Védelem a gonosztól | 5 akcióig élőholt és démoni támadó ellen +4 védelem, 30% sebzéscsökkentés, továbbá mérgezés- és betegségimmunitás. |
| 👼 | Őrangyal | 5 akcióig várakozik; az első halálos csapást kivédi, gyógyít, majd azonnal elfogy. |
| ⛪ | Szentély | 4 akcióig 50% sebzéscsökkentést és Mérgezés/Betegség/Vérzés elleni immunitást ad; a védett karakter első fegyveres vagy támadó varázsakciójánál azonnal megszűnik. |

Azonos típusú, különböző forrásból származó számszerű buffok összeadódnak. Ugyanaz a forrás ugyanazt a hatást újra alkalmazva frissíti a bejegyzést. Emiatt a Mézsör és a Fűszeres bor külön-külön frissíthető és egymással halmozható; mindkettő egyszerre adja a 🎯 +1 találatot és a ⚡ +2 kezdeményezést 10 akcióra.

A `Feltámasztás` 25% HP-val és 0 mannával, az `Igazi feltámasztás` teljes HP-val és 50% mannával teszi vissza ugyanazt a `LiveCharacter` példányt a tetemhez legközelebbi szabad mezőre. A karakter egy pályán legfeljebb egyszer térhet vissza; ez a jelző és a papi Isteni ítélet 0–4 közötti varázslatciklusa a karaktermentés része. Új pálya indításakor a feltámasztási korlát törlődik. Az Isteni ítélet ötödik papi varázslata célkiválasztáskor 0 mannába kerül; a csatabeli koncentrációs kudarc ezt az ingyenes alkalmat is elfogyasztja. Siker esetén a sebzés és gyógyítás kétszeres, az időzített hatások időtartama kétszeres, de a tisztítás és feltámasztás önmagában nem duplázódik.

### Varázslattanulás és memorizálás

Varázslatgyűjteménye kizárólag a Papnak (`C005`, `Divine`) és a Mágusnak (`C006`, `Arcane`) van. Ez szándékosan nem azonos a `UsesMana` szabállyal: a Lovag használ mannát, de nem tanul varázslatokat. A karakter két külön listát tárol:

- az ismert varázslatok tartós varázskönyvét;
- az ismert varázslatokból pihenéskor összeállított, aktuálisan memorizált készletet.
- nyolc, mentett gyorshelyet, amelyek kizárólag memorizált varázslatra mutathatnak.

A varázsláshoz kötelező kasztfókusz tartozik: a Mágus személyes `Varázskönyvet`, a Pap személyes `Szent szimbólumot` kap. Ez mindig a hátizsák első helyén van, karakterhez kötött, ezért nem mozgatható, dobható el, adható el vagy vásárolható meg, és a véletlen tárgygenerátorok sem választhatják. A korábbi mágikus kezdőtárgyak (`M003` Szent szimbólum és `M004` Tanonc pálcája) már nem kerülnek új karakterhez, piacra vagy véletlen felszerelésbe; kizárólag régi mentések feloldhatósága miatt maradnak adatdefinícióként. Betöltéskor a régi kezdőtárgy eltűnik, az új fókusz az első helyre kerül, a korábbi hátizsáktartalom pedig egy hellyel jobbra tolódik, amennyiben elfér.

Karakterlapfókuszban a fókusztárgyon nyomott `Enter` a jobb oldali karakterpanel helyén nyitja meg a varázslatinformációs oldalt. Ez felsorolja az ismert varázslatokat, külön jelöli a memorizáltakat és az `F1–F8` gyorshelyet, megmutatja a memória kapacitását, a kijelölt varázslat szintjét, mannaköltségét, célzástípusát és leírását, továbbá az 1–5. varázslatszint karakter-szintküszöbeit és a következő feloldást. A fel/le nyilak böngésznek, az `F1–F8` a kijelölt memorizált varázslatot rendeli a gyorshelyhez, az `Enter` a partivezér memorizált varázslatát indítja, az `Esc` pedig bezárja az oldalt.

A memorizálható különböző varázslatok száma egész osztással számolódik:

```text
2 + floor(Intelligencia / 3) + floor(karakterszint / 5)
```

Például 8 Intelligencián és 1. szinten ez `2 + 2 + 0 = 4`. Ugyanaz a varázslat nem foglalhat több helyet. A kézi karaktergenerálás végén a játékos a kaszt négy első szintű varázslatából pontosan hármat választ; ezek rögtön ismertek és memorizáltak. Gyorsindításnál, zsoldosnál és fejlesztői NPC-generálásnál a három kezdővarázslat automatikus.

Új varázslatszint az 1., 5., 10., 15. és 20. karakterszinten nyílik meg. Minden egyes elért karakterszinthez egy, az aktuális szinten már használható, még nem ismert varázslat tanulható. A vezető választóképernyőt kap, az NPC-k véletlenszerűen választanak. Több egyszerre elért szint külön tanulási alkalmakat jelent. Az ismert és memorizált varázslat-ID-k, valamint a gyorshelyek a karaktermentés részei; régi mentésből betöltött Pap vagy Mágus három determinisztikusan választott első szintű kezdővarázslatot kap. Memorizáláskor az új varázslatok automatikusan az első szabad gyorshelyekre kerülnek, a már érvényes kézi kiosztás megmarad.

### Varázslás és célzás

A partivezér a térképen `V`-vel nyitja meg a memorizált varázslatok színes választóképernyőjét, az `F1–F8` billentyűkkel pedig közvetlenül indítja a megfelelő gyorshelyet. A keskeny, legfeljebb tizenkét varázslatsort egyszerre mutató felugró panel a térkép közepére rajzolódik; előtte eltárolja a lefedett térképcellák rúnáját és színeit, bezárásakor pedig kizárólag ezeket állítja vissza. Így sem teljes konzoltörlés, sem teljes térkép- vagy karakterlapfrissítés nem történik. A választó megmutatja a szintet, mannaköltséget és célponttípust. Entitás-, mező-, terület- és iránycélzásnál az egy konzolcella széles `╳` célkereszt jelenik meg: a nyilak mozgatják, a `Tab` a CSV-s szabályoknak megfelelő érvényes célpontok között léptet, az `Enter` megerősít, az `Esc` megszakít. Érvényes célhoz a hatótáv, a már felfedett mező és szükség esetén a látóvonal is teljesüljön. Az önmagára és az egész partira ható varázslatok nem nyitnak célkeresztet.

Sikeres aktiváláskor a teljes CSV-s mannaköltség levonódik. Csatán kívül nincs külön koncentrációs kudarc. Csatában a varázslat a karakter fegyveres támadása helyetti teljes akció, és a mannaköltség levonása után százalékos kudarcpróba történik:

```text
kudarc esélye = clamp(30 - Intelligencia - Ügyesség, 0, 100)%
```

Ha a `d100` eredménye legfeljebb a kudarc esélye, a varázslat meghiúsul, a manna és az akció elvész. Siker esetén a játék végrehajtja a CSV-ben sorolt hatásokat, és a naplóban összegzi a célpontonkénti sebzést, próbát, kontrollt vagy helyváltoztatást. A sebző és időzített mágushatások csatán kívül is működnek; az ellenfelek saját mozgási akciójuk elején szenvedik el a körönkénti sebzést. A karakterlap csata közbeni részleges frissítése a mágikus védőhatásokat is emojival jelzi.

### Pihenés a labirintusban

A `P` billentyűvel pályánként pontosan egyszer lehet pihenni. A pihenés csak akkor indul el, ha a vezető egy szoba belsejében áll, minden élő partitag ugyanabban a szobában van, nincs bent élő ellenfél, a szobának van ajtaja, és minden hozzá tartozó ajtó `Locked` állapotú. A felhasznált pihenési lehetőség a teljes játékmentés része.

Pihenéskor minden élő partitag 1d10 HP-t gyógyul a normál gyógyulásmódosítókkal, a mannája az aktuális maximumra töltődik, továbbá 10 élelem- és 10 vízpontot fogyaszt. A betegségre, mérgezésre és vérzésre egymástól függetlenül `30 + Egészség × 2` százalék eséllyel történik gyógyulási próba; siker esetén az adott állapot megszűnik. Ezután a Papok és Mágusok újra összeállíthatják memorizált készletüket. A pihenés végén a szoba ajtajai `Closed` állapotba kerülnek, és újraindulnak a szükséglet-, szörny- és partitárs-időzítők.

A `#Fegyverek` és `#Páncélok` CSV-szekció kasztoszlopai határozzák meg, mely osztályok viselhetik az adott tárgyat. A fegyvereknél a Harcos, Barbár és Lovag, a páncéloknál a Harcos és Lovag alapértelmezetten engedélyezett; a többi kaszt engedélyét az `igen` érték adja. Minden fegyvernek 1–13 közötti `MinimumErő` értéke is van, és csak legalább ekkora Erővel szerelhető fel. A mágikus fejlesztések öröklik az alapfegyver követelményét. A korlátozás csak a felszereléshelyekre vonatkozik, hátizsákban bármely karakter hordozhat bármilyen tárgyat. Az ellenőrzés központilag a `LiveCharacter` végleges, tervezett inventoryállapotán fut, ezért a kézi mozgatásra és cserére, a kezdőfelszerelésre, a mentés betöltésére és a véletlen NPC-felszerelésre is érvényes.

A kétkezes fegyver kizárólag az első fegyverhelyen viselhető. Amíg ott kétkezes fegyver van, a második fegyverhelynek üresnek kell lennie és a karakterlapon `⛔` lezárásként jelenik meg. Kétkezes fegyver csak üres második hely mellett szerelhető fel; a második hely pedig nem tölthető fel, amíg az elsőben kétkezes fegyver marad. A hátizsákban ez a korlátozás sem érvényes. Minden kétkezes fegyver páncéltörő: találatkor az ellenfél teljes, képességbónuszokkal növelt páncéljának felét figyelmen kívül hagyja lefelé kerekítve. A sebzésből ezért `ceil(páncél / 2)` vonódik le; a csatanapló az eredeti és a tényleges páncélértéket is mutatja.

Az `I` a kijelölt tárgy összes jelenleg ismert adatát az alsó üzenetnaplóba írja: név és stabil ID minden tárgynál; fegyvertípus, sebzés, egy-/kétkezes jelleg és engedélyezett kasztok a fegyvereknél; védelem és engedélyezett kasztok a páncéloknál; valamint a CSV-ből betöltött jellemzés. A fegyverekhez és páncélokhoz tartozó szöveg a `Jellemzés`, az általános tárgyaknál szintén a `Jellemzés` nevű oszlopból érkezik.

Az `I` ezen kívül kijelzi a Sima/Varázs/Legendás ritkaságot, a mágikus erőt és az alapárat. Használati tárgynál a hatást és annak számszerű értékét is megmutatja.

Az `Enter` a megtekintett karakter kijelölt hátizsáktárgyát használja el. Az ételek 15–100 élelem-, az egyszerű italok 30–40 vízpontot töltenek; a Gyógytea 60 vizet és 5–15 HP-t ad. A titkos raktár Mézsöre és Fűszeres bora 40 víz mellett 10 akcióra 🎯 +1 találatot és ⚡ +2 kezdeményezést biztosít, ezért teljes víznél is elfogyasztható. A három gyógyital 20/50/120 HP-t, a három varázsital 15/40/90 mannát állít helyre. Az ellenméreg a mérgezést, a gyógyfüves orvosság a betegséget, a kötés a vérzést szünteti meg. A tárgy csak sikeres, tényleges hatás esetén fogy el: teljes HP-n nem vész el gyógyital, nem varázshasználónál varázsital, illetve hiányzó állapotnál gyógyító kellék.

Ha a kijelölés egy partitárs sorára esik akkor az `I` a társ nevét és magyar mozgásprofilját írja az üzenetnaplóba.

A `D` a kijelölt tárgyat a parti vezetőjének aktuális térképmezőjére dobja. A `GroundItemPile` egy pozíción tetszőleges számú tárgyat tárol, a térképen cián `◆` jel mutatja; a halom nem akadályozza a mozgást. A földi halmok a labirintusszint futásidejű állapotához tartoznak, új pályán megszűnnek, a teljes játékmentésben viszont megmaradnak. A vezető a halmon állva `K`-val próbálja a tárgyakat az élő parti hátizsákjaiba venni, a vezértől kezdve; ami továbbra sem fér el, a földön marad.

A rejtett `Ctrl+Shift+Y` fejlesztői gyorsbillentyű Harcos–Mágus–Lovag, a `Ctrl+Alt+X` pedig Barbár–Tolvaj–Pap sorrendben tölti fel a parti szabad helyeit. A `RandomCharacterGenerator` minden társhoz:

- a gyorsbillentyűhöz rögzített osztály mellett érvényes véletlen faj–képesség kombinációt készít;
- az osztály CSV-s névkészletéből lehetőleg még nem használt nevet választ;
- 2–30. szint közé fejleszti a normál HP-/mannadobásokkal;
- szintjének megfelelő eséllyel választ tehetségeket;
- véletlen fegyvereket, páncélt és hátizsáktartalmat ad; a három varázstárgyhelyre a varázshasználóknál egy pálca, egy használható tekercs és egy passzív tárgy, másoknál két pálca és egy passzív tárgy kerül; kétkezes első fegyvernél a második hely üres marad;
- véletlen NPC-mozgásprofilt rendel hozzá.

A rejtett `Ctrl+Shift+Í` fejlesztői gyorsbillentyű — ha van szabad hely — pontosan egy új NPC-t ad a partihoz. A karakter 1. szintű marad és kizárólag az osztály `#Osztály kezdőfelszerelés` CSV-s szabálya szerinti alapfelszerelést kapja; véletlen magasabb szintet és extra felszerelést nem.

## Labirintusgenerálás

A generátor kezdetben falakkal tölti fel a pályát, majd rekurzív mélységi bejárással összefüggő folyosóhálózatot vés ki egy ötlépéses logikai rácson. A csomópontok két cella szélesek; az összekötő folyosók a konfigurált valószínűséggel kétcellásak.

Ezután a generátor:

1. a bejárat körül garantált 3×3-as kezdőtermet alakít ki;
2. véletlen méretű további szobákat próbál elhelyezni;
3. ajtóval kapcsolja őket a meglévő járatokhoz;
4. útkereséssel ellenőrzi, hogy a bejárat és kijárat kapcsolata megmaradt-e;
5. elhelyezi a kijáratot;
6. üres, járható cellákon ládákat és konfigurált ellenfeleket helyez el.

A kezdőterem védett: más szoba fala nem írhatja felül, és nem kerülhet bele láda vagy ellenfél. A 3×3-as járható belső teret külön falburok veszi körül, a korábban kivésett folyosókapcsolatok helyén ajtókkal. A vezető a terem középső celláján áll, ezért egyik oldalán sem kezd közvetlenül fal mellett. A legfeljebb három társ elsőként a távolabbi sarokcellákat foglalja el, így nem zárják körül a vezetőt.

## Pályavége és fogadó

A kijárat elérésekor a játék még a pályaszám növelése előtt lezárja az aktuális labirintusszintet. Az egy karakternek járó teljesítési jutalom:

```text
teljesítési XP = BaseLevelCompletionExperience × teljesített pályaszám
```

Ezt az összeget minden életben maradt partitag külön és teljes egészében megkapja; itt nem érvényes a harci 60/40-es XP-elosztás. Minden túlélő karakter saját osztálymódosítója és szintlépési HP-/mannadobása dolgozza fel a jutalmat. A vezető szintlépése a megszokott tehetségválasztási folyamatot is elindíthatja. A halott társak nem kapnak teljesítési XP-t.

Jutalmazás után a parti a fogadóban pihen: kizárólag a túlélők aktuális HP-ja és mannája töltődik maximumra. A 0 HP-s társ halott marad; a pálya végén kikerül a partiból és a karakter-nyilvántartásból, tehát végleg elveszik. A középre igazított színes pályavége képernyő megmutatja a képletet és összeget, karakterenként az XP-t, szintváltozást és feltöltött erőforrásokat, továbbá külön megemlékezik az elvesztett társakról. Enter vagy Space nyitja meg a fogadó kereskedőjét; a piacról `Esc` a toborzáshoz vezet. A toborzás után minden túlélő Pap és Mágus memorizálhat, így az újonnan csatlakozott zsoldos is felkészíthető. Ezután következnek a pletykák, végül `Enter` vagy `Esc` a következő pályára visz.

### Fogadói kereskedés

Minden `IItemDefinition` pozitív `BasePrice` alapárral rendelkezik, amely közvetlenül az `adatok.csv` megfelelő sorából származik. Hiányzó, nulla vagy negatív ár betöltési hibát okoz. Az árskála az egyszerű ellátmány néhány aranyas tartományától az alapfegyvereken és vérteken át a több tízezer aranyas legendás felszerelésekig terjed; a legerősebb legendás gyűrűk és amulettek szintén ritka és drága fogadói ajánlatok.

A fogadó minden látogatáskor új, véletlen piacot készít. A kereskedő normál és mágikus tárgyainak eladási ára 80% eséllyel az alapár 105–150%-a, 20% eséllyel kedvezményes 85–100%. A parti tárgyaiért jóval kevesebbet, az alapár véletlen 40–70%-át kínálja. Az ajánlatok az adott fogadólátogatás teljes ideje alatt stabilak, ezért a nézetváltással nem dobhatók újra; a visszavásárlási ár mindig alacsonyabb a lehetséges eladási árnál.

A piac `←`/`→` vagy `Tab` billentyűvel vált a vásárlás és eladás között, `↑`/`↓` választ, az `Enter` végrehajtja az üzletet. Eladáskor a teljes parti hátizsákjainak tárgyai láthatók a tulajdonos nevével; a felszerelt tárgyak előbb az inventoryban tehetők hátizsákba. A bevétel és kiadás a partyvezér aranyát módosítja. Vásárláskor a tárgy először a vezér első üres hátizsákhelyére kerül, telt hátizsáknál pedig parti-sorrendben a következő szabad hellyel rendelkező társ kapja. Ha az összes hátizsák tele van, a vásárlás meghiúsul és arany nem fogy.

A készlet a nem legendás tárgyak alapár szerint rendezett, fokozatosan feloldódó részéből készül. A teljesített pálya növekedésével nyolc újabb, jellemzően értékesebb tárgytípus kerülhet a jelöltek közé, a tényleges kínálat pedig pályánként egy hellyel nő, legfeljebb tizenkettőig. A súlyozott választás a feloldott készleten belül az értékesebb tárgyakat részesíti előnyben, így később több és jobb portéka jelenik meg anélkül, hogy az olcsó ellátmány teljesen eltűnne.

Legendás tárgy külön ritka dobással kerülhet a fogadóba: az esély az első pálya után 1.5%, pályánként további 0.5 százalékponttal nő, és legfeljebb 8%. Egy látogatáskor legfeljebb egy Legendás ajánlat jelenik meg, az alapár 125–180%-áért. A választható Legendás készlet pályánként bővül, így korán csak az olcsóbb legendák kerülhetnek elő.

### Kovácsmester és Páncélmíves

Fogadóba érkezéskor a Kovácsmester és a Páncélmíves egymástól független 50%-os jelenlétdobást kap. A fogadós a fő fogadói menüben közli, hogy egyikük, mindkettőjük vagy egyikük sem érkezett meg; csak a jelen lévő mesterek kapnak választható menüpontot. A Kovácsmester kizárólag fegyvert, a Páncélmíves kizárólag páncélt ad el, visszavásárlás nélkül.

Mindkét mester készlete egyenletes 2–4 darabos kezdődobásból és `floor(teljesített pálya / 3)` további tárgyból áll. A készlet és minden tétel ára már a fogadóba érkezéskor rögzül; az ár az adott definíció alapárának egymástól független 90–150%-a, és semmilyen más fogadói árszorzó nem módosítja. A kínálat ár szerint növekvő sorrendben jelenik meg.

A 4. pályától egy mágikus készlethely nyílik, az 5. pályától kettő, a 10. pályától három, a 15. pályától négy. A mágikus készlethelyek a 4–7. pályán `+1`, a 8–11. pályán `+2`, a 12. pályától legfeljebb `+3` mágikus erejű felszerelést választanak. A 10. pályától mesterenként 50% eséllyel pontosan egy mágikus készlethelyet az adott mester kategóriájába tartozó Legendás tárgy vált fel.

### Fogadói toborzás

A kereskedés után minden fogadólátogatáskor 1–3 zsoldos jelenik meg. A rendszer először ugyanennyi különböző osztályt választ, majd osztályonként addig dob fajt és képességeket, amíg a karakter teljesíti az adott osztály minimumait. A jelöltek neve a karakter-nyilvántartásban és az adott ajánlatban is egyedi, amíg az osztály névkészlete ezt lehetővé teszi.

A vezérnél alacsonyabb szintű zsoldos ingyen csatlakozik. Azonos vagy magasabb szinten az alap felbérlési díj `zsoldos szintje × 100` arany, amelyre fogadólátogatásonként egyszer kisorsolt 50–150%-os szorzó kerül. Az ajánlati ár a toborzóképernyő használata közben nem változik. Ha nincs elég arany, a felvétel meghiúsul, és teljes parti esetén a régi társ kiválasztása és elvesztése sem történik meg.

A zsoldos célpontszintje a partyvezér aktuális szintje körüli zárt ±3 tartományból készül, a játékadatokban elérhető szintekre szorítva. A karakter a szintlépés normál HP-/mannadobásait és a szintjéhez illő véletlen tehetségeket kapja. Alacsony szinten az osztály CSV-s kezdőfelszerelését viseli; a szint emelkedésével növekvő eséllyel annak nem legendás, mágikus továbbfejlesztéseit kaphatja meg. Hátizsákjában pontosan 1–3 véletlen használati tárgy van, például étel, ital, gyógyital, varázsital, ellenméreg, orvosság vagy kötés.

Szabad partihely esetén az `Enter` azonnal felveszi a kijelölt zsoldost. Négyfős partinál előbb ki kell választani a lecserélendő, nem vezető társat. A lecserélt karakter kikerül a partiból és a központi karakter-nyilvántartásból, ezért végleg elveszik; a csere képernyője `Esc`-pel következmény nélkül megszakítható.

### Fogadói pletykák

A toborzás után a fogadós egy véletlen pletykát mutat. Az `N` billentyűvel legfeljebb három alkalommal kérhető új pletyka; az ajánlatok nem kerülnek aranyba. A kezdő pletykával együtt így egy fogadólátogatás során legfeljebb négy információ olvasható. A rendszer lehetőség szerint nem ismétli meg ugyanazt a teljes pletykaszöveget. `Enter` vagy `Esc` lezárja a fogadót és elindítja a következő pályát.

A pletykáknak két típusa van:

- **úti pletyka:** a következő szint nevét, szobaszámát és -méretét, folyosójellegét, falstílusát, összes konfigurált ellenféltípusát és csoportvezéreit ismerteti;
- **szörnypletyka:** a teljesített szint előtti, aktuális vagy következő szint találkozásaiból választ egy ellenfelet, majd kiírja a térképjelét, erősségét, HP-ját, Erejét, Páncélját, Gyorsaságát, XP-jutalmát, számított mozgási periódusát, továbbá minden képességének nevét, aktiválási esélyét, értékét és CSV-s leírását.

A pletyka mindig az aktuális `MazeLevelConfigurations` és `GameDataCatalog` adataiból készül, ezért a pályák vagy ellenfelek későbbi hangolása automatikusan megjelenik benne; nincs külön, könnyen elavuló kézzel írt pletykaadatbázis.

A rejtett `Ctrl+Shift+E` fejlesztői gyorsbillentyű a partyvezért a kijárat melletti, járható és objektumtól mentes mezők közül a hozzá legközelebbire teleportálja. A teleport frissíti a vezér útvonalát és a látómezőt is; ha nincs megfelelő szabad mező, csak naplóüzenet jelenik meg.

Az első tizenegy labirintusszint külön, találkozásalapú konfigurációval rendelkezik. A 12. szinttől a rendszer fokozatosan növekvő szobaszámot és jutalmat, valamint erősségi szakaszonként cserélődő homogén, vegyes és vezércsoportokat képez. A találkozások stabil ellenfél-ID-ket használnak, így a pályák összetétele kiszámíthatóan hangolható.

| Szint | Téma | Fal | Szín | Dupla folyosó esélye | Fő ellenfelek |
|---:|---|:---:|---|---:|---|
| 1 | Patkányjáratok | `█` | sötétszürke | 95% | patkányok, koboldok, goblinok |
| 2 | Patkányvezér | `█` | sötétszürke | 40% | óriáspatkányok, koboldok, csontváz, patkányember vezér |
| 3 | Goblinüregek | `▓` | sötétzöld | 75% | koboldok, goblinok, farkasok |
| 4 | Vadállatok odúi | `▒` | sötétsárga | 70% | goblinok, csontvázak, zombik, ork vezér |
| 5 | A holtak katakombái | `▓` | sötétszürke | 82% | csontvázak, zombik, ghoul vezér |
| 6 | A nagy csarnokok szintje | `▦` | sötétsárga | 20% | orkok, hobgoblinok, ogre vezér |
| 7 | A mérgező barlang | `▒` | sötétcián | 88% | pókok, nyálkák, gyíkok, baziliszkuszok |
| 8 | Az ork haditábor | `▓` | sötétpiros | 78% | orkok, hobgoblinok, bugbearek, sámánok |
| 9 | Az elátkozott sírkamrák | `▦` | sötétmagenta | 92% | múmiák, ghoulok, wightok, éji banyák |
| 10 | Az óriások erődje | `▩` | szürke | 12% | ogrék, trollok, ettinek, fagyóriás |
| 11 | A sárkánykultusz szentélye | `▥` | piros | 80% | wyvernek, kimérák, ork sámánok, vörös sárkány |

A `MazeLevelConfiguration` a fal egyetlen konzolcellás `Rune` karakterét, `ConsoleColor` színét és a pálya megjelenített nevét is tartalmazza. Ezek bekerülnek a `MazeGenerationSettings` és a futásidejű `Maze` objektumba. A járhatóság és látóvonal az adott példány `WallRune` értékét használja, nem egy rögzített `█` karaktert; a renderer az adott pálya falszínével rajzol. A fal karaktere, színe és pályanév a teljes játékmentés része, a régi mentések pedig `█`, sötétszürke és „Labirintus” alapértékkel tölthetők be.

A `DoubleWidthCorridorChance` a legtöbb pályán 0,7–0,95 között marad. Tematikus kivétel a nagy csarnokok 0,20-as és az óriások erődjének 0,12-es értéke: ezekben ritkábbak a két cella széles összeköttetések, miközben a szobák jóval nagyobbak és számosabbak.

A kampány zárópályája a 21. szint, „A Káoszrubin rejtekhelye”. A 20. szinten Kael-Zhur, a Káoszsárkány őrzi a tizenkettedik kulcsot; az utána megnyíló rejtekhelyen a káosz a korábbi világok lényeit vegyes csoportokba sodorja. Mitikus szörnyek, élőholtak, bestiák, sárkányok és démonok együtt őrzik a nagy, magenta kristályfalú termeket. A kijárat maga a Káoszrubinhoz vezető végpont: tizenkét aranykulcs nélkül nem aktiválható, mind a tizenkettő birtokában pedig fogadó és új pályagenerálás helyett elindítja a XV., befejező fejezetet. A finálé név és kaszt szerint külön méltatja a parti minden életben maradt tagját, majd győzelemmel lezárja a futamot.

## Szörnyek erőssége és képességei

Az `adatok.csv` `#Ellenségek` szekciója 50 szörnydefiníciót tartalmaz. Minden sor 1–5 közötti `Erősség` értéket és egy vagy két `#Szörnyképességek`-azonosítót tárol. A betöltő hibát jelez tartományon kívüli erősségnél, kettőnél több képességnél vagy ismeretlen képességhivatkozásnál. Az erősség nem módosítja automatikusan a statisztikákat: a HP, Erő, Páncél, Gyorsaság és XP továbbra is külön hangolható; a szint a pályagenerálás számára használható besorolás.

A térképi szörnyrúnák erősség szerinti színe:

| Erősség | Szín |
|---:|---|
| 1 | zöld |
| 2 | sárga |
| 3 | sötétsárga |
| 4 | piros |
| 5 | magenta |

A `MonsterAbilityDefinition` azonosítót, nevet, hatástípust, 0–100%-os aktiválási esélyt, értéket és leírást tartalmaz. Jelenlegi aktív hatások:

- `Poison`, `Disease`, `Bleeding`: sikeres szörnytámadás után a CSV-s eséllyel hozzáadja a Mérgezés, Betegség vagy Vérzés karakterállapotot;
- `ExtraDamage`: sikeres találatkor a megadott eséllyel hozzáadja a konfigurált extra sebzést;
- `InitiativeBonus`: állandóan hozzáadódik a szörny kezdeményezéséhez;
- `ArmorBonus`: állandóan hozzáadódik a szörny páncéljához.

A `Trait` hatású Élőholt, Regeneráció, Repülő és Démoni képesség már típusos adatként elérhető, de önmagában még nem hajt végre általános csata- vagy mozgáshatást. Az Élőholt (`MA001`) jelölést az Áldott fegyver tehetség már használja: papnál +2 találatot és +2 sebzést ad az ilyen ellenfelek ellen. A regeneráció körönkénti gyógyítása, a repülés terepszabálya és a démoni kategória további hatásai későbbi bővítések.

### Szörnyzsákmány és keresés

Az ellenfél halálakor `MonsterCorpse` kerül a pályára, amely megőrzi a szörny definícióazonosítóját és azt, hogy átkutatták-e már. Ez minden halálútnál azonos: vezéri csata, automatikus NPC-csata és felfedezés közbeni varázssebzés után is kereshető tetem marad. A tetemen állva a `K` pontosan egyszer sorsolja ki a zsákmányt; az eredmény és az átkutatottság a teljes játékmentés része. Partitárs teteme nem fosztható ki, a definíció nélküli régi tetem pedig nem generál új zsákmányt.

A `#Zsákmány paraméterek` globális alapszabályai:

```text
kulcs alap-esélye       = 10%
arany alap-esélye       = 40%
arany mennyisége        = 1..(szörny Erősség × 10)
tolvaj esélyszorzója    = 130%
Intelligencia-bónusz    = +1 százalékpont / Intelligencia
```

Az esélyszámítás sorrendje `floor(alapesély × tolvajszorzó) + Intelligencia-bónusz`, 0–100%-ra korlátozva. A tolvajszorzó csak akkor él, ha maga a kereső partyvezér Tolvaj; az Intelligencia minden osztálynál hozzáadódik. Például egy 10 Intelligenciájú Tolvaj egy 40%-os felszerelésesélyt `40 × 1,30 + 10 = 62%` eséllyel old fel.

A `#Szörny zsákmány` szörnyenként beállítja az egy darab felszerelés alap-esélyét, az engedélyezett Fegyver/Páncél/Varázstárgy kategóriákat, a minimum és maximum ritkaságot, a maximális mágikus erőt és az alapár felső korlátját. A kategória és a megfelelő tárgy véletlen; személyes varázsfókusz nem sorsolható. A Goblin 40%-os alapeséllyel legfeljebb 100 arany értékű sima fegyvert vagy páncélt, a Fekete sárkány 95%-os alapeséllyel akár 10-es mágikus erejű, 30 000 aranyig terjedő Varázs vagy Legendás felszerelést adhat. A konfiguráció nélküli szörny kulcsot és aranyat továbbra is dobhat, felszerelést nem.

A megtalált tárgyak sorban az élő party hátizsákjaiba kerülnek. Ha minden hátizsák tele van, `GroundItemPile` formájában a tetem mezőjén maradnak. Ugyanez a keresési művelet veszi fel a korábban kézzel ledobott tárgyakat is; az arany közvetlenül a partyvezérhez kerül.

A térképi kincsesláda felvételekor külön főnyereménydobás történik. A `#Zsákmány paraméterek` 10%-os alapesélyét ugyanaz a Tolvaj-szorzó és Intelligencia-bónusz növeli, mint a tetemkeresést. Siker esetén a láda aranyjutalma háromszoros. A Tolvaj Mestertolvaj tehetségének kétszerezése ezzel halmozódik, ezért a két hatás együtt hatszoros jutalmat ad. Az alap-esély és a főnyeremény-szorzó is CSV-ből hangolható.

### Ajtók

Az ajtó nem egyszerű térképrúna, hanem `MazeDoor` állapotobjektum. Négy állapota van:

| Állapot | Jel | Járható | Újra zárható |
|---|---:|---:|---:|
| Kulcsra zárt | `╫` | nem | igen |
| Nyitott | `╱` | igen | igen |
| Zárt | `╬` | nem | igen |
| Bezúzott | `▒` | igen | nem |

A kezdőterem ajtaja mindig nyitott. A további szobaajtók generáláskor 80% eséllyel kulcsra zártak, 10% eséllyel zártak és 10% eséllyel nyitottak. A zárt és kulcsra zárt ajtó a mozgást és a látóvonalat is blokkolja.

Ajtó mellett a vezető az `N` billentyűvel nyit, a `Z` billentyűvel bezár, a `K` billentyűvel kulcsra zár. A `K` helyzetfüggő: ha a vezető tetemen vagy földi tárgyhalmon áll, előbb a keresés/felvétel történik, ezért ilyenkor nem kezeli a szomszédos ajtót. A simán zárt ajtó szabadon nyitható. Kulcsra zárt ajtónál a nyitási sorrend:

1. a `T003` kulcs garantáltan nyit és eltűnik a hátizsákból; tolvajnál előtte térképre rajzolt modális ablak kérdezi meg, hogy valóban felhasználja-e;
2. kulcs nélkül, illetve a kulcs használatának elutasításakor a tolvaj százalékos Ügyesség-próbát tesz;
3. sikertelen zárnyitás vagy más osztály esetén `1d20 ≤ Erő` próba következik, amely siker esetén végleg bezúzza az ajtót.

A tolvaj kulcsválasztó ablaka a varázslás ablakához hasonlóan csak az alatta levő térképcellákat menti el és állítja vissza. `I`, `Y` vagy `Enter` használja a kulcsot; `N` vagy `Esc` megtartja és a zárnyitást választja. A kulcs nélküli nyitás egyetlen `N` lenyomásra egy próbának számít akkor is, ha a sikertelen tolvajpróbát rögtön erőpróba követi. A próba egymástól függetlenül 1–2 élelmet és 1–2 vizet fogyaszt; a minimumok és maximumok a `#Ajtópróba paraméterek` szekcióból hangolhatók. Kulccsal történő nyitás és a simán zárt ajtó kinyitása nem fogyaszt szükségletet.

Ha nem tolvaj partyvezér nyitna kulcsra zárt ajtót, a játék legfeljebb két mező Chebyshev-távolságon belül megkeresi a legnagyobb Ügyességű élő NPC tolvajt. A segítő a saját kulcsát használhatja a kulcsválasztó ablakban, vagy a saját Ügyességével tesz zárnyitási próbát; a szükségletköltséget is ő fizeti. Sikertelen próbája után egy második, térképre rajzolt ablakban a játékos dönt arról, hogy a vezér megpróbálja-e Erőből bezúzni az ajtót. Elutasításkor az ajtó zárva marad, és nem történik automatikus erőpróba.

A tolvaj zárnyitási esélye 10 Ügyességnél 90%, 11-nél 93%, 12-nél 96%, 13-nál 100%; alacsonyabb értéknél fokozatosan csökken. Kulcsra záráshoz egy elfogyó kulcs vagy tolvaj osztály szükséges. Minden művelet, dobás és a levont élelem/víz eredménye az alsó üzenetnaplóban jelenik meg. Jelenleg mindig a parti vezetője kezeli az ajtót.

## Látómező és köd

A `FogOfWar` pályánként külön logikai tömbben tárolja a már felfedezett cellákat. A játékos körül 5 cellás Chebyshev-távolságon belül Bresenham-jellegű látóvonal-ellenőrzés történik. A fal és a zárt vagy kulcsra zárt ajtó látható lehet, de blokkolja a mögötte lévő cellákat; a nyitott és bezúzott ajtó nem blokkol.

A rendszer a két már felfedezett végpont közötti, legfeljebb háromcellás rövid ködcsíkot automatikusan kitölti, kivéve ha ajtó van benne. A `Ctrl+Shift+U` csak a megjelenítés számára fedi fel vagy rejti vissza a teljes térképet; a tényleges felfedezettségi adatokat nem írja át.

## Csata algoritmusa

A csata váltott akciókból áll. A vezér minden saját akciójánál döntési promptot kap: a szóköz fegyveres támadást indít, használható harci varázslat esetén a `V` vagy `F1–F8` varázslást választ, jogosult papnál/lovagnál pedig a `T` halottűzést. A prompt nem belső billentyűvárakozás: a csata visszatér a fő ciklusba, és csak az aktuális `BattleId`/`TurnId` értékhez elfogadott parancs lépteti tovább. A varázslás teljes akciót használ, és a fenti Intelligencia- és Ügyességalapú kudarcpróbát végzi. Az NPC-csata megszakítás nélkül, egyelőre kizárólag fizikai támadásokkal fut le és csak egy végeredmény-összefoglalót ír a naplóba. Menekülés nincs. Mindkét út ugyanazt a `BattleSystem` algoritmust és a játék közös `Random` példányát használja.

A részletes vezéri csatanapló csak a ténylegesen érvényesülő nem nulla tehetségbónuszokat írja ki. A nulla gyógyítás/mannatöltés és a nulla támadó- vagy védelmi tehetségérték nem foglal helyet a naplóban.

A vezér csatájában minden megjelenített harci esemény után részlegesen frissül a karakterlap állapot-, HP- és mannasora. A naplóesemények nem állítják meg külön Space-várakozással a sessiont; az állapotgép a következő emberi döntésnél vár. A többi karakterlapsor és a térkép nem rajzolódik újra, így a kör közben változó állapotok és erőforrások azonnal láthatók maradnak fölösleges teljes képernyős frissítés nélkül.

A defenzív és agresszív NPC a saját mozgási időpontjában aktívan megtámadja a szomszédos szörnyet. Bármely profil automatikusan visszaharcol akkor is ha egy szörny az ő mezőjére próbál lépni. NPC-győzelemkor a szörny holttestté válik és az egyetlen összefoglaló üzenet parttagonként mutatja az XP-részesedést valamint az esetleges szint- és erőforrásnövekedést. NPC-vereségkor a karakter 0 HP-val a partiban marad, a partistátusz `💀` jellel mutatja, térképi avatárja pedig az elesés helyén `PartyMemberCorpse` objektummá alakul. Ez megőrzi a `LiveCharacter` hivatkozást, így a későbbi feltámasztás varázslat ugyanazt a karaktert állíthatja majd vissza az aktuális pályán. Ha a parti nélküle eléri a kijáratot, a társ végleg kikerül a partiból és a karakter-nyilvántartásból.

Amikor egy NPC a partyvezér saját harcába segít be, a gyógyító és állapottisztító varázslatait továbbra is a megszokott vészhelyzeti szabályok szerint használja. Támadó varázslatot azonban csak akkor süt el, ha a vezér HP-ja legfeljebb a maximum fele, az ellenfél boss vagy 5-ös erősségi kategóriájú, illetve ha a szörny Erő + Gyorsaság összege nagyobb a vezér Erő + Ügyesség összegénél. Ez a takarékossági szabály nem vonatkozik az NPC saját külön harcára.

Az `Enemy.CurrentHitPoints` a szörny futásidejű HP-ja. A `BattleSystem` ebből indítja a harcot és ide írja vissza a maradékot ezért egy NPC-t legyőző sérült szörny nem gyógyul vissza a következő találkozás előtt.

### 1. Kezdeményezés

Mindkét fél egyszer dob egy előjeles `1d2` módosítót: a dobás `-1`, `-2`, `+1` vagy `+2`, az előjel és a nagyság külön véletlen választás eredménye.

```text
játékos kezdeményezése  = Ügyesség + bónuszok - állapotbüntetés + előjeles 1d2
ellenfél kezdeményezése = Gyorsaság + képességbónusz + előjeles 1d2
```

A játékos kezd, ha az eredménye nagyobb vagy egyenlő; döntetlennél tehát a játékosé az első támadás. Ezután a felek felváltva támadnak.

### 2. Találati próba

Minden támadásnál új `1d20` dobás készül.

```text
támadóérték = 1d20 + támadó sebességi képessége + bónuszok - állapotbüntetés
célérték     = 11 + védekező sebességi képessége
találat      = támadóérték >= célérték
```

A játékos sebességi képessége az Ügyesség, az ellenfélé a Gyorsaság. A Harcos, Barbár és Lovag fegyveres támadásához az Erő további találati bónuszt ad: 7–9 Erőnél +1, 10–12-nél +2, 13-nál +3. A szabály nem keménykódolt kasztlista: az `adatok.csv` `#Erő találati bónusz` szekciója osztályonként külön `MinimumErő` és `Bónusz` küszöbsorokat tárol, így a jogosult osztályok és a görbe külön-külön hangolhatók. Mindig a karakter Erőértékét nem meghaladó legmagasabb küszöb érvényesül; a napló csak a tényleges, nem nulla `Erő-találat` bónuszt mutatja.

Sikertelen próba esetén nincs sebzés. A természetes 20 a játékos és az ellenfél számára is automatikus, kritikus találat; a tolvaj Halálos pontosság tehetsége természetes 18–20 között teszi kritikussá a támadást.

### Kritikus találat

Az általános kritikus találat kétszeres nyers sebzést okoz. A Halálos pontosság ehelyett háromszoros kritikus szorzót ad, tehát a két kritikus szabály nem szorzódik össze. Az Orvtámadás ettől külön támadási szorzó, ezért kritikussal együtt is érvényesülhet. A szorzás a páncél és más védelmi levonások előtt történik.

Az ellenfél aktiválódott `ExtraDamage` képessége a nyers sebzés része, ezért kritikus találatnál szintén duplázódik. A találat után felkerülő állapotok, a méregkeverő külön `1d6` sebzése, valamint a mérgezés és vérzés időszakos sebzése nem része a kritikus szorzásnak. A természetes 20-as ellenféltámadást a tolvaj Kitérés tehetsége nem háríthatja el. Minden extra vagy ismételt támadás külön találati dobást végez, ezért külön-külön lehet kritikus.

### 3. Játékos sebzése

A rendszer az első olyan fegyverhelyet használja, amely nem védelmi típusú (`WT003`). Ha nincs támadófegyver, az alapsebzés `1d2`.

- `WT002` fegyvernél a sebzésképesség az Ügyesség;
- minden más támadófegyvernél az Erő;
- a fegyver alapsebzése a CSV-ben megadott zárt tartományból dobódik;
- ezen felül `0..2` véletlen sebzés jár.

```text
képességbónusz = max(0, (képesség - 1) / 2)  (egész osztás)
nyers sebzés   = fegyversebzés + képességbónusz + 0..2 + támadóbónuszok
végső sebzés   = max(1, nyers sebzés × támadási szorzók × kritikus szorzó - ellenfél páncélja - állapotbüntetés)
```

### 4. Ellenfél sebzése

Találat esetén az ellenfél sebzése:

```text
nyers sebzés = ellenfél Erő + dobás(1..max(2, ellenfél Erő)) + aktiválódott extra sebzés
védelem      = páncél tartományából dobott érték
              + az első felszerelt védelmi fegyver/pajzs tartományából dobott érték
végső sebzés = max(1, nyers sebzés × kritikus szorzó - védelem)
```

Ha nincs páncél vagy pajzs, annak védelme nulla. Találat esetén legalább 1 sebzés mindig átjut. A sikeres találat sebzésszámítása után külön dobódnak a szörny állapatterjesztő képességei; új vagy frissített állapot esetén a csatanapló feltűnő `⚠️ ÁLLAPOT` jelzéssel, az állapot saját emojijával és az időtartam újraindításának tényével jelzi azt. A karakter saját támadási szakasza után a mérgezés és vérzés egyetlen összesített naplóüzenetben sebez, figyelmen kívül hagyva a páncélt; az időtartam ekkor csökken.

### 5. Befejezés

A támadások addig váltakoznak, amíg valamelyik fél HP-ja nullára nem csökken.

- **Játékosgyőzelem:** megkapja az ellenfél XP-jét; az ellenfél kikerül a pálya aktív listájából, és holttest kerül a helyére; a szükséglet-időzítő újabb egy percről indul.
- **Játékosvereség:** a karakter HP-ja 0 marad, halottnak számít, és a játék véget ér. A főmenüből halott karakterrel nem indítható új játék.

Az ellenfél definíciója változatlan adat. A `Resolve` a csata alatt lokális `EnemyDefinition` másolaton számol, majd a maradék HP-t visszaírja az `Enemy.CurrentHitPoints` értékébe; így a túlélő, korábban megsérült szörny állapota menthető. A játékos HP-ja közvetlenül a `LiveCharacter` objektumon változik.

## Megjelenítés

A `ConsoleRenderer` a pályát, a karakterlapot, az ASCII-képpanelt és az üzenetnaplót egy rögzített konzolelrendezésben jeleníti meg. A jobb alsó képpanel öt képsorból és az azt körülvevő két keretsorból áll; a rövidebb portrékat a renderer üres sorokkal egészíti ki. Mozgáskor és csatakor csak az érintett cellákat vagy panelsorokat írja újra. Emiatt a játékmeneti osztályok a teljes újrarajzolás helyett célzott renderer-metódusokat hívnak.

Az `AsciiPortraits` mind a hat karakterosztályhoz (`C001`–`C006`) és az első tíz ellenfélhez (`E001`–`E010`) külön, ötsoros portrét tartalmaz. Normál nézetben mindig a karakterlapon éppen megjelenített partitag osztályképe látható a karakter saját színével; a bal/jobb karakterváltás a képpanelt is azonnal újrarajzolja. Vezéri csata kezdetén a panel az aktuális ellenfél azonosító szerinti portréjára vált, színe az ellenfél 1–5-ös erősségi szintjét követi. A csata lezárásakor visszaáll a megjelenített karakter portréja. Ismeretlen vagy még portré nélküli azonosítóhoz külön `???` tartalékkép tartozik.

A `Shift+F1` a fő játékhurokban, karakterlapfókuszban, varázsválasztás/célzás alatt és a vezéri csata billentyűvárakozásakor is megnyitja ugyanazt a súgóképernyőt, mint a főmenü. A sima `F1` az első varázslat-gyorshely. Bezáráskor a játék az aktuális térképet és karakterlapot rajzolja vissza; a futásidejű játékállapot nem változik.

A karakterlap a faj és osztály alatt egy-egy sort tart fenn a tehetségeknek és az aktív állapotoknak. Az `Áll:` sor a negatív állapotok CSV-s ikonjai mellett a fenti buffemojikat is megjeleníti, hogy a hatás a súgó jelmagyarázata alapján azonosítható legyen. Ha a nevek együtt nem férnek el a 27 karakteres panelen, minden elem azonos rendelkezésre álló hosszra rövidül, így az összes aktív bejegyzés látható marad.

A pálya mérete a renderer játékterének méretéből származik, ezért a generálás és a konzolelrendezés jelenleg közvetetten össze van kötve.

## Mentés

A `CharacterSaveService` a karaktereket a futtatási könyvtár `karakterek.json` fájljába menti. Megmarad többek között:

- faj és osztály;
- képességek, HP/manna és generált bónuszok;
- élelem, víz, arany, szint és XP;
- a szintlépésekből összegyűlt maximális HP- és mannanövekmény;
- a kiválasztott tehetségek azonosítói;
- az aktív nem szükségletalapú állapotok azonosítói;
- fegyverek, páncél, varázstárgyak és hátizsák, az üres helyeket is megőrző pozíciókkal;
- az ismert és memorizált varázslatok, valamint az `F1–F8` gyorshelyek;
- az aktív karakter indexe.

A karaktermentés definícióazonosítókat használ, és a betöltéskor az aktuális `GameDataCatalog` elemeihez kapcsolja vissza őket. Régebbi, névalapú mentésekhez kompatibilitási útvonal is tartozik.

A játék közbeni `F9` előbb visszateszi az esetleg kézben tartott inventorytárgyat, majd a futtatási könyvtár `mentések` almappájába ír. Vezéri csata alatt a mentési kérés a győztes csata, XP-elosztás és esetleges tehetségválasztás lezárásakor teljesül, így nem keletkezhet félbehagyott harci körből következetlen állás; vereségnél a függő kérés elmarad. A fájlnév alakja `Főkarakter_yyyyMMdd_HHmmss_fff.save`, ezért minden mentés külön választható marad. A teljes játékmentés tartalmazza:

- a teljes karakterlistát, partit, inventorykat, állapotokat és erőforrásokat;
- a pályaszintet, vezetőpozíciót, nézési irányt és követési útvonalat;
- a teljes térképrácsot, szobákat, kijáratot és ajtóállapotokat;
- az ellenfelek pozícióját, aktuális HP-ját, mozgási időzítését, profilját, üldözési döntését, csoportazonosítóját és vezér/tag szerepét, továbbá a ládákat, a szörnytetemek definícióját és átkutatottságát, valamint a földi tárgyhalmokat;
- a partitársak térképi pozícióit és az elesett társak karakterkapcsolatát;
- a felfedezett ködmezőket, partiparancsot, valamint a szétszóródás, ellenfélmozgás és szükségletfogyás hátralévő idejét.

A főmenü mentésválasztója időrendben listázza a `.save` fájlokat a főkarakter nevével, a pályaszámmal és a mentés idejével. Betöltéskor a statikus definíciók továbbra is az aktuális `GameDataCatalog` elemeiből oldódnak fel. A mentési séma verziózott; ismeretlen verzió vagy sérült állomány hibaüzenettel visszautasításra kerül.

## Függőségek és állapotkezelés

A projekt csak a .NET alaprendszerét használja, külső NuGet-csomag nincs. A függőségek konstruktoron keresztül jutnak el a fő objektumokhoz, de nincs külön függőséginjektáló keretrendszer.

Fontos állapotélettartamok:

- `GameDataCatalog`: egy alkalmazásfutásra változatlan;
- `CharacterRoster`, `Party` és `LiveCharacter`: menük és játékok között tovább él, JSON-ba menthető;
- `Game`: egy játékindítás idejére él;
- `Maze`, `Player`, `FogOfWar`: egy labirintusszint idejére él;
- `BattleSystem`: egy `Game` példányhoz tartozik;
- ellenfél aktuális HP: az adott `Enemy` teljes labirintusszintbeli életére megmarad.

## Bővítési irányelvek

- Új statikus tartalmat lehetőleg az `adatok.csv` és egy megfelelő `Definition` típus bővítésével adjunk hozzá.
- Új CSV-szekcióhoz a `DataSection`, a `ParseSection`, az `AddDefinition` és a `GameDataCatalog` összehangolt módosítása szükséges.
- Új labirintusszint-hangolás elsődleges helye a `MazeLevelConfigurations`.
- Új harci szabály a `BattleSystem` felelőssége; a renderer csak a kapott harci eseményt jelenítse meg.
- Új mentendő karakterállapothoz a `LiveCharacter`, a mentési DTO és mindkét átalakítás együttes frissítése szükséges.
- A világobjektumok örököljék a `WorldObject` típust, és a `Maze` tartsa fenn a foglaltsági szabályokat.
- A meglévő mentések és CSV-azonosítók kompatibilitását fejlesztéskor meg kell őrizni.

## Jelenlegi technikai korlátok

- Nincs automatikus tesztprojekt; a fő ellenőrzés jelenleg a fordítás és a kézi konzolos próba.
- A CSV-feldolgozás nem teljes RFC-kompatibilis CSV-parser.
- A konzolméretek és koordináták nagyrészt rögzítettek.
- Mind a húsz mágusvarázslat működik; a húsz papi varázslat egyedi gyógyító, tisztító, feltámasztó és szent harci hatásai még nincsenek implementálva.
- Az élelem és víz csökken és fogyóeszközökkel visszatölthető; az alacsony és nulla szükségletszintek állapot- és csatakezdő büntetéseket okoznak, de a labirintusban csatán kívül nem sebeznek közvetlenül.
- A teljes pályaállapot menthető és visszatölthető, de a mentési séma jelenleg egyetlen, `1`-es verziót támogat.
