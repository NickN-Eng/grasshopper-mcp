### What your `bridge.py` is doing (and why it’s “Python-shaped”)

Your `bridge.py` is **not part of the Grasshopper/Rhino plugin process** — it’s an **MCP server process** that:

* exposes MCP **tools** like `add_component`, `connect_components`, etc.
* exposes MCP **resources** like `grasshopper://status`, `grasshopper://component_library`, etc.
* when a tool is called, it opens a **TCP socket to `localhost:8080`**, sends a **single-line JSON command** (`{"type": "...", "parameters": {...}}\n`), then reads a **single-line JSON response** and returns it. 

That “MCP server as a separate process” pattern lines up with MCP’s **stdio transport** model: the host launches the server as a subprocess, the server speaks JSON-RPC over stdin/stdout, and logs go to stderr. ([Model Context Protocol][1])

---

## How you’d write the same MCP bridge in C# (.NET)

Good news: there’s now an **official C# MCP SDK** (preview) that makes the server side pretty straightforward. ([GitHub][2])

Below is a “direct translation” of your Python design: **C# MCP server over stdio** + **TCP bridge to Grasshopper on port 8080**.

### 1) `Program.cs` — start an MCP server over stdio

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);

// IMPORTANT: log to stderr so you don't corrupt MCP stdout (JSON-RPC stream).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly(); // discovers [McpServerToolType] + [McpServerTool]

await builder.Build().RunAsync();
```

This is essentially the “Getting Started (Server)” pattern from the official SDK. ([GitHub][2])

### 2) `GrasshopperBridgeTools.cs` — implement the tools + TCP bridge

```csharp
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

[McpServerToolType]
public static class GrasshopperBridgeTools
{
    private static readonly string Host =
        Environment.GetEnvironmentVariable("GRASSHOPPER_HOST") ?? "localhost";

    private static readonly int Port =
        int.TryParse(Environment.GetEnvironmentVariable("GRASSHOPPER_PORT"), out var p) ? p : 8080;

    private static readonly Dictionary<string, string> ComponentMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        // Number Slider variants
        ["number slider"] = "Number Slider",
        ["numeric slider"] = "Number Slider",
        ["num slider"] = "Number Slider",
        ["slider"] = "Number Slider",

        // Other common normalizations
        ["md slider"] = "MD Slider",
        ["multidimensional slider"] = "MD Slider",
        ["multi-dimensional slider"] = "MD Slider",
        ["graph mapper"] = "Graph Mapper",

        // Math ops
        ["add"] = "Addition",
        ["addition"] = "Addition",
        ["plus"] = "Addition",
        ["sum"] = "Addition",
        ["subtract"] = "Subtraction",
        ["subtraction"] = "Subtraction",
        ["minus"] = "Subtraction",
        ["difference"] = "Subtraction",
        ["multiply"] = "Multiplication",
        ["multiplication"] = "Multiplication",
        ["times"] = "Multiplication",
        ["product"] = "Multiplication",
        ["divide"] = "Division",
        ["division"] = "Division",

