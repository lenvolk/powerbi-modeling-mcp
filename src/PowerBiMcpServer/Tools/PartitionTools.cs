using System.ComponentModel;
using System.Linq;
using System.Text;
using Microsoft.AnalysisServices.Tabular;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PowerBiMcpServer.Services;

namespace PowerBiMcpServer.Tools;

[McpServerToolType]
public sealed class PartitionTools
{
    private const int MaxExpressionLength = 50000;
    private readonly ConnectionManager _cm;

    public PartitionTools(ConnectionManager cm) => _cm = cm;

    [McpServerTool(Name = "partition_list",
        Title = "List Partitions",
        ReadOnly = true, Idempotent = true)]
    [Description("Lists partitions for all tables or a specific table, including source type and mode information.")]
    public string ListPartitions(
        [Description("Connection ID")] string connectionId,
        [Description("Optional table name. Leave empty to include every table.")] string? tableName = null)
    {
        try
        {
            var model = _cm.GetModel(connectionId);
            var tables = string.IsNullOrWhiteSpace(tableName)
                ? model.Tables.Cast<Table>()
                : new[]
                {
                    model.Tables.Find(tableName)
                    ?? throw new InvalidOperationException($"Table '{tableName}' not found.")
                };

            var sb = new StringBuilder(2048);
            sb.AppendLine("| Table | Partition | Source Type | Mode | Source Class |");
            sb.AppendLine("| --- | --- | --- | --- | --- |");

            foreach (var table in tables)
            foreach (var partition in table.Partitions)
            {
                sb.AppendLine(
                    $"| {Escape(table.Name)} | {Escape(partition.Name)} | {Escape(partition.SourceType.ToString())} | {Escape(partition.Mode.ToString())} | {Escape(GetSourceClass(partition))} |");
            }

            return sb.ToString();
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "partition_get",
        Title = "Get Partition",
        ReadOnly = true, Idempotent = true)]
    [Description("Shows detailed information about a partition, including its expression or query definition.")]
    public string GetPartition(
        [Description("Connection ID")] string connectionId,
        [Description("Table name containing the partition")] string tableName,
        [Description("Partition name to inspect")] string partitionName)
    {
        try
        {
            var (table, partition) = FindPartition(connectionId, tableName, partitionName);
            var (language, expression) = GetPartitionExpression(partition);

            var sb = new StringBuilder(1024);
            sb.AppendLine($"# Partition: {partition.Name}");
            sb.AppendLine($"- **Table**: {table.Name}");
            sb.AppendLine($"- **Source Type**: {partition.SourceType}");
            sb.AppendLine($"- **Source Class**: {GetSourceClass(partition)}");
            sb.AppendLine($"- **Mode**: {partition.Mode}");
            sb.AppendLine($"- **Description**: {(string.IsNullOrWhiteSpace(partition.Description) ? "(none)" : partition.Description)}");
            sb.AppendLine($"\n## Expression\n\n```{language}\n{expression}\n```");

            return sb.ToString();
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "partition_update_expression",
        Title = "Update Partition Expression",
        ReadOnly = false)]
    [Description("Updates the expression/query text for an M, Calculated, or Query partition.")]
    public string UpdatePartitionExpression(
        [Description("Connection ID")] string connectionId,
        [Description("Table name containing the partition")] string tableName,
        [Description("Partition name to update")] string partitionName,
        [Description("New expression or query text")] string newExpression)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(newExpression))
                return "Error: New expression cannot be empty.";
            if (newExpression.Length > MaxExpressionLength)
                return $"Error: Expression exceeds the maximum length of {MaxExpressionLength} characters.";

            var (_, partition) = FindPartition(connectionId, tableName, partitionName);

            switch (partition.SourceType)
            {
                case PartitionSourceType.M when partition.Source is MPartitionSource mSource:
                    mSource.Expression = newExpression;
                    break;
                case PartitionSourceType.Calculated when partition.Source is CalculatedPartitionSource calcSource:
                    calcSource.Expression = newExpression;
                    break;
                case PartitionSourceType.Query when partition.Source is QueryPartitionSource querySource:
                    querySource.Query = newExpression;
                    break;
                default:
                    return $"Error: Updating expressions for source type '{partition.SourceType}' is not supported.";
            }

            _cm.SaveChanges(connectionId);
            return $"Partition **{partitionName}** in table **{tableName}** updated.";
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    private (Table Table, Partition Partition) FindPartition(string connectionId, string tableName, string partitionName)
    {
        var model = _cm.GetModel(connectionId);
        var table = model.Tables.Find(tableName)
            ?? throw new InvalidOperationException($"Table '{tableName}' not found.");
        var partition = table.Partitions.Find(partitionName)
            ?? throw new InvalidOperationException($"Partition '{partitionName}' not found in table '{tableName}'.");
        return (table, partition);
    }

    private static (string Language, string Expression) GetPartitionExpression(Partition partition)
    {
        return partition.SourceType switch
        {
            PartitionSourceType.M when partition.Source is MPartitionSource mSource
                => ("m", SafeExpression(mSource.Expression)),
            PartitionSourceType.Calculated when partition.Source is CalculatedPartitionSource calcSource
                => ("dax", SafeExpression(calcSource.Expression)),
            PartitionSourceType.Query when partition.Source is QueryPartitionSource querySource
                => ("sql", SafeExpression(querySource.Query)),
            _ => ("text", $"(Unsupported or unavailable expression for source type '{partition.SourceType}')")
        };
    }

    private static string GetSourceClass(Partition partition) =>
        partition.Source?.GetType().Name ?? "(none)";

    private static string SafeExpression(string? expression) =>
        string.IsNullOrWhiteSpace(expression) ? "(empty)" : expression;

    private static string Escape(string value) =>
        value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
}
