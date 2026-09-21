using KaoszRubin.Application;
using KaoszRubin.Application.Quests;
using KaoszRubin.Combat;
using KaoszRubin.Data;
using KaoszRubin.Domain;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;
using KaoszRubin.Domain.Magic;
using KaoszRubin.Domain.Quests;
using KaoszRubin.Infrastructure;
using KaoszRubin.Infrastructure.Quests;
using KaoszRubin.UI;
using System.Runtime;
using System.Security.Cryptography.Xml;
using static KaoszRubin.UI.GameInput;
using MainMenu = KaoszRubin.UI.MainMenu;

namespace KaoszRubin.Application;

public sealed partial class Game
{
    private LevelUpResult AddExperience(int amount) => PartyLeader.AddExperience(
        amount,
        _gameData.ExperienceByLevel,
        _gameData.GetVitalityGrowth(PartyLeader.Abilities.Health),
        _gameData.GetManaGrowth(PartyLeader.Abilities.Intelligence),
        _gameData.GetCharacterResourceGrowth(PartyLeader.CharacterClass.Id),
        _random);

    private IReadOnlyList<ExperienceAward> DistributeExperience(LiveCharacter winner, int totalExperience, bool isQuest) =>
        _progressionService.DistributeExperience(winner, totalExperience, CharacterRoster.Party.Members, isQuest);

    private static readonly HashSet<string> MerchantExcludedItemIds = ["W001", "W005", "A001", "A002",
        // Witcher-only consumables (potions and medical supplies)
        "T011", "T012", "T013", "T014", "T015", "T016", "T017", "T018", "T019", "T020",
        // Secret-stash-only drinks
        "T023", "T024"];


    private IReadOnlyList<IItemDefinition> AllTradableItems() => _gameData.Items.Cast<IItemDefinition>()
        .Concat(_gameData.Weapons).Concat(_gameData.Armors).Concat(_gameData.MagicItems)
        .Where(item => !SpellcastingRules.IsRestrictedFromTradingAndGeneration(item))
        .Where(item => !MerchantExcludedItemIds.Contains(item.Id)).ToList();

    private ExperienceAward AwardExperience(LiveCharacter character, int amount) =>
        _progressionService.AwardExperience(character, amount);

    private LevelUpResult AwardExperienceResult(LiveCharacter character, int amount) =>
        _progressionService.AwardExperienceResult(character, amount);

    private static string FormatExperienceAwards(IEnumerable<ExperienceAward> awards) =>
        CharacterProgressionService.FormatExperienceAwards(awards);

    private void GrantPartyExperienceForDevelopment()
    {
        var awards = CharacterRoster.Party.Members.Where(character => character.IsAlive)
            .Select(character => AwardExperience(character, 5000)).ToList();
        foreach (var award in awards.Where(award => award.Result.LeveledUp))
            ResolvePerkOffers(award.Character, award.Result);
        var weaponGrants = CharacterRoster.Party.Members.Select(character =>
            $"{character.Name}: {DevelopmentWeaponGrantService.Grant(character, _gameData.Weapons, _random).Count}/6 fegyver").ToList();
        _maze.AddCorpse(new MonsterCorpse(_player.Position, "tesztHulla", _maze.Enemies.First().Definition.Id,
            guaranteedLootIds: ["T001", "T002"]));

        _renderer.RefreshCharacterSheet(PartyLeader);
        _renderer.DrawDeveloperMessage($"Fejlesztői mód: 5000 XP minden partitagnak + fegyverek + loot. {FormatExperienceAwards(awards)} " +
            string.Join("; ", weaponGrants));
    }

    private void TriggerDeveloperLevelUp()
    {
        var neededExperience = PartyLeader.GetExperienceNeededForNextLevel(_gameData.ExperienceByLevel);
        if (neededExperience <= 0)
        {
            _renderer.DrawDeveloperMessage("Fejlesztői mód: a karakter már elérte a maximális szintet.");
            return;
        }

        var result = AddExperience(neededExperience);
        ResolvePerkOffers(PartyLeader, result);
        _renderer.RefreshCharacterSheet(PartyLeader);
    }

