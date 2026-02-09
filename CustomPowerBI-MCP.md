# Custom Power BI MCP Server (`powerbi-mcp-server`)

## Why It Was Created

The official Microsoft `powerbi-modeling-mcp` is a **closed-source .NET binary** distributed via a VS Code extension. It only supports **stdio transport**, making it impossible to use over HTTP(S) for remote or multi-client scenarios.

This reimplementation was built from scratch to:

1. **Add HTTP(S) / Streamable HTTP transport** — run as a web server (`--transport http --port 5100`) for remote access, multi-session use, and non-VS Code clients
2. **Keep stdio as default** — fully compatible with VS Code / GitHub Copilot Chat
3. **Open source** — customizable, extensible, debuggable

## How to Run

```bash
# stdio (default, same as official)
dotnet run --project src/PowerBiMcpServer

# HTTP transport
dotnet run --project src/PowerBiMcpServer -- --transport http --port 5100
```

Flags: `--readonly`, `--skipconfirmation`, `--host`, `--port`, `--compatibility`

## Differences from Official Microsoft MCP

| Aspect | Official (`powerbi-modeling-mcp`) | Custom (`powerbi-mcp-server`) |
|--------|-----------------------------------|-------------------------------|
| **Transport** | stdio only | stdio + HTTP(S) |
| **Source** | Closed-source binary (VSIX) | Open-source C# |
| **Tool style** | ~21 mega-tools with `operation` parameter | 46 individual tools (one per action) |
| **Coverage** | Broader — includes calculation groups, calendars, security roles, perspectives, partitions, hierarchies, cultures, translations, named expressions, query groups, traces, transactions | Core — connections, model, tables, columns, measures, relationships, DAX, TMDL/PBIP |
| **Runtime** | Pre-compiled platform binary | `dotnet run` (requires .NET 8 SDK) |
| **Auth** | Built-in Azure auth | Azure.Identity (DefaultAzureCredential) |

### Categories Only in Official

Calculation groups, calendars, cultures, partitions, perspectives, security roles, user hierarchies, named expressions, functions, object translations, query groups, traces, transactions.

### Advantages of Custom

- HTTP transport for remote/multi-client use
- One-tool-per-action is simpler for LLMs to invoke
- Fully extensible — add new tools by creating a `[McpServerToolType]` class
