using MazeGame.Application;
using System.Text.Json;

namespace MazeGame.UI;

/// <summary>Kiszűri a csak replikációs sorszámban eltérő, vizuálisan azonos snapshotokat.</summary>
public static class CoopGuestRenderFingerprint
{
    public static string Compute(SessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return JsonSerializer.Serialize(snapshot with { SnapshotSequence = 0, LastEventSequence = 0 });
    }
}
