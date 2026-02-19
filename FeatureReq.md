# Feature Request: Fix Fabric XMLA Authentication from Docker Containers

## Problem

When running the Power BI Modeling MCP Server inside a Docker container, `connection_connect_fabric` fails even when a valid token is acquired:

```
Authentication failed for all authenticators
CreateSession → 401
```

The token is obtained successfully (via Azure CLI or `PBI_MODELING_MCP_ACCESS_TOKEN`), flows into `ConnectFabric`, but ADOMD/TOM rejects it at session creation.

## Root Cause: `Password=` vs `AccessToken` Property

The **primary issue is how the token is passed to ADOMD/TOM**. The current code embeds the token in the connection string via `Password=`:

```csharp
// Current code — ConnectionTools.cs
connectionString = $"Data Source={xmlaEndpoint};Initial Catalog={safeCatalog};Password={token};";
```

Microsoft's documentation explicitly states:

> *"Using the Password connection string property to pass an access token is discouraged. Use the AccessToken property instead."*

When `Password=` is used, ADOMD.NET tries its **internal MSAL authenticator chain first** (browser redirect, Windows Integrated/SSPI, ADAL cache, etc.) before falling back to treating the value as a raw bearer token. On Linux containers, **every one of these internal authenticators fails** → "Authentication failed for all authenticators" → 401.

On Windows, some authenticators (SSPI, cached tokens) may succeed as fallbacks, which is why this works locally but not in Docker.

### Secondary requirements (must also be met)

| Requirement | Where to check |
|---|---|
| **XMLA endpoint enabled (tenant)** | Admin Portal → Tenant settings → Integration → "Allow XMLA endpoints and Analyze in Excel" |
| **XMLA endpoint set to Read Write (capacity)** | Admin Portal → Capacity settings → [capacity] → Power BI Workloads → XMLA Endpoint |
| **Workspace on Premium/Fabric capacity** | Shared/Pro capacity has **no XMLA endpoint** — requires P1+, PPU, F2+, or A1+ |
| **B2B/guest users use tenant domain** | If cross-tenant, use `powerbi://api.powerbi.com/v1.0/fabrikam.com/...` not `myorg` |

## Proposed Code Fix

### 1. Store the access token separately from the connection string

Update `ConnectionInfo` to carry the token:

```csharp
// Models/ConnectionInfo.cs
public sealed class ConnectionInfo
{
    public required string Id             { get; set; }
    public required string Name           { get; init; }
    public required string ConnectionString { get; init; }
    public required ConnectionKind Kind   { get; init; }
    public string? DatabaseName           { get; init; }
    public string? WorkspaceName          { get; init; }
    public string? AccessToken            { get; init; }   // ← NEW
    public DateTime ConnectedAt           { get; init; } = DateTime.UtcNow;
}
```

### 2. Use `Server.AccessToken` in `ConnectionManager.Connect()`

```csharp
// ConnectionManager.cs — new overload
public string Connect(string connectionString, string name, ConnectionKind kind,
    string? databaseName = null, string? workspaceName = null, string? accessToken = null)
{
    var server = new Server();

    if (!string.IsNullOrEmpty(accessToken))
    {
        // Set AccessToken BEFORE connecting — bypasses MSAL authenticator chain entirely
        server.AccessToken = new AccessToken(accessToken, DateTimeOffset.UtcNow.AddHours(1));
    }

    server.Connect(connectionString);

    return RegisterConnection(new ConnInfo
    {
        Id               = "",
        Name             = name,
        ConnectionString = connectionString,
        Kind             = kind,
        DatabaseName     = databaseName,
        WorkspaceName    = workspaceName,
        AccessToken      = accessToken
    }, server: server);
}
```

### 3. Use `AdomdConnection.AccessToken` in `ExecuteDaxQuery()`

```csharp
// ConnectionManager.cs — ExecuteDaxQuery
public string ExecuteDaxQuery(string connectionId, string dax)
{
    var conn = Get(connectionId);
    if (conn.OfflineModel != null)
        return "Error: DAX queries cannot be executed against offline PBIP/TMDL models.";

    using var adomd = new AdomdConnection(conn.Info.ConnectionString);

    if (!string.IsNullOrEmpty(conn.Info.AccessToken))
    {
        adomd.AccessToken = new AdomdAccessToken(conn.Info.AccessToken, DateTimeOffset.UtcNow.AddHours(1));
    }

    adomd.Open();
    // ... rest of method unchanged
}
```

