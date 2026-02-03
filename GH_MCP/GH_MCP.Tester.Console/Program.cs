using GH_MCP.Client;
using GH_MCP.Client.Logging;
using GH_MCP.Client.Models;
using Newtonsoft.Json;

namespace GH_MCP.Tester.Console;

class Program
{
    private static GrasshopperClient? _client;
    private static readonly List<string> _history = new();
    private static bool _connected = false;
    private static string? _logDirectory;

    static async Task Main(string[] args)
    {
        // Configure logging - find logs directory relative to exe or use explicit path
        _logDirectory = FindLogsDirectory();
        if (_logDirectory != null)
        {
            McpLogger.Configure(_logDirectory, $"tester-console-{DateTime.Now:yyyyMMdd-HHmmss}.log", LogLevel.Debug);
            System.Console.WriteLine($"Logging to: {McpLogger.LogFilePath}");
        }

        System.Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        System.Console.WriteLine("║           GH_MCP Tester - Grasshopper MCP Test Tool          ║");
        System.Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        System.Console.WriteLine();

        // Check for --log option to enable logging to specific directory
        var logArgIndex = Array.FindIndex(args, a => a == "--log");
        if (logArgIndex >= 0 && logArgIndex + 1 < args.Length)
        {
            var logDir = args[logArgIndex + 1];
            McpLogger.Configure(logDir, $"tester-console-{DateTime.Now:yyyyMMdd-HHmmss}.log", LogLevel.Debug);
            System.Console.WriteLine($"Logging enabled: {McpLogger.LogFilePath}");
            args = args.Where((_, i) => i != logArgIndex && i != logArgIndex + 1).ToArray();
        }

        // Check for batch mode (piped input)
        if (args.Length > 0 && args[0] == "--json")
        {
            await RunBatchJsonMode();
            return;
        }

        if (args.Length > 0 && File.Exists(args[0]))
        {
            await RunFileMode(args[0]);
            return;
        }

        // Interactive REPL mode
        await RunInteractiveMode();
    }

    static string? FindLogsDirectory()
    {
        // Try to find logs directory relative to repo root
        var current = AppContext.BaseDirectory;
        for (int i = 0; i < 6; i++) // Go up to 6 levels
        {
            var logsPath = Path.Combine(current, "logs");
            if (Directory.Exists(logsPath))
                return logsPath;

            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }

        // Create logs in current directory as fallback
        var fallback = Path.Combine(AppContext.BaseDirectory, "logs");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    static async Task RunInteractiveMode()
    {
        PrintHelp();
        System.Console.WriteLine();

        while (true)
        {
            System.Console.Write(_connected ? "[connected] > " : "[disconnected] > ");
            var input = System.Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(input))
                continue;

            _history.Add(input);

            try
            {
                var shouldExit = await ProcessCommand(input);
                if (shouldExit)
                    break;
            }
            catch (Exception ex)
            {
                PrintError($"Error: {ex.Message}");
            }

            System.Console.WriteLine();
        }
    }

    static async Task RunBatchJsonMode()
    {
        _client = new GrasshopperClient();
        _connected = true;

        string? line;
        while ((line = System.Console.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                var command = JsonConvert.DeserializeObject<GrasshopperCommand>(line);
                if (command != null)
                {
                    var response = await _client.SendCommandAsync(command);
                    System.Console.WriteLine(JsonConvert.SerializeObject(response));
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine(JsonConvert.SerializeObject(GrasshopperResponse.CreateError(ex.Message)));
            }
        }
    }

    static async Task RunFileMode(string filePath)
    {
        System.Console.WriteLine($"Executing commands from: {filePath}");
        System.Console.WriteLine();

        _client = new GrasshopperClient();
        _connected = true;

        var lines = await File.ReadAllLinesAsync(filePath);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;

            System.Console.WriteLine($"> {line}");
            await ProcessCommand(line);
            System.Console.WriteLine();
        }
    }

