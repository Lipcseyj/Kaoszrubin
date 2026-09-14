using KaoszRubin.Data;
using KaoszRubin.Domain.Quests;
using KaoszRubin.World;

namespace KaoszRubin.Infrastructure.Quests;

public static class QuestChestPlacement
{
    public static void Place(Maze maze, GameDataCatalog data, IReadOnlyDictionary<string, QuestChestId> placements)
    {
        var occupied = new HashSet<Position>();
        var ids = maze.TreasureChests.Where(chest => chest.Definition is not null)
            .Select(chest => chest.Definition!.Id).ToHashSet();
        var pending = new List<TreasureChest>();
        foreach (var (roomId, chestId) in placements)
        {
            if (!ids.Add(chestId)) throw new InvalidDataException($"A questláda többször szerepel a pályán: {chestId}.");
            var definition = data.GetQuestChest(chestId);
            var room = maze.GetRoomByContentId(roomId)
                ?? throw new InvalidDataException($"Hiányzó ládaszoba: {roomId}.");
            var center = new Position(room.TopLeft.X + room.Width / 2, room.TopLeft.Y + room.Height / 2);
            var candidates = room.InteriorPositions().Where(position => maze.IsWalkable(position) &&
                maze.GetObjectAt(position) is null && maze.GetTrapAt(position) is null &&
                !occupied.Contains(position) && position != maze.Entrance && position != maze.Exit)
                .OrderBy(position => Math.Abs(position.X - center.X) + Math.Abs(position.Y - center.Y)).ToArray();
            if (candidates.Length == 0) throw new InvalidDataException($"Nincs hely a questládának: {roomId}.");
            occupied.Add(candidates[0]);
            pending.Add(new(candidates[0], definition));
        }
        foreach (var chest in pending) maze.AddTreasureChest(chest);
    }
}
