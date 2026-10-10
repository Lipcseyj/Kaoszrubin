using System.Text;

namespace KaoszRubin.World;

/// <summary>Egy falmező megjelenése. A járhatóságot továbbra is a labirintus falkezelése adja.</summary>
public sealed record DungeonWallStyle(string Id, string Name, string Description, Rune Rune, ConsoleColor Color)
{
    public override string ToString() => $"{Name} — {Rune} ({Color})";
}

/// <summary>
/// Közös dungeon-falkatalógus. A pályákban WallStyle = DungeonWallStyles.Catacombs formában használható.
/// Egy stílus jelének vagy színének módosítása minden rá hivatkozó, újonnan generált pályára érvényes.
/// </summary>
public static class DungeonWallStyles
{
    public static DungeonWallStyle SolidStone { get; } =
        new(nameof(SolidStone), "Tömör kő", "Klasszikus, teljesen kitöltött fal.", new('█'), ConsoleColor.DarkGray);

    public static DungeonWallStyle RatTunnels { get; } =
        new(nameof(RatTunnels), "Repedezett járatfal", "Málló kövek és szűk állatjáratok.", new('≆'), ConsoleColor.DarkGray);

    public static DungeonWallStyle OldSewers { get; } =
        new(nameof(OldSewers), "Régi csatorna", "Vízjárta, rétegzett csatornakő.", new('≓'), ConsoleColor.DarkCyan);

    public static DungeonWallStyle GoblinBurrows { get; } =
        new(nameof(GoblinBurrows), "Goblin vájat", "Szabálytalan, karcolt vájatfal.", new('⍝'), ConsoleColor.DarkGreen);

    public static DungeonWallStyle BeastDens { get; } =
        new(nameof(BeastDens), "Barlangi szikla", "Nyers, töredezett sziklatömb.", new('⬔'), ConsoleColor.DarkYellow);

    public static DungeonWallStyle Catacombs { get; } =
        new(nameof(Catacombs), "Katakombakő", "Rekeszes, sírfülkéket idéző falazat.", new('⊟'), ConsoleColor.Gray);

    public static DungeonWallStyle GreatHalls { get; } =
        new(nameof(GreatHalls), "Oszlopcsarnok", "Aranybarna oszlopokkal tagolt fal.", new('Ш'), ConsoleColor.DarkYellow);

    public static DungeonWallStyle PoisonCaves { get; } =
        new(nameof(PoisonCaves), "Mérgezett üledék", "Hullámos, nedves barlangi rétegek.", new('≋'), ConsoleColor.DarkCyan);

    public static DungeonWallStyle OrcPalisade { get; } =
        new(nameof(OrcPalisade), "Ork cölöpfal", "Vörösbarna, durva erődítés.", new('⌸'), ConsoleColor.DarkRed);

    public static DungeonWallStyle CursedTombs { get; } =
        new(nameof(CursedTombs), "Átkozott sírpecsét", "Keresztpecsétekkel lezárt sírkövek.", new('⊠'), ConsoleColor.DarkMagenta);

    public static DungeonWallStyle GiantFortress { get; } =
        new(nameof(GiantFortress), "Óriás kváderkő", "Nagy, keresztben kötött kőtömbök.", new('▩'), ConsoleColor.Gray);

    public static DungeonWallStyle DragonCult { get; } =
        new(nameof(DragonCult), "Sárkányszentély", "Vörös, rovátkolt kultuszfal.", new('▥'), ConsoleColor.Red);

    public static DungeonWallStyle RottingMarsh { get; } =
        new(nameof(RottingMarsh), "Lápi romfal", "Zöldes, vízjárta romok.", new('≙'), ConsoleColor.DarkGreen);

    public static DungeonWallStyle SunkenPalace { get; } =
        new(nameof(SunkenPalace), "Elsüllyedt palotafal", "Türkiz, sávos királyi díszkő.", new('⊒'), ConsoleColor.DarkCyan);

