import { App } from "@modelcontextprotocol/ext-apps";
import {
  applyDocumentTheme,
  applyHostStyleVariables,
  applyHostFonts,
} from "@modelcontextprotocol/ext-apps";

// ── Types ────────────────────────────────────────────────────────────────────
interface Param {
  name: string;
  type: string;
  description: string;
  optional: boolean;
}

interface Tool {
  name: string;
  title: string;
  description: string;
  readOnly: boolean;
  idempotent: boolean;
  parameters: Param[];
}

interface Domain {
  name: string;
  icon: string;
  description: string;
  tools: Tool[];
}

// ── Embedded Tool Catalog — 13 domains, 71 tools ────────────────────────────
const domains: Domain[] = [
  {
    name: "Connection", icon: "🔌",
    description: "Connect to Power BI Desktop, Fabric workspaces, or PBIP folders and manage active connections.",
    tools: [
      { name: "connection_connect_desktop", title: "Connect to Power BI Desktop", description: "Discovers running Power BI Desktop instances and connects to the one matching the given file name. Returns a connection ID. If no file name is provided, lists all running instances.", readOnly: true, idempotent: false, parameters: [
        { name: "fileName", type: "string?", description: "Name (or partial name) of the .pbix file open in Power BI Desktop. Leave empty to list running instances.", optional: true }
      ]},
      { name: "connection_connect_fabric", title: "Connect to Fabric Workspace", description: "Connects to a semantic model in a Microsoft Fabric workspace via the XMLA endpoint. Authenticates using Azure Identity (DefaultAzureCredential) or the PBI_MODELING_MCP_ACCESS_TOKEN environment variable.", readOnly: true, idempotent: false, parameters: [
        { name: "workspaceName", type: "string", description: "Name of the Fabric workspace", optional: false },
        { name: "semanticModelName", type: "string", description: "Name of the semantic model (database)", optional: false }
      ]},
      { name: "connection_open_pbip", title: "Open Semantic Model from PBIP", description: "Opens a semantic model from a Power BI Project (PBIP) TMDL folder. This is an offline connection that works with TMDL files on disk.", readOnly: true, idempotent: false, parameters: [
        { name: "tmdlFolderPath", type: "string", description: "Path to the definition/ (TMDL) folder inside the .SemanticModel directory", optional: false }
      ]},
      { name: "connection_list", title: "List Active Connections", description: "Lists all active connections to Power BI semantic models.", readOnly: true, idempotent: true, parameters: [] },
      { name: "connection_disconnect", title: "Disconnect", description: "Disconnects from a semantic model and releases resources.", readOnly: false, idempotent: true, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID returned by a connect operation", optional: false }
      ]}
    ]
  },
  {
    name: "Model", icon: "🏗️",
    description: "Inspect, rename, refresh, and get statistics for the overall semantic model database.",
    tools: [
      { name: "model_get", title: "Get Model Info", description: "Returns comprehensive information about the semantic model including tables, relationships, measures, cultures, perspectives, and roles.", readOnly: true, idempotent: true, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false }
      ]},
      { name: "model_get_stats", title: "Get Model Statistics", description: "Returns statistics about the semantic model: row counts, column cardinality, and memory usage (via DAX INFO functions).", readOnly: true, idempotent: true, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false }
      ]},
      { name: "model_rename", title: "Rename Database", description: "Renames the semantic model database.", readOnly: false, idempotent: true, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "newName", type: "string", description: "New name for the database", optional: false }
      ]},
      { name: "model_refresh", title: "Refresh Model", description: "Triggers a refresh of the semantic model or specific tables. Use with caution — this can take a long time for large models.", readOnly: false, idempotent: false, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "tableNames", type: "string?", description: "Optional: comma-separated table names to refresh. Leave empty for full refresh.", optional: true }
      ]}
    ]
  },
  {
    name: "Table", icon: "📊",
    description: "List, inspect, create, update, rename, and delete tables in the semantic model.",
    tools: [
      { name: "table_list", title: "List Tables", description: "Lists all tables in the semantic model with their column count, measure count, and partition info.", readOnly: true, idempotent: true, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false }
      ]},
      { name: "table_get", title: "Get Table Details", description: "Returns detailed information about a specific table including all columns, measures, partitions, and hierarchies.", readOnly: true, idempotent: true, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "tableName", type: "string", description: "Name of the table", optional: false }
      ]},
      { name: "table_create", title: "Create Table", description: "Creates a new calculated table in the semantic model using a DAX expression.", readOnly: false, idempotent: false, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "tableName", type: "string", description: "Name for the new table", optional: false },
        { name: "daxExpression", type: "string", description: "DAX expression for the calculated table (e.g., 'CALENDAR(DATE(2020,1,1), DATE(2025,12,31))')", optional: false },
        { name: "description", type: "string?", description: "Optional description", optional: true }
      ]},
      { name: "table_delete", title: "Delete Table", description: "Deletes a table from the semantic model. WARNING: This also removes all columns, measures, and relationships associated with this table.", readOnly: false, idempotent: false, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "tableName", type: "string", description: "Name of the table to delete", optional: false }
      ]},
      { name: "table_rename", title: "Rename Table", description: "Renames a table in the semantic model.", readOnly: false, idempotent: true, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "tableName", type: "string", description: "Current table name", optional: false },
        { name: "newName", type: "string", description: "New table name", optional: false }
      ]},
      { name: "table_update", title: "Update Table Properties", description: "Updates table properties such as description and hidden state.", readOnly: false, idempotent: false, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "tableName", type: "string", description: "Table name", optional: false },
        { name: "description", type: "string?", description: "New description (null to keep current)", optional: true },
        { name: "isHidden", type: "bool?", description: "Set hidden state (null to keep current)", optional: true }
      ]}
    ]
  },
  {
    name: "Column", icon: "📐",
    description: "List, inspect, create calculated columns, update properties, rename, and delete columns.",
    tools: [
      { name: "column_list", title: "List Columns", description: "Lists all columns in a table with data type, visibility, and sort order.", readOnly: true, idempotent: true, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "tableName", type: "string", description: "Table name", optional: false }
      ]},
      { name: "column_get", title: "Get Column Details", description: "Returns detailed information about a specific column.", readOnly: true, idempotent: true, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "tableName", type: "string", description: "Table name", optional: false },
        { name: "columnName", type: "string", description: "Column name", optional: false }
      ]},
      { name: "column_create", title: "Create Calculated Column", description: "Creates a new calculated column in a table using a DAX expression.", readOnly: false, idempotent: false, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "tableName", type: "string", description: "Table name", optional: false },
        { name: "columnName", type: "string", description: "Column name", optional: false },
        { name: "expression", type: "string", description: "DAX expression (e.g., '[Sales Amount] * 0.1')", optional: false },
        { name: "dataType", type: "string", description: "Data type: String, Int64, Double, DateTime, Boolean, Decimal, etc.", optional: true },
        { name: "description", type: "string?", description: "Optional description", optional: true },
        { name: "formatString", type: "string?", description: "Optional format string (e.g., '#,##0.00')", optional: true }
      ]},
      { name: "column_update", title: "Update Column Properties", description: "Updates column properties such as description, format string, display folder, hidden state, and sort-by column.", readOnly: false, idempotent: false, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "tableName", type: "string", description: "Table name", optional: false },
        { name: "columnName", type: "string", description: "Column name", optional: false },
        { name: "description", type: "string?", description: "New description", optional: true },
        { name: "formatString", type: "string?", description: "Format string", optional: true },
        { name: "displayFolder", type: "string?", description: "Display folder", optional: true },
        { name: "isHidden", type: "bool?", description: "Hidden state", optional: true },
        { name: "sortByColumn", type: "string?", description: "Sort-by column name (must exist in the same table)", optional: true }
      ]},
      { name: "column_delete", title: "Delete Column", description: "Deletes a column from a table. Cannot delete columns that are used by measures or relationships.", readOnly: false, idempotent: false, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "tableName", type: "string", description: "Table name", optional: false },
        { name: "columnName", type: "string", description: "Column name", optional: false }
      ]},
      { name: "column_rename", title: "Rename Column", description: "Renames a column in a table.", readOnly: false, idempotent: true, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "tableName", type: "string", description: "Table name", optional: false },
        { name: "columnName", type: "string", description: "Current column name", optional: false },
        { name: "newName", type: "string", description: "New column name", optional: false }
      ]}
    ]
  },
  {
    name: "Measure", icon: "📏",
    description: "List, inspect, create, update, rename, move, and delete DAX measures.",
    tools: [
      { name: "measure_list", title: "List Measures", description: "Lists all measures in a table (or across the entire model if no table name is given).", readOnly: true, idempotent: true, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "tableName", type: "string?", description: "Optional table name. If omitted, lists measures from all tables.", optional: true }
      ]},
      { name: "measure_get", title: "Get Measure", description: "Returns the full definition of a measure including its DAX expression, format string, and description.", readOnly: true, idempotent: true, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "tableName", type: "string", description: "Table name where the measure resides", optional: false },
        { name: "measureName", type: "string", description: "Measure name", optional: false }
      ]},
      { name: "measure_create", title: "Create Measure", description: "Creates a new DAX measure in the specified table.", readOnly: false, idempotent: false, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "tableName", type: "string", description: "Table name to add the measure to", optional: false },
        { name: "measureName", type: "string", description: "Measure name", optional: false },
        { name: "expression", type: "string", description: "DAX expression (e.g., 'SUM(Sales[Amount])')", optional: false },
        { name: "formatString", type: "string?", description: "Optional format string (e.g., '$#,##0.00', '0.00%')", optional: true },
        { name: "description", type: "string?", description: "Optional description", optional: true },
        { name: "displayFolder", type: "string?", description: "Optional display folder", optional: true },
        { name: "isHidden", type: "bool", description: "Hide from client tools", optional: true }
      ]},
      { name: "measure_update", title: "Update Measure", description: "Updates a measure's DAX expression, format string, description, display folder, or hidden state.", readOnly: false, idempotent: false, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "tableName", type: "string", description: "Table name", optional: false },
        { name: "measureName", type: "string", description: "Measure name", optional: false },
        { name: "expression", type: "string?", description: "New DAX expression", optional: true },
        { name: "formatString", type: "string?", description: "New format string", optional: true },
        { name: "description", type: "string?", description: "New description", optional: true },
        { name: "displayFolder", type: "string?", description: "New display folder", optional: true },
        { name: "isHidden", type: "bool?", description: "Hidden state", optional: true }
      ]},
      { name: "measure_delete", title: "Delete Measure", description: "Deletes a measure from the semantic model.", readOnly: false, idempotent: false, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "tableName", type: "string", description: "Table name", optional: false },
        { name: "measureName", type: "string", description: "Measure name", optional: false }
      ]},
      { name: "measure_rename", title: "Rename Measure", description: "Renames a measure.", readOnly: false, idempotent: true, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "tableName", type: "string", description: "Table name", optional: false },
        { name: "measureName", type: "string", description: "Current measure name", optional: false },
        { name: "newName", type: "string", description: "New measure name", optional: false }
      ]},
      { name: "measure_move", title: "Move Measure to Table", description: "Moves a measure from one table to another. The DAX expression remains the same.", readOnly: false, idempotent: false, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "sourceTable", type: "string", description: "Source table name", optional: false },
        { name: "measureName", type: "string", description: "Measure name", optional: false },
        { name: "destinationTable", type: "string", description: "Destination table name", optional: false }
      ]}
    ]
  },
  {
    name: "Relationship", icon: "🔗",
    description: "List, create, delete, activate/deactivate, and find relationships between tables.",
    tools: [
      { name: "relationship_list", title: "List Relationships", description: "Lists all relationships in the semantic model showing from/to tables and columns, cardinality, cross-filter direction, and active state.", readOnly: true, idempotent: true, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false }
      ]},
      { name: "relationship_create", title: "Create Relationship", description: "Creates a new relationship between two tables. By default creates a many-to-one active relationship with single cross-filter direction.", readOnly: false, idempotent: false, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "fromTable", type: "string", description: "'From' table name (many side)", optional: false },
        { name: "fromColumn", type: "string", description: "'From' column name", optional: false },
        { name: "toTable", type: "string", description: "'To' table name (one side / lookup)", optional: false },
        { name: "toColumn", type: "string", description: "'To' column name", optional: false },
        { name: "crossFilter", type: "string", description: "Cross-filter direction: OneDirection, BothDirections, Automatic", optional: true },
        { name: "isActive", type: "bool", description: "Set to false to create an inactive relationship", optional: true }
      ]},
      { name: "relationship_delete", title: "Delete Relationship", description: "Deletes a relationship between two tables.", readOnly: false, idempotent: false, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "fromTable", type: "string", description: "'From' table name", optional: false },
        { name: "fromColumn", type: "string", description: "'From' column name", optional: false },
        { name: "toTable", type: "string", description: "'To' table name", optional: false },
        { name: "toColumn", type: "string", description: "'To' column name", optional: false }
      ]},
      { name: "relationship_activate", title: "Activate/Deactivate Relationship", description: "Activates or deactivates a relationship.", readOnly: false, idempotent: true, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "fromTable", type: "string", description: "'From' table name", optional: false },
        { name: "fromColumn", type: "string", description: "'From' column name", optional: false },
        { name: "toTable", type: "string", description: "'To' table name", optional: false },
        { name: "toColumn", type: "string", description: "'To' column name", optional: false },
        { name: "activate", type: "bool", description: "True to activate, false to deactivate", optional: false }
      ]},
      { name: "relationship_find", title: "Find Relationships for Table", description: "Finds all relationships connected to a specific table (as either from or to).", readOnly: true, idempotent: true, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "tableName", type: "string", description: "Table name to search for", optional: false }
      ]}
    ]
  },
  {
    name: "DAX Query", icon: "🔍",
    description: "Execute DAX queries, evaluate measures, preview table data, get DMV info, and explore distinct values.",
    tools: [
      { name: "dax_query", title: "Execute DAX Query", description: "Executes a DAX query (must start with EVALUATE or DEFINE) and returns the result as a Markdown table.", readOnly: true, idempotent: false, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "query", type: "string", description: "DAX query starting with EVALUATE or DEFINE", optional: false }
      ]},
      { name: "dax_evaluate_measure", title: "Evaluate a Measure", description: "Evaluates a single measure expression in the context of the model and returns the scalar result.", readOnly: true, idempotent: false, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "expression", type: "string", description: "DAX expression to evaluate (e.g. SUM('Sales'[Amount]))", optional: false },
        { name: "label", type: "string", description: "Optional label for the result column", optional: true }
      ]},
      { name: "dax_info_tables", title: "Get DMV - Tables Info", description: "Returns DMV information about all tables including row counts and sizes using INFO.STORAGETABLECOLUMNS().", readOnly: true, idempotent: true, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false }
      ]},
      { name: "dax_info_relationships", title: "Get DMV - Relationships Info", description: "Returns DMV information about all relationships with resolved table/column names, activity status, and cross-filter direction.", readOnly: true, idempotent: true, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false }
      ]},
      { name: "dax_info_measures", title: "Get DMV - Measures Info", description: "Returns DMV information about all measures including their expressions.", readOnly: true, idempotent: true, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false }
      ]},
      { name: "dax_preview_table", title: "Preview Table Data", description: "Returns the first N rows from a table for quick data preview. Defaults to 10 rows.", readOnly: true, idempotent: false, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "tableName", type: "string", description: "Table name", optional: false },
        { name: "topN", type: "int", description: "Number of rows to return (max 1000)", optional: true }
      ]},
      { name: "dax_distinct_values", title: "Get Distinct Values", description: "Returns distinct values of a column, useful for understanding data distribution.", readOnly: true, idempotent: false, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "tableName", type: "string", description: "Table name", optional: false },
        { name: "columnName", type: "string", description: "Column name", optional: false },
        { name: "topN", type: "int", description: "Max rows to return", optional: true }
      ]}
    ]
  },
  {
    name: "Hierarchy", icon: "🪜",
    description: "List, inspect, create, and delete hierarchies within tables.",
    tools: [
      { name: "hierarchy_list", title: "List Hierarchies", description: "Lists all hierarchies in a table (or across the entire model if no table name is given).", readOnly: true, idempotent: true, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "tableName", type: "string?", description: "Optional table name. If omitted, lists hierarchies from all tables.", optional: true }
      ]},
      { name: "hierarchy_get", title: "Get Hierarchy", description: "Returns the full definition of a hierarchy including its levels, description, and display folder.", readOnly: true, idempotent: true, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "tableName", type: "string", description: "Table name where the hierarchy resides", optional: false },
        { name: "hierarchyName", type: "string", description: "Hierarchy name", optional: false }
      ]},
      { name: "hierarchy_create", title: "Create Hierarchy", description: "Creates a new hierarchy in the specified table with the given levels.", readOnly: false, idempotent: false, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "tableName", type: "string", description: "Table name to add the hierarchy to", optional: false },
        { name: "hierarchyName", type: "string", description: "Hierarchy name", optional: false },
        { name: "levels", type: "string", description: "Comma-separated column names in order from top to bottom level (e.g., 'Country,State,City')", optional: false },
        { name: "description", type: "string?", description: "Optional description", optional: true },
        { name: "displayFolder", type: "string?", description: "Optional display folder", optional: true },
        { name: "isHidden", type: "bool", description: "Hide from client tools", optional: true }
      ]},
      { name: "hierarchy_delete", title: "Delete Hierarchy", description: "Deletes a hierarchy from the semantic model.", readOnly: false, idempotent: false, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "tableName", type: "string", description: "Table name", optional: false },
        { name: "hierarchyName", type: "string", description: "Hierarchy name", optional: false }
      ]}
    ]
  },
  {
    name: "Partition", icon: "🧩",
    description: "List, inspect, and update partition expressions for table data sources.",
    tools: [
      { name: "partition_list", title: "List Partitions", description: "Lists partitions for all tables or a specific table, including source type and mode information.", readOnly: true, idempotent: true, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "tableName", type: "string?", description: "Optional table name. Leave empty to include every table.", optional: true }
      ]},
      { name: "partition_get", title: "Get Partition", description: "Shows detailed information about a partition, including its expression or query definition.", readOnly: true, idempotent: true, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "tableName", type: "string", description: "Table name containing the partition", optional: false },
        { name: "partitionName", type: "string", description: "Partition name to inspect", optional: false }
      ]},
      { name: "partition_update_expression", title: "Update Partition Expression", description: "Updates the expression/query text for an M, Calculated, or Query partition.", readOnly: false, idempotent: false, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "tableName", type: "string", description: "Table name containing the partition", optional: false },
        { name: "partitionName", type: "string", description: "Partition name to update", optional: false },
        { name: "newExpression", type: "string", description: "New expression or query text", optional: false }
      ]}
    ]
  },
  {
    name: "Perspective", icon: "👁️",
    description: "List, inspect, create, delete perspectives and manage their table membership.",
    tools: [
      { name: "perspective_list", title: "List Perspectives", description: "Lists all perspectives in the model with counts of included tables, columns, and measures.", readOnly: true, idempotent: true, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false }
      ]},
      { name: "perspective_get", title: "Get Perspective", description: "Gets details of a perspective including all included tables, columns, and measures.", readOnly: true, idempotent: true, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "perspectiveName", type: "string", description: "Perspective name", optional: false }
      ]},
      { name: "perspective_create", title: "Create Perspective", description: "Creates a new perspective.", readOnly: false, idempotent: false, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "name", type: "string", description: "Perspective name", optional: false },
        { name: "description", type: "string?", description: "Optional description", optional: true }
      ]},
      { name: "perspective_delete", title: "Delete Perspective", description: "Deletes a perspective.", readOnly: false, idempotent: false, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "perspectiveName", type: "string", description: "Perspective name", optional: false }
      ]},
      { name: "perspective_add_table", title: "Add Table to Perspective", description: "Adds a table to a perspective, optionally including all its columns and measures.", readOnly: false, idempotent: false, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "perspectiveName", type: "string", description: "Perspective name", optional: false },
        { name: "tableName", type: "string", description: "Table name", optional: false },
        { name: "includeAll", type: "bool", description: "Include all columns and measures", optional: true }
      ]},
      { name: "perspective_remove_table", title: "Remove Table from Perspective", description: "Removes a table from a perspective.", readOnly: false, idempotent: false, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "perspectiveName", type: "string", description: "Perspective name", optional: false },
        { name: "tableName", type: "string", description: "Table name", optional: false }
      ]}
    ]
  },
  {
    name: "Role (RLS)", icon: "🛡️",
    description: "List, inspect, create, delete security roles and manage row-level security (RLS) table filters.",
    tools: [
      { name: "role_list", title: "List Roles", description: "Lists all roles in the model with name, description, and permission counts.", readOnly: true, idempotent: true, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false }
      ]},
      { name: "role_get", title: "Get Role", description: "Returns the full definition of a role including model permission and table permissions with DAX filter expressions.", readOnly: true, idempotent: true, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "roleName", type: "string", description: "Role name", optional: false }
      ]},
      { name: "role_create", title: "Create Role", description: "Creates a new security role in the model.", readOnly: false, idempotent: false, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "name", type: "string", description: "Role name", optional: false },
        { name: "description", type: "string?", description: "Optional description", optional: true },
        { name: "modelPermission", type: "string", description: "Model permission (Read, Administrator, None)", optional: true }
      ]},
      { name: "role_delete", title: "Delete Role", description: "Deletes a security role from the model.", readOnly: false, idempotent: false, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "roleName", type: "string", description: "Role name", optional: false }
      ]},
      { name: "role_set_table_filter", title: "Set Table Filter for Role", description: "Sets a DAX filter expression on a table for a role (Row-Level Security).", readOnly: false, idempotent: false, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "roleName", type: "string", description: "Role name", optional: false },
        { name: "tableName", type: "string", description: "Table name", optional: false },
        { name: "filterExpression", type: "string", description: "DAX filter expression (e.g., '[UserId] = USERPRINCIPALNAME()')", optional: false }
      ]},
      { name: "role_clear_table_filter", title: "Clear Table Filter for Role", description: "Clears (removes) a DAX filter on a table for a role.", readOnly: false, idempotent: false, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "roleName", type: "string", description: "Role name", optional: false },
        { name: "tableName", type: "string", description: "Table name", optional: false }
      ]}
    ]
  },
  {
    name: "Calculation Group", icon: "🧮",
    description: "List, inspect, create, and delete calculation groups and their calculation items.",
    tools: [
      { name: "calcgroup_list", title: "List Calculation Groups", description: "Lists all calculation groups in the model with item count and precedence.", readOnly: true, idempotent: true, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false }
      ]},
      { name: "calcgroup_get", title: "Get Calculation Group", description: "Returns details of a calculation group and all its items (name, ordinal, DAX expression).", readOnly: true, idempotent: true, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "tableName", type: "string", description: "Calculation group table name", optional: false }
      ]},
      { name: "calcgroup_create", title: "Create Calculation Group", description: "Creates a new calculation group table with optional description and precedence.", readOnly: false, idempotent: false, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "name", type: "string", description: "Calculation group name", optional: false },
        { name: "description", type: "string?", description: "Optional description", optional: true },
        { name: "precedence", type: "int?", description: "Optional precedence (integer)", optional: true }
      ]},
      { name: "calcgroup_add_item", title: "Add Calculation Item", description: "Adds a new calculation item to an existing calculation group.", readOnly: false, idempotent: false, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "tableName", type: "string", description: "Calculation group table name", optional: false },
        { name: "itemName", type: "string", description: "Calculation item name", optional: false },
        { name: "expression", type: "string", description: "DAX expression for the item", optional: false },
        { name: "ordinal", type: "int?", description: "Optional ordinal position", optional: true },
        { name: "formatStringExpression", type: "string?", description: "Optional dynamic format string expression", optional: true }
      ]},
      { name: "calcgroup_delete_item", title: "Delete Calculation Item", description: "Deletes a calculation item from a calculation group.", readOnly: false, idempotent: false, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "tableName", type: "string", description: "Calculation group table name", optional: false },
        { name: "itemName", type: "string", description: "Calculation item name", optional: false }
      ]},
      { name: "calcgroup_delete", title: "Delete Calculation Group", description: "Deletes an entire calculation group table from the model.", readOnly: false, idempotent: false, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "tableName", type: "string", description: "Calculation group table name", optional: false }
      ]}
    ]
  },
  {
    name: "TMDL / PBIP", icon: "📁",
    description: "Export/import TMDL folders, read TMDL files, discover PBIP projects, and list TMDL file structures.",
    tools: [
      { name: "tmdl_export", title: "Export Model to TMDL Folder", description: "Exports the connected semantic model to a TMDL folder on disk. Creates the folder if it doesn't exist.", readOnly: false, idempotent: false, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "folderPath", type: "string", description: "Full path to the target TMDL folder", optional: false }
      ]},
      { name: "tmdl_import", title: "Import TMDL Folder to Model", description: "Imports a TMDL folder into the connected semantic model, replacing the current model definition. Use with caution.", readOnly: false, idempotent: false, parameters: [
        { name: "connectionId", type: "string", description: "Connection ID", optional: false },
        { name: "folderPath", type: "string", description: "Full path to the TMDL folder to import", optional: false }
      ]},
      { name: "tmdl_read_folder", title: "Read TMDL Folder Summary", description: "Reads a TMDL folder from disk and returns a summary of the model definition without connecting to any server.", readOnly: true, idempotent: true, parameters: [
        { name: "folderPath", type: "string", description: "Full path to the TMDL folder", optional: false }
      ]},
      { name: "tmdl_read_file", title: "Read TMDL File Content", description: "Reads and returns the raw content of a specific .tmdl file from disk.", readOnly: true, idempotent: true, parameters: [
        { name: "filePath", type: "string", description: "Full path to the .tmdl file", optional: false }
      ]},
      { name: "pbip_discover", title: "Discover PBIP Project", description: "Scans a directory for Power BI Project (.pbip) files and returns the project structure including model, report, and dataset paths.", readOnly: true, idempotent: true, parameters: [
        { name: "directoryPath", type: "string", description: "Path to the directory to scan for .pbip files", optional: false }
      ]},
      { name: "tmdl_list_files", title: "List TMDL Files in Folder", description: "Lists all TMDL files in a folder with their relative paths, organized by subfolder.", readOnly: true, idempotent: true, parameters: [
        { name: "folderPath", type: "string", description: "Full path to the TMDL folder", optional: false }
      ]}
    ]
  }
];

