using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Svg.Skia;

namespace openwalls;

public static class IconUtils
{
    private static WindowIcon? _cachedIcon;

    public static WindowIcon? LoadSvgIcon()
    {
        if (_cachedIcon != null) return _cachedIcon;

        try
        {
            var svgSource = SvgSource.Load("avares://openwalls/assets/openwalls_logo.svg");
            
            if (svgSource != null)
            {
                // Render to a 32x32 bitmap for the Taskbar/Tray
                var renderSize = new PixelSize(32, 32);
                var bitmap = new RenderTargetBitmap(renderSize, new Vector(96, 96));
                using (var ctx = bitmap.CreateDrawingContext())
                {
                    var svgImage = new SvgImage { Source = svgSource };
                    ctx.DrawImage(svgImage, new Rect(0, 0, 32, 32));
                }
                _cachedIcon = new WindowIcon(bitmap);
                return _cachedIcon;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to load logo.svg: {ex.Message}");
        }

        return null;
    }
}
