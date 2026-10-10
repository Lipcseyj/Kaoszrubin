# A hüllőbirodalom dungeonjei

A 14. pálya után a **15. A pikkelytrón elsüllyedt palotája**, majd a **16. A vedlő isten temploma** következik. A korábbi 15–23. pályák változatlan sorrendben a 17–25. helyre kerültek. A kampány 25 pályás, a két új dungeon hatfős partival számol.

| Pálya | Méret | Kincsesládák | Arany/láda | Csapdák | Átokesély |
|---|---|---|---|---|---|
| Pikkelytrón | 3–4 széles képernyő | 16–22 | 1000–2000 | 12–18 | 14% |
| Vedlő isten | 4–5 széles képernyő | 18–24 | 1100–2200 | 16–22 | 20% |

A területek névvel jelennek meg. A palota sorrendje: elárasztott kapu → krokodilmedencék → opcionális kaszárnyák → királyi pikkelytrón. A templom: vedlés kapuja → mérgek csarnoka → papi körmenetek termei → opcionális ősi szentélyterület → főoltár. A boss-terem mindig a tényleges utolsó képernyőn van. A templom negyedik képernyőjén négyképernyős változatban az ősi mellékszentély és a főoltár egyaránt jelen van.

A második palotaképernyő külön medenceszobájában két krokodilidomár, hat mocsári krokodil és egy óriáskrokodil vár. A sekély víz járható, 25%-kal növeli a felfedezéskori mozgási késleltetést és egy pont terepi megterhelést ad. Nem zár el termeket vagy átjárókat.

## Két harci kultúra

**A gyíkemberek Pikkelylégiója** területeket és utánpótlási útvonalakat véd. A tömegét a régről ismert portyázók adják: a kapuőrrajokban három-négy pajzsos őr mellett nyolc-tizenkét portyázó áll, a folyosókon hasonló menetoszlopok járnak. Az őrök lassabbak, erős páncélt, vastag bőrt és garantált légiós pajzsot kapnak. A vadászok gyorsabbak, hosszúíjat és közeli pengét használnak. A sámánok gyógyítanak, növelik a csoport védelmét és harci erejét, kőbőrrel védenek vagy jégbilinccsel akasztanak meg. Az idomár az állatkülönítmény vezetője, a krokodilok erejét a tömeg és a természetes zúzócsapás egészíti ki.

Ez a kultúra a hadrend összetételében és a tényleges felszerelésben jelenik meg; a meglévő csoport- és varázsló-AI vezeti. Új pajzsfal- vagy szomszédvédő szabály nem került a harcrendszerbe. A király erős, pajzsos közelharcos, dupla támadással és zúzócsapással.

**A kígyóemberek Vedlés-körmenete** az ellenség mozgását és állapotát támadja. Az íjászok harci íjjal és mérgezett célbalövéssel veszélyesek, a gyors templomőrök dupla támadást használnak. A papok gyógyítják és védik a különítményeket. A méregmágus két célpontot érintő méregigézetet, savnyilat, savas dögvészvihart, lassítást, fekete bilincset és vakítást használ. A főpap az egész őrséget gyógyító véráldozatot, védelmi és támogató litániákat is kap.

A **méregigézet** külön aktív szörnyképesség: hatmezős hatótáv, két célpont, csatánként két használat és négykörös lehűlés. Savas sebzést okoz, a tartós mérgezés ellen 20-as nehézségű Egészség-próba véd. A dögvészvihar savsebzése és lassítása külön hatás: az ellenméreg nem helyettesíti a savvédelmet.

A belső templomterületre egy medúza és egy óriásbaziliszkusz kerül. A negyedik képernyő mellékszentélyében pontosan egy ősi hidra és három óriáskígyó vár. Ezek ritka, erős teremőrök, nem aranykulcsos bossok.

## Új ellenfelek és fokozatos megjelenés

Az erősség az ellenfél 1–5 közötti kategóriája, nem kampánypálya- vagy karakterszint.

| ID | Lény | Erősség | Szerep | Pályák |
|---|---|---:|---|---|
| E118 | Óriáskígyó | 3 | Vastag bőr, szorító támadás, mozgást megakasztó fojtó ölelés | 13–17 |
| E119 | Gyíkember vadász | 2 | Gyors hosszúíjász | 13–16 |
| E120 | Pajzsos gyíkőr | 3 | Lassabb, páncélozott, garantált pajzsos őr | 13–16 |
| E121 | Gyíkember sámán | 3 | Gyógyítás, védelem, erősítés, megakasztás | 14–15 |
| E122 | Krokodilidomár | 3 | Krokodilokkal járó katonai vezető | 13–15 |
| E123 | Gyíkember király | 4 + bossbónusz | A palota aranykulcsos főellenfele | 15 |
| E124 | Kígyóíjász | 3 | Fürge lövész, mérgezett célbalövés | 14–16 |
| E125 | Kígyó templomőr | 3 | Gyors, dupla támadású elit közelharcos | 16 |
| E126 | Méregmágus | 3 | Mérgezés és állapotokat rontó mágia | 16 |
| E127 | Kígyó főpap | 4 + bossbónusz | A templom aranykulcsos főellenfele | 16 |

