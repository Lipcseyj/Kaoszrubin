using System.Threading.Channels;

namespace KaoszRubin.Application;

public interface ICoopTransportEndpoint
{
    ValueTask SendAsync(string message, CancellationToken cancellationToken = default);
    ValueTask<string> ReceiveAsync(CancellationToken cancellationToken = default);
}

/// <summary>Ugyanazt a szöveges wire-protokollt használó, tesztelhető kétirányú kapcsolat hálózati I/O nélkül.</summary>
public static class InMemoryCoopTransport
{
    public static (ICoopTransportEndpoint Host, ICoopTransportEndpoint Client) CreatePair()
    {
        var clientToHost = Channel.CreateUnbounded<string>();
        var hostToClient = Channel.CreateUnbounded<string>();
        return (new Endpoint(hostToClient.Writer, clientToHost.Reader),
            new Endpoint(clientToHost.Writer, hostToClient.Reader));
    }

    private sealed class Endpoint(ChannelWriter<string> outgoing, ChannelReader<string> incoming)
        : ICoopTransportEndpoint
    {
        public ValueTask SendAsync(string message, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("Üres transportüzenet nem küldhető.", nameof(message));
            return outgoing.WriteAsync(message, cancellationToken);
        }

        public ValueTask<string> ReceiveAsync(CancellationToken cancellationToken = default) =>
            incoming.ReadAsync(cancellationToken);
    }
}
