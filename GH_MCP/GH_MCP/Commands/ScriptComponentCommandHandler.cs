using System;
using System.Collections.Generic;
using System.Linq;
using GrasshopperMCP.Models;
using Grasshopper;
using Grasshopper.Kernel;
using GH_MCP.Utils;
using Rhino;
using System.IO;

namespace GH_MCP.Commands
{
    /// <summary>
    /// Handler for script component operations (get/set code, compilation).
    /// </summary>
    public static class ScriptComponentCommandHandler
    {
        private static readonly string LogPath = Path.Combine(GrasshopperMCP.GH_MCPInfo.GhaFolder, "script-commands.log");

        private static void Log(string message)
        {
            try
            {
                File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
            }
            catch
            {
                // Ignore logging errors
            }
        }

        /// <summary>
        /// Lists all script components in the active Grasshopper document.
        /// Returns a list with ID, name, position, and basic info for each script component.
        /// </summary>
        public static object GetScriptComponents(Command command)
        {
            object result = null;
            Exception exception = null;

            Log("GetScriptComponents called");

            // Use ManualResetEventSlim to block until UI thread completes
            using (var completed = new System.Threading.ManualResetEventSlim(false))
            {
                RhinoApp.InvokeOnUiThread(new Action(() =>
                {
                    try
                    {
                        var doc = Instances.ActiveCanvas?.Document;
                        if (doc == null)
                        {
                            Log("ERROR: No active Grasshopper document");
                            throw new InvalidOperationException("No active Grasshopper document");
                        }

                        var scriptComponents = new List<object>();

                        // Find all script components (C#, Python, VB, etc.)
                        foreach (var obj in doc.Objects)
                        {
                            // Check if it's a script component by type name
                            var typeName = obj.GetType().FullName;
                            var isScriptComponent = typeName != null && (
                                typeName.StartsWith("RhinoCodePluginGH.Components.") ||
                                typeName.StartsWith("ScriptComponents.Component_"));

                            if (isScriptComponent && obj is IGH_Component component)
                            {
                                var componentInfo = new Dictionary<string, object>
                                {
                                    { "id", obj.InstanceGuid.ToString() },
                                    { "name", obj.Name },
                                    { "nickname", obj.NickName },
                                    { "type", typeName },
                                    { "category", component.Category },
                                    { "subcategory", component.SubCategory },
                                    { "x", obj.Attributes?.Pivot.X ?? 0 },
                                    { "y", obj.Attributes?.Pivot.Y ?? 0 },
                                    { "input_count", component.Params?.Input?.Count ?? 0 },
                                    { "output_count", component.Params?.Output?.Count ?? 0 }
                                };

                                // Try to get script text
                                try
                                {
                                    var scriptText = CSharpScriptCompiler.GetScriptText(obj);
                                    componentInfo["has_script"] = scriptText != null;
                                    componentInfo["script_length"] = scriptText?.Length ?? 0;
                                }
                                catch
                                {
                                    componentInfo["has_script"] = false;
                                    componentInfo["script_length"] = 0;
                                }

                                scriptComponents.Add(componentInfo);
                            }
                        }

                        result = new Dictionary<string, object>
                        {
                            { "count", scriptComponents.Count },
                            { "components", scriptComponents }
                        };

                        Log($"Found {scriptComponents.Count} script components");
                    }
                    catch (Exception ex)
                    {
                        Log($"ERROR in GetScriptComponents: {ex.Message}");
                        exception = ex;
                    }
                    finally
                    {
                        // Signal that UI thread work is complete
                        completed.Set();
                    }
                }));

                // Wait for UI thread to complete (with 30 second timeout)
                if (!completed.Wait(30000))
                {
                    Log("ERROR: GetScriptComponents timed out");
                    throw new TimeoutException("UI thread operation timed out after 30 seconds");
                }
            }

            if (exception != null)
                throw exception;

            return result;
        }

        /// <summary>
        /// Gets the source code of a C# script component.
        /// Returns the full script text including usings, class definition, and methods.
        /// </summary>
        public static object GetScriptCode(Command command)
        {
            object result = null;
            Exception exception = null;

            var componentId = command.GetParameter<string>("component_id");
            Log($"GetScriptCode called for component: {componentId}");

