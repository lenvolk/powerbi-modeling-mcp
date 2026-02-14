using System.ComponentModel;
using System.Text;
using Microsoft.AnalysisServices.Tabular;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PowerBiMcpServer.Services;

namespace PowerBiMcpServer.Tools;

/// <summary>
/// Tools for managing Calculation Groups: list, get, create, add item, delete item, delete group.
/// </summary>
[McpServerToolType]
public sealed class CalculationGroupTools
{
    private readonly ConnectionManager _cm;
    public CalculationGroupTools(ConnectionManager cm) => _cm = cm;

    [McpServerTool(Name = "calcgroup_list",
        Title = "List Calculation Groups",
        ReadOnly = true, Idempotent = true)]
    [Description("Lists all calculation groups in the model with item count and precedence.")]
    public string ListCalculationGroups(
        [Description("Connection ID")] string connectionId)
    {
        try
        {
            var model = _cm.GetModel(connectionId);
            var sb = new StringBuilder(1024);

            sb.AppendLine("| Table | Items | Precedence |");
            sb.AppendLine("| --- | ---: | ---: |");

            foreach (var table in model.Tables)
            {
                if (table.CalculationGroup is null) continue;
                var cg = table.CalculationGroup;
                sb.AppendLine($"| {EscapeMd(table.Name)} | {cg.CalculationItems.Count} | {cg.Precedence} |");
            }

            return sb.ToString();
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "calcgroup_get",
        Title = "Get Calculation Group",
        ReadOnly = true, Idempotent = true)]
    [Description("Returns details of a calculation group and all its items (name, ordinal, DAX expression).")]
    public string GetCalculationGroup(
        [Description("Connection ID")] string connectionId,
        [Description("Calculation group table name")] string tableName)
    {
        try
        {
            var table = FindCalcGroupTable(connectionId, tableName);
            var cg = table.CalculationGroup!;

            var sb = new StringBuilder(2048);
            sb.AppendLine($"# Calculation Group: {EscapeMd(table.Name)}");
            sb.AppendLine($"- **Precedence**: {cg.Precedence}");
            if (!string.IsNullOrEmpty(table.Description))
                sb.AppendLine($"- **Description**: {EscapeMd(table.Description)}");

            sb.AppendLine("\n| Item | Ordinal | Expression |");
            sb.AppendLine("| --- | ---: | --- |");
            foreach (var item in cg.CalculationItems)
            {
                var expr = EscapeMd(item.Expression ?? "");
                sb.AppendLine($"| {EscapeMd(item.Name)} | {item.Ordinal} | `{expr}` |");
            }

            return sb.ToString();
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "calcgroup_create",
        Title = "Create Calculation Group",
        ReadOnly = false)]
    [Description("Creates a new calculation group table with optional description and precedence.")]
    public string CreateCalculationGroup(
        [Description("Connection ID")] string connectionId,
        [Description("Calculation group name")] string name,
        [Description("Optional description")] string? description = null,
        [Description("Optional precedence (integer)")] int? precedence = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Error: Calculation group name cannot be empty.";
            if (name.Length > 256)
                return "Error: Calculation group name exceeds 256 character limit.";

            var model = _cm.GetModel(connectionId);
            if (model.Tables.Find(name) is not null)
                return $"Error: A table named '{name}' already exists.";

            var table = new Table { Name = name, Description = description ?? string.Empty };
            table.Columns.Add(new DataColumn
            {
                Name = "Name",
                DataType = DataType.String,
                SourceColumn = "Name"
            });
            table.Partitions.Add(new Partition
            {
                Name = name,
                Source = new CalculationGroupSource()
            });
            table.CalculationGroup = new CalculationGroup();
            if (precedence.HasValue)
                table.CalculationGroup.Precedence = precedence.Value;

            model.Tables.Add(table);
            model.DiscourageImplicitMeasures = true;
            _cm.SaveChanges(connectionId);

            return $"Calculation group **{EscapeMd(name)}** created.";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "calcgroup_add_item",
        Title = "Add Calculation Item",
        ReadOnly = false)]
    [Description("Adds a new calculation item to an existing calculation group.")]
    public string AddCalculationItem(
        [Description("Connection ID")] string connectionId,
        [Description("Calculation group table name")] string tableName,
        [Description("Calculation item name")] string itemName,
        [Description("DAX expression for the item")] string expression,
        [Description("Optional ordinal position")] int? ordinal = null,
        [Description("Optional dynamic format string expression")] string? formatStringExpression = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(itemName))
                return "Error: Calculation item name cannot be empty.";
            if (itemName.Length > 256)
                return "Error: Calculation item name exceeds 256 character limit.";
            if (string.IsNullOrWhiteSpace(expression))
                return "Error: DAX expression cannot be empty.";

            var table = FindCalcGroupTable(connectionId, tableName);
            var cg = table.CalculationGroup!;

            if (cg.CalculationItems.Find(itemName) is not null)
                return $"Error: Calculation item '{itemName}' already exists in '{tableName}'.";

            var item = new CalculationItem
            {
                Name = itemName,
                Expression = expression,
                Ordinal = ordinal ?? cg.CalculationItems.Count
            };

            cg.CalculationItems.Add(item);
            _cm.SaveChanges(connectionId);

            return $"Calculation item **{EscapeMd(itemName)}** added to group **{EscapeMd(tableName)}**.";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "calcgroup_delete_item",
        Title = "Delete Calculation Item",
        ReadOnly = false, Destructive = true)]
    [Description("Deletes a calculation item from a calculation group.")]
    public string DeleteCalculationItem(
        [Description("Connection ID")] string connectionId,
        [Description("Calculation group table name")] string tableName,
        [Description("Calculation item name")] string itemName)
    {
        try
        {
            var table = FindCalcGroupTable(connectionId, tableName);
            var cg = table.CalculationGroup!;
            var item = cg.CalculationItems.Find(itemName)
                ?? throw new InvalidOperationException($"Calculation item '{itemName}' not found in '{tableName}'.");

            cg.CalculationItems.Remove(item);
            _cm.SaveChanges(connectionId);

            return $"Calculation item **{EscapeMd(itemName)}** deleted from group **{EscapeMd(tableName)}**.";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "calcgroup_delete",
        Title = "Delete Calculation Group",
        ReadOnly = false, Destructive = true)]
    [Description("Deletes an entire calculation group table from the model.")]
    public string DeleteCalculationGroup(
        [Description("Connection ID")] string connectionId,
        [Description("Calculation group table name")] string tableName)
    {
        try
        {
            var model = _cm.GetModel(connectionId);
            var table = model.Tables.Find(tableName)
                ?? throw new InvalidOperationException($"Table '{tableName}' not found.");
            if (table.CalculationGroup is null)
                throw new InvalidOperationException($"Table '{tableName}' is not a calculation group.");

            model.Tables.Remove(table);
            _cm.SaveChanges(connectionId);

            return $"Calculation group **{EscapeMd(tableName)}** deleted.";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    private Table FindCalcGroupTable(string connectionId, string tableName)
    {
        var model = _cm.GetModel(connectionId);
        var table = model.Tables.Find(tableName)
            ?? throw new InvalidOperationException($"Table '{tableName}' not found.");
        if (table.CalculationGroup is null)
            throw new InvalidOperationException($"Table '{tableName}' is not a calculation group.");
        return table;
    }

    private static string EscapeMd(string s) =>
        s.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");
}
