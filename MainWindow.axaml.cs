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

public partial class MainWindow : Window, IWallpaperDisplay
{
    private LibVLC? _libVLC;
    private MediaPlayer? _mediaPlayer;
    private bool _isOptimizationPaused = false;
    private uint _ownProcessId;
    private WallpaperConfig _config = new();
    
    // Multi-threading
    private CancellationTokenSource? _optimizationCts;
    private Task? _optimizationTask;
    
    private int _screenWidth;
    private int _screenHeight;
    private ProceduralRenderer? _proceduralRenderer;
    private ClockOverlayWindow? _clockHUD;
    private WallpaperManager? _wallpaperManager;

    // IWallpaperDisplay implementation
    Border IWallpaperDisplay.ColorLayer => ColorLayer;
    Image IWallpaperDisplay.ImageLayer => ImageLayer;
    Control IWallpaperDisplay.VideoLayer => VideoLayer;
    ProceduralCanvas IWallpaperDisplay.ProceduralLayer => ProceduralLayer;
    Grid IWallpaperDisplay.ClockBackdropLayer => ClockBackdropLayer;
    Image IWallpaperDisplay.ClockBackground => ClockBackground;
    Panel IWallpaperDisplay.FallbackText => FallbackText;
    int IWallpaperDisplay.DisplayWidth => _screenWidth;
    int IWallpaperDisplay.DisplayHeight => _screenHeight;

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
            
            // Explicitly set logical window size to match monitor bounds
            // This prevents Avalonia from centering layout in a smaller logical box
            Width = _screenWidth;
            Height = _screenHeight;
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
        _libVLC = new LibVLC("--file-caching=500", "--quiet", "--no-video-title-show");
        _mediaPlayer = new MediaPlayer(_libVLC);
        VideoLayer.MediaPlayer = _mediaPlayer;

        _clockHUD = new ClockOverlayWindow();
        var hudHandle = TryGetPlatformHandle();
        if (hudHandle != null) _clockHUD.SetParentHandle(hudHandle.Handle);
        _clockHUD.Show();

        // Initialize Manager
        _wallpaperManager = new WallpaperManager(this, _libVLC, _mediaPlayer, _proceduralRenderer, _clockHUD);

        // Background Optimization logic handled by VideoOptimizer
        StartBackgroundOptimization();
        LoadConfig();
    }

    private void OnPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            var preset = _config.Library.FirstOrDefault(p => p.Id == _config.CurrentPresetId);
            EditClockMenuItem.IsVisible = (preset?.Type == WallpaperType.Clock);
            MainContextMenu.Open(this);
        }
    }

    private void OnEditClockClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var preset = _config.Library.FirstOrDefault(p => p.Id == _config.CurrentPresetId);
        if (preset?.Type == WallpaperType.Clock)
        {
            var editWin = new ClockEditWindow(preset);
            editWin.PreviewChanged += (p) => OnWallpaperChanged(_config);
            editWin.Saved += () => LoadConfig();
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

                    var placement = new Win32Api.WINDOWPLACEMENT { length = Marshal.SizeOf<Win32Api.WINDOWPLACEMENT>() };
                    Win32Api.GetWindowPlacement(hwnd, ref placement);
                    
                    bool isMaximized = (placement.showCmd == Win32Api.SW_SHOWMAXIMIZED);
                    bool isFullSize = false;

                    if (Win32Api.GetWindowRect(hwnd, out Win32Api.RECT rect))
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
                    await Dispatcher.UIThread.InvokeAsync(() => {
                        _wallpaperManager?.PausePlayback();
                        _isOptimizationPaused = true;
                    });
                }
                else if (!anyCovering && _isOptimizationPaused)
                {
                    await Dispatcher.UIThread.InvokeAsync(async () => {
                        _isOptimizationPaused = false;
                        await Task.Delay(100);
                        OnWallpaperChanged(_config);
                    });
                }

                await Task.Delay(1000, token);
            }
        }, token);
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
        _wallpaperManager?.OnWallpaperChanged(config);
    }

    private void ApplyAdvancedStyles(IntPtr hwnd)
    {
        IntPtr exStyle = Win32Api.GetWindowLongPtr(hwnd, Win32Api.GWL_EXSTYLE);
        long newExStyle = exStyle.ToInt64() | Win32Api.WS_EX_TOOLWINDOW | Win32Api.WS_EX_NOACTIVATE;
        Win32Api.SetWindowLongPtr(hwnd, Win32Api.GWL_EXSTYLE, new IntPtr(newExStyle));
    }

    private void ResizeToParent(IntPtr hwnd)
    {
        IntPtr desktopHwnd = Win32Api.FindWindow("Progman", string.Empty);
        if (Win32Api.GetClientRect(desktopHwnd, out Win32Api.RECT rect))
        {
            Win32Api.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, rect.Right - rect.Left, rect.Bottom - rect.Top, Win32Api.SWP_NOACTIVATE);
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        VideoOptimizer.CancelAll();
        _optimizationCts?.Cancel();
        _wallpaperManager?.Dispose();
        _clockHUD?.Close();
        base.OnClosing(e);
    }
}