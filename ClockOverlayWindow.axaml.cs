using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using System.Runtime.InteropServices;

namespace openwalls;

public partial class ClockOverlayWindow : Window
{
    private DispatcherTimer? _timer;
    private WallpaperConfig? _currentConfig;

    private IntPtr _parentHandle = IntPtr.Zero;

    public ClockOverlayWindow()
    {
        InitializeComponent();
    }

    public void SetParentHandle(IntPtr parent) => _parentHandle = parent;

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var handle = TryGetPlatformHandle();
            if (handle != null)
            {
                IntPtr hwnd = handle.Handle;
                
                // 1. Set as ToolWindow and Transparent
                IntPtr exStyle = Win32Api.GetWindowLongPtr(hwnd, Win32Api.GWL_EXSTYLE);
                long newExStyle = exStyle.ToInt64() | Win32Api.WS_EX_TOOLWINDOW | Win32Api.WS_EX_NOACTIVATE | Win32Api.WS_EX_TRANSPARENT;
                Win32Api.SetWindowLongPtr(hwnd, Win32Api.GWL_EXSTYLE, new IntPtr(newExStyle));

                // 2. Attach to Parent or Desktop
                if (_parentHandle != IntPtr.Zero)
                {
                    Win32Api.SetParent(hwnd, _parentHandle);
                    // Match parent size
                    Win32Api.RECT rect;
                    if (Win32Api.GetClientRect(_parentHandle, out rect))
                    {
                        Win32Api.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, rect.Right - rect.Left, rect.Bottom - rect.Top, Win32Api.SWP_NOACTIVATE);
                    }
                }
                else
                {
                    WallpaperUtils.AttachToDesktop(hwnd);
                }
            }
        }

        _timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, UpdateClock);
        _timer.Start();
    }

    public void UpdateConfiguration(WallpaperConfig config)
    {
        _currentConfig = config;
        var preset = config.Library.FirstOrDefault(p => p.Id == config.CurrentPresetId);
        
        IsVisible = (preset?.Type == WallpaperType.Clock);
        if (IsVisible) UpdateClock(null, EventArgs.Empty);
    }

    private void UpdateClock(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        ClockTime.Text = now.ToString("h:mm");
        ClockDate.Text = now.ToString("dddd, MMMM dd").ToUpper();

        if (_currentConfig != null)
        {
            var preset = _currentConfig.Library.FirstOrDefault(p => p.Id == _currentConfig.CurrentPresetId);
            if (preset != null && preset.Type == WallpaperType.Clock)
            {
                ClockTime.FontSize = preset.ClockFontSize;
                ClockTime.Foreground = SolidColorBrush.Parse(preset.ClockFontColor);
                ClockDate.Foreground = SolidColorBrush.Parse(preset.ClockFontColor);
                
                var tt = (TranslateTransform)ClockContainer.RenderTransform!;
                tt.X = preset.ClockHorizontalOffset;
                tt.Y = preset.ClockVerticalOffset;
            }
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _timer?.Stop();
        base.OnClosing(e);
    }
}
