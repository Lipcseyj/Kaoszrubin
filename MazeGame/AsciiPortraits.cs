namespace MazeGame;

/// <summary>A jobb alsó képpanel legfeljebb ötsoros, egycellás karakterekből álló portréi.</summary>
public static class AsciiPortraits
{
    private const int CanvasWidth = 17;

    private static readonly IReadOnlyDictionary<string, AsciiPortrait> CharacterClasses =
        new Dictionary<string, AsciiPortrait>(StringComparer.OrdinalIgnoreCase)
        {
            ["C001"] = Portrait(
                "     └__┘",
                "     (••)  │",
                "    /|==|--╪",
                "     /  \\",
                "    /____\\"),
            ["C002"] = Portrait(
                "    ╭━━╮  Đ",
                "    (òó) / ",
                "   /|##|/▲ ",
                "    |  |",
                "   /_/\\_\\"),
            // C003 - Lovag
            ["C003"] = Portrait(
"     /▲\\     ║",
"    [• •]    ║",
"  ╔═|███|═╗  ║",
"  ║ |███|═╬══╣",
"  ╚═/___\\═╝"),
            ["C004"] = Portrait(
                "     ▒▒▒▒  │",
                "    ▒(••)▒ ┼",
                "    /|__|--╯",
                "     /  \\",
                "    /_  _\\"),
            ["C005"] = Portrait(
                "      _†_",
                "     (• •)  ☼",
                "    /|___|--┤",
                "     |   |",
                "    /_____\\"),
            ["C006"] = Portrait(
                "      /\\   ✦",
                "     /__\\ ( )",
                "     (••)--╂",
                "    /|~~|  │",
                "     /__\\ │")
        };


