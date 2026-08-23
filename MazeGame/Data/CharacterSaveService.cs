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
        if (!File.Exists(_filePath)) return new CharacterRoster();
        return Deserialize(File.ReadAllText(_filePath));
    }

    public CharacterRoster Deserialize(string json)
    {
        var roster = new CharacterRoster();
        var savedRoster = JsonSerializer.Deserialize<RosterSaveData>(json, JsonOptions) ?? new RosterSaveData();
        foreach (var savedCharacter in savedRoster.Characters)
            roster.Add(CreateLiveCharacter(savedCharacter));

        if (savedRoster.SelectedCharacterIndex is int selectedIndex && selectedIndex >= 0 && selectedIndex < roster.Characters.Count)
        {
            roster.Select(roster.Characters[selectedIndex]);
            var partyMembers = savedRoster.PartyMemberIndices
                .Where(index => index >= 0 && index < roster.Characters.Count)
                .Select(index => roster.Characters[index]);
            roster.Party.Restore(roster.Characters[selectedIndex], partyMembers);
        }

        return roster;
    }

    public void Save(CharacterRoster roster)
        => File.WriteAllText(_filePath, Serialize(roster));

    public string Serialize(CharacterRoster roster)
    {
        var savedRoster = new RosterSaveData
        {
            SelectedCharacterIndex = roster.SelectedCharacter is null ? null : Enumerable.Range(0, roster.Characters.Count).FirstOrDefault(index => roster.Characters[index] == roster.SelectedCharacter),
            PartyMemberIndices = roster.Party.Members.Select(member => Enumerable.Range(0, roster.Characters.Count)
                .First(index => roster.Characters[index] == member)).ToList(),
            Characters = roster.Characters.Select(CreateSaveData).ToList()
        };
        return JsonSerializer.Serialize(savedRoster, JsonOptions);
    }

    private LiveCharacter CreateLiveCharacter(CharacterSaveData saved)
    {
        var race = FindSavedDefinition(_gameData.Races, saved.RaceId, saved.RaceName, "faj");
        var characterClass = FindSavedDefinition(_gameData.CharacterClasses, saved.CharacterClassId, saved.CharacterClassName, "osztály");
        var compatibleName = saved.Name[..Math.Min(saved.Name.Length, LiveCharacter.MaximumNameLength)];
        if (string.IsNullOrWhiteSpace(compatibleName)) compatibleName = "Névtelen";
        var character = new LiveCharacter(compatibleName, race, characterClass, saved.Abilities,
            _gameData.GetMinimumVitality(saved.Abilities.Health) + saved.VitalityBonus,
            characterClass.UsesMana ? _gameData.GetMinimumMana(saved.Abilities.Intelligence) + saved.ManaBonus : 0,
            saved.VitalityBonus, characterClass.UsesMana ? saved.ManaBonus : 0, saved.Color ?? ConsoleColor.Cyan);
        character.ApplySavedLevelGrowth(saved.LevelVitalityIncrease ?? 0, saved.LevelManaIncrease ?? 0);
        character.SetCurrentResources(saved.CurrentVitality, saved.CurrentMana);
        character.SetNeedLevels(saved.FoodLevel ?? 100, saved.WaterLevel ?? 100);
        character.SetGold(saved.Gold ?? 0);
        character.SetProgress(saved.Level ?? 1, saved.Experience ?? 0);
        character.SetNpcBehavior(saved.NpcBehavior);

        if (character.IsSpellcaster)
        {
            var knownSpells = saved.KnownSpellIds.Count > 0
                ? saved.KnownSpellIds.Select(_gameData.GetSpell).ToList()
                : DefaultLegacySpells(character);
            foreach (var spell in knownSpells) character.LearnSpell(spell);
            var memorizedIds = saved.MemorizedSpellIds.Count > 0
                ? saved.MemorizedSpellIds
                : knownSpells.Take(SpellcastingRules.StartingSpellCount).Select(spell => spell.Id).ToList();
            character.SetMemorizedSpells(knownSpells.Where(spell => memorizedIds.Contains(spell.Id, StringComparer.OrdinalIgnoreCase)));
        }

        var weaponIds = saved.WeaponIds.Count > 0 ? saved.WeaponIds : saved.WeaponNames;
        for (var index = 0; index < Math.Min(2, weaponIds.Count); index++)
            if (weaponIds[index] is { } weaponId) character.EquipWeapon(index, FindSavedDefinition(_gameData.Weapons, weaponId, saved.WeaponNames.ElementAtOrDefault(index), "fegyver"));
        if ((saved.ArmorId ?? saved.ArmorName) is { } armorId) character.EquipArmor(FindSavedDefinition(_gameData.Armors, armorId, saved.ArmorName, "páncél"));
        var magicItemIds = saved.MagicItemIds.Count > 0 ? saved.MagicItemIds : saved.MagicItemNames.Cast<string?>().ToList();
        for (var index = 0; index < Math.Min(LiveCharacter.MaximumMagicItemCount, magicItemIds.Count); index++)
            if (magicItemIds[index] is { } magicItemId)
                character.SetInventoryItem(InventorySlotKind.MagicItem, index,
                    FindSavedDefinition(_gameData.MagicItems, magicItemId, saved.MagicItemNames.ElementAtOrDefault(index), "varázstárgy"));
        for (var index = 0; index < Math.Min(LiveCharacter.MaximumBackpackItemCount, saved.BackpackItems.Count); index++)
            if (saved.BackpackItems[index] is { } item)
                character.SetInventoryItem(InventorySlotKind.Backpack, index, ResolveItem(item));
        foreach (var perkId in saved.PerkIds)
        {
            var perk = _gameData.GetPerk(perkId);
            character.AddPerk(perk);
            if (!saved.AppliedPerkBonusIds.Contains(perkId, StringComparer.OrdinalIgnoreCase))
                character.ApplyPerkAcquisitionBonus(perk);
        }
        var savedStatuses = saved.Statuses.Count > 0
            ? saved.Statuses
            : saved.StatusIds.Select(id => new StatusSaveData(id, null)).ToList();
        foreach (var savedStatus in savedStatuses.Where(status => status.Id is not CharacterStatusIds.Hungry and not CharacterStatusIds.Thirsty))
            character.RestoreStatus(_gameData.GetStatus(savedStatus.Id), savedStatus.RemainingActivations);
        character.SynchronizeNeedStatuses(_gameData.GetStatus(CharacterStatusIds.Hungry), _gameData.GetStatus(CharacterStatusIds.Thirsty));

        return character;
    }

    private CharacterSaveData CreateSaveData(LiveCharacter character) => new()
    {
        Name = character.Name,
        Color = character.Color,
        NpcBehavior = character.NpcBehavior,
        RaceId = character.Race.Id,
        CharacterClassId = character.CharacterClass.Id,
        Abilities = character.Abilities,
        CurrentVitality = character.CurrentVitality,
        CurrentMana = character.CurrentMana,
        VitalityBonus = character.VitalityBonus,
        ManaBonus = character.ManaBonus,
        FoodLevel = character.FoodLevel,
        WaterLevel = character.WaterLevel,
        Gold = character.Gold,
        Level = character.Level,
        Experience = character.Experience,
        LevelVitalityIncrease = character.UnmodifiedMaximumVitality - (_gameData.GetMinimumVitality(character.Abilities.Health) + character.VitalityBonus),
        LevelManaIncrease = character.UsesMana
            ? character.UnmodifiedMaximumMana - (_gameData.GetMinimumMana(character.Abilities.Intelligence) + character.ManaBonus)
            : 0,
        WeaponIds = character.WeaponSlots.Select(weapon => weapon?.Id).ToList(),
        ArmorId = character.Armor?.Id,
        MagicItemIds = character.MagicItems.Select(item => item?.Id).ToList(),
        BackpackItems = character.Backpack.Select(item => item is null ? null : new ItemSaveData(item.GetType().Name, item.Id)).ToList(),
        PerkIds = character.Perks.Select(perk => perk.Id).ToList(),
        AppliedPerkBonusIds = character.Perks.Select(perk => perk.Id).ToList(),
        StatusIds = character.Statuses.Select(status => status.Id).ToList(),
        Statuses = character.Statuses.Select(status => new StatusSaveData(status.Id, character.GetStatusDuration(status.Id))).ToList(),
        KnownSpellIds = character.KnownSpells.Select(spell => spell.Id).ToList(),
        MemorizedSpellIds = character.MemorizedSpells.Select(spell => spell.Id).ToList()
    };

    private List<SpellDefinition> DefaultLegacySpells(LiveCharacter character)
    {
        SpellcastingRules.TryGetSchool(character.CharacterClass.Id, out var school);
        return _gameData.GetSpells(school, 1).OrderBy(spell => spell.Id).Take(SpellcastingRules.StartingSpellCount).ToList();
    }

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
        public List<int> PartyMemberIndices { get; init; } = [];
        public List<CharacterSaveData> Characters { get; init; } = [];
    }

    private sealed class CharacterSaveData
    {
        public string Name { get; init; } = string.Empty;
        public ConsoleColor? Color { get; init; }
        public NpcBehavior? NpcBehavior { get; init; }
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
        public int? Gold { get; init; }
        public int? Level { get; init; }
        public int? Experience { get; init; }
        public int? LevelVitalityIncrease { get; init; }
        public int? LevelManaIncrease { get; init; }
        public List<string?> WeaponIds { get; init; } = [];
        public string? ArmorId { get; init; }
        public List<string?> MagicItemIds { get; init; } = [];
        public List<string?> WeaponNames { get; init; } = [];
        public string? ArmorName { get; init; }
        public List<string> MagicItemNames { get; init; } = [];
        public List<ItemSaveData?> BackpackItems { get; init; } = [];
        public List<string> PerkIds { get; init; } = [];
        public List<string> AppliedPerkBonusIds { get; init; } = [];
        public List<string> StatusIds { get; init; } = [];
        public List<StatusSaveData> Statuses { get; init; } = [];
        public List<string> KnownSpellIds { get; init; } = [];
        public List<string> MemorizedSpellIds { get; init; } = [];
    }

    private sealed record ItemSaveData(string Type, string Id, string? Name = null);
    private sealed record StatusSaveData(string Id, int? RemainingActivations);
}
