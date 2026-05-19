import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const PROJECT_ROOT = path.resolve(__dirname, "..");
const WALLPAPERS_DIR = path.resolve(PROJECT_ROOT, "wallpapers");
const ASSETS_DIR = path.resolve(PROJECT_ROOT, "assets");

const server = new McpServer({
  name: "openwalls-bridge",
  version: "1.1.0",
});

// RESOURCE: Rules of the Lab
server.resource(
  "rules-of-the-lab",
  "api://rules",
  async () => {
    return {
      contents: [{
        uri: "api://rules",
        mimeType: "text/markdown",
        text: "# AI Bridge Rules\n\n1. **PROCEDURAL vs CLOCK**: If you are writing C# logic (glitches, glows, animations), you MUST set `type: 'Procedural'`. Use `type: 'Clock'` ONLY for static background images with a simple time overlay.\n2. **AI IDENTIFICATION**: Always ensure `IsAiGenerated: true` is set in the metadata so your creations appear in the AI Studio tab.\n3. **ITERATION**: Use `create_wallpaper_pack` first, then `edit_logic` for code, then `update_wallpaper_metadata` for final polish (colors, themes)."
      }]
    };
  }
);

// RESOURCE: API Documentation
server.resource(
  "api-docs",
  "api://docs",
  async () => {
    const docs = fs.readFileSync(path.resolve(PROJECT_ROOT, "MakeforOW.md"), "utf-8");
    return {
      contents: [{
        uri: "api://docs",
        mimeType: "text/markdown",
        text: docs
      }]
    };
  }
);

// TOOL: List Wallpapers
server.tool(
  "list_wallpapers",
  "Lists all modular wallpaper folders in the library",
  {},
  async () => {
    const folders = fs.readdirSync(WALLPAPERS_DIR);
    return {
      content: [{ type: "text", text: `Wallpapers found: ${folders.join(", ")}` }]
    };
  }
);

// TOOL: Create Wallpaper Pack
server.tool(
  "create_wallpaper_pack",
  "Scaffolds a new modular wallpaper folder. IMPORTANT: Use 'Procedural' type if you intend to write custom C# logic scripts.",
  {
    name: z.string().describe("Name of the wallpaper (e.g., 'NeonCity')"),
    type: z.enum(["Video", "Image", "Procedural", "Clock"]).describe("The wallpaper type. Use 'Procedural' for custom code logic.")
  },
  async ({ name, type }) => {
    const folderName = name.replace(/\s+/g, "_");
    const targetDir = path.resolve(WALLPAPERS_DIR, folderName);
    
    if (fs.existsSync(targetDir)) {
      return { content: [{ type: "text", text: `Error: Wallpaper '${name}' already exists.` }] };
    }

    fs.mkdirSync(targetDir, { recursive: true });

    // Copy default asset
    const defaultAsset = path.resolve(ASSETS_DIR, "samurai-warrior-observing-village-moonlight.jpg");
    if (fs.existsSync(defaultAsset)) {
      fs.copyFileSync(defaultAsset, path.resolve(targetDir, "backdrop.jpg"));
    }

    const config = {
      Id: Math.random().toString(36).substring(2, 9),
      Name: name,
      Type: type,
      Path: "backdrop.jpg",
      ClockImagePath: type === "Clock" ? "backdrop.jpg" : undefined,
      IsAiGenerated: true
    };

    fs.writeFileSync(path.resolve(targetDir, "wallpaper.json"), JSON.stringify(config, null, 2));

    return {
      content: [{ type: "text", text: `Successfully created ${type} wallpaper: ${name} in ${folderName}/` }]
    };
  }
);

// TOOL: Edit Logic
server.tool(
  "edit_logic",
  "Writes or updates the C# logic script for a procedural wallpaper",
  {
    folder: z.string().describe("The name of the wallpaper folder"),
    code: z.string().describe("The C# logic script content")
  },
  async ({ folder, code }) => {
    const targetDir = path.resolve(WALLPAPERS_DIR, folder);
    if (!fs.existsSync(targetDir)) {
      return { content: [{ type: "text", text: "Error: Folder not found." }] };
    }

    // Security Check: Forbidden tokens
    const forbidden = ["System.IO", "System.Net", "Process", "Reflection", "DllImport"];
    const violation = forbidden.find(token => code.includes(token));
    if (violation) {
      return { content: [{ type: "text", text: `SECURITY ALERT: Code contains forbidden token '${violation}'. Save aborted.` }] };
    }

    fs.writeFileSync(path.resolve(targetDir, "logic.cs"), code);
    return {
      content: [{ type: "text", text: `Successfully updated logic.cs for ${folder}.` }]
    };
  }
);

