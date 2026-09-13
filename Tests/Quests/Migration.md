# Questmigráció – végrehajtási napló

## 2026-09-13: a 0. lépés elkészült

Terv: `C:/Users/rockm/Documents/Codex/2026-09-12/h/outputs/quest-migracios-terv.md`.
Kiinduló commit: `0913a64` (`Introducing QuestManager to Game`), tiszta munkafa.

Ez a változtatás a tesztalapot készíti elő. Az alkalmazás futásidejű működését,
a mentés formátumát és a kompatibilitási bridge-eket még nem módosítja.
Az 1–7. lépés továbbra is nyitott; ez önmagában nem a terv első kiadható migrációs mérföldköve.

### Ellenőrzött tesztalap

A `QuestTestFixture` kis, memóriabeli világ-, inventory-, jutalom- és beszélgetési
adapterekkel állítja össze a valódi `QuestManager`, `QuestStateStore`,
`QuestAvailabilityService`, `QuestProgressEngine`, `QuestCompletionProcessor` és
`QuestRewardService` szolgáltatásokat. A fixture nem tölt CSV-t, nem indít Game-et
vagy UI-t, és nem használ legacy quest API-t. Minden teszt saját állapotot kap.

A `QuestManagerTests` 12 külön regisztrált tesztje a publikus manager/handle API-kon ellenőrzi:

- a történeti kapu megnyílását, visszazárását és az aktív állapot megőrzését;
- a zárolt és feladott küldetések kizárását a tömeges aktiválásból;
- az objective-hoz tartozó eseményeket, az aktív állapotot és a progress felső határát;
- a collect kezdeti inventoryját és a 2/3 → 3/3 → 2/3 változásjelzéseket;
- a mellékhatásmentes leadhatóság-ellenőrzést, az egyszeri fogyasztást,
  XP-elosztást és fix/véletlen tárgyjutalmat, telt hátizsák esetén is;
- a leadás előtt megváltozott inventory újraellenőrzését;
- a sikertelen tárgyelvétel jutalom nélküli elutasítását és az újrapróbálást;
- az Active és ReadyToTurnIn állapotból történő azonnali feladást;
- két azonos NPC-típus külön progressét, leadását és feladását;
- a globális quest azonosságát és a hibás NPC/hiányzó instance elutasítását;
- a beszélgetés NPC-példányazonosítójának továbbítását;
- a follower kill célpontját, részvételi és történeti feltételét, valamint az adapternek átadott távolsághatárt.

A follower teszt az adapter felé továbbított szerződést vizsgálja; a tényleges
térképi távolság és az élő Game integrációjának ellenőrzése későbbi lépés.
A tömeges aktiválás tesztje meglévő aktiválási feltételekre vonatkozik: a CSV-ben
feltétel nélkül szereplő Roderic questek történeti indítása még nincs rendezve.

### A korábbi két teszthiba oka

1. Az NPC-párbeszédek tesztje 73 bejegyzést várt, de az `81becb5`
   (`Improved npc conversations`) commit eltávolította az NPCD070–NPCD073 sorokat.
   A helyes jelenlegi darabszám 69. A párbeszédek, történeti választások, questek
   és az egyes questtípusok darabszáma külön, várt/tényleges értéket tartalmazó
   ellenőrzést kapott. A legacy NPC-életciklus külön tesztként fut, ezért egy
   CSV-eltérés többé nem akadályozza meg a végrehajtását.
2. Külön, questtől független tesztkarbantartás: a `6aa359a` commit szándékosan
   növelte a csapdaszámokat. Az 1., 10. és utolsó szint tesztelvárása ehhez
   igazodott: 3–7, 5–10, illetve 6–13. A játékmeneti konfiguráció változatlan.

### Fordítási és teszteredmények

Parancsok a repository gyökeréből:

```powershell
dotnet build 'KáoszrubinApps.slnx' --no-restore --no-incremental -v:minimal
& '.\Tests\bin\Debug\net10.0-windows\KaoszrubinTests.exe'
```

| Ellenőrzés | Előtte | Utána |
|---|---:|---:|
| Fordítási hiba | 0 | 0 |
| CS0618 warning | 43 | 43 |
| Sikeres teszt | 233 | 248 |
| Hibás teszt | 2 | 0 |

A 13 új regisztráció: 12 típusos teszt és a különválasztott legacy életciklusteszt.
Az összes figyelmeztetés meglévő legacy használatból ered; nincs új `NoWarn`
vagy figyelmeztetést elnyomó pragma. A build összegzésében ismétlődő sorok
duplikáció nélkül számolandók.

| Fájl | CS0618 kiindulópont és jelenlegi érték |
|---|---:|
| `Káoszrubin/Application/Game.cs` | 26 |
| `Káoszrubin/Application/WorldSnapshots.cs` | 3 |
| `Káoszrubin/Data/GameStateMapper.cs` | 3 |
| `Káoszrubin/Data/GameSaveService.cs` | 1 |
| `Káoszrubin/World/WorldNpc.cs` | 2 |
| `Tests/Program.cs` | 8 |

### Következő lépés

Az 1. lépésben az `ActivateNpcQuest`, `AbandonQuest`,
`AbandonActiveQuestsFromNpc` és `ResolveTemporaryFollowerAtExit` útvonalakat
kell az új API-ra vezetni, a történeti aktiválás és az automatikus felvétel
szétválasztásával. Az explore/escort események, a már felfedezett kijáratra
utólag felvett quest és az inventoryváltozások projekciója szintén ide tartozik.

Az egyszeri jutalmazás tesztje a sikeres leadás megismétlését védi. A jutalmazó
adapter által dobott kivétel esetén a fogyasztás/XP/tárgyjutalom még nem atomi;
ezt a mostani csomag nem javította, és nem rögzíti helyes viselkedésként.

Az első kiadható mérföldkő továbbra is az 1–2. lépés együtt: a típusos állapot
legyen az egyetlen játékmeneti állapotforrás, stabil NPC-azonossággal és működő
régi/új mentésbetöltéssel. A jelenlegi zöld tesztek ezt még nem igazolják.
