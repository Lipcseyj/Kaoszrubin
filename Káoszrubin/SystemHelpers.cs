using System;
using System.Diagnostics;

namespace KaoszRubin
{
    public static class SystemHelpers
    {
        private static readonly Version IbmSchemeMinimumVersion = new(1, 21);

        public static void EnsureWindowsTerminal()
        {
            if (IsRunningInWindowsTerminal())
                return;

            string exePath = Environment.ProcessPath
                ?? throw new InvalidOperationException(
                    "Nem határozható meg az EXE elérési útja.");

            Version? terminalVersion = GetWindowsTerminalVersion();

            string colorScheme =
                terminalVersion is not null &&
                terminalVersion >= IbmSchemeMinimumVersion
                    ? "IBM 5153"
                    : "Vintage";

            Debug.WriteLine("Launching in Windows Terminal with color scheme: " + colorScheme);

            var psi = new ProcessStartInfo
            {
                FileName = "wt.exe",
                UseShellExecute = true,
                WorkingDirectory = AppContext.BaseDirectory
            };

            psi.ArgumentList.Add("--window");
            psi.ArgumentList.Add("new");

            psi.ArgumentList.Add("--maximized");

            psi.ArgumentList.Add("--colorScheme");
            psi.ArgumentList.Add(colorScheme);

            psi.ArgumentList.Add(exePath);

            Process.Start(psi);

            Environment.Exit(0);
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

                string output = process.StandardOutput.ReadToEnd().Trim();

                process.WaitForExit();

                ret = Version.TryParse(output, out Version? version)
                    ? version
                    : null;

                Debug.WriteLine($"Windows Terminal version: {version}");

                return ret;
            }
            catch
            {
                return null;
            }
        }
    }
}