// ── State ────────────────────────────────────────────────────────────────────
let currentTab = "overview";
let expandedTool: string | null = null;
let expandedDomain: string | null = null;
let searchQuery = "";

// ── App lifecycle ────────────────────────────────────────────────────────────
// Try MCP host connection with timeout — fall back to standalone rendering
let mcpConnected = false;
try {
  const app = new App({ name: "powerbi-mcp-visualizer", version: "1.0.0" });

  app.ontoolinput = () => {};
  app.ontoolresult = () => { render(); };

  app.onhostcontextchanged = (ctx) => {
    if (ctx.theme) applyDocumentTheme(ctx.theme);
    if (ctx.styles?.variables) applyHostStyleVariables(ctx.styles.variables);
    if (ctx.styles?.css?.fonts) applyHostFonts(ctx.styles.css.fonts);
    if (ctx.safeAreaInsets) {
      const { top, right, bottom, left } = ctx.safeAreaInsets;
      document.body.style.padding = `${top}px ${right}px ${bottom}px ${left}px`;
    }
  };

  app.onteardown = async () => ({ state: {} });

  // Race connect against a 2-second timeout for standalone mode
  await Promise.race([
    app.connect(),
    new Promise((_, reject) => setTimeout(() => reject(new Error("timeout")), 2000))
  ]);
  mcpConnected = true;
} catch {
  // Not inside MCP host — render standalone
}

