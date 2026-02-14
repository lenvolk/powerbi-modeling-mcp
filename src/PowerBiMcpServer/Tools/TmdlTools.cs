using System.ComponentModel;
using System.Text;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using PowerBiMcpServer.Models;
using PowerBiMcpServer.Services;

namespace PowerBiMcpServer.Tools;

/// <summary>
/// Tools for working with TMDL (Tabular Model Definition Language) and PBIP (Power BI Project) files.
/// </summary>
[McpServerToolType]
public sealed class TmdlTools
{
    private readonly ConnectionManager _cm;
    private readonly TmdlService _tmdl;
    private readonly ServerSettings _settings;

    public TmdlTools(ConnectionManager cm, TmdlService tmdl, ServerSettings settings)
    {
        _cm = cm;
        _tmdl = tmdl;
        _settings = settings;
    }

    [McpServerTool(Name = "tmdl_export",
        Title = "Export Model to TMDL Folder",
        ReadOnly = false)]
    [Description("Exports the connected semantic model to a TMDL folder on disk. Creates the folder if it doesn't exist.")]
    public string ExportTmdl(
        [Description("Connection ID")] string connectionId,
        [Description("Full path to the target TMDL folder")] string folderPath)
    {
        try
        {
            if (_settings.IsReadOnly)
                return "Error: Cannot export in read-only mode. Restart without --readonly.";

            folderPath = ValidatePath(folderPath);
            var db = _cm.GetDatabase(connectionId);
            _tmdl.SaveToFolder(db, folderPath);
            return $"Model exported to TMDL folder: `{folderPath}`";
        }
        catch (UnauthorizedAccessException ex) { return $"Error: {ex.Message}"; }
        catch (Exception ex) { return $"Error exporting TMDL: {ex.Message}"; }
    }

    [McpServerTool(Name = "tmdl_import",
        Title = "Import TMDL Folder to Model",
        ReadOnly = false, Destructive = true)]
    [Description("Imports a TMDL folder into the connected semantic model, replacing the current model definition. Use with caution.")]
    public string ImportTmdl(
        [Description("Connection ID")] string connectionId,
        [Description("Full path to the TMDL folder to import")] string folderPath)
    {
        try
        {
            folderPath = ValidatePath(folderPath);
            if (!Directory.Exists(folderPath))
                return $"Error: Folder not found: `{folderPath}`";

            var db = _cm.GetDatabase(connectionId);
            var imported = _tmdl.LoadFromFolder(folderPath);
            imported.Model.CopyTo(db.Model);
            _cm.SaveChanges(connectionId);
            return $"TMDL folder imported and model updated from: `{folderPath}`";
        }
        catch (UnauthorizedAccessException ex) { return $"Error: {ex.Message}"; }
        catch (Exception ex) { return $"Error importing TMDL: {ex.Message}"; }
    }

    [McpServerTool(Name = "tmdl_read_folder",
        Title = "Read TMDL Folder Summary",
        ReadOnly = true, Idempotent = true)]
    [Description("Reads a TMDL folder from disk and returns a summary of the model definition without connecting to any server.")]
    public string ReadTmdlFolder(
        [Description("Full path to the TMDL folder")] string folderPath)
    {
        try
        {
            folderPath = ValidatePath(folderPath);
            if (!Directory.Exists(folderPath))
                return $"Error: Folder not found: `{folderPath}`";

            return _tmdl.GetModelSummary(folderPath);
        }
        catch (UnauthorizedAccessException ex) { return $"Error: {ex.Message}"; }
        catch (Exception ex) { return $"Error reading TMDL: {ex.Message}"; }
    }

