# Játékidő

A kampány az **1. nap 08:00** időpontban kezdődik. Az idő a parti közös állapota, pályaváltás és külön questhelyszín után is folytatódik.

| Esemény | Eltelt játékidő |
| --- | --- |
| 30 másodperc aktív térképi játék, azaz egy felfedezési kör | 1 perc |
| Egy harci kör, taktikai és gyorsharcban is | 1 perc |
| Sikeres tábori vagy fogadói pihenés | 8 óra |
| Menük, személyes és közös ablakok, fogadói ügyintézés, döntésre várakozás | 0 perc |

Az erdei fogadók az eltelt játékidőt követik: kétóránként fokozatos áruutánpótlás, négyóránként egy vándormester érkezése vagy távozása, hatóránként egy normál zsoldos cseréje. A nyolcórás pihenés is számít. Részletek: [ForestSuppliesAndInns.md](ForestSuppliesAndInns.md).

Harcban a megkezdett kör egyszer számít; a résztvevők és az akciók száma nem növeli külön az időt. Visszavonulás és döntetlen után is megmarad az eltelt idő. A sikertelen pihenési próbálkozás, illetve a pihenés visszaigazolása nem ad újabb nyolc órát.

Éjfélkor nő a napszám. **06:00–17:59 nappal (☀️), 18:00–05:59 éjszaka (🌙).** A napszakjelzés a későbbi mechanikák alapja; jelenleg az órához kapcsolódik, az éhség, a látótáv és az ellenfelek működése a meglévő szabályokat követi. A nyolcórás pihenés a naptárban ugrás, nem több száz állapothatás-kör lefuttatása.

## Kijelzés

A karakterlap felső sorában balra a ▦ jel és a pályaszám, középre igazítva a nap, óra, perc és napszak szerepel. Az idő két oldalán │ választóvonal különíti el a pályaszámtól és a jobb szélen állandó helyet kapó homokórától. Keskeny panelen a választóvonalakon belüli szóközök elmaradnak, szükség esetén a nap jelölése rövidül. Az aranykulcsok az **Arany** sorának második oszlopában jelennek meg; a fogadói vásárlások és eladások frissítése is megtartja őket.

## Mentés és közös játék

A 41-es játékmentés a teljes eltelt percet és a megkezdett térképi kör hátralévő milliszekundumait tárolja. A betöltés, a harc és a fogadói szünet nem fogyasztja el ezt a hátralévő időt. A felfüggesztett kampány világának visszaállítása nem tekeri vissza a közben eltelt játékidőt.

Régebbi mentések az 1. nap 08:00 időpontról indulnak, mert a korábbi eltelt idő nem állapítható meg belőlük. A host vezeti az órát; teljes snapshot és delta is továbbítja a vendégeknek. A hálózati protokoll verziója 107, ezért a közös játék résztvevői azonos frissített játékverziót használjanak.

A későbbi időarány és pihenési idő a GameTimeClock.MinutesPerRound, illetve GameTimeClock.RestHours értékkel módosítható.
