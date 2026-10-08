# Hatfős parti – megvalósítási állapot

2026. október 8. – az első szállítási ütem elkészült.

## Elkészült: kapacitásmodell, kampányállapot és migráció

- A `Party.MaximumSize` technikai felső határ 6. A tényleges csatlakozási korlátot a `Party.Capacity` és az `IsFull` adja; ez alapból továbbra is 4.
- A `PartyCapacityRules` közösen tartalmazza az 5. és 8. sikeresen lezárt főpályához kötött küszöböket. Az `ExpandedPartyEnabled` jelenleg kikapcsolt: az alakzat és a harci kezelés bővítéséig az új helyek és jutalmak nem használhatók.
- A kampány a legmagasabb sikeresen teljesített főpályát, a két támogatás felhasználását és a két feloldás egyszeri bemutatását menti. Az ismételt vagy régebbi teljesítés, halál, társeltávolítás és vezetőváltás nem vonja vissza a haladást. Új kampány négy helyről indul.
- A főpálya fogadói lezárása az aktuális túlélők jutalmazása és az elesettek eltávolítása után rögzíti a teljesítést. A végső kampányzárás is rögzíti a teljesítést. Mellékhelyszín, területváltás, visszatérő expedíció és fejlesztői pályaugrás nem növeli ezt az állapotot.
- A fogadói, világ-NPC-, egyedi követő-, fejlesztői és coop csatlakozás a közös aktuális korláthoz igazodik; a `Party.Add` a végső ellenőrzés. Azonos karakterazonosító nem foglalhat két helyet.
- A támogatott normál felvételhez elkészült az egy műveletben végzett hozzáadás és támogatásfelhasználás. Teli parti, duplikált tag vagy történeti NPC esetén a támogatás nem fogy el. A normál fogadói felvétel ellenőrzi a hozzáadás eredményét, a társcsere egyetlen partiművelet, és sikertelen csatlakozásért nincs fizetés.
- A 36-os mentésformátum tárolja az új kampányállapotot. Régi kampánymentésben a 6–8. pálya öt, a 9. vagy későbbi pálya hat hely jogosultságát kapja. Questmentés a felfüggesztett kampányból következtet; a quest nehézsége nem számít. Új mentésben a pályaszám alapján nincs becslés.
- A visszatöltés a kapacitásállapotot a taglista helyreállítása előtt alkalmazza. A felfüggesztett kampánypillanatkép visszaállítása monoton összefésüléssel megőrzi az elhasznált támogatásokat és a bemutatásokat. Az eredeti négy alakzati slot változatlan.
- A 103-as coop protokoll a host kapacitását és kampányállapotát továbbítja. A fogadó helyi és vendégfejléce az aktuális kapacitást mutatja.

## Ellenőrzés

- A játék fordítása: sikeres, 0 hiba, 0 figyelmeztetés.
- Az új, önálló regressziós csomag: **19/19 sikeres**. Kapacitáshatárok, egyszeri jutalmak, párhuzamos támogatásfelhasználás és coop belépés, mentési roundtrip hat taggal és felszereléssel, régi/questmentés-migráció, monoton kampányállapot, jutalmazási sorrend, hálózati adatok és fogadói kapacitásjelzés.
- A meglévő teljes csomag: **424/424 sikeres**. A `ForestTerrainAmbushPlacementIsStable` elavult, pontos balanszszámokat elváró feltétele javítva: a rajtaütések meglétét, célterepét és pozitív kiváltási távolságát ellenőrzi. A terepre helyezés tényleges generálási próbái továbbra is ellenőrzik a csoportlétszámot, a rejtőzést, a távolságot és a csoportazonosítót. A pályabalansz változatlan.

Futtatás a játék projektkönyvtárából:

```powershell
dotnet run --project Tools/PartyProgression.Tests/PartyProgression.Tests.csproj
dotnet run --project ../Tests/KaoszrubinTests.csproj --no-restore -- --only-failed
```

## Következő ütem

Hat alakzati slot, 2×3-as geometria, közvetlen sorok közti harci kapcsolatok és a hat karaktert kezelő felület, a meglévő 2×2-es viselkedés megőrzésével. A partistátusz egy sorral lejjebb kerül; az alakzat jelzése alatta kap helyet, a sorokat és karakterhelyeket ábrázoló kis ikonrendszerrel, a terv kiegészítése szerint.

Ezt követi az ötödik hely látható megnyitása, a támogatott fogadói jelöltek és egyszeri történeti bemutatás; majd a 3×2-es széles harcrend és a hatodik hely bekapcsolása. A jelöltgenerálás, a toborzási támogatások fogadói felülete, az alakzatváltás és forgatás, a több soros harci kezelés, a karakterlap átrendezése és a pályák gazdasági/tartalmi hangolása még nincs megvalósítva.
