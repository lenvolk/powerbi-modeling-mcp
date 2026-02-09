# Copilot Instructions — Power BI Modeling MCP Server

## Project Overview

Open-source C#/.NET 8 reimplementation of the Microsoft Power BI Modeling MCP server. Exposes MCP tools for AI agents to manage Power BI semantic models (tables, columns, measures, relationships, DAX queries, TMDL/PBIP files). Supports **stdio** (default, VS Code/Copilot) and **HTTP/Streamable-HTTP** transports.

## Architecture

- **Single .NET 8 project** at `src/PowerBiMcpServer/` — no solution-level test or library projects.
- **Entry point**: `src/PowerBiMcpServer/Program.cs` — branches on `--transport` into `WebApplication` (HTTP) or `Host` (stdio). Both register the same DI services and scan for tools via `WithToolsFromAssembly()`.
- **Three layers**:
  - `Models/` — POCOs: `ConnectionInfo` (connection metadata + `ConnectionKind` enum), `ServerSettings` (CLI flags).
  - `Services/` — stateful singletons: `ConnectionManager` (thread-safe `ConcurrentDictionary` of TOM `Server` connections + ADOMD DAX execution), `PowerBiDesktopDiscovery` (Windows-only PBI Desktop instance detection via port files), `TmdlService` (TMDL serialize/deserialize via `TmdlSerializer`).
  - `Tools/` — MCP tool classes, one per domain: `ConnectionTools`, `TableTools`, `ColumnTools`, `MeasureTools`, `RelationshipTools`, `DaxQueryTools`, `ModelTools`, `TmdlTools`.

## Key Conventions

### MCP Tool Pattern
Every tool class follows this exact pattern — **do not deviate**:
1. Annotate the class with `[McpServerToolType]` — auto-discovered by `WithToolsFromAssembly()`.
2. Constructor-inject only `Services/` singletons (never other tool classes).
3. Each public method = one MCP tool. Annotate with `[McpServerTool(Name = "domain_action", Title = "...", ReadOnly = ..., Idempotent = ...)]` and `[Description("...")]`.
4. Parameters use `[Description("...")]` attributes. Use `string` return type — format output as **Markdown** (tables, headings, code blocks).
5. Wrap method body in `try/catch (Exception ex)` returning `$"Error: {ex.Message}"` — tools never throw.
6. Write operations must call `_cm.SaveChanges(connectionId)` after modifying TOM objects.
7. Read-only tools set `ReadOnly = true`; idempotent tools set `Idempotent = true`.

Example — see `src/PowerBiMcpServer/Tools/MeasureTools.cs` for the canonical CRUD pattern.

### Tool Naming
Tool names follow `{domain}_{action}` snake_case: `connection_connect_desktop`, `measure_create`, `dax_query`, `tmdl_export`. The domain prefix groups related tools.

### Connection Flow
All tools (except connection tools) require a `connectionId` string obtained from `ConnectionTools`. The flow is:
1. `connection_connect_desktop` / `connection_connect_fabric` / `connection_open_pbip` → returns connection ID
2. Pass `connectionId` to any subsequent tool call
3. `ConnectionManager.GetModel(connectionId)` → TOM `Model` for read/write; `ConnectionManager.ExecuteDaxQuery(connectionId, dax)` → ADOMD for queries.

### Read-Only Enforcement
`ConnectionManager.SaveChanges()` checks `ServerSettings.IsReadOnly` and throws if `--readonly` is active. Tools don't need to duplicate this check.

## Build & Run

```bash
# Build
dotnet build src/PowerBiMcpServer

# Run (stdio — default)
dotnet run --project src/PowerBiMcpServer

# Run (HTTP transport)
dotnet run --project src/PowerBiMcpServer -- --transport http --port 5100

# CLI flags: --readonly, --skipconfirmation, --host 0.0.0.0, --compatibility Full
```

The assembly output name is `powerbi-mcp-server` (set in `.csproj`), not `PowerBiMcpServer`.

## Dependencies

- `ModelContextProtocol` / `ModelContextProtocol.AspNetCore` v0.2.0-preview.2 — MCP SDK for .NET
- `Microsoft.AnalysisServices.NetCore.retail.amd64` — TOM (Tabular Object Model) for model manipulation
- `Microsoft.AnalysisServices.AdomdClient.NetCore.retail.amd64` — ADOMD for DAX query execution
- `Azure.Identity` — `DefaultAzureCredential` for Fabric workspace auth

## Adding a New Tool Domain

1. Create `Tools/{Domain}Tools.cs` with `[McpServerToolType]`, inject needed services.
2. Add CRUD methods following the pattern in existing tools (list/get/create/update/delete/rename).
3. Return Markdown-formatted strings; never throw exceptions from tool methods.
4. No registration code needed — assembly scanning picks up the class automatically.

## Key Differences from Official Microsoft MCP

This repo uses **one-tool-per-action** (46 individual tools) vs. the official's **mega-tool with operation parameter** (~21 tools). This design is intentionally simpler for LLMs to invoke. Coverage focuses on core modeling (connections, model, tables, columns, measures, relationships, DAX, TMDL) — calculation groups, calendars, security roles, perspectives, partitions, hierarchies, cultures, translations are not yet implemented.
