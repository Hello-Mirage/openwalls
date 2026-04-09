using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace openwalls;

public enum WallpaperType
{
    Gradient,
    Image,
    Video,
    Color,
    Procedural,
    Clock
}

public class WallpaperPreset : INotifyPropertyChanged
{
    private bool _isActive;
    private string? _path;
    private string? _color;
    private string? _thumbnailPath;

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Untitled";
    
    public string? Path 
    { 
        get => _path; 
        set { _path = value; OnPropertyChanged(); } 
    }
    
    public string? Color 
    { 
        get => _color; 
        set { _color = value; OnPropertyChanged(); } 
    }
    
    public string? ThumbnailPath 
    { 
        get => _thumbnailPath; 
        set { _thumbnailPath = value; OnPropertyChanged(); } 
    }

    public string? ProceduralId { get; set; } // ID for C# animation engine
    public string? ClockImagePath { get; set; } // Custom backdrop for Clock type
    public string? BaseDirectory { get; set; } // Local folder path for modular wallpapers
    public WallpaperType Type { get; set; }
    public bool IsMuted { get; set; } = true;
    public DateTime DateAdded { get; set; } = DateTime.Now;

    [JsonIgnore]
    public bool IsActive 
    { 
        get => _isActive; 
        set { _isActive = value; OnPropertyChanged(); } 
    }

    public string GetResourcePath(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return "";
        if (System.IO.Path.IsPathRooted(relativePath)) return relativePath;
        if (string.IsNullOrEmpty(BaseDirectory)) return relativePath;
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(BaseDirectory, relativePath));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class WallpaperConfig
{
    public string? CurrentPresetId { get; set; }
    public List<WallpaperPreset> Library { get; set; } = new List<WallpaperPreset>();
    
    public WallpaperType DefaultType { get; set; } = WallpaperType.Procedural;
    public string? DefaultPath { get; set; }

    public static string BaseDir => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "openwalls");

    public static string ConfigPath => System.IO.Path.Combine(BaseDir, "config.json");
    
    public static string LibraryDir
    {
        get
        {
            var localPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wallpapers");
            if (Directory.Exists(localPath)) return localPath;
            return System.IO.Path.Combine(BaseDir, "wallpapers");
        }
    }
}