    private void ResolvePerkOffers(LiveCharacter character, LevelUpResult result)
    {
        var offers = CreatePerkOffers(character, result);
        var control = _session.CharacterControls.FirstOrDefault(candidate => candidate.CharacterId == character.Id);
        if (control is { ControllerKind: CharacterControllerKind.RemotePlayer,
                ConnectionState: PlayerConnectionState.Connected, AssignedPlayerId: not null })
        {
            ResolveRemoteLevelUp(character, result, offers);
            return;
        }
        
        _activeLevelUpPrompt = new LevelUpPromptSnapshot(Guid.NewGuid(), character.Id, character.Name,
            LevelUpPromptKind.Summary, result.PreviousLevel, result.CurrentLevel, result.VitalityGained,
            result.ManaGained, [], "A vezető véglegesíti a fejlődési döntéseket…",
            result.Bonuses.Select(bonus => new LevelUpBonusSnapshot(bonus.Level, bonus.Vitality, bonus.Mana))
                .ToArray());
        try
        {
            RunHostWindow($"Szintlépés — {character.Name}",
                $"A vezető {character.Name} szintlépési döntéseit kezeli…", () =>
                {
                    PlaySessionSound(SoundEffect.NewSkill, [character.Id]);
                    var selectedPerks = _renderer.DrawLevelUpScreen(character, result, offers);
                    foreach (var perk in selectedPerks)
                        if (character.AddPerk(perk))
                            character.ApplyPerkAcquisitionBonus(perk);
                    if (ShouldChooseSpecialization(character, offers)) ResolveLocalSpecialization(character);
                    ResolveLocalClassFeatureUpgrades(character, result);
                    ResolveLocalTacticalDisciplines(character, result);
                    ResolveLocalAbilityIncreases(character, result);
                    ResolveLocalWeaponProficiencies(character, result);
                    ResolveSpellLearning(character, result);
                });
        }
        finally
        {
            _activeLevelUpPrompt = null;
            ForceCoopSnapshotPublish();
        }
    }

    private void ResolveRemoteLevelUp(LiveCharacter character, LevelUpResult result,
        IReadOnlyList<PerkOffer> offers)
    {
        WaitForRemoteLevelUpChoice(character, result, LevelUpPromptKind.Summary, [],
            offers.Count > 0
                ? "🌠 Új TEHETSÉG ébred benned! Nyomj meg egy billentyűt... 🌠"
                : "🌟 Nyomj meg egy billentyűt a kaland folytatásához! 🌟",
            CharacterProgressionService.UpcomingMilestones(character)
                .Select(text => new LevelUpTextLineSnapshot(text, ConsoleColor.DarkCyan)).ToArray());
        foreach (var offer in offers)
        {
            var choices = offer.Choices.Select(perk => new LevelUpChoiceSnapshot(perk.Id, perk.Name, perk.Description)).ToArray();
            PlaySessionSound(SoundEffect.NewSkill, [character.Id]);
            var selectedId = WaitForRemoteLevelUpChoice(character, result, LevelUpPromptKind.PerkChoice, choices,
                $"{offer.Tier}. tehetségfokozat — a nem választott tehetség végleg elveszik.",
                [new($"{character.Name} — {character.CharacterClass.Name} — {offer.Tier}. fokozat", ConsoleColor.Cyan),
                 new($"A tehetség a {offer.TriggerLevel}. szint elérésekor vált elérhetővé.", ConsoleColor.DarkCyan),
                 new("A nem választott tehetség végleg elveszik ennél a karakternél.", ConsoleColor.Red)]);
            var perk = offer.Choices.FirstOrDefault(candidate => candidate.Id == selectedId) ?? offer.Choices[0];
            if (character.AddPerk(perk))
            {
                character.ApplyPerkAcquisitionBonus(perk);
            }
            if (offer.Tier == 1) ResolveRemoteSpecialization(character, result);
        }
        if (ShouldChooseSpecialization(character, offers)) ResolveRemoteSpecialization(character, result);
        ResolveRemoteClassFeatureUpgrades(character, result);
        ResolveRemoteTacticalDisciplines(character, result);
        ResolveRemoteAbilityIncreases(character, result);
        ResolveRemoteWeaponProficiencies(character, result);
        ResolveRemoteSpellLearning(character, result);
    }

