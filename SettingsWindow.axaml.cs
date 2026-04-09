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

namespace openwalls;

public partial class SettingsWindow : Window
{
    public static event Action<WallpaperConfig>? WallpaperChanged;
    
    private WallpaperConfig _config = new();
    private ObservableCollection<WallpaperPreset> _library = new();

    public SettingsWindow()
    {
        InitializeComponent();
        this.Icon = IconUtils.LoadSvgIcon();
        LoadLibrary();
    }

    private void NavList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (LibraryView == null || MarketplaceView == null) return;
        LibraryView.IsVisible = NavList.SelectedIndex == 0;
        MarketplaceView.IsVisible = NavList.SelectedIndex == 1;
    }

    private void LoadLibrary()
    {
        // 1. Ensure directories exist
        if (!Directory.Exists(WallpaperConfig.BaseDir)) Directory.CreateDirectory(WallpaperConfig.BaseDir);
        if (!Directory.Exists(WallpaperConfig.LibraryDir)) Directory.CreateDirectory(WallpaperConfig.LibraryDir);

        // 2. Load core config (Current active ID, etc)
        if (File.Exists(WallpaperConfig.ConfigPath))
        {
            try
            {
                string json = File.ReadAllText(WallpaperConfig.ConfigPath);
                _config = JsonConvert.DeserializeObject<WallpaperConfig>(json) ?? new WallpaperConfig();
            }
            catch { _config = new WallpaperConfig(); }
        }

        // 3. Scan Modular Library
        var allPresets = new List<WallpaperPreset>();
        var folders = Directory.GetDirectories(WallpaperConfig.LibraryDir);
        foreach (var folder in folders)
        {
            var metaPath = Path.Combine(folder, "wallpaper.json");
            if (File.Exists(metaPath))
            {
                try
                {
                    var preset = JsonConvert.DeserializeObject<WallpaperPreset>(File.ReadAllText(metaPath));
                    if (preset != null)
                    {
                        preset.BaseDirectory = folder;
                        allPresets.Add(preset);
                    }
                }
                catch { }
            }
        }

        // 4. Handle Migration/Default injection if library is empty
        if (allPresets.Count == 0)
        {
            InitializeDefaultFolders();
            // Re-scan after initializing defaults
            LoadLibrary(); 
            return;
        }

        _library = new ObservableCollection<WallpaperPreset>(allPresets.OrderByDescending(p => p.DateAdded));
        _config.Library = allPresets;

        foreach (var p in _library) p.IsActive = (p.Id == _config.CurrentPresetId);
        
        LibraryItemsControl.ItemsSource = _library;
    }

    private void InitializeDefaultFolders()
    {
        var defaults = new List<WallpaperPreset>
        {
            new WallpaperPreset { Name = "Zen Clock", Type = WallpaperType.Clock, ClockImagePath = "samurai.jpg" },
            new WallpaperPreset { Name = "Deep Space", Type = WallpaperType.Procedural, ProceduralId = "starfield" },
            new WallpaperPreset { Name = "Matrix Code", Type = WallpaperType.Procedural, ProceduralId = "matrix" },
            new WallpaperPreset { Name = "Neural Swarm", Type = WallpaperType.Procedural, ProceduralId = "swarm" }
        };

        foreach (var d in defaults)
        {
            var folder = Path.Combine(WallpaperConfig.LibraryDir, d.Name.Replace(" ", ""));
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            
            d.BaseDirectory = folder;
            
            // Move asset if it exists in root
            if (d.Type == WallpaperType.Clock)
            {
                var src = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets/samurai-warrior-observing-village-moonlight.jpg");
                if (File.Exists(src)) File.Copy(src, Path.Combine(folder, "samurai.jpg"), true);
            }

            File.WriteAllText(Path.Combine(folder, "wallpaper.json"), JsonConvert.SerializeObject(d, Formatting.Indented));
        }
    }

    private void AddColor_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string hex)
        {
            var preset = new WallpaperPreset { Name = $"Color {hex}", Color = hex, Type = WallpaperType.Color };
            // Save as modular folder
            var folder = Path.Combine(WallpaperConfig.LibraryDir, $"Color_{hex.Replace("#", "")}");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "wallpaper.json"), JsonConvert.SerializeObject(preset, Formatting.Indented));
            LoadLibrary();
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

        foreach (var file in files)
        {
            var path = file.Path.LocalPath;
            var ext = Path.GetExtension(path).ToLower();
            var type = (ext == ".mp4" || ext == ".mkv" || ext == ".mov" || ext == ".avi") ? WallpaperType.Video : WallpaperType.Image;
            
            var name = Path.GetFileNameWithoutExtension(path);
            var folder = Path.Combine(WallpaperConfig.LibraryDir, name.Replace(" ", "_") + "_" + Guid.NewGuid().ToString().Substring(0, 4));
            Directory.CreateDirectory(folder);

            var destFile = "backdrop" + ext;
            File.Copy(path, Path.Combine(folder, destFile), true);

            var preset = new WallpaperPreset { Name = name, Path = destFile, Type = type };
            File.WriteAllText(Path.Combine(folder, "wallpaper.json"), JsonConvert.SerializeObject(preset, Formatting.Indented));
        }
        LoadLibrary();
    }

    private void Preset_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is WallpaperPreset preset)
        {
            _config.CurrentPresetId = preset.Id;
            foreach (var p in _library) p.IsActive = (p.Id == preset.Id);
            SaveConfig();
            WallpaperChanged?.Invoke(_config);
        }
    }

    private async void ChangeClockImage_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.DataContext is WallpaperPreset preset && preset.Type == WallpaperType.Clock)
        {
            var topLevel = GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = $"Choose Backdrop for {preset.Name}",
                FileTypeFilter = new[] { FilePickerFileTypes.ImageAll }
            });

            if (files.Any())
            {
                var src = files.First().Path.LocalPath;
                var ext = Path.GetExtension(src);
                var dest = Path.Combine(preset.BaseDirectory!, "custom_bg" + ext);
                File.Copy(src, dest, true);
                
                preset.ClockImagePath = "custom_bg" + ext;
                File.WriteAllText(Path.Combine(preset.BaseDirectory!, "wallpaper.json"), JsonConvert.SerializeObject(preset, Formatting.Indented));
                
                WallpaperChanged?.Invoke(_config);
            }
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
                
                ThumbnailConverter.ClearCache(Path.Combine(preset.BaseDirectory!, "thumbnail" + ext));
                LoadLibrary();
            }
        }
    }

    private void DeletePreset_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.DataContext is WallpaperPreset preset)
        {
            if (Directory.Exists(preset.BaseDirectory))
            {
                try { Directory.Delete(preset.BaseDirectory, true); } catch { }
            }
            LoadLibrary();
        }
    }

    private void DragStrip_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) this.BeginMoveDrag(e);
    }

    private void SaveConfig()
    {
        try
        {
            string json = JsonConvert.SerializeObject(_config, Formatting.Indented);
            File.WriteAllText(WallpaperConfig.ConfigPath, json);
        }
        catch { }
    }
}

