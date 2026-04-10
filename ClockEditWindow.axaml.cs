using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Newtonsoft.Json;

namespace openwalls;

public partial class ClockEditWindow : Window
{
    private readonly WallpaperPreset _originalPreset;
    private readonly WallpaperPreset _livePreset;
    public event Action<WallpaperPreset>? PreviewChanged;
    public event Action? Saved;

    public ClockEditWindow(WallpaperPreset preset)
    {
        InitializeComponent();
        _originalPreset = preset;
        
        // Deep clone for live editing
        _livePreset = JsonConvert.DeserializeObject<WallpaperPreset>(JsonConvert.SerializeObject(preset))!;
        
        // Initialize UI values
        SizeSlider.Value = _livePreset.ClockFontSize;
        XSlider.Value = _livePreset.ClockHorizontalOffset;
        YSlider.Value = _livePreset.ClockVerticalOffset;
    }

    private async void OnSetImageClick(object sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Pick Clock Background Image",
            AllowMultiple = false,
            FileTypeFilter = new[] { FilePickerFileTypes.ImageAll }
        });

        if (files.Count >= 1)
        {
            var src = files[0].Path.LocalPath;
            var dest = Path.Combine(_livePreset.BaseDirectory ?? "", "clock_bg" + Path.GetExtension(src));
            File.Copy(src, dest, true);
            
            _livePreset.ClockBackdropPath = Path.GetFileName(dest);
            _livePreset.ClockBackdropType = "Image";
            UpdatePreview();
        }
    }

    private async void OnSetVideoClick(object sender, RoutedEventArgs e)
    {
        var topLevel = GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Pick Clock Background Video",
            AllowMultiple = false,
            FileTypeFilter = new[] { 
                new FilePickerFileType("Videos") { Patterns = new[] { "*.mp4", "*.mov", "*.mkv" } } 
            }
        });

        if (files.Count >= 1)
        {
            var src = files[0].Path.LocalPath;
            var dest = Path.Combine(_livePreset.BaseDirectory ?? "", "clock_bg" + Path.GetExtension(src));
            File.Copy(src, dest, true);
            
            _livePreset.ClockBackdropPath = Path.GetFileName(dest);
            _livePreset.ClockBackdropType = "Video";
            UpdatePreview();
        }
    }

    private void OnSliderChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_livePreset == null) return;
        _livePreset.ClockFontSize = SizeSlider.Value;
        _livePreset.ClockHorizontalOffset = XSlider.Value;
        _livePreset.ClockVerticalOffset = YSlider.Value;
        UpdatePreview();
    }

    private void OnColorLightClick(object sender, RoutedEventArgs e)
    {
        _livePreset.ClockFontColor = "#FFFFFF";
        UpdatePreview();
    }

    private void OnColorDarkClick(object sender, RoutedEventArgs e)
    {
        _livePreset.ClockFontColor = "#000000";
        UpdatePreview();
    }

    private void UpdatePreview() => PreviewChanged?.Invoke(_livePreset);

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        // Copy live values back to original
        _originalPreset.ClockFontSize = _livePreset.ClockFontSize;
        _originalPreset.ClockFontColor = _livePreset.ClockFontColor;
        _originalPreset.ClockHorizontalOffset = _livePreset.ClockHorizontalOffset;
        _originalPreset.ClockVerticalOffset = _livePreset.ClockVerticalOffset;
        _originalPreset.ClockBackdropPath = _livePreset.ClockBackdropPath;
        _originalPreset.ClockBackdropType = _livePreset.ClockBackdropType;

        // Save to wallpaper.json
        if (!string.IsNullOrEmpty(_originalPreset.BaseDirectory))
        {
            var path = Path.Combine(_originalPreset.BaseDirectory, "wallpaper.json");
            File.WriteAllText(path, JsonConvert.SerializeObject(_originalPreset, Formatting.Indented));
        }

        Saved?.Invoke();
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        PreviewChanged?.Invoke(_originalPreset); // Revert live preview
        Close();
    }
}
