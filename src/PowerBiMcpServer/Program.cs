// ---------------------------------------------------------------------------
// Power BI Modeling MCP Server — open-source reimplementation
// Supports both stdio (default) and HTTP(S) / Streamable-HTTP transports.
// ---------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using PowerBiMcpServer.Models;
using PowerBiMcpServer.Services;
using PowerBiMcpServer.Tools;

// ── CLI argument parsing ────────────────────────────────────────────────────
var transport  = GetArg(args, "--transport", "stdio");           // stdio | http
var host       = GetArg(args, "--host", "127.0.0.1");           // bind address
var port       = int.Parse(GetArg(args, "--port", "5100"));     // listen port
var mode       = args.Contains("--readonly") ? "readonly" : "readwrite";
var skipConfirm = args.Contains("--skipconfirmation");
var compatibility = GetArg(args, "--compatibility", "PowerBI"); // PowerBI | Full

// ── Shared service registration ─────────────────────────────────────────────
if (transport.Equals("http", StringComparison.OrdinalIgnoreCase))
{
    // ── HTTP / Streamable-HTTP transport ─────────────────────────────────────
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddSingleton(new ServerSettings(mode, skipConfirm, compatibility));
    builder.Services.AddSingleton<ConnectionManager>();
    builder.Services.AddSingleton<PowerBiDesktopDiscovery>();
    builder.Services.AddSingleton<TmdlService>();

    builder.Services.AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name  = "powerbi-mcp-server",
            Version = "1.0.0"
        };
    })
    .WithHttpTransport(httpOptions =>
    {
        httpOptions.Stateless = false; // stateful — each session keeps its PBI connection
    })
    .WithToolsFromAssembly();

    var app = builder.Build();

    // MCP endpoint
    app.MapMcp("/mcp");

    // Health / readiness probes
    app.MapGet("/healthz", (ConnectionManager cm) => Results.Ok(new
    {
        status      = "healthy",
        transport   = "http",
        version     = "1.0.0",
        connections = cm.ActiveConnectionCount
    }));

    app.Urls.Clear();
    app.Urls.Add($"http://{host}:{port}");

    Console.Error.WriteLine($"Power BI MCP Server listening on http://{host}:{port}/mcp");
    Console.Error.WriteLine($"Health endpoint: http://{host}:{port}/healthz");

    await app.RunAsync();
}
else
{
    // ── stdio transport (default — compatible with VS Code / GitHub Copilot) ─
    var builder = Host.CreateApplicationBuilder(args);

    builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

    builder.Services.AddSingleton(new ServerSettings(mode, skipConfirm, compatibility));
    builder.Services.AddSingleton<ConnectionManager>();
    builder.Services.AddSingleton<PowerBiDesktopDiscovery>();
    builder.Services.AddSingleton<TmdlService>();

    builder.Services
        .AddMcpServer(options =>
        {
            options.ServerInfo = new()
            {
                Name  = "powerbi-mcp-server",
                Version = "1.0.0"
            };
        })
        .WithStdioServerTransport()
        .WithToolsFromAssembly();

    await builder.Build().RunAsync();
}

// ── Helpers ─────────────────────────────────────────────────────────────────
static string GetArg(string[] args, string name, string defaultValue)
{
    for (int i = 0; i < args.Length - 1; i++)
        if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            return args[i + 1];
    return defaultValue;
}
