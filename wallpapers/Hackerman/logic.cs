// REDLINE CYBER-SPHERE v3.0
// [Intense Hacking Interface]
using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;

RedlineState state;
if (!State.ContainsKey("redline")) {
    state = new RedlineState();
    // Initialize some initial circuits
    for(int i = 0; i < 20; i++) state.Circuits.Add(new CircuitLine(new Rect(Bounds)));
    State["redline"] = state;
} else {
    state = (RedlineState)State["redline"];
}

dc.FillRectangle(new SolidColorBrush(Color.Parse("#080202")), new Rect(Bounds));

// --- COLOR TOKENS ---
var redGlow = new SolidColorBrush(Color.Parse("#FF1122"));
var redMid = new SolidColorBrush(Color.Parse("#880511"));
var redDim = new SolidColorBrush(Color.Parse("#330005"));
var whiteHot = new SolidColorBrush(Color.Parse("#FFDDDD"));

float centerX = (float)Bounds.Width / 2;
float centerY = (float)Bounds.Height / 2;

// --- 1. THE GRID ---
float gridSize = 80;
float offset = (float)(Time.TotalSeconds * 20 % gridSize);
for (float x = offset; x < Bounds.Width; x += gridSize) {
    DrawLine(new Point(x, 0), new Point(x, Bounds.Height), redDim, 1);
}
for (float y = offset; y < Bounds.Height; y += gridSize) {
    DrawLine(new Point(0, y), new Point(Bounds.Width, y), redDim, 1);
}

// --- 2. DYNAMIC CIRCUITS ---
foreach(var c in state.Circuits) {
    c.Update(DeltaTime, new Rect(Bounds));
    c.Draw(dc, redMid, redGlow, whiteHot);
}

// Randomly add/remove circuits
if (Rng.NextDouble() < 0.05 && state.Circuits.Count < 40) state.Circuits.Add(new CircuitLine(new Rect(Bounds)));

// --- 3. CENTRAL DATA BOX ---
float boxW = 500;
float boxH = 400;
float boxX = centerX - boxW/2;
float boxY = centerY - boxH/2;

// Box shadow / inner glow
FillRect(new Rect(boxX, boxY, boxW, boxH), new SolidColorBrush(Color.Parse("#DD020000")));
DrawRect(new Rect(boxX, boxY, boxW, boxH), redGlow, 2);
DrawRect(new Rect(boxX - 5, boxY - 5, boxW + 10, boxH + 10), redMid, 1);

// Decryption Logs
state.WaitTimer -= DeltaTime;
if (state.WaitTimer <= 0) {
    state.WaitTimer = (float)Rng.NextDouble() * 0.1f;
    string prefix = Rng.NextDouble() > 0.5 ? "01101011 : " : "01101010 : ";
    string[] msgs = { "SYS.ACCESS.GRANTED", "NET_BREACH_V3", "SEC.DECRYPTION.BROKEN", "FIREWALL.OVERRIDE.ACTIVE", ">RUN CRACK.EXE", "STATUS: DECRYPTING", "DATA_STREAM_42", "KERNEL..LEVEL..ROOT" };
    state.Logs.Add(prefix + msgs[Rng.Next(msgs.Length)]);
    if (state.Logs.Count > 18) state.Logs.RemoveAt(0);
}

DrawText("01101011", new Point(boxX + 20, boxY + 20), 24, whiteHot);
DrawLine(new Point(boxX + 20, boxY + 55), new Point(boxX + 160, boxY + 55), redGlow, 2);

for (int i = 0; i < state.Logs.Count; i++) {
    DrawText(state.Logs[i], new Point(boxX + 20, boxY + 70 + (i * 16)), 12, redGlow);
}

// Right side of data box
state.HexRotate -= DeltaTime * 2f;
for(int i = 0; i < 6; i++) {
    DrawLine(new Point(boxX + boxW - 80, boxY + 60 + (i*15)), new Point(boxX + boxW - 20, boxY + 60 + (i*15)), redMid, 2);
}

// Flashing Warning
if (Math.Sin(Time.TotalSeconds * 10) > 0) {
    DrawText("!! ROOT@CYBERNET !!", new Point(boxX + boxW - 180, boxY + boxH - 40), 14, whiteHot);
}

// --- 4. HUD DECORATIONS ---
float radius = 400 + (float)Math.Sin(Time.TotalSeconds)*10;
for(int i=0; i<3; i++) {
    float startA = (float)Time.TotalSeconds * 0.5f + i * 2.1f;
    float rx1 = boxX - 100 + (float)Math.Cos(startA) * radius;
    float ry1 = boxY + boxH/2 + (float)Math.Sin(startA) * radius;
    DrawLine(new Point(boxX - 100, boxY + boxH/2), new Point(rx1, ry1), redDim, 4);
    dc.DrawEllipse(redMid, null, new Point(rx1, ry1), 4, 4);
}

public class RedlineState {
    public List<CircuitLine> Circuits = new();
    public List<string> Logs = new();
    public float WaitTimer = 0;
    public float HexRotate = 0;
}

public class CircuitLine {
    public List<Point> Points = new();
    public int MaxLength = 5;
    public float Speed = 200;
    public Point CurrentPos;
    public Point CurrentDir;
    
    public CircuitLine(Rect bounds) {
        Random r = new Random();
        CurrentPos = new Point(r.NextDouble() * bounds.Width, r.NextDouble() * bounds.Height);
        Points.Add(CurrentPos);
        PickDirection(r);
        MaxLength = r.Next(3, 10);
        Speed = r.Next(300, 1000);
    }
    
    public void PickDirection(Random r) {
        int d = r.Next(0, 4);
        if (d==0) CurrentDir = new Point(1, 0);
        if (d==1) CurrentDir = new Point(-1, 0);
        if (d==2) CurrentDir = new Point(0, 1);
        if (d==3) CurrentDir = new Point(0, -1);
    }
    
    public void Update(float dt, Rect bounds) {
        CurrentPos = new Point(CurrentPos.X + CurrentDir.X * Speed * dt, CurrentPos.Y + CurrentDir.Y * Speed * dt);
        
        if (new Random().NextDouble() < 0.05) {
            Points.Add(CurrentPos);
            PickDirection(new Random());
        }
        
        if (Points.Count > MaxLength) {
            Points.RemoveAt(0);
        }
        
        // Wrap
        if (CurrentPos.X < -50 || CurrentPos.X > bounds.Width + 50 || CurrentPos.Y < -50 || CurrentPos.Y > bounds.Height + 50) {
            Points.Clear();
            Random r = new Random();
            CurrentPos = new Point(r.NextDouble() * bounds.Width, r.NextDouble() * bounds.Height);
            Points.Add(CurrentPos);
        }
    }
    
    public void Draw(DrawingContext dc, IBrush dim, IBrush glow, IBrush hot) {
        if (Points.Count < 1) return;
        var p = new Pen(dim, 2);
        for(int i=0; i<Points.Count-1; i++) dc.DrawLine(p, Points[i], Points[i+1]);
        dc.DrawLine(new Pen(glow, 2), Points[Points.Count-1], CurrentPos);
        dc.DrawEllipse(hot, null, CurrentPos, 3, 3);
    }
}