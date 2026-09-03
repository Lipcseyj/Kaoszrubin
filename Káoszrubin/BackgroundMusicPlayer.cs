using NAudio.Wave;

namespace KaoszRubin;

/// <summary>NAudio háttérzene-lejátszó; a WAV hangeffektek útvonalától teljesen független.</summary>
public sealed class BackgroundMusicPlayer : IDisposable
{
    private readonly object _sync = new();
    private readonly Action<string>? _reportFailure;
    private readonly GameSettings _settings;
    private WaveOut? _output;
    private AudioFileReader? _reader;
    private MemoryStream? _compressedAudio;
    private CancellationTokenSource? _scheduledAction;
    private int? _activeMazeLevel;
    private bool _exitDiscovered;
    private bool _inInn;
    private bool _isExitVolumeReduced;
    private bool _disposed;

    public BackgroundMusicPlayer(GameSettings settings, Action<string>? reportFailure = null)
    {
        _settings = settings;
        _reportFailure = reportFailure;
    }

    /// <summary>Új pályán azonnal indít; azonos snapshot nem indítja újra a zenét.</summary>
    public void SynchronizeMazeLevel(int mazeLevel, bool exitDiscovered = false, bool inInn = false)
    {
        lock (_sync)
        {
            if (_disposed) return;
            var levelChanged = _activeMazeLevel != mazeLevel;
            if (levelChanged)
            {
                StopLocked();
                _activeMazeLevel = mazeLevel;
                _exitDiscovered = false;
                _inInn = false;
            }
            if (inInn)
            {
                _inInn = true;
                StopLocked();
                return;
            }
            _inInn = false;
            if (exitDiscovered)
            {
                BeginExitFadeLocked();
                return;
            }
            if (levelChanged && _settings.Enabled) PlayRandomTrackLocked();
        }
    }

    public void MarkExitDiscovered()
    {
        lock (_sync) BeginExitFadeLocked();
    }

    public void EnterInn()
    {
        lock (_sync)
        {
            _inInn = true;
            StopLocked();
        }
    }

    public void ApplySettings()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _settings.Normalize();
            if (!_settings.Enabled)
            {
                StopLocked();
                return;
            }
            if (_output is not null)
            {
                if (!_isExitVolumeReduced) _output.Volume = _settings.VolumePercent / 100f;
                return;
            }
            // A hangerő módosítása önmagában ne szakítsa meg a két szám közötti csendet.
            if (_scheduledAction is not null) return;
            if (_activeMazeLevel.HasValue && !_exitDiscovered && !_inInn) PlayRandomTrackLocked();
        }
    }

    private void PlayRandomTrackLocked()
    {
        if (!_settings.Enabled || _exitDiscovered || _inInn || _disposed) return;
        if (BackgroundMusicCatalog.RandomTrackPath() is not { } path)
        {
            _reportFailure?.Invoke("A Zene mappában nem található lejátszható MP3-fájl.");
            return;
        }
        try
        {
            StopLocked();
            _compressedAudio = new MemoryStream(File.ReadAllBytes(path), writable: false);
            _reader = new AudioFileReader(_compressedAudio);
            _output = new WaveOut { Volume = _settings.VolumePercent / 100f };
            _output.PlaybackStopped += PlaybackStopped;
            _output.Init(_reader);
            _output.Play();
        }
        catch (Exception exception)
        {
            StopLocked();
            _reportFailure?.Invoke($"Háttérzene nem indítható: {exception.Message}");
        }
    }

    private void PlaybackStopped(object? sender, StoppedEventArgs eventArgs)
    {
        lock (_sync)
        {
            if (_disposed || !ReferenceEquals(sender, _output)) return;
            ReleasePlaybackLocked();
            if (eventArgs.Exception is not null)
            {
                _reportFailure?.Invoke($"A háttérzene lejátszása megszakadt: {eventArgs.Exception.Message}");
                return;
            }
            ScheduleNextTrackLocked();
        }
    }

    private void ScheduleNextTrackLocked()
    {
        if (!_settings.Enabled || _exitDiscovered || _inInn || _disposed) return;
        CancelScheduledActionLocked();
        var cancellation = _scheduledAction = new CancellationTokenSource();
        var delay = TimeSpan.FromMinutes(Random.Shared.Next(2, 7));
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, cancellation.Token);
                lock (_sync)
                {
                    if (!cancellation.IsCancellationRequested && ReferenceEquals(cancellation, _scheduledAction))
                    {
                        _scheduledAction = null;
                        PlayRandomTrackLocked();
                    }
                }
            }
            catch (OperationCanceledException) { }
            finally { cancellation.Dispose(); }
        });
    }

    private void BeginExitFadeLocked()
    {
        if (_disposed || _exitDiscovered) return;
        _exitDiscovered = true;
        CancelScheduledActionLocked();
        if (_output is null) return;
        _isExitVolumeReduced = true;
        _output.Volume = _settings.VolumePercent / 100f * 0.25f;
    }

    public void Stop()
    {
        lock (_sync) StopLocked();
    }

    private void StopLocked()
    {
        CancelScheduledActionLocked();
        ReleasePlaybackLocked(stopOutput: true);
        _isExitVolumeReduced = false;
    }

    private void ReleasePlaybackLocked(bool stopOutput = false)
    {
        var output = _output;
        if (output is not null) output.PlaybackStopped -= PlaybackStopped;
        _output = null;
        var reader = _reader;
        _reader = null;
        var compressedAudio = _compressedAudio;
        _compressedAudio = null;
        if (stopOutput) output?.Stop();
        output?.Dispose();
        reader?.Dispose();
        compressedAudio?.Dispose();
    }

    private void CancelScheduledActionLocked()
    {
        var scheduled = _scheduledAction;
        _scheduledAction = null;
        scheduled?.Cancel();
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            StopLocked();
        }
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
