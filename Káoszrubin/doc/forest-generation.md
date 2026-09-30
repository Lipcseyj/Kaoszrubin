# Erdei pályagenerálás

A 6. pálya (`Tiltott Erdő`) beállításai a `World/MazeLevelConfiguration.cs` fájlban,
a `ForestMazeLayoutConfiguration` alatti `ForestGenerationConfiguration` objektumban találhatók.
Az új alapbeállítás `ForestDensity = 0.58`: nagyobb rétek és összefüggő erdőfoltok váltakoznak.
A változtatások új pálya generálásakor érvényesülnek; a már elmentett térképek megmaradnak.

## Borítás és növényzet

| Beállítás | Jelentés |
| --- | --- |
| `ForestDensity` | A belső térkép fával vagy sűrű bozótként kitöltött hányada **a tavak, tisztások, épületek és ösvények elhelyezése előtt**. 0–1 közötti érték. |
| `GroveSize` | A facsoportok térbeli léptékének tartománya, mezőben. Kisebb érték több apró ligetet, nagyobb érték nagyobb erdőtömböket ad. Nem az egyes ligetek pontos sugara. |
| `BiomeSize` | A fafajokat elválasztó tájegységek léptéke. Általában a `GroveSize` fölé érdemes állítani. |
| `PineChance` | A fenyves tájegységek területi részaránya. 0: lombos; 1: fenyves. A fafaj nem cellánként sorsolódik. |
| `ThicketChance` | A fás részeken sűrű bozótfoltokat kijelölő mező borítása. |
| `BushChance`, `FlowerBushChance` | A nyílt erdőszegély bokros és virágos bokros foltjainak közelítő részaránya. Együtt legfeljebb 1. |
| `BushGroupSize` | A bokros és sűrű bozótfoltok térbeli léptéke. |
| `ForestEdgeWidth` | Milyen széles sávban jelenhetnek meg bokrok a fás terület nyílt oldalán (1–10 mező). |
| `UndergrowthChance`, `DenseUndergrowthChance` | A szabad talajon összefüggő aljnövényzetfoltokat adnak; járhatók. Együtt legfeljebb 1. |

A fafajok, a bozót, a bokrok és az aljnövényzet külön térbeli rétegek; arányaikat nem kell egyetlen
közös 1-es összegbe beosztani. A fenyvesek és a bozót tényleges mennyisége attól is függ, hogyan
metszik a fás területet. A bokrok helyét az erdőszegély korlátozza; a tisztások belsejét és az
épületeket nem töltik ki. A fás foltok és az aljnövényzet változó léptékű, szabálytalan formák.

## Vizes területek és ösvények

| Beállítás | Jelentés |
| --- | --- |
| `LakeCount`, `LakeRadius` | Tavak száma és közelítő sugara képernyőnként. A partvonal szabálytalan. |
| `MarshCount`, `MarshRadius` | Tavaktól független mocsárfoltok száma és közelítő sugara. `LakeCount = new(0, 0)` mellett is működik. |
| `MarshChance` | A tavak körüli mocsaras partszakaszok aránya. Az önálló mocsarakat nem kapcsolja ki. |
| `TrailWidth` | Az ösvények alapvető szélessége (1–5 mező). A partnál, illetve mocsári átkelőnél szűkülhet. |
| `TrailWinding` | 0: közvetlenebb utak; 1: erősebb kanyarok. A terepet kerülő útkeresés mindkét esetben működik. |
| `ExtraTrailChance` | A tisztások további összeköttetéseinek esélye (0–1). Növelése több hurkot és kerülőutat eredményez. |

A vizes foltok kerülik a szobák és épületek fenntartott helyét. Szűk térképen kevesebb folt férhet el;
az összeérő tavak/mocsarak összeolvadhatnak. Az ösvények először száraz útvonalat keresnek. Csak teljesen
körbezárt száraz szigethez készül keskeny, járható mocsári átkelő. A termek és a természetes nyílt
területek is elérhetők maradnak. Az épületajtók az eddigi nyitott/csukott/zárt szabályokat követik.

## Erdei épületek

Egy épület továbbra is egyetlen erdei helyszínnek számít, de a belseje több külön szobát is
tartalmazhat. Az alaprajz háromféle lehet:

| Típus | Alaprajz |
| --- | --- |
| Kunyhó | A `BuildingSize` szerinti kis épület, egy vagy a `BuildingPartitionChance` alapján két szobával. |
| Nagy épület | A `ManorBuildingWidth` és `ManorBuildingHeight` szerinti téglalap, amelyet a generátor 3–8 összefüggő szobára oszt. |
| Labirintusépület | Helyi, 3×3-as helyiségekből és keskeny folyosókból álló, hurkokat is tartalmazó belső járathálózat. |

`ManorBuildingChance` és `LabyrinthBuildingChance` adja a két nagyobb típus esélyét; a fennmaradó
rész kunyhó. A két érték összege legfeljebb 1 lehet. A nagy épület célzott szobaszámát a
`ManorRoomCount`, legkisebb szobaméretét a `BuildingMinimumRoomSize` szabályozza.
`BuildingExtraConnectionChance` további belső átjárókat és hurkokat, a
`BuildingSecondEntranceChance` pedig lehetséges második külső ajtót ad. Minden belső helyiség külön
szobaként kerül a térképbe, ezért önálló találkozást, kincset vagy küldetéstartalmat kaphat. A teljes
épület bejárhatóságát a pálya közös hozzáférhetőségi ellenőrzése garantálja.

## Kiinduló beállítások

| Táj | `ForestDensity` | `GroveSize` | `BiomeSize` | `TrailWinding` | `ExtraTrailChance` |
| --- | --- | --- | --- | --- | --- |
| Rét elszórt facsoportokkal | `0.18` | `new(3, 8)` | `16` | `0.35` | `0.15` |
| Váltakozó erdők és tisztások | `0.58` | `new(5, 14)` | `22` | `0.75` | `0.35` |
| Sűrű, ösvényes rengeteg | `0.95`–`1.0` | `new(8, 18)` | `26` | `0.90` | `0.25` |

Nyílt réthez a bokrokat és az aljnövényzetet is lehet csökkenteni. Nagy mocsaras tájhoz például
`MarshCount = new(2, 4)` és `MarshRadius = new(5, 10)` adható. Teljesen száraz térképhez a
`LakeCount` és `MarshCount` egyaránt `new(0, 0)` legyen.

A végső járható arány nem pontosan `1 - ForestDensity`: a tisztások és ösvények növelik, a víz,
bokrok és épületfalak csökkentik. A `RoomCount` képernyők között elosztott értéke a garantált
tisztások/épületek száma; magas értéke még 1-es erdősűrűségnél is sok nyílt helyet vág ki.
A térkép külső, egymezős kerete zárt marad, amíg a képernyőkapcsoló átjárókat nem készít rá.

## Képernyőnkénti profilok és gráf

Az erdei layout opcionális `ExplicitGraph` konfigurációja stabil azonosítóval, névvel és
`AreaCoordinate` koordinátával írja le a képernyőket. A kapcsolatok külön listában szerepelnek, ezért
két szomszédos koordináta csak explicit él esetén kap átjárót. Az explicit gráfnak összefüggőnek kell
lennie, egy koordinátát csak egy terület használhat, és minden él ortogonálisan szomszédos képernyőket
köthet össze.

Minden `ForestAreaDefinition` választ egy template-et és opcionális
`ForestGenerationConfigurationPatch` felülírást. A feloldási sorrend: közös erdőkonfiguráció →
template-öröklés → képernyőfelülírás. A beépített template-ek:

- `mixed-forest` – normál vegyes erdő;
- `swamp` – mocsárvidék;
- `lakes-and-manors` – tavak és kúriák;
- `dense-cabin-forest` – sűrű erdő kunyhókkal;
- `forest-labyrinth` – sűrű, kanyargó erdő labirintusépületekkel;
- `open-groves` – nyílt ligetek.

Egyedi template pontosan egy másik template-ből örökölhet. A körkörös öröklést, az ismeretlen
hivatkozást és a duplikált azonosítót a betöltő elutasítja. Az egyes képernyők külön, stabil seedet
kapnak, ezért egy terület szerkesztése nem rendezi át a többi képernyő véletlen terepét.

