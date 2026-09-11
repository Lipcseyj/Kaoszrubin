using System.Diagnostics;

namespace KaoszRubin.Tests.Coop;

internal sealed class CoopHarnessProcess : IDisposable
{
    private readonly Process _process;

    private CoopHarnessProcess(Process process) => _process = process;

    public int Id => _process.Id;
    public bool HasExited => _process.HasExited;

    public static CoopHarnessProcess StartRole(string role, string? scenario, int port, string workspaceRoot)
    {
        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("A futtathato folyamat utvonala nem elerheto.");
        var arguments = $"--coop-role {role} --port {port} --workspace \"{workspaceRoot}\"";
        if (!string.IsNullOrWhiteSpace(scenario)) arguments += $" --scenario \"{scenario}\"";

        var startInfo = new ProcessStartInfo
        {
            FileName = processPath,
            Arguments = arguments,
            UseShellExecute = true,
            WorkingDirectory = AppContext.BaseDirectory
        };

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Nem sikerult elinditani a(z) {role} szerepkoru folyamatot.");
        return new CoopHarnessProcess(process);
    }

    public bool WaitForExit(int millisecondsTimeout) => _process.WaitForExit(millisecondsTimeout);

    public void Kill()
    {
        if (_process.HasExited) return;
        _process.Kill(entireProcessTree: true);
    }

    public void Dispose()
    {
        try
        {
            if (!_process.HasExited) _process.Kill(entireProcessTree: true);
        }
        catch
        {
        }
        _process.Dispose();
    }
}