    private static bool ShouldChooseSpecialization(LiveCharacter character, IReadOnlyList<PerkOffer> offers) =>
        CharacterProgressionService.ShouldChooseSpecialization(character, offers);

    private void ResolveLocalSpecialization(LiveCharacter character)
    {
        if (character.SpecializationId is not null) return;
        var choices = ClassSpecializations.ForClass(character.CharacterClass.Id);
        if (choices.Count > 0) character.ChooseSpecialization(_renderer.DrawSpecializationChoice(character, choices).Id);
    }

    private void ResolveRemoteSpecialization(LiveCharacter character, LevelUpResult result)
    {
        if (character.SpecializationId is not null) return;
        var choices = ClassSpecializations.ForClass(character.CharacterClass.Id);
        if (choices.Count == 0) return;
        var projected = choices.Select(choice => new LevelUpChoiceSnapshot(choice.Id, choice.Name, choice.Description)).ToArray();
        var selectedId = WaitForRemoteLevelUpChoice(character, result, LevelUpPromptKind.SpecializationChoice,
            projected, "Válassz végleges papi vagy mágusi specializációt.",
            [new($"{character.Name} — {character.CharacterClass.Name}", ConsoleColor.Cyan),
             new("Ez a választás végleges.", ConsoleColor.Red)]);
        character.ChooseSpecialization(choices.FirstOrDefault(choice => choice.Id == selectedId)?.Id ?? choices[0].Id);
    }

    private static IEnumerable<int> PendingClassFeatureMilestones(LiveCharacter character, LevelUpResult result) =>
        CharacterProgressionService.PendingClassFeatureMilestones(character, result);

    private void ResolveLocalClassFeatureUpgrades(LiveCharacter character, LevelUpResult result)
    {
        foreach (var milestone in PendingClassFeatureMilestones(character, result).ToArray())
        {
            var choices = ClassFeatureUpgrades.ForClass(character.CharacterClass.Id)
                .Where(choice => !character.HasClassFeatureUpgrade(choice.Id)).ToArray();
            if (choices.Length == 0) return;
            character.ChooseClassFeatureUpgrade(
                _renderer.DrawClassFeatureUpgradeChoice(character, choices, milestone).Id);
        }
    }

    private void ResolveRemoteClassFeatureUpgrades(LiveCharacter character, LevelUpResult result)
    {
        foreach (var milestone in PendingClassFeatureMilestones(character, result).ToArray())
        {
            var choices = ClassFeatureUpgrades.ForClass(character.CharacterClass.Id)
                .Where(choice => !character.HasClassFeatureUpgrade(choice.Id)).ToArray();
            if (choices.Length == 0) return;
            var projected = choices.Select(choice =>
                new LevelUpChoiceSnapshot(choice.Id, choice.Name, choice.Description)).ToArray();
            var selectedId = WaitForRemoteLevelUpChoice(character, result, LevelUpPromptKind.ClassFeatureChoice,
                projected, $"{milestone}. szint — válassz végleges osztályképesség-fejlesztést.",
                [new($"{character.Name} — {character.CharacterClass.Name} — {milestone}. szint", ConsoleColor.Cyan),
                 new("A választás végleges; a 20. szinten egy másik fejlesztés választható.", ConsoleColor.Red)]);
            character.ChooseClassFeatureUpgrade(choices.FirstOrDefault(choice => choice.Id == selectedId)?.Id ?? choices[0].Id);
        }
    }

    private void ResolveLocalTacticalDisciplines(LiveCharacter character, LevelUpResult result)
    {
        foreach (var milestone in CharacterProgressionService
                     .PendingTacticalDisciplineMilestones(character, result).ToArray())
        {
            var choices = CharacterProgressionService.TacticalDisciplineChoices(character);
            if (choices.Count == 0) return;
            character.ChooseTacticalDiscipline(
                _renderer.DrawTacticalDisciplineChoice(character, choices, milestone).Id);
        }
    }

