using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Characters;

namespace KaoszRubin.UI;

/// <summary>A jobb alsó képpanel legfeljebb ötsoros, egycellás karakterekből álló portréi.</summary>
public static class AsciiPortraits
{
    private const int CanvasWidth = 17;

    private static readonly IReadOnlyDictionary<string, AsciiPortrait> CharacterClasses =
        new Dictionary<string, AsciiPortrait>(StringComparer.OrdinalIgnoreCase)
        {
            [CharacterClassIds.Harcos] = Portrait(
                """
                     └__┘
                     (••)  │
                    /|==|--╪
                     /  \
                    /____\
                """),
            [CharacterClassIds.Barbár] = Portrait(
                """
                    ╭━━╮  Đ
                    (òó) / 
                   /|##|/▲ 
                    |  |
                   /_/\_\
                """),
            // C003 - Lovag
            [CharacterClassIds.Lovag] = Portrait(
                """
                     /▲\    ║
                    [• •]   ║
                  ╔═|███|═╗ ║
                  ║ |███|═╬═╣
                  ╚═/___\═╝
                """),
            [CharacterClassIds.Tolvaj] = Portrait(
                """
                     ▒▒▒▒  │
                    ▒(••)▒ ┼
                    /|__|--╯
                     /  \
                    /_  _\
                """),
            [CharacterClassIds.Pap] = Portrait(
                """
                      _†_
                     (• •)  ☼
                    /|___|--┤
                     |   |
                    /_____\
                """),
            [CharacterClassIds.Mágus] = Portrait(
                """
                      /\   ✦
                     /__\ ( )
                     (••)--╂
                    /|~~|  │
                     /__\  │
                """)
        };


