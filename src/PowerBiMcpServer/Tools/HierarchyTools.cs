using System.ComponentModel;
using System.Text;
using Microsoft.AnalysisServices.Tabular;
using ModelContextProtocol.Server;
using PowerBiMcpServer.Services;

namespace PowerBiMcpServer.Tools;

/// <summary>
/// Tools for managing hierarchies: list, get, create, delete.
/// </summary>
[McpServerToolType]
public sealed class HierarchyTools
{
    private readonly ConnectionManager _cm;
    public HierarchyTools(ConnectionManager cm) => _cm = cm;

    [McpServerTool(Name = "hierarchy_list",
        Title = "List Hierarchies",
        ReadOnly = true, Idempotent = true)]
    [Description("Lists all hierarchies in a table (or across the entire model if no table name is given).")]
    public string ListHierarchies(
        [Description("Connection ID")] string connectionId,
        [Description("Optional table name. If omitted, lists hierarchies from all tables.")] string? tableName = null)
    {
        try
        {
            var model = _cm.GetModel(connectionId);

            var tables = string.IsNullOrWhiteSpace(tableName)
                ? model.Tables.Cast<Table>()
                : new[] { model.Tables.Find(tableName)
                    ?? throw new InvalidOperationException($"Table '{tableName}' not found.") };

            var sb = new StringBuilder(2048);
            sb.AppendLine("| Table | Hierarchy | Levels | Hidden |");
            sb.AppendLine("| --- | --- | --- | --- |");

            foreach (var table in tables)
            foreach (var h in table.Hierarchies)
            {
                var levels = string.Join(" → ", h.Levels.OrderBy(l => l.Ordinal).Select(l => l.Name));
                sb.AppendLine($"| {EscapeMd(table.Name)} | {EscapeMd(h.Name)} | {EscapeMd(levels)} | {h.IsHidden} |");
            }

            return sb.ToString();
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "hierarchy_get",
        Title = "Get Hierarchy",
        ReadOnly = true, Idempotent = true)]
    [Description("Returns the full definition of a hierarchy including its levels, description, and display folder.")]
    public string GetHierarchy(
        [Description("Connection ID")] string connectionId,
        [Description("Table name where the hierarchy resides")] string tableName,
        [Description("Hierarchy name")] string hierarchyName)
    {
        try
        {
            var hierarchy = FindHierarchy(connectionId, tableName, hierarchyName);

            var sb = new StringBuilder(1024);
            sb.AppendLine($"# Hierarchy: {hierarchy.Name}");
            sb.AppendLine($"- **Table**: {tableName}");
            sb.AppendLine($"- **Hidden**: {hierarchy.IsHidden}");
            sb.AppendLine($"- **Display Folder**: {hierarchy.DisplayFolder ?? "(none)"}");
            if (!string.IsNullOrEmpty(hierarchy.Description))
                sb.AppendLine($"\n> {hierarchy.Description}");

            sb.AppendLine("\n## Levels\n");
            sb.AppendLine("| Ordinal | Level Name | Column |");
            sb.AppendLine("| --- | --- | --- |");
            foreach (var level in hierarchy.Levels.OrderBy(l => l.Ordinal))
            {
                var colName = level.Column?.Name ?? "(unknown)";
                sb.AppendLine($"| {level.Ordinal} | {EscapeMd(level.Name)} | {EscapeMd(colName)} |");
            }

            return sb.ToString();
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "hierarchy_create",
        Title = "Create Hierarchy",
        ReadOnly = false)]
    [Description("Creates a new hierarchy in the specified table with the given levels.")]
    public string CreateHierarchy(
        [Description("Connection ID")] string connectionId,
        [Description("Table name to add the hierarchy to")] string tableName,
        [Description("Hierarchy name")] string hierarchyName,
        [Description("Comma-separated column names in order from top to bottom level (e.g., 'Country,State,City')")] string levels,
        [Description("Optional description")] string? description = null,
        [Description("Optional display folder")] string? displayFolder = null,
        [Description("Hide from client tools")] bool isHidden = false)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(hierarchyName))
                return "Error: Hierarchy name cannot be empty.";
            if (hierarchyName.Length > 256)
                return "Error: Hierarchy name exceeds 256 character limit.";
            if (string.IsNullOrWhiteSpace(levels))
                return "Error: Levels cannot be empty. Provide comma-separated column names.";

            var model = _cm.GetModel(connectionId);
            var table = model.Tables.Find(tableName)
                ?? throw new InvalidOperationException($"Table '{tableName}' not found.");

            if (table.Hierarchies.Find(hierarchyName) is not null)
                return $"Error: Hierarchy '{hierarchyName}' already exists in table '{tableName}'.";

            var columnNames = levels.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (columnNames.Length == 0)
                return "Error: At least one level column must be specified.";

            // Validate all columns exist before creating
            foreach (var colName in columnNames)
            {
                if (table.Columns.Find(colName) is null)
                    return $"Error: Column '{colName}' not found in table '{tableName}'.";
            }

            var hierarchy = new Hierarchy
            {
                Name = hierarchyName,
                Description = description ?? "",
                DisplayFolder = displayFolder ?? "",
                IsHidden = isHidden
            };

            table.Hierarchies.Add(hierarchy);

            for (int i = 0; i < columnNames.Length; i++)
            {
                var colName = columnNames[i];
                hierarchy.Levels.Add(new Level
                {
                    Name = colName,
                    Ordinal = i,
                    Column = table.Columns.Find(colName)
                });
            }

            _cm.SaveChanges(connectionId);

            return $"Hierarchy **{hierarchyName}** created in table **{tableName}** with {columnNames.Length} level(s).";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "hierarchy_delete",
        Title = "Delete Hierarchy",
        ReadOnly = false, Destructive = true)]
    [Description("Deletes a hierarchy from the semantic model.")]
    public string DeleteHierarchy(
        [Description("Connection ID")] string connectionId,
        [Description("Table name")] string tableName,
        [Description("Hierarchy name")] string hierarchyName)
    {
        try
        {
            var model = _cm.GetModel(connectionId);
            var table = model.Tables.Find(tableName)
                ?? throw new InvalidOperationException($"Table '{tableName}' not found.");
            var hierarchy = table.Hierarchies.Find(hierarchyName)
                ?? throw new InvalidOperationException($"Hierarchy '{hierarchyName}' not found in table '{tableName}'.");

            table.Hierarchies.Remove(hierarchy);
            _cm.SaveChanges(connectionId);

            return $"Hierarchy **{hierarchyName}** deleted from table **{tableName}**.";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    private Hierarchy FindHierarchy(string connectionId, string tableName, string hierarchyName)
    {
        var model = _cm.GetModel(connectionId);
        var table = model.Tables.Find(tableName)
            ?? throw new InvalidOperationException($"Table '{tableName}' not found.");
        return table.Hierarchies.Find(hierarchyName)
            ?? throw new InvalidOperationException($"Hierarchy '{hierarchyName}' not found in table '{tableName}'.");
    }

    private static string EscapeMd(string s) =>
        s.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");
}
