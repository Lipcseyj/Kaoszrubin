using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using MazeGame.Application;
using MazeGame.Domain.Characters;

namespace MazeGame.Transport.SignalR;

/// <summary>
/// A szinkron konzolos játékhurok és az aszinkron SignalR host közötti latest-value pumpa.
/// A játék szála sosem vár hálózati I/O-ra; torlódáskor csak a legfrissebb snapshot marad meg.
/// </summary>
public sealed class CoopHostRuntime : ICoopHostLoop, IAsyncDisposable
{
    private static readonly TimeSpan PublishInterval = TimeSpan.FromMilliseconds(100);
    private readonly CoopHostGateway _gateway;
    private readonly CoopSignalRServer _server;
    private readonly Channel<SessionSnapshot> _snapshots = Channel.CreateBounded<SessionSnapshot>(
        new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true
        });
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _publishWorker;
    private long _nextPublishUtcTicks;

    private CoopHostRuntime(CoopHostGateway gateway, CoopSignalRServer server, string connectionHint)
    {
        _gateway = gateway;
        _server = server;
        ConnectionHint = connectionHint;
        _publishWorker = PublishLoopAsync();
    }

    public string ConnectionHint { get; }
    public Exception? LastPublishError { get; private set; }

    public static async Task<CoopHostRuntime> StartAsync(GameSession session, string applicationVersion,
        string catalogHash, Func<string, LiveCharacter>? deserializeCharacter = null,
        Action<LiveCharacter>? registerCharacter = null, int port = 5127,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        var publisher = new SessionReplicationPublisher();
        var gateway = new CoopHostGateway(session,
            new SessionHandshakeService(session, applicationVersion, catalogHash), publisher,
            deserializeCharacter, registerCharacter);
        var server = await CoopSignalRServer.StartAsync(gateway, $"http://0.0.0.0:{port}", cancellationToken);
        return new CoopHostRuntime(gateway, server, CreateConnectionHint(port));
    }

    public bool ShouldPublish(DateTime utcNow)
    {
        if (_gateway.ConnectedClientCount == 0) return false;
        var nowTicks = utcNow.Ticks;
        var nextTicks = Interlocked.Read(ref _nextPublishUtcTicks);
        if (nowTicks < nextTicks) return false;
        Interlocked.Exchange(ref _nextPublishUtcTicks, utcNow.Add(PublishInterval).Ticks);
        return true;
    }

    public bool TryPublish(SessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return _snapshots.Writer.TryWrite(snapshot);
    }

    private async Task PublishLoopAsync()
    {
        try
        {
            await foreach (var snapshot in _snapshots.Reader.ReadAllAsync(_shutdown.Token))
            {
                try
                {
                    await _server.PublishSnapshotAsync(snapshot, _shutdown.Token);
                    LastPublishError = null;
                }
                catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    LastPublishError = exception;
                }
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
        }
    }

    private static string CreateConnectionHint(int port)
    {
        try
        {
            var address = Dns.GetHostAddresses(Dns.GetHostName())
                .FirstOrDefault(candidate => candidate.AddressFamily == AddressFamily.InterNetwork &&
                                             !IPAddress.IsLoopback(candidate));
            if (address is not null) return $"http://{address}:{port}";
        }
        catch (SocketException)
        {
        }
        return $"http://localhost:{port}";
    }

    public async ValueTask DisposeAsync()
    {
        _snapshots.Writer.TryComplete();
        _shutdown.Cancel();
        try { await _publishWorker; } catch (OperationCanceledException) { }
        await _server.DisposeAsync();
        _shutdown.Dispose();
    }
}