// TOOL: Update Wallpaper Metadata
server.tool(
  "update_wallpaper_metadata",
  "Updates properties in a wallpaper's wallpaper.json (e.g., font colors, sizes, or paths)",
  {
    folder: z.string().describe("The name of the wallpaper folder"),
    updatesJson: z.string().describe("A JSON string of properties to update (e.g., '{\"ClockFontColor\": \"#00FF00\"}')")
  },
  async ({ folder, updatesJson }) => {
    let updates;
    try {
      updates = JSON.parse(updatesJson);
    } catch (e) {
      return { content: [{ type: "text", text: "Error: Invalid JSON format for updates." }] };
    }
    const targetDir = path.resolve(WALLPAPERS_DIR, folder);
    const configPath = path.resolve(targetDir, "wallpaper.json");
    
    if (!fs.existsSync(configPath)) {
      return { content: [{ type: "text", text: "Error: wallpaper.json not found." }] };
    }

    const currentConfig = JSON.parse(fs.readFileSync(configPath, "utf-8"));
    const updatedConfig = { ...currentConfig, ...updates };

    fs.writeFileSync(configPath, JSON.stringify(updatedConfig, null, 2));

    return {
      content: [{ type: "text", text: `Successfully updated metadata for ${folder}.` }]
    };
  }
);

// TOOL: Delete Wallpaper
server.tool(
  "delete_wallpaper",
  "Deletes a wallpaper folder from the library. Use this to clean up failed creations.",
  {
    folder: z.string().describe("The name of the wallpaper folder to delete")
  },
  async ({ folder }) => {
    const targetDir = path.resolve(WALLPAPERS_DIR, folder);
    if (!fs.existsSync(targetDir)) {
      return { content: [{ type: "text", text: "Error: Folder not found." }] };
    }

    fs.rmSync(targetDir, { recursive: true, force: true });
    return {
      content: [{ type: "text", text: `Successfully deleted wallpaper: ${folder}` }]
    };
  }
);

// TOOL: Read Wallpaper Logic
server.tool(
  "read_wallpaper_logic",
  "Reads the C# logic script of an existing wallpaper. Use this to learn from existing patterns.",
  {
    folder: z.string().describe("The name of the wallpaper folder")
  },
  async ({ folder }) => {
    const filePath = path.resolve(WALLPAPERS_DIR, folder, "logic.cs");
    if (!fs.existsSync(filePath)) {
      return { content: [{ type: "text", text: "Error: logic.cs not found in this folder." }] };
    }
    const code = fs.readFileSync(filePath, "utf-8");
    return {
      content: [{ type: "text", text: code }]
    };
  }
);

// TOOL: Read Wallpaper Config
server.tool(
  "read_wallpaper_config",
  "Reads the wallpaper.json configuration of an existing wallpaper.",
  {
    folder: z.string().describe("The name of the wallpaper folder")
  },
  async ({ folder }) => {
    const filePath = path.resolve(WALLPAPERS_DIR, folder, "wallpaper.json");
    if (!fs.existsSync(filePath)) {
      return { content: [{ type: "text", text: "Error: wallpaper.json not found in this folder." }] };
    }
    const json = fs.readFileSync(filePath, "utf-8");
    return {
      content: [{ type: "text", text: json }]
    };
  }
);

// TOOL: Get AI Studio Contents
server.tool(
  "get_ai_studio_contents",
  "Returns a list of all wallpapers specifically tagged as AI-generated. Use this to discover your creations.",
  {},
  async () => {
    const folders = fs.readdirSync(WALLPAPERS_DIR);
    const aiWallpapers = [];

    for (const folder of folders) {
      const configPath = path.resolve(WALLPAPERS_DIR, folder, "wallpaper.json");
      if (fs.existsSync(configPath)) {
        try {
          const config = JSON.parse(fs.readFileSync(configPath, "utf-8"));
          if (config.IsAiGenerated) {
            aiWallpapers.push({ folder, name: config.Name, type: config.Type });
          }
        } catch (e) {}
      }
    }

    return {
      content: [{ type: "text", text: `AI Studio Wallpapers: ${JSON.stringify(aiWallpapers, null, 2)}` }]
    };
  }
);

async function main() {
  const transport = new StdioServerTransport();
  await server.connect(transport);
  console.error("Openwalls AI Bridge running...");
}

main().catch(console.error);
