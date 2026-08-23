using MazeGame.Domain.Characters;
using MazeGame.Domain.Inventory;

namespace MazeGame;

public enum InnMarketMode { Buy, Sell }

public sealed record InnStockOffer(IItemDefinition Item, int Price);

public sealed record InnSellOffer(LiveCharacter Owner, int BackpackIndex, IItemDefinition Item, int Price);

public sealed record InnRumor(string Title, IReadOnlyList<string> Lines, ConsoleColor Color);
