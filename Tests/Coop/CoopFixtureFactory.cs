using KaoszRubin.Application;
using KaoszRubin.Data;
using KaoszRubin.Domain.Characters;
using KaoszRubin.UI;

namespace KaoszRubin.Tests.Coop;

internal sealed record CoopFixture(
    string WorkspaceRoot,
    string ApplicationVersion,
    string CatalogHash,
    GameDataCatalog Catalog,
    CharacterSaveService CharacterSaveService,
    GameSaveService GameSaveService,
    GameSettingsService GameSettingsService,
    CharacterRoster HostRoster,
    LiveCharacter HostLeader,
    LiveCharacter GuestCharacter);

internal static class CoopFixtureFactory
{
    public static CoopFixture Create(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        Directory.CreateDirectory(workspaceRoot);

        var catalogPath = Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName);
        var catalogBytes = File.ReadAllBytes(catalogPath);
        var catalog = CsvGameDataLoader.Load(catalogPath);
        var characterSaveService = new CharacterSaveService(Path.Combine(workspaceRoot, "characters.json"), catalog);
        var gameSaveService = new GameSaveService(Path.Combine(workspaceRoot, "saves"), characterSaveService);
        var gameSettingsService = new GameSettingsService(Path.Combine(workspaceRoot, "beallitasok.json"));

        var race = catalog.Races.FirstOrDefault(value => value.Id == "R001") ?? catalog.Races.First();
        var hostClass = catalog.CharacterClasses.First(value => value.Id == CharacterClassIds.Harcos);
        var guestClass = catalog.CharacterClasses.First(value => value.Id == CharacterClassIds.Mágus);

        var hostLeader = CreateCharacter(catalog, "Host", race, hostClass,
            new PrimaryAbilities(10, 9, 10, 8), 4, 4, ConsoleColor.Cyan);
        var hostCompanion = CreateCharacter(catalog, "Tarsa", race, hostClass,
            new PrimaryAbilities(9, 8, 9, 8), 3, 3, ConsoleColor.Green);
        var guestCharacter = CreateCharacter(catalog, "Vendeg", race, guestClass,
            new PrimaryAbilities(8, 8, 9, 11), 3, 5, ConsoleColor.Yellow);

        var hostRoster = new CharacterRoster();
        hostRoster.Add(hostLeader);
        hostRoster.Add(hostCompanion);
        hostRoster.Select(hostLeader);

        return new CoopFixture(
            workspaceRoot,
            MainMenu.AppVersion,
            CatalogFingerprint.Compute(catalogBytes),
            catalog,
            characterSaveService,
            gameSaveService,
            gameSettingsService,
            hostRoster,
            hostLeader,
            guestCharacter);
    }

    public static string CreateTemporaryWorkspaceRoot(string? requestedWorkspace)
    {
        if (!string.IsNullOrWhiteSpace(requestedWorkspace)) return requestedWorkspace;
        return Path.Combine(Path.GetTempPath(), "kr-coopsim", Guid.NewGuid().ToString("N"));
    }

    private static LiveCharacter CreateCharacter(GameDataCatalog catalog, string name, RaceDefinition race,
        CharacterClassDefinition characterClass, PrimaryAbilities abilities, int vitalityBonus, int manaBonus,
        ConsoleColor color)
    {
        // Az Alkalmazkodó fajok pontosan egy pont szétosztását követelik meg; determinisztikusan az erőre adjuk.
        var adaptableBonus = race.HasTrait(RaceTraits.Adaptable)
            ? new PrimaryAbilities(1, 0, 0, 0)
            : default;
        return LiveCharacterFactory.Create(name, race, characterClass, abilities, vitalityBonus, manaBonus,
            catalog, color, adaptableBonus);
    }
}
