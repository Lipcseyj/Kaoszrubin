using System;
using System.Collections.Generic;
using System.Text;

namespace MazeGame.UI;

public class AsciiArts
{
    public static string GetMainScreen()
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "UI", "MainScreen.txt"));
    }
}
