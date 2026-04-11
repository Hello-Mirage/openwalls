using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Input;
using Avalonia.Threading;
using System.Globalization;

namespace openwalls;

public partial class SettingsWindow : Window
{
    public static event Action<WallpaperConfig>? WallpaperChanged;
    private readonly LibraryManager _libraryManager = new();

    public SettingsWindow()
    {
        InitializeComponent();
        this.Icon = IconUtils.LoadSvgIcon();
        _libraryManager.LoadLibrary();
        LibraryItemsControl.ItemsSource = _libraryManager.Library;
    }

    private void NavList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (LibraryView == null || MarketplaceView == null) return;
        LibraryView.IsVisible = NavList.SelectedIndex == 0;
        MarketplaceView.IsVisible = NavList.SelectedIndex == 1;
    }

    private void AddColor_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string hex)
        {
            _libraryManager.AddColor(hex);
            RefreshLibrary();
        }
    }

    private async void AddNew_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Add New Wallpaper (Plug and Play)",
            AllowMultiple = true,
            FileTypeFilter = new[] { new FilePickerFileType("Media Files") { Patterns = new[] { "*.jpg", "*.png", "*.jpeg", "*.mp4", "*.mkv", "*.mov", "*.avi" } } }
        });

        foreach (var file in files) _libraryManager.AddFile(file.Path.LocalPath);
        RefreshLibrary();
    }

    private void Preset_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is WallpaperPreset preset)
        {
            _libraryManager.Config.CurrentPresetId = preset.Id;
            _libraryManager.RefreshActiveState();
            _libraryManager.SaveConfig();
            WallpaperChanged?.Invoke(_libraryManager.Config);
        }
    }

    private void EditClockSettings_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.DataContext is WallpaperPreset preset && preset.Type == WallpaperType.Clock)
        {
            var editWin = new ClockEditWindow(preset);
            editWin.PreviewChanged += (p) => WallpaperChanged?.Invoke(_libraryManager.Config);
            editWin.Saved += () => RefreshLibrary();
            editWin.Show();
        }
    }

    private async void SetThumbnail_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.DataContext is WallpaperPreset preset)
        {
            var topLevel = GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = $"Set Thumbnail for {preset.Name}",
                FileTypeFilter = new[] { FilePickerFileTypes.ImageAll }
            });

            if (files.Any())
            {
                var sourcePath = files.First().Path.LocalPath;
                var ext = Path.GetExtension(sourcePath);
                var destPath = Path.Combine(preset.BaseDirectory!, "thumbnail" + ext);

                File.Copy(sourcePath, destPath, true);
                preset.ThumbnailPath = "thumbnail" + ext;
                File.WriteAllText(Path.Combine(preset.BaseDirectory!, "wallpaper.json"), JsonConvert.SerializeObject(preset, Formatting.Indented));
                
                ThumbnailConverter.ClearCache(destPath);
                RefreshLibrary();
            }
        }
    }

    private void DeletePreset_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.DataContext is WallpaperPreset preset)
        {
            _libraryManager.DeletePreset(preset);
            RefreshLibrary();
        }
    }

    private void RefreshLibrary()
    {
        _libraryManager.LoadLibrary();
        LibraryItemsControl.ItemsSource = _libraryManager.Library;
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        ThumbnailConverter.ClearAllCache();
        base.OnClosing(e);
    }

    private void DragStrip_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) this.BeginMoveDrag(e);
    }
}
