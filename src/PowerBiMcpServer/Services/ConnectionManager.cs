using System.Collections.Concurrent;
using Microsoft.AnalysisServices.Tabular;
using Microsoft.AnalysisServices.AdomdClient;
using PowerBiMcpServer.Models;
using ConnInfo = PowerBiMcpServer.Models.ConnectionInfo;

namespace PowerBiMcpServer.Services;

/// <summary>
/// Manages connections to Analysis Services instances (Power BI Desktop local,
/// Fabric XMLA endpoint, or PBIP/TMDL files).
/// Thread-safe for multi-session HTTP use.
/// </summary>
public sealed class ConnectionManager : IDisposable
{
    private readonly ConcurrentDictionary<string, ManagedConnection> _connections = new();
    private readonly ServerSettings _settings;

    public ConnectionManager(ServerSettings settings) => _settings = settings;

    public int ActiveConnectionCount => _connections.Count;

    // ── Connect ─────────────────────────────────────────────────────────────

    public string Connect(string connectionString, string name, ConnectionKind kind,
        string? databaseName = null, string? workspaceName = null)
    {
        var server = new Server();
        server.Connect(connectionString);

        return RegisterConnection(new ConnInfo
        {
            Id               = "",
            Name             = name,
            ConnectionString = connectionString,
            Kind             = kind,
            DatabaseName     = databaseName,
            WorkspaceName    = workspaceName
        }, server: server);
    }

    public string ConnectOffline(string tmdlPath)
    {
        if (!Directory.Exists(tmdlPath))
            throw new DirectoryNotFoundException($"TMDL folder not found: {tmdlPath}");

        var model = TmdlSerializer.DeserializeModelFromFolder(tmdlPath);
        var name  = Path.GetFileName(Path.GetDirectoryName(tmdlPath) ?? tmdlPath);

        return RegisterConnection(new ConnInfo
        {
            Id               = "",
            Name             = name,
            ConnectionString = tmdlPath, // Use path as connection string for offline
            Kind             = ConnectionKind.PbipFolder,
            DatabaseName     = name
        }, offlineModel: model);
    }

    private string RegisterConnection(ConnInfo info, Server? server = null, Model? offlineModel = null)
    {
        string id;
        ManagedConnection managed;
        do
        {
            id = Guid.NewGuid().ToString("N")[..12];
            info.Id = id;
            managed = new ManagedConnection(info, server, offlineModel);
        }
        while (!_connections.TryAdd(id, managed));

        return id;
    }

    // ── Lookup ──────────────────────────────────────────────────────────────

    public ManagedConnection Get(string connectionId)
    {
        if (_connections.TryGetValue(connectionId, out var c))
            return c;
        throw new InvalidOperationException(
            $"Connection '{connectionId}' not found. Use connection_operations to connect first.");
    }

    public IReadOnlyList<ConnInfo> ListConnections() =>
        _connections.Values.Select(c => c.Info).ToList();

    // ── DAX query execution ─────────────────────────────────────────────────

    public string ExecuteDaxQuery(string connectionId, string dax)
    {
        var conn = Get(connectionId);
        if (conn.OfflineModel != null)
            return "Error: DAX queries cannot be executed against offline PBIP/TMDL models. Connect to a running instance (Desktop or Fabric) to query data.";

        using var adomd = new AdomdConnection(conn.Info.ConnectionString);
        adomd.Open();

        if (!string.IsNullOrEmpty(conn.Info.DatabaseName))
            adomd.ChangeDatabase(conn.Info.DatabaseName);

        using var cmd = adomd.CreateCommand();
        cmd.CommandText = dax;
        cmd.CommandTimeout = 300; // 5 minutes

        using var reader = cmd.ExecuteReader();
        return FormatReaderAsMarkdown(reader);
    }

    // ── TOM helpers ─────────────────────────────────────────────────────────

    public Database GetDatabase(string connectionId)
    {
        var conn = Get(connectionId);
        if (conn.Server == null)
            throw new InvalidOperationException("This operation requires a live server connection, but the current connection is offline (PBIP/TMDL).");

        if (conn.Server.Databases.Count == 0)
            throw new InvalidOperationException("No databases on this server.");

        if (!string.IsNullOrEmpty(conn.Info.DatabaseName))
            return conn.Server.Databases.FindByName(conn.Info.DatabaseName)
                ?? throw new InvalidOperationException($"Database '{conn.Info.DatabaseName}' not found.");

        return conn.Server.Databases[0];
    }

    public Model GetModel(string connectionId)
    {
        var conn = Get(connectionId);
        if (conn.OfflineModel != null) return conn.OfflineModel;

        var db = GetDatabase(connectionId);
        return db.Model ?? throw new InvalidOperationException("Database has no tabular model.");
    }

    public void SaveChanges(string connectionId)
    {
        if (_settings.IsReadOnly)
            throw new InvalidOperationException("Server is in read-only mode. Restart without --readonly to make changes.");

        var conn = Get(connectionId);
        if (conn.OfflineModel != null)
        {
            // Serialize back to folder
            TmdlSerializer.SerializeModelToFolder(conn.OfflineModel, conn.Info.ConnectionString);
            return;
        }

        var db = GetDatabase(connectionId);
        db.Model.SaveChanges();
    }

    // ── Disconnect / Dispose ────────────────────────────────────────────────

    public void Disconnect(string connectionId, out bool found)
    {
        found = _connections.TryRemove(connectionId, out var c);
        if (found) c!.Dispose();
    }

    public void Dispose()
    {
        // Drain atomically to avoid race with concurrent Connect()
        foreach (var key in _connections.Keys.ToList())
            if (_connections.TryRemove(key, out var c))
                c.Dispose();
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private static string FormatReaderAsMarkdown(AdomdDataReader reader)
    {
        const int MAX_ROWS = 10_000; // Prevent memory exhaustion on large result sets

        var cols = Enumerable.Range(0, reader.FieldCount)
            .Select(i => reader.GetName(i))
            .ToList();

        var lines = new List<string>();
        lines.Add("| " + string.Join(" | ", cols) + " |");
        lines.Add("| " + string.Join(" | ", cols.Select(_ => "---")) + " |");

        int rowCount = 0;
        while (reader.Read() && rowCount < MAX_ROWS)
        {
            var vals = Enumerable.Range(0, reader.FieldCount)
                .Select(i => reader.IsDBNull(i) ? "" : EscapeMdCell(reader.GetValue(i)?.ToString() ?? ""));
            lines.Add("| " + string.Join(" | ", vals) + " |");
            rowCount++;
        }

        if (rowCount >= MAX_ROWS)
            lines.Add($"\n⚠️ **Result truncated at {MAX_ROWS:N0} rows**");

        return string.Join("\n", lines);
    }

    private static string EscapeMdCell(string s) =>
        s.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");
}

/// <summary>Holds a TOM Server (online) or a Model (offline).</summary>
public sealed class ManagedConnection : IDisposable
{
    public ConnInfo Info         { get; }
    public Server?  Server       { get; }
    public Model?   OfflineModel { get; }

    /// <summary>Serializes TOM access — TOM Server objects are not thread-safe.</summary>
    public SemaphoreSlim Lock { get; } = new(1, 1);

    public ManagedConnection(ConnInfo info, Server? server, Model? offlineModel)
    {
        Info         = info;
        Server       = server;
        OfflineModel = offlineModel;
    }

    public void Dispose()
    {
        Lock.Dispose();
        Server?.Disconnect();
        // OfflineModel is just a POCO tree, no disposal needed
    }
}
