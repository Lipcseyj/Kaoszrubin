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

Az erdei termek `RoomKind` értéke megkülönbözteti a tisztást, kunyhót, kúriát és
labirintusépületet. Az encounter `AreaId` és `TargetRoomKind` mezőkkel stabil területre és konkrét
helyiségtípusra célozható; a régi `ScreenNumber` továbbra is támogatott.

## Erdei pályagráf-szerkesztő

A `Tools/ForestMapEditor` Windows alkalmazás a gráf vizuális szerkesztésére szolgál. A csomópontok
rácson mozgathatók, elnevezhetők, template-hez rendelhetők, összeköthetők, és a legfontosabb
víz-, mocsár-, erdősűrűség- és épületparaméterek képernyőnként felülírhatók. A szerkesztő ugyanazzal
a generátorral készít ASCII előnézetet, mint a játék. A verziózott JSON formátumot a
`ForestConfigurationJson` közösen validálja és olvassa.
