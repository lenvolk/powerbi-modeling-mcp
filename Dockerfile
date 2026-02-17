# ── Build stage ──────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/PowerBiMcpServer/PowerBiMcpServer.csproj", "src/PowerBiMcpServer/"]
RUN dotnet restore "src/PowerBiMcpServer/PowerBiMcpServer.csproj"

COPY . .
WORKDIR "/src/src/PowerBiMcpServer"
RUN dotnet publish "PowerBiMcpServer.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ── Runtime stage ────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# ICU + OpenSSL for ADOMD/Fabric connectivity
RUN apt-get update && apt-get install -y --no-install-recommends \
    libicu72 libssl3 ca-certificates \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

EXPOSE 5100

ENTRYPOINT ["dotnet", "powerbi-mcp-server.dll", "--transport", "http", "--host", "0.0.0.0", "--port", "5100"]
