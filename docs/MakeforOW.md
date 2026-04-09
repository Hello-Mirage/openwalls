# Contributing to Openwalls

Welcome to the Openwalls Community! This guide will teach you how to create "Plug-and-Play" wallpapers that can be shared with anyone in the world.

## The Modular Structure

Openwalls uses a Modular Folder System. This means every wallpaper is a single folder. To share it, you just zip the folder. To install it, the user just drops the folder into their library.

### Your Wallpaper Folder
```text
/MyCoolWallpaper/
├── wallpaper.json        # The heart of your wallpaper (metadata)
├── backdrop.mp4          # Your video or image file
└── thumbnail.jpg         # The icon shown in the dashboard
```

---

## The wallpaper.json Schema

This file tells the engine what your wallpaper is and how to play it.

```json
{
  "Id": "unique-id-here",  
  "Name": "Neon Nights",
  "Type": "Video",
  "Path": "backdrop.mp4",
  "ThumbnailPath": "thumbnail.jpg",
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

If you set your type to Procedural, the engine will look for a logic.cs file in your folder. This is where you can write custom C# code to draw anything!

### The logic.cs Template
Your script has access to a WallpaperContext object (implicit globals). 

```csharp
// Use dc for drawing, Bounds for screen size, and Time for animations.
dc.FillRectangle(Brushes.Black, new Rect(Bounds));

// Use State to store variables between frames
int count;
if (!State.ContainsKey("myCount")) {
    count = 0;
} else {
    count = (int)State["myCount"];
}

DrawText("Hello Modular World!", new Point(100, 100), 40, Brushes.White);
State["myCount"] = count + 1;
```

### Available API:
- dc: The Avalonia DrawingContext.
- Bounds: The Size of the screen.
- Time: A TimeSpan of how long the wallpaper has been running.
- DeltaTime: Time since the last frame (useful for physics).
- State: A Dictionary<string, object> to persist variables between frames.
- Rng: A Random instance for variety.
- DrawText(string, Point, double, IBrush): A helper for fast text rendering.

---

## Security and Sandboxing

To ensure the safety of our community, Openwalls runs all procedural scripts in a **Hardened Sandbox**.

### Pre-Execution Scanning
Before your script is compiled, the engine scans the code for "Forbidden Tokens." If your script attempted to access any of the following, it will be blocked:
- **Files & OS**: `System.IO`, `File`, `Directory`, `Process`.
- **Network**: `System.Net`, `HttpClient`, `Socket`.
- **Stealth**: `System.Reflection`, `GetType`, `DllImport`.

### Assembly Whitelisting
The engine only loads a limited set of drawing and math libraries. Even if you don't use the forbidden keywords, trying to use an unlisted assembly will result in a compilation error.

**If your wallpaper is blocked:** Check your `logic.cs` for any forbidden tokens and ensure you are only using the provided `WallpaperContext` API for your animations.

---

## Best Practices for Creators

1. Resolution: Design for 1920x1080 or 3840x2160.
2. Video Looping: Ensure your video has a seamless loop. Avoid abrupt cuts.
3. File Size: Keep video backgrounds under 100MB for smooth performance on all systems.
4. Thumbnail: Always provide a thumbnail.jpg (approx 400px wide) so your wallpaper looks premium in the dashboard.
5. No Absolute Paths: Never use C:\Users\... in your wallpaper.json. Always use just the filename. The engine handles the rest!

---

## Sharing Your Work

1. Right-click your wallpaper folder.
2. Select Compress to ZIP file.
3. Share the ZIP on the OWMarketplace!

Happy Creating!