        // Output
        ["panel"] = "Panel",
        ["text panel"] = "Panel",
        ["output panel"] = "Panel",
        ["display"] = "Panel",
    };

    private static string NormalizeComponentType(string input) =>
        ComponentMapping.TryGetValue(input.Trim().ToLowerInvariant(), out var mapped) ? mapped : input;

    private static async Task<string> SendToGrasshopperAsync(string commandType, object? parameters, CancellationToken ct)
    {
        var command = new { type = commandType, parameters = parameters ?? new { } };
        var jsonLine = JsonSerializer.Serialize(command) + "\n"; // newline-delimited

        using var client = new TcpClient();
        await client.ConnectAsync(Host, Port, ct);
        await using var stream = client.GetStream();

        var bytes = Encoding.UTF8.GetBytes(jsonLine);
        await stream.WriteAsync(bytes, ct);

        // Read until newline (your Python does the same).
        var buffer = new byte[4096];
        var sb = new StringBuilder();
        while (true)
        {
            var read = await stream.ReadAsync(buffer, ct);
            if (read == 0) break;

            sb.Append(Encoding.UTF8.GetString(buffer, 0, read));
            if (sb.ToString().Contains('\n')) break;
        }

        return sb.ToString().Trim();
    }

    [McpServerTool(Name = "add_component"), Description("Add a component to the Grasshopper canvas.")]
    public static Task<string> AddComponent(
        [Description("Component type (e.g. 'Number Slider', 'Panel', 'Circle')")] string component_type,
        [Description("X coordinate on the canvas")] float x,
        [Description("Y coordinate on the canvas")] float y,
        CancellationToken ct)
    {
        var type = NormalizeComponentType(component_type);
        return SendToGrasshopperAsync("add_component", new { type, x, y }, ct);
    }

    [McpServerTool(Name = "clear_document"), Description("Clear the Grasshopper document.")]
    public static Task<string> ClearDocument(CancellationToken ct) =>
        SendToGrasshopperAsync("clear_document", null, ct);

    [McpServerTool(Name = "save_document"), Description("Save the Grasshopper document to a path.")]
    public static Task<string> SaveDocument(string path, CancellationToken ct) =>
        SendToGrasshopperAsync("save_document", new { path }, ct);

    [McpServerTool(Name = "load_document"), Description("Load a Grasshopper document from a path.")]
    public static Task<string> LoadDocument(string path, CancellationToken ct) =>
        SendToGrasshopperAsync("load_document", new { path }, ct);

    [McpServerTool(Name = "get_document_info"), Description("Get information about the current Grasshopper document.")]
    public static Task<string> GetDocumentInfo(CancellationToken ct) =>
        SendToGrasshopperAsync("get_document_info", null, ct);

    [McpServerTool(Name = "connect_components"), Description("Connect two components in the Grasshopper canvas.")]
    public static Task<string> ConnectComponents(
        string source_id,
        string target_id,
        string? source_param = null,
        string? target_param = null,
        int? source_param_index = null,
        int? target_param_index = null,
        CancellationToken ct = default)
    {
        // Mirrors your Python payload keys (sourceId/targetId/etc).
        var payload = new Dictionary<string, object?>
        {
            ["sourceId"] = source_id,
            ["targetId"] = target_id,
            ["sourceParam"] = source_param,
            ["targetParam"] = target_param,
            ["sourceParamIndex"] = source_param_index,
            ["targetParamIndex"] = target_param_index,
        };

        // Drop nulls to keep payload clean
        var cleaned = payload.Where(kv => kv.Value is not null)
                             .ToDictionary(kv => kv.Key, kv => kv.Value);

        return SendToGrasshopperAsync("connect_components", cleaned, ct);
    }

    // Add the rest: create_pattern, get_available_patterns, get_component_info, get_all_components, etc.
}
```

**Notes:**

* Returning `string` here means “return the Grasshopper JSON response verbatim”. That’s the simplest 1:1 translation of your Python bridge. 
* MCP stdio messages are newline-delimited JSON-RPC; your *Grasshopper* socket protocol is also newline-delimited JSON — so the “read until newline” pattern maps cleanly. ([Model Context Protocol][1])
* The C# SDK supports the attribute approach shown above (`[McpServerToolType]`, `[McpServerTool]`) and stdio hosting. ([GitHub][2])

---

## Pros/cons: C# vs Python for the MCP bridge

### C# bridge (pros)

* **Single ecosystem** with your plugin: shared DTOs, shared validation rules, shared logging conventions.
* **Deployment is often cleaner in Windows/.NET shops**: you can `dotnet publish` a single-file exe and avoid managing Python envs.
* **Type safety & refactors**: easy to keep tool signatures aligned with your Grasshopper command schema.

### C# bridge (cons)

* The official C# SDK is explicitly **preview** (expect breaking changes). ([GitHub][2])
* Slightly more boilerplate than the “decorate a function” Python style.

### Python bridge (pros)

* **FastMCP is extremely ergonomic**: decorate functions, run server. ([gofastmcp.com][3])
* Very fast iteration for “glue code” bridges like yours (exactly what `bridge.py` is). 
* Tons of MCP examples in the wild are Python-first (so copy/paste velocity is high).

### Python bridge (cons)

* **Packaging/runtime management**: either require Python installed, or ship a bundled executable (still doable, just another pipeline).
* Less compile-time safety: schema drift between bridge ↔ Grasshopper protocol can sneak in.

---

## “The rest of my plugin is C#. Why Python here?”

Because this part is **not really “the plugin”** — it’s a **separate MCP server process** that Claude/Desktop/VSCode launches via stdio. MCP is language-agnostic, and stdio subprocess servers are the default model. ([Model Context Protocol][1])

So Python is commonly chosen because:

* a lightweight MCP server in Python is *very* quick to write with FastMCP. ([gofastmcp.com][3])
* it keeps the Rhino/Grasshopper plugin isolated: the plugin just exposes a local TCP API; the MCP layer is a thin adapter (exactly what you’ve got). 

---

### My practical recommendation for *your* setup

If you want the cleanest “all-C# / one toolchain / one installer” story, **port the bridge to C#** now that the official C# SDK exists. ([GitHub][2])
If you’re still iterating fast on the tool surface area, **keeping Python** is totally reasonable — it’s glue code, and FastMCP makes that painless. ([gofastmcp.com][3])

[1]: https://modelcontextprotocol.io/specification/2025-06-18/basic/transports "Transports - Model Context Protocol"
[2]: https://github.com/modelcontextprotocol/csharp-sdk "GitHub - modelcontextprotocol/csharp-sdk: The official C# SDK for Model Context Protocol servers and clients. Maintained in collaboration with Microsoft."
[3]: https://gofastmcp.com/ "Welcome to FastMCP 3.0! - FastMCP"
