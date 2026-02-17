# Improvements — Linux Branch

**Branch:** `linux-mcp`

Changes made to enable Linux x64 support for the Power BI Modeling MCP Server.

---

## Linux Support

### NuGet Package Update

Replaced deprecated `*.NetCore.retail.amd64` packages with the unified cross-platform packages.

| Before | After |
|---|---|
| `Microsoft.AnalysisServices.NetCore.retail.amd64` 19.84.1 | `Microsoft.AnalysisServices` 19.87.7 |
| `Microsoft.AnalysisServices.AdomdClient.NetCore.retail.amd64` 19.84.1 | `Microsoft.AnalysisServices.AdomdClient` 19.87.7 |

The old `*.retail.amd64` packages were deprecated (Dec 2024). The new packages support Windows + Linux x64.

### Offline PBIP Mode

`ConnectionManager` supports offline TMDL loading via `ConnectOffline()`. On Linux (no PBI Desktop), `connection_open_pbip` loads the model into memory using `TmdlSerializer.DeserializeModelFromFolder()` — no localhost AS connection needed.

- `GetModel()` returns the offline model when available
- `SaveChanges()` serializes back to the TMDL folder for offline connections
- `ExecuteDaxQuery()` returns a clear error for offline connections (DAX requires a running AS engine)

### Dockerfile

Updated for Linux runtime with ICU and OpenSSL dependencies. Defaults to HTTP transport on port 5100.

```bash
docker build -t powerbi-mcp-server .
docker run -p 5100:5100 -e PBI_MODELING_MCP_ACCESS_TOKEN="<token>" powerbi-mcp-server
```

### Build Fix

Fixed `ConnectionInfo.Id` property from `init`-only to `set` — `RegisterConnection` assigns the ID after object creation. Added missing `using Microsoft.AnalysisServices.Tabular` in `TmdlTools.cs`.

---

## What Works on Linux

| Feature | Status |
|---|---|
| Fabric/XMLA remote connections | ✅ |
| DAX queries via Fabric | ✅ |
| PBIP/TMDL file editing (offline) | ✅ |
| All 12 tool domains | ✅ |
| stdio + HTTP transports | ✅ |
| PBI Desktop discovery | ❌ (Windows-only, graceful no-op) |
| DAX on local PBIP files | ❌ (needs AS engine) |

---

## Files Modified

| File | Change |
|---|---|
| `PowerBiMcpServer.csproj` | Package swap to cross-platform versions |
| `Models/ConnectionInfo.cs` | `Id` property: `init` → `set` |
| `Tools/TmdlTools.cs` | Added `using Microsoft.AnalysisServices.Tabular` |
| `Dockerfile` | Linux runtime with ICU/OpenSSL, HTTP default |
| `README.md` | Rewritten for Linux |
| `IMPROVEMENTS.md` | This file |
| `QAtesting.md` | Linux test plan |
