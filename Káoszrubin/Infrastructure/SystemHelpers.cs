using System;
using System.Diagnostics;

namespace KaoszRubin.Infrastructure
{
    public static class SystemHelpers
    {
        private static readonly Version IbmSchemeMinimumVersion = new(1, 21);
        internal const string TerminalChildArgument = "--kaoszrubin-terminal-child";

        public static bool EnsureWindowsTerminal(IReadOnlyCollection<string>? arguments = null)
        {
            var hasChildMarker = arguments?.Contains(TerminalChildArgument, StringComparer.OrdinalIgnoreCase) == true;
            if (!ShouldRelaunchInWindowsTerminal(OperatingSystem.IsWindows(), IsRunningInWindowsTerminal(),
                    hasChildMarker, Debugger.IsAttached))
            {
                StartupLog.Info("terminal.relaunch.skipped",
                    $"windows={OperatingSystem.IsWindows()}; wtSession={IsRunningInWindowsTerminal()}; " +
                    $"childMarker={hasChildMarker}; debugger={Debugger.IsAttached}");
                return true;
            }

            string exePath = Environment.ProcessPath
                ?? throw new InvalidOperationException(
                    "Nem határozható meg az EXE elérési útja.");

            StartupLog.Info("terminal.version.start");
            Version? terminalVersion = GetWindowsTerminalVersion();

            string colorScheme =
                terminalVersion is not null &&
                terminalVersion >= IbmSchemeMinimumVersion
                    ? "IBM 5153"
                    : "Vintage";

            StartupLog.Info("terminal.launch.start",
                $"version={terminalVersion?.ToString() ?? "ismeretlen"}; scheme={colorScheme}; exe={exePath}");

            var psi = new ProcessStartInfo
            {
                FileName = "wt.exe",
                UseShellExecute = true,
                WorkingDirectory = AppContext.BaseDirectory
            };

            psi.ArgumentList.Add("--window");
            psi.ArgumentList.Add("new");

            psi.ArgumentList.Add("--maximized");

            psi.ArgumentList.Add("new-tab");

            psi.ArgumentList.Add("--title");
            psi.ArgumentList.Add("Káoszrubin");

            psi.ArgumentList.Add("--colorScheme");
            psi.ArgumentList.Add(colorScheme);

            psi.ArgumentList.Add("--startingDirectory");
            psi.ArgumentList.Add(AppContext.BaseDirectory);

            psi.ArgumentList.Add(exePath);
            psi.ArgumentList.Add(TerminalChildArgument);

            try
            {
                var process = Process.Start(psi);
                if (process is null)
                {
                    StartupLog.Warning("terminal.launch.failed", "A Process.Start nem adott vissza folyamatot; folytatás a jelenlegi konzolban.");
                    return true;
                }
                StartupLog.Info("terminal.launch.requested", $"launcherPid={process.Id}; a szülőfolyamat szabályosan kilép.");
                return false;
            }
            catch (Exception exception)
            {
                StartupLog.Error("terminal.launch.failed", exception);
                return true;
            }
        }

        internal static bool ShouldRelaunchInWindowsTerminal(bool isWindows, bool hasWindowsTerminalSession,
            bool hasChildMarker, bool debuggerAttached) =>
            isWindows && !hasWindowsTerminalSession && !hasChildMarker && !debuggerAttached;

        private static bool IsRunningInWindowsTerminal()
        {
            return !string.IsNullOrEmpty(
                Environment.GetEnvironmentVariable("WT_SESSION"));
        }

        private static Version? GetWindowsTerminalVersion()
        {
            try
            {
                Version? ret;
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                psi.ArgumentList.Add("-NoProfile");
                psi.ArgumentList.Add("-NonInteractive");
                psi.ArgumentList.Add("-Command");

                psi.ArgumentList.Add(
                    "(Get-AppxPackage Microsoft.WindowsTerminal " +
                    "| Select-Object -First 1 -ExpandProperty Version).ToString()");

                using var process = Process.Start(psi);

                if (process == null)
                    return null;

                var outputRead = process.StandardOutput.ReadToEndAsync();
                var errorRead = process.StandardError.ReadToEndAsync();

                if (!process.WaitForExit(3000))
                {
                    try { process.Kill(entireProcessTree: true); }
                    catch { }
                    StartupLog.Warning("terminal.version.timeout", "A Windows Terminal verziólekérdezése 3 másodperc alatt nem fejeződött be.");
                    return null;
                }

                string output = outputRead.GetAwaiter().GetResult().Trim();
                string error = errorRead.GetAwaiter().GetResult().Trim();
                if (process.ExitCode != 0)
                    StartupLog.Warning("terminal.version.failed",
                        $"exitCode={process.ExitCode}; stderr={error}");

                ret = Version.TryParse(output, out Version? version)
                    ? version
                    : null;

                StartupLog.Info("terminal.version.complete", version?.ToString() ?? "nem állapítható meg");

                return ret;
            }
            catch (Exception exception)
            {
                StartupLog.Error("terminal.version.failed", exception);
                return null;
            }
        }
    }
}
