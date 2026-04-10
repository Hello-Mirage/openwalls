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

    private string GetUserLibraryDir()
    {
        string pcName = Environment.MachineName.Replace(" ", "_");
        string path = Path.Combine(WallpaperConfig.LibraryDir, "user", pcName);
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        return path;
    }

    private void LoadLibrary()
    {
        // 1. Ensure directories exist
        try
        {
            if (!Directory.Exists(WallpaperConfig.BaseDir)) Directory.CreateDirectory(WallpaperConfig.BaseDir);
            if (!Directory.Exists(WallpaperConfig.LibraryDir)) Directory.CreateDirectory(WallpaperConfig.LibraryDir);
            
            // Proactively create user folder
            GetUserLibraryDir();
        }
        catch { }

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

        // 3. Scan Modular Library (Recursive)
        var allPresets = new List<WallpaperPreset>();
        if (Directory.Exists(WallpaperConfig.LibraryDir))
        {
            var metaFiles = Directory.GetFiles(WallpaperConfig.LibraryDir, "wallpaper.json", SearchOption.AllDirectories);
            foreach (var metaPath in metaFiles)
            {
                try
                {
                    var preset = JsonConvert.DeserializeObject<WallpaperPreset>(File.ReadAllText(metaPath));
                    if (preset != null)
                    {
                        preset.BaseDirectory = Path.GetDirectoryName(metaPath);
                        allPresets.Add(preset);
                    }
                }
                catch { }
            }
        }

        // 4. Sync Registry (Bootstrap missing stock wallpapers)
        if (SyncRegistryWithLibrary(allPresets))
        {
            LoadLibrary(); // Re-scan if something was added
            return;
        }

        _library = new ObservableCollection<WallpaperPreset>(allPresets.OrderByDescending(p => p.DateAdded));
        _config.Library = allPresets;

        foreach (var p in _library) p.IsActive = (p.Id == _config.CurrentPresetId);
        
        LibraryItemsControl.ItemsSource = _library;
    }

    private bool SyncRegistryWithLibrary(List<WallpaperPreset> existing)
    {
        bool added = false;
        try
        {
            var registryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wallpapers/registry.json");
            System.Diagnostics.Debug.WriteLine($"[OW] Syncing registry from: {registryPath}");
            
            if (!File.Exists(registryPath)) 
            {
                System.Diagnostics.Debug.WriteLine("[OW] Registry file NOT FOUND.");
                return false;
            }

            string json = File.ReadAllText(registryPath);
            var stock = JsonConvert.DeserializeObject<List<WallpaperPreset>>(json);
            if (stock == null) return false;

            foreach (var d in stock)
            {
                // check if ID or Name already exists in any case
                bool exists = existing.Any(p => 
                    p.Id.Equals(d.Id, StringComparison.OrdinalIgnoreCase) || 
                    p.Name.Equals(d.Name, StringComparison.OrdinalIgnoreCase));

                if (exists) continue;

                var sanitizedName = d.Name.Replace(" ", "");
                var folder = Path.Combine(WallpaperConfig.LibraryDir, sanitizedName);
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                
                d.BaseDirectory = folder;
                System.Diagnostics.Debug.WriteLine($"[OW] Bootstrapping missing preset: {d.Name} at {folder}");
                
                // Copy assets if they exist in build output root
                if (d.Type == WallpaperType.Clock)
                {
                    var src = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets/samurai-warrior-observing-village-moonlight.jpg");
                    if (File.Exists(src)) File.Copy(src, Path.Combine(folder, "samurai.jpg"), true);

                    var currentThumbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"wallpapers/{sanitizedName}/thumbnail.png");
                    var thumbDest = Path.Combine(folder, "thumbnail.png");
                    
                    if (File.Exists(currentThumbPath)) File.Copy(currentThumbPath, thumbDest, true);
                    else if (File.Exists(src)) File.Copy(src, thumbDest, true);
                }
                else if (d.Type == WallpaperType.Procedural)
                {
                    var srcLogic = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"wallpapers/{sanitizedName}/logic.cs");
                    if (File.Exists(srcLogic)) File.Copy(srcLogic, Path.Combine(folder, "logic.cs"), true);
                    
                    var srcThumb = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"wallpapers/{sanitizedName}/thumbnail.png");
                    if (File.Exists(srcThumb)) File.Copy(srcThumb, Path.Combine(folder, "thumbnail.png"), true);
                }

                File.WriteAllText(Path.Combine(folder, "wallpaper.json"), JsonConvert.SerializeObject(d, Formatting.Indented));
                added = true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OW] Registry Sync Error: {ex.Message}");
        }
        return added;
    }

    private void AddColor_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string hex)
        {
            var preset = new WallpaperPreset { Name = $"Color {hex}", Color = hex, Type = WallpaperType.Color };
            // Save as modular folder in user directory
            var folder = Path.Combine(GetUserLibraryDir(), $"Color_{hex.Replace("#", "")}");
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
            var folder = Path.Combine(GetUserLibraryDir(), name.Replace(" ", "_") + "_" + Guid.NewGuid().ToString().Substring(0, 4));
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

    private void EditClockSettings_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.DataContext is WallpaperPreset preset && preset.Type == WallpaperType.Clock)
        {
            var editWin = new ClockEditWindow(preset);
            editWin.PreviewChanged += (p) => WallpaperChanged?.Invoke(_config); // Live preview if this is the active wallpaper
            editWin.Saved += () => LoadLibrary(); // Refresh library view
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

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        ThumbnailConverter.ClearAllCache();
        base.OnClosing(e);
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

public class ThumbnailConverter : IMultiValueConverter
{
    private static Dictionary<string, Bitmap> _cache = new();
    private static HashSet<string> _loading = new();

    public static void ClearCache(string path) => _cache.Remove(path);
    public static void ClearAllCache() 
    {
        foreach (var bmp in _cache.Values) bmp.Dispose();
        _cache.Clear();
        _loading.Clear();
    }

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count > 0 && values[0] is WallpaperPreset preset)
        {
            // 1. Determine what we are looking for
            bool wantsBrush = typeof(IBrush).IsAssignableFrom(targetType);
            bool wantsImage = typeof(IImage).IsAssignableFrom(targetType);

            // 2. Identify the resource path
            string? thumbRel = !string.IsNullOrEmpty(preset.ThumbnailPath) ? preset.ThumbnailPath : 
                               (preset.Type == WallpaperType.Image ? preset.Path : 
                               (preset.Type == WallpaperType.Clock ? preset.ClockImagePath : null));

            string? thumbPath = preset.GetResourcePath(thumbRel);
            
            // Hardcoded dev fallback for source directory
            if (!string.IsNullOrEmpty(thumbRel) && !File.Exists(thumbPath))
            {
                var devPath = Path.Combine("D:/openwalls", "wallpapers", preset.Name.Replace(" ", ""), thumbRel);
                if (File.Exists(devPath)) thumbPath = devPath;
            }

            // 3. Handle Images (Bitmaps)
            if (!string.IsNullOrEmpty(thumbPath))
            {
                // Try from cache
                if (_cache.TryGetValue(thumbPath, out var cached))
                {
                    if (wantsImage) return cached;
                    if (wantsBrush) return new ImageBrush(cached) { Stretch = Stretch.UniformToFill };
                    return null;
                }

                // Try to Load if File Exists
                if (File.Exists(thumbPath))
                {
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
                                    preset.ThumbnailPath = preset.ThumbnailPath; // Trigger refresh
                                });
                            }
                            catch { Dispatcher.UIThread.Post(() => _loading.Remove(currentPath!)); }
                        });
                    }
                }
            }

            // 4. Handle Fallbacks while loading or if missing
            if (preset.Type == WallpaperType.Clock)
            {
                var samuraiPaths = new[] {
                    preset.GetResourcePath(preset.ClockImagePath),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets/samurai-warrior-observing-village-moonlight.jpg"),
                    "D:/openwalls/assets/samurai-warrior-observing-village-moonlight.jpg"
                };

                foreach (var p in samuraiPaths)
                {
                    if (!string.IsNullOrEmpty(p) && File.Exists(p))
                    {
                        // Load and cache the fallback so we don't repeat this
                        try {
                            var bmp = new Bitmap(p);
                            _cache[p] = bmp;
                            if (wantsImage) return bmp;
                            if (wantsBrush) return new ImageBrush(bmp) { Stretch = Stretch.UniformToFill };
                        } catch {}
                    }
                }
            }

            // 5. Handle Colors/Procedural (Brushes)
            IBrush? colorBrush = null;
            if (preset.Type == WallpaperType.Color && !string.IsNullOrEmpty(preset.Color)) 
                colorBrush = Brush.Parse(preset.Color);
            else if (preset.Type == WallpaperType.Procedural)
            {
                if (preset.Name.Contains("Matrix", StringComparison.OrdinalIgnoreCase)) colorBrush = Brush.Parse("#004400");
                else if (preset.Name.Contains("Space", StringComparison.OrdinalIgnoreCase)) colorBrush = Brush.Parse("#000033");
                else if (preset.Name.Contains("Hack", StringComparison.OrdinalIgnoreCase)) colorBrush = Brush.Parse("#330000");
                else if (preset.Name.Contains("Swarm", StringComparison.OrdinalIgnoreCase)) colorBrush = Brush.Parse("#222222");
            }

            if (colorBrush != null)
            {
                if (wantsBrush) return colorBrush;
                return null; // Don't return brush for Image.Source
            }

            if (wantsBrush) return Brush.Parse("#1a1a1a");
        }
        return null;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
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
