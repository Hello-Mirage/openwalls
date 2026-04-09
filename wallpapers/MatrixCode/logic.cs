// Openwalls Community Shader: Nuclear Matrix
// A high-performance digital rain effect with CJK support.

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using openwalls;

// Entry point logic
MatrixState state;
if (!State.ContainsKey("matrix")) {
    state = new MatrixState();
    int cols = (int)(Bounds.Width / 20) + 1;
    for (int i = 0; i < cols; i++) {
        state.ColumnHeads.Add(Rng.Next(-(int)Bounds.Height, 0));
    }
    State["matrix"] = state;
} else {
    state = (MatrixState)State["matrix"];
}

dc.FillRectangle(Brushes.Black, new Rect(Bounds));

var chars = "ﾊﾐﾋｰｳｼﾅﾓﾆｻﾜﾂｵﾘｱﾎﾃﾏｹﾒｴｶｷﾑﾕﾗｾﾈｽﾀﾇﾍ0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZアイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロ".ToCharArray();

for (int i = 0; i < state.ColumnHeads.Count; i++) {
    float y = state.ColumnHeads[i];
    int charCount = (int)(y / 20);
    
    // Draw trail
    for (int j = 0; j < 20; j++) {
        int idx = charCount - j;
        if (idx < 0) continue;
        
        float opacity = 1.0f - (j / 20.0f);
        var brush = new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), 0, 255, 70));
        
        char c = chars[Rng.Next(chars.Length)];
        DrawText(c.ToString(), new Point(i * 20, idx * 20), 16, brush);
    }
    
    // Draw bright head
    DrawText(chars[Rng.Next(chars.Length)].ToString(), new Point(i * 20, charCount * 20), 16, Brushes.White);
    
    state.ColumnHeads[i] += 5 + (float)Rng.NextDouble() * 10;
    if (state.ColumnHeads[i] > Bounds.Height + 400) {
        state.ColumnHeads[i] = -200;
    }
}

public class MatrixState {
    public List<float> ColumnHeads = new();
}
