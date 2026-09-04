using KaoszRubin.Application;

namespace KaoszRubin.UI;

public enum InventoryInputAction { MoveUp, MoveDown, Inspect, Drop, Use, MoveItem, SplitStack, DistributeStack, CharacterDetails, GiveFollowerStack }

/// <summary>A host és a coop vendég közös, kontextusfüggő billentyűkiosztása.</summary>
public static class GameInputBindings
{
    public static int BattleDetailsPageDirection(ConsoleKeyInfo key) => key.Key switch
    {
        ConsoleKey.Add => 1,
        ConsoleKey.Subtract => -1,
        _ => key.KeyChar switch { '+' => 1, '-' => -1, _ => 0 }
    };

    public static bool IsCharacterSheetToggle(ConsoleKey key) => key == ConsoleKey.Tab;

    public static InventoryInputAction? InventoryAction(ConsoleKey key) => key switch
    {
        ConsoleKey.UpArrow => InventoryInputAction.MoveUp,
        ConsoleKey.DownArrow => InventoryInputAction.MoveDown,
        ConsoleKey.I => InventoryInputAction.Inspect,
        ConsoleKey.D => InventoryInputAction.Drop,
        ConsoleKey.Enter => InventoryInputAction.Use,
        ConsoleKey.Spacebar => InventoryInputAction.MoveItem,
        ConsoleKey.F => InventoryInputAction.SplitStack,
        ConsoleKey.S => InventoryInputAction.DistributeStack,
        ConsoleKey.R => InventoryInputAction.CharacterDetails,
        ConsoleKey.K => InventoryInputAction.GiveFollowerStack,
        _ => null
    };

    public static CharacterAction? CharacterAction(ConsoleKey key) => key switch
    {
        ConsoleKey.N => Application.CharacterAction.OpenDoor,
        ConsoleKey.Z => Application.CharacterAction.CloseOrLockDoor,
        ConsoleKey.K => Application.CharacterAction.SearchCurrentPosition,
        _ => null
    };

    public static LeaderAction? LeaderAction(ConsoleKey key, bool canActivateExit) => key switch
    {
        ConsoleKey.A => Application.LeaderAction.ToggleFormation,
        ConsoleKey.G => Application.LeaderAction.ToggleRegrouping,
        ConsoleKey.H => Application.LeaderAction.ToggleHoldPosition,
        ConsoleKey.M => Application.LeaderAction.ScatterParty,
        ConsoleKey.T => Application.LeaderAction.ToggleAttackMode,
        ConsoleKey.P => Application.LeaderAction.Rest,
        ConsoleKey.Enter when canActivateExit => Application.LeaderAction.ActivateExit,
        _ => null
    };
}
