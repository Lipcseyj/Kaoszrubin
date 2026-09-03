using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Characters;

namespace KaoszRubin.Data;

public sealed record BossNarrative(string ChapterTitle, IReadOnlyList<string> Speech);

public static class StoryNarratives
{
    public static readonly IReadOnlyList<string> CampaignIntroduction =
    [
        "Az Aranykor hajnalán négy ősi elementálmágus őrizte a világ egyensúlyát: Pyranthos, a Lángok Atyja; Nymara, a Mélytengerek Asszonya; Goram, a Hegyek Szíve; és Zephyriel, az Ég Vándora. Együtt alkották meg a Káoszrubint, amelyben tűz, víz, föld és szél ereje egyetlen, lüktető drágakővé forrt.",
        "A rubin hatalma azonban nagyobbnak bizonyult alkotói bölcsességénél. A négy szövetséges egymás ellen fordult, palotáik elégtek, tengereik felforrtak, hegyeik meghasadtak. A végső összecsapásban Zephyriel ragadta magához a követ, és mielőtt társai elérhették volna, egy másik dimenzióba rejtette: a folyton változó Káoszlabirintusba.",
        "Zephyriel tizenkét aranylakatot kovácsolt a dimenzió kapujára. Mindegyikhez egyetlen aranykulcs tartozik, s azokat a labirintus legfélelmetesebb őrzőire bízta. Aki mind a tizenkettőt megszerzi, megnyithatja a Rubin Útját — és kezébe veheti azt a hatalmat, amely birodalmakat emelhet fel vagy törölhet el.",
        "Most Vhar-Zul, a Sötét Úr is a Káoszrubint keresi. Árnyékhadseregei már áttörték a dimenzió peremét. Ha ő ér előbb a kőhöz, nem marad királyság, amely ellenállhatna neki.",
        "Aurelios Máguskirály ezért hívatott benneteket a Csillagtoronyba. Jósai, a Csillagszeműek ugyanazt a jelet látták mind a hét éjszakai égen: tizenkét aranyfény között a ti alakotok állt. A jóslat szerint ti vagytok a Kulcshordozók — az egyetlenek, akik végigjárhatják a kaotikus szinteket anélkül, hogy a dimenzió elnyelné őket.",
        "Nem indultok teljesen egyedül. Aurelios ügynököket küldött elétek: kereskedőket, mestereket, gyógyítókat és titkok tudóit. A Káoszlabirintus fogadóiban várnak majd rátok, ahol a világok közötti vihar rövid időre elcsendesedik.",
        "Gyűjtsétek össze a tizenkét aranykulcsot. Előzzétek meg Vhar-Zult. Találjátok meg a Káoszrubint — és amikor eljön az idő, döntsétek el, méltó volt-e Aurelios bizalma."
    ];

    public static readonly IReadOnlyList<string> TwelveKeysStory =
    [
        "A tizenkettedik boss elbukik. Az utolsó aranykulcs a levegőbe emelkedik, és társai felelnek hívására: tizenkét fénypont kering körülöttetek, akár egy aranyból rajzolt csillagkép.",
        "A kulcsok egyszerre fordulnak el láthatatlan zárakban. A Káoszlabirintus megrázkódik. Távoli falak omlanak le, eddig nem létező lépcsők nőnek ki a semmiből, és a mélységből olyan harangszó kondul, amelyet nem füllel, hanem a csontjaitokban hallotok.",
        "Ekkor megjelenik előttetek Aurelios Máguskirály áttetsző képmása. „Beteljesítettétek a Csillagszeműek első jóslatát” — mondja. — „De a kulcsok csak a külső pecséteket törték fel. Zephyriel a Rubinhoz vezető utat huszonegy halálos szint mögé rejtette. Ami mögöttetek van, próba volt. Ami előttetek áll, háború.”",
        "A látomás mögött egy másik alak is kirajzolódik: Vhar-Zul fekete koronája, majd két parázsló szem. A Sötét Úr nevetése végigviharzik a dimenzión. Ő is megérezte a zárak felnyílását — és most már pontosan tudja, merre vezet a Rubin Útja.",
        "A tizenkét kulcs egyetlen ragyogó pecsétté olvad a parti előtt. A kapu túloldalán huszonegy új, brutális világ vár, mind közelebb a Káoszrubin lüktető fényéhez. A küldetés első célja teljesült. A valódi verseny most kezdődik."
    ];

