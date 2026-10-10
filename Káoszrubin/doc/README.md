# Káoszrubin játék architektúrája

> A dokumentum a jelenlegi kód működését írja le. A hangolható értékek elsődleges forrása
> a [game-data.csv](../Data/game-data.csv), a pályáké a
> [MazeLevelConfiguration.cs](../World/MazeLevelConfiguration.cs).
> Mentésformátum: **35**; session-protokoll: **102**. Ezek egymástól független verziók.

## Tartalom

- [Áttekintés](#attekintes)
- [Indítási és fő adatfolyam](#inditasi-es-fo-adatfolyam)
- [Projektfelépítés](#projektfelepites)
- [Adatmodell és CSV](#adatmodell-es-csv)
- [Karakter létrehozása és fejlődése](#karakter-letrehozasa-es-fejlodese)
- [Játékhurok és időmodell](#jatekhurok)
- [Parti](#parti)
- [Labirintusgenerálás](#labirintusgeneralas)
- [Pályavége és fogadó](#palyavege-es-fogado)
- [Szörnyek](#szornyek)
- [Látómező és köd](#latomezo-es-kod)
- [Taktikai harc](#csata-algoritmusa)
- [Ellenséges AI](#ellenseges-ai)
- [Megjelenítés](#megjelenites)
- [Mentés](#mentes)
- [Függőségek és állapotkezelés](#fuggosegek-es-allapotkezeles)
- [Adatbővítés](#adatbovites)
- [Csapdák](#csapdak)
- [Fegyverek és sebzéstípusok](#fegyverek-sebzestipusok-es-tartalekfegyver)
- [Varázslatok](#varazslatok)
- [Session-események](#session-esemenyek)

*Kapcsolódó részletes leírások:*

- [coop](coop-design-readme.md)
- [erdei generálás](forest-generation-readme.md)
- [térkép szerkesztő](../Tools/MapEditor/map-editor-readme.md)
- [küldetésrendszer](quest-readme.md)
- [küldetésszobák célképernyője és AreaId-ja](quest-room-placement-readme.md)

## Ténylegesen működő fejlesztői funkciók

Az alábbi rejtett gyorsbillentyűk közvetlenül be vannak kötve a játék fő bemeneti ciklusába:

- `Ctrl+Shift+U`: a teljes térkép megjelenítésének be- és kikapcsolása a felfedezettségi adatok módosítása nélkül;
- `Ctrl+Shift+R`: az aktuális futam következő, újonnan generált labirintusának indítása;
- `Ctrl+Shift+E`: a partyvezér teleportálása a kijárathoz legközelebbi szabad mezőre;
- `Ctrl+Shift+Y`: a szabad partihelyek feltöltése Harcos–Mágus–Lovag sorrendben;
- `Ctrl+Shift+Í`: egy véletlen osztályú, első szintű NPC hozzáadása, ha van szabad partihely;
- `Ctrl+Shift+I`: a falakon való áthaladás be- és kikapcsolása.
- `Ctrl+Shift+L`: pályaszám bekérése, majd a teljes parti áthelyezése a kiválasztott kampánypálya bejáratához;
- `Ctrl+Alt+S`: a partyvezér azonnali felléptetése a következő szintre a hiányzó XP megadásával;
- `Ctrl+Alt+W`: minden partitagnak +5000XP + fegyverek;
- `Ctrl+Alt+X`: a szabad partihelyek feltöltése Barbár–Tolvaj–Pap sorrendben;
- `Ctrl+Alt+N`: teleport a következő egyedi npc-hez.
- `Ctrl+Alt+B`: teleport a következő boss-hoz.
- `Ctrl+Alt+K`: teleport egy pozícióra.
- `Ctrl+Alt+T`: paraméterezhető harci tesztpálya létrehozása. A vezér mellé azonos szintű Mágus, Pap és Lovag kerül alapfelszereléssel és véletlenül memorizált, szintjükön elérhető varázslatokkal. A felső térfélen legfeljebb 8, egyenként legfeljebb 12 fős, helyben várakozó ellenségcsoport áll; mindegyik mellett egy jelölőláda látható. A pálya kezdetben teljesen felfedett, a köd `Ctrl+Shift+U`-val kapcsolható vissza.

A két rögzített osztályszett magasabb szintű, véletlenül generált karakterei három felszerelt varázstárgyat és pontosan egy kulcsot kapnak. A kulcs számára telt hátizsáknál az utolsó véletlen tárgy helye szabadul fel. A Mágus, Pap és Lovag egy pálcát, egy számukra használható tekercset és egy passzív gyűrűt vagy amulettet visel. A Harcos, Barbár és Tolvaj tekercs helyett egy második pálcát kap, így a tekercsek normál kasztkorlátozása változatlan marad.

<a id="attekintes"></a>

## Áttekintés

A Káoszrubin egy Windowsra célzó, .NET 10 konzolos labirintusjáték, helyi egyjátékos és LAN coop móddal.
A host birtokolja és módosítja a világot; a vendég szemantikus parancsokat küld, és replikált
állapotot jelenít meg. Az alkalmazás adatvezérelt: a fajok, osztályok, ellenfelek, felszerelések,
varázslatok, küldetések és fejlődési küszöbök a `game-data.csv` fájlból töltődnek be.
A karakterlista JSON-fájlban, a teljes futamok időbélyeges `.save` állományokban maradnak meg.

### Támadóvarázslatok becsapódása

A buffok és gyógyítások a `defensive-spell.wav` hangot használják térképen és csatában is. Ez közös session-hang: akkor is hallható a helyi és coop játékosok számára, ha másik partitag varázsol önmagára vagy egy társára. A `BecsapódásIdőMs = 0` csak a vizuális effektet kapcsolja ki, a hangot nem.

A támadóvarázslatok becsapódását az egycélpontos és lánctámadásoknál pulzáló előtér/háttér, területi támadásoknál kifelé futó színhullám jelzi. A lángtölcsér a sebzés tényleges kúpalakját követi. Csak felfedett térképcellák rajzolódnak; minden képkocka az aktuális térképállapot jelét színezi át, lejáratkor pedig az aktuális mező és harci fókusz áll helyre.

A `#Varázslatok` és `#Papi varázslatok` szekció opcionális megjelenítési oszlopai:

- `BecsapódásSzín`: `Red` (tűz/pusztítás), `Blue` (jég/arkán), `YellowBrown` (villám/szent fény), továbbá a sötét varázslatokhoz `Purple`, `SicklyGreen`, `Shadow` vagy `BloodRed`. Az előtér és a háttér együtt változik. Üres mezőnél az arkán iskola kék, a papi iskola sárgásbarna.
- `BecsapódásIdőMs`: nemnegatív egész ezredmásodperc. `1500` = 1,5 másodperc, `3000` = 3 másodperc, `0` = kikapcsolva. Üres vagy hiányzó oszlop esetén egy célpont és lánc: 1500 ms, terület és irány/tölcsér: 3000 ms. Ez csak a látvány ideje; a varázshatások körökben megadott időtartama külön adat marad.
- `CsakEllenség`: `igen` esetén a varázslat kizárólag ellenséges varázsprofilba kerülhet; játékos nem tanulhatja, memorizálhatja vagy használhatja varázstárgyból.
- `NaplóEmoji`: a varázslat neve előtt megjelenő, akár több jelből álló ikon. Üres mezőnél `✨` az alapértelmezés. A játékos sebző és a sötét ellenséges varázslatok külön, tematikus ikont kapnak a CSV-ben. Az ellenséges sebző varázslat naplója célpontonként a ténylegesen elvesztett és a megmaradt HP-t, valamint az alkalmazott ellenállásokat mutatja.

A parti varázssebzés elleni védelme felszerelt gyűrűből vagy amulettből és időleges varázshatásból állhat. A tűz-, sav-, jég-, villám- és nekrotikus védelem csak a megfelelő típusú varázssebzésre hat; az általános varázsvédelem mindegyikre és a típus nélküli varázssebzésre is. Azonos típusú értékek összeadódnak, legfeljebb 100%-ig. A típusos és az általános védelem egymás után, százalékosan csökkenti a mentő után fennmaradó sebzést. A felszerelések százalékértéke állandó; a P032, P033 és S033 védővarázslatok értéke a varázsló Intelligenciájával és szintjével nő, legfeljebb 75%-ig. A számítás a közvetlen ellenséges varázslatokra, a baráti tűzre, a folyamatos varázshatásokra és a viharterületekre is érvényes; a fegyversebzés változatlan szabályokat használ.

Az ellenséges varázshasználók kezdetben nem ismerik a parti ellenállását. Csak saját, ténylegesen célba érő és ellenállásba ütköző varázslatuk vagy viharterületük után tárolnak becslést az adott partitag és sebzéstípus párosához. A becslés relatív tévedése 1 Intelligenciánál legfeljebb ±50%, 20 Intelligenciától legfeljebb ±2%; minden új megfigyelés frissíti. A becslés csak az adott ellenség varázslat- és célpontpontozását befolyásolja, más ellenséghez nem jut át, és a mentésben megmarad.

A mellékelt CSV minden effekt nélküli varázslatnál (buff, gyógyítás, teleportáció, feltámasztás és általános mágiaoszlatás) explicit `0` értéket használ. A 24 támadóvarázslat különböző, jellegéhez igazított időt kapott 500–5000 ms között: a Villámcsapás 500, a Mágikus lövedék 700, a Tűzgolyó 3000, a Meteorzápor 4700, az Arkán kataklizma 5000 ms. Az ellenséges gyengítések, például a Vakítás és a Lassítás, továbbra is becsapódási effektet kapnak.

Az animáció a közös varázsvégrehajtásból indul, ezért a tárgyból, NPC-től és ellenségtől érkező támadásokat is kezeli. A végrehajtás csak regisztrálja az időalapú effektet, a normál játékhurok pedig képkockánként rajzolja; nincs várakozó ciklus vagy háttérszálas konzolírás. A sebzés, a következő harci kör, a mozgás, a szükségletek, az NPC-önellátás, a beszélgetések és a coop feldolgozás az animáció közben is folytatódhat. Egymást átfedő effektek egyszerre élhetnek, a később indított kerül felülre. Átirányított konzolkimenetnél és nem látható becsapódásnál nem indul vizuális effekt.

### Halottűzés

Az `MA001` Élőholt tulajdonságú ellenfelek ellen a Pap és a Lovag az első körtől külön kasztakciót használhat, karakterenként 10 körös újrahasználati idővel. Az 1. körben használt képesség a 11.-ben, a 4.-ben használt a 14.-ben válik újra elérhetővé; az extra akciók nem rövidítik a várakozást. Új csatában ismét az első körtől elérhető. A hatótáv 2 térképcella, átlósan is, alakzattól és fegyvertől függetlenül. A partyvezérnél ez a `T` billentyű, az NPC-k pedig automatikusan választják. A képesség nem varázslat, ezért nem fogyaszt mannát, nem igényel memorizálást vagy fókusztárgyat, de egy teljes harci akcióba kerül.

- Pap: `1d20 + Intelligencia + szint/2` a `10 + ellenfél-erősség×2` nehézség ellen. Sikerre az élőholt két akciót kihagy. Legalább 10 pontos túldobás az 1–2-es erősségű, nem vezér élőholtat azonnal megsemmisíti.
- Lovag: `1d20 + Erő + szint/3` ugyanilyen nehézség ellen. Sikerre `1d6 + szint/2` szent sebzést okoz, az ellenfél kihagyja következő akcióját, a Lovag pedig két akcióra +2 védelmet kap.
- Kudarc esetén is elfogy az akció és elindul a 10 körös újrahasználati idő. A 4–5-ös erősségű és vezér ellenfelek nem semmisíthetők meg azonnal.

### Bossok és aranykulcsok

Az `EnemyDefinition.IsBoss` jelöli a tizenkét bossfajt. Ezek: Patkányember, Ghoul, Ork sámán, Fagyóriás, Vörös sárkány, Hidra, Vén beholder, Csontsárkány, Ősvámpír, Drakolich, Balor démon és végül a Káoszsárkány. A kampány első célja mind a 12 aranykulcs összegyűjtése.

Egy bossfaj legyőzése pontosan egyszer ad aranykulcsot, ezért ismételt példány vagy mentés-visszatöltés nem sokszorozhatja a jutalmat. A kulcsok nem inventorytárgyak: a játékállás az összegyűjtött bossazonosítókat tárolja, a karakterlap pedig a labirintusszint mellett `🔑 n/12` formában mutatja az előrehaladást. A tizenkettedik kulcs megszerzése külön cél-teljesítési üzenetet ad.

Amikor egy boss mezője először ténylegesen láthatóvá válik, modális bossbemutató ablak jelenik meg. Az ablak a varázsválasztóhoz hasonlóan kizárólag a térkép fölé rajzolódik: előtte elmenti az érintett térképcellák vizuális állapotát, bezáráskor pedig célzottan csak ezeket állítja vissza. A karakterlap és a teljes konzol újrarajzolása nem szükséges. A már bemutatott bossazonosítók szintén a teljes játékmentés részei. Mind a 12 boss saját, E/1-ben elmondott történetet kapott a II–XIII. fejezetben. A korai őrzők csak töredékeket és szóbeszédeket ismernek, a későbbiek egyre pontosabban beszélnek a kulcsokról, a Káoszrubinról és az előttünk álló útról. A tizenkét kulcs megszerzése a XIV. fejezettel folytatja a történetet.

Új játék indításakor ugyanezzel a térképoverlay-mechanizmussal jelenik meg a Káoszrubin eredetét,
Aurelios Máguskirály megbízását, Vhar-Zul fenyegetését és a Kulcshordozók küldetését elmesélő
nyitófejezet. A tizenkettedik kulcs megszerzése a XIV. fejezet mérföldköve, nem játékbefejezés.
A kampány összesen **22 szintből** áll; nem nyílik további huszonkét pálya a kulcsok után.

A megoldás fő felelősségi területei:

- **indítás és menü:** az adatok betöltése, karakterkezelés és játékindítás;
- **adatmodell:** CSV-ből érkező, többnyire változatlan definíciók;
- **karakterállapot:** a játék során változó értékek és felszerelés;
- **világmodell:** labirintus, szobák, játékos és térképi objektumok;
- **játékmenet:** bemenet, időzített események, pályaváltás és találkozások;
- **harc:** több résztvevős taktikai körök, mozgás, közelharc, lövészet, varázslás és kasztakciók;
- **megjelenítés:** közvetlen, részleges konzolfrissítés.

<a id="inditasi-es-fo-adatfolyam"></a>

## Indítási és fő adatfolyam

```text
Program
  ├─ CsvGameDataLoader ── game-data.csv ──> GameDataCatalog
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
2. Az alkalmazás kimeneti könyvtárából betölti az `game-data.csv` fájlt.
3. A `CsvGameDataLoader` létrehozza a `GameDataCatalog` katalógust.
4. A `MainMenu` betölti a `karakterek.json` állományt, ha létezik.
5. A felhasználó karaktert készíthet, választhat vagy törölhet, illetve játékot indíthat.
6. A `Game` minden labirintusszinthez új világot, játékospozíciót és ködállapotot hoz létre, de ugyanazt a `LiveCharacter` példányt használja tovább.

<a id="projektfelepites"></a>

## Projektfelépítés

### `World`: világmodell

- `Maze.cs`: a pályarács, a szobák és a térképi objektumok tárolója.
- `MazeGenerator.cs`: labirintus, szobák, ládák és ellenfelek létrehozása.
- `ForestMazeGenerator.cs`: erdei tisztások, összefüggő ösvények, tavak, mocsárszegély, növényzet és zárt épületek létrehozása.
- `ForestMazeConfiguration.cs`: az erdősűrűség, terepgyakoriságok, tavak, épületek, falstílusok, ösvényszélesség, rúnák és színek szerkesztői API-ja.
- `ForestAreaConfiguration.cs`: névvel ellátott erdőképernyők, örökölhető template-ek, helyi felülírások és explicit gráfkapcsolatok.
- `ForestConfigurationJson.cs`: a szerkesztő és a játék közös, verziózott JSON-formátuma.
- `MazeTerrainStyle.cs`: a rúnánként konfigurálható járhatóság, látástakarás és megjelenítés közös modellje.
- `DungeonAreaGraph.cs`: a többképernyős szint absztrakt, kétdimenziós gráfja és a mellékágas/hurkos gráfgenerátor.
- `DungeonLevel.cs`: a létrejött képernyők, az aktív képernyő, valamint a stabil bejárati és kijárati képernyő kezelése.
- `MazeLevelConfiguration.cs`: szintenkénti nehézség és generálási tartományok.
- `MazeGenerationSettings.cs`: egy konkrét generálás már kisorsolt beállításai.
- `FogOfWar.cs`: felfedezett cellák, látóvonal és fejlesztői felfedés.
- `Player.cs`, `Enemy.cs`: mozgó világobjektumok.
- `TreasureChest.cs`, `Corpse.cs`: felvehető vagy dekoratív világobjektumok.
- `WorldObject.cs`: minden, pályaborítástól független objektum alaptípusa.
- `Position.cs`, `Direction.cs`, `Room.cs`: alapvető térbeli értékobjektumok.

### Többképernyős pályagráf

A többképernyős szintet a futásidő már nem a `Areas` lista első és utolsó eleme alapján értelmezi.
A `DungeonLevel` külön `EntranceAreaId` és `ExitAreaId` értéket őriz, ezért a képernyők sorrendje nem
határozza meg a bejáratot, a valódi pályakijáratot vagy a visszatérő expedíció célját. A mentési formátum
a képernyők kétdimenziós koordinátáját és topológiai szerepét is tárolja; a 31-es mentésekből a 32-es
migráció a korábbi lineáris sorrendet állítja vissza. A 33-as formátum képernyőnként egyszer menti a
terepstílus-palettát is, így a járható aljnövényzet és a nem járható víz mentés után sem változik padlóvá.
A 34-es formátum a Tiltott Erdő 6. pályára történt beszúrását vezeti át: a régi 6–21. kampányszinteket,
helyazonosítókat, ad-hoc párbeszédszinteket és az NPC-k csatlakozási történetét eggyel feljebb számozza.

A `DungeonAreaGraphGenerator` összefüggő, ortogonális metarácsot készít. A konfiguráció megadja a
képernyőszám tartományát, a bejárat és kijárat minimális gráftávolságát, a maximális csomóponti fokszámot,
a mellékágak és a plusz hurkok esélyét. A generátor minden csomópontot bejárat, főút, elágazás, mellékág,
zsákutca vagy kijárat szereppel jelöl. A képernyők közti átjárók a koordinátákból következő égtáj szerinti
páros oldalon készülnek el. A klasszikus és széles pályák ugyanezen modell lineáris tervét használják;
az erdei generátor pedig erre a közös alapra köti rá a procedurális vagy explicit gráfot.

Az erdei elrendezéshez a pályakonfiguráció `Layout` mezője `ForestMazeLayoutConfiguration` értéket kap.
A benne lévő `DungeonAreaGraphConfiguration` 6–10 vagy akár más számú képernyőből mellékágas, hurkolható
gráfot készít. Alternatívaként az `ExplicitForestAreaGraphConfiguration` névvel, koordinátával,
template-tel és stabil azonosítóval adja meg az egyes képernyőket. A template-ek egymásból örökölhetnek,
a képernyők pedig csak az eltérő paramétereket írják felül. A `ForestGenerationConfiguration` szabályozza a területi erdőborítást, a fa- és
bokorcsoportok léptékét, a lombos és fenyves tájegységek arányát, az aljnövényzetet, a tavak és önálló
mocsarak számát/méretét, a mocsaras tópart arányát, az ösvények szélességét, kanyargását és kerülőágait,
valamint az épületek számát, méretét, belső tagolását és ajtóállapotainak esélyét.
A `ForestTerrainPalette` minden tereptípus rúnáját, előtér- és háttérszínét külön engedi felülírni.
Az összefüggő növényzetfoltok alapja a több léptékű, térben torzított `ForestTerrainField`.
A borítási küszöböt a tényleges térkép kvantilise adja: a `ForestDensity` már a teljes belső területre
hat, nem pusztán az ösvényszélek ritkítására. A fafajokat külön, nagyobb léptékű mező választja el;
a bokrok csak a nyílt erdőszegélyen jelennek meg. A tavak és mocsarak szabálytalan, forgatott foltok,
a tavakat kerülő ösvények hullámzó köztes pontokat kötnek össze súlyozott útkereséssel. Az elzárt
természetes tisztások is bekötést kapnak; vízzel teljesen körbezárt száraz szigethez szükség esetén
keskeny mocsári átkelő vezet. A véletlenmag az egész generálást reprodukálhatóvá teszi.
A `CTRL+M` régiótérképe csak a ködből már ismert képernyőket és kapcsolatokat mutatja;
az el nem ért területek neve és a rejtett gráfrész nem szivárog ki. A vendégjátékos ugyanezt a hosttól
kapott pillanatképből látja.
Hangolási példák és a paraméterek pontos jelentése: [Erdei pályagenerálás](forest-generation.md).

Az egy képernyőn belüli tisztások a közös `Room` modellt használják. Emiatt a `RoomEncounters` és a
kincsesládák tisztásokra, a `CorridorEncounters` pedig ösvényekre és egyéb járható erdei területekre kerülnek.
A konfigurált épületek ebbe a közös teremszámba tartozó, fallal körülvett belső terek: egy külső ajtót,
megfelelő méretnél pedig külön ajtós belső válaszfalat kapnak, ezért valódi mini-labirintusként működnek.
A fa, fenyő és sűrű bozót nem járható és takarja a látást; a bokrok átláthatók, de nem járhatók; az
aljnövényzet és a mocsár járható; a víz nem járható, de nem takarja a túlpartot. A terepszínek a host és
a coop vendég világpillanatképében is azonosan jelennek meg.

### `Application`: játékmenet és futásvezérlés

A `Game` részleges osztály: a fájlok ugyanazt a futásidejű állapotot használják, nem külön játékokat.

| Fájl vagy fájlcsoport | Felelősség |
|---|---|
| [Game.cs](../Application/Game.cs) | Közös állapot, konstrukció, session- és harci snapshotok |
| [Game.World.cs](../Application/Game.World.cs) | Fő játékhurok, világindítás és visszaállítás |
| `Game.Exploration*.cs`, `Game.WorldSimulation.cs` | Felfedezés, lövészet, ellenfélmozgás, üldözés és keresés |
| `Game.Combat*.cs`, `Game.BattleInputAndSpells.cs` | Taktikai összecsapások, AI, akciófeloldás és varázslás |
| `Game.Session.cs`, `Game.SharedWindows.cs` | Parancsfeldolgozás, közös ablakok és coop-egyeztetés |
| [Game.PlayerWindowsAndNeeds.cs](../Application/Game.PlayerWindowsAndNeeds.cs) | Személyes ablakszünetek, időjelző, állapotkörök és szükségletek |
| `Game.Inventory.cs`, `Game.Progression.cs`, `Game.QuestsAndConversations.cs` | Inventory, fejlődés, küldetések és párbeszédek |

- `DoorInteractionController.cs`, `InnController.cs`: az összetett játékosi interakciók vezérlői.
- `GameSettings.cs`, `InnTrade.cs`, `ReturnExpeditionRules.cs`: alkalmazásszintű beállítások és játékmeneti szabályok.
- A `PartyMovementController`, `PartyFormationController`, `PartyAiController`, `PartySustenanceService`,
  `LootAndInventoryService`, `DungeonTrapService` és `DungeonExpeditionCoordinator` külön felelősségeket
  vesz át a koordinátortól.
- Az `Application/Quests` tartalmazza a küldetéskezelőt, állapottárat, haladás-, jutalom-, utazás- és
  megjelenítési szolgáltatásokat; a világ- és mentésadapterek az `Infrastructure/Quests` alatt vannak.

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

A `Definition` végű típusok az `game-data.csv` tartalmát képviselik. A `LiveCharacter` ezzel szemben változó futásidejű állapot: HP, manna, szükségletek, arany, XP, szint, felszerelés, hátizsák, valamint az ismert és memorizált varázslatok.

### `UI`: menük

- `MainMenu`: új játék, mentésbetöltő, karakterlista, kiválasztás, törlés, gyorsindítás, súgó, valamint LAN coop host/join indítás. A host mód szükség esetén létrehoz egy átvehető első szintű NPC társat; a vendég címet és játékosnevet ad meg, majd név/osztály/szint alapján választ a host szabad NPC-i közül.
- `CharacterCreationScreen`: név- és fajválasztás, tulajdonságdobás, osztályjogosultság és karakter létrehozása.
- `ConsoleRenderer.cs`: a teljes konzolos nézet és annak részleges frissítése.
- `AsciiPortraits.cs`: a jobb alsó képpanel beépített ASCII-ábrái.
- `GameInput.cs`: a közvetlen billentyűzetes játékvezérlés leképezése.

### `Audio` és `Infrastructure`

- `Audio`: háttérzene és hangeffektusok lejátszása.
- `Infrastructure`: a Windows Terminal környezet felismerése és elindítása.

### `Combat`: harci szabályrendszer

- `BattleSystem`: megjelenítéstől független dobások, fegyveres támadások és karakter-/ellenfélfelkészítés.
- `BattleKill`, `BattleCharacterResult`, `BattleLogEntry`: jutalmazáshoz és összegzéshez használt eredmények és naplóesemények.
- `BattleEncounter`: a tényleges, több karaktert és ellenfelet összekötő futásidejű összecsapás.
- `TacticalBattleState`: résztvevők, kezdeményezési sorrend, körszám és monoton `TurnId`.
- `TacticalBattleCoordinator`, `BattleActionCoordinator`: célpontok, mozgás, lekötés és akciók szabályai.
- `BattleState.cs`: `BattleId`, taktikák és karakterenkénti `CharacterBattleChoices`; a fájlnév
  nem jelent önálló `BattleState` osztályt.

### `Application`: session- és parancshatár

- `GameSession`: egy futó játék helyi vagy később hálózati parancsainak egyetlen belépési pontja. Tulajdonjog, session-fázis és monoton parancssorszám alapján validál, majd a `Game` egyetlen szimulációs szála olvassa ki az elfogadott parancsokat.
- `SessionContracts`: stabil `PlayerId`/`CharacterId` alapú commandok, vezérlési állapotok és sorrendezett session-eseményfolyam. Minden harci választás `BattleActionCommand`, amely az aktuális `BattleId` és `TurnId` mellett csak szemantikus inputot hordoz: akciótípust, varázslat-ID-t, opcionális varázstárgyslotot és célpozíciót.
- `SessionSnapshots`: a doménobjektumoktól leválasztott, JSON-nal körbeírható host read model. A `SessionSnapshot` protokollverziót, monoton snapshot-sorszámot, eseménykurzort, fázist, parti-erőforrásokat és -pozíciókat, vezérlési kiosztást, valamint opcionális aktív harci promptot tartalmaz. A snapshot harci része csak az aktuális session-prompt azonosítóival és akció-whitelistjével hozható létre.
- `WorldSnapshots`: a kliens által ismert pályarész read modelje. Kódpontként tárolt felfedett cellákat, ajtókat, ellenfeleket, ládákat, tetemeket és földi tárgykupacokat tartalmaz; köd mögötti geometriát vagy entitást nem publikál. A dinamikus térképi objektumok stabil futásidejű `WorldEntityId`-t kapnak, amelyre a későbbi deltaüzenetek hivatkozhatnak.
- `WorldDeltas`: két egymást követő, azonos `WorldId` értékű snapshot determinisztikus különbsége. Cellafelfedést vagy cellaváltozást, ajtó-upsertet és -eltávolítást, entitás-upsertet, illetve stabil ID-alapú entitáseltávolítást hordoz. A delta `FromSnapshotSequence`/`ToSnapshotSequence` párja megakadályozza, hogy a kliens rossz baseline-ra alkalmazza; pályaváltáskor teljes snapshot szükséges.
- `SessionReplicationPublisher`: transportfüggetlen, kliensenkénti host-publisher. Első kapcsolódáskor teljes snapshotot ad, majd kizárólag a kliens által ACK-olt snapshotból képez deltát. Korlátozott pending snapshot-ablakot tart; ismeretlen ACK, explicit resync vagy pályaváltás esetén teljes snapshotra vált vissza. A delta-frame a friss session/party állapotot world rész nélkül és mellette a world deltát hordozza.
- `ClientSessionStore` + `WorldDeltaReducer`: transportfüggetlen kliensoldali read model. A teljes snapshotot és legfeljebb 16 korábbi baseline-t tart meg, a deltát mindig annak deklarált snapshotjára alkalmazza, majd teljes, renderelhető `SessionSnapshot`-ot publikál. Sikerre ACK-ot, hiányzó vagy hibás baseline-ra resync-kérést ad; más játékosnak címzett vagy inkompatibilis frame-et elutasít.
- `CoopProtocol`: protokollverziót, alkalmazásverziót és SHA-256 katalógushasht hordozó handshake, 256 bites reconnect-token, snapshot ACK/resync üzenetek, valamint explicit allowlistes JSON wire codec. A wire formátum nem fogad CLR-típusneveket vagy tetszőleges polimorf payloadot; minden támogatott commandnak és szerverüzenetnek stabil protokollneve van.
- `CoopHostGateway`: a SignalR connection ID-t a handshake során kiosztott `PlayerId`-hoz köti, és minden karakterátvételi kérésnél, commandnál, ACK-nál és resyncnél ellenőrzi ezt a kötést. A gateway kezeli a disconnect → NPC átmenetet és címzetten készíti el a publisher frame-jeit; így a Hubban nincs gameplay-szabály.
- `InMemoryCoopTransport`: kétirányú, aszinkron host–client csatornapár ugyanazzal a szöveges wire-protokollal, amelyet később a SignalR továbbít. Integrációs tesztben végigviszi a hello/accept, NPC-hozzárendelés, távoli command, teljes snapshot és ACK folyamatot hálózati függőség nélkül.
- `Transport/SignalR`: beágyazható Kestrel/SignalR LAN host és valódi SignalR kliens. A `/coop` Hub egyetlen `SendWire` metódussal fogad, a `ReceiveWire` kliensmetódussal küld, és kizárólag a `CoopHostGateway` eredményét továbbítja. A `CoopHostRuntime` egyetlen latest-value csatornával, 100 ms-os ütemezéssel választja le a szinkron játékhurkot az aszinkron hálózati I/O-ról; torlódáskor nem blokkolja a játékot, hanem a köztes snapshotokat eldobja. A `CoopSignalRClient` végigviszi a hello/reconnect folyamatot, NPC-karaktert kér, a frame-eket a `ClientSessionStore`-ba vezeti, automatikusan visszaküldi az ACK/resync választ, monoton command ID-t oszt és szemantikus commandot küld. Az alapértelmezett `http://0.0.0.0:5127` csak LAN-fejlesztési végpont, publikus internetre TLS/relay nélkül nem tehető ki.
- `InventorySnapshots`: minden felszerelés- és hátizsákhelyet — az üreseket is — explicit `(InventorySlotKind, index)` címmel publikáló read model. A slotban definíció-ID, megjelenítési név, kategória, ritkaság és töltetszám utazik; a részletes statisztikát az azonos verziójú helyi katalógus adja. A `LiveCharacter.InventoryRevision` minden sikeres inventory-mutációnál egyszer nő, hogy a későbbi commandok elavult kliensállapotot észlelhessenek.
- `InventoryTransferCommand` + `InventoryTransferService`: a kliens csak forrás- és célkaraktert/slotot, valamint az elvárt inventoryrevíziókat küldi. Itemdefiníció és töltetszám nem érkezhet a klienstől. A host ugyanazzal a közös szabálykészlettel validálja és hajtja végre az atomi slotcserét; a tárgyak és töltetek vagy mindkét oldalon együtt változnak, vagy semmi nem módosul. A vendég a saját karakterén belül minden szokásos mozgatást használhat, valódi partykarakterek között pedig kizárólag hátizsákból hátizsákba adhat át vagy cserélhet tárgyat; más karakter felszerelését, használatát, eldobását, felezését és szétosztását nem kezelheti. A hostnak adott és a hosttól elvett tárgy külön naplóüzenetet kap. A `DistributeInventoryStackCommand` és `InventoryDistributionService` az `S`-sel kijelölt elfogyasztható kötegből egy darabot lefoglal a forráskarakternél, a többit a valódi partitagok között körkörösen osztja; azonos köteget tölt, különben az első üres hátizsákhelyet használja, és a helyhiány miatt ki nem osztható maradékot veszteség nélkül a forrásban hagyja.
- `UseInventoryItemCommand`, `DropInventoryItemCommand`, `PickUpGroundItemCommand`: a további inventory-műveletek is definíció- és hatáseredmény nélküli szándékok. Használatnál/eldobásnál inventoryrevízió és slot, pickupnál ezen felül stabil `WorldEntityId`, kupacrevízió, item-index és üres cél-hátizsákslot utazik. A host oldja fel a tárgyat, célt és karakterpozíciót, alkalmazza a fogyóeszköz hatását, illetve módosítja a world állapotot.
- A `GroundItemPile` item-entrynként megőrzi a töltetszámot és saját monoton revíziót vezet. A world snapshot és a mentés is hordozza ezeket, ezért egy részben elhasznált pálca vagy tekercs ledobás–felvétel, reconnect és mentés után sem töltődik vissza véletlenül.
- A konzolos leader mozgása, ajtókezelése, partiparancsai, pihenése és pályaváltása már ezen az útvonalon halad. Távoli vezérlő egy NPC-partitag mozgását veheti át; ilyenkor az automatikus NPC-mozgás leáll, disconnectkor visszaáll, reconnectkor pedig ugyanahhoz a karakterhez tér vissza.
- A helyi és távoli emberi karakterek a közös, host-authoritatív `BattleEncounter` résztvevői.
  A `BattlePromptEvent` az aktuális `BattleId` / `TurnId` és akció-whitelist alapján kér döntést
  a karakter tulajdonosától. A host ellenőrzi a célpontot, jogosultságot, erőforrást és véletlent;
  a kliens nem küld dobás- vagy sebzéseredményt. Disconnect esetén az NPC-vezérlés veszi át a karaktert.
- A snapshot a fogadói ajánlatokat és döntéseket, közös történeti/pályakép/pihenési ablakokat,
  memorizálási és szintlépési promptokat, személyes ablakszüneteket, küldetésnaplót,
  időjelzőt, varázseffekteket és lövedékeket is hordozza. A vendég varázsolhat, célozhat,
  fogadói tranzakciót küldhet, és válaszolhat a saját fejlődési/memorizálási promptjára;
  ezek nem csak tervezett bővítések.
- A `Game.CreateSessionSnapshot()` a futó állapotból leválasztott read modelt készít.
  A publikálás dirty-jelzés és legfeljebb 10 Hz-es ütemkorlát alapján történik, kétmásodperces
  heartbeat mellett. A felfedett terep és a ténylegesen észlelt ellenfél két külön szűrés:
  ismert cellán álló, már nem látható ellenfél helyett csak korlátozott emlék vagy hangjel kerülhet át.
- A valódi parti publikus karakterlap- és inventoryadatait minden vendég megkapja,
  a karakterhez kötött promptokat és vezérlést a tulajdonjog határolja.
  A `CharacterSheetPanel` és `GameInputBindings` hoston és vendégen közös.
  Az `N/Z/K` a karakter saját pozíciójára hat; a pályaváltás és partiparancsok leader-only műveletek.
  A vendég render fingerprintje figyelmen kívül hagyja a puszta replikációs sorszámváltozást.

#### Replikációs adatfolyam

```text
helyi / távoli szándék
  → GameSession validáció és command queue
  → SessionCommandDispatcher → Game és szabályszolgáltatások
  → SessionSnapshot + WorldSnapshot
  → SessionReplicationPublisher → teljes kép vagy ACK-baseline delta
  → wire codec / SignalR
  → ClientSessionStore → WorldDeltaReducer → CoopGuestScreen
```

<a id="adatmodell-es-csv"></a>

## Adatmodell és `game-data.csv`

A CSV `#` karakterrel kezdődő szekciókból áll. A betöltő az ékezeteket és kis-/nagybetűket figyelmen kívül hagyva azonosítja a szekcióneveket. A jelenlegi szekciók:

- fajok és faji képességbónuszok;
- osztályok, képességminimumok és kezdőfelszerelések;
- osztályonkénti tehetségek és tehetségfokozatok;
- osztályonkénti karakter- és később NPC-ként is használható nevek;
- karakterállapotok;
- szintlépési XP-küszöbök;
- ellenfelek;
- szörnyképességek és külön, sorrendezett szörnyképesség-hatások;
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

Az `game-data.csv` a projektfájl beállítása miatt fordításkor a kimeneti könyvtárba másolódik. A program futáskor ezt a másolatot olvassa, nem feltétlenül a forráskönyvtárban lévő fájlt.

<a id="karakter-letrehozasa-es-fejlodese"></a>

## Karakter létrehozása és fejlődése

A karaktergenerálás négy elsődleges képességre egy véletlen méretű pontkészletet oszt el. Az alap 25 pont 15% eséllyel nem kap bónuszt, 50% eséllyel +1, 25% eséllyel +2, 10% eséllyel pedig +3 ponttal nő, így a tényleges készlet 25–28 pont. Mindegyik érték legalább 1 és legfeljebb 10 a dobás során. Ehhez adódnak hozzá a faj módosítói, majd a végeredmény 1 és 13 közé szorul. A kézi generálás azokat a dobásokat, amelyek a kiválasztott fajjal egyetlen osztály CSV-s minimumát sem teljesítik, megjelenítés nélkül automatikusan újradobja. A képességdobás képernyő már az elfogadás előtt felsorolja az eredményhez választható osztályokat, és az osztályválasztó ugyanezt az előre kiszámított listát használja. Ugyanez a pontkészletdobás érvényes a kézi, gyorsindításos és véletlen NPC-karaktergenerálásra.

A karakternév 1–13 karakter hosszú lehet. A korábbi mentésekből érkező hosszabb nevek betöltéskor 13 karakterre rövidülnek, hogy a karakterlap rögzített fejlécébe illeszkedjenek.

Minden `LiveCharacter` tartós `ConsoleColor` tulajdonsággal rendelkezik. Kézi karaktergeneráláskor a játékos egy jól látható színpalettáról választ; gyorsindításkor és fejlesztői társgeneráláskor a szín véletlen. Régi mentéseknél az alapértelmezés cián.

Az `game-data.csv` `#Karakternevek` szekciója osztályonként 20 `CharacterNameDefinition` rekordot tartalmaz. A gyorsindítás az elkészült karakter tényleges osztályának névkészletéből választ, és előnyben részesíti a karakterlistában még nem használt neveket. Ha egy osztály mind a 20 neve foglalt, az ismétlődés megengedett. A definíciók nem játékos karakterek későbbi elnevezésére is újrahasználhatók.

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

Minden elért új szinthez külön HP- és – mannát használó osztálynál – mannadobás tartozik. A dobás zárt tartományát az `game-data.csv` `#Szintlépés életerő növekedés`, illetve `#Szintlépés manna növekedés` szekciója adja meg az Egészség és az Intelligencia alapján. A növekmény egyszerre emeli a maximális és az aktuális erőforrást, tehát a szintlépés részleges feltöltést is jelent. Több egyszerre elért szint minden bónusza külön kisorsolódik és összeadódik.

### Tehetségek

Minden osztályhoz hat `PerkDefinition` tartozik az `game-data.csv` `#Tehetségek` szekciójában. A mezők: stabil azonosító, név, leírás, osztályazonosító és fokozat. A hat tehetség három egymást kizáró párt alkot; fokozatonként pontosan két definíció szükséges.

A választási szintek rögzítettek, nincs ±2 szintes ablak vagy 40%-os aktiválási dobás:

| Fokozat | Alapeset | Alkalmazkodó faj |
|---:|---:|---:|
| 1 | 5. szint | 3. szint |
| 2 | 15. szint | 13. szint |
| 3 | 25. szint | 25. szint |

A [PerkProgressionRules](../Domain/Characters/PerkDefinition.cs) adja a küszöböket.
A játékos a pár egyik tagját választja, az NPC automatikus választást kap.
A megszerzett azonosító és az egyszeri bónusz alkalmazásának állapota mentődik.
Több szint egyszerre történő elérése több fejlődési választást eredményezhet.

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

Az állapotok az `game-data.csv` `#Állapotok` szekciójának `StatusDefinition` rekordjai. A CSV állapotonként tárolja az emojit, időtartamot, körsebzést, támadó- és kezdeményezésbüntetést, maximum-erőforrás és regeneráció százalékokat, csatakezdő veszteségeket és a nulla szükségletszint szorzóját. Hibás sebzéstartomány, százalék vagy üres emoji betöltési hibát okoz.

| Állapot | Aktív hatás |
|---|---|
| 🍖 Éhes | −2 fizikai sebzés és 75%-os HP-gyógyulás; nulla élelemnél csatakezdéskor a maximális HP 5%-a elveszik |
| 💧 Szomjas | −3 kezdeményezés, −1 találati próba és csatakezdéskor 5% maximálismanna-vesztés; nulla víznél minden büntetés kétszeres |
| ☠️ Mérgezés | saját támadási kör végén 1d4 közvetlen sebzés, hat aktiválódás után elmúlik |
| 🤒 Betegség | a maximális HP és manna 80%-os, minden HP-/mannavisszatöltés 50%-os; nem jár le magától |
| 🩸 Vérzés | saját támadási kör végén 1d3 közvetlen sebzés, négy aktiválódás után elmúlik |

Az Éhes és Szomjas állapot származtatott: 30 vagy alacsonyabb élelem-, illetve vízszintnél automatikusan aktív, magasabb értéknél megszűnik. A többi állapot hátralévő aktiválódásszámmal együtt mentődik. Az ismételt mérgezés vagy vérzés nem halmozódik, hanem visszaállítja az állapot teljes CSV-s időtartamát. Az ellenméreg, gyógyfüves orvosság és kötés továbbra is azonnal eltávolítja a megfelelő állapotot. A karakterlap az állapotok neve helyett a CSV-s emojikat mutatja.

<a id="jatekhurok"></a>

## Játékhurok

A `Game.Run` a [Game.World.cs](../Application/Game.World.cs) fájlban körülbelül 20 ms-os
várakozással ismétlődő, egyszálú koordinációs ciklus. A bemenet és a session-parancsok feldolgozása
után lépteti az esedékes szimulációs eseményeket, frissíti a megjelenítést és publikálja a coop-állapotot.
Ez nem rögzített 50 Hz-es fizikai időlépés: az ütemezés `DateTime.UtcNow` alapú határidőket használ.

### Időmodell és a világóra jelzése

Nincs naptár vagy kijelzett napszak. A karakterlap fejlécében azonban explicit időjelző látható:
felfedezéskor három másodpercenként váltakozó `⌛` / `⏳`, szünetkor `⌛⏸`.
A host által számított `ExplorationClockIndicator` a session-snapshot része; a vendég nem külön órát futtat.

| Rendszer | Mikor lép előre? | Ütem / mértékegység |
|---|---|---|
| Ellenfél- és NPC-mozgás | Aktív felfedezés | Példányonkénti határidő, sebesség és terep alapján |
| Mérgezés, vérzés, aktív karakter- és ellenfélvarázshatások | Aktív felfedezés, állva is | Közös **30 másodperces felfedezési kör** |
| Élelem és víz | Aktív felfedezés | **60 másodperc** |
| NPC-önellátás és panaszellenőrzés | Aktív felfedezés | **1 másodperc** |
| Szétszóródás | Aktív felfedezés | **10 másodperc**, szünettel meghosszabbítva |
| Harci sorrend, varázshatások és lehűlések | Taktikai harc | Harci körök és résztvevőakciók |
| Becsapódás és lövedék animációja | Renderelési idővonal | Ezredmásodperc; nem a varázshatás időtartama |

A mozgás nem rövidíti a buffok élettartamát. A korábbi „tíz sikeres lépés = egy akció” szabály
már nem aktív; a lépésszámláló csak kompatibilitási mező. Harcban a körléptetés védi a
varázshatásokat attól, hogy az extra támadások ugyanazon körben többször fogyasszák őket.
Mérgezés és vérzés felfedezésben is sebezhet, és karakterhalált okozhat.

### Szünetek és nem blokkoló nézetek

| Helyzet | Felfedezési idő |
|---|---|
| Térképen állás vagy mozgás | Telik |
| Oldalsó karakterlap/inventory, tárgymozgatás, naplógörgetés | Telik; az inventory önmagában nem blokkoló ablak |
| Varázsbecsapódás animációja | Telik; az effekt nem birtokolja a játékhurkot |
| Súgó, bestiárium, régiótérkép, beállítások, küldetésnapló, részletes karakterablak | Szünet a személyes ablakkezelőn keresztül |
| Coopban regisztrált varázslatinformációs ablak | Közös szünet; a helyi oldalsó lap nem minden útvonalon regisztrál ilyen ablakot |
| Közös döntési/modális ablak | A `RunHostWindow` `Paused` fázist állít be |
| Taktikai harc | A felfedezési idő áll, a harci állapotgép működik |
| Fogadó, játék vége | A normál felfedezési szimuláció nem fut |

A [PlayerWindowKindRules](../Application/SessionContracts.cs) szerint az `Inventory` kivételével
minden felsorolt személyes ablak blokkoló. Coopban bármely játékos ilyen ablaka megállítja a közös
felfedezést; a többiek állapotsávból látják az okot. Csak az utolsó ablak bezárása után történik folytatás;
a megszakadt kapcsolathoz tartozó ablakok törlődnek.

A [ShiftExplorationSchedules](../Application/Game.BattleInputAndSpells.cs) a szünet idejével eltolja
a szükséglet-, állapot-, NPC-önellátási, mozgási, panasz-, beszélgetés- és szétszóródási határidőket.
Ezzel a kezelt ablakszünet nem válik bezáráskor felgyűlt események sorozatává.
Az egyedi, közvetlen `Paused` hívásokat külön kell ellenőrizni: a fázisjelzés önmagában nem
egyenértékű minden határidő megőrzésével.

Az ellenfelek járhatósági és foglaltsági szabályok szerint mozognak. A találkozás taktikai csatát
indít; nem szükséges hozzá, hogy két élő szereplő ténylegesen ugyanazt a cellát foglalja el.

### Szörnymozgási profilok

Minden szörny a pályageneráláskor egyszer kap mozgási profilt és járőrirányt. A profil a szörny teljes pályabeli életére megmarad, és a teljes játékmentés része:

- `Stationary`: normál helyzetben egy helyben áll;
- `Wander`: minden mozgási időpontban véletlen szomszédos irányt választ;
- `Patrol`: egyenes vonalban halad, akadálynál megfordul, majd az ellenkező irányba folytatja útját.

Ha egy találkozás nem ír elő mozgást, a szobában generált szörny 80% eséllyel helyben áll; a fennmaradó 20% egyenlően oszlik meg a kóborló és járőr profil között. Folyosón a helyben állás esélye 10%, a maradék 90% fele-fele arányban kóborló vagy járőr. A jelenlegi szobai csoportkonfigurációk kifejezetten `Stationary` profilt kérnek, ezért ezek tagjai és vezérei észlelés előtt együtt, helyben várakoznak.

Profiltól függetlenül a szörny üldözni kezdi a látótávján és tiszta látóvonalban észlelt legközelebbi élő partitagot. Azonos távolságnál véletlen a kezdeti választás, de a már üldözött, továbbra is érzékelhető célpont elsőbbséget élvez, ezért az ellenfél nem váltogat indokolatlanul a partitagok között. Egy csoport tagjai megosztják egymással az észlelt célpont helyét. Az alvó és félálomban lévő tagok csak rövid reakciókésés után indulnak el.

Az `Ellenségek` CSV-fejezet `Nyomérzék` oszlopa 0–8 közötti útvonaltávolságként szabályozza a látást kiegészítő érzékelést. Ez fajtól függően szaglást, hallást, rezgésérzékelést vagy természetfeletti hatodik érzéket jelent; például a farkasok nyomot szagolnak, az élőholtak az élők jelenlétét érzik. A nyomérzék járható kapcsolatot igényel, ezért lezárt falon vagy ajtón át nem teszi mindentudóvá az ellenfelet. A látás vagy nyomérzék által megtalált célpont helyét a falka megosztja.

Az üldözők járható útvonalon, zárt ajtókat és foglalt mezőket kerülve közelítenek; partitársba ütközve vele kezdenek csatát. Ha sem látással, sem nyomérzékkel nem találják a célpontot, 8–12 saját mozgásig emlékeznek rá: előbb az utolsó ismert mezőhöz mennek, majd továbbhaladnak a célpont utoljára megfigyelt irányába. Egy átmenetileg elállt út csak három egymást követő sikertelen közelítés után szakítja meg az üldözést.

Nyomvesztéskor a falka közös keresést kezd az utolsó ismert hely körül. Legfeljebb három tag esetén a leggyorsabb egy, nagyobb falkánál a leggyorsabb két tag lesz felderítő; a többiek kétmezős biztosítógyűrűbe húzódnak. A felderítők legfeljebb hat mezőre távolodnak a közös keresési ponttól, nyilvántartják a már bejárt mezőket, és előbb a még felderítetlen folyosókat és elágazásokat járják végig. Csak a 30–120 saját keresési alkalomból álló közös keresés lejárta után térnek vissza fokozatosan az eredeti helyükhöz.

Az üldözési és keresési állapot, a célpont, a hátralévő memória, az utolsó ismert hely és irány, az egymást követő útkeresési hibák, a közös keresési pont és a felderített mezők mentéskor megmaradnak. Régi mentésből hiányzó profil alapértéke a korábbi működést megőrző `Wander`; a hiányzó csoportazonosító magányos ellenfelet jelent.

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

A periódusos fogyás külön-külön lefut a parti minden élő tagjára és az ideiglenes követőkre, az adott karakter saját maximális és aktuális HP-ja alapján. A halott társak és követők szükségletei már nem változnak.

A gépi vezérlésű partitag és követő a percenkénti szükségletcsökkenés, illetve saját csata után a normál Éhes vagy Szomjas állapotnál véletlenül 1–3 megfelelő adagot próbál elfogyasztani a saját hátizsákjából. Minden adag előtt újraértékeli a hiányt, és kihagyja azt a tárgyat, amely 100 fölött 15 pontnál több élelmet vagy vizet pazarolna. Sérüléskor ugyanígy 1–3 gyógyitalt használhat, de csak legfeljebb 15 HP túltöltéssel; ezt és a panaszidőzítőket a játék másodpercenként egyszer ellenőrzi, nem minden NPC-mozgásnál és főciklus-iterációban. A fogyasztás, az elfogyott készlet és a nulla szükségletszint a közös naplóba kerül. Nulla élelem/víz, illetve fél HP alatti állapot és gyógyital hiánya esetén karakterenként és problémánként véletlen 2–3 percenként ismétlődő panasz jelenik meg; a probléma megszűnése törli az időzítőt.

Az ideiglenes követő varázshasználó saját magát is felveszi az automatikus gyógyítás lehetséges célpontjai közé. Harcban 35%, felfedezéskor 50% HP alatt választható, és a valódi party tagjaival együtt mindig a legalacsonyabb HP-arányú elérhető célpontot gyógyítja. A szokásos memorizálási, hatótáv-, mannaköltség- és 20%-os mannatartalék-szabályok változatlanok; harcban 10% HP alatti vészhelyzet felülírhatja a tartalékot.

A taktikai csata lezárásakor a résztvevő karakterek költsége a lejátszott körökből származik,
nem `1d5 + szörnyerősség` dobásból:

```text
élelemvesztés = vízvesztés = max(1, lejátszott harci körök)
```

A konkrét lezárási útvonal választja ki az érintett résztvevőket. A
[PartySustenanceService](../Application/PartySustenanceService.cs) alkalmazza a költséget
és szinkronizálja az Éhes/Szomjas állapotot. A nehéz terep külön terhelést is gyűjt:
tíz terhelési pont egy további élelem- és két vízpontot fogyaszt.

A szintléptető fejlesztői gyorsbillentyű `Ctrl+Alt+S`; ugyanazt a fejlődési útvonalat használja,
mint a normál XP-jóváírás.

<a id="parti"></a>

## Parti

A `Party` 1–4 egyedi `LiveCharacter` objektumot tartalmaz. Első tagja az aktív karakter és a csapat
vezetője. Társak a világ NPC-iből, fogadói toborzással, coop-belépéssel vagy fejlesztői eszközökkel
csatlakozhatnak. A valódi partitagok a központi karakterlistában is szerepelnek; az ideiglenes követők
külön térképi résztvevők, nem foglalnak partihelyet. A session stabil `CharacterId` alapján kezeli
a tulajdonjogot; a mentés visszaépítésében rosterhivatkozások is szerepelnek.

A vezető és a társak ugyanabban a taktikai összecsapásban is részt vehetnek. A harci XP a
60/40-es parti szabály szerint minden élő taghoz eljuthat; minden részesülő a saját osztályának
fejlődési szabályait használja. A közös arany a vezetőn tárolódik. A periódusos szükségletfogyás
minden élő tagra és követőre lefut, a csata utáni költség a résztvevőkhöz kötött.

A vezető és a társak térképi jele az osztály magyar nevének nagy kezdőbetűje: `H`, `B`, `L`, `T`, `P` vagy `M`. A jel a karakter saját színével rajzolódik. A társak minden új pályán szélességi kereséssel a vezetőhöz legközelebbi üres, járható cellákra kerülnek. Foglalják a mezőjüket az ellenfelek és a vezető elől; egymásra vagy szörnyre nem lépnek és zárt ajtón nem haladnak át. A szörny- és partitárstetemek viszont nem blokkolják sem az NPC-k sem a szörnyek útkeresését vagy lépését; az élő szereplő ideiglenesen eltakarja a tetem jelét, amely a mező elhagyásakor újra láthatóvá válik.

Az NPC-ként vezérelt `LiveCharacter` nullable `NpcBehavior` tulajdonsága mentésre kerül. A vezetőnél inaktív. Generáláskor a barbár mindig `Aggressive`, a lovag mindig `Defensive`, a tolvaj mindig `Scout`, a pap és a mágus mindig `Cautious`; a harcos fele-fele eséllyel `Defensive` vagy `Aggressive`. Régi mentésből származó NPC alapértéke az első elhelyezéskor `Defensive`.

A partitársak mozgása a `Game` egyszálú eseményciklusában fut. Minden avatár saját következő
mozgási időpontot kap; az ütemet a mobilitás, a terep és a felzárkózási helyzet módosítja.
A jelenlegi alap NPC-késleltetés 250–300 ms, a felzárkózási alap 90 ms, az ember által
vezérelt mozgás alapkorlátja 85 ms. A végleges időpontot az ütemező számolja, ezért ezek nem
minden szereplőre és terepre érvényes, rögzített lépésidők.

A játék legfeljebb a vezér utolsó 256 sikeres pozícióját tartja nyilván. A vezetőt követő NPC-k nem annak pillanatnyi X/Y-koordinátája köré választanak célmezőt: feloszlatott alakzatnál is az alakzatszerkesztő BAL ELSŐ → JOBB ELSŐ → BAL HÁTSÓ → JOBB HÁTSÓ slotsorrendje szerint céloznak egymást követő nyompontokat, és szélességi útkereséssel lépnek felé. A vezér saját slotja kimarad a követők sorából, az ideiglenes követők pedig a rendes partitagok után következnek. A speciális előremenő vagy ellenségre reagáló mozgási profil színesítheti ezt a viselkedést, de utána minden társ a saját slotsorrend szerinti nyompontjához tér vissza.

Zárt alakzatban a négy rendes partitag alaphelyzetben 2×2-es blokkban mozog. Normál nyílbillentyűvel a sikeres lépés irányába fordul, `Shift+nyíl` használatakor viszont oldalaz vagy hátrál, és megtartja a nézési irányát. A `Ctrl+bal/jobb` 90 fokos fordulása helyben történik: a blokk ugyanazt a négy mezőt foglalja, a tagok ezen belül rendeződnek át, miközben a karakterlapon beállított slotbeosztás megmarad. Sikertelen, falba vagy foglalt mezőbe futó mozgási kísérlet sem a pozíciót, sem a nézési irányt nem módosítja. Ha a következő lépés blokkban fal miatt nem fér el, de a vezér célmezője és a kifűződéshez használt mezők járhatók, az alakzat `Locked` állapota megmarad és az elrendezés ideiglenesen libasorra vált. A vezér belép a szűkületbe, minden következő tag pedig az előtte haladó előző helyét foglalja el. A sor ezért nem merev egyenes: fokozatosan fűződik ki a szobából és a folyosó kanyarjait is követi. A szűkület utáni első szabad területen a rendszer automatikusan visszaállítja a 2×2-es elrendezést. A libasor nem ad első-/hátsósori harci védelmet, mert a szereplők térben nem ilyen rendben állnak.

Az ideiglenes követő nem foglal alakzati slotot. Zárt alakzat mellett hátsó kísérőpozíciót keres, nem indul önálló felderítésre vagy távoli ellenfél után, és az alakzat lépésekor lehetőség szerint egy szomszédos szabad mezőre kitér. Közvetlenül szomszédos ellenséggel azonban megküzd, így egy szűkületben az alakzat elé szorult követő sem okozhat tartós holtpontot. Ha lemarad, járható útvonalon zárkózik fel; az alakzat feloszlatásakor visszakapja a saját NPC-viselkedését. Harcban továbbra is önálló követő résztvevő, alakzati védelem nélkül.

Térképfókuszban a tartós `H` / `G` / `T` partiparancsok és az ideiglenes `M` szétszóródás
írják felül az automatikus profilokat. A tartós módok egymást kizárják;
részletesen lásd a [Partiparancsok](#partiparancsok) szakaszt.

#### Egyéni NPC-mozgásprofilok

- a defenzív társ legalább két vezérlépéssel korábbi nyompontot követ és így egy üres mezőt hagy közöttük; ötmezős rálátáson belüli szörny felé indul és mellé érve automatikusan megtámadja;
- az agresszív társ az előre eső tágas mezőket keresi és nem lép a vezető előtti szűk folyosóba; ötmezős rálátáson belüli szörny felé indul és mellé érve automatikusan megtámadja;
- a felderítő legfeljebb tíz mezőre halad a vezető előtt; ötmezős rálátáson belüli szörny észlelésekor visszatér a vezér nyomvonalára;
- az óvatos társ legalább két vezérlépéssel korábbi nyompontot követ; ellenség észlelésekor sem indul felé.

A közös látótér az élő karakterek észlelési forrásainak uniója. A látótáv karakterenként eltérhet
kaszt, faj, varázshatás és pályasötétség szerint; nem minden társ használ rögzített ötmezős sugarat.
A [FogOfWar.UpdatePartyVisibility](../World/FogOfWar.cs) az új felfedéseket, aktuális láthatósági
változásokat és észlelési emlékeket is visszaadja a célzott újrarajzoláshoz.

A karakterlap a jobb panel legfelső sorában kezdődik, ezért nem hagy kihasználatlan üres sort a fejléc felett. A faj és osztály alatt a megszerzett tehetségek két sorban jelennek meg; a neveket a renderer a két sor között osztja el és szükség esetén soronként egyenletesen rövidíti. A tizenkét hátizsáksor alatt három sort tart fenn a társaknak. Minden sor a karakter saját színével mutatja az osztály kezdőbetűjét, a nevet, a szintet és az aktuális/maximális HP-t. A vezető nem ismétlődik meg ezekben a sorokban.

A `Tab` vált a térkép- és karakterlapfókusz között. Karakterlapfókuszban a `KARAKTERLAP` cím zöld hátteret kap, a fel/le nyilak pedig minden felszereléshely, varázstárgyhely, hátizsákhely és partitárs között léptetik az aktív kijelölést; az üres helyek is célpontok. A kiválasztott sor DarkCyan hátteret kap, miközben megtartja saját előtérszínét; a renderer a logikai kategória és index alapján karakterenként külön megőrzi az utolsó kijelölést. Lépéskor csak a választható tárgy- és partisorok rajzolódnak újra, a teljes karakterlap és térkép nem.

Kijelölt partitársnál a `Del` megerősítést kér a végleges kirúgáshoz. Jóváhagyáskor a karakter felszerelésével együtt kikerül a rosterből és a partiból; élő avatárja vagy pályán maradt teteme eltűnik a térképről, mozgási időzítője pedig törlődik. Ha az ő karakterlapja volt megnyitva, a panel automatikusan visszavált a partivezérre.

Az `Esc` a térkép- és karakterlapfókuszból is megerősítést kér, mielőtt visszatér a főmenübe, mert a legutóbbi mentés utáni állapot elveszik. A célzó- és varázslatinformációs képernyőkön az `Esc` továbbra is csak az aktuális segédnézetet zárja be.

Karakterlapfókuszban a bal/jobb nyíl körkörösen vált a parti tagjainak, majd az ideiglenes követő NPC-knek a karakterlapja között. A követő inventoryja látható, de nem jelölhető ki és semmilyen inventoryművelettel nem módosítható. A vendég ugyanezt a teljes valódi party és az ideiglenes követők között kapja meg; a host a megjelenítéshez szükséges karakterlap- és inventory-snapshotot publikálja, követőhöz vezérlési jogot viszont nem. A három rögzített társsor mindig ugyanazokat a valódi partitársakat mutatja; az éppen megtekintett társ sora `▶` jelölést kap. A vezető lapjának megtekintésekor nincs nyíl a társsorokban. A megtekintett lap nem változtatja meg a térképen irányított vezetőt.

Az inventory rögzített helyekből áll: **két kéz és egy tartalékfegyver**, egy páncélhely,
három varázstárgyhely és tizenkét hátizsákhely. A kompatibilis tárgyak helyenként legfeljebb
kilences kötegbe vonhatók össze; a töltet- és példányállapotot a mozgatás megőrzi.
A felszereléshelyek csak a saját kategóriájukat fogadják el, a hátizsák bármelyiket.

| Billentyű | Inventoryművelet |
|---|---|
| `Enter` | Használat; a tartalékfegyver helyén fegyvercsere |
| `Space` | Kiemelés és elhelyezés; kompatibilis kötegek összevonása vagy érvényes atomi csere |
| `D` | Eldobás |
| `F` | Hátizsákköteg felezése |
| `S` | Elfogyasztható köteg szétosztása a valódi parti között |
| `I` | Tárgyvizsgálati oldal |

A fókusz elhagyása visszateszi a még kézben tartott tárgyat. A host validálja a slotokat,
kasztkorlátokat, karakterkötést, aktív átkot és inventoryrevíziókat; a kliens nem küld kész eredményt.

A fegyverek és páncélok ritkasága `Normal`, `Magic` vagy `Legendary`, a felületen Sima, Varázs és Legendás néven jelenik meg. A CSV-ben minden kézzel felvett felszerelés külön `Kategória`, opcionális `AlapId` és `MágikusErő` mezőt kap. A mágikus erő már adatként, menthető tárgydefiníció részeként rendelkezésre áll; a jelenlegi csatában a mágikus `+N` felszerelések megnövelt sebzés-/védelmi tartománya aktív, az általános mágikus-erő mechanika a későbbi varázsrendszer bővítési pontja.

A generált `+1/+2/+3` fegyverek és páncélok harci tartományának mindkét széle rendre 1/2/3 ponttal nő. Az eddigi 2×/4×/7× alapárhoz általában fix 500 arany mágikus felár adódik. A kámzsa (`A001`) és bőrvért (`A002`) kivétel: feláruk `MágikusErő × 500`, vagyis +1/+2/+3 fokozaton 500/1000/1500 arany. Egyedül a bunkónak (`W005`) nem készül generált mágikus változata; minden pajzs fejleszthető.

A `#Tárgybővítések` szekció határozza meg a név-utótagot, harci bónuszt, árszorzót, mágikus erőt és tartósságbónuszt. Betöltéskor minden Sima fegyverből és páncélból automatikusan létrejön a három Varázs változat. A `+1`, `+2`, `+3` bónusz a sebzés- vagy védelmi tartomány mindkét végére rákerül, a maximális tartósságot pedig rendre 15/30/50%-kal növeli; az ár az alapár 2×, 4× és 7× értékéből számolódik. Így új alapfelszerelés vagy új bővítési fokozat hozzáadásához nem kell C# kódot módosítani.

A frissen létrehozott felszerelészsákmány megmaradt tartóssága a `#Zsákmány paraméterek` között beállított 25–100%-os tartományból sorsolódik, de sosem érkezik törötten; a törhetetlen tárgyakat ez nem érinti. A kereskedő az ép állapotra kisorsolt visszavásárlási árat a megmaradt tartósság arányával szorozza, legalább egy aranyat ajánlva. A `T028` javítókészlet egy kiválasztott, vagy célpont nélkül a legrosszabb állapotú felszerelésen 50 pontot javít, de a terepi javítás 75%-os állapotnál megáll; a közvetlen használat felfedezésben és fogadóban engedélyezett, harcban nem. A kereskedő fix készletében két, a jelen lévő Kovácsmester és Páncélmíves kínálatában egy-egy készlet szerepel. A Fegyvermester tehetség minden fegyverkopási eseményt, a Páncélmester pedig minden páncél- és pajzskopási eseményt egy ponttal csökkent.

A CSV ezen felül húsz egyedi nevű Legendás fegyvert és húsz Legendás páncélt tartalmaz. Ezek nem generált átnevezések: külön sebzésük/védelmük, kasztengedélyük, alapfelszerelés-hivatkozásuk, mágikus erejük, leírásuk és áruk van. A katalógus és a mentés már kezeli őket; későbbi pályatárgy-generálás közvetlenül ezekből a definíciókból válogathat majd.

### Varázstárgyak

A véletlen világzsákmányként megszerzett `Magic` ritkaságú fegyver, páncél és varázstárgy konkrét példányazonosítót és `IsIdentified = false` állapotot kap. A fogadóban vásárolt, kezdő-, küldetés- és karakterhez kötött Legendás felszerelés azonosított. Az állapot a tárggyal együtt mozog az inventoryslotok, karakterek és földi tárgyhalmok között, bekerül a karakter- és játékmentésbe, a régi mentésekből hiányzó állapot pedig kompatibilitásból azonosított példányt jelent.

Az azonosítatlan snapshot nem továbbítja a katalógusazonosítót, valódi nevet, leírást, árat, mágikus erőt vagy töltetet. Helyettük kategóriaalapú ismeretlen név és a mágikus erőből képzett gyenge/közepes/erős/rendkívüli aura látható. Ismeretlen pálca és tekercs nem kerül a varázsválasztóba; a passzív vagy felszerelési tárgy hatása azonosítás nélkül is működik. Friss világzsákmánynál a parti legmagasabb effektív Intelligenciájú élő Mágusa egyszer automatikusan azonosítási próbát tesz: az esély `clamp(25 + Intelligencia × 5 − mágikus erő × 10, 5, 95)%`. A próba a példányállapot létrehozásakor történik, ezért a földre dobás, újrafelvétel vagy karakterek közötti átadás nem ad új próbát. A Vándormágus garantált azonosításának ára `20 + ceil(alapár × 0,08) + mágikus erő × 15`. Azonosítatlan tárgy kereskedői ajánlata az alapár 25%-a, a coop kliens pedig csak ezt az ajánlatot és az álcázott példányt kapja meg.

A `#Tárgyátkok` szekció az átokazonosítót, nevet, hatástípust, értéket, 1–3-as erősséget, kompatibilis tárgykategóriákat és a leíró szövegeket tárolja. A normál világzsákmányként dobott `Magic` tárgy 8%, a 10. szintű Elátkozott sírkamrákban 30% eséllyel kap egy kompatibilis átkot; a dobás egyszer történik és a példányállapottal mentődik. A fogadói, kezdő-, küldetés-, karakterhez kötött és Legendás tárgyak nem kapnak véletlen átkot.

Az átok hátizsákban és tartalékfegyver-helyen nyugalomban marad. Aktív fegyver-, páncél- vagy varázstárgyhelyre kerülve aktiválódik, rögzíti a viselő `CharacterId`-ját, és az inventory validáció megtiltja az eltávolítását, cseréjét vagy más karakternek adását. Az aktiválódás azonosítás nélkül is látható, de az átokazonosító és a pontos hatás csak azonosítva kerül a snapshotba. A nyolc implementált hatás: `HitPenalty`, `DefensePenalty`, `InitiativePenalty`, `MovementPenalty`, `CombatWeight`, `ManaCost`, `HealthPenalty`, `IntelligencePenalty`; az aktív értékek azonos típuson belül összeadódnak.

Az `IsPurified` példányállapot véglegesen hatástalanítja az átkot, törli az aktiválást és a karakterkötést, de auditálhatóság és mentéskompatibilitás miatt megőrzi az eredeti átok metaadatait. A papi `Átoktörés` (`BreakItemCurse`) egy célzott karakter legerősebb aktív tárgyátkát tisztítja meg; azonos erősségnél a stabil felszerelési slotsorrend dönt. A Vándormágus az egész parti bármely azonosított, még aktív vagy nyugalomban lévő átkát garantáltan eltávolítja. A szolgáltatás díja `50 + ceil(alapár × 0,12) + mágikus erő × 25 + átokerősség × 100` arany.

A `#Varázstárgyak` szekcióban nincs Sima ritkaság: minden definíció legalább Varázs, az egyedi ereklyék Legendás kategóriájúak. A `MagicItemDefinition` mezői: altípus, ritkaság, alapár, maximális töltet, opcionális varázslat-ID, passzív hatás és érték, kasztengedély, jellemzés és mágikus erő. Négy altípus létezik: `Ring`, `Amulet`, `Wand`, `Scroll`.

A katalógus 57 varázstárgyat tartalmaz: 12 gyűrűt, 12 amulettet, 9 pálcát és 24 tekercset. A gyűrűk között pontosan öt, az amulettek között szintén öt egyedi Legendás darab van. A gyűrűk és amulettek felszerelve összeadódó passzív csatabónuszt adhatnak:

- `Initiative`: hozzáadódik a kezdeményezéshez;
- `Hit`: hozzáadódik minden fegyveres találati próbához;
- `Damage`: hozzáadódik a sikeres fegyveres támadás sebzéséhez;
- `Defense`: hozzáadódik az ellenfél támadásakor számított védelemhez;
- `BattleHeal`: minden csata kezdetén legfeljebb a maximumig HP-t tölt;
- `BattleMana`: minden csata kezdetén legfeljebb a maximumig mannát tölt.

A bónusz csak akkor él, ha a tárgy valamelyik varázstárgyhelyen van; hátizsákból nem hat. A felszerelési ellenőrzés a varázstárgy kasztengedélyét is ugyanabban az atomi inventory-ellenőrzésben vizsgálja, mint a fegyvereket és páncélokat.

A `Strength`, `Dexterity`, `Health` és `Intelligence` passzív varázstárgyhatások rendre az Erő, Ügyesség, Egészség és Intelligencia effektív értékét növelik. Több felszerelt tárgy azonos képességre adott bónusza összeadódik, de az effektív érték legfeljebb 13 lehet. Az alapérték nem módosul, ezért a bónusz levételkor megszűnik és mentéskor sem válik véglegessé. A karakterlap és a coop snapshot az effektív értéket mutatja; a harci, ajtó-, keresési és varázslási próbák szintén ezt használják. A `+1` képességgyűrűk ára 1200, a `+2` képességamuletteké 3000 arany, és mindegyiket bármely kaszt viselheti.

A tekercs pontosan egy töltetű. A [SpellcastingRules.CanUseCastingItem](../Domain/Characters/SpellcastingRules.cs)
szerint mágusiskolájú tekercset Mágus, papi tekercset Pap, Lovag **vagy Mágus** használhat;
a felszerelésnek a tárgy saját CSV-s kasztengedélyét is teljesítenie kell.
A Harcos, Barbár és Tolvaj nem használ tekercset. Pálcát kaszttól és varázsiskolától függetlenül
bárki használhat. A betöltő ellenőrzi a varázslathivatkozást és a töltetszámot; új varázslathoz
nem keletkezik automatikusan új pálca vagy tekercs.

A `SpellId` határozza meg a varázstárgyhoz kötött varázslatot. Ha használható tekercs vagy pálca van a karakter három varázstárgyhelyének egyikén, a `V` varázsválasztóban külön `📜 0M`, illetve `🪄 0M` sor jelenik meg. Ezekhez nem kell fókusztárgy, ismertség, memorizálás vagy manna. Varázshasználónál az eszközös varázslat a memorizált lista mellett jelenik meg; azonos varázslat esetén külön normál és eszközös sor választható. A célzás `Esc` megszakításakor semmi nem fogy. A célpont megerősítése után a tekercs eltűnik, a pálca aktuális töltete eggyel csökken; a harci koncentrációs kudarc is fogyaszt. A töltet az inventorymozgatáskor és karakterek közötti átadáskor a tárggyal mozog, a karaktermentés pedig slotonként tárolja. A 0 töltetű pálca megmarad, de nem jelenik meg a varázslistában. A gyűrűk és amulettek passzív hatásai továbbra is teljesen működnek.

#### Varázstárgyak rövid összefoglalója

| Típus | Működés | Elfogyás |
|---|---|---|
| Gyűrű, amulett | Felszerelve passzív tulajdonság-, harci vagy ellenállásbónusz | Nincs töltetfogyás |
| Pálca | Egy kötött varázslat, manna és kasztfókusz nélkül | Elsütésenként egy töltet; az üres tárgy megmarad |
| Tekercs | Egy kötött varázslat, iskola- és kasztkorlátokkal | Egy használat után eltűnik |

Mindegyik a három varázstárgyhely egyikét használja; hátizsákból sem passzív hatás,
sem pálca-/tekercsvarázslás nem működik. Azonos passzív bónuszok összeadódhatnak.
A használó effektív tulajdonságai számítanak a varázslat feloldásakor.

A célzás megszakítása nem fogyaszt töltetet. A megerősített használat koncentrációs kudarc esetén
is elhasználja a töltetet és az akciót. Az Isteni ítélet normál papi varázslási ciklusa nem
aktiválódik tárgyból; az Időmegállítás csatánkénti korlátja viszont arra is érvényes.
Mozgatás, földre dobás, karakterek közötti átadás, mentés és reconnect megőrzi a töltetállapotot.

### Varázslatdefiníciók és szintek

A `SpellDefinition` stabil azonosítót, nevet, `Arcane` vagy `Divine` iskolát, 1–5 közötti varázslatszintet, pozitív alap-mannaköltséget, leírást és célzási metaadatokat tartalmaz. Az `game-data.csv` `#Varázslatok` és `#Papi varázslatok` szekcióinak oszlopai: `Id`, `Név`, `Szint`, `Manna`, `Leírás`, `Célzás`, `Hatótáv`, `Terület`, `Látóvonal`, `HasználatiMód`, `BecsapódásSzín`, `BecsapódásIdőMs`, `CsakEllenség`, `BecsapódásMinta`, `ViharMinta`, `ViharSzín`, `NemHatÉlőholtra`, `NaplóEmoji`. A célzás típusa `Self`, `Party`, `PartyMember`, `Enemy`, `Corpse`, `Cell`, `Area` vagy `Direction`; a használati mód `Exploration`, `Combat` vagy `Both`. Mindkét iskola mannaköltsége és leírása a tényleges CSV-s hatásokhoz van hangolva.

A `D001`–`D017` sötét készlet meglévő hatástípusokra épülő, ellenség-only támadó, kontrolláló, védő és gyógyító varázslatokat tartalmaz. A caster profilok erősség és szerep szerint kapnak belőlük; a játékosoldali listázás, tanulás, memorizálás, tárgyvalidáció és végrehajtás egymástól függetlenül is kizárja ezeket.

Az alábbi táblázat az eredeti varázslatkészlet áttekintése, **nem a teljes aktuális katalógus**.
A készlet azóta látótáv-, fegyverbűvölési, ellenállási és más hatásokkal bővült;
a teljes név- és hatáslista a CSV-ben található.

| Szint | Mágusvarázslatok | Papi varázslatok |
|---:|---|---|
| 1 | Mágikus lövedék; Fagyasztó érintés; Égő kéz; Lángoló nyíl | Gyógyító érintés; Szent fény; Áldás; Méregűzés |
| 2 | Villámcsapás; Láthatatlanság; Arkán páncél; Lassítás | Szent pajzs; Gyógyítás; Védelem a gonosztól; Betegségűzés |
| 3 | Tűzgolyó; Jégvihar; Teleportáció; Mágia szétoszlatása | Szent csapás; Isteni védelem; Megtisztítás; Bátorság imája |
| 4 | Villámvihar; Meteorzápor; Láncvillám; Kőbőr | Feltámasztás; Szent ítélet; Őrangyal; Tömeges gyógyítás |
| 5 | Időmegállítás; Dezintegráció; Dimenziókapu; Arkán kataklizma | Isteni csoda; Isteni harag; Szentély; Igazi feltámasztás |

A CSV-betöltő visszautasítja az 1–5 tartományon kívüli szintet, az ismeretlen célzás- vagy
használatimód-nevet és a negatív hatótávot/területet. Iskolánként legalább **28** definíciót,
az 1–3. szinten legalább hatot, a 4–5. szinten legalább ötöt vár; ez minimum, nem felső korlát.
A `GameDataCatalog.GetSpell` azonosító szerint, a `GetSpells(school, level)` iskola és szint szerint keres.

Az összetett működést a `#Varázshatások` szekció írja le. Egy varázslathoz több, sorrendben
végrehajtott `SpellEffectDefinition` tartozhat. A sor konfigurálja a hatástípust, kockát,
Intelligencia- és szintszorzót, állandó értéket, körökben számolt időtartamot, esélyt,
`Auto`/`Attack`/`SaveHalf`/`SaveNegates` feloldást és opcionális paramétert.
A betöltő ellenőrzi az ID-ket, kockakifejezéseket, tartományokat és a varázslatok hatáshivatkozásait.

A mágikus támadás `d20 + Intelligencia + tárgyi találati bónusz` a szörny `11 + effektív Gyorsaság` értéke ellen; az Arkán fókusz további +2-t ad, a természetes 20 kritikus. Az ellenpróba célszáma `10 + floor(Intelligencia / 2) + varázslatszint`; siker esetén a `SaveHalf` felezi a sebzést, a `SaveNegates` teljesen kivédi a mellékhatást. Az Elemi mester a kiszámolt sebzést 25%-kal növeli.

Az implementált mágushatások lefedik az egycélpontos és területi sebzést, a kétmezős iránykúpot, égést és viharsebzést, sebességcsökkentést, minden második akció kihagyását, láthatatlanságot, arkán páncélt, kőbőrt és vérzésvédelmet, láncoló sebzést, varázshatás-szétoszlatást, ön- és partiteleportációt, csatánként egyszeri két extra akciót, kivégzési küszöböt és véletlen elemi mellékhatást. Az időzített hatások a karakter- és pályamentés részei; a fogadóban az élő karakterekről törlődnek.

Az implementált papi hatások gyógyítanak, Mérgezést/Betegséget/Vérzést tisztítanak, valamint találatot, fizikai sebzést, kezdeményezést, védelmet és sebzéscsökkentést adnak. A Szent fény, Szent csapás, Szent ítélet és Isteni harag élőholt (`MA001`) vagy démoni (`MA010`) célpont ellen 50%-kal nagyobbat sebez. A Védelem a gonosztól kizárólag ilyen támadó ellen ad +4 védelmet, 30% sebzéscsökkentést és mérgezés-/betegségvédelmet. Az Őrangyal az első halálos ellenséges csapást 1 HP-n kivédi és utólag gyógyít. A Szentély a varázslás pillanatában három mezőn belüli élő tagokra kerül; 50% sebzéscsökkentést és súlyosállapot-védelmet ad, de az adott karakter első fegyveres vagy támadó varázsakciójánál megszűnik.

### Aktív karakterbuffok és ikonjaik

A karakterlap `Áll:` sora a hagyományos állapotok mellett a pozitív varázshatásokat is emojival jelzi.
Az időtartam **körökben** fogy: felfedezéskor a közös 30 másodperces állapotkörben,
harcban a résztvevő körléptetésénél. A lépések száma már nem számít.
A mentéskompatibilitás miatt a `RemainingActions` JSON-mezőnév megmaradt, de a kód
`RemainingRounds` néven használja. Az Isteni ítélet az alap-időtartamot megkétszerezheti.

Az alábbi ikonlista válogatás; az aktuális értékekhez a CSV hatássorai az irányadók.

| Ikon | Aktív hatás | Forrás, érték és alap-időtartam (kör) |
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

Varázslatgyűjteménye a Papnak (`C005`, `Divine`), Mágusnak (`C006`, `Arcane`) és
Lovagnak (`C003`, `Divine`) is van. A [SpellcastingRules](../Domain/Characters/SpellcastingRules.cs)
külön kezeli a kezdőkészletet, tanulást, szintfeloldást és memóriakapacitást. A karakter tárolja:

- az ismert varázslatok tartós varázskönyvét;
- az ismert varázslatokból pihenéskor összeállított, aktuálisan memorizált készletet.
- nyolc, mentett gyorshelyet, amelyek kizárólag memorizált varázslatra mutathatnak.

A normál varázsláshoz kasztfókusz tartozik: a Mágus személyes `Varázskönyvet`, a Pap és Lovag
személyes `Szent szimbólumot` használ. Ez a hátizsák első helyén, karakterhez kötve marad;
nem mozgatható, dobható el, adható el vagy vásárolható meg. A régi `M003` és `M004`
kezdőtárgyazonosítók kompatibilitási adatként maradtak meg, nem az új normál varázslás fókuszai.

Karakterlapfókuszban a fókusztárgyon nyomott `Enter` a jobb oldali karakterpanel helyén nyitja meg a varázslatinformációs oldalt. Ez felsorolja az ismert varázslatokat, külön jelöli a memorizáltakat és az `F1–F8` gyorshelyet, megmutatja a memória kapacitását, a kijelölt varázslat szintjét, mannaköltségét, célzástípusát és leírását, továbbá az 1–5. varázslatszint karakter-szintküszöbeit és a következő feloldást. A fel/le nyilak böngésznek, az `F1–F8` a kijelölt memorizált varázslatot rendeli a gyorshelyhez, az `Enter` a partivezér memorizált varázslatát indítja, az `Esc` pedig bezárja az oldalt.

A memóriakapacitás effektív Intelligenciából, egész osztással számolódik:

| Kaszt | Kapacitás | Kezdővarázslat | Tanulási szintek | Legmagasabb varázslatszint |
|---|---|---:|---|---|
| Mágus | `2 + Int/3 + szint/5` | 3 | Minden szint | 1/2/3/4/5 az 1/5/10/15/20. szinten |
| Pap | `2 + Int/4 + szint/5` | 3 | Minden szint | 1/2/3/4/5 az 1/5/10/15/20. szinten |
| Lovag | `min(4, 1 + Int/5 + szint/10)` | 0 | 2., 5., majd 8., 11., 14., … | 1; a 8. karakterszinttől 2 |

Ugyanaz a varázslat nem foglalhat több helyet. Kézi generáláskor a Pap és Mágus három
első szintű varázslatot választ; automatikus generáláskor a rendszer választja őket.
Emberi karakter fejlődési döntése a tulajdonosához kerül, az NPC automatikusan választ.
Több elért szint külön tanulási alkalmakat jelenthet. Az ismert/memorizált ID-k és a nyolc
gyorshely mentődik; memorizáláskor az érvényes kézi gyorshelykiosztás megmarad.

### Varázslás és célzás

A partivezér a térképen `V`-vel nyitja meg a memorizált varázslatok színes választóképernyőjét, az `F1–F8` billentyűkkel pedig közvetlenül indítja a megfelelő gyorshelyet. A keskeny, legfeljebb tizenkét varázslatsort egyszerre mutató felugró panel a térkép közepére rajzolódik; előtte eltárolja a lefedett térképcellák rúnáját és színeit, bezárásakor pedig kizárólag ezeket állítja vissza. Így sem teljes konzoltörlés, sem teljes térkép- vagy karakterlapfrissítés nem történik. A választó megmutatja a szintet, mannaköltséget és célponttípust. Entitás-, mező-, terület- és iránycélzásnál az egy konzolcella széles `╳` célkereszt jelenik meg: a nyilak mozgatják, a `Tab` a CSV-s szabályoknak megfelelő érvényes célpontok között léptet, az `Enter` megerősít, az `Esc` megszakít. Érvényes célhoz a hatótáv, a már felfedett mező és szükség esetén a látóvonal is teljesüljön. Az önmagára és az egész partira ható varázslatok nem nyitnak célkeresztet.

Aktiváláskor az effektív mannaköltség vonódik le; ezt tehetség és tárgyátok is módosíthatja.
Csatán kívül nincs koncentrációs kudarc. Harcban a varázslat teljes akció; koncentrációs
kudarc csak **lekötött** varázslónál lehetséges:

```text
nem lekötött: 0%
lekötött: clamp(clamp(30 - Intelligencia - Ügyesség, 0, 100) + 15 - fókuszcsökkentés, 0, 100)%
```

Operatív botnál a Jártas fok 5, a Mester fok 10 százalékpont fókuszcsökkentést ad.

Kudarc esetén a manna/töltet és az akció elvész. Sikerre a CSV hatásai oldódnak fel;
a napló célpontonként mutatja a tényleges HP-veszteséget, ellenállásokat és maradék HP-t.
A folyamatos karakter- és ellenfélvarázshatások csatán kívül a közös 30 másodperces
állapotkörben lépnek, nem minden ellenfélmozgásnál. Az aktív viharterületek külön
belépési és pulzusütemezést használnak.

### Pihenés a labirintusban

A `P` billentyűvel pályánként pontosan egyszer lehet pihenni. A pihenés csak akkor indul el, ha a vezető egy szoba belsejében áll, minden élő partitag ugyanabban a szobában van, nincs bent élő ellenfél, a szobának van ajtaja, és minden hozzá tartozó ajtó `Locked` állapotú. A felhasznált pihenési lehetőség a teljes játékmentés része.

Pihenéskor minden élő partitag 1d10 HP-t gyógyul a normál gyógyulásmódosítókkal, a mannája az aktuális maximumra töltődik, továbbá 10 élelem- és 10 vízpontot fogyaszt. A betegségre, mérgezésre és vérzésre egymástól függetlenül `30 + Egészség × 2` százalék eséllyel történik gyógyulási próba; siker esetén az adott állapot megszűnik. Ezután a Papok és Mágusok újra összeállíthatják memorizált készletüket. A pihenés végén a szoba ajtajai `Closed` állapotba kerülnek, és újraindulnak a szükséglet-, szörny- és partitárs-időzítők.

A fogadói pihenés nyolc órás, szintfüggő szobadíját a közös aranyból fizetjük, megerősítés után. A pályavégi fogadó egyszeri pihenést enged. Erdei fogadóban az utolsó fogadói pihenés végétől 16 órának kell eltelnie; a tábori pihenés ezt az időpontot nem írja felül. Az erdei szobák drágábbak, mindkét fogadótípus szobaára véletlen eltérést kap. Lakomázáskor egy játékóra telik el; erdei fogadóban a parti naponta egyszer lakomázhat. A részletes díjak és mentési szabályok: [ForestSuppliesAndInns.md](ForestSuppliesAndInns.md).

A `#Fegyverek` és `#Páncélok` CSV-szekció kasztoszlopai határozzák meg, mely osztályok viselhetik az adott tárgyat. A fegyvereknél a Harcos, Barbár és Lovag, a páncéloknál a Harcos és Lovag alapértelmezetten engedélyezett; a többi kaszt engedélyét az `igen` érték adja. Minden fegyvernek 1–13 közötti `MinimumErő` értéke is van, és csak legalább ekkora Erővel szerelhető fel. A mágikus fejlesztések öröklik az alapfegyver követelményét. A korlátozás csak a felszereléshelyekre vonatkozik, hátizsákban bármely karakter hordozhat bármilyen tárgyat. Az ellenőrzés központilag a `LiveCharacter` végleges, tervezett inventoryállapotán fut, ezért a kézi mozgatásra és cserére, a kezdőfelszerelésre, a mentés betöltésére és a véletlen NPC-felszerelésre is érvényes.

A kétkezes fegyver kizárólag az első fegyverhelyen viselhető. Amíg ott kétkezes fegyver van, a második fegyverhelynek üresnek kell lennie és a karakterlapon `⛔` lezárásként jelenik meg. Kétkezes fegyver csak üres második hely mellett szerelhető fel; a második hely pedig nem tölthető fel, amíg az elsőben kétkezes fegyver marad. A hátizsákban ez a korlátozás sem érvényes. Minden kétkezes fegyver páncéltörő: találatkor az ellenfél teljes, képességbónuszokkal növelt páncéljának felét figyelmen kívül hagyja lefelé kerekítve. A sebzésből ezért `ceil(páncél / 2)` vonódik le; a csatanapló az eredeti és a tényleges páncélértéket is mutatja.

Az `I` a kijelölt tárgy adatait a jobb oldali **tárgyvizsgálati oldalon** mutatja,
nem hosszú naplóbejegyzésként. A [ItemInspectionFormatter](../UI/ItemInspectionFormatter.cs)
az ismert példányadatokat, fegyver-/páncélstatisztikát, kasztkorlátot, ritkaságot,
mágikus erőt, tartósságot, árat és CSV-jellemzést formázza.
Azonosítatlan tárgynál a rejtett adatok nem jelennek meg.
A vizsgálati oldal saját bemeneti ága csak bezárást kezel: `Esc`, `I` vagy `Enter`;
onnan nem használható, dobható vagy mozgatható tárgy.

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

<a id="labirintusgeneralas"></a>

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

<a id="palyavege-es-fogado"></a>

## Pályavége és fogadó

A kijárat elérésekor a játék még a pályaszám növelése előtt lezárja az aktuális labirintusszintet. Az egy karakternek járó teljesítési jutalom:

```text
teljesítési XP = BaseLevelCompletionExperience × teljesített pályaszám
```

Ezt az összeget minden életben maradt partitag külön és teljes egészében megkapja; itt nem érvényes a harci 60/40-es XP-elosztás. Minden túlélő karakter saját osztálymódosítója és szintlépési HP-/mannadobása dolgozza fel a jutalmat. A vezető szintlépése a megszokott tehetségválasztási folyamatot is elindíthatja. A halott társak nem kapnak teljesítési XP-t.

Jutalmazás után a parti a fogadóban pihen: kizárólag a túlélők aktuális HP-ja és mannája töltődik maximumra. A 0 HP-s társ halott marad; a pálya végén kikerül a partiból és a karakter-nyilvántartásból, tehát végleg elveszik. A középre igazított színes pályavége képernyő megmutatja a képletet és összeget, karakterenként az XP-t, szintváltozást és feltöltött erőforrásokat, továbbá külön megemlékezik az elvesztett társakról. Enter vagy Space nyitja meg a fogadó kereskedőjét; a piacról `Esc` a toborzáshoz vezet. A toborzás után minden túlélő Pap és Mágus memorizálhat, így az újonnan csatlakozott zsoldos is felkészíthető. Ezután következnek a pletykák, végül `Enter` vagy `Esc` a következő pályára visz.

### Fogadói kereskedés

Minden `IItemDefinition` pozitív `BasePrice` alapárral rendelkezik, amely közvetlenül az `game-data.csv` megfelelő sorából származik. Hiányzó, nulla vagy negatív ár betöltési hibát okoz. Az árskála az egyszerű ellátmány néhány aranyas tartományától az alapfegyvereken és vérteken át a több tízezer aranyas legendás felszerelésekig terjed; a legerősebb legendás gyűrűk és amulettek szintén ritka és drága fogadói ajánlatok.

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

A vezérnél alacsonyabb szintű zsoldos ingyen csatlakozik. Azonos vagy magasabb szinten az alap felbérlési díj `zsoldos szintje × 100` arany, amelyre fogadólátogatásonként egyszer kisorsolt 50–150%-os szorzó kerül. Az ajánlati ár a toborzóképernyő használata közben nem változik. Az erdei fogadók visszalátogatáskor is megőrzik a várakozó zsoldosokat és áraikat; hat játékóránként egy normál jelölt cserélődik, illetve egy felvett jelölt helyére új érkezhet. Ha nincs elég arany, a felvétel meghiúsul, és teljes parti esetén a régi társ kiválasztása és elvesztése sem történik meg.

A zsoldos célpontszintje a partyvezér aktuális szintje körüli zárt ±3 tartományból készül, a játékadatokban elérhető szintekre szorítva. A karakter a szintlépés normál HP-/mannadobásait és a szintjéhez illő véletlen tehetségeket kapja. Alacsony szinten az osztály CSV-s kezdőfelszerelését viseli; a szint emelkedésével növekvő eséllyel annak nem legendás, mágikus továbbfejlesztéseit kaphatja meg. Hátizsákjában pontosan 1–3 véletlen használati tárgy van, például étel, ital, gyógyital, varázsital, ellenméreg, orvosság vagy kötés.

Szabad partihely esetén az `Enter` azonnal felveszi a kijelölt zsoldost. Négyfős partinál előbb ki kell választani a lecserélendő, nem vezető társat. A lecserélt karakter kikerül a partiból és a központi karakter-nyilvántartásból, ezért végleg elveszik; a csere képernyője `Esc`-pel következmény nélkül megszakítható.

### Fogadói pletykák

A toborzás után a fogadós egy véletlen pletykát mutat. Az `N` billentyűvel legfeljebb három alkalommal kérhető új pletyka; az ajánlatok nem kerülnek aranyba. A kezdő pletykával együtt így egy fogadólátogatás során legfeljebb négy információ olvasható. A rendszer lehetőség szerint nem ismétli meg ugyanazt a teljes pletykaszöveget. `Enter` vagy `Esc` lezárja a fogadót és elindítja a következő pályát.

A pletykáknak két típusa van:

- **úti pletyka:** a következő szint nevét, szobaszámát és -méretét, folyosójellegét, falstílusát, összes konfigurált ellenféltípusát és csoportvezéreit ismerteti;
- **szörnypletyka:** a teljesített szint előtti, aktuális vagy következő szint találkozásaiból választ egy ellenfelet, majd kiírja a térképjelét, erősségét, HP-ját, Erejét, Páncélját, Gyorsaságát, XP-jutalmát, számított mozgási periódusát, továbbá minden képességének nevét, aktiválási esélyét, értékét és CSV-s leírását.

A pletyka mindig az aktuális `MazeLevelConfigurations` és `GameDataCatalog` adataiból készül, ezért a pályák vagy ellenfelek későbbi hangolása automatikusan megjelenik benne; nincs külön, könnyen elavuló kézzel írt pletykaadatbázis.

A rejtett `Ctrl+Shift+E` fejlesztői gyorsbillentyű a partyvezért a kijárat melletti, járható és objektumtól mentes mezők közül a hozzá legközelebbire teleportálja. A teleport frissíti a vezér útvonalát és a látómezőt is; ha nincs megfelelő szabad mező, csak naplóüzenet jelenik meg.

A kampány 1–22. szintjét a `MazeLevelConfigurations` konfigurálja; az általános generálási
segédszabályok nem helyettesítik a kampány konkrét boss-, NPC-, csapda- és küldetéstartalmát.
A **6. szint a Tiltott Erdő**: többképernyős erdei gráfot használ, és engedélyezett JSON-felülírást
tölthet a `ForestLevelGraphs/level-6.json` állományból. A korábbi 6–21. szint 7–22-re tolódott.
A találkozások stabil ellenfél-ID-ket használnak.

Az alábbi táblázat a kampány elejének áttekintése; a pontos generálási paraméterek a kódban vannak.

| Szint | Téma | Fal | Szín | Dupla folyosó esélye | Fő ellenfelek |
|---:|---|:---:|---|---:|---|
| 1 | Patkányjáratok | `█` | sötétszürke | 95% | patkányok, koboldok, goblinok |
| 2 | Patkányvezér | `█` | sötétszürke | 40% | óriáspatkányok, koboldok, csontváz, patkányember vezér |
| 3 | Goblinüregek | `▓` | sötétzöld | 75% | koboldok, goblinok, farkasok |
| 4 | Vadállatok odúi | `▒` | sötétsárga | 70% | goblinok, csontvázak, zombik, ork vezér |
| 5 | A holtak katakombái | `▓` | sötétszürke | 82% | csontvázak, zombik, ghoul vezér |
| 6 | Tiltott Erdő | tereppaletta | terepenként | ösvénykonfiguráció | erdei találkozások, NPC-k és küldetések |
| 7 | A nagy csarnokok szintje | `▦` | sötétsárga | 20% | orkok, hobgoblinok, ogre vezér |
| 8 | A mérgező barlang | `▒` | sötétcián | 88% | pókok, nyálkák, gyíkok, baziliszkuszok |
| 9 | Az ork haditábor | `▓` | sötétpiros | 78% | orkok, hobgoblinok, bugbearek, sámánok |
| 10 | Az elátkozott sírkamrák | `▦` | sötétmagenta | 92% | múmiák, ghoulok, wightok, éji banyák |
| 11 | Az óriások erődje | `▩` | szürke | 12% | ogrék, trollok, ettinek, fagyóriás |
| 12 | A sárkánykultusz szentélye | `▥` | piros | 80% | wyvernek, kimérák, ork sámánok, vörös sárkány |

A `MazeLevelConfiguration` a fal egyetlen konzolcellás `Rune` karakterét, `ConsoleColor` színét és a pálya megjelenített nevét is tartalmazza. Ezek bekerülnek a `MazeGenerationSettings` és a futásidejű `Maze` objektumba. A járhatóság és látóvonal az adott példány `WallRune` értékét használja, nem egy rögzített `█` karaktert; a renderer az adott pálya falszínével rajzol. A fal karaktere, színe és pályanév a teljes játékmentés része, a régi mentések pedig `█`, sötétszürke és „Labirintus” alapértékkel tölthetők be.

A `DoubleWidthCorridorChance` a legtöbb pályán 0,7–0,95 között marad. Tematikus kivétel a nagy csarnokok 0,20-as és az óriások erődjének 0,12-es értéke: ezekben ritkábbak a két cella széles összeköttetések, miközben a szobák jóval nagyobbak és számosabbak.

A kampány zárópályája a **22. szint**, „A Káoszrubin rejtekhelye”;
a **21. szinten** Kael-Zhur, a Káoszsárkány őrzi a tizenkettedik kulcsot.
A finálé kijárata csak az összes kulcs birtokában aktiválható, és fogadó/új generálás helyett
a XV., befejező fejezetet indítja. A finálé méltatja az életben maradt partitagokat és lezárja a futamot.
A végső sorszám egyetlen kódbeli forrása a `MazeLevelConfigurations.FinalLevel`.

<a id="szornyek"></a>

## Szörnyek erőssége és képességei

Az `game-data.csv` `#Ellenségek` szekciója tetszőleges számú, `|` jellel elválasztott `KépességIds` értéket és külön `Jellemzők` mezőt tárol. A jelenlegi jellemzők az `Undead`, `Demonic` és `Flying`; ezek nem foglalnak képességhelyet. A betöltő hibát jelez tartományon kívüli erősségnél, ismeretlen képességnél vagy jellemzőnél. Az erősség nem módosítja automatikusan a statisztikákat: a HP, Erő, Páncél, Gyorsaság és XP továbbra is külön hangolható.

A térképi szörnyrúnák erősség szerinti színe:

| Erősség | Szín |
|---:|---|
| 1 | zöld |
| 2 | sárga |
| 3 | sötétsárga |
| 4 | piros |
| 5 | magenta |

A `MonsterAbilityDefinition` metaadatai a `#Szörnyképességek`, sorrendezett hatásai pedig a `#Szörnyképesség-hatások` szekcióból érkeznek. A fejléc az aktiválási pont (`Passive`, `OnHit`, `TurnStart`, `Active`) mellett külön végrehajtási módot (`WeaponAttack`, `AbilityAttack`, `SavingThrow`, `Automatic`), célzást, lehűlést, hatótávot, célpontszámot, AI-súlyt, opcionális fegyverszűrőt, csatánkénti használati korlátot, előkészítési időt, támadásszámot, képességcsoportot és használat utáni hátrálást tartalmaz. A `HasználatCsatánként` nulla értéke korlátlan használatot jelent.

Minden hatássor saját típust, fix értéket vagy dobástartományt, állapotazonosítót, sebzéstípust, esélyt, ellenállási tulajdonságot és nehézséget, valamint időtartamot hordozhat. A sorok a képességazonosító és a pozitív `Sorrend` alapján kapcsolódnak a fejlécükhöz; ismeretlen fejléc, hiányzó hatás vagy ismétlődő sorrend betöltési hiba. A képesség globális aktiválása után minden rész-hatás külön esélyt és — ha be van állítva — külön ellenállási próbát kap, ezért egy összetett képesség sebzése érvényesülhet akkor is, ha a célpont a hozzá tartozó állapotot kivédi. Az ellenállás `d20 + effektív tulajdonság >= Nehézség` esetén sikeres. A `Dobás` az adott hatás fix értékét váltja ki, az állapothatás pozitív `Időtartam` értéke pedig felülírja az állapot alapértelmezett időtartamát. A passzív hatások nem lehetnek véletlenszerűek vagy időzítettek; a betöltő a további ellentmondó beállításokat is elutasítja.

Az `Előkészítés` értéke megadja, hány akción át jelzi előre a szörny a képességet; ezalatt nem támad és nem varázsol, a megingás pedig megszakítja a készülődést. A `Támadásszám` minden lövéshez külön találati és hatásdobást végez, de a töltet és a lehűlés csak egyszer fogy. Többszörös támadás jelenleg kizárólag egy célpontra és támadódobásos képességre állítható. Az azonos `Képességcsoport` értékű képességek közös lehűlést kapnak, így például a Vén beholder nem süthet el két külön szemsugarat egymást követő akciókban. Az előkészített képesség és a hátralévő készülődési idő a 29-es játékmentés része. Jelenlegi hatások:

- `Poison`, `Disease`, `Bleeding`: sikeres szörnytámadás után a CSV-s eséllyel hozzáadja a Mérgezés, Betegség vagy Vérzés karakterállapotot;
- `ExtraDamage`: sikeres találatkor a megadott eséllyel hozzáadja a konfigurált extra sebzést;
- `InitiativeBonus`: állandóan hozzáadódik a szörny kezdeményezéséhez;
- `ArmorBonus`: állandóan hozzáadódik a szörny páncéljához;
- `Regeneration`: minden saját kör elején a megadott HP-t visszatölti;
- `ApplyStatus`: találatkor vagy aktív képességként a CSV-ben hivatkozott állapotot alkalmazza.

Az Élőholt és Démoni jellemzőt a szent sebzés, a gonosz elleni védelem és az élőholtűzés használja. A régi `MA001`, `MA009` és `MA010` azonosítók mentés- és küldetés-kompatibilitási álnevek maradtak. A Repülő jellemző +1 taktikai mozgást ad; harcban az útkeresés más aktív harcolók és az erdei fák lombkoronája fölött is átvezetheti a repülőt, de falon, épületfalon vagy vízen nem. A repülő csak szabad, járható mezőn szállhat le.

A Medúza Dermesztő tekintete 21-es nehézségű Egészség-próba ellenében két saját akcióra Kődermedtséget okoz. A baziliszkuszok és beholderek Bénító sugara továbbra is Kődermedtséget és nekrotikus sebzést okozhat, csatánként kétszer használható. A Lich, Drakolich, Balor és Vén beholder több célpontot érintő Rémületkeltést használhat, amely 5–7 nekrotikus sebzést is okoz és csatánként egyszer süthető el. Mindegyik aktív képesség a CSV-ben beállított hatótávval, eséllyel, célpontszámmal, AI-súllyal, lehűléssel és használati korláttal működik. Többcélpontos használatkor a töltet és a lehűlés egyszer fogy el. A betöltő ellenőrzi a hivatkozott állapotokat, fegyvereket és az aktív képességek pozitív lehűlését.

A Goblin íjász, Csontváz íjász és Ork íjász közös, két külön találati dobást végző Dupla lövést kapott. Ez az íjász saját különleges lövésével közös lehűlési csoportban van. Az Ork íjász Megakasztó lövése egy akció előkészítést igényel, így a játékos előre látja a veszélyes nehéz nyilat és megingással megszakíthatja.

Az `OnHit` képességek `FegyverIds` mezője meghatározza, mely támadások válthatják ki a hatást; üres mező esetén bármely fegyver megfelel. Így például a wyvern fullánkja mérgezhet, a harapása azonban nem. Az MA002, MA003 és MA004 fegyverlistái a méreg, betegség és vérzés tényleges forrásaira vannak szűkítve.

A többcélpontos, nem fizikai leheletfegyvereket a szörny egy teljes akcióval előkészíti, és ezt a harci napló előre jelzi. A következő saját körben elsüti a leheletet, majd három körös lehűlés kezdődik. Több elérhető célpontnál az AI előnyben részesíti a leheletet, egy célpontnál többnyire a normál fegyverei közül választ. Az alkalmi támadások nem használhatnak leheletet. A képesség- és fegyverlehűlések, valamint az előkészített fegyver szörnypéldányhoz kötöttek, a 17-es játékmentés részei. A megmaradt csatánkénti képességtölteteket a 18-as játékmentés őrzi meg.

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

A hordható fegyverrel harcoló humanoid ellenfelek saját fegyverére külön, jelenleg 30%-os keresési esély vonatkozik; ezt a `SajátFegyverEsély` zsákmányparaméter szabályozza, és ugyanúgy módosítja a kereső Intelligenciája, faja és Tolvaj osztálya. A fegyvert választó példány pontosan a generáláskor kiválasztott fegyvert hordja, ezért például az ököllel érkező Zombi nem dob bunkót. A fegyvert nem választó, de normál és természetes támadást vegyesen használó ellenfél minden hordható fegyvere jelölt lehet. Természetes és más szörnykizárólagos fegyver nem zsákmányolható. Sikertelen sajátfegyver-dobás után a régi általános felszerelésdobás még megtörténhet, de a két dobás együtt is legfeljebb egy véletlen felszerelést eredményez.

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

Ajtó mellett minden ember által vezérelt karakter az `N` billentyűvel nyit, a `Z` billentyűvel bezár, a `K` billentyűvel kulcsra zár. A művelet mindig az adott karakter saját pozícióját használja. A `K` helyzetfüggő: ha a karakter tetemen vagy földi tárgyhalmon áll, előbb a keresés/felvétel történik, ezért ilyenkor nem kezeli a szomszédos ajtót. A simán zárt ajtó szabadon nyitható. Kulcsra zárt ajtónál a nyitási sorrend:

1. a `T003` kulcs garantáltan nyit és eltűnik a hátizsákból; tolvajnál előtte térképre rajzolt modális ablak kérdezi meg, hogy valóban felhasználja-e;
2. kulcs nélkül, illetve a kulcs használatának elutasításakor a tolvaj százalékos Ügyesség-próbát tesz;
3. sikertelen zárnyitás vagy más osztály esetén `1d20 ≤ Erő` próba következik, amely siker esetén végleg bezúzza az ajtót.

A tolvaj kulcsválasztó ablaka a varázslás ablakához hasonlóan csak az alatta levő térképcellákat menti el és állítja vissza. `I`, `Y` vagy `Enter` használja a kulcsot; `N` vagy `Esc` megtartja és a zárnyitást választja. A kulcs nélküli nyitás egyetlen `N` lenyomásra egy próbának számít akkor is, ha a sikertelen tolvajpróbát rögtön erőpróba követi. A próba egymástól függetlenül 1–2 élelmet és 1–2 vizet fogyaszt; a minimumok és maximumok a `#Ajtópróba paraméterek` szekcióból hangolhatók. Kulccsal történő nyitás és a simán zárt ajtó kinyitása nem fogyaszt szükségletet.

Ha nem tolvaj partyvezér nyitna kulcsra zárt ajtót, a játék legfeljebb két mező Chebyshev-távolságon belül megkeresi a legnagyobb Ügyességű élő NPC tolvajt. A segítő a saját kulcsát használhatja a kulcsválasztó ablakban, vagy a saját Ügyességével tesz zárnyitási próbát; a szükségletköltséget is ő fizeti. Sikertelen próbája után egy második, térképre rajzolt ablakban a játékos dönt arról, hogy a vezér megpróbálja-e Erőből bezúzni az ajtót. Elutasításkor az ajtó zárva marad, és nem történik automatikus erőpróba.

A tolvaj zárnyitási esélye 10 Ügyességnél 90%, 11-nél 93%, 12-nél 96%, 13-nál 100%;
alacsonyabb értéknél fokozatosan csökken. Kulcsra záráshoz elfogyó kulcs vagy Tolvaj kaszt szükséges.
A napló a műveletet, dobást és szükségletköltséget mutatja.
A műveletet kezdeményező emberi karakter saját pozíciója és jogosultsága számít, nem mindig a vezetőé.
Küldetésajtóhoz külön, típusos questhozzáférés tartozhat; egy lezárt questkaput a normál kulcs/erőpróba
nem helyettesít. Lásd [QuestDoorAccessService](../Application/Quests/QuestDoorAccessService.cs).

<a id="latomezo-es-kod"></a>

## Látómező és köd

### Felfedezettség, aktuális látótér és észlelés

A [FogOfWar](../World/FogOfWar.cs) három külön fogalmat kezel:

| Fogalom | API / állapot | Jelentés |
|---|---|---|
| Tartós térképismeret | `IsRevealed`, `_revealed` | Egyszer megismert terep; később is kirajzolható |
| Aktuális látótér | `IsCurrentlyVisible`, `_currentlyVisible` | Az élő parti észlelési forrásainak jelenlegi uniója |
| Ellenfél észlelése | `IsEnemyVisible` | Látótér és lopakodás/észlelés, illetve explicit harci láthatóság |

Egy megismert mező önmagában nem teszi mindig láthatóvá az ott mozgó ellenséget.
A fejlesztői `Ctrl+Shift+U` nem írja át a tartós felfedezettséget, és az ellenfélészlelés
nem pusztán a fejlesztői felfedésből következik.

### Látótáv és geometria

A természetes karakterlátótáv alapja 5, Tolvajnak +2, Éles érzékek faji tulajdonságnál +1,
legfeljebb 8. Erre kerül a `VisionBonus` varázshatás és a pálya `VisionModifier` értéke;
a végső érték 1–10. Fényforrások és más fényhatások további környezeti megjelenítést is adhatnak.

A konzolcellák oldalarányát a látótáv **2:1 vízszintes korrekcióval** kezeli:

```text
látótávolság = max(ceil(abs(dx) / 2), abs(dy))
```

Így az ötös látótáv legfeljebb tíz konzolcellára nyúlik vízszintesen, öt cellára függőlegesen.
Ez nem azonos a harci `TacticalDistance` metrikával, amely a két komponenst összeadja.

A sugár Bresenham-jellegű egyenes, nem útkeresés. A
[Maze.BlocksSight](../World/Maze.cs) ajtóállapot és regisztrált `MazeTerrainStyle.BlocksSight`
szerint dönt; nem minden nem járható terep takarja a látást. Fa, fenyő és sűrű bozót takarhat,
víz és átlátható bokor nem. Élő karakter és ellenség nem látásblokkoló.

A célcellát a látóvonal elérhetőnek tekinti még akkor is, ha az maga takaró fal/ajtó;
a mögötte lévő cellák már nem láthatók. A tökéletes átló továbbra sem vizsgálja mindkét
oldalsó sarokcellát: nincs supercover-szabály, két összeérő sarokfal között átcsúszhat a sugár.
Blokkoláskor nem keres kerülőutat.

### Lopakodás, hang és rövid emlékezet

- Az ellenség rejtőzködése a CSV `Stealth` értékéből és a terep `ConcealmentBonus` értékéből áll;
  a karakter `DetectionBonus` értéke csökkenti. Észlelhetően aktív ellenségnél a lopakodási levonás nulla.
- A vizuális észlelési sugár `max(1, látótáv - effektív rejtőzködés)`.
- A hallótáv alapja 4; Tolvaj és Éles érzékek külön-külön +2-t ad. A szörny `Noise` értékét
  a terep zajmódosítója alakítja. Hangészleléshez nem szükséges közvetlen látóvonal.
- Az eltűnt, de életben maradt ellenségről az utolsó látott hely három partimozgásig marad meg.
  A hangjel két partimozgásig él, és csak közelítő pozíciót jelez, nem pontos vizuális célpontot.
- A world snapshot a ténylegesen észlelt/harci ellenfeleket és külön a korlátozott emlékeket küldi;
  nem minden felfedett cellán álló ellenség teljes állapotát.

### Terepfelfedési kiegészítések

A legfeljebb háromcellás, két felfedezett végpont közötti vízszintes/függőleges ködrés kitölthető,
ha valamelyik végpont az aktuális felfedéshez kapcsolódik. Ez térképismeret, nem új látóvonal.
Az erdő közvetlenül látott szegélyéből a konfigurált összefüggő lombkorona kis, szabálytalan
mélységben tovább rajzolódhat; ez **nem** teszi láthatóvá a mögötte lévő lényeket vagy tárgyakat.

### Varázslatok lővonala és hatásterülete

| Támadás | Takarás szabálya |
|---|---|
| Egycélpontos | A célvalidáció a hatótávot és a CSV szerinti `RequiresLineOfSight` értéket ellenőrzi |
| Területi | A célmező validációján túl a robbanás középpontjából minden érintett cellára külön látóvonal kell |
| Irány/tölcsér | A varázslótól számított alakzat és cellánkénti látóvonal metszete |
| Láncsebzés | Az első célponthoz tartozó középpontból legfeljebb négy, négycellás környezetben lévő cél; a további célokhoz is látóvonal kell |
| Meteorzápor | Három kisorsolt becsapódási középpont az érvényes területen; középen 100%, szomszédokon 60%, átfedésnél összeadás, takarásellenőrzéssel |

A [SpellAreaFootprint](../Domain/Magic/SpellAreaFootprint.cs) kizárja a takaró terepcellákat
és a fal mögötti cellákat. Ugyanezt a területet használja a sebzés és az effekt;
a területi sebzés baráti tüzet is okozhat. A lánc nem falon át ugráló, kerülőutat kereső villám:
a [ApplyChainDamage](../Domain/Magic/SpellExecutionService.cs) az első cél körüli jelölteket szűri.

<a id="csata-algoritmusa"></a>

## Csata algoritmusa

A jelenlegi csata **több résztvevős taktikai összecsapás**, nem két fél felváltva lefutó párbaja.
A [Game.StartBattle](../Application/Game.Combat.cs) létrehozza a `BattleEncounter` objektumot;
a [TacticalBattleState](../Combat/TacticalBattleFoundations.cs) tartja a résztvevőket és a sorrendet.

### Résztvevők és körök

1. A kezdeményező karakter és ellenség az első körtől aktív.
2. Az élő parti és ideiglenes követői felkészítést kapnak; a többi karakter a második körtől léphet be.
3. A csata környezetében lévő, járható útvonalon időben elérő ellenfelek is résztvevők lehetnek;
   a rendszer későbbi erősítést is kezel.
4. A résztvevők kezdeményezési sorrendben kapnak mozgást és akciót. A lekötés,
   alakzatvédelem, hátbatámadás, megingás és előkészített képesség az aktuális térbeli állapotra épül.
5. Emberi akciónál a játék visszatér a fő ciklusba; az aktuális karakter tulajdonosának
   érvényes `BattleId` / `TurnId` parancsára vár. NPC-nél az AI választ.

Host és vendég fegyveres támadást, varázslatot/célzást, jogosult kasztakciót és további
engedélyezett taktikai műveleteket is használhat. A prompt whitelistje az irányadó,
nem egy változatlan billentyűlista. A tartalékfegyver cseréje teljes akció.
Az NPC-k is varázsolhatnak, gyógyíthatnak és felszerelést válthatnak; nem csak fizikai támadást futtatnak.

A felfedezés szünetel, de a harci állapotgép és a renderelési idővonal tovább működik.
Van feltételes visszavonulás, biztonságos célmezők keresésével és lekötött ellenség reakciótámadásával.
Öt inaktív teljes kör után a patthelyzet lezárható. A gyorsharc ugyanennek az összecsapásnak
automatizált megjelenítési/végrehajtási módja, nem külön sebzésszabály.

A részletes vezéri csatanapló csak a ténylegesen érvényesülő nem nulla tehetségbónuszokat írja ki. A nulla gyógyítás/mannatöltés és a nulla támadó- vagy védelmi tehetségérték nem foglal helyet a naplóban.

A leader csatájában minden megjelenített harci esemény után részlegesen frissül a karakterlap állapot-, HP- és mannasora. Távoli harcnál a host konzolja megfigyelőként mutatja a naplót és azt, hogy melyik vendég akciójára vár. A naplóesemények nem állítják meg külön Space-várakozással a sessiont; az állapotgép a következő emberi döntésnél vár. A többi karakterlapsor és a térkép nem rajzolódik újra, így a kör közben változó állapotok és erőforrások azonnal láthatók maradnak fölösleges teljes képernyős frissítés nélkül.

A defenzív/agresszív NPC felfedezéskor kezdeményezhet találkozást; más profil is harcba kerülhet,
ha egy ellenség eléri. Az elesett társ 0 HP-val a partiban marad és `PartyMemberCorpse` képviseli
a térképen. Ez ugyanazt a `LiveCharacter` példányt őrzi, ezért a már működő feltámasztás
visszaállíthatja. Távoli karakter halálakor a session feloldja a vezérlését.
A kijáraton történő lezáráskor a nem feltámasztott társ végleg elveszhet.

Az NPC-varázslás saját taktikai tervet és célpontértékelést használ
(`NpcSpellPlanningPolicy`, `NpcSpellPlanEvaluator`, `NpcSpellcastingPolicy`). Figyelembe veszi
a sérülést, állapotokat, mannatartalékot, hatótávot és az ellenfeleket; a tervet végrehajtás előtt
újra validálja. Emberi karakter nem kap helyette automatikus támogató akciót.

Az `Enemy.CurrentHitPoints` a szörny futásidejű HP-ja. A `BattleSystem` ebből indítja a harcot és ide írja vissza a maradékot ezért egy NPC-t legyőző sérült szörny nem gyógyul vissza a következő találkozás előtt.

### Kezdeményezés

Mindkét fél egyszer dob egy előjeles `1d2` módosítót: a dobás `-1`, `-2`, `+1` vagy `+2`, az előjel és a nagyság külön véletlen választás eredménye.

```text
játékos kezdeményezése  = mobilitásból számolt alap + bónuszok - állapotbüntetés + előjeles 1d2
ellenfél kezdeményezése = Gyorsaság + képességbónusz + előjeles 1d2
```

Az első kör kezdeményezőpárja külön nyitósorrendet kaphat; rajtaütés ezt módosíthatja.
A teljes sorrend később kezdeményezés szerint rendeződik, stabil azonosítóval oldva a döntetlent.
A kör elején a dinamikus módosítók változása átvezethető.
Az Első csapás +10 nyitó-, de csak +2 normál kezdeményezést ad;
fegyverjártasság, diszciplína, felszerelési súly és hatások is számítanak.

### Találati próba

Minden támadásnál új `1d20` dobás készül.

```text
támadóérték = 1d20 + támadó sebességi képessége + bónuszok - állapotbüntetés
célérték     = 11 + védekező sebességi képessége
találat      = támadóérték >= célérték
```

A játékos sebességi képessége az Ügyesség, az ellenfélé a Gyorsaság. A Harcos, Barbár és Lovag fegyveres támadásához az Erő további találati bónuszt ad: 7–9 Erőnél +1, 10–12-nél +2, 13-nál +3. A szabály nem keménykódolt kasztlista: az `game-data.csv` `#Erő találati bónusz` szekciója osztályonként külön `MinimumErő` és `Bónusz` küszöbsorokat tárol, így a jogosult osztályok és a görbe külön-külön hangolhatók. Mindig a karakter Erőértékét nem meghaladó legmagasabb küszöb érvényesül; a napló csak a tényleges, nem nulla `Erő-találat` bónuszt mutatja.

Sikertelen próba esetén nincs sebzés. A természetes 20 automatikus kritikus találat.
A kritikus tartományt a Halálos pontosság, fegyverjártasság és a mágikus fegyver is bővítheti:
a +2 mágikus fegyver +5%, a legalább +3-as +10% kritikus esélybónuszt ad.
A részletes számítás a [BattleSystem](../Combat/BattleSystem.cs) felelőssége.

### Kritikus találat

Az általános kritikus találat kétszeres nyers sebzést okoz. A Halálos pontosság ehelyett háromszoros kritikus szorzót ad, tehát a két kritikus szabály nem szorzódik össze. Az Orvtámadás ettől külön támadási szorzó, ezért kritikussal együtt is érvényesülhet. A szorzás a páncél és más védelmi levonások előtt történik.

Az ellenfél aktiválódott `ExtraDamage` képessége a nyers sebzés része, ezért kritikus találatnál szintén duplázódik. A találat után felkerülő állapotok, a méregkeverő külön `1d6` sebzése, valamint a mérgezés és vérzés időszakos sebzése nem része a kritikus szorzásnak. A természetes 20-as ellenféltámadást a tolvaj Kitérés tehetsége nem háríthatja el. Minden extra vagy ismételt támadás külön találati dobást végez, ezért külön-külön lehet kritikus.

### Karakter sebzése

A rendszer operatív kézfegyvert használ; a tartalék nem támad és a törött felszerelés nem teljes értékű.
A kétfegyveres második csapás külön képzettségi feltételekhez kötött. Fegyvertelenül az alapsebzés `1d2`.

- `WT002` fegyvernél a sebzésképesség az Ügyesség;
- minden más támadófegyvernél az Erő;
- a fegyver alapsebzése a CSV-ben megadott zárt tartományból dobódik;
- ezen felül `0..2` véletlen sebzés jár.

```text
képességbónusz = max(0, (képesség - 1) / 2)  (egész osztás)
nyers sebzés   = fegyversebzés + képességbónusz + 0..2 + támadóbónuszok
végső sebzés   = max(1, nyers sebzés × támadási szorzók × kritikus szorzó - ellenfél páncélja - állapotbüntetés)
```

### Ellenfél sebzése

Találat esetén az ellenfél sebzése:

```text
nyers sebzés = a tényleges támadófegyver dobása + erőbónusz + támadó- és képességmódosítók
védelem      = páncél tartományából dobott érték
              + az első felszerelt védelmi fegyver/pajzs tartományából dobott érték
végső sebzés = max(1, nyers sebzés × kritikus szorzó - védelem)
```

Ha nincs páncél vagy pajzs, annak védelme nulla. Találat esetén legalább 1 sebzés mindig átjut. A sikeres találat sebzésszámítása után külön dobódnak a szörny állapatterjesztő képességei; új vagy frissített állapot esetén a csatanapló feltűnő `⚠️ ÁLLAPOT` jelzéssel, az állapot saját emojijával és az időtartam újraindításának tényével jelzi azt. A karakter saját támadási szakasza után a mérgezés és vérzés egyetlen összesített naplóüzenetben sebez, figyelmen kívül hagyva a páncélt; az időtartam ekkor csökken.

### Befejezés

A csata az egyik oldal vereségével, sikeres visszavonulással vagy patthelyzettel zárulhat.
Az öléshez kötött XP, kulcs és tetem egyszer kerül elszámolásra; a csata végén a résztvevőnkénti
erőforrás- és állapotösszegzés, köralapú szükségletköltség és függő fejlődési választások következnek.
A vezető végleges veresége játék végét okozza. A világba visszatérés újraütemezi a mozgást és szükségleteket.

Az ellenfél definíciója változatlan adat. A játékos támadásának számítása egy külön `EnemyDefenseSnapshot` objektumban tartja a csata során változó HP-t és az adott támadáshoz módosított védelmi adatokat, majd a maradék HP-t visszaírja az `Enemy.CurrentHitPoints` értékébe. Az ellenfél támadása közvetlenül az `Enemy` példányt kapja, ezért a definíció mellett a futásidejű varázshatásokat, képességtölteteket és lehűléseket is ugyanabból a forrásból olvassa. A játékos HP-ja közvetlenül a `LiveCharacter` objektumon változik.


<a id="ellenseges-ai"></a>

## Ellenséges AI

### Ellenség varázsló AI

EnemySpellcastingService alapján az ellenséges varázslók már egész komoly AI-t kapnak: nem egyszerűen véletlenszerűen elsütnek egy varázslatot, hanem mana, cooldown, célpont, varázslóstílus és várható hasznosság alapján választanak. Van viszont néhány fontos aszimmetria és egy-két gyanús pont is.
Az alap működés így néz ki: az ellenség csak akkor próbál varázsolni, ha van SpellcasterProfile-ja, van legalább egy ellenséges karakter, és átmegy a CastingChancePercent dobáson. Ezután végignézi a profilban felsorolt varázslatokat, kiszűri azt, amihez nincs elég mana vagy cooldownon van, pontozza a maradékot, majd a legjobb pontszámút választja. A pontszámot még ±15%-kal randomizálja is, tehát nem mindig determinisztikusan ugyanazt lövi. Beillesztett szöveg.txtTXT

Különösen tetszik benne a mana reserve:
var reserve = profile.MaximumMana * profile.ManaReservePercent / 100;
...
var urgent = plan.Score >= 140;
if (!urgent && caster.CurrentMana - spell.ManaCost < reserve)
    continue;
Vagyis nem égeti el automatikusan az összes manáját. Ha viszont egy varázslat elég fontos (Score >= 140), akkor hozzányúlhat a tartalékhoz. Beillesztett szöveg.txtTXT

A varázsló stílusa is ténylegesen számít. Az Artillery +25%-ot ad a sebző varázslatok értékelésére, a Controller +30%-ot a kontrollokra, a Support +30%-ot a támogató varázslatokra, a BattleMage +20%-ot a saját magára rakott varázslatokra, a Necromancer pedig +15%-ot a sebzésre és kontrollra. Beillesztett szöveg.txtTXT
A célpontválasztás sem buta. Egycélpontos támadó varázslatnál először a legalacsonyabb aktuális HP-jú karaktert célozza; ha többen ugyanott állnak HP-ban, akkor a magasabb intelligenciájút preferálja:
.OrderBy(item => item.Character.CurrentVitality)
.ThenByDescending(item => item.Character.EffectiveAbilities.Intelligence)
Tehát van benne egyfajta „végezd ki a sebesültet, illetve veszélyes mágust” logika. Beillesztett szöveg.txtTXT
Területi varázslatnál azt a célpontot keresi, amely körül a legtöbb partitagnak jutna a hatásból. Gyógyításnál pedig a legrosszabb HP%-on álló szövetségest választja, buffnál pedig lehetőleg olyat, akin még nincs rajta az adott hatás. Beillesztett szöveg.txtTXT

A sebző varázslatoknál az ellenséges mágus ereje így készül:
dice
+ effect.Value
+ Intelligence * IntelligenceMultiplier
+ StrengthTier * LevelMultiplier
Tehát itt fontos: nem az ellenség tényleges szintje kerül a LevelMultiplier mögé, hanem a StrengthTier. Ez lehet szándékos, csak érdemes tudni róla. Beillesztett szöveg.txtTXT
A játékosok elleni mágikus támadás és mentő pedig jelenleg teljesen Dexterity-alapú.

Támadó dobás:
d20 + enemy Intelligence
    vs
11 + player Dexterity
Mentődobás:
d20 + player Dexterity
    vs
10 + enemy Intelligence / 2 + spell.Level
SaveHalf esetén sikeres mentő felezi a sebzést, SaveNegates esetén nullázza. Beillesztett szöveg.txtTXT
És itt kapcsolódik az előző kérdésedhez egy nagyon fontos dolog:
Az ellenséges varázslatok ellen ebben a kódban nincs MagicResistance
Legalábbis ebben a service-ben sehol nem látok olyat, hogy a játékos valamiféle MagicResistance értéke beszámítana. A mentő kizárólag:
d20 + Dexterity

A DC pedig:
10 + ellenséges INT / 2 + varázslatszint. Beillesztett szöveg.txtTXT
Ez tehát jelenleg erősen aszimmetrikus:
	Játékos → szörny	Szörny → játékos
Mágikus támadás	INT vs Speed	INT vs Dexterity
Mentő	Speed vs INT-alapú DC	Dexterity vs INT-alapú DC
MagicResistance	✅ sebzést csökkent	❌ ebben nincs
SaveHalf	✅	✅
SaveNegates	✅	✅


Van viszont még egy apróbb, szerintem valószínű hiba is. Az általános nem-sebző effekt felrakásánál ezt használod:
private bool EffectSucceeds(...)
    => resolution switch
    {
        SpellResolution.Attack => ...,
        SpellResolution.SaveNegates => !Resists(...),
        _ => true
    };

Ez azt jelenti, hogy SaveHalf esetén egy nem-sebző effekt automatikusan sikerül, mert beleesik az _ => true ágba. Beillesztett szöveg.txtTXT
Ez például egy SpeedPenalty, SkipAlternate, Burning, Storm stb. esetén érdekes lehet. Az aktív effektek nagy része ezen az úton kerül rá a karakterre. Beillesztett szöveg.txtTXT
A sebzésnél ez nem probléma, mert ott külön a ResolveDamage() kezeli a SaveHalf-ot. De állapotjellegű hatásnál a SaveHalf jelenleg nem csinál semmit a sikerességgel.
Még egy különbség: az ellenséges spell attacknál nincs természetes 1/20 szabály és kritikus varázslat sem. Egyszerűen:
d20 + intelligence < 11 + Dexterity
esetén 0 sebzés, különben teljes sebzés. Beillesztett szöveg.txtTXT
Összességében az AI-része kifejezetten fejlett, de a játékos és az ellenség varázsrendszere jelenleg nem teljesen szimmetrikus. A három dolog, amit én különösen átnéznék, az: MagicResistance a játékos oldalon, SaveHalf viselkedése nem-sebző effekteknél, illetve hogy a StrengthTier valóban szándékosan helyettesíti-e a caster levelt

### Ellenség harci AI

A **szokásos fegyveres támadásnál** a közelharci és a távolsági célpontot ugyanaz a sorrend választja ki:

1. Az élő partitagok közül csak azokat veszi számításba, akiket az adott fegyver elér. Közelharcban ez többnyire a szomszédos mező; távolsági fegyvernél a hatótáv és a rálátás is számít.
2. Az elérhető célpontok közül **a legalacsonyabb aktuális HP-arányú** karaktert támadja. Azonos aránynál a közelebbit választja. Nem automatikusan a vezért vagy a legkevesebb abszolút HP-val rendelkezőt. [Célpontválasztás](../Combat/TacticalBattleCoordinator.cs)

Az aktív blokkalakzat elölről védi a hátsó sor tagját, ha az előtte álló párja él. Ilyenkor az ellenség először a nem védett partitagok közül választ. Ha senki sem érhető el támadással, mozgási célt keres. [Alakzatvédelem](../Combat/BattleEncounter.cs) · [Mozgási cél](../Application/Game.Combat.Resolution.cs)

A több célpontot érintő fegyvereknél a fenti sorrend adja az elsődleges célpontot, majd a támadás alakja szerint kerülnek mellé továbbiak. A **tölcsér alakú támadás kivétel**: azt az irányt keresi, amelyik a legtöbb karaktert éri el. A szörnyek képességei és varázslatai saját célzási szabályokat használnak. [Többcélú és tölcsér támadások](../Combat/TacticalBattleCoordinator.cs)

<a id="megjelenites"></a>

## Megjelenítés

A `ConsoleRenderer` a pályát, karakterlapot, ASCII-képpanelt és naplót jeleníti meg.
A játéktér 170×44 konzolcella; a jobb panel szélessége nagyobb terminálnál bővülhet.
A normál napló hét látható sort ad, 1200+ pixeles kijelzőn és megfelelő konzolmagasságnál
négy további sorral bővül. A portré öt képsorból és két keretsorból áll, és **mindig alulra igazított**:
sorkoordinátája a naplómagasságtól függ, ezért a 1080p és 1200+ elrendezés eltér.

Mozgáskor és csatakor csak az érintett cellák/panelsorok frissülnek.
A `PlayfieldConsoleWriter` az azonos színű szomszédos cellákat sorfutamokba csoportosítja.
A `TerminalViewport` túl kicsi vagy átméretezés alatt álló konzolnál megakadályozza a hibás
koordinátájú rajzolást; stabil méretnél visszaállítható a nézet.

Az `AsciiPortraits` mind a hat karakterosztályhoz (`C001`–`C006`) és az első tíz ellenfélhez (`E001`–`E010`) külön, ötsoros portrét tartalmaz. Normál nézetben mindig a karakterlapon éppen megjelenített partitag osztályképe látható a karakter saját színével; a bal/jobb karakterváltás a képpanelt is azonnal újrarajzolja. Vezéri csata kezdetén a panel az aktuális ellenfél azonosító szerinti portréjára vált, színe az ellenfél 1–5-ös erősségi szintjét követi. A csata lezárásakor visszaáll a megjelenített karakter portréja. Ismeretlen vagy még portré nélküli azonosítóhoz külön `???` tartalékkép tartozik.

A `Shift+F1` a fő játékhurokban, karakterlapfókuszban, varázsválasztás/célzás alatt és a vezéri csata billentyűvárakozásakor is megnyitja ugyanazt a súgóképernyőt, mint a főmenü. A sima `F1` az első varázslat-gyorshely. Bezáráskor a játék az aktuális térképet és karakterlapot rajzolja vissza; a futásidejű játékállapot nem változik.

A karakterlap a faj és osztály alatt egy-egy sort tart fenn a tehetségeknek és az aktív állapotoknak. Az `Áll:` sor a negatív állapotok CSV-s ikonjai mellett a fenti buffemojikat is megjeleníti, hogy a hatás a súgó jelmagyarázata alapján azonosítható legyen. Ha a nevek együtt nem férnek el a 27 karakteres panelen, minden elem azonos rendelkezésre álló hosszra rövidül, így az összes aktív bejegyzés látható marad.

A pálya mérete a renderer játékterének méretéből származik, ezért a generálás és a konzolelrendezés jelenleg közvetetten össze van kötve.

<a id="mentes"></a>

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

<a id="fuggosegek-es-allapotkezeles"></a>

## Függőségek és állapotkezelés

A fő alkalmazás [projektfájlja](../Kaoszrubin.csproj) `net10.0-windows` célt és
Windows Forms támogatást használ. Függőségei:

| Függőség | Feladat |
|---|---|
| `Microsoft.AspNetCore.App` framework reference | Beágyazott Kestrel/SignalR host |
| `Microsoft.AspNetCore.SignalR.Client` NuGet | Valódi coop-kliens |
| `NAudio` NuGet | Hang és háttérzene |
| `Moq` a tesztprojektben | Tesztfüggőségek helyettesítése |

A játékmeneti szolgáltatások többnyire konstruktoron keresztül kapják függőségeiket.
A SignalR/Kestrel infrastruktúra saját ASP.NET Core szolgáltatásregisztrációt használ;
a teljes játékdomén nincs külön DI-konténerre építve.

Fontos állapotélettartamok:

- `GameDataCatalog`: egy alkalmazásfutásra változatlan;
- `CharacterRoster`, `Party` és `LiveCharacter`: menük és játékok között tovább él, JSON-ba menthető;
- `Game`: egy játékindítás idejére él;
- `DungeonLevel`: egy többképernyős szint állapotát tartja;
- `Maze`, `FogOfWar`: képernyőnként megmarad, amikor másik area aktív;
- `Player`: az aktuális világba helyezett vezető avatárja;
- `BattleSystem`: egy `Game` példányhoz tartozik;
- `BattleEncounter`: egy konkrét taktikai összecsapás idejére él;
- ellenfél aktuális HP: az adott `Enemy` teljes labirintusszintbeli életére megmarad.

## Bővítési irányelvek

- Új statikus tartalmat lehetőleg az `game-data.csv` és egy megfelelő `Definition` típus bővítésével adjunk hozzá.
- Új CSV-szekcióhoz a `DataSection`, a `ParseSection`, az `AddDefinition` és a `GameDataCatalog` összehangolt módosítása szükséges.
- Új labirintusszint-hangolás elsődleges helye a `MazeLevelConfigurations`.
- Új harci szabály a `BattleSystem` felelőssége; a renderer csak a kapott harci eseményt jelenítse meg.
- Új mentendő karakterállapothoz a `LiveCharacter`, a mentési DTO és mindkét átalakítás együttes frissítése szükséges.
- A világobjektumok örököljék a `WorldObject` típust, és a `Maze` tartsa fenn a foglaltsági szabályokat.
- A meglévő mentések és CSV-azonosítók kompatibilitását fejlesztéskor meg kell őrizni.

## Jelenlegi technikai korlátok

- A CSV-feldolgozás nem teljes RFC-kompatibilis parser; idézőjeles, vesszőt tartalmazó mező nem támogatott.
- A játék Windows- és konzolgeometria-függő, bár a nagyobb panel, magasabb napló és viewport-kezelés adaptív.
- A látóvonal nem supercover: a tökéletes átló sarokfalai külön szabály nélkül nem zárják el a sugarat.
- A személyes/közös ablakszünetek határidő-kompenzáltak; közvetlen, egyedi `Paused` útvonalnál ezt külön biztosítani kell.
- A mentés nem tetszőleges félbehagyott taktikai kör checkpointja; harc közben halasztott mentési kérés használatos.
- A LAN coop alapvégpontja nem publikus internetes, TLS/relay nélküli szolgáltatás.
- A session szinkron eseményközlése bizonyos útvonalakon zárolás alatt fut; lásd a session-események szakaszát.

Van futtatható regressziós tesztprojekt, működő papi és lovagi varázslás, valamint 1–35 közötti
mentésmigráció; ezek nem hiányzó vagy későbbre tervezett funkciók.


<a id="adatbovites"></a>

## Adatbővítés

A fegyvereknél és páncéloknál általában elég egy szabályosan kitöltött, egyedi azonosítójú CSV-sor. Ezután automatikusan:
- betöltődnek a katalógusba;
- menthetők és coopban továbbíthatók;
- bekerülhetnek a normál kereskedő kínálatába;
- eladhatók;
- megjelennek a megfelelő véletlen tárgykészletekben;
- szörnyzsákmányként is eshetnek, ha beleférnek az adott szörny #Szörny zsákmány korlátaiba.

#### Fegyvereknél bővíteni kell a WeaponProficiency.cs -ben a ForWeapon-t

A sima tárgyaknál is elegendő a CSV-sor, ha valamelyik már létező általános hatást használják, például:
- Food
- Water
- Heal
- RestoreMana
- CurePoison
- CureDisease
- StopBleeding
- None
Új, eddig nem létező hatástípushoz viszont kódmódosítás kell. Néhány tárgy jelenleg azonosító alapján különleges: kulcs, gyógytea, mézsör és fűszeres bor. Hasonló egyedi működésű új tárgyhoz szintén kód kell.
A varázstárgyaknál a meglévő típusokkal és hatásokkal elég lehet a CSV. Új MagicItemKind, új passzív hatás vagy teljesen új működés esetén kódot is kell írni. A varázslathoz kötött pálcának vagy tekercsnek természetesen érvényes varázslatazonosítóra kell hivatkoznia.
A szörnyeknél a CSV-sor önmagában csak betölti a definíciót. Attól még a szörny nem jelenik meg a pályán, mert a pályák ellenféltalálkozásai jelenleg a MazeLevelConfiguration kódjában vannak felsorolva. Tehát egy új normál szörnyhöz legalább:
1. Új sor kell a #Ellenségek szakaszba.
2. Be kell tenni valamelyik pálya szoba- vagy folyosótalálkozásai közé.
3. Ha felszerelést is dobhat, kell hozzá #Szörny zsákmány sor.
4. Ha képessége van, létező #Szörnyképességek-azonosítót kell megadni, vagy új képességsort kell felvenni.
További opcionális szörnyteendők:
- Saját portréhoz bejegyzés kell az AsciiPortraits kódba; enélkül a ??? alapértelmezett portrét kapja.
- A MonsterIds konstans nem kötelező a betöltéshez, de érdemes felvenni, ha kódból hivatkozunk rá.
- Új bossnál már több kódmódosítás kell: bosslista, pályakonfiguráció, történeti jelenet, kulcs- és kampánylogika.
- Új képességhatás csak akkor pusztán CSV-s, ha a hatástípus már létezik a kódban.
Röviden: új hagyományos tárgynál többnyire elég a CSV; új szörnynél a CSV mellett a pályakonfigurációba is be kell tenni, különben soha nem fog kisorsolódni.

### Ha a csv-ben áthelyezek egy fejezetet máshová, be fog töltődni?

Igen. Az game-data.csv teljes szekciói sorrendtől függetlenül betöltődnek, mert a hivatkozások ellenőrzése csak a teljes fájl beolvasása után történik.
Feltételek:
- A szekció fejlécét is mozgasd, például #Fegyverek.
- A hozzá tartozó oszlopfejléc és minden adatsor maradjon alatta.
- Ne kerüljön közéjük másik #Szekció, mert attól kezdve a sorok már ahhoz tartoznak.
- Az azonosítók maradjanak egyediek, a hivatkozások pedig létezzenek.
- A CSV-mezők sorrendjét ne változtasd meg önmagában.
Fontos: a játék a futási mappába másolt game-data.csv-t olvassa. Módosítás után újra kell fordítani vagy kézzel frissíteni például a bin/Debug/net10.0/game-data.csv fájlt. Ha a régi bináris mellett régi másolat marad, a változás nem látszik.

### Kampányszint beszúrásának ellenőrzőlistája

A Tiltott Erdő 6. szintként történő beszúrása **már megtörtént**. A kampány jelenlegi végpontja 22;
a 33→34 mentésmigráció a régi 6–21. sorszámokat 7–22-re helyezi.
Újabb beszúráskor nem ezt a migrációt kell újra lefuttatni, hanem új verziólépést kell készíteni.

1. **Pályakonfiguráció:** új dictionary-kulcs és egyező `Level`, név, layout, találkozások,
   speciális szobák, NPC-k, csapdák és jutalmak; szükség szerint `FinalLevel` frissítése.
2. **Tartalomhivatkozások:** minden ellenfél-, csapda-, quest- és NPC-ID létezzen;
   az explicit erdei gráf stabil areaazonosítói és kapcsolatai is legyenek érvényesek.
3. **Balansz:** ellenőrizni kell a sorszámfüggő teljesítési XP-t, fogadói kínálatot,
   mágikus felszerelést, szolgáltatásokat, csapdasávokat és közeli szintekre épülő pletykákat.
4. **Bossok:** ismételt boss-ID nem ad új kulcsot. Új kulcsadó bossnál a bosskészlet,
   kulcsszám, kijárati feltétel és történeti szöveg összehangolt módosítása szükséges.
5. **Pályakép:** a `Pictures` könyvtárban a pályanévből normalizált PNG-név használatos;
   például „Az Elveszett Erőd” → `azelveszetterod.png`. A hiányzó kép nem blokkoló adat.
6. **Mentés:** verziózott migráció kell a kampány-/nehézségszinthez, helyazonosítóhoz,
   felfüggesztett kampányhoz, párbeszédszinthez és csatlakozási történethez is, ahol érintettek.
   A már generált világot nem szabad új konfigurációval véletlenül felülírni.
7. **Ellenőrzés:** teljes kampánytartomány, kijárat/fogadó/finálé, új és régi mentés,
   pályagráf, kulcsjutalom és host–vendég snapshot egyezése.

Források: [pályakonfiguráció](../World/MazeLevelConfiguration.cs),
[mentésmigráció](../Data/GameSaveService.cs), [erdőgenerálás](forest-generation.md).

<a id="csapdak"></a>

## Csapdák

A `#Csapdák` CSV-fejezet definiálja a csapdatípusok hatását, nehézségét, valamint a sikeres
észlelésért és hatástalanításért közvetlenül a próbát végző karakternek járó XP-t. A
`MazeLevelConfigurations` `TrapCount` és `TrapIds` mezői adják meg szintenként a darabszámot és a
használható készletet. A `GuaranteedTraps` listában csapdaazonosítóval előírható egy-egy garantált
példány; az opcionális, 1-től számozott `ScreenNumber` többképernyős pályán a helyét is rögzíti.
A képernyő nélkül megadott garantált csapdákat a rendszer egyenletesen osztja el, és mindegyik
beleszámít a pálya teljes csapdaszámába. A csapdák a kezdőtértől, kijárattól,
ajtóktól és egymástól távol jelennek meg. A mezőre lépés előtt
automatikus Észlelés-próba történik Intelligenciából és Ügyességből; a Tolvaj +30 bónuszt kap.
A felfedezett csapda `K`-val, Ügyesség-próbával hatástalanítható. Az első kudarc biztonságos,
a további kudarcok 50%-os elsülési kockázatot hordoznak. A közvetlen csapdasebzés nem lehet halálos,
az első négy pályán nagyjából 15%, később 25% max-HP korláttal működik. A csapdaállapot
mentődik; rejtett csapda nem kerül a coop snapshotba, a felfedezett/elsült/hatástalanított cella viszont
a szokásos world deltán át replikálódik.

A `#NPC küldetések` szekció `Disarm` típusú céljainál a `CélId` kétféle lehet: az `ANY` minden
sikeresen hatástalanított csapdát számol, egy konkrét csapdaazonosító — például `TR101` — viszont csak
az adott típust. A hatástalanítási quest-esemény magával viszi a `TrapDefinition` objektumot, ezért több
egyidejű általános és típusspecifikus küldetés egymástól függetlenül haladhat. A betöltő az ismeretlen
csapdaazonosítót hibaként jelzi. A questhaladás továbbra is csak darabszámot ment; a cél típusa a
CSV-definícióból töltődik vissza, ezért a mentési formátum nem változott.

Az NPC lehet egyszerre egyedi és visszatérő viszonyú: az első találkozás saját karakterlapot kap,
a későbbi pályán pedig ugyanaz a kampányhoz kötött karakter térhet vissza. A `#NPC küldetések`
`MegbízóTávozikLeadásUtán` mezője `igen` értékkel a jutalomösszegzés után eltávolítja a megbízót
az aktuális pályáról, miközben a karaktere és a viszonya megmarad. Az NPCQ042 ezt használja Merionnál.

<a id="partiparancsok"></a>

## Partiparancsok

A leader felfedezés közben három, egymást kizáró tartós NPC-parancsot adhat: `H` Megállj,
`G` Gyülekező és `T` Támadás. Egy mód bekapcsolása megszakítja a másik kettőt és az ideiglenes
szétszóródást. A Megállj minden automatikus NPC-mozgást leállít; a Gyülekező a saját
viselkedési profil és az ellenségkeresés elé helyezi a leader melletti felzárkózást, majd ott tartja a
társakat; a Támadás ideiglenesen minden nem játékos által irányított társat agresszív profilként
kezeli. Kikapcsoláskor az eredeti egyéni profiljuk visszaáll. A parancsállapot mentődik, a változások
közös session-aktivitásként a coop klienseken is megjelennek.

## NPC-generálási útvonalak

| Felhasználás | Generálási belépési pont | Színpaletta |
|---|---|---|
| Első pályás ingyenes társ | `CreateLevelOne(characterClass, usedNames)` | Fehér nélkül |
| Hagyományos world-NPC | `CreateRecruit(characterClass, leaderLevel, usedNames)` | Fehér nélkül |
| Egyedi world-NPC | Egyedi definíció/factory, illetve `CreateUniqueRecruit(...)` | Karakterdefiníció szerint |
| Fogadói zsoldos | `CreateRecruit(..., allowWhiteColor: true)` | Fehér is lehet |
| Fejlesztői osztályszett | `CreateDevelopmentCharacter(...)` | Fehér nélkül |
| Fejlesztői első szintű NPC | `CreateLevelOne(usedNames)` | Fehér nélkül |

A közös generátor a [RandomCharacterGenerator](../Data/RandomCharacterGenerator.cs),
publikus belépési pontjai a [RandomCharacterGenerator.Api.cs](../Data/RandomCharacterGenerator.Api.cs)
fájlban találhatók. Egyedi karakterhez a
[UniqueNpcCharacterFactory](../Data/UniqueNpcCharacterFactory.cs) is használatos.

Hívási területek: [világindítás](../Application/Game.World.cs),
[fogadó](../Application/InnController.cs), [fejlesztői eszközök](../Application/Game.DeveloperTools.cs).

<a id="fegyverek-sebzestipusok-es-tartalekfegyver"></a>

## Fegyverek, sebzéstípusok és tartalékfegyver

Az íjak és íjpuskák `Projectile` támadásmódú, Ügyességet használó fegyverek. A támadható célpontot a minimum–maximum hatótáv, a rálátás és a megfelelő hátizsákbeli lőszer együtt határozza meg; minden lövés egy nyilat (`T029`) vagy íjpuskalövedéket (`T030`) fogyaszt. Közvetlen közelről alapból -3 találati módosító jár, amelyet a fegyvercsalád Mester fokozata megszüntet. A harcos és a tolvaj meglévő taktikái távolsági fegyvernél külön lövészeti elnevezést kapnak, de ugyanazt a menthető csataállapotot és hálózati parancsot használják.

A fogadói kereskedő mindig külön, 12 darabos nyíl- és íjpuskalövedék-csomagokat ad a korábbi sorsolt készlethez. Az adott szinten legerősebb feloldott íjat és íjpuskát szintén külön ajánlatként teszi mellé; a Kovácsmester minden addig feloldott alapváltozatot hozzáad a saját korábbi kínálatához. A feloldási szint a `#Karaktergenerálási felszerelés` táblából származik, a lövedékek és a nem szörnyeknek fenntartott távolsági fegyverek pedig nem vesznek részt a régi, rögzített méretű készlet sorsolásában, így nem szorítanak ki korábbi árukat. A tényleges lőszert használó elesett ellenfeleknél az átkutatás módosítók előtti 65% eséllyel 2–6 megfelelő lövedéket is találhat; a természetes távolsági fegyverek nem adnak lőszert.

Az automatikusan irányított csapattag a fegyverválasztáskor figyelembe veszi a lekötést, a lőszert és a ténylegesen elérhető célpontokat. Lekötött lövészként használható közelharci tartalékra vált, lőszer nélkül szintén tartalékot keres, és kedvező távoli célpontnál visszaválthat lövőfegyverre. Ha mozognia kell, közelharci fegyverrel szomszédos támadóhelyet, távolsági fegyverrel pedig a fegyver lőtávján belüli, szabad lővonalú tüzelőállást keres.

A lövős ellenfél a fegyver maximuma alatti biztonságos lőtávot részesíti előnyben. Ha nincs lekötve és túl közel került, olyan elérhető mezőre hátrál, ahonnan továbbra is legalább egy célpontot lát és elér; távolról a lövőfegyvert, közvetlen közelről az elérhető közelharci támadást választja. Lekötött, kizárólag távolsági támadással rendelkező ellenfél továbbra is lőhet, de ugyanazt a -3 közeli találati büntetést kapja, amely a részletes harci bontásban is megjelenik.

Az aktív szörnyképességek `Látóvonal`, `Végrehajtás` és `HátrálásHasználatUtán` mezői írják le a célzott lövéseket. Az `AbilityAttack` végrehajtás normál szörny-találati dobást és közeli lövési büntetést használ; a külön hatássorban megadott `Stagger` 1–3 értéke könnyű, normál vagy súlyos megingást jelent. Az első ilyen képességek a goblin íjász Menekülő lövése, a csontváz íjász Csonthegyű nyila, az ork íjász Megakasztó lövése és az orgyilkosok Mérgezett célbalövése. A Medúza Dermesztő tekintete és a beholder Bénító sugara lővonalat igényel, de a későbbi teljes képességreformig megtartja saját globális hatásesélyét.

A fegyver CSV-sora a súly után a következő oszlopokat tartalmazza: Sebzéstípus (vágás/szúrás/zúzás/tűz/sav/nekrotikus/káosz), MaxCélpont (1–4), HátsóSor (igen/nem), Fegyvercsalád. A pallos (W009), nagybalta (W017) és kétkezes pöröly (W013) alapból két célpontot érhet el. Ez nem távolsági támadás: mindkét célpontnak szomszédosnak kell lennie a támadóval és egymással is. A kiválasztott ellenfél az első célpont, a második a megfelelő élő szomszédokból rögzített térképi sorrendben választódik. Célpontonként külön találati és sebzési dobás történik, de az állapothatások és a körléptetés csak egyszer futnak.

A hátsó soros támadást a ténylegesen kézben tartott támadófegyver HátsóSor jelzője engedélyezi. A fegyver ilyenkor az élő első soros társ lekötött ellenfeleit éri el. Új szálfegyverhez nem szükséges új fegyverazonosítót beégetni. A mágikus változatok öröklik a sebzéstípust, célpontszámot, hátsó soros használatot és családot.

A páncélok a súly után, a szörnyek az alvásjelző, FegyverIds és FegyvertVálaszt után VágásVédelem, SzúrásVédelem, ZúzásVédelem, TűzVédelem, SavVédelem, NekrotikusVédelem és KáoszVédelem oszlopokat kapnak. Fegyveres támadáskor ezek előjeles páncélmódosítók: a megfelelő sebzéstípusnál a páncélhoz adódnak a páncéltörés és a minimumsebzés alkalmazása előtt. A szörnyek ugyanezen mezői a közvetlen és időszakos varázssebzést tízszázalékos lépésekben módosítják: `+10` teljes varázssebzés-immunitás, `-10` kétszeres varázssebzés. A fizikai százalékos csökkentés csak vágás, szúrás és zúzás ellen hat; a Szentély és más általános csökkentések minden típusnál érvényesek. A harci részletező fegyveres támadásnál páncélpontként, varázslatnál százalékban jelzi a típusvédelmet. A bestiárium mindkét jelentést feltünteti, a páncél tárgyleírása a pontértékeket sorolja fel.

Minden betöltött szörnynek `|` jellel elválasztott, ellenőrzött FegyverIds listája van; a lista normál és természetes fegyvert vegyesen is tartalmazhat. Az `EnemyDefinition.Weapons` és `ShieldOption` kizárólag a típus statikus fegyver- és pajzsopcióit tartalmazza, a definíciót a példányosítás nem másolja vagy módosítja. Az adott ellenfél tényleges felszerelése az `Enemy.AttackWeapons`, `EquippedWeapon` és `EquippedShield` tulajdonságokban él. Sebzése a használt fegyver tartományából dobódik, ehhez az erőbónusz és a meglévő szörnyképességek adódnak. A természetes, illetve nulla alapárú szörnyfegyverek nem viselhetők, nem kereskedhetők és nem kapnak generált mágikus változatot. Ha a FegyvertVálaszt értéke `igen`, minden létrejövő szörnypéldány egyszer választ a listából, a neve harcban például `Zombi (bunkó)`. `nem` értéknél minden támadás újra sorsol az `AttackWeapons` listából; a minotaurusz így váltogatja a nagybalta és a szarvöklelés használatát. A felszerelt fegyver és pajzs együtt kerül a mentésbe, ezért betöltéskor egyik sem sorsolódik újra. A szörny többcélú fegyvere ugyanazzal a kisorsolt támadással a MaxCélpont szerinti számú, egymáshoz kapcsolódó közeli partitagot támadja; a leheletek így 2–4 célpontra is hathatnak. A HátsóSor jelzőjű szörnyfegyver legfeljebb két taktikai egységről is használható.

Az inventory Weapon típusú helyei: 0 = első kéz, 1 = második kéz, 2 = tartalék. A tartalék beleszámít a cipelt súlyba, de nem ad támadást, pajzsvédelmet, jártasságbónuszt vagy felszerelt súlybüntetést. A karakterlap 20. sora a tartalék, 21. sora a páncél. Felfedezéskor a tartalékon Enter cserél; csatában C, egy teljes akcióért. Kétkezes tartalék elővételekor a másik kéz tárgya üres hátizsákhelyre kerül; helyhiánynál a csere egészében elmarad. A csere revíziózott és atomi, hoston és vendégnél is működik. A mentés három fegyverazonosítót ír; a régi kétfegyveres mentések üres tartalékkal tölthetők be.

A `DualWieldingRules.CanEquipOffhand` az elhelyezési jogosultságot ellenőrzi, ezért a pajzsot képzettség nélkül is elfogadja. A támadási útvonalak kizárólag a `TryGetWeapons` eredményéből készíthetnek második csapást; ez a pajzscsaládot mindig kizárja, és tőrhöz vagy kardhoz megköveteli a Kétfegyveres harc diszciplínát, valamint mindkét érintett család legalább Jártas fokát.

## Foglald össze hogy milyen szörnyeket kell legyőzni ahhoz, hogy +1 +2 +3 illetve legendás zsákmányt szerezzünk, és milyen eséllyel

A CSV-ben szereplő FelszerelésEsély nem közvetlenül a +1/+2/+3 vagy Legendás tárgy esélye, hanem annak az esélye, hogy egyáltalán dobjon a szörny a saját felszerelési zsákmánytáblájáról. Ezután a játék véletlen kategóriát, majd azon belül egy megfelelő árú és erősségű tárgyat választ.
Legfeljebb +1 felszerelés
- Hobgoblin — 55%
- Ogre — 40%
- Troll — 15%
- Minotaurusz — 50%
- Útonálló — 50%
Legfeljebb +2 felszerelés
Ezekből +1 vagy +2 eshet:
- Vérfarkas — 25%
- Bugbear — 50%
- Ettin — 45%
- Patkányember — 65%
Legfeljebb +3, de nem Legendás
Ezekből +1, +2 vagy +3 felszerelés eshet:
- Múmia — 35%
- Medúza — 45%
- Kiméra — 30%
- Ork sámán — 60%
- Wight — 50%
- Wyvern — 20%
- Kőgólem — 25%
- Hidra — 25%
- Démonpók — 25%
Legendás zsákmányra is képes ellenfelek
Ezek tábláján +1/+2/+3 és Legendás tárgy is szerepelhet:
- Vámpír — 70%
- Vörös sárkány — 90%
- Lich — 90%
- Démonlovag — 85%
- Balor démon — 90%
- Fekete sárkány — 95%
- Éji banya — 65%
- Fagyóriás — 65%
- Halállovag — 85%
- Csontsárkány — 80%
- Ősvámpír — 95%
- Pokolfejedelem — 100%
- Drakolich — 100%
- Káoszsárkány — 100%
Különleges esetek:
- Beholder — 65%: csak varázstárgyat dobhat, akár Legendásat.
- Vén beholder — 95%: csak varázstárgyat dobhat, akár Legendásat.
- Éji banya — fegyvert vagy varázstárgyat dobhat, páncélt nem.
- Fagyóriás — fegyvert vagy páncélt dobhat, gyűrűt, amulettet, pálcát vagy tekercset nem.
Mi módosítja az esélyeket?
A kijelzett 🎁 esély:
alapesély + Intelligencia
További módosítók:
- Tolvaj esetén először az alapesély 130%-a számít.
- Éles érzékek faji tulajdonság: további +15 százalékpont.
- Az eredmény legfeljebb 100%.
Ha a szörny saját fegyvere is elvihető, arra előbb külön dobás történik. Ennek alapja 30%, szintén módosítja a kereső Intelligenciája, kasztja és faja. Ha a saját fegyver kiesik, abban a keresésben a felszerelési tábla már nem dob.
Ezért például a „Vörös sárkány — 90%” nem 90% Legendás esélyt jelent, hanem 90%-os alap felszereléstábla-esélyt. A Legendás tárgy tényleges esélye ennek csak egy része, mert a játék a teljes engedélyezett Varázs–Legendás készletből választ.

<a id="varazslatok"></a>

## Varázslatok

A jelenlegi forráskód és a [game-data.csv](C:/Dev/Kaoszrubin/Káoszrubin/Data/game-data.csv:923) alapján **8 elsülési animációminta, 4 tartós viharminta, karakteraurák és egy külön Meteorzápor-effekt** van. Ezek a konzolos térkép jeleit, előtér- és háttérszíneit animálják.

**Az elsülési animációk**

A legtöbb effekt megtartja az érintett karakter vagy térképelem jelét, és azt színezi át. Az oszlopok, lángok és örvények az üres mezőkön további karaktereket is megjelenítenek. [Megvalósítás](C:/Dev/Kaoszrubin/Káoszrubin/UI/SpellImpactVisual.cs:20)

| Beállítás | Megjelenés | Jelenlegi használat |
|---|---|---|
| `Ripple` | Ismétlődő fehér–világos–sötét pulzálás; területi varázslatnál kifelé futó színhullám. | Az alapértelmezett minta; részletes lista alább. |
| `Flash` | Erős fehér villanás színes háttéren, majd elsötétedés. | **Jégvihar**: kék, 3,6 mp; **Fénykitörés**: aranybarna, 2,4 mp. |
| `Bolt` | Gyors, ismétlődő fehér/színes felvillanás az érintett mezőkön. | **Villámvihar**: aranybarna, 3,8 mp; **Savnyíl**: betegzöld, 1,2 mp; **Léleksorvasztás**: árnyszín, 1,6 mp; **Kárhozatvihar**: lila, 4 mp; **Szent csapás**: aranybarna, 1,4 mp. |
| `Pillars` | Eltolt ritmusban villogó oszlopok; üres mezőkön `│` és `·`. | **Armageddon**: vörös, 4,2 mp; **Szent ítélet**: aranybarna, 3,2 mp; **Isteni harag**: aranybarna, 4 mp. |
| `FallingFlames` | Szétszórtan felvillanó lángminta; üres mezőkön `✦` és `·`. | **Mennyei tűz**: vörös, 4,5 mp. |
| `Halo` | Ismétlődő fehér/arany felragyogás, közte fekete háttér. | **Őrangyal**: aranybarna, 4 mp; utána tartós aura is. |
| `Ward` | Színes védőmező és fehér jel váltakozása. | **Szentély**: aranybarna, 3 mp; utána tartós aura is. |
| `Vortex` | Térben eltolt villogás; üres mezőkön `╱` és `·` ad örvénylő mintát. | **Lángörvény**: vörös, 2,2 mp; utána külön viharminta. |

A `Bolt` jelenleg a célmezőket villogtatja: nincs hozzá kirajzolt, végigrepülő varázslövedék vagy a célpontokat összekötő villámív.

**A Meteorzápor külön effektje**

A **Meteorzápor (`S011`)** 4,7 másodperces, külön kezelt animációt használ:

- Három kisorsolt becsapódási középpont jelenik meg az érvényes célterületen.
- Egy becsapódás jele `✹`; ha több meteor ugyanoda esik, szám jelzi a darabszámot.
- A középpontok 450 ms-os eltolással fehér–vörös villanást kapnak.
- A sérült környező üres mezőket `▒`, a többi üres célmezőt `·` jelöli; a domináns színek vörös és sárga.

Ez **az `S011` azonosítóhoz kötött kódbeli kivétel**, nem választható általános `BecsapódásMinta`.

**A pályán maradó viharok**

Ezek az elsülési animáció után is megmaradó területek. Mintájuk másodpercenként változik; a szereplők jelei láthatók maradnak, az üres padlómezők kapják a mintakaraktereket. [Megvalósítás](C:/Dev/Kaoszrubin/Káoszrubin/UI/StormZoneVisual.cs:8)

| `ViharMinta` | Megjelenés | Jelenlegi használat és alapidőtartam |
|---|---|---|
| `Drift` | Ritkás, sodródó `╱` és `·`, foltos sötét háttér. | Alapértelmezett; jelenleg nincs ezt használó tartós viharvarázslat. |
| `Rain` | Esőszerű `│` és `·`. | **Jégvihar**: kék, 3 kör; **Dögvészvihar**: betegzöld, 3 kör. |
| `Crackle` | Széttöredezett villám-/repedésminta: `╱`, `╲`. | **Villámvihar**: aranybarna, 3 kör; **Kárhozatvihar**: lila, 3 kör; **Armageddon**: vörös, 5 kör. |
| `Embers` | Parázsszerű `✦` és `·`, ritkás sötét háttérfoltok. | **Lángörvény**: aranybarna, 3 kör. |

A Lángörvény jó példa a két szín különválasztására: **az elsülése vörös, a megmaradó parazsa aranybarna**.

**Tartós karakteraurák**

A kedvező aktív varázshatások közül a `Halo` vagy `Ward` mintájú varázslatok **sötét színű hátteret adnak a partitag jele mögé**. Ez állandó háttérszín, nem további pulzáló animáció. [Aurakezelés](C:/Dev/Kaoszrubin/Káoszrubin/UI/SpellAuraVisual.cs:8)

Jelenleg ezt használja az **Őrangyal** — alapból 5 körig, illetve elhasználódásig — és a **Szentély**, alapból 4 körig. Mindkettő sötét aranybarna aurát ad. Több megfelelő aktív varázslatnál az első megtalált aura színe érvényesül. Az ellenségek térképi kirajzolása jelenleg nem használja ezt az aurakezelést.

**A `Ripple` mintát használó többi varázslat**

Az üres `BecsapódásMinta` mező is `Ripple`-t jelent. Az alábbiaknál jelenleg engedélyezett az animáció:

| Szín | Varázslatok |
|---|---|
| **Kék — `Blue`** | Mágikus lövedék, Fagyasztó érintés, Láthatatlanság, Arkán páncél, Lassítás, Mágia szétoszlatása, Arkán kataklizma, Erőpajzs, Jégbilincs, Fényvarázslat, Rémkép, Arkán védőháló. |
| **Vörös — `Red`** | Égő kéz, Lángoló nyíl, Tűzgolyó, Dezintegráció, Lángáldás, Lángoló fegyverzet. |
| **Aranybarna — `YellowBrown`** | Villámcsapás, Láncvillám, Hasadó villám, Vakítás; Gyógyító érintés, Szent fény, Áldás, Szent pajzs, Gyógyítás, Méregűzés, Feltámasztás, Isteni védelem, Isteni csoda, Betegségűzés, Megtisztítás, Bátorság imája, Tömeges gyógyítás, Igazi feltámasztás, Remény imája, Szent fegyver, Égi bástya, Fegyveráldás, Átoktörés, Tűzoltalom, Lélekpajzs. |
| **Lila — `Purple`** | Kárhozat nyila, Rémület hulláma, Védelem a gonosztól. |
| **Betegzöld — `SicklyGreen`** | Kőbőr, Dermesztő átok, Dögvészvihar. |
| **Árny — `Shadow`** | Sírpenge, Halálosztó fegyverzet, Árnyéktű, Árnyékpajzs, Fekete bilincs, Léleklánc, Fekete nap. |
| **Vérvörös — `BloodRed`** | Pokoli lehelet, Vérvillám, Pokoltűzgömb. |

A paletták világos/sötét színpárokat jelentenek. A `Blue` konkrétan cián és sötétkék, a `Shadow` szürke és sötétszürke, a `BloodRed` vörös és sötét bíbor. [Színpárok](C:/Dev/Kaoszrubin/Káoszrubin/UI/SpellVisualPalette.cs:7)

**Konfigurálás**

A [Data/game-data.csv](C:/Dev/Kaoszrubin/Káoszrubin/Data/game-data.csv:923) `#Varázslatok` és `#Papi varázslatok` szekcióiban:

| Mező | Mit állít? | Üres mező esetén |
|---|---|---|
| `BecsapódásSzín` | Az elsülés palettáját; a fenti hét érték használható. | Arkán: `Blue`; papi: `YellowBrown`. |
| `BecsapódásIdőMs` | Az elsülési animáció hosszát, ezredmásodpercben. `0` kikapcsolja. | `Area`/`Direction`: 3000 ms; minden más célzás: 1500 ms. |
| `BecsapódásMinta` | A nyolc elsülési minta egyikét. | `Ripple`. |
| `ViharMinta` | A tartós terület négy mintájának egyikét. | `Drift`. |
| `ViharSzín` | A tartós vihar és a karakteraura palettáját. | A `BecsapódásSzín` értéke. |
| `NaplóEmoji` | A varázslat naplóbeli jelölését. | `✨`. |

Például a Lángörvény vizuális beállításai: `Red`, `2200`, `Vortex`, `Embers`, `YellowBrown`.

**Tartós vihar létrehozásához a vizuális mezők mellett valódi `Storm` hatás is szükséges** a `#Varázshatások` szekcióban, területi vagy irányított célzással és sebzéskockával. A megmaradás hosszát ott az `IdőtartamKör` adja meg. Aurához pedig aktív kedvező varázshatás és `Halo` vagy `Ward` minta kell.

A területi animáció a tényleges hatóterületet követi, a falak és takarások levágják; az egycélpontos effekt követi a mozgó célpontot. Az animáció közben a játék tovább fut, és az effektek a coop megjelenítésben is szerepelnek.

**Jelenleg kikapcsolt elsülési animációk:** Teleportáció, Időmegállítás, Dimenziókapu, Harci gyorsítás, Vérbástya, Sötét litánia, Véráldozat, Vérgyógyítás, Étel és ital teremtése.

Módosítás után újra kell fordítani és indítani a játékot, mert futáskor a kimeneti mappába másolt CSV-t olvassa. A README azon állítása, hogy minden buff és gyógyítás vizuálja ki van kapcsolva, **már elavult**; a fenti lista a jelenlegi kódot és adatokat követi.

<a id="session-esemenyek"></a>

## Session-események

### Eseménytípus és címzett

Az esemény nem automatikusan naplóüzenet. A
[GameSession](../Application/GameSession.cs), [CoopHostGateway](../Application/CoopHostGateway.cs)
és [SessionEventService](../Application/SessionEventService.cs) eltérő feladatokat végez:
vezérlési állapot, címzett parancsvisszajelzés, illetve közös aktivitás-/hang-/effektfolyam.

| Esemény | Jelleg | Címzett / felhasználás |
|---|---|---|
| `SessionPhaseChangedEvent` | Session-állapot | Résztvevők állapotfrissítése |
| `CharacterControlChangedEvent` | Tulajdonjog és vezérlés | Résztvevők vezérlési képe |
| `GameCommandRejectedEvent` | Parancsvisszajelzés | Csak a parancs küldőjének `PlayerId` értéke |
| `BattlePromptEvent` | Harci döntés | Az aktuális karakter gazdája; mások megfigyelők |
| `BattleEndedEvent` | Harci állapot | Az összecsapás lezárása |

### UI, világaktivitás és elutasítás szétválasztása

| Példa | Útvonal |
|---|---|
| „Kézben: Pallos…” | Lokális renderer / panelüzenet |
| „Goblin elesett.”, „Ajtó kinyílt.”, „Küldetés teljesítve.” | `RecordSessionActivity`, közös replikált aktivitás |
| „Az inventory azóta megváltozott.” | `GameCommandRejectedEvent`, a küldőnek címzett válasz |

A sorszám és kliensoldali kurzor akadályozza meg a replikált aktivitások és effektek ismételt lejátszását.
A snapshot-ACK/resync technikai üzenet, nem játékvilági történést jelentő naplóbejegyzés.

### Zárolási korlát

A `GameSession.Publish` szinkron módon hívja az `EventPublished` feliratkozóit;
egyes hívási helyek `_stateGate` zárolás alatt futnak. Ez jelenlegi technikai korlát,
nem már megvalósított „lockon kívüli pending-event queue”. A feliratkozókban kerülni kell
a hosszú, blokkoló műveleteket és a reentráns állapotmódosítást.

## Ellenőrzési térkép

| Terület | Forrás / regressziós ellenőrzés |
|---|---|
| Felfedezési idő, állapothatások és óra | `Game.PlayerWindowsAndNeeds.cs`, `LiveCharacter.cs`, [Tests.WorldAndUi.cs](../../Tests/Tests.WorldAndUi.cs), [Tests.WorldAi.cs](../../Tests/Tests.WorldAi.cs) |
| Felfedezettség, képarány és lombkorona | `FogOfWar.cs`, `CharacterClassRules.cs`, [Tests.WorldAi.cs](../../Tests/Tests.WorldAi.cs) |
| Fal által vágott varázsterület, baráti tűz és animáció | `SpellAreaFootprint.cs`, [SpellImpactTests.cs](../../Tests/SpellImpactTests.cs) |
| Taktikai körök, lekötés és visszavonulás | `BattleEncounter.cs`, `TacticalBattleFoundations.cs`, [Tests.Combat.cs](../../Tests/Tests.Combat.cs) |
| Session, tulajdonjog és replikáció | [Tests.Session.cs](../../Tests/Tests.Session.cs), [coop tesztharness](../../Tests/Coop/README.md) |
| Típusos questek és mentésmigráció | [Tests/Quests](../../Tests/Quests), [quest dokumentáció](quest-readme.md) |

A `Tests/KaoszrubinTests.csproj` saját futtatható regressziós tesztprogram,
nem hagyományos xUnit/NUnit tesztprojekt. A futtatási belépési pont és szűrés a
[Tests.cs](../../Tests/Tests.cs) fájlban található.
