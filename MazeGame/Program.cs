using System.Text;
using MazeGame.Data;
using MazeGame.UI;
using MazeGame;
using MazeGame.Application;
using System.Reflection;

SystemHelpers.EnsureWindowsTerminal();

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

Console.ForegroundColor = ConsoleColor.Magenta;

//ImageViewer.Show(Path.Combine(AppContext.BaseDirectory, "UI\\dragon.png"));
//Console.ReadKey();
//ImageViewer.Close();
//Console.ReadKey();

var dataPath = Path.Combine(AppContext.BaseDirectory, "adatok.csv");
var gameData = CsvGameDataLoader.Load(dataPath);
var savePath = Path.Combine(AppContext.BaseDirectory, "karakterek.json");
var gameSaveDirectory = Path.Combine(AppContext.BaseDirectory, "mentések");
var applicationVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
var catalogHash = CatalogFingerprint.ComputeFile(dataPath);
new MazeGame.UI.MainMenu(gameData, savePath, gameSaveDirectory, applicationVersion, catalogHash).Run();

