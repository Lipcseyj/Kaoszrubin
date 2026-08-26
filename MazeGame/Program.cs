using System.Text;
using MazeGame.Data;
using MazeGame.UI;
using MazeGame;
using MazeGame.Application;
using System.Reflection;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

Console.ForegroundColor = ConsoleColor.Magenta;

var dataPath = Path.Combine(AppContext.BaseDirectory, "adatok.csv");
var gameData = CsvGameDataLoader.Load(dataPath);
var savePath = Path.Combine(AppContext.BaseDirectory, "karakterek.json");
var gameSaveDirectory = Path.Combine(AppContext.BaseDirectory, "mentések");
var applicationVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
var catalogHash = CatalogFingerprint.ComputeFile(dataPath);
new MainMenu(gameData, savePath, gameSaveDirectory, applicationVersion, catalogHash).Run();
