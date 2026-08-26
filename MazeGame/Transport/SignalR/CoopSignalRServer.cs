using MazeGame.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MazeGame.Transport.SignalR;

/// <summary>Beágyazható LAN host. Az internetes publikáláshoz később TLS/relay réteg szükséges.</summary>
public sealed class CoopSignalRServer : IAsyncDisposable
{
    private readonly WebApplication _application;
    private readonly CoopHostGateway _gateway;
    private readonly IHubContext<CoopHub> _hubContext;

    private CoopSignalRServer(WebApplication application, CoopHostGateway gateway)
    {
        _application = application;
        _gateway = gateway;
        _hubContext = application.Services.GetRequiredService<IHubContext<CoopHub>>();
    }

    public IReadOnlyCollection<string> Addresses => _application.Urls.ToArray();

    public static async Task<CoopSignalRServer> StartAsync(CoopHostGateway gateway,
        string listenUrl = "http://0.0.0.0:5127", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        if (!Uri.TryCreate(listenUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttp)
            throw new ArgumentException("A LAN listen URL abszolút http:// cím legyen.", nameof(listenUrl));

        var builder = WebApplication.CreateSlimBuilder();
        // A konzolos renderer kimenetét a Kestrel alapértelmezett konzollogja nem írhatja felül.
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls(listenUrl);
        builder.Services.AddSingleton(gateway);
        builder.Services.AddSignalR(options =>
        {
            options.MaximumReceiveMessageSize = 1024 * 1024;
            options.EnableDetailedErrors = false;
        });
        var application = builder.Build();
        application.MapGet("/", () => Results.Ok(new { service = "MazeGame Coop", protocol = SessionProtocol.Version }));
        application.MapHub<CoopHub>(CoopHub.Path);
        await application.StartAsync(cancellationToken);
        return new CoopSignalRServer(application, gateway);
    }

    public async Task PublishSnapshotAsync(SessionSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        var messages = _gateway.DrainPendingMessages().Concat(_gateway.CreateReplicationMessages(snapshot));
        await Task.WhenAll(messages.Select(message => _hubContext.Clients.Client(message.ConnectionId)
            .SendAsync(CoopHub.ClientReceiveMethod, message.WireMessage, cancellationToken)));
    }

    public async ValueTask DisposeAsync()
    {
        await _application.StopAsync();
        await _application.DisposeAsync();
    }
}
