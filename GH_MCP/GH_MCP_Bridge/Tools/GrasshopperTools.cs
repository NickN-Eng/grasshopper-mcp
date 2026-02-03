using System.ComponentModel;
using GH_MCP.Client;
using GH_MCP.Client.Models;
using ModelContextProtocol.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GH_MCP_Bridge.Tools;

/// <summary>
/// MCP Tools for interacting with Grasshopper
/// </summary>
[McpServerToolType]
public static class GrasshopperTools
{
    private static GrasshopperClient CreateClient()
    {
        var host = Environment.GetEnvironmentVariable("GRASSHOPPER_HOST") ?? "localhost";
        var portStr = Environment.GetEnvironmentVariable("GRASSHOPPER_PORT");
        var port = int.TryParse(portStr, out var p) ? p : 8080;
        return new GrasshopperClient(host, port);
    }

    private static string SerializeResponse(GrasshopperResponse response)
    {
        return JsonConvert.SerializeObject(response, Formatting.Indented);
    }

    [McpServerTool(Name = "add_component")]
    [Description("Add a component to the Grasshopper canvas. Common types: Number Slider, Panel, Addition, Circle, Point, XY Plane, Box, Line, Construct Point")]
    public static async Task<string> AddComponent(
        [Description("Component type (e.g., 'Number Slider', 'Panel', 'Circle', 'Addition')")] string component_type,
        [Description("X coordinate on the canvas")] float x,
        [Description("Y coordinate on the canvas")] float y,
        CancellationToken ct)
    {
        using var client = CreateClient();
        var normalizedType = ComponentNormalizer.Normalize(component_type);
        var response = await client.AddComponentAsync(normalizedType, x, y, ct);
        return SerializeResponse(response);
    }

    [McpServerTool(Name = "connect_components")]
    [Description("Connect two components in the Grasshopper canvas. For multi-input components (Addition, Multiplication, etc.), the system automatically routes to the next available input (A, B, C...).")]
    public static async Task<string> ConnectComponents(
        [Description("ID of the source component (output side)")] string source_id,
        [Description("ID of the target component (input side)")] string target_id,
        [Description("Name of the source output parameter (optional)")] string? source_param = null,
        [Description("Name of the target input parameter (optional)")] string? target_param = null,
        [Description("Index of the source parameter (optional, alternative to source_param)")] int? source_param_index = null,
        [Description("Index of the target parameter (optional, alternative to target_param)")] int? target_param_index = null,
        CancellationToken ct = default)
    {
        using var client = CreateClient();

        // Get target component info to check for multi-input components
        var targetInfoResponse = await client.GetComponentInfoAsync(target_id, ct);

        // Handle intelligent routing for multi-input math components
        if (targetInfoResponse.Success && target_param == null && target_param_index == null)
        {
            var componentType = targetInfoResponse.GetProperty<string>("type");
            if (componentType != null && ComponentNormalizer.IsMultiInputMathComponent(componentType))
            {
                // Get existing connections to find next available input
                var connectionsResponse = await client.GetConnectionsAsync(ct);
                if (connectionsResponse.Success)
                {
                    var connections = connectionsResponse.GetResultAs<JArray>();
                    var occupiedInputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    if (connections != null)
                    {
                        foreach (var conn in connections)
                        {
                            if (conn["targetId"]?.ToString() == target_id)
                            {
                                var param = conn["targetParam"]?.ToString();
                                if (!string.IsNullOrEmpty(param))
                                    occupiedInputs.Add(param);
                            }
                        }
                    }

                    // Find first available input
                    foreach (var inputName in ComponentNormalizer.GetMultiInputParameterNames())
                    {
                        if (!occupiedInputs.Contains(inputName))
                        {
                            target_param = inputName;
                            break;
                        }
                    }
                }
            }
        }

        var response = await client.ConnectComponentsAsync(
            source_id, target_id, source_param, target_param, source_param_index, target_param_index, ct);
        return SerializeResponse(response);
    }

    [McpServerTool(Name = "clear_document")]
    [Description("Clear the Grasshopper document, removing all components and connections.")]
    public static async Task<string> ClearDocument(CancellationToken ct)
    {
        using var client = CreateClient();
        var response = await client.ClearDocumentAsync(ct);
        return SerializeResponse(response);
    }

