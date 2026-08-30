using KaoszRubin.Application;
using KaoszRubin.Domain.Characters;
using Microsoft.AspNetCore.SignalR.Client;

namespace KaoszRubin.Transport.SignalR;

public enum CoopClientConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Faulted,
    Disposed
}

/// <summary>
/// SignalR klienskapcsolat a transportfüggetlen wire-protokollhoz. A hálózati frame-eket a
/// ClientSessionStore-ba vezeti, annak ACK/resync válaszát pedig automatikusan visszaküldi.
/// </summary>
public sealed class CoopSignalRClient : IAsyncDisposable
{
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(10);
    private readonly object _gate = new();
    private readonly SemaphoreSlim _incomingGate = new(1, 1);
    private readonly SemaphoreSlim _controlRequestGate = new(1, 1);
    private readonly HubConnection _connection;
    private readonly string _applicationVersion;
    private readonly string _catalogHash;
    private readonly string _displayName;
    private TaskCompletionSource<ServerHello>? _pendingHandshake;
    private TaskCompletionSource<CharacterControlResult>? _pendingControlRequest;
    private ClientSessionStore? _sessionStore;
    private string? _reconnectToken;
    private long _nextCommandId;
    private bool _disposed;
    private CoopClientConnectionState _state = CoopClientConnectionState.Disconnected;

    public CoopSignalRClient(string hostUrl, string applicationVersion, string catalogHash, string displayName)
    {
        if (string.IsNullOrWhiteSpace(applicationVersion))
            throw new ArgumentException("Az alkalmazásverzió nem lehet üres.", nameof(applicationVersion));
        if (string.IsNullOrWhiteSpace(catalogHash))
            throw new ArgumentException("A katalógushash nem lehet üres.", nameof(catalogHash));
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 32)
            throw new ArgumentException("A játékosnév 1 és 32 karakter közötti lehet.", nameof(displayName));