    private void ResolveRemoteTacticalDisciplines(LiveCharacter character, LevelUpResult result)
    {
        foreach (var milestone in CharacterProgressionService
                     .PendingTacticalDisciplineMilestones(character, result).ToArray())
        {
            var choices = CharacterProgressionService.TacticalDisciplineChoices(character);
            if (choices.Count == 0) return;
            var projected = choices.Select(choice =>
                new LevelUpChoiceSnapshot(choice.Id, choice.Name, choice.Description)).ToArray();
            var selectedId = WaitForRemoteLevelUpChoice(character, result,
                LevelUpPromptKind.TacticalDisciplineChoice, projected,
                $"{milestone}. szint — válassz taktikai diszciplínát.",
                [new($"{character.Name} — {character.CharacterClass.Name} — {milestone}. szint", ConsoleColor.Cyan),
                 new("Két különböző diszciplína tanulható: egy a 8., egy a 18. szinten.", ConsoleColor.Green)]);
            character.ChooseTacticalDiscipline(
                choices.FirstOrDefault(choice => choice.Id == selectedId)?.Id ?? choices[0].Id);
        }
    }

    private static IReadOnlyList<(string Id, string Name, string Description)> AbilityIncreaseChoices(
        LiveCharacter character) => CharacterProgressionService.AbilityIncreaseChoices(character);

    private void ResolveLocalAbilityIncreases(LiveCharacter character, LevelUpResult result)
    {
        var earned = result.CurrentLevel / 3;
        while (character.AbilityIncreasesClaimed < earned)
        {
            var choices = AbilityIncreaseChoices(character);
            if (choices.Count == 0) { character.ClaimUnspendableAbilityIncrease(); continue; }
            var milestone = (character.AbilityIncreasesClaimed + 1) * 3;
            ApplyAbilityIncrease(character, _renderer.DrawAbilityIncreaseChoice(character, choices, milestone));
        }
    }

    private void ResolveRemoteAbilityIncreases(LiveCharacter character, LevelUpResult result)
    {
        var earned = result.CurrentLevel / 3;
        while (character.AbilityIncreasesClaimed < earned)
        {
            var choices = AbilityIncreaseChoices(character);
            if (choices.Count == 0) { character.ClaimUnspendableAbilityIncrease(); continue; }
            var milestone = (character.AbilityIncreasesClaimed + 1) * 3;
            var projected = choices.Select(choice =>
                new LevelUpChoiceSnapshot(choice.Id, choice.Name, choice.Description)).ToArray();
            var selectedId = WaitForRemoteLevelUpChoice(character, result, LevelUpPromptKind.AbilityChoice,
                projected, $"{milestone}. szint — növelj meg egy képességet 1 ponttal (maximum 13).",
                [new($"{character.Name} — {milestone}. szint", ConsoleColor.Cyan),
                 new("Növelj meg egy képességet 1 ponttal! Maximum: 13.", ConsoleColor.Green)]);
            ApplyAbilityIncrease(character,
                choices.FirstOrDefault(choice => choice.Id == selectedId).Id ?? choices[0].Id);
        }
    }

    private bool ApplyAbilityIncrease(LiveCharacter character, string abilityId)
    {
        if (!_progressionService.ApplyAbilityIncrease(character, abilityId)) return false;
        PlaySessionSound(SoundEffect.NewSkill, [character.Id]);
        return true;
    }

    private static int EarnedWeaponProficiencyAdvances(LiveCharacter character, int level) =>
        CharacterProgressionService.EarnedWeaponProficiencyAdvances(character, level);

    private IReadOnlyList<(string Id, string Name, string Description)> WeaponProficiencyChoices(
        LiveCharacter character) => _progressionService.WeaponProficiencyChoices(character);

    private static int NextWeaponProficiencyMilestone(LiveCharacter character) =>
        CharacterProgressionService.NextWeaponProficiencyMilestone(character);

