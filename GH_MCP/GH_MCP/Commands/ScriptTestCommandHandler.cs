using System;
using System.Collections.Generic;
using System.IO;
using GrasshopperMCP.Models;
using Grasshopper.Kernel;
using GH_MCP.Utils;
using Rhino;

namespace GH_MCP.Commands
{
    /// <summary>
    /// Handler for testing C# script compilation.
    /// This runs automatically to test compilation mechanisms.
    /// </summary>
    public static class ScriptTestCommandHandler
    {
        private static readonly string LogPath = Path.Combine(
            Path.GetTempPath(),
            "grasshopper-mcp-script-test.log");

        public static object TestScriptCompilation(Command command)
        {
            object result = null;
            Exception exception = null;

            RhinoApp.InvokeOnUiThread(new Action(() =>
            {
                try
                {
                    Log("=== Script Compilation Test Started ===");

                    // Get component ID from arguments
                    var componentId = command.GetParameter<string>("component_id");
                    if (string.IsNullOrEmpty(componentId))
                    {
                        throw new ArgumentException("Missing component_id parameter");
                    }

                    Log($"Component ID: {componentId}");

                    // Find the component
                    var doc = Grasshopper.Instances.ActiveCanvas?.Document;
                    if (doc == null)
                    {
                        throw new InvalidOperationException("No active Grasshopper document");
                    }

                    var docObj = doc.FindObject(new Guid(componentId), false);
                    var component = docObj as IGH_Component;
                    if (component == null)
                    {
                        throw new InvalidOperationException($"Component not found or invalid type: {componentId}");
                    }

                    Log($"Component found: {component.Name} ({component.GetType().FullName})");

                    // Run investigation
                    Log("\n--- Running Investigation ---");
                    var investigationReport = CSharpScriptCompiler.Investigate(component);
                    var investigationPath = Path.Combine(Path.GetDirectoryName(LogPath), "investigation-report.txt");
                    File.WriteAllText(investigationPath, investigationReport);
                    Log($"Investigation report written to: {investigationPath}");

                    // Test compilation methods
                    Log("\n--- Testing Compilation Methods ---");
                    var testResult = CSharpScriptCompiler.TestCompilationMethods(component);
                    var testPath = Path.Combine(Path.GetDirectoryName(LogPath), "compilation-test-result.txt");
                    File.WriteAllText(testPath, testResult.Log);
                    Log($"Test results written to: {testPath}");

                    // Try to get current script text
                    Log("\n--- Getting Script Text ---");
                    var currentScript = CSharpScriptCompiler.GetScriptText(component);
                    if (currentScript != null)
                    {
                        Log($"Successfully retrieved script text ({currentScript.Length} characters)");
                        var scriptPath = Path.Combine(Path.GetDirectoryName(LogPath), "current-script.txt");
                        File.WriteAllText(scriptPath, currentScript);
                        Log($"Script text written to: {scriptPath}");
                    }
                    else
                    {
                        Log("Failed to retrieve script text");
                    }

                    // Try to set new script text
                    Log("\n--- Setting New Script Text ---");
                    var newScript = @"// Test script
private void RunScript(object x, ref object A)
{
    A = ""Hello from automated test!"";
}";

                    if (CSharpScriptCompiler.SetScriptText(component, newScript))
                    {
                        Log("✅ Successfully set new script text");
                    }
                    else
                    {
                        Log("❌ Failed to set new script text");
                    }

                    // Try compilation
                    Log("\n--- Attempting Compilation ---");
                    List<string> errors;
                    bool success = CSharpScriptCompiler.Compile(component, out errors);

                    if (success)
                    {
                        Log("✅ COMPILATION SUCCEEDED!");
                    }
                    else
                    {
                        Log("❌ COMPILATION FAILED");
                        Log($"Errors ({errors.Count}):");
                        foreach (var error in errors)
                        {
                            Log($"  - {error}");
                        }
                    }

                    Log("\n=== Test Completed ===");
                    Log($"Full log: {LogPath}");

                    result = new Dictionary<string, object>
                    {
                        { "success", success },
                        { "log_path", LogPath },
                        { "investigation_path", investigationPath },
                        { "test_path", testPath },
                        { "errors", errors }
                    };
                }
                catch (Exception ex)
                {
                    Log($"❌ Exception: {ex.Message}");
                    Log(ex.StackTrace);
                    exception = ex;
                }
            }));

            if (exception != null)
                throw exception;

            return result;
        }

        private static void Log(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logMessage = $"[{timestamp}] {message}{Environment.NewLine}";

            try
            {
                File.AppendAllText(LogPath, logMessage);
                RhinoApp.WriteLine($"[ScriptTest] {message}");
            }
            catch
            {
                // Silently fail if logging doesn't work
            }
        }
    }
}
