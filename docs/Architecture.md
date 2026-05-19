# Openwalls Architecture

Openwalls is engineered to be a modular, scalable, and high-performance wallpaper engine for the Windows desktop environment. This document outlines the core technical architecture and the interaction between subsystems.

## 1. System Topology

- **Desktop Layer Integration**: Attaches the Avalonia display window natively directly into the Windows `WorkerW` background hierarchy. This ensures the wallpaper runs correctly behind desktop icons without capturing foreground window events.
- **Background Smart Pausing**: Periodically queries `user32.dll` to find overlapping fullscreen or snapped applications. Openwalls will selectively pause playback and disable heavy rendering timers when the desktop is hidden.

## 2. The Modular "Plug-and-Play" Library
All wallpapers are organized in isolated directories inside the local `wallpapers/` path or the user's `AppData/Roaming/openwalls/` location.
The `WallpaperManager` leverages a factory-like pattern to instantiate the appropriate player based on the `Type` defined in each pack's `wallpaper.json`.

Supported playback mechanisms:
- `LibVLCSharp`: Powers seamless video loop decoding for `.mp4`, `.mkv` formats.
- `Avalonia.Media.Imaging`: Renders high-fidelity static image backgrounds.
- `Clock Overlay`: An overlay window using an independent update timer mapped to system time.

## 3. Sandboxed Plugin Execution (MCP Bridge)

Procedural rendering leverages `Microsoft.CodeAnalysis.CSharp.Scripting`. Custom community wallpapers provide a `logic.cs` file which is compiled at load time into a native execution delegate (`ScriptRunner<object>`).

**Security Boundary**:
- **Static Token Inspection**: Before compilation, `ProceduralRenderer.cs` scans the `logic.cs` byte stream to detect and reject critical keywords (`System.IO`, `System.Reflection`).
- **Whitelisted Name Resolution**: The Roslyn compiler is passed explicitly referenced assemblies (`Avalonia`, `System.Linq`).

## 4. MCP Server Integration
Node.js-based AI proxy interface located at `/mcp/`. Extends typical Model Context Protocol configurations that allow third-party language models to:
1. Scaffold wallpaper directory structures.
2. Directly query the `WallpaperContext` API signatures.
3. Automatically deploy syntax-checked C# procedural codes into the workspace.
