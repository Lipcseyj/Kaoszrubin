# A hatfős parti bevezetésének terve

Állapot: megvalósítási javaslat, 2026. október 7. A dokumentum nem módosítja a játék működését.

## 1. Javasolt döntés

Az ötödik partihely az **5. kampánypálya sikeres befejezésekor**, a hatodik a **8. kampánypálya sikeres befejezésekor** nyíljon meg. A feloldás már az adott pálya utáni fogadóban érvényesüljön, így a következő pályára bővített csapattal lehet indulni.

Itt a „szint” a labirintus kampánypályáját jelenti, nem a vezető vagy egy társ karakterszintjét.

| Kampányszakasz | Létszámkorlát | Játékmeneti cél |
|---|---:|---|
| 1–5. pálya | 4 | A kasztok, felszerelések és a jelenlegi 2×2-es alakzat megismerése. |
| Az 5. pálya utáni fogadó; 6–8. pálya | 5 | Egy eddig hiányzó szerep hozzáadása; háromsoros alakzat kipróbálása. |
| A 8. pálya utáni fogadó; 9–22. pálya | 6 | Többféle csapatépítés, két eltérő nagy alakzat és összetettebb találkozások. |

A 8. pálya vége azért jó második mérföldkő, mert három teljes pálya marad az ötfős csapat megszokására. A hatodik társ első nagy próbatétele a 9. pálya, **Az ork haditábor**, ahol már most vegyes ellenfélcsoportok, testőrök és varázshasználók vannak. Ha a játékteszt túl gyorsnak találja a bővítést, a második küszöb adatból a 9. pálya végére tolható; a kiinduló terv azonban 5/8.

A cél: a játékos minden bővítéskor új csapatépítési és helyezkedési döntést kapjon. A több tag természetesen erősíti a partit, és ennek érződnie is kell.

## 2. A jelenlegi rendszerből következő feltételek

A kód és a játékadatok alapján:

- A `Party.MaximumSize` jelenleg 4; a visszatöltés is ehhez vágja a taglistát.
- Hat kaszt van: Harcos, Barbár, Lovag, Tolvaj, Pap és Mágus. A hat hely ezért különösen jól illik a jelenlegi kasztkészlethez.
- Az alakzat négy név szerinti slotot és 2×2-es geometriát használ. Az első/hátsó párokhoz közelharci védelem, hátsó támadás, felkészítés és helycsere kapcsolódik.
- A szűkületeken már működik a nyomvonalat követő libasor és a blokk automatikus visszaállítása.
- Az 5. pálya **A holtak katakombái**, Roderic küldetésszálaival; a 6. **Tiltott Erdő**; a 7. **A nagy csarnokok szintje**; a 8. **A mérgező barlang**.
- Az ideiglenes küldetéskövetők külön résztvevők, és nem fogyasztanak rendes partihelyet.
- A fogadóban jelenleg 1–3 különböző kasztú zsoldos jelenik meg, a vezető szintje körüli ±3 tartományból.
- Harci XP-nél a jóváírt győztes 60%-ot kap, a többi élő tag a maradék 40%-on osztozik. A küldetés-XP egyenlően oszlik meg; a pályateljesítési XP-t minden túlélő teljes összegben megkapja.
- A karakterlap aktuális megvalósítása négy partisort jelenít meg; a fogadó, a vendégfelület és az alakzatszerkesztő is külön ellenőrzést igényel.

Ezért a létszámkonstans megemelése önmagában nem teljes megoldás. A három fő feladat a kampányhoz kötött feloldás, a változó alakzatgeometria és az új társak tényleges használhatósága.

## 3. A feloldás élménye és pontos szabályai

### Az ötödik hely: megerősített expedíció

Az 5. pálya teljesítési képernyőjén rövid történeti üzenet jelenjen meg: a katakombák után Aurelios hálózata elismeri, hogy a Kulcshordozók nagyobb kíséretet vezethetnek. A feloldás legyen jól látható jutalom: **„Új partihely! Mostantól legfeljebb 5 fővel indulhattok.”**

