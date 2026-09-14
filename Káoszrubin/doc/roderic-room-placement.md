# Roderic rework – szobaelhelyezési előfeltétel

Elkészült: 2026-09-14.

## Konfiguráció

A MazeLevelConfiguration.SpecialRoomPlacements név szerint rendel szabályt
a QuestRoomIds vagy BossRoomIds listában szereplő szobához. Szabály nélkül
megmarad a korábbi, bejárattól mért geometriai távolság szerinti kiosztás.

- MiddleRoute: a szobaközép a bejárat–kijárat út középső harmadához essen.
  A generátor a járathálózat legrövidebb távolságait használja. Ha L a kijárat
  távolsága, S és E pedig a szobaközép távolsága a két végponttól,
  az út menti hely (S − E + L) / 2, a kitérő (S + E − L) / 2.
  A kitérő legfeljebb max(8 mező, L 15%-a). A középhez közeli, kisebb
  kitérővel elérhető szoba élvez elsőbbséget.
- SideBranch: a szoba belsejének és falhatárának kizárásakor a többi
  járható mezőnek és a kijáratnak elérhetőnek kell maradnia a bejárattól.
  A generátor egyetlen ajtót tart meg, a többi nyílást befalazza.
  Ez topológiai mellékágat jelent, nem a térkép külső széle szerinti koordinátát.

A közönséges zárt és zárolt ajtók a távolságmérésben nyitható járatoknak
számítanak. A mellékszobák kialakítása után történik a találkozószoba kiválasztása.
Véletlen tartalom csak a sikeres szobakiosztás után kerül a pályára.

Ha az elrendezés nem felel meg, a generátor új topológiával próbálkozik,
legfeljebb 128 alkalommal. A korlát után hibát jelez; nem ad vissza szabálytalan
pályát. A konstruktor opcionális Random paramétere reprodukálhatóvá teszi a generálást.

## Jelenlegi bekötés és határok

Az 5. pályán RODERIC_MEETING MiddleRoute, RODERIC_INSIGNIA SideBranch.
A meglévő NPC-elhelyezés továbbra is a találkozószoba belsejébe teszi Rodericet.
Más pályák és a Malrec-helyszín nem kaptak új elhelyezési szabályt.

Ez a lépés nem vezet be questfeltételes ajtónyitást, új ellenfeleket,
questládákat vagy történeti változásokat. Régi mentések szobáit nem helyezi át.
A kész szobák és ajtók a meglévő mentési és hálózati reprezentációban maradnak.

## Ellenőrzés

- 80 seed, felváltva 55×31-es és 170×44-es pályákon.
- 40 seed az aktuális 5. pálya beállításaival; 40 seed egy találkozószobával
  és három leendő lezárható boss-szobával.
- Az összes mellékszobát egyszerre kizárva is elérhető a kijárat és minden
  külső járható mező; a találkozószoba megfelel a távolsági feltételeknek.
- Egyetlen ajtó mellékszobánként, véletlen ládák kizárása, seed szerinti
  reprodukálhatóság és hibás konfiguráció elutasítása.
- Teljes solution build: 0 hiba, 0 warning. Teljes tesztkészlet: 287 PASS, 0 FAIL.

Interaktív végigjátszás nem történt. A következő előfeltétel azóta elkészült:
[questfeltételes ajtók](roderic-quest-doors.md), a feltörés és bezúzás
útvonalaira is kiterjesztett hostoldali ellenőrzéssel.
