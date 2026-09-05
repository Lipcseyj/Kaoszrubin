using NAudio.Wave;

namespace KaoszRubin;

public enum SoundEffect
{
    Step1, Step2, Step3, Step4, Step5,
    BattleStart, Hit, Miss, Victory, Victory2, MemberKilled, LevelStart, LevelComplete, Rest,
    Chest, DoorOpen, DoorClose, OffensiveSpell, DefensiveSpell, Chest2, Item, MainMenu,
    MonsterKilledBySpell, MonsterSpotted, NewSkill, NewSpellUnlocked, NewWeaponProficiency,
    PlayerGotHit, Waiting, EndSequence, MusicTrack
}

/// <summary>NAudio-alapú, egymással párhuzamosan is lejátszható WAV hangeffektek.</summary>
public sealed class SoundEffects : IDisposable
{
    private static readonly string SoundsDirectory = Path.Combine(AppContext.BaseDirectory, "Sounds");
    private static readonly IReadOnlyDictionary<SoundEffect, byte[]> WavCache = LoadWavCache();
    private readonly Dictionary<SoundEffect, DateTime> _lastPlayed = [];
    private readonly HashSet<ActivePlayback> _activePlaybacks = [];
    private readonly object _sync = new();
    private readonly Action<string>? _reportFailure;
    private readonly GameSettings _settings;
    private bool _disposed;

    public SoundEffects(GameSettings? settings = null, Action<string>? reportFailure = null)
    {
        _settings = settings ?? new GameSettings();
        _reportFailure = reportFailure;
    }

    public void Play(SoundEffect effect)
    {
        if (TryReservePlayback(effect)) PlayCore(effect);
    }

    public void PlayAndWait(SoundEffect effect)
    {
        if (!TryReservePlayback(effect)) return;
        using var completed = new ManualResetEventSlim();
        if (PlayCore(effect, _ => completed.Set())) completed.Wait();
    }

    public void ApplySettings()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _settings.Normalize();
            var volume = _settings.SoundEffectsVolumePercent / 100f;
            foreach (var playback in _activePlaybacks) playback.Output.Volume = volume;
            if (!_settings.SoundEffectsEnabled)
                foreach (var playback in _activePlaybacks.ToArray()) playback.Output.Stop();
        }
    }

    private bool TryReservePlayback(SoundEffect effect)
    {
        var now = DateTime.UtcNow;
        var cooldown = effect.ToString().Contains("Step")
            ? TimeSpan.FromMilliseconds(1500)
            : TimeSpan.FromMilliseconds(75);
        lock (_sync)
        {
            if (_disposed || !_settings.SoundEffectsEnabled || _settings.SoundEffectsVolumePercent == 0) return false;
            if (_lastPlayed.TryGetValue(effect, out var lastPlayed) && now - lastPlayed < cooldown) return false;
            _lastPlayed[effect] = now;
            return true;
        }
    }

    private bool PlayCore(SoundEffect effect, Action<StoppedEventArgs>? stopped = null)
    {
        try
        {
            if (!OperatingSystem.IsWindows()) return false;
            if (!WavCache.TryGetValue(effect, out var wav))
            {
                _reportFailure?.Invoke($"Hangeffekt nem található a memóriacache-ben: {ToFileName(effect)}.wav");
                return false;
            }
            var playback = new ActivePlayback(wav, _settings.SoundEffectsVolumePercent / 100f);
            playback.Output.PlaybackStopped += (_, args) => FinishPlayback(playback, args, stopped);
            lock (_sync)
            {
                if (_disposed)
                {
                    playback.Dispose();
                    return false;
                }
                _activePlaybacks.Add(playback);
            }
            playback.Output.Play();
            return true;
        }
        catch (Exception exception)
        {
            _reportFailure?.Invoke($"Hangeffekt hiba: {exception.Message}");
            return false;
        }
    }

    private void FinishPlayback(ActivePlayback playback, StoppedEventArgs args, Action<StoppedEventArgs>? stopped)
    {
        lock (_sync) _activePlaybacks.Remove(playback);
        playback.Dispose();
        if (args.Exception is not null)
            _reportFailure?.Invoke($"A hangeffekt lejátszása megszakadt: {args.Exception.Message}");
        stopped?.Invoke(args);
    }

    private static IReadOnlyDictionary<SoundEffect, byte[]> LoadWavCache()
    {
        var cache = new Dictionary<SoundEffect, byte[]>();
        foreach (var effect in Enum.GetValues<SoundEffect>())
        {
            var path = Path.Combine(SoundsDirectory, ToFileName(effect) + ".wav");
            if (File.Exists(path)) cache[effect] = File.ReadAllBytes(path);
        }
        return cache;
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

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var playback in _activePlaybacks.ToArray()) playback.Dispose();
            _activePlaybacks.Clear();
        }
        GC.SuppressFinalize(this);
    }

    private sealed class ActivePlayback : IDisposable
    {
        private readonly MemoryStream _stream;
        private readonly WaveFileReader _reader;
        private int _disposed;

        public ActivePlayback(byte[] wav, float volume)
        {
            _stream = new MemoryStream(wav, writable: false);
            _reader = new WaveFileReader(_stream);
            Output = new WaveOut { Volume = volume };
            Output.Init(_reader);
        }

        public WaveOut Output { get; }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            Output.Dispose();
            _reader.Dispose();
            _stream.Dispose();
        }
    }
}
