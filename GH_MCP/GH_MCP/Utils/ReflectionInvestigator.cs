using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Grasshopper.Kernel;

namespace GH_MCP.Utils
{
    /// <summary>
    /// Tool for investigating C# script components via reflection to discover compilation methods.
    /// This is Option A: Reflection-only approach that doesn't require DLL references.
    /// </summary>
    public class ReflectionInvestigator
    {
        private readonly StringBuilder _log = new StringBuilder();

        /// <summary>
        /// Investigates a C# script component to find compilation-related methods.
        /// </summary>
        /// <param name="component">The C# script component to investigate</param>
        /// <returns>Investigation report as a string</returns>
        public string InvestigateComponent(GH_Component component)
        {
            _log.Clear();
            _log.AppendLine("=== C# Script Component Investigation ===");
            _log.AppendLine($"Component Type: {component.GetType().FullName}");
            _log.AppendLine($"Assembly: {component.GetType().Assembly.GetName().Name}");
            _log.AppendLine();

            // Step 1: Explore the component itself
            _log.AppendLine("--- Component Properties ---");
            ExploreProperties(component, component.GetType());

            // Step 2: Try to get Context property
            var context = GetPropertyValue(component, "Context");
            if (context != null)
            {
                _log.AppendLine("\n--- Context Object Found ---");
                _log.AppendLine($"Context Type: {context.GetType().FullName}");
                _log.AppendLine($"Assembly: {context.GetType().Assembly.GetName().Name}");
                _log.AppendLine();

                // Step 3: Explore Context properties
                _log.AppendLine("--- Context Properties ---");
                ExploreProperties(context, context.GetType());

                // Step 4: Find compilation-related methods
                _log.AppendLine("\n--- Potential Compilation Methods ---");
                FindCompilationMethods(context);

                // Step 5: Explore Script property if it exists
                var script = GetPropertyValue(context, "Script");
                if (script != null)
                {
                    _log.AppendLine("\n--- Script Object Found ---");
                    _log.AppendLine($"Script Type: {script.GetType().FullName}");
                    ExploreProperties(script, script.GetType());
                    FindCompilationMethods(script);
                }
            }

            return _log.ToString();
        }

        /// <summary>
        /// Explores all properties of an object and logs them.
        /// </summary>
        private void ExploreProperties(object obj, Type type)
        {
            var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (var prop in properties.OrderBy(p => p.Name))
            {
                try
                {
                    var value = prop.GetValue(obj);
                    var valueStr = value == null ? "null" :
                                  value is string ? $"\"{value}\"" :
                                  value.ToString();

                    _log.AppendLine($"  {GetAccessModifier(prop)} {prop.PropertyType.Name} {prop.Name} = {valueStr}");
                }
                catch (Exception ex)
                {
                    _log.AppendLine($"  {GetAccessModifier(prop)} {prop.PropertyType.Name} {prop.Name} [Error: {ex.Message}]");
                }
            }
        }

        /// <summary>
        /// Finds methods that might trigger compilation based on naming patterns.
        /// </summary>
        private void FindCompilationMethods(object obj)
        {
            var type = obj.GetType();
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            var compilationKeywords = new[] { "compile", "build", "execute", "refresh", "update", "save", "run", "process" };

            var candidates = methods.Where(m =>
                compilationKeywords.Any(keyword =>
                    m.Name.ToLower().Contains(keyword)))
                .OrderBy(m => m.Name)
                .ToList();

            if (candidates.Count == 0)
            {
                _log.AppendLine("  No compilation-related methods found.");
                return;
            }

            foreach (var method in candidates)
            {
                var parameters = method.GetParameters();
                var paramStr = parameters.Length == 0 ?
                    "()" :
                    $"({string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"))})";

                _log.AppendLine($"  {GetAccessModifier(method)} {method.ReturnType.Name} {method.Name}{paramStr}");
            }
        }

        /// <summary>
        /// Attempts to call various compilation methods discovered on a component.
        /// </summary>
        public CompilationTestResult TestCompilationMethods(GH_Component component)
        {
            var result = new CompilationTestResult();
            _log.Clear();
            _log.AppendLine("=== Testing Compilation Methods ===");

            var context = GetPropertyValue(component, "Context");
            if (context == null)
            {
                result.Success = false;
                result.ErrorMessage = "Could not access Context property";
                result.Log = _log.ToString();
                return result;
            }

            // Test 1: Try Menu_SaveScriptClicked (from previous attempt)
            TestMethod(context, "Menu_SaveScriptClicked", new object[] { null, EventArgs.Empty }, result);

            // Test 2: Try common compilation method names
            var methodsToTry = new[]
            {
                ("Compile", new object[0]),
                ("CompileScript", new object[0]),
                ("Build", new object[0]),
                ("Rebuild", new object[0]),
                ("Execute", new object[0]),
                ("Refresh", new object[0]),
                ("Update", new object[0]),
                ("SaveScript", new object[0])
            };

            foreach (var (methodName, args) in methodsToTry)
            {
                TestMethod(context, methodName, args, result);
            }

            result.Log = _log.ToString();
            return result;
        }

        /// <summary>
        /// Tests a specific method invocation and logs the result.
        /// </summary>
        private void TestMethod(object target, string methodName, object[] args, CompilationTestResult result)
        {
            try
            {
                var type = target.GetType();
                var method = type.GetMethod(methodName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (method == null)
                {
                    _log.AppendLine($"❌ Method '{methodName}' not found");
                    return;
                }

                _log.AppendLine($"🔍 Testing: {methodName}");

                var returnValue = method.Invoke(target, args);

                _log.AppendLine($"✅ Success! Method '{methodName}' executed without error");
                _log.AppendLine($"   Return value: {returnValue ?? "void"}");

                result.SuccessfulMethods.Add(new MethodInvocationResult
                {
                    MethodName = methodName,
                    Success = true,
                    ReturnValue = returnValue
                });
            }
            catch (TargetInvocationException ex)
            {
                _log.AppendLine($"⚠️  Method '{methodName}' threw exception: {ex.InnerException?.Message ?? ex.Message}");
                result.SuccessfulMethods.Add(new MethodInvocationResult
                {
                    MethodName = methodName,
                    Success = false,
                    ErrorMessage = ex.InnerException?.Message ?? ex.Message
                });
            }
            catch (Exception ex)
            {
                _log.AppendLine($"❌ Failed to invoke '{methodName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the value of a property via reflection.
        /// </summary>
        private object GetPropertyValue(object obj, string propertyName)
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
        /// Gets the access modifier string for a member.
        /// </summary>
        private string GetAccessModifier(MemberInfo member)
        {
            if (member is MethodInfo method)
            {
                if (method.IsPublic) return "public";
                if (method.IsFamily) return "protected";
                if (method.IsPrivate) return "private";
                return "internal";
            }

            if (member is PropertyInfo prop)
            {
                var getMethod = prop.GetGetMethod(true);
                var setMethod = prop.GetSetMethod(true);
                var mainMethod = getMethod ?? setMethod;

                if (mainMethod.IsPublic) return "public";
                if (mainMethod.IsFamily) return "protected";
                if (mainMethod.IsPrivate) return "private";
                return "internal";
            }

            return "unknown";
        }
    }

    /// <summary>
    /// Result of compilation method testing.
    /// </summary>
    public class CompilationTestResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public List<MethodInvocationResult> SuccessfulMethods { get; set; } = new List<MethodInvocationResult>();
        public string Log { get; set; }
    }

    /// <summary>
    /// Result of a single method invocation attempt.
    /// </summary>
    public class MethodInvocationResult
    {
        public string MethodName { get; set; }
        public bool Success { get; set; }
        public object ReturnValue { get; set; }
        public string ErrorMessage { get; set; }
    }
}