Roderic jelenléte saját megszólalást adhat az eseményhez, de a kapacitás ne függjön az ő túlélésétől, felvételétől vagy opcionális küldetéseitől. A hely szabadon használható zsoldosra, megfelelő egyedi NPC-re vagy coop karakterre.

### A hatodik hely: felkészülés a haditáborra

A 8. pálya után a fogadóban Aurelios ügynöke figyelmeztessen a haditábor szervezett csapataira. Jutalom: **„Új partihely! A Kulcshordozók csapata mostantól 6 fős lehet.”** Ezzel együtt váljon elérhetővé a széles harcrend.

### Tartósság és eseménysorrend

1. A rendes kampánypálya sikeres lezárása frissíti a teljesített kampánymérföldköveket.
2. A meglévő csapat megkapja a pályateljesítési jutalmat; lezárul az elvesztett társak kezelése.
3. Megtörténik a kapacitás feloldása és az egyszeri bemutatás.
4. A fogadó toborzása már az új korlátot használja.
5. Az új társ felszerelhető és a meglévő szabályok szerint felkészíthető a következő pályára. Az előző pálya jutalmát utólag nem kapja meg.

A feloldás kampányállapot: halál, kirúgás, vezetőváltás és korábbi pályára visszatérés nem csökkenti. Egy új kampány újra négy helyről indul. Új formátumú mentésben pusztán egy magasabb pályán állás vagy a fejlesztői pályaugrás nem teljesít mérföldkövet. Mellékküldetés helyszíne, nehézségi szintje és képernyőváltás sem old fel helyet. A küszöbellenőrzés legyen idempotens, és `>=` feltételt használjon, hogy későbbi migráció vagy pótlás is működjön.

## 4. Az új társ felvétele valódi választás legyen

A két feloldáshoz egyszeri **toborzási támogatás** járjon. Ez egy normál zsoldos ingyenes felvételére jogosít; a feloldó fogadóban három különböző kasztú jelölt közül lehet választani.

- Legalább egy jelölt olyan kasztból érkezzen, amely még nincs a csapatban, ha van ilyen.
- A támogatott jelöltek célpontszintje `max(1, vezetőszint − 1)` legyen, a normál karaktergenerálás szintlépési és felszerelési szabályaival.
- Legyen náluk használható alapfelszerelés és kezdő élelem/ital. Pap és Mágus a fogadóban felkészíthető legyen.
- A támogatás csak sikeres felvételkor fogyjon el. Elhalasztott választásnál a következő fogadóban is felhasználható; újrasorsolás ugyanazon látogatáson belül nincs.
- A két támogatás külön azonosítót kapjon, és az egyszer már felhasznált támogatás mentésből vagy visszatérő expedícióval ne váljon újra elérhetővé.
- Egyedi NPC-k saját csatlakozási feltételei megmaradnak. A támogatás normál zsoldosokra szól; a játékos nincs kijelölt történeti karakterhez kötve.

Ez biztosítja, hogy az új hely rögtön kipróbálható legyen, miközben a játékosnak döntenie kell a csapata hiányzó szerepéről. A további toborzás a meglévő árképzéssel működik. Azonos kaszt többször is felvehető; a hat különböző kasztért ne járjon külön automatikus bónusz, amely egyetlen felállásra terelné a játékost.

## 5. Alakzatok: a bővítés legfontosabb taktikai tartalma

A méreteket mindig **szélesség × mélység** értelemben használjuk, a csapat nézési irányához képest.

