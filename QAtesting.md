# QA Testing — Linux Branch

**Branch:** `linux-mcp`

---

## Build Verification

```bash
# Must pass on both Windows and Linux
dotnet build src/PowerBiMcpServer/PowerBiMcpServer.csproj
# Expected: Build succeeded. 0 Warning(s), 0 Error(s)
```

---

## Linux-Specific Tests

### L1. Build on Linux x64

```bash
dotnet publish src/PowerBiMcpServer -c Release -r linux-x64 --no-self-contained -o publish/linux-x64
```
**Expected:** Publish succeeds, `publish/linux-x64/powerbi-mcp-server.dll` exists.

### L2. Docker Build

```bash
docker build -t powerbi-mcp-server .
```
**Expected:** Image builds successfully.

### L3. Docker Run + Health Check

```bash
docker run -d -p 5100:5100 --name pbi-test powerbi-mcp-server
curl http://localhost:5100/healthz
docker stop pbi-test && docker rm pbi-test
```
**Expected:** `{"status":"healthy","transport":"http","version":"1.0.0","connections":0}`

### L4. stdio Transport on Linux

```bash
echo '{"jsonrpc":"2.0","method":"initialize","id":1,"params":{"capabilities":{}}}' | dotnet run --project src/PowerBiMcpServer
```
**Expected:** JSON-RPC response with server capabilities.

### L5. Desktop Discovery on Linux (Graceful No-Op)

Call `connection_connect_desktop` via MCP.
**Expected:** Returns "No running Power BI Desktop instances found" (not a crash).

### L6. Offline PBIP Load

Call `connection_open_pbip` with a valid TMDL folder path.
**Expected:** Returns connection ID, table/measure counts. No localhost connection attempted.

### L7. DAX Query on Offline Connection

Load a PBIP model (L6), then call `dax_query`.
**Expected:** Returns error: "DAX queries cannot be executed against offline PBIP/TMDL models."

### L8. Fabric Connection from Linux

Set `PBI_MODELING_MCP_ACCESS_TOKEN` and call `connection_connect_fabric`.
**Expected:** Connects successfully via XMLA endpoint.

### L9. DAX Query via Fabric from Linux

Connect to Fabric (L8), then run `dax_query` with `EVALUATE TOPN(5, 'TableName')`.
**Expected:** Returns markdown table with 5 rows.

### L10. TMDL Export on Linux

Load PBIP (L6), call `tmdl_export` to a temp folder.
**Expected:** TMDL files written to disk.

### L11. SaveChanges on Offline Connection

Load PBIP (L6), create a measure, verify model is serialized back to TMDL folder.
**Expected:** `.tmdl` file updated on disk.

---

## Cross-Platform Tests (Run on Both Windows and Linux)

### X1. Package Restore

```bash
dotnet restore src/PowerBiMcpServer/PowerBiMcpServer.csproj
```
**Expected:** No NU1603 warnings (version mismatch).

### X2. All Tool Domains Load

Start server in HTTP mode, check tool count.
**Expected:** 69 tools discovered.

### X3. Path Validation

Call `tmdl_read_file` with `/etc/passwd` (Linux) or `C:\Windows\System32\config\SAM` (Windows).
**Expected:** "Access to system directory is not allowed."

---

## Test Summary

| Category | Tests | Platform |
|---|---|---|
| Linux-specific | L1–L11 | Linux x64 |
| Cross-platform | X1–X3 | Both |
| **Total** | **14** | — |

