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
5. A **Mentés** első alkalommal fájlnevet kér, később ugyanazt a fájlt frissíti. A **Mentés másként** mindig új JSON-fájlt hoz létre.
6. A **Megnyitás** a szerkesztővel korábban mentett JSON-fájlt tölti vissza.

Az **Oldalpanel** gombbal a jobb oldali panel bármikor elrejthető vagy visszahozható. A szerkesztő
maximalizálva indul, ezért kisebb vagy nagyított kijelzőn sem kellene lelógnia.

A pálya betöltése jelenleg a képernyőgráfot, neveket, template-választásokat és helyi
felülírásokat hozza be. A pálya közös alapkonfigurációja, encounterei és jutalmai továbbra is
a játék pályakonfigurációjában maradnak; a szerkesztő JSON-ja az erdei gráfot írja le.

A **Validálás** ellenőrzi az összefüggőséget, az azonosítókat, koordinátákat, kapcsolatokat és
template-hivatkozásokat. A **Képernyő előnézete** fix seeddel a játék tényleges
`ForestMazeGenerator` osztályát futtatja. A mentett `.json` fájl verziózott
`ForestLevelGraphDocument`, amely tartalmazza a célszintet is, és amelyet a játék
`ForestConfigurationJson.DeserializeDocument` / `Deserialize` metódusai töltenek be.
