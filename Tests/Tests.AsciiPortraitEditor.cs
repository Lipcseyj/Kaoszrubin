using AsciiArtEditor.Services;

internal static partial class Program
{
    static void PortraitPaletteCollectsAllUniqueUnicodeRunes()
    {
        var palette = PortraitPalette.Collect(["aλa\n", "😀b"]);

        Assert(palette[0] == " " && palette.Contains("─") && palette.Contains("█") &&
               palette.Contains("◆") && palette.Contains("a") && palette.Contains("λ") &&
               palette.Contains("😀") && palette.Contains("b") &&
               palette.Count > 500 && palette.Distinct(StringComparer.Ordinal).Count() == palette.Count,
            "A portrépaletta nem tartalmaz elég egyedi rajzolókaraktert és a forrás glyphjeit.");
    }

    static void PaletteSettingsRoundTripFavouritesAndNames()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ascii-palette-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "palette-settings.json");
        try
        {
            var store = new PaletteSettingsStore(path);
            var saved = new PaletteSettings
            {
                Favourites = ["◆", "λ", "😀"],
                PageNames = new Dictionary<int, string> { [0] = "Pinned", [2] = "Arrows" }
            };

            var savedSuccessfully = store.TrySave(saved, out var saveError);
            var loadedSuccessfully = store.TryLoad(out var loaded, out var loadError);

            Assert(savedSuccessfully && loadedSuccessfully && saveError is null && loadError is null &&
                   loaded.Favourites.SequenceEqual(saved.Favourites, StringComparer.Ordinal) &&
                   loaded.PageNames.Count == 2 && loaded.PageNames[0] == "Pinned" && loaded.PageNames[2] == "Arrows",
                "A palettabeállítások nem őrizték meg a kedvenceket és az oldalelnevezéseket.");
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    static void PaletteSettingsRejectMalformedJsonWithoutThrowing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ascii-palette-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{ invalid json");
            var loadedSuccessfully = new PaletteSettingsStore(path).TryLoad(out var settings, out var error);

            Assert(!loadedSuccessfully && !string.IsNullOrWhiteSpace(error) &&
                   settings.Favourites.Count == 0 && settings.PageNames.Count == 0,
                "A hibás palettabeállítás nem biztonságos üres beállításokra esett vissza.");
        }
        finally
        {
            File.Delete(path);
        }
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