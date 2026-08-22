# MazeGame architektúra

## Áttekintés

A MazeGame egy .NET 10 konzolos, egyjátékos labirintusjáték. Az alkalmazás adatvezérelt: a fajok, osztályok, ellenfelek, felszerelések, varázslatok és fejlődési küszöbök az `adatok.csv` fájlból töltődnek be. A karakterlista JSON-fájlban, a teljes futamok pedig időbélyeges `.save` állományokban maradnak meg.

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
- `AsciiPortraits.cs`: a jobb alsó képpanel legfeljebb öt sor magas beépített ASCII-ábrái.

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

A `Definition` végű típusok az `adatok.csv` tartalmát képviselik. A `LiveCharacter` ezzel szemben változó futásidejű állapot: HP, manna, szükségletek, arany, XP, szint, felszerelés és hátizsák.

### `UI`: menük

- `MainMenu`: új játék, mentésbetöltő, karakterlista, kiválasztás, törlés, gyorsindítás és súgó.
- `CharacterCreationScreen`: név- és fajválasztás, tulajdonságdobás, osztályjogosultság és karakter létrehozása.

### `Combat`: harci szabályrendszer

- `BattleSystem`: a megjelenítéstől független harci algoritmus.
- `BattleResult`, `BattleLogEntry`: a csata végeredménye és megjeleníthető eseményei.

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

A karaktergenerálás négy elsődleges képességre egy véletlen méretű pontkészletet oszt el. Az alap 25 pont 15% eséllyel nem kap bónuszt, 50% eséllyel +1, 25% eséllyel +2, 10% eséllyel pedig +3 ponttal nő, így a tényleges készlet 25–28 pont. Mindegyik érték legalább 1 és legfeljebb 10 a dobás során. Ehhez adódnak hozzá a faj módosítói, majd a végeredmény 1 és 13 közé szorul. Ugyanez a pontkészletdobás érvényes a kézi, gyorsindításos és véletlen NPC-karaktergenerálásra.

A karakternév 1–13 karakter hosszú lehet. A korábbi mentésekből érkező hosszabb nevek betöltéskor 13 karakterre rövidülnek, hogy a karakterlap rögzített fejlécébe illeszkedjenek.

Minden `LiveCharacter` tartós `ConsoleColor` tulajdonsággal rendelkezik. Kézi karaktergeneráláskor a játékos egy jól látható színpalettáról választ; gyorsindításkor és fejlesztői társgeneráláskor a szín véletlen. Régi mentéseknél az alapértelmezés cián.

Az `adatok.csv` `#Karakternevek` szekciója osztályonként 20 `CharacterNameDefinition` rekordot tartalmaz. A gyorsindítás az elkészült karakter tényleges osztályának névkészletéből választ, és előnyben részesíti a karakterlistában még nem használt neveket. Ha egy osztály mind a 20 neve foglalt, az ismétlődés megengedett. A definíciók nem játékos karakterek későbbi elnevezésére is újrahasználhatók.

Csak olyan osztály választható, amelynek minden CSV-ben megadott képességminimumát teljesíti a karakter. A maximális HP és manna képlete:

```text
max HP    = egészséghez tartozó CSV-minimum + 1..15 életerőbónusz
max manna = intelligenciához tartozó CSV-minimum + 1..15 mannabónusz
```

