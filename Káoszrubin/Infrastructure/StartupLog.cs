using System.Runtime.InteropServices;
using System.Text;

namespace KaoszRubin.Infrastructure;

/// <summary>A grafikus felület előtt is használható, hibabiztos indítási napló.</summary>
public static class StartupLog
{
    private static readonly object Sync = new();
    private static readonly string SessionId = Guid.NewGuid().ToString("N")[..8];
    private static string? _filePath;
    private static bool _initialized;

    public static string? FilePath
    {
        get { lock (Sync) return _filePath; }
    }

    public static void Initialize()
    {
        lock (Sync)
        {
            if (_initialized) return;
            _initialized = true;
            foreach (var directory in CandidateDirectories())
            {
                try
                {
                    Directory.CreateDirectory(directory);
                    _filePath = Path.Combine(directory, $"startup-{DateTime.Now:yyyyMMdd}.log");
                    Append("INFO", "log.initialized", $"Napló: {_filePath}");
                    return;
                }
                catch
                {
                    _filePath = null;
                }
            }
        }
    }

    public static void Info(string eventName, string? details = null) => Write("INFO", eventName, details);
    public static void Warning(string eventName, string? details = null) => Write("WARN", eventName, details);
    public static void Error(string eventName, Exception exception) => Write("ERROR", eventName, exception.ToString());

    public static string EnvironmentSummary() => string.Join("; ",
        $"pid={Environment.ProcessId}",
        $"process={Environment.ProcessPath ?? "ismeretlen"}",
        $"base={AppContext.BaseDirectory}",
        $"cwd={Environment.CurrentDirectory}",
        $"os={RuntimeInformation.OSDescription}",
        $"runtime={RuntimeInformation.FrameworkDescription}",
        $"64bit={Environment.Is64BitProcess}",
        $"interactive={Environment.UserInteractive}",
        $"debugger={System.Diagnostics.Debugger.IsAttached}",
        $"wtSession={!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WT_SESSION"))}",
        $"inputRedirected={Console.IsInputRedirected}",
        $"outputRedirected={Console.IsOutputRedirected}");

    private static void Write(string level, string eventName, string? details)
    {
        Initialize();
        lock (Sync) Append(level, eventName, details);
    }

    private static void Append(string level, string eventName, string? details)
    {
        if (_filePath is null) return;
        try
        {
            var line = $"{DateTimeOffset.Now:O} [{level}] session={SessionId} {eventName}";
            if (!string.IsNullOrWhiteSpace(details)) line += $" | {details}";
            File.AppendAllText(_filePath, line + Environment.NewLine, new UTF8Encoding(false));
        }
        catch
        {
            // A naplózás soha nem akadályozhatja a játék elindulását.
        }
    }

    private static IEnumerable<string> CandidateDirectories()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "naplók");
        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localData)) yield return Path.Combine(localData, "Kaoszrubin", "naplók");
        yield return Path.Combine(Path.GetTempPath(), "Kaoszrubin", "naplók");
    }
}