    private void ResolveLocalWeaponProficiencies(LiveCharacter character, LevelUpResult result)
    {
        var earned = EarnedWeaponProficiencyAdvances(character, result.CurrentLevel);
        PlaySessionSound(SoundEffect.NewWeaponProficiency, [character.Id]);
        while (character.WeaponProficiencyAdvances < earned)
        {
            var choices = WeaponProficiencyChoices(character);
            if (choices.Count == 0) return;
            var milestone = NextWeaponProficiencyMilestone(character);
            character.TryAdvanceWeaponProficiency(_renderer.DrawWeaponProficiencyChoice(character, choices, milestone));
        }
    }

    private void ResolveRemoteWeaponProficiencies(LiveCharacter character, LevelUpResult result)
    {
        var earned = EarnedWeaponProficiencyAdvances(character, result.CurrentLevel);
        PlaySessionSound(SoundEffect.NewWeaponProficiency, [character.Id]);
        while (character.WeaponProficiencyAdvances < earned)
        {
            var choices = WeaponProficiencyChoices(character);
            if (choices.Count == 0) return;
            var milestone = NextWeaponProficiencyMilestone(character);
            var projected = choices.Select(choice =>
                new LevelUpChoiceSnapshot(choice.Id, choice.Name, choice.Description)).ToArray();
            var selectedId = WaitForRemoteLevelUpChoice(character, result, LevelUpPromptKind.WeaponProficiencyChoice,
                projected, $"{milestone}. szint — válassz fegyverjártassági fejlesztést.",
                [new($"{character.Name} — {(milestone == 1 ? "karakteralkotás" : $"{milestone}. szint")}", ConsoleColor.Cyan),
                 new("Legfeljebb két fegyvercsalád tanulható; egy család Jártas, majd Mester lehet.", ConsoleColor.Green)]);
            character.TryAdvanceWeaponProficiency(choices.FirstOrDefault(choice => choice.Id == selectedId).Id ?? choices[0].Id);
        }
    }