            // Use ManualResetEventSlim to block until UI thread completes
            using (var completed = new System.Threading.ManualResetEventSlim(false))
            {
                RhinoApp.InvokeOnUiThread(new Action(() =>
                {
                    try
                    {
                        if (string.IsNullOrEmpty(componentId))
                        {
                            Log("ERROR: Missing component_id parameter");
                            throw new ArgumentException("Missing component_id parameter");
                        }

                        var doc = Instances.ActiveCanvas?.Document;
                        if (doc == null)
                        {
                            Log("ERROR: No active Grasshopper document");
                            throw new InvalidOperationException("No active Grasshopper document");
                        }

                        var obj = doc.FindObject(new Guid(componentId), false);
                        if (obj == null)
                        {
                            Log($"ERROR: Component not found: {componentId}");
                            throw new InvalidOperationException($"Component not found: {componentId}");
                        }

                        // Get script text
                        var scriptText = CSharpScriptCompiler.GetScriptText(obj);
                        if (scriptText == null)
                        {
                            Log($"ERROR: Could not retrieve script text from component: {componentId}");
                            throw new InvalidOperationException($"Could not retrieve script text from component: {componentId}");
                        }

                        // Get component info
                        var component = obj as IGH_Component;
                        var inputs = new List<object>();
                        var outputs = new List<object>();

                        if (component != null)
                        {
                            foreach (var param in component.Params.Input)
                            {
                                inputs.Add(new Dictionary<string, object>
                                {
                                    { "name", param.Name },
                                    { "nickname", param.NickName },
                                    { "type", param.TypeName },
                                    { "optional", param.Optional }
                                });
                            }

                            foreach (var param in component.Params.Output)
                            {
                                outputs.Add(new Dictionary<string, object>
                                {
                                    { "name", param.Name },
                                    { "nickname", param.NickName },
                                    { "type", param.TypeName }
                                });
                            }
                        }

                        result = new Dictionary<string, object>
                        {
                            { "component_id", componentId },
                            { "name", obj.Name },
                            { "nickname", obj.NickName },
                            { "code", scriptText },
                            { "code_length", scriptText.Length },
                            { "inputs", inputs },
                            { "outputs", outputs }
                        };

                        Log($"Successfully retrieved script code ({scriptText.Length} chars)");
                    }
                    catch (Exception ex)
                    {
                        Log($"ERROR in GetScriptCode: {ex.Message}");
                        exception = ex;
                    }
                    finally
                    {
                        completed.Set();
                    }
                }));

                if (!completed.Wait(30000))
                {
                    Log("ERROR: GetScriptCode timed out");
                    throw new TimeoutException("UI thread operation timed out after 30 seconds");
                }
            }

            if (exception != null)
                throw exception;

            return result;
        }

        /// <summary>
        /// Sets the source code of a C# script component and optionally compiles it.
        /// </summary>
        public static object SetScriptCode(Command command)
        {
            object result = null;
            Exception exception = null;

            var componentId = command.GetParameter<string>("component_id");
            var code = command.GetParameter<string>("code");
            var compile = command.GetParameter<bool>("compile");

            Log($"SetScriptCode called for component: {componentId}, compile={compile}, code_length={code?.Length ?? 0}");

            // Use ManualResetEventSlim to block until UI thread completes
            using (var completed = new System.Threading.ManualResetEventSlim(false))
            {
                RhinoApp.InvokeOnUiThread(new Action(() =>
                {
                    try
                    {
                        if (string.IsNullOrEmpty(componentId))
                        {
                            Log("ERROR: Missing component_id parameter");
                            throw new ArgumentException("Missing component_id parameter");
                        }

                        if (code == null)
                        {
                            Log("ERROR: Missing code parameter");
                            throw new ArgumentException("Missing code parameter");
                        }

                        var doc = Instances.ActiveCanvas?.Document;
                        if (doc == null)
                        {
                            Log("ERROR: No active Grasshopper document");
                            throw new InvalidOperationException("No active Grasshopper document");
                        }

                        var obj = doc.FindObject(new Guid(componentId), false);
                        if (obj == null)
                        {
                            Log($"ERROR: Component not found: {componentId}");
                            throw new InvalidOperationException($"Component not found: {componentId}");
                        }

                        List<string> errors = new List<string>();
                        bool success = false;

                        if (compile)
                        {
                            // Set and compile
                            Log("Calling SetAndCompile...");
                            success = CSharpScriptCompiler.SetAndCompile(obj, code, out errors);
                            Log($"SetAndCompile result: {success}, errors: {errors.Count}");
                        }
                        else
                        {
                            // Just set the text
                            Log("Calling SetScriptText...");
                            success = CSharpScriptCompiler.SetScriptText(obj, code);
                            Log($"SetScriptText result: {success}");
                        }

                        result = new Dictionary<string, object>
                        {
                            { "success", success },
                            { "component_id", componentId },
                            { "compiled", compile },
                            { "errors", errors },
                            { "error_count", errors.Count }
                        };

                        Log($"SetScriptCode completed successfully: {success}");
                    }
                    catch (Exception ex)
                    {
                        Log($"ERROR in SetScriptCode: {ex.Message}\n{ex.StackTrace}");
                        exception = ex;
                    }
                    finally
                    {
                        completed.Set();
                    }
                }));

                if (!completed.Wait(30000))
                {
                    Log("ERROR: SetScriptCode timed out");
                    throw new TimeoutException("UI thread operation timed out after 30 seconds");
                }
            }

