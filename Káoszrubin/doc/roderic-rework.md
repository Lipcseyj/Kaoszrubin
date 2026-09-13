# Dolgozzuk át Roderic küldetéseit

Előfeltételek:
- tisztázni a bossroom-ok működését, mert jobban ki kellesz használni őket az új küldetésekben
- Roderic-et középen kéne elhelyezni, hogy nehogy mire eljutunk hozzá, már a pálya végére érjünk, és ne legyen értelme a küldetésnek, 
  ugyanakkor az elején meg azért nem jó, mert túl sokat kellene visszafelé menni hozzá

Eddig nem volt jól követhető a történet, ráadásul a CACHE_BLOCKED állapot megakasztotta a sztorivonalat.

Az új küldetések:
## Első küldetés: "Egy oldalon harcolunk"
Roderic bizalmatlanságát először úgy tudjuk csökkenteni, hogy bizonyítjuk, hogy közös az ellenségünk.
Feladat: győzz le 8 élőholtat

## Második küldetés: "Az elesettek jelvényei"
A bizalmatlanság csökkenése után Roderic beszél a társairól. Ez a küldetés nagyjából változatlan maradhat.
Feladat: szerezd meg a 3 jelvényt
Megjegyzés: a jelvényeket továbbra is az első 3 legyőzött csontvázlovag átkutatása adja. A konfigurációnak
biztosítania kell hogy az első 3 csontvázlovag akikkel a játékos találkozik ezen a pályán legyen egy bossroomban.
Előfeltétel: legyenek olyan bossroom-ok amik a pálya szélén vannak, és nem akadályozzák a labirintus bejárását. Az ajtajuk csak akkor 
nyitható ki, ha egy bizonyos küldetés aktív. (ez új játékmechanika lesz)
A ghoul-ok egy zárt ajtajú bossroomban lesznek, ami csak akkor nyitható ki, ha a küldetés aktív.

## Harmadik küldetés: "A pátriárkák árnyai"
Ezután Roderic beszél arról, hogy velük tartott a rend 2 pátriárkája, és Roderic szemtanúja volt ahogy borzalmas élőholt teremtmények
elkapták őket. Roderic csatlakozik hozzánk követőként és együtt fel kell kutatnunk a 2 pátriárkát, 
akik Ghoul-á változva formájában vannak jelen egy bossroom-ban egyéb élőholtak társaságában.
A borzalmas igazság feltárása után le kell győznünk a gonosszá vált pátriarkákat.

a győzelem után Roderic elmondja, hogy a pátriárkák egy titkos helyen tartották a rend ereklyéit, és hogy azokat vissza kell szereznünk. 
Feladat: győzz le 2 ghoul-t (csak ez a két ghoul lesz a pályán)
A pátriárkák ez zárt ajtajú bossroomban lesznek, ami csak akkor nyitható ki, ha a küldetés aktív.

## Negyedik küldetés: "A rend ereklyéi"
A rend ereklyéi egy kincsesládában vannak elrejtve. 
Roderic tovább követ minket és együtt meg kell szereznünk a kincsesládát, hogy az ereklyéket visszaszerezzük. 
Roderic motivációja: amíg az ereklyék egy rendtagnál vannak, addig a rend még nem veszett el teljesen.
Feladat: szerezd meg a speciális kincsesládát

A CACHE küldetés mellékszálat meg kell szüntetni, azt felváltja ez a küldetés. Az ereklye ládába bele lehet pakolni a készleteket is.

Előfeltételek: a játékba be kell vezetni, hogy a kincsesládák ne mind ugyanolyanok legyenek, hanem legyenek quest ládák. 
Ezeket csv-ből konfigurálhatóvá kell tenni, a tartalmuk teljesen csv-ből jönnek. 
Azt  is be kell vezetni az új Quest rétegbe,hogy legyen olyan küldetésfeltétel, ami egy bizonyos láda megtalálát írja elő.
Az ereklye-láda is zárt ajtajú bossroomban lesznek, ami csak akkor nyitható ki, ha a küldetés aktív.
A 2.globális boss-t meg kell változtatni, hogy ne ghoul legyen mert a ghoul-ok más szerepet kaptak.

## Negyedik küldetés: "Az esküszegő"
Ezután beszél nekünk csak Roderic Malrec-ről és ha vállaljuk a küldetést, akkor el kell győznünk Malrec-et, aki egy bossroomban lesz
a speciális pályán. A küldetés szövegezését kell legfejebb kicsit alakítani, egyébként változatlan maradhat. 