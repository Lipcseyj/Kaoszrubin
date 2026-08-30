using NAudio.Wave;

namespace MazeGame;

/// <summary>Egyszerű NAudio PoC háttérzene-lejátszó; a WAV hangeffektek útvonalától teljesen független.</summary>
public sealed class BackgroundMusicPlayer : IDisposable
{
    private readonly Action<string>? _reportFailure;
    private WaveOut? _output;
    private AudioFileReader? _reader;
    private MemoryStream? _compressedAudio;

    public BackgroundMusicPlayer(Action<string>? reportFailure = null)
    {
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
            _output.Init(_reader);
            _output.Play();
        }
        catch (Exception exception)
        {
            Stop();
            _reportFailure?.Invoke($"Háttérzene nem indítható: {exception.Message}");
        }
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