    [McpServerTool(Name = "save_document")]
    [Description("Save the Grasshopper document to a specified file path.")]
    public static async Task<string> SaveDocument(
        [Description("Full path where to save the document (e.g., 'C:/path/to/file.gh')")] string path,
        CancellationToken ct)
    {
        using var client = CreateClient();
        var response = await client.SaveDocumentAsync(path, ct);
        return SerializeResponse(response);
    }

    [McpServerTool(Name = "load_document")]
    [Description("Load a Grasshopper document from a specified file path.")]
    public static async Task<string> LoadDocument(
        [Description("Full path to the Grasshopper file to load")] string path,
        CancellationToken ct)
    {
        using var client = CreateClient();
        var response = await client.LoadDocumentAsync(path, ct);
        return SerializeResponse(response);
    }

    [McpServerTool(Name = "get_document_info")]
    [Description("Get information about the current Grasshopper document.")]
    public static async Task<string> GetDocumentInfo(CancellationToken ct)
    {
        using var client = CreateClient();
        var response = await client.GetDocumentInfoAsync(ct);
        return SerializeResponse(response);
    }

    [McpServerTool(Name = "get_component_info")]
    [Description("Get detailed information about a specific component, including its inputs, outputs, and current values.")]
    public static async Task<string> GetComponentInfo(
        [Description("ID of the component to get information about")] string component_id,
        CancellationToken ct)
    {
        using var client = CreateClient();
        var response = await client.GetComponentInfoAsync(component_id, ct);
        return SerializeResponse(response);
    }

    [McpServerTool(Name = "get_all_components")]
    [Description("Get a list of all components in the current document with their IDs, types, and positions.")]
    public static async Task<string> GetAllComponents(CancellationToken ct)
    {
        using var client = CreateClient();
        var response = await client.GetAllComponentsAsync(ct);
        return SerializeResponse(response);
    }

    [McpServerTool(Name = "get_connections")]
    [Description("Get a list of all connections between components in the current document.")]
    public static async Task<string> GetConnections(CancellationToken ct)
    {
        using var client = CreateClient();
        var response = await client.GetConnectionsAsync(ct);
        return SerializeResponse(response);
    }

    [McpServerTool(Name = "search_components")]
    [Description("Search for Grasshopper components by name or category.")]
    public static async Task<string> SearchComponents(
        [Description("Search query to find components")] string query,
        CancellationToken ct)
    {
        using var client = CreateClient();
        var response = await client.SearchComponentsAsync(query, ct);
        return SerializeResponse(response);
    }

    [McpServerTool(Name = "get_component_parameters")]
    [Description("Get the list of input and output parameters for a specific component type.")]
    public static async Task<string> GetComponentParameters(
        [Description("Type of component to get parameters for")] string component_type,
        CancellationToken ct)
    {
        using var client = CreateClient();
        var normalizedType = ComponentNormalizer.Normalize(component_type);
        var response = await client.GetComponentParametersAsync(normalizedType, ct);
        return SerializeResponse(response);
    }

    [McpServerTool(Name = "validate_connection")]
    [Description("Validate if a connection between two components is possible before attempting it.")]
    public static async Task<string> ValidateConnection(
        [Description("ID of the source component (output side)")] string source_id,
        [Description("ID of the target component (input side)")] string target_id,
        [Description("Name of the source parameter (optional)")] string? source_param = null,
        [Description("Name of the target parameter (optional)")] string? target_param = null,
        CancellationToken ct = default)
    {
        using var client = CreateClient();
        var response = await client.ValidateConnectionAsync(source_id, target_id, source_param, target_param, ct);
        return SerializeResponse(response);
    }

    [McpServerTool(Name = "create_pattern")]
    [Description("Create a pattern of components based on a high-level description (e.g., '3D voronoi cube', 'parametric circle').")]
    public static async Task<string> CreatePattern(
        [Description("High-level description of what to create")] string description,
        CancellationToken ct)
    {
        using var client = CreateClient();
        var response = await client.CreatePatternAsync(description, ct);
        return SerializeResponse(response);
    }

    [McpServerTool(Name = "get_available_patterns")]
    [Description("Get a list of available patterns that match a query.")]
    public static async Task<string> GetAvailablePatterns(
        [Description("Query to search for patterns")] string query,
        CancellationToken ct)
    {
        using var client = CreateClient();
        var response = await client.GetAvailablePatternsAsync(query, ct);
        return SerializeResponse(response);
    }
}
