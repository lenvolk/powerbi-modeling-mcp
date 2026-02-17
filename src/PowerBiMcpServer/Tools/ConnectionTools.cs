using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using Azure.Identity;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PowerBiMcpServer.Models;
using PowerBiMcpServer.Services;

namespace PowerBiMcpServer.Tools;

/// <summary>
/// Tools for connecting to Power BI semantic models.
/// Supports Power BI Desktop (local), Fabric workspace (remote), and PBIP folders.
/// </summary>
[McpServerToolType]
public sealed class ConnectionTools
{
    private readonly ConnectionManager _cm;
    private readonly PowerBiDesktopDiscovery _discovery;
    private readonly TmdlService _tmdl;

    public ConnectionTools(ConnectionManager cm, PowerBiDesktopDiscovery discovery, TmdlService tmdl)
    {
        _cm        = cm;
        _discovery = discovery;
        _tmdl      = tmdl;
    }

    // ── Power BI Desktop ────────────────────────────────────────────────────

    [McpServerTool(Name = "connection_connect_desktop",
        Title = "Connect to Power BI Desktop",
        ReadOnly = true, Idempotent = false)]
    [Description("Discovers running Power BI Desktop instances and connects to the one matching the given file name. "
        + "Returns a connection ID to use with other tools. If no file name is provided, lists all running instances.")]
    public string ConnectDesktop(
        [Description("Name (or partial name) of the .pbix file open in Power BI Desktop. Leave empty to list running instances.")]
        string? fileName = null)
    {
        try
        {
            var instances = _discovery.Discover();

            if (instances.Count == 0)
                return "No running Power BI Desktop instances found. Please open a .pbix file in Power BI Desktop first.";

            if (string.IsNullOrWhiteSpace(fileName))
            {
                var sb = new StringBuilder(512);
                sb.AppendLine("## Running Power BI Desktop instances\n");
                foreach (var inst in instances)
                    sb.AppendLine($"- **{inst.FileName}** — port {inst.Port}");
                sb.AppendLine("\nProvide the file name to connect.");
                return sb.ToString();
            }

            // Search the already-discovered list instead of calling Discover() again
            var match = instances.FirstOrDefault(i =>
                i.FileName.Contains(fileName, StringComparison.OrdinalIgnoreCase));
            if (match is null)
                return $"No Power BI Desktop instance found matching '{fileName}'. Running instances:\n"
                     + string.Join("\n", instances.Select(i => $"  - {i.FileName}"));

            var id = _cm.Connect(match.ConnectionString, match.FileName, ConnectionKind.PowerBIDesktop);
            return $"Connected to **{match.FileName}** (connection `{id}`). You can now use this connection ID with other tools.";
        }
        catch (Exception ex) { return $"Error connecting to Power BI Desktop: {ex.Message}"; }
    }

    // ── Fabric Workspace ────────────────────────────────────────────────────

