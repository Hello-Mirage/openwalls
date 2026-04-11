using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace openwalls;

public class WallpaperContext
{
    public DrawingContext dc { get; set; } = null!;
    public Size Bounds { get; set; }
    public TimeSpan Time { get; set; }
    public float DeltaTime { get; set; }
    public Random Rng { get; } = new();
    public Dictionary<string, object> State { get; } = new();

    public void DrawText(string text, Point pos, double size, IBrush foreground)
    {
        var ft = new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture, 
            FlowDirection.LeftToRight, Typeface.Default, size, foreground);
        dc.DrawText(ft, pos);
    }
}

public class ProceduralRenderer
{
    private readonly ProceduralCanvas _canvas;
    private DispatcherTimer? _timer;
    private DateTime _startTime;
    private DateTime _lastFrameTime;
    private ScriptRunner<object>? _scriptRunner;
    private WallpaperContext _ctx = new();
    private bool _isLoaded = false;
    private string? _securityError;
    private CancellationTokenSource? _cts;

    public ProceduralRenderer(ProceduralCanvas canvas) => _canvas = canvas;

    public void Start(WallpaperPreset preset)
    {
        Stop();
        _securityError = null;
        _isLoaded = false;
        _startTime = DateTime.Now;
        _lastFrameTime = DateTime.Now;
        
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        Task.Run(async () =>
        {
            try
            {
                var scriptPath = Path.Combine(preset.BaseDirectory ?? "", "logic.cs");
                if (!File.Exists(scriptPath)) return;

                var code = File.ReadAllText(scriptPath);
                if (token.IsCancellationRequested) return;

                // Phase 1: Static Security Scan
                if (IsMalicious(code, out string violation))
                {
                    if (token.IsCancellationRequested) return;
                    Dispatcher.UIThread.Post(() => {
                        _securityError = $"SECURITY VIOLATION: {violation}";
                        _isLoaded = true;
                        _canvas.InvalidateVisual();
                    });
                    return;
                }

                // Phase 2: Lazy Compilation 
                var runner = await RoslynCompiler.Compile(code);
                if (token.IsCancellationRequested) return;
                
                _scriptRunner = runner;
                Dispatcher.UIThread.Post(() => {
                    _isLoaded = true;
                    _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, Tick);
                    _timer.Start();
                });
            }
            catch (Exception ex)
            {
                if (token.IsCancellationRequested) return;
                Dispatcher.UIThread.Post(() => {
                    _securityError = $"COMPILATION ERROR: {ex.Message}";
                    _isLoaded = true;
                    _canvas.InvalidateVisual();
                });
            }
        }, token);
    }

    // Nested class to isolate heavy Roslyn dependencies from MainWindow JIT
    private static class RoslynCompiler
    {
        public static async Task<ScriptRunner<object>> Compile(string code)
        {
            var assemblies = new[]
            {
                typeof(object).Assembly,
                typeof(Enumerable).Assembly,
                typeof(DrawingContext).Assembly,
                typeof(ProceduralRenderer).Assembly
            };

            var references = assemblies.Select(a => {
                if (!string.IsNullOrEmpty(a.Location))
                    return MetadataReference.CreateFromFile(a.Location);

                // Fallback for Single-File / Published builds
                // Check TRUSTED_PLATFORM_ASSEMBLIES for the actual path to the DLL
                var name = a.GetName().Name + ".dll";
                var trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
                if (trustedAssemblies != null)
                {
                    var path = trustedAssemblies.Split(Path.PathSeparator)
                        .FirstOrDefault(p => p.EndsWith(name, StringComparison.OrdinalIgnoreCase));
                    if (path != null)
                        return MetadataReference.CreateFromFile(path);
                }

                throw new Exception($"Metadata for {a.GetName().Name} not found. Please ensure assemblies are available.");
            }).ToArray();

            var options = ScriptOptions.Default
                .WithReferences(references)
                .WithImports("System", "System.Linq", "System.Collections.Generic", "Avalonia", "Avalonia.Media", "openwalls");

            var script = CSharpScript.Create(code, options, typeof(WallpaperContext));
            return script.CreateDelegate();
        }
    }

    private bool IsMalicious(string code, out string violation)
    {
        violation = "";
        var blacklisted = new[] { 
            "System.IO", "File", "Directory", "Path.", "Stream",
            "System.Net", "HttpClient", "WebClient", "Socket", "Http.", "Tcp.",
            "System.Diagnostics", "Process", "Start(",
            "Reflection", "GetType", "typeof", "Assembly", "Invoke", "FieldInfo", "PropertyInfo",
            "DllImport", "extern", "unsafe", "fixed", "marshal",
            "Environment", "Registry", "WriteAll", "Delete", "Move"
        };

        foreach (var token in blacklisted)
        {
            if (code.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                violation = $"Forbidden Token Detected: '{token}'";
                return true;
            }
        }
        return false;
    }

    private void Tick(object? sender, EventArgs e) => _canvas.InvalidateVisual();

    public void Render(DrawingContext dc, Size bounds)
    {
        if (!_isLoaded) return;

        if (_securityError != null)
        {
            var brush = new SolidColorBrush(Color.Parse("#44ff0000"));
            dc.FillRectangle(brush, new Rect(bounds));
            var ft = new FormattedText(_securityError, System.Globalization.CultureInfo.CurrentCulture, 
                FlowDirection.LeftToRight, Typeface.Default, 24, Brushes.White);
            dc.DrawText(ft, new Point(50, 50));
            return;
        }

        if (_scriptRunner == null) return;

        var now = DateTime.Now;
        _ctx.dc = dc;
        _ctx.Bounds = bounds;
        _ctx.Time = now - _startTime;
        _ctx.DeltaTime = (float)(now - _lastFrameTime).TotalSeconds;
        _lastFrameTime = now;

        try
        {
            _scriptRunner(_ctx).Wait();
        }
        catch (Exception ex)
        {
            _securityError = $"RUNTIME ERROR: {ex.InnerException?.Message ?? ex.Message}";
            Stop();
            _isLoaded = true;
            _canvas.InvalidateVisual();
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        
        _timer?.Stop();
        _timer = null;
        _scriptRunner = null;
        _ctx.State.Clear();
        _isLoaded = false;
        _securityError = null;
    }
}
