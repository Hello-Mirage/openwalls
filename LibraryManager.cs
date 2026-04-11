using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace openwalls;

public class LibraryManager
{
    private WallpaperConfig _config = new();
    private ObservableCollection<WallpaperPreset> _library = new();

    public WallpaperConfig Config => _config;
    public ObservableCollection<WallpaperPreset> Library => _library;

    public void LoadLibrary()
    {
        EnsureDirectories();
        LoadConfig();

        var allPresets = ScanForPresets();
        
        if (SyncRegistryWithLibrary(allPresets))
        {
            LoadLibrary(); // Re-scan if something was added
            return;
        }

        _library = new ObservableCollection<WallpaperPreset>(allPresets.OrderByDescending(p => p.DateAdded));
        _config.Library = allPresets;

        RefreshActiveState();
    }

    private void EnsureDirectories()
    {
        try
        {
            if (!Directory.Exists(WallpaperConfig.BaseDir)) Directory.CreateDirectory(WallpaperConfig.BaseDir);
            if (!Directory.Exists(WallpaperConfig.LibraryDir)) Directory.CreateDirectory(WallpaperConfig.LibraryDir);
            GetUserLibraryDir();
        }
        catch { }
    }

    private void LoadConfig()
    {
        if (File.Exists(WallpaperConfig.ConfigPath))
        {
            try
            {
                string json = File.ReadAllText(WallpaperConfig.ConfigPath);
                _config = JsonConvert.DeserializeObject<WallpaperConfig>(json) ?? new WallpaperConfig();
            }
            catch { _config = new WallpaperConfig(); }
        }
    }

    private List<WallpaperPreset> ScanForPresets()
    {
        var allPresets = new List<WallpaperPreset>();
        if (Directory.Exists(WallpaperConfig.LibraryDir))
        {
            var metaFiles = Directory.GetFiles(WallpaperConfig.LibraryDir, "wallpaper.json", SearchOption.AllDirectories);
            foreach (var metaPath in metaFiles)
            {
                try
                {
                    var preset = JsonConvert.DeserializeObject<WallpaperPreset>(File.ReadAllText(metaPath));
                    if (preset != null)
                    {
                        preset.BaseDirectory = Path.GetDirectoryName(metaPath);
                        allPresets.Add(preset);
                    }
                }
                catch { }
            }
        }
        return allPresets;
    }

    public void SaveConfig()
    {
        try
        {
            string json = JsonConvert.SerializeObject(_config, Formatting.Indented);
            File.WriteAllText(WallpaperConfig.ConfigPath, json);
        }
        catch { }
    }

    public string GetUserLibraryDir()
    {
        string pcName = Environment.MachineName.Replace(" ", "_");
        string path = Path.Combine(WallpaperConfig.LibraryDir, "user", pcName);
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        return path;
    }

    public void AddColor(string hex)
    {
        var preset = new WallpaperPreset { Name = $"Color {hex}", Color = hex, Type = WallpaperType.Color };
        var folder = Path.Combine(GetUserLibraryDir(), $"Color_{hex.Replace("#", "")}");
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "wallpaper.json"), JsonConvert.SerializeObject(preset, Formatting.Indented));
        LoadLibrary();
    }

    public void AddFile(string path)
    {
        var ext = Path.GetExtension(path).ToLower();
        var type = (ext == ".mp4" || ext == ".mkv" || ext == ".mov" || ext == ".avi") ? WallpaperType.Video : WallpaperType.Image;
        
        var name = Path.GetFileNameWithoutExtension(path);
        var folder = Path.Combine(GetUserLibraryDir(), name.Replace(" ", "_") + "_" + Guid.NewGuid().ToString().Substring(0, 4));
        Directory.CreateDirectory(folder);

        var destFile = "backdrop" + ext;
        File.Copy(path, Path.Combine(folder, destFile), true);

        var preset = new WallpaperPreset { Name = name, Path = destFile, Type = type };
        File.WriteAllText(Path.Combine(folder, "wallpaper.json"), JsonConvert.SerializeObject(preset, Formatting.Indented));
    }

    public void DeletePreset(WallpaperPreset preset)
    {
        if (Directory.Exists(preset.BaseDirectory))
        {
            try { Directory.Delete(preset.BaseDirectory, true); } catch { }
        }
        LoadLibrary();
    }

    public void RefreshActiveState()
    {
        foreach (var p in _library) p.IsActive = (p.Id == _config.CurrentPresetId);
    }

    private bool SyncRegistryWithLibrary(List<WallpaperPreset> existing)
    {
        bool added = false;
        try
        {
            var registryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wallpapers/registry.json");
            if (!File.Exists(registryPath)) return false;

            string json = File.ReadAllText(registryPath);
            var stock = JsonConvert.DeserializeObject<List<WallpaperPreset>>(json);
            if (stock == null) return false;

            foreach (var d in stock)
            {
                bool exists = existing.Any(p => 
                    p.Id.Equals(d.Id, StringComparison.OrdinalIgnoreCase) || 
                    p.Name.Equals(d.Name, StringComparison.OrdinalIgnoreCase));

                if (exists) continue;

                var sanitizedName = d.Name.Replace(" ", "");
                var folder = Path.Combine(WallpaperConfig.LibraryDir, sanitizedName);
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                
                d.BaseDirectory = folder;
                BootstrapAssets(d, folder, sanitizedName);

                File.WriteAllText(Path.Combine(folder, "wallpaper.json"), JsonConvert.SerializeObject(d, Formatting.Indented));
                added = true;
            }
        }
        catch { }
        return added;
    }

    private void BootstrapAssets(WallpaperPreset d, string folder, string sanitizedName)
    {
        if (d.Type == WallpaperType.Clock)
        {
            var src = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets/samurai-warrior-observing-village-moonlight.jpg");
            if (File.Exists(src)) File.Copy(src, Path.Combine(folder, "samurai.jpg"), true);

            var currentThumbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"wallpapers/{sanitizedName}/thumbnail.png");
            var thumbDest = Path.Combine(folder, "thumbnail.png");
            
            if (File.Exists(currentThumbPath)) File.Copy(currentThumbPath, thumbDest, true);
            else if (File.Exists(src)) File.Copy(src, thumbDest, true);
        }
        else if (d.Type == WallpaperType.Procedural)
        {
            var srcLogic = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"wallpapers/{sanitizedName}/logic.cs");
            if (File.Exists(srcLogic)) File.Copy(srcLogic, Path.Combine(folder, "logic.cs"), true);
            
            var srcThumb = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"wallpapers/{sanitizedName}/thumbnail.png");
            if (File.Exists(srcThumb)) File.Copy(srcThumb, Path.Combine(folder, "thumbnail.png"), true);
        }
    }
}
