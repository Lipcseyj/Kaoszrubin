using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace MazeGame;

public enum SoundEffect
{
    Step1,
    Step2,
    BattleStart,
    Hit,
    Miss,
    Victory,
    Victory2,
    MemberKilled,
    LevelStart,
    LevelComplete,
    Rest,
    Chest,
    DoorOpen,
    DoorClose,
    OffensiveSpell,
    DefensiveSpell,
    Chest2,
    Item,
    MainMenu,
    MonsterKilledBySpell,
    MonsterSpotted,
    NewSkill,
    NewSpellUnlocked,
    NewWeaponProficiency,
    PlayerGotHit,
    Waiting,
    EndSequence,
    MusicTrack
}

public sealed class SoundEffects
{
    private static readonly IReadOnlyDictionary<SoundEffect, (int Frequency, int Duration)> ToneSettings =
        new Dictionary<SoundEffect, (int, int)>
        {
            [SoundEffect.Step1] = (180, 55),
            [SoundEffect.Step2] = (200, 55),
            [SoundEffect.BattleStart] = (330, 220),
            [SoundEffect.Hit] = (700, 70),
            [SoundEffect.Miss] = (250, 70),
            [SoundEffect.Victory] = (880, 320),
            [SoundEffect.Victory2] = (900, 320),
            [SoundEffect.MemberKilled] = (120, 360),
            [SoundEffect.LevelStart] = (520, 220),
            [SoundEffect.LevelComplete] = (780, 260),
            [SoundEffect.Rest] = (360, 240),
            [SoundEffect.Chest] = (980, 140),
            [SoundEffect.DoorOpen] = (280, 110),
            [SoundEffect.DoorClose] = (200, 90),
            [SoundEffect.OffensiveSpell] = (1100, 170),
            [SoundEffect.DefensiveSpell] = (640, 190),
            [SoundEffect.Chest2] = (1100, 180),
            [SoundEffect.Item] = (520, 120),
            [SoundEffect.MainMenu] = (440, 500),
            [SoundEffect.MonsterKilledBySpell] = (900, 250),
            [SoundEffect.MonsterSpotted] = (620, 180),
            [SoundEffect.NewSkill] = (760, 220),
            [SoundEffect.NewSpellUnlocked] = (980, 260),
            [SoundEffect.NewWeaponProficiency] = (680, 240),
            [SoundEffect.PlayerGotHit] = (160, 180),
            [SoundEffect.Waiting] = (420, 200),
            [SoundEffect.EndSequence] = (440, 500),
            [SoundEffect.MusicTrack] = (440, 500)
        };
    private readonly string _soundsDirectory = Path.Combine(AppContext.BaseDirectory, "Sounds");
    private readonly Dictionary<SoundEffect, DateTime> _lastPlayed = [];
    private readonly object _sync = new();
    private readonly Action<string>? _reportFailure;

    public SoundEffects(Action<string>? reportFailure = null)
    {
        _reportFailure = reportFailure;
    }

    public void Play(SoundEffect effect)
    {
        if (!TryReservePlayback(effect)) return;
        _ = Task.Run(() => PlayCore(effect, waitForCompletion: false));
    }

    /// <summary>Harci visszajelzés, amelyet a következő naplósor előtt teljesen lejátszunk.</summary>
    public void PlayAndWait(SoundEffect effect)
    {
        if (!TryReservePlayback(effect)) return;
        PlayCore(effect, waitForCompletion: true);
    }

    private bool TryReservePlayback(SoundEffect effect)
    {
        var now = DateTime.UtcNow;
        var cooldown = effect.ToString().Contains("Step")
            ? TimeSpan.FromMilliseconds(1500)
            : TimeSpan.FromMilliseconds(75);
        lock (_sync)
        {
            if (_lastPlayed.TryGetValue(effect, out var lastPlayed) && now - lastPlayed < cooldown) return false;
            _lastPlayed[effect] = now;
            return true;
        }
    }

