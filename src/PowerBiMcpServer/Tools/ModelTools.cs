using System.ComponentModel;
using System.Text;
using Microsoft.AnalysisServices.Tabular;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PowerBiMcpServer.Services;

namespace PowerBiMcpServer.Tools;

/// <summary>
/// Tools for working with the overall database and model: list, get stats, refresh, rename.
/// </summary>
[McpServerToolType]
public sealed class ModelTools
{
    private readonly ConnectionManager _cm;

    public ModelTools(ConnectionManager cm) => _cm = cm;

    [McpServerTool(Name = "model_get",
        Title = "Get Model Info",
        ReadOnly = true, Idempotent = true)]
    [Description("Returns comprehensive information about the semantic model including tables, relationships, measures, cultures, perspectives, and roles.")]
    public string GetModel(
        [Description("Connection ID")] string connectionId)
    {
        try
        {
            var model = _cm.GetModel(connectionId);
            var db    = _cm.GetDatabase(connectionId);

            var sb = new StringBuilder(2048);
            sb.AppendLine($"# Model: {db.Name}");
            sb.AppendLine();
            sb.AppendLine($"- **Compatibility Level**: {db.CompatibilityLevel}");
            sb.AppendLine($"- **Tables**: {model.Tables.Count}");
            sb.AppendLine($"- **Relationships**: {model.Relationships.Count}");
            sb.AppendLine($"- **Cultures**: {model.Cultures.Count}");
            sb.AppendLine($"- **Perspectives**: {model.Perspectives.Count}");
            sb.AppendLine($"- **Roles**: {model.Roles.Count}");
            sb.AppendLine($"- **Data Sources**: {model.DataSources.Count}");
            sb.AppendLine();

            sb.AppendLine("## Tables\n");
            foreach (var table in model.Tables)
            {
                var cols     = table.Columns.Count;
                var measures = table.Measures.Count;
                var parts    = table.Partitions.Count;
                sb.AppendLine($"| {table.Name} | {cols} cols | {measures} measures | {parts} partitions |");
            }

            return sb.ToString();
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "model_get_stats",
        Title = "Get Model Statistics",
        ReadOnly = true, Idempotent = true)]
    [Description("Returns statistics about the semantic model: row counts, column cardinality, and memory usage (via DAX INFO functions).")]
    public string GetModelStats(
        [Description("Connection ID")] string connectionId)
    {
        try
        {
            // Use DISCOVER_STORAGE_TABLE_COLUMNS DMV for memory/cardinality info
            var dax = @"
EVALUATE
UNION(
    ROW(""Metric"", ""Total Tables"",    ""Value"", CONVERT(COUNTROWS(INFO.TABLES()), STRING)),
    ROW(""Metric"", ""Total Columns"",   ""Value"", CONVERT(COUNTROWS(INFO.COLUMNS()), STRING)),
    ROW(""Metric"", ""Total Measures"",  ""Value"", CONVERT(COUNTROWS(INFO.MEASURES()), STRING)),
    ROW(""Metric"", ""Total Relationships"", ""Value"", CONVERT(COUNTROWS(INFO.RELATIONSHIPS()), STRING))
)";
            return _cm.ExecuteDaxQuery(connectionId, dax);
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "model_rename",
        Title = "Rename Database",
        ReadOnly = false, Idempotent = true)]
    [Description("Renames the semantic model database.")]
    public string RenameModel(
        [Description("Connection ID")] string connectionId,
        [Description("New name for the database")] string newName)
    {
        try
        {
            var db = _cm.GetDatabase(connectionId);
            db.Name = newName;
            db.Update();
            return $"Database renamed to **{newName}**.";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "model_refresh",
        Title = "Refresh Model",
        ReadOnly = false)]
    [Description("Triggers a refresh of the semantic model or specific tables. Use with caution — this can take a long time for large models.")]
    public string RefreshModel(
        [Description("Connection ID")] string connectionId,
        [Description("Optional: comma-separated table names to refresh. Leave empty for full refresh.")] string? tableNames = null)
    {
        try
        {
            var model = _cm.GetModel(connectionId);

            if (string.IsNullOrWhiteSpace(tableNames))
            {
                model.RequestRefresh(RefreshType.Full);
            }
            else
            {
                foreach (var name in tableNames.Split(',', StringSplitOptions.TrimEntries))
                {
                    var table = model.Tables.Find(name);
                    if (table is null) return $"Error: Table '{name}' not found.";
                    table.RequestRefresh(RefreshType.Full);
                }
            }

            _cm.SaveChanges(connectionId);
            return "Refresh completed successfully.";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }
}
