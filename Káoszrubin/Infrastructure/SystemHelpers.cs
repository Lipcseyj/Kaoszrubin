using System;
using System.Diagnostics;

namespace KaoszRubin.Infrastructure
{
    public static class SystemHelpers
    {
        private static readonly Version IbmSchemeMinimumVersion = new(1, 21);
        private static readonly TimeSpan TerminalLaunchTimeout = TimeSpan.FromSeconds(8);
        internal const string TerminalChildArgument = "--kaoszrubin-terminal-child";
        private const string TerminalChildArgumentPrefix = TerminalChildArgument + "=";

        public static bool EnsureWindowsTerminal(IReadOnlyCollection<string>? arguments = null)
        {
            string? handshakeId = GetTerminalHandshakeId(arguments);
            var hasChildMarker = arguments?.Any(value =>
                value.Equals(TerminalChildArgument, StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith(TerminalChildArgumentPrefix, StringComparison.OrdinalIgnoreCase)) == true;

            if (handshakeId is not null)
                return CompleteTerminalChildHandshake(handshakeId);

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

            TerminalLaunchHandshake handshake;
            try
            {
                handshake = TerminalLaunchHandshake.Create();
            }
            catch (Exception exception)
            {
                StartupLog.Error("terminal.handshake.create-failed", exception);
                return true;
            }

            using var ownedHandshake = handshake;
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
            psi.ArgumentList.Add(TerminalChildArgumentPrefix + handshake.Id);

            try
            {
                var process = Process.Start(psi);
                if (process is null)
                {
                    StartupLog.Warning("terminal.launch.failed", "A Process.Start nem adott vissza folyamatot; folytatás a jelenlegi konzolban.");
                    return true;
                }
                StartupLog.Info("terminal.launch.requested",
                    $"launcherPid={process.Id}; handshake={handshake.Id}");

                if (!handshake.ChildReady.WaitOne(TerminalLaunchTimeout))
                {
                    handshake.CancelChild.Set();
                    StartupLog.Warning("terminal.launch.timeout",
                        $"A gyermekfolyamat {TerminalLaunchTimeout.TotalSeconds:0} másodperc alatt nem jelentkezett; " +
                        "folytatás a jelenlegi konzolban.");
                    return true;
                }

                handshake.AllowChild.Set();
                StartupLog.Info("terminal.launch.confirmed",
                    "A gyermekfolyamat elindult; a szülőfolyamat szabályosan kilép.");
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

        internal static string? GetTerminalHandshakeId(IReadOnlyCollection<string>? arguments)
        {
            string? argument = arguments?.FirstOrDefault(value =>
                value.StartsWith(TerminalChildArgumentPrefix, StringComparison.OrdinalIgnoreCase));
            string? id = argument?[TerminalChildArgumentPrefix.Length..];
            return Guid.TryParseExact(id, "N", out var parsed) ? parsed.ToString("N") : null;
        }

        private static bool CompleteTerminalChildHandshake(string handshakeId)
        {
            try
            {
                using var handshake = TerminalLaunchHandshake.Open(handshakeId);
                handshake.ChildReady.Set();

                int decision = WaitHandle.WaitAny(
                    [handshake.AllowChild, handshake.CancelChild], TerminalLaunchTimeout);
                if (decision == 0)
                {
                    StartupLog.Info("terminal.child.confirmed", $"handshake={handshakeId}");
                    return true;
                }

                StartupLog.Warning("terminal.child.cancelled",
                    decision == WaitHandle.WaitTimeout
                        ? $"handshake={handshakeId}; a szülőfolyamat nem erősítette meg az indítást időben."
                        : $"handshake={handshakeId}; a szülőfolyamat a saját konzoljában folytatja.");
                return false;
            }
            catch (Exception exception)
            {
                StartupLog.Error("terminal.child.handshake-failed", exception);
                return false;
            }
        }

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

        private sealed class TerminalLaunchHandshake : IDisposable
        {
            private const string NamePrefix = @"Local\Kaoszrubin.TerminalLaunch.";

            private TerminalLaunchHandshake(string id, EventWaitHandle childReady,
                EventWaitHandle allowChild, EventWaitHandle cancelChild)
            {
                Id = id;
                ChildReady = childReady;
                AllowChild = allowChild;
                CancelChild = cancelChild;
            }

            public string Id { get; }
            public EventWaitHandle ChildReady { get; }
            public EventWaitHandle AllowChild { get; }
            public EventWaitHandle CancelChild { get; }

            public static TerminalLaunchHandshake Create()
            {
                string id = Guid.NewGuid().ToString("N");
                return new TerminalLaunchHandshake(
                    id,
                    new EventWaitHandle(false, EventResetMode.ManualReset, EventName(id, "ready")),
                    new EventWaitHandle(false, EventResetMode.ManualReset, EventName(id, "allow")),
                    new EventWaitHandle(false, EventResetMode.ManualReset, EventName(id, "cancel")));
            }

            public static TerminalLaunchHandshake Open(string id) => new(
                id,
                EventWaitHandle.OpenExisting(EventName(id, "ready")),
                EventWaitHandle.OpenExisting(EventName(id, "allow")),
                EventWaitHandle.OpenExisting(EventName(id, "cancel")));

            public void Dispose()
            {
                ChildReady.Dispose();
                AllowChild.Dispose();
                CancelChild.Dispose();
            }

            private static string EventName(string id, string suffix) => $"{NamePrefix}{id}.{suffix}";
        }
    }
}
