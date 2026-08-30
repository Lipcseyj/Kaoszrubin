using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace KaoszRubin
{
    public static class SystemHelpers
    {
        public static void EnsureWindowsTerminal()
        {
            if (IsRunningInWindowsTerminal())
                return;

            string exePath = Environment.ProcessPath
                ?? throw new InvalidOperationException(
                    "Nem határozható meg az EXE elérési útja.");

            var psi = new ProcessStartInfo
            {
                FileName = "wt.exe",
                UseShellExecute = true,
                WorkingDirectory = AppContext.BaseDirectory
            };

            psi.ArgumentList.Add("--window");
            psi.ArgumentList.Add("new");
            psi.ArgumentList.Add("--maximized");
            psi.ArgumentList.Add(exePath);

            Process.Start(psi);

            Environment.Exit(0);
        }

        static bool IsRunningInWindowsTerminal()
        {
            return !string.IsNullOrEmpty(
                Environment.GetEnvironmentVariable("WT_SESSION"));
        }
    }
}
