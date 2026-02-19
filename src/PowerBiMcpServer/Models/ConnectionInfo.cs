namespace PowerBiMcpServer.Models;

/// <summary>Describes a discovered or configured connection to a Power BI semantic model.</summary>
public sealed class ConnectionInfo
{
    public required string Id             { get; set; }
    public required string Name           { get; init; }
    public required string ConnectionString { get; init; }
    public required ConnectionKind Kind   { get; init; }
    public string? DatabaseName           { get; init; }
    public string? WorkspaceName          { get; init; }
    public string? AccessToken            { get; init; }
    public DateTime ConnectedAt           { get; init; } = DateTime.UtcNow;
}

public enum ConnectionKind
{
    PowerBIDesktop,
    FabricWorkspace,
    PbipFolder
}