    public static readonly IReadOnlyDictionary<string, BossNarrative> BossNarratives =
        new Dictionary<string, BossNarrative>(StringComparer.OrdinalIgnoreCase)
        {
            [MonsterIds.Patkányember] = new("II. fejezet — A csatornák koronája",
            [
                "Rikkancs vagyok, a Patkányjáratok királya! Ne nevess a koronámon — tizenkét kanálból hajlítottam, és mindet becsületesen loptam.",
                "Ezt a fényes kulcsot egy kék köpenyes, szélből szőtt ember dobta a fészkembe. Azt mondta, őrizzem, amíg a falak énekelni nem kezdenek. A falak sosem énekelnek. Csak a patkányok. Főleg éjjel.",
                "Ha a csontzabáló Morghult keresitek, vigyetek neki sót. Utálja a sót. Én meg Morghult utálom, mert megette három unokatestvéremet — bár az egyik talán csak elköltözött."
            ]),
            [MonsterIds.Ghoul] = new("III. fejezet — Morghul lakomája",
            [
                "Morghulnak hívtak, amikor még emlékeztem a saját arcomra. Most a katakombák neveznek el minden éjjel újra, amikor a koporsófedelek alatt megfordulnak a holtak.",
                "A kulcs nem étel. Megpróbáltam. Nem hús, nem csont, még csak nem is sikolt. De amikor a markomban tartom, tizenkét dobbanást hallok a föld mélyéről. A tizenkettedik után mindig hideg szél fúj végig a sírokon.",
                "Grashka, az ork sámán azt állítja, tudja, milyen ajtót nyit. Hazudik. Grashka mindig hazudik. Egyszer azt mondta, a koponya nem levesestál. Ostoba ork."
            ]),
            [MonsterIds.OrkSámán] = new("IV. fejezet — Grashka füstjóslata",
            [
                "Én vagyok Grashka, a Vasagyar törzs füstlátója. A többiek azt hiszik, a szellemek beszélnek hozzám. Valójában többnyire a füst beszél, és annak is rettenetes a memóriája.",
                "Az aranykulcsot álmomban kaptam egy négyarcú vihartól. Négy hang veszekedett benne: láng, hullám, kő és szél. A szél győzött, de úgy remegett, mint aki tudja, hogy egyszer visszajönnek érte.",
                "Északon Hrold, a fagyóriás vár. Fél a vörös szárnyaktól, bár ezt sosem vallaná be. Ha találkoztok vele, mondjátok meg, hogy Grashka szerint a szakálla csak ráfagyott kecskeszőr."
            ]),
            [MonsterIds.Fagyóriás] = new("V. fejezet — Hrold dermedt esküje",
            [
                "Hrold Jégszakáll vagyok. Százhetven telet számoltam, aztán meguntam. Azóta a jégcsapokat számolom. Ez itt a négyszáznyolcvankétezredik. Vagy ugyanaz, mint tegnap.",
                "A kulcsot egy vörös sárkány karmaiból téptem ki, amikor még fiatal és ostoba volt. Azóta nagyobb lett, én pedig bölcsebb: ma már tudom, hogy Azrakar nem felejt. A tüze néha még álmomban is megolvasztja a csarnok falát.",
                "A kulcs belsejében kaput látok, a kapu mögött pedig egy vörös követ. Nem tudom, miért kell tizenkét kulcs egyetlen kapuhoz. Talán a kicsi népek ennyire félnek a huzattól."
            ]),
            [MonsterIds.VörösSárkány] = new("VI. fejezet — Azrakar parázstrónusa",
            [
                "Azrakar vagyok, az Első Parázs örököse. Láttam királyokat megöregedni, birodalmakat hamuvá válni, és Hroldot elfutni a saját megperzselt szakállával. Ezt a részt különösen szívesen láttam.",
                "Tudom, hogy a kulcs pecsétet tör. Zephyriel, a szél ősmágusa maga bízta az elődeimre. Azt mondta, tizenkét őrző közül egy se értse az egész tervet. Bölcs óvatosság — vagy gyáva bizalmatlanság.",
                "A mocsárban Sziszara, a hidra őrzi a következő kulcsot. Kilenc feje van, és mind a kilenc más történetet mesél arról, hogyan győzött le engem. Egyik sem igaz. A tizedik történet viszont talán az lenne."
            ]),
            [MonsterIds.Hidra] = new("VII. fejezet — Sziszara kilenc hangja",
            [
                "Sziszara vagyok. Én mondom ezt, nem a bal szélső fej. Az mindig hazudik. A jobb szélső szerint mindannyian Sziszara vagyunk, de ő egyszer egy követ is tojásnak nézett.",
                "A kulcs a mocsár fenekéről került elő, egy szél nélküli vihar után. Ha közel tesszük a többi aranyhoz, énekel. Ha közel tesszük egy békához, a béka felrobban. Ezt fontosabb felfedezésnek tartom.",
                "A kristálycsarnokban Xyrax figyel minden irányba. Ő látja a kulcsok közötti fonalakat. Mi nem szeretjük Xyraxot. Túl sok szeme van. Ezt kilenc fej teljes egyetértésben mondja."
            ]),
            [MonsterIds.VénBeholder] = new("VIII. fejezet — Xyrax ezer látomása",
            [
                "Xyrax vagyok, a Századik Tekintet. Egyik szemem a jelent látja, három a lehetséges jövőket, kettő a múlt hazugságait. A maradékot viszketésre használom.",
                "Látom a tizenkét kulcs aranyfonalát. Nem ajtót nyitnak: helyet kényszerítenek a káoszra, hogy ajtóvá váljon. Mögötte a négy őserő egyetlen rubinban marja egymást, és huszonegy árnyék áll közte és a világotok között.",
                "Ossyra, a csontsárkány még emlékszik Zephyriel hangjára. Fél saját ősétől, Nharaztól, a drakolichtól. Jogos félelem. Egy lehetséges jövőben Nharaz megeszi a lelkemet. Tizenhét másikban én eszem meg az övét."
            ]),
            [MonsterIds.Csontsárkány] = new("IX. fejezet — Ossyra csontemlékezete",
            [
                "Ossyra volt a nevem, amikor pikkely fedte e csontokat. Zephyriel akkor érkezett a dermedt mélységbe, amikor még az ég is fiatalabb volt. Nem parancsolt. Könyörgött, hogy őrizzem a kulcsot azoktól, akik a Rubint fegyverré tennék.",
                "A halál nem oldotta fel az eskümet. Csak elvette belőle a meleget. Évszázadok óta hallom, ahogy a pecsétek egymást keresik a dimenzión át.",
                "Nharaz, az első sárkányból lett drakolich azt hiszi, a Rubin visszaadhatja a húsát. Téved. A Rubin nem visszaad: átír. Ha eljuttok hozzá, ne higgyetek annak a hangnak, amelyen a halott anyátok szólít majd benneteket."
            ]),
            [MonsterIds.Ősvámpír] = new("X. fejezet — Velkhar örök éjszakája",
            [
                "Velkhar gróf vagyok, és már akkor untam ezt az erődöt, amikor dédapáitok még udvariasan kopogtak a kripták ajtaján. Mostanában csak Aurelios kémei jönnek. Udvariatlanok, de legalább friss vérük van.",
                "Igen, ismerem a Máguskirály tervét. Emberei fogadóról fogadóra építették ki az útvonalatokat. Azt hiszik, nem vettem észre őket. Hagytam, hogy továbbmenjenek; kíváncsi voltam, valóban megérkeznek-e a csillagok kiválasztottjai.",
                "Vhar-Zul is küldött követeket. Azt ígérte, nappaltalan világot ad nekem. Ostoba ajánlat — már van egy. A Drakolich viszont elfogadta az övét. Nharaz tudja, hogyan nyílik a belső út, és kinek a vére kell hozzá."
            ]),
            [MonsterIds.Drakolich] = new("XI. fejezet — Nharaz fekete evangéliuma",
            [
                "Nharaz vagyok, akit a sárkányok is ősüknek neveztek, mielőtt nevemet kivésték a csontjaikból. Ossyra figyelmeztetett rólam, igaz? Mindig szeretett történetekkel védekezni a valóság ellen.",
                "A tizenkét kulcs a külső pecsét gyűrűjét bontja fel. Utána huszonegy szint következik, mindegyik Zephyriel egy-egy emlékéből és félelméből épült. Az út végén nem kincsesház áll, hanem a Káoszrubin börtöne.",
                "Vhar-Zul megígérte, hogy a Rubin lángjával új testet ad nekem. Tudom, hogy hazudik. Én is hazudtam neki. A Balor, Ashkaroth azonban vakon szolgálja. Ha legyőzitek, a Sötét Úr végre személyesen is rátok figyel majd."
            ]),
            [MonsterIds.BalorDémon] = new("XII. fejezet — Ashkaroth vértrónusa",
            [
                "Ashkaroth vagyok, Vhar-Zul ostora és a Vértrónus ura. Nem véletlenül jutottatok idáig. Hagytuk, hogy összegyűjtsétek a kulcsokat, mert a pecséteket sem démon, sem árnyék nem törheti fel — csak azok, akiket a csillagok megjelöltek.",
                "Ti nem Aurelios hősei vagytok. Ti vagytok a kulcs, amelyet mindkét király ugyanabba a zárba próbál illeszteni. Amint az utolsó aranydarab a helyére kerül, uram seregei meglátják a Rubin Útját.",
                "A Káoszsárkány nem szolgál minket. Kael-Zhur még Vhar-Zult is gyűlöli, mert ismeri a Rubin valódi árát. Menjetek csak hozzá. Ha megöltök, a halálom lesz a jelzőtűz, amely odavezeti hozzátok a Sötét Urat."
            ]),
            [MonsterIds.Káoszsárkány] = new("XIII. fejezet — Kael-Zhur, az utolsó lakat",
            [
                "Kael-Zhur vagyok, a Káosz első lélegzete és Zephyriel utolsó bűne. Nem születtem. A Rubin álmodott meg engem, hogy legyen valami, amitől még a négy alkotója is félhet.",
                "Tudom, miért jöttetek. Aurelios azt mondta, meg kell előznötök Vhar-Zult. Vhar-Zul azt hiszi, ti nyitjátok ki neki az utat. Mindketten igazat mondanak, és mindketten hazudnak. A Káoszrubin nem engedelmeskedik annak, aki megtalálja — átformálja őt arra, amire a világ leginkább vágyik vagy amitől legjobban retteg.",
                "Kulcsom az utolsó a tizenkettőből. Ha elveszitek, feltárul Zephyriel huszonegy szintből álló belső útja. Ott már nem őrzők várnak, hanem egy ősmágus emlékei, széthullott törvények és Vhar-Zul közeledő serege.",
                "Évszázadok óta őrzöm ezt a lakatot, és bevallom: rettenetesen unatkozom. Mutassátok meg, Kulcshordozók, hogy a csillagok valóban benneteket láttak — vagy csak egy különösen kegyetlen tréfát játszottak velem."
            ]),
            [MonsterIds.SirMalrec] = new("Roderic krónikája — A megtört eskü",
            [
                "Roderic. Még mindig mások pajzsa mögé bújsz, hogy ne kelljen látnod a saját kudarcodat?",
                "Az eskünk velünk halt. Én csak kimondtam azt, amit te azóta sem mersz: a holtaknak nem tartozunk hűséggel.",
                "Gyertek hát. Hadd lássam, vajon az új bajtársaid tovább kitartanak-e melletted, mint a régiek."
            ])
        };

    public static IReadOnlyList<string> CreateCampaignFinale(IEnumerable<LiveCharacter> livingParty, string leaderName)
    {
        var paragraphs = new List<string>
        {
            "A Káoszrubin a rejtekhely legutolsó termében lebeg. Belsejében tűz, víz, föld és szél kergeti egymást, mintha a négy ősi elementálmágus vitája még mindig nem ért volna véget. Amikor megérintitek, a kő egyetlen szívdobbanásnyi időre elnémul — aztán bíbor fénye elnyeli a labirintust.",
            "Nem zuhantok, mégis világok suhannak el mellettetek. A káosz huszonegy megtört törvénye egyetlen villanásba roskad, majd márvány érinti a lábatokat. Saját világotokban álltok, Aurelios Máguskirály tróntermében, a Káoszrubinnal együtt.",
            "A Csillagszeműek teljes köre vár benneteket az aranykupola alatt. Már órákkal korábban meggyújtották a tizenkét csillaglámpást: megérezték, hogy közeledik a kiválasztott. Vhar-Zul árnyéka visszahúzódik az ólomüveg ablakokról, Aurelios pedig leszáll trónjáról, és király létére fejet hajt előttetek.",
            "A királyi krónikás felnyitja az üresen hagyott aranylapokat. Nemcsak a Káoszrubin visszatérését jegyzi fel, hanem mindazok nevét is, akik élve járták végig az utat:"
        };

        paragraphs.AddRange(livingParty.Select(CreateSurvivorTribute));
        paragraphs.Add(
            $"Aurelios végül {leaderName} kezére teszi a kezét. „A csillagok kiválasztottak benneteket, de nem a jóslat győzött helyettetek. Ti tettétek valóra. Mától nem alattvalóimként, hanem a birodalom megmentőiként álltok előttem.”");
        paragraphs.Add(
            "A tizenkét csillaglámpás egyszerre lobban fel, a tróntermet pedig harangzúgás és ujjongás tölti be. A Káoszrubin hazatért, Vhar-Zul terve meghiúsult, és a túlélők neve örökre felkerült az Aranykor új krónikájába. Gratulálunk — végigjártátok a Káoszlabirintust, és megnyertétek a játékot!");
        return paragraphs;
    }

