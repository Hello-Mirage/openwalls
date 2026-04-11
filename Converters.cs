using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System.IO;
using System.Threading.Tasks;

namespace openwalls;

public class ActiveColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (value is bool b && b) ? Brush.Parse("White") : Brush.Parse("Transparent");
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
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
            bool wantsBrush = typeof(IBrush).IsAssignableFrom(targetType);
            bool wantsImage = typeof(IImage).IsAssignableFrom(targetType);

            string? thumbRel = !string.IsNullOrEmpty(preset.ThumbnailPath) ? preset.ThumbnailPath : 
                               (preset.Type == WallpaperType.Image ? preset.Path : 
                               (preset.Type == WallpaperType.Clock ? preset.ClockImagePath : null));

            string? thumbPath = preset.GetResourcePath(thumbRel);
            
            if (!string.IsNullOrEmpty(thumbRel) && !File.Exists(thumbPath))
            {
                var devPath = Path.Combine("D:/openwalls", "wallpapers", preset.Name.Replace(" ", ""), thumbRel);
                if (File.Exists(devPath)) thumbPath = devPath;
            }

            if (!string.IsNullOrEmpty(thumbPath))
            {
                if (_cache.TryGetValue(thumbPath, out var cached))
                {
                    if (wantsImage) return cached;
                    if (wantsBrush) return new ImageBrush(cached) { Stretch = Stretch.UniformToFill };
                    return null;
                }

                if (File.Exists(thumbPath) && !_loading.Contains(thumbPath))
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
                        try {
                            if (!_cache.ContainsKey(p)) _cache[p] = new Bitmap(p);
                            var bmp = _cache[p];
                            if (wantsImage) return bmp;
                            if (wantsBrush) return new ImageBrush(bmp) { Stretch = Stretch.UniformToFill };
                        } catch {}
                    }
                }
            }

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
                return null;
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
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (value is WallpaperType type && parameter is string target) ? type.ToString() == target : false;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}
