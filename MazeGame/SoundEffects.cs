using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace MazeGame;

public enum SoundEffect
{
    Step,
    BattleStart,
    Hit,
    Miss,
    Victory,
    Defeat,
    LevelStart,
    LevelComplete,
    Rest,
    Chest,
    DoorOpen,
    DoorClose,
    OffensiveSpell,
    DefensiveSpell
}

public sealed class SoundEffects
{
    private static readonly IReadOnlyDictionary<SoundEffect, (int Frequency, int Duration)> ToneSettings =
        new Dictionary<SoundEffect, (int, int)>
        {
            [SoundEffect.Step] = (180, 55),
            [SoundEffect.BattleStart] = (330, 220),
            [SoundEffect.Hit] = (700, 70),
            [SoundEffect.Miss] = (250, 70),
            [SoundEffect.Victory] = (880, 320),
            [SoundEffect.Defeat] = (120, 360),
            [SoundEffect.LevelStart] = (520, 220),
            [SoundEffect.LevelComplete] = (780, 260),
            [SoundEffect.Rest] = (360, 240),
            [SoundEffect.Chest] = (980, 140),
            [SoundEffect.DoorOpen] = (280, 110),
            [SoundEffect.DoorClose] = (200, 90),
            [SoundEffect.OffensiveSpell] = (1100, 170),
            [SoundEffect.DefensiveSpell] = (640, 190)
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
        var now = DateTime.UtcNow;
        var cooldown = effect == SoundEffect.Step ? TimeSpan.FromMilliseconds(150) : TimeSpan.FromMilliseconds(75);
        lock (_sync)
        {
            if (_lastPlayed.TryGetValue(effect, out var lastPlayed) && now - lastPlayed < cooldown) return;
            _lastPlayed[effect] = now;
        }
        _ = Task.Run(() => PlayCore(effect));
    }

    private void PlayCore(SoundEffect effect)
    {
        try
        {
            if (!OperatingSystem.IsWindows()) return;
            Directory.CreateDirectory(_soundsDirectory);
            var name = ToFileName(effect);
            var mp3Path = Path.Combine(_soundsDirectory, name + ".mp3");
            var wavPath = Path.Combine(_soundsDirectory, name + ".wav");
            if (!File.Exists(mp3Path) && !File.Exists(wavPath)) GenerateFallbackWav(wavPath, ToneSettings[effect]);
            if (File.Exists(mp3Path)) PlayMp3OnStaThread(mp3Path, ToneSettings[effect].Duration);
            else if (!PlaySound(wavPath, IntPtr.Zero, SoundAsync | SoundFilename))
                _reportFailure?.Invoke($"Hangeffekt nem indítható: {wavPath}");
        }
        catch (Exception exception)
        {
            _reportFailure?.Invoke($"Hangeffekt hiba: {exception.Message}");
        }
    }

    [SupportedOSPlatform("windows")]
    private void PlayMp3OnStaThread(string path, int fallbackDuration)
    {
        var thread = new Thread(() =>
        {
            object? player = null;
            try
            {
                var playerType = Type.GetTypeFromProgID("WMPlayer.OCX")
                    ?? throw new InvalidOperationException("A Windows Media Player nem érhető el.");
                player = Activator.CreateInstance(playerType)
                    ?? throw new InvalidOperationException("A Windows Media Player nem indítható.");
                dynamic mediaPlayer = player;
                mediaPlayer.URL = new Uri(path).AbsoluteUri;
                mediaPlayer.controls.play();
                Thread.Sleep(Math.Max(1_000, fallbackDuration * 4));
                mediaPlayer.controls.stop();
            }
            catch (Exception exception)
            {
                _reportFailure?.Invoke($"MP3 lejátszási hiba: {exception.Message}");
            }
            finally
            {
                if (player is not null && Marshal.IsComObject(player)) Marshal.ReleaseComObject(player);
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
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
        _ => effect.ToString().ToLowerInvariant()
    };

    private const uint SoundAsync = 0x0001;
    private const uint SoundFilename = 0x00020000;

    [DllImport("winmm.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool PlaySound(string soundName, IntPtr module, uint flags);
}
