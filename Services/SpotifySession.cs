using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace SpotifyTaskbarWidget.Services;

public class SpotifySession
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;

    public event Action? MetadataChanged;
    public event Action? PlaybackChanged;
    public event Action? TimelineChanged;
    public event Action? SessionChanged;

    public string TrackTitle { get; private set; } = "Spotify";
    public string Artist    { get; private set; } = "Not playing";
    public BitmapImage? AlbumArt { get; private set; }
    public bool IsPlaying { get; private set; }
    public TimeSpan Position { get; private set; }
    public TimeSpan Duration { get; private set; }
    public bool HasSession => _session != null;

    public async Task InitAsync()
    {
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _manager.SessionsChanged += OnSessionsChanged;

        TryPickSpotifySession();
        if (_session == null)
            AttachSession(_manager.GetCurrentSession());

        if (_session != null)
        {
            await RefreshMetadataAsync();
            RefreshPlayback();
            RefreshTimeline();
        }
    }

    private void TryPickSpotifySession()
    {
        if (_manager == null) return;
        foreach (var s in _manager.GetSessions())
        {
            if (s.SourceAppUserModelId.Contains("Spotify", StringComparison.OrdinalIgnoreCase))
            {
                AttachSession(s);
                return;
            }
        }
    }

    private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
    {
        Application.Current?.Dispatcher.InvokeAsync(async () =>
        {
            TryPickSpotifySession();
            if (_session != null)
            {
                await RefreshMetadataAsync();
                RefreshPlayback();
                RefreshTimeline();
            }
            SessionChanged?.Invoke();
        });
    }

    private void AttachSession(GlobalSystemMediaTransportControlsSession? session)
    {
        if (_session != null)
        {
            _session.MediaPropertiesChanged  -= OnMediaPropertiesChanged;
            _session.PlaybackInfoChanged     -= OnPlaybackInfoChanged;
            _session.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
        }

        _session = session;

        if (_session != null)
        {
            _session.MediaPropertiesChanged  += OnMediaPropertiesChanged;
            _session.PlaybackInfoChanged     += OnPlaybackInfoChanged;
            _session.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
        }
        else
        {
            TrackTitle = "Spotify";
            Artist     = "Not playing";
            AlbumArt   = null;
            IsPlaying  = false;
            Position   = TimeSpan.Zero;
            Duration   = TimeSpan.Zero;
        }
    }

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        => Application.Current?.Dispatcher.InvokeAsync(async () =>
        {
            await RefreshMetadataAsync();
            MetadataChanged?.Invoke();
        });

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        => Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            RefreshPlayback();
            PlaybackChanged?.Invoke();
        });

    private void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
        => Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            RefreshTimeline();
            TimelineChanged?.Invoke();
        });

    private async Task RefreshMetadataAsync()
    {
        if (_session == null) return;
        try
        {
            var props = await _session.TryGetMediaPropertiesAsync();
            if (props == null) return;

            TrackTitle = string.IsNullOrWhiteSpace(props.Title)  ? "Unknown" : props.Title;
            Artist     = string.IsNullOrWhiteSpace(props.Artist) ? ""        : props.Artist;
            AlbumArt   = props.Thumbnail != null ? await LoadThumbnailAsync(props.Thumbnail) : null;
        }
        catch { }
    }

    private static async Task<BitmapImage?> LoadThumbnailAsync(IRandomAccessStreamReference thumbnailRef)
    {
        try
        {
            using var stream = await thumbnailRef.OpenReadAsync();
            if (stream.Size == 0) return null;

            var ms = new MemoryStream();

            // Copy WinRT stream → MemoryStream via DataReader in chunks
            using var reader = new DataReader(stream);
            reader.InputStreamOptions = InputStreamOptions.Partial;
            uint remaining = (uint)stream.Size;
            const uint chunk = 65536;
            while (remaining > 0)
            {
                uint toRead = Math.Min(chunk, remaining);
                uint loaded = await reader.LoadAsync(toRead);
                if (loaded == 0) break;
                var buf = new byte[loaded];
                reader.ReadBytes(buf);
                ms.Write(buf, 0, (int)loaded);
                remaining -= loaded;
            }

            ms.Seek(0, SeekOrigin.Begin);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource  = ms;
            bitmap.CacheOption   = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch { return null; }
    }

    private void RefreshPlayback()
    {
        if (_session == null) return;
        try
        {
            var info  = _session.GetPlaybackInfo();
            IsPlaying = info?.PlaybackStatus ==
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        }
        catch { }
    }

    private void RefreshTimeline()
    {
        if (_session == null) return;
        try
        {
            var tl = _session.GetTimelineProperties();
            if (tl == null) return;
            Position = tl.Position;
            Duration = tl.EndTime;
        }
        catch { }
    }

    public void TickPosition()
    {
        if (!IsPlaying || Duration <= TimeSpan.Zero) return;
        var next = Position + TimeSpan.FromMilliseconds(500);
        Position = next > Duration ? Duration : next;
    }

    public async Task PlayPauseAsync() { if (_session != null) await _session.TryTogglePlayPauseAsync(); }
    public async Task NextAsync()      { if (_session != null) await _session.TrySkipNextAsync(); }
    public async Task PrevAsync()      { if (_session != null) await _session.TrySkipPreviousAsync(); }
}
