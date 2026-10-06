using AsciiArtEditor.Services;

internal static partial class Program
{
    static void PortraitPaletteCollectsAllUniqueUnicodeRunes()
    {
        var palette = PortraitPalette.Collect(["aλa\n", "😀b"]);

        Assert(palette.SequenceEqual([" ", "a", "λ", "😀", "b"]),
            "A portrépaletta nem őrizte meg az összes egyedi Unicode-karaktert forrássorrendben.");
    }

    static void AsciiPortraitSourceParsesDictionaries()
    {
        var source = new AsciiPortraitSource();
        var fixture = CreateAsciiPortraitSourceFixture();
        var characters = source.ParseDictionary(fixture, PortraitDictionary.CharacterClasses);
        var enemies = source.ParseDictionary(fixture, PortraitDictionary.Enemies);

        Assert(characters.Single().KeyExpression == "CharacterClassIds.Harcos" &&
               characters.Single().Content == "    hero" &&
               enemies.Single().KeyExpression == "MonsterIds.Kobold" &&
               enemies.Single().Content == "  rat",
            "A portréforrás nem olvasta be a szótárkulcsokat és a raw stringek tartalmát.");
    }

    static void AsciiPortraitSourceUpdatesOneEntry()
    {
        var source = new AsciiPortraitSource();
        var path = WriteAsciiPortraitFixture();
        try
        {
            var result = source.SavePortraitInFile(path, PortraitDictionary.CharacterClasses,
                "CharacterClassIds.Harcos", "updated");
            var fileContent = File.ReadAllText(path);
            var characters = source.ParseDictionary(fileContent, PortraitDictionary.CharacterClasses);
            var enemies = source.ParseDictionary(fileContent, PortraitDictionary.Enemies);

            Assert(result.Success && !result.Inserted && characters.Single().Content == "updated" &&
                   enemies.Single().Content == "  rat",
                "A portréfrissítés nem korlátozódott a kijelölt bejegyzésre.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    static void AsciiPortraitSourceInsertsEntry()
    {
        var source = new AsciiPortraitSource();
        var path = WriteAsciiPortraitFixture();
        try
        {
            const string key = "\"custom.enemy\"";
            var result = source.SavePortraitInFile(path, PortraitDictionary.Enemies, key, "  /\\\n  x");
            var updatedSource = File.ReadAllText(path);
            var enemies = source.ParseDictionary(updatedSource, PortraitDictionary.Enemies);

            Assert(result.Success && result.Inserted && enemies.Count == 2 &&
                   enemies.Single(entry => entry.KeyExpression == key).Content == "  /\\\n  x" &&
                   source.ParseDictionary(updatedSource, PortraitDictionary.CharacterClasses).Single().Content == "    hero",
                "Az új portré nem a kijelölt szótárba került, vagy a másik szótár megváltozott.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    static void AsciiPortraitSourceRejectsInvalidKey()
    {
        var source = new AsciiPortraitSource();
        var path = WriteAsciiPortraitFixture();
        try
        {
            var original = File.ReadAllText(path);
            var result = source.SavePortraitInFile(path, PortraitDictionary.Enemies,
                "SomeCode.Execute()", "unsafe");
            var unsupportedLiteral = source.SavePortraitInFile(path, PortraitDictionary.Enemies,
                "MonsterIds.Custom", "unsupported \"\"\" raw delimiter");

            Assert(!result.Success && !unsupportedLiteral.Success && File.ReadAllText(path) == original,
                "A hibás C# kulcsot vagy nem támogatott raw stringet a portrémentés elfogadta vagy módosította a forrást.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WriteAsciiPortraitFixture()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ascii-portraits-{Guid.NewGuid():N}.cs");
        File.WriteAllText(path, CreateAsciiPortraitSourceFixture());
        return path;
    }

    private static string CreateAsciiPortraitSourceFixture() => string.Join('\n',
    [
        "private static readonly IReadOnlyDictionary<string, AsciiPortrait> CharacterClasses =",
        "    new Dictionary<string, AsciiPortrait>(StringComparer.OrdinalIgnoreCase)",
        "    {",
        "        [CharacterClassIds.Harcos] = Portrait(",
        "            \"\"\"",
        "                hero",
        "            \"\"\"),",
        "    };",
        "private static readonly IReadOnlyDictionary<string, AsciiPortrait> Enemies =",
        "    new Dictionary<string, AsciiPortrait>(StringComparer.OrdinalIgnoreCase)",
        "    {",
        "        [MonsterIds.Kobold] = Portrait(",
        "            \"\"\"",
        "              rat",
        "            \"\"\"),",
        "    };"
    ]);
}