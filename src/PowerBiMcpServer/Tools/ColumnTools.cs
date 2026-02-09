using System.ComponentModel;
using System.Text;
using Microsoft.AnalysisServices.Tabular;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PowerBiMcpServer.Services;

namespace PowerBiMcpServer.Tools;

/// <summary>
/// Tools for managing columns in tables: list, get, create, update, delete, rename.
/// </summary>
[McpServerToolType]
public sealed class ColumnTools
{
    private readonly ConnectionManager _cm;
    public ColumnTools(ConnectionManager cm) => _cm = cm;

    [McpServerTool(Name = "column_list",
        Title = "List Columns",
        ReadOnly = true, Idempotent = true)]
    [Description("Lists all columns in a table with data type, visibility, and sort order.")]
    public string ListColumns(
        [Description("Connection ID")] string connectionId,
        [Description("Table name")] string tableName)
    {
        try
        {
            var table = GetTable(connectionId, tableName);
            var sb = new StringBuilder($"## Columns in {tableName}\n\n");
            sb.AppendLine("| Name | Data Type | Type | Hidden | Format | Description |");
            sb.AppendLine("| --- | --- | --- | --- | --- | --- |");

            foreach (var col in table.Columns)
            {
                var kind = col.Type.ToString();
                var fmt  = col.FormatString ?? "";
                var desc = (col.Description ?? "").Replace("\n", " ");
                sb.AppendLine($"| {col.Name} | {col.DataType} | {kind} | {col.IsHidden} | {fmt} | {desc} |");
            }
            return sb.ToString();
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "column_get",
        Title = "Get Column Details",
        ReadOnly = true, Idempotent = true)]
    [Description("Returns detailed information about a specific column.")]
    public string GetColumn(
        [Description("Connection ID")] string connectionId,
        [Description("Table name")] string tableName,
        [Description("Column name")] string columnName)
    {
        try
        {
            var table = GetTable(connectionId, tableName);
            var col = table.Columns.Find(columnName)
                ?? throw new InvalidOperationException($"Column '{columnName}' not found in table '{tableName}'.");

            var sb = new StringBuilder();
            sb.AppendLine($"# Column: {col.Name}");
            sb.AppendLine($"- **Table**: {tableName}");
            sb.AppendLine($"- **Data Type**: {col.DataType}");
            sb.AppendLine($"- **Type**: {col.Type}");
            sb.AppendLine($"- **Hidden**: {col.IsHidden}");
            sb.AppendLine($"- **Format String**: {col.FormatString ?? "(none)"}");
            sb.AppendLine($"- **Display Folder**: {col.DisplayFolder ?? "(none)"}");
            sb.AppendLine($"- **Sort By Column**: {col.SortByColumn?.Name ?? "(none)"}");
            sb.AppendLine($"- **Summarize By**: {col.SummarizeBy}");
            if (!string.IsNullOrEmpty(col.Description))
                sb.AppendLine($"\n> {col.Description}");

            if (col is CalculatedColumn calc)
                sb.AppendLine($"\n**DAX Expression:**\n```dax\n{calc.Expression}\n```");

            return sb.ToString();
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "column_create",
        Title = "Create Calculated Column",
        ReadOnly = false)]
    [Description("Creates a new calculated column in a table using a DAX expression.")]
    public string CreateColumn(
        [Description("Connection ID")] string connectionId,
        [Description("Table name")] string tableName,
        [Description("Column name")] string columnName,
        [Description("DAX expression (e.g., '[Sales Amount] * 0.1')")] string expression,
        [Description("Data type: String, Int64, Double, DateTime, Boolean, Decimal, etc.")] string dataType = "Double",
        [Description("Optional description")] string? description = null,
        [Description("Optional format string (e.g., '#,##0.00')")] string? formatString = null)
    {
        try
        {
            var table = GetTable(connectionId, tableName);

            if (table.Columns.Find(columnName) is not null)
                return $"Error: Column '{columnName}' already exists in table '{tableName}'.";

            var col = new CalculatedColumn
            {
                Name         = columnName,
                Expression   = expression,
                DataType     = Enum.Parse<DataType>(dataType, ignoreCase: true),
                Description  = description ?? "",
                FormatString = formatString
            };

            table.Columns.Add(col);
            _cm.SaveChanges(connectionId);

            return $"Calculated column **{columnName}** created in table **{tableName}**.";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "column_update",
        Title = "Update Column Properties",
        ReadOnly = false)]
    [Description("Updates column properties such as description, format string, display folder, hidden state, and sort-by column.")]
    public string UpdateColumn(
        [Description("Connection ID")] string connectionId,
        [Description("Table name")] string tableName,
        [Description("Column name")] string columnName,
        [Description("New description")] string? description = null,
        [Description("Format string")] string? formatString = null,
        [Description("Display folder")] string? displayFolder = null,
        [Description("Hidden state")] bool? isHidden = null,
        [Description("Sort-by column name (must exist in the same table)")] string? sortByColumn = null)
    {
        try
        {
            var table = GetTable(connectionId, tableName);
            var col = table.Columns.Find(columnName)
                ?? throw new InvalidOperationException($"Column '{columnName}' not found.");

            if (description  is not null) col.Description  = description;
            if (formatString is not null) col.FormatString  = formatString;
            if (displayFolder is not null) col.DisplayFolder = displayFolder;
            if (isHidden     is not null) col.IsHidden      = isHidden.Value;
            if (sortByColumn is not null)
            {
                var sortCol = table.Columns.Find(sortByColumn)
                    ?? throw new InvalidOperationException($"Sort-by column '{sortByColumn}' not found.");
                col.SortByColumn = sortCol;
            }

            _cm.SaveChanges(connectionId);
            return $"Column **{columnName}** updated.";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "column_delete",
        Title = "Delete Column",
        ReadOnly = false)]
    [Description("Deletes a column from a table. Cannot delete columns that are used by measures or relationships.")]
    public string DeleteColumn(
        [Description("Connection ID")] string connectionId,
        [Description("Table name")] string tableName,
        [Description("Column name")] string columnName)
    {
        try
        {
            var table = GetTable(connectionId, tableName);
            var col = table.Columns.Find(columnName)
                ?? throw new InvalidOperationException($"Column '{columnName}' not found.");

            table.Columns.Remove(col);
            _cm.SaveChanges(connectionId);

            return $"Column **{columnName}** deleted from table **{tableName}**.";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "column_rename",
        Title = "Rename Column",
        ReadOnly = false, Idempotent = true)]
    [Description("Renames a column in a table.")]
    public string RenameColumn(
        [Description("Connection ID")] string connectionId,
        [Description("Table name")] string tableName,
        [Description("Current column name")] string columnName,
        [Description("New column name")] string newName)
    {
        try
        {
            var table = GetTable(connectionId, tableName);
            var col = table.Columns.Find(columnName)
                ?? throw new InvalidOperationException($"Column '{columnName}' not found.");

            col.Name = newName;
            _cm.SaveChanges(connectionId);

            return $"Column renamed from **{columnName}** to **{newName}**.";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    private Table GetTable(string connectionId, string tableName)
    {
        var model = _cm.GetModel(connectionId);
        return model.Tables.Find(tableName)
            ?? throw new InvalidOperationException($"Table '{tableName}' not found.");
    }
}
