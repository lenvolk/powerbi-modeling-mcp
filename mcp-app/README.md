# Power BI MCP Visualizer — MCP App

Interactive visual dashboard that shows **what** the Power BI Modeling MCP Server does and **how** it works.

Built as an [MCP App](https://github.com/modelcontextprotocol/ext-apps) — a tool + HTML resource that renders inside MCP-enabled hosts like Claude Desktop.

## What It Shows

- **Overview** — Architecture diagram (Host → MCP Server → TOM Services → Power BI Model) with all 13 tool domains and their tools
- **All Tools** — Searchable table of all 65+ tools with domain, name, description, and read/write access
- **Architecture** — Three-layer design (Models → Services → Tools), transports, and dependencies
- **Connection Flow** — Step-by-step visual timeline of how `connectionId` flows through the system

## Quick Start

```bash
cd mcp-app

# Install dependencies
npm install

# Build the UI (bundles into single HTML file)
npm run build

# Start the MCP App server
npm run serve
# → Power BI MCP Visualizer listening on http://localhost:3001/mcp
```

## Testing with basic-host

```bash
# Clone the MCP Apps SDK
git clone --depth 1 https://github.com/modelcontextprotocol/ext-apps.git /tmp/mcp-ext-apps

# Terminal 1: Run this server
cd mcp-app && npm run dev

# Terminal 2: Run the basic-host
cd /tmp/mcp-ext-apps/examples/basic-host
npm install
SERVERS='["http://localhost:3001/mcp"]' npm run start
# Open http://localhost:8080
```

## Project Structure

```
mcp-app/
├── server.ts          # MCP server — registerAppTool + registerAppResource
├── src/
│   ├── index.html     # HTML shell with CSS
│   └── mcp-app.ts     # Client-side App lifecycle + rendering
├── dist/              # Built single-file HTML (generated)
├── vite.config.ts     # Vite + vite-plugin-singlefile
├── tsconfig.json
└── package.json
```

## How It Works

1. **Tool** (`powerbi_mcp_dashboard`) — Called by the LLM/host. Returns a structured JSON catalog of all tool domains, architecture info, and connection flow. Also includes a Markdown text fallback for non-UI hosts.

2. **Resource** (`ui://powerbi-mcp-visualizer/dashboard`) — Serves the bundled single-file HTML (built by Vite). The HTML contains the full interactive dashboard.

3. **Link** — The tool's `_meta.ui.resourceUri` points to the resource. When a host calls the tool, it renders the resource and passes the tool result as `structuredContent` to the App.

4. **App** — The client-side `mcp-app.ts` uses the MCP Apps SDK `App` class to receive `ontoolresult`, parse the structured content, and render the dashboard with tabs, cards, and diagrams.