A `Ctrl+M` a játékban a felfedezett régiótérképet nyitja meg. A már meglátogatott területek neve
látszik; egy felfedett átjáró túloldala név nélküli kérdőjelként jelenik meg. A térkép a területek
mentett ködállapotából épül, így nem igényel külön mentésmigrációt és nem fedi fel előre a gráfot.

## Épületstílusok és célzott encounterek

A `BuildingStyles` súlyozott listája különböző falrúnát, színt és opcionálisan engedélyezett
alaprajztípusokat rendelhet az épületekhez. Üres lista esetén a régi `Palette.BuildingWall` működik.
A jelenlegi tereptárolás miatt az eltérő falstílusoknak egyedi rúnát kell használniuk.

### Részletes magyarázat a legfontosabb épületparaméterekhez

Az alábbi mezők együtt határozzák meg, mennyi épület lesz, mekkorák lesznek, és mennyire
szabdalt/összetett belső tereket kapnak:

| Beállítás | Mit szabályoz pontosan | Gyakorlati hatás | Tipikus tartomány |
| --- | --- | --- | --- |
| `BuildingCount` | Képernyőnként mennyi épületet próbáljon elhelyezni a generátor. | Több beltéri helyiség, több potenciális találkozás és kincs. | `new(0, 0)`–`new(3, 4)` |
| `BuildingSize` | A kunyhó (`Cabin`) típus alapterülete (szélesség és magasság). | Kis érték: szűk kunyhók; nagy érték: tágasabb kunyhók. | `new(4, 7)`–`new(7, 10)` |
| `BuildingPartitionChance` | A kunyhó kettéosztásának esélye. | Magas értéknél gyakrabban lesz 2 szobás kunyhó. | `0.0`–`1.0` |
| `ManorBuildingChance` | A „nagy épület” (kúria) esélye a nem-kunyhó döntésben. | Több kúria, több belső szoba. | `0.2`–`0.8` |
| `LabyrinthBuildingChance` | A labirintusépület esélye. | Több kanyargós, 3×3-as helyiségrácsos belső tér. | `0.05`–`0.6` |
| `ManorBuildingWidth`, `ManorBuildingHeight` | Kúria külső téglalapmérete. | Nagyobb kúria → több osztható tér, hosszabb belső útvonalak. | ~`new(10, 18)` / `new(8, 14)` |
| `ManorRoomCount` | Kúriában célzott szobaszám. | Magasabb érték: tagoltabb, több belső ajtó. | `new(3, 8)` |
| `LabyrinthBuildingWidth`, `LabyrinthBuildingHeight` | Labirintusépület külső mérete. | Minél nagyobb, annál több 3×3-as modul fér el. | `new(13, 22)` / `new(8, 17)` |
| `BuildingExtraConnectionChance` | Extra belső ajtók/átjárók esélye. | Több hurok, kevesebb zsákutca az épületen belül. | `0.1`–`0.35` |
| `BuildingSecondEntranceChance` | Második külső bejárat esélye. | Több „átmenő” épület, jobb alternatív útvonalak. | `0.1`–`0.4` |
| `LockedBuildingDoorChance` | Létrejövő épületajtó zárt (`Locked`) állapotának esélye. | Több kulcs-/ajtóinterakció, lassabb bejárás. | `0.05`–`0.25` |
| `OpenBuildingDoorChance` | Nyitott (`Open`) állapot esélye; a maradék többnyire `Closed`. | Gyorsabb belépés, kevesebb ajtónyitás. | `0.05`–`0.25` |

Megjegyzés: `ManorBuildingChance + LabyrinthBuildingChance` legfeljebb `1.0` legyen. A fennmaradó rész
automatikusan kunyhó (`Cabin`).

#### `BuildingStyles` részletesen

A stíluslista minden eleme:

- egy stílusazonosító (`"mossy-timber"`),
- egy falstílus (`MazeTerrainStyle`) saját rúnával/színnel,
- egy súly (`Weight`),
- opcionálisan engedélyezett alaprajztípusok (`AllowedLayouts`).

Példa értelmezés a kijelölt konfigurációból:

- `mossy-timber` súly `4` → gyakoribb;
- `old-stone` súly `3` → közepesen gyakori;
- `dark-manor` súly `2` + `AllowedLayouts = { Manor, Labyrinth }` → ritkább, és kunyhóra nem kerül.

A sorsolás a súlyok arányában történik a layoutnak megfelelő szűrés után.

## Ajánlott „starter” erdei szintkonfiguráció (teljes minta)

Az alábbi minta jó kiindulópont egy új erdei pályához: változatos, de még jól kontrollálható
paraméterekkel indul. A pályaszinthez tartozó encounter/lista mezőket külön kell kitölteni.

```csharp
Layout = new ForestMazeLayoutConfiguration(
	new DungeonAreaGraphConfiguration(new IntRange(5, 7), MinimumExitDistance: 3,
		MaximumDegree: 3, BranchChance: 0.45, ExtraConnectionChance: 0.15),
	new ForestGenerationConfiguration
	{
		ForestDensity = 0.55,
		GroveSize = new IntRange(5, 13),
		BiomeSize = 20,
		PineChance = 0.25,
		BushChance = 0.16,
		FlowerBushChance = 0.05,
		BushGroupSize = new IntRange(2, 5),
		ForestEdgeWidth = 3,
		ThicketChance = 0.08,
		UndergrowthChance = 0.24,
		DenseUndergrowthChance = 0.10,

		LakeCount = new IntRange(1, 2),
		LakeRadius = new IntRange(2, 5),
		MarshChance = 0.55,
		MarshCount = new IntRange(1, 3),
		MarshRadius = new IntRange(3, 6),

		TrailWidth = 2,
		TrailWinding = 0.70,
		ExtraTrailChance = 0.30,

		BuildingCount = new IntRange(1, 2),
		BuildingSize = new IntRange(5, 8),
		BuildingPartitionChance = 0.70,
		ManorBuildingChance = 0.40,
		LabyrinthBuildingChance = 0.12,
		ManorBuildingWidth = new IntRange(10, 18),
		ManorBuildingHeight = new IntRange(8, 14),
		ManorRoomCount = new IntRange(3, 7),
		LabyrinthBuildingWidth = new IntRange(13, 20),
		LabyrinthBuildingHeight = new IntRange(9, 16),
		BuildingExtraConnectionChance = 0.18,
		BuildingSecondEntranceChance = 0.20,

		BuildingStyles =
		[
			new("starter-timber", new("starter-timber-wall", new('▓'),
				ConsoleColor.DarkYellow, ConsoleColor.Black, false, true), 4),
			new("starter-stone", new("starter-stone-wall", new('▣'),
				ConsoleColor.Gray, ConsoleColor.Black, false, true), 3),
			new("starter-manor", new("starter-manor-wall", new('▤'),
				ConsoleColor.DarkGray, ConsoleColor.Black, false, true), 2,
				new HashSet<ForestBuildingLayout>
					{ ForestBuildingLayout.Manor, ForestBuildingLayout.Labyrinth })
		],

		LockedBuildingDoorChance = 0.15,
		OpenBuildingDoorChance = 0.15
	})
```

Gyors finomhangolási javaslatok induláshoz:

- Túl zsúfolt: csökkentsd `ForestDensity`-t (`0.50` környékére) vagy emeld `TrailWidth`-et.
- Túl sok beltér: csökkentsd `BuildingCount`-ot és/vagy `ManorBuildingChance`-t.
- Kevés alternatív útvonal: emeld `ExtraTrailChance`-t és `BuildingSecondEntranceChance`-t.

Az erdei termek `RoomKind` értéke megkülönbözteti a tisztást, kunyhót, kúriát és
labirintusépületet. Az encounter `AreaId` és `TargetRoomKind` mezőkkel stabil területre és konkrét
helyiségtípusra célozható; a régi `ScreenNumber` továbbra is támogatott.

## Erdei pályagráf-szerkesztő

A `Tools/ForestMapEditor` Windows alkalmazás a gráf vizuális szerkesztésére szolgál. A csomópontok
rácson mozgathatók, elnevezhetők, template-hez rendelhetők, összeköthetők, és a legfontosabb
víz-, mocsár-, erdősűrűség- és épületparaméterek képernyőnként felülírhatók. A szerkesztő ugyanazzal
a generátorral készít ASCII előnézetet, mint a játék. A verziózott JSON formátumot a
`ForestConfigurationJson` közösen validálja és olvassa.
