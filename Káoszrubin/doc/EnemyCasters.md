EnemySpellcastingService alapján az ellenséges varázslók már egész komoly AI-t kapnak: nem egyszerűen véletlenszerűen elsütnek egy varázslatot, hanem mana, cooldown, célpont, varázslóstílus és várható hasznosság alapján választanak. Van viszont néhány fontos aszimmetria és egy-két gyanús pont is.
Az alap működés így néz ki: az ellenség csak akkor próbál varázsolni, ha van SpellcasterProfile-ja, van legalább egy ellenséges karakter, és átmegy a CastingChancePercent dobáson. Ezután végignézi a profilban felsorolt varázslatokat, kiszűri azt, amihez nincs elég mana vagy cooldownon van, pontozza a maradékot, majd a legjobb pontszámút választja. A pontszámot még ±15%-kal randomizálja is, tehát nem mindig determinisztikusan ugyanazt lövi. Beillesztett szöveg.txtTXT

Különösen tetszik benne a mana reserve:
var reserve = profile.MaximumMana * profile.ManaReservePercent / 100;
...
var urgent = plan.Score >= 140;
if (!urgent && caster.CurrentMana - spell.ManaCost < reserve)
    continue;
Vagyis nem égeti el automatikusan az összes manáját. Ha viszont egy varázslat elég fontos (Score >= 140), akkor hozzányúlhat a tartalékhoz. Beillesztett szöveg.txtTXT

A varázsló stílusa is ténylegesen számít. Az Artillery +25%-ot ad a sebző varázslatok értékelésére, a Controller +30%-ot a kontrollokra, a Support +30%-ot a támogató varázslatokra, a BattleMage +20%-ot a saját magára rakott varázslatokra, a Necromancer pedig +15%-ot a sebzésre és kontrollra. Beillesztett szöveg.txtTXT
A célpontválasztás sem buta. Egycélpontos támadó varázslatnál először a legalacsonyabb aktuális HP-jú karaktert célozza; ha többen ugyanott állnak HP-ban, akkor a magasabb intelligenciájút preferálja:
.OrderBy(item => item.Character.CurrentVitality)
.ThenByDescending(item => item.Character.EffectiveAbilities.Intelligence)
Tehát van benne egyfajta „végezd ki a sebesültet, illetve veszélyes mágust” logika. Beillesztett szöveg.txtTXT
Területi varázslatnál azt a célpontot keresi, amely körül a legtöbb partitagnak jutna a hatásból. Gyógyításnál pedig a legrosszabb HP%-on álló szövetségest választja, buffnál pedig lehetőleg olyat, akin még nincs rajta az adott hatás. Beillesztett szöveg.txtTXT

A sebző varázslatoknál az ellenséges mágus ereje így készül:
dice
+ effect.Value
+ Intelligence * IntelligenceMultiplier
+ StrengthTier * LevelMultiplier
Tehát itt fontos: nem az ellenség tényleges szintje kerül a LevelMultiplier mögé, hanem a StrengthTier. Ez lehet szándékos, csak érdemes tudni róla. Beillesztett szöveg.txtTXT
A játékosok elleni mágikus támadás és mentő pedig jelenleg teljesen Dexterity-alapú.

Támadó dobás:
d20 + enemy Intelligence
    vs
11 + player Dexterity
Mentődobás:
d20 + player Dexterity
    vs
10 + enemy Intelligence / 2 + spell.Level
SaveHalf esetén sikeres mentő felezi a sebzést, SaveNegates esetén nullázza. Beillesztett szöveg.txtTXT
És itt kapcsolódik az előző kérdésedhez egy nagyon fontos dolog:
Az ellenséges varázslatok ellen ebben a kódban nincs MagicResistance
Legalábbis ebben a service-ben sehol nem látok olyat, hogy a játékos valamiféle MagicResistance értéke beszámítana. A mentő kizárólag:
d20 + Dexterity

A DC pedig:
10 + ellenséges INT / 2 + varázslatszint. Beillesztett szöveg.txtTXT
Ez tehát jelenleg erősen aszimmetrikus:
	Játékos → szörny	Szörny → játékos
Mágikus támadás	INT vs Speed	INT vs Dexterity
Mentő	Speed vs INT-alapú DC	Dexterity vs INT-alapú DC
MagicResistance	✅ sebzést csökkent	❌ ebben nincs
SaveHalf	✅	✅
SaveNegates	✅	✅


Van viszont még egy apróbb, szerintem valószínű hiba is. Az általános nem-sebző effekt felrakásánál ezt használod:
private bool EffectSucceeds(...)
    => resolution switch
    {
        SpellResolution.Attack => ...,
        SpellResolution.SaveNegates => !Resists(...),
        _ => true
    };

Ez azt jelenti, hogy SaveHalf esetén egy nem-sebző effekt automatikusan sikerül, mert beleesik az _ => true ágba. Beillesztett szöveg.txtTXT
Ez például egy SpeedPenalty, SkipAlternate, Burning, Storm stb. esetén érdekes lehet. Az aktív effektek nagy része ezen az úton kerül rá a karakterre. Beillesztett szöveg.txtTXT
A sebzésnél ez nem probléma, mert ott külön a ResolveDamage() kezeli a SaveHalf-ot. De állapotjellegű hatásnál a SaveHalf jelenleg nem csinál semmit a sikerességgel.
Még egy különbség: az ellenséges spell attacknál nincs természetes 1/20 szabály és kritikus varázslat sem. Egyszerűen:
d20 + intelligence < 11 + Dexterity
esetén 0 sebzés, különben teljes sebzés. Beillesztett szöveg.txtTXT
Összességében az AI-része kifejezetten fejlett, de a játékos és az ellenség varázsrendszere jelenleg nem teljesen szimmetrikus. A három dolog, amit én különösen átnéznék, az: MagicResistance a játékos oldalon, SaveHalf viselkedése nem-sebző effekteknél, illetve hogy a StrengthTier valóban szándékosan helyettesíti-e a caster levelt