    private static readonly IReadOnlyDictionary<string, AsciiPortrait> Enemies =
       new Dictionary<string, AsciiPortrait>(StringComparer.OrdinalIgnoreCase)
       {
           // E001 - Óriáspatkány
           [MonsterIds.Óriáspatkány] = Portrait(
               """
                    ___
                 __/o  \_
                /  ___   \___
                \_/   \__    ~
                       /_/
               """),

           // E002 - Kobold
           [MonsterIds.Kobold] = Portrait(
               """
                  /\     /\
                 /  \___/  \
                <  o   o   >
                 \__▽_____/ 
                   /|_|\ 
               """),

           // E003 - Goblin
           [MonsterIds.Goblin] = Portrait(
               """
                 /\       /\
                <  \_____/  >
                 \ ò   ó  /
                  \_▽▽▽__/
                   /|  |\
               """),

           // E004 - Csontváz
           [MonsterIds.Csontváz] = Portrait(
               """
                   .----.
                  / ◉  ◉ \
                 |  ▽▽▽▽  |
                  \_||||_/
                  /|    |\  †
               """),

           // E005 - Farkas
           [MonsterIds.Farkas] = Portrait(
               """
                 /\       /\
                /  \_____/  \
               |    •   •    |
                \    /▲\    /
                 \__/   \__/
               """),

           // E006 - Zombi
           [MonsterIds.Zombi] = Portrait(
               """
                    _____
                   /x   o\
                  /__△____\
                 _/|     |\_
                   /_\ /_\ 
               """),

           // E007 - Ork
           [MonsterIds.Ork] = Portrait(
               """
                   ______
                  / ò  ó \
                 |  _▲_  |
                 | /↑ ↑\ |
                 \_|██|_/
               """),

           // E008 - Hobgoblin
           [MonsterIds.Hobgoblin] = Portrait(
               """
                   __/\__
                  / (• •) \
                 /__|██|__\-->
                    |  |
                   /_/\_\ 
               """),

           // E009 - Óriáspók
           [MonsterIds.Óriáspók] = Portrait(
               """
               \  \  /  /
                \ _\/\_ /
               --(••••)--
                / /||\ \
               /_/ || \_\
               """),

           // E010 - Gnoll
           [MonsterIds.Gnoll] = Portrait(
               """
                  /\____/\
                 /  •  •  \
                /   __▲__  \
                \__/▽▽▽\__/
                   /|  |\
               """),

           // E011 - Lidércfarkas
           [MonsterIds.Lidércfarkas] = Portrait(
               """
                 /\       /\
                /  \_____/  \
               |    ◉   ◉    |
                \   /▽\     /
                 ~~/   \_~~
               """),

           // E012 - Ogre
           [MonsterIds.Ogre] = Portrait(
               """
                   _______
                  / o   o \
                 |    ▲    |
                 |  _____  |
                /|_/     \_|\
               """),

           // E013 - Troll
           [MonsterIds.Troll] = Portrait(
               """
                  __/\____
                 /  o   o \
                /    ___   \
               |   _/▽▽\_  |
                \_/|____|\_/
               """),

           // E014 - Minotaurusz
           [MonsterIds.Minotaurusz] = Portrait(
               """
                \__     __/
                   \___/
                  / ò ó \
                 |  (▲)  |
                  \_===_/
               """),

           // E015 - Múmia
           [MonsterIds.Múmia] = Portrait(
               """
                   .----.
                  /==o===\
                 |===|====|
                 |==/ \===|
                  /_| |_\
               """),

           // E016 - Medúza
           [MonsterIds.Medúza] = Portrait(
               """
                 ~S~S~S~S~
                S/ ò  ó  \S
                S|   ▲   |S
                 \  ▽▽  /
                  \____/
               """),

           // E017 - Kiméra
           [MonsterIds.Kiméra] = Portrait(
               """
                /\  /\  /\
               (o )(ò )( o)
                \▲/\▽/\▲/
                  \====/~~~
                   /\/\
               """),

           // E018 - Beholder
           [MonsterIds.Beholder] = Portrait(
               """
                 \◉/ \◉/\◉/
                  \  |  /
                 .--(◉)--.
                (  ▽▽▽▽  )
                 '------'
               """),

           // E019 - Vámpír
           [MonsterIds.Vámpír] = Portrait(
               """
                   _____
                  /ò   ó\
                 |   ▽   |
                 \  ▼ ▼  /
                 /V\___/V\
               """),

           // E020 - Vérfarkas
           [MonsterIds.Vérfarkas] = Portrait(
               """
                 /\       /\
                /  \_____/  \
               |   ò     ó  |
                \  /▽▽▽\   /
                /\/     \/\
               """),

           // E021 - Vörös sárkány
           [MonsterIds.VörösSárkány] = Portrait(
               """
                  /\____/\
                 / ò    ó \
                <   /▲\    >
                 \_▽▽▽▽___/
                ~~~/\  /\~~~
               """),

           // E022 - Lich
           [MonsterIds.Lich] = Portrait(
               """
                   .---.
                  /◉   ◉\
                 | ☠☠☠ |
                 \_|||||_/
                  /|___|\
               """),

           // E023 - Démonlovag
           [MonsterIds.Démonlovag] = Portrait(
               """
                  /|____/|
                 | ◉   ◉ |
                 |  /▲\  |
                /|==|█|==|\
                   /| |\   †
               """),

           // E024 - Balor démon
           [MonsterIds.BalorDémon] = Portrait(
               """
                \_/\___/\_/
                 / ◉     ◉ \
                |   ▽▽▽▽▽   |
                /\__|██|__/\
                 ~~\|  |/~~
               """),

           // E025 - Fekete sárkány
           [MonsterIds.FeketeSárkány] = Portrait(
               """
                \^/\____/\^/
                 \ ◉    ◉ /
                  \  /▲\  /
                  /_▽▽▽▽_\
                 <==/\/\==>
               """),

           // E026 - Óriásdenevér
           [MonsterIds.Óriásdenevér] = Portrait(
               """
               \\           //
                \\  /\_/\  //
                 >\( • • )/<
                /  \  ▽  /  \
               /_/\_\___/_/\_\
               """),

           // E027 - Savanyálka
           [MonsterIds.Savanyálka] = Portrait(
               """
                    _____
                  _/     \_
                 /  •   •  \
                /  ~~~~~~~  \
                \___________/
               """),

           // E028 - Útonálló
           [MonsterIds.Útonálló] = Portrait(
               """
                    _____
                   /_____\
                  | •   • |
                 /|___▲___|\
                  /|     |\  /
               """),

           // E029 - Barlangi gyík
           [MonsterIds.BarlangiGyík] = Portrait(
               """
                      __
                 ____/• \___
                /  _    ___  \__
                \_/ \__/   \___>
                    /_/
               """),

           // E030 - Pestishordozó patkány
           [MonsterIds.PestishordozóPatkány] = Portrait(
               """
                   _☠_
                 __/x  \_
                /  ___   \___
                \_/   \__   ~~
                  ~*~  /_/
               """),

           // E031 - Bugbear
           [MonsterIds.Bugbear] = Portrait(
               """
                  /\____/\
                 /  ò  ó  \
                |    ▲     |
                |  ▽▽▽▽▽   |
                 \_/|██|\_/
               """),

           // E032 - Hárpia
           [MonsterIds.Hárpia] = Portrait(
               """
               \\  /\_/\  //
                \\( ò ó )//
                 \ \_▽_/ /
                  \|/ \|/
                   /\ /\
               """),

           // E033 - Ghoul
           [MonsterIds.Ghoul] = Portrait(
               """
                   _____
                  /◉   ◉\
                 |   ▲   |
                 \ ▽▽▽▽▽ /
                __/|    |\__
               """),

           // E034 - Ifjú baziliszkusz
           [MonsterIds.IfjúBaziliszkusz] = Portrait(
               """
                   ^ ^ ^
                __/◉___◉\___
               /    /▲\     \
               \__▽▽▽_______>~
                  /_/ \_\
               """),

           // E035 - Ork sámán
           [MonsterIds.OrkSámán] = Portrait(
               """
                  ^\____/^
                  / ◉  ◉ \
                 |  _▲_   |
                 \_/↑ ↑\_/
                  /|☼☼|\  Y
               """),

           // E036 - Ettin
           [MonsterIds.Ettin] = Portrait(
               """
                 ___     ___
                /ò ó\___/ó ò\
               |  ▲ |   | ▲  |
                \_▽_/███\_▽_/
                  /|     |\
               """),

           // E037 - Wight
           [MonsterIds.Wight] = Portrait(
               """
                   .~~~~.
                  / ◉  ◉ \
                 |   ▽▽   |
                 \__||||__/
                 ~~/|  |\~~
               """),

           // E038 - Wyvern
           [MonsterIds.Wyvern] = Portrait(
               """
               \\   /\___
                \\_/◉  ▲ \__
                 >  ▽▽▽    _>
                /\/\____/\/
               /       \___~>
               """),

           // E039 - Kőgólem
           [MonsterIds.Kőgólem] = Portrait(
               """
                  ._______.
                 /| ■   ■ |\
                | |   ▲   | |
                |_|_______|_|
                  /|_____|\ 
               """),

           // E040 - Éji banya
           [MonsterIds.ÉjiBanya] = Portrait(
               """
                    /\
                  _/  \_
                 / ◉  ◉ \
                /  __▲__  \
                \_/▽▽▽▽\_/
               """),

           // E041 - Fagyóriás
           [MonsterIds.Fagyóriás] = Portrait(
               """
                  /\____/\
                 / ◉    ◉ \
                |   __▲__  |
                |  /||||\  |
                 \_/|██|\_/
               """),

           // E042 - Halállovag
           [MonsterIds.Halállovag] = Portrait(
               """
                 †■■■■■■■†
                 | Í   Í |
                 |   ■   |
                /|==|█|==|\
                  /|___|\  †
               """),

           // E043 - Hidra
           [MonsterIds.Hidra] = Portrait(
               """
                /\  /\  /\
               (◉ )(◉ )(◉ )
                \▲/\▲/\▲/
                 \▽▽▽▽▽/
                  /| | |\
               """),

           // E044 - Csontsárkány
           [MonsterIds.Csontsárkány] = Portrait(
               """
                \^/\____/\^/
                 \ x    x /
                  \_☠▲☠_/
                  /_||||_\
                 <==/\/\==>
               """),

           // E045 - Démonpók
           [MonsterIds.Démonpók] = Portrait(
               """
               \  \  /  /
                \_/◉\/◉\_/
               --(▽▽▽▽▽)--
                / /|██|\ \
               /_/ /  \ \_\
               """),

           // E046 - Ősvámpír
           [MonsterIds.Ősvámpír] = Portrait(
               """
                  /\____/\
                 / ◉    ◉ \
                |   __▲__  |
                 \  ▼--▼  /
                 /V\_██_/V\
               """),

           // E047 - Pokolfejedelem
           [MonsterIds.Pokolfejedelem] = Portrait(
               """
               \\^/\____/\^//
                \ ◉    ◉  /
                |  ▽▽▽▽▽  |
               /|==|██|==|\
                 ~~\|  |/~~
               """),

           // E048 - Vén beholder
           [MonsterIds.VénBeholder] = Portrait(
               """
               \◉/\◉/\◉/\◉/
                \  \ | /  /
                .--(◎◎)--.
               ( ▽▽▽▽▽▽▽ )
                '--------'
               """),

           // E049 - Drakolich
           [MonsterIds.Drakolich] = Portrait(
               """
               \^/\_☠__/\^/
                \ ◉    ◉ /
                 \_||||_/
                 /▽▽▽▽▽▽\
                <==/\/\==>
               """),

           // E050 - Káoszsárkány
           [MonsterIds.Káoszsárkány] = Portrait(
               """
               \^/\◎_☠_◎/\^/
                \ ◉    ◉ /
                <  ▽▽▽▽  >
                 \_█╳█_/
                ~<=/\/\=>~
               """),

           // E051 - Patkányember
           [MonsterIds.Patkányember] = Portrait(
               """
                 (\_____/)
                 / ò   ó \
                <   _▲_   > 
                 \_▽▽_/|-/
                   /|  |\
               """),

           // E052 - Csontváz Lovag
           [MonsterIds.CsontvázLovag] = Portrait(
               """
                   .-††-.
                  / ◉  ◉ \
                 |  ▽▽▽▽  |
                  \_||||_/
                  /|    |\
               """),

           // E053 - Sir Malrec
           [MonsterIds.SirMalrec] = Portrait(
               """
                  /■■††■■\
                 | Ď   Ď |
                 |   Ô   |
                /|=▽▽▽▽▽=|\
                  /|___|\  †
               """),

           // E061 - Élőholt pátriárka
           [MonsterIds.ÉlőholtPátriárka] = Portrait(
               """
                    .†††.
                   / ◉  ◉ \
                  |  ▽▓▽   |
                   \_||||_/
                   /|†██|\
               """),

           // E062 - Goblin főnök
           [MonsterIds.GoblinFőnök] = Portrait(
               """
                     ^^^
                  /\_____/\
                 <  ò   ó  >
                  \_▽▽▽▽_/
                   /|██|\==>
               """),

           // E063 - Ork törzsfő
           [MonsterIds.OrkTörzsfő] = Portrait(
               """
                  \__^^__/
                   / ò ó \
                  | _▲_  |
                 /|=↑██↑=|\
                   /|  |\
               """),

           // E064 - Kaszás Wight
           [MonsterIds.KaszásWight] = Portrait(
               """
                    .~~~~.
                   / ◉  ◉ \   )
                  |   ▽▽   |  /
                  \__||||__/=/
                  ~~/|  |\ /
               """),

           // E065 - Vámpír kardmester
           [MonsterIds.VámpírKardmester] = Portrait(
               """
                    _____    /
                   /ò   ó\  /
                  |   ▽   |/
                   \  ▼▼ /==
                   /V\___/V\
               """),

           // E066 - Csontváz őr
           [MonsterIds.CsontvázŐr] = Portrait(
               """
                     /▲\    |
                    [◉ ◉]   |
                   | ▽▽▽ |  |
                  /|_||||_|\|
                    /|  |\  †
               """),

           // E067 - Páncélozott zombi
           [MonsterIds.PáncélozottZombi] = Portrait(
               """
                    .___._
                   /x   ◉\
                  |  _▲_  |]
                  |/|███|\|]
                   /_| |_\
               """),

           // E068 - Barlangi troll
           [MonsterIds.BarlangiTroll] = Portrait(
               """
                   __/\____
                  / ◉    ◉ \
                 |    ___   |
                /|  _/▽▽\_ |\
                  \_/|██|\_/
               """),

           // E069 - Vén múmia
           [MonsterIds.VénMúmia] = Portrait(
               """
                    .-†-.
                   /==◉===\
                  |==/▲\===|
                  |==▽▽▽===|
                   /_|=|_\
               """),

           // E070 - Alfa vérfarkas
           [MonsterIds.AlfaVérfarkas] = Portrait(
               """
                 /\         /\
                /  \_______/  \
               |   ò       ó   |
                \   /▽▽▽\    /
                /\/\/   \/\/\
               """),

           // E071 - Ősi minotaurusz
           [MonsterIds.ŐsiMinotaurusz] = Portrait(
               """
               \___       ___/
                \__\_____/__/
                   / ò ◉ \
                  |  (▲)  |
                 /|_==██==_|\
               """),

           // E072 - Káoszkultista
           [MonsterIds.Káoszkultista] = Portrait(
               """
                     /\
                    /╳ \
                   /(◉ ◉)\
                  /_| ▽ |_\  †
                    /|_|\
               """),

           // E073 - Sötételf orgyilkos
           [MonsterIds.SötételfOrgyilkos] = Portrait(
               """
                     ▒▒▒▒
                    ▒(◉◉)▒
                 <==/|__|\==>
                    /|  |\
                    /_  _\
               """),

           // E074 - Nekromanta
           [MonsterIds.Nekromanta] = Portrait(
               """
                     _☠_    ✦
                    /___\  ( )
                   / ◉ ◉ \--╂
                  /|_╳╳╳_|\ │
                    /___\   │
               """),

           // E075 - Gargoyle
           [MonsterIds.Gargoyle] = Portrait(
               """
                 /\_/\ /\_/\
                /  ◉ \_/ ◉  \
               <     /▲\     >
                \__▽▽▽▽▽__/
                  /_/   \_\
               """),

           // E076 - Óriásskorpió
           [MonsterIds.Óriásskorpió] = Portrait(
               """
                \_        _/
                 \(◉)__(◉)/
                  \_▽▽▽▽_/
                 /|/|  |\|\
                     \__>~
               """),

           // E077 - Pokolkutya
           [MonsterIds.Pokolkutya] = Portrait(
               """
                 ^/\_______/\^
                /   ◉     ◉   \
               |     /▲\      |
                \__▽▽▽▽▽____/
                 ~~/\/  \/\~~
               """),

           // E078 - Kígyóember
           [MonsterIds.Kígyóember] = Portrait(
               """
                    .-S-.
                   / ◉ ◉ \   |
                  |   ▲   |   |
                   \  Y  /====
                   /|~~~|\
               """),

           // E079 - Küklopsz
           [MonsterIds.Küklopsz] = Portrait(
               """
                    _______
                   /   ◉   \
                  |   _▲_   |
                 /|  ▽▽▽▽▽  |\
                   /|███|\==O
               """),

           // E080 - Árnylidérc
           [MonsterIds.Árnylidérc] = Portrait(
               """
                    ~~~~~
                  ~~ ◉ ◉ ~~
                 ~~   ▽   ~~
                  ~~|||||~~
                   ~~/ \~~
               """),

           // E081 - Élő páncél
           [MonsterIds.ÉlőPáncél] = Portrait(
               """
                     /▲\    |
                    [   ]   |
                   | ╳██╳ |  |
                  /|=|██|=|\|
                    /|__|\  †
               """),

           // E082 - Martalóc
           [MonsterIds.Martalóc] = Portrait(
               """
                     ___
                    /_o_\
                   | ò ó |  |
                  /|_▽▽▽_|\ |
                   /|██|\==>
               """),

           // E083 - Káoszlovag
           [MonsterIds.Káoszlovag] = Portrait(
               """
                   /■╳╳■\   †
                  | ◉  ◉ |  |
                  |  /▲\ |==|
                 /|==|██|==|\
                   /|___|\
               """),
           // E084 - Pokolfajzat
           [MonsterIds.Pokolfajzat] = Portrait(
               """
                    _/\_/\
                   / ◉  ◉ \
                  |   ▽▽   |
                  |  /__\  |
                   ~~    ~~
               """),

           // E085 - Démoni korcs
           [MonsterIds.DémoniKorcs] = Portrait(
               """
                  /\_   _/\
                 / ◉\_/◉  \
                |   /▲\    |
                 \_▽▽▽▽__/
                  /_/ \_\ 
               """),

           // E086 - Parázsdémon
           [MonsterIds.Parázsdémon] = Portrait(
               """
                   .-^^-.
                  / ◉  ◉ \
                 |  ╲▲╱   |
                 |  ▽▽▽   |
                  \_🔥🔥_/
               """),

           // E087 - Karmos démon
           [MonsterIds.KarmosDémon] = Portrait(
               """
                  /\_   _/\
                 / ◉\_/◉  \
                |   ▽▲▽    |
                /|_/   \_|\ 
                  /_/ \_\ 
               """),

           // E088 - Pokolőr
           [MonsterIds.Pokolőr] = Portrait(
               """
                   /|____|\
                  | ◉    ◉ |
                  |  /▲\   |
                 /|==|██|==|\
                    /_  _\ 
               """),

           // E089 - Vérdémon
           [MonsterIds.Vérdémon] = Portrait(
               """
                  /\_____/\
                 / ◉  ▽  ◉ \
                |   \▲/    |
                |  ▼▼ ▼▼   |
                 \_/███\_/
               """),

           // E090 - Goblin vajákos
           [MonsterIds.GoblinVajákos] = Portrait(
               """
                  /\_____/\ 
                 <  ò   ó  >
                  \  ▽▽▽  /
                   |☼☼☼|  |
                    /  \  
               """),

           // E091 - Káoszmágus tanítvány
           [MonsterIds.KáoszmágusTanítvány] = Portrait(
               """
                      /\
                     /✦\
                     (◉◉)  
                    /|╳|\
                     /_\
               """),

           // E092 - Káoszpap
           [MonsterIds.Káoszpap] = Portrait(
               """
                      _☼_
                     (◉◉)
                    /|╳|\
                    /|█|\  †
                     /_\
               """),

           // E093 - Boszorkány
           [MonsterIds.Boszorkány] = Portrait(
               """
                     __~_~
                    / ◉ ◉ \
                    \  ▽  /
                    /|~~~|\*
                     /___\
               """),

           // E094 - Ork vérpap
           [MonsterIds.OrkVérpap] = Portrait(
               """
                    ______
                   / ò  ó \
                  |  _▲_   |
                  \_/☼☼\_/†
                   /|██|\ 
               """),

           // E095 - Kígyópap
           [MonsterIds.Kígyópap] = Portrait(
               """
                  ~s~s~s~s~
                 s/ ◉  ◉  \s
                 s|   ▲    |s
                  \  ▽▽   /
                   \_| |_/
               """),

           // E096 - Káoszmágus
           [MonsterIds.Káoszmágus] = Portrait(
               """
                      /\
                     /╳╳\
                     (◉◉) 
                    /|▒✦|\
                    /_||_\
               """),

           // E097 - Sötét druida
           [MonsterIds.SötétDruida] = Portrait(
               """
                    ~\^^/~ 
                   / ◉  ◉ \
                  |   ▲    |
                  |  \_/  |*
                   /|__|\ 
               """),

           // E098 - Vérmágus
           [MonsterIds.Vérmágus] = Portrait(
               """
                      /\
                     /▒▒\
                     (◉◉)  
                    /|▒▒|\
                    / \_/ \
               """),

           // E099 - Káosz főpap
           [MonsterIds.KáoszFőpap] = Portrait(
               """
                      ☼╳☼
                     /◉◉\
                    /|██|\
                   /_|██|_\†
                     /__\
               """),

           // E100 - Feketemágus
           [MonsterIds.Feketemágus] = Portrait(
               """
                      /\
                     /☠\
                     (◉◉) 
                    /|╳|\
                    /_||_\
               """),
       };

    private static readonly AsciiPortrait Unknown = Portrait(
        """
              ???
             (? ?)
            /|___|╲
             /   \
            /_____\
        """);

    public static AsciiPortrait ForCharacterClass(string classId) =>
        CharacterClasses.GetValueOrDefault(classId, Unknown);

    public static AsciiPortrait ForEnemy(string enemyId) =>
        Enemies.GetValueOrDefault(enemyId, Unknown);

    private static AsciiPortrait Portrait(string portrait) =>
        new(portrait.ReplaceLineEndings("\n").Split('\n'), CanvasWidth);
}

public sealed record AsciiPortrait(IReadOnlyList<string> Lines, int CanvasWidth);
