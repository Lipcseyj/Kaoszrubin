using System.Diagnostics;
using System.Text;

namespace KaoszRubin.Tests.Coop;

/// <summary>
/// A szerepablak konzoljat ugyanugy keszíti elo, mint az eles jatek indulasa: UTF-8 kodolas,
/// hogy az emoji- es ekezetes karakterek is helyesen jelenjenek meg. Emellett PID-jelzofajlt ir,
/// mert a Windows Terminalon keresztul inditott folyamatot a vezerlo kulonben nem tudna kovetni.
/// </summary>
internal sealed class CoopRoleConsole : IDisposable
{
    private readonly string? _pidFilePath;
    private bool _disposed;

    private CoopRoleConsole(string? pidFilePath) => _pidFilePath = pidFilePath;

    public static CoopRoleConsole Initialize(string role, CoopHarnessOptions options)
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
        }
        catch (IOException)
        {
            // Atiranyitott konzolnal a kodolas beallitasa nem kotelezo.
        }

        string? pidFilePath = null;
        if (!string.IsNullOrWhiteSpace(options.Workspace))
        {
            try
            {
                Directory.CreateDirectory(options.Workspace);
                pidFilePath = GetPidFilePath(options.Workspace, role);
                File.WriteAllText(pidFilePath, Environment.ProcessId.ToString());
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                pidFilePath = null;
            }
        }

        return new CoopRoleConsole(pidFilePath);
    }

    public static string GetPidFilePath(string workspaceRoot, string role) =>
        Path.Combine(workspaceRoot, $"{role}.pid");

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_pidFilePath is null) return;
        try
        {
            if (File.Exists(_pidFilePath)) File.Delete(_pidFilePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    public static Process? TryResolveRoleProcess(string workspaceRoot, string role, TimeSpan timeout)
    {
        var pidFilePath = GetPidFilePath(workspaceRoot, role);
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (File.Exists(pidFilePath) &&
                    int.TryParse(File.ReadAllText(pidFilePath).Trim(), out var processId))
                {
                    return Process.GetProcessById(processId);
                }
            }
            catch (Exception exception) when (exception is IOException or ArgumentException or
                                                  InvalidOperationException or FormatException)
            {
                // A fajl eppen irodik, vagy a folyamat mar kilepett; ujraprobalunk.
            }

            Thread.Sleep(100);
        }

        return null;
    }
}
