# Feature Request: Docker Container Authentication to Microsoft Fabric

## Problem

When running the Power BI Modeling MCP Server inside a Docker container, calling `connection_connect_fabric` fails with:

```
An error occurred invoking 'connection_connect_fabric'.
```

**Root cause:** `DefaultAzureCredential` tries 9 credential sources in order — all fail in a plain Docker container because:

- No Azure CLI installed in the `dotnet/aspnet:8.0` runtime image
- No managed identity endpoint (container not running on Azure infrastructure)
- No browser available for interactive login
- No `AZURE_*` environment variables configured
- No `PBI_MODELING_MCP_ACCESS_TOKEN` environment variable set

## Current Behavior

The code in `ConnectionTools.ConnectFabric()` has two auth paths:

1. **`PBI_MODELING_MCP_ACCESS_TOKEN` env var** — uses the token directly as a password in the connection string
2. **`DefaultAzureCredential`** — falls back to the Azure Identity chain, which fails in a bare container

When neither path succeeds, the tool returns a generic error with no actionable guidance.

## Proposed Solutions

### Solution 1: Pass a pre-fetched token (already supported, no code changes)

```bash
# Host machine
$TOKEN = az account get-access-token --resource "https://analysis.windows.net/powerbi/api" --query accessToken -o tsv

# Run container
docker run -p 5100:5100 -e PBI_MODELING_MCP_ACCESS_TOKEN="$TOKEN" powerbi-mcp-server
```

**Limitation:** Tokens expire in ~60–90 minutes. Suitable for dev/testing only.

### Solution 2: Service Principal via environment variables (recommended for production)

Register an Entra ID app registration and pass the standard Azure Identity env vars:

```bash
docker run -p 5100:5100 \
  -e AZURE_TENANT_ID="<tenant-id>" \
  -e AZURE_CLIENT_ID="<client-id>" \
  -e AZURE_CLIENT_SECRET="<client-secret>" \
  powerbi-mcp-server
```

`DefaultAzureCredential` picks up `EnvironmentCredential` (first in the chain) automatically. **No code changes needed.**

Prerequisites for the service principal:
- Power BI API permission `Dataset.ReadWrite.All` granted
- Added as workspace member/admin in Fabric
- "Allow service principals to use Power BI APIs" enabled in Fabric admin settings

### Solution 3: Managed Identity (Azure-hosted containers)

When deployed to Azure Container Apps, ACI, or AKS, assign a managed identity:

```bash
az containerapp identity assign -n my-app -g my-rg --system-assigned
```

`DefaultAzureCredential` auto-detects managed identity via IMDS. No env vars or code changes needed.

## Suggested Improvements

### 1. Better error message in `ConnectFabric`

The current catch block returns a generic message. Enhance it to suggest all available auth options:

```csharp
catch (Exception ex)
{
    return "Error: Authentication failed.\n\n"
         + "To authenticate from a Docker container, use one of:\n"
         + "1. Set `PBI_MODELING_MCP_ACCESS_TOKEN` env var with a valid access token\n"
         + "2. Set `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET` for service principal auth\n"
         + "3. Use managed identity when running on Azure (ACI, ACA, AKS)\n\n"
         + $"Details: {ex.Message}";
}
```

### 2. Document Docker auth in README

Add a section to `README.md` covering authentication options when running via Docker, including the three methods above with copy-paste examples.

### 3. Add `docker-compose.yml` with env var placeholders

```yaml
services:
  powerbi-mcp:
    build: .
    ports:
      - "5100:5100"
    environment:
      # Option A: Pre-fetched token (short-lived)
      # PBI_MODELING_MCP_ACCESS_TOKEN: "<token>"
      # Option B: Service principal (long-lived)
      # AZURE_TENANT_ID: "<tenant-id>"
      # AZURE_CLIENT_ID: "<client-id>"
      # AZURE_CLIENT_SECRET: "<client-secret>"
```

## Summary

| Approach | Code Change | Best For |
|---|---|---|
| `PBI_MODELING_MCP_ACCESS_TOKEN` | None | Quick dev/testing |
| Service principal env vars | None | CI/CD, long-running containers |
| Managed identity | None | Azure-hosted production |
| Improved error message | Minor | Developer experience |
