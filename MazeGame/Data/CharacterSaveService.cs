using System.Text.Json;
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
        var race = _gameData.GetRace(saved.RaceName);
        var characterClass = _gameData.GetCharacterClass(saved.CharacterClassName);
        var character = new LiveCharacter(saved.Name, race, characterClass, saved.Abilities,
            _gameData.GetMinimumVitality(saved.Abilities.Health) + saved.VitalityBonus,
            characterClass.UsesMana ? _gameData.GetMinimumMana(saved.Abilities.Intelligence) + saved.ManaBonus : 0,
            saved.VitalityBonus, characterClass.UsesMana ? saved.ManaBonus : 0);
        character.SetCurrentResources(saved.CurrentVitality, saved.CurrentMana);

        for (var index = 0; index < Math.Min(2, saved.WeaponNames.Count); index++)
            if (saved.WeaponNames[index] is { } weaponName) character.EquipWeapon(index, _gameData.GetWeapon(weaponName));
        if (saved.ArmorName is { } armorName) character.EquipArmor(_gameData.GetArmor(armorName));
        foreach (var itemName in saved.MagicItemNames) character.AddMagicItem(_gameData.GetMagicItem(itemName));
        foreach (var item in saved.BackpackItems) character.AddToBackpack(ResolveItem(item));

        return character;
    }

    private CharacterSaveData CreateSaveData(LiveCharacter character) => new()
    {
        Name = character.Name,
        RaceName = character.Race.Name,
        CharacterClassName = character.CharacterClass.Name,
        Abilities = character.Abilities,
        CurrentVitality = character.CurrentVitality,
        CurrentMana = character.CurrentMana,
        VitalityBonus = character.VitalityBonus,
        ManaBonus = character.ManaBonus,
        WeaponNames = character.WeaponSlots.Select(weapon => weapon?.Name).ToList(),
        ArmorName = character.Armor?.Name,
        MagicItemNames = character.MagicItems.Select(item => item.Name).ToList(),
        BackpackItems = character.Backpack.Select(item => new ItemSaveData(item.GetType().Name, item.Name)).ToList()
    };

    private IItemDefinition ResolveItem(ItemSaveData item) => item.Type switch
    {
        nameof(WeaponDefinition) => _gameData.GetWeapon(item.Name),
        nameof(ArmorDefinition) => _gameData.GetArmor(item.Name),
        nameof(MagicItemDefinition) => _gameData.GetMagicItem(item.Name),
        _ => throw new InvalidOperationException($"Ismeretlen mentett tárgytípus: {item.Type}")
    };

    private sealed class RosterSaveData
    {
        public int? SelectedCharacterIndex { get; init; }
        public List<CharacterSaveData> Characters { get; init; } = [];
    }

    private sealed class CharacterSaveData
    {
        public string Name { get; init; } = string.Empty;
        public string RaceName { get; init; } = string.Empty;
        public string CharacterClassName { get; init; } = string.Empty;
        public PrimaryAbilities Abilities { get; init; }
        public int CurrentVitality { get; init; }
        public int CurrentMana { get; init; }
        public int VitalityBonus { get; init; }
        public int ManaBonus { get; init; }
        public List<string?> WeaponNames { get; init; } = [];
        public string? ArmorName { get; init; }
        public List<string> MagicItemNames { get; init; } = [];
        public List<ItemSaveData> BackpackItems { get; init; } = [];
    }

    private sealed record ItemSaveData(string Type, string Name);
}