| Alakzat | Mikortól | Előny | Hátrány |
|---|---|---|---|
| 2×2-es blokk | Kezdettől; legfeljebb 4 tényleges taggal | Ismert, kompakt felállás. | Korlátozott szerepkészlet. |
| 2×3-as menetoszlop | Az ötödik hely feloldásától | Két mező széles; két első harcos mögött több támogató fér el. | Hosszabb, hátulról támadható; a harmadik sor közelharci lehetősége korlátozott. |
| 3×2-es széles harcrend | A hatodik hely feloldásától | Három első harcos és három közvetlen támogató; nyílt termekben erősebb front. | Három mező széles helyet igényel; szűkületben gyakrabban kell libasorra váltani. |
| Libasor | Automatikusan szűkületben | Járható marad a folyosó és a kanyar. | Nincs blokkhoz kötött védelmi előny. |

Öt főnél a 2×3-as rács egyik helye üres. A játékos dönthesse el, hol legyen a rés; ez befolyásolja a védelmet és a felzárkózás rendjét. Hatra feloldott kapacitás, de csak öt tényleges tag esetén a széles harcrend is választható, ugyancsak egy üres hellyel.

Az első változatban alakzatformát és teljes slotbeosztást csak harcon kívül lehessen váltani. Harcban a meglévő forgatás, mozgás, feloszlatás és az akcióba kerülő szomszédos helycsere adjon alkalmazkodási lehetőséget. Így az új szerkesztő nem válik ingyenes harci átrendezéssé.

Példa azonos hat tagra, felfelé néző alakzatban:

```text
        Menetoszlop                 Széles harcrend
             ↑                            ↑
       Harcos   Lovag           Harcos   Lovag   Barbár
       Tolvaj   Barbár          Tolvaj   Pap     Mágus
       Pap      Mágus
```

Ezek példák, nem kaszthoz kötött slotkorlátozások. A támogatók és első harcosok szerepét a felszerelés, a specializáció és a beállított taktika is módosíthatja.

### Védelmi és támadási szabályok

- A védelem a tényleges elhelyezkedésből következzen: a blokkon belüli, közvetlenül előtte álló élő társ adhatja a jelenlegi elölről érkező közelharc elleni védelmet. Az üres hely megszakítja ezt a kapcsolatot; a védelem nem lép át rajta.
- A védelemhez a védőnek a tervezett szomszédos mezőn kell állnia és védelemre alkalmas állapotban kell lennie. A saját közelharci lekötöttség, az oldalról/hátulról érkező támadás és az alakzat felbomlása a meglévő szabályokhoz igazodjon. Az új geometria ne gyengítse le a régi 2×2-es viselkedést.
- A három sor nem ad háromszoros vagy halmozódó védelmi bónuszt. Egy támadásra legfeljebb egy védőkapcsolat alkalmazható.
- A „hátsó sorból támadhat” fegyver és a tolvaj meglévő kivétele csak a közvetlenül előtte álló társ által lekötött, szabályosan elérhető ellenfélre működjön. A harmadik sorból nem lehet automatikusan két társon át döfni.
- Lövés, gyógyítás, varázslat, aura és baráti tűz megtartja saját távolság- és célzási szabályait. A plusz slot nem növeli automatikusan a hatótávot.
- A felkészítés és a biztonságos tárgyhasználat minden megfelelő, első sor mögötti tagra legyen értelmezhető, saját lekötöttségi ellenőrzéssel. A két név szerinti hátsó billentyűparancs helyett célszemély-választás szükséges.
- A jelenlegi első/hátsó helycsere általánosuljon **szomszédos sorok közötti váltásra**, a meglévő akcióköltség megtartásával. A harmadik sorból az elsőbe jutás több lépés, nem ingyenes átugrás.

### Mozgás és forgatás

A meglévő kanyarkövető libasort kell általánosítani hat tagra. A rendszer a két kiválasztott blokkforma valamelyikébe álljon vissza, amikor annak ténylegesen van helye. Az ideiglenes követők a rendes tagok után következzenek, és ne foglalják el a blokk visszaállításának célmezőit.

A 2×3-as és 3×2-es téglalap **90 fokos elfordítása nem fér ugyanabba a lábnyomba**, mint a régi négyzet. Ez külön megvalósítási feladat. Az új célmezőket és az átrendeződés járható útját előre ellenőrizni kell; a tagok nem ugorhatnak falon vagy más élő szereplőn át. Sikertelen fordulásnál a pozíciók és a nézési irány megmaradnak. Szűkületben a játékos a meglévő libasoros haladással kerülhet tágasabb helyre.