    public static DungeonWallStyle SerpentTemple { get; } =
        new(nameof(SerpentTemple), "Kígyótemplom", "Okker színű, kígyóornamentikás fal.", new('≗'), ConsoleColor.DarkYellow);

    public static DungeonWallStyle DeepPassages { get; } =
        new(nameof(DeepPassages), "Sötét mélyszikla", "Tömör és nyomasztó mélyjárati fal.", new('▉'), ConsoleColor.DarkGray);

    public static DungeonWallStyle CrystalHalls { get; } =
        new(nameof(CrystalHalls), "Kristályréteg", "Fényes, ferdén hasadó kristályfal.", new('▧'), ConsoleColor.Cyan);

    public static DungeonWallStyle FrozenDepths { get; } =
        new(nameof(FrozenDepths), "Jégréteg", "Fehér, egymásra fagyott rétegek.", new('▤'), ConsoleColor.White);

    public static DungeonWallStyle VampireFortress { get; } =
        new(nameof(VampireFortress), "Vámpírerőd", "Sötét, tömött pórusú erődkő.", new('⣿'), ConsoleColor.DarkMagenta);

    public static DungeonWallStyle DragonGraveyard { get; } =
        new(nameof(DragonGraveyard), "Csontkő", "Szürkés, csontokkal tagolt sírkő.", new('⌺'), ConsoleColor.Gray);

    public static DungeonWallStyle EmberWastes { get; } =
        new(nameof(EmberWastes), "Parázskő", "Vörösbarna, izzó hasadásokkal szelt kő.", new('▚'), ConsoleColor.DarkRed);

    public static DungeonWallStyle BloodThrone { get; } =
        new(nameof(BloodThrone), "Vértrón díszköve", "Vörös, belső négyzettel jelölt trónfal.", new('▣'), ConsoleColor.Red);

    public static DungeonWallStyle ChaosHeart { get; } =
        new(nameof(ChaosHeart), "Káosz kristályfala", "Idegen, széttartó magenta mintázat.", new('⍔'), ConsoleColor.Magenta);

    public static DungeonWallStyle ChaosThrone { get; } =
        new(nameof(ChaosThrone), "Káosz pecsétfala", "Szögletes, torz pecsétekkel teli fal.", new('⍂'), ConsoleColor.Magenta);

    public static DungeonWallStyle MalrecChapel { get; } =
        new(nameof(MalrecChapel), "Sírkápolna", "Lilás, sötét keresztrácsos kápolnakő.", new('⌻'), ConsoleColor.DarkMagenta);

    public static DungeonWallStyle ClassicHeavyStone { get; } =
        new(nameof(ClassicHeavyStone), "Klasszikus nehéz kő", "Széles, majdnem teljes kőtömb.", new('▉'), ConsoleColor.Gray);

    public static DungeonWallStyle ClassicThickStone { get; } =
        new(nameof(ClassicThickStone), "Klasszikus vastag kő", "Vastag, függőlegesen tagolt tömb.", new('▊'), ConsoleColor.DarkGray);

    public static DungeonWallStyle ClassicNarrowStone { get; } =
        new(nameof(ClassicNarrowStone), "Klasszikus keskeny kő", "Karcsúbb, függőleges kőtömb.", new('▋'), ConsoleColor.DarkGray);

    public static DungeonWallStyle ClassicHalfStone { get; } =
        new(nameof(ClassicHalfStone), "Klasszikus félkő", "Jobb oldalon kitöltött félblokk.", new('▐'), ConsoleColor.DarkGray);

    public static DungeonWallStyle ClassicWeatheredStone { get; } =
        new(nameof(ClassicWeatheredStone), "Klasszikus mállott kő", "Ritka, halvány szemcsézet.", new('░'), ConsoleColor.DarkYellow);

    public static DungeonWallStyle ClassicRoughStone { get; } =
        new(nameof(ClassicRoughStone), "Klasszikus érdes kő", "Közepesen sűrű szemcsézet.", new('▒'), ConsoleColor.DarkGray);

