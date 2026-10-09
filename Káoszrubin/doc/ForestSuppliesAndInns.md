# Erdei ellátmány és fogadói megállók

A 6. és 13. kampánypálya két-két garantált ellátmányládát kap. Mindegyik épületen belüli mellékszobában van, saját miniboss és kíséret védi. A láda az őr életében zárt marad. A miniboss legyőzése után quest nélkül is hozzáférhető, ezért az ellátás nem függ a küldetés előzetes felvételétől. A később felvett küldetés felismeri a már kinyitott ládát.

## Küldetések és őrségek

| Pálya | Hely | Küldetésadó | Őr és kísérete | XP |
|---|---|---|---|---:|
| 6 | Hollók kereszteződése, RAVENS_LOOT_ROOM | Renegát orgyilkos | Holló klánvezér + 4 orgyilkos | 1800 |
| 6 | Elveszett kúriák, ORC_TRIBE_ROOM | Elf kósza a Mohakapuban | Ork raktárnok + 2 ork testőr + 2 ork íjász + ork vérpap | 2200 |
| 13 | A királyi zsilip romjai, SLUICE_SUPPLY_ROOM | Mocsári révész | Zsilip őrkapitány + 3 csontvázlovag + 3 páncélozott zombi | 3400 |
| 13 | Elsüllyedt udvarházak, SUNKEN_COURT_SUPPLY_ROOM | Mocsári révész | Lápi udvarmester + 4 martalóc + 2 káoszmágus tanítvány | 3800 |

A klánvezér gyors, mérgező orgyilkos; a raktárnok erős közelharcos; az őrkapitány élőholt lovag; az udvarmester támogató varázshasználó. Az új ellenfelek minibossok, aranykulcsot nem adnak. A visszatérő expedíció nem teremti újra őket. Az őrségek állandó QUEST-csoportok, a miniboss a vezérük.

A questek egyszer teljesíthetők, a láda első kinyitása teljesíti a céljukat. A küldetésadó a jutalmazás után a pályán marad. A ládák tartalma megtartható, a küldetés leadása nem fogyasztja el.

## Ellátmány

| Láda | Élelem | Víz | Füstölt hús | Gyógyital | Varázsital | Egyéb | Arany |
|---|---:|---:|---:|---|---|---|---:|
| Holló klán | 18 | 24 | 6 | 8 kicsi | 6 kicsi | 4 ellenméreg, 4 kötés | 200 |
| Ork törzs | 24 | 24 | 8 | 8 kicsi | 6 kicsi | 4 betegség elleni szer, 4 fáklya | 150 |
| Királyi zsilip | 30 | 36 | 6 | 12 közepes | 8 közepes | 8 ellenméreg, 6 kötés | 450 |
| Lápi udvar | 36 | 42 | 12 | 12 közepes | 8 közepes | 6 betegség elleni szer, 6 fáklya | 550 |

A mennyiségek darabszámok. A 13. pálya nagyobb készlete hatfős partival számol. A meglévő ládakezelés osztja hátizsákokba a tárgyakat; a csapat a készletet átrendezheti. Ami nem fér el, a ládában marad, és később is felvehető. Az arany és a nyitási questprogress egyszer jár.

## Fogadói megállók

| Pálya | Fogadó | Hely |
|---|---|---|
| 6 | A Fáradt Holló | Hollók kereszteződése |
| 13 | A Száraz Kulacs | A révész szigete |
| 13 | A Rozsdás Korona | Elsüllyedt udvarházak |

A fogadók külön épületet foglalnak el, véletlen őrség és csapda nem kerül beléjük. Ajtóik nyitva vannak. A 13. pálya két fogadója a főút középső és késői szakaszán segít; az északi kitérő a zsilip készletéhez, az udvarházak a második készlethez vezetnek.

A térképen a **♨** jelre kell állni, majd **Enter**. Harc közben nem lehet betérni, és minden élő, jelen lévő társnak legfeljebb nyolc lépésre kell lennie a vezértől. Minden fogadó egyetlen látogatást enged, amelyen belül a meglévő fogadói szolgáltatások használhatók: pihenés és varázslatmemorizálás, lakoma, kereskedő és vajákos, valamint a szokásos mesterek és toborzás. Az alapkészlet a parti feloldott kapacitásával nő.

A kilépő menüpont **Vissza az erdei útra**. A térkép, az aktív terület és a vezér pozíciója megmarad; a meglévő társak a helyükön maradnak, az újonnan felvett társak a közelében állnak fel. A megálló nem teljesíti a pályát, nem ad pályateljesítési XP-t, nem old fel új partihelyet, és nem indít következő pályát. Az időzített felfedezési események a fogadói tartózkodás alatt szünetelnek. Az elhasznált fogadó jele szürke. Az átjárók ugyanezt a nyolcmezős közelségi szabályt használják; a kijárathoz kísért NPC-knél a határ hét mező. A Gyülekező (G) parancs közvetlenül a vezér mellé hívja a társakat, és ilyenkor az utolsó szabad szomszédos mezőt is elfoglalhatják. A vezér Shift+kurzorral helyet cserélhet velük. Normál követéskor a társak továbbra is mozgásteret hagynak.

## Konfiguráció, mentés és hálózat

A fogadók a MazeLevelConfiguration.ForestInns listában adhatók meg szobaazonosítóval, névvel és AreaId-val. A pályaszerkesztőben külön mezőjük van. A generátor egész épületet foglal le, a szobához legalább kilenc szabad belső mezőt követel. Az őrzött ládákhoz a game-data.csv Quest ládák szekciójának opcionális ŐrzőEnemyId oszlopa kapcsol minibosst. A questszobák szabad helyigénye az őrök és a láda darabszámából adódik.

A 6. pálya JSON-gráfjával egyező beépített tartalék a ForbiddenForestGraph.cs. Így hiányzó JSON mellett is megmaradnak a questek és a fogadó célterületei. A 13. pálya tartaléka továbbra is a SunkenCrownsForest.cs.

A 39-es mentésformátum minden területen megőrzi a fogadókat és a felhasznált látogatást. A részlegesen kiürített questláda a meglévő mentési mechanikával őrzi a maradékot. A fogadók a felfedezett térképpel együtt jutnak a többjátékos klienshez; a látogatás és a térképjel színváltozása deltafrissítést kap.

A korábban elmentett térképeket a migráció megőrzi. **Az új ládák, őrségek és fogadók újonnan generált 6. és 13. pályán jelennek meg.** Meglévő mentett pályára nem kerülnek be utólag.

## Ellenőrzés

Tíz-tíz kezdőértékkel ellenőrzött teljes generálás mindkét pályán: bejárhatóság, pontos célterületek, külön fogadóépületek, csapdamentes belsők és teljes ládaőrség. Külön teszt vizsgálja az élő őr miatti zárolást, az egyszeri ládanyitást, a később felvett questet, a minibossok visszatérésének kizárását, a fogadó egyszeri használatát, a mentést, a ködöt és a hálózati deltát. A fogadói készlet és vásárlás öt- és hatfős partival is ellenőrzött, pályateljesítési jutalom nélkül.

A készletek és a minibossok harci nehézsége végigjátszás alapján tovább hangolható.
