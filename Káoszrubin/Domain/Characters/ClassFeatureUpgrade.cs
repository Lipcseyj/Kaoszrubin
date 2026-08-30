namespace KaoszRubin.Domain.Characters;

public sealed record ClassFeatureUpgradeDefinition(string Id, string Name, string Description,
    string CharacterClassId);

public static class ClassFeatureUpgrades
{
    public const string FighterPrecise = "UPGRADE-C001-PRECISE";
    public const string FighterPowerful = "UPGRADE-C001-POWERFUL";
    public const string FighterDefensive = "UPGRADE-C001-DEFENSIVE";
    public const string BarbarianWildRage = "UPGRADE-C002-WILD";
    public const string BarbarianEnduringRage = "UPGRADE-C002-ENDURING";
    public const string BarbarianBloodRage = "UPGRADE-C002-BLOOD";
    public const string KnightBodyguard = "UPGRADE-C003-BODYGUARD";
    public const string KnightMarbleWall = "UPGRADE-C003-MARBLE";
    public const string KnightRetaliation = "UPGRADE-C003-RETALIATION";
    public const string ThiefAmbush = "UPGRADE-C004-AMBUSH";
    public const string ThiefObserve = "UPGRADE-C004-OBSERVE";
    public const string ThiefPoison = "UPGRADE-C004-POISON";
    public const string PriestOverflowingLife = "UPGRADE-C005-LIFE";
    public const string PriestSteadfastProtection = "UPGRADE-C005-PROTECTION";
    public const string PriestMercifulJudgment = "UPGRADE-C005-JUDGMENT";
    public const string MageRagingElements = "UPGRADE-C006-ELEMENTS";
    public const string MagePerfectIllusion = "UPGRADE-C006-ILLUSION";
    public const string MageLifeHarvest = "UPGRADE-C006-LIFE-HARVEST";

    private static readonly IReadOnlyList<ClassFeatureUpgradeDefinition> Definitions =
    [
        new(FighterPrecise, "🎯 Kimért pontosság", "A Pontos állás sebzése ×0,75 helyett ×0,85.", CharacterClassIds.Harcos),
        new(FighterPowerful, "💥 Zúzó lendület", "Az Erőteljes állás az ellenfél páncéljának 75%-át töri át.", CharacterClassIds.Harcos),
        new(FighterDefensive, "🛡️ Áthatolhatatlan állás", "A Védekező állás +3 helyett +4 védelmet ad.", CharacterClassIds.Harcos),
        new(BarbarianWildRage, "🩸 Vad düh", "A Düh +7–12 sebzést ad, de a védelem büntetése -3.", CharacterClassIds.Barbár),
        new(BarbarianEnduringRage, "🔥 Kitartó düh", "A Düh 5 akcióig tart és akciónként +4–7 sebzést ad.", CharacterClassIds.Barbár),
        new(BarbarianBloodRage, "❤️‍🔥 Vérdüh", "Minden dühös találat 1–3 HP-t visszatölt.", CharacterClassIds.Barbár),
        new(KnightBodyguard, "🛡️ Testőr", "A Védelmező közbelépési esélye 75%-ról 90%-ra nő.", CharacterClassIds.Lovag),
        new(KnightMarbleWall, "🏰 Márványfal", "Közbelépéskor a lovag a kivédett sebzés harmada helyett a negyedét kapja.", CharacterClassIds.Lovag),
        new(KnightRetaliation, "⚔️ Megtorlás", "Közbelépés után a lovag következő támadása +2 találatot és +4 sebzést kap.", CharacterClassIds.Lovag),
        new(ThiefAmbush, "🌑 Halálos rajtaütés", "Az Orvtámadás első találata ×2 helyett ×2,5 sebzést okoz.", CharacterClassIds.Tolvaj),
        new(ThiefObserve, "👁️ Gyenge pont", "Megfigyelésnél a természetes 19 is kétszeres kritikus találat.", CharacterClassIds.Tolvaj),
        new(ThiefPoison, "☠️ Erős méreg", "A Mérgezett penge találatonként +2–6 méregsebzést okoz.", CharacterClassIds.Tolvaj),
        new(PriestOverflowingLife, "💚 Túláradó élet", "Minden HP-gyógyítás további 15%-kal erősebb.", CharacterClassIds.Pap),
        new(PriestSteadfastProtection, "⛪ Rendíthetetlen oltalom", "A papi védővarázslatok további 1 akcióval tovább tartanak.", CharacterClassIds.Pap),
        new(PriestMercifulJudgment, "⚖️ Irgalmas ítélet", "A papi varázssebzés 10%-a visszagyógyítja a papot.", CharacterClassIds.Pap),
        new(MageRagingElements, "🌩️ Tomboló elemek", "A közvetlen arkán varázssebzés további 15%-kal nő.", CharacterClassIds.Mágus),
        new(MagePerfectIllusion, "🎭 Tökéletes illúzió", "Az arkán kontroll- és védőhatások további 1 akcióval tovább tartanak.", CharacterClassIds.Mágus),
        new(MageLifeHarvest, "💀 Életaratás", "A közvetlen varázssebzés 15%-a visszagyógyítja a mágust.", CharacterClassIds.Mágus)
    ];

    public static IReadOnlyList<ClassFeatureUpgradeDefinition> ForClass(string characterClassId) => Definitions
        .Where(definition => string.Equals(definition.CharacterClassId, characterClassId,
            StringComparison.OrdinalIgnoreCase)).ToArray();

    public static ClassFeatureUpgradeDefinition? Find(string id) => Definitions.FirstOrDefault(definition =>
        string.Equals(definition.Id, id, StringComparison.OrdinalIgnoreCase));
}
