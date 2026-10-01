# Erdei pályagráf-szerkesztő

Indítás fejlesztői környezetből:

```powershell
dotnet run --project Tools\ForestMapEditor\ForestMapEditor.csproj
```

A vásznon a képernyők kijelölhetők és rácspontra húzhatók. A jobb oldali panelen szerkeszthető a
stabil azonosító, a játékban megjelenő név, a template és a leggyakoribb területi felülírások.
A beállított felülírások új, névvel ellátott, több képernyőn is kiválasztható template-ként is elmenthetők.
A felső sávban állítható a bejárat és a kijárat. A kapcsolatpanelen két ortogonálisan szomszédos
képernyő között hozható létre vagy törölhető él.

## Első lépések

1. Válassz pályaszámot, majd a **Pálya betöltése** gomb a kiválasztott kampánypálya beépített erdőgráfját nyitja meg.
   Ha a pálya nem erdei layoutot használ, a szerkesztő figyelmeztet és kihagyja a betöltést.
2. Kattints egy területdobozra. A jobb oldali **Kijelölt képernyő beállításai** panel frissül.
3. A bepipált erdősűrűség-, tó-, mocsár- és épületértékek felülírják a template-et.
4. A **Módosítások alkalmazása** után az előnézet már az új beállítást használja.
   Az **Új előnézet** minden alkalommal új seedet sorsol; az **Előző seed ismétlése** ugyanazt
   a generálást reprodukálja. A seed az ablak címében és az állapotsorban is megjelenik.
5. A **Mentés** első alkalommal fájlnevet kér, később ugyanazt a fájlt frissíti. A **Mentés másként** mindig új JSON-fájlt hoz létre.
6. A **Megnyitás** a szerkesztővel korábban mentett JSON-fájlt tölti vissza.

Az **Oldalpanel** gombbal a jobb oldali panel bármikor elrejthető vagy visszahozható. A szerkesztő
maximalizálva indul, ezért kisebb vagy nagyított kijelzőn sem kellene lelógnia.

A pálya betöltése jelenleg a képernyőgráfot, neveket, template-választásokat és helyi
felülírásokat hozza be. A pálya közös alapkonfigurációja, encounterei és jutalmai továbbra is
a játék pályakonfigurációjában maradnak; a szerkesztő JSON-ja az erdei gráfot írja le.

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