    [McpServerTool(Name = "tmdl_read_file",
        Title = "Read TMDL File Content",
        ReadOnly = true, Idempotent = true)]
    [Description("Reads and returns the raw content of a specific .tmdl file from disk.")]
    public string ReadTmdlFile(
        [Description("Full path to the .tmdl file")] string filePath)
    {
        try
        {
            filePath = ValidatePath(filePath);

            // Only allow reading TMDL-related files
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext is not ".tmdl" and not ".json")
                return $"Error: Only .tmdl and .json files can be read. Got: '{ext}'";

            if (!File.Exists(filePath))
                return $"Error: File not found: `{filePath}`";

            var content = File.ReadAllText(filePath);
            return $"```tmdl\n{content}\n```";
        }
        catch (UnauthorizedAccessException ex) { return $"Error: {ex.Message}"; }
        catch (Exception ex) { return $"Error reading file: {ex.Message}"; }
    }

    [McpServerTool(Name = "pbip_discover",
        Title = "Discover PBIP Project",
        ReadOnly = true, Idempotent = true)]
    [Description("Scans a directory for Power BI Project (.pbip) files and returns the project structure including model, report, and dataset paths.")]
    public string DiscoverPbipProject(
        [Description("Path to the directory to scan for .pbip files")] string directoryPath)
    {
        try
        {
            directoryPath = ValidatePath(directoryPath);
            if (!Directory.Exists(directoryPath))
                return $"Error: Directory not found: `{directoryPath}`";

            var sb = new StringBuilder(512);
            sb.AppendLine("## PBIP Project Discovery\n");

            // Find .pbip files (TopDirectoryOnly to prevent full-disk scans)
            var pbipFiles = Directory.GetFiles(directoryPath, "*.pbip", SearchOption.TopDirectoryOnly);
            if (pbipFiles.Length == 0)
            {
                sb.AppendLine("No `.pbip` files found in the directory.");
                return sb.ToString();
            }

            foreach (var pbip in pbipFiles)
            {
                sb.AppendLine($"### {Path.GetFileName(pbip)}");
                sb.AppendLine($"- Path: `{pbip}`");

                var dir = Path.GetDirectoryName(pbip)!;
                var modelDirs = Directory.GetDirectories(dir, "*.SemanticModel", SearchOption.TopDirectoryOnly);
                var reportDirs = Directory.GetDirectories(dir, "*.Report", SearchOption.TopDirectoryOnly);

                if (modelDirs.Length > 0)
                {
                    foreach (var md in modelDirs)
                    {
                        sb.AppendLine($"- Semantic Model: `{md}`");
                        var defFolder = Path.Combine(md, "definition");
                        if (Directory.Exists(defFolder))
                        {
                            sb.AppendLine($"  - Definition folder: `{defFolder}`");
                            var tmdlFiles = Directory.GetFiles(defFolder, "*.tmdl", SearchOption.AllDirectories);
                            sb.AppendLine($"  - TMDL files: {tmdlFiles.Length}");
                        }
                    }
                }

                if (reportDirs.Length > 0)
                {
                    foreach (var rd in reportDirs)
                        sb.AppendLine($"- Report: `{rd}`");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }
        catch (UnauthorizedAccessException ex) { return $"Error: {ex.Message}"; }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    [McpServerTool(Name = "tmdl_list_files",
        Title = "List TMDL Files in Folder",
        ReadOnly = true, Idempotent = true)]
    [Description("Lists all TMDL files in a folder with their relative paths, organized by subfolder.")]
    public string ListTmdlFiles(
        [Description("Full path to the TMDL folder")] string folderPath)
    {
        try
        {
            folderPath = ValidatePath(folderPath);
            if (!Directory.Exists(folderPath))
                return $"Error: Folder not found: `{folderPath}`";

            var files = Directory.GetFiles(folderPath, "*.tmdl", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(folderPath, f))
                .OrderBy(f => f)
                .ToArray();

            if (files.Length == 0)
                return "No `.tmdl` files found in the folder.";

            var sb = new StringBuilder(1024);
            sb.AppendLine($"## TMDL Files ({files.Length})\n");
            string lastDir = "";
            foreach (var f in files)
            {
                var dir = Path.GetDirectoryName(f) ?? "";
                if (dir != lastDir)
                {
                    sb.AppendLine($"\n### {(string.IsNullOrEmpty(dir) ? "(root)" : dir)}");
                    lastDir = dir;
                }
                sb.AppendLine($"- {Path.GetFileName(f)}");
            }

            return sb.ToString();
        }
        catch (UnauthorizedAccessException ex) { return $"Error: {ex.Message}"; }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    /// <summary>
    /// Validates and canonicalizes a path. Rejects paths with traversal sequences
    /// and paths to system directories.
    /// </summary>
    private static string ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new UnauthorizedAccessException("Path cannot be empty.");

        var fullPath = Path.GetFullPath(path);

        // Block well-known system directories
        var blocked = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
            "/etc", "/var", "/usr", "/bin", "/sbin", "/boot", "/proc", "/sys"
        };

        foreach (var b in blocked)
        {
            if (!string.IsNullOrEmpty(b) && fullPath.StartsWith(b, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException($"Access to system directory '{b}' is not allowed.");
        }

        return fullPath;
    }
}