    static async Task<bool> ProcessCommand(string input)
    {
        var parts = ParseCommand(input);
        if (parts.Length == 0)
            return false;

        var cmd = parts[0].ToLowerInvariant();

        switch (cmd)
        {
            case "exit":
            case "quit":
            case "q":
                System.Console.WriteLine("Goodbye!");
                return true;

            case "help":
            case "?":
                PrintHelp();
                break;

            case "connect":
                await Connect(parts.Length > 1 ? parts[1] : "localhost", parts.Length > 2 ? int.Parse(parts[2]) : 8080);
                break;

            case "disconnect":
                Disconnect();
                break;

            case "status":
                await GetStatus();
                break;

            case "send":
                if (parts.Length < 2)
                {
                    PrintError("Usage: send <json>");
                    break;
                }
                await SendRawJson(string.Join(" ", parts.Skip(1)));
                break;

            case "add":
                if (parts.Length < 4)
                {
                    PrintError("Usage: add <type> <x> <y>");
                    break;
                }
                await AddComponent(parts[1], float.Parse(parts[2]), float.Parse(parts[3]));
                break;

            case "list":
                await ListComponents();
                break;

            case "info":
                if (parts.Length < 2)
                {
                    PrintError("Usage: info <component_id>");
                    break;
                }
                await GetComponentInfo(parts[1]);
                break;

            case "wire":
            case "connect_components":
                if (parts.Length < 3)
                {
                    PrintError("Usage: wire <source_id> <target_id> [source_param] [target_param]");
                    break;
                }
                await ConnectComponents(
                    parts[1],
                    parts[2],
                    parts.Length > 3 ? parts[3] : null,
                    parts.Length > 4 ? parts[4] : null);
                break;

            case "connections":
                await GetConnections();
                break;

            case "clear":
                await ClearDocument();
                break;

            case "save":
                if (parts.Length < 2)
                {
                    PrintError("Usage: save <path>");
                    break;
                }
                await SaveDocument(parts[1]);
                break;

            case "load":
                if (parts.Length < 2)
                {
                    PrintError("Usage: load <path>");
                    break;
                }
                await LoadDocument(parts[1]);
                break;

            case "search":
                if (parts.Length < 2)
                {
                    PrintError("Usage: search <query>");
                    break;
                }
                await SearchComponents(string.Join(" ", parts.Skip(1)));
                break;

            case "params":
                if (parts.Length < 2)
                {
                    PrintError("Usage: params <component_type>");
                    break;
                }
                await GetComponentParameters(string.Join(" ", parts.Skip(1)));
                break;

            case "validate":
                if (parts.Length < 3)
                {
                    PrintError("Usage: validate <source_id> <target_id> [source_param] [target_param]");
                    break;
                }
                await ValidateConnection(
                    parts[1],
                    parts[2],
                    parts.Length > 3 ? parts[3] : null,
                    parts.Length > 4 ? parts[4] : null);
                break;

            case "pattern":
                if (parts.Length < 2)
                {
                    PrintError("Usage: pattern <description>");
                    break;
                }
                await CreatePattern(string.Join(" ", parts.Skip(1)));
                break;

            case "history":
                PrintHistory();
                break;

            case "script":
                await EnterScriptMode();
                break;

            // Verification commands for AI testing
            case "export":
                await ExportDocumentState();
                break;

            case "assert_exists":
            case "exists":
                if (parts.Length < 2)
                {
                    PrintError("Usage: assert_exists <component_id>");
                    break;
                }
                await AssertComponentExists(parts[1]);
                break;

            case "assert_connection":
            case "assert_wire":
                if (parts.Length < 3)
                {
                    PrintError("Usage: assert_connection <source_id> <target_id> [source_param] [target_param]");
                    break;
                }
                await AssertConnectionExists(
                    parts[1],
                    parts[2],
                    parts.Length > 3 ? parts[3] : null,
                    parts.Length > 4 ? parts[4] : null);
                break;

            case "assert_count":
                if (parts.Length < 2)
                {
                    PrintError("Usage: assert_count <expected_count>");
                    break;
                }
                await AssertComponentCount(int.Parse(parts[1]));
                break;

            case "hash":
                await GetDocumentHash();
                break;

            default:
                PrintError($"Unknown command: {cmd}. Type 'help' for available commands.");
                break;
        }

        return false;
    }