    private static string CreateSurvivorTribute(LiveCharacter character) => character.CharacterClass.Id switch
    {
        CharacterClassIds.Harcos =>
            $"{character.Name}, a harcos erős keze sosem hagyta cserben társait. Ellenfelei rettegtek fegyverének súlyától, barátai pedig tudták, hogy mellette a legvadabb roham is megtörik.",
        CharacterClassIds.Barbár =>
            $"{character.Name}, a barbár fékezhetetlen bátorsága utat tört ott is, ahol más már csak a biztos halált látta. Haragja viharként söpört végig a szörnyeken, de társait mindvégig hűséges szívvel oltalmazta.",
        CharacterClassIds.Lovag =>
            $"{character.Name}, a lovag pajzsa élő várfalként állt a csapat előtt. Becsülete a legsötétebb síkokon sem homályosult el, és esküjét még a Káosz sem tudta megtörni.",
        CharacterClassIds.Tolvaj =>
            $"{character.Name}, a tolvaj ott talált ösvényt, ahol más csak zárakat, csapdákat és árnyakat látott. Éles szeme és gyors keze számtalanszor mentette meg a csapatot, gyakran még azelőtt, hogy társai észrevették volna a veszélyt.",
        CharacterClassIds.Pap =>
            $"{character.Name}, a pap hite fényt gyújtott a holtak és démonok birodalmában. Imái visszahívták társait a kétségbeesés pereméről, szent erejétől pedig még a sír nyughatatlan urai is meghátráltak.",
        CharacterClassIds.Mágus =>
            $"{character.Name}, a mágus tudása megszelídítette a labirintus vad erőit. Varázslatai csillagfényként hasították fel a sötétséget, és elméje olyan titkokat fejtett meg, amelyeket évszázadok óta senki sem mert megérinteni.",
        _ =>
            $"{character.Name}, a {character.CharacterClass.Name.ToLowerInvariant()} rendíthetetlen társként járta végig a Káoszlabirintust; neve méltán került a birodalom legnagyobb hősei közé."
    };
}
