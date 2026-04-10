using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace openwalls;

public static class VideoOptimizer
{
    private static readonly string OptimizedFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets");
    private static readonly ConcurrentHashSet<string> InProgress = new();
    private static readonly ConcurrentDictionary<string, Process> ActiveProcesses = new();

    public static event Action<string>? OptimizationStarted;
    public static event Action<string, double>? ProgressUpdated;
    public static event Action<string, bool>? OptimizationFinished;

    static VideoOptimizer()
    {
        if (!Directory.Exists(OptimizedFolder))
        {
            Directory.CreateDirectory(OptimizedFolder);
        }
    }

    public static (string path, bool isOptimized) GetOptimizedPath(string sourcePath)
    {
        if (string.IsNullOrEmpty(sourcePath)) return (sourcePath, false);

        string fileName = Path.GetFileNameWithoutExtension(sourcePath);
        string hash = Math.Abs(sourcePath.GetHashCode()).ToString("X");
        string optimizedPath = Path.Combine(OptimizedFolder, $"{fileName}_{hash}_hevc.mp4");

        // SMART CHECK: Only use the optimized file if it exists AND we aren't currently writing to it
        if (File.Exists(optimizedPath) && !InProgress.Contains(sourcePath))
        {
            return (optimizedPath, true);
        }

        return (optimizedPath, false);
    }

    public static void OptimizeAsync(string sourcePath)
    {
        if (InProgress.Contains(sourcePath)) return;

        Task.Run(async () =>
        {
            if (!InProgress.Add(sourcePath)) return;

            try
            {
                var (optimizedPath, exists) = GetOptimizedPath(sourcePath);
                if (exists) { OptimizationFinished?.Invoke(sourcePath, true); return; }

                OptimizationStarted?.Invoke(sourcePath);

                double durationSeconds = 0;
                try {
                    var ffprobeStartInfo = new ProcessStartInfo {
                        FileName = "ffprobe",
                        Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{sourcePath}\"",
                        RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true
                    };
                    using var ffprobe = Process.Start(ffprobeStartInfo);
                    if (ffprobe != null) {
                        string output = await ffprobe.StandardOutput.ReadToEndAsync();
                        double.TryParse(output.Trim(), out durationSeconds);
                    }
                } catch { }

                // Define encoder attempts: Hardware first, then lean software fallback
                var attempts = new List<(string name, string encoderArgs)> {
                    ("NVIDIA", "-c:v hevc_nvenc -preset p1 -tag:v hvc1"),
                    ("Intel", "-c:v hevc_qsv -preset veryfast -tag:v hvc1"),
                    ("AMD", "-c:v hevc_amf -quality speed -tag:v hvc1"),
                    ("Software", "-c:v libx265 -crf 28 -preset ultrafast -x265-params rc-lookahead=5:bframes=2:ref=1:pools=none -threads 1 -tag:v hvc1")
                };

                bool anySuccess = false;
                foreach (var (name, encoder) in attempts)
                {
                    string args = $"-i \"{sourcePath}\" {encoder} -an -y -progress - \"{optimizedPath}\"";
                    
                    var startInfo = new ProcessStartInfo {
                        FileName = "ffmpeg", Arguments = args,
                        RedirectStandardOutput = true, RedirectStandardError = true,
                        UseShellExecute = false, CreateNoWindow = true
                    };

                    using var process = Process.Start(startInfo);
                    if (process != null)
                    {
                        ActiveProcesses[sourcePath] = process;
                        try { process.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }

                        _ = Task.Run(async () => {
                            try {
                                while (!process.StandardOutput.EndOfStream) {
                                    string? line = await process.StandardOutput.ReadLineAsync();
                                    if (line == null) break;
                                    
                                    if (line.StartsWith("out_time_ms=") && durationSeconds > 0) {
                                        if (long.TryParse(line.Substring("out_time_ms=".Length), out long timeMs)) {
                                            double progress = (timeMs / 1000.0) / durationSeconds;
                                            ProgressUpdated?.Invoke(sourcePath, Math.Clamp(progress * 100.0, 0, 100));
                                        }
                                    }
                                }
                            } catch {}
                        });

                        await process.WaitForExitAsync();
                        ActiveProcesses.TryRemove(sourcePath, out _);
                        
                        if (process.ExitCode == 0 && File.Exists(optimizedPath))
                        {
                            anySuccess = true;
                            break; 
                        }
                        else if (File.Exists(optimizedPath)) 
                        {
                            File.Delete(optimizedPath); // Clean up failed partial file
                        }
                    }
                }

                OptimizationFinished?.Invoke(sourcePath, anySuccess);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Optimization failed for {sourcePath}: {ex.Message}");
                OptimizationFinished?.Invoke(sourcePath, false);
            }
            finally
            {
                InProgress.Remove(sourcePath);
                ActiveProcesses.TryRemove(sourcePath, out _);
            }
        });
    }

    public static void CancelAll()
    {
        foreach (var process in ActiveProcesses.Values)
        {
            try { if (!process.HasExited) process.Kill(true); } catch { }
        }
        ActiveProcesses.Clear();
    }
}

// Simple ConcurrentHashSet wrapper
public class ConcurrentHashSet<T> where T : notnull
{
    private readonly ConcurrentDictionary<T, byte> _dict = new();
    public bool Add(T item) => _dict.TryAdd(item, 0);
    public bool Contains(T item) => _dict.ContainsKey(item);
    public bool Remove(T item) => _dict.TryRemove(item, out _);
}
