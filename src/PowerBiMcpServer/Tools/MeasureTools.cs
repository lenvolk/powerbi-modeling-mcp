using System.ComponentModel;
using System.Text;
using Microsoft.AnalysisServices.Tabular;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PowerBiMcpServer.Services;

namespace PowerBiMcpServer.Tools;

/// <summary>
/// Tools for managing DAX measures: list, get, create, update, delete, rename, move.
/// </summary>
[McpServerToolType]
public sealed class MeasureTools
{
    private readonly ConnectionManager _cm;
    public MeasureTools(ConnectionManager cm) => _cm = cm;

    [McpServerTool(Name = "measure_list",
        Title = "List Measures",
        ReadOnly = true, Idempotent = true)]
    [Description("Lists all measures in a table (or across the entire model if no table name is given).")]
    public string ListMeasures(
        [Description("Connection ID")] string connectionId,
        [Description("Optional table name. If omitted, lists measures from all tables.")] string? tableName = null)
    {
        try
        {
            var model = _cm.GetModel(connectionId);
            var sb = new StringBuilder(2048);

            var tables = string.IsNullOrWhiteSpace(tableName)
                ? model.Tables.Cast<Table>()
                : new[] { model.Tables.Find(tableName)
                    ?? throw new InvalidOperationException($"Table '{tableName}' not found.") };

            sb.AppendLine("| Table | Measure | Expression | Format | Display Folder |");
            sb.AppendLine("| --- | --- | --- | --- | --- |");

            foreach (var table in tables)
            foreach (var m in table.Measures)
            {
                var expr = Truncate(EscapeMd(m.Expression ?? ""), 60);
                sb.AppendLine($"| {EscapeMd(table.Name)} | {EscapeMd(m.Name)} | `{expr}` | {EscapeMd(m.FormatString ?? "")} | {EscapeMd(m.DisplayFolder ?? "")} |");
            }

            return sb.ToString();
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "measure_get",
        Title = "Get Measure",
        ReadOnly = true, Idempotent = true)]
    [Description("Returns the full definition of a measure including its DAX expression, format string, and description.")]
    public string GetMeasure(
        [Description("Connection ID")] string connectionId,
        [Description("Table name where the measure resides")] string tableName,
        [Description("Measure name")] string measureName)
    {
        try
        {
            var measure = FindMeasure(connectionId, tableName, measureName);

            var sb = new StringBuilder(1024);
            sb.AppendLine($"# Measure: {measure.Name}");
            sb.AppendLine($"- **Table**: {tableName}");
            sb.AppendLine($"- **Format String**: {measure.FormatString ?? "(none)"}");
            sb.AppendLine($"- **Display Folder**: {measure.DisplayFolder ?? "(none)"}");
            sb.AppendLine($"- **Hidden**: {measure.IsHidden}");
            if (!string.IsNullOrEmpty(measure.Description))
                sb.AppendLine($"\n> {measure.Description}");
            sb.AppendLine($"\n## DAX Expression\n\n```dax\n{measure.Expression}\n```");

            return sb.ToString();
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "measure_create",
        Title = "Create Measure",
        ReadOnly = false)]
    [Description("Creates a new DAX measure in the specified table.")]
    public string CreateMeasure(
        [Description("Connection ID")] string connectionId,
        [Description("Table name to add the measure to")] string tableName,
        [Description("Measure name")] string measureName,
        [Description("DAX expression (e.g., 'SUM(Sales[Amount])')")] string expression,
        [Description("Optional format string (e.g., '$#,##0.00', '0.00%')")] string? formatString = null,
        [Description("Optional description")] string? description = null,
        [Description("Optional display folder")] string? displayFolder = null,
        [Description("Hide from client tools")] bool isHidden = false)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(measureName))
                return "Error: Measure name cannot be empty.";
            if (measureName.Length > 256)
                return "Error: Measure name exceeds 256 character limit.";
            if (string.IsNullOrWhiteSpace(expression))
                return "Error: DAX expression cannot be empty.";
            var model = _cm.GetModel(connectionId);
            var table = model.Tables.Find(tableName)
                ?? throw new InvalidOperationException($"Table '{tableName}' not found.");

            if (table.Measures.Find(measureName) is not null)
                return $"Error: Measure '{measureName}' already exists in table '{tableName}'.";

            var measure = new Measure
            {
                Name          = measureName,
                Expression    = expression,
                FormatString  = formatString,
                Description   = description ?? "",
                DisplayFolder = displayFolder ?? "",
                IsHidden      = isHidden
            };

            table.Measures.Add(measure);
            _cm.SaveChanges(connectionId);

            return $"Measure **{measureName}** created in table **{tableName}**.";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "measure_update",
        Title = "Update Measure",
        ReadOnly = false)]
    [Description("Updates a measure's DAX expression, format string, description, display folder, or hidden state.")]
    public string UpdateMeasure(
        [Description("Connection ID")] string connectionId,
        [Description("Table name")] string tableName,
        [Description("Measure name")] string measureName,
        [Description("New DAX expression")] string? expression = null,
        [Description("New format string")] string? formatString = null,
        [Description("New description")] string? description = null,
        [Description("New display folder")] string? displayFolder = null,
        [Description("Hidden state")] bool? isHidden = null)
    {
        try
        {
            var measure = FindMeasure(connectionId, tableName, measureName);

            if (expression    is not null) measure.Expression    = expression;
            if (formatString  is not null) measure.FormatString  = formatString;
            if (description   is not null) measure.Description   = description;
            if (displayFolder is not null) measure.DisplayFolder = displayFolder;
            if (isHidden      is not null) measure.IsHidden      = isHidden.Value;

            _cm.SaveChanges(connectionId);
            return $"Measure **{measureName}** updated.";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "measure_delete",
        Title = "Delete Measure",
        ReadOnly = false, Destructive = true)]
    [Description("Deletes a measure from the semantic model.")]
    public string DeleteMeasure(
        [Description("Connection ID")] string connectionId,
        [Description("Table name")] string tableName,
        [Description("Measure name")] string measureName)
    {
        try
        {
            var model = _cm.GetModel(connectionId);
            var table = model.Tables.Find(tableName)
                ?? throw new InvalidOperationException($"Table '{tableName}' not found.");
            var measure = table.Measures.Find(measureName)
                ?? throw new InvalidOperationException($"Measure '{measureName}' not found.");

            table.Measures.Remove(measure);
            _cm.SaveChanges(connectionId);

            return $"Measure **{measureName}** deleted from table **{tableName}**.";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "measure_rename",
        Title = "Rename Measure",
        ReadOnly = false, Idempotent = true)]
    [Description("Renames a measure.")]
    public string RenameMeasure(
        [Description("Connection ID")] string connectionId,
        [Description("Table name")] string tableName,
        [Description("Current measure name")] string measureName,
        [Description("New measure name")] string newName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(newName))
                return "Error: New measure name cannot be empty.";
            if (newName.Length > 256)
                return "Error: New measure name exceeds 256 character limit.";

            var measure = FindMeasure(connectionId, tableName, measureName);
            measure.Name = newName;
            _cm.SaveChanges(connectionId);
            return $"Measure renamed from **{measureName}** to **{newName}**.";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "measure_move",
        Title = "Move Measure to Table",
        ReadOnly = false)]
    [Description("Moves a measure from one table to another. The DAX expression remains the same.")]
    public string MoveMeasure(
        [Description("Connection ID")] string connectionId,
        [Description("Source table name")] string sourceTable,
        [Description("Measure name")] string measureName,
        [Description("Destination table name")] string destinationTable)
    {
        try
        {
            var model = _cm.GetModel(connectionId);
            var src = model.Tables.Find(sourceTable)
                ?? throw new InvalidOperationException($"Source table '{sourceTable}' not found.");
            var dst = model.Tables.Find(destinationTable)
                ?? throw new InvalidOperationException($"Destination table '{destinationTable}' not found.");
            var measure = src.Measures.Find(measureName)
                ?? throw new InvalidOperationException($"Measure '{measureName}' not found in '{sourceTable}'.");

            // Clone properties
            var newMeasure = new Measure
            {
                Name          = measure.Name,
                Expression    = measure.Expression,
                FormatString  = measure.FormatString,
                Description   = measure.Description,
                DisplayFolder = measure.DisplayFolder,
                IsHidden      = measure.IsHidden
            };

            src.Measures.Remove(measure);
            dst.Measures.Add(newMeasure);
            _cm.SaveChanges(connectionId);

            return $"Measure **{measureName}** moved from **{sourceTable}** to **{destinationTable}**.";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    private Measure FindMeasure(string connectionId, string tableName, string measureName)
    {
        var model = _cm.GetModel(connectionId);
        var table = model.Tables.Find(tableName)
            ?? throw new InvalidOperationException($"Table '{tableName}' not found.");
        return table.Measures.Find(measureName)
            ?? throw new InvalidOperationException($"Measure '{measureName}' not found in table '{tableName}'.");
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";

    private static string EscapeMd(string s) =>
        s.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");
}
