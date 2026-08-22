# MazeGame architektúra

## Áttekintés

A MazeGame egy .NET 10 konzolos, egyjátékos labirintusjáték. Az alkalmazás adatvezérelt: a fajok, osztályok, ellenfelek, felszerelések, varázslatok és fejlődési küszöbök az `adatok.csv` fájlból töltődnek be. A futás közbeni karakterállapot JSON-fájlban marad meg.

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

### `Domain`: játékadatok és karakterállapot

- `IGameDefinition`: az azonosítóval és névvel rendelkező definíciók közös szerződése.
- `Domain/Characters`: fajok, osztályok, képességek, kezdőfelszerelés, karakterlista és `LiveCharacter`.
- `Domain/Characters/Party.cs`: az aktív vezetőből és legfeljebb három társából álló csapat.
- `Domain/Combat`: ellenfél-, fegyver-, fegyvertípus- és páncéldefiníciók, valamint zárt számtartományok.
- `Domain/Inventory`: általános tárgyfelület és hétköznapi tárgyak.
- `Domain/Magic`: varázstárgyak és varázslatok.

A `Definition` végű típusok az `adatok.csv` tartalmát képviselik. A `LiveCharacter` ezzel szemben változó futásidejű állapot: HP, manna, szükségletek, arany, XP, szint, felszerelés és hátizsák.

### `UI`: menük

- `MainMenu`: karakterlista, kiválasztás, törlés, gyorsindítás, súgó és játékindítás.
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
- egészségből számított minimum életerő;
- intelligenciából számított minimum manna.

A sorok közötti kapcsolatok szöveges azonosítókon alapulnak, például `C001`, `W004` vagy `E001`. Új adat hozzáadásakor az azonosítóknak egyedieknek, a hivatkozásoknak pedig feloldhatóknak kell lenniük. A CSV egyszerű vessző menti darabolást használ, ezért idézőjeles, vesszőt tartalmazó mezőket jelenleg nem támogat.

Az `adatok.csv` a projektfájl beállítása miatt fordításkor a kimeneti könyvtárba másolódik. A program futáskor ezt a másolatot olvassa, nem feltétlenül a forráskönyvtárban lévő fájlt.

## Karakter létrehozása és fejlődése

A karaktergenerálás négy elsődleges képességre oszt el összesen 25 pontot. Mindegyik érték legalább 1 és legfeljebb 10 a dobás során. Ehhez adódnak hozzá a faj módosítói, majd a végeredmény 1 és 13 közé szorul.

A karakternév 1–13 karakter hosszú lehet. A korábbi mentésekből érkező hosszabb nevek betöltéskor 13 karakterre rövidülnek, hogy a karakterlap rögzített fejlécébe illeszkedjenek.

Minden `LiveCharacter` tartós `ConsoleColor` tulajdonsággal rendelkezik. Kézi karaktergeneráláskor a játékos egy jól látható színpalettáról választ; gyorsindításkor és fejlesztői társgeneráláskor a szín véletlen. Régi mentéseknél az alapértelmezés cián.

Az `adatok.csv` `#Karakternevek` szekciója osztályonként 20 `CharacterNameDefinition` rekordot tartalmaz. A gyorsindítás az elkészült karakter tényleges osztályának névkészletéből választ, és előnyben részesíti a karakterlistában még nem használt neveket. Ha egy osztály mind a 20 neve foglalt, az ismétlődés megengedett. A definíciók nem játékos karakterek későbbi elnevezésére is újrahasználhatók.

Csak olyan osztály választható, amelynek minden CSV-ben megadott képességminimumát teljesíti a karakter. A maximális HP és manna képlete:

```text
max HP    = egészséghez tartozó CSV-minimum + 1..15 életerőbónusz
max manna = intelligenciához tartozó CSV-minimum + 1..15 mannabónusz
```

Mannát csak a `CharacterClassRules` által varázshasználónak minősített osztályok kapnak. A kezdőfelszerelés az osztályazonosítóhoz tartozó CSV-sorból épül fel.

Győztes csata után a karakter az ellenfél teljes XP-jutalmát megkapja. A következő szint tényleges küszöbe:

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
| Pap | Áldott fegyver | várakozik | ellenfél-kategória és élőholt jelölés szükséges |
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

Az állapotok az `adatok.csv` `#Állapotok` szekciójának `StatusDefinition` rekordjai. A `LiveCharacter` az aktív állapotok definícióit tartja nyilván; az általános `AddStatus` és `RemoveStatus` műveletekkel további állapotok is beköthetők. A kezdeti katalógus az Éhes, Szomjas, Mérgezés, Betegség és Vérzés állapotot tartalmazza.

Az Éhes és Szomjas állapot származtatott: 30 vagy alacsonyabb élelem-, illetve vízszintnél automatikusan aktív, magasabb értéknél megszűnik. Mentés betöltésekor és a szükségletek csökkenésekor újraszinkronizálódik. A többi állapot tartósan mentődik, de játékmeneti hatása és kiváltó eseménye még nincs bekötve.

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

A szükségletek jelenleg nem okoznak közvetlen sebzést vagy más hátrányt.

