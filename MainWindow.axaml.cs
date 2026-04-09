using System;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using System.Runtime.InteropServices;
using LibVLCSharp.Shared;

namespace openwalls;

public partial class MainWindow : Window
{
    private LibVLC? _libVLC;
    private MediaPlayer? _mediaPlayer;
    private DispatcherTimer? _optimizationTimer;
    private bool _isOptimizationPaused = false;
    private uint _ownProcessId;
    
    private int _screenWidth;
    private int _screenHeight;

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

        // Initialize LibVLC
        Core.Initialize();
        _libVLC = new LibVLC();
        _mediaPlayer = new MediaPlayer(_libVLC);
        VideoLayer.MediaPlayer = _mediaPlayer;

        // Setup Optimization Timer
        _optimizationTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, CheckForMaximization);
        _optimizationTimer.Start();

        // Load initial config
        LoadConfig();
    }

    private void CheckForMaximization(object? sender, EventArgs e)
    {
        bool anyCovering = false;
        string triggerInfo = "";
        
        Win32Api.EnumWindows((hwnd, lParam) =>
        {
            // 1. Core Visibility: Must be visible and NOT minimized
            if (!Win32Api.IsWindowVisible(hwnd) || Win32Api.IsIconic(hwnd)) return true;

            // 2. Ignore our own process
            Win32Api.GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == _ownProcessId) return true;

            // 3. NUCLEAR TITLE BLACKLIST
            StringBuilder titleBuilder = new StringBuilder(256);
            Win32Api.GetWindowText(hwnd, titleBuilder, titleBuilder.Capacity);
            string title = titleBuilder.ToString().Trim();

            // Ignore empty titles, whitespace titles, and the specific "Settings" culprit
            if (string.IsNullOrWhiteSpace(title) || title.Equals("Settings", StringComparison.OrdinalIgnoreCase)) 
                return true;

            // 4. Taskbar Visibility Heuristic
            long exStyle = Win32Api.GetWindowLongPtr(hwnd, Win32Api.GWL_EXSTYLE).ToInt64();
            bool isAppWindow = (exStyle & Win32Api.WS_EX_APPWINDOW) != 0;
            bool isToolWindow = (exStyle & Win32Api.WS_EX_TOOLWINDOW) != 0;
            IntPtr owner = Win32Api.GetWindow(hwnd, Win32Api.GW_OWNER);

            bool isTaskbarWindow = isAppWindow || (!isToolWindow && owner == IntPtr.Zero);
            if (!isTaskbarWindow) return true;

            // 5. Class Filter: Ignore system layers and Input Experience
            StringBuilder className = new StringBuilder(256);
            Win32Api.GetClassName(hwnd, className, className.Capacity);
            string cls = className.ToString();
            if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Windows.UI.Core.CoreWindow") return true;

            // 6. Covering Check: Is it maximized or full-screen?
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
            if (_mediaPlayer?.IsPlaying == true)
            {
                _mediaPlayer.Pause();
                _isOptimizationPaused = true;
                Console.WriteLine($"[OPTIMIZATION] Pausing for: {triggerInfo}");
            }
        }
        else if (!anyCovering && _isOptimizationPaused)
        {
            _mediaPlayer?.Play();
            _isOptimizationPaused = false;
            Console.WriteLine("[OPTIMIZATION] Resuming (Desktop visible)");
        }
    }

    private void LoadConfig()
    {
        if (File.Exists(WallpaperConfig.ConfigPath))
        {
            try
            {
                string json = File.ReadAllText(WallpaperConfig.ConfigPath);
                var config = Newtonsoft.Json.JsonConvert.DeserializeObject<WallpaperConfig>(json);
                if (config != null) OnWallpaperChanged(config);
            }
            catch { /* Ignore invalid config */ }
        }
    }

    private void OnWallpaperChanged(WallpaperConfig config)
    {
        var preset = config.Library.FirstOrDefault(p => p.Id == config.CurrentPresetId);
        
        // Reset layers
        ColorLayer.IsVisible = false;
        ImageLayer.IsVisible = false;
        VideoLayer.IsVisible = false;
        FallbackText.IsVisible = false;
        ImageLayer.Opacity = 0;
        VideoLayer.Opacity = 0;
        
        if (_mediaPlayer?.IsPlaying == true) _mediaPlayer.Stop();
        _isOptimizationPaused = false;

        if (preset == null)
        {
            FallbackText.IsVisible = true;
            return;
        }

        switch (preset.Type)
        {
            case WallpaperType.Color:
                if (!string.IsNullOrEmpty(preset.Color))
                {
                    ColorLayer.Background = SolidColorBrush.Parse(preset.Color);
                    ColorLayer.IsVisible = true;
                }
                else FallbackText.IsVisible = true;
                break;

            case WallpaperType.Image:
                if (!string.IsNullOrEmpty(preset.Path) && File.Exists(preset.Path))
                {
                    try
                    {
                        ImageLayer.Source = new Bitmap(preset.Path);
                        ImageLayer.IsVisible = true;
                        ImageLayer.Opacity = 1;
                    }
                    catch { FallbackText.IsVisible = true; }
                }
                else FallbackText.IsVisible = true;
                break;

            case WallpaperType.Video:
                if (!string.IsNullOrEmpty(preset.Path) && File.Exists(preset.Path))
                {
                    try
                    {
                        using var media = new Media(_libVLC!, preset.Path, FromType.FromPath);
                        media.AddOption(":input-repeat=65535"); // Loop
                        if (preset.IsMuted) _mediaPlayer!.Mute = true;
                        
                        VideoLayer.IsVisible = true;
                        VideoLayer.Opacity = 1;
                        _mediaPlayer!.Play(media);
                    }
                    catch { FallbackText.IsVisible = true; }
                }
                else FallbackText.IsVisible = true;
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
        _optimizationTimer?.Stop();
        _mediaPlayer?.Dispose();
        _libVLC?.Dispose();
        base.OnClosing(e);
    }
}