## 6. A pályák tanítsák meg az új lehetőségeket

| Pálya | Tanítandó döntés | Tervezett tartalom |
|---|---|---|
| 6. Tiltott Erdő | Összetartás vagy felderítés? | A pálya elején biztonságosabb kipróbálási szakasz; később áttekinthető, oldalirányból közeledő csoportok. A Tolvaj és a több észlelési forrás értéke látszódjon. |
| 7. Nagy csarnokok | Kit kötünk le, kit támadunk először? | Vegyes ellenfélcsapatok: front mögötti támogató, több megközelítési út és választható kerülő. |
| 8. Mérgező barlang | Ki kezeli a csapat állapotait? | Az ellenméreg, gyógyítás és tartalék fogyóeszközök szerepe; rövid szűkületek és utána biztonságos rendeződési tér. |
| 9. Ork haditábor | Menetoszlop vagy széles front? | Tágasabb bejárati próbaharc a három első hellyel; később testőr és sámán/vérpap, legalább két használható megközelítés. |
| 10. pályától | Melyik felállás működik itt? | A szűk és nyílt területek váltakozása, helyezkedésre kényszerítő meglévő varázslatok és összetett csoportok. |

Az első öt- és hatfős próbaharc legyen könnyebben olvasható. Később az ellenfelek összetétele és elhelyezése adja a kihívást: például két közelharcos mögött varázshasználó, vagy távolabbról érkező második csoport. Új hullámrendszer külön fejlesztés lenne; az első változat a meglévő csoportokat és mozgásprofilokat használja.

A veszélyes területet és a várható bekerítést a térkép, a pletyka vagy az észlelhető mozgás jelezze. Az AI a meglévő látási és ellenállás-megfigyelési szabályokat tartsa be; a bővítés ne adjon számára rejtett tudást a partiról.

## 7. Egyensúly, XP és ellátás

### Harci erő

Öt tag legfeljebb 25%-kal, hat legfeljebb 50%-kal több karakterakciót jelent négyhez képest, ha mindenki részt vesz és cselekvőképes. A tényleges előny függ a kasztoktól, manától, távolságtól, bénítástól és a pálya geometriájától; ez nem kész nehézségszorzó.

Első mérési körben a meglévő sebzés-, HP- és bossértékek maradjanak. A 6. és 9. pálya belépő találkozásait a feloldás örömére hangoljuk. A többi kijelölt találkozásnál először támogató ellenfél, eltérő elhelyezés vagy két irányból fenyegető csoport legyen a változtatás. Kerüljük az egész pályára szórt plusz szörnyeket, mert azok a játékidőt és az XP/aranybevételeket is emelik.

A kampány tartalma az adott szakaszban elérhető kapacitásra készüljön. A pillanatnyi élő létszám ne indítson folyamatos automatikus HP-/sebzésskálázást: egy társ halála vagy elküldése nem gyengíti le a világot. A feltöltésre a fogadó adjon megbízható lehetőséget; kisebb csapattal továbbmenni vállalt nehézség.

### Fejlődés

Példa 300 harci XP-re, azonos jóváírt győztessel és mindenkit életben tartva:

| Élő létszám | Győztes | Többi tag fejenként |
|---|---:|---:|
| 4 | 180 | 40 |
| 5 | 180 | 30 |
| 6 | 180 | 24 |

Ez a támogatók lemaradásának kockázata. Az első változatban maradjon a 60/40-es szabály, hogy a létszámbővítés és az XP-reform hatása külön mérhető legyen. A vezetőhöz közeli szintű belépő és a minden túlélőnek teljesen járó pályajutalom biztosítja a kezdő használhatóságot; a küldetésjutalmak további hígulását is mérni kell.

