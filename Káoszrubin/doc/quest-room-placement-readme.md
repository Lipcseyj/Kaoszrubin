# Küldetésszobák elhelyezése képernyőn vagy areán

A `MazeLevelConfiguration.QuestRoomPlacements` szobánként adja meg a célterületet.
A szobaazonosító a `QuestRoomIds` listában is szerepeljen.

```csharp
QuestRoomIds = ["SCREEN_ROOM", "FOREST_ROOM", "DEFAULT_ROOM"],
QuestRoomPlacements = new Dictionary<string, QuestRoomPlacementConfiguration>
{
    ["SCREEN_ROOM"] = new(ScreenNumber: 2),
    ["FOREST_ROOM"] = new(AreaId: "LOST_MANOR")
},
SpecialRoomPlacements = new Dictionary<string, SpecialRoomPlacement>
{
    ["SCREEN_ROOM"] = SpecialRoomPlacement.SideBranch,
    ["FOREST_ROOM"] = SpecialRoomPlacement.MiddleRoute
}
```

Ez szemléltető példa: a kiválasztott pályának legalább két képernyővel és
`LOST_MANOR` azonosítójú területtel kell rendelkeznie.

## Szabályok

- `ScreenNumber`: a generált területlista 1-től számozott indexe, az encounterekhez hasonlóan.
  Gráfos pályán ez nem a bejárattól számított gráftávolság. Ha a képernyőszám sorsolt,
  a megadott számnak az adott generálásban is léteznie kell.
- `AreaId`: pontos, kis-/nagybetűérzékeny stabil területazonosító. Explicit erdei gráfban
  ezt érdemes használni, mert a lista átrendezése nem változtatja meg a célt.
- Ha mindkettő meg van adva, ugyanazt a területet kell kijelölniük; az ütközés hiba.
- Hiányzó bejegyzés vagy `new()` esetén a szoba a kijárati területre kerül, a régi működés szerint.
- A `SpecialRoomPlacements` ettől függetlenül határozza meg a helyi főút/mellékág szabályt.
  A `QuestDoorRequirements` is a célterületre követi a szobát.
- A `BossRoomIds` szobái továbbra is a kijárati területre kerülnek.
- Hibás szobaazonosító, nem létező AreaId és tartományon kívüli képernyőszám esetén
  a generálás egyértelmű hibát jelez; nem helyezi át csendben máshová a szobát.

## Ládák, ellenfelek és NPC-k

A `QuestChestPlacements` és a `QuestRoomEnemyEncounters` továbbra is szobaazonosítóra hivatkozik.
A futásidő megkeresi a szoba tényleges területét, és ott helyezi el a tartalmát. Egy pálya
questládái és garantált szobaellenfelei több különböző területre is szétoszthatók.

A questroomhoz kötött NPC-találkozások már a szoba tényleges területét követik.
A szobaazonosítóknak a teljes pályán egyedinek kell lenniük; egy questláda-azonosító
nem ismétlődhet külön képernyőkön sem.

## MapEditor

A **Labirintus pálya / Pályakonfiguráció** panel **Küldetésszobák célterülete** táblázatában
szobaazonosító, képernyőszám és AreaId adható meg. A célmezők üresen hagyhatók.
A **Pályaadatok mentése** a megfelelő C# szótárat írja a pályakonfigurációba;
a játék az új beállítást újrafordítás és új pályagenerálás után használja.

Mentésformátum- vagy protokollverzió-váltás nem szükséges: a meglévő mentés már területenként
tárolja a létrejött szobákat, ládákat és ellenfeleket. A változás az újonnan generált pályákra hat.
