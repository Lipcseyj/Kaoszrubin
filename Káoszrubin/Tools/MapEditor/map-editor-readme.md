# MapEditor pályaszerkesztő

Indítás fejlesztői környezetből:

```powershell
dotnet run --project Tools\MapEditor\MapEditor.csproj
```

A vásznon a képernyők kijelölhetők és rácspontra húzhatók. A jobb oldali panelen szerkeszthető a
stabil azonosító, a játékban megjelenő név, a template és a leggyakoribb területi felülírások.
A beállított felülírások új, névvel ellátott, több képernyőn is kiválasztható template-ként is elmenthetők.
A felső sávban állítható a bejárat és a kijárat. A kapcsolatpanelen két ortogonálisan szomszédos
képernyő között hozható létre vagy törölhető él.

## Első lépések

1. Válassz pályaszámot, majd a **Pálya betöltése** gomb megmutatja a kampánypályát. A klasszikus pálya egy
   képernyő, a széles labirintus a megadott gráf seed alapján sorsolt képernyőszámmal látható. Erdős pályánál
   a C# alapkonfigurációra a meglévő `ForestLevelGraphs/level-x.json` gráfja töltődik rá.
2. Kattints egy területdobozra. A jobb oldali **Kijelölt képernyő beállításai** panel frissül.
3. A bepipált erdősűrűség-, tó-, mocsár- és épületértékek felülírják a template-et.
4. A **Módosítások alkalmazása** után az előnézet már az új beállítást használja.
   Az **Új előnézet** minden alkalommal új seedet sorsol; az **Előző seed ismétlése** ugyanazt
   a generálást reprodukálja. A seed az ablak címében és az állapotsorban is megjelenik.
5. A **Mentés** a betöltött erdei JSON-fájlt frissíti. Új erdei gráfnál a megfelelő `level-x.json`
   lesz a cél, ha a pálya JSON-felülírást használ. A szerkesztett fájl neve az erdei fül tetején látszik.
   A **Mentés másként** másik JSON-fájlt választ.
6. A **Megnyitás** a szerkesztővel korábban mentett JSON-fájlt tölti vissza.

Az **Oldalpanel** gombbal a jobb oldali panel bármikor elrejthető vagy visszahozható. A szerkesztő
maximalizálva indul, ezért kisebb vagy nagyított kijelzőn sem kellene lelógnia.
Az erdei fül alján lévő **Terephatás/rajtaütés overlay** csak az előnézet megjelenítését érinti,
és alapból ki van kapcsolva. A **Véletlen gráf seed** és **Másik gráf** csak véletlen gráfnál vagy
változó képernyőszámú labirintusnál látszik: a gráf előnézetét ismételhetővé vagy újrasorsolhatóvá teszi.
Betöltött erdei JSON gráfnál nincs hatása.

A jobb oldali négy fülön az erdei gráf, a labirintus pályakonfigurációja, a harci találkozások,
valamint az NPC-k, elhelyezéseik, párbeszédeik és küldetéseik szerkeszthetők. A felső **Mentés**
csak az erdei gráf JSON-ját írja. A többi fül saját mentőgombja közvetlenül a
`World/MazeLevelConfiguration.cs`, illetve `Data/game-data.csv` megfelelő részét írja.
Az NPC-találkozások `AreaId` oszlopába a térképen kijelölt erdei képernyő azonosítója illeszthető;
`QuestRoomId` és `AreaId` egyszerre nem használható. A CSV-ben létrehozott új NPC-k és questek
típusos azonosítója és legacy leképezése is automatikusan bővül.

Az NPC-fülön a **NPC-találkozás** választóval egy megjelenés szövegei és questjei
tekinthetők át. A kiválasztott találkozásnál létrehozott sor automatikusan megkapja
az NPC- és találkozásazonosítót. A párbeszéd `TalálkozásId` mezője üresen általános
szöveget, kitöltve csak az adott megjelenésre érvényes szöveget jelent. Azonos
viszonysávban a találkozáshoz kötött szöveg elsőbbséget élvez. A questek
`TalálkozásId`, `MinimumViszony` és `MaximumViszony` mezői a felajánlást korlátozzák;
a már aktív quest a következő találkozásnál is leadható. Az NPC-k
`VisszatérőViszony` oszlopába írt `igen` ugyanazt a karaktert és mentett viszonyát
viszi tovább a későbbi pályákra. Az elhelyezést mentsd el a rá hivatkozó szöveg
vagy quest előtt.

A harci találkozásoknál az erdei gráf stabil `AreaId`-t, a klasszikus és széles labirintus
képernyőszámot (`ScreenNumber`) használ a célzás segédgombján. A széles pálya `AREA_1`, `AREA_2`
stb. azonosítókat is kap, de változó képernyőszámnál csak a konfigurált minimumig létező
képernyők célozhatók biztosan. A mentés ezt ellenőrzi.

A labirintus és a harci találkozások mezői C# kifejezéseket tartalmaznak. Mentés után fordítsd újra
a játékot és indítsd újra a szerkesztőt, hogy az előnézet az új C# konfigurációt használja.
Az egyes labirintusmezők fölött látható rövid magyarázat; a címkére vagy a mezőre állva részletes
tooltip és példa jelenik meg.
Az egyedi történeti questek külön szabályait a `QuestCatalogBuilder` tartalmazza; ezekhez a
szabályokhoz a CSV sor önmagában nem elegendő.

A **Validálás** ellenőrzi az összefüggőséget, az azonosítókat, koordinátákat, kapcsolatokat és
template-hivatkozásokat. Az előnézet a kiválasztott pálya közös erdőkonfigurációjával és a játék tényleges
`ForestMazeGenerator` osztályával készül. A mentett `.json` fájl verziózott
`ForestLevelGraphDocument`, amely tartalmazza a célszintet is, és amelyet a játék
`ForestConfigurationJson.DeserializeDocument` / `Deserialize` metódusai töltenek be.

## Egyedi sablonok

A template mentése így működik:
1. Válassz ki egy képernyőt.
2. Válassz egy meglévő template-et alapnak.
3. Módosítsd a kívánt értékeket a tulajdonságrácsban.
4. A „Template ID” mezőbe írj stabil technikai azonosítót, például foggy-manor.
5. A „Név” mezőbe írj megjelenő nevet, például Ködös kúriavidék.
6. Nyomd meg a „Beállítások mentése új template-ként” gombot.
Ezután:
- Az új template a korábban kiválasztott template-ből örököl.
- Csak az attól eltérő értékeket tárolja.
- A jelenlegi képernyő automatikusan átvált az új template-re.
- A képernyő saját felülírásai kiürülnek, mert azok már a template részévé váltak.
- Az új template megjelenik a template-listában, így más képernyőkhöz is kiválasztható.
- A paletta és a BuildingStyles módosításai szintén bekerülnek.
- A template a térkép JSON-fájljába kerül a következő mentéskor.
Fontos: ez projekt-JSON-on belüli template, nem kerül automatikusan a játék beépített globális template-katalógusába. Másik JSON-fájlban csak akkor lesz elérhető, ha abban is szerepel.
Meglévő saját template-et jelenleg nem lehet azonos ID-val felülírni. Módosított változathoz új ID-t kell megadni.
