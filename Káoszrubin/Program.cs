using System.Text;
using KaoszRubin.Data;
using KaoszRubin.UI;
using KaoszRubin;
using KaoszRubin.Application;
using System.Reflection;

SystemHelpers.EnsureWindowsTerminal();

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

Console.ForegroundColor = ConsoleColor.Magenta;

var dataPath = Path.Combine(AppContext.BaseDirectory, "adatok.csv");
var gameData = CsvGameDataLoader.Load(dataPath);
var savePath = Path.Combine(AppContext.BaseDirectory, "karakterek.json");
var gameSaveDirectory = Path.Combine(AppContext.BaseDirectory, "mentések");
var applicationVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
var catalogHash = CatalogFingerprint.ComputeFile(dataPath);
new KaoszRubin.UI.MainMenu(gameData, savePath, gameSaveDirectory, applicationVersion, catalogHash).Run();