    public static DungeonWallStyle ClassicDenseStone { get; } =
        new(nameof(ClassicDenseStone), "Klasszikus sűrű kő", "Sötét, sűrű szemcsézet.", new('▓'), ConsoleColor.Gray);

    public static DungeonWallStyle ClassicLayeredStone { get; } =
        new(nameof(ClassicLayeredStone), "Klasszikus réteges kő", "Vízszintesen csíkozott blokk.", new('▤'), ConsoleColor.Gray);

    public static DungeonWallStyle ClassicColumnedStone { get; } =
        new(nameof(ClassicColumnedStone), "Klasszikus rovátkolt kő", "Függőlegesen csíkozott blokk.", new('▥'), ConsoleColor.DarkYellow);

    public static DungeonWallStyle ClassicGridStone { get; } =
        new(nameof(ClassicGridStone), "Klasszikus rácsos kő", "Szabályos négyzetrácsos kő.", new('▦'), ConsoleColor.DarkYellow);

    public static DungeonWallStyle ClassicSlantedStone { get; } =
        new(nameof(ClassicSlantedStone), "Klasszikus ferde kő", "Ferde, párhuzamos sávok.", new('▧'), ConsoleColor.Gray);

    public static DungeonWallStyle ClassicMosaicStone { get; } =
        new(nameof(ClassicMosaicStone), "Klasszikus mozaikkő", "Ferdén szőtt kékeszöld mozaik.", new('▨'), ConsoleColor.DarkCyan);

    public static DungeonWallStyle ClassicCrossedStone { get; } =
        new(nameof(ClassicCrossedStone), "Klasszikus keresztkő", "Két irányban ferde kőrács.", new('▩'), ConsoleColor.Gray);

    public static DungeonWallStyle MonolithicStone { get; } =
        new(nameof(MonolithicStone), "Monolitkő", "Sötét, nagyméretű négyzettömb.", new('⯀'), ConsoleColor.DarkGray);

    public static DungeonWallStyle PittedStone { get; } =
        new(nameof(PittedStone), "Lyukacsos romkő", "Ritka üregekkel tagolt, világos romfal.", new('⬚'), ConsoleColor.Gray);

    public static DungeonWallStyle DressedStone { get; } =
        new(nameof(DressedStone), "Faragott félkő", "Felül világos, alul tömör kőtömb.", new('⬒'), ConsoleColor.Gray);

    public static DungeonWallStyle BracketStone { get; } =
        new(nameof(BracketStone), "Bástyakő", "Nyitott, szögletes bástyaminta.", new('⊏'), ConsoleColor.DarkYellow);

    public static DungeonWallStyle BrokenLines { get; } =
        new(nameof(BrokenLines), "Törött vonalkő", "Választható falblokk a közös jelkészletből.", new('≇'), ConsoleColor.Gray);

    public static DungeonWallStyle WaveStone { get; } =
        new(nameof(WaveStone), "Hullámkő", "Választható falblokk a közös jelkészletből.", new('≈'), ConsoleColor.Gray);

    public static DungeonWallStyle CrackedWave { get; } =
        new(nameof(CrackedWave), "Repedt hullámkő", "Választható falblokk a közös jelkészletből.", new('≉'), ConsoleColor.Gray);

    public static DungeonWallStyle DoubleWave { get; } =
        new(nameof(DoubleWave), "Kettős hullámkő", "Választható falblokk a közös jelkészletből.", new('≊'), ConsoleColor.Gray);

    public static DungeonWallStyle RippleStone { get; } =
        new(nameof(RippleStone), "Hullámpecsétes kő", "Választható falblokk a közös jelkészletből.", new('≌'), ConsoleColor.Gray);

    public static DungeonWallStyle ArcStone { get; } =
        new(nameof(ArcStone), "Íves rétegkő", "Választható falblokk a közös jelkészletből.", new('≍'), ConsoleColor.Gray);

