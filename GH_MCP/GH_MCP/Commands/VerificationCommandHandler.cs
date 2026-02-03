using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using GrasshopperMCP.Models;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Rhino;
using Newtonsoft.Json;

namespace GH_MCP.Commands
{
    /// <summary>
    /// Handles verification commands for AI-driven testing
    /// These commands are used to verify document state and assert conditions
    /// </summary>
    public static class VerificationCommandHandler
    {
        /// <summary>
        /// Export full document state as JSON
        /// Includes all components, their parameters, values, and connections
        /// </summary>
        public static object ExportDocumentState(Command command)
        {
            object result = null;
            Exception exception = null;

            RhinoApp.InvokeOnUiThread(new Action(() =>
            {
                try
                {
                    var doc = Grasshopper.Instances.ActiveCanvas?.Document;
                    if (doc == null)
                    {
                        throw new InvalidOperationException("No active Grasshopper document");
                    }

                    var components = new List<object>();
                    var connections = new List<object>();

                    foreach (var obj in doc.Objects)
                    {
                        var componentData = new Dictionary<string, object>
                        {
                            { "id", obj.InstanceGuid.ToString() },
                            { "type", obj.GetType().Name },
                            { "name", obj.NickName },
                            { "description", obj.Description },
                            { "x", obj.Attributes?.Pivot.X ?? 0 },
                            { "y", obj.Attributes?.Pivot.Y ?? 0 }
                        };

                        // Get inputs/outputs for components
                        if (obj is IGH_Component component)
                        {
                            var inputs = new List<object>();
                            var outputs = new List<object>();

                            foreach (var input in component.Params.Input)
                            {
                                var inputData = new Dictionary<string, object>
                                {
                                    { "name", input.Name },
                                    { "nickname", input.NickName },
                                    { "type", input.TypeName },
                                    { "sourceCount", input.SourceCount },
                                    { "optional", input.Optional }
                                };

                                // Track connections
                                foreach (var source in input.Sources)
                                {
                                    connections.Add(new Dictionary<string, object>
                                    {
                                        { "sourceId", source.Attributes?.GetTopLevel?.DocObject?.InstanceGuid.ToString() },
                                        { "sourceParam", source.Name },
                                        { "targetId", obj.InstanceGuid.ToString() },
                                        { "targetParam", input.Name }
                                    });
                                }

                                inputs.Add(inputData);
                            }

                            foreach (var output in component.Params.Output)
                            {
                                outputs.Add(new Dictionary<string, object>
                                {
                                    { "name", output.Name },
                                    { "nickname", output.NickName },
                                    { "type", output.TypeName },
                                    { "recipientCount", output.Recipients.Count }
                                });
                            }

                            componentData["inputs"] = inputs;
                            componentData["outputs"] = outputs;
                        }

                        // Get slider value
                        if (obj is GH_NumberSlider slider)
                        {
                            componentData["sliderValue"] = slider.CurrentValue;
                            componentData["sliderMin"] = slider.Slider.Minimum;
                            componentData["sliderMax"] = slider.Slider.Maximum;
                        }

                        // Get panel content
                        if (obj is GH_Panel panel)
                        {
                            componentData["panelContent"] = panel.UserText;
                        }

                        components.Add(componentData);
                    }

                    result = new Dictionary<string, object>
                    {
                        { "exportTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") },
                        { "documentName", doc.DisplayName },
                        { "documentPath", doc.FilePath ?? "" },
                        { "componentCount", doc.Objects.Count },
                        { "connectionCount", connections.Count },
                        { "components", components },
                        { "connections", connections }
                    };
                }
                catch (Exception ex)
                {
                    exception = ex;
                    RhinoApp.WriteLine($"Error in ExportDocumentState: {ex.Message}");
                }
            }));

            while (result == null && exception == null)
            {
                Thread.Sleep(10);
            }

            if (exception != null)
            {
                throw exception;
            }

            return result;
        }

        /// <summary>
        /// Assert that a component with the given ID exists
        /// </summary>
        public static object AssertComponentExists(Command command)
        {
            string componentId = command.GetParameter<string>("componentId");
            if (string.IsNullOrEmpty(componentId))
            {
                return new Dictionary<string, object>
                {
                    { "passed", false },
                    { "error", "Missing required parameter: componentId" }
                };
            }

            object result = null;
            Exception exception = null;

