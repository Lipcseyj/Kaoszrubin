using System.Diagnostics;

namespace KaoszRubin.Tests.Coop;

internal sealed class CoopHarnessProcess : IDisposable
{
    private static readonly TimeSpan RoleResolveTimeout = TimeSpan.FromSeconds(20);

    private readonly Process _launcher;
    private readonly Process? _roleProcess;
    private readonly bool _launchedInWindowsTerminal;

    private CoopHarnessProcess(string role, Process launcher, Process? roleProcess, bool launchedInWindowsTerminal)
    {
        Role = role;
        _launcher = launcher;
        _roleProcess = roleProcess;
        _launchedInWindowsTerminal = launchedInWindowsTerminal;
    }

    public string Role { get; }
    public int Id => (_roleProcess ?? _launcher).Id;
    public bool LaunchedInWindowsTerminal => _launchedInWindowsTerminal;
    public bool RoleProcessResolved => _roleProcess is not null;

    public bool HasExited
    {
        get
        {
            // Windows Terminal alatt a wt.exe indito azonnal kilep, ezert a valodi szerepfolyamatot
            // kell figyelni; csak annak hianyaban esunk vissza az inditora.
            try { return (_roleProcess ?? _launcher).HasExited; }
            catch (InvalidOperationException) { return true; }
        }
    }

    public static CoopHarnessProcess StartRole(string role, string? scenario, int port, string workspaceRoot)
    {
        var processPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("A futtathato folyamat utvonala nem elerheto.");

        var pidFilePath = CoopRoleConsole.GetPidFilePath(workspaceRoot, role);
        try
        {
            if (File.Exists(pidFilePath)) File.Delete(pidFilePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }

        var arguments = new List<string>
        {
            "--coop-role", role,
            "--port", port.ToString(),
            "--workspace", workspaceRoot
        };
        if (!string.IsNullOrWhiteSpace(scenario))
        {
            arguments.Add("--scenario");
            arguments.Add(scenario);
        }

        var title = role.Equals("host", StringComparison.OrdinalIgnoreCase)
            ? "Káoszrubin coop – HOST"
            : "Káoszrubin coop – VENDÉG";

        var startInfo = CoopTerminalLauncher.CreateStartInfo(title, processPath, arguments,
            out var usesWindowsTerminal);
        var launcher = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Nem sikerult elinditani a(z) {role} szerepkoru folyamatot.");

        var roleProcess = CoopRoleConsole.TryResolveRoleProcess(workspaceRoot, role, RoleResolveTimeout);
        return new CoopHarnessProcess(role, launcher, roleProcess, usesWindowsTerminal);
    }

    public bool WaitForExit(int millisecondsTimeout) => (_roleProcess ?? _launcher).WaitForExit(millisecondsTimeout);

    public void Kill()
    {
        KillProcess(_roleProcess);
        // A Terminal-ablakot nem loujuk ki: az a felhasznalo ablaka, a szerepfolyamat kilepesevel zarul.
        if (!_launchedInWindowsTerminal) KillProcess(_launcher);
    }

    private static void KillProcess(Process? process)
    {
        if (process is null) return;
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (exception is InvalidOperationException or
                                              System.ComponentModel.Win32Exception or NotSupportedException)
        {
        }
    }

    public void Dispose()
    {
        Kill();
        _roleProcess?.Dispose();
        _launcher.Dispose();
    }
}
