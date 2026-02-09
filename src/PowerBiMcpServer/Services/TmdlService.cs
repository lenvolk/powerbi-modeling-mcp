using System.Text;
using System.Text.Json;
using Microsoft.AnalysisServices.Tabular;

namespace PowerBiMcpServer.Services;

/// <summary>
/// Reads and writes TMDL (Tabular Model Definition Language) folder structures
/// used by Power BI Project (PBIP) files.
/// </summary>
public sealed class TmdlService
{
    /// <summary>
    /// Loads a tabular database from a TMDL folder (the "definition/" folder
    /// inside a .SemanticModel directory of a PBIP project).
    /// </summary>
    public Database LoadFromFolder(string tmdlFolderPath)
    {
        if (!Directory.Exists(tmdlFolderPath))
            throw new DirectoryNotFoundException($"TMDL folder not found: {tmdlFolderPath}");

        var db = TmdlSerializer.DeserializeDatabaseFromFolder(tmdlFolderPath);
        return db;
    }

    /// <summary>
    /// Serializes a tabular database/model to a TMDL folder.
    /// </summary>
    public void SaveToFolder(Database database, string tmdlFolderPath)
    {
        Directory.CreateDirectory(tmdlFolderPath);
        TmdlSerializer.SerializeDatabaseToFolder(database, tmdlFolderPath);
    }

    /// <summary>
    /// Discovers .SemanticModel folders in a PBIP project root directory.
    /// Returns paths to the definition/ subfolders.
    /// </summary>
    public IReadOnlyList<string> DiscoverSemanticModels(string projectRoot)
    {
        var results = new List<string>();

        if (!Directory.Exists(projectRoot))
            return results;

        // PBIP structure: *.SemanticModel/definition/
        foreach (var dir in Directory.GetDirectories(projectRoot, "*.SemanticModel", SearchOption.AllDirectories))
        {
            var defDir = Path.Combine(dir, "definition");
            if (Directory.Exists(defDir))
                results.Add(defDir);
        }

        return results;
    }

    /// <summary>
    /// Generates a model summary from the TMDL folder.
    /// </summary>
    public string GetModelSummary(string tmdlFolderPath)
    {
        var db = LoadFromFolder(tmdlFolderPath);
        var model = db.Model;

        var sb = new StringBuilder();
        sb.AppendLine($"# Model: {db.Name}");
        sb.AppendLine();
        sb.AppendLine($"- **Tables**: {model.Tables.Count}");
        sb.AppendLine($"- **Relationships**: {model.Relationships.Count}");
        sb.AppendLine($"- **Cultures**: {model.Cultures.Count}");
        sb.AppendLine($"- **Perspectives**: {model.Perspectives.Count}");
        sb.AppendLine($"- **Roles**: {model.Roles.Count}");
        sb.AppendLine();

        foreach (var table in model.Tables)
        {
            var measureCount = table.Measures.Count;
            var colCount     = table.Columns.Count;
            sb.AppendLine($"## Table: {table.Name}  ({colCount} columns, {measureCount} measures)");
        }

        return sb.ToString();
    }
}
