using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using System.Runtime.InteropServices;
using LibVLCSharp.Shared;

namespace openwalls;

public class ProceduralCanvas : Control
{
    public ProceduralRenderer? Renderer { get; set; }
    public override void Render(DrawingContext context)
    {
        Renderer?.Render(context, Bounds.Size);
        base.Render(context);
    }
}

public partial class MainWindow : Window
{
    private LibVLC? _libVLC;
    private MediaPlayer? _mediaPlayer;
    private bool _isOptimizationPaused = false;
    private uint _ownProcessId;
    private WallpaperConfig _config = new();
    
    // Multi-threading
    private CancellationTokenSource? _optimizationCts;
    private Task? _optimizationTask;
    
    // Zen Clock Timer
    private DispatcherTimer? _clockTimer;
    
    private int _screenWidth;
    private int _screenHeight;
    private ProceduralRenderer? _proceduralRenderer;

    public MainWindow()
    {
        InitializeComponent();
        SettingsWindow.WallpaperChanged += OnWallpaperChanged;
        _ownProcessId = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (Screens.Primary != null)
        {
            _screenWidth = Screens.Primary.Bounds.Width;
            _screenHeight = Screens.Primary.Bounds.Height;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var handle = TryGetPlatformHandle();
            if (handle != null)
            {
                IntPtr hwnd = handle.Handle;
                ApplyAdvancedStyles(hwnd);
                WallpaperUtils.AttachToDesktop(hwnd);
                ResizeToParent(hwnd);
                Win32Api.SetWindowPos(hwnd, Win32Api.HWND_BOTTOM, 0, 0, 0, 0, 
                    Win32Api.SWP_NOMOVE | Win32Api.SWP_NOSIZE | Win32Api.SWP_NOACTIVATE);
                Win32Api.ShowWindow(hwnd, Win32Api.SW_SHOW);
            }
        }

        _proceduralRenderer = new ProceduralRenderer(ProceduralLayer);
        ProceduralLayer.Renderer = _proceduralRenderer;

        Core.Initialize();
        _libVLC = new LibVLC();
        _mediaPlayer = new MediaPlayer(_libVLC);
        VideoLayer.MediaPlayer = _mediaPlayer;

        // Zen Clock Timer (1s updates)
        _clockTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, UpdateClock);
        _clockTimer.Start();

