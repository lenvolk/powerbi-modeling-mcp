using System.ComponentModel;
using System.Linq;
using System.Text;
using Microsoft.AnalysisServices.Tabular;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PowerBiMcpServer.Services;

namespace PowerBiMcpServer.Tools;

/// <summary>
/// Tools for managing perspectives: list, get, create, delete, add/remove tables.
/// </summary>
[McpServerToolType]
public sealed class PerspectiveTools
{
    private readonly ConnectionManager _cm;
    public PerspectiveTools(ConnectionManager cm) => _cm = cm;

    [McpServerTool(Name = "perspective_list",
        Title = "List Perspectives",
        ReadOnly = true, Idempotent = true)]
    [Description("Lists all perspectives in the model with counts of included tables, columns, and measures.")]
    public string ListPerspectives(
        [Description("Connection ID")] string connectionId)
    {
        try
        {
            var model = _cm.GetModel(connectionId);
            var sb = new StringBuilder(1024);

            sb.AppendLine("| Perspective | Description | Tables | Columns | Measures |");
            sb.AppendLine("| --- | --- | --- | --- | --- |");

            foreach (var p in model.Perspectives)
            {
                var tableCount = p.PerspectiveTables.Count;
                var columnCount = p.PerspectiveTables.Sum(t => t.PerspectiveColumns.Count);
                var measureCount = p.PerspectiveTables.Sum(t => t.PerspectiveMeasures.Count);
                sb.AppendLine($"| {EscapeMd(p.Name)} | {EscapeMd(p.Description ?? "")} | {tableCount} | {columnCount} | {measureCount} |");
            }

            return sb.ToString();
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "perspective_get",
        Title = "Get Perspective",
        ReadOnly = true, Idempotent = true)]
    [Description("Gets details of a perspective including all included tables, columns, and measures.")]
    public string GetPerspective(
        [Description("Connection ID")] string connectionId,
        [Description("Perspective name")] string perspectiveName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(perspectiveName))
                return "Error: Perspective name cannot be empty.";
            if (perspectiveName.Length > 256)
                return "Error: Perspective name exceeds 256 character limit.";

            var perspective = FindPerspective(connectionId, perspectiveName);
            var sb = new StringBuilder(2048);

            sb.AppendLine($"# Perspective: {EscapeMd(perspective.Name)}");
            if (!string.IsNullOrWhiteSpace(perspective.Description))
                sb.AppendLine($"\n> {perspective.Description}");

            foreach (var pt in perspective.PerspectiveTables)
            {
                sb.AppendLine($"\n## Table: {EscapeMd(pt.Table.Name)}");

                if (pt.PerspectiveColumns.Count > 0)
                {
                    sb.AppendLine("\n**Columns**");
                    foreach (var c in pt.PerspectiveColumns)
                        sb.AppendLine($"- {EscapeMd(c.Column.Name)}");
                }
                else
                {
                    sb.AppendLine("\n**Columns**\n- (none)");
                }

                if (pt.PerspectiveMeasures.Count > 0)
                {
                    sb.AppendLine("\n**Measures**");
                    foreach (var m in pt.PerspectiveMeasures)
                        sb.AppendLine($"- {EscapeMd(m.Measure.Name)}");
                }
                else
                {
                    sb.AppendLine("\n**Measures**\n- (none)");
                }
            }

