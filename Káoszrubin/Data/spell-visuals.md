# Varázslatok megjelenése

A `game-data.csv` arkán és papi varázslatainál a `CsakEllenség` után három opcionális mező szabályozza a mintát és a tartós vihart:

| Mező | Választható értékek | Alapérték |
| --- | --- | --- |
| `BecsapódásMinta` | `Ripple`, `Flash`, `Bolt`, `Pillars`, `FallingFlames`, `Halo`, `Ward`, `Vortex` | `Ripple` |
| `ViharMinta` | `Drift`, `Embers`, `Rain`, `Crackle` | `Drift` |
| `ViharSzín` | `Red`, `Blue`, `YellowBrown`, `Purple`, `SicklyGreen`, `Shadow`, `BloodRed` | A `BecsapódásSzín` értéke |

A mintát vagy a színt át lehet másolni egy másik varázslat sorába kódmódosítás nélkül. A `ViharMinta` és `ViharSzín` csak akkor látható, ha a varázslathoz `Storm` hatás is tartozik. A `Halo` és `Ward` mintájú, aktív jótékony varázslat a védett szereplő mezőjén tartós színes hátteret is ad.

Példa: a Lángörvény `Vortex` becsapódást, `Embers` vihart és az alap piros becsapódástól eltérő `YellowBrown` viharszínt használ.
