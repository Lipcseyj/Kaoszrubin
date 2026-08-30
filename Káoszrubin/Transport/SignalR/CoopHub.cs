using KaoszRubin.Application;
using Microsoft.AspNetCore.SignalR;

namespace KaoszRubin.Transport.SignalR;

/// <summary>Vékony SignalR adapter: minden szabályt a transportfüggetlen gateway-re bíz.</summary>
public sealed class CoopHub(CoopHostGateway gateway) : Hub
{
    public const string Path = "/coop";
    public const string ServerSendMethod = "SendWire";
    public const string ClientReceiveMethod = "ReceiveWire";

    public async Task SendWire(string wireMessage)
    {
        var outgoing = gateway.HandleIncoming(Context.ConnectionId, wireMessage);
        foreach (var message in outgoing)
            await Clients.Client(message.ConnectionId).SendAsync(ClientReceiveMethod, message.WireMessage,
                Context.ConnectionAborted);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        gateway.Disconnect(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
