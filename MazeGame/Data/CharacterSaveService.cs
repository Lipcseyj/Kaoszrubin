using System.Text.Json;
using MazeGame.Domain;
using MazeGame.Domain.Characters;
using MazeGame.Domain.Combat;
using MazeGame.Domain.Inventory;
using MazeGame.Domain.Magic;

namespace MazeGame.Data;

/// <summary>A generált karakterek tartós, JSON-alapú tárolása.</summary>
public sealed class CharacterSaveService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _filePath;
    private readonly GameDataCatalog _gameData;

    public CharacterSaveService(string filePath, GameDataCatalog gameData)
    {
        _filePath = filePath;
        _gameData = gameData;
    }

    public CharacterRoster Load()
    {
        var roster = new CharacterRoster();
        if (!File.Exists(_filePath)) return roster;

        var savedRoster = JsonSerializer.Deserialize<RosterSaveData>(File.ReadAllText(_filePath), JsonOptions) ?? new RosterSaveData();
        foreach (var savedCharacter in savedRoster.Characters)
            roster.Add(CreateLiveCharacter(savedCharacter));

        if (savedRoster.SelectedCharacterIndex is int selectedIndex && selectedIndex >= 0 && selectedIndex < roster.Characters.Count)
            roster.Select(roster.Characters[selectedIndex]);

        return roster;
    }

    public void Save(CharacterRoster roster)
    {
        var savedRoster = new RosterSaveData
        {
            SelectedCharacterIndex = roster.SelectedCharacter is null ? null : Enumerable.Range(0, roster.Characters.Count).FirstOrDefault(index => roster.Characters[index] == roster.SelectedCharacter),
            Characters = roster.Characters.Select(CreateSaveData).ToList()
        };
        File.WriteAllText(_filePath, JsonSerializer.Serialize(savedRoster, JsonOptions));
    }

    private LiveCharacter CreateLiveCharacter(CharacterSaveData saved)
    {
        var race = FindSavedDefinition(_gameData.Races, saved.RaceId, saved.RaceName, "faj");
        var characterClass = FindSavedDefinition(_gameData.CharacterClasses, saved.CharacterClassId, saved.CharacterClassName, "osztály");
        var character = new LiveCharacter(saved.Name, race, characterClass, saved.Abilities,
            _gameData.GetMinimumVitality(saved.Abilities.Health) + saved.VitalityBonus,
            characterClass.UsesMana ? _gameData.GetMinimumMana(saved.Abilities.Intelligence) + saved.ManaBonus : 0,
            saved.VitalityBonus, characterClass.UsesMana ? saved.ManaBonus : 0);
        character.SetCurrentResources(saved.CurrentVitality, saved.CurrentMana);
        character.SetNeedLevels(saved.FoodLevel ?? 100, saved.WaterLevel ?? 100);

        var weaponIds = saved.WeaponIds.Count > 0 ? saved.WeaponIds : saved.WeaponNames;
        for (var index = 0; index < Math.Min(2, weaponIds.Count); index++)
            if (weaponIds[index] is { } weaponId) character.EquipWeapon(index, FindSavedDefinition(_gameData.Weapons, weaponId, saved.WeaponNames.ElementAtOrDefault(index), "fegyver"));
        if ((saved.ArmorId ?? saved.ArmorName) is { } armorId) character.EquipArmor(FindSavedDefinition(_gameData.Armors, armorId, saved.ArmorName, "páncél"));
        var magicItemIds = saved.MagicItemIds.Count > 0 ? saved.MagicItemIds : saved.MagicItemNames;
        for (var index = 0; index < magicItemIds.Count; index++)
            character.AddMagicItem(FindSavedDefinition(_gameData.MagicItems, magicItemIds[index], saved.MagicItemNames.ElementAtOrDefault(index), "varázstárgy"));
        foreach (var item in saved.BackpackItems) character.AddToBackpack(ResolveItem(item));

        return character;
    }

    private CharacterSaveData CreateSaveData(LiveCharacter character) => new()
    {
        Name = character.Name,
        RaceId = character.Race.Id,
        CharacterClassId = character.CharacterClass.Id,
        Abilities = character.Abilities,
        CurrentVitality = character.CurrentVitality,
        CurrentMana = character.CurrentMana,
        VitalityBonus = character.VitalityBonus,
        ManaBonus = character.ManaBonus,
        FoodLevel = character.FoodLevel,
        WaterLevel = character.WaterLevel,
        WeaponIds = character.WeaponSlots.Select(weapon => weapon?.Id).ToList(),
        ArmorId = character.Armor?.Id,
        MagicItemIds = character.MagicItems.Select(item => item.Id).ToList(),
        BackpackItems = character.Backpack.Select(item => new ItemSaveData(item.GetType().Name, item.Id)).ToList()
    };

    private IItemDefinition ResolveItem(ItemSaveData item) => item.Type switch
    {
        nameof(WeaponDefinition) => FindSavedDefinition(_gameData.Weapons, item.Id, item.Name, "fegyver"),
        nameof(ArmorDefinition) => FindSavedDefinition(_gameData.Armors, item.Id, item.Name, "páncél"),
        nameof(MagicItemDefinition) => FindSavedDefinition(_gameData.MagicItems, item.Id, item.Name, "varázstárgy"),
        nameof(MiscItemDefinition) => FindSavedDefinition(_gameData.Items, item.Id, item.Name, "tárgy"),
        _ => throw new InvalidOperationException($"Ismeretlen mentett tárgytípus: {item.Type}")
    };

    private static T FindSavedDefinition<T>(IReadOnlyList<T> definitions, string id, string? legacyName, string typeName) where T : IGameDefinition
    {
        var definition = definitions.FirstOrDefault(candidate => string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? definitions.FirstOrDefault(candidate => string.Equals(candidate.Name, legacyName, StringComparison.OrdinalIgnoreCase));
        return definition ?? throw new InvalidOperationException($"A mentésben szereplő '{id ?? legacyName}' {typeName} nem található az adatok.csv fájlban.");
    }

    private sealed class RosterSaveData
    {
        public int? SelectedCharacterIndex { get; init; }
        public List<CharacterSaveData> Characters { get; init; } = [];
    }

    private sealed class CharacterSaveData
    {
        public string Name { get; init; } = string.Empty;
        public string RaceId { get; init; } = string.Empty;
        public string CharacterClassId { get; init; } = string.Empty;
        // Régi mentések egyszeri betöltéséhez; új mentésbe már csak az ID-k kerülnek.
        public string? RaceName { get; init; }
        public string? CharacterClassName { get; init; }
        public PrimaryAbilities Abilities { get; init; }
        public int CurrentVitality { get; init; }
        public int CurrentMana { get; init; }
        public int VitalityBonus { get; init; }
        public int ManaBonus { get; init; }
        public int? FoodLevel { get; init; }
        public int? WaterLevel { get; init; }
        public List<string?> WeaponIds { get; init; } = [];
        public string? ArmorId { get; init; }
        public List<string> MagicItemIds { get; init; } = [];
        public List<string?> WeaponNames { get; init; } = [];
        public string? ArmorName { get; init; }
        public List<string> MagicItemNames { get; init; } = [];
        public List<ItemSaveData> BackpackItems { get; init; } = [];
    }

    private sealed record ItemSaveData(string Type, string Id, string? Name = null);
}
