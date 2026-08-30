using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Characters;

namespace KaoszRubin;

/// <summary>A jobb alsó képpanel legfeljebb ötsoros, egycellás karakterekből álló portréi.</summary>
public static class AsciiPortraits
{
    private const int CanvasWidth = 17;

    private static readonly IReadOnlyDictionary<string, AsciiPortrait> CharacterClasses =
        new Dictionary<string, AsciiPortrait>(StringComparer.OrdinalIgnoreCase)
        {
            [CharacterClassIds.Harcos] = Portrait(
                "     └__┘",
                "     (••)  │",
                "    /|==|--╪",
                "     /  \\",
                "    /____\\"),
            [CharacterClassIds.Barbár] = Portrait(
                "    ╭━━╮  Đ",
                "    (òó) / ",
                "   /|##|/▲ ",
                "    |  |",
                "   /_/\\_\\"),
            // C003 - Lovag
            [CharacterClassIds.Lovag] = Portrait(
"     /▲\\    ║",
"    [• •]   ║",
"  ╔═|███|═╗ ║",
"  ║ |███|═╬═╣",
"  ╚═/___\\═╝"),
            [CharacterClassIds.Tolvaj] = Portrait(
                "     ▒▒▒▒  │",
                "    ▒(••)▒ ┼",
                "    /|__|--╯",
                "     /  \\",
                "    /_  _\\"),
            [CharacterClassIds.Pap] = Portrait(
                "      _†_",
                "     (• •)  ☼",
                "    /|___|--┤",
                "     |   |",
                "    /_____\\"),
            [CharacterClassIds.Mágus] = Portrait(
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
           [MonsterIds.Óriáspatkány] = Portrait(
               "     ___",
               "  __/o  \\_",
               " /  ___   \\___",
               " \\_/   \\__    ~",
               "        /_/"),

           // E002 - Kobold
           [MonsterIds.Kobold] = Portrait(
               "   /\\     /\\",
               "  /  \\___/  \\",
               " <  o   o   >",
               "  \\__▽_____/ ",
               "    /|_|\\ "),

           // E003 - Goblin
           [MonsterIds.Goblin] = Portrait(
               "  /\\       /\\",
               " <  \\_____/  >",
               "  \\ ò   ó  /",
               "   \\_▽▽▽__/",
               "    /|  |\\"),

           // E004 - Csontváz
           [MonsterIds.Csontváz] = Portrait(
    "    .----.",
    "   / ◉  ◉ \\",
    "  |  ▽▽▽▽  |",
    "   \\_||||_/",
    "   /|    |\\  †"),

           // E005 - Farkas
           [MonsterIds.Farkas] = Portrait(
               "  /\\       /\\",
               " /  \\_____/  \\",
               "|    •   •    |",
               " \\    /▲\\    /",
               "  \\__/   \\__/"),

           // E006 - Zombi
           [MonsterIds.Zombi] = Portrait(
               "     _____",
               "    /x   o\\",
               "   /__△____\\",
               "  _/|     |\\_",
               "    /_\\ /_\\ "),

           // E007 - Ork
           [MonsterIds.Ork] = Portrait(
               "    ______",
               "   / ò  ó \\",
               "  |  _▲_  |",
               "  | /↑ ↑\\ |",
               "  \\_|██|_/"),

           // E008 - Hobgoblin
           [MonsterIds.Hobgoblin] = Portrait(
               "    __/\\__",
               "   / (• •) \\",
               "  /__|██|__\\-->",
               "     |  |",
               "    /_/\\_\\ "),

           // E009 - Óriáspók
           [MonsterIds.Óriáspók] = Portrait(
               "\\  \\  /  /",
               " \\ _\\/\\_ /",
               "--(••••)--",
               " / /||\\ \\",
               "/_/ || \\_\\"),

           // E010 - Gnoll
           [MonsterIds.Gnoll] = Portrait(
               "   /\\____/\\",
               "  /  •  •  \\",
               " /   __▲__  \\",
               " \\__/▽▽▽\\__/",
               "    /|  |\\"),

           // E011 - Lidércfarkas
           [MonsterIds.Lidércfarkas] = Portrait(
    "  /\\       /\\",
    " /  \\_____/  \\",
    "|    ◉   ◉    |",
    " \\   /▽\\     /",
    "  ~~/   \\_~~"),

           // E012 - Ogre
           [MonsterIds.Ogre] = Portrait(
    "    _______",
    "   / o   o \\",
    "  |    ▲    |",
    "  |  _____  |",
    " /|_/     \\_|\\"),

           // E013 - Troll
           [MonsterIds.Troll] = Portrait(
    "   __/\\____",
    "  /  o   o \\",
    " /    ___   \\",
    "|   _/▽▽\\_  |",
    " \\_/|____|\\_/"),

           // E014 - Minotaurusz
           [MonsterIds.Minotaurusz] = Portrait(
    " \\__     __/",
    "    \\___/",
    "   / ò ó \\",
    "  |  (▲)  |",
    "   \\_===_/"),

           // E015 - Múmia
           [MonsterIds.Múmia] = Portrait(
    "    .----.",
    "   /==o===\\",
    "  |===|====|",
    "  |==/ \\===|",
    "   /_| |_\\"),

           // E016 - Medúza
           [MonsterIds.Medúza] = Portrait(
    " ~S~S~S~S~",
    "S / ò  ó \\ S",
    " S|   ▲  |S",
    "  \\  ▽▽  /",
    "   \\_____/"),

           // E017 - Kiméra
           [MonsterIds.Kiméra] = Portrait(
    " /\\  /\\  /\\",
    "(o )(ò )( o)",
    " \\▲/\\▽/\\▲/",
    "   \\====/~~~",
    "    /\\/\\"),

           // E018 - Beholder
           [MonsterIds.Beholder] = Portrait(
    "  \\◉/ \\◉/ \\◉/",
    "   \\  |  /",
    "  .--(◉)--.",
    " (  ▽▽▽▽  )",
    "  '------'"),

           // E019 - Vámpír
           [MonsterIds.Vámpír] = Portrait(
    "    _____",
    "   /ò   ó\\",
    "  |   ▽   |",
    "  \\  ▼ ▼  /",
    "  /V\\___/V\\"),

           // E020 - Vérfarkas
           [MonsterIds.Vérfarkas] = Portrait(
    "  /\\       /\\",
    " /  \\_____/  \\",
    "|   ò     ó   |",
    " \\  /▽▽▽\\   /",
    " /\\/     \\/\\"),

           // E021 - Vörös sárkány
           [MonsterIds.VörösSárkány] = Portrait(
    "   /\\____/\\",
    "  / ò    ó \\",
    " <   /▲\\    >",
    "  \\_▽▽▽▽___/",
    " ~~~/\\  /\\~~~"),

           // E022 - Lich
           [MonsterIds.Lich] = Portrait(
    "    .---.",
    "   /◉   ◉\\",
    "  |  ☠☠☠  |",
    "  \\_|||||_/",
    "   /|___|\\"),

           // E023 - Démonlovag
           [MonsterIds.Démonlovag] = Portrait(
    "   /\\___/\\",
    "  | ◉   ◉ |",
    "  |  /▲\\  |",
    " /|==|█|==|\\",
    "    /| |\\   †"),

           // E024 - Balor démon
           [MonsterIds.BalorDémon] = Portrait(
    " \\_/\\___/\\_/",
    "  / ◉     ◉ \\",
    " |   ▽▽▽▽▽   |",
    " /\\__|██|__/\\",
    "  ~~\\|  |/~~"),

           // E025 - Fekete sárkány
           [MonsterIds.FeketeSárkány] = Portrait(
    " \\^/\\____/\\^/",
    "  \\ ◉    ◉ /",
    "   \\  /▲\\  /",
    "   /_▽▽▽▽_\\",
    "  <==/\\/\\==>"),

           // E026 - Óriásdenevér
           [MonsterIds.Óriásdenevér] = Portrait(
    "\\\\           //",
    " \\\\  /\\_/\\  //",
    "  >\\( • • )/<",
    " /  \\  ▽  /  \\",
    "/_/\\_\\___/_/\\_\\"),

           // E027 - Savanyálka
           [MonsterIds.Savanyálka] = Portrait(
    "     _____",
    "   _/     \\_",
    "  /  •   •  \\",
    " /  ~~~~~~~  \\",
    " \\___________/"),

           // E028 - Útonálló
           [MonsterIds.Útonálló] = Portrait(
    "     _____",
    "    /_____\\",
    "   | •   • |",
    "  /|___▲___|\\",
    "   /|     |\\  /"),

           // E029 - Barlangi gyík
           [MonsterIds.BarlangiGyík] = Portrait(
    "       __",
    "  ____/• \\___",
    " /  _    ___  \\__",
    " \\_/ \\__/   \\___>",
    "     /_/"),

           // E030 - Pestishordozó patkány
           [MonsterIds.PestishordozóPatkány] = Portrait(
    "    _☠_",
    "  __/x  \\_",
    " /  ___   \\___",
    " \\_/   \\__   ~~",
    "   ~*~  /_/"),

           // E031 - Bugbear
           [MonsterIds.Bugbear] = Portrait(
    "   /\\____/\\",
    "  /  ò  ó  \\",
    " |    ▲     |",
    " |  ▽▽▽▽▽   |",
    "  \\_/|██|\\_/"),

           // E032 - Hárpia
           [MonsterIds.Hárpia] = Portrait(
    "\\\\  /\\_/\\  //",
    " \\\\( ò ó )//",
    "  \\ \\_▽_/ /",
    "   \\|/ \\|/",
    "    /\\ /\\"),

           // E033 - Ghoul
           [MonsterIds.Ghoul] = Portrait(
    "    _____",
    "   /◉   ◉\\",
    "  |   ▲   |",
    "  \\ ▽▽▽▽▽ /",
    " __/|    |\\__"),

           // E034 - Ifjú baziliszkusz
           [MonsterIds.IfjúBaziliszkusz] = Portrait(
    "    ^ ^ ^",
    " __/◉___◉\\___",
    "/    /▲\\     \\",
    "\\__▽▽▽_______>~",
    "   /_/ \\_\\"),

           // E035 - Ork sámán
           [MonsterIds.OrkSámán] = Portrait(
    "   ^\\____/^",
    "   / ◉  ◉ \\",
    "  |  _▲_   |",
    "  \\_/↑ ↑\\_/",
    "   /|☼☼|\\  Y"),

           // E036 - Ettin
           [MonsterIds.Ettin] = Portrait(
    "  ___     ___",
    " /ò ó\\___/ó ò\\",
    "|  ▲ |   | ▲  |",
    " \\_▽_/███\\_▽_/",
    "   /|     |\\"),

           // E037 - Wight
           [MonsterIds.Wight] = Portrait(
    "    .~~~~.",
    "   / ◉  ◉ \\",
    "  |   ▽▽   |",
    "  \\__||||__/",
    "  ~~/|  |\\~~"),

           // E038 - Wyvern
           [MonsterIds.Wyvern] = Portrait(
    "\\\\   /\\___",
    " \\\\_/◉  ▲ \\__",
    "  >  ▽▽▽    _>",
    " /\\/\\____/\\/",
    "/       \\___~>"),

           // E039 - Kőgólem
           [MonsterIds.Kőgólem] = Portrait(
    "   ._______.",
    "  /| ■   ■ |\\",
    " | |   ▲   | |",
    " |_|_______|_|",
    "   /|_____|\\ "),

           // E040 - Éji banya
           [MonsterIds.ÉjiBanya] = Portrait(
    "     /\\",
    "   _/  \\_",
    "  / ◉  ◉ \\",
    " /  __▲__  \\",
    " \\_/▽▽▽▽\\_/"),

           // E041 - Fagyóriás
           [MonsterIds.Fagyóriás] = Portrait(
    "   /\\____/\\",
    "  / ◉    ◉ \\",
    " |   __▲__   |",
    " |  /||||\\   |",
    "  \\_/|██|\\_/"),

           // E042 - Halállovag
           [MonsterIds.Halállovag] = Portrait(
    "   /\\___/\\",
    "  | ◉   ◉ |",
    "  |  ☠▲☠  |",
    " /|==|█|==|\\",
    "   /|___|\\  †"),

           // E043 - Hidra
           [MonsterIds.Hidra] = Portrait(
    " /\\  /\\  /\\",
    "(◉ )(◉ )(◉ )",
    " \\▲/\\▲/\\▲/",
    "  \\▽▽▽▽▽/",
    "   /| | |\\"),

           // E044 - Csontsárkány
           [MonsterIds.Csontsárkány] = Portrait(
    " \\^/\\____/\\^/",
    "  \\ x    x /",
    "   \\_☠▲☠_/",
    "   /_||||_\\",
    "  <==/\\/\\==>"),

           // E045 - Démonpók
           [MonsterIds.Démonpók] = Portrait(
    "\\  \\  /  /",
    " \\_/◉\\/◉\\_/",
    "--(▽▽▽▽▽)--",
    " / /|██|\\ \\",
    "/_/ /  \\ \\_\\"),

           // E046 - Ősvámpír
           [MonsterIds.Ősvámpír] = Portrait(
    "   /\\____/\\",
    "  / ◉    ◉ \\",
    " |   __▲__  |",
    "  \\  ▼▼▼▼  /",
    "  /V\\_██_/V\\"),

           // E047 - Pokolfejedelem
           [MonsterIds.Pokolfejedelem] = Portrait(
    "\\\\^/\\____/\\^//",
    " \\ ◉    ◉ /",
    " |  ▽▽▽▽▽  |",
    "/|==|██|==|\\",
    "  ~~\\|  |/~~"),

           // E048 - Vén beholder
           [MonsterIds.VénBeholder] = Portrait(
    "\\◉/\\◉/\\◉/\\◉/",
    " \\  \\ | /  /",
    " .--(◎◎)--.",
    "( ▽▽▽▽▽▽▽ )",
    " '--------'"),

           // E049 - Drakolich
           [MonsterIds.Drakolich] = Portrait(
    "\\^/\\_☠__/\\^/",
    " \\ ◉    ◉ /",
    "  \\_||||_/",
    "  /▽▽▽▽▽▽\\",
    " <==/\\/\\==>"),

           // E050 - Káoszsárkány
           [MonsterIds.Káoszsárkány] = Portrait(
    "\\^/\\◎_☠_◎/\\^/",
    " \\ ◉    ◉ /",
    " <  ▽▽▽▽  >",
    "  \\_█╳█_/",
    " ~<=/\\/\\=>~"),

           // E051 - Patkányember
           [MonsterIds.Patkányember] = Portrait(
    "  (\\_____/)",
    "  / ò   ó \\__",
    " <   _▲_____)~",
    "  \\_▽▽_/|--†",
    "    /|  |\\")
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
