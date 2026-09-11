using KaoszRubin.Application;
using KaoszRubin.UI;

namespace KaoszRubin.Tests.Coop;

internal static class CoopGuestRole
{
    private const int ConnectionRetryCount = 8;

    public static int Run(CoopHarnessOptions options)
    {
        using var roleConsole = CoopRoleConsole.Initialize("guest", options);
        var workspaceRoot = CoopFixtureFactory.CreateTemporaryWorkspaceRoot(options.Workspace);
        var fixture = CoopFixtureFactory.Create(workspaceRoot);
        var hostUrl = $"http://localhost:{options.Port}";
        var guest = fixture.GuestCharacter;
        var characterData = fixture.CharacterSaveService.SerializeCharacter(guest);

        Console.Title = $"KR Coop Guest - {options.Scenario ?? "manual"}";
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Kaoszrubin coop vendeg szerep");
        Console.ResetColor();
        Console.WriteLine($"Scenario: {options.Scenario ?? "manual"}");
        Console.WriteLine($"Host cim: {hostUrl}");
        Console.WriteLine($"Karakter: {guest.Name} ({guest.CharacterClass.Name})");
        Console.WriteLine();

        var screen = new CoopGuestScreen(fixture.ApplicationVersion, fixture.CatalogHash, fixture.Catalog,
            fixture.GameSettingsService);

        for (var attempt = 1; attempt <= ConnectionRetryCount; attempt++)
        {
            try
            {
                screen.RunAsync(hostUrl, guest.Name, guest, characterData, _ => { })
                    .GetAwaiter().GetResult();
                return 0;
            }
            catch (Exception exception) when (attempt < ConnectionRetryCount && LooksLikeHostNotReady(exception))
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"Csatlakozas varakozik ({attempt}/{ConnectionRetryCount}): {exception.Message}");
                Console.ResetColor();
                Thread.Sleep(500);
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException or HttpRequestException)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"A vendeg kliens leallt: {exception.Message}");
                Console.ResetColor();
                Console.WriteLine("Nyomj meg egy billentyut a kilepeshez.");
                Console.ReadKey(intercept: true);
                return 1;
            }
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("A host nem elerheto a megadott idon belul.");
        Console.ResetColor();
        Console.WriteLine("Nyomj meg egy billentyut a kilepeshez.");
        Console.ReadKey(intercept: true);
        return 1;
    }

    private static bool LooksLikeHostNotReady(Exception exception)
    {
        if (exception is HttpRequestException) return true;
        return exception.InnerException is HttpRequestException;
    }
}
