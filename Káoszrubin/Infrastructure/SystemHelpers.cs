using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace KaoszRubin.Infrastructure;

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

        Log.Info($"wtSession={IsRunningInWindowsTerminal()}");

        if (!ShouldRelaunchInWindowsTerminal(OperatingSystem.IsWindows(), hasChildMarker, Debugger.IsAttached))
        {
            Log.Info("terminal.relaunch.skipped",
                $"windows={OperatingSystem.IsWindows()}; wtSession={IsRunningInWindowsTerminal()}; " +
                $"childMarker={hasChildMarker}; debugger={Debugger.IsAttached}");
            //maximize the current console window if we are already in Windows Terminal
            MaximizeWindow();
            return true;
        }

        string exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException(
                "Nem határozható meg az EXE elérési útja.");

        Log.Info("terminal.version.start");
        string colorScheme = GetPreferredTerminalColorScheme(out Version? terminalVersion);

        Log.Info("terminal.launch.start",
            $"version={terminalVersion?.ToString() ?? "ismeretlen"}; scheme={colorScheme}; exe={exePath}");

        TerminalLaunchHandshake handshake;
        try
        {
            handshake = TerminalLaunchHandshake.Create();
        }
        catch (Exception exception)
        {
            Log.Error("terminal.handshake.create-failed", exception);
            MaximizeWindow();
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
                Log.Warning("terminal.launch.failed", "A Process.Start nem adott vissza folyamatot; folytatás a jelenlegi konzolban.");
                MaximizeWindow();
                return true;
            }
            Log.Info("terminal.launch.requested",
                $"launcherPid={process.Id}; handshake={handshake.Id}");

            if (!handshake.ChildReady.WaitOne(TerminalLaunchTimeout))
            {
                handshake.CancelChild.Set();
                Log.Warning("terminal.launch.timeout",
                    $"A gyermekfolyamat {TerminalLaunchTimeout.TotalSeconds:0} másodperc alatt nem jelentkezett; " +
                    "folytatás a jelenlegi konzolban.");
                MaximizeWindow();
                return true;
            }

            handshake.AllowChild.Set();
            Log.Info("terminal.launch.confirmed",
                "A gyermekfolyamat elindult; a szülőfolyamat szabályosan kilép.");
            return false;
        }
        catch (Exception exception)
        {
            Log.Error("terminal.launch.failed", exception);
            MaximizeWindow();
            return true;
        }
    }

    private static void MaximizeWindow()
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            nint consoleWindowHandle = MyNativeMethods.GetConsoleWindow();
            if (consoleWindowHandle == nint.Zero)
            {
                Log.Warning("terminal.maximize.failed", "GetConsoleWindow() hwnd=0");
                return;
            }

            nint targetWindowHandle = consoleWindowHandle;
            string target = "console-host";

            if (IsRunningInWindowsTerminal())
            {
                // Windows Terminal / ConPTY alatt a GetConsoleWindow() egy nem látható
                // PseudoConsoleWindow handle-jét adhatja. A valódi Terminal ablak a
                // root owner, tipikusan CASCADIA_HOSTING_WINDOW_CLASS osztályú ablak.
                nint terminalWindowHandle = MyNativeMethods.GetAncestor(
                    consoleWindowHandle, MyNativeMethods.GA_ROOTOWNER);

                if (terminalWindowHandle != nint.Zero &&
                    MyNativeMethods.IsWindowsTerminalWindow(terminalWindowHandle))
                {
                    targetWindowHandle = terminalWindowHandle;
                    target = "windows-terminal";
                }
                else
                {
                    // Ritka fallback: ha a root-owner lánc valamiért nem használható,
                    // csak akkor nyúlunk a foreground ablakhoz, ha az bizonyíthatóan
                    // Windows Terminal. Így egy másik alkalmazást nem maximalizálunk.
                    nint foregroundWindowHandle = MyNativeMethods.GetForegroundWindow();
                    if (foregroundWindowHandle != nint.Zero &&
                        MyNativeMethods.IsWindowsTerminalWindow(foregroundWindowHandle))
                    {
                        targetWindowHandle = foregroundWindowHandle;
                        target = "windows-terminal-foreground-fallback";
                    }
                    else
                    {
                        Log.Warning(
                            "terminal.maximize.failed",
                            $"wtSession=true; consoleHwnd={consoleWindowHandle}; " +
                            $"rootOwnerHwnd={terminalWindowHandle}; " +
                            $"rootOwnerClass={MyNativeMethods.GetWindowClassName(terminalWindowHandle)}");
                        return;
                    }
                }
            }

            Log.Info(
                "terminal.maximize.start",
                $"target={target}; hwnd={targetWindowHandle}");

            MyNativeMethods.ShowWindow(targetWindowHandle, MyNativeMethods.SW_MAXIMIZE);

            Log.Info(
                "terminal.maximize.complete",
                $"target={target}; hwnd={targetWindowHandle}");
        }
        catch (Exception exception)
        {
            Log.Error("terminal.maximize.failed", exception);
        }
    }

    internal static bool ShouldRelaunchInWindowsTerminal(bool isWindows, bool hasChildMarker, bool debuggerAttached) =>
        isWindows && !hasChildMarker && !debuggerAttached;

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
                Log.Info("terminal.child.confirmed", $"handshake={handshakeId}");
                return true;
            }

            Log.Warning("terminal.child.cancelled",
                decision == WaitHandle.WaitTimeout
                    ? $"handshake={handshakeId}; a szülőfolyamat nem erősítette meg az indítást időben."
                    : $"handshake={handshakeId}; a szülőfolyamat a saját konzoljában folytatja.");
            return false;
        }
        catch (Exception exception)
        {
            Log.Error("terminal.child.handshake-failed", exception);
            return false;
        }
    }

    /// <summary>
    /// A Windows Terminal verziójához illeszkedő színsémát adja vissza. Ugyanezt használja a játék
    /// indítása és minden eszköz, amely a játékot saját Terminal-ablakban futtatja.
    /// </summary>
    public static string GetPreferredTerminalColorScheme() => GetPreferredTerminalColorScheme(out _);

    internal static string GetPreferredTerminalColorScheme(out Version? terminalVersion)
    {
        terminalVersion = GetWindowsTerminalVersion();
        return terminalVersion is not null && terminalVersion >= IbmSchemeMinimumVersion
            ? "IBM 5153"
            : "Vintage";
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
                Log.Warning("terminal.version.timeout", "A Windows Terminal verziólekérdezése 3 másodperc alatt nem fejeződött be.");
                return null;
            }

            string output = outputRead.GetAwaiter().GetResult().Trim();
            string error = errorRead.GetAwaiter().GetResult().Trim();
            if (process.ExitCode != 0)
                Log.Warning("terminal.version.failed",
                    $"exitCode={process.ExitCode}; stderr={error}");

            ret = Version.TryParse(output, out Version? version)
                ? version
                : null;

            Log.Info("terminal.version.complete", version?.ToString() ?? "nem állapítható meg");

            return ret;
        }
        catch (Exception exception)
        {
            Log.Error("terminal.version.failed", exception);
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


internal static class MyNativeMethods
{
    internal const int SW_MAXIMIZE = 3;
    internal const uint GA_ROOTOWNER = 3;

    private const string WindowsTerminalWindowClass = "CASCADIA_HOSTING_WINDOW_CLASS";

    [DllImport("kernel32.dll")]
    internal static extern nint GetConsoleWindow();

    [DllImport("user32.dll")]
    internal static extern nint GetAncestor(nint hWnd, uint gaFlags);

    [DllImport("user32.dll")]
    internal static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassName(
        nint hWnd,
        StringBuilder lpClassName,
        int nMaxCount);

    internal static string GetWindowClassName(nint hWnd)
    {
        if (hWnd == nint.Zero)
            return string.Empty;

        var className = new StringBuilder(256);
        return GetClassName(hWnd, className, className.Capacity) > 0
            ? className.ToString()
            : string.Empty;
    }

    internal static bool IsWindowsTerminalWindow(nint hWnd) =>
        string.Equals(
            GetWindowClassName(hWnd),
            WindowsTerminalWindowClass,
            StringComparison.Ordinal);
}