        StartBackgroundOptimization();
        LoadConfig();
    }

    private void UpdateClock(object? sender, EventArgs e)
    {
        if (ClockLayer.IsVisible)
        {
            var now = DateTime.Now;
            ClockTime.Text = now.ToString("h:mm");
            ClockDate.Text = now.ToString("dddd, MMMM dd").ToUpper();
        }
    }

    private void StartBackgroundOptimization()
    {
        _optimizationCts = new CancellationTokenSource();
        var token = _optimizationCts.Token;

        _optimizationTask = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                bool anyCovering = false;
                string triggerInfo = "";

                Win32Api.EnumWindows((hwnd, lParam) =>
                {
                    if (!Win32Api.IsWindowVisible(hwnd) || Win32Api.IsIconic(hwnd)) return true;
                    Win32Api.GetWindowThreadProcessId(hwnd, out uint pid);
                    if (pid == _ownProcessId) return true;

                    StringBuilder titleBuilder = new StringBuilder(256);
                    Win32Api.GetWindowText(hwnd, titleBuilder, titleBuilder.Capacity);
                    string title = titleBuilder.ToString().Trim();
                    if (string.IsNullOrWhiteSpace(title) || title.Equals("Settings", StringComparison.OrdinalIgnoreCase)) return true;

                    long exStyle = Win32Api.GetWindowLongPtr(hwnd, Win32Api.GWL_EXSTYLE).ToInt64();
                    bool isAppWindow = (exStyle & Win32Api.WS_EX_APPWINDOW) != 0;
                    bool isToolWindow = (exStyle & Win32Api.WS_EX_TOOLWINDOW) != 0;
                    IntPtr owner = Win32Api.GetWindow(hwnd, Win32Api.GW_OWNER);

                    bool isTaskbarWindow = isAppWindow || (!isToolWindow && owner == IntPtr.Zero);
                    if (!isTaskbarWindow) return true;

                    StringBuilder className = new StringBuilder(256);
                    Win32Api.GetClassName(hwnd, className, className.Capacity);
                    string cls = className.ToString();
                    if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Windows.UI.Core.CoreWindow") return true;

                    var placement = new Win32Api.WINDOWPLACEMENT();
                    placement.length = Marshal.SizeOf<Win32Api.WINDOWPLACEMENT>();
                    Win32Api.GetWindowPlacement(hwnd, ref placement);
                    
                    bool isMaximized = (placement.showCmd == Win32Api.SW_SHOWMAXIMIZED);
                    bool isFullSize = false;

                    Win32Api.RECT rect;
                    if (Win32Api.GetWindowRect(hwnd, out rect))
                    {
                        int w = rect.Right - rect.Left;
                        int h = rect.Bottom - rect.Top;
                        if (w >= _screenWidth - 10 && h >= _screenHeight - 80) isFullSize = true;
                    }

                    if (isMaximized || isFullSize)
                    {
                        anyCovering = true;
                        triggerInfo = $"Title: {title}, State: {(isMaximized ? "Maximized" : "Snapped")}";
                        return false; 
                    }

                    return true;
                }, IntPtr.Zero);

                if (anyCovering && !_isOptimizationPaused)
                {
                    await Dispatcher.UIThread.InvokeAsync(() => PausePlayback(triggerInfo));
                }
                else if (!anyCovering && _isOptimizationPaused)
                {
                    await Dispatcher.UIThread.InvokeAsync(() => ResumePlayback());
                }

                await Task.Delay(1000, token);
            }
        }, token);
    }

    private void PausePlayback(string info)
    {
        if (_mediaPlayer?.IsPlaying == true) _mediaPlayer.Pause();
        _proceduralRenderer?.Stop(); 
        _isOptimizationPaused = true;
    }

    private void ResumePlayback()
    {
        _isOptimizationPaused = false;
        if (_mediaPlayer != null && _mediaPlayer.Media != null) _mediaPlayer.Play();
        
        if (_config.CurrentPresetId != null)
        {
            var preset = _config.Library.FirstOrDefault(p => p.Id == _config.CurrentPresetId);
            if (preset?.Type == WallpaperType.Procedural)
            {
                _proceduralRenderer?.Start(preset);
            }
        }
    }

    private void LoadConfig()
    {
        if (File.Exists(WallpaperConfig.ConfigPath))
        {
            try
            {
                string json = File.ReadAllText(WallpaperConfig.ConfigPath);
                _config = Newtonsoft.Json.JsonConvert.DeserializeObject<WallpaperConfig>(json) ?? new WallpaperConfig();
                if (_config.CurrentPresetId == null && _config.Library.Any()) _config.CurrentPresetId = _config.Library.First().Id;
                OnWallpaperChanged(_config);
            }
            catch { }
        }
        else
        {
            _config = new WallpaperConfig();
            _config.CurrentPresetId = _config.Library.First().Id;
            OnWallpaperChanged(_config);
        }
    }

    private void OnWallpaperChanged(WallpaperConfig config)
    {
        _config = config;
        var preset = config.Library.FirstOrDefault(p => p.Id == config.CurrentPresetId);
        
        ColorLayer.IsVisible = false;
        ImageLayer.IsVisible = false;
        VideoLayer.IsVisible = false;
        ProceduralLayer.IsVisible = false;
        ClockLayer.IsVisible = false;
        FallbackText.IsVisible = false;
        ImageLayer.Opacity = 0;
        VideoLayer.Opacity = 0;
        
        if (_mediaPlayer?.IsPlaying == true) _mediaPlayer.Stop();
        _proceduralRenderer?.Stop();
        _isOptimizationPaused = false;

        if (preset == null) { FallbackText.IsVisible = true; return; }

        switch (preset.Type)
        {
            case WallpaperType.Color:
                if (!string.IsNullOrEmpty(preset.Color)) { ColorLayer.Background = SolidColorBrush.Parse(preset.Color); ColorLayer.IsVisible = true; }
                break;
            case WallpaperType.Image:
                var imgPath = preset.GetResourcePath(preset.Path);
                if (!string.IsNullOrEmpty(imgPath) && File.Exists(imgPath)) { ImageLayer.Source = new Bitmap(imgPath); ImageLayer.IsVisible = true; ImageLayer.Opacity = 1; }
                break;
            case WallpaperType.Video:
                var videoPath = preset.GetResourcePath(preset.Path);
                if (!string.IsNullOrEmpty(videoPath) && File.Exists(videoPath))
                {
                    using var media = new Media(_libVLC!, videoPath, FromType.FromPath);
                    media.AddOption(":input-repeat=65535");
                    if (preset.IsMuted) _mediaPlayer!.Mute = true;
                    VideoLayer.IsVisible = true;
                    VideoLayer.Opacity = 1;
                    _mediaPlayer!.Play(media);
                }
                break;
            case WallpaperType.Procedural:
                ProceduralLayer.IsVisible = true; 
                _proceduralRenderer?.Start(preset); 
                break;
            case WallpaperType.Clock:
                var relativeClockPath = preset.ClockImagePath ?? "assets/samurai-warrior-observing-village-moonlight.jpg";
                var clockPath = preset.GetResourcePath(relativeClockPath);
                
                if (File.Exists(clockPath)) 
                {
                    ClockBackground.Source = new Bitmap(clockPath);
                }
                else 
                {
                    // Fallback to absolute check or assets folder
                    var fallbackPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativeClockPath);
                    if (File.Exists(fallbackPath)) 
                    {
                        ClockBackground.Source = new Bitmap(fallbackPath);
                    }
                    else if (File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets/samurai-warrior-observing-village-moonlight.jpg")))
                    {
                        ClockBackground.Source = new Bitmap(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets/samurai-warrior-observing-village-moonlight.jpg"));
                    }
                }
                ClockLayer.IsVisible = true;
                UpdateClock(null, EventArgs.Empty);
                break;
            default:
                FallbackText.IsVisible = true;
                break;
        }
    }

    private void ApplyAdvancedStyles(IntPtr hwnd)
    {
        IntPtr exStyle = Win32Api.GetWindowLongPtr(hwnd, Win32Api.GWL_EXSTYLE);
        long newExStyle = exStyle.ToInt64() | Win32Api.WS_EX_TOOLWINDOW | Win32Api.WS_EX_NOACTIVATE;
        Win32Api.SetWindowLongPtr(hwnd, Win32Api.GWL_EXSTYLE, new IntPtr(newExStyle));
    }

    private void ResizeToParent(IntPtr hwnd)
    {
        Win32Api.RECT rect;
        IntPtr desktopHwnd = Win32Api.FindWindow("Progman", null);
        if (Win32Api.GetClientRect(desktopHwnd, out rect))
        {
            Win32Api.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, rect.Right - rect.Left, rect.Bottom - rect.Top, Win32Api.SWP_NOACTIVATE);
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _optimizationCts?.Cancel();
        _proceduralRenderer?.Stop();
        _clockTimer?.Stop();
        _mediaPlayer?.Dispose();
        _libVLC?.Dispose();
        base.OnClosing(e);
    }
}