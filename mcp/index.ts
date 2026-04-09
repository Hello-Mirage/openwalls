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
  version: "1.0.0",
});

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
  "Scaffolds a new modular wallpaper folder with a default samurai backdrop",
  {
    name: z.string().describe("Name of the wallpaper (e.g., 'NeonCity')"),
    type: z.enum(["Video", "Image", "Procedural", "Clock"]).describe("The wallpaper rendering type")
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
      ClockImagePath: type === "Clock" ? "backdrop.jpg" : undefined
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

async function main() {
  const transport = new StdioServerTransport();
  await server.connect(transport);
  console.error("Openwalls AI Bridge running...");
}

main().catch(console.error);
