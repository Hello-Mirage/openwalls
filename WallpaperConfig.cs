using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace openwalls;

public enum WallpaperType
{
    Gradient,
    Image,
    Video,
    Color
}

public class WallpaperPreset : INotifyPropertyChanged
{
    private bool _isActive;
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Untitled";
    public string? Path { get; set; }
    public string? Color { get; set; } // Hex Color
    public string? ThumbnailPath { get; set; } // Custom Thumbnail Path
    public WallpaperType Type { get; set; }
    public bool IsMuted { get; set; } = true;
    public DateTime DateAdded { get; set; } = DateTime.Now;

    [JsonIgnore]
    public bool IsActive 
    { 
        get => _isActive; 
        set { _isActive = value; OnPropertyChanged(); } 
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class WallpaperConfig
{
    public string? CurrentPresetId { get; set; }
    public List<WallpaperPreset> Library { get; set; } = new List<WallpaperPreset>();
    
    public WallpaperType DefaultType { get; set; } = WallpaperType.Gradient;
    public string? DefaultPath { get; set; }

    public static string ConfigPath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
        "openwalls", "config.json");
}
