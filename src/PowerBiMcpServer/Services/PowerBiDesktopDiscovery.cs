using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace PowerBiMcpServer.Services;

/// <summary>
/// Discovers Power BI Desktop instances running on the local machine by
/// locating the Analysis Services (msmdsrv.exe) processes spawned by PBI Desktop
/// and reading their port files.
/// </summary>
public sealed class PowerBiDesktopDiscovery
{
    private readonly ILogger<PowerBiDesktopDiscovery> _logger;

    public PowerBiDesktopDiscovery(ILogger<PowerBiDesktopDiscovery>? logger = null)
    {
        _logger = logger ?? NullLogger<PowerBiDesktopDiscovery>.Instance;
    }

    /// <summary>
    /// Returns a list of (fileName, port) tuples for each running PBI Desktop instance.
    /// </summary>
    public IReadOnlyList<PbiDesktopInstance> Discover()
    {
        var results = new List<PbiDesktopInstance>();

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return results; // PBI Desktop only runs on Windows

        try
        {
            // PBI Desktop launches msmdsrv.exe as a child process. Each instance
            // writes its port to a file named "msmdsrv.port.txt" inside a temp folder
            // under the user's LocalAppData.
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var pbiTempRoot  = Path.Combine(localAppData, "Microsoft", "Power BI Desktop", "AnalysisServicesWorkspaces");

            if (!Directory.Exists(pbiTempRoot))
                return results;

            Process[]? pbiProcs = null;

            foreach (var workspaceDir in Directory.GetDirectories(pbiTempRoot))
            {
                var dataDir  = Path.Combine(workspaceDir, "Data");
                var portFile = Path.Combine(dataDir, "msmdsrv.port.txt");

                if (!File.Exists(portFile)) continue;

                var portStr = File.ReadAllText(portFile, System.Text.Encoding.Unicode).Trim();
                if (!int.TryParse(portStr, out var port))
                {
                    // Fallback: try stripping null characters in case of encoding mismatch
                    portStr = new string(portStr.Where(c => c != '\0').ToArray()).Trim();
                    if (!int.TryParse(portStr, out port)) continue;
                }

                // Enumerate PBI processes once and reuse across all workspaces
                pbiProcs ??= GetPbiProcesses();

                var fileName = TryGetPbiFileName(workspaceDir, pbiProcs) ?? $"Power BI Desktop (port {port})";

                results.Add(new PbiDesktopInstance(fileName, port, workspaceDir));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Power BI Desktop discovery failed: {Message}", ex.Message);
        }

        return results;
    }

    /// <summary>
    /// Finds the PBI Desktop instance whose window title contains the given file name.
    /// </summary>
    public PbiDesktopInstance? FindByFileName(string fileName)
    {
        var instances = Discover();
        return instances.FirstOrDefault(i =>
            i.FileName.Contains(fileName, StringComparison.OrdinalIgnoreCase));
    }

    private static Process[] GetPbiProcesses()
    {
        var procs = Process.GetProcessesByName("PBIDesktop");
        return procs.Length > 0 ? procs : Process.GetProcessesByName("Microsoft.PowerBI.Desktop");
    }

    private static string? TryGetPbiFileName(string workspaceDir, Process[] pbiProcs)
    {
        try
        {
            foreach (var proc in pbiProcs)
            {
                try
                {
                    var title = proc.MainWindowTitle;
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        // The title is typically: "FileName - Power BI Desktop"
                        var dashIdx = title.LastIndexOf(" - Power BI Desktop", StringComparison.OrdinalIgnoreCase);
                        if (dashIdx > 0)
                            return title[..dashIdx].Trim();
                        return title;
                    }
                }
                catch { }
            }
        }
        catch { }
        return null;
    }
}

public sealed record PbiDesktopInstance(string FileName, int Port, string WorkspaceDir)
{
    public string ConnectionString => $"Data Source=localhost:{Port};";
}
