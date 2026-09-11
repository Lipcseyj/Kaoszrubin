namespace KaoszRubin.Tests.Coop;

internal static class CoopSimulationHarness
{
    /// <summary>
    /// A szallitott szcenariok mind kezi vezerlesuek; ez a seam teszi lehetove, hogy egy jovobeli
    /// szcenario automatikus billentyubeadast kerjen (<see cref="WindowsConsoleKeyScript"/>).
    /// </summary>
    private static readonly ICoopKeyScript KeyScript = NoOpCoopKeyScript.Instance;

    public static void Run(CoopHarnessOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        switch (options.Mode)
        {
            case CoopHarnessMode.HostRole:
                Environment.ExitCode = CoopHostRole.Run(options);
                return;
            case CoopHarnessMode.GuestRole:
                Environment.ExitCode = CoopGuestRole.Run(options);
                return;
            default:
                Environment.ExitCode = RunController(options);
                return;
        }
    }

    private static int RunController(CoopHarnessOptions options)
    {
        var scenario = ResolveScenario(options.Scenario);
        if (scenario is null) return 0;

        var workspaceRoot = CoopFixtureFactory.CreateTemporaryWorkspaceRoot(options.Workspace);
        Directory.CreateDirectory(workspaceRoot);
        var children = new List<CoopHarnessProcess>();

        try
        {
            PrintScenarioBriefing(scenario, options.Port, workspaceRoot);

            if (scenario.StartHost)
            {
                children.Add(CoopHarnessProcess.StartRole("host", scenario.Name, options.Port, workspaceRoot));
                Thread.Sleep(1200);
            }

            if (scenario.StartGuest)
            {
                children.Add(CoopHarnessProcess.StartRole("guest", scenario.Name, options.Port, workspaceRoot));
            }

            Console.WriteLine();
            Console.WriteLine("A szerepablakok elindultak. Esc: mindket ablak leallitasa.");

            foreach (var child in children) KeyScript.SendKeys(child);

            while (true)
            {
                if (children.Count > 0 && children.All(child => child.HasExited))
                {
                    Console.WriteLine("Minden szerepablak bezarult.");
                    break;
                }

                if (Console.KeyAvailable && Console.ReadKey(intercept: true).Key == ConsoleKey.Escape)
                {
                    Console.WriteLine("Leallitas...");
                    break;
                }

                Thread.Sleep(100);
            }

            return 0;
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"A coop szimulacio nem indithato: {exception.Message}");
            Console.ResetColor();
            return 1;
        }
        finally
        {
            foreach (var child in children)
            {
                try { child.Kill(); } catch { }
                child.Dispose();
            }

            if (string.IsNullOrWhiteSpace(options.Workspace)) TryDeleteWorkspace(workspaceRoot);
        }
    }

    private static CoopScenario? ResolveScenario(string? requestedScenario)
    {
        if (CoopScenarios.TryGet(requestedScenario, out var scenario)) return scenario;
        if (!string.IsNullOrWhiteSpace(requestedScenario))
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"Ismeretlen szcenario: {requestedScenario}");
            Console.ResetColor();
        }

        var scenarios = CoopScenarios.List();
        Console.WriteLine("Kaoszrubin coop szimulacio - valassz szcenariot:");
        Console.WriteLine();
        for (var index = 0; index < scenarios.Count; index++)
            Console.WriteLine($"  {index + 1}) {scenarios[index].Name} - {scenarios[index].Description}");
        Console.WriteLine("  0) Kilepes");
        Console.WriteLine();
        Console.Write("Valasztas: ");

        var input = Console.ReadLine();
        if (!int.TryParse(input, out var choice) || choice <= 0 || choice > scenarios.Count) return null;
        return scenarios[choice - 1];
    }

    private static void PrintScenarioBriefing(CoopScenario scenario, int port, string workspaceRoot)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Szcenario: {scenario.Name} - {scenario.Description}");
        Console.ResetColor();
        Console.WriteLine($"Port: {port}");
        Console.WriteLine($"Ideiglenes munkakonyvtar: {workspaceRoot}");
        Console.WriteLine();

        WriteHints("Kezi lepesek", scenario.ControllerInstructions, port);
        WriteHints("Host ablak", scenario.HostHints, port);
        WriteHints("Vendeg ablak", scenario.GuestHints, port);
    }

    private static void WriteHints(string title, IReadOnlyList<string> hints, int port)
    {
        if (hints.Count == 0) return;
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine($"{title}:");
        Console.ResetColor();
        foreach (var hint in hints)
            Console.WriteLine($"  - {hint.Replace("{port}", port.ToString(), StringComparison.Ordinal)}");
        Console.WriteLine();
    }

    private static void TryDeleteWorkspace(string workspaceRoot)
    {
        try
        {
            if (Directory.Exists(workspaceRoot)) Directory.Delete(workspaceRoot, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"A munkakonyvtar nem torolheto: {workspaceRoot}");
        }
    }
}