        _applicationVersion = applicationVersion;
        _catalogHash = catalogHash;
        _displayName = displayName;
        _connection = new HubConnectionBuilder()
            .WithUrl(BuildHubUri(hostUrl))
            .WithAutomaticReconnect([TimeSpan.Zero, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(5)])
            .Build();
        _connection.On<string>(CoopHub.ClientReceiveMethod, HandleWireAsync);
        _connection.Reconnecting += HandleReconnectingAsync;
        _connection.Reconnected += HandleReconnectedAsync;
        _connection.Closed += HandleClosedAsync;
    }

    public PlayerId? PlayerId { get; private set; }
    public PlayerId? HostPlayerId { get; private set; }
    public IReadOnlyList<CharacterId> AvailableCharacterIds { get; private set; } = [];
    public IReadOnlyList<CoopCharacterOption> AvailableCharacters { get; private set; } = [];
    public SessionSnapshot? CurrentSnapshot => _sessionStore?.CurrentSnapshot;
    public CoopClientConnectionState State { get { lock (_gate) return _state; } }

    public event Action<CoopClientConnectionState>? ConnectionStateChanged;
    public event Action<SessionSnapshot>? SnapshotChanged;
    public event Action<CharacterControlResult>? CharacterControlResultReceived;
    public event Action<GameCommandRejectedEvent>? CommandRejected;
    public event Action<CharacterStateSync>? CharacterStateReceived;
    public event Action<CoopProtocolError>? ProtocolErrorReceived;

    public async Task<ServerHello> ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        lock (_gate)
        {
            if (_state != CoopClientConnectionState.Disconnected)
                throw new InvalidOperationException("A SignalR kliens már elindult vagy éppen kapcsolódik.");
        }
        SetState(CoopClientConnectionState.Connecting);
        try
        {
            await _connection.StartAsync(cancellationToken);
            return await AuthenticateAsync(cancellationToken);
        }
        catch
        {
            SetState(CoopClientConnectionState.Faulted);
            await _connection.StopAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<CharacterControlResult> RequestCharacterControlAsync(CharacterId characterId,
        CancellationToken cancellationToken = default)
        => await RequestControlAsync(new CharacterControlRequest(RequireConnectedPlayer(), characterId),
            cancellationToken);

    public async Task<CharacterControlResult> JoinCharacterAsync(string characterData,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(characterData))
            throw new ArgumentException("A karakteradat nem lehet üres.", nameof(characterData));
        return await RequestControlAsync(new JoinCharacterRequest(RequireConnectedPlayer(), characterData),
            cancellationToken);
    }

    private async Task<CharacterControlResult> RequestControlAsync(object request,
        CancellationToken cancellationToken)
    {
        await _controlRequestGate.WaitAsync(cancellationToken);
        try
        {
            var completion = NewCompletion<CharacterControlResult>();
            lock (_gate) _pendingControlRequest = completion;
            try
            {
                await SendWireAsync(request, cancellationToken);
                return await completion.Task.WaitAsync(HandshakeTimeout, cancellationToken);
            }
            finally
            {
                lock (_gate)
                {
                    if (_pendingControlRequest == completion) _pendingControlRequest = null;
                }
            }
        }
        finally
        {
            _controlRequestGate.Release();
        }
    }

    public long NextCommandId()
    {
        ThrowIfDisposed();
        return Interlocked.Increment(ref _nextCommandId);
    }

    public Task SendCommandAsync(GameCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var playerId = RequireConnectedPlayer();
        if (command.SenderId != playerId)
            throw new ArgumentException("A command SenderId-ja nem a helyi játékosé.", nameof(command));
        if (command.CommandId <= 0)
            throw new ArgumentException("A command sorszámának pozitívnak kell lennie.", nameof(command));
        return SendWireAsync(command, cancellationToken);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _connection.StopAsync(cancellationToken);
        SetState(CoopClientConnectionState.Disconnected);
    }

    private async Task<ServerHello> AuthenticateAsync(CancellationToken cancellationToken)
    {
        var completion = NewCompletion<ServerHello>();
        lock (_gate) _pendingHandshake = completion;
        try
        {
            await SendWireAsync(new ClientHello(SessionProtocol.Version, _applicationVersion, _catalogHash,
                _displayName, _reconnectToken), cancellationToken);
            var hello = await completion.Task.WaitAsync(HandshakeTimeout, cancellationToken);
            if (!hello.Accepted)
                throw new InvalidOperationException(hello.RejectionReason ?? "A host elutasította a kapcsolódást.");
            return hello;
        }
        finally
        {
            lock (_gate)
            {
                if (_pendingHandshake == completion) _pendingHandshake = null;
            }
        }
    }

    private async Task HandleWireAsync(string wireMessage)
    {
        await _incomingGate.WaitAsync();
        try
        {
            object message;
            try
            {
                message = CoopProtocolJson.Decode(wireMessage);
            }
            catch (Exception exception) when (exception is System.Text.Json.JsonException or NotSupportedException)
            {
                PublishProtocolError(new CoopProtocolError("invalid-server-message", exception.Message));
                return;
            }

            switch (message)
            {
                case ServerHello hello:
                    HandleServerHello(hello);
                    break;
                case CharacterControlResult control:
                    if (control.PlayerId != PlayerId)
                    {
                        PublishProtocolError(new CoopProtocolError("character-control-recipient-mismatch",
                            "A karakterátvételi válasz másik játékoshoz tartozik."));
                        break;
                    }
                    lock (_gate) _pendingControlRequest?.TrySetResult(control);
                    CharacterControlResultReceived?.Invoke(control);
                    break;
                case SessionReplicationFrame frame:
                    await HandleReplicationFrameAsync(frame);
                    break;
                case GameCommandRejectedEvent rejected:
                    CommandRejected?.Invoke(rejected);
                    break;
                case CharacterStateSync characterState:
                    if (characterState.PlayerId != PlayerId)
                        PublishProtocolError(new CoopProtocolError("character-state-recipient-mismatch",
                            "A karakterállapot másik játékoshoz tartozik."));
                    else
                        CharacterStateReceived?.Invoke(characterState);
                    break;
                case CoopProtocolError error:
                    PublishProtocolError(error);
                    break;
                default:
                    PublishProtocolError(new CoopProtocolError("invalid-direction",
                        "A host nem szerver → kliens protokollüzenetet küldött."));
                    break;
            }
        }
        finally
        {
            _incomingGate.Release();
        }
    }

    private void HandleServerHello(ServerHello hello)
    {
        TaskCompletionSource<ServerHello>? pending;
        lock (_gate) pending = _pendingHandshake;
        if (pending is null)
        {
            PublishProtocolError(new CoopProtocolError("unexpected-server-hello",
                "Handshake-válasz érkezett aktív kapcsolódási kísérlet nélkül."));
            return;
        }
        if (hello.Accepted && hello.PlayerId is { } playerId)
        {
            if (hello.ProtocolVersion != SessionProtocol.Version ||
                string.IsNullOrWhiteSpace(hello.CatalogHash) ||
                !string.Equals(hello.CatalogHash.Trim(), _catalogHash.Trim(), StringComparison.OrdinalIgnoreCase) ||
                playerId.Value == Guid.Empty || hello.HostPlayerId is null ||
                string.IsNullOrWhiteSpace(hello.ReconnectToken))
            {
                pending.TrySetException(new InvalidOperationException(
                    "A host elfogadó handshake-válasza inkompatibilis vagy hiányos."));
                return;
            }
            if (PlayerId is { } existing && existing != playerId)
            {
                pending.TrySetException(new InvalidOperationException("Reconnectkor megváltozott a PlayerId."));
                return;
            }
            PlayerId = playerId;
            HostPlayerId = hello.HostPlayerId;
            _reconnectToken = hello.ReconnectToken;
            AvailableCharacterIds = hello.AvailableCharacterIds ?? [];
            AvailableCharacters = hello.AvailableCharacters ?? [];
            if (_sessionStore is null)
            {
                _sessionStore = new ClientSessionStore(playerId);
                _sessionStore.SnapshotChanged += snapshot => SnapshotChanged?.Invoke(snapshot);
            }
            SetState(CoopClientConnectionState.Connected);
        }
        pending.TrySetResult(hello);
    }

    private async Task HandleReplicationFrameAsync(SessionReplicationFrame frame)
    {
        var store = _sessionStore;
        if (store is null)
        {
            PublishProtocolError(new CoopProtocolError("handshake-required",
                "Snapshot érkezett a sikeres handshake előtt."));
            return;
        }
        var result = store.Apply(frame);
        if (result.Response is not null) await SendWireAsync(result.Response, CancellationToken.None);
        if (result.Status == ClientFrameApplyStatus.Rejected)
            PublishProtocolError(new CoopProtocolError("replication-rejected", result.Error ?? "Hibás frame."));
    }

    private Task HandleReconnectingAsync(Exception? exception)
    {
        SetState(CoopClientConnectionState.Reconnecting);
        return Task.CompletedTask;
    }

    private async Task HandleReconnectedAsync(string? connectionId)
    {
        try
        {
            await AuthenticateAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            SetState(CoopClientConnectionState.Faulted);
            PublishProtocolError(new CoopProtocolError("reconnect-failed", exception.Message));
        }
    }

    private Task HandleClosedAsync(Exception? exception)
    {
        if (!_disposed)
        {
            var closedException = exception ?? new InvalidOperationException("A SignalR kapcsolat lezárult.");
            lock (_gate)
            {
                _pendingHandshake?.TrySetException(closedException);
                _pendingControlRequest?.TrySetException(closedException);
            }
            SetState(exception is null ? CoopClientConnectionState.Disconnected : CoopClientConnectionState.Faulted);
        }
        return Task.CompletedTask;
    }

    private PlayerId RequireConnectedPlayer()
    {
        ThrowIfDisposed();
        if (State != CoopClientConnectionState.Connected || PlayerId is not { } playerId)
            throw new InvalidOperationException("A kliens nincs hitelesített, csatlakozott állapotban.");
        return playerId;
    }

    private Task SendWireAsync(object message, CancellationToken cancellationToken) =>
        _connection.SendAsync(CoopHub.ServerSendMethod, CoopProtocolJson.Encode(message), cancellationToken);

    private void PublishProtocolError(CoopProtocolError error) => ProtocolErrorReceived?.Invoke(error);

    private void SetState(CoopClientConnectionState state)
    {
        var changed = false;
        lock (_gate)
        {
            if (_state != state)
            {
                _state = state;
                changed = true;
            }
        }
        if (changed) ConnectionStateChanged?.Invoke(state);
    }

    private static Uri BuildHubUri(string hostUrl)
    {
        if (!Uri.TryCreate(hostUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("A host URL abszolút http:// vagy https:// cím legyen.", nameof(hostUrl));
        var builder = new UriBuilder(uri);
        if (!builder.Path.TrimEnd('/').EndsWith(CoopHub.Path, StringComparison.OrdinalIgnoreCase))
            builder.Path = $"{builder.Path.TrimEnd('/')}{CoopHub.Path}";
        return builder.Uri;
    }

    private static TaskCompletionSource<T> NewCompletion<T>() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _connection.DisposeAsync();
        SetState(CoopClientConnectionState.Disposed);
        _incomingGate.Dispose();
        _controlRequestGate.Dispose();
    }
}
