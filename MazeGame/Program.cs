using System.Text;
using MazeGame.Data;
using MazeGame.UI;
using MazeGame;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

var dataPath = Path.Combine(AppContext.BaseDirectory, "adatok.csv");
var gameData = CsvGameDataLoader.Load(dataPath);
var savePath = Path.Combine(AppContext.BaseDirectory, "karakterek.json");
var gameSaveDirectory = Path.Combine(AppContext.BaseDirectory, "mentések");
new MainMenu(gameData, savePath, gameSaveDirectory).Run();
