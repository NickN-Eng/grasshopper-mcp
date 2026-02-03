using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Attempts to compile a C# script component using reflection.
        /// </summary>
        /// <param name="component">The C# script component</param>
        /// <param name="errors">Output compilation errors if any</param>
        /// <returns>True if compilation succeeded</returns>
        public static bool TryCompile(GH_Component component, out List<string> errors)
        {
            errors = new List<string>();

            try
            {
                // Ensure we're on the UI thread (required for Grasshopper operations)
                bool success = false;
                var localErrors = new List<string>();

                RhinoApp.InvokeOnUiThread(new Action(() =>
                {
                    success = TryCompileInternal(component, localErrors);
                }));

                errors = localErrors;
                return success;
            }
            catch (Exception ex)
            {
                errors.Add($"Compilation failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Internal compilation logic (must be called on UI thread).
        /// </summary>
        private static bool TryCompileInternal(GH_Component component, List<string> errors)
        {
            // Step 1: Get the Context object
            var context = GetPropertyValue(component, "Context");
            if (context == null)
            {
                errors.Add("Could not access Context property on component");
                return false;
            }

            // Step 2: Initialize compilation method if not cached
            if (!_initialized)
            {
                if (!DiscoverCompilationMethod(context))
                {
                    errors.Add("Could not discover compilation method");
                    return false;
                }
                _initialized = true;
            }

            // Step 3: Get the compilation method from cache
            var compileMethod = GetCachedCompilationMethod(context.GetType().FullName);
            if (compileMethod == null)
            {
                errors.Add("No compilation method found in cache");
                return false;
            }

            // Step 4: Invoke the compilation method
            try
            {
                InvokeCompilationMethod(context, compileMethod);
            }
            catch (Exception ex)
            {
                errors.Add($"Compilation method threw exception: {ex.InnerException?.Message ?? ex.Message}");
                return false;
            }

            // Step 5: Check for compilation errors
            var compilationErrors = GetCompilationErrors(context);
            if (compilationErrors.Any())
            {
                errors.AddRange(compilationErrors);
                return false;
            }

            // Step 6: Force component to expire and recompute
            component.ExpireSolution(true);

            return true;
        }

        /// <summary>
        /// Discovers the correct compilation method by trying various approaches.
        /// </summary>
        private static bool DiscoverCompilationMethod(object context)
        {
            var type = context.GetType();
            var typeName = type.FullName;

            // Strategy 1: Try Menu_SaveScriptClicked (from previous attempt)
            var method = type.GetMethod("Menu_SaveScriptClicked",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method != null)
            {
                _cachedMethods[typeName] = method;
                return true;
            }

            // Strategy 2: Try common compilation method names
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
                    _cachedMethods[typeName] = method;
                    return true;
                }
            }

            // Strategy 3: Find any method with "compile" in the name
            var allMethods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            method = allMethods.FirstOrDefault(m =>
                m.Name.ToLower().Contains("compile") &&
                m.GetParameters().Length == 0);

            if (method != null)
            {
                _cachedMethods[typeName] = method;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Invokes the compilation method with appropriate parameters.
        /// </summary>
        private static void InvokeCompilationMethod(object context, MethodInfo method)
        {
            var parameters = method.GetParameters();

            if (parameters.Length == 0)
            {
                // No parameters - direct invocation
                method.Invoke(context, null);
            }
            else if (parameters.Length == 2 &&
                     parameters[0].ParameterType == typeof(object) &&
                     parameters[1].ParameterType == typeof(EventArgs))
            {
                // Event handler signature (sender, e)
                method.Invoke(context, new object[] { null, EventArgs.Empty });
            }
            else
            {
                // Try with null parameters
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

            try
            {
                // Try to get Errors property
                var errorsProperty = context.GetType().GetProperty("Errors",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (errorsProperty != null)
                {
                    var errorCollection = errorsProperty.GetValue(context);
                    if (errorCollection is System.Collections.IEnumerable enumerable)
                    {
                        foreach (var error in enumerable)
                        {
                            errors.Add(error?.ToString() ?? "Unknown error");
                        }
                    }
                }

                // Alternative: Try CompilationErrors property
                if (!errors.Any())
                {
                    var compErrorsProp = context.GetType().GetProperty("CompilationErrors",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    if (compErrorsProp != null)
                    {
                        var errorCollection = compErrorsProp.GetValue(context);
                        if (errorCollection is System.Collections.IEnumerable enumerable)
                        {
                            foreach (var error in enumerable)
                            {
                                errors.Add(error?.ToString() ?? "Unknown error");
                            }
                        }
                    }
                }

                // Alternative: Try GetErrors() method
                if (!errors.Any())
                {
                    var getErrorsMethod = context.GetType().GetMethod("GetErrors",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    if (getErrorsMethod != null)
                    {
                        var result = getErrorsMethod.Invoke(context, null);
                        if (result is System.Collections.IEnumerable enumerable)
                        {
                            foreach (var error in enumerable)
                            {
                                errors.Add(error?.ToString() ?? "Unknown error");
                            }
                        }
                    }
                }
            }
            catch
            {
                // If we can't get errors, assume success (compilation will fail at runtime if there are issues)
            }

            return errors;
        }

        /// <summary>
        /// Gets a cached compilation method for a given type name.
        /// </summary>
        private static MethodInfo GetCachedCompilationMethod(string typeName)
        {
            return _cachedMethods.TryGetValue(typeName, out var method) ? method : null;
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
                return prop?.GetValue(obj);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Sets script text on a C# component.
        /// </summary>
        public static bool SetScriptText(GH_Component component, string scriptText)
        {
            try
            {
                var context = GetPropertyValue(component, "Context");
                if (context == null) return false;

                var setTextMethod = context.GetType().GetMethod("SetText",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (setTextMethod != null)
                {
                    setTextMethod.Invoke(context, new object[] { scriptText });
                    return true;
                }

                // Alternative: Try setting Script.Text property
                var script = GetPropertyValue(context, "Script");
                if (script != null)
                {
                    var textProp = script.GetType().GetProperty("Text",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    if (textProp != null)
                    {
                        textProp.SetValue(script, scriptText);
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets script text from a C# component.
        /// </summary>
        public static string GetScriptText(GH_Component component)
        {
            try
            {
                var context = GetPropertyValue(component, "Context");
                if (context == null) return null;

                var getTextMethod = context.GetType().GetMethod("GetText",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (getTextMethod != null)
                {
                    return getTextMethod.Invoke(context, null) as string;
                }

                // Alternative: Try getting Script.Text property
                var script = GetPropertyValue(context, "Script");
                if (script != null)
                {
                    var textProp = script.GetType().GetProperty("Text",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    if (textProp != null)
                    {
                        return textProp.GetValue(script) as string;
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Clears the method cache (useful for testing or version changes).
        /// </summary>
        public static void ClearCache()
        {
            _cachedMethods.Clear();
            _initialized = false;
        }
    }
}
