using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.AnalysisServices.Tabular;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PowerBiMcpServer.Services;

namespace PowerBiMcpServer.Tools;

/// <summary>
/// Tools for managing tables in the semantic model: list, get, create, update, delete, rename.
/// </summary>
[McpServerToolType]
public sealed class TableTools
{
    private readonly ConnectionManager _cm;
    public TableTools(ConnectionManager cm) => _cm = cm;

    [McpServerTool(Name = "table_list",
        Title = "List Tables",
        ReadOnly = true, Idempotent = true)]
    [Description("Lists all tables in the semantic model with their column count, measure count, and partition info.")]
    public string ListTables(
        [Description("Connection ID")] string connectionId)
    {
        try
        {
            var model = _cm.GetModel(connectionId);
            var sb = new StringBuilder(1024);
            sb.AppendLine("## Tables\n");
            sb.AppendLine("| Table | Columns | Measures | Partitions | Hidden |");
            sb.AppendLine("| --- | --- | --- | --- | --- |");

            foreach (var t in model.Tables)
            {
                sb.AppendLine($"| {EscapeMd(t.Name)} | {t.Columns.Count} | {t.Measures.Count} | {t.Partitions.Count} | {t.IsHidden} |");
            }
            return sb.ToString();
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "table_get",
        Title = "Get Table Details",
        ReadOnly = true, Idempotent = true)]
    [Description("Returns detailed information about a specific table including all columns, measures, partitions, and hierarchies.")]
    public string GetTable(
        [Description("Connection ID")] string connectionId,
        [Description("Name of the table")] string tableName)
    {
        try
        {
            var model = _cm.GetModel(connectionId);
            var table = model.Tables.Find(tableName)
                ?? throw new InvalidOperationException($"Table '{tableName}' not found.");

            var sb = new StringBuilder(2048);
            sb.AppendLine($"# Table: {table.Name}");
            if (!string.IsNullOrEmpty(table.Description))
                sb.AppendLine($"\n> {table.Description}");
            sb.AppendLine($"\n- **Hidden**: {table.IsHidden}");

            // Columns
            sb.AppendLine("\n## Columns\n");
            sb.AppendLine("| Name | Data Type | Hidden | Sort By | Description |");
            sb.AppendLine("| --- | --- | --- | --- | --- |");
            foreach (var col in table.Columns)
            {
                var sortBy = col.SortByColumn?.Name ?? "";
                var desc   = EscapeMd(col.Description ?? "");
                sb.AppendLine($"| {EscapeMd(col.Name)} | {col.DataType} | {col.IsHidden} | {EscapeMd(sortBy)} | {desc} |");
            }

            // Measures
            if (table.Measures.Count > 0)
            {
                sb.AppendLine("\n## Measures\n");
                sb.AppendLine("| Name | Expression | Format String | Description |");
                sb.AppendLine("| --- | --- | --- | --- |");
                foreach (var m in table.Measures)
                {
                    var expr = EscapeMd(m.Expression ?? "");
                    var fmt  = EscapeMd(m.FormatString ?? "");
                    var desc = EscapeMd(m.Description ?? "");
                    sb.AppendLine($"| {EscapeMd(m.Name)} | `{Truncate(expr, 80)}` | {fmt} | {desc} |");
                }
            }

            // Partitions
            sb.AppendLine("\n## Partitions\n");
            foreach (var p in table.Partitions)
            {
                sb.AppendLine($"- **{p.Name}** — Source type: {p.SourceType}");
            }

            return sb.ToString();
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "table_create",
        Title = "Create Table",
        ReadOnly = false)]
    [Description("Creates a new calculated table in the semantic model using a DAX expression.")]
    public string CreateTable(
        [Description("Connection ID")] string connectionId,
        [Description("Name for the new table")] string tableName,
        [Description("DAX expression for the calculated table (e.g., 'CALENDAR(DATE(2020,1,1), DATE(2025,12,31))')")] string daxExpression,
        [Description("Optional description")] string? description = null)
    {
        try
        {
            var model = _cm.GetModel(connectionId);

            if (model.Tables.Find(tableName) is not null)
                return $"Error: Table '{tableName}' already exists.";

            var table = new Table { Name = tableName, Description = description ?? "" };
            table.Partitions.Add(new Partition
            {
                Name = tableName,
                Source = new CalculatedPartitionSource { Expression = daxExpression }
            });

            model.Tables.Add(table);
            _cm.SaveChanges(connectionId);

            return $"Table **{tableName}** created successfully.";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "table_delete",
        Title = "Delete Table",
        ReadOnly = false)]
    [Description("Deletes a table from the semantic model. WARNING: This also removes all columns, measures, and relationships associated with this table.")]
    public string DeleteTable(
        [Description("Connection ID")] string connectionId,
        [Description("Name of the table to delete")] string tableName)
    {
        try
        {
            var model = _cm.GetModel(connectionId);
            var table = model.Tables.Find(tableName)
                ?? throw new InvalidOperationException($"Table '{tableName}' not found.");

            model.Tables.Remove(table);
            _cm.SaveChanges(connectionId);

            return $"Table **{tableName}** deleted.";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "table_rename",
        Title = "Rename Table",
        ReadOnly = false, Idempotent = true)]
    [Description("Renames a table in the semantic model.")]
    public string RenameTable(
        [Description("Connection ID")] string connectionId,
        [Description("Current table name")] string tableName,
        [Description("New table name")] string newName)
    {
        try
        {
            var model = _cm.GetModel(connectionId);
            var table = model.Tables.Find(tableName)
                ?? throw new InvalidOperationException($"Table '{tableName}' not found.");

            table.Name = newName;
            _cm.SaveChanges(connectionId);

            return $"Table renamed from **{tableName}** to **{newName}**.";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "table_update",
        Title = "Update Table Properties",
        ReadOnly = false)]
    [Description("Updates table properties such as description and hidden state.")]
    public string UpdateTable(
        [Description("Connection ID")] string connectionId,
        [Description("Table name")] string tableName,
        [Description("New description (null to keep current)")] string? description = null,
        [Description("Set hidden state (null to keep current)")] bool? isHidden = null)
    {
        try
        {
            var model = _cm.GetModel(connectionId);
            var table = model.Tables.Find(tableName)
                ?? throw new InvalidOperationException($"Table '{tableName}' not found.");

            if (description is not null) table.Description = description;
            if (isHidden is not null)    table.IsHidden = isHidden.Value;

            _cm.SaveChanges(connectionId);
            return $"Table **{tableName}** updated.";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";

    private static string EscapeMd(string s) =>
        s.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");
}
