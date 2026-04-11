# Openwalls AI Bridge (MCP)

The Openwalls AI Bridge allows you to connect your wallpaper engine to an AI agent (like Claude Desktop). This enables the AI to natively browse, create, and manage your modular wallpapers.

## Prerequisites

- **Node.js**: Ensure you have Node.js installed.
- **Dependencies**: Run the following in the `mcp/` directory:
  ```powershell
  cd mcp
  npm install
  ```

## 🔌 Connecting to Claude Desktop

To allow Claude to manage your wallpapers, you need to add the bridge to your Claude Desktop configuration.

1.  Open the configuration file:
    `%APPDATA%\Claude\claude_desktop_config.json`
2.  Add the following to the `mcpServers` object (ensure you update the paths to your actual installation location):

```json
{
  "mcpServers": {
    "openwalls-bridge": {
      "command": "npx",
      "args": [
        "-y",
        "tsx",
        "D:/openwalls/mcp/index.ts"
      ],
      "cwd": "D:/openwalls/mcp"
    }
  }
}
```

3.  **Restart Claude Desktop** completely.

## 🛠️ Available AI Tools

Once connected, the AI will have access to the following tools:

| Tool | Description |
| :--- | :--- |
| `list_wallpapers` | Returns a list of all installed modular wallpapers. |
| `create_wallpaper_pack` | Scaffolds a new wallpaper folder with correct metadata. |
| `edit_logic` | Writes or updates C# procedural scripts (`logic.cs`). |

## 🧪 AI Example Tasks

You can ask an AI connected via this bridge to:
- *"Show me a list of my current wallpapers."*
- *"Create a new procedural wallpaper called 'CyberFog'."*
- *"Optimize the draw loop in my MatrixCode wallpaper."*