// ── Inject CSS ───────────────────────────────────────────────────────────────
const style = document.createElement("style");
style.textContent = `
  :root {
    --bg-primary: #1e1e1e;
    --bg-secondary: #252526;
    --bg-tertiary: #2d2d30;
    --bg-hover: #37373d;
    --text-primary: #cccccc;
    --text-secondary: #9d9d9d;
    --text-muted: #6b6b6b;
    --accent: #4fc1ff;
    --accent-green: #4ec9b0;
    --accent-yellow: #dcdcaa;
    --accent-orange: #ce9178;
    --accent-purple: #c586c0;
    --border: #3c3c3c;
    --badge-read-bg: rgba(78, 201, 176, 0.15);
    --badge-read-fg: #4ec9b0;
    --badge-write-bg: rgba(206, 145, 120, 0.15);
    --badge-write-fg: #ce9178;
    --badge-idempotent-bg: rgba(220, 220, 170, 0.15);
    --badge-idempotent-fg: #dcdcaa;
    --badge-optional-bg: rgba(197, 134, 192, 0.15);
    --badge-optional-fg: #c586c0;
    --font-mono: 'Cascadia Code', 'Fira Code', 'Consolas', 'Courier New', monospace;
    --font-sans: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
  }
  * { margin: 0; padding: 0; box-sizing: border-box; }
  body {
    font-family: var(--vscode-font-family, var(--font-sans));
    font-size: var(--vscode-font-size, 13px);
    color: var(--vscode-foreground, var(--text-primary));
    background: var(--vscode-editor-background, var(--bg-primary));
    line-height: 1.5;
    padding: 16px;
  }
  #root { max-width: 960px; margin: 0 auto; }

  /* Header */
  .header { text-align: center; margin-bottom: 24px; }
  .header h1 { font-size: 22px; font-weight: 600; margin-bottom: 6px; color: var(--vscode-foreground, var(--text-primary)); }
  .header p { font-size: 13px; color: var(--vscode-descriptionForeground, var(--text-secondary)); max-width: 600px; margin: 0 auto 16px; }

  /* Stats bar */
  .stats-bar { display: flex; justify-content: center; gap: 24px; flex-wrap: wrap; }
  .stat { text-align: center; }
  .stat-value { display: block; font-size: 24px; font-weight: 700; color: var(--vscode-foreground, var(--accent)); }
  .stat-label { font-size: 11px; text-transform: uppercase; letter-spacing: 0.5px; color: var(--vscode-descriptionForeground, var(--text-muted)); }

  /* Tabs */
  .tabs { display: flex; gap: 4px; margin-bottom: 20px; border-bottom: 1px solid var(--vscode-panel-border, var(--border)); padding-bottom: 0; }
  .tab { background: none; border: none; padding: 8px 16px; font-size: 13px; cursor: pointer; color: var(--vscode-descriptionForeground, var(--text-secondary)); border-bottom: 2px solid transparent; transition: all 0.15s; font-family: inherit; }
  .tab:hover { color: var(--vscode-foreground, var(--text-primary)); }
  .tab.active { color: var(--vscode-foreground, var(--accent)); border-bottom-color: var(--vscode-focusBorder, var(--accent)); }

  /* Sections */
  .section { display: none; }
  .section.active { display: block; }

  /* Search */
  .search-box { width: 100%; padding: 8px 12px; margin-bottom: 16px; background: var(--vscode-input-background, var(--bg-secondary)); color: var(--vscode-input-foreground, var(--text-primary)); border: 1px solid var(--vscode-input-border, var(--border)); border-radius: 4px; font-size: 13px; font-family: inherit; outline: none; }
  .search-box:focus { border-color: var(--vscode-focusBorder, var(--accent)); }
  .search-box::placeholder { color: var(--vscode-input-placeholderForeground, var(--text-muted)); }

  /* Domain cards */
  .domain-grid { display: flex; flex-direction: column; gap: 12px; }
  .domain-card { background: var(--vscode-editor-background, var(--bg-secondary)); border: 1px solid var(--vscode-panel-border, var(--border)); border-radius: 8px; overflow: hidden; }
  .domain-header { display: flex; align-items: center; gap: 12px; padding: 14px 16px; cursor: pointer; transition: background 0.15s; }
  .domain-header:hover { background: var(--vscode-list-hoverBackground, var(--bg-hover)); }
  .domain-icon { font-size: 24px; flex-shrink: 0; }
  .domain-info { flex: 1; min-width: 0; }
  .domain-name { font-weight: 600; font-size: 14px; }
  .domain-desc { font-size: 12px; color: var(--vscode-descriptionForeground, var(--text-secondary)); margin-top: 2px; }
  .domain-badge { background: var(--vscode-badge-background, var(--bg-tertiary)); color: var(--vscode-badge-foreground, var(--text-secondary)); padding: 2px 10px; border-radius: 12px; font-size: 11px; font-weight: 600; white-space: nowrap; }
  .domain-chevron { font-size: 16px; color: var(--vscode-descriptionForeground, var(--text-muted)); transition: transform 0.2s; }
  .domain-chevron.open { transform: rotate(90deg); }

  /* Tool list inside domain */
  .domain-tools { display: none; border-top: 1px solid var(--vscode-panel-border, var(--border)); }
  .domain-tools.open { display: block; }
  .tool-item { padding: 10px 16px 10px 52px; border-bottom: 1px solid var(--vscode-panel-border, rgba(60,60,60,0.5)); cursor: pointer; transition: background 0.15s; }
  .tool-item:last-child { border-bottom: none; }
  .tool-item:hover { background: var(--vscode-list-hoverBackground, var(--bg-hover)); }
  .tool-item-header { display: flex; align-items: center; gap: 8px; }
  .tool-name { font-family: var(--vscode-editor-font-family, var(--font-mono)); font-size: 12px; color: var(--accent); }
  .tool-title { font-size: 13px; color: var(--vscode-foreground, var(--text-primary)); flex: 1; }
  .tool-badges { display: flex; gap: 4px; }
  .badge { padding: 1px 7px; border-radius: 3px; font-size: 10px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.3px; }
  .badge-read { background: var(--badge-read-bg); color: var(--badge-read-fg); }
  .badge-write { background: var(--badge-write-bg); color: var(--badge-write-fg); }
  .badge-idempotent { background: var(--badge-idempotent-bg); color: var(--badge-idempotent-fg); }

  /* Tool detail panel */
  .tool-detail { display: none; margin-top: 8px; padding: 12px; background: var(--vscode-editor-background, var(--bg-primary)); border: 1px solid var(--vscode-panel-border, var(--border)); border-radius: 6px; }
  .tool-detail.open { display: block; }
  .tool-detail-desc { font-size: 12px; color: var(--vscode-descriptionForeground, var(--text-secondary)); margin-bottom: 10px; line-height: 1.5; }
  .param-table { width: 100%; border-collapse: collapse; font-size: 12px; }
  .param-table th { text-align: left; padding: 6px 8px; font-size: 10px; text-transform: uppercase; letter-spacing: 0.5px; color: var(--vscode-descriptionForeground, var(--text-muted)); border-bottom: 1px solid var(--vscode-panel-border, var(--border)); }
  .param-table td { padding: 6px 8px; border-bottom: 1px solid var(--vscode-panel-border, rgba(60,60,60,0.3)); vertical-align: top; }
  .param-name { font-family: var(--vscode-editor-font-family, var(--font-mono)); color: var(--accent-yellow); font-size: 11px; }
  .param-type { font-family: var(--vscode-editor-font-family, var(--font-mono)); color: var(--accent-green); font-size: 11px; }
  .param-desc { color: var(--vscode-descriptionForeground, var(--text-secondary)); }
  .badge-optional { background: var(--badge-optional-bg); color: var(--badge-optional-fg); }
  .badge-required { background: rgba(79, 193, 255, 0.15); color: #4fc1ff; }
  .no-params { color: var(--vscode-descriptionForeground, var(--text-muted)); font-style: italic; font-size: 12px; }

  /* Architecture */
  .arch-section { margin-bottom: 24px; }
  .arch-title { display: flex; align-items: center; gap: 8px; font-size: 15px; font-weight: 600; margin-bottom: 12px; }
  .layer-card { background: var(--vscode-editor-background, var(--bg-secondary)); border: 1px solid var(--vscode-panel-border, var(--border)); border-radius: 6px; padding: 14px 16px; margin-bottom: 8px; }
  .layer-name { font-weight: 600; font-family: var(--vscode-editor-font-family, var(--font-mono)); color: var(--accent-yellow); font-size: 13px; }
  .layer-desc { font-size: 12px; color: var(--vscode-descriptionForeground, var(--text-secondary)); margin: 4px 0 8px; }
  .file-list { display: flex; flex-wrap: wrap; gap: 4px; }
  .file-tag { font-family: var(--vscode-editor-font-family, var(--font-mono)); font-size: 11px; padding: 2px 8px; border-radius: 3px; background: var(--vscode-badge-background, var(--bg-tertiary)); color: var(--vscode-badge-foreground, var(--text-secondary)); }
  .transport-grid { display: flex; gap: 8px; flex-wrap: wrap; }
  .transport-card { display: flex; align-items: center; gap: 6px; padding: 8px 14px; background: var(--vscode-editor-background, var(--bg-secondary)); border: 1px solid var(--vscode-panel-border, var(--border)); border-radius: 6px; font-size: 12px; }
  .transport-dot { width: 6px; height: 6px; border-radius: 50%; background: var(--accent-green); }
  .dep-grid { display: flex; flex-direction: column; gap: 6px; }
  .dep-card { padding: 8px 14px; background: var(--vscode-editor-background, var(--bg-secondary)); border: 1px solid var(--vscode-panel-border, var(--border)); border-radius: 6px; font-size: 12px; }

  /* Connection flow */
  .flow-container { max-width: 600px; margin: 0 auto; }
  .flow-intro { text-align: center; margin-bottom: 20px; font-size: 13px; color: var(--vscode-descriptionForeground, var(--text-secondary)); }
  .flow-step { display: flex; gap: 16px; min-height: 48px; }
  .flow-line { display: flex; flex-direction: column; align-items: center; width: 20px; }
  .flow-dot { width: 12px; height: 12px; border-radius: 50%; background: var(--vscode-focusBorder, var(--accent)); flex-shrink: 0; margin-top: 4px; }
  .flow-connector { width: 2px; flex: 1; background: var(--vscode-panel-border, var(--border)); margin: 4px 0; }
  .flow-content { flex: 1; padding-bottom: 16px; font-size: 13px; line-height: 1.5; }
  .flow-content code { font-family: var(--vscode-editor-font-family, var(--font-mono)); font-size: 11px; color: var(--accent); background: var(--vscode-badge-background, var(--bg-secondary)); padding: 1px 6px; border-radius: 3px; }

  /* Diagram */
  .diagram { text-align: center; margin-bottom: 24px; padding: 20px; background: var(--vscode-editor-background, var(--bg-secondary)); border: 1px solid var(--vscode-panel-border, var(--border)); border-radius: 8px; }
  .diagram-label { font-size: 14px; font-weight: 600; margin-bottom: 16px; }
  .diagram-row { display: flex; align-items: center; justify-content: center; gap: 8px; flex-wrap: wrap; }
  .diagram-box { padding: 10px 16px; border-radius: 6px; font-size: 13px; font-weight: 500; border: 1px solid var(--vscode-panel-border, var(--border)); }
  .diagram-box.host { background: rgba(79, 193, 255, 0.1); color: var(--accent); }
  .diagram-box.server { background: rgba(78, 201, 176, 0.1); color: var(--accent-green); }
  .diagram-box.service { background: rgba(220, 220, 170, 0.1); color: var(--accent-yellow); }
  .diagram-box.target { background: rgba(206, 145, 120, 0.1); color: var(--accent-orange); }
  .diagram-arrow { font-size: 18px; color: var(--vscode-descriptionForeground, var(--text-muted)); }
`;
document.head.appendChild(style);

