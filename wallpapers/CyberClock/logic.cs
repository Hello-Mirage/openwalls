// CYBERCLOCK v1.0
// Neon cyber aesthetic — big time display, HUD rings, scanlines, glitch pulse

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using openwalls;

CyberState state;
if (!State.ContainsKey("cyber")) {
    state = new CyberState();
    State["cyber"] = state;
} else {
    state = (CyberState)State["cyber"];
}

state.RingRot += DeltaTime * 0.4f;
state.RingRotSlow += DeltaTime * 0.15f;
state.GlitchTimer += DeltaTime;
state.ParticleTimer += DeltaTime;

// Spawn particles
if (state.ParticleTimer > 0.06f) {
    state.ParticleTimer = 0;
    state.Particles.Add(new CyberParticle {
        X = (float)(Rng.NextDouble() * Bounds.Width),
        Y = (float)Bounds.Height + 5,
        Speed = (float)(0.3 + Rng.NextDouble() * 1.2),
        Alpha = (float)(0.3 + Rng.NextDouble() * 0.7),
        Size = (float)(1 + Rng.NextDouble() * 3)
    });
    if (state.Particles.Count > 120) state.Particles.RemoveAt(0);
}

// Update particles
for (int i = state.Particles.Count - 1; i >= 0; i--) {
    state.Particles[i].Y -= state.Particles[i].Speed * 60 * DeltaTime;
    state.Particles[i].Alpha -= DeltaTime * 0.3f;
    if (state.Particles[i].Y < -10 || state.Particles[i].Alpha <= 0)
        state.Particles.RemoveAt(i);
}

// --- DESIGN TOKENS ---
var cyan       = new SolidColorBrush(Color.Parse("#00FFFF"));
var cyanDim    = new SolidColorBrush(Color.FromArgb(40,  0, 255, 255));
var cyanMid    = new SolidColorBrush(Color.FromArgb(90,  0, 255, 255));
var magenta    = new SolidColorBrush(Color.Parse("#FF00AA"));
var magentaDim = new SolidColorBrush(Color.FromArgb(50, 255, 0, 170));
var white      = new SolidColorBrush(Color.Parse("#FFFFFF"));
var darkBg     = new SolidColorBrush(Color.Parse("#050510"));

// ===== BACKGROUND =====
dc.FillRectangle(darkBg, new Rect(Bounds));

// Vignette — subtle radial darkening via layered rects
float cx = (float)Bounds.Width / 2;
float cy = (float)Bounds.Height / 2;

// ===== SCANLINES =====
for (float y = 0; y < Bounds.Height; y += 4) {
    var sl = new SolidColorBrush(Color.FromArgb(18, 0, 0, 0));
    DrawLine(new Point(0, y), new Point(Bounds.Width, y), sl, 2);
}

// ===== GRID =====
float gridPulse = (float)(Math.Sin(Time.TotalSeconds * 0.6) * 0.5 + 0.5);
var gridBrush = new SolidColorBrush(Color.FromArgb((byte)(gridPulse * 22 + 8), 0, 255, 255));
float gridSz = 80;
for (float x = 0; x < Bounds.Width; x += gridSz)
    DrawLine(new Point(x, 0), new Point(x, Bounds.Height), gridBrush, 1);
for (float y = 0; y < Bounds.Height; y += gridSz)
    DrawLine(new Point(0, y), new Point(Bounds.Width, y), gridBrush, 1);

// ===== RISING PARTICLES =====
foreach (var p in state.Particles) {
    var pb = new SolidColorBrush(Color.FromArgb((byte)(p.Alpha * 200), 0, 255, 255));
    FillRect(new Rect(p.X, p.Y, p.Size, p.Size * 2), pb);
}

// ===== HUD RINGS (centered) =====
// Outer slow counter-rotate ring — dashes
int dashCount = 32;
for (int i = 0; i < dashCount; i++) {
    float angle = state.RingRotSlow + i * (float)(2 * Math.PI / dashCount);
    float r1 = 230, r2 = 242;
    float x1 = cx + (float)Math.Cos(angle) * r1;
    float y1 = cy + (float)Math.Sin(angle) * r1;
    float x2 = cx + (float)Math.Cos(angle) * r2;
    float y2 = cy + (float)Math.Sin(angle) * r2;
    byte alpha = (byte)(i % 2 == 0 ? 180 : 60);
    var db = new SolidColorBrush(Color.FromArgb(alpha, 0, 255, 255));
    DrawLine(new Point(x1, y1), new Point(x2, y2), db, 2);
}

// Middle ring — fast rotation with 6 arc accents
for (int i = 0; i < 6; i++) {
    float angle = state.RingRot + i * (float)(2 * Math.PI / 6);
    float x1 = cx + (float)Math.Cos(angle) * 190;
    float y1 = cy + (float)Math.Sin(angle) * 190;
    float x2 = cx + (float)Math.Cos(angle + 0.25) * 205;
    float y2 = cy + (float)Math.Sin(angle + 0.25) * 205;
    DrawLine(new Point(x1, y1), new Point(x2, y2), magenta, 3);
}

// Inner ring — counter direction, tick marks
for (int i = 0; i < 60; i++) {
    float angle = -state.RingRot * 0.7f + i * (float)(2 * Math.PI / 60);
    float r1 = i % 5 == 0 ? 148f : 154f;
    float r2 = 162f;
    float x1 = cx + (float)Math.Cos(angle) * r1;
    float y1 = cy + (float)Math.Sin(angle) * r1;
    float x2 = cx + (float)Math.Cos(angle) * r2;
    float y2 = cy + (float)Math.Sin(angle) * r2;
    byte ta = (byte)(i % 5 == 0 ? 220 : 80);
    var tb = new SolidColorBrush(Color.FromArgb(ta, 0, 255, 255));
    DrawLine(new Point(x1, y1), new Point(x2, y2), tb, i % 5 == 0 ? 2 : 1);
}

// Core circle fill
FillRect(new Rect(cx - 140, cy - 140, 280, 280), cyanDim);
DrawRect(new Rect(cx - 140, cy - 140, 280, 280), cyanMid, 1);

// ===== CLOCK HANDS (analog-style progress arcs via line segments) =====
var now = DateTime.Now;
float secAngle   = (float)((now.Second + now.Millisecond / 1000.0) / 60.0 * 2 * Math.PI) - (float)(Math.PI / 2);
float minAngle   = (float)((now.Minute + now.Second / 60.0) / 60.0 * 2 * Math.PI) - (float)(Math.PI / 2);
float hourAngle  = (float)((now.Hour % 12 + now.Minute / 60.0) / 12.0 * 2 * Math.PI) - (float)(Math.PI / 2);

// Second hand — thin cyan
DrawLine(
    new Point(cx, cy),
    new Point(cx + (float)Math.Cos(secAngle) * 120, cy + (float)Math.Sin(secAngle) * 120),
    cyan, 2
);
// Minute hand — magenta
DrawLine(
    new Point(cx, cy),
    new Point(cx + (float)Math.Cos(minAngle) * 95, cy + (float)Math.Sin(minAngle) * 95),
    magenta, 3
);
// Hour hand — white
DrawLine(
    new Point(cx, cy),
    new Point(cx + (float)Math.Cos(hourAngle) * 65, cy + (float)Math.Sin(hourAngle) * 65),
    white, 4
);

// Center dot
FillRect(new Rect(cx - 5, cy - 5, 10, 10), magenta);
DrawRect(new Rect(cx - 5, cy - 5, 10, 10), cyan, 1);

// ===== DIGITAL TIME =====
bool glitchActive = state.GlitchTimer > 4.5f && state.GlitchTimer < 4.6f;
float timeY = cy + 175;
string timeStr = now.ToString("HH:mm:ss");
if (glitchActive) {
    string[] glitchChars = {"#","@","!","?","X","0"};
    char[] arr = timeStr.ToCharArray();
    for (int g = 0; g < arr.Length; g++)
        if (arr[g] != ':' && Rng.NextDouble() < 0.4)
            arr[g] = glitchChars[Rng.Next(glitchChars.Length)][0];
    timeStr = new string(arr);
}
if (state.GlitchTimer > 5.0f) state.GlitchTimer = 0;

// Shadow / glow pass
var glowBrush = new SolidColorBrush(Color.FromArgb(60, 0, 255, 255));
DrawText(timeStr, new Point(cx - 140 + 2, timeY + 2), 52, glowBrush);
DrawText(timeStr, new Point(cx - 140,     timeY),     52, glitchActive ? magenta : cyan);

// Date strip
string dateStr = now.ToString("ddd  dd MMM  yyyy").ToUpper();
DrawText(dateStr, new Point(cx - 110, timeY + 65), 15, cyanMid);

// ===== CORNER HUD LABELS =====
DrawText("SYS_CLOCK_V3", new Point(20, 18), 11, cyanMid);
DrawText("LOC://CYBERSPACE", new Point(20, 36), 11, cyanMid);

string uptimeStr = $"UPTIME :: {(int)Time.TotalSeconds / 3600:D2}:{((int)Time.TotalSeconds % 3600) / 60:D2}:{(int)Time.TotalSeconds % 60:D2}";
DrawText(uptimeStr, new Point((float)Bounds.Width - 260, 18), 11, cyanMid);
DrawText("STATUS :: ONLINE", new Point((float)Bounds.Width - 200, 36), 11, magentaDim);

// Bottom bar
float bby = (float)Bounds.Height - 28;
FillRect(new Rect(0, bby, Bounds.Width, 28), cyanDim);
DrawLine(new Point(0, bby), new Point(Bounds.Width, bby), cyanMid, 1);
DrawText("// CYBERCLOCK — OPENWALLS //", new Point(cx - 130, bby + 7), 12, cyan);

public class CyberParticle {
    public float X, Y, Speed, Alpha, Size;
}

public class CyberState {
    public float RingRot = 0;
    public float RingRotSlow = 0;
    public float GlitchTimer = 0;
    public float ParticleTimer = 0;
    public List<CyberParticle> Particles = new();
}
