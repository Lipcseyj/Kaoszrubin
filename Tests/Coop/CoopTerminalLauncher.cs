using System.Diagnostics;
using KaoszRubin.Infrastructure;

namespace KaoszRubin.Tests.Coop;

/// <summary>
/// A szerepablakokat ugyanugy inditja, ahogy az eles jatek indul: uj, maximalizalt Windows Terminal
/// ablakban, a Terminal verziojahoz illo szinsemaval. A legacy conhost nem tudja kirajzolni a jatek
/// emoji-karaktereit (BMP-n kivuli kodpontok), ezert a Terminal hasznalata kotelezo a vizualis teszthez.
/// </summary>
internal static class CoopTerminalLauncher
{
    public static ProcessStartInfo CreateStartInfo(string title, string executablePath,
        IReadOnlyList<string> executableArguments, out bool usesWindowsTerminal)
    {
        usesWindowsTerminal = IsWindowsTerminalAvailable();
        if (!usesWindowsTerminal)
        {
            var fallback = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = true,
                WorkingDirectory = AppContext.BaseDirectory
            };
            foreach (var argument in executableArguments) fallback.ArgumentList.Add(argument);
            return fallback;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "wt.exe",
            UseShellExecute = true,
            WorkingDirectory = AppContext.BaseDirectory
        };

        startInfo.ArgumentList.Add("--window");
        startInfo.ArgumentList.Add("new");
        startInfo.ArgumentList.Add("--fullscreen");
        startInfo.ArgumentList.Add("new-tab");
        startInfo.ArgumentList.Add("--title");
        startInfo.ArgumentList.Add(title);
        startInfo.ArgumentList.Add("--colorScheme");
        startInfo.ArgumentList.Add(SystemHelpers.GetPreferredTerminalColorScheme());
        startInfo.ArgumentList.Add("--startingDirectory");
        startInfo.ArgumentList.Add(AppContext.BaseDirectory);
        startInfo.ArgumentList.Add(executablePath);
        foreach (var argument in executableArguments) startInfo.ArgumentList.Add(argument);
        return startInfo;
    }

    private static bool IsWindowsTerminalAvailable()
    {
        if (!OperatingSystem.IsWindows()) return false;
        var pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathVariable)) return false;
        foreach (var directory in pathVariable.Split(Path.PathSeparator,
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                if (File.Exists(Path.Combine(directory, "wt.exe"))) return true;
            }
            catch (ArgumentException)
            {
                // Ervenytelen PATH-bejegyzest atlepunk.
            }
        }

        return false;
    }
}
