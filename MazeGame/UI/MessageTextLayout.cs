namespace MazeGame.UI;

/// <summary>Konzolpanelek szóhatáron tördelő közös szövegelrendezése.</summary>
public static class MessageTextLayout
{
    public static IEnumerable<string> Wrap(string message, int width)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        while (message.Length > width)
        {
            var splitAt = message.LastIndexOf(' ', width);
            if (splitAt <= 0) splitAt = width;
            yield return message[..splitAt];
            message = message[splitAt..].TrimStart();
        }
        yield return message;
    }
}
