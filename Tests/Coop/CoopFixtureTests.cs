using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Magic;

namespace KaoszRubin.Tests.Coop;

internal static class CoopFixtureTests
{
    public static void FourLevelThirtyCharactersCarryRequestedSupplies()
    {
        var root = CoopFixtureFactory.CreateTemporaryWorkspaceRoot(null);
        try
        {
            var fixture = CoopFixtureFactory.Create(root);
            var characters = fixture.HostRoster.Party.Members.Append(fixture.GuestCharacter).ToArray();
            Check(characters.Length == 4 && characters.All(character => character.Level == 30),
                "A tesztparti nem négy 30-as szintű karakterből áll.");
            Check(characters.Select(character => character.CharacterClass.Id).ToHashSet().SetEquals(
                    [CharacterClassIds.Harcos, CharacterClassIds.Mágus,
                     CharacterClassIds.Tolvaj, CharacterClassIds.Lovag]),
                "A tesztparti kasztjai hibásak.");
            Check(fixture.HostRoster.Party.Members.Count == 3 &&
                  fixture.HostRoster.Party.Members.Any(character => character.CharacterClass.Id == CharacterClassIds.Tolvaj) &&
                  fixture.HostRoster.Party.Members.Any(character => character.CharacterClass.Id == CharacterClassIds.Lovag),
                "A tolvaj és a lovag nem tagja a host induló partijának.");
            var session = new GameSession(fixture.HostRoster.Party, fixture.HostLeader);
            Check(session.TryJoinRemoteCharacter(session.RegisterRemotePlayer(), fixture.GuestCharacter,
                      out var joinError) && fixture.HostRoster.Party.Members.Count == 4,
                $"A vendég csatlakozása után nem négytagú a parti: {joinError}");

            foreach (var character in characters)
            {
                Check(Count(character, "T012") == 3 && Count(character, "T002") == 5 &&
                      Count(character, "T001") == 5, $"{character.Name} alapellátmánya hiányos.");
                Check(character.MagicItems.Count(item => item?.Kind == MagicItemKind.Ring) == 1 &&
                      character.MagicItems.Count(item => item?.Kind == MagicItemKind.Amulet) == 1 &&
                      CountKind(character, MagicItemKind.Ring) == 1 &&
                      CountKind(character, MagicItemKind.Amulet) == 1,
                    $"{character.Name} felszerelt gyűrűje vagy amulettje hiányzik.");
                Check(character.Perks.Count == 3 &&
                      character.TacticalDisciplines.Count == TacticalDisciplineProgression.EarnedChoices(30) &&
                      character.WeaponProficiencyAdvances == WeaponProficiencyProgression.EarnedAdvances(
                          character.CharacterClass.Id, 30),
                    $"{character.Name} 30. szintű fejlődési képességei hiányosak.");
            }

            var fighter = characters.Single(character => character.CharacterClass.Id == CharacterClassIds.Harcos);
            Check(Has(fighter, "W017", "A003", "W011", "W010", "W015", "W040", "W039") &&
                  Count(fighter, "T029") >= 24 && Count(fighter, "T018") == 5,
                "A harcos felszerelése hiányos.");
            var knight = characters.Single(character => character.CharacterClass.Id == CharacterClassIds.Lovag);
            Check(Has(knight, "A005", "W004", "W029", "W011", "W009", "W016", "W044") &&
                  Count(knight, "T030") >= 24 && Count(knight, "T019") == 5,
                "A lovag felszerelése hiányos.");
            var thief = characters.Single(character => character.CharacterClass.Id == CharacterClassIds.Tolvaj);
            Check(Has(thief, "W001", "W024", "W043", "W042", "W002", "W003", "A002-PLUS2") &&
                  Count(thief, "T030") >= 24 && Count(thief, "T003") == 8 &&
                  Count(thief, "T025") == 4 && Count(thief, "W022") == 2 &&
                  Count(thief, "T031") == 5, "A tolvaj felszerelése hiányos.");
            var mage = fixture.GuestCharacter;
            Check(mage.KnownSpells.Count > 0 && mage.MemorizedSpells.Count > 0,
                "A mágus nem tanulta vagy memorizálta a szintjének megfelelő varázslatait.");
            Check(Has(mage, "W018", "M023", "M015", "MW006", "MW007") &&
                  mage.MagicItems[2]?.Id == "MW006" &&
                  mage.Backpack.Any(item => item?.Id == "MW007") &&
                  Count(mage, "T019") == 0 && Count(mage, "T018") == 0 &&
                  Count(mage, "T017") == 5 &&
                  new[] { "MS009", "MS011", "MS012", "PS011", "PS012" }
                      .All(id => Count(mage, id) == 2) &&
                  CountKind(mage, MagicItemKind.Scroll) == 10 &&
                  CountKind(mage, MagicItemKind.Wand) == 2,
                "A mágus felszerelése hiányos.");
            var restored = fixture.CharacterSaveService.DeserializeCharacter(
                fixture.CharacterSaveService.SerializeCharacter(mage));
            Check(restored.Level == 30 && CountKind(restored, MagicItemKind.Scroll) == 10 &&
                  CountKind(restored, MagicItemKind.Wand) == 2,
                "A vendég felszerelése nem éli túl a kapcsolat előtti mentési kört.");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static bool Has(LiveCharacter character, params string[] ids) =>
        ids.All(id => Count(character, id) > 0);

    private static int Count(LiveCharacter character, string id) =>
        Slots(character).Where(slot => slot.Item?.Id == id).Sum(slot => slot.Quantity);

    private static int CountKind(LiveCharacter character, MagicItemKind kind) =>
        Slots(character).Where(slot => slot.Item is MagicItemDefinition { Kind: var itemKind } && itemKind == kind)
            .Sum(slot => slot.Quantity);

    private static IEnumerable<(IItemDefinition? Item, int Quantity)> Slots(LiveCharacter character)
    {
        for (var index = 0; index < character.WeaponSlots.Count; index++)
            yield return (character.WeaponSlots[index], character.GetInventoryItemQuantity(InventorySlotKind.Weapon, index));
        yield return (character.Armor, character.GetInventoryItemQuantity(InventorySlotKind.Armor, 0));
        for (var index = 0; index < LiveCharacter.MaximumMagicItemCount; index++)
            yield return (character.MagicItems[index], character.GetInventoryItemQuantity(InventorySlotKind.MagicItem, index));
        for (var index = 0; index < character.Backpack.Count; index++)
            yield return (character.Backpack[index], character.GetInventoryItemQuantity(InventorySlotKind.Backpack, index));
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
