using System.ComponentModel;
using GH_MCP.Client;
using ModelContextProtocol.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GH_MCP_Bridge.Resources;

/// <summary>
/// MCP Resources for Grasshopper information
/// </summary>
[McpServerResourceType]
public static class GrasshopperResources
{
    private static GrasshopperClient CreateClient()
    {
        var host = Environment.GetEnvironmentVariable("GRASSHOPPER_HOST") ?? "localhost";
        var portStr = Environment.GetEnvironmentVariable("GRASSHOPPER_PORT");
        var port = int.TryParse(portStr, out var p) ? p : 8080;
        return new GrasshopperClient(host, port);
    }

    [McpServerResource(Name = "status", UriTemplate = "grasshopper://status")]
    [Description("Get current Grasshopper canvas status including all components, connections, and helpful recommendations")]
    public static async Task<string> GetStatus(CancellationToken ct)
    {
        using var client = CreateClient();

        try
        {
            // Get document info
            var docInfoResponse = await client.GetDocumentInfoAsync(ct);
            var docInfo = docInfoResponse.Success ? docInfoResponse.GetResultData() : null;

            // Get all components
            var componentsResponse = await client.GetAllComponentsAsync(ct);
            var components = componentsResponse.Success
                ? componentsResponse.GetResultAs<JArray>() ?? new JArray()
                : new JArray();

            // Get all connections
            var connectionsResponse = await client.GetConnectionsAsync(ct);
            var connections = connectionsResponse.Success
                ? connectionsResponse.GetResultAs<JArray>() ?? new JArray()
                : new JArray();

            // Build component summaries
            var componentSummaries = new JArray();
            foreach (var component in components)
            {
                var summary = new JObject
                {
                    ["id"] = component["id"],
                    ["type"] = component["type"],
                    ["position"] = new JObject
                    {
                        ["x"] = component["x"],
                        ["y"] = component["y"]
                    }
                };

                // Add settings for specific component types
                var componentType = component["type"]?.ToString();
                if (componentType == "Number Slider")
                {
                    summary["settings"] = new JObject
                    {
                        ["min"] = component["min"] ?? 0,
                        ["max"] = component["max"] ?? 10,
                        ["value"] = component["value"] ?? 5
                    };
                }

                componentSummaries.Add(summary);
            }

            // Component hints for common issues
            var componentHints = new JObject
            {
                ["Number Slider"] = new JObject
                {
                    ["description"] = "Single numeric value slider with adjustable range",
                    ["common_usage"] = "Use for single numeric inputs like radius, height, count, etc.",
                    ["NOT_TO_BE_CONFUSED_WITH"] = "MD Slider (which is for multi-dimensional values)"
                },
                ["MD Slider"] = new JObject
                {
                    ["description"] = "Multi-dimensional slider for vector input",
                    ["common_usage"] = "Use for vector inputs, NOT for simple numeric values",
                    ["NOT_TO_BE_CONFUSED_WITH"] = "Number Slider (which is for single numeric values)"
                },
                ["Panel"] = new JObject
                {
                    ["description"] = "Displays text or numeric data",
                    ["common_usage"] = "Use for displaying outputs and debugging"
                },
                ["Addition"] = new JObject
                {
                    ["description"] = "Adds two or more numbers",
                    ["common_usage"] = "Connect two Number Sliders to inputs A and B",
                    ["parameters"] = new JArray { "A", "B" },
                    ["connection_tip"] = "First slider should connect to input A, second to input B"
                }
            };

            var recommendations = new JArray
            {
                "When needing a simple numeric input control, ALWAYS use 'Number Slider', not MD Slider",
                "For vector inputs (like 3D points), use 'MD Slider' or 'Construct Point' with multiple Number Sliders",
                "Use 'Panel' to display outputs and debug values",
                "When connecting multiple sliders to Addition, first slider goes to input A, second to input B"
            };

            var status = new JObject
            {
                ["status"] = "Connected to Grasshopper",
                ["document"] = JToken.FromObject(docInfo ?? new object()),
                ["components"] = componentSummaries,
                ["connections"] = connections,
                ["component_hints"] = componentHints,
                ["recommendations"] = recommendations,
                ["canvas_summary"] = $"Current canvas has {componentSummaries.Count} components and {connections.Count} connections"
            };

            return JsonConvert.SerializeObject(status, Formatting.Indented);
        }
        catch (Exception ex)
        {
            var errorStatus = new JObject
            {
                ["status"] = $"Error: {ex.Message}",
                ["document"] = new JObject(),
                ["components"] = new JArray(),
                ["connections"] = new JArray()
            };
            return JsonConvert.SerializeObject(errorStatus, Formatting.Indented);
        }
    }

    [McpServerResource(Name = "component_guide", UriTemplate = "grasshopper://component_guide")]
    [Description("Comprehensive guide for Grasshopper components, their inputs/outputs, and how to connect them")]
    public static string GetComponentGuide()
    {
        var guide = new JObject
        {
            ["title"] = "Grasshopper Component Guide",
            ["description"] = "Guide for creating and connecting Grasshopper components",
            ["components"] = new JArray
            {
                new JObject
                {
                    ["name"] = "Point",
                    ["category"] = "Params",
                    ["description"] = "Creates a point at specific coordinates",
                    ["inputs"] = new JArray
                    {
                        new JObject { ["name"] = "X", ["type"] = "Number" },
                        new JObject { ["name"] = "Y", ["type"] = "Number" },
                        new JObject { ["name"] = "Z", ["type"] = "Number" }
                    },
                    ["outputs"] = new JArray
                    {
                        new JObject { ["name"] = "Pt", ["type"] = "Point" }
                    }
                },
                new JObject
                {
                    ["name"] = "Number Slider",
                    ["category"] = "Params",
                    ["description"] = "Creates a slider for numeric input with adjustable range and precision",
                    ["inputs"] = new JArray(),
                    ["outputs"] = new JArray
                    {
                        new JObject { ["name"] = "N", ["type"] = "Number", ["description"] = "Number output" }
                    },
                    ["settings"] = new JObject
                    {
                        ["min"] = new JObject { ["description"] = "Minimum value", ["default"] = 0 },
                        ["max"] = new JObject { ["description"] = "Maximum value", ["default"] = 10 },
                        ["value"] = new JObject { ["description"] = "Current value", ["default"] = 5 }
                    },
                    ["disambiguation"] = new JObject
                    {
                        ["correct_usage"] = "When needing a simple numeric input, ALWAYS use 'Number Slider', not MD Slider"
                    }
                },
                new JObject
                {
                    ["name"] = "Circle",
                    ["category"] = "Curve",
                    ["description"] = "Creates a circle",
                    ["inputs"] = new JArray
                    {
                        new JObject { ["name"] = "Plane", ["type"] = "Plane", ["description"] = "Base plane for the circle" },
                        new JObject { ["name"] = "Radius", ["type"] = "Number", ["description"] = "Circle radius" }
                    },
                    ["outputs"] = new JArray
                    {
                        new JObject { ["name"] = "C", ["type"] = "Circle" }
                    }
                },
                new JObject
                {
                    ["name"] = "XY Plane",
                    ["category"] = "Vector",
                    ["description"] = "Creates an XY plane at the world origin or at a specified point",
                    ["inputs"] = new JArray
                    {
                        new JObject { ["name"] = "Origin", ["type"] = "Point", ["description"] = "Origin point", ["optional"] = true }
                    },
                    ["outputs"] = new JArray
                    {
                        new JObject { ["name"] = "Plane", ["type"] = "Plane" }
                    }
                },
                new JObject
                {
                    ["name"] = "Addition",
                    ["category"] = "Maths",
                    ["description"] = "Adds two or more numbers",
                    ["inputs"] = new JArray
                    {
                        new JObject { ["name"] = "A", ["type"] = "Number", ["description"] = "First input value" },
                        new JObject { ["name"] = "B", ["type"] = "Number", ["description"] = "Second input value" }
                    },
                    ["outputs"] = new JArray
                    {
                        new JObject { ["name"] = "Result", ["type"] = "Number" }
                    }
                },
                new JObject
                {
                    ["name"] = "Panel",
                    ["category"] = "Params",
                    ["description"] = "Displays text or numeric data",
                    ["inputs"] = new JArray
                    {
                        new JObject { ["name"] = "Input", ["type"] = "Any" }
                    },
                    ["outputs"] = new JArray()
                },
                new JObject
                {
                    ["name"] = "Construct Point",
                    ["category"] = "Vector",
                    ["description"] = "Constructs a point from X, Y, Z coordinates",
                    ["inputs"] = new JArray
                    {
                        new JObject { ["name"] = "X", ["type"] = "Number" },
                        new JObject { ["name"] = "Y", ["type"] = "Number" },
                        new JObject { ["name"] = "Z", ["type"] = "Number" }
                    },
                    ["outputs"] = new JArray
                    {
                        new JObject { ["name"] = "Pt", ["type"] = "Point" }
                    }
                },
                new JObject
                {
                    ["name"] = "Line",
                    ["category"] = "Curve",
                    ["description"] = "Creates a line between two points",
                    ["inputs"] = new JArray
                    {
                        new JObject { ["name"] = "Start", ["type"] = "Point" },
                        new JObject { ["name"] = "End", ["type"] = "Point" }
                    },
                    ["outputs"] = new JArray
                    {
                        new JObject { ["name"] = "L", ["type"] = "Line" }
                    }
                },
                new JObject
                {
                    ["name"] = "Box",
                    ["category"] = "Surface",
                    ["description"] = "Creates a box",
                    ["inputs"] = new JArray
                    {
                        new JObject { ["name"] = "Base", ["type"] = "Plane" },
                        new JObject { ["name"] = "X", ["type"] = "Number", ["description"] = "X size" },
                        new JObject { ["name"] = "Y", ["type"] = "Number", ["description"] = "Y size" },
                        new JObject { ["name"] = "Z", ["type"] = "Number", ["description"] = "Z size" }
                    },
                    ["outputs"] = new JArray
                    {
                        new JObject { ["name"] = "Box", ["type"] = "Box" }
                    }
                }
            },
            ["tips"] = new JArray
            {
                "Always use XY Plane component for plane inputs",
                "Specify parameter names when connecting components",
                "For Circle components, make sure to use the correct inputs (Plane and Radius)",
                "Use get_component_info to check the actual parameter names of a component",
                "Use validate_connection to check if a connection is possible before attempting it"
            }
        };

        return JsonConvert.SerializeObject(guide, Formatting.Indented);
    }

