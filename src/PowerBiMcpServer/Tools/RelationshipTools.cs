using System.ComponentModel;
using System.Text;
using Microsoft.AnalysisServices.Tabular;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PowerBiMcpServer.Services;

namespace PowerBiMcpServer.Tools;

/// <summary>
/// Tools for managing relationships between tables: list, create, update, delete, activate/deactivate.
/// </summary>
[McpServerToolType]
public sealed class RelationshipTools
{
    private readonly ConnectionManager _cm;
    public RelationshipTools(ConnectionManager cm) => _cm = cm;

    [McpServerTool(Name = "relationship_list",
        Title = "List Relationships",
        ReadOnly = true, Idempotent = true)]
    [Description("Lists all relationships in the semantic model showing from/to tables and columns, cardinality, cross-filter direction, and active state.")]
    public string ListRelationships(
        [Description("Connection ID")] string connectionId)
    {
        try
        {
            var model = _cm.GetModel(connectionId);
            var sb = new StringBuilder(1024);
            sb.AppendLine("## Relationships\n");
            sb.AppendLine("| # | From Table | From Column | To Table | To Column | Cardinality | Cross Filter | Active |");
            sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");

            int i = 1;
            foreach (var rel in model.Relationships.OfType<SingleColumnRelationship>())
            {
                sb.AppendLine($"| {i++} | {EscapeMd(rel.FromTable.Name)} | {EscapeMd(rel.FromColumn.Name)} | "
                    + $"{EscapeMd(rel.ToTable.Name)} | {EscapeMd(rel.ToColumn.Name)} | "
                    + $"{rel.FromCardinality}-to-{rel.ToCardinality} | {rel.CrossFilteringBehavior} | {rel.IsActive} |");
            }

            if (model.Relationships.Count == 0)
                sb.AppendLine("No relationships found.");

            return sb.ToString();
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "relationship_create",
        Title = "Create Relationship",
        ReadOnly = false)]
    [Description("Creates a new relationship between two tables. By default creates a many-to-one active relationship with single cross-filter direction.")]
    public string CreateRelationship(
        [Description("Connection ID")] string connectionId,
        [Description("'From' table name (many side)")] string fromTable,
        [Description("'From' column name")] string fromColumn,
        [Description("'To' table name (one side / lookup)")] string toTable,
        [Description("'To' column name")] string toColumn,
        [Description("Cross-filter direction: OneDirection, BothDirections, Automatic")] string crossFilter = "OneDirection",
        [Description("Set to false to create an inactive relationship")] bool isActive = true)
    {
        try
        {
            var model = _cm.GetModel(connectionId);
            var ft = model.Tables.Find(fromTable)
                ?? throw new InvalidOperationException($"Table '{fromTable}' not found.");
            var fc = ft.Columns.Find(fromColumn)
                ?? throw new InvalidOperationException($"Column '{fromColumn}' not found in '{fromTable}'.");
            var tt = model.Tables.Find(toTable)
                ?? throw new InvalidOperationException($"Table '{toTable}' not found.");
            var tc = tt.Columns.Find(toColumn)
                ?? throw new InvalidOperationException($"Column '{toColumn}' not found in '{toTable}'.");

            var rel = new SingleColumnRelationship
            {
                Name       = $"{fromTable}_{fromColumn}_{toTable}_{toColumn}",
                FromColumn = fc,
                ToColumn   = tc,
                FromCardinality = RelationshipEndCardinality.Many,
                ToCardinality   = RelationshipEndCardinality.One,
                CrossFilteringBehavior = Enum.Parse<CrossFilteringBehavior>(crossFilter, ignoreCase: true),
                IsActive   = isActive
            };

            model.Relationships.Add(rel);
            _cm.SaveChanges(connectionId);

            return $"Relationship created: **{fromTable}[{fromColumn}]** → **{toTable}[{toColumn}]** ({(isActive ? "active" : "inactive")}).";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "relationship_delete",
        Title = "Delete Relationship",
        ReadOnly = false)]
    [Description("Deletes a relationship between two tables.")]
    public string DeleteRelationship(
        [Description("Connection ID")] string connectionId,
        [Description("'From' table name")] string fromTable,
        [Description("'From' column name")] string fromColumn,
        [Description("'To' table name")] string toTable,
        [Description("'To' column name")] string toColumn)
    {
        try
        {
            var model = _cm.GetModel(connectionId);
            var rel = model.Relationships.OfType<SingleColumnRelationship>()
                .FirstOrDefault(r =>
                    r.FromTable.Name == fromTable && r.FromColumn.Name == fromColumn &&
                    r.ToTable.Name == toTable     && r.ToColumn.Name == toColumn)
                ?? throw new InvalidOperationException(
                    $"Relationship from {fromTable}[{fromColumn}] to {toTable}[{toColumn}] not found.");

            model.Relationships.Remove(rel);
            _cm.SaveChanges(connectionId);

            return $"Relationship **{fromTable}[{fromColumn}]** → **{toTable}[{toColumn}]** deleted.";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "relationship_activate",
        Title = "Activate/Deactivate Relationship",
        ReadOnly = false, Idempotent = true)]
    [Description("Activates or deactivates a relationship.")]
    public string ActivateRelationship(
        [Description("Connection ID")] string connectionId,
        [Description("'From' table name")] string fromTable,
        [Description("'From' column name")] string fromColumn,
        [Description("'To' table name")] string toTable,
        [Description("'To' column name")] string toColumn,
        [Description("True to activate, false to deactivate")] bool activate)
    {
        try
        {
            var model = _cm.GetModel(connectionId);
            var rel = model.Relationships.OfType<SingleColumnRelationship>()
                .FirstOrDefault(r =>
                    r.FromTable.Name == fromTable && r.FromColumn.Name == fromColumn &&
                    r.ToTable.Name == toTable     && r.ToColumn.Name == toColumn)
                ?? throw new InvalidOperationException("Relationship not found.");

            rel.IsActive = activate;
            _cm.SaveChanges(connectionId);

            return $"Relationship {(activate ? "activated" : "deactivated")}.";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "relationship_find",
        Title = "Find Relationships for Table",
        ReadOnly = true, Idempotent = true)]
    [Description("Finds all relationships connected to a specific table (as either from or to).")]
    public string FindRelationships(
        [Description("Connection ID")] string connectionId,
        [Description("Table name to search for")] string tableName)
    {
        try
        {
            var model = _cm.GetModel(connectionId);
            var matches = model.Relationships.OfType<SingleColumnRelationship>()
                .Where(r => r.FromTable.Name == tableName || r.ToTable.Name == tableName)
                .ToList();

            if (matches.Count == 0)
                return $"No relationships found involving table '{tableName}'.";

            var sb = new StringBuilder(1024);
            sb.AppendLine($"## Relationships for {tableName}\n");
            foreach (var r in matches)
            {
                var dir = r.FromTable.Name == tableName ? "→" : "←";
                sb.AppendLine($"- {EscapeMd(r.FromTable.Name)}[{EscapeMd(r.FromColumn.Name)}] {dir} {EscapeMd(r.ToTable.Name)}[{EscapeMd(r.ToColumn.Name)}] " +
                    $"({r.FromCardinality}-to-{r.ToCardinality}, {r.CrossFilteringBehavior}, {(r.IsActive ? "active" : "inactive")})");
            }
            return sb.ToString();
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    private static string EscapeMd(string s) =>
        s.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");
}
