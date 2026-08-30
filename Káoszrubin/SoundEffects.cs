using System.Runtime.InteropServices;
using System.Text;

namespace KaoszRubin;

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
    // A cache az első SoundEffects példány létrehozásakor, még a főmenü hangja előtt egyszer töltődik be,
    // és az összes későbbi host/vendég lejátszó ugyanazokat a memóriablokkokat használja.
    private static readonly string SoundsDirectory = Path.Combine(AppContext.BaseDirectory, "Sounds");
    private static readonly IReadOnlyDictionary<SoundEffect, CachedWav> WavCache = LoadWavCache();
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
        // SND_ASYNC azonnal visszatér; cache mellett nincs fájl-I/O, ezért nincs szükség külön thread-pool feladatra.
        PlayCore(effect, waitForCompletion: false);
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
            if (!WavCache.TryGetValue(effect, out var cached))
            {
                _reportFailure?.Invoke($"Hangeffekt nem található a memóriacache-ben: {ToFileName(effect)}.wav");
                return;
            }
            if (!PlaySoundMemory(cached.Pointer, IntPtr.Zero, SoundMemory | (waitForCompletion ? 0 : SoundAsync)))
                _reportFailure?.Invoke($"Hangeffekt nem indítható memóriából: {ToFileName(effect)}.wav");
        }
        catch (Exception exception)
        {
            _reportFailure?.Invoke($"Hangeffekt hiba: {exception.Message}");
        }
    }

    private static IReadOnlyDictionary<SoundEffect, CachedWav> LoadWavCache()
    {
        var cache = new Dictionary<SoundEffect, CachedWav>();
        foreach (var effect in Enum.GetValues<SoundEffect>())
        {
            var path = Path.Combine(SoundsDirectory, ToFileName(effect) + ".wav");
            if (File.Exists(path)) cache[effect] = new CachedWav(File.ReadAllBytes(path));
        }
        return cache;
    }

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
    private const uint SoundMemory = 0x0004;

    [DllImport("winmm.dll", EntryPoint = "PlaySoundW", SetLastError = true)]
    private static extern bool PlaySoundMemory(IntPtr soundData, IntPtr module, uint flags);

    private sealed class CachedWav
    {
        // SND_ASYNC mellett a mutatónak a teljes lejátszás alatt stabilnak kell maradnia.
        // A statikus cache élettartama a folyamatéval azonos, ezért a rögzítés nem okoz dangling pointert.
        private readonly GCHandle _handle;

        public CachedWav(byte[] data)
        {
            _handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            Pointer = _handle.AddrOfPinnedObject();
        }

        public IntPtr Pointer { get; }
    }
}
