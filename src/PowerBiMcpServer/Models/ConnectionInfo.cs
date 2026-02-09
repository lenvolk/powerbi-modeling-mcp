namespace PowerBiMcpServer.Models;

/// <summary>Describes a discovered or configured connection to a Power BI semantic model.</summary>
public sealed class ConnectionInfo
{
    public required string Id             { get; init; }
    public required string Name           { get; init; }
    public required string ConnectionString { get; set; }
    public required ConnectionKind Kind   { get; init; }
    public string? DatabaseName           { get; set; }
    public string? WorkspaceName          { get; set; }
    public DateTime ConnectedAt           { get; init; } = DateTime.UtcNow;
}

public enum ConnectionKind
{
    PowerBIDesktop,
    FabricWorkspace,
    PbipFolder
}
