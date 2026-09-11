using KaoszRubin.Application;
using KaoszRubin.Data;
using KaoszRubin.Domain.Characters;
using KaoszRubin.Transport.SignalR;

namespace KaoszRubin.Tests.Coop;

internal static class CoopHostRole
{
    public static int Run(CoopHarnessOptions options)
    {
        using var roleConsole = CoopRoleConsole.Initialize("host", options);
        var workspaceRoot = CoopFixtureFactory.CreateTemporaryWorkspaceRoot(options.Workspace);
        var fixture = CoopFixtureFactory.Create(workspaceRoot);
        fixture.HostRoster.Party.SetLeader(fixture.HostLeader);

        try
        {
            var game = new Game(fixture.Catalog, fixture.HostRoster, fixture.HostLeader,
                fixture.GameSaveService, musicSettings: fixture.GameSettingsService);
            var host = CoopHostRuntime.StartAsync(game.Session, fixture.ApplicationVersion, fixture.CatalogHash,
                    fixture.CharacterSaveService.DeserializeCharacter,
                    character => RegisterRemoteCharacter(fixture.HostRoster, character),
                    port: options.Port)
                .GetAwaiter().GetResult();
            try
            {
                Console.Title = $"KR Coop Host - {options.Scenario ?? "manual"}";
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("Kaoszrubin coop host szerep");
                Console.ResetColor();
                Console.WriteLine($"Scenario: {options.Scenario ?? "manual"}");
                Console.WriteLine($"Csatlakozasi cim: {host.ConnectionHint}");
                Console.WriteLine("Esc: kilepes lobbybol");
                Console.WriteLine();
                Console.WriteLine("Varakozas a vendeg karakterere...");
                while (game.Session.ConnectedRemoteCharacterCount == 0)
                {
                    if (Console.KeyAvailable && Console.ReadKey(intercept: true).Key == ConsoleKey.Escape)
                        return 0;
                    Thread.Sleep(50);
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Vendeg csatlakozott. Jatek indul...");
                Console.ResetColor();
                Thread.Sleep(400);
                game.Run(host);
                return 0;
            }
            finally
            {
                host.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or System.Net.Sockets.SocketException)
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("A coop host nem indithato:");
            Console.WriteLine(exception.Message);
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Nyomj meg egy billentyut a kilepeshez.");
            Console.ReadKey(intercept: true);
            return 1;
        }
    }

    private static void RegisterRemoteCharacter(CharacterRoster roster, LiveCharacter remoteCharacter)
    {
        if (roster.Characters.All(character => character.Id != remoteCharacter.Id)) roster.Add(remoteCharacter);
    }
}
