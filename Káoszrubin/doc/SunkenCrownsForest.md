# Süllyedt koronák lápvidéke

A kampány új 13. pályája, a sárkánykultusz szentélye és A rothadó mocsár között. Tizenkét, egyenként 170×44 mezős terület: nagyobb a Tiltott Erdő nyolc területből álló JSON-gráfjánál. A tizennégy kapcsolat három hurkot ad, ezért a mellékágakról több úton is vissza lehet térni a főútvonalra.

## Terep és útvonalak

A főút nyugatról keletre vezet: **A nádas kapuja → Fuldokló erdő → A révész szigete → Koronák töltése → Elsüllyedt udvarházak → A vízbe fúlt trón**. A kijárat a trón területén van. A révész a szigetén ismerteti az útvonalakat.

| Területazonosító | Név | Terep és szerep |
|---|---|---|
| REED_GATE | A nádas kapuja | Nádas, mocsárfoltok, szélesebb induló ösvény, egy kunyhó. |
| DROWNED_WOOD | Fuldokló erdő | Elárasztott erdő, tavak, házak; druida vezette állatcsapatok. |
| FERRY_ISLAND | A révész szigete | Tavakkal körülvett ligetek, kunyhók; északi és déli elágazás. |
| CROWN_CAUSEWAY | Koronák töltése | Hárommezős ösvény, romok; kultista és orgyilkos járőrök. |
| SUNKEN_COURT | Elsüllyedt udvarházak | Három–négy kúria, sok szoba és víz; papok, tanítványok és martalócok őrségei. |
| DROWNED_THRONE | A vízbe fúlt trón | Két–három kúria, tavak; kis kígyópap–hüllő előőrs és kultista őrség, kijárat. |
| LEECH_MIRE | Piócák ingoványa | Tíz–tizennégy nagy mocsárfolt, épület nélkül; piócák, viperák, ogre–goblin hordák. |
| BLACK_MIRROR | Fekete tükör | Feketevizű tavak, nagy lápfoltok, épület nélkül; lidércek és zombik. |
| REED_LABYRINTH | Nádrengeteg | Sűrű aljnövényzet, kanyargó keskeny ösvények; rejtőző gyíkember–vipera csapatok. |
| WITCH_GROVE | Boszorkányok ligete | Sűrű, elárasztott őserdő, kunyhók; druidák, lápi lidércek és varangyok. |
| CROCODILE_LAKES | Krokodilok tavai | Hét–tíz tó, mocsaras partszegélyek; egy óriáskrokodil kisebb krokodilokkal. |
| OLD_SLUICE | A királyi zsilip romjai | Romos kúriák és zsiliptavak; nekromanták, páncélozott zombik és csontvázlovagok. |

Az északi hurok a révész szigetét köti vissza a töltéshez a krokodiltavakon és a zsilipen át. Délen a Fuldokló erdő–ingovány–Fekete tükör–révész szigete egy kisebb hurok; a hosszabb déli út a Fekete tükörtől a Nádrengetegen és a boszorkányligeten keresztül az udvarházakig vezet.

A tavak nem járhatók, a láp lassít és leshelyet biztosít. A száraz tisztások, járható nádasok és az ösvények minden területen elérhetők maradnak. A romok falai mohás fát, sötét követ és régi udvarházi falazatot használnak; nyitott, zárt és kulccsal zárt ajtók vegyesen keletkeznek.

## Találkozások és kincsek

A gyenge fauna adja a tömeget: piócák, viperák, mérges varangyok, savanyálkák és kisebb krokodilok. A vándorló pióca–vipera csapatok 16–23 tagúak; a mocsári varangy–vipera csoportok lesből támadnak. Az ogrék goblinokat vezetnek, a druidák állatokat és lidérceket támogatnak. A veszélyes romőrségek kisebbek, viszont papot, nekromantát vagy tanítványt kapnak. A hüllőnépek egyelőre néhány előőrssel jelennek meg.

A konfiguráció 96–120 szobát/tisztást és összesen 28–36 ládát kér, ládánként 650–1400 arannyal. A tervezett átlagos ládaarany 32 800 a teljes, tizenkét területes pályára; a tárgyátok esélye 18%. Az ellenfelek felszerelése és további zsákmánya a meglévő rendszert követi.

Boss és új questszoba most nem került ide. A Vízbe fúlt trón épületei a későbbi questhez kötött boss számára is használhatók lesznek. Az óriáskrokodil jelenleg rendes vezérellenfél.

## Szerkesztés és mentések

A terep szerkeszthető fájlja: [ForestLevelGraphs/level-13.json](../ForestLevelGraphs/level-13.json). Ugyanazt az 1-es dokumentumsémát használja, mint a 6. pálya. A tizenkét terület beépített sablonokat és helyi felülírásokat használ. A sablonok öröklött értékei mellett a közös erdei paraméterek és a találkozások a [MazeLevelConfiguration.cs](../World/MazeLevelConfiguration.cs) fájlban vannak.

A [SunkenCrownsForest.cs](../World/SunkenCrownsForest.cs) ugyanennek a gráfnak a beépített tartaléka. Hiányzó vagy hibás JSON esetén is léteznek a találkozások stabil területazonosítói. A két változat induló egyezését teszt ellenőrzi. Későbbi JSON-szerkesztéskor a találkozások által használt azonosítókat meg kell tartani, vagy a találkozásokat is hozzá kell igazítani. A projekt minden ForestLevelGraphs/*.json fájlt a játék mellé másol.

A régi 13–22. pályák most 14–23. számon érhetők el, a kampányhatár 23. Az NPC-találkozások, látásmódosítók, csapdaküszöbök, ellenséges mágikus fegyverek, a magas mágikus questjutalmak és a fogadói prémiumkészlet későbbi küszöbei követik a számozást.

A 38-as mentésformátum automatikusan átvezeti a kampánypályát, az aktuális nehézséget, a felfüggesztett kampányt, a társak csatlakozását, a karakterek kampányhivatkozásait és a teljesített pályák számát. A külön questhelyszínek saját azonosítója és nehézsége megmarad. A régi térképet és rajta a szereplőket a migráció megőrzi; aki már a korábbi 13. vagy későbbi pályán állt, ugyanott folytatja az új számozásban.

## Ellenőrzés

Öt különböző kezdőértékkel minden terület bejárható volt. Az új pályán **750–865 ellenfél, 20–22 varázshasználó, 26 szörnytípus és 28–35 láda** keletkezett. Minden területen legalább tizenkét ellenfél van. A célzott vezérek a kijelölt területen, a kúriaőrségek épületbelsőben jelennek meg; a mocsári lesben állók megfelelő terepen vannak.

A teljes 8–23. generálási ellenőrzés 80 pályát vizsgál. A külön tesztek ellenőrzik a JSON betöltését, a tartalék gráfot és a régi/felfüggesztett mentések egyszeri átvezetését. Ezek az elhelyezés helyességét bizonyítják; a harci nehézséget és a tizenkét terület végigjátszási idejét játék közben még hangolni kell.
