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
using System.Diagnostics;
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

    
    private int _screenWidth;
    private int _screenHeight;
    private ProceduralRenderer? _proceduralRenderer;
    private ClockOverlayWindow? _clockHUD;
    private Bitmap? _backgroundBitmap;
    private Bitmap? _clockBackgroundBitmap;

    public MainWindow()
    {
        InitializeComponent();
        Icon = IconUtils.LoadSvgIcon();
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
        _libVLC = new LibVLC(
            "--file-caching=500", 
            "--quiet", 
            "--no-video-title-show"
        );
        _mediaPlayer = new MediaPlayer(_libVLC);
        VideoLayer.MediaPlayer = _mediaPlayer;

        // Initialize HUD Overlay
        _clockHUD = new ClockOverlayWindow();
        
        var hudHandle = TryGetPlatformHandle();
        if (hudHandle != null) _clockHUD.SetParentHandle(hudHandle.Handle);
        
        _clockHUD.Show();

        // 1. Subscribe to Optimization Progress
        VideoOptimizer.OptimizationStarted += path => Dispatcher.UIThread.Post(() => {
            if (OptimizationStatusHUD != null) OptimizationStatusHUD.IsVisible = true;
            if (OptimizationProgressBar != null) OptimizationProgressBar.Value = 0;
            if (OptimizationProgressPercent != null) OptimizationProgressPercent.Text = "0%";
        });

        VideoOptimizer.ProgressUpdated += (path, percent) => Dispatcher.UIThread.Post(() => {
            if (OptimizationProgressBar != null) OptimizationProgressBar.Value = percent;
            if (OptimizationProgressPercent != null) OptimizationProgressPercent.Text = $"{(int)percent}%";
        });

        VideoOptimizer.OptimizationFinished += (path, success) => Dispatcher.UIThread.Post(() => {
            if (OptimizationStatusHUD != null) OptimizationStatusHUD.IsVisible = false;
            // Refresh if the current video was just optimized
            if (success && _config.CurrentPresetId != null)
            {
                var preset = _config.Library.FirstOrDefault(p => p.Id == _config.CurrentPresetId);
                var currentPath = preset?.GetResourcePath(preset.Path);
                if (currentPath == path) OnWallpaperChanged(_config);
            }
        });

        // 2. Start Services
        StartBackgroundOptimization();
        LoadConfig();
    }

    private void OnPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            // Only show edit menu if clock is active
            var preset = _config.Library.FirstOrDefault(p => p.Id == _config.CurrentPresetId);
            EditClockMenuItem.IsVisible = (preset?.Type == WallpaperType.Clock);
            MainContextMenu.Open(this);
        }
    }

    private void OnEditClockClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var preset = _config.Library.FirstOrDefault(p => p.Id == _config.CurrentPresetId);
        if (preset != null && preset.Type == WallpaperType.Clock)
        {
            var editWin = new ClockEditWindow(preset);
            editWin.PreviewChanged += (p) => OnWallpaperChanged(_config); // Live preview by refreshing config/preset
            editWin.Saved += () => LoadConfig(); // Persistence
            editWin.Show();
        }
    }

    private void OnExitClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close();



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
        // Aggressive: Stop entirely to release video buffers and Roslyn memory
        if (_mediaPlayer?.IsPlaying == true) _mediaPlayer.Stop();
        _proceduralRenderer?.Stop(); 
        
        _backgroundBitmap?.Dispose();
        _backgroundBitmap = null;
        ImageLayer.Source = null;

        _clockBackgroundBitmap?.Dispose();
        _clockBackgroundBitmap = null;
        ClockBackground.Source = null;

        _isOptimizationPaused = true;
        
        // Immediate clean up
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
    }

    private async void ResumePlayback()
    {
        _isOptimizationPaused = false;
        // Wait a small moment for Windows to finish redrawing before we hammer the GPU
        await Task.Delay(100);
        // Reload everything from config
        OnWallpaperChanged(_config);
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
            if (_config.Library.Any()) _config.CurrentPresetId = _config.Library.First().Id;
            OnWallpaperChanged(_config);
        }
    }

    private void OnWallpaperChanged(WallpaperConfig config)
    {
        _config = config;
        var preset = config.Library.FirstOrDefault(p => p.Id == config.CurrentPresetId);
        
        if (_libVLC == null || _mediaPlayer == null) return;

        ColorLayer.IsVisible = false;
        ImageLayer.IsVisible = false;
        VideoLayer.IsVisible = false;
        ProceduralLayer.IsVisible = false;
        ClockBackdropLayer.IsVisible = false;
        FallbackText.IsVisible = false;
        ImageLayer.Opacity = 0;
        VideoLayer.Opacity = 0;
        
        if (_mediaPlayer != null && (_mediaPlayer.IsPlaying || _mediaPlayer.State == VLCState.Paused)) 
        {
            _mediaPlayer.Stop();
        }
        _proceduralRenderer?.Stop();
        _isOptimizationPaused = false;

        // Dispose old bitmaps to free RAM
        _backgroundBitmap?.Dispose();
        _backgroundBitmap = null;
        ImageLayer.Source = null;

        _clockBackgroundBitmap?.Dispose();
        _clockBackgroundBitmap = null;
        ClockBackground.Source = null;

        // Update HUD
        _clockHUD?.UpdateConfiguration(config);

        if (preset == null) { FallbackText.IsVisible = true; return; }

        switch (preset.Type)
        {
            case WallpaperType.Color:
                {
                    if (!string.IsNullOrEmpty(preset.Color)) { ColorLayer.Background = SolidColorBrush.Parse(preset.Color); ColorLayer.IsVisible = true; }
                    break;
                }
            case WallpaperType.Image:
                {
                    var imgPath = preset.GetResourcePath(preset.Path);
                    if (!string.IsNullOrEmpty(imgPath) && File.Exists(imgPath)) 
                    { 
                        _backgroundBitmap = new Bitmap(imgPath);
                        ImageLayer.Source = _backgroundBitmap;
                        ImageLayer.IsVisible = true; 
                        ImageLayer.Opacity = 1; 
                    }
                    break;
                }
            case WallpaperType.Video:
                {
                    var videoPath = preset.GetResourcePath(preset.Path);
                    if (!string.IsNullOrEmpty(videoPath) && File.Exists(videoPath))
                    {
                        // Check for optimized version
                        var (optimizedPath, isOptimized) = VideoOptimizer.GetOptimizedPath(videoPath);
                        var pathToPlay = isOptimized ? optimizedPath : videoPath;

                        try 
                        {
                            using var media = new Media(_libVLC!, pathToPlay, FromType.FromPath);
                            media.AddOption(":input-repeat=65535");
                            if (preset.IsMuted) _mediaPlayer!.Mute = true;
                            VideoLayer.IsVisible = true;
                            VideoLayer.Opacity = 1;
                            _mediaPlayer!.Play(media);
                        }
                        catch (Exception ex)
                        {
                            // FALLBACK: If optimized file fails (locked or corrupted), play source
                            Debug.WriteLine($"Playback failed for {pathToPlay}: {ex.Message}. Falling back to source.");
                            using var sourceMedia = new Media(_libVLC!, videoPath, FromType.FromPath);
                            sourceMedia.AddOption(":input-repeat=65535");
                            VideoLayer.IsVisible = true;
                            VideoLayer.Opacity = 1;
                            _mediaPlayer!.Play(sourceMedia);
                        }

                        // If not optimized, start optimization in background for next time
                        if (!isOptimized) VideoOptimizer.OptimizeAsync(videoPath);
                    }
                    break;
                }
            case WallpaperType.Procedural:
                {
                    ProceduralLayer.IsVisible = true; 
                    _proceduralRenderer?.Start(preset); 
                    break;
                }
            case WallpaperType.Clock:
                {
                    ClockBackground.IsVisible = false;
                    
                    // 1. Handle Backdrop (Image or Video)
                    if (preset.ClockBackdropType == "Video" && !string.IsNullOrEmpty(preset.ClockBackdropPath))
                    {
                        var videoPath = preset.GetResourcePath(preset.ClockBackdropPath);
                        if (File.Exists(videoPath))
                        {
                            var (optimizedPath, isOptimized) = VideoOptimizer.GetOptimizedPath(videoPath);
                            var pathToPlay = isOptimized ? optimizedPath : videoPath;

                            try 
                            {
                                using var media = new Media(_libVLC!, pathToPlay, FromType.FromPath);
                                media.AddOption(":input-repeat=65535");
                                _mediaPlayer!.Mute = true;
                                VideoLayer.IsVisible = true;
                                VideoLayer.Opacity = 1;
                                _mediaPlayer!.Play(media);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Clock backdrop failed for {pathToPlay}: {ex.Message}. Falling back to source.");
                                using var sourceMedia = new Media(_libVLC!, videoPath, FromType.FromPath);
                                sourceMedia.AddOption(":input-repeat=65535");
                                VideoLayer.IsVisible = true;
                                VideoLayer.Opacity = 1;
                                _mediaPlayer!.Play(sourceMedia);
                            }

                            if (!isOptimized) VideoOptimizer.OptimizeAsync(videoPath);
                        }
                    }
                    else
                    {
                        var relativeClockPath = preset.ClockBackdropPath ?? preset.ClockImagePath ?? "assets/samurai-warrior-observing-village-moonlight.jpg";
                        var clockPath = preset.GetResourcePath(relativeClockPath);
                        
                        if (File.Exists(clockPath)) 
                        {
                            _clockBackgroundBitmap = new Bitmap(clockPath);
                            ClockBackground.Source = _clockBackgroundBitmap;
                            ClockBackground.IsVisible = true;
                        }
                    }

                    // 2. Enable Backdrop Layer (Background image for clock)
                    ClockBackdropLayer.IsVisible = true;
                    break;
                }
            default:
                {
                    FallbackText.IsVisible = true;
                    break;
                }
        }

        // Force a collection to clean up unmanaged bitmap memory immediately
        Dispatcher.UIThread.Post(() => {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        }, DispatcherPriority.Background);
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
        VideoOptimizer.CancelAll();
        _optimizationCts?.Cancel();
        _proceduralRenderer?.Stop();
        _mediaPlayer?.Dispose();
        _libVLC?.Dispose();
        _clockHUD?.Close();
        base.OnClosing(e);
    }
}