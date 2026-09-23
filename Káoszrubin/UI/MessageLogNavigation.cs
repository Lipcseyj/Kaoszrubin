namespace KaoszRubin.UI;

public enum MessageLogNavigation
{
    OlderPage,
    NewerPage,
    Oldest,
    Newest
}

public static class MessageLogNavigationRules
{
    public static bool TryFromKey(ConsoleKey key, out MessageLogNavigation navigation)
    {
        navigation = key switch
        {
            ConsoleKey.PageUp => MessageLogNavigation.OlderPage,
            ConsoleKey.PageDown => MessageLogNavigation.NewerPage,
            ConsoleKey.Home => MessageLogNavigation.Oldest,
            ConsoleKey.End => MessageLogNavigation.Newest,
            _ => default
        };
        return key is ConsoleKey.PageUp or ConsoleKey.PageDown or ConsoleKey.Home or ConsoleKey.End;
    }

    public static int CalculateOffset(int currentOffset, int entryCount, int visibleLineCount,
        MessageLogNavigation navigation)
    {
        var pageSize = Math.Max(1, visibleLineCount);
        var maximumOffset = Math.Max(0, entryCount - pageSize);
        return navigation switch
        {
            MessageLogNavigation.OlderPage => Math.Min(maximumOffset, currentOffset + pageSize),
            MessageLogNavigation.NewerPage => Math.Max(0, currentOffset - pageSize),
            MessageLogNavigation.Oldest => maximumOffset,
            MessageLogNavigation.Newest => 0,
            _ => Math.Clamp(currentOffset, 0, maximumOffset)
        };
    }
}
