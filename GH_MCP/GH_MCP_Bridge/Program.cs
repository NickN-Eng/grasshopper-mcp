using GH_MCP.Client.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

// Configure file logging for AI-readable logs
ConfigureLogging();

// MCP servers communicate over stdio, so we must log to stderr to avoid
// corrupting the JSON-RPC stream.
var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = Microsoft.Extensions.Logging.LogLevel.Trace;
});

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "Grasshopper MCP Bridge",
            Version = "1.0.0"
        };
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly()
    .WithResourcesFromAssembly();

Console.Error.WriteLine("Starting Grasshopper MCP Bridge Server...");
Console.Error.WriteLine("Connecting to Grasshopper on localhost:8080 (configurable via GRASSHOPPER_HOST and GRASSHOPPER_PORT)");

if (McpLogger.IsEnabled)
{
    Console.Error.WriteLine($"Logging to: {McpLogger.LogFilePath}");
}

await builder.Build().RunAsync();

static void ConfigureLogging()
{
    // Check for GRASSHOPPER_MCP_LOG_DIR environment variable
    var logDir = Environment.GetEnvironmentVariable("GRASSHOPPER_MCP_LOG_DIR");

    if (string.IsNullOrEmpty(logDir))
    {
        // Try to find logs directory relative to exe
        logDir = FindLogsDirectory();
    }

    if (!string.IsNullOrEmpty(logDir))
    {
        McpLogger.Configure(logDir, $"mcp-bridge-{DateTime.Now:yyyyMMdd-HHmmss}.log", GH_MCP.Client.Logging.LogLevel.Debug, logToConsole: false);
    }
}

static string? FindLogsDirectory()
{
    var current = AppContext.BaseDirectory;
    for (int i = 0; i < 6; i++)
    {
        var logsPath = Path.Combine(current, "logs");
        if (Directory.Exists(logsPath))
            return logsPath;

        var parent = Directory.GetParent(current);
        if (parent == null) break;
        current = parent.FullName;
    }

    return null;
}