// ── Initial render ───────────────────────────────────────────────────────────
render();

// ── Render ───────────────────────────────────────────────────────────────────
function render() {
  const root = document.getElementById("root")!;

  const totalTools = domains.reduce((s, d) => s + d.tools.length, 0);
  const readOnlyTools = domains.reduce((s, d) => s + d.tools.filter(t => t.readOnly).length, 0);
  const writeTools = totalTools - readOnlyTools;

  root.innerHTML = `
    <div class="header">
      <h1>Power BI Modeling MCP Server</h1>
      <p>Open-source C#/.NET 8 MCP server exposing 71 tools for AI agents to manage Power BI semantic models. Supports stdio and HTTP transports.</p>
      <div class="stats-bar">
        <div class="stat"><span class="stat-value">${domains.length}</span><span class="stat-label">Domains</span></div>
        <div class="stat"><span class="stat-value">${totalTools}</span><span class="stat-label">Tools</span></div>
        <div class="stat"><span class="stat-value">${readOnlyTools}</span><span class="stat-label">Read-Only</span></div>
        <div class="stat"><span class="stat-value">${writeTools}</span><span class="stat-label">Write</span></div>
      </div>
    </div>

    <div class="tabs" id="tabs">
      ${tab("overview", "Overview")}
      ${tab("tools", "Tool Explorer")}
      ${tab("architecture", "Architecture")}
      ${tab("connection-flow", "Connection Flow")}
    </div>

    <div class="section ${currentTab === "overview" ? "active" : ""}">${renderOverview()}</div>
    <div class="section ${currentTab === "tools" ? "active" : ""}">${renderToolExplorer()}</div>
    <div class="section ${currentTab === "architecture" ? "active" : ""}">${renderArchitecture()}</div>
    <div class="section ${currentTab === "connection-flow" ? "active" : ""}">${renderConnectionFlow()}</div>
  `;

  wireEvents();
}

