using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SpotifyTaskbarWidget.Services;

namespace SpotifyTaskbarWidget;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly SpotifySession _session = new();
    private readonly DispatcherTimer _timer;
    private readonly DispatcherTimer _topmostTimer;

    // WinEvent hook for taskbar click detection
    private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);
    private WinEventDelegate? _winEventDelegate;
    private IntPtr _winEventHook;

    [DllImport("user32.dll")] private static extern IntPtr SetWinEventHook(uint eMin, uint eMax, IntPtr hmod, WinEventDelegate proc, uint pid, uint tid, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr FindWindow(string cls, string? name);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr hWnd);

    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT   = 0x0000;
    private const uint SWP_NOMOVE    = 0x0002;
    private const uint SWP_NOSIZE    = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;

    private string _trackTitle  = "Spotify";
    private string _artist      = "Not playing";
    private BitmapImage? _albumArt;
    private bool _isPlaying;
    private string _elapsedStr  = "0:00";
    private string _durationStr = "0:00";
    private double _progressPct;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string TrackTitle
    {
        get => _trackTitle;
        set { _trackTitle = value; Notify(); }
    }
    public string Artist
    {
        get => _artist;
        set { _artist = value; Notify(); }
    }
    public BitmapImage? AlbumArt
    {
        get => _albumArt;
        set { _albumArt = value; Notify(); Notify(nameof(NoArtVisibility)); }
    }
    public Visibility NoArtVisibility => _albumArt == null ? Visibility.Visible : Visibility.Hidden;
    public bool IsPlaying
    {
        get => _isPlaying;
        set { _isPlaying = value; Notify(); Notify(nameof(PlayPauseIcon)); }
    }
    public string PlayPauseIcon => _isPlaying ? "⏸" : "▶";
    public string ElapsedStr
    {
        get => _elapsedStr;
        set { _elapsedStr = value; Notify(); }
    }
    public string DurationStr
    {
        get => _durationStr;
        set { _durationStr = value; Notify(); }
    }
    public double ProgressPct
    {
        get => _progressPct;
        set { _progressPct = value; Notify(); }
    }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += (_, _) => { _session.TickPosition(); SyncTimeline(); };
        _timer.Start();

        // Re-assert topmost every 2 s as a backup
        _topmostTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _topmostTimer.Tick += (_, _) => BringToTopmost();
        _topmostTimer.Start();

        _session.MetadataChanged += () =>
        {
            TrackTitle = _session.TrackTitle;
            Artist     = _session.Artist;
            AlbumArt   = _session.AlbumArt;
            SyncTimeline();
        };
        _session.PlaybackChanged += () => IsPlaying = _session.IsPlaying;
        _session.TimelineChanged += SyncTimeline;
        _session.SessionChanged  += () =>
        {
            TrackTitle = _session.TrackTitle;
            Artist     = _session.Artist;
            AlbumArt   = _session.AlbumArt;
            IsPlaying  = _session.IsPlaying;
            SyncTimeline();
        };

        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (_, _) =>
            Dispatcher.InvokeAsync(() => TaskbarPositioner.PositionWindow(this));
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        LoadSpotifyIcon();
        TaskbarPositioner.PositionWindow(this);

        try
        {
            await _session.InitAsync();
        }
        catch { }

        TrackTitle = _session.TrackTitle;
        Artist     = _session.Artist;
        AlbumArt   = _session.AlbumArt;
        IsPlaying  = _session.IsPlaying;
        SyncTimeline();
    }

    private void LoadSpotifyIcon()
    {
        try
        {
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Spotify", "Spotify.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "Spotify.exe"),
            };

            foreach (var path in candidates)
            {
                if (!File.Exists(path)) continue;
                var ico = System.Drawing.Icon.ExtractAssociatedIcon(path);
                if (ico == null) continue;
                Icon = Imaging.CreateBitmapSourceFromHIcon(
                    ico.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                ico.Dispose();
                return;
            }
        }
        catch { }
    }

    private void SyncTimeline()
    {
        var pos = _session.Position;
        var dur = _session.Duration;
        ElapsedStr  = FormatTime(pos);
        DurationStr = FormatTime(dur);
        ProgressPct = dur.TotalSeconds > 0 ? pos.TotalSeconds / dur.TotalSeconds * 100.0 : 0;
    }

    private static string FormatTime(TimeSpan t) =>
        t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}"
            : $"{(int)t.TotalMinutes}:{t.Seconds:D2}";

    private async void PrevButton_Click(object sender, RoutedEventArgs e)      { try { await _session.PrevAsync();      } catch { } }
    private async void PlayPauseButton_Click(object sender, RoutedEventArgs e) { try { await _session.PlayPauseAsync(); } catch { } }
    private async void NextButton_Click(object sender, RoutedEventArgs e)      { try { await _session.NextAsync();      } catch { } }

    private void HeartButton_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("spotify:") { UseShellExecute = true }); } catch { }
    }

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void Window_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var menu = new ContextMenu();

        void Add(string header, Action action)
        {
            var item = new MenuItem { Header = header };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }

        Add("Open Spotify",    () => { try { Process.Start(new ProcessStartInfo("spotify:") { UseShellExecute = true }); } catch { } });
        Add("Snap to taskbar", () => TaskbarPositioner.PositionWindow(this));
        menu.Items.Add(new Separator());
        Add("Exit",            () => Application.Current.Shutdown());

        menu.PlacementTarget = this;
        menu.IsOpen = true;
        e.Handled   = true;
    }

    // Keep widget above taskbar at all times
    private const int WM_WINDOWPOSCHANGING = 0x0046;
    private static readonly IntPtr HWND_TOPMOST = new(-1);

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPOS
    {
        public IntPtr hwnd, hwndInsertAfter;
        public int x, y, cx, cy;
        public uint flags;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        source?.AddHook(WndProc);

        // Hook foreground-change events so taskbar click brings us forward
        _winEventDelegate = OnWinEvent;
        _winEventHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
            IntPtr.Zero, _winEventDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_winEventHook != IntPtr.Zero)
            UnhookWinEvent(_winEventHook);
        base.OnClosed(e);
    }

    private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        // When the taskbar (or any of its children) gains foreground → push widget back on top
        var taskbar = FindWindow("Shell_TrayWnd", null);
        if (hwnd == taskbar || IsChildOfTaskbar(hwnd, taskbar))
            Dispatcher.BeginInvoke(BringToTopmost);
    }

    private static bool IsChildOfTaskbar(IntPtr hwnd, IntPtr taskbar)
    {
        var p = hwnd;
        for (int i = 0; i < 5 && p != IntPtr.Zero; i++)
        {
            p = GetParent(p);
            if (p == taskbar) return true;
        }
        return false;
    }

    private void BringToTopmost()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
        catch { }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_WINDOWPOSCHANGING)
        {
            var pos = Marshal.PtrToStructure<WINDOWPOS>(lParam);
            pos.hwndInsertAfter = HWND_TOPMOST;
            Marshal.StructureToPtr(pos, lParam, false);
        }
        return IntPtr.Zero;
    }

    private void Notify([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
