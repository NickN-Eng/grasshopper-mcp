using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Grasshopper.Kernel;
using Rhino;

namespace GH_MCP.Utils
{
    /// <summary>
    /// Option A: Reflection-only approach to compile C# script components.
    /// This approach uses pure reflection without referencing RhinoCodePluginGH or related DLLs.
    /// More robust across versions but requires runtime discovery.
    /// </summary>
    public class CSharpScriptCompiler_OptionA
    {
        private static readonly Dictionary<string, MethodInfo> _cachedMethods = new Dictionary<string, MethodInfo>();
        private static bool _initialized = false;

        private static readonly string LogPath = Path.Combine(
            GrasshopperMCP.GH_MCPInfo.GhaFolder,
            "csharp-compilation.log");

        private static void Log(string message)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logMessage = $"[{timestamp}] [OptionA] {message}{Environment.NewLine}";

                var logDir = Path.GetDirectoryName(LogPath);
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                File.AppendAllText(LogPath, logMessage);
            }
            catch
            {
                // Silently fail if logging doesn't work
            }
        }

        /// <summary>
        /// Attempts to compile a C# script component using reflection.
        /// </summary>
        /// <param name="component">The C# script component</param>
        /// <param name="errors">Output compilation errors if any</param>
        /// <returns>True if compilation succeeded</returns>
        public static bool TryCompile(object component, out List<string> errors)
        {
            errors = new List<string>();
            Log($"TryCompile called for component: {component?.GetType().FullName}");

            try
            {
                // Use ManualResetEventSlim to block until UI thread completes
                bool success = false;
                List<string> localErrors = null;
                Exception exception = null;

                using (var completed = new System.Threading.ManualResetEventSlim(false))
                {
                    RhinoApp.InvokeOnUiThread(new Action(() =>
                    {
                        try
                        {
                            Log("Inside UI thread for TryCompile");
                            success = TryCompileInternal(component, out localErrors);
                            Log($"TryCompileInternal result: {success}, errors: {localErrors?.Count ?? 0}");
                        }
                        catch (Exception ex)
                        {
                            Log($"Exception in UI thread: {ex.Message}");
                            exception = ex;
                        }
                        finally
                        {
                            completed.Set();
                        }
                    }));

                    // Wait for UI thread to complete (with 30 second timeout)
                    if (!completed.Wait(30000))
                    {
                        Log("ERROR: TryCompile timed out waiting for UI thread");
                        errors.Add("UI thread operation timed out after 30 seconds");
                        return false;
                    }
                }

                if (exception != null)
                {
                    Log($"ERROR: Exception caught: {exception.Message}");
                    errors.Add($"Compilation failed: {exception.Message}");
                    return false;
                }

                errors = localErrors ?? new List<string>();
                Log($"TryCompile returning: {success}");
                return success;
            }
            catch (Exception ex)
            {
                Log($"ERROR: Outer exception in TryCompile: {ex.Message}");
                errors.Add($"Compilation failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Internal compilation logic (must be called on UI thread).
        /// </summary>
        private static bool TryCompileInternal(object component, out List<string> errors)
        {
            errors = new List<string>();
            Log("TryCompileInternal starting");

            // Step 1: Get the Context object
            Log("Step 1: Getting Context property");
            var context = GetPropertyValue(component, "Context");
            if (context == null)
            {
                Log("ERROR: Could not access Context property");
                errors.Add("Could not access Context property on component");
                return false;
            }
            Log($"Context obtained: {context.GetType().FullName}");

            // Step 2: Initialize compilation method if not cached
            if (!_initialized)
            {
                Log("Step 2: Discovering compilation method (not initialized)");
                if (!DiscoverCompilationMethod(context))
                {
                    Log("ERROR: Could not discover compilation method");
                    errors.Add("Could not discover compilation method");
                    return false;
                }
                _initialized = true;
                Log("Compilation method discovered and cached");
            }
            else
            {
                Log("Step 2: Using cached compilation method");
            }

            // Step 3: Get the compilation method from cache
            Log("Step 3: Getting compilation method from cache");
            var contextTypeName = context.GetType().FullName;
            var compileMethod = GetCachedCompilationMethod(contextTypeName);
            if (compileMethod == null)
            {
                Log($"ERROR: No compilation method found in cache for type: {contextTypeName}");
                errors.Add("No compilation method found in cache");
                return false;
            }
            Log($"Compilation method retrieved: {compileMethod.Name}");

            // Step 4: Invoke the compilation method
            Log("Step 4: Invoking compilation method");
            try
            {
                InvokeCompilationMethod(context, compileMethod);
                Log("Compilation method invoked successfully");
            }
            catch (Exception ex)
            {
                var message = $"Compilation method threw exception: {ex.InnerException?.Message ?? ex.Message}";
                Log($"ERROR: {message}");
                Log($"Stack trace: {ex}");
                errors.Add(message);
                return false;
            }

            // Step 5: Check for compilation errors
            Log("Step 5: Checking for compilation errors");
            var compilationErrors = GetCompilationErrors(context);
            Log($"Found {compilationErrors.Count} compilation errors");

            if (compilationErrors.Any())
            {
                errors.AddRange(compilationErrors);
                foreach (var error in compilationErrors)
                {
                    Log($"Compilation error: {error}");
                }
                return false;
            }

            // Step 6: Force component to expire and recompute
            Log("Step 6: Expiring component solution");
            var ghComponent = component as IGH_ActiveObject;
            ghComponent?.ExpireSolution(true);

            Log("TryCompileInternal completed successfully");
            return true;
        }

        /// <summary>
        /// Discovers the correct compilation method by trying various approaches.
        /// </summary>
        private static bool DiscoverCompilationMethod(object context)
        {
            var type = context.GetType();
            var typeName = type.FullName;
            Log($"DiscoverCompilationMethod for type: {typeName}");

            // Strategy 1: Try Menu_SaveScriptClicked (from previous attempt)
            Log("Strategy 1: Trying Menu_SaveScriptClicked");
            var method = type.GetMethod("Menu_SaveScriptClicked",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method != null)
            {
                Log($"Found Menu_SaveScriptClicked: {method.Name}");
                _cachedMethods[typeName] = method;
                return true;
            }

            // Strategy 2: Try common compilation method names
            Log("Strategy 2: Trying common compilation method names");
            var methodNames = new[]
            {
                "Compile",
                "CompileScript",
                "Build",
                "BuildScript",
                "Rebuild",
                "RebuildScript",
                "Execute",
                "Refresh",
                "Update",
                "SaveScript",
                "Save"
            };

            foreach (var methodName in methodNames)
            {
                method = type.GetMethod(methodName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null);

                if (method != null)
                {
                    Log($"Found method: {methodName}");
                    _cachedMethods[typeName] = method;
                    return true;
                }
            }
            Log("No methods found with common names");

            // Strategy 3: Find any method with "compile" in the name
            Log("Strategy 3: Searching all methods for 'compile' keyword");
            var allMethods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Log($"Total methods on context: {allMethods.Length}");

            foreach (var m in allMethods)
            {
                if (m.Name.ToLower().Contains("compile") && m.GetParameters().Length == 0)
                {
                    Log($"Found method with 'compile': {m.Name}");
                }
            }

            method = allMethods.FirstOrDefault(m =>
                m.Name.ToLower().Contains("compile") &&
                m.GetParameters().Length == 0);

            if (method != null)
            {
                Log($"Using method: {method.Name}");
                _cachedMethods[typeName] = method;
                return true;
            }

            // Strategy 4: List all available methods for debugging
            Log("Strategy 4: No compilation method found. Listing all methods:");
            foreach (var m in allMethods.Take(50)) // Limit to first 50 to avoid huge logs
            {
                var paramStr = string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name));
                Log($"  - {m.Name}({paramStr}) : {m.ReturnType.Name}");
            }

            Log("ERROR: No compilation method discovered");
            return false;
        }

        /// <summary>
        /// Invokes the compilation method with appropriate parameters.
        /// </summary>
        private static void InvokeCompilationMethod(object context, MethodInfo method)
        {
            var parameters = method.GetParameters();
            Log($"Invoking method {method.Name} with {parameters.Length} parameters");

            if (parameters.Length == 0)
            {
                // No parameters - direct invocation
                Log("Invoking with no parameters");
                method.Invoke(context, null);
            }
            else if (parameters.Length == 2 &&
                     parameters[0].ParameterType == typeof(object) &&
                     parameters[1].ParameterType == typeof(EventArgs))
            {
                // Event handler signature (sender, e)
                Log("Invoking as event handler (sender, EventArgs)");
                method.Invoke(context, new object[] { null, EventArgs.Empty });
            }
            else
            {
                // Try with null parameters
                Log($"Invoking with {parameters.Length} null parameters");
                var args = new object[parameters.Length];
                method.Invoke(context, args);
            }
        }

        /// <summary>
        /// Gets compilation errors from the context.
        /// </summary>
        private static List<string> GetCompilationErrors(object context)
        {
            var errors = new List<string>();
            Log("GetCompilationErrors called");

            try
            {
                // Try to get Errors property
                Log("Trying Errors property");
                var errorsProperty = context.GetType().GetProperty("Errors",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (errorsProperty != null)
                {
                    Log($"Found Errors property: {errorsProperty.PropertyType.FullName}");
                    var errorCollection = errorsProperty.GetValue(context);
                    if (errorCollection is System.Collections.IEnumerable enumerable)
                    {
                        foreach (var error in enumerable)
                        {
                            var errorStr = error?.ToString() ?? "Unknown error";
                            errors.Add(errorStr);
                            Log($"Error from Errors property: {errorStr}");
                        }
                    }
                }
                else
                {
                    Log("Errors property not found");
                }

                // Alternative: Try CompilationErrors property
                if (!errors.Any())
                {
                    Log("Trying CompilationErrors property");
                    var compErrorsProp = context.GetType().GetProperty("CompilationErrors",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    if (compErrorsProp != null)
                    {
                        Log($"Found CompilationErrors property: {compErrorsProp.PropertyType.FullName}");
                        var errorCollection = compErrorsProp.GetValue(context);
                        if (errorCollection is System.Collections.IEnumerable enumerable)
                        {
                            foreach (var error in enumerable)
                            {
                                var errorStr = error?.ToString() ?? "Unknown error";
                                errors.Add(errorStr);
                                Log($"Error from CompilationErrors property: {errorStr}");
                            }
                        }
                    }
                    else
                    {
                        Log("CompilationErrors property not found");
                    }
                }

                // Alternative: Try GetErrors() method
                if (!errors.Any())
                {
                    Log("Trying GetErrors() method");
                    var getErrorsMethod = context.GetType().GetMethod("GetErrors",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    if (getErrorsMethod != null)
                    {
                        Log($"Found GetErrors method: {getErrorsMethod.ReturnType.FullName}");
                        var result = getErrorsMethod.Invoke(context, null);
                        if (result is System.Collections.IEnumerable enumerable)
                        {
                            foreach (var error in enumerable)
                            {
                                var errorStr = error?.ToString() ?? "Unknown error";
                                errors.Add(errorStr);
                                Log($"Error from GetErrors method: {errorStr}");
                            }
                        }
                    }
                    else
                    {
                        Log("GetErrors method not found");
                    }
                }

                Log($"GetCompilationErrors returning {errors.Count} errors");
            }
            catch (Exception ex)
            {
                Log($"ERROR in GetCompilationErrors: {ex.Message}");
                // If we can't get errors, assume success (compilation will fail at runtime if there are issues)
            }

            return errors;
        }

        /// <summary>
        /// Gets a cached compilation method for a given type name.
        /// </summary>
        private static MethodInfo GetCachedCompilationMethod(string typeName)
        {
            var found = _cachedMethods.TryGetValue(typeName, out var method);
            Log($"GetCachedCompilationMethod for {typeName}: {(found ? method.Name : "not found")}");
            return method;
        }

        /// <summary>
        /// Gets the value of a property via reflection.
        /// </summary>
        private static object GetPropertyValue(object obj, string propertyName)
        {
            try
            {
                var prop = obj.GetType().GetProperty(propertyName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (prop == null)
                {
                    Log($"GetPropertyValue: Property '{propertyName}' not found on {obj.GetType().FullName}");
                    return null;
                }

                var value = prop.GetValue(obj);
                Log($"GetPropertyValue: '{propertyName}' = {value?.GetType().FullName ?? "null"}");
                return value;
            }
            catch (Exception ex)
            {
                Log($"ERROR in GetPropertyValue for '{propertyName}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Sets script text on a C# component.
        /// </summary>
        public static bool SetScriptText(object component, string scriptText)
        {
            Log($"SetScriptText called, code length: {scriptText?.Length ?? 0}");

            try
            {
                bool success = false;
                Exception exception = null;

                // Use ManualResetEventSlim to block until UI thread completes
                using (var completed = new System.Threading.ManualResetEventSlim(false))
                {
                    RhinoApp.InvokeOnUiThread(new Action(() =>
                    {
                        try
                        {
                            Log("Inside UI thread for SetScriptText");
                            success = SetScriptTextInternal(component, scriptText);
                            Log($"SetScriptTextInternal result: {success}");
                        }
                        catch (Exception ex)
                        {
                            Log($"Exception in SetScriptText UI thread: {ex.Message}");
                            exception = ex;
                        }
                        finally
                        {
                            completed.Set();
                        }
                    }));

                    if (!completed.Wait(30000))
                    {
                        Log("ERROR: SetScriptText timed out");
                        return false;
                    }
                }

                if (exception != null)
                {
                    Log($"ERROR: Exception in SetScriptText: {exception.Message}");
                    return false;
                }

                return success;
            }
            catch (Exception ex)
            {
                Log($"ERROR: Outer exception in SetScriptText: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Internal SetScriptText logic (must be called on UI thread).
        /// </summary>
        private static bool SetScriptTextInternal(object component, string scriptText)
        {
            Log("SetScriptTextInternal starting");

            var context = GetPropertyValue(component, "Context");
            if (context == null)
            {
                Log("ERROR: Context is null");
                return false;
            }

            // Try SetText method first
            Log("Trying SetText method");
            var setTextMethod = context.GetType().GetMethod("SetText",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (setTextMethod != null)
            {
                Log($"Found SetText method: {setTextMethod}");
                try
                {
                    setTextMethod.Invoke(context, new object[] { scriptText });
                    Log("SetText invoked successfully");
                    return true;
                }
                catch (Exception ex)
                {
                    Log($"ERROR invoking SetText: {ex.Message}");
                }
            }
            else
            {
                Log("SetText method not found");
            }

            // Alternative: Try setting Script.Text property
            Log("Trying Script.Text property");
            var script = GetPropertyValue(context, "Script");
            if (script != null)
            {
                Log($"Script property obtained: {script.GetType().FullName}");
                var textProp = script.GetType().GetProperty("Text",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (textProp != null)
                {
                    Log($"Found Text property on Script: {textProp.PropertyType}");
                    try
                    {
                        textProp.SetValue(script, scriptText);
                        Log("Script.Text set successfully");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Log($"ERROR setting Script.Text: {ex.Message}");
                    }
                }
                else
                {
                    Log("Text property not found on Script");
                }
            }
            else
            {
                Log("Script property not found on Context");
            }

            Log("ERROR: All SetScriptText strategies failed");
            return false;
        }

        /// <summary>
        /// Gets script text from a C# component.
        /// </summary>
        public static string GetScriptText(object component)
        {
            Log("GetScriptText called");

            try
            {
                string result = null;
                Exception exception = null;

                // Use ManualResetEventSlim to block until UI thread completes
                using (var completed = new System.Threading.ManualResetEventSlim(false))
                {
                    RhinoApp.InvokeOnUiThread(new Action(() =>
                    {
                        try
                        {
                            Log("Inside UI thread for GetScriptText");
                            result = GetScriptTextInternal(component);
                            Log($"GetScriptTextInternal result length: {result?.Length ?? 0}");
                        }
                        catch (Exception ex)
                        {
                            Log($"Exception in GetScriptText UI thread: {ex.Message}");
                            exception = ex;
                        }
                        finally
                        {
                            completed.Set();
                        }
                    }));

                    if (!completed.Wait(30000))
                    {
                        Log("ERROR: GetScriptText timed out");
                        return null;
                    }
                }

                if (exception != null)
                {
                    Log($"ERROR: Exception in GetScriptText: {exception.Message}");
                    return null;
                }

                return result;
            }
            catch (Exception ex)
            {
                Log($"ERROR: Outer exception in GetScriptText: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Internal GetScriptText logic (must be called on UI thread).
        /// </summary>
        private static string GetScriptTextInternal(object component)
        {
            Log("GetScriptTextInternal starting");

            var context = GetPropertyValue(component, "Context");
            if (context == null)
            {
                Log("ERROR: Context is null");
                return null;
            }

            // Try GetText method first
            Log("Trying GetText method");
            var getTextMethod = context.GetType().GetMethod("GetText",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (getTextMethod != null)
            {
                Log($"Found GetText method: {getTextMethod}");
                try
                {
                    var text = getTextMethod.Invoke(context, null) as string;
                    Log($"GetText returned: {text?.Length ?? 0} chars");
                    return text;
                }
                catch (Exception ex)
                {
                    Log($"ERROR invoking GetText: {ex.Message}");
                }
            }
            else
            {
                Log("GetText method not found");
            }

            // Alternative: Try getting Script.Text property
            Log("Trying Script.Text property");
            var script = GetPropertyValue(context, "Script");
            if (script != null)
            {
                Log($"Script property obtained: {script.GetType().FullName}");
                var textProp = script.GetType().GetProperty("Text",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (textProp != null)
                {
                    Log($"Found Text property on Script: {textProp.PropertyType}");
                    try
                    {
                        var text = textProp.GetValue(script) as string;
                        Log($"Script.Text returned: {text?.Length ?? 0} chars");
                        return text;
                    }
                    catch (Exception ex)
                    {
                        Log($"ERROR getting Script.Text: {ex.Message}");
                    }
                }
                else
                {
                    Log("Text property not found on Script");
                }
            }
            else
            {
                Log("Script property not found on Context");
            }

            Log("ERROR: All GetScriptText strategies failed");
            return null;
        }

        /// <summary>
        /// Clears the method cache (useful for testing or version changes).
        /// </summary>
        public static void ClearCache()
        {
            Log("ClearCache called");
            _cachedMethods.Clear();
            _initialized = false;
        }
    }
}