public class ActiveColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        return (value is bool b && b) ? Brush.Parse("White") : Brush.Parse("Transparent");
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
}

public class ThumbnailConverter : IValueConverter
{
    private static Dictionary<string, Bitmap> _cache = new();
    private static HashSet<string> _loading = new();

    public static void ClearCache(string path) => _cache.Remove(path);

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is WallpaperPreset preset)
        {
            if (preset.Type == WallpaperType.Procedural)
            {
                if (preset.ProceduralId == "starfield") return Brush.Parse("#000033");
                if (preset.ProceduralId == "matrix") return Brush.Parse("#002200");
                if (preset.ProceduralId == "swarm") return Brush.Parse("#222222");
                return Brush.Parse("#333333");
            }

            string? thumbRel = !string.IsNullOrEmpty(preset.ThumbnailPath) ? preset.ThumbnailPath : 
                               (preset.Type == WallpaperType.Image ? preset.Path : 
                               (preset.Type == WallpaperType.Clock ? preset.ClockImagePath : null));

            string? thumbPath = preset.GetResourcePath(thumbRel);

            if (!string.IsNullOrEmpty(thumbPath) && File.Exists(thumbPath))
            {
                if (_cache.TryGetValue(thumbPath, out var cached)) return cached;
                
                if (!_loading.Contains(thumbPath))
                {
                    _loading.Add(thumbPath);
                    var currentPath = thumbPath;
                    Task.Run(() =>
                    {
                        try
                        {
                            using var stream = File.OpenRead(currentPath);
                            var bitmap = Bitmap.DecodeToWidth(stream, 400);
                            _cache[currentPath] = bitmap;
                            Dispatcher.UIThread.Post(() => 
                            {
                                _loading.Remove(currentPath);
                                preset.ThumbnailPath = preset.ThumbnailPath; // Trigger UI refresh
                            });
                        }
                        catch { Dispatcher.UIThread.Post(() => _loading.Remove(currentPath!)); }
                    });
                }
                return null;
            }

            if (preset.Type == WallpaperType.Color && !string.IsNullOrEmpty(preset.Color)) return Brush.Parse(preset.Color);
        }
        return null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
}

public class TypeToVisibilityConverter : IValueConverter
{
    public static readonly TypeToVisibilityConverter Instance = new();
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        return (value is WallpaperType type && parameter is string target) ? type.ToString() == target : false;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
}
