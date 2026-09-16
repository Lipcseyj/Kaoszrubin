# Questládák – a Roderic-rework harmadik előfeltétele

Az azonosítható questládák tartalma CSV-ből származik. Az első kinyitás teljesíti
a ládához tartozó küldetéscélt akkor is, ha a hátizsák tele van. A fel nem vett
tárgyak a ládában maradnak: visszalépéssel vagy a láda mezőjén átkutatással
később felvehetők. Az arany egyszer vehető fel; jackpot és véletlen extra
zsákmány nem módosítja a konfigurált tartalmat.

## Konfigurálás

A következő példa szemléltetés, nem Roderic végleges ereklyeládája:

```csv
#Quest ládák
Id;Név;Arany
RELIC_EXAMPLE;Példa ereklyeláda;17

#Quest láda tartalom
Id;TárgyId;Darab
RELIC_EXAMPLE;T001;2
RELIC_EXAMPLE;T011;3
```

Az arany nemnegatív, a tárgymennyiség pozitív egész. A tárgyazonosítóknak
létezniük kell a katalógusban. Duplikált ládaazonosító, ugyanazon ládában
ismétlődő tárgysor és ismeretlen hivatkozás betöltési hibát okoz.

A quest CSV-sorának típusa `OpenQuestChest`, célazonosítója a láda ID-ja,
elvárt mennyisége `1`. Ebből `QuestObjective.OpenQuestChest(QuestChestId)`
keletkezik. Más láda nyitása nem teljesíti ezt a célt. Az általános
`OpenChests` célba a questláda első nyitása is beleszámít.

A pályakonfiguráció `QuestChestPlacements` szótára szobaazonosítóhoz rendeli
a ládaazonosítót. A `QuestChestPlacement.Place` szabad belső mezőt választ;
hiányzó szoba, hiányzó definíció, helyhiány és pályán belüli duplikált láda
esetén hibát jelez. A questajtó külön, a meglévő ajtókonfigurációban állítható.

## Futás és mentés

A `QuestChestService.Collect` kezeli az egyszeri nyitási eseményt és a sikeresen
eltárolt tárgyak levonását. A láda üresen is a pályán marad, megváltozott jellel.
Ha az adott pályán már nyitott ládához később aktiválunk questet, a cél azonnal
teljesül. Ez az utólagos ellenőrzés az aktuális pályát vizsgálja; külön,
pályákon átívelő ládanyitási előzményt nem tárol.

A mentésverzió 24: ládaazonosítót, nyitottságot, maradék aranyat és maradék
tárgymennyiségeket tárol. Betöltéskor az üres vagy részleges láda nem töltődik
újra. A korábbi mentések hagyományos ládái változatlan tartalommal migrálódnak.
A coop-protokoll 84: a láda neve, azonosítója, nyitottsága és maradék darabszáma
a világpillanatképpel és annak deltájával is továbbítódik.

## Állapot és ellenőrzés

Öt új teszt fedi a CSV-feloldást és hibás adatokat, a pontos és egyszeri
nyitási célt, a valós hátizsákkapacitást, a részleges/üres mentést és
coop-deltát, valamint a név szerinti szobaelhelyezést.

Teljes solution build: 0 warning, 0 hiba. Teljes tesztcsomag: 293 PASS,
3 FAIL, kizárólag a korábban ismert durability-tesztekben. Ezeket a felhasználó
kérésére nem javítottuk. Interaktív játékbeli és külön gépes coop-próba nem történt.

Az ezt követő [Roderic-rework](roderic-rework-implementation.md) már beköti
a ládatartalmat és a szobaelhelyezést, valamint kiváltja a CACHE-szálat.
