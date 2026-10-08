# Hatfős parti – megvalósítási állapot

2026. október 8. – a harmadik szállítási ütem elkészült: az ötödik partihely és a hozzá tartozó toborzási támogatás már aktív.

## Kapacitás, kampányállapot és mentés

- A technikai felső határ 6. A tényleges csatlakozási korlátot a `Party.Capacity` és az `IsFull` adja. Az alapértelmezett `ExpandedPartyEnabled = true`, `MaximumEnabledCapacity = 5`: új kampányban 4 hely, az 5. sikeresen lezárt főpálya után 5 hely használható. A hatodik hely és jutalma a negyedik ütemig még kikapcsolt; a már megszerzett jogosultság a kampányállapotban megmarad.
- A közös kapacitásszabály az 5. és 8. sikeresen lezárt főpályához köti az új helyeket. A kampány menti a legmagasabb teljesített főpályát, a két támogatás felhasználását és az egyszeri bemutatásokat. Halál, eltávolítás, vezetőváltás és régi pillanatkép visszaállítása nem vonja vissza ezeket; új kampány négy helyről indul.
- A főpálya fogadói lezárása a túlélők jutalmazása és az elesettek eltávolítása után rögzíti a teljesítést. Mellékhelyszín, területváltás és fejlesztői pályaugrás nem old fel új helyet.
- A fogadói, világ-NPC-, egyedi követő-, fejlesztői és coop csatlakozás a közös korláthoz igazodik. A támogatott normál felvétel és a támogatás felhasználása egyetlen partiművelet; sikertelen felvételnél nincs támogatásfogyás. A társcsere is egyetlen művelet, sikertelen normál felvételért nincs fizetés.
- A **37-es mentésformátum** tárolja a kampányállapotot, az explicit alakzatformát és mind a hat slotot. A régi négy hely változatlan 2×2-es beosztásban migrálódik, az új helyek üresek. Későbbi bővítéskor a régi hátsó tagok a menetoszlop középső sorába kerülnek.
- Régi kampánymentésben a 6–8. pálya öt, a 9. vagy későbbi pálya hat hely jogosultságát kapja. Questmentés a felfüggesztett kampányból következtet. Új mentés pályaszámából nincs becslés. A visszatöltés a kapacitásállapotot a taglista helyreállítása előtt alkalmazza.
- A **105-ös coop protokoll** továbbítja a kapacitást, a kampányállapotot, a hathelyes alakzatot és a harci célszemélyeket. A hálózati játékosok számának korlátja változatlan.

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

## Harmadik ütem: feloldás és támogatott fogadói felvétel

- A pályateljesítési képernyő a túlélők XP-je és az elesettek kezelése után bemutatja a megerősített expedíció történetét: Aurelios hálózata elismeri a Kulcshordozókat, és megnyílik az ötödik hely. A bemutatás csak a képernyő elfogadása után lesz elhasználtnak jelölve. Roderic jelenléte és opcionális küldetései nem feltételek.
- Fel nem használt támogatás mellett a fogadó három eltérő kasztú normál zsoldost készít. Ha van hiányzó kaszt a partiban, legalább egy jelölt abból érkezik. Szintjük pontosan `max(1, vezetőszint - 1)`; a normál szintlépési, kasztfejlődési és szinthez igazodó felszerelési szabályokat használják. Kezdő élelem, víz és megfelelő gyógy-/mannaital jár; a Pap és Mágus fókusza és kezdő varázslatai megmaradnak.
- Egy látogatáson belül a jelöltek és normál áraik állandók. Menüváltás és visszatérő expedíció utáni fogadófolytatás nem sorsol újra. Halasztáskor a támogatás megmarad; a következő új fogadólátogatás ismét három támogatott jelöltet készít.
- A host a kiválasztott ajánlat azonosítóját és a fogadói állapot verzióját ellenőrzi. A csatlakozás és a támogatás felhasználása egyetlen partiművelet. Siker után a jelölt kikerül a készletből, a további jelöltek normál áron vehetők fel. Elavult ajánlat, teli parti, érvénytelen cél vagy sikertelen hozzáadás nem fogyaszt támogatást vagy aranyat.
- Teli parti esetén a támogatás megmarad; a meglévő, külön megerősített társcsere normál áron működik. Egyedi NPC és visszavett társ nem használhatja a normál zsoldos támogatását; saját felvételi ára érvényesül.
- A toborzási képernyő külön jelzi a támogatással ingyenes ajánlatot és a megmaradt támogatást. A vezető választási ablaka a vendégek számára megosztott; az ajánlatok, a támogatás, a feloldási történet és a sikeres felvétel tranzakciója a host pillanatképének része. A toborzás továbbra is vezetői döntés.
- Az ötödik társ a korábbi pálya teljesítési XP-jét utólag nem kapja meg. A mentés-visszatöltés megtartja a halasztott támogatást, a felvett ötödik tagot és az elhasznált támogatást; régi kampánypillanatkép nem adja vissza a jutalmat.

## Ellenőrzés

- Játékfordítás: **0 hiba, 0 figyelmeztetés**.
- Önálló regressziós csomag: **55/55 sikeres**. Kapacitás és támogatások, párhuzamos felvétel, régi/questmentés-migráció, hat tag felszerelésének mentése, mindkét téglalap minden nézési iránnyal és vezérhellyel, üres slot, libasor, előre tervezett forgatás, foglalt/elérhetetlen cél, követő elhelyezése, sorvédelem, hatótáv, sorcsere és lekötésátadás, felkészítés, parancsadatok, ikonok és a panel rajzolási útja.
- Meglévő teljes regressziós csomag: **424/424 sikeres**. A korábban javított `ForestTerrainAmbushPlacementIsStable` továbbra is átmegy; a pályabalansz ehhez az ütemhez nem változott.
- A harmadik ütem 15 további próbája ellenőrzi az éles ötös korlátot, a későbbi hatos jogosultság megtartását, mind a hat kaszt támogatott generálását, a hiányzó kaszt garantálását, a halasztást, a normál és egyedi felvételt, a teli partit és társcserét, a jutalmazási sorrendet, a történetet, a mentést és a coop adatokat.
- A 6., 7. és 8. pálya tényleges kampánygenerátorával két-két térképmag mellett minden generált terület kezdőterében ellenőriztük az ötfős menetoszlop elhelyezhetőségét és a harmadik sor harci sugarát. A 6. pálya a csomagolt JSON erdőgráfot használta. A pályakonfiguráció és az encounterek balansza nem változott.
- A teljes interaktív helyi/coop játékteszt, a sokféle ajtó és képernyőváltás végigjátszása, valamint a 6–10. pálya harci és ellátási mérései még hátravannak. Az automatizált geometriai és felvételi ellenőrzések nem helyettesítik ezeket.

Futtatás a játék projektkönyvtárából:

```powershell
dotnet run --project Tools/PartyProgression.Tests/PartyProgression.Tests.csproj
dotnet run --project ../Tests/KaoszrubinTests.csproj --no-restore -- --only-failed
```

## Következő ütem

A negyedik ütemben a hatodik hely és a széles harcrend kampánybeli bekapcsolása következik, a 8. főpálya teljesítéséhez tartozó második támogatással és a 9–10. pálya próbájával. Ezután a teljes mentés/coop játékteszt, valamint a találkozások és az ellátás végső hangolása zárja a tervet.
