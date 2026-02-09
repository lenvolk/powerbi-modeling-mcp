# Power BI Modeling MCP Server (HTTP Demo)

Open-source C#/.NET 8 MCP server for Power BI semantic models with **stdio + HTTP transport** support. Built from scratch to demonstrate how to extend the [official Power BI Modeling MCP](https://github.com/microsoft/powerbi-modeling-mcp) with HTTP(S) connectivity for remote and multi-client scenarios.

## Quick Start

**Prerequisites:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### 1. Build

```bash
git clone https://github.com/microsoft/powerbi-modeling-mcp.git -b demo
cd powerbi-modeling-mcp
dotnet build src/PowerBiMcpServer
```

### 2. Start the server (HTTP mode)

```bash
dotnet run --project src/PowerBiMcpServer -- --transport http --port 5100
```

The server runs in the foreground. Keep this terminal open. You should see output on stderr confirming:

```
Power BI MCP Server listening on http://127.0.0.1:5100/mcp
Health endpoint: http://127.0.0.1:5100/healthz
```

### 3. Verify it's running

Open a second terminal and check the health endpoint:

```bash
curl http://localhost:5100/healthz
```

Expected response:

```json
{"status":"healthy","transport":"http","version":"1.0.0","connections":0}
```

### 4. Connect VS Code

This repo includes a `.vscode/mcp.json` that points to the running server. Open the workspace in VS Code with [GitHub Copilot](https://marketplace.visualstudio.com/items?itemName=GitHub.copilot) installed, then restart the MCP server from the Command Palette (**MCP: List Servers** > **powerbi-mcp-server** > **Restart Server**). You should see "46 tools" discovered.

> **Important:** Unlike stdio mode, the HTTP server must be started manually *before* VS Code can connect. If you see a "fetch failed" error, make sure the server is running (step 2).

## VS Code MCP Configuration

The `.vscode/mcp.json` in this repo is pre-configured for HTTP mode:

```jsonc
{
  "servers": {
    // HTTP transport — connect to the server running on localhost
    // Start the server first: dotnet run --project src/PowerBiMcpServer -- --transport http --port 5100
    "powerbi-mcp-server": {
      "type": "sse",
      "url": "http://localhost:5100/mcp"
    }

    // stdio transport — launches the server as a child process (uncomment to use instead)
    // "powerbi-mcp-server": {
    //   "type": "stdio",
    //   "command": "dotnet",
    //   "args": [
    //     "run",
    //     "--project",
    //     "${workspaceFolder}/src/PowerBiMcpServer/PowerBiMcpServer.csproj"
    //   ]
    // }
  }
}
```

To switch to stdio mode (server launches automatically with VS Code), comment out the SSE block and uncomment the stdio block.

## CLI Options

| Flag                 | Default     | Description                                   |
| -------------------- | ----------- | --------------------------------------------- |
| `--transport`        | `stdio`     | Transport mode: `stdio` or `http`             |
| `--host`             | `127.0.0.1` | Bind address (HTTP mode)                      |
| `--port`             | `5100`      | Listen port (HTTP mode)                       |
| `--readonly`         | off         | Prevent all write operations                  |
| `--skipconfirmation` | off         | Skip write-operation confirmation prompts     |
| `--compatibility`    | `PowerBI`   | Set to `Full` for Analysis Services databases |

**Environment variable:** `PBI_MODELING_MCP_ACCESS_TOKEN` — provide a token for Fabric workspace auth instead of interactive login.

## Available Tools

Each tool is a single action (one-tool-per-action design, simpler for LLMs than mega-tools). Grouped by domain:

| Domain           | Tools                                                                         |
| ---------------- | ----------------------------------------------------------------------------- |
| **connection**   | `connect_desktop`, `connect_fabric`, `open_pbip`, `list`, `disconnect`        |
| **model**        | `get`, `get_stats`, `rename`, `refresh`                                       |
| **table**        | `list`, `get`, `create`, `delete`, `rename`, `update`                         |
| **column**       | `list`, `get`, `create`, `update`, `delete`, `rename`                         |
| **measure**      | `list`, `get`, `create`, `update`, `delete`, `rename`, `move`                 |
| **relationship** | `list`, `create`, `delete`, `activate`, `find`                                |
| **dax**          | `query`, `evaluate_measure`, `info_tables`, `info_relationships`, `info_measures`, `preview_table`, `distinct_values` |
| **tmdl**         | `export`, `import`, `read_folder`, `read_file`, `list_files`, `pbip_discover` |

### Usage Flow

1. Connect: `connection_connect_desktop`, `connection_connect_fabric`, or `connection_open_pbip`
2. Use the returned `connectionId` with any other tool
3. Disconnect when done: `connection_disconnect`

## Architecture

```
src/PowerBiMcpServer/
├── Program.cs              # Entry point — stdio or HTTP transport
├── Models/
│   ├── ConnectionInfo.cs   # Connection metadata + ConnectionKind enum
│   └── ServerSettings.cs   # CLI flags (readonly, compatibility, etc.)
├── Services/
│   ├── ConnectionManager.cs         # Thread-safe TOM connection pool + DAX execution
│   ├── PowerBiDesktopDiscovery.cs   # Windows PBI Desktop instance detection
│   └── TmdlService.cs              # TMDL folder serialize/deserialize
└── Tools/
    ├── ConnectionTools.cs   # Connect/disconnect/list
    ├── TableTools.cs        # Table CRUD
    ├── ColumnTools.cs       # Column CRUD
    ├── MeasureTools.cs      # Measure CRUD + move
    ├── RelationshipTools.cs # Relationship CRUD + activate
    ├── DaxQueryTools.cs     # DAX execution + DMV queries
    ├── ModelTools.cs        # Model info/stats/refresh/rename
    └── TmdlTools.cs         # TMDL/PBIP file operations
```

## Differences from Official Microsoft MCP

| Aspect     | Official                                   | This Demo                         |
| ---------- | ------------------------------------------ | --------------------------------- |
| Transport  | stdio only                                 | stdio + HTTP(S)                   |
| Source     | Closed-source binary                       | Open-source C#                    |
| Tool style | ~21 mega-tools with `operation` parameter  | 46 individual one-tool-per-action |
| Runtime    | Pre-compiled platform binary               | `dotnet run` (.NET 8 SDK)         |

## License

[MIT](LICENSE)
