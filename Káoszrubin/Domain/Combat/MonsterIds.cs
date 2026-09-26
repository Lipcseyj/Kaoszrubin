namespace KaoszRubin.Domain.Combat;

public static class MonsterIds
{
    public const string Óriáspatkány = "E001";
    public const string Kobold = "E002";
    public const string Goblin = "E003";
    public const string Csontváz = "E004";
    public const string Farkas = "E005";
    public const string Zombi = "E006";
    public const string Ork = "E007";
    public const string Hobgoblin = "E008";
    public const string Óriáspók = "E009";
    public const string Gnoll = "E010";
    public const string Lidércfarkas = "E011";
    public const string Ogre = "E012";
    public const string Troll = "E013";
    public const string Minotaurusz = "E014";
    public const string Múmia = "E015";
    public const string Medúza = "E016";
    public const string Kiméra = "E017";
    public const string Beholder = "E018";
    public const string Vámpír = "E019";
    public const string Vérfarkas = "E020";
    public const string VörösSárkány = "E021";
    public const string Lich = "E022";
    public const string Démonlovag = "E023";
    public const string BalorDémon = "E024";
    public const string FeketeSárkány = "E025";
    public const string Óriásdenevér = "E026";
    public const string Savanyálka = "E027";
    public const string Útonálló = "E028";
    public const string BarlangiGyík = "E029";
    public const string PestishordozóPatkány = "E030";
    public const string Bugbear = "E031";
    public const string Hárpia = "E032";
    public const string Ghoul = "E033";
    public const string ÓriásBaziliszkusz = "E034";
    public const string OrkSámán = "E035";
    public const string Ettin = "E036";
    public const string Wight = "E037";
    public const string Wyvern = "E038";
    public const string Kőgólem = "E039";
    public const string ÉjiBanya = "E040";
    public const string Fagyóriás = "E041";
    public const string Halállovag = "E042";
    public const string Hidra = "E043";
    public const string Csontsárkány = "E044";
    public const string Démonpók = "E045";
    public const string Ősvámpír = "E046";
    public const string Pokolfejedelem = "E047";
    public const string VénBeholder = "E048";
    public const string Drakolich = "E049";
    public const string Káoszsárkány = "E050";
    public const string Patkányember = "E051";
    public const string CsontvázLovag = "E052";
    public const string SirMalrec = "E053";
    public const string ÉlőholtPátriárka = "E061";
    public const string GoblinFőnök = "E062";
    public const string OrkTörzsfő = "E063";
    public const string ŐsiHidra = "E064";
    public const string VámpírKardmester = "E065";
    public const string CsontvázŐr = "E066";
    public const string PáncélozottZombi = "E067";
    public const string BarlangiTroll = "E068";
    public const string VénMúmia = "E069";
    public const string AlfaVérfarkas = "E070";
    public const string ŐsiMinotaurusz = "E071";

    public const string Orgyilkos = "E072";
    public const string SötételfOrgyilkos = "E073";
    public const string Nekromanta = "E074";
    public const string Gargoyle = "E075";
    public const string Óriásskorpió = "E076";
    public const string Pokolkutya = "E077";
    public const string Kígyóember = "E078";
    public const string Küklopsz = "E079";
    public const string Árnylidérc = "E080";
    public const string ÉlőPáncél = "E081";
    public const string Martalóc = "E082";
    public const string Káoszlovag = "E083";
    public const string Pokolfajzat = "E084";
    public const string DémoniKorcs = "E085";
    public const string Parázsdémon = "E086";
    public const string KarmosDémon = "E087";
    public const string Pokolőr = "E088";
    public const string Vérdémon = "E089";
    public const string GoblinVajákos = "E090";
    public const string KáoszmágusTanítvány = "E091";
    public const string Káoszpap = "E092";
    public const string Boszorkány = "E093";
    public const string OrkVérpap = "E094";
    public const string Kígyópap = "E095";
    public const string Káoszmágus = "E096";
    public const string SötétDruida = "E097";
    public const string Vérmágus = "E098";
    public const string KáoszFőpap = "E099";
    public const string Feketemágus = "E100";
    public const string GoblinÍjász = "E101";
    public const string CsontvázÍjász = "E102";
    public const string OrkÍjász = "E103";
    public const string Vadkan = "E104";
    public const string HegyiHiúz = "E105";

    public static IReadOnlySet<string> Bosses { get; } = new HashSet<string>(
    [
        Patkányember, Ghoul, OrkSámán, Fagyóriás, VörösSárkány, Hidra,
        VénBeholder, Csontsárkány, Ősvámpír, Drakolich, BalorDémon, Káoszsárkány
    ], StringComparer.OrdinalIgnoreCase);

    public static IReadOnlySet<string> MiniBosses { get; } = new HashSet<string>(
        [SirMalrec], StringComparer.OrdinalIgnoreCase);
}