Mannát csak a `CharacterClassRules` által varázshasználónak minősített osztályok kapnak. A kezdőfelszerelés az osztályazonosítóhoz tartozó CSV-sorból épül fel.

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
| Tolvaj | Mestertolvaj | részleges | dupla ládaarany kész; ritka tárgydobás tárgyrendszerre vár |
| Pap | Gyógyító kegyelem | várakozik | gyógyító varázsrendszer szükséges |
| Pap | Áldott fegyver | kész | élőholt (`MA001`) ellen +2 találat és +2 sebzés |
| Pap | Szentély | kész | ellenséges támadásonként 20% eséllyel kimarad a támadás |
| Pap | Hitforrás | kész | +12 manna választáskor és csata elején legfeljebb 5 manna visszatöltés |
| Pap | Feltámadás | várakozik | játéknap- és feltámadási rendszer szükséges |
| Pap | Isteni ítélet | várakozik | papi varázsrendszer szükséges |
| Mágus | Arkán fókusz | várakozik | mágikus találati próba szükséges |
| Mágus | Mannatartalék | kész | +15 maximális és aktuális manna választáskor |
| Mágus | Elemi mester | várakozik | sebző varázsrendszer szükséges |
| Mágus | Mágikus pajzs | kész | a beérkező sebzés felfelé kerekített negyedét manna nyeli el |
| Mágus | Láncvarázslat | várakozik | varázslás szükséges |
| Mágus | Főmágus | részleges | +25 manna kész; varázslatköltség-csökkentés varázsrendszerre vár |

A csatában aktiválódó tehetségek bekerülnek a harci napló számításaiba és magyarázó szövegeibe. Az egyszer használható túlélési és első támadásos hatások minden csata elején új harci kontextust kapnak.

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

1. **Billentyűzet:** nyilakkal játékosmozgás, `Esc`-pel visszatérés, fejlesztői gyorsbillentyűk.
2. **Ellenfélmozgás:** csatán kívül 700 ms-onként minden ellenfél véletlen irányba próbál lépni.
3. **Szükségletcsökkenés:** csatán kívül percenként csökken az élelem és a víz.

Az ellenfelek nem léphetnek falra, bejáratra, kijáratra vagy foglalt mezőre. Ha a játékos és egy ellenfél azonos cellára kerül, azonnal csata indul. Csata alatt a világ ideje és az ellenfelek mozgása megáll.

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

A vezető és a társak térképi jele az osztály magyar nevének nagy kezdőbetűje: `H`, `B`, `L`, `T`, `P` vagy `M`. A jel a karakter saját színével rajzolódik. A társak minden új pályán szélességi kereséssel a vezetőhöz legközelebbi üres, járható cellákra kerülnek. Foglalják a mezőjüket az ellenfelek és a vezető elől; egymásra vagy szörnyre nem lépnek és zárt ajtón nem haladnak át.

Az NPC-ként vezérelt `LiveCharacter` nullable `NpcBehavior` tulajdonsága mentésre kerül. A vezetőnél inaktív. Generáláskor a barbár mindig `Aggressive`, a lovag mindig `Defensive`, a tolvaj mindig `Scout`, a pap és a mágus mindig `Cautious`; a harcos fele-fele eséllyel `Defensive` vagy `Aggressive`. Régi mentésből származó NPC alapértéke az első elhelyezéskor `Defensive`.

A partitársak mozgása a `Game` meglévő egyszálú eseményciklusában fut. Minden avatár saját következő mozgási időpontot kap: normál helyzetben 180–240 ms közötti kis véletlen eltéréssel lépnek; legalább öt mezős lemaradásnál 130–170 ms-ra és legalább nyolc mezőnél 90–120 ms-ra gyorsulnak. Az induló késleltetésük 80–240 ms között eltérő. Így nem egyszerre próbálják elfoglalni ugyanazokat a mezőket de nagy lemaradásból is gyorsan felzárkóznak.

A játék legfeljebb a vezér utolsó 256 sikeres pozícióját tartja nyilván. A vezetőt követő NPC-k nem annak pillanatnyi X/Y-koordinátája köré választanak célmezőt: a parti sorrendje szerint nyompontot céloznak és szélességi útkereséssel lépnek felé. A sorrend legfeljebb egy további lépésnyi formációs késést okoz ezért a hátsó társ sem marad látványosan messzebb. A speciális előremenő vagy ellenségre reagáló viselkedés után ugyanehhez a nyomvonalhoz térnek vissza.

Térképfókuszban két ideiglenes partiparancs írhatja felül a profilok mozgását:

- a `H` tartósan ki- és bekapcsolja a helyben maradást; aktív állapotban a társak nem kezdeményeznek mozgást vagy támadást de a rájuk lépni próbáló szörnnyel továbbra is automatikusan megküzdenek;
- az `M` 10 másodpercre szétszóródást rendel el: minden élő társ járható útvonalon legfeljebb tíz Manhattan-távolságra húzódik a vezértől és közben továbbra is felderít. Ez az időszak a `H` állapotát is ideiglenesen felülírja. Lejáratkor minden társ visszakapja a korábbi profilját; ha a `H` előtte aktív volt akkor ismét helyben marad.

- a defenzív társ legalább két vezérlépéssel korábbi nyompontot követ és így egy üres mezőt hagy közöttük; ötmezős rálátáson belüli szörny felé indul és mellé érve automatikusan megtámadja;
- az agresszív társ az előre eső tágas mezőket keresi és nem lép a vezető előtti szűk folyosóba; ötmezős rálátáson belüli szörny felé indul és mellé érve automatikusan megtámadja;
- a felderítő legfeljebb tíz mezőre halad a vezető előtt; ötmezős rálátáson belüli szörny észlelésekor visszatér a vezér nyomvonalára;
- az óvatos társ legalább két vezérlépéssel korábbi nyompontot követ; ellenség észlelésekor sem indul felé.

Minden társ ugyanazzal az ötmezős és falak/ajtók által takart látótérszámítással hívja a `FogOfWar.RevealFrom` műveletet. A társ induló környezete azonnal láthatóvá válik és minden sikeres NPC-lépés csak az újonnan felfedett valamint az elhagyott/elfoglalt térképcellákat rajzolja újra.

A karakterlap a jobb panel legfelső sorában kezdődik, ezért nem hagy kihasználatlan üres sort a fejléc felett. A faj és osztály alatt a megszerzett tehetségek két sorban jelennek meg; a neveket a renderer a két sor között osztja el és szükség esetén soronként egyenletesen rövidíti. A tíz hátizsáksor alatt három sort tart fenn a társaknak. Minden sor a karakter saját színével mutatja az osztály kezdőbetűjét, a nevet, a szintet és az aktuális/maximális HP-t. A vezető nem ismétlődik meg ezekben a sorokban.

A `Tab` vált a térkép- és karakterlapfókusz között. Karakterlapfókuszban a `KARAKTERLAP` cím zöld hátteret kap, a fel/le nyilak pedig minden felszereléshely, varázstárgyhely, hátizsákhely és partitárs között léptetik az aktív kijelölést; az üres helyek is célpontok. A kiválasztott sor DarkCyan hátteret kap, miközben megtartja saját előtérszínét; a renderer a logikai kategória és index alapján karakterenként külön megőrzi az utolsó kijelölést. Lépéskor csak a választható tárgy- és partisorok rajzolódnak újra, a teljes karakterlap és térkép nem.

Karakterlapfókuszban a bal/jobb nyíl körkörösen vált a parti tagjainak teljes karakterlapja és inventoryja között. A három rögzített társsor mindig ugyanazokat a társakat mutatja; az éppen megtekintett társ sora `▶` jelölést kap. A vezető lapjának megtekintésekor nincs nyíl a társsorokban. A megtekintett lap nem változtatja meg a térképen irányított vezetőt.

Az inventory rögzített helyekből áll: két fegyverhely, egy páncélhely, három varázstárgyhely és tíz hátizsákhely. Minden `IItemDefinition` kategóriája fegyver, páncél, varázstárgy vagy általános tárgy. A felszereléshelyek csak a saját kategóriájukat fogadják el, a hátizsák bármelyiket. A `Space` kiemeli a kijelölt tárgyat, majd ugyanazon vagy másik partitag érvényes helyére teszi; foglalt cél esetén a két tárgy helyet cserél, ha a teljes csere után mindkét felszerelés érvényes. A fókusz elhagyása visszateszi a még kézben tartott tárgyat, így az nem veszhet el.

