# Hatfős parti – megvalósítási állapot

2026. október 8. – a második szállítási ütem kódja elkészült; a látható kampányfeloldás még kikapcsolt.

## Kapacitás, kampányállapot és mentés

- A technikai felső határ 6. A tényleges csatlakozási korlátot a `Party.Capacity` és az `IsFull` adja; az alapértelmezett `ExpandedPartyEnabled = false` mellett továbbra is 4.
- A közös kapacitásszabály az 5. és 8. sikeresen lezárt főpályához köti az új helyeket. A kampány menti a legmagasabb teljesített főpályát, a két támogatás felhasználását és az egyszeri bemutatásokat. Halál, eltávolítás, vezetőváltás és régi pillanatkép visszaállítása nem vonja vissza ezeket; új kampány négy helyről indul.
- A főpálya fogadói lezárása a túlélők jutalmazása és az elesettek eltávolítása után rögzíti a teljesítést. Mellékhelyszín, területváltás és fejlesztői pályaugrás nem old fel új helyet.
- A fogadói, világ-NPC-, egyedi követő-, fejlesztői és coop csatlakozás a közös korláthoz igazodik. A támogatott normál felvétel és a támogatás felhasználása egyetlen partiművelet; sikertelen felvételnél nincs támogatásfogyás. A társcsere is egyetlen művelet, sikertelen normál felvételért nincs fizetés.
- A **37-es mentésformátum** tárolja a kampányállapotot, az explicit alakzatformát és mind a hat slotot. A régi négy hely változatlan 2×2-es beosztásban migrálódik, az új helyek üresek. Későbbi bővítéskor a régi hátsó tagok a menetoszlop középső sorába kerülnek.
- Régi kampánymentésben a 6–8. pálya öt, a 9. vagy későbbi pálya hat hely jogosultságát kapja. Questmentés a felfüggesztett kampányból következtet. Új mentés pályaszámából nincs becslés. A visszatöltés a kapacitásállapotot a taglista helyreállítása előtt alkalmazza.
- A **104-es coop protokoll** továbbítja a kapacitást, a kampányállapotot, a hathelyes alakzatot és a harci célszemélyeket. A hálózati játékosok számának korlátja változatlan.

## Alakzat és mozgás

- A közös geometriai API kezeli a 2×2-es blokkot, a 2×3-as menetoszlopot és a 3×2-es széles harcrendet. A sor és az oszlop a formából következik; minden élő rendes tag pontosan egyszer szerepel. Ötfős partiban a szándékosan üres slot megmarad.
- A 3×2-es forma technikai alapjai már a közös modell részei; a kampánybeli hatodik hely és a széles forma látható használata a későbbi feloldási ütemhez tartozik.
- A követési sorrend, a rácselhelyezés, a libasor, a kanyar és a visszafordulás hat tagot kezel. Az ideiglenes követő nem foglal rendes slotot.
- A téglalap fordulása és a hosszabb átrendeződés előre megtervezett, járható, szomszédos lépésekből áll. Elérhetetlen vagy foglalt cél esetén a tervezés nem módosít pozíciót. Az út közben érintett terep, látótér, láda és csapda minden lépésnél érvényesül; újonnan felfedezett csapda vagy elesett társ megszakítja a végrehajtást és feloszlatja az alakzatot.
- A régi 2×2-es forgatási viselkedés megmarad. Teljes alakzatszerkesztés csak harcon kívül nyitható meg.

## Harc és felület

- A védelemhez a közvetlenül előző sor azonos oszlopában élő, helyén álló, nem megingott társ szükséges. Üres középső sloton nincs átugró védelem. Lekötött célpont, oldal- vagy háttámadás, feloszlott alakzat és libasor nem kapja ezt a védelmet.
- A hátsó közelharc csak a közvetlen társ által lekötött, ténylegesen elérhető ellenfeleket engedi; a harmadik sor nem támad két társon keresztül.
- A harci sorcsere egy akcióba kerül, csak szomszédos sorok azonos oszlopú tagjai között működik, és a két mező lekötéseit átadja. A felkészítés bármely szabad támogató sorbeli társra kiadható.
- **H**: sorcsere, **K**: felkészítés; a célválasztóban **1–6** választ, **Esc** megszakít. A 2×2-es gyorsparancsok megmaradnak. A vendég célválasztója harc- vagy körváltáskor bezárul, a host újra ellenőrzi a parancsot.
- A karakterlap hat státuszsort és a valós kapacitást mutatja. A partisora egy sorral lejjebb, 42-től kezdődik; az alakzat ikonja alattuk, a 48. soron van. Nézési nyíl, Braille-rács, soronkénti színes karakterpontok és állapotikon helyettesíti a régi szöveges alakzatjelzést. A helyi és vendégfelület közös ikonrendszert használ.
- Az alakzatszerkesztőben **F** vált formát az aktuális kapacitás és létszám keretein belül. A rács, a sorok szerepe, az üres helyek és a nézési irány látható; a széles rács hosszú karakternevei nem írnak át a szomszédos helyre.

## Ellenőrzés

- Játékfordítás: **0 hiba, 0 figyelmeztetés**.
- Önálló regressziós csomag: **40/40 sikeres**. Kapacitás és támogatások, párhuzamos felvétel, régi/questmentés-migráció, hat tag felszerelésének mentése, mindkét téglalap minden nézési iránnyal és vezérhellyel, üres slot, libasor, előre tervezett forgatás, foglalt/elérhetetlen cél, követő elhelyezése, sorvédelem, hatótáv, sorcsere és lekötésátadás, felkészítés, parancsadatok, ikonok és a panel rajzolási útja.
- Meglévő teljes regressziós csomag: **424/424 sikeres**. A korábban javított `ForestTerrainAmbushPlacementIsStable` továbbra is átmegy; a pályabalansz ehhez az ütemhez nem változott.
- A teljes interaktív helyi/coop játékteszt, a sokféle ajtó és képernyőváltás kampánypróbája, valamint a 6–10. pálya mérései még hátravannak. Az automatizált ellenőrzések nem helyettesítik ezeket.

Futtatás a játék projektkönyvtárából:

```powershell
dotnet run --project Tools/PartyProgression.Tests/PartyProgression.Tests.csproj
dotnet run --project ../Tests/KaoszrubinTests.csproj --no-restore -- --only-failed
```

## Következő ütem

Az ötödik hely látható feloldása, az egyszeri történeti bemutatás és a támogatott fogadói jelöltek bekötése, majd a 6–8. pálya kipróbálása. Ezt követi a hatodik hely és a széles harcrend kampánybeli bekapcsolása, a 9–10. pálya próbája, majd a találkozások és az ellátás végső hangolása. Az új kapacitás alapértelmezett kapcsolója ezekkel együtt kapcsolható be.