A rejtett `Ctrl+Shift+S` fejlesztői gyorsbillentyű pontosan a következő szinthez hiányzó XP-t adja a karakternek. Ugyanazt a fejlődési és bónuszdobási útvonalat használja, mint egy valódi csatagyőzelem.

## Parti

A `Party` 1–4 egyedi `LiveCharacter` objektumot tartalmaz. Első tagja mindig az aktív karakter és egyben a csapat vezetője. Új vezető kiválasztása új, egyszemélyes partit kezd; a társak normál felvételi folyamata későbbi fejlesztési pont. A parti tagjai a központi karakterlistában is szerepelnek, ezért ugyanazzal a karaktermentési modellel tárolódnak. A mentés a tagok karakterlistabeli indexeit őrzi.

A jelenlegi játékmenetben a vezető mozog, harcol, kap XP-t, vesz fel aranyat és fogyaszt szükségleteket. A társak egyelőre nem vesznek részt a harcban vagy az erőforrás-fogyasztásban.

A vezető és a társak térképi jele az osztály magyar nevének nagy kezdőbetűje: `H`, `B`, `L`, `T`, `P` vagy `M`. A jel a karakter saját színével rajzolódik. A társak minden új pályán szélességi kereséssel a vezetőhöz legközelebbi üres, járható cellákra kerülnek. Nem mozognak, és foglalják a mezőjüket az ellenfelek és a vezető elől.

A karakterlap a tíz hátizsáksor alatt három sort tart fenn a társaknak. Minden sor a karakter saját színével mutatja az osztály kezdőbetűjét, a nevet, a szintet és az aktuális/maximális HP-t. A vezető nem ismétlődik meg ezekben a sorokban.

A rejtett `Ctrl+Shift+Y` fejlesztői gyorsbillentyű négy főre tölti a partit. A `RandomCharacterGenerator` minden társhoz:

- érvényes véletlen faj–képesség–osztály kombinációt készít;
- az osztály CSV-s névkészletéből lehetőleg még nem használt nevet választ;
- 2–30. szint közé fejleszti a normál HP-/mannadobásokkal;
- szintjének megfelelő eséllyel választ tehetségeket;
- véletlen fegyvereket, páncélt, varázstárgyakat és hátizsáktartalmat ad.

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

Az 1–3. labirintusszint külön konfigurációval rendelkezik. A későbbi szintek a harmadik szintből számított, fokozatosan növekvő szobaszámot, jutalmat és ellenfélszámot kapnak. Az ellenféltípusok listája azonban külön konfiguráció nélkül továbbra is a harmadik szint típusaiból származik.

### Ajtók

Az ajtó nem egyszerű térképrúna, hanem `MazeDoor` állapotobjektum. Négy állapota van:

| Állapot | Jel | Járható | Újra zárható |
|---|---:|---:|---:|
| Kulcsra zárt | `╫` | nem | igen |
| Nyitott | `╱` | igen | igen |
| Zárt | `╬` | nem | igen |
| Bezúzott | `▒` | igen | nem |

A kezdőterem ajtaja mindig nyitott. A további szobaajtók generáláskor 20% eséllyel kulcsra zártak, 60% eséllyel zártak és 20% eséllyel nyitottak. A zárt és kulcsra zárt ajtó a mozgást és a látóvonalat is blokkolja.

Ajtó mellett a vezető az `N` billentyűvel nyit, a `Z` billentyűvel bezár, a `K` billentyűvel kulcsra zár. A simán zárt ajtó szabadon nyitható. Kulcsra zárt ajtónál a nyitási sorrend:

1. a `T003` kulcs garantáltan nyit és eltűnik a hátizsákból;
2. kulcs nélkül a tolvaj százalékos Ügyesség-próbát tesz;
3. sikertelen zárnyitás vagy más osztály esetén `1d20 ≤ Erő` próba következik, amely siker esetén végleg bezúzza az ajtót.

A tolvaj zárnyitási esélye 10 Ügyességnél 90%, 11-nél 93%, 12-nél 96%, 13-nál 100%; alacsonyabb értéknél fokozatosan csökken. Kulcsra záráshoz egy elfogyó kulcs vagy tolvaj osztály szükséges. Minden művelet és dobás eredménye az alsó üzenetnaplóban jelenik meg. Jelenleg mindig a parti vezetője kezeli az ajtót.

## Látómező és köd

A `FogOfWar` pályánként külön logikai tömbben tárolja a már felfedezett cellákat. A játékos körül 5 cellás Chebyshev-távolságon belül Bresenham-jellegű látóvonal-ellenőrzés történik. A fal és a zárt vagy kulcsra zárt ajtó látható lehet, de blokkolja a mögötte lévő cellákat; a nyitott és bezúzott ajtó nem blokkol.

A rendszer a két már felfedezett végpont közötti, legfeljebb háromcellás rövid ködcsíkot automatikusan kitölti, kivéve ha ajtó van benne. A `Ctrl+Shift+U` csak a megjelenítés számára fedi fel vagy rejti vissza a teljes térképet; a tényleges felfedezettségi adatokat nem írja át.

## Csata algoritmusa