Ha a papok, mágusok vagy későn csatlakozott tagok tartósan lemaradnak, külön összehasonlító tesztben próbáljuk ki az **50/50-es** harci elosztást, a teljes XP-pool növelése nélkül. Ez önálló egyensúlydöntés: a jóváírt győztes fejlődését is lassítja. A szintkülönbség mellett a kasztok eltérő XP-küszöbeit, az XP-szerzési ütemet és a harci használhatóságot is értékelni kell.

### Ellátás és tárgyak

A meglévő karakterenkénti étel-, víz- és fogadói költségek már természetesen növelik a nagyobb csapat fenntartását. Külön általános zsoldadó nem szükséges az első változatban.

A normál fogadói készlet elérhető étel-, víz- és alapvető gyógyító mennyisége igazodjon a feloldott kapacitáshoz. Nagyobb kapacitás esetén az alapkészlet kiinduló szorzója 5/4 vagy 6/4 lehet, felfelé kerekítve; ez kínálat, nem ingyenes juttatás. A ritka mágikus felszerelések kínálata és a bossjutalmak ne nőjenek ugyanilyen automatikus arányban.

A hatfős parti több hátizsákhelyet és nagyobb közös látóteret is kap. Ezek a feloldás jutalmának részei, de a ládák mennyiségét, a csapdaészlelést és a több embert érintő gyógyító/buffhatásokat is ellenőrizni kell. A meglévő hatótáv- és halmozási korlátok maradjanak érvényben.

## 8. Felület és coop

- A fejléc a valódi feloldást mutassa: `Parti: 4/4`, majd `4/5` vagy `5/6`. Az alapértelmezett maximális hatos érték nem megfelelő kijelzés.
- A csapatnézet hat tagot tudjon elérhetővé tenni. Normál ablakméreten minden tag legyen látható; kisebb panelen görgetés és egyértelmű kijelölés működjön.
- A lezárt helyek rövid magyarázatot kapjanak: „Az 5. pálya teljesítése után”, illetve „A 8. pálya teljesítése után”. Az üres, már feloldott hely külön állapot.
- Az alakzatszerkesztő mutassa a tényleges rácsot, a sorok szerepét, az üres helyeket és a nézési irányt. A forma kiválasztásakor az új beosztás előnézete jelenjen meg.
- A harci felkészítés és helycsere célpontválasztót használjon; ne kelljen minden új slothoz új billentyűt megtanulni. A régi gyorsparancsok a 2×2-es elrendezésben megőrizhetők.
- A host számítsa a kapacitást és végezze a feloldást. A vendég ugyanazt a kapacitást, alakzatot és toborzási állapotot kapja meg.
- Minden emberi karakter egy rendes helyet foglal. A hatos parti önmagában nem emeli a támogatott hálózati játékosok számát.
- Teljes parti esetén a coop csatlakozás ne távolítson el automatikusan NPC-t. A jelenlegi tulajdonjogi és jogosultsági szabályok mellett világos hibaüzenet szükséges.
- Az új tag felvétele és a támogatás felhasználása egyetlen hostoldali tranzakció legyen; két egyidejű kérés nem töltheti túl a partit.

## 9. Megvalósítási terv

### A. Kampányállapot és kapacitás

A technikai felső határ legyen 6, az aktuális feloldott kapacitás ettől külön állapot. Közös kapacitásszabály és sikeres csatlakozási művelet kezelje a fogadói toborzást, világ-NPC-t, egyedi küldetéstársat, coop belépést és fejlesztői eszközöket. A `Party.Add` végső ellenőrzése is érvényesítse az aktuális kapacitást; pusztán a felület ellenőrzése nem elég.

Tárolandó: teljesített fő kampánymérföldkövek, a két toborzási támogatás állapota és az egyszeri bemutatások állapota. A kapacitás ezekből származtatható. A feloldási küszöbök egy közös, hangolható konfigurációban legyenek.

