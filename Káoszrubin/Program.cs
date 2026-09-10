using System.Text;
using KaoszRubin.Data;
using KaoszRubin.UI;
using KaoszRubin;
using KaoszRubin.Application;
using System.Reflection;

Log.Initialize();
Log.Info("process.start", Log.EnvironmentSummary());
AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
{
    Log.Warning("process.last-chance", $"isTerminating={eventArgs.IsTerminating}");
    if (eventArgs.ExceptionObject is Exception exception)
        Log.Error("process.unhandled-exception", exception);
    else
        Log.Warning("process.unhandled-exception", eventArgs.ExceptionObject?.ToString());
};
TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
{
    Log.Error("process.unobserved-task-exception", eventArgs.Exception);
    eventArgs.SetObserved();
};

try
{
    if (!SystemHelpers.EnsureWindowsTerminal(args))
    {
        Log.Info("process.relaunch-parent-exit");
        return;
    }

    Log.Info("console.configure.start");
    Console.OutputEncoding = Encoding.UTF8;
    Console.InputEncoding = Encoding.UTF8;
    Console.ForegroundColor = ConsoleColor.Magenta;
    Log.Info("console.configure.complete");
    if (TerminalViewport.TryGetSize(out var startupViewport))
        Log.Info("console.viewport", $"width={startupViewport.Width}; height={startupViewport.Height}");
    else
        Log.Warning("console.viewport", "A konzol mérete indításkor még nem volt lekérdezhető.");

    var dataPath = Path.Combine(AppContext.BaseDirectory, CsvGameDataLoader.GameDataFileName);
    Log.Info("game-data.load.start", dataPath);
    var gameData = CsvGameDataLoader.Load(dataPath);
    Log.Info("game-data.load.complete");
    var savePath = Path.Combine(AppContext.BaseDirectory, "karakterek.json");
    var gameSaveDirectory = Path.Combine(AppContext.BaseDirectory, "mentések");
    var applicationVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
    var catalogHash = CatalogFingerprint.ComputeFile(dataPath);
    Log.Info("main-menu.construct.start", $"version={applicationVersion}; catalog={catalogHash}");
    var mainMenu = new KaoszRubin.UI.MainMenu(gameData, savePath, gameSaveDirectory, applicationVersion, catalogHash);
    Log.Info("main-menu.run.start");
    mainMenu.Run();
    Log.Info("main-menu.run.complete");
}
catch (Exception exception)
{
    Log.Error("process.fatal", exception);
    if (System.Diagnostics.Debugger.IsAttached) throw;
    try
    {
        Console.ResetColor();
        Console.Error.WriteLine();
        Console.Error.WriteLine("A Káoszrubin futása hibával leállt.");
        Console.Error.WriteLine($"A részletek a játéknaplóban találhatók: {Log.FilePath ?? "nem sikerült naplófájlt létrehozni"}");
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
    Log.Info("process.end", $"exitCode={Environment.ExitCode}");
}

