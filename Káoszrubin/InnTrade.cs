using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Inventory;

namespace KaoszRubin;

public enum InnMarketMode { Buy, Sell }

public sealed record InnStockOffer(IItemDefinition Item, int Price);

public sealed record InnSellOffer(LiveCharacter Owner, int BackpackIndex, IItemDefinition Item, int Price);

public sealed record InnRumor(string Title, IReadOnlyList<string> Lines, ConsoleColor Color);

public sealed record LevelCompletionResult(LiveCharacter Character, LevelUpResult Experience);