            if (exception != null)
                throw exception;

            return result;
        }

        /// <summary>
        /// Compiles a C# script component.
        /// </summary>
        public static object CompileScript(Command command)
        {
            object result = null;
            Exception exception = null;

            var componentId = command.GetParameter<string>("component_id");
            Log($"CompileScript called for component: {componentId}");

            // Use ManualResetEventSlim to block until UI thread completes
            using (var completed = new System.Threading.ManualResetEventSlim(false))
            {
                RhinoApp.InvokeOnUiThread(new Action(() =>
                {
                    try
                    {
                        if (string.IsNullOrEmpty(componentId))
                        {
                            Log("ERROR: Missing component_id parameter");
                            throw new ArgumentException("Missing component_id parameter");
                        }

                        var doc = Instances.ActiveCanvas?.Document;
                        if (doc == null)
                        {
                            Log("ERROR: No active Grasshopper document");
                            throw new InvalidOperationException("No active Grasshopper document");
                        }

                        var obj = doc.FindObject(new Guid(componentId), false);
                        if (obj == null)
                        {
                            Log($"ERROR: Component not found: {componentId}");
                            throw new InvalidOperationException($"Component not found: {componentId}");
                        }

                        List<string> errors;
                        Log("Calling CSharpScriptCompiler.Compile...");
                        bool success = CSharpScriptCompiler.Compile(obj, out errors);
                        Log($"Compile result: {success}, errors: {errors.Count}");

                        result = new Dictionary<string, object>
                        {
                            { "success", success },
                            { "component_id", componentId },
                            { "errors", errors },
                            { "error_count", errors.Count }
                        };

                        Log($"CompileScript completed successfully: {success}");
                    }
                    catch (Exception ex)
                    {
                        Log($"ERROR in CompileScript: {ex.Message}\n{ex.StackTrace}");
                        exception = ex;
                    }
                    finally
                    {
                        completed.Set();
                    }
                }));

                if (!completed.Wait(30000))
                {
                    Log("ERROR: CompileScript timed out");
                    throw new TimeoutException("UI thread operation timed out after 30 seconds");
                }
            }

            if (exception != null)
                throw exception;

            return result;
        }

        /// <summary>
        /// Investigates a script component using reflection to discover available properties and methods.
        /// This is a diagnostic tool to help understand the component structure.
        /// </summary>
        public static object InvestigateScriptComponent(Command command)
        {
            object result = null;
            Exception exception = null;

            var componentId = command.GetParameter<string>("component_id");
            Log($"InvestigateScriptComponent called for component: {componentId}");

            // Use ManualResetEventSlim to block until UI thread completes
            using (var completed = new System.Threading.ManualResetEventSlim(false))
            {
                RhinoApp.InvokeOnUiThread(new Action(() =>
                {
                    try
                    {
                        if (string.IsNullOrEmpty(componentId))
                        {
                            Log("ERROR: Missing component_id parameter");
                            throw new ArgumentException("Missing component_id parameter");
                        }

                        var doc = Instances.ActiveCanvas?.Document;
                        if (doc == null)
                        {
                            Log("ERROR: No active Grasshopper document");
                            throw new InvalidOperationException("No active Grasshopper document");
                        }

                        var obj = doc.FindObject(new Guid(componentId), false);
                        if (obj == null)
                        {
                            Log($"ERROR: Component not found: {componentId}");
                            throw new InvalidOperationException($"Component not found: {componentId}");
                        }

                        Log("Creating ReflectionInvestigator...");
                        var investigator = new ReflectionInvestigator();
                        var report = investigator.InvestigateComponent(obj);

                        // Save report to file for easy reading
                        var reportPath = Path.Combine(GrasshopperMCP.GH_MCPInfo.GhaFolder,
                            $"investigation-{componentId}.txt");
                        File.WriteAllText(reportPath, report);
                        Log($"Investigation report saved to: {reportPath}");

                        result = new Dictionary<string, object>
                        {
                            { "success", true },
                            { "component_id", componentId },
                            { "report", report },
                            { "report_file", reportPath }
                        };

                        Log("InvestigateScriptComponent completed successfully");
                    }
                    catch (Exception ex)
                    {
                        Log($"ERROR in InvestigateScriptComponent: {ex.Message}\n{ex.StackTrace}");
                        exception = ex;
                    }
                    finally
                    {
                        completed.Set();
                    }
                }));

                if (!completed.Wait(30000))
                {
                    Log("ERROR: InvestigateScriptComponent timed out");
                    throw new TimeoutException("UI thread operation timed out after 30 seconds");
                }
            }

            if (exception != null)
                throw exception;

            return result;
        }
    }
}
