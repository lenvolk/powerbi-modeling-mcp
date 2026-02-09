namespace PowerBiMcpServer.Models;

/// <summary>Global server settings parsed from CLI arguments.</summary>
public sealed record ServerSettings(
    string Mode,           // "readwrite" | "readonly"
    bool SkipConfirmation,
    string Compatibility   // "PowerBI" | "Full"
)
{
    public bool IsReadOnly => Mode.Equals("readonly", StringComparison.OrdinalIgnoreCase);
}
