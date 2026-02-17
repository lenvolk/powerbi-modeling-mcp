import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
  ListResourcesRequestSchema,
  ReadResourceRequestSchema,
} from "@modelcontextprotocol/sdk/types.js";
import { readFileSync, existsSync } from "fs";
import { fileURLToPath } from "url";
import { dirname, join } from "path";

// ── Load MCP App HTML ────────────────────────────────────────────────────────
const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

let appHtml: string | null = null;

// Built by Vite to build/src/mcp-app.html
const htmlPath = join(__dirname, "src", "mcp-app.html");
if (existsSync(htmlPath)) {
  try {
    appHtml = readFileSync(htmlPath, "utf-8");
    console.error("MCP App HTML loaded successfully from:", htmlPath);
  } catch (error) {
    console.error("Warning: Could not load mcp-app.html. MCP App not available.");
    console.error("Build the app with: npm run build:app");
  }
} else {
  console.error("Warning: mcp-app.html not found at", htmlPath);
  console.error("Build the app with: npm run build:app");
}

// ── MCP App resource URI ─────────────────────────────────────────────────────
const MCP_APP_RESOURCE_URI = "powerbi://app/dashboard";

// ── Server setup ─────────────────────────────────────────────────────────────
const server = new Server(
  { name: "powerbi-mcp-visualizer", version: "1.0.0" },
  { capabilities: { tools: {}, resources: {} } }
);

// ── List tools ───────────────────────────────────────────────────────────────
server.setRequestHandler(ListToolsRequestSchema, async () => {
  return {
    tools: [
      // MCP App tool — only included if HTML is built
      ...(appHtml
        ? [
            {
              name: "powerbi_mcp_dashboard",
              description:
                "Interactive visual dashboard showing all 62 tools, parameters, architecture, and connection flow of the Power BI Modeling MCP Server. Click on any tool to see its full parameter schema.",
              inputSchema: {
                type: "object" as const,
                properties: {},
                required: [],
              },
              _meta: {
                ui: {
                  resourceUri: MCP_APP_RESOURCE_URI,
                  visibility: ["model", "app"],
                },
              },
            },
          ]
        : []),
    ],
  };
});

// ── Handle tool calls ────────────────────────────────────────────────────────
server.setRequestHandler(CallToolRequestSchema, async (request) => {
  const { name } = request.params;

  if (name === "powerbi_mcp_dashboard") {
    return {
      content: [
        {
          type: "text" as const,
          text: "Launching Power BI MCP Dashboard interactive UI...",
        },
      ],
    };
  }

  return {
    content: [{ type: "text" as const, text: `Unknown tool: ${name}` }],
    isError: true,
  };
});

// ── List resources ───────────────────────────────────────────────────────────
server.setRequestHandler(ListResourcesRequestSchema, async () => {
  return {
    resources: [
      ...(appHtml
        ? [
            {
              uri: MCP_APP_RESOURCE_URI,
              mimeType: "text/html",
              name: "Power BI MCP Dashboard",
              description:
                "Interactive visual dashboard for the Power BI Modeling MCP Server",
            },
          ]
        : []),
    ],
  };
});

// ── Read resources ───────────────────────────────────────────────────────────
server.setRequestHandler(ReadResourceRequestSchema, async (request) => {
  const { uri } = request.params;

  if (uri === MCP_APP_RESOURCE_URI) {
    if (!appHtml) {
      throw new Error(
        "MCP App HTML not available. Build the app with: npm run build:app"
      );
    }
    return {
      contents: [
        {
          uri,
          mimeType: "text/html",
          text: appHtml,
        },
      ],
    };
  }

  throw new Error(`Unknown resource: ${uri}`);
});

// ── Start stdio transport ────────────────────────────────────────────────────
async function main() {
  const transport = new StdioServerTransport();
  await server.connect(transport);
  console.error("Power BI MCP Visualizer running on stdio");
}

main().catch((error) => {
  console.error("Server error:", error);
  process.exit(1);
});
