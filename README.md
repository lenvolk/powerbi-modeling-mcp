# Power BI Modeling MCP Server — Linux Edition

Open-source C#/.NET 8 MCP server for Power BI semantic models. Runs on **Linux x64** and **Windows**. Supports **stdio** and **HTTP** transports.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Linux: `libicu` and `libssl` (pre-installed on most distros)

## Quick Start

```bash
git clone https://github.com/user/powerbi-modeling-mcp.git -b linux-mcp
cd powerbi-modeling-mcp
dotnet build src/PowerBiMcpServer
```

### stdio (VS Code / GitHub Copilot)

```bash
dotnet run --project src/PowerBiMcpServer
```

### HTTP

```bash
dotnet run --project src/PowerBiMcpServer -- --transport http --port 5100
```

Verify: `curl http://localhost:5100/healthz`

## Docker (Linux)

```bash
docker build -t powerbi-mcp-server .
docker run -p 5100:5100 -e PBI_MODELING_MCP_ACCESS_TOKEN="<token>" powerbi-mcp-server
```

## Publish for Linux

```bash
# Framework-dependent (requires .NET 8 runtime on target)
dotnet publish src/PowerBiMcpServer -c Release -r linux-x64 --no-self-contained -o publish/linux-x64

# Self-contained (no runtime needed, ~80MB)
dotnet publish src/PowerBiMcpServer -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o publish/linux-x64-sc
```

> Do **not** use `PublishTrimmed=true` — TOM uses reflection.

## Linux Capabilities

| Feature | Supported | Notes |
|---|---|---|
| Fabric/XMLA connections | ✅ | Remote via ADOMD |
| DAX queries (Fabric) | ✅ | Over HTTP/XMLA |
| PBIP/TMDL editing | ✅ | Offline in-memory via TmdlSerializer |
| DAX on local PBIP | ❌ | Requires AS engine |
| PBI Desktop discovery | ❌ | Windows-only app |

## CLI Options

| Flag | Default | Description |
|---|---|---|
| `--transport` | `stdio` | `stdio` or `http` |
| `--host` | `127.0.0.1` | Bind address (HTTP) |
| `--port` | `5100` | Listen port (HTTP) |
| `--readonly` | off | Block write operations |
| `--skipconfirmation` | off | Skip write confirmations |
| `--compatibility` | `PowerBI` | `Full` for Analysis Services |

**Environment:** `PBI_MODELING_MCP_ACCESS_TOKEN` — Fabric auth token (alternative to `az login`).

## VS Code MCP Configuration

```jsonc
{
  "servers": {
    // HTTP — start server first, then connect
    "powerbi-mcp-server": {
      "type": "sse",
      "url": "http://localhost:5100/mcp"
    }

    // stdio — auto-launches with VS Code
    // "powerbi-mcp-server": {
    //   "type": "stdio",
    //   "command": "dotnet",
    //   "args": ["run", "--project", "${workspaceFolder}/src/PowerBiMcpServer/PowerBiMcpServer.csproj"]
    // }
  }
}
```

## Available Tools

| Domain | Tools |
|---|---|
| **connection** | `connect_desktop`, `connect_fabric`, `open_pbip`, `list`, `disconnect` |
| **model** | `get`, `get_stats`, `rename`, `refresh` |
| **table** | `list`, `get`, `create`, `delete`, `rename`, `update` |
| **column** | `list`, `get`, `create`, `update`, `delete`, `rename` |
| **measure** | `list`, `get`, `create`, `update`, `delete`, `rename`, `move` |
| **relationship** | `list`, `create`, `delete`, `activate`, `find` |
| **dax** | `query`, `evaluate_measure`, `info_tables`, `info_relationships`, `info_measures`, `preview_table`, `distinct_values` |
| **tmdl** | `export`, `import`, `read_folder`, `read_file`, `list_files`, `pbip_discover` |
| **hierarchy** | `list`, `get`, `create`, `delete` |
| **perspective** | `list`, `get`, `create`, `delete`, `add_table`, `remove_table` |
| **role** | `list`, `get`, `create`, `delete`, `set_table_filter`, `clear_table_filter` |
| **partition** | `list`, `get`, `update_expression` |
| **calcgroup** | `list`, `get`, `create`, `delete`, `add_item`, `delete_item` |

### Usage Flow

1. Connect: `connection_connect_fabric` (Linux) or `connection_open_pbip` (offline TMDL)
2. Use the returned `connectionId` with any tool
3. Disconnect: `connection_disconnect`

> On Linux, `connection_connect_desktop` returns a helpful message — PBI Desktop is Windows-only.

## Architecture

```
src/PowerBiMcpServer/
├── Program.cs                         # Entry point — stdio or HTTP
├── Models/
│   ├── ConnectionInfo.cs              # Connection metadata
│   └── ServerSettings.cs              # CLI flags
├── Services/
│   ├── ConnectionManager.cs           # Connection pool + offline PBIP + DAX
│   ├── PowerBiDesktopDiscovery.cs     # Windows-only (graceful no-op on Linux)
│   └── TmdlService.cs                # TMDL serialize/deserialize
└── Tools/                             # 12 tool classes, one per domain
```

## License

[MIT](LICENSE)
