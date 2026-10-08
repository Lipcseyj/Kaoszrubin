using System;
using System.IO;
using System.Text;
using AsciiArtEditor.Services;
using KaoszRubin.Infrastructure;

Log.Initialize();
Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

if (!SystemHelpers.EnsureWindowsTerminal(args, forwardArguments: true))
    return 0;

var editorArguments = SystemHelpers.GetArgumentsToForward(args);

Console.WriteLine("ASCII Portrait Editor (simple parser/debug tool)");

var path = editorArguments.Length > 0 ? editorArguments[0] : FindPortraitSourcePath();

if (path is null || !File.Exists(path))
{
    Console.WriteLine("Could not find Káoszrubin/UI/AsciiPortraits.cs. Provide its full path as the first argument.");
    return 1;
}

var source = new AsciiPortraitSource();
var portraits = source.LoadPortraitsFromFile(path);
Console.WriteLine($"Loaded {portraits.Count} portrait entries from: {Path.GetFileName(path)}\n");

if (editorArguments.Length >= 4 && editorArguments[1].Equals("update", StringComparison.OrdinalIgnoreCase))
{
    var id = editorArguments[2];
    var newFile = editorArguments[3];
    if (!File.Exists(newFile))
    {
        Console.WriteLine($"New content file not found: {newFile}");
        return 1;
    }

    var newContent = File.ReadAllText(newFile);
    var set = 1;
    if (editorArguments.Length >= 5 &&
        (!int.TryParse(editorArguments[4], out set) || set is not (1 or 2)))
    {
        Console.WriteLine("Portrait set must be 1 or 2 (optional fifth argument).");
        return 1;
    }
    var ok = source.UpdatePortraitInFile(path, id, newContent, set);
    Console.WriteLine(ok ? "Update succeeded." : "Update failed: id not found or unsupported format.");
    return ok ? 0 : 2;
}

// Start interactive editor
var app = new AsciiArtEditor.Ui.EditorApp(path, source, portraits);
app.Run();

return 0;

static string? FindPortraitSourcePath()
{
    foreach (var startDirectory in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Káoszrubin", "UI", "AsciiPortraits.cs");
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }
    }

    return null;
}
