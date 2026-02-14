# Testing the Power BI Modeling MCP Server

Step-by-step guide to test the HTTP MCP server end-to-end with VS Code and GitHub Copilot.

---

## Prerequisites

| What | Why | Download |
|------|-----|----------|
| **.NET 8 SDK** | Build and run the MCP server | [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/8.0) |
| **VS Code** | MCP client (with Copilot) | [code.visualstudio.com](https://code.visualstudio.com/download) |
| **GitHub Copilot extension** | AI agent that uses MCP tools | [VS Code Marketplace](https://marketplace.visualstudio.com/items?itemName=GitHub.copilot) |
| **Power BI Desktop** (Windows) | Hosts a sample semantic model | [Microsoft Store](https://aka.ms/pbidesktopstore) or [Download Center](https://www.microsoft.com/download/details.aspx?id=58494) |

> **No Power BI Desktop?** Skip to [Option B: Test with PBIP files](#option-b-test-with-pbip-files-no-power-bi-desktop-needed) — no install required.

---

## Step 1: Get a Sample .pbix File

Download the **AdventureWorks** sample from Microsoft:

1. Go to: https://learn.microsoft.com/power-bi/create-reports/desktop-dimensional-model-report#load-data
2. Download the **AdventureWorks Sales.xlsx** and create a simple model, **or** use any `.pbix` file you already have.

Alternatively, download a ready-made sample `.pbix`:
- https://github.com/microsoft/powerbi-desktop-samples/tree/main/DAX — pick any `.pbix` file (e.g., `Contoso Sales Sample.pbix`)

**Open the `.pbix` file in Power BI Desktop and keep it running.**

---

## Step 2: Start the MCP Server

Open a terminal in the repo folder:

```bash
dotnet run --project src/PowerBiMcpServer -- --transport http --port 5100
```

Keep this terminal open. Verify it's running:

```bash
curl http://localhost:5100/healthz
```

You should see:
```json
{"status":"healthy","transport":"http","version":"1.0.0","connections":0}
```

---

## Step 3: Connect VS Code to the MCP Server

1. Open this repo folder in VS Code
2. The `.vscode/mcp.json` is already configured for HTTP mode
3. Open Command Palette (`Ctrl+Shift+P`) → **MCP: List Servers**
4. Select **powerbi-mcp-server** → **Start Server** (or **Restart Server**)
5. You should see **69 tools** discovered

---

## Step 4: Test with Copilot Chat

Open **GitHub Copilot Chat** (`Ctrl+Shift+I` or click the Copilot icon) and select **Agent** mode. Try these prompts in order:

### 4.1 — Discover Power BI Desktop instances

```
List all running Power BI Desktop instances
```

This calls `connection_connect_desktop` with no file name and should return the name(s) of open `.pbix` files.

### 4.2 — Connect to your model

```
Connect to 'AdventureWorks' in Power BI Desktop
```

Replace `AdventureWorks` with whatever file name you see from step 4.1. You'll get back a **connection ID** (e.g., `a1b2c3d4`).

### 4.3 — Explore the model

```
Show me all tables in the model
```

```
List all measures in the model
```

```
Show me the relationships in the model
```

### 4.4 — Run a DAX query

```
Run this DAX query: EVALUATE TOPN(5, 'Product')
```

### 4.5 — Make a change (write operation)

```
Add a new measure called "Total Sales" with expression SUM('Sales'[SalesAmount]) to the Sales table
```

> **Note:** The server will allow writes unless you started it with `--readonly`. Copilot may ask for confirmation before executing write operations.

### 4.6 — Get model statistics

```
Show me model statistics including table row counts
```

### 4.7 — Disconnect

```
Disconnect from the model
```

---

## Option B: Test with PBIP Files (No Power BI Desktop Needed)

If you don't have Power BI Desktop, you can test with TMDL files on disk.

### Create a minimal test model

Create a folder `test-model/definition/` in the repo and add this file:

**`test-model/definition/model.tmdl`**
```
model Model
	culture: en-US
```

**`test-model/definition/tables/Sales.tmdl`**
```
table Sales

	measure 'Total Amount' = SUM('Sales'[Amount])
		formatString: $#,##0.00

	column Amount
		dataType: double
		sourceColumn: Amount

	column ProductKey
		dataType: int64
		sourceColumn: ProductKey
```

### Test prompts for PBIP mode

```
Discover PBIP projects in the current directory
```

```
Read the TMDL folder summary from test-model/definition
```

```
List all TMDL files in test-model/definition
```

> **Note:** PBIP/TMDL mode works offline — it reads model definitions from disk. You cannot run DAX queries in this mode since there's no Analysis Services engine.

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| "fetch failed" in VS Code | Server isn't running. Start it first (Step 2) |
| "No running Power BI Desktop instances found" | Open a `.pbix` file in Power BI Desktop |
| "Connection not found" | You need to connect first — ask Copilot to connect |
| Server won't build | Make sure you have .NET 8 SDK: `dotnet --version` |
| Tools not showing in Copilot | Restart the MCP server from Command Palette |
| Write operations blocked | Remove `--readonly` flag when starting the server |

---

## What's Being Tested

| Step | Tools exercised | Transport |
|------|----------------|-----------|
| 4.1 | `connection_connect_desktop` | HTTP/SSE |
| 4.2 | `connection_connect_desktop` | HTTP/SSE |
| 4.3 | `table_list`, `measure_list`, `relationship_list` | HTTP/SSE |
| 4.4 | `dax_query` | HTTP/SSE |
| 4.5 | `measure_create` | HTTP/SSE |
| 4.6 | `model_get_stats` | HTTP/SSE |
| 4.7 | `connection_disconnect` | HTTP/SSE |

All tool invocations go through the HTTP transport at `http://localhost:5100/mcp` — this is what differentiates this demo from the official stdio-only server.
