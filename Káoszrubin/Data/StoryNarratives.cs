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
        "Ekkor megjelenik előttetek Aurelios Máguskirály áttetsző képmása. „Beteljesítettétek a Csillagszeműek első jóslatát” — mondja. — „A tizenkét pecsét feltört. A következő kapu már a Káoszrubin rejtekhelyére vezet. Vhar-Zul is megérezte a nyíló utat: most kell megelőznötök.”",
        "A látomás mögött egy másik alak is kirajzolódik: Vhar-Zul fekete koronája, majd két parázsló szem. A Sötét Úr nevetése végigviharzik a dimenzión. Ő is megérezte a zárak felnyílását — és most már pontosan tudja, merre vezet a Rubin Útja.",
        "A tizenkét kulcs egyetlen ragyogó pecsétté olvad a parti előtt. A kapu túloldalán a Káoszrubin rejtekhelye vár. A tizenkét őrző története egyetlen döntéshez vezetett: kinek a kezébe kerülhet az a hatalom, amely már egyszer elpusztította az Aranykort?"
    ];

    public static readonly IReadOnlyDictionary<string, BossNarrative> BossNarratives =
        new Dictionary<string, BossNarrative>(StringComparer.OrdinalIgnoreCase)
        {
            [MonsterIds.OrkTörzsfő] = new("1. kulcs — Vasagyar megtört zászlaja",
            [
                "A Harci tanácsterem falára nem térképet, hanem a szökött ork családok neveit szögezték. Grond Vasagyar minden név mellé vasfogat vert. A dezertőr egykor ezt a listát őrizte, mielőtt saját fivére is felkerült rá.",
                "„Grond vagyok, a Vasagyar törzs ura. A sámánok csak tanácsot adnak. Én döntöm el, ki eszik, ki harcol, és ki kerül a vadászok elé. A szökevény azért küldött titeket, mert maga nem mert visszajönni.”",
                "A törzsfő a nyakában függő aranykulcsra markol. „A szél ura őrzésért adta. Vhar-Zul fegyvereket kínál érte. Előbb azonban bizonyítanom kell, hogy a csillagok emberei is elvéreznek. Testőrök, zárjátok a sort!”",
            ]),
            [MonsterIds.Fagyóriás] = new("2. kulcs — Jégszakáll utolsó vámja",
            [
                "Hrold csarnokában szánok és törött expedíciós jelvények fagytak a falba. Az óriásvadász társai itt fizették ki utolsó vámjukat. Azóta az erőd minden átutazóját ugyanennek az adósságnak a nevében fosztják ki.",
                "„Hrold Jégszakáll vagyok. Egykor átvezettem a kicsi népeket a hágón. Aztán Azrakar felégette a falvaimat, az utazók pedig a vörös sárkánynak fizettek védelemért. Most nekem fizetnek. Mind.”",
                "Jégbe foglalt aranykulcs lóg az övén. „Zephyriel azt kérte, senkit ne engedjek át, aki hatalmat keres. Az óriásvadász szerint megszegtem az esküt. Mondjátok meg neki: az eskü nem eteti az éhezőket.”",
            ]),
            [MonsterIds.VörösSárkány] = new("3. kulcs — A Parázstrón hamis hívei",
            [
                "A Parázsszentély oszlopain királyok neveit kaparták át Azrakar nevére. Az orgyilkos hívek minden nemzedékben új áldozatot hoztak a trónnak. A sárkánykutató expedíciója azonban felismerte: a feliratok egy régi őrzői esküt rejtenek.",
                "„Azrakar vagyok, a Parázstrón ura. Hrold csak a szakállát veszítette a tüzemben. Az emberek egész királyságokat. A kutatótok mégis azt híreszteli, hogy nem isten vagyok, hanem egy halott mágus szolgája.”",
                "Az aranykulcs a megolvadt koronák között izzik. „Zephyriel őrzést kért a véremtől. Én hódolatot kaptam érte. Ha elviszitek a kulcsot, ki fogja többé megkülönböztetni a sárkányt az istentől?”",
            ]),
            [MonsterIds.ŐsiHidra] = new("4. kulcs — A koronafaló kilenc hangja",
            [
                "Az Elárasztott méregcsarnok vizében koronák csillannak. Sziszara már akkor itt élt, amikor a lápi paloták még a napot látták. Kilenc torka utánozza az elsüllyedt királyok harangjait: a révész eltűnt hajósai ezt követték a mélybe.",
                "„Sziszara vagyok” — mondja a középső fej. „Mi vagyunk Sziszara” — felel a többi. „Az aranykalapos királyok mind átkelést kértek. Mind megfizettek. A ti révészetek azt hiszi, a halottakért jár a visszajáró.”",
                "Zephyriel kulcsa egy széttört koronában függ az ősi hidra mellkasán. „A szél ide rejtette, ahol a tűz nem érheti el. Sszar-Kor vissza akarja kérni az ősei aranyát. Szeth-Issz istennek nevez minket. Ti minek neveztek majd, amikor a kilencedik száj is harap?”",
            ]),
            [MonsterIds.GyíkemberKirály] = new("5. kulcs — A pikkelytrón zsarnoka",
            [
                "A Királyi pikkelytrón lépcsőire levert koronákat szegecseltek. Sszar-Kor ezekből csinált rangjelzést a Pikkelylégiónak. A levéltáros listáján mindegyik korona mellett egy felégetett lápi település neve áll.",
                "„Sszar-Kor vagyok. A lápban portyázóimat láttátok, itt a légiót. Pajzs tartja a csapást, vadász zárja az oldalt, sámán emeli fel az elesettet. A koronák urai vitatkoztak. Az én katonáim engedelmeskednek.”",
                "A király az aranykulcsot a trón karfájából emeli ki. „Zephyriel őrzővé tett. Szeth-Issz az engedelmességemet akarja, a levéltáros a múltamat. Egyikük sem kapja meg. Ha a paphoz készültök, előbb bizonyítsátok, hogy át tudtok törni egy valódi hadrenden.”",
            ]),
            [MonsterIds.KígyóFőpap] = new("6. kulcs — A vedlő isten hamis hangja",
            [
                "A főoltár körül emberi neveket véstek levetett kígyóbőrökbe. A szökevény felismerte köztük azokat, akiket a lápi fogadók elől hurcoltak el. Szeth-Issz minden áldozatot az isten új testének egy darabjává nyilvánított.",
                "„Szeth-Issz vagyok, a Vedlő Isten hangja. Sszar-Kor pajzsot adott a katonának. Én értelmet adok a halálának. A szökevényetek azért fél tőlem, mert megmutattam neki, mennyire vékony a saját bőre.”",
                "A főpap aranykulcsot merít az oltár mérgébe. „A szél őrzést kért. Vhar-Zul megváltást ígért. Ha elég név oldódik fel ebben a kehelyben, a pecsét engem választ új testének. Kezdődjék a körmenet!”",
            ]),
            [MonsterIds.VénBeholder] = new("7. kulcs — A kristályok fogoly tekintete",
            [
                "A Századik Tekintet szentélyében a kristályok emberi emlékeket vetítenek a falra. Xyrax a mérnök eltűnt társait használta szemnek: minden új járatot rajtuk át tanult meg, majd a testüket a gólemekre bízta.",
                "„Xyrax vagyok. Száz szemmel születtem, de az kevés volt ehhez a világhoz. A mérnök vissza akarja kapni a barátait. Melyik emléküket? A bátrakét, akiket ő küldött ide, vagy a haldoklókét, akiket magukra hagyott?”",
                "Az aranykulcs lebegő kristályba zárva forog. „Zephyriel fonalai mind ide vezetnek. Ossyra még emlékszik az esküre, Nharaz már eladta. Én mindkettőjüket láttam. Most a ti emlékeitekkel teszem teljessé a térképet.”",
            ]),
            [MonsterIds.Csontsárkány] = new("8. kulcs — A dermedt eskü feloldása",
            [
                "A dermedt eskü sírkamrájában a fagyjáró kalauz őseinek nevei sorakoznak. Egykor Ossyra szárnya alatt kerestek menedéket. Zephyriel mindannyiukat a pecsét védelmére eskette, és az eskü túlélte a testüket.",
                "„Ossyra volt a nevem, amikor pikkely fedett. A kalauzotok a saját vérét akarja felszabadítani. Megértem. De a halál nem mondta ki helyettünk, hogy vége az őrségnek.”",
                "Az aranykulcs a sárkány bordái között csillan. „Nharaz visszaígérte a húsomat, ha átadom. Nem tettem. Ha ti a Rubint fegyverré teszitek, ugyanúgy hazudtatok a holtaknak, mint ő. Mutassátok meg, hogy az élőkben még lehet bízni.”",
            ]),
            [MonsterIds.Ősvámpír] = new("9. kulcs — Az örökéj név szerint",
            [
                "Az örökéj tróntermében minden teríték mellett egy család címere áll. Velkhar meghívói nemzedékek óta nem engedték haza a vendégeket. A halottlátó remete azért küldött ide, hogy a neveiket ismét valaki élő mondja ki.",
                "„Velkhar gróf vagyok. Aurelios kémei kereskedőnek öltöztek, Vhar-Zul követei szövetséget kínáltak. Mind azt hitték, hogy a nappaltalan világért bármelyikük szolgája leszek. Pedig én csak jó társaságot kerestem.”",
                "A gróf aranykulccsal kocogtatja a kristálypoharat. „Zephyriel pecsétje megőrizte a vendégeim emlékeit. A remetétek szabadon engedné őket. Ti is ezért jöttetek, vagy csak új gazdát hoztok ugyanannak a zárnak?”",
            ]),
            [MonsterIds.Drakolich] = new("10. kulcs — A holtak hamis feltámadása",
            [
                "A fekete evangélium kriptájában a holtak neveit nem kőbe, hanem friss bőrbe írták. Nharaz új testet ígért követőinek, miközben a sárkánykutató nyomai szerint a lelküket a saját bordái közé égette.",
                "„Nharaz vagyok. Ossyra esküvel vigasztalja a holtakat, én lehetőséget adok nekik. A kutatótok bizonyítékot keres a csalásra. Nézzen rám: a halál után is beszélek. Kell ennél több bizonyíték a feltámadásra?”",
                "Az aranykulcs csontból formált szív mellett függ. „Vhar-Zul a Rubin tüzét ígérte, ha elárulom Zephyriel zárját. Tudom, hogy hazudik. Ő is tudja, hogy én hazudom. Ashkaroth viszont még mindig hisz neki. Az ilyen hitből lesz a háború.”",
            ]),
            [MonsterIds.BalorDémon] = new("11. kulcs — A Vértrónus láncai",
            [
                "A Vértrónus lábánál a démonvadász régi láncai hevernek. Ashkaroth a megszökött vadász helyére annak társait kötötte, majd minden új ostromhoz az ő kiáltásukkal adta meg a jelet.",
                "„Ashkaroth vagyok, Vhar-Zul ostora. A lánctalan vadász szabadságról beszél. Én megmutattam neki az árát. Mindenki, akit itt felszabadítotok, újabb ok lesz arra, hogy uram a világotokat válassza következő célpontnak.”",
                "A balor a Vértrónus karfájába vert aranykulcsra mutat. „A pecséteket csak a csillagok kiválasztottjai törhetik fel. Ezért kellett idáig eljutnotok. Kael-Zhur az utolsó lakat. Ha ő elesik, a ti utatok lesz Vhar-Zul útja is.”",
            ]),
            [MonsterIds.Káoszsárkány] = new("12. kulcs — Zephyriel utolsó bűne",
            [
                "Az utolsó lakat termében nem kincshalom áll, hanem Zephyriel arcát viselő töredezett szobrok. A káoszzarándok itt értette meg, hogy a sárkány nem Vhar-Zul fegyvere: a Rubin alkotójának elhallgatott bűne.",
                "„Kael-Zhur vagyok. Nem születtem. A Rubin álmodott meg, amikor Zephyriel a társai ellen fordult. Azért zárt ide, mert bennem ismerte fel azt, amivé a hatalom őt tette.”",
                "Az utolsó aranykulcs a sárkány mellkasába nőtt. „Aurelios megmentést ígér, Vhar-Zul uralmat. Mindketten azt hiszik, a kő engedelmeskedni fog. Ha legyőztök, feltárul a Rubin rejtekhelye. A zarándoknak mondjátok meg: az őrző végül az élőkre bízta a döntést.”",
            ]),
            [MonsterIds.SirMalrec] = new("Roderic krónikája — A megtört eskü",
            [
                "A sírkápolnában Sir Malrec az Esküszegő a régi bajtársak pajzsait fordította a fal felé. Az Ezüst Eskü egykori parancsnoka most négy csontvázlovagot vezet. Roderic azért kérte a segítségeteket, hogy a rend emléke ne ezzel az őrséggel együtt tűnjön el.",
                "„Roderic. Még mindig mások pajzsa mögé bújsz? Hallottuk a katakombákban rekedtek hangját, amikor lezártad a kaput. Te az élőket mentetted. Mi bent maradtunk. Azóta minden éjjel újra meghalljuk a zár kattanását.”",
                "Malrec kardja a négy csontvázlovag felé int. „Az élőknek tett esküt én már feloldottam. A holtaknak új hűséget fogadtam. Ezek a régi bajtársaid. Gyertek: lássuk, az új barátaid tovább kitartanak-e melletted.”",
            ]),
            [MonsterIds.HollóKlánvezér] = new("A Hollók keresztútja — Varrik utolsó zsákmánya",
            [
                "Varrik Feketeszárny a Hollók kereszteződésénél nem ékszert halmozott fel, hanem az erdőn átkelő karavánok ellátmányát. A menekülők éhezése biztosította, hogy mindig legyen, aki megfizeti a klánt.",
                "„Azt hittétek, a Hollók csak erszényt vágnak? Én utat adok. Kenyeret, vizet, nevet a hamis papírokra. A küldetéstek megbízója mindezt vissza akarja kérni, csak az árát nem akarja kifizetni.”",
                "A klánvezér Varrik vésésű tőrét a raktárláda tetejére koppintja. A rejtekben orgyilkosok mozdulnak. „A készlet a miénk. Ha az útra kell nektek, bizonyítsátok, hogy élve ki is értek vele az erdőből.”",
            ]),
            [MonsterIds.OrkRaktárnok] = new("Az erdei raktár — Durgan számadása",
            [
                "Durgan Hordótörő az erdei őrség zsákmányából táplálta a haditábort. Minden ellopott zsák mellé ugyanazt írta: hadiadó. A visszaszerzésre váró ellátmány mögött falvak üres kamrái állnak.",
                "„Durgan vagyok. Grond harcol, a sámán jósol, de én tudom, hány adag marad télre. A megbízótoknak könnyű visszakérni ezt a ládát. Az én katonáimnak akkor nem jut semmi.”",
                "A raktárnok baltájával szétüti a bejárat mellé tett hordót, és őrei elé áll. „Aki a készlethez nyúl, az egész haditábort hívja ki. Kezdjük velem.”",
            ]),
            [MonsterIds.ZsilipŐrkapitány] = new("Az elsüllyedt út — Aldren utolsó őrsége",
            [
                "Sir Aldren a láp zsilipjénél maradt, amikor az udvar víz alá került. A mentéshez félretett ellátmányt azóta is őrzi, bár a parancsot adó király rég halott. A készletért érkezőket ugyanannak a fosztogató hadnak nézi.",
                "„Sir Aldren vagyok, a zsilip őrkapitánya. A korona parancsa szerint ezek a ládák az evakuálásra kellenek. A megbízótok nem hozott új parancsot. Csak újabb éhes embereket.”",
                "Rozsdás kardja mögött az őrség felsorakozik. „Ha a régi udvar már nem tér vissza, mutassátok meg, hogy van még valaki, akiért érdemes kinyitnom a zsilipet.”",
            ]),
            [MonsterIds.LápiUdvarmester] = new("A lápi udvar — Mirelda üres lakomája",
            [
                "Mirelda udvarmester egy elsüllyedt királyság utolsó lakomáját készíti elő. A tányérok üresek, de a kamrákban őrzött ellátmány valódi: a láp túlélői ebből juthatnának a következő menedékig.",
                "„Mirelda vagyok. Az udvar addig él, amíg valaki megteríti az asztalt. A megbízótok szerint ezt a készletet szét kell osztani. Akkor ki marad, aki fogadja a hazatérő királyt?”",
                "Az udvarmester intésére a kultista kíséret is feláll. „A koronák a vízben vannak, de a kötelesség itt maradt. Ha véget akartok vetni a lakomának, üljetek az utolsó vendégek helyére.”",
            ]),
        };

    public static IReadOnlyList<string> CreateCampaignFinale(IEnumerable<LiveCharacter> livingParty, string leaderName)
    {
        var paragraphs = new List<string>
        {
            "A Káoszrubin a rejtekhely legutolsó termében lebeg. Belsejében tűz, víz, föld és szél kergeti egymást, mintha a négy ősi elementálmágus vitája még mindig nem ért volna véget. Amikor megérintitek, a kő egyetlen szívdobbanásnyi időre elnémul — aztán bíbor fénye elnyeli a labirintust.",
            "Nem zuhantok, mégis világok suhannak el mellettetek. A káosz megtört törvényei egyetlen villanásba roskadnak, majd márvány érinti a lábatokat. Saját világotokban álltok, Aurelios Máguskirály tróntermében, a Káoszrubinnal együtt.",
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
