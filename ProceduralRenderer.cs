using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Controls;

namespace openwalls;

public class ProceduralRenderer
{
    private readonly Control _target;
    private DispatcherTimer? _timer;
    private string? _currentId;
    private Random _rng = new();
    private double _time = 0;

    // Starfield Data
    private List<Star> _stars = new();
    private class Star { public double X, Y, Z, Size; public Color Color; }

    public ProceduralRenderer(Control target)
    {
        _target = target;
    }

    public void Start(string id)
    {
        Stop();
        _currentId = id;
        _time = 0;

        if (id == "starfield") InitializeStarfield();

        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Render, OnTick);
        _timer.Start();
    }

    public void Stop()
    {
        _timer?.Stop();
        _currentId = null;
    }

    private void InitializeStarfield()
    {
        _stars.Clear();
        for (int i = 0; i < 200; i++)
        {
            _stars.Add(new Star
            {
                X = _rng.NextDouble() * 2000 - 1000,
                Y = _rng.NextDouble() * 2000 - 1000,
                Z = _rng.NextDouble() * 1000,
                Size = _rng.NextDouble() * 2 + 1,
                Color = Color.FromArgb(255, (byte)_rng.Next(200, 255), (byte)_rng.Next(200, 255), 255)
            });
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _time += 0.033;
        _target.InvalidateVisual();
    }

    public void Render(DrawingContext dc, Size size)
    {
        if (_currentId == "starfield") RenderStarfield(dc, size);
        else if (_currentId == "plasma") RenderPlasma(dc, size);
    }

    private void RenderStarfield(DrawingContext dc, Size size)
    {
        double centerX = size.Width / 2;
        double centerY = size.Height / 2;

        foreach (var star in _stars)
        {
            star.Z -= 2; // Move toward viewer
            if (star.Z <= 0) star.Z = 1000;

            double k = 128.0 / star.Z;
            double px = star.X * k + centerX;
            double py = star.Y * k + centerY;

            if (px < 0 || px > size.Width || py < 0 || py > size.Height) continue;

            double s = star.Size * k;
            byte alpha = (byte)Math.Clamp(255 - (star.Z / 4), 0, 255);
            var brush = new SolidColorBrush(Color.FromArgb(alpha, star.Color.R, star.Color.G, star.Color.B));
            
            dc.DrawEllipse(brush, null, new Point(px, py), s, s);
        }
    }

    private void RenderPlasma(DrawingContext dc, Size size)
    {
        // Smooth plasma effect using moving gradients
        var center = new Point(size.Width / 2, size.Height / 2);
        
        // Multiple moving blobs of color
        for (int i = 0; i < 3; i++)
        {
            double phase = _time * 0.5 + (i * Math.PI * 0.6);
            double x = Math.Sin(phase * 0.7) * (size.Width * 0.3) + center.X;
            double y = Math.Cos(phase * 1.1) * (size.Height * 0.3) + center.Y;
            double radius = (Math.Sin(_time * 0.3 + i) * 0.2 + 0.8) * (size.Width * 0.6);

            var color = i switch
            {
                0 => Color.FromArgb(40, 78, 204, 163), // Mint
                1 => Color.FromArgb(40, 15, 52, 96),   // Deep Blue
                2 => Color.FromArgb(40, 26, 26, 46),   // Darkest
                _ => Colors.Transparent
            };

            dc.DrawEllipse(new RadialGradientBrush
            {
                GradientStops = new GradientStops
                {
                    new GradientStop(color, 0),
                    new GradientStop(Colors.Transparent, 1)
                }
            }, null, new Point(x, y), radius, radius);
        }
    }
}
