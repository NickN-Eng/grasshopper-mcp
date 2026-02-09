using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace GH_MCP.Utils
{
    /// <summary>
    /// Utility for analyzing DLL files to discover types, methods, and signatures
    /// relevant to C# script compilation. This helps with implementing Option B.
    /// </summary>
    public class DllAnalyzer
    {
        /// <summary>
        /// Analyzes a DLL file and generates a report of relevant types and methods.
        /// </summary>
        /// <param name="dllPath">Path to the DLL file</param>
        /// <param name="keywords">Keywords to filter relevant types/methods (e.g., "Script", "Compile")</param>
        /// <returns>Analysis report as a string</returns>
        public static string AnalyzeDll(string dllPath, params string[] keywords)
        {
            var report = new StringBuilder();
            report.AppendLine("=".PadRight(80, '='));
            report.AppendLine($"DLL ANALYSIS REPORT");
            report.AppendLine($"File: {Path.GetFileName(dllPath)}");
            report.AppendLine($"Path: {dllPath}");
            report.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine("=".PadRight(80, '='));
            report.AppendLine();

            try
            {
                // Load the assembly
                var assembly = Assembly.LoadFrom(dllPath);
                report.AppendLine($"Assembly Name: {assembly.FullName}");
                report.AppendLine($"Runtime Version: {assembly.ImageRuntimeVersion}");
                report.AppendLine();

                // Get all types
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray();
                    report.AppendLine($"⚠️  Warning: Some types could not be loaded");
                    foreach (var loaderEx in ex.LoaderExceptions.Where(e => e != null).Take(5))
                    {
                        report.AppendLine($"   - {loaderEx.Message}");
                    }
                    report.AppendLine();
                }

                // Filter types by keywords if provided
                var relevantTypes = keywords.Length == 0
                    ? types
                    : types.Where(t => keywords.Any(kw =>
                        ContainsIgnoreCase(t.Name, kw) ||
                        (t.Namespace != null && ContainsIgnoreCase(t.Namespace, kw))))
                      .ToArray();

                report.AppendLine($"Total Types: {types.Length}");
                report.AppendLine($"Relevant Types (filtered): {relevantTypes.Length}");
                report.AppendLine();

                // Analyze each relevant type
                foreach (var type in relevantTypes.OrderBy(t => t.FullName))
                {
                    AnalyzeType(type, report, keywords);
                }

                // Summary of interesting findings
                report.AppendLine();
                report.AppendLine("=".PadRight(80, '='));
                report.AppendLine("SUMMARY OF INTERESTING FINDINGS");
                report.AppendLine("=".PadRight(80, '='));
                report.AppendLine();

                GenerateSummary(relevantTypes, report, keywords);
            }
            catch (Exception ex)
            {
                report.AppendLine($"❌ Error analyzing DLL: {ex.Message}");
                report.AppendLine(ex.StackTrace);
            }

            return report.ToString();
        }

        /// <summary>
        /// Analyzes a single type and appends details to the report.
        /// </summary>
        private static void AnalyzeType(Type type, StringBuilder report, string[] keywords)
        {
            report.AppendLine("-".PadRight(80, '-'));
            report.AppendLine($"Type: {type.FullName}");
            report.AppendLine($"Namespace: {type.Namespace}");
            report.AppendLine($"Assembly: {type.Assembly.GetName().Name}");

            // Base type
            if (type.BaseType != null)
            {
                report.AppendLine($"Base Type: {type.BaseType.FullName}");
            }

            // Interfaces
            var interfaces = type.GetInterfaces();
            if (interfaces.Any())
            {
                report.AppendLine($"Interfaces: {string.Join(", ", interfaces.Select(i => i.Name))}");
            }

            report.AppendLine();

            // Properties
            var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var relevantProps = keywords.Length == 0
                ? properties
                : properties.Where(p => keywords.Any(kw =>
                    ContainsIgnoreCase(p.Name, kw)))
                  .ToArray();

            if (relevantProps.Any())
            {
                report.AppendLine("  Properties:");
                foreach (var prop in relevantProps.OrderBy(p => p.Name))
                {
                    var access = GetAccessModifier(prop);
                    var getter = prop.GetGetMethod(true) != null ? "get;" : "";
                    var setter = prop.GetSetMethod(true) != null ? "set;" : "";
                    report.AppendLine($"    {access} {prop.PropertyType.Name} {prop.Name} {{ {getter} {setter} }}");
                }
                report.AppendLine();
            }

            // Methods
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            var relevantMethods = keywords.Length == 0
                ? methods.Where(m => !m.IsSpecialName) // Exclude property getters/setters
                : methods.Where(m => !m.IsSpecialName && keywords.Any(kw =>
                    ContainsIgnoreCase(m.Name, kw)))
                  .ToArray();

            if (relevantMethods.Any())
            {
                report.AppendLine("  Methods:");
                foreach (var method in relevantMethods.OrderBy(m => m.Name))
                {
                    var access = GetAccessModifier(method);
                    var parameters = method.GetParameters();
                    var paramStr = parameters.Length == 0
                        ? "()"
                        : $"({string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"))})";

                    report.AppendLine($"    {access} {method.ReturnType.Name} {method.Name}{paramStr}");
                }
                report.AppendLine();
            }

            report.AppendLine();
        }

        /// <summary>
        /// Generates a summary of interesting findings.
        /// </summary>
        private static void GenerateSummary(Type[] types, StringBuilder report, string[] keywords)
        {
            // Find types with "Compile" methods
            var typesWithCompile = types
                .Where(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Any(m => ContainsIgnoreCase(m.Name, "Compile")))
                .ToList();

            if (typesWithCompile.Any())
            {
                report.AppendLine("📝 Types with 'Compile' methods:");
                foreach (var type in typesWithCompile)
                {
                    var compileMethods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Where(m => ContainsIgnoreCase(m.Name, "Compile"));
                    report.AppendLine($"   - {type.FullName}");
                    foreach (var method in compileMethods)
                    {
                        var paramStr = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name));
                        report.AppendLine($"      → {method.Name}({paramStr})");
                    }
                }
                report.AppendLine();
            }

            // Find types with "Context" in name
            var contextTypes = types
                .Where(t => ContainsIgnoreCase(t.Name, "Context"))
                .ToList();

            if (contextTypes.Any())
            {
                report.AppendLine("📝 Types with 'Context' in name:");
                foreach (var type in contextTypes)
                {
                    report.AppendLine($"   - {type.FullName}");
                }
                report.AppendLine();
            }

            // Find types inheriting from GH_Component
            var ghComponents = types
                .Where(t => t.BaseType != null && (
                    t.BaseType.Name.Contains("GH_Component") ||
                    t.BaseType.FullName?.Contains("Grasshopper.Kernel.GH_Component") == true))
                .ToList();

            if (ghComponents.Any())
            {
                report.AppendLine("📝 Types inheriting from GH_Component:");
                foreach (var type in ghComponents)
                {
                    report.AppendLine($"   - {type.FullName}");
                }
                report.AppendLine();
            }
        }

        /// <summary>
        /// Gets the access modifier string for a member.
        /// </summary>
        private static string GetAccessModifier(MemberInfo member)
        {
            if (member is MethodInfo method)
            {
                if (method.IsPublic) return "public";
                if (method.IsFamily) return "protected";
                if (method.IsPrivate) return "private";
                if (method.IsAssembly) return "internal";
                return "private";
            }

            if (member is PropertyInfo prop)
            {
                var getMethod = prop.GetGetMethod(true);
                var setMethod = prop.GetSetMethod(true);
                var mainMethod = getMethod ?? setMethod;

                if (mainMethod == null) return "unknown";
                if (mainMethod.IsPublic) return "public";
                if (mainMethod.IsFamily) return "protected";
                if (mainMethod.IsPrivate) return "private";
                if (mainMethod.IsAssembly) return "internal";
                return "private";
            }

            return "unknown";
        }

        /// <summary>
        /// Analyzes all relevant DLLs in the CustomPackages folder.
        /// </summary>
        /// <param name="customPackagesPath">Path to CustomPackages folder</param>
        /// <returns>Combined analysis report</returns>
        public static string AnalyzeCustomPackages(string customPackagesPath)
        {
            var report = new StringBuilder();
            report.AppendLine("=".PadRight(80, '='));
            report.AppendLine("CUSTOM PACKAGES ANALYSIS");
            report.AppendLine("=".PadRight(80, '='));
            report.AppendLine();

            if (!Directory.Exists(customPackagesPath))
            {
                report.AppendLine($"❌ Directory not found: {customPackagesPath}");
                return report.ToString();
            }

            var keywords = new[] { "Script", "Compile", "CSharp", "Context", "Code", "Build" };

            var dlls = new[]
            {
                "RhinoCodePluginGH.dll",
                "RhinoCodePlatform.GH.dll",
                "RhinoCodePlatform.GH1.dll",
                "RhinoCodePlatform.GH.Context.dll",
                "Rhino.Runtime.Code.dll",
                "Rhino.Runtime.Code.Languages.Roslyn.dll"
            };

            foreach (var dllName in dlls)
            {
                var dllPath = Path.Combine(customPackagesPath, dllName);
                if (!File.Exists(dllPath))
                {
                    report.AppendLine($"⚠️  DLL not found: {dllName}");
                    report.AppendLine();
                    continue;
                }

                report.AppendLine();
                report.AppendLine($"Analyzing: {dllName}");
                report.AppendLine();

                try
                {
                    var dllReport = AnalyzeDll(dllPath, keywords);
                    report.AppendLine(dllReport);
                }
                catch (Exception ex)
                {
                    report.AppendLine($"❌ Error analyzing {dllName}: {ex.Message}");
                }

                report.AppendLine();
                report.AppendLine("=".PadRight(80, '='));
                report.AppendLine();
            }

            return report.ToString();
        }

        /// <summary>
        /// Helper method for case-insensitive contains check (compatible with .NET Framework 4.8).
        /// </summary>
        private static bool ContainsIgnoreCase(string source, string value)
        {
            return source?.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
