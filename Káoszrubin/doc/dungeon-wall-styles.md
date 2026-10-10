# Közös dungeon-falstílusok

A falak központi helye: [DungeonWallStyles.cs](../World/DungeonWallStyles.cs).
A katalógus 89 névvel ellátott stílust, 84 különböző jelet tartalmaz.
A csatolt falrajzok teljes használható jelkészlete és a klasszikus tömbök is benne vannak:

Ш≆≇≈≉≊≋≌≍≎≏≐≑≒≓≔≕≖≗≘≙≚≢≣⊟⊠⊡⊏⊐⊑⊒⊓⊔⊞⌸⌺⌻⌼⍁⍂⍝⍓⍔⍯▀▇█▉▊▋▐░▒▓▙▚▛▜▝▞▟■▢▣▤▥▦▧▨▩⠿⡿⢿⣿⬒⬓⬔⬕⬖⬗⬘⬙⬚⯀

## Használat pályakonfigurációban

```csharp
WallStyle = DungeonWallStyles.Catacombs,
```

Név alapján is lekérhető:

```csharp
WallStyle = DungeonWallStyles.Get("Catacombs"),
// Magyar névvel ugyanaz a stílus:
WallStyle = DungeonWallStyles.Get("Katakombakő"),
```

Az elérhető stílusokat a `DungeonWallStyles.All` adja.
A jel és a szín a katalógus adott sorában módosítható. Minden rá hivatkozó, újonnan
generált pálya átveszi a változást a következő fordítás után. A már generált,
elmentett pályák saját jele és színe megmarad.

A `WallStyle` együtt választja ki a jelet és a színt, és elsőbbséget élvez a régi
`WallRune` / `WallColor` mezőkkel szemben. Egyedi falhoz `WallStyle = null` mellett
ezek továbbra is használhatók. A 6. és 13. erdei pálya fáit és terepsablonjait a
dungeon-katalógus nem írja át.

## Választás a MapEditorban

A **Labirintus pálya → Közös falstílus** listában magyar név szerint választható a fal.
A lista név szerint rendezett, gépeléssel kereshető; alatta színes, háromsoros
falminta látható. A közös stílus használatakor az egyedi jel- és színmező inaktív.

A **Pályaadatok mentése** a név szerinti katalógushivatkozást írja a pálya
konfigurációjába. Ezután fordítsd újra a játékot és a pályaszerkesztőt.
Az **Egyedi fal / erdei terep** választás visszaengedi a régi mezőket.
Az ismeretlen egyedi C# falstílus-kifejezések változtatás nélkül megmaradnak.

## Kampánypályák falai

| Pálya | Téma | Stílus | Jel | Szín |
|---|---|---|---|---|
| 1. | Patkányjáratok | Repedezett járatfal | ≆ | DarkGray |
| 2. | Patkányvezér | Régi csatorna | ≓ | DarkCyan |
| 3. | Goblinüregek | Goblin vájat | ⍝ | DarkGreen |
| 4. | Vadállatok odúi | Barlangi szikla | ⬔ | DarkYellow |
| 5. | A holtak katakombái | Katakombakő | ⊟ | Gray |
| 7. | A nagy csarnokok szintje | Oszlopcsarnok | Ш | DarkYellow |
| 8. | A mérgező barlang | Mérgezett üledék | ≋ | DarkCyan |
| 9. | Az ork haditábor | Ork cölöpfal | ⌸ | DarkRed |
| 10. | Az elátkozott sírkamrák | Átkozott sírpecsét | ⊠ | DarkMagenta |
| 11. | Az óriások erődje | Óriás kváderkő | ▩ | Gray |
| 12. | A sárkánykultusz szentélye | Sárkányszentély | ▥ | Red |
| 14. | A rothadó mocsár | Lápi romfal | ≙ | DarkGreen |
| 15. | A pikkelytrón elsüllyedt palotája | Elsüllyedt palotafal | ⊒ | DarkCyan |
| 16. | A vedlő isten temploma | Kígyótemplom | ≗ | DarkYellow |
| 17. | A fojtogató mélyjárat | Sötét mélyszikla | ▉ | DarkGray |
| 18. | A megtört kristálycsarnok | Kristályréteg | ▧ | Cyan |
| 19. | A dermedt mélység | Jégréteg | ▤ | White |
| 20. | Az örökéj vámpírerődje | Vámpírerőd | ⣿ | DarkMagenta |
| 21. | A sárkányok temetője | Csontkő | ⌺ | Gray |
| 22. | A démoni sík: Parázspusztaság | Parázskő | ▚ | DarkRed |
| 23. | A démoni sík: Vértrónus | Vértrón díszköve | ▣ | Red |
| 24. | A káosz szíve | Káosz kristályfala | ⍔ | Magenta |
| 25. | A káosz trónja | Káosz pecsétfala | ⍂ | Magenta |
| Küldetéshelyszín | Sir Malrec sírkápolnája | Sírkápolna | ⌻ | DarkMagenta |

A kampányon túli tartalék pályák a sötét mélyszikla és az átkozott sírpecsét
közül választanak. A katalógus további klasszikus és díszes blokkjai új pályákhoz
is rendelkezésre állnak.

Egy stílus egyetlen, ismételhető falmezőt jelöl, nem többmezős grafikai szövetet.
A falak továbbra is járhatatlanok és takarják a látást. Ha a hullámkő (`≈`)
a pikkelytrón palotájában falnak van választva, a krokodilmedence `∿` jelet
kap, hogy a járható víz és a fal ne keveredjen össze.