    [McpServerResource(Name = "component_library", UriTemplate = "grasshopper://component_library")]
    [Description("Full library of Grasshopper components organized by category with data type information")]
    public static string GetComponentLibrary()
    {
        var library = new JObject
        {
            ["categories"] = new JArray
            {
                new JObject
                {
                    ["name"] = "Params",
                    ["components"] = new JArray
                    {
                        new JObject { ["name"] = "Point", ["description"] = "Point parameter" },
                        new JObject { ["name"] = "Number Slider", ["description"] = "Numeric slider with range" },
                        new JObject { ["name"] = "Panel", ["description"] = "Text/data display" },
                        new JObject { ["name"] = "Number", ["description"] = "Number parameter" },
                        new JObject { ["name"] = "Curve", ["description"] = "Curve parameter" }
                    }
                },
                new JObject
                {
                    ["name"] = "Maths",
                    ["components"] = new JArray
                    {
                        new JObject { ["name"] = "Addition", ["description"] = "Add numbers" },
                        new JObject { ["name"] = "Subtraction", ["description"] = "Subtract numbers" },
                        new JObject { ["name"] = "Multiplication", ["description"] = "Multiply numbers" },
                        new JObject { ["name"] = "Division", ["description"] = "Divide numbers" }
                    }
                },
                new JObject
                {
                    ["name"] = "Vector",
                    ["components"] = new JArray
                    {
                        new JObject { ["name"] = "XY Plane", ["description"] = "XY plane at origin" },
                        new JObject { ["name"] = "XZ Plane", ["description"] = "XZ plane at origin" },
                        new JObject { ["name"] = "YZ Plane", ["description"] = "YZ plane at origin" },
                        new JObject { ["name"] = "Construct Point", ["description"] = "Point from X,Y,Z" }
                    }
                },
                new JObject
                {
                    ["name"] = "Curve",
                    ["components"] = new JArray
                    {
                        new JObject { ["name"] = "Circle", ["description"] = "Circle from plane and radius" },
                        new JObject { ["name"] = "Line", ["description"] = "Line between two points" },
                        new JObject { ["name"] = "Rectangle", ["description"] = "Rectangle on plane" }
                    }
                },
                new JObject
                {
                    ["name"] = "Surface",
                    ["components"] = new JArray
                    {
                        new JObject { ["name"] = "Box", ["description"] = "Box primitive" },
                        new JObject { ["name"] = "Sphere", ["description"] = "Sphere primitive" },
                        new JObject { ["name"] = "Cylinder", ["description"] = "Cylinder primitive" },
                        new JObject { ["name"] = "Cone", ["description"] = "Cone primitive" },
                        new JObject { ["name"] = "Extrude", ["description"] = "Extrude curve" }
                    }
                }
            },
            ["dataTypes"] = new JArray
            {
                new JObject
                {
                    ["name"] = "Number",
                    ["description"] = "A numeric value",
                    ["compatibleWith"] = new JArray { "Number", "Integer", "Double" }
                },
                new JObject
                {
                    ["name"] = "Point",
                    ["description"] = "A 3D point in space",
                    ["compatibleWith"] = new JArray { "Point3d", "Point" }
                },
                new JObject
                {
                    ["name"] = "Vector",
                    ["description"] = "A 3D vector",
                    ["compatibleWith"] = new JArray { "Vector3d", "Vector" }
                },
                new JObject
                {
                    ["name"] = "Plane",
                    ["description"] = "A plane in 3D space",
                    ["compatibleWith"] = new JArray { "Plane" }
                },
                new JObject
                {
                    ["name"] = "Curve",
                    ["description"] = "A curve object",
                    ["compatibleWith"] = new JArray { "Curve", "Circle", "Line", "Arc", "Polyline" }
                },
                new JObject
                {
                    ["name"] = "Brep",
                    ["description"] = "A boundary representation (solid/surface)",
                    ["compatibleWith"] = new JArray { "Brep", "Surface", "Solid" }
                }
            }
        };

        return JsonConvert.SerializeObject(library, Formatting.Indented);
    }
}