    [McpServerTool(Name = "connection_connect_fabric",
        Title = "Connect to Fabric Workspace",
        ReadOnly = true, Idempotent = false)]
    [Description("Connects to a semantic model in a Microsoft Fabric workspace via the XMLA endpoint. "
        + "Authenticates using Azure Identity (DefaultAzureCredential) or the PBI_MODELING_MCP_ACCESS_TOKEN environment variable.")]
    public string ConnectFabric(
        [Description("Name of the Fabric workspace")] string workspaceName,
        [Description("Name of the semantic model (database)")] string semanticModelName)
    {
        // Build XMLA endpoint
        var xmlaEndpoint = $"powerbi://api.powerbi.com/v1.0/myorg/{Uri.EscapeDataString(workspaceName)}";

        // Escape semicolons in names to prevent connection string injection
        var safeCatalog = semanticModelName.Replace(";", "");

        // Determine access token
        var envToken = Environment.GetEnvironmentVariable("PBI_MODELING_MCP_ACCESS_TOKEN");
        string connectionString;

        if (!string.IsNullOrEmpty(envToken))
        {
            connectionString = $"Data Source={xmlaEndpoint};Initial Catalog={safeCatalog};"
                             + $"Password={envToken};";
        }
        else
        {
            // Use DefaultAzureCredential (interactive browser, managed identity, etc.)
            try
            {
                var credential = new DefaultAzureCredential();
                var token = credential.GetToken(
                    new Azure.Core.TokenRequestContext(new[] { "https://analysis.windows.net/powerbi/api/.default" }));
                connectionString = $"Data Source={xmlaEndpoint};Initial Catalog={safeCatalog};"
                                 + $"Password={token.Token};";
            }
            catch (Exception ex)
            {
                return $"Error: Authentication failed. Set PBI_MODELING_MCP_ACCESS_TOKEN or sign in via Azure CLI.\n{ex.Message}";
            }
        }

        var id = _cm.Connect(connectionString, semanticModelName, ConnectionKind.FabricWorkspace,
            databaseName: semanticModelName, workspaceName: workspaceName);

        return $"Connected to **{semanticModelName}** in workspace **{workspaceName}** (connection `{id}`).";
    }

    // ── PBIP / TMDL ─────────────────────────────────────────────────────────

    [McpServerTool(Name = "connection_open_pbip",
        Title = "Open Semantic Model from PBIP",
        ReadOnly = true, Idempotent = false)]
    [Description("Opens a semantic model from a Power BI Project (PBIP) TMDL folder. "
        + "This is an offline connection that works with TMDL files on disk.")]
    public string OpenPbip(
        [Description("Path to the definition/ (TMDL) folder inside the .SemanticModel directory")] string tmdlFolderPath)
    {
        try
        {
            // For PBIP we don't connect via XMLA; instead we load the model in memory.
            var id = _cm.ConnectOffline(tmdlFolderPath);
            var model = _cm.GetModel(id);

            var sb = new StringBuilder();
            sb.AppendLine($"# Loaded PBIP model from `{tmdlFolderPath}`");
            sb.AppendLine($"Connection: `{id}`");
            sb.AppendLine();
            sb.AppendLine($"- **Tables**: {model.Tables.Count}");
            sb.AppendLine($"- **Relationships**: {model.Relationships.Count}");
            sb.AppendLine($"- **Measures**: {model.Tables.SelectMany(t => t.Measures).Count()}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"Error loading TMDL folder: {ex.Message}";
        }
    }

    // ── List / Disconnect ───────────────────────────────────────────────────

    [McpServerTool(Name = "connection_list",
        Title = "List Active Connections",
        ReadOnly = true, Idempotent = true)]
    [Description("Lists all active connections to Power BI semantic models.")]
    public string ListConnections()
    {
        var conns = _cm.ListConnections();
        if (conns.Count == 0)
            return "No active connections. Use connection_connect_desktop, connection_connect_fabric, or connection_open_pbip to connect.";

        var sb = new StringBuilder(1024);
        sb.AppendLine("## Active Connections\n");
        foreach (var c in conns)
        {
            // Redact passwords/tokens from connection strings for security
            var safeConnStr = Regex.Replace(c.ConnectionString, 
                @"Password=[^;]+", "Password=***", RegexOptions.IgnoreCase);
            sb.AppendLine($"- **{c.Name}** (`{c.Id}`) — {c.Kind}, connected {c.ConnectedAt:u}");
            sb.AppendLine($"  Connection: `{safeConnStr}`");
        }
        return sb.ToString();
    }

    [McpServerTool(Name = "connection_disconnect",
        Title = "Disconnect",
        ReadOnly = false, Idempotent = true)]
    [Description("Disconnects from a semantic model and releases resources.")]
    public string Disconnect(
        [Description("Connection ID returned by a connect operation")] string connectionId)
    {
        _cm.Disconnect(connectionId, out var found);
        return found
            ? $"Disconnected `{connectionId}`."
            : $"Connection `{connectionId}` not found — it may have already been disconnected.";
    }
}
