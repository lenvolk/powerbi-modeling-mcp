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

// ── Version ─────────────────────────────────────────────────────────────────
var serverVersion = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

// ── CLI argument parsing ────────────────────────────────────────────────────
string transport, host, portStr, compatibility;
try
{
    transport     = GetArg(args, "--transport", "stdio");           // stdio | http
    host          = GetArg(args, "--host", "127.0.0.1");           // bind address
    portStr       = GetArg(args, "--port", "5100");
    compatibility = GetArg(args, "--compatibility", "PowerBI"); // PowerBI | Full
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    Environment.Exit(1);
    return; // unreachable but satisfies compiler
}

if (!int.TryParse(portStr, out var port) || port < 1 || port > 65535)
{
    Console.Error.WriteLine($"Error: Invalid port number '{portStr}'. Must be between 1 and 65535.");
    Environment.Exit(1);
}
var mode       = args.Contains("--readonly") ? "readonly" : "readwrite";
var skipConfirm = args.Contains("--skipconfirmation");

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
            Version = serverVersion
        };
    })
    .WithHttpTransport(httpOptions =>
    {
        httpOptions.Stateless = false; // stateful — each session keeps its PBI connection
    })
    .WithToolsFromAssembly();

    var app = builder.Build();

    // Ensure ConnectionManager is disposed on shutdown
    var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
    var cm = app.Services.GetRequiredService<ConnectionManager>();
    lifetime.ApplicationStopping.Register(() => cm.Dispose());

    // MCP endpoint
    app.MapMcp("/mcp");

    // Health / readiness probes
    app.MapGet("/healthz", (ConnectionManager cm) => Results.Ok(new
    {
        status      = "healthy",
        transport   = "http",
        version     = serverVersion,
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
                Version = serverVersion
            };
        })
        .WithStdioServerTransport()
        .WithToolsFromAssembly();

    await builder.Build().RunAsync();
}

// ── Helpers ─────────────────────────────────────────────────────────────────
static string GetArg(string[] args, string name, string defaultValue)
{
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            if (i == args.Length - 1)
                throw new ArgumentException($"Argument '{name}' requires a value.");
            return args[i + 1];
        }
    }
    return defaultValue;
}
