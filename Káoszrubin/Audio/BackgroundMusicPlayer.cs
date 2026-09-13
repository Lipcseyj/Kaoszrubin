using NAudio.Wave;

namespace KaoszRubin.Audio;

/// <summary>
/// A háttérzene logikai környezete. Az enum-nevek egyben a Music könyvtár
/// alkönyvtárainak nevei is.
/// </summary>
public enum BackgroundMusicContext
{
    Map,
    Inn,
    Menu,
    SmallBattle,
    LargeBattle,
    RareLevelup
}

/// <summary>
/// NAudio háttérzene-lejátszó; a WAV hangeffektek útvonalától teljesen független.
///
/// A játék mindig jelzi a lejátszónak, hogy milyen zenei kontextusba került
/// (<see cref="EnterMap"/>, <see cref="EnterInn"/>, <see cref="EnterMenu"/> stb.).
/// Kontextusváltáskor az előző zene és az esetleges várakozás megszakad,
/// majd az új kontextus Music alkönyvtárából azonnal elindul egy véletlen szám.
/// A számok között továbbra is véletlen, 2–6 perces csend van.
/// </summary>
public sealed class BackgroundMusicPlayer : IDisposable
{
    private readonly object _sync = new();
    private readonly GameSettings _settings;
    private Action<string>? _reportMessages;

    private WaveOut? _output;
    private AudioFileReader? _reader;
    private MemoryStream? _compressedAudio;
    private CancellationTokenSource? _scheduledAction;

    private int? _activeMazeLevel;
    private BackgroundMusicContext? _context;
    private bool _exitDiscovered;
    private bool _isExitVolumeReduced;
    private bool _disposed;

    public BackgroundMusicPlayer(GameSettings settings, Action<string>? reportMessages = null)
    {
        _settings = settings;
        _reportMessages = reportMessages;
    }

    public void SetReportCallback(Action<string>? reportMessages)
    {
        lock (_sync)
        {
            if (_disposed) return;
            _reportMessages = reportMessages;
        }
    }

    /// <summary>
    /// Az aktuális zenei környezet. Addig <c>null</c>, amíg a játék először
    /// nem jelzi valamelyik Enter... metódussal vagy a pályaszinkronnal.
    /// </summary>
    public BackgroundMusicContext? Context
    {
        get
        {
            lock (_sync) return _context;
        }
    }

