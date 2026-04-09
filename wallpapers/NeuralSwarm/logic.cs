// Openwalls Community Shader: Neural Swarm
// A bio-organic particle swarm with kinetic connections.

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using openwalls;

// Entry Point Logic
List<Particle> particles;
if (!State.ContainsKey("particles")) {
    particles = new List<Particle>();
    for (int i = 0; i < 150; i++) {
        particles.Add(new Particle {
            Pos = new Point(Rng.NextDouble() * Bounds.Width, Rng.NextDouble() * Bounds.Height),
            Vel = new Vector(Rng.NextDouble() * 2 - 1, Rng.NextDouble() * 2 - 1)
        });
    }
    State["particles"] = particles;
} else {
    particles = (List<Particle>)State["particles"];
}

dc.FillRectangle(new SolidColorBrush(Color.Parse("#0a0a0a")), new Rect(Bounds));

for (int i = 0; i < particles.Count; i++) {
    var p = particles[i];
    p.Pos += p.Vel;
    
    if (p.Pos.X < 0 || p.Pos.X > Bounds.Width) p.Vel = new Vector(-p.Vel.X, p.Vel.Y);
    if (p.Pos.Y < 0 || p.Pos.Y > Bounds.Height) p.Vel = new Vector(p.Vel.X, -p.Vel.Y);
    
    // Draw connections
    for (int j = i + 1; j < particles.Count; j++) {
        var p2 = particles[j];
        double dist = Math.Sqrt(Math.Pow(p.Pos.X - p2.Pos.X, 2) + Math.Pow(p.Pos.Y - p2.Pos.Y, 2));
        if (dist < 100) {
            float opacity = (float)(1.0 - (dist / 100));
            var pen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(opacity * 100), 200, 200, 255)), 0.5);
            dc.DrawLine(pen, p.Pos, p2.Pos);
        }
    }
    
    dc.DrawEllipse(Brushes.White, null, p.Pos, 1.5, 1.5);
    particles[i] = p; // Update struct in list
}

public class Particle {
    public Point Pos;
    public Vector Vel;
}