    public static DungeonWallStyle LayeredRipple { get; } =
        new(nameof(LayeredRipple), "Réteges hullámkő", "Választható falblokk a közös jelkészletből.", new('≎'), ConsoleColor.Gray);

    public static DungeonWallStyle StackedRipple { get; } =
        new(nameof(StackedRipple), "Hármas hullámkő", "Választható falblokk a közös jelkészletből.", new('≏'), ConsoleColor.Gray);

    public static DungeonWallStyle DottedBands { get; } =
        new(nameof(DottedBands), "Pontozott sávkő", "Választható falblokk a közös jelkészletből.", new('≐'), ConsoleColor.Gray);

    public static DungeonWallStyle MarkedBands { get; } =
        new(nameof(MarkedBands), "Jelölt sávkő", "Választható falblokk a közös jelkészletből.", new('≑'), ConsoleColor.Gray);

    public static DungeonWallStyle TiltedJoint { get; } =
        new(nameof(TiltedJoint), "Ferde fugakő", "Választható falblokk a közös jelkészletből.", new('≒'), ConsoleColor.Gray);

    public static DungeonWallStyle DottedJoint { get; } =
        new(nameof(DottedJoint), "Pontfugás kő", "Választható falblokk a közös jelkészletből.", new('≔'), ConsoleColor.Gray);

    public static DungeonWallStyle SteppedJoint { get; } =
        new(nameof(SteppedJoint), "Lépcsős fugakő", "Választható falblokk a közös jelkészletből.", new('≕'), ConsoleColor.Gray);

    public static DungeonWallStyle RingJoint { get; } =
        new(nameof(RingJoint), "Gyűrűs fugakő", "Választható falblokk a közös jelkészletből.", new('≖'), ConsoleColor.Gray);

    public static DungeonWallStyle InlaidCircle { get; } =
        new(nameof(InlaidCircle), "Körberakásos kő", "Választható falblokk a közös jelkészletből.", new('≘'), ConsoleColor.Gray);

    public static DungeonWallStyle RingMasonry { get; } =
        new(nameof(RingMasonry), "Gyűrűs falazat", "Választható falblokk a közös jelkészletből.", new('≚'), ConsoleColor.Gray);

    public static DungeonWallStyle BrokenBands { get; } =
        new(nameof(BrokenBands), "Hasadt sávkő", "Választható falblokk a közös jelkészletből.", new('≢'), ConsoleColor.Gray);

    public static DungeonWallStyle DottedBox { get; } =
        new(nameof(DottedBox), "Pontbetétes kockakő", "Választható falblokk a közös jelkészletből.", new('⊡'), ConsoleColor.Gray);

    public static DungeonWallStyle RightBracket { get; } =
        new(nameof(RightBracket), "Jobbos bástyakő", "Választható falblokk a közös jelkészletből.", new('⊐'), ConsoleColor.Gray);

    public static DungeonWallStyle LowerBracket { get; } =
        new(nameof(LowerBracket), "Lépcsős bástyakő", "Választható falblokk a közös jelkészletből.", new('⊑'), ConsoleColor.Gray);

    public static DungeonWallStyle GateStone { get; } =
        new(nameof(GateStone), "Kapukő", "Választható falblokk a közös jelkészletből.", new('⊓'), ConsoleColor.Gray);

    public static DungeonWallStyle UStone { get; } =
        new(nameof(UStone), "Vályús kő", "Választható falblokk a közös jelkészletből.", new('⊔'), ConsoleColor.Gray);

    public static DungeonWallStyle CrossBox { get; } =
        new(nameof(CrossBox), "Keresztbetétes kockakő", "Választható falblokk a közös jelkészletből.", new('⊞'), ConsoleColor.Gray);

    public static DungeonWallStyle SquareGrid { get; } =
        new(nameof(SquareGrid), "Négyzetes pecsétkő", "Választható falblokk a közös jelkészletből.", new('⌼'), ConsoleColor.Gray);