    private void PlayCore(SoundEffect effect, bool waitForCompletion)
    {
        try
        {
            if (!OperatingSystem.IsWindows()) return;
            Directory.CreateDirectory(_soundsDirectory);
            var name = ToFileName(effect);
            var mp3Path = Path.Combine(_soundsDirectory, name + ".mp3");
            var wavPath = Path.Combine(_soundsDirectory, name + ".wav");
            if (File.Exists(mp3Path)) PlayMp3(mp3Path, ToneSettings[effect].Duration, waitForCompletion);
            else if (!PlaySound(wavPath, IntPtr.Zero, SoundFilename | (waitForCompletion ? 0 : SoundAsync)))
                _reportFailure?.Invoke($"Hangeffekt nem indítható: {wavPath}");
        }
        catch (Exception exception)
        {
            _reportFailure?.Invoke($"Hangeffekt hiba: {exception.Message}");
        }
    }

    [SupportedOSPlatform("windows")]
    private void PlayMp3(string path, int fallbackDuration, bool waitForCompletion)
    {
        // Use mciSendString to play MP3s instead of automating Windows Media Player via COM.
        // Using the COM-based Windows Media Player control caused RCW lifetime issues
        // when the player was accessed from managed threads. mciSendString avoids COM
        // RCW lifetime management and is sufficient for short sound effects.
        void PlayCore()
        {
            try
            {
                var alias = "snd" + Guid.NewGuid().ToString("N");
                var openCmd = $"open \"{path}\" type mpegvideo alias {alias}";
                mciSendString(openCmd, null, 0, IntPtr.Zero);
                mciSendString($"play {alias}", null, 0, IntPtr.Zero);
                Thread.Sleep(Math.Max(1_000, fallbackDuration * 4));
                mciSendString($"stop {alias}", null, 0, IntPtr.Zero);
                mciSendString($"close {alias}", null, 0, IntPtr.Zero);
            }
            catch (Exception exception)
            {
                _reportFailure?.Invoke($"MP3 lejátszási hiba: {exception.Message}");
            }
        }

        if (waitForCompletion) PlayCore();
        else _ = Task.Run(PlayCore);
    }

    [DllImport("winmm.dll", CharSet = CharSet.Auto)]
    private static extern int mciSendString(string command, StringBuilder? returnValue, int returnLength, IntPtr winHandle);

    private static void GenerateFallbackWav(string path, (int Frequency, int Duration) settings)
    {
        const int sampleRate = 22_050;
        var sampleCount = sampleRate * settings.Duration / 1000;
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + sampleCount * 2);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(sampleCount * 2);
        for (var index = 0; index < sampleCount; index++)
        {
            var fade = 1.0 - (double)index / sampleCount;
            var value = (short)(Math.Sin(2 * Math.PI * settings.Frequency * index / sampleRate) * 5000 * fade);
            writer.Write(value);
        }
    }

    private static string ToFileName(SoundEffect effect) => effect switch
    {
        SoundEffect.BattleStart => "battle-start",
        SoundEffect.LevelStart => "level-start",
        SoundEffect.LevelComplete => "level-complete",
        SoundEffect.DoorOpen => "door-open",
        SoundEffect.DoorClose => "door-close",
        SoundEffect.OffensiveSpell => "offensive-spell",
        SoundEffect.DefensiveSpell => "defensive-spell",
        SoundEffect.MonsterKilledBySpell => "monsterkilledbyspell",
        SoundEffect.MonsterSpotted => "monsterspotted",
        SoundEffect.NewSpellUnlocked => "newspellunlocked",
        SoundEffect.NewWeaponProficiency => "newweaponproficiency",
        SoundEffect.PlayerGotHit => "playergothit",
        SoundEffect.MemberKilled => "memberkilled",
        SoundEffect.MainMenu => "mainmenu",
        SoundEffect.EndSequence => "endsequence",
        SoundEffect.MusicTrack => "musictrack",
        _ => effect.ToString().ToLowerInvariant()
    };

    private const uint SoundAsync = 0x0001;
    private const uint SoundFilename = 0x00020000;

    [DllImport("winmm.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool PlaySound(string soundName, IntPtr module, uint flags);
}