function tab(id: string, label: string) {
  return `<button class="tab ${currentTab === id ? "active" : ""}" data-tab="${id}">${label}</button>`;
}

// ── Overview ─────────────────────────────────────────────────────────────────
function renderOverview(): string {
  return `
    <div class="diagram">
      <div class="diagram-label">How It Works</div>
      <div class="diagram-row">
        <div class="diagram-box host">🤖 AI Agent / Host</div>
        <span class="diagram-arrow">→</span>
        <div class="diagram-box server">⚙️ MCP Server (.NET 8)</div>
        <span class="diagram-arrow">→</span>
        <div class="diagram-box service">🔧 TOM / ADOMD</div>
        <span class="diagram-arrow">→</span>
        <div class="diagram-box target">📊 Power BI</div>
      </div>
      <div class="diagram-row" style="margin-top:12px;">
        <div class="diagram-box host" style="font-size:11px;padding:6px 12px">VS Code · Copilot · Claude</div>
        <span class="diagram-arrow"> </span>
        <div class="diagram-box server" style="font-size:11px;padding:6px 12px">stdio / HTTP</div>
        <span class="diagram-arrow"> </span>
        <div class="diagram-box service" style="font-size:11px;padding:6px 12px">ConnectionManager · TmdlService</div>
        <span class="diagram-arrow"> </span>
        <div class="diagram-box target" style="font-size:11px;padding:6px 12px">Desktop · Fabric · PBIP</div>
      </div>
    </div>

    <div class="domain-grid">
      ${domains.map(d => `
        <div class="domain-card">
          <div class="domain-header" data-domain-toggle="${esc(d.name)}">
            <div class="domain-icon">${d.icon}</div>
            <div class="domain-info">
              <div class="domain-name">${esc(d.name)}</div>
              <div class="domain-desc">${esc(d.description)}</div>
            </div>
            <div class="domain-badge">${d.tools.length} tools</div>
            <div class="domain-chevron ${expandedDomain === d.name ? "open" : ""}">▶</div>
          </div>
          <div class="domain-tools ${expandedDomain === d.name ? "open" : ""}">
            ${d.tools.map(t => renderToolItem(t)).join("")}
          </div>
        </div>
      `).join("")}
    </div>
  `;
}

