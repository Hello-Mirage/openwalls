// Openwalls Community Shader: Deep Space
// A cinematic 3D starfield with motion blur trails.

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using openwalls;

// Entry Point Logic
List<Star> stars;
if (!State.ContainsKey("stars")) {
    stars = new List<Star>();
    for (int i = 0; i < 400; i++) {
        stars.Add(new Star {
            X = (float)(Rng.NextDouble() * 2 - 1),
            Y = (float)(Rng.NextDouble() * 2 - 1),
            Z = (float)Rng.NextDouble(),
            Velocity = 0.005f + (float)Rng.NextDouble() * 0.01f
        });
    }
    State["stars"] = stars;
} else {
    stars = (List<Star>)State["stars"];
}

dc.FillRectangle(Brushes.Black, new Rect(Bounds));

foreach (var s in stars) {
    float oldZ = s.Z;
    s.Z -= s.Velocity;
    
    if (s.Z <= 0) {
        s.Z = 1.0f;
        s.X = (float)(Rng.NextDouble() * 2 - 1);
        s.Y = (float)(Rng.NextDouble() * 2 - 1);
        oldZ = 1.0f;
    }

    float k = 1200.0f;
    float px = (s.X * k / s.Z) + (float)Bounds.Width / 2;
    float py = (s.Y * k / s.Z) + (float)Bounds.Height / 2;
    
    float ox = (s.X * k / oldZ) + (float)Bounds.Width / 2;
    float oy = (s.Y * k / oldZ) + (float)Bounds.Height / 2;

    float size = (1 - s.Z) * 3;
    float alpha = 1.0f - s.Z;
    var pen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(alpha * 255), 255, 255, 255)), size);
    
    dc.DrawLine(pen, new Point(ox, oy), new Point(px, py));
}

public class Star {
    public float X, Y, Z;
    public float Velocity;
}