Érintett fő területek: `Domain/Characters/Party.cs`, `CharacterRoster.cs`, `Application/InnController.cs`, `Game.World.cs`, `Game.QuestsAndConversations.cs`, `Game.FormationAndInteraction.cs`, a fejlesztői és coop csatlakozási utak.

### B. Alakzatmodell és mozgás

A négy rögzített mezőt váltsa fel slotlista és explicit alakzatforma. Egy slot sorszáma önmagában ne jelentsen mindig első vagy hátsó sort: a sor/oszlop a választott forma geometriájából következzen. A normalizálás minden élő rendes tagot pontosan egyszer tartson meg, duplikált karakterazonosító nélkül.

A követési sorrend, elhelyezés, útkeresés, átjáróváltás, forgatás és libasoros visszaállás közös geometriai API-t használjon. A négyfős blokk működése regressziós viszonyítási alap.

Érintett fő területek: `Domain/Characters/PartyFormation.cs`, `Application/PartyFormationController.cs`, `PartyFormationAssemblyPlanner.cs`, `PartyFormationAssemblyCoordinator.cs`, `PartyMovementController.cs`, `Game.FormationAndInteraction.cs`.

### C. Harci szabályok és AI

A név szerinti első/hátsó párok helyett geometriai védő- és támogatókapcsolatok szükségesek. Ez érinti a védelmet, hátsó közelharcot, felkészítést, tárgyhasználatot, helycserét és az NPC-akcióválasztást. A taktikai résztvevőlista, harci csatlakozási sugarak és kezdő elhelyezés ne hagyják rendszeresen harcon kívül a harmadik sort.

Érintett fő területek: `Combat/BattleEncounter.cs`, `TacticalBattleCoordinator.cs`, `Application/Game.Combat.AI.cs`, `Game.Combat.Resolution.cs`, `Game.Combat.cs`, `SessionContracts.cs`.

### D. Felületek, tartalom, gazdaság

Hat elérhető karakter, formaválasztás, célpontválasztás és kapacitásjelzés a helyi és vendégfelületeken. Ezután egyszeri feloldási történetek, támogatott jelöltek, fogadói alapkészlet és a 6–10. pálya kijelölt találkozásainak hangolása.

Érintett fő területek: `UI/FormationEditor.cs`, `ConsoleRenderer.CharacterSheet.cs`, `ConsoleRenderer.cs`, `CoopGuestScreen.cs`, `BattleCommandPanel.cs`, `Application/SessionSnapshots.cs`, `World/MazeLevelConfiguration.cs`, `Data/game-data.csv` és a történeti szövegek.

### E. Mentés és hálózati kompatibilitás

A mentésformátum új verziója tárolja a kampányfeloldást és az új alakzatot. A jelenlegi formátum 35. A hálózati szerződés változásakor a session-protokoll verzióját is emelni kell; régi és új vendég között a meglévő kézfogás világos verzióhibát adjon.

Régi mentésben a négy alakzati slot eredeti beosztása 2×2-es formában változatlan maradjon; az új helyek üresek. Későbbi bővítéskor a régi hátsó tagok a menetoszlop középső sorába kerüljenek, ne automatikusan két mezővel hátrébb.

A korábbi formátum nem tárolja az új feloldási történetet. Kompatibilitási becslésként a rendes kampány 6–8. pályáján lévő régi mentés öt, a 9. vagy későbbi pályán lévő hat helyet kapjon. Questhelyszíni mentésnél a `SuspendedCampaign` kampányhelyzetéből kell következtetni, nem a küldetés nehézségéből. Ez egyszeri migrációs kedvezmény: régi fejlesztői pályaugrásokat nem lehet biztosan megkülönböztetni a valódi előrehaladástól.

A régi mentésben új támogatás legfeljebb egyszer legyen használható a következő fogadóban, feloldásonként. A történeti visszatérés és a felfüggesztett kampány visszaállítása egy közös, monoton kampányelőrehaladást használjon, hogy régi pillanatkép ne vonhassa vissza az új kapacitást vagy az elköltött támogatást.