    private void ResolveRemoteSpellLearning(LiveCharacter character, LevelUpResult result)
    {
        if (!character.IsSpellcaster) return;
        var simulatedKnown = character.KnownSpells.Select(spell => spell.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var learningCount = result.Bonuses.Count(bonus =>
        {
            if (!SpellcastingRules.TryGetSchool(character.CharacterClass.Id, out var school)) return false;
            var candidate = _gameData.Spells.FirstOrDefault(spell => !spell.EnemyOnly && spell.School == school &&
                spell.Level <= SpellcastingRules.MaximumSpellLevel(character.CharacterClass.Id, bonus.Level) && !simulatedKnown.Contains(spell.Id));
            if (candidate is null) return false;
            simulatedKnown.Add(candidate.Id);
            return true;
        });
        var learnedNumber = 0;
        foreach (var bonus in result.Bonuses)
        {
            var choices = SpellcastingRules.AvailableUnknownSpells(character, _gameData, bonus.Level);
            if (choices.Count == 0) continue;
            learnedNumber++;
            var projected = choices.Select(spell => new LevelUpChoiceSnapshot(spell.Id,
                $"{spell.Level}. szint — {spell.Name}", spell.Description)).ToArray();
            PlaySessionSound(SoundEffect.NewSpellUnlocked, [character.Id]);
            var selectedId = WaitForRemoteLevelUpChoice(character, result, LevelUpPromptKind.SpellChoice,
                projected, $"{learnedNumber}/{learningCount}. új varázslat");
            character.LearnSpell(choices.FirstOrDefault(spell => spell.Id == selectedId) ?? choices[0]);
        }
    }

    private string? WaitForRemoteLevelUpChoice(LiveCharacter character, LevelUpResult result,
        LevelUpPromptKind kind, IReadOnlyList<LevelUpChoiceSnapshot> choices, string message,
        IReadOnlyList<LevelUpTextLineSnapshot>? contextLines = null)
    {
        var previousPhase = _session.Phase;
        _activeLevelUpPrompt = new LevelUpPromptSnapshot(Guid.NewGuid(), character.Id, character.Name, kind,
            result.PreviousLevel, result.CurrentLevel, result.VitalityGained, result.ManaGained, choices, message,
            result.Bonuses.Select(bonus =>
                new LevelUpBonusSnapshot(bonus.Level, bonus.Vitality, bonus.Mana)).ToArray(), contextLines);
        _levelUpResponse = null;
        _levelUpPromptCompleted = false;
        _session.SetPhase(GameSessionPhase.Paused);
        CoopWindowStatusBanner.Refresh(() => $"Várakozás {character.Name} szintlépési döntésére...");
        var replicatedLines = kind == LevelUpPromptKind.Summary
            ? LevelUpWindow.BuildSummary(character.Name, result.PreviousLevel, result.CurrentLevel,
                _activeLevelUpPrompt.Bonuses ?? [], result.VitalityGained, result.ManaGained, character.UsesMana,
                character.CurrentVitality, character.MaximumVitality, character.CurrentMana,
                character.MaximumMana, message, contextLines?.Select(line => line.Text).ToArray())
            : LevelUpWindow.UsesSwordFrame(kind)
                ? LevelUpWindow.BuildChoice(kind, contextLines ?? [], choices, 0)
                : MagicProgressionWindow.BuildLearning(character.Name, message, choices, 0);
        var replicatedWindow = kind == LevelUpPromptKind.Summary
            ? FramedWindow.LevelUp
            : LevelUpWindow.UsesSwordFrame(kind) ? FramedWindow.LevelUpChoice : FramedWindow.SpellLearning;
        var replicatedWidth = kind == LevelUpPromptKind.Summary
            ? LevelUpWindow.Width
            : LevelUpWindow.UsesSwordFrame(kind) ? LevelUpWindow.ChoiceWidth(kind) :
                MagicProgressionWindow.LearningWidth;
        _renderer.DrawReplicatedWindow(replicatedWidth, replicatedLines, replicatedWindow);
        PlaySessionSound(SoundEffect.Waiting, [PartyLeader.Id]);
        RequestCoopSnapshotPublish();
        while (!_levelUpPromptCompleted)
        {
            ProcessSessionCommands();
            var stillConnected = _session.CharacterControls.Any(control => control.CharacterId == character.Id &&
                control.ControllerKind == CharacterControllerKind.RemotePlayer &&
                control.ConnectionState == PlayerConnectionState.Connected);
            if (!stillConnected) break;
            TryPublishScheduledCoopSnapshot(DateTime.UtcNow);
            Thread.Sleep(20);
        }
        var response = _levelUpResponse;
        _activeLevelUpPrompt = null;
        _levelUpResponse = null;
        _levelUpPromptCompleted = false;
        _renderer.ClearReplicatedWindow();
        CoopWindowStatusBanner.Clear();
        _session.SetPhase(previousPhase);
        RequestCoopSnapshotPublish();
        return response;
    }

    private void ResolveSpellLearning(LiveCharacter character, LevelUpResult result)
    {
        if (!character.IsSpellcaster) return;
        var simulatedKnown = character.KnownSpells.Select(spell => spell.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var learningCount = 0;
        foreach (var bonus in result.Bonuses)
        {
            if (!SpellcastingRules.TryGetSchool(character.CharacterClass.Id, out var school)) break;
            var simulatedChoice = _gameData.Spells.FirstOrDefault(spell => !spell.EnemyOnly && spell.School == school &&
                spell.Level <= SpellcastingRules.MaximumSpellLevel(character.CharacterClass.Id, bonus.Level) && !simulatedKnown.Contains(spell.Id));
            if (simulatedChoice is null) continue;
            simulatedKnown.Add(simulatedChoice.Id);
            learningCount++;
        }
        var learnedNumber = 0;
        foreach (var bonus in result.Bonuses)
        {
            var choices = SpellcastingRules.AvailableUnknownSpells(character, _gameData, bonus.Level);
            if (choices.Count > 0)
            {
                learnedNumber++;
                PlaySessionSound(SoundEffect.NewSpellUnlocked, [character.Id]);
                character.LearnSpell(_renderer.DrawSpellLearningScreen(character, choices, learnedNumber, learningCount));
            }
        }
    }

    private IReadOnlyList<PerkOffer> CreatePerkOffers(LiveCharacter character, LevelUpResult result) =>
        _progressionService.CreatePerkOffers(character, result);
}