A csata automatikus váltott támadásokból áll, de minden naplózott esemény után a játékosnak szóközzel kell továbblépnie. Nincs menekülés vagy harci akcióválasztás. A `BattleSystem` ugyanazt a `Random` példányt használja, mint a játék többi véletlen eseménye.

### 1. Kezdeményezés

Mindkét fél egyszer dob egy előjeles `1d2` módosítót: a dobás `-1`, `-2`, `+1` vagy `+2`, az előjel és a nagyság külön véletlen választás eredménye.

```text
játékos kezdeményezése  = Ügyesség + előjeles 1d2
ellenfél kezdeményezése = Gyorsaság + előjeles 1d2
```

A játékos kezd, ha az eredménye nagyobb vagy egyenlő; döntetlennél tehát a játékosé az első támadás. Ezután a felek felváltva támadnak.

### 2. Találati próba

Minden támadásnál új `1d20` dobás készül.

```text
támadóérték = 1d20 + támadó sebességi képessége
célérték     = 11 + védekező sebességi képessége
találat      = támadóérték >= célérték
```

A játékos sebességi képessége az Ügyesség, az ellenfélé a Gyorsaság. Sikertelen próba esetén nincs sebzés.

### 3. Játékos sebzése

A rendszer az első olyan fegyverhelyet használja, amely nem védelmi típusú (`WT003`). Ha nincs támadófegyver, az alapsebzés `1d2`.

- `WT002` fegyvernél a sebzésképesség az Ügyesség;
- minden más támadófegyvernél az Erő;
- a fegyver alapsebzése a CSV-ben megadott zárt tartományból dobódik;
- ezen felül `0..2` véletlen sebzés jár.

```text
képességbónusz = max(0, (képesség - 1) / 2)  (egész osztás)
nyers sebzés   = fegyversebzés + képességbónusz + 0..2
végső sebzés   = max(1, nyers sebzés - ellenfél páncélja)
```

### 4. Ellenfél sebzése

Találat esetén az ellenfél sebzése:

```text
nyers sebzés = ellenfél Erő + dobás(1..max(2, ellenfél Erő))
védelem      = páncél tartományából dobott érték
              + az első felszerelt védelmi fegyver/pajzs tartományából dobott érték
végső sebzés = max(1, nyers sebzés - védelem)
```

Ha nincs páncél vagy pajzs, annak védelme nulla. Találat esetén legalább 1 sebzés mindig átjut.

### 5. Befejezés

A támadások addig váltakoznak, amíg valamelyik fél HP-ja nullára nem csökken.

- **Játékosgyőzelem:** megkapja az ellenfél XP-jét; az ellenfél kikerül a pálya aktív listájából, és holttest kerül a helyére; a szükséglet-időzítő újabb egy percről indul.
- **Játékosvereség:** a karakter HP-ja 0 marad, halottnak számít, és a játék véget ér. A főmenüből halott karakterrel nem indítható új játék.

Az ellenfél definíciója változatlan adat. A fogyó ellenfél-HP a `Resolve` metódus lokális `EnemyDefinition` másolatában él, és a csata után nem kerül mentésre. A játékos HP-ja közvetlenül a `LiveCharacter` objektumon változik, ezért menthető állapot.

## Megjelenítés

A `ConsoleRenderer` a pályát, a karakterlapot, az ASCII-képpanelt és az üzenetnaplót egy rögzített konzolelrendezésben jeleníti meg. A jobb alsó képpanel öt képsorból és az azt körülvevő két keretsorból áll; a rövidebb portrékat a renderer üres sorokkal egészíti ki. Mozgáskor és csatakor csak az érintett cellákat vagy panelsorokat írja újra. Emiatt a játékmeneti osztályok a teljes újrarajzolás helyett célzott renderer-metódusokat hívnak.

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
- fegyverek, páncél, varázstárgyak és hátizsák;
- az aktív karakter indexe.

A mentés definícióazonosítókat használ, és a betöltéskor az aktuális `GameDataCatalog` elemeihez kapcsolja vissza őket. Régebbi, névalapú mentésekhez kompatibilitási útvonal is tartozik. A labirintus, az aktuális pályaszint, az ellenfelek és a köd nem része a mentésnek.

## Függőségek és állapotkezelés

A projekt csak a .NET alaprendszerét használja, külső NuGet-csomag nincs. A függőségek konstruktoron keresztül jutnak el a fő objektumokhoz, de nincs külön függőséginjektáló keretrendszer.

Fontos állapotélettartamok:

- `GameDataCatalog`: egy alkalmazásfutásra változatlan;
- `CharacterRoster`, `Party` és `LiveCharacter`: menük és játékok között tovább él, JSON-ba menthető;
- `Game`: egy játékindítás idejére él;
- `Maze`, `Player`, `FogOfWar`: egy labirintusszint idejére él;
- `BattleSystem`: egy `Game` példányhoz tartozik;
- ellenfél csata-HP: egyetlen csata idejére él.

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
- Az élelem és víz csökken, de a nullára fogyásnak még nincs játékmeneti következménye.
- A pálya állapota nem menthető és nem tölthető vissza.