### Szállítási sorrend

1. Kapacitásmodell, kampánymérföldkövek és migráció, az új látható feloldás még kikapcsolva.
2. Hat slot, 2×3-as geometria, harci kapcsolatok és felületek; a teljes 2×2-es regresszió ellenőrzése.
3. Az 5. pálya utáni feloldás, toborzási támogatás és a 6–8. pálya kipróbálása.
4. Széles 3×2-es forma, a 8. pálya utáni feloldás és a 9–10. pálya kipróbálása.
5. Mentés/coop teljes ellenőrzése, találkozások és ellátás végső hangolása; csak ezután közös kiadás.

## 10. Elfogadási feltételek és játékteszt

### Funkcionális ellenőrzések

- A 4/5/6 kapacitás mindegyik csatlakozási úton érvényesül; a vezető is egy helyet foglal.
- Az 5. és 8. főpálya befejezése pontosan egyszer old fel, már a következő toborzás előtt. Az 5. pálya közepén és a 8. pálya közepén még nincs új hely.
- Mellékküldetés, képernyőváltás, fogadó ismételt megnyitása és fejlesztői ugrás nem ad új jutalmat.
- Halál, kirúgás, vezetőváltás, mentés-visszatöltés és visszatérő expedíció megtartja a kapacitást és a támogatások állapotát.
- A régi mentésből minden tag és felszerelés megmarad; questhelyszín és felfüggesztett kampány migrációja is helyes.
- Öt-/hatfős alakzat minden irányban átjut ajtón, kanyaron és szűkületen; képernyőváltáskor nem marad hátra társ, és nincs tartós holtpont ideiglenes követővel sem.
- A téglalap forgatása nem teleportál, nem ütközik, és sikertelen átrendeződésnél nem hagy félig módosított állapotot.
- A hiányzó védő, üres középső hely, halott vagy alkalmatlan védő, oldal-/háttámadás, felbomlás és libasor helyesen módosítja a védelmet.
- A harmadik sor nem kap indokolatlan közelharci hatótávot; a több célpontos buff/gyógyítás és a baráti tűz helyes marad.
- A teljes harci és küldetés-XP összege a létszámtól függetlenül megmarad; a pályajutalom továbbra is túlélőnként jár.
- Hat karakter kezelhető helyi és vendégfelületen; párhuzamos felvétel és újracsatlakozás sem tölti túl a partit.

### Mérési terv

A meglévő harci tesztpálya-generátort bővítsük választható 4/5/6 fős felállással és alakzatformával. Azonos térképmag, felszerelési erő és ellenfélkészlet mellett hasonlítsuk össze az alap négyest, az ötöst és a hatost. Külön kampánypróba mérje a fejlődést és az ellátást a 6–10. pályán.

Vizsgálandó csapatok: hat eltérő kaszt; több közelharcos; több varázshasználó; tolvajos felderítőcsapat; egy tagját elvesztett parti; későn csatlakozott támogató; coop emberi karaktert tartalmazó felállás.

Mérjük a játékos által meghozott döntések számát, körszámot és tényleges játékidőt; a kapott sebzést, haláleseteket és fogyóeszköz-/mannaköltséget; a társak aktív részvételét és fejlődési ütemét; az alakzat miatti várakozást és elakadásokat.

Kiinduló cél: a hatfős csapat legalább két eltérő felállása legyen használható, és mindkét nagy alakzatnak legyen helyzeti előnye. A harmadik sor tagjai ne töltsék rendszeresen a köreik többségét hasznos akció nélkül. A játékos által kezelt, egyszerű találkozások időtartama lehetőleg ne nőjön 20%-nál többet; ez előzetes cél, nem mért eredmény. A bonyolult főcsaták lehetnek hosszabbak, ha több valódi döntést adnak.

A bővítés akkor sikeres, ha az ötödik és hatodik társ érkezése érezhető jutalom, és később a csapat összetétele, a terep és az alakzat együtt határozza meg a jó megoldást.
