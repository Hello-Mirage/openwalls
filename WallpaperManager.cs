using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using System.Diagnostics;

namespace openwalls;

public interface IWallpaperDisplay
{
    Border ColorLayer { get; }
    Image ImageLayer { get; }
    Control VideoLayer { get; }
    ProceduralCanvas ProceduralLayer { get; }
    Grid ClockBackdropLayer { get; }
    Image ClockBackground { get; }
    Panel FallbackText { get; }
}

public class WallpaperManager : IDisposable
{
    private readonly IWallpaperDisplay _display;
    private readonly LibVLC? _libVLC;
    private readonly MediaPlayer? _mediaPlayer;
    private readonly ProceduralRenderer _proceduralRenderer;
    private readonly ClockOverlayWindow _clockHUD;
    
    private Bitmap? _backgroundBitmap;
    private Bitmap? _clockBackgroundBitmap;
    private bool _isOptimizationPaused = false;

    public WallpaperManager(IWallpaperDisplay display, LibVLC vlc, MediaPlayer mp, ProceduralRenderer pr, ClockOverlayWindow hud)
    {
        _display = display;
        _libVLC = vlc;
        _mediaPlayer = mp;
        _proceduralRenderer = pr;
        _clockHUD = hud;
    }

    public void OnWallpaperChanged(WallpaperConfig config)
    {
        var preset = config.Library.FirstOrDefault(p => p.Id == config.CurrentPresetId);
        
        if (_libVLC == null || _mediaPlayer == null) return;

        ResetLayers();
        
        if (_mediaPlayer.IsPlaying || _mediaPlayer.State == VLCState.Paused) 
        {
            _mediaPlayer.Stop();
        }
        _proceduralRenderer.Stop();
        _isOptimizationPaused = false;

        DisposeBitmaps();

        _clockHUD.UpdateConfiguration(config);

        if (preset == null) { _display.FallbackText.IsVisible = true; return; }

        switch (preset.Type)
        {
            case WallpaperType.Color:
                HandleColorWallpaper(preset);
                break;
            case WallpaperType.Image:
                HandleImageWallpaper(preset);
                break;
            case WallpaperType.Video:
                HandleVideoWallpaper(preset);
                break;
            case WallpaperType.Procedural:
                HandleProceduralWallpaper(preset);
                break;
            case WallpaperType.Clock:
                HandleClockWallpaper(preset);
                break;
            default:
                _display.FallbackText.IsVisible = true;
                break;
        }

        CleanupMemory();
    }

    private void ResetLayers()
    {
        _display.ColorLayer.IsVisible = false;
        _display.ImageLayer.IsVisible = false;
        _display.VideoLayer.IsVisible = false;
        _display.ProceduralLayer.IsVisible = false;
        _display.ClockBackdropLayer.IsVisible = false;
        _display.FallbackText.IsVisible = false;
        _display.ImageLayer.Opacity = 0;
        _display.VideoLayer.Opacity = 0;
    }

    private void DisposeBitmaps()
    {
        _backgroundBitmap?.Dispose();
        _backgroundBitmap = null;
        _display.ImageLayer.Source = null;

        _clockBackgroundBitmap?.Dispose();
        _clockBackgroundBitmap = null;
        _display.ClockBackground.Source = null;
    }

    private void HandleColorWallpaper(WallpaperPreset preset)
    {
        if (!string.IsNullOrEmpty(preset.Color)) 
        { 
            _display.ColorLayer.Background = SolidColorBrush.Parse(preset.Color); 
            _display.ColorLayer.IsVisible = true; 
        }
    }

    private void HandleImageWallpaper(WallpaperPreset preset)
    {
        var imgPath = preset.GetResourcePath(preset.Path);
        if (!string.IsNullOrEmpty(imgPath) && File.Exists(imgPath)) 
        { 
            _backgroundBitmap = new Bitmap(imgPath);
            _display.ImageLayer.Source = _backgroundBitmap;
            _display.ImageLayer.IsVisible = true; 
            _display.ImageLayer.Opacity = 1; 
        }
    }