    private static readonly IReadOnlyDictionary<string, AsciiPortrait> Enemies =
       new Dictionary<string, AsciiPortrait>(StringComparer.OrdinalIgnoreCase)
       {
           // E001 - Óriáspatkány
           ["E001"] = Portrait(
               "     ___",
               "  __/o  \\_",
               " /  ___   \\___",
               " \\_/   \\__    ~",
               "        /_/"),

           // E002 - Kobold
           ["E002"] = Portrait(
               "   /\\     /\\",
               "  /  \\___/  \\",
               " <  o   o   >",
               "  \\__▽_____/ ",
               "    /|_|\\ "),

           // E003 - Goblin
           ["E003"] = Portrait(
               "  /\\       /\\",
               " <  \\_____/  >",
               "  \\ ò   ó  /",
               "   \\_▽▽▽__/",
               "    /|  |\\"),

           // E004 - Csontváz
           ["E004"] = Portrait(
    "    .----.",
    "   / ◉  ◉ \\",
    "  |  ▽▽▽▽  |",
    "   \\_||||_/",
    "   /|    |\\  †"),

           // E005 - Farkas
           ["E005"] = Portrait(
               "  /\\       /\\",
               " /  \\_____/  \\",
               "|    •   •    |",
               " \\    /▲\\    /",
               "  \\__/   \\__/"),

           // E006 - Zombi
           ["E006"] = Portrait(
               "     _____",
               "    /x   o\\",
               "   /__△____\\",
               "  _/|     |\\_",
               "    /_\\ /_\\ "),

           // E007 - Ork
           ["E007"] = Portrait(
               "    ______",
               "   / ò  ó \\",
               "  |  _▲_  |",
               "  | /↑ ↑\\ |",
               "  \\_|██|_/"),

           // E008 - Hobgoblin
           ["E008"] = Portrait(
               "    __/\\__",
               "   / (• •) \\",
               "  /__|██|__\\-->",
               "     |  |",
               "    /_/\\_\\ "),

           // E009 - Óriáspók
           ["E009"] = Portrait(
               "\\  \\  /  /",
               " \\ _\\/\\_ /",
               "--(••••)--",
               " / /||\\ \\",
               "/_/ || \\_\\"),

           // E010 - Gnoll
           ["E010"] = Portrait(
               "   /\\____/\\",
               "  /  •  •  \\",
               " /   __▲__  \\",
               " \\__/▽▽▽\\__/",
               "    /|  |\\"),

           // E011 - Lidércfarkas
           ["E011"] = Portrait(
    "  /\\       /\\",
    " /  \\_____/  \\",
    "|    ◉   ◉    |",
    " \\   /▽\\     /",
    "  ~~/   \\_~~"),

           // E012 - Ogre
           ["E012"] = Portrait(
    "    _______",
    "   / o   o \\",
    "  |    ▲    |",
    "  |  _____  |",
    " /|_/     \\_|\\"),

           // E013 - Troll
           ["E013"] = Portrait(
    "   __/\\____",
    "  /  o   o \\",
    " /    ___   \\",
    "|   _/▽▽\\_  |",
    " \\_/|____|\\_/"),

           // E014 - Minotaurusz
           ["E014"] = Portrait(
    " \\__     __/",
    "    \\___/",
    "   / ò ó \\",
    "  |  (▲)  |",
    "   \\_===_/"),

           // E015 - Múmia
           ["E015"] = Portrait(
    "    .----.",
    "   /==o===\\",
    "  |===|====|",
    "  |==/ \\===|",
    "   /_| |_\\"),

           // E016 - Medúza
           ["E016"] = Portrait(
    " ~S~S~S~S~",
    "S / ò  ó \\ S",
    " S|   ▲  |S",
    "  \\  ▽▽  /",
    "   \\_____/"),

           // E017 - Kiméra
           ["E017"] = Portrait(
    " /\\  /\\  /\\",
    "(o )(ò )( o)",
    " \\▲/\\▽/\\▲/",
    "   \\====/~~~",
    "    /\\/\\"),

           // E018 - Beholder
           ["E018"] = Portrait(
    "  \\◉/ \\◉/ \\◉/",
    "   \\  |  /",
    "  .--(◉)--.",
    " (  ▽▽▽▽  )",
    "  '------'"),

           // E019 - Vámpír
           ["E019"] = Portrait(
    "    _____",
    "   /ò   ó\\",
    "  |   ▽   |",
    "  \\  ▼ ▼  /",
    "  /V\\___/V\\"),

           // E020 - Vérfarkas
           ["E020"] = Portrait(
    "  /\\       /\\",
    " /  \\_____/  \\",
    "|   ò     ó   |",
    " \\  /▽▽▽\\   /",
    " /\\/     \\/\\"),

           // E021 - Vörös sárkány
           ["E021"] = Portrait(
    "   /\\____/\\",
    "  / ò    ó \\",
    " <   /▲\\    >",
    "  \\_▽▽▽▽___/",
    " ~~~/\\  /\\~~~"),

           // E022 - Lich
           ["E022"] = Portrait(
    "    .---.",
    "   /◉   ◉\\",
    "  |  ☠☠☠  |",
    "  \\_|||||_/",
    "   /|___|\\"),

           // E023 - Démonlovag
           ["E023"] = Portrait(
    "   /\\___/\\",
    "  | ◉   ◉ |",
    "  |  /▲\\  |",
    " /|==|█|==|\\",
    "    /| |\\   †"),

           // E024 - Balor démon
           ["E024"] = Portrait(
    " \\_/\\___/\\_/",
    "  / ◉     ◉ \\",
    " |   ▽▽▽▽▽   |",
    " /\\__|██|__/\\",
    "  ~~\\|  |/~~"),

           // E025 - Fekete sárkány
           ["E025"] = Portrait(
    " \\^/\\____/\\^/",
    "  \\ ◉    ◉ /",
    "   \\  /▲\\  /",
    "   /_▽▽▽▽_\\",
    "  <==/\\/\\==>"),

           // E026 - Óriásdenevér
           ["E026"] = Portrait(
    "\\\\           //",
    " \\\\  /\\_/\\  //",
    "  >\\( • • )/<",
    " /  \\  ▽  /  \\",
    "/_/\\_\\___/_/\\_\\"),

           // E027 - Savanyálka
           ["E027"] = Portrait(
    "     _____",
    "   _/     \\_",
    "  /  •   •  \\",
    " /  ~~~~~~~  \\",
    " \\___________/"),

           // E028 - Útonálló
           ["E028"] = Portrait(
    "     _____",
    "    /_____\\",
    "   | •   • |",
    "  /|___▲___|\\",
    "   /|     |\\  /"),

           // E029 - Barlangi gyík
           ["E029"] = Portrait(
    "       __",
    "  ____/• \\___",
    " /  _    ___  \\__",
    " \\_/ \\__/   \\___>",
    "     /_/"),

           // E030 - Pestishordozó patkány
           ["E030"] = Portrait(
    "    _☠_",
    "  __/x  \\_",
    " /  ___   \\___",
    " \\_/   \\__   ~~",
    "   ~*~  /_/"),

           // E031 - Bugbear
           ["E031"] = Portrait(
    "   /\\____/\\",
    "  /  ò  ó  \\",
    " |    ▲     |",
    " |  ▽▽▽▽▽   |",
    "  \\_/|██|\\_/"),

           // E032 - Hárpia
           ["E032"] = Portrait(
    "\\\\  /\\_/\\  //",
    " \\\\( ò ó )//",
    "  \\ \\_▽_/ /",
    "   \\|/ \\|/",
    "    /\\ /\\"),

           // E033 - Ghoul
           ["E033"] = Portrait(
    "    _____",
    "   /◉   ◉\\",
    "  |   ▲   |",
    "  \\ ▽▽▽▽▽ /",
    " __/|    |\\__"),

           // E034 - Ifjú baziliszkusz
           ["E034"] = Portrait(
    "    ^ ^ ^",
    " __/◉___◉\\___",
    "/    /▲\\     \\",
    "\\__▽▽▽_______>~",
    "   /_/ \\_\\"),

           // E035 - Ork sámán
           ["E035"] = Portrait(
    "   ^\\____/^",
    "   / ◉  ◉ \\",
    "  |  _▲_   |",
    "  \\_/↑ ↑\\_/",
    "   /|☼☼|\\  Y"),

           // E036 - Ettin
           ["E036"] = Portrait(
    "  ___     ___",
    " /ò ó\\___/ó ò\\",
    "|  ▲ |   | ▲  |",
    " \\_▽_/███\\_▽_/",
    "   /|     |\\"),

           // E037 - Wight
           ["E037"] = Portrait(
    "    .~~~~.",
    "   / ◉  ◉ \\",
    "  |   ▽▽   |",
    "  \\__||||__/",
    "  ~~/|  |\\~~"),

           // E038 - Wyvern
           ["E038"] = Portrait(
    "\\\\   /\\___",
    " \\\\_/◉  ▲ \\__",
    "  >  ▽▽▽    _>",
    " /\\/\\____/\\/",
    "/       \\___~>"),

           // E039 - Kőgólem
           ["E039"] = Portrait(
    "   ._______.",
    "  /| ■   ■ |\\",
    " | |   ▲   | |",
    " |_|_______|_|",
    "   /|_____|\\ "),

           // E040 - Éji banya
           ["E040"] = Portrait(
    "     /\\",
    "   _/  \\_",
    "  / ◉  ◉ \\",
    " /  __▲__  \\",
    " \\_/▽▽▽▽\\_/"),

           // E041 - Fagyóriás
           ["E041"] = Portrait(
    "   /\\____/\\",
    "  / ◉    ◉ \\",
    " |   __▲__   |",
    " |  /||||\\   |",
    "  \\_/|██|\\_/"),

           // E042 - Halállovag
           ["E042"] = Portrait(
    "   /\\___/\\",
    "  | ◉   ◉ |",
    "  |  ☠▲☠  |",
    " /|==|█|==|\\",
    "   /|___|\\  †"),

           // E043 - Hidra
           ["E043"] = Portrait(
    " /\\  /\\  /\\",
    "(◉ )(◉ )(◉ )",
    " \\▲/\\▲/\\▲/",
    "  \\▽▽▽▽▽/",
    "   /| | |\\"),

           // E044 - Csontsárkány
           ["E044"] = Portrait(
    " \\^/\\____/\\^/",
    "  \\ x    x /",
    "   \\_☠▲☠_/",
    "   /_||||_\\",
    "  <==/\\/\\==>"),

           // E045 - Démonpók
           ["E045"] = Portrait(
    "\\  \\  /  /",
    " \\_/◉\\/◉\\_/",
    "--(▽▽▽▽▽)--",
    " / /|██|\\ \\",
    "/_/ /  \\ \\_\\"),

           // E046 - Ősvámpír
           ["E046"] = Portrait(
    "   /\\____/\\",
    "  / ◉    ◉ \\",
    " |   __▲__  |",
    "  \\  ▼▼▼▼  /",
    "  /V\\_██_/V\\"),

           // E047 - Pokolfejedelem
           ["E047"] = Portrait(
    "\\\\^/\\____/\\^//",
    " \\ ◉    ◉ /",
    " |  ▽▽▽▽▽  |",
    "/|==|██|==|\\",
    "  ~~\\|  |/~~"),

           // E048 - Vén beholder
           ["E048"] = Portrait(
    "\\◉/\\◉/\\◉/\\◉/",
    " \\  \\ | /  /",
    " .--(◎◎)--.",
    "( ▽▽▽▽▽▽▽ )",
    " '--------'"),

           // E049 - Drakolich
           ["E049"] = Portrait(
    "\\^/\\_☠__/\\^/",
    " \\ ◉    ◉ /",
    "  \\_||||_/",
    "  /▽▽▽▽▽▽\\",
    " <==/\\/\\==>"),

           // E050 - Káoszsárkány
           ["E050"] = Portrait(
    "\\^/\\◎_☠_◎/\\^/",
    " \\ ◉    ◉ /",
    " <  ▽▽▽▽  >",
    "  \\_█╳█_/",
    " ~<=/\\/\\=>~")
       };

    private static readonly AsciiPortrait Unknown = Portrait(
        "      ???",
        "     (? ?)",
        "    /|___|╲",
        "     /   \\",
        "    /_____\\");

    public static AsciiPortrait ForCharacterClass(string classId) =>
        CharacterClasses.GetValueOrDefault(classId, Unknown);

    public static AsciiPortrait ForEnemy(string enemyId) =>
        Enemies.GetValueOrDefault(enemyId, Unknown);

    private static AsciiPortrait Portrait(params string[] lines) => new(lines, CanvasWidth);
}

public sealed record AsciiPortrait(IReadOnlyList<string> Lines, int CanvasWidth);