    static string[] ParseCommand(string input)
    {
        var parts = new List<string>();
        var current = "";
        var inQuotes = false;

        foreach (var c in input)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ' ' && !inQuotes)
            {
                if (!string.IsNullOrEmpty(current))
                {
                    parts.Add(current);
                    current = "";
                }
            }
            else
            {
                current += c;
            }
        }

        if (!string.IsNullOrEmpty(current))
            parts.Add(current);

        return parts.ToArray();
    }

    static void EnsureConnected()
    {
        if (!_connected || _client == null)
        {
            _client = new GrasshopperClient();
            _connected = true;
        }
    }

    static async Task Connect(string host, int port)
    {
        _client = new GrasshopperClient(host, port);
        var success = await _client.TestConnectionAsync();
        _connected = success;

        if (success)
        {
            PrintSuccess($"Connected to {host}:{port}");
        }
        else
        {
            PrintError($"Failed to connect to {host}:{port}. Is Grasshopper running with GH_MCP component?");
        }
    }

    static void Disconnect()
    {
        _client?.Dispose();
        _client = null;
        _connected = false;
        PrintSuccess("Disconnected");
    }

    static async Task GetStatus()
    {
        EnsureConnected();
        var response = await _client!.GetDocumentInfoAsync();
        PrintResponse(response);
    }

    static async Task SendRawJson(string json)
    {
        EnsureConnected();
        var command = JsonConvert.DeserializeObject<GrasshopperCommand>(json);
        if (command == null)
        {
            PrintError("Invalid JSON");
            return;
        }
        var response = await _client!.SendCommandAsync(command);
        PrintResponse(response);
    }

    static async Task AddComponent(string type, float x, float y)
    {
        EnsureConnected();
        var response = await _client!.AddComponentAsync(type, x, y);
        PrintResponse(response);
    }

    static async Task ListComponents()
    {
        EnsureConnected();
        var response = await _client!.GetAllComponentsAsync();
        PrintResponse(response);
    }

    static async Task GetComponentInfo(string componentId)
    {
        EnsureConnected();
        var response = await _client!.GetComponentInfoAsync(componentId);
        PrintResponse(response);
    }

    static async Task ConnectComponents(string sourceId, string targetId, string? sourceParam, string? targetParam)
    {
        EnsureConnected();
        var response = await _client!.ConnectComponentsAsync(sourceId, targetId, sourceParam, targetParam);
        PrintResponse(response);
    }

    static async Task GetConnections()
    {
        EnsureConnected();
        var response = await _client!.GetConnectionsAsync();
        PrintResponse(response);
    }

    static async Task ClearDocument()
    {
        EnsureConnected();
        var response = await _client!.ClearDocumentAsync();
        PrintResponse(response);
    }

    static async Task SaveDocument(string path)
    {
        EnsureConnected();
        var response = await _client!.SaveDocumentAsync(path);
        PrintResponse(response);
    }

    static async Task LoadDocument(string path)
    {
        EnsureConnected();
        var response = await _client!.LoadDocumentAsync(path);
        PrintResponse(response);
    }

    static async Task SearchComponents(string query)
    {
        EnsureConnected();
        var response = await _client!.SearchComponentsAsync(query);
        PrintResponse(response);
    }

    static async Task GetComponentParameters(string componentType)
    {
        EnsureConnected();
        var response = await _client!.GetComponentParametersAsync(componentType);
        PrintResponse(response);
    }

    static async Task ValidateConnection(string sourceId, string targetId, string? sourceParam, string? targetParam)
    {
        EnsureConnected();
        var response = await _client!.ValidateConnectionAsync(sourceId, targetId, sourceParam, targetParam);
        PrintResponse(response);
    }

    static async Task CreatePattern(string description)
    {
        EnsureConnected();
        var response = await _client!.CreatePatternAsync(description);
        PrintResponse(response);
    }

    static async Task EnterScriptMode()
    {
        System.Console.WriteLine("Enter JSON (end with empty line):");
        var json = "";
        string? line;
        while (!string.IsNullOrEmpty(line = System.Console.ReadLine()))
        {
            json += line + "\n";
        }

        if (!string.IsNullOrWhiteSpace(json))
        {
            await SendRawJson(json.Trim());
        }
    }

    #region Verification Commands

    static async Task ExportDocumentState()
    {
        EnsureConnected();
        var response = await _client!.ExportDocumentStateAsync();
        PrintResponse(response);
    }

    static async Task AssertComponentExists(string componentId)
    {
        EnsureConnected();
        var response = await _client!.AssertComponentExistsAsync(componentId);
        PrintAssertionResult(response);
    }

    static async Task AssertConnectionExists(string sourceId, string targetId, string? sourceParam, string? targetParam)
    {
        EnsureConnected();
        var response = await _client!.AssertConnectionExistsAsync(sourceId, targetId, sourceParam, targetParam);
        PrintAssertionResult(response);
    }

    static async Task AssertComponentCount(int expectedCount)
    {
        EnsureConnected();
        var response = await _client!.AssertComponentCountAsync(expectedCount);
        PrintAssertionResult(response);
    }

    static async Task GetDocumentHash()
    {
        EnsureConnected();
        var response = await _client!.GetDocumentHashAsync();
        PrintResponse(response);
    }

    static void PrintAssertionResult(GrasshopperResponse response)
    {
        if (!response.Success)
        {
            System.Console.ForegroundColor = ConsoleColor.Red;
            System.Console.WriteLine($"ERROR: {response.Error}");
            System.Console.ResetColor();
            return;
        }

        var data = response.GetResultData();
        if (data is Newtonsoft.Json.Linq.JObject jobj)
        {
            var passedToken = jobj["passed"];
            var passed = passedToken != null && passedToken.Type == Newtonsoft.Json.Linq.JTokenType.Boolean && (bool)passedToken;
            if (passed)
            {
                System.Console.ForegroundColor = ConsoleColor.Green;
                System.Console.WriteLine("ASSERTION PASSED");
            }
            else
            {
                System.Console.ForegroundColor = ConsoleColor.Red;
                System.Console.WriteLine("ASSERTION FAILED");
            }
            System.Console.ResetColor();
            System.Console.ForegroundColor = ConsoleColor.Cyan;
            System.Console.WriteLine(JsonConvert.SerializeObject(data, Formatting.Indented));
            System.Console.ResetColor();
        }
        else
        {
            PrintResponse(response);
        }
    }

    #endregion

    static void PrintHistory()
    {
        System.Console.WriteLine("Command History:");
        for (int i = 0; i < _history.Count; i++)
        {
            System.Console.WriteLine($"  {i + 1}. {_history[i]}");
        }
    }

    static void PrintHelp()
    {
        System.Console.WriteLine("Commands:");
        System.Console.WriteLine("  connect [host] [port]     - Connect to GH_MCP (default: localhost:8080)");
        System.Console.WriteLine("  disconnect                - Disconnect from GH_MCP");
        System.Console.WriteLine("  status                    - Get document info");
        System.Console.WriteLine();
        System.Console.WriteLine("  add <type> <x> <y>        - Add component (e.g., add \"Number Slider\" 100 100)");
        System.Console.WriteLine("  list                      - List all components");
        System.Console.WriteLine("  info <id>                 - Get component info");
        System.Console.WriteLine("  wire <src> <tgt> [sp] [tp]- Connect components");
        System.Console.WriteLine("  connections               - List all connections");
        System.Console.WriteLine();
        System.Console.WriteLine("  clear                     - Clear document");
        System.Console.WriteLine("  save <path>               - Save document");
        System.Console.WriteLine("  load <path>               - Load document");
        System.Console.WriteLine();
        System.Console.WriteLine("  search <query>            - Search components");
        System.Console.WriteLine("  params <type>             - Get component parameters");
        System.Console.WriteLine("  validate <src> <tgt>      - Validate connection");
        System.Console.WriteLine("  pattern <description>     - Create pattern");
        System.Console.WriteLine();
        System.Console.WriteLine("  send <json>               - Send raw JSON command");
        System.Console.WriteLine("  script                    - Enter multi-line JSON mode");
        System.Console.WriteLine("  history                   - Show command history");
        System.Console.WriteLine("  help                      - Show this help");
        System.Console.WriteLine("  exit                      - Exit");
        System.Console.WriteLine();
        System.Console.WriteLine("Verification (AI Testing):");
        System.Console.WriteLine("  export                    - Export full document state as JSON");
        System.Console.WriteLine("  assert_exists <id>        - Assert component exists");
        System.Console.WriteLine("  assert_connection <s> <t> - Assert connection exists");
        System.Console.WriteLine("  assert_count <n>          - Assert component count");
        System.Console.WriteLine("  hash                      - Get document state hash");
        System.Console.WriteLine();
        System.Console.WriteLine("Batch modes:");
        System.Console.WriteLine("  GH_MCP.Tester.Console.exe commands.txt    - Execute commands from file");
        System.Console.WriteLine("  type input.json | GH_MCP.Tester.Console.exe --json  - JSON batch mode");
    }

    static void PrintResponse(GrasshopperResponse response)
    {
        if (response.Success)
        {
            System.Console.ForegroundColor = ConsoleColor.Green;
            System.Console.WriteLine("SUCCESS");
            System.Console.ResetColor();
        }
        else
        {
            System.Console.ForegroundColor = ConsoleColor.Red;
            System.Console.WriteLine($"ERROR: {response.Error}");
            System.Console.ResetColor();
        }

        var data = response.GetResultData();
        if (data != null)
        {
            System.Console.ForegroundColor = ConsoleColor.Cyan;
            System.Console.WriteLine(JsonConvert.SerializeObject(data, Formatting.Indented));
            System.Console.ResetColor();
        }
    }

    static void PrintSuccess(string message)
    {
        System.Console.ForegroundColor = ConsoleColor.Green;
        System.Console.WriteLine(message);
        System.Console.ResetColor();
    }

    static void PrintError(string message)
    {
        System.Console.ForegroundColor = ConsoleColor.Red;
        System.Console.WriteLine(message);
        System.Console.ResetColor();
    }
}
