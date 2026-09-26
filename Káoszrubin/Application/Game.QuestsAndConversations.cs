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
    private bool EncounterWorldNpc(WorldNpc npc)
    {
        if (!npc.CanStartConversation) return false;
        return RunHostWindow($"Beszélgetés — {npc.Character.Name}",
            $"A vezető {npc.Character.Name} párbeszédét kezeli…", () => EncounterWorldNpcCore(npc));
    }

    private IReadOnlyList<NpcQuestUiEntry> GetNpcQuestUiEntries(WorldNpc npc)
    {
        if (!npc.IsQuestNpc) return Array.Empty<NpcQuestUiEntry>();

        var npcId =
            LegacyNpcIdMap.ToQuestNpcId(
                npc.DefinitionId);

        var instanceId =
            _questWorldContext.GetInstanceId(
                npc);

        return _questManager
            .For(npcId, instanceId)
            .GetQuests()
            .Select(quest =>
                new NpcQuestUiEntry(
                    quest.Title,
                    quest.State,
                    quest.Progress,
                    quest.RequiredCount))
            .ToArray();
    }

    private bool EncounterWorldNpcCore(WorldNpc npc)
    {
        var definition = npc.DefinitionId == "NPC-FIRST-COMPANION"
            ? new NpcDefinition(
                "NPC-FIRST-COMPANION",
                npc.Character.Name,
                "na",
                NpcDisposition.Friendly,
                NpcWorldBehavior.Friendly,
                true,
                false)
            : _gameData.GetNpc(npc.DefinitionId);
        if (definition.Unique && string.Equals(definition.StoryId, EliraStoryId, StringComparison.OrdinalIgnoreCase))
        {
            ConverseWithFirstUniqueNpc(npc);
            _renderer.CharacterSheet.RefreshCharacterSheet();
            return false;
        }
        if (definition.Unique && string.Equals(definition.StoryId, RodericStoryId, StringComparison.OrdinalIgnoreCase))
        {
            ConverseWithRoderic(npc);
            _renderer.CharacterSheet.RefreshCharacterSheet();
            return false;
        }
        if (definition.Unique)
        {
            _renderer.DrawUniqueNpcIntroduction(npc);
            _renderer.CharacterSheet.RefreshCharacterSheet();
            return false;
        }
        ProcessQuestProgressChanges(_questManager.SynchronizeCollectQuests());
        var questNpc = _questManager.For(LegacyNpcIdMap.ToQuestNpcId(npc.DefinitionId),
            _questWorldContext.GetInstanceId(npc));
        var unfinishedQuests = questNpc.GetActiveQuests().Where(quest => quest.IsActive).ToArray();
        var reminder = unfinishedQuests.Length == 0
            ? null
            : QuestReminderText.Build(npc.Character, unfinishedQuests, _random.Next(QuestReminderText.TemplateCount));
        var result = _renderer.DrawWorldNpcRecruitment(npc, CanNpcJoin(npc), GetNpcQuestUiEntries(npc), reminder);
        ProcessNpcQuests(npc);
        if (result == WorldNpcInteractionResult.Continue)
        {
            _renderer.CharacterSheet.RefreshCharacterSheet();
            return true;
        }
        if (result == WorldNpcInteractionResult.Join && CharacterRoster.Party.Add(npc.Character))
        {
            npc.Character.SetNpcJoinOrigin(_mazeLevel, "A pályán csatlakozott");
            _maze.RemoveWorldNpc(npc);
            var avatar = new PartyMemberAvatar(npc.Position, npc.Character);
            _maze.AddPartyMember(avatar);
            _nextPartyMoves[avatar] = DateTime.UtcNow;
            RevealFor(npc.Character, avatar.Position);
            _renderer.CharacterSheet.RefreshCharacterSheet();
            _renderer.DrawInventoryMessage($"🤝 {npc.Character.Name} ingyen csatlakozott a partihoz.", ConsoleColor.Green);
            RequestCoopSnapshotPublish();
            return false;
        }

        npc.Decline();
        _renderer.CharacterSheet.RefreshCharacterSheet();
        _renderer.DrawInventoryMessage(result == WorldNpcInteractionResult.Join ? "A parti megtelt; előbb helyet kell felszabadítani."
            : $"{npc.Character.Name} egyelőre itt marad.", ConsoleColor.Yellow);
        return false;
    }

    private void ConverseWithRoderic(WorldNpc npc)
    {
        if (npc.StoryStateId is "FOLLOWING" or "RELICS_ACTIVE" or "MALREC_FIGHT")
        {
            ProcessNpcQuests(npc, activateOffered: false, confirmTurnIn: false);
            if (RodericStoryProgression.NextState(npc.StoryStateId, _questManager.Roderic.Quests) is not { } next)
                return;
            npc.SetStoryState(next);
            if (next == "MALREC_DEFEATED")
            {
                _pendingRodericReturn = true;
                return;
            }
        }
        if (string.Equals(npc.StoryStateId, "PROOF_ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            var quest = _questManager.GetQuest(QuestId.RodericTheDeadAreNotPrey);
            if (quest.Progress < quest.RequiredCount)
            {
                ShowNpcStoryChoiceWithReplica(npc,
                    $"Bizonyítsátok hogy közös az ellenségünk. Eddig {quest.Progress}/{quest.RequiredCount} élőholt bukott el.",
                    ["Visszatérünk ha végeztünk."]);
                _renderer.CharacterSheet.RefreshCharacterSheet();
                return;
            }

            ProcessNpcQuests(npc, activateOffered: false);
            if (!quest.IsCompleted) return;
            npc.SetStoryState("PROOF_COMPLETE");
            RunStoryConversation(npc);
            _renderer.CharacterSheet.RefreshCharacterSheet();
            return;
        }

        if (string.Equals(npc.StoryStateId, "INSIGNIAS_ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            var count = CountPartyBackpackItems(MiscItemIds.FallenKnightInsignia);
            if (count < 3)
            {
                ShowNpcStoryChoiceWithReplica(npc,
                    $"Három jelvényt keressetek. Eddig {count}/3 került elő.",
                    ["Folytatjuk a keresést."]);
                _renderer.CharacterSheet.RefreshCharacterSheet();
                return;
            }

            ProcessNpcQuests(npc, activateOffered: false);
            if (!_questManager.GetQuest(QuestId.RodericFallenComradesInsignia).IsCompleted) return;
            npc.SetStoryState("CONFESSION");
            RunStoryConversation(npc);
            _renderer.CharacterSheet.RefreshCharacterSheet();
            return;
        }

        if (_gameData.GetNpcStoryChoices(npc.StoryId ?? string.Empty, npc.StoryStateId,
                npc.Friendliness).Count > 0)
        {
            RunStoryConversation(npc);
            if (string.Equals(npc.StoryStateId, "JOIN_ACCEPTED", StringComparison.OrdinalIgnoreCase))
                TryFinalizeRodericPermanentJoin();
            if (npc.StoryStateId == "MALREC_READY" && npc.State == WorldNpcState.Following)
                _pendingRodericExpedition = true;
            _renderer.CharacterSheet.RefreshCharacterSheet();
            return;
        }
        _renderer.DrawUniqueNpcIntroduction(npc);
        _renderer.CharacterSheet.RefreshCharacterSheet();
    }

    private void ShowNpcStoryChoiceWithReplica(WorldNpc npc, string prompt, IReadOnlyList<string> choices)
    {
        var conversationId = Guid.NewGuid();
        _activeAdHocConversation = new AdHocConversationSnapshot(conversationId, npc.Character.Name,
            npc.Character.Race.Name, npc.Character.CharacterClass.Name, [], prompt, choices);
        ForceCoopSnapshotPublish();
        try
        {
            _renderer.DrawUniqueNpcStoryChoice(npc, prompt, choices);
            _renderer.CharacterSheet.RefreshCharacterSheet();
        }
        finally
        {
            _activeAdHocConversation = null;
        }
    }

    private void RunStoryConversation(WorldNpc npc)
    {
        var conversationId = Guid.NewGuid();
        var transcript = new List<string>();
        try
        {
            while (true)
            {
                var choices = _gameData.GetNpcStoryChoices(npc.StoryId ?? string.Empty, npc.StoryStateId,
                    npc.Friendliness);
                if (choices.Count == 0) return;
                _activeAdHocConversation = new AdHocConversationSnapshot(conversationId, npc.Character.Name,
                    npc.Character.Race.Name, npc.Character.CharacterClass.Name, transcript.ToArray(),
                    choices[0].Prompt, choices.Select(choice => choice.Text).ToArray());
                ForceCoopSnapshotPublish();
                var index = _renderer.DrawUniqueNpcStoryChoice(npc, choices[0].Prompt,
                    choices.Select(choice => choice.Text).ToArray(), transcript);
                var selected = choices[index];
                transcript.Add($"Te: {selected.Text}");
                transcript.AddRange(selected.Response.Split('|',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                npc.AdjustFriendliness(selected.FriendlinessChange);
                npc.SetStoryState(selected.NextStateId);
                ApplyNpcStoryAction(npc, selected);
                if (selected.ContinueConversation) continue;
                _activeAdHocConversation = new AdHocConversationSnapshot(conversationId, npc.Character.Name,
                    npc.Character.Race.Name, npc.Character.CharacterClass.Name, transcript.ToArray(),
                    string.Empty, []);
                ForceCoopSnapshotPublish();
                _renderer.DrawUniqueNpcStoryResponse(npc, transcript);
                return;
            }
        }
        finally
        {
            _activeAdHocConversation = null;
            ForceCoopSnapshotPublish();
        }
    }

    private void ApplyNpcStoryAction(WorldNpc npc, NpcStoryChoiceDefinition choice)
    {
        switch (choice.Action)
        {
            case NpcStoryAction.None:
            case NpcStoryAction.TravelToLocation:
            case NpcStoryAction.RequestPermanentJoin:
                return;
            case NpcStoryAction.ActivateQuest when choice.ActionParameter is { } questId:
                ActivateNpcQuest(npc, questId);
                return;
            case NpcStoryAction.BeginFollowing:
                if (npc.State == WorldNpcState.Following) return;
                if (!BeginTemporaryFollowing(npc))
                {
                    npc.SetStoryState(choice.StateId);
                    npc.AdjustFriendliness(-choice.FriendlinessChange);
                }
                return;
            default:
                throw new InvalidOperationException($"Hiányos NPC-történeti hatás: {choice.Id}/{choice.Action}.");
        }
    }

    private void ActivateNpcQuest(WorldNpc npc, string questId)
    {
        var quest = _questManager.For(LegacyNpcIdMap.ToQuestNpcId(npc.DefinitionId),
            _questWorldContext.GetInstanceId(npc)).GetQuest(LegacyQuestIdMap.ToQuestId(questId));
        if (quest.IsInProgress || quest.IsResolved) return;
        quest.Activate();
        var questMessage = $"📜 Új küldetés: {quest.Title} — {quest.Description} " +
            $"Jutalom: {quest.ExperienceReward} XP.";
        _renderer.DrawInventoryMessage(questMessage, ConsoleColor.Cyan);
        RecordSessionActivity(SessionActivityKind.Support, questMessage, ConsoleColor.Cyan);
        RequestCoopSnapshotPublish();
    }

    private void ConverseWithFirstUniqueNpc(WorldNpc npc)
    {
        if (npc.ConversationStage == 0)
        {
            var sameRaceMembers = CharacterRoster.Party.Members.Count(character =>
                string.Equals(character.Race.Id, npc.Character.Race.Id, StringComparison.OrdinalIgnoreCase));

            var affinity = string.Equals(
                PartyLeader.Race.Id,
                npc.Character.Race.Id,
                StringComparison.OrdinalIgnoreCase)
                    ? 2
                    : sameRaceMembers > 0
                        ? 1
                        : 0;

            if (affinity > 0)
            {
                npc.AdjustFriendliness(affinity);

                var affinityMessage =
                    $"🌿 Faji rokonszenv: Elira viszonya +{affinity}.";

                _renderer.DrawInventoryMessage(
                    affinityMessage,
                    ConsoleColor.Green);

                RecordSessionActivity(
                    SessionActivityKind.Support,
                    affinityMessage,
                    ConsoleColor.Green);
            }
        }

        var result = _renderer.DrawUniqueNpcConversation(npc);
        var friendlinessChange = result.FriendlinessChange;

        if (npc.ConversationStage == 2 &&
            result.ChoiceIndex == 1 &&
            !CharacterRoster.Party.Members.Any(character =>
                string.Equals(
                    character.Race.Id,
                    npc.Character.Race.Id,
                    StringComparison.OrdinalIgnoreCase)))
        {
            friendlinessChange = -1;
        }

        npc.AdjustFriendliness(friendlinessChange);

        if (result.ChoiceIndex >= 0)
            npc.AdvanceConversation();

        if (result.FollowRequested &&
            npc.State != WorldNpcState.Following)
        {
            BeginTemporaryFollowing(npc);
        }

        if (npc.State == WorldNpcState.Following)
            ProcessNpcQuests(npc);

        _renderer.CharacterSheet.RefreshCharacterSheet();

        var friendlinessColor =
            friendlinessChange >= 0
                ? ConsoleColor.Green
                : ConsoleColor.DarkYellow;

        var friendlinessMessage =
            $"🌿 Elira viszonya: {npc.Friendliness}/10.";

        _renderer.DrawInventoryMessage(
            friendlinessMessage,
            friendlinessColor);

        RecordSessionActivity(
            SessionActivityKind.Support,
            friendlinessMessage,
            friendlinessColor);
    }

    private bool BeginTemporaryFollowing(WorldNpc npc)
    {
        if (_maze.PartyMembers.Any(member => member.IsTemporaryFollower))
        {
            var message =
                "Már van egy ideiglenes követőtök.";

            _renderer.DrawInventoryMessage(
                message,
                ConsoleColor.DarkYellow);

            RecordSessionActivity(
                SessionActivityKind.Support,
                message,
                ConsoleColor.DarkYellow);

            return false;
        }

        var npcId =
            LegacyNpcIdMap.ToQuestNpcId(
                npc.DefinitionId);

        var instanceId =
            _questWorldContext.GetInstanceId(npc);

        var questNpc =
            _questManager.For(
                npcId,
                instanceId);

        // ------------------------------------------------------------
        // NPC áthelyezése a világból temporary followerként a partyba
        // ------------------------------------------------------------

        if (!_maze.RemoveWorldNpc(npc))
            return false;

        npc.BeginFollowing();

        var avatar =
            new PartyMemberAvatar(
                npc.Position,
                npc.Character,
                npc);

        _maze.AddPartyMember(avatar);

        _nextPartyMoves[avatar] =
            DateTime.UtcNow;

        // ------------------------------------------------------------
        // Az új quest-rendszer aktiválja az elérhető questeket
        // ------------------------------------------------------------

        var activatedQuests =
            questNpc.ActivateAvailableQuests();

        if (activatedQuests.Count > 0)
        {
            ForceCoopSnapshotPublish();

            foreach (var quest in activatedQuests)
            {
                var questMessage =
                    $"📜 Új küldetés: {quest.Title} — " +
                    $"{quest.Description} " +
                    $"Jutalom: {quest.ExperienceReward} XP" +
                    $"{DescribeQuestItemRewards(quest)}.";

                _renderer.DrawInventoryMessage(
                    questMessage,
                    ConsoleColor.Cyan);

                RecordSessionActivity(
                    SessionActivityKind.Support,
                    questMessage,
                    ConsoleColor.Cyan);
            }
        }

        // ------------------------------------------------------------
        // Quest offer UI
        // ------------------------------------------------------------

        var offers =
            activatedQuests
                .Select(QuestPresentationSnapshot.From)
                .ToArray();

        if (offers.Length > 0)
        {
            if (npcId == QuestNpcId.EliraSilverbranch)
                _renderer.DrawUniqueNpcQuestOffer(npc, offers);
            else
                _renderer.DrawGenericUniqueNpcQuestOffer(npc, offers);
        }

        _renderer.CharacterSheet.RefreshCharacterSheet();

        var followerMessage =
            $"🌿 {npc.Character.Name} ideiglenes követőként csatlakozott. " +
            "Nem foglal partyhelyet.";

        _renderer.DrawInventoryMessage(
            followerMessage,
            ConsoleColor.Cyan);

        RecordSessionActivity(
            SessionActivityKind.Support,
            followerMessage,
            ConsoleColor.Cyan);

        return true;
    }

    private void ProcessNpcQuests(WorldNpc npc, bool activateOffered = true, QuestKey? selectedQuest = null,
        bool confirmTurnIn = true)
    {
        if (!npc.IsQuestNpc) return;
    
        var npcId =
            LegacyNpcIdMap.ToQuestNpcId(
                npc.DefinitionId);

        var instanceId =
            _questWorldContext.GetInstanceId(npc);

        var questNpc =
            _questManager.For(
                npcId,
                instanceId);

        // ------------------------------------------------------------
        // Új questek aktiválása
        // ------------------------------------------------------------

        if (activateOffered)
        {
            var activated =
                questNpc.ActivateAvailableQuests();

            foreach (var quest in activated)
            {
                var questMessage =
                    $"📜 Új küldetés: {quest.Title} — " +
                    $"{quest.Description} " +
                    $"Jutalom: {quest.ExperienceReward} XP" +
                    $"{DescribeQuestItemRewards(quest)}.";

                _renderer.DrawInventoryMessage(
                    questMessage,
                    ConsoleColor.Cyan);

                RecordSessionActivity(
                    SessionActivityKind.Support,
                    questMessage,
                    ConsoleColor.Cyan);
            }
        }

        // A collect questek progressze nem additív,
        // hanem az aktuális inventoryból származik.
        ProcessQuestProgressChanges(_questManager.SynchronizeCollectQuests());

        // Frissen kérjük le, mert az aktiválás és az inventory
        // szinkron közben változhatott az állapotuk.
        var quests =
            questNpc.GetActiveQuests()
                .Where(quest => selectedQuest is null || quest.Key == selectedQuest)
                .ToArray();

        foreach (var quest in quests)
        {
            // --------------------------------------------------------
            // Még nincs kész
            // --------------------------------------------------------

            if (!quest.IsReadyToTurnIn)
            {
                var progressMessage =
                    $"📜 {quest.Title}: " +
                    $"{quest.Progress}/{quest.RequiredCount}";

                _renderer.DrawInventoryMessage(
                    progressMessage,
                    ConsoleColor.DarkYellow);

                RecordSessionActivity(
                    SessionActivityKind.Support,
                    progressMessage,
                    ConsoleColor.DarkYellow);

                continue;
            }

            // --------------------------------------------------------
            // Leadási megerősítés
            // --------------------------------------------------------

            QuestCompletionResult completion;
            try
            {
                if (!confirmTurnIn) completion = quest.Complete();
                else if (!QuestTurnInService.TryComplete(
                             quest,
                             snapshot => RunHostWindow(
                                 $"Küldetés leadása — {snapshot.Title}",
                                 $"A vezető eldönti, hogy leadja-e a(z) {snapshot.Title} küldetést.",
                                 () => _renderer.ConfirmQuestTurnIn(
                                     npc.Character.Name,
                                     snapshot)),
                             out completion))
                {
                    var postponedMessage =
                        $"📜 {quest.Title}: a jutalom felvétele elhalasztva.";

                    _renderer.DrawInventoryMessage(
                        postponedMessage,
                        ConsoleColor.DarkYellow);

                    RecordSessionActivity(
                        SessionActivityKind.Support,
                        postponedMessage,
                        ConsoleColor.DarkYellow);

                    continue;
                }
            }
            catch (InvalidOperationException exception)
            {
                var errorMessage =
                    $"📜 {quest.Title}: " +
                    $"a küldetés most nem adható le. " +
                    $"{exception.Message}";

                _renderer.DrawInventoryMessage(
                    errorMessage,
                    ConsoleColor.DarkYellow);

                RecordSessionActivity(
                    SessionActivityKind.Support,
                    errorMessage,
                    ConsoleColor.DarkYellow);

                continue;
            }

            // --------------------------------------------------------
            // Unique NPC barátságosság
            // --------------------------------------------------------

            if (_gameData
                .GetNpc(npc.DefinitionId)
                .Unique)
            {
                npc.AdjustFriendliness(1);
            }

            // --------------------------------------------------------
            // Level-up / perk UI
            // --------------------------------------------------------

            foreach (var award in
                     completion.Rewards.LevelUpAwards)
            {
                ResolvePerkOffers(
                    award.Character,
                    award.Result);
            }

            if (completion.Rewards.HasLevelUps)
            {
                _renderer.RefreshCharacterSheet(
                    PartyLeader);
            }

            if (completion.Rewards.HasItemRewards)
            {
                PlaySessionSound(
                    SoundEffect.Item);
            }

            // --------------------------------------------------------
            // Legacy journal frissítése
            // --------------------------------------------------------

            var experienceSummary =
                FormatExperienceAwards(
                    completion.Rewards.ExperienceAwards);

            var itemRewards =
                FormatQuestItemRewards(
                    completion.Rewards);

            var completedEntry =
                _questJournal[quest.Key] with
                {
                    CompletionExperienceSummary =
                        experienceSummary,

                    CompletionItemRewardSummary =
                        string.IsNullOrWhiteSpace(itemRewards)
                            ? "nem volt tárgyjutalom"
                            : itemRewards
                };

            _questJournal[quest.Key] =
                completedEntry;
            _questSaveAdapter.RecordCompletion(quest, experienceSummary,
                completedEntry.CompletionItemRewardSummary!);

            // --------------------------------------------------------
            // Visszajelzés
            // --------------------------------------------------------

            var completionMessage =
                $"✅ Küldetés teljesítve: {quest.Title}. " +
                $"XP: {experienceSummary}." +
                (!string.IsNullOrWhiteSpace(itemRewards)
                    ? $" 🎁 {itemRewards}"
                    : string.Empty);

            _renderer.DrawInventoryMessage(
                completionMessage,
                ConsoleColor.Green);

            RecordSessionActivity(
                SessionActivityKind.Support,
                completionMessage,
                ConsoleColor.Green);

            RequestCoopSnapshotPublish();

            RunHostWindow(
                $"Küldetés teljesítve — {completedEntry.Title}",
                $"A vezető {completedEntry.Title} " +
                "küldetésének összegzését olvassa…",
                () => QuestCompletionWindow.Show(
                    completedEntry));
        }

        RequestCoopSnapshotPublish();
    }

    private bool CanNpcJoin(WorldNpc npc)
    {
        if (!npc.Recruitable)
            return false;

        if (!npc.IsQuestNpc)
            return true;

        var npcId =
            LegacyNpcIdMap.ToQuestNpcId(
                npc.DefinitionId);

        var instanceId =
            _questWorldContext.GetInstanceId(
                npc);

        return _questManager
            .For(npcId, instanceId)
            .AreAllQuestsResolved;
    }

    private static string DescribeQuestItemRewards(QuestHandle quest)
    {
        var parts =
            new List<string>();

        if (quest.FixedRewardItem is not null &&
            quest.FixedRewardItemCount > 0)
        {
            parts.Add(
                $"{quest.FixedRewardItem.Name} " +
                $"×{quest.FixedRewardItemCount}");
        }

        if (quest.RandomRewardCount > 0)
        {
            parts.Add(
                $"{quest.RandomRewardCount} véletlen tárgy");
        }

        return parts.Count == 0
            ? string.Empty
            : " + " + string.Join(
                " + ",
                parts);
    }

    private static string FormatQuestItemRewards(QuestRewardResult rewards)
    {
        if (rewards.ItemRewards.Count == 0)
            return string.Empty;

        var summary =
            string.Join(
                ", ",
                rewards.ItemRewards
                    .GroupBy(reward =>
                        reward.Item.Name)
                    .Select(group =>
                        $"{group.Key} ×{group.Count()}"));

        return rewards.DroppedItemCount == 0
            ? summary
            : $"{summary} " +
              $"({rewards.DroppedItemCount} a földön)";
    }

    private void ShowQuestJournal()
    {
        var options = BuildQuestFastTravelOptions();
        var selectedQuestId = RunHostPersonalWindow(PlayerWindowKind.QuestJournal,
            () => QuestJournalWindow.Show(OrderedQuestJournal(), options,
                coopStatusProvider: CurrentHostCoopWindowStatus));
        if (selectedQuestId?.FastTravelQuestId is { } questId)
            CompleteQuestByFastTravel(questId, options);
        else if (selectedQuestId?.AbandonedQuestId is { } abandonedQuestId)
            AbandonQuest(abandonedQuestId, notify: true);
    }

    private void AbandonQuest(QuestKey key, bool notify)
    {
        if (!_questManager.TryGetQuest(key, out var quest) || !quest.IsInProgress) return;
        quest.Abandon();
        if (notify)
        {
            var message = $"× Küldetés feladva: {quest.Title}.";
            _renderer.DrawInventoryMessage(message, ConsoleColor.DarkYellow);
            RecordSessionActivity(SessionActivityKind.System, message, ConsoleColor.DarkYellow);
        }
        RequestCoopSnapshotPublish();
    }
    private void AbandonActiveQuestsFromNpc(string npcId)
    {
        var giver = LegacyNpcIdMap.ToQuestNpcId(npcId);
        foreach (var quest in _questManager.GetActiveQuests().Where(quest => quest.Giver == giver))
            quest.Abandon();
        RequestCoopSnapshotPublish();
    }

    private IEnumerable<WorldNpc> CurrentQuestNpcs() =>
        _maze.WorldNpcs
            .Concat(_maze.PartyMembers.Where(member => member.TemporaryFollower is not null)
                .Select(member => member.TemporaryFollower!))
            .Concat(_temporaryFollowersEnteringNextMaze)
            .Distinct();

    private IReadOnlyList<QuestFastTravelOption> BuildQuestFastTravelOptions() =>
        new QuestTravelService(_questManager, _questWorldContext).BuildOptions(_maze.WorldNpcs,
            npc => FindQuestTravelDistance(_player.Position, npc.Position));
    private int? FindQuestTravelDistance(Position origin, Position destination)
    {
        var distances = new Dictionary<Position, int> { [origin] = 0 };
        var queue = new Queue<Position>();
        queue.Enqueue(origin);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == destination) return distances[current];
            foreach (var direction in Enum.GetValues<Direction>())
            {
                var next = current + direction;
                if (distances.ContainsKey(next) || !_maze.IsInside(next) ||
                    (!_maze.IsWalkable(next) && _maze.GetDoorAt(next) is null) ||
                    !_fogOfWar.IsRevealed(next)) continue;
                distances[next] = distances[current] + 1;
                queue.Enqueue(next);
            }
        }
        return null;
    }

    private void CompleteQuestByFastTravel(QuestKey key, IReadOnlyList<QuestFastTravelOption> options)
    {
        var selection = options.SingleOrDefault(candidate => candidate.Key == key);
        if (selection is null || !new QuestTravelService(_questManager, _questWorldContext).TryResolve(
                selection, _maze.WorldNpcs, npc => FindQuestTravelDistance(_player.Position, npc.Position),
                out var option, out var npc)) return;
        foreach (var character in CharacterRoster.Party.Members.Where(character => character.IsAlive))
        {
            character.ConsumeFood(option.NeedCost);
            character.ConsumeWater(option.NeedCost);
            character.SynchronizeNeedStatuses(_gameData.GetStatus(CharacterStatusIds.Hungry),
                _gameData.GetStatus(CharacterStatusIds.Thirsty));
        }
        var travelMessage = $"🗺️ A csapat felkereste {npc.Character.Name} karaktert, majd visszatért. " +
            $"🍖-{option.NeedCost} 💧-{option.NeedCost} minden élő csapattagnak.";
        _renderer.DrawInventoryMessage(travelMessage, ConsoleColor.Cyan);
        RecordSessionActivity(SessionActivityKind.System, travelMessage, ConsoleColor.Cyan);
        ProcessNpcQuests(npc, activateOffered: false, selectedQuest: key);
        _renderer.RefreshCharacterSheet(PartyLeader);
        RequestCoopSnapshotPublish();
    }

    private void ShowCharacterDetails()
    {
        var character = _renderer.CharacterSheet.DisplayedCharacter;
        ConsoleColor? selectedColor = null;
        RunHostPersonalWindow(PlayerWindowKind.CharacterDetails,
            () => selectedColor = CharacterDetailsWindow.Show(CreateCharacterDetailsSnapshot(character),
                _gameData, CurrentHostCoopWindowStatus));
        if (selectedColor is not { } color || !character.ChangeColor(color)) return;
        _renderer.CharacterSheet.RefreshCharacterSheet();
        _renderer.DrawMapCellAfterBattle(_maze, _fogOfWar,
            character == PartyLeader ? _player.Position :
            _maze.PartyMembers.FirstOrDefault(member => member.Character == character)?.Position ?? _player.Position,
            _player.Position);
        RequestCoopSnapshotPublish();
    }

    private IReadOnlyList<QuestJournalEntrySnapshot> OrderedQuestJournal()
    {
        SynchronizeInventoryQuests();
        return NpcQuestCoordinator.OrderedQuestJournal(_questJournal.Values);
    }

    private IItemDefinition? RollQuestReward(int experienceReward)
    {
        var maximumRarity = experienceReward >= 2000 ? ItemRarity.Legendary :
            experienceReward >= 800 ? ItemRarity.Magic : ItemRarity.Normal;
        var maximumPrice = Math.Max(80, experienceReward * 2);
        var maximumMagicPower = Math.Max(0, experienceReward / 300);
        var candidates = QuestRewardItems().Where(item => item.Rarity <= maximumRarity &&
            item.BasePrice <= maximumPrice && item.MagicPower <= maximumMagicPower).ToArray();
        return candidates.Length == 0 ? null : candidates[_random.Next(candidates.Length)];
    }


    private IEnumerable<IItemDefinition> QuestRewardItems() => _gameData.Items.Cast<IItemDefinition>()
        .Concat(_gameData.Weapons).Concat(_gameData.Armors).Concat(_gameData.MagicItems)
        .Where(item => !_gameData.IsTradeExcluded(item.Id))
        .Where(item => !SpellcastingRules.IsRestrictedFromTradingAndGeneration(item));

    private int CountPartyBackpackItems(string itemId) => CharacterRoster.Party.Members.Sum(character =>
        Enumerable.Range(0, LiveCharacter.MaximumBackpackItemCount)
            .Where(index => string.Equals(character.Backpack[index]?.Id, itemId, StringComparison.OrdinalIgnoreCase))
            .Sum(index => character.GetInventoryItemQuantity(InventorySlotKind.Backpack, index)));

    private void RemovePartyBackpackItems(string itemId, int count)
    {
        var remaining = count;
        foreach (var character in CharacterRoster.Party.Members)
        for (var index = 0; index < LiveCharacter.MaximumBackpackItemCount && remaining > 0; index++)
            while (remaining > 0 && string.Equals(character.Backpack[index]?.Id, itemId,
                       StringComparison.OrdinalIgnoreCase) &&
                   character.RemoveOneInventoryItem(InventorySlotKind.Backpack, index)) remaining--;
    }

    private void RegisterNpcQuestKill(Enemy defeatedEnemy)
    {
        //TODO: remove when quest is stable and tested
        var quest = _questManager.Roderic.Quests.PatriarchsShadows;

        var changes = _questManager.RegisterKill(defeatedEnemy);
        Log.Info($"Roderic progress: {quest.Progress}/{quest.RequiredCount} after killing {defeatedEnemy.Definition.Name}. Changes: {changes.Count}");

        ProcessQuestProgressChanges(changes);
    }

    private void ProcessQuestProgressChanges(IReadOnlyList<QuestProgressChange> changes)
    {
        if (changes.Count == 0)
            return;

        foreach (var change in changes)
        {
            var quest =
                _questManager.GetQuest(
                    change.QuestId,
                    change.GiverInstanceId);

            var color =
                change.BecameReadyToTurnIn
                    ? ConsoleColor.Cyan
                    : ConsoleColor.DarkYellow;

            var status =
                change.BecameReadyToTurnIn
                    ? " — teljesítve, leadható!"
                    : string.Empty;

            _renderer.DrawInventoryMessage(
                $"📜 {quest.Title}: " +
                $"{quest.Progress}/{quest.RequiredCount}" +
                status,
                color);
        }

        RequestCoopSnapshotPublish();
    }

    private void ProjectQuestChange(QuestHandle quest)
    {
        _npcQuestCoordinator.SynchronizeQuestJournal(_questJournal, quest);
        MarkCoopSnapshotDirty();
    }

    private void SynchronizeInventoryQuests()
    {
        if (_synchronizingQuestInventory || _maze is null) return;
        _synchronizingQuestInventory = true;
        try
        {
            ProcessQuestProgressChanges(_questInventorySynchronizer.Synchronize(CharacterRoster.Party.Members));
        }
        finally
        {
            _synchronizingQuestInventory = false;
        }
    }
}