// ── Tool Explorer (searchable flat list) ─────────────────────────────────────
function renderToolExplorer(): string {
  const q = searchQuery.toLowerCase();
  const filtered = domains.flatMap(d =>
    d.tools
      .filter(t => !q || t.name.includes(q) || t.title.toLowerCase().includes(q) || t.description.toLowerCase().includes(q) || d.name.toLowerCase().includes(q))
      .map(t => ({ ...t, domainName: d.name, domainIcon: d.icon }))
  );

  return `
    <input class="search-box" id="tool-search" type="text" placeholder="Search ${domains.reduce((s,d) => s + d.tools.length, 0)} tools by name, domain, or description..." value="${esc(searchQuery)}" />
    <div style="margin-bottom:12px;font-size:12px;color:var(--vscode-descriptionForeground,var(--text-muted));">${filtered.length} tool${filtered.length !== 1 ? "s" : ""} found. Click any tool to expand parameter details.</div>
    <div class="domain-grid">
      ${filtered.map(t => `
        <div class="domain-card" style="border-left:3px solid ${t.readOnly ? "var(--badge-read-fg)" : "var(--badge-write-fg)"};">
          ${renderToolItem(t, true)}
        </div>
      `).join("")}
    </div>
  `;
}

// ── Single tool item ─────────────────────────────────────────────────────────
function renderToolItem(t: Tool, showDomain = false): string {
  const isExpanded = expandedTool === t.name;
  const extra = t as unknown as { domainName?: string; domainIcon?: string };
  const domainInfo = showDomain && extra.domainName ? `<span style="font-size:11px;color:var(--vscode-descriptionForeground,var(--text-muted));margin-right:8px;">${extra.domainIcon ?? ""} ${esc(extra.domainName)}</span>` : "";

  return `
    <div class="tool-item" data-tool-toggle="${esc(t.name)}">
      <div class="tool-item-header">
        ${domainInfo}
        <span class="tool-name">${esc(t.name)}</span>
        <span class="tool-title">${esc(t.title)}</span>
        <div class="tool-badges">
          <span class="badge ${t.readOnly ? "badge-read" : "badge-write"}">${t.readOnly ? "read" : "write"}</span>
          ${t.idempotent ? '<span class="badge badge-idempotent">idempotent</span>' : ""}
        </div>
      </div>
      <div class="tool-detail ${isExpanded ? "open" : ""}">
        <div class="tool-detail-desc">${esc(t.description)}</div>
        ${t.parameters.length > 0 ? `
          <table class="param-table">
            <thead><tr><th>Parameter</th><th>Type</th><th>Required</th><th>Description</th></tr></thead>
            <tbody>
              ${t.parameters.map(p => `
                <tr>
                  <td class="param-name">${esc(p.name)}</td>
                  <td class="param-type">${esc(p.type)}</td>
                  <td><span class="badge ${p.optional ? "badge-optional" : "badge-required"}">${p.optional ? "optional" : "required"}</span></td>
                  <td class="param-desc">${esc(p.description)}</td>
                </tr>
              `).join("")}
            </tbody>
          </table>
        ` : '<div class="no-params">No parameters</div>'}
      </div>
    </div>
  `;
}

