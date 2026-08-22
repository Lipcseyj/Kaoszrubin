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
- `AsciiPortraits.cs`: a jobb oldali képpanel beépített ASCII-ábrái.

### `Data`: betöltés és mentés

- `CsvGameDataLoader`: szekciókra bontott CSV-feldolgozó. Elsődlegesen UTF-8-at olvas, hibás UTF-8 esetén Windows-1250-re vált.
- `GameDataCatalog`: központi, csak olvasható definíciógyűjtemény és azonosító alapú keresési felület.
- `CharacterSaveService`: a futó karakterek és az aktív kiválasztás JSON-szerializálása, illetve visszaépítése a katalógus definícióiból.

### `Domain`: játékadatok és karakterállapot

- `IGameDefinition`: az azonosítóval és névvel rendelkező definíciók közös szerződése.
- `Domain/Characters`: fajok, osztályok, képességek, kezdőfelszerelés, karakterlista és `LiveCharacter`.
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

## Labirintusgenerálás

A generátor kezdetben falakkal tölti fel a pályát, majd rekurzív mélységi bejárással összefüggő folyosóhálózatot vés ki egy ötlépéses logikai rácson. A csomópontok két cella szélesek; az összekötő folyosók a konfigurált valószínűséggel kétcellásak.

Ezután a generátor:

1. véletlen méretű szobákat próbál elhelyezni;
2. ajtóval kapcsolja őket a meglévő járatokhoz;
3. útkereséssel ellenőrzi, hogy a bejárat és kijárat kapcsolata megmaradt-e;
4. elhelyezi a kijáratot;
5. üres, járható cellákon ládákat és konfigurált ellenfeleket helyez el.

Az 1–3. labirintusszint külön konfigurációval rendelkezik. A későbbi szintek a harmadik szintből számított, fokozatosan növekvő szobaszámot, jutalmat és ellenfélszámot kapnak. Az ellenféltípusok listája azonban külön konfiguráció nélkül továbbra is a harmadik szint típusaiból származik.

## Látómező és köd

A `FogOfWar` pályánként külön logikai tömbben tárolja a már felfedezett cellákat. A játékos körül 5 cellás Chebyshev-távolságon belül Bresenham-jellegű látóvonal-ellenőrzés történik. A fal és az ajtó látható lehet, de blokkolja a mögötte lévő cellákat.

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

A `ConsoleRenderer` a pályát, a karakterlapot, az ASCII-képpanelt és az üzenetnaplót egy rögzített konzolelrendezésben jeleníti meg. Mozgáskor és csatakor csak az érintett cellákat vagy panelsorokat írja újra. Emiatt a játékmeneti osztályok a teljes újrarajzolás helyett célzott renderer-metódusokat hívnak.

A pálya mérete a renderer játékterének méretéből származik, ezért a generálás és a konzolelrendezés jelenleg közvetetten össze van kötve.

## Mentés

A `CharacterSaveService` a karaktereket a futtatási könyvtár `karakterek.json` fájljába menti. Megmarad többek között:

- faj és osztály;
- képességek, HP/manna és generált bónuszok;
- élelem, víz, arany, szint és XP;
- fegyverek, páncél, varázstárgyak és hátizsák;
- az aktív karakter indexe.

A mentés definícióazonosítókat használ, és a betöltéskor az aktuális `GameDataCatalog` elemeihez kapcsolja vissza őket. Régebbi, névalapú mentésekhez kompatibilitási útvonal is tartozik. A labirintus, az aktuális pályaszint, az ellenfelek és a köd nem része a mentésnek.

## Függőségek és állapotkezelés

A projekt csak a .NET alaprendszerét használja, külső NuGet-csomag nincs. A függőségek konstruktoron keresztül jutnak el a fő objektumokhoz, de nincs külön függőséginjektáló keretrendszer.

Fontos állapotélettartamok:

- `GameDataCatalog`: egy alkalmazásfutásra változatlan;
- `CharacterRoster` és `LiveCharacter`: menük és játékok között tovább él, JSON-ba menthető;
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