            return sb.ToString();
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "perspective_create",
        Title = "Create Perspective",
        ReadOnly = false)]
    [Description("Creates a new perspective.")]
    public string CreatePerspective(
        [Description("Connection ID")] string connectionId,
        [Description("Perspective name")] string name,
        [Description("Optional description")] string? description = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Error: Perspective name cannot be empty.";
            if (name.Length > 256)
                return "Error: Perspective name exceeds 256 character limit.";

            var model = _cm.GetModel(connectionId);
            if (model.Perspectives.Find(name) is not null)
                return $"Error: Perspective '{name}' already exists.";

            var perspective = new Perspective
            {
                Name = name,
                Description = description ?? ""
            };

            model.Perspectives.Add(perspective);
            _cm.SaveChanges(connectionId);

            return $"Perspective **{name}** created.";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "perspective_delete",
        Title = "Delete Perspective",
        ReadOnly = false, Destructive = true)]
    [Description("Deletes a perspective.")]
    public string DeletePerspective(
        [Description("Connection ID")] string connectionId,
        [Description("Perspective name")] string perspectiveName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(perspectiveName))
                return "Error: Perspective name cannot be empty.";
            if (perspectiveName.Length > 256)
                return "Error: Perspective name exceeds 256 character limit.";

            var model = _cm.GetModel(connectionId);
            var perspective = model.Perspectives.Find(perspectiveName)
                ?? throw new InvalidOperationException($"Perspective '{perspectiveName}' not found.");

            model.Perspectives.Remove(perspective);
            _cm.SaveChanges(connectionId);

            return $"Perspective **{perspectiveName}** deleted.";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "perspective_add_table",
        Title = "Add Table to Perspective",
        ReadOnly = false)]
    [Description("Adds a table to a perspective, optionally including all its columns and measures.")]
    public string AddTableToPerspective(
        [Description("Connection ID")] string connectionId,
        [Description("Perspective name")] string perspectiveName,
        [Description("Table name")] string tableName,
        [Description("Include all columns and measures")] bool includeAll = true)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(perspectiveName))
                return "Error: Perspective name cannot be empty.";
            if (perspectiveName.Length > 256)
                return "Error: Perspective name exceeds 256 character limit.";
            if (string.IsNullOrWhiteSpace(tableName))
                return "Error: Table name cannot be empty.";
            if (tableName.Length > 256)
                return "Error: Table name exceeds 256 character limit.";

            var model = _cm.GetModel(connectionId);
            var perspective = model.Perspectives.Find(perspectiveName)
                ?? throw new InvalidOperationException($"Perspective '{perspectiveName}' not found.");
            var table = model.Tables.Find(tableName)
                ?? throw new InvalidOperationException($"Table '{tableName}' not found.");

            if (perspective.PerspectiveTables.Any(t => t.Table.Name == tableName))
                return $"Error: Table '{tableName}' already exists in perspective '{perspectiveName}'.";

            var pt = new PerspectiveTable { Table = table };
            if (includeAll)
            {
                foreach (var col in table.Columns)
                    pt.PerspectiveColumns.Add(new PerspectiveColumn { Column = col });
                foreach (var m in table.Measures)
                    pt.PerspectiveMeasures.Add(new PerspectiveMeasure { Measure = m });
            }

            perspective.PerspectiveTables.Add(pt);
            _cm.SaveChanges(connectionId);

            return $"Table **{tableName}** added to perspective **{perspectiveName}**.";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "perspective_remove_table",
        Title = "Remove Table from Perspective",
        ReadOnly = false)]
    [Description("Removes a table from a perspective.")]
    public string RemoveTableFromPerspective(
        [Description("Connection ID")] string connectionId,
        [Description("Perspective name")] string perspectiveName,
        [Description("Table name")] string tableName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(perspectiveName))
                return "Error: Perspective name cannot be empty.";
            if (perspectiveName.Length > 256)
                return "Error: Perspective name exceeds 256 character limit.";
            if (string.IsNullOrWhiteSpace(tableName))
                return "Error: Table name cannot be empty.";
            if (tableName.Length > 256)
                return "Error: Table name exceeds 256 character limit.";

            var model = _cm.GetModel(connectionId);
            var perspective = model.Perspectives.Find(perspectiveName)
                ?? throw new InvalidOperationException($"Perspective '{perspectiveName}' not found.");

            var pt = perspective.PerspectiveTables.FirstOrDefault(t => t.Table.Name == tableName)
                ?? throw new InvalidOperationException($"Table '{tableName}' not found in perspective '{perspectiveName}'.");

            perspective.PerspectiveTables.Remove(pt);
            _cm.SaveChanges(connectionId);

            return $"Table **{tableName}** removed from perspective **{perspectiveName}**.";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    private Perspective FindPerspective(string connectionId, string perspectiveName)
    {
        var model = _cm.GetModel(connectionId);
        return model.Perspectives.Find(perspectiveName)
            ?? throw new InvalidOperationException($"Perspective '{perspectiveName}' not found.");
    }

    private static string EscapeMd(string s) =>
        s.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");
}
