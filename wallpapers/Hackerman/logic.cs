// ELITE HACKERMAN HUD v2.0
// [Electric Lofi Edition]
// No forbidden tokens detected

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using openwalls;

HudState state;
if (!State.ContainsKey("hud")) {
    state = new HudState();
    State["hud"] = state;
} else {
    state = (HudState)State["hud"];
}

dc.FillRectangle(Brushes.Black, new Rect(Bounds));

// --- DESIGN TOKENS ---
var lime = new SolidColorBrush(Color.Parse("#CCFF00"));
var limeDim = new SolidColorBrush(Color.Parse("#22CCFF00"));
var limeMid = new SolidColorBrush(Color.Parse("#44CCFF00"));
var accent = new SolidColorBrush(Color.Parse("#FF0055")); // Subtle alert accent

// --- LAYER 0: CIRCUIT GRID ---
float gridSize = 100;
float gridPulse = (float)(Math.Sin(Time.TotalSeconds * 0.5) * 0.5 + 0.5);
var gridPen = new SolidColorBrush(Color.FromArgb((byte)(gridPulse * 30), 204, 255, 0));

for (float x = 0; x < Bounds.Width; x += gridSize) {
    DrawLine(new Point(x, 0), new Point(x, Bounds.Height), gridPen, 1);
}
for (float y = 0; y < Bounds.Height; y += gridSize) {
    DrawLine(new Point(0, y), new Point(Bounds.Width, y), gridPen, 1);
}

// --- LAYER 1: ROTATING HUD RING ---
state.HudRotate += DeltaTime * 0.2f;
float centerX = (float)Bounds.Width - 300;
float centerY = (float)Bounds.Height / 2;

// Outer Ring
for (int i = 0; i < 8; i++) {
    float angle = state.HudRotate + i * (float)Math.PI / 4;
    float x1 = centerX + (float)Math.Cos(angle) * 180;
    float y1 = centerY + (float)Math.Sin(angle) * 180;
    float x2 = centerX + (float)Math.Cos(angle + 0.2) * 200;
    float y2 = centerY + (float)Math.Sin(angle + 0.2) * 200;
    DrawLine(new Point(x1, y1), new Point(x2, y2), limeMid, 4);
}

// Inner pulse
float innerPulse = (float)(Math.Sin(Time.TotalSeconds * 2) * 20 + 100);
DrawRect(new Rect(centerX - (innerPulse/2), centerY - (innerPulse/2), innerPulse, innerPulse), limeDim, 2);

// --- LAYER 2: NETWORK SPIKE GRAPH ---
state.GraphTimer += DeltaTime;
if (state.GraphTimer > 0.05f) {
    state.GraphTimer = 0;
    state.GraphData.Add((float)Rng.NextDouble() * 100);
    if (state.GraphData.Count > 100) state.GraphData.RemoveAt(0);
}

float graphW = (float)Bounds.Width * 0.6f;
float graphH = 150;
float graphX = 50;
float graphY = (float)Bounds.Height - 200;

for (int i = 1; i < state.GraphData.Count; i++) {
    float x1 = graphX + (i-1) * (graphW / 100);
    float y1 = graphY + (graphH - state.GraphData[i-1]);
    float x2 = graphX + i * (graphW / 100);
    float y2 = graphY + (graphH - state.GraphData[i]);
    DrawLine(new Point(x1, y1), new Point(x2, y2), lime, 2);
    
    // Fill under spike
    if (i % 5 == 0) {
        DrawLine(new Point(x2, y2), new Point(x2, graphY + graphH), limeDim, 1);
    }
}
DrawText("UPLINK THROUGHPUT :: 8.4 TB/S", new Point(graphX, graphY - 25), 12, lime);

// --- LAYER 3: SCROLLING HEX STRIP ---
state.HexTimer += DeltaTime;
if (state.HexTimer > 0.08f) {
    state.HexTimer = 0;
    state.HexIndex = (state.HexIndex + 1) % 50;
}

for (int i = 0; i < 50; i++) {
    string hex = ((state.HexIndex + i) * 12345).ToString("X8");
    float op = (float)i / 50.0f;
    var b = new SolidColorBrush(Color.FromArgb((byte)(op * 100), 204, 255, 0));
    DrawText(hex, new Point(Bounds.Width - 120, i * 20 + 50), 10, b);
}

// --- LAYER 4: TERMINAL CONSOLE ---
state.MsgTimer += DeltaTime;
if (state.MsgTimer > 0.15f) {
    state.MsgTimer = 0;
    string[] protos = { "TCP", "UPLINK", "KRNL", "INJECT", "MAP", "SYNC" };
    string[] stats = { "OK", "FAIL", "88%", "99%", "EXEC", "VOID" };
    state.Logs.Add($"> [{DateTime.Now:HH:mm:ss}] {protos[Rng.Next(protos.Length)]} :: {stats[Rng.Next(stats.Length)]}");
    if (state.Logs.Count > 25) state.Logs.RemoveAt(0);
}

FillRect(new Rect(50, 50, 400, 650), new SolidColorBrush(Color.Parse("#AA000000")));
DrawRect(new Rect(50, 50, 400, 650), limeMid, 1);
DrawText("PROCESS_MONITOR_V2.0", new Point(60, 60), 12, lime);

for (int i = 0; i < state.Logs.Count; i++) {
    DrawText(state.Logs[i], new Point(70, i * 20 + 90), 11, lime);
}

// --- SIGNATURE ---
var bigFont = new Typeface("Space Grotesk", FontStyle.Italic, FontWeight.Black);
var ft = new FormattedText("HACKERMAN", System.Globalization.CultureInfo.CurrentCulture, 
    FlowDirection.LeftToRight, bigFont, 56, lime);
dc.DrawText(ft, new Point(Bounds.Width - 450, Bounds.Height - 120));
DrawText("ELITE CYBER-STUDIO // DEC-77", new Point(Bounds.Width - 450, Bounds.Height - 150), 14, limeMid);

public class HudState {
    public List<float> GraphData = new();
    public List<string> Logs = new();
    public float GraphTimer = 0;
    public float MsgTimer = 0;
    public float HudRotate = 0;
    public float HexTimer = 0;
    public int HexIndex = 0;
}