### 4. Update `ConnectionTools.ConnectFabric()` — remove `Password=` from connection string

```csharp
// ConnectionTools.cs — ConnectFabric
public string ConnectFabric(string workspaceName, string semanticModelName)
{
    var xmlaEndpoint = $"powerbi://api.powerbi.com/v1.0/myorg/{Uri.EscapeDataString(workspaceName)}";
    var safeCatalog = semanticModelName.Replace(";", "");

    // Connection string WITHOUT Password — token goes via AccessToken property
    var connectionString = $"Data Source={xmlaEndpoint};Initial Catalog={safeCatalog};";

    var envToken = Environment.GetEnvironmentVariable("PBI_MODELING_MCP_ACCESS_TOKEN");
    string accessToken;

    if (!string.IsNullOrEmpty(envToken))
    {
        accessToken = envToken;
    }
    else
    {
        try
        {
            var credential = new DefaultAzureCredential();
            var tokenResult = credential.GetToken(
                new Azure.Core.TokenRequestContext(new[] { "https://analysis.windows.net/powerbi/api/.default" }));
            accessToken = tokenResult.Token;
        }
        catch (Exception ex)
        {
            return "Error: Authentication failed.\n\n"
                 + "To authenticate, use one of:\n"
                 + "1. Set `PBI_MODELING_MCP_ACCESS_TOKEN` env var with a valid access token\n"
                 + "2. Set `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET` for service principal auth\n"
                 + "3. Use managed identity when running on Azure (ACI, ACA, AKS)\n\n"
                 + $"Details: {ex.Message}";
        }
    }

    // Pass token separately — ConnectionManager uses Server.AccessToken property
    var id = _cm.Connect(connectionString, semanticModelName, ConnectionKind.FabricWorkspace,
        databaseName: semanticModelName, workspaceName: workspaceName, accessToken: accessToken);

    return $"Connected to **{semanticModelName}** in workspace **{workspaceName}** (connection `{id}`).";
}
```

## Why This Fixes the 401

| Before (broken on Linux) | After (works everywhere) |
|---|---|
| Token embedded in `Password=` field | Token set via `Server.AccessToken` / `AdomdConnection.AccessToken` |
| ADOMD tries MSAL authenticator chain → all fail on Linux | `AccessToken` property **bypasses MSAL entirely** |
| Falls back to treating `Password` as raw token — inconsistent | Direct bearer token injection — deterministic |

## Docker Runtime Authentication Options

### Option A: Pre-fetched token (dev/testing)

```bash
$TOKEN = az account get-access-token --resource "https://analysis.windows.net/powerbi/api" --query accessToken -o tsv
docker run -p 5100:5100 -e PBI_MODELING_MCP_ACCESS_TOKEN="$TOKEN" powerbi-mcp-server
```

Tokens expire in ~60–90 minutes.

### Option B: Service Principal env vars (production)

```bash
docker run -p 5100:5100 \
  -e AZURE_TENANT_ID="<tenant-id>" \
  -e AZURE_CLIENT_ID="<client-id>" \
  -e AZURE_CLIENT_SECRET="<client-secret>" \
  powerbi-mcp-server
```

`DefaultAzureCredential` picks up `EnvironmentCredential` automatically. The service principal must:
- Have Power BI API permission `Dataset.ReadWrite.All`
- Be added as workspace member/admin in Fabric
- "Allow service principals to use Power BI APIs" enabled in Fabric admin settings

### Option C: Managed Identity (Azure-hosted containers)

```bash
az containerapp identity assign -n my-app -g my-rg --system-assigned
```

No env vars needed. `DefaultAzureCredential` auto-detects managed identity.

## Checklist Before Testing

- [ ] Workspace is on Premium (P1+), PPU, or Fabric (F2+) capacity
- [ ] Admin Portal → Tenant settings → "Allow XMLA endpoints" = **Enabled**
- [ ] Capacity settings → XMLA Endpoint = **Read Write**
- [ ] Service principal (if used) is workspace member + "Allow service principals to use Power BI APIs" enabled
- [ ] If B2B/guest user: use tenant domain in Data Source URL instead of `myorg`

## Summary

| Issue | Fix |
|---|---|
| `Password=` triggers MSAL auth chain → fails on Linux | Switch to `Server.AccessToken` / `AdomdConnection.AccessToken` property |
| Generic error message | Enhanced message with all auth options |
| No capacity/tenant guidance | Checklist added above |
