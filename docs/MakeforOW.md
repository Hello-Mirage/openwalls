# Contributing to Openwalls

Welcome to the Openwalls Community! This guide will teach you how to create "Plug-and-Play" wallpapers that can be shared with anyone in the world.

## The Modular Structure

Openwalls uses a Modular Folder System. This means every wallpaper is a single folder. 

### Your Wallpaper Folder
```text
/MyCoolWallpaper/
├── logic.cs              # Your C# script (for Procedural types)
├── backdrop.mp4          # Your video or image file
└── thumbnail.png         # The icon shown in the dashboard
```

---

## 🚀 The AI/MCP Workflow (True Plug-and-Play)

If you are an AI agent (MCP) or a developer adding a new wallpaper to the build, follow these rules to ensure zero code-base interference:

1.  **Create the Folder**: Add your wallpaper folder to `wallpapers/`.
2.  **Add Logic**: For procedural wallpapers, create your `logic.cs` inside that folder.
3.  **Register the Asset**: Add a single entry to `wallpapers/registry.json`.
    -   **DO NOT EDIT `SettingsWindow.axaml.cs`**. The application will automatically bootstrap and "heal" your folder metadata based on this registry entry.
    -   The `Id` in the registry should be unique and lowercase-kebab-case.

### Example Registry Entry:
```json
{
  "Id": "my-cool-wallpaper",
  "Name": "My Cool Wallpaper",
  "Type": "Procedural",
  "ThumbnailPath": "thumbnail.png"
}
```

---

## The wallpaper.json Schema (Local Metadata)

While the registry handles the initial bootstrap, each folder also contains a `wallpaper.json` which persists local settings.

```json
{
  "Id": "unique-id-here",  
  "Name": "Neon Nights",
  "Type": "Video",
  "Path": "backdrop.mp4",
  "ThumbnailPath": "thumbnail.png",
  "IsMuted": true
}
```

### Supported Types:
| Type | Description |
| :--- | :--- |
| Video | Plays a looping video file (.mp4, .mkv, .mov). |
| Image | Displays a static high-res image (.jpg, .png). |
| Clock | Displays the current time over a custom image. |
| Procedural | Uses a logic.cs file for custom C# animations. |

---

## Scripting Guide (Advanced)

If you set your type to Procedural, the engine will look for a logic.cs file in your folder. 

### The logic.cs Template
Your script has access to a WallpaperContext object (implicit globals). 

```csharp
// Use dc for drawing, Bounds for screen size, and Time for animations.
dc.FillRectangle(Brushes.Black, new Rect(Bounds));

// Use State to store variables between frames
if (!State.ContainsKey("myCount")) State["myCount"] = 0;
int count = (int)State["myCount"];

DrawText("Hello Modular World!", new Point(100, 100), 40, Brushes.White);
State["myCount"] = count + 1;
```

### Available API:
- dc: The Avalonia DrawingContext.
- Bounds: The Size of the screen.
- Time: A TimeSpan of how long the wallpaper has been running.
- DeltaTime: Time since the last frame.
- State: A Dictionary<string, object> to persist variables.
- Rng: A Random instance.
- DrawText(string, Point, double, IBrush): A helper for text.

---

## Security and Sandboxing

Openwalls runs all procedural scripts in a **Hardened Sandbox**.

### Forbidden Tokens:
- **Files & OS**: `System.IO`, `File`, `Directory`, `Process`.
- **Network**: `System.Net`, `HttpClient`, `Socket`.
- **Reflection**: `System.Reflection`, `GetType`, `typeof`, `Assembly`.
- **Unsafe**: `DllImport`, `unsafe`, `fixed`.

---

## Best Practices
1. **No Internal Changes**: Never modify `SettingsWindow.axaml.cs` to add presets. Use `registry.json`.
2. **File Size**: Keep video backgrounds under 100MB.
3. **Thumbnails**: Always provide a `thumbnail.png` (400px wide).
4. **No Absolute Paths**: The engine handles relocation automatically.

Happy Creating!