            RhinoApp.InvokeOnUiThread(new Action(() =>
            {
                try
                {
                    var doc = Grasshopper.Instances.ActiveCanvas?.Document;
                    if (doc == null)
                    {
                        result = new Dictionary<string, object>
                        {
                            { "passed", false },
                            { "error", "No active Grasshopper document" }
                        };
                        return;
                    }

                    if (!Guid.TryParse(componentId, out Guid guid))
                    {
                        result = new Dictionary<string, object>
                        {
                            { "passed", false },
                            { "error", $"Invalid GUID format: {componentId}" }
                        };
                        return;
                    }

                    var component = doc.FindObject(guid, true);
                    if (component != null)
                    {
                        result = new Dictionary<string, object>
                        {
                            { "passed", true },
                            { "componentId", componentId },
                            { "componentType", component.GetType().Name },
                            { "componentName", component.NickName }
                        };
                    }
                    else
                    {
                        result = new Dictionary<string, object>
                        {
                            { "passed", false },
                            { "componentId", componentId },
                            { "error", $"Component not found: {componentId}" }
                        };
                    }
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            }));

            while (result == null && exception == null)
            {
                Thread.Sleep(10);
            }

            if (exception != null)
            {
                return new Dictionary<string, object>
                {
                    { "passed", false },
                    { "error", exception.Message }
                };
            }

            return result;
        }

        /// <summary>
        /// Assert that a connection exists between two components
        /// </summary>
        public static object AssertConnectionExists(Command command)
        {
            string sourceId = command.GetParameter<string>("sourceId");
            string targetId = command.GetParameter<string>("targetId");
            string sourceParam = command.GetParameter<string>("sourceParam");
            string targetParam = command.GetParameter<string>("targetParam");

            if (string.IsNullOrEmpty(sourceId) || string.IsNullOrEmpty(targetId))
            {
                return new Dictionary<string, object>
                {
                    { "passed", false },
                    { "error", "Missing required parameters: sourceId and targetId" }
                };
            }

            object result = null;
            Exception exception = null;

