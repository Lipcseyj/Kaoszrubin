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
    public const string IfjúBaziliszkusz = "E034";
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

    public static IReadOnlySet<string> Bosses { get; } = new HashSet<string>(
    [
        Patkányember, Ghoul, OrkSámán, Fagyóriás, VörösSárkány, Hidra,
        VénBeholder, Csontsárkány, Ősvámpír, Drakolich, BalorDémon, Káoszsárkány
    ], StringComparer.OrdinalIgnoreCase);

    public static IReadOnlySet<string> MiniBosses { get; } = new HashSet<string>(
        [SirMalrec], StringComparer.OrdinalIgnoreCase);
}
