using System.Text;
using KaoszRubin.Data;
using KaoszRubin.UI;
using KaoszRubin;
using KaoszRubin.Application;
using System.Reflection;

StartupLog.Initialize();
StartupLog.Info("process.start", StartupLog.EnvironmentSummary());
AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
{
    if (eventArgs.ExceptionObject is Exception exception)
        StartupLog.Error("process.unhandled-exception", exception);
    else
        StartupLog.Warning("process.unhandled-exception", eventArgs.ExceptionObject?.ToString());
};
TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
    StartupLog.Error("process.unobserved-task-exception", eventArgs.Exception);

try
{
    if (!SystemHelpers.EnsureWindowsTerminal(args))
    {
        StartupLog.Info("process.relaunch-parent-exit");
        return;
    }

    StartupLog.Info("console.configure.start");
    Console.OutputEncoding = Encoding.UTF8;
    Console.InputEncoding = Encoding.UTF8;
    Console.ForegroundColor = ConsoleColor.Magenta;
    StartupLog.Info("console.configure.complete");
    if (TerminalViewport.TryGetSize(out var startupViewport))
        StartupLog.Info("console.viewport", $"width={startupViewport.Width}; height={startupViewport.Height}");
    else
        StartupLog.Warning("console.viewport", "A konzol mérete indításkor még nem volt lekérdezhető.");

    var dataPath = Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName);
    StartupLog.Info("game-data.load.start", dataPath);
    var gameData = CsvGameDataLoader.Load(dataPath);
    StartupLog.Info("game-data.load.complete");
    var savePath = Path.Combine(AppContext.BaseDirectory, "karakterek.json");
    var gameSaveDirectory = Path.Combine(AppContext.BaseDirectory, "mentések");
    var applicationVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
    var catalogHash = CatalogFingerprint.ComputeFile(dataPath);
    StartupLog.Info("main-menu.construct.start", $"version={applicationVersion}; catalog={catalogHash}");
    var mainMenu = new KaoszRubin.UI.MainMenu(gameData, savePath, gameSaveDirectory, applicationVersion, catalogHash);
    StartupLog.Info("main-menu.run.start");
    mainMenu.Run();
    StartupLog.Info("main-menu.run.complete");
}
catch (Exception exception)
{
    StartupLog.Error("process.fatal", exception);
    if (System.Diagnostics.Debugger.IsAttached) throw;
    try
    {
        Console.ResetColor();
        Console.Error.WriteLine();
        Console.Error.WriteLine("A Káoszrubin indítása hibával leállt.");
        Console.Error.WriteLine($"A részletek az indítási naplóban találhatók: {StartupLog.FilePath ?? "nem sikerült naplófájlt létrehozni"}");
        if (!Console.IsInputRedirected)
        {
            Console.Error.WriteLine("Nyomj meg egy billentyűt a bezáráshoz.");
            Console.ReadKey(intercept: true);
        }
    }
    catch
    {
        // A végső hibajelzés sem fedheti el az eredeti kivételt.
    }
    Environment.ExitCode = 1;
}
finally
{
    StartupLog.Info("process.end", $"exitCode={Environment.ExitCode}");
}

