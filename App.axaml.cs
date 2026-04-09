using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Svg.Skia;
using System.Collections.Generic;

namespace openwalls;

public partial class App : Application
{
    private SettingsWindow? _settingsWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            desktop.MainWindow = mainWindow;
            
            // Initialize Tray Icon
            var trayIcon = new TrayIcon
            {
                ToolTipText = "Openwalls",
                Menu = new NativeMenu
                {
                    Items =
                    {
                        new NativeMenuItem("Settings")
                        {
                            Header = "Wallpaper Settings",
                            Command = new TraySettingsCommand(this)
                        },
                        new NativeMenuItemSeparator(),
                        new NativeMenuItem("Exit")
                        {
                            Header = "Exit Openwalls",
                            Command = new TrayExitCommand(desktop)
                        }
                    }
                }
            };

            // Load logo to Tray Icon via IconUtils
            var icon = IconUtils.LoadSvgIcon();
            if (icon != null) trayIcon.Icon = icon;

            var trayIcons = new TrayIcons { trayIcon };
            TrayIcon.SetIcons(this, trayIcons);
        }

        base.OnFrameworkInitializationCompleted();
    }

    public void ShowSettings()
    {
        if (_settingsWindow == null)
        {
            _settingsWindow = new SettingsWindow();
            _settingsWindow.Closed += (s, e) => _settingsWindow = null;
            _settingsWindow.Show();
        }
        else
        {
            _settingsWindow.Activate();
        }
    }
}

public class TraySettingsCommand : System.Windows.Input.ICommand
{
    private readonly App _app;
    public TraySettingsCommand(App app) => _app = app;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _app.ShowSettings();
    public event EventHandler? CanExecuteChanged;
}

public class TrayExitCommand : System.Windows.Input.ICommand
{
    private readonly IClassicDesktopStyleApplicationLifetime _lifetime;
    public TrayExitCommand(IClassicDesktopStyleApplicationLifetime lifetime) => _lifetime = lifetime;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _lifetime.Shutdown();
    public event EventHandler? CanExecuteChanged;
}