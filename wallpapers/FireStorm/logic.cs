// Fire Storm Animation
// dc: DrawingContext, Bounds: Size, Time: TimeSpan, State: Dictionary<string, object>

dc.FillRectangle(Brushes.Black, new Rect(Bounds));

int particleCount = 100;
if (!State.ContainsKey("particles"))
{
    var p = new List<Point>();
    for (int i = 0; i < particleCount; i++)
        p.Add(new Point(Rng.NextDouble() * Bounds.Width, Bounds.Height + Rng.NextDouble() * 100));
    State["particles"] = p;
}

var particles = (List<Point>)State["particles"];
for (int i = 0; i < particles.Count; i++)
{
    var pt = particles[i];
    double speed = Rng.NextDouble() * 5 + 2;
    double drift = Math.Sin(Time.TotalSeconds + i) * 2;
    
    pt = new Point(pt.X + drift, pt.Y - speed);
    
    if (pt.Y < -20)
    {
        pt = new Point(Rng.NextDouble() * Bounds.Width, Bounds.Height + 50);
    }
    
    particles[i] = pt;
    
    double size = Rng.NextDouble() * 4 + 1;
    var color = Color.FromArgb(200, 255, (byte)(Rng.Next(100, 200)), 0);
    dc.DrawEllipse(new SolidColorBrush(color), null, pt, size, size);
}
