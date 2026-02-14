using System.ComponentModel;
using System.Text;
using Microsoft.AnalysisServices.Tabular;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PowerBiMcpServer.Services;

namespace PowerBiMcpServer.Tools;

/// <summary>
/// Tools for managing security roles: list, get, create, delete, set/clear table filters.
/// </summary>
[McpServerToolType]
public sealed class RoleTools
{
    private readonly ConnectionManager _cm;
    public RoleTools(ConnectionManager cm) => _cm = cm;

    [McpServerTool(Name = "role_list",
        Title = "List Roles",
        ReadOnly = true, Idempotent = true)]
    [Description("Lists all roles in the model with name, description, and permission counts.")]
    public string ListRoles(
        [Description("Connection ID")] string connectionId)
    {
        try
        {
            var model = _cm.GetModel(connectionId);
            var sb = new StringBuilder(1024);

            sb.AppendLine("| Role | Description | Table Permissions | Members |");
            sb.AppendLine("| --- | --- | --- | --- |");

            foreach (var role in model.Roles.Cast<ModelRole>())
            {
                var description = EscapeMd(role.Description ?? "");
                var tablePermCount = role.TablePermissions.Count;
                var memberCount = role.Members.Count;

                sb.AppendLine($"| {EscapeMd(role.Name)} | {description} | {tablePermCount} | {memberCount} |");
            }

            return sb.ToString();
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "role_get",
        Title = "Get Role",
        ReadOnly = true, Idempotent = true)]
    [Description("Returns the full definition of a role including model permission and table permissions with DAX filter expressions.")]
    public string GetRole(
        [Description("Connection ID")] string connectionId,
        [Description("Role name")] string roleName)
    {
        try
        {
            var role = FindRole(connectionId, roleName);
            var sb = new StringBuilder(2048);

            sb.AppendLine($"# Role: {role.Name}");
            sb.AppendLine($"- **Model Permission**: {role.ModelPermission}");
            if (!string.IsNullOrEmpty(role.Description))
                sb.AppendLine($"- **Description**: {role.Description}");
            sb.AppendLine($"- **Members**: {role.Members.Count}");
            sb.AppendLine($"- **Table Permissions**: {role.TablePermissions.Count}");

            if (role.TablePermissions.Count > 0)
            {
                sb.AppendLine("\n## Table Permissions");
                sb.AppendLine("| Table | Filter Expression |");
                sb.AppendLine("| --- | --- |");

                foreach (var tp in role.TablePermissions.Cast<TablePermission>())
                {
                    var filter = string.IsNullOrWhiteSpace(tp.FilterExpression) 
                        ? "(no filter)" 
                        : $"`{EscapeMd(tp.FilterExpression)}`";
                    sb.AppendLine($"| {EscapeMd(tp.Table.Name)} | {filter} |");
                }
            }

            if (role.Members.Count > 0)
            {
                sb.AppendLine("\n## Members");
                foreach (var member in role.Members.Cast<ModelRoleMember>())
                {
                    sb.AppendLine($"- {EscapeMd(member.MemberName)}");
                }
            }

            return sb.ToString();
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "role_create",
        Title = "Create Role",
        ReadOnly = false)]
    [Description("Creates a new security role in the model.")]
    public string CreateRole(
        [Description("Connection ID")] string connectionId,
        [Description("Role name")] string name,
        [Description("Optional description")] string? description = null,
        [Description("Model permission (Read, Administrator, None)")] string modelPermission = "Read")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Error: Role name cannot be empty.";
            if (name.Length > 256)
                return "Error: Role name exceeds 256 character limit.";

            var model = _cm.GetModel(connectionId);

            if (model.Roles.Find(name) is not null)
                return $"Error: Role '{name}' already exists.";

            if (!Enum.TryParse<ModelPermission>(modelPermission, true, out var permission))
                return $"Error: Invalid model permission '{modelPermission}'. Valid values: Read, Administrator, None.";

            var role = new ModelRole
            {
                Name = name,
                Description = description ?? "",
                ModelPermission = permission
            };

            model.Roles.Add(role);
            _cm.SaveChanges(connectionId);

            return $"Role **{name}** created with permission **{permission}**.";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "role_delete",
        Title = "Delete Role",
        ReadOnly = false, Destructive = true)]
    [Description("Deletes a security role from the model.")]
    public string DeleteRole(
        [Description("Connection ID")] string connectionId,
        [Description("Role name")] string roleName)
    {
        try
        {
            var model = _cm.GetModel(connectionId);
            var role = model.Roles.Find(roleName)
                ?? throw new InvalidOperationException($"Role '{roleName}' not found.");

            model.Roles.Remove(role);
            _cm.SaveChanges(connectionId);

            return $"Role **{roleName}** deleted.";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "role_set_table_filter",
        Title = "Set Table Filter for Role",
        ReadOnly = false)]
    [Description("Sets a DAX filter expression on a table for a role (Row-Level Security).")]
    public string SetTableFilter(
        [Description("Connection ID")] string connectionId,
        [Description("Role name")] string roleName,
        [Description("Table name")] string tableName,
        [Description("DAX filter expression (e.g., '[UserId] = USERPRINCIPALNAME()')")] string filterExpression)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filterExpression))
                return "Error: Filter expression cannot be empty.";

            var model = _cm.GetModel(connectionId);
            var role = model.Roles.Find(roleName)
                ?? throw new InvalidOperationException($"Role '{roleName}' not found.");
            var table = model.Tables.Find(tableName)
                ?? throw new InvalidOperationException($"Table '{tableName}' not found.");

            // Find existing permission or create new one
            var permission = role.TablePermissions.Cast<TablePermission>()
                .FirstOrDefault(tp => tp.Table.Name == tableName);

            if (permission is not null)
            {
                permission.FilterExpression = filterExpression;
            }
            else
            {
                permission = new TablePermission
                {
                    Table = table,
                    FilterExpression = filterExpression
                };
                role.TablePermissions.Add(permission);
            }

            _cm.SaveChanges(connectionId);

            return $"Filter set for role **{roleName}** on table **{tableName}**: `{filterExpression}`";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "role_clear_table_filter",
        Title = "Clear Table Filter for Role",
        ReadOnly = false)]
    [Description("Clears (removes) a DAX filter on a table for a role.")]
    public string ClearTableFilter(
        [Description("Connection ID")] string connectionId,
        [Description("Role name")] string roleName,
        [Description("Table name")] string tableName)
    {
        try
        {
            var model = _cm.GetModel(connectionId);
            var role = model.Roles.Find(roleName)
                ?? throw new InvalidOperationException($"Role '{roleName}' not found.");

            var permission = role.TablePermissions.Cast<TablePermission>()
                .FirstOrDefault(tp => tp.Table.Name == tableName);

            if (permission is null)
                return $"No filter found for role **{roleName}** on table **{tableName}**.";

            role.TablePermissions.Remove(permission);
            _cm.SaveChanges(connectionId);

            return $"Filter cleared for role **{roleName}** on table **{tableName}**.";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    private ModelRole FindRole(string connectionId, string roleName)
    {
        var model = _cm.GetModel(connectionId);
        return model.Roles.Find(roleName)
            ?? throw new InvalidOperationException($"Role '{roleName}' not found.");
    }

    private static string EscapeMd(string s) =>
        s.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");
}