    private void HandleVideoWallpaper(WallpaperPreset preset)
    {
        var videoPath = preset.GetResourcePath(preset.Path);
        if (!string.IsNullOrEmpty(videoPath) && File.Exists(videoPath))
        {
            var (optimizedPath, isOptimized) = VideoOptimizer.GetOptimizedPath(videoPath);
            var pathToPlay = isOptimized ? optimizedPath : videoPath;

            try 
            {
                using var media = new Media(_libVLC!, pathToPlay, FromType.FromPath);
                media.AddOption(":input-repeat=65535");
                if (preset.IsMuted) _mediaPlayer!.Mute = true;
                _display.VideoLayer.IsVisible = true;
                _display.VideoLayer.Opacity = 1;
                _mediaPlayer!.Play(media);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Playback failed: {ex.Message}");
                PlaySourceVideo(videoPath);
            }

            if (!isOptimized) VideoOptimizer.OptimizeAsync(videoPath);
        }
    }

    private void PlaySourceVideo(string videoPath)
    {
        using var sourceMedia = new Media(_libVLC!, videoPath, FromType.FromPath);
        sourceMedia.AddOption(":input-repeat=65535");
        _display.VideoLayer.IsVisible = true;
        _display.VideoLayer.Opacity = 1;
        _mediaPlayer!.Play(sourceMedia);
    }

    private void HandleProceduralWallpaper(WallpaperPreset preset)
    {
        _display.ProceduralLayer.IsVisible = true; 
        _proceduralRenderer.Start(preset); 
    }

    private void HandleClockWallpaper(WallpaperPreset preset)
    {
        _display.ClockBackground.IsVisible = false;
        
        if (preset.ClockBackdropType == "Video" && !string.IsNullOrEmpty(preset.ClockBackdropPath))
        {
            var videoPath = preset.GetResourcePath(preset.ClockBackdropPath);
            if (File.Exists(videoPath))
            {
                var (optimizedPath, isOptimized) = VideoOptimizer.GetOptimizedPath(videoPath);
                var pathToPlay = isOptimized ? optimizedPath : videoPath;

                try 
                {
                    using var media = new Media(_libVLC!, pathToPlay, FromType.FromPath);
                    media.AddOption(":input-repeat=65535");
                    _mediaPlayer!.Mute = true;
                    _display.VideoLayer.IsVisible = true;
                    _display.VideoLayer.Opacity = 1;
                    _mediaPlayer!.Play(media);
                }
                catch { PlaySourceVideo(videoPath); }

                if (!isOptimized) VideoOptimizer.OptimizeAsync(videoPath);
            }
        }
        else
        {
            var relativeClockPath = preset.ClockBackdropPath ?? preset.ClockImagePath ?? "assets/samurai-warrior-observing-village-moonlight.jpg";
            var clockPath = preset.GetResourcePath(relativeClockPath);
            
            if (File.Exists(clockPath)) 
            {
                _clockBackgroundBitmap = new Bitmap(clockPath);
                _display.ClockBackground.Source = _clockBackgroundBitmap;
                _display.ClockBackground.IsVisible = true;
            }
        }

        _display.ClockBackdropLayer.IsVisible = true;
    }

    public void PausePlayback()
    {
        if (_mediaPlayer?.IsPlaying == true) _mediaPlayer.Stop();
        _proceduralRenderer.Stop(); 
        DisposeBitmaps();
        _isOptimizationPaused = true;
        CleanupMemory();
    }

    private void CleanupMemory()
    {
        Dispatcher.UIThread.Post(() => {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        }, DispatcherPriority.Background);
    }

    public void Dispose()
    {
        _mediaPlayer?.Dispose();
        _libVLC?.Dispose();
        DisposeBitmaps();
    }
}
