using KaoszRubin.Application;
using KaoszRubin.Data;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Magic;
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
    public static CoopFixture Create(string workspaceRoot, string? settingsPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        Directory.CreateDirectory(workspaceRoot);

        var catalogPath = Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName);
        var catalogBytes = File.ReadAllBytes(catalogPath);
        var catalog = CsvGameDataLoader.Load(catalogPath);
        var characterSaveService = new CharacterSaveService(Path.Combine(workspaceRoot, "characters.json"), catalog);
        var gameSaveService = new GameSaveService(Path.Combine(workspaceRoot, "saves"), characterSaveService);
        var gameSettingsService = new GameSettingsService(settingsPath ??
            Path.Combine(workspaceRoot, "beallitasok.json"));

        var race = catalog.Races.FirstOrDefault(value => value.Id == "R001") ?? catalog.Races.First();
        var hostClass = catalog.CharacterClasses.First(value => value.Id == CharacterClassIds.Harcos);
        var guestClass = catalog.CharacterClasses.First(value => value.Id == CharacterClassIds.Mágus);

        var hostLeader = CreateCharacter(catalog, "Host", race, hostClass,
            new PrimaryAbilities(10, 9, 10, 8), 4, 4, ConsoleColor.Cyan);
        var thief = CreateCharacter(catalog, "Tolvaj", race,
            catalog.CharacterClasses.First(value => value.Id == CharacterClassIds.Tolvaj),
            new PrimaryAbilities(9, 11, 9, 8), 3, 3, ConsoleColor.Green);
        var knight = CreateCharacter(catalog, "Lovag", race,
            catalog.CharacterClasses.First(value => value.Id == CharacterClassIds.Lovag),
            new PrimaryAbilities(11, 8, 10, 8), 4, 3, ConsoleColor.White);
        var guestCharacter = CreateCharacter(catalog, "Vendeg", race, guestClass,
            new PrimaryAbilities(8, 8, 9, 11), 3, 5, ConsoleColor.Yellow);

        PrepareCharacter(hostLeader, catalog, 11);
        PrepareCharacter(thief, catalog, 12);
        PrepareCharacter(knight, catalog, 13);
        PrepareCharacter(guestCharacter, catalog, 14);

        var hostRoster = new CharacterRoster();
        hostRoster.Add(hostLeader);
        hostRoster.Add(thief);
        hostRoster.Add(knight);
        hostRoster.Select(hostLeader);
        if (!hostRoster.Party.Add(thief) || !hostRoster.Party.Add(knight))
            throw new InvalidOperationException("A coop tesztparti nem állítható össze.");

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

    private static void PrepareCharacter(LiveCharacter character, GameDataCatalog catalog, int seed)
    {
        var random = new Random(seed);
        SpellcastingRules.GiveAutomaticStartingSpells(character, catalog, random);
        new RandomCharacterGenerator(catalog, random).PrepareExistingCharacterForTest(character, 30);
        if (character.Level != 30)
            throw new InvalidOperationException($"{character.Name} nem érte el a 30. szintet.");

        for (var index = 0; index < character.WeaponSlots.Count; index++)
            Require(character.SetInventoryItem(InventorySlotKind.Weapon, index, null), character, "fegyverhely törlése");
        Require(character.EquipArmor(null), character, "páncélhely törlése");
        for (var index = 0; index < LiveCharacter.MaximumMagicItemCount; index++)
            Require(character.SetInventoryItem(InventorySlotKind.MagicItem, index, null), character, "varázstárgyhely törlése");
        for (var index = 0; index < character.Backpack.Count; index++)
            if (!SpellcastingRules.IsSpellcastingFocus(character.Backpack[index]))
                Require(character.SetInventoryItem(InventorySlotKind.Backpack, index, null), character, "hátizsákhely törlése");

        void Equip(int slot, string id) => Require(character.EquipWeapon(slot, catalog.GetWeapon(id)), character, id);
        void Armor(string id) => Require(character.EquipArmor(catalog.GetArmor(id)), character, id);
        void Carry(IItemDefinition item, int count = 1)
        {
            for (var index = 0; index < count; index++)
                Require(character.AddToBackpack(item), character, item.Id);
        }
        void Weapon(string id, int count = 1) => Carry(catalog.GetWeapon(id), count);
        void Item(string id, int count = 1) => Carry(catalog.GetItem(id), count);
        void Magic(string id, int count = 1) => Carry(catalog.GetMagicItem(id), count);
        void EquipMagic(int slot, string id) => Require(character.SetInventoryItem(InventorySlotKind.MagicItem,
            slot, catalog.GetMagicItem(id)), character, id);

        switch (character.CharacterClass.Id)
        {
            case CharacterClassIds.Harcos:
                Equip(0, "W017"); Armor("A003"); Equip(2, "W010");
                Weapon("W011"); Weapon("W015"); Weapon("W040"); Weapon("W039"); Item("T029", 24);
                EquipMagic(0, "M006"); EquipMagic(1, "M012"); Item("T018", 5);
                break;
            case CharacterClassIds.Lovag:
                Equip(0, "W004"); Equip(1, "W029"); Armor("A005"); Equip(2, "W011");
                Weapon("W009"); Weapon("W016"); Weapon("W044"); Item("T030", 24);
                EquipMagic(0, "M001"); EquipMagic(1, "M011"); Item("T019", 5);
                break;
            case CharacterClassIds.Tolvaj:
                Equip(0, "W001"); Equip(1, "W024"); Equip(2, "W002"); Armor("A002-PLUS2");
                Weapon("W043"); Weapon("W042"); Item("T030", 24); Item("T003", 8);
                Item("T025", 4); Weapon("W003"); Weapon("W022", 2); Item("T031", 5);
                EquipMagic(0, "M005"); EquipMagic(1, "M013");
                break;
            case CharacterClassIds.Mágus:
                Equip(0, "W018");
                EquipMagic(0, "M023"); EquipMagic(1, "M015"); EquipMagic(2, "MW006");
                Magic("MW007");
                foreach (var id in new[] { "MS009", "MS011", "MS012", "PS011", "PS012" }) Magic(id, 2);
                Item("T017", 5);
                break;
        }

        Item("T012", 3); Item("T002", 5); Item("T001", 5);
    }

    private static void Require(bool success, LiveCharacter character, string item)
    {
        if (!success) throw new InvalidOperationException($"{character.Name}: {item} nem fér el vagy nem használható.");
    }
}