    public static DungeonWallStyle CornerGrid { get; } =
        new(nameof(CornerGrid), "Sarokpecsétes kő", "Választható falblokk a közös jelkészletből.", new('⍁'), ConsoleColor.Gray);

    public static DungeonWallStyle UpDiagonals { get; } =
        new(nameof(UpDiagonals), "Emelkedő ékkő", "Választható falblokk a közös jelkészletből.", new('⍓'), ConsoleColor.Gray);

    public static DungeonWallStyle BarredCross { get; } =
        new(nameof(BarredCross), "Rácsos pecsétkő", "Választható falblokk a közös jelkészletből.", new('⍯'), ConsoleColor.Gray);

    public static DungeonWallStyle UpperBlock { get; } =
        new(nameof(UpperBlock), "Felső tömbfal", "Választható falblokk a közös jelkészletből.", new('▀'), ConsoleColor.Gray);

    public static DungeonWallStyle ClassicLowerBlock { get; } =
        new(nameof(ClassicLowerBlock), "Klasszikus alsó tömbfal", "Választható falblokk a közös jelkészletből.", new('▇'), ConsoleColor.Gray);

    public static DungeonWallStyle LowerLeftBlock { get; } =
        new(nameof(LowerLeftBlock), "Bal alsó tömbkő", "Választható falblokk a közös jelkészletből.", new('▙'), ConsoleColor.Gray);

    public static DungeonWallStyle UpperLeftBlock { get; } =
        new(nameof(UpperLeftBlock), "Bal felső tömbkő", "Választható falblokk a közös jelkészletből.", new('▛'), ConsoleColor.Gray);

    public static DungeonWallStyle UpperRightBlock { get; } =
        new(nameof(UpperRightBlock), "Jobb felső tömbkő", "Választható falblokk a közös jelkészletből.", new('▜'), ConsoleColor.Gray);

    public static DungeonWallStyle UpperRightCorner { get; } =
        new(nameof(UpperRightCorner), "Jobb felső sarokkő", "Választható falblokk a közös jelkészletből.", new('▝'), ConsoleColor.Gray);

    public static DungeonWallStyle OpposedCorners { get; } =
        new(nameof(OpposedCorners), "Átlós sarokkő", "Választható falblokk a közös jelkészletből.", new('▞'), ConsoleColor.Gray);

    public static DungeonWallStyle LowerRightBlock { get; } =
        new(nameof(LowerRightBlock), "Jobb alsó tömbkő", "Választható falblokk a közös jelkészletből.", new('▟'), ConsoleColor.Gray);

    public static DungeonWallStyle BlackSquare { get; } =
        new(nameof(BlackSquare), "Négyzetkő", "Választható falblokk a közös jelkészletből.", new('■'), ConsoleColor.Gray);

    public static DungeonWallStyle HollowSquare { get; } =
        new(nameof(HollowSquare), "Üreges négyzetkő", "Választható falblokk a közös jelkészletből.", new('▢'), ConsoleColor.Gray);

    public static DungeonWallStyle BrailleSparse { get; } =
        new(nameof(BrailleSparse), "Szemcsés póruskő", "Választható falblokk a közös jelkészletből.", new('⠿'), ConsoleColor.Gray);

    public static DungeonWallStyle BrailleLeft { get; } =
        new(nameof(BrailleLeft), "Balos póruskő", "Választható falblokk a közös jelkészletből.", new('⡿'), ConsoleColor.Gray);

    public static DungeonWallStyle BrailleRight { get; } =
        new(nameof(BrailleRight), "Jobbos póruskő", "Választható falblokk a közös jelkészletből.", new('⢿'), ConsoleColor.Gray);

    public static DungeonWallStyle LowerSplitStone { get; } =
        new(nameof(LowerSplitStone), "Alsó osztott kő", "Választható falblokk a közös jelkészletből.", new('⬓'), ConsoleColor.Gray);

    public static DungeonWallStyle RightSplitStone { get; } =
        new(nameof(RightSplitStone), "Jobb osztott kő", "Választható falblokk a közös jelkészletből.", new('⬕'), ConsoleColor.Gray);

