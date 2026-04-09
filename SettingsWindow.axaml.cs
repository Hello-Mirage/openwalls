using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace openwalls;

public partial class SettingsWindow : Window
{
    public static event Action<WallpaperConfig>? WallpaperChanged;
    
    private WallpaperConfig _config = new();
    private ObservableCollection<WallpaperPreset> _library = new();

    public SettingsWindow()
    {
        InitializeComponent();
        LoadLibrary();
    }

    private void LoadLibrary()
    {
        if (File.Exists(WallpaperConfig.ConfigPath))
        {
            try
            {
                string json = File.ReadAllText(WallpaperConfig.ConfigPath);
                _config = JsonConvert.DeserializeObject<WallpaperConfig>(json) ?? new WallpaperConfig();
            }
            catch { _config = new WallpaperConfig(); }
        }

        _library = new ObservableCollection<WallpaperPreset>(_config.Library);
        
        // Sync Initial Active State
        foreach (var p in _library) p.IsActive = (p.Id == _config.CurrentPresetId);
        
        LibraryItemsControl.ItemsSource = _library;
    }

    private void AddColor_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string hex)
        {
            var preset = new WallpaperPreset
            {
                Name = $"Color {hex}",
                Color = hex,
                Type = WallpaperType.Color
            };

            _library.Add(preset);
            _config.Library.Add(preset);
            SaveConfig();
        }
    }

    private async void AddNew_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Add New Wallpaper to Library",
            AllowMultiple = true,
            FileTypeFilter = new[] 
            { 
                new FilePickerFileType("Media Files") { Patterns = new[] { "*.jpg", "*.png", "*.jpeg", "*.mp4", "*.mkv", "*.mov", "*.avi" } } 
            }
        });

        foreach (var file in files)
        {
            var path = file.Path.LocalPath;
            var ext = Path.GetExtension(path).ToLower();
            var type = (ext == ".mp4" || ext == ".mkv" || ext == ".mov" || ext == ".avi") 
                ? WallpaperType.Video : WallpaperType.Image;

            var preset = new WallpaperPreset
            {
                Name = Path.GetFileNameWithoutExtension(path),
                Path = path,
                Type = type
            };

            _library.Add(preset);
            _config.Library.Add(preset);
        }

        SaveConfig();
    }

    private void Preset_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is WallpaperPreset preset)
        {
            _config.CurrentPresetId = preset.Id;
            
            // Update Active States
            foreach (var p in _library) p.IsActive = (p.Id == preset.Id);
            
            SaveConfig();
            WallpaperChanged?.Invoke(_config);
        }
    }

    private void SaveConfig()
    {
        try
        {
            string dir = Path.GetDirectoryName(WallpaperConfig.ConfigPath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            
            string json = JsonConvert.SerializeObject(_config, Formatting.Indented);
            File.WriteAllText(WallpaperConfig.ConfigPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save config: {ex.Message}");
        }
    }
}

public class ActiveColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        bool isActive = value is bool b && b;
        return isActive ? Brush.Parse("White") : Brush.Parse("Transparent");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
}