A fegyverek és páncélok ritkasága `Normal`, `Magic` vagy `Legendary`, a felületen Sima, Varázs és Legendás néven jelenik meg. A CSV-ben minden kézzel felvett felszerelés külön `Kategória`, opcionális `AlapId` és `MágikusErő` mezőt kap. A mágikus erő már adatként, menthető tárgydefiníció részeként rendelkezésre áll; a jelenlegi csatában a mágikus `+N` felszerelések megnövelt sebzés-/védelmi tartománya aktív, az általános mágikus-erő mechanika a későbbi varázsrendszer bővítési pontja.

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

Mind a 12 mágus- és mind a 12 papi varázslathoz tartozik egy tekercs. A tekercs pontosan egy töltetű és kizárólag a Mágus (`C006`) varázstárgyhelyére szerelhető; hátizsákban más kaszt is szállíthatja. A kilenc pálca 3–8 töltetet és kizárólag mágusiskolájú varázslatot hivatkozhat. A CSV-betöltő ellenőrzi a varázslat-ID létezését, megtiltja a papi varázslatot tartalmazó pálcát, valamint hibát jelez a nem egytöltetű vagy nem kizárólag mágusnak engedélyezett tekercsnél.

A `MaximumCharges` és `SpellId` jelenleg a teljes varázslási rendszer számára előkészített definíciós adat. A pálcák maradék töltetének példányszintű fogyasztása és a tekercsek tényleges elsütése még nem aktív, mert a hivatkozott 24 varázslat célzás-, költség- és hatásalgoritmusa még nem készült el. Emiatt használatkor egyelőre nem fogy el töltet vagy tekercs; a gyűrűk és amulettek fenti passzív hatásai viszont már teljesen működnek.

A `#Fegyverek` és `#Páncélok` CSV-szekció kasztoszlopai határozzák meg, mely osztályok viselhetik az adott tárgyat. A fegyvereknél a Harcos, Barbár és Lovag, a páncéloknál a Harcos és Lovag alapértelmezetten engedélyezett; a többi kaszt engedélyét az `igen` érték adja. A korlátozás csak a felszereléshelyekre vonatkozik, hátizsákban bármely karakter hordozhat bármilyen tárgyat. Az ellenőrzés központilag a `LiveCharacter` végleges, tervezett inventoryállapotán fut, ezért a kézi mozgatásra és cserére, a kezdőfelszerelésre, a mentés betöltésére és a véletlen NPC-felszerelésre is érvényes.

A kétkezes fegyver kizárólag az első fegyverhelyen viselhető. Amíg ott kétkezes fegyver van, a második fegyverhelynek üresnek kell lennie és a karakterlapon `⛔` lezárásként jelenik meg. Kétkezes fegyver csak üres második hely mellett szerelhető fel; a második hely pedig nem tölthető fel, amíg az elsőben kétkezes fegyver marad. A hátizsákban ez a korlátozás sem érvényes.

Az `I` a kijelölt tárgy összes jelenleg ismert adatát az alsó üzenetnaplóba írja: név és stabil ID minden tárgynál; fegyvertípus, sebzés, egy-/kétkezes jelleg és engedélyezett kasztok a fegyvereknél; védelem és engedélyezett kasztok a páncéloknál; valamint a CSV-ből betöltött jellemzés. A fegyverekhez és páncélokhoz tartozó szöveg a `Jellemzés`, az általános tárgyaknál szintén a `Jellemzés` nevű oszlopból érkezik.

Az `I` ezen kívül kijelzi a Sima/Varázs/Legendás ritkaságot, a mágikus erőt és az alapárat. Használati tárgynál a hatást és annak számszerű értékét is megmutatja.

Az `Enter` a megtekintett karakter kijelölt hátizsáktárgyát használja el. Az ételek 15–100 élelem-, az italok 30–40 vízpontot töltenek; a három gyógyital 20/50/120 HP-t, a három varázsital 15/40/90 mannát állít helyre. Az ellenméreg a mérgezést, a gyógyfüves orvosság a betegséget, a kötés a vérzést szünteti meg. A tárgy csak sikeres, tényleges hatás esetén fogy el: teljes HP-n nem vész el gyógyital, nem varázshasználónál varázsital, illetve hiányzó állapotnál gyógyító kellék.

Ha a kijelölés egy partitárs sorára esik akkor az `I` a társ nevét és magyar mozgásprofilját írja az üzenetnaplóba.

A `D` a kijelölt tárgyat a parti vezetőjének aktuális térképmezőjére dobja. A `GroundItemPile` egy pozíción tetszőleges számú tárgyat tárol, a térképen cián `◆` jel mutatja; a halom nem akadályozza a mozgást. A földi halmok a labirintusszint futásidejű állapotához tartoznak, ezért új pályán megszűnnek és jelenleg nem kerülnek karaktermentésbe.

A rejtett `Ctrl+Shift+Y` fejlesztői gyorsbillentyű négy főre tölti a partit. A `RandomCharacterGenerator` minden társhoz:

- érvényes véletlen faj–képesség–osztály kombinációt készít;
- az osztály CSV-s névkészletéből lehetőleg még nem használt nevet választ;
- 2–30. szint közé fejleszti a normál HP-/mannadobásokkal;
- szintjének megfelelő eséllyel választ tehetségeket;
- kizárólag a kasztja által használható véletlen fegyvereket és páncélt, továbbá varázstárgyakat és korlátozás nélküli hátizsáktartalmat ad; kétkezes első fegyvernél a második hely üres marad;
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

Jutalmazás után a parti a fogadóban pihen: kizárólag a túlélők aktuális HP-ja és mannája töltődik maximumra. A 0 HP-s társ halott marad; a pálya végén kikerül a partiból és a karakter-nyilvántartásból, tehát végleg elveszik. A középre igazított színes pályavége képernyő megmutatja a képletet és összeget, karakterenként az XP-t, szintváltozást és feltöltött erőforrásokat, továbbá külön megemlékezik az elvesztett társakról. Enter vagy Space nyitja meg a fogadó kereskedőjét; a piacról `Esc` a toborzáshoz, onnan `Esc` a következő pályára visz.

### Fogadói kereskedés

Minden `IItemDefinition` pozitív `BasePrice` alapárral rendelkezik, amely közvetlenül az `adatok.csv` megfelelő sorából származik. Hiányzó, nulla vagy negatív ár betöltési hibát okoz. Az árskála az egyszerű ellátmány néhány aranyas tartományától az alapfegyvereken és vérteken át a több tízezer aranyas legendás felszerelésekig terjed; a legerősebb legendás gyűrűk és amulettek szintén ritka és drága fogadói ajánlatok.

A fogadó minden látogatáskor új, véletlen piacot készít. A kereskedő normál és mágikus tárgyainak eladási ára 80% eséllyel az alapár 105–150%-a, 20% eséllyel kedvezményes 85–100%. A parti tárgyaiért jóval kevesebbet, az alapár véletlen 40–70%-át kínálja. Az ajánlatok az adott fogadólátogatás teljes ideje alatt stabilak, ezért a nézetváltással nem dobhatók újra; a visszavásárlási ár mindig alacsonyabb a lehetséges eladási árnál.

A piac `←`/`→` vagy `Tab` billentyűvel vált a vásárlás és eladás között, `↑`/`↓` választ, az `Enter` végrehajtja az üzletet. Eladáskor a teljes parti hátizsákjainak tárgyai láthatók a tulajdonos nevével; a felszerelt tárgyak előbb az inventoryban tehetők hátizsákba. A bevétel és kiadás a partyvezér aranyát módosítja. Vásárláskor a tárgy először a vezér első üres hátizsákhelyére kerül, telt hátizsáknál pedig parti-sorrendben a következő szabad hellyel rendelkező társ kapja. Ha az összes hátizsák tele van, a vásárlás meghiúsul és arany nem fogy.

A készlet a nem legendás tárgyak alapár szerint rendezett, fokozatosan feloldódó részéből készül. A teljesített pálya növekedésével nyolc újabb, jellemzően értékesebb tárgytípus kerülhet a jelöltek közé, a tényleges kínálat pedig pályánként egy hellyel nő, legfeljebb tizenkettőig. A súlyozott választás a feloldott készleten belül az értékesebb tárgyakat részesíti előnyben, így később több és jobb portéka jelenik meg anélkül, hogy az olcsó ellátmány teljesen eltűnne.

Legendás tárgy külön ritka dobással kerülhet a fogadóba: az esély az első pálya után 1.5%, pályánként további 0.5 százalékponttal nő, és legfeljebb 8%. Egy látogatáskor legfeljebb egy Legendás ajánlat jelenik meg, az alapár 125–180%-áért. A választható Legendás készlet pályánként bővül, így korán csak az olcsóbb legendák kerülhetnek elő.

### Fogadói toborzás

A kereskedés után minden fogadólátogatáskor 1–3 zsoldos jelenik meg. A rendszer először ugyanennyi különböző osztályt választ, majd osztályonként addig dob fajt és képességeket, amíg a karakter teljesíti az adott osztály minimumait. A jelöltek neve a karakter-nyilvántartásban és az adott ajánlatban is egyedi, amíg az osztály névkészlete ezt lehetővé teszi.

A vezérnél alacsonyabb szintű zsoldos ingyen csatlakozik. Azonos vagy magasabb szinten az alap felbérlési díj `zsoldos szintje × 100` arany, amelyre fogadólátogatásonként egyszer kisorsolt 50–150%-os szorzó kerül. Az ajánlati ár a toborzóképernyő használata közben nem változik. Ha nincs elég arany, a felvétel meghiúsul, és teljes parti esetén a régi társ kiválasztása és elvesztése sem történik meg.

A zsoldos célpontszintje a partyvezér aktuális szintje körüli zárt ±3 tartományból készül, a játékadatokban elérhető szintekre szorítva. A karakter a szintlépés normál HP-/mannadobásait és a szintjéhez illő véletlen tehetségeket kapja. Alacsony szinten az osztály CSV-s kezdőfelszerelését viseli; a szint emelkedésével növekvő eséllyel annak nem legendás, mágikus továbbfejlesztéseit kaphatja meg. Hátizsákjában pontosan 1–3 véletlen használati tárgy van, például étel, ital, gyógyital, varázsital, ellenméreg, orvosság vagy kötés.

Szabad partihely esetén az `Enter` azonnal felveszi a kijelölt zsoldost. Négyfős partinál előbb ki kell választani a lecserélendő, nem vezető társat. A lecserélt karakter kikerül a partiból és a központi karakter-nyilvántartásból, ezért végleg elveszik; a csere képernyője `Esc`-pel következmény nélkül megszakítható.

A rejtett `Ctrl+Shift+E` fejlesztői gyorsbillentyű a partyvezért a kijárat melletti, járható és objektumtól mentes mezők közül a hozzá legközelebbire teleportálja. A teleport frissíti a vezér útvonalát és a látómezőt is; ha nincs megfelelő szabad mező, csak naplóüzenet jelenik meg.

Az 1–3. labirintusszint külön konfigurációval rendelkezik. A későbbi szintek a harmadik szintből számított, fokozatosan növekvő szobaszámot, jutalmat és ellenfélszámot kapnak. Az 50 szörny teljes katalógusa rendelkezésre áll, de az aktuális pályakonfigurációk továbbra is konkrét ellenfél-ID-ket sorolnak fel; az erősségi szint alapján összeállított automatikus pályakészlet későbbi bővítési pont.

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

### Ajtók

Az ajtó nem egyszerű térképrúna, hanem `MazeDoor` állapotobjektum. Négy állapota van:

| Állapot | Jel | Járható | Újra zárható |
|---|---:|---:|---:|
| Kulcsra zárt | `╫` | nem | igen |
| Nyitott | `╱` | igen | igen |
| Zárt | `╬` | nem | igen |
| Bezúzott | `▒` | igen | nem |

A kezdőterem ajtaja mindig nyitott. A további szobaajtók generáláskor 80% eséllyel kulcsra zártak, 10% eséllyel zártak és 10% eséllyel nyitottak. A zárt és kulcsra zárt ajtó a mozgást és a látóvonalat is blokkolja.

Ajtó mellett a vezető az `N` billentyűvel nyit, a `Z` billentyűvel bezár, a `K` billentyűvel kulcsra zár. A simán zárt ajtó szabadon nyitható. Kulcsra zárt ajtónál a nyitási sorrend:

1. a `T003` kulcs garantáltan nyit és eltűnik a hátizsákból;
2. kulcs nélkül a tolvaj százalékos Ügyesség-próbát tesz;
3. sikertelen zárnyitás vagy más osztály esetén `1d20 ≤ Erő` próba következik, amely siker esetén végleg bezúzza az ajtót.

A tolvaj zárnyitási esélye 10 Ügyességnél 90%, 11-nél 93%, 12-nél 96%, 13-nál 100%; alacsonyabb értéknél fokozatosan csökken. Kulcsra záráshoz egy elfogyó kulcs vagy tolvaj osztály szükséges. Minden művelet és dobás eredménye az alsó üzenetnaplóban jelenik meg. Jelenleg mindig a parti vezetője kezeli az ajtót.

## Látómező és köd

A `FogOfWar` pályánként külön logikai tömbben tárolja a már felfedezett cellákat. A játékos körül 5 cellás Chebyshev-távolságon belül Bresenham-jellegű látóvonal-ellenőrzés történik. A fal és a zárt vagy kulcsra zárt ajtó látható lehet, de blokkolja a mögötte lévő cellákat; a nyitott és bezúzott ajtó nem blokkol.

A rendszer a két már felfedezett végpont közötti, legfeljebb háromcellás rövid ködcsíkot automatikusan kitölti, kivéve ha ajtó van benne. A `Ctrl+Shift+U` csak a megjelenítés számára fedi fel vagy rejti vissza a teljes térképet; a tényleges felfedezettségi adatokat nem írja át.

## Csata algoritmusa

A csata automatikus váltott támadásokból áll. A vezér csatájában minden naplózott esemény után a játékosnak szóközzel kell továbblépnie; az NPC-csata megszakítás nélkül lefut és csak egy végeredmény-összefoglalót ír a naplóba. Nincs menekülés vagy harci akcióválasztás. Mindkét út ugyanazt a `BattleSystem` algoritmust és a játék közös `Random` példányát használja.

A részletes vezéri csatanapló csak a ténylegesen érvényesülő nem nulla tehetségbónuszokat írja ki. A nulla gyógyítás/mannatöltés és a nulla támadó- vagy védelmi tehetségérték nem foglal helyet a naplóban.

A vezér csatájában minden megjelenített harci esemény után részlegesen frissül a karakterlap állapot-, HP- és mannasora. A többi karakterlapsor és a térkép nem rajzolódik újra, így a kör közben változó állapotok és erőforrások azonnal láthatók maradnak fölösleges teljes képernyős frissítés nélkül.

A defenzív és agresszív NPC a saját mozgási időpontjában aktívan megtámadja a szomszédos szörnyet. Bármely profil automatikusan visszaharcol akkor is ha egy szörny az ő mezőjére próbál lépni. NPC-győzelemkor a szörny holttestté válik és az egyetlen összefoglaló üzenet parttagonként mutatja az XP-részesedést valamint az esetleges szint- és erőforrásnövekedést. NPC-vereségkor a karakter 0 HP-val a partiban marad, a partistátusz `💀` jellel mutatja, térképi avatárja pedig az elesés helyén `PartyMemberCorpse` objektummá alakul. Ez megőrzi a `LiveCharacter` hivatkozást, így a későbbi feltámasztás varázslat ugyanazt a karaktert állíthatja majd vissza az aktuális pályán. Ha a parti nélküle eléri a kijáratot, a társ végleg kikerül a partiból és a karakter-nyilvántartásból.

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

A játékos sebességi képessége az Ügyesség, az ellenfélé a Gyorsaság. Sikertelen próba esetén nincs sebzés. A természetes 20 a játékos és az ellenfél számára is automatikus, kritikus találat; a tolvaj Halálos pontosság tehetsége természetes 18–20 között teszi kritikussá a támadást.

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

Az ellenfél definíciója változatlan adat. A fogyó ellenfél-HP a `Resolve` metódus lokális `EnemyDefinition` másolatában él, és a csata után nem kerül mentésre. A játékos HP-ja közvetlenül a `LiveCharacter` objektumon változik, ezért menthető állapot.

## Megjelenítés

A `ConsoleRenderer` a pályát, a karakterlapot, az ASCII-képpanelt és az üzenetnaplót egy rögzített konzolelrendezésben jeleníti meg. A jobb alsó képpanel öt képsorból és az azt körülvevő két keretsorból áll; a rövidebb portrékat a renderer üres sorokkal egészíti ki. Mozgáskor és csatakor csak az érintett cellákat vagy panelsorokat írja újra. Emiatt a játékmeneti osztályok a teljes újrarajzolás helyett célzott renderer-metódusokat hívnak.

Az `F1` a fő játékhurokban, karakterlapfókuszban és a vezéri csata billentyűvárakozásakor is megnyitja ugyanazt a súgóképernyőt, mint a főmenü. Bezárásakor a játék az aktuális térképet és karakterlapot rajzolja vissza; a futásidejű játékállapot nem változik.

A karakterlap a faj és osztály alatt egy-egy sort tart fenn a tehetségeknek és az aktív állapotoknak. Ha a nevek együtt nem férnek el a 27 karakteres panelen, minden elem azonos rendelkezésre álló hosszra rövidül, így az összes aktív bejegyzés látható marad.

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
- az aktív karakter indexe.

A karaktermentés definícióazonosítókat használ, és a betöltéskor az aktuális `GameDataCatalog` elemeihez kapcsolja vissza őket. Régebbi, névalapú mentésekhez kompatibilitási útvonal is tartozik.

A játék közbeni `Ctrl+S` előbb visszateszi az esetleg kézben tartott inventorytárgyat, majd a futtatási könyvtár `mentések` almappájába ír. Vezéri csata alatt a mentési kérés a győztes csata, XP-elosztás és esetleges tehetségválasztás lezárásakor teljesül, így nem keletkezhet félbehagyott harci körből következetlen állás; vereségnél a függő kérés elmarad. A fájlnév alakja `Főkarakter_yyyyMMdd_HHmmss_fff.save`, ezért minden mentés külön választható marad. A teljes játékmentés tartalmazza:

- a teljes karakterlistát, partit, inventorykat, állapotokat és erőforrásokat;
- a pályaszintet, vezetőpozíciót, nézési irányt és követési útvonalat;
- a teljes térképrácsot, szobákat, kijáratot és ajtóállapotokat;
- az ellenfelek pozícióját és aktuális HP-ját, ládákat, holttesteket és földi tárgyhalmokat;
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
- A harc csak közelharcot valósít meg; a CSV-ben lévő varázslatok még nem részei az algoritmusnak.
- Az élelem és víz csökken és fogyóeszközökkel visszatölthető; az alacsony és nulla szükségletszintek állapot- és csatakezdő büntetéseket okoznak, de a labirintusban csatán kívül nem sebeznek közvetlenül.
- A teljes pályaállapot menthető és visszatölthető, de a mentési séma jelenleg egyetlen, `1`-es verziót támogat.