    /// <summary>
    /// Új pályán Map zenére vált és azonnal indít.
    /// Azonos pályasnapshot önmagában nem indítja újra az aktuális zenét.
    ///
    /// A <paramref name="inInn"/> paraméter a régi hívási helyekkel való
    /// kompatibilitás miatt megmaradt; új kódból inkább az
    /// <see cref="EnterInn"/> és <see cref="EnterMap"/> metódusokat használd.
    /// </summary>
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
                _context = null;
            }

            if (inInn)
            {
                SetContextLocked(BackgroundMusicContext.Inn);
            }
            else if (levelChanged || _context is null || _context == BackgroundMusicContext.Inn)
            {
                // Az Inn -> Map átmenetet a régi SynchronizeMazeLevel hívások
                // továbbra is automatikusan kezelik. Battle/Menu/RareLevelup
                // kontextust egy azonos pályás snapshot nem ír felül.
                SetContextLocked(BackgroundMusicContext.Map);
            }

            if (exitDiscovered)
                BeginExitFadeLocked();
        }
    }

    /// <summary>
    /// Jelzi, hogy a pálya kijárata ismertté vált.
    /// A hangerőcsökkentés csak Map zene közben történik; más kontextus zenéjét
    /// (például csatát) ez nem halkítja le.
    /// </summary>
    public void MarkExitDiscovered()
    {
        lock (_sync) BeginExitFadeLocked();
    }

    /// <summary>Music/Map kontextusra vált.</summary>
    public void EnterMap() => SetContext(BackgroundMusicContext.Map);

    /// <summary>Music/Inn kontextusra vált.</summary>
    public void EnterInn() => SetContext(BackgroundMusicContext.Inn);

    /// <summary>Music/Menu kontextusra vált.</summary>
    public void EnterMenu() => SetContext(BackgroundMusicContext.Menu);

    /// <summary>Music/SmallBattle kontextusra vált.</summary>
    public void EnterSmallBattle() => SetContext(BackgroundMusicContext.SmallBattle);

    /// <summary>Music/LargeBattle kontextusra vált.</summary>
    public void EnterLargeBattle() => SetContext(BackgroundMusicContext.LargeBattle);

    /// <summary>
    /// Music/RareLevelup kontextusra vált.
    /// A ritka/kerek szintlépés lezárása után a játék hívja meg újra az
    /// <see cref="EnterMap"/>, <see cref="EnterInn"/> vagy más megfelelő
    /// környezetváltó metódust.
    /// </summary>
    public void EnterRareLevelup() => SetContext(BackgroundMusicContext.RareLevelup);

    /// <summary>
    /// A játék beállításainak módosítása után alkalmazza az engedélyezett állapotot
    /// és a hangerőt az aktuális zenei kontextusra.
    /// </summary>
    public void ApplySettings()
    {
        lock (_sync)
        {
            if (_disposed) return;

            _settings.Normalize();

            if (!_settings.MusicEnabled)
            {
                StopLocked();
                return;
            }

            if (_output is not null)
            {
                if (!_isExitVolumeReduced)
                    _output.Volume = _settings.MusicVolumePercent / 100f;
                return;
            }

            // A hangerő módosítása önmagában ne szakítsa meg
            // a két szám közötti csendet.
            if (_scheduledAction is not null) return;

            if (_context.HasValue && CanPlayCurrentContextLocked())
                PlayRandomTrackLocked();
        }
    }

    private void SetContext(BackgroundMusicContext context)
    {
        lock (_sync)
        {
            if (_disposed) return;
            SetContextLocked(context);
        }
    }

    /// <summary>
    /// Kontextusváltás közös implementációja.
    /// Azonos kontextust nem indít újra, kivéve ha nincs sem lejátszás,
    /// sem már betervezett következő szám.
    /// </summary>
    private void SetContextLocked(BackgroundMusicContext context)
    {
        if (_disposed) return;

        if (_context == context)
        {
            if (_settings.MusicEnabled &&
                _output is null &&
                _scheduledAction is null &&
                CanPlayCurrentContextLocked())
            {
                PlayRandomTrackLocked();
            }

            return;
        }

        StopLocked();
        _context = context;

        if (_settings.MusicEnabled && CanPlayCurrentContextLocked())
            PlayRandomTrackLocked();
    }

    private bool CanPlayCurrentContextLocked() =>
        !_disposed &&
        _settings.MusicEnabled &&
        _context.HasValue &&
        (_context != BackgroundMusicContext.Map || !_exitDiscovered);

    private void PlayRandomTrackLocked()
    {
        if (!CanPlayCurrentContextLocked() || _context is not { } context)
            return;

        if (BackgroundMusicCatalog.RandomTrackPath(context) is not { } path)
        {
            _reportMessages?.Invoke(
                $"A {BackgroundMusicCatalog.RelativeDirectory(context)} mappában " +
                "nem található lejátszható MP3-fájl.");
            return;
        }

        try
        {
            StopPlaybackLocked();

            _compressedAudio = new MemoryStream(
                File.ReadAllBytes(path),
                writable: false);

            _reader = new AudioFileReader(_compressedAudio);
            _output = new WaveOut
            {
                Volume = CurrentVolumeLocked()
            };

            _output.PlaybackStopped += PlaybackStopped;
            _output.Init(_reader);
            _output.Play();
        }
        catch (Exception exception)
        {
            StopLocked();
            _reportMessages?.Invoke(
                $"Háttérzene nem indítható ({BackgroundMusicCatalog.RelativeDirectory(context)}): " +
                exception.Message);
        }
    }

    private float CurrentVolumeLocked() =>
        _context == BackgroundMusicContext.Map && _exitDiscovered
            ? _settings.MusicVolumePercent / 100f * 0.25f
            : _settings.MusicVolumePercent / 100f;

    private void PlaybackStopped(object? sender, StoppedEventArgs eventArgs)
    {
        lock (_sync)
        {
            if (_disposed || !ReferenceEquals(sender, _output)) return;

            ReleasePlaybackLocked();

            if (eventArgs.Exception is not null)
            {
                _reportMessages?.Invoke(
                    $"A háttérzene lejátszása megszakadt: {eventArgs.Exception.Message}");
                return;
            }

            ScheduleNextTrackLocked();
        }
    }

    private void ScheduleNextTrackLocked()
    {
        if (!CanPlayCurrentContextLocked()) return;

        CancelScheduledActionLocked();

        var context = _context;
        var cancellation = _scheduledAction = new CancellationTokenSource();
        var delay = TimeSpan.FromMinutes(Random.Shared.Next(2, 7));

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, cancellation.Token);

                lock (_sync)
                {
                    if (!cancellation.IsCancellationRequested &&
                        ReferenceEquals(cancellation, _scheduledAction) &&
                        _context == context)
                    {
                        _scheduledAction = null;
                        PlayRandomTrackLocked();
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                cancellation.Dispose();
            }
        });
    }

    private void BeginExitFadeLocked()
    {
        if (_disposed || _exitDiscovered) return;

        _exitDiscovered = true;

        // A kijárat megtalálása csak a Map háttérzenét érinti.
        // Battle/Inn/Menu/RareLevelup zene változatlanul szólhat tovább.
        if (_context != BackgroundMusicContext.Map) return;

        CancelScheduledActionLocked();

        if (_output is null) return;

        _isExitVolumeReduced = true;
        _output.Volume = _settings.MusicVolumePercent / 100f * 0.25f;
    }

    /// <summary>Az aktuális zenét és a betervezett következő számot leállítja.</summary>
    public void Stop()
    {
        lock (_sync) StopLocked();
    }

    private void StopLocked()
    {
        CancelScheduledActionLocked();
        StopPlaybackLocked();
    }

    /// <summary>
    /// Csak az aktuális audio playbacket állítja le. A kontextus és a
    /// következő szám ütemezése ettől még külön kezelhető.
    /// </summary>
    private void StopPlaybackLocked()
    {
        ReleasePlaybackLocked(stopOutput: true);
        _isExitVolumeReduced = false;
    }

    private void ReleasePlaybackLocked(bool stopOutput = false)
    {
        var output = _output;

        if (output is not null)
            output.PlaybackStopped -= PlaybackStopped;

        _output = null;

        var reader = _reader;
        _reader = null;

        var compressedAudio = _compressedAudio;
        _compressedAudio = null;

        if (stopOutput)
            output?.Stop();

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

/// <summary>
/// A háttérzene-fájlok helyét és véletlen kiválasztását kezeli.
/// Minden zenei kontextus a Music könyvtár azonos nevű alkönyvtárát használja.
/// </summary>
public static class BackgroundMusicCatalog
{
    public static string? RandomTrackPath(BackgroundMusicContext context, Action<string>? reportMessages = null)
    {
        var directory = DirectoryPath(context);
        if (!Directory.Exists(directory)) return null;

        var tracks = Directory.GetFiles(
            directory,
            "*.mp3",
            SearchOption.TopDirectoryOnly);

        string? track = tracks.Length == 0
            ? null
            : tracks[Random.Shared.Next(tracks.Length)];

        if (track != null)
        {
            reportMessages?.Invoke($"Zene: {RelativeDirectory(context)}\\{Path.GetFileName(track)}");
        }

        return track;
    }

    public static string DirectoryPath(BackgroundMusicContext context) =>
        Path.Combine(
            AppContext.BaseDirectory,
            "Music",
            context.ToString());

    public static string RelativeDirectory(BackgroundMusicContext context) =>
        Path.Combine(
            "Music",
            context.ToString());
}