// ── Architecture ─────────────────────────────────────────────────────────────
function renderArchitecture(): string {
  const layers = [
    { name: "Models", description: "POCOs for connection metadata and server settings", files: ["ConnectionInfo.cs", "ServerSettings.cs"] },
    { name: "Services", description: "Stateful singletons for TOM connections, DAX execution, TMDL, and PBI Desktop discovery", files: ["ConnectionManager.cs", "PowerBiDesktopDiscovery.cs", "TmdlService.cs"] },
    { name: "Tools", description: "MCP tool classes — one per domain, auto-discovered via assembly scanning", files: ["ConnectionTools.cs", "ModelTools.cs", "TableTools.cs", "ColumnTools.cs", "MeasureTools.cs", "RelationshipTools.cs", "DaxQueryTools.cs", "HierarchyTools.cs", "PartitionTools.cs", "PerspectiveTools.cs", "RoleTools.cs", "CalculationGroupTools.cs", "TmdlTools.cs"] }
  ];
  const transports = ["stdio (default — VS Code, GitHub Copilot)", "HTTP / Streamable-HTTP"];
  const deps = ["ModelContextProtocol SDK for .NET", "Microsoft.AnalysisServices (TOM)", "Microsoft.AnalysisServices.AdomdClient (DAX)", "Azure.Identity (Fabric auth)"];

  return `
    <div class="arch-section">
      <div class="arch-title">🏛️ Three-Layer Architecture</div>
      ${layers.map(l => `
        <div class="layer-card">
          <div class="layer-name">${esc(l.name)}/</div>
          <div class="layer-desc">${esc(l.description)}</div>
          <div class="file-list">${l.files.map(f => `<span class="file-tag">${esc(f)}</span>`).join("")}</div>
        </div>
      `).join("")}
    </div>
    <div class="arch-section">
      <div class="arch-title">🔌 Transports</div>
      <div class="transport-grid">${transports.map(t => `<div class="transport-card"><span class="transport-dot"></span>${esc(t)}</div>`).join("")}</div>
    </div>
    <div class="arch-section">
      <div class="arch-title">📦 Key Dependencies</div>
      <div class="dep-grid">${deps.map(d => `<div class="dep-card">${esc(d)}</div>`).join("")}</div>
    </div>
  `;
}