A 6. pálya portyázói változatlanok. A 13–14. pályán az új népekből kisebb előőrsök jelennek meg, a palotában tömegessé válnak. A templomban kisebb gyíkember-kíséretek maradnak. Az óriáskígyó a következő mélyjáratban is jelen van.

A CSV ellenségszekciójának új, opcionális `PajzsEsély%` oszlopa 0–100 közötti érték. Üresen a korábbi 50%-os esély marad. A pajzsos gyíkőr és a király 100%-ot kap; mentett felszerelés visszaállításánál nincs új sorsolás. Minden új ellenfélhez van konzolos portré.

## Bossok és megbízások

A **Sszar-Kor gyíkember király** a `SCALED_KING_THRONE` questszobában hat pajzsos őrrel, két sámánnal és három vadásszal jelenik meg. A levéltáros adja hozzá **A pikkelytrón zsarnoka** megbízást.

A **Szeth-Issz kígyó főpap** a `SHEDDING_HIGH_ALTAR` questszobában öt templomőrrel, két kígyópappal és három íjásszal jelenik meg. A kultuszszökevény adja hozzá **A vedlő isten hamis hangja** megbízást. A boss-termek külön küldetés felvétele nélkül is megközelíthetők.

A Patkányember és a Ghoul rendes ellenfél marad, bossbónusz és aranykulcs nélkül. A kulcsbossok száma továbbra is tizenkettő. A két új boss saját bemutatkozást, 10–50%-os HP-bónuszt és a fejlesztői boss-teleport céljai között saját helyet kap. Az Ork sámán korábbi boss-szerepe továbbra is megmaradt.

| Pálya | Küldetésadó | Egyszeri feladatok |
|---|---|---|
| 15 | Mocsári révész, kaputerület | Két idomár, hat vadász, hat adag víz |
| 15 | Elsüllyedt udvar levéltárosa, kaputerület | A király, hat pajzsos őr, három kincsesláda |
| 16 | Barlangi alkimista, kaputerület | Két méregmágus, három ellenméreg |
| 16 | Rúnatörő adeptus, második képernyő | Két Rémképrúna, a kijárat felderítése |
| 16 | Kultuszszökevény, kaputerület | A főpap, három kígyópap |

A szükséges Rémképrúnák a második és harmadik templomképernyőn garantáltak. Minden feladat alacsony viszonynál is felvehető, egyszer teljesíthető, és a saját NPC-találkozásához kötött. Mind az öt visszatérő szereplő összesen négy kampánypályán szerepel, így a korábbi 3–5 visszatéréses korlát megmarad. Magas viszonynál további készlet, védőtárgy vagy tekercs és helyi taktikai információ jár.

## Régi mentések

A mentésformátum **43**, a session-protokoll **109**.

A 42-es és korábbi mentések a régi 15–23. pályát 17–25-re helyezik át. A meglévő térkép, felfüggesztett kampány, társak csatlakozási pályája, beszélgetési pályaszám és kampány-előrehaladás megmarad. A külön questhelyszín saját nehézsége nem változik.

A régi Patkányember- és Ghoul-kulcs az új két őrző kulcsává alakul, így egy késői mentésből sem veszhet el a kapunyitás. A még élő régi példány bossbónusza megszűnik, a HP-ja arányosan igazodik a rendes ellenfélhez. Az új bossbemutatók külön megjelenhetnek. A migráció ismételten nem tolja el a számozást.

A két új dungeon akkor érhető el normál továbbhaladással, ha a mentés még a régi 15. pálya előtt jár. Egy már azon túljutott mentés az eredeti helyszínén folytatódik; az új termek és őrségek a frissen generált pályákon jelennek meg.

A bejárhatóságot, mindkét képernyőméretet, a kíséreteket, boss-küldetéseket és medencéket 12–12 generálás ellenőrzi. Külön próba ellenőrzi a tényleges mérgezést, a pajzsot és a mentésmigrációt. A teljes kampány küldetéscéljai és garantált rúnái öt különböző kezdőértékkel futnak. A csaták nehézségét és az ellátmánygazdaságot végigjátszással még hangolni lehet.