    public static DungeonWallStyle LeftDiamond { get; } =
        new(nameof(LeftDiamond), "Balos gyémántkő", "Választható falblokk a közös jelkészletből.", new('⬖'), ConsoleColor.Gray);

    public static DungeonWallStyle RightDiamond { get; } =
        new(nameof(RightDiamond), "Jobbos gyémántkő", "Választható falblokk a közös jelkészletből.", new('⬗'), ConsoleColor.Gray);

    public static DungeonWallStyle TopDiamond { get; } =
        new(nameof(TopDiamond), "Felső gyémántkő", "Választható falblokk a közös jelkészletből.", new('⬘'), ConsoleColor.Gray);

    public static DungeonWallStyle BottomDiamond { get; } =
        new(nameof(BottomDiamond), "Alsó gyémántkő", "Választható falblokk a közös jelkészletből.", new('⬙'), ConsoleColor.Gray);

    public static DungeonWallStyle FourBands { get; } =
        new(nameof(FourBands), "Négysávos kő", "Sűrű, vízszintesen rétegzett fal.", new('≣'), ConsoleColor.Gray);

    public static IReadOnlyList<DungeonWallStyle> All { get; } = Array.AsReadOnly<DungeonWallStyle>(
    [
        SolidStone,
        RatTunnels,
        OldSewers,
        GoblinBurrows,
        BeastDens,
        Catacombs,
        GreatHalls,
        PoisonCaves,
        OrcPalisade,
        CursedTombs,
        GiantFortress,
        DragonCult,
        RottingMarsh,
        SunkenPalace,
        SerpentTemple,
        DeepPassages,
        CrystalHalls,
        FrozenDepths,
        VampireFortress,
        DragonGraveyard,
        EmberWastes,
        BloodThrone,
        ChaosHeart,
        ChaosThrone,
        MalrecChapel,
        ClassicHeavyStone,
        ClassicThickStone,
        ClassicNarrowStone,
        ClassicHalfStone,
        ClassicWeatheredStone,
        ClassicRoughStone,
        ClassicDenseStone,
        ClassicLayeredStone,
        ClassicColumnedStone,
        ClassicGridStone,
        ClassicSlantedStone,
        ClassicMosaicStone,
        ClassicCrossedStone,
        MonolithicStone,
        PittedStone,
        DressedStone,
        BracketStone,
        BrokenLines,
        WaveStone,
        CrackedWave,
        DoubleWave,
        RippleStone,
        ArcStone,
        LayeredRipple,
        StackedRipple,
        DottedBands,
        MarkedBands,
        TiltedJoint,
        DottedJoint,
        SteppedJoint,
        RingJoint,
        InlaidCircle,
        RingMasonry,
        BrokenBands,
        DottedBox,
        RightBracket,
        LowerBracket,
        GateStone,
        UStone,
        CrossBox,
        SquareGrid,
        CornerGrid,
        UpDiagonals,
        BarredCross,
        UpperBlock,
        ClassicLowerBlock,
        LowerLeftBlock,
        UpperLeftBlock,
        UpperRightBlock,
        UpperRightCorner,
        OpposedCorners,
        LowerRightBlock,
        BlackSquare,
        HollowSquare,
        BrailleSparse,
        BrailleLeft,
        BrailleRight,
        LowerSplitStone,
        RightSplitStone,
        LeftDiamond,
        RightDiamond,
        TopDiamond,
        BottomDiamond,
        FourBands,
    ]);

    /// <summary>Lekérés stabil azonosítóval vagy a listában látható magyar névvel; kisbetű/nagybetű mindegy.</summary>
    public static DungeonWallStyle Get(string idOrName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idOrName);
        return All.FirstOrDefault(style =>
            string.Equals(style.Id, idOrName.Trim(), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(style.Name, idOrName.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Ismeretlen dungeon-falstílus: {idOrName}", nameof(idOrName));
    }
}