// ── Connection Flow ──────────────────────────────────────────────────────────
function renderConnectionFlow(): string {
  const steps = [
    { text: "Call <code>connection_connect_desktop</code>, <code>connection_connect_fabric</code>, or <code>connection_open_pbip</code>" },
    { text: "Receive a <code>connectionId</code> string" },
    { text: "Pass <code>connectionId</code> to any subsequent tool call" },
    { text: "<code>ConnectionManager.GetModel(connectionId)</code> → TOM Model for read/write" },
    { text: "<code>ConnectionManager.ExecuteDaxQuery(connectionId, dax)</code> → ADOMD for queries" },
  ];

  return `
    <div class="flow-container">
      <div class="flow-intro">All tools (except connection tools) require a <code>connectionId</code> obtained from the connection flow below.</div>
      ${steps.map((s, i) => `
        <div class="flow-step">
          <div class="flow-line">
            <div class="flow-dot"></div>
            ${i < steps.length - 1 ? '<div class="flow-connector"></div>' : ""}
          </div>
          <div class="flow-content"><strong>${i + 1}.</strong> ${s.text}</div>
        </div>
      `).join("")}
    </div>
  `;
}

// ── Event wiring ─────────────────────────────────────────────────────────────
function wireEvents() {
  // Tab clicks
  document.querySelectorAll<HTMLElement>(".tab").forEach(btn => {
    btn.addEventListener("click", () => {
      currentTab = btn.dataset.tab!;
      render();
    });
  });

  // Domain toggle
  document.querySelectorAll<HTMLElement>("[data-domain-toggle]").forEach(el => {
    el.addEventListener("click", () => {
      const name = el.dataset.domainToggle!;
      expandedDomain = expandedDomain === name ? null : name;
      render();
    });
  });

  // Tool toggle
  document.querySelectorAll<HTMLElement>("[data-tool-toggle]").forEach(el => {
    el.addEventListener("click", (e) => {
      e.stopPropagation();
      const name = el.dataset.toolToggle!;
      expandedTool = expandedTool === name ? null : name;
      render();
    });
  });

  // Search
  const searchInput = document.getElementById("tool-search") as HTMLInputElement | null;
  if (searchInput) {
    searchInput.addEventListener("input", () => {
      searchQuery = searchInput.value;
      render();
      // Re-focus search input after render
      const newInput = document.getElementById("tool-search") as HTMLInputElement;
      if (newInput) {
        newInput.focus();
        newInput.setSelectionRange(newInput.value.length, newInput.value.length);
      }
    });
  }
}

// ── Helpers ───────────────────────────────────────────────────────────────────
function esc(s: string): string {
  const el = document.createElement("span");
  el.textContent = s;
  return el.innerHTML;
}
