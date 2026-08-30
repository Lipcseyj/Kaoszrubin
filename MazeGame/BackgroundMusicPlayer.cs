using NAudio.Wave;

namespace MazeGame;

/// <summary>NAudio háttérzene-lejátszó; a WAV hangeffektek útvonalától teljesen független.</summary>
public sealed class BackgroundMusicPlayer : IDisposable
{
    private readonly Action<string>? _reportFailure;
    private readonly MusicSettings _settings;
    private WaveOut? _output;
    private AudioFileReader? _reader;
    private MemoryStream? _compressedAudio;
    private int? _activeMazeLevel;

    public BackgroundMusicPlayer(MusicSettings settings, Action<string>? reportFailure = null)
    {
        _settings = settings;
        _reportFailure = reportFailure;
    }

    public void Play(string path)
    {
        try
        {
            Stop();
            if (!File.Exists(path))
            {
                _reportFailure?.Invoke($"Háttérzene nem található: {path}");
                return;
            }

            // Egyszeri fájlolvasás; a Media Foundation dekóder ezután kizárólag a memóriastreamből olvas.
            _compressedAudio = new MemoryStream(File.ReadAllBytes(path), writable: false);
            _reader = new AudioFileReader(_compressedAudio);
            _output = new WaveOut();
            _output.Volume = _settings.VolumePercent / 100f;
            _output.Init(_reader);
            _output.Play();
        }
        catch (Exception exception)
        {
            Stop();
            _reportFailure?.Invoke($"Háttérzene nem indítható: {exception.Message}");
        }
    }

    /// <summary>A host és a vendég közös pályazene-váltása; azonos snapshotok nem indítják újra a számot.</summary>
    public void SynchronizeMazeLevel(int mazeLevel)
    {
        if (_activeMazeLevel == mazeLevel) return;
        _activeMazeLevel = mazeLevel;
        if (!_settings.Enabled)
        {
            Stop();
            return;
        }
        if (BackgroundMusicCatalog.RandomTrackPath() is { } path)
        {
            Play(path);
        }
        else
        {
            Stop();
            _reportFailure?.Invoke("A Zene mappában nem található lejátszható MP3-fájl.");
        }
    }

    public void ApplySettings()
    {
        _settings.Normalize();
        if (!_settings.Enabled)
        {
            Stop();
            return;
        }

        if (_output is not null)
        {
            _output.Volume = _settings.VolumePercent / 100f;
            return;
        }

        if (_activeMazeLevel.HasValue && BackgroundMusicCatalog.RandomTrackPath() is { } path)
            Play(path);
    }

    public void Stop()
    {
        _output?.Stop();
        _output?.Dispose();
        _reader?.Dispose();
        _compressedAudio?.Dispose();
        _output = null;
        _reader = null;
        _compressedAudio = null;
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}

public static class BackgroundMusicCatalog
{
    public static string? RandomTrackPath()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "Zene");
        if (!Directory.Exists(directory)) return null;

        string[] tracks = Directory.GetFiles(directory, "*.mp3", SearchOption.TopDirectoryOnly);
        return tracks.Length == 0 ? null : tracks[Random.Shared.Next(tracks.Length)];
    }
}
