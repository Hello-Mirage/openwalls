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
    State["stars"] = stars;
} else {
    stars = (List<Star>)State["stars"];
}

// Ensure star density is incredibly high
while (stars.Count < 2500) {
    stars.Add(new Star {
        X = (float)(Rng.NextDouble() * 2 - 1),
        Y = (float)(Rng.NextDouble() * 2 - 1),
        Z = (float)Rng.NextDouble(),
        Velocity = 0.005f + (float)Rng.NextDouble() * 0.01f
    });
}

Pen[] cachedPens;
if (!State.ContainsKey("pens")) {
    cachedPens = new Pen[100];
    for (int i = 0; i < 100; i++) {
        float f = i / 99.0f;
        float size = f * 3;
        var brush = new SolidColorBrush(Color.FromArgb((byte)(f * 255), 255, 255, 255));
        var pen = new Pen(brush, size);
        cachedPens[i] = pen;
    }
    State["pens"] = cachedPens;
} else {
    cachedPens = (Pen[])State["pens"];
}

dc.FillRectangle(Brushes.Black, new Rect(Bounds));

// Pre-calculate loop variables to reduce operations
float hw = (float)Bounds.Width / 2;
float hh = (float)Bounds.Height / 2;
float k = 1200.0f;

foreach (var s in stars) {
    float oldZ = s.Z;
    s.Z -= s.Velocity;
    
    if (s.Z <= 0) {
        s.Z = 1.0f;
        s.X = (float)(Rng.NextDouble() * 2 - 1);
        s.Y = (float)(Rng.NextDouble() * 2 - 1);
        oldZ = 1.0f;
    }

    float px = (s.X * k / s.Z) + hw;
    float py = (s.Y * k / s.Z) + hh;
    
    float ox = (s.X * k / oldZ) + hw;
    float oy = (s.Y * k / oldZ) + hh;

    // Use cached pen based on depth (1.0 = distant, 0.0 = close)
    int depthIndex = (int)((1.0f - s.Z) * 99);
    var pen = cachedPens[Math.Clamp(depthIndex, 0, 99)];
    
    dc.DrawLine(pen, new Point(ox, oy), new Point(px, py));
}

public class Star {
    public float X, Y, Z;
    public float Velocity;
}
