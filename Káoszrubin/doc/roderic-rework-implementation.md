# Roderic új küldetéssora

Az újonnan létrehozott katakombapályák az öt feladatból álló történetet használják:

1. **Egy oldalon harcolunk** (`NPCQ040`): nyolc élőholt, követőfeltétel nélkül.
2. **Az elesettek jelvényei** (`NPCQ037`): három jelvény, a lezárt mellékszoba
   három csontvázlovagjának átkutatásából.
3. **A pátriárkák árnyai** (`NPCQ038`): két `E061` élőholt pátriárka. Roderic
   követőként, legfeljebb hat mező Manhattan-távolságból vesz részt a harcban.
   Más élőholt nem számít bele. Az első észleléskor Roderic felismeri őket.
4. **A rend ereklyéi** (`NPCQ041`): a `RODERIC_ORDER_RELICS` láda első nyitása.
5. **Az esküszegő** (`NPCQ039`): Sir Malrec legyőzése a külön sírkápolnában,
   Roderic részvételével és a Malrec-párbeszéd után.

A három lezárt mellékszoba az adott küldetéshez kötött ajtón át érhető el.
A pátriárkákból és csontvázlovagokból az ötödik pálya véletlen találkozásai
nem adnak további példányokat. Az ereklyeládát négy zombi őrzi.
Roderic továbbra is a főút középső részén vár; a küldetések opcionálisak.

## Párbeszéd és jutalom

A bizonyítás és a jelvények Rodericnél adhatók le. Miután csatlakozott,
a teljesített történeti cél lezárása és az azt követő párbeszéd a következő
harcon kívüli pillanatban indul. A quest API egyszer osztja ki a jutalmat.
A pátriárkák után az ereklyék következnek; Malrecről csak ezután beszél.
A külön helyszínre utazás előtt a történet `MALREC_READY`, érkezéskor
`MALREC_APPROACH` állapotú, ezért a találkozási párbeszéd nem játszódik le
már a katakombákban. A végleges csatlakozás továbbra is a barátságosságtól függ.

Az ereklyeláda teljes tartalma a CSV-ben van: az Ezüst Eskü nagypecsétje,
4 T012 gyógyital, 2 T004 kenyér, 2 T006 füstölt hús és 4 T002 bőrkulacs.
Ez közös, fix készlet; nem partitagok számával szorzott jutalom. Az arany 0.
A nagypecsét innen szerezhető meg, Malrec nem ad második példányt.
A teli hátizsák nem gátolja a nyitási célt; minden be nem férő tárgy a ládában
marad. A CACHE-választások és a `GrantEmergencySupplies` hatás megszűntek.

Az új pátriárka kezdeti értékei a CSV-ben hangolhatók: 8. szint, 180 HP,
6 páncél, 700 XP, W009 fegyver, Undead tulajdonság.

## Kompatibilitás

A mentésverzió 25, a coop-protokoll 85 (új questazonosító). A felhasználó döntése szerint a régi Roderic-történet
katakombai feladatai átálláskor lezártnak számítanak, új jutalom nélkül.
A már létrehozott pályába nem kerül új szoba, ellenfél vagy láda. Roderic
Malrec felé vezet tovább; a már folyó Malrec-harc és a végső döntések állapota
megmarad. A migráció a régi háromöléses pengepróba mentett progresszét is
az új katalógushoz igazítja, akkor is, ha Roderic már távozott.

A régi `QuestId`-értékek megmaradtak. A név szerinti API új nevei:
`FightingOnTheSameSide`, `PatriarchsShadows`, `OrderRelics`.
Az `OpenQuestChest` mellett a CSV új `KillWithTraits` típusa követő nélküli
tulajdonságalapú ölésszámlálást támogat.

A merge után bukó follower-teszt valódi egyezési hibát jelzett: a kisorsolt
felszerelés megváltoztatja a runtime ellenfél-definíciót. A konkrét célpontú
quest most stabil ellenfél-ID-t hasonlít össze teljes record-egyezés helyett.

## Ellenőrzés

Célzott teszt fedi az öt feladat teljes sorrendjét, az eltérő célpontokat,
a követő részvételét, a teli hátizsákot, az ellenfélkonfigurációt és a régi
mentések migrációját. A meglévő többseedes szobatesztek az új három lezárt
mellékággal is ellenőrzik a kijárat elérhetőségét.
Interaktív játékbeli és külön gépes coop-végigjátszás nem történt.

Build: 0 warning, 0 hiba. A csomag során egyszer futott teljes regresszió:
299 PASS és 3, a megváltozott tartalom miatt elavult elvárás. Ezek javítása
után mindhárom teszt szűrve sikeres; az utolsó kódmódosítások célzott
történeti, mentési, expedíciós és replikációs tesztjei szintén sikeresek.
A teljes futás korábbi durability-tesztjei is sikeresek voltak.
