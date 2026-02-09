using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GrasshopperMCP.Models;
using Grasshopper;
using Grasshopper.Kernel;
using Rhino;
using Newtonsoft.Json;

namespace GH_MCP.Commands
{
    /// <summary>
    /// Debug commands for investigating Grasshopper canvas state
    /// </summary>
    public static class DebugCommandHandler
    {
        /// <summary>
        /// Dumps all components on the canvas to a JSON file with full type information.
        /// This helps identify what type names Grasshopper actually uses.
        /// </summary>
        public static object DumpCanvasComponents(Command command)
        {
            object result = null;
            Exception exception = null;

            RhinoApp.InvokeOnUiThread(new Action(() =>
            {
                try
                {
                    var doc = Instances.ActiveCanvas?.Document;
                    if (doc == null)
                    {
                        throw new InvalidOperationException("No active Grasshopper document");
                    }

                    var components = new List<object>();

                    foreach (var obj in doc.Objects)
                    {
                        var componentInfo = new Dictionary<string, object>
                        {
                            { "id", obj.InstanceGuid.ToString() },
                            { "name", obj.Name },
                            { "nickname", obj.NickName },
                            { "type_full_name", obj.GetType().FullName },
                            { "type_name", obj.GetType().Name },
                            { "type_namespace", obj.GetType().Namespace },
                            { "assembly_name", obj.GetType().Assembly.GetName().Name },
                            { "is_component", obj is IGH_Component }
                        };

                        // Add component-specific info
                        if (obj is IGH_Component component)
                        {
                            componentInfo["category"] = component.Category;
                            componentInfo["subcategory"] = component.SubCategory;
                            componentInfo["input_count"] = component.Params?.Input?.Count ?? 0;
                            componentInfo["output_count"] = component.Params?.Output?.Count ?? 0;

                            // List all input parameter names
                            var inputParams = new List<string>();
                            if (component.Params?.Input != null)
                            {
                                foreach (var param in component.Params.Input)
                                {
                                    inputParams.Add(param.NickName);
                                }
                            }
                            componentInfo["input_params"] = inputParams;

                            // List all output parameter names
                            var outputParams = new List<string>();
                            if (component.Params?.Output != null)
                            {
                                foreach (var param in component.Params.Output)
                                {
                                    outputParams.Add(param.NickName);
                                }
                            }
                            componentInfo["output_params"] = outputParams;
                        }

                        // Add position
                        if (obj.Attributes?.Pivot != null)
                        {
                            componentInfo["x"] = obj.Attributes.Pivot.X;
                            componentInfo["y"] = obj.Attributes.Pivot.Y;
                        }

                        // Check for common properties that might indicate it's a script component
                        var type = obj.GetType();
                        var properties = type.GetProperties()
                            .Select(p => p.Name)
                            .ToList();

                        componentInfo["has_context_property"] = properties.Contains("Context");
                        componentInfo["has_code_property"] = properties.Contains("Code");
                        componentInfo["has_script_property"] = properties.Contains("Script");
                        componentInfo["all_properties"] = properties;

                        components.Add(componentInfo);
                    }

                    // Write to JSON file
                    var tempPath = Path.Combine(Path.GetTempPath(), "grasshopper-canvas-dump.json");
                    var json = JsonConvert.SerializeObject(new
                    {
                        timestamp = DateTime.Now.ToString("o"),
                        component_count = components.Count,
                        components = components
                    }, Formatting.Indented);

                    File.WriteAllText(tempPath, json);

                    result = new Dictionary<string, object>
                    {
                        { "success", true },
                        { "component_count", components.Count },
                        { "dump_path", tempPath },
                        { "components", components }
                    };
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            }));

            if (exception != null)
                throw exception;

            return result;
        }

        /// <summary>
        /// Gets detailed type information for a specific component.
        /// Useful for investigating properties and methods available on script components.
        /// </summary>
        public static object InspectComponent(Command command)
        {
            object result = null;
            Exception exception = null;

            RhinoApp.InvokeOnUiThread(new Action(() =>
            {
                try
                {
                    var componentId = command.GetParameter<string>("component_id");
                    if (string.IsNullOrEmpty(componentId))
                    {
                        throw new ArgumentException("Missing component_id parameter");
                    }

                    var doc = Instances.ActiveCanvas?.Document;
                    if (doc == null)
                    {
                        throw new InvalidOperationException("No active Grasshopper document");
                    }

                    var obj = doc.FindObject(new Guid(componentId), false);
                    if (obj == null)
                    {
                        throw new InvalidOperationException($"Component not found: {componentId}");
                    }

                    var type = obj.GetType();

                    // Get all properties
                    var properties = type.GetProperties()
                        .Select(p => new Dictionary<string, object>
                        {
                            { "name", p.Name },
                            { "type", p.PropertyType.FullName },
                            { "can_read", p.CanRead },
                            { "can_write", p.CanWrite }
                        })
                        .ToList();

                    // Get all methods
                    var methods = type.GetMethods()
                        .Where(m => m.IsPublic && !m.Name.StartsWith("get_") && !m.Name.StartsWith("set_"))
                        .Select(m => new Dictionary<string, object>
                        {
                            { "name", m.Name },
                            { "return_type", m.ReturnType.FullName },
                            { "parameters", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}").ToList() }
                        })
                        .ToList();

                    // Write detailed report to file
                    var tempPath = Path.Combine(Path.GetTempPath(), $"component-inspection-{componentId}.txt");
                    using (var writer = new StreamWriter(tempPath))
                    {
                        writer.WriteLine($"Component Inspection Report");
                        writer.WriteLine($"Generated: {DateTime.Now}");
                        writer.WriteLine($"Component ID: {componentId}");
                        writer.WriteLine();
                        writer.WriteLine($"Type: {type.FullName}");
                        writer.WriteLine($"Assembly: {type.Assembly.GetName().Name}");
                        writer.WriteLine($"Namespace: {type.Namespace}");
                        writer.WriteLine();
                        writer.WriteLine($"Properties ({properties.Count}):");
                        foreach (var prop in properties)
                        {
                            writer.WriteLine($"  - {prop["name"]} : {prop["type"]} (R:{prop["can_read"]} W:{prop["can_write"]})");
                        }
                        writer.WriteLine();
                        writer.WriteLine($"Methods ({methods.Count}):");
                        foreach (var method in methods)
                        {
                            var paramList = method["parameters"] as List<string>;
                            writer.WriteLine($"  - {method["name"]}({string.Join(", ", paramList)}) : {method["return_type"]}");
                        }
                    }

                    result = new Dictionary<string, object>
                    {
                        { "component_id", componentId },
                        { "name", obj.Name },
                        { "nickname", obj.NickName },
                        { "type_full_name", type.FullName },
                        { "type_name", type.Name },
                        { "namespace", type.Namespace },
                        { "assembly", type.Assembly.GetName().Name },
                        { "property_count", properties.Count },
                        { "method_count", methods.Count },
                        { "properties", properties },
                        { "methods", methods },
                        { "inspection_report_path", tempPath }
                    };
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            }));

            if (exception != null)
                throw exception;

            return result;
        }
    }
}