            RhinoApp.InvokeOnUiThread(new Action(() =>
            {
                try
                {
                    var doc = Grasshopper.Instances.ActiveCanvas?.Document;
                    if (doc == null)
                    {
                        result = new Dictionary<string, object>
                        {
                            { "passed", false },
                            { "error", "No active Grasshopper document" }
                        };
                        return;
                    }

                    if (!Guid.TryParse(sourceId, out Guid sourceGuid) || !Guid.TryParse(targetId, out Guid targetGuid))
                    {
                        result = new Dictionary<string, object>
                        {
                            { "passed", false },
                            { "error", "Invalid GUID format" }
                        };
                        return;
                    }

                    var sourceObj = doc.FindObject(sourceGuid, true);
                    var targetObj = doc.FindObject(targetGuid, true);

                    if (sourceObj == null || targetObj == null)
                    {
                        result = new Dictionary<string, object>
                        {
                            { "passed", false },
                            { "error", $"Component not found: source={sourceObj != null}, target={targetObj != null}" }
                        };
                        return;
                    }

                    // Check if target component has inputs connected from source
                    bool connectionFound = false;
                    string foundSourceParam = null;
                    string foundTargetParam = null;

                    if (targetObj is IGH_Component targetComponent)
                    {
                        foreach (var input in targetComponent.Params.Input)
                        {
                            // If targetParam specified, only check that input
                            if (!string.IsNullOrEmpty(targetParam) &&
                                !input.Name.Equals(targetParam, StringComparison.OrdinalIgnoreCase) &&
                                !input.NickName.Equals(targetParam, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            foreach (var source in input.Sources)
                            {
                                var sourceDocObj = source.Attributes?.GetTopLevel?.DocObject;
                                if (sourceDocObj != null && sourceDocObj.InstanceGuid == sourceGuid)
                                {
                                    // If sourceParam specified, verify it matches
                                    if (!string.IsNullOrEmpty(sourceParam))
                                    {
                                        if (source.Name.Equals(sourceParam, StringComparison.OrdinalIgnoreCase) ||
                                            source.NickName.Equals(sourceParam, StringComparison.OrdinalIgnoreCase))
                                        {
                                            connectionFound = true;
                                            foundSourceParam = source.Name;
                                            foundTargetParam = input.Name;
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        connectionFound = true;
                                        foundSourceParam = source.Name;
                                        foundTargetParam = input.Name;
                                        break;
                                    }
                                }
                            }

                            if (connectionFound) break;
                        }
                    }

                    if (connectionFound)
                    {
                        result = new Dictionary<string, object>
                        {
                            { "passed", true },
                            { "sourceId", sourceId },
                            { "targetId", targetId },
                            { "sourceParam", foundSourceParam },
                            { "targetParam", foundTargetParam }
                        };
                    }
                    else
                    {
                        result = new Dictionary<string, object>
                        {
                            { "passed", false },
                            { "sourceId", sourceId },
                            { "targetId", targetId },
                            { "error", "Connection not found" }
                        };
                    }
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            }));

            while (result == null && exception == null)
            {
                Thread.Sleep(10);
            }

            if (exception != null)
            {
                return new Dictionary<string, object>
                {
                    { "passed", false },
                    { "error", exception.Message }
                };
            }

            return result;
        }

        /// <summary>
        /// Assert that the document has a specific number of components
        /// </summary>
        public static object AssertComponentCount(Command command)
        {
            int? expectedCount = null;
            if (command.Parameters.TryGetValue("expected", out var expectedObj))
            {
                if (int.TryParse(expectedObj?.ToString(), out int parsed))
                {
                    expectedCount = parsed;
                }
            }

            if (!expectedCount.HasValue)
            {
                return new Dictionary<string, object>
                {
                    { "passed", false },
                    { "error", "Missing required parameter: expected (integer)" }
                };
            }

            object result = null;
            Exception exception = null;

            RhinoApp.InvokeOnUiThread(new Action(() =>
            {
                try
                {
                    var doc = Grasshopper.Instances.ActiveCanvas?.Document;
                    if (doc == null)
                    {
                        result = new Dictionary<string, object>
                        {
                            { "passed", false },
                            { "error", "No active Grasshopper document" }
                        };
                        return;
                    }

                    int actualCount = doc.Objects.Count;
                    bool passed = actualCount == expectedCount.Value;

                    result = new Dictionary<string, object>
                    {
                        { "passed", passed },
                        { "expected", expectedCount.Value },
                        { "actual", actualCount },
                        { "difference", actualCount - expectedCount.Value }
                    };
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            }));

            while (result == null && exception == null)
            {
                Thread.Sleep(10);
            }

            if (exception != null)
            {
                return new Dictionary<string, object>
                {
                    { "passed", false },
                    { "error", exception.Message }
                };
            }

            return result;
        }

        /// <summary>
        /// Get a hash of the document state for quick comparison
        /// </summary>
        public static object GetDocumentHash(Command command)
        {
            object result = null;
            Exception exception = null;

            RhinoApp.InvokeOnUiThread(new Action(() =>
            {
                try
                {
                    var doc = Grasshopper.Instances.ActiveCanvas?.Document;
                    if (doc == null)
                    {
                        throw new InvalidOperationException("No active Grasshopper document");
                    }

                    // Build a string representation of document state
                    var sb = new StringBuilder();

                    // Sort components by GUID for consistent ordering
                    var sortedObjects = doc.Objects.OrderBy(o => o.InstanceGuid.ToString()).ToList();

                    foreach (var obj in sortedObjects)
                    {
                        sb.Append(obj.InstanceGuid);
                        sb.Append(obj.GetType().Name);
                        sb.Append(obj.NickName);

                        if (obj is IGH_Component component)
                        {
                            foreach (var input in component.Params.Input)
                            {
                                foreach (var source in input.Sources)
                                {
                                    sb.Append(source.Attributes?.GetTopLevel?.DocObject?.InstanceGuid);
                                    sb.Append(source.Name);
                                    sb.Append(input.Name);
                                }
                            }
                        }

                        if (obj is GH_NumberSlider slider)
                        {
                            sb.Append(slider.CurrentValue);
                        }
                    }

                    // Compute SHA256 hash
                    using (var sha256 = SHA256.Create())
                    {
                        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                        var hashBytes = sha256.ComputeHash(bytes);
                        var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

                        result = new Dictionary<string, object>
                        {
                            { "hash", hash },
                            { "componentCount", doc.Objects.Count },
                            { "timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") }
                        };
                    }
                }
                catch (Exception ex)
                {
                    exception = ex;
                    RhinoApp.WriteLine($"Error in GetDocumentHash: {ex.Message}");
                }
            }));

            while (result == null && exception == null)
            {
                Thread.Sleep(10);
            }

            if (exception != null)
            {
                throw exception;
            }

            return result;
        }
    }
}
