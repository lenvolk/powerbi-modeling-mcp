using System.ComponentModel;
using System.Text;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PowerBiMcpServer.Services;

namespace PowerBiMcpServer.Tools;

/// <summary>
/// Tools for executing DAX queries and EVALUATE statements against the connected semantic model.
/// </summary>
[McpServerToolType]
public sealed class DaxQueryTools
{
    private readonly ConnectionManager _cm;
    public DaxQueryTools(ConnectionManager cm) => _cm = cm;

    [McpServerTool(Name = "dax_query",
        Title = "Execute DAX Query",
        ReadOnly = true)]
    [Description("Executes a DAX query (must start with EVALUATE or DEFINE) and returns the result as a Markdown table. Use for data exploration, validation, and analysis.")]
    public string ExecuteQuery(
        [Description("Connection ID")] string connectionId,
        [Description("DAX query starting with EVALUATE or DEFINE")] string query)
    {
        try
        {
            var trimmed = query.TrimStart();
            if (!trimmed.StartsWith("EVALUATE", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.StartsWith("DEFINE", StringComparison.OrdinalIgnoreCase))
            {
                return "Error: DAX queries must start with `EVALUATE` or `DEFINE`. " +
                       "To query a table, use `EVALUATE 'TableName'`.";
            }

            return _cm.ExecuteDaxQuery(connectionId, query);
        }
        catch (Exception ex) { return $"Error executing DAX query: {ex.Message}"; }
    }

    [McpServerTool(Name = "dax_evaluate_measure",
        Title = "Evaluate a Measure",
        ReadOnly = true)]
    [Description("Evaluates a single measure expression in the context of the model and returns the scalar result. Wraps the expression in a ROW() EVALUATE statement.")]
    public string EvaluateMeasure(
        [Description("Connection ID")] string connectionId,
        [Description("DAX expression to evaluate (e.g. SUM('Sales'[Amount]))")] string expression,
        [Description("Optional label for the result column")] string label = "Result")
    {
        try
        {
            var dax = $"EVALUATE ROW(\"{label}\", {expression})";
            return _cm.ExecuteDaxQuery(connectionId, dax);
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "dax_info_tables",
        Title = "Get DMV - Tables Info",
        ReadOnly = true, Idempotent = true)]
    [Description("Returns DMV information about all tables including row counts and sizes using INFO.STORAGETABLECOLUMNS().")]
    public string GetTablesInfo(
        [Description("Connection ID")] string connectionId)
    {
        try
        {
            const string dax = @"
EVALUATE
SELECTCOLUMNS(
    INFO.STORAGETABLECOLUMNS(),
    ""Table"", [DIMENSION_NAME],
    ""Column"", [ATTRIBUTE_NAME],
    ""DataType"", [DATATYPE],
    ""EncodingType"", [DICTIONARY_ISRESIDENT],
    ""Rows"", [COLUMN_CARDINALITY]
)
ORDER BY [Table], [Column]";

            return _cm.ExecuteDaxQuery(connectionId, dax);
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "dax_info_relationships",
        Title = "Get DMV - Relationships Info",
        ReadOnly = true, Idempotent = true)]
    [Description("Returns DMV information about all active relationships and their referential integrity stats.")]
    public string GetRelationshipsInfo(
        [Description("Connection ID")] string connectionId)
    {
        try
        {
            const string dax = @"
EVALUATE
SELECTCOLUMNS(
    INFO.RELATIONSHIPS(),
    ""ID"", [ID],
    ""FromTable"", [FROMTABLEID],
    ""FromColumn"", [FROMCOLUMNID],
    ""ToTable"", [TOTABLEID],
    ""ToColumn"", [TOCOLUMNID],
    ""IsActive"", [ISACTIVE],
    ""CrossFilter"", [CROSSFILTERINGBEHAVIOR],
    ""JoinOn"", [JOINONDATE_BEHAVIOR],
    ""MissingKeys"", [MISSINGKEYS],
    ""InvalidRows"", [INVALIDROWS]
)";

            return _cm.ExecuteDaxQuery(connectionId, dax);
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "dax_info_measures",
        Title = "Get DMV - Measures Info",
        ReadOnly = true, Idempotent = true)]
    [Description("Returns DMV information about all measures including their expressions.")]
    public string GetMeasuresInfo(
        [Description("Connection ID")] string connectionId)
    {
        try
        {
            const string dax = @"
EVALUATE
SELECTCOLUMNS(
    INFO.MEASURES(),
    ""Table"", [MEASUREGROUP_NAME],
    ""Measure"", [MEASURE_NAME],
    ""Expression"", [EXPRESSION],
    ""DataType"", [DATATYPE],
    ""IsHidden"", [MEASURE_IS_VISIBLE],
    ""DisplayFolder"", [MEASURE_DISPLAY_FOLDER]
)
ORDER BY [Table], [Measure]";

            return _cm.ExecuteDaxQuery(connectionId, dax);
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "dax_preview_table",
        Title = "Preview Table Data",
        ReadOnly = true)]
    [Description("Returns the first N rows from a table for quick data preview. Defaults to 10 rows.")]
    public string PreviewTable(
        [Description("Connection ID")] string connectionId,
        [Description("Table name")] string tableName,
        [Description("Number of rows to return (max 1000)")] int topN = 10)
    {
        try
        {
            topN = Math.Clamp(topN, 1, 1000);
            var dax = $"EVALUATE TOPN({topN}, '{tableName}')";
            return _cm.ExecuteDaxQuery(connectionId, dax);
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "dax_distinct_values",
        Title = "Get Distinct Values",
        ReadOnly = true)]
    [Description("Returns distinct values of a column, useful for understanding data distribution.")]
    public string GetDistinctValues(
        [Description("Connection ID")] string connectionId,
        [Description("Table name")] string tableName,
        [Description("Column name")] string columnName,
        [Description("Max rows to return")] int topN = 100)
    {
        try
        {
            topN = Math.Clamp(topN, 1, 5000);
            var dax = $"EVALUATE TOPN({topN}, DISTINCT('{tableName}'[{columnName}]))";
            return _cm.ExecuteDaxQuery(connectionId, dax);
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }
}
