using System;
using System.Collections.Generic;
using System.IO;
using Grasshopper.Kernel;

namespace GH_MCP.Utils
{
    /// <summary>
    /// Hybrid C# script compiler that uses both reflection (Option A) and direct references (Option B)
    /// with intelligent fallback logic. This is the recommended public API for script compilation.
    /// </summary>
    public static class CSharpScriptCompiler
    {
        private static readonly string LogPath = Path.Combine(
            Path.GetDirectoryName(typeof(CSharpScriptCompiler).Assembly.Location),
            "..", "..", "..", "..", "logs", "csharp-compilation.log");

        private static CompilationStrategy _lastSuccessfulStrategy = CompilationStrategy.Unknown;

        /// <summary>
        /// Compilation strategies available.
        /// </summary>
        public enum CompilationStrategy
        {
            Unknown,
            DirectReference,  // Option B
            Reflection        // Option A
        }

        /// <summary>
        /// Compiles a C# script component using the best available strategy.
        /// Tries direct reference first (if available), then falls back to reflection.
        /// </summary>
        /// <param name="component">The C# script component to compile</param>
        /// <param name="errors">Output list of compilation errors</param>
        /// <returns>True if compilation succeeded</returns>
        public static bool Compile(GH_Component component, out List<string> errors)
        {
            errors = new List<string>();
            var strategy = CompilationStrategy.Unknown;

            try
            {
                // Strategy 1: Try last successful strategy first for performance
                if (_lastSuccessfulStrategy != CompilationStrategy.Unknown)
                {
                    if (TryCompileWithStrategy(_lastSuccessfulStrategy, component, out errors, out strategy))
                    {
                        Log($"✅ Compilation succeeded using cached strategy: {strategy}");
                        return true;
                    }
                    else
                    {
                        Log($"⚠️  Cached strategy {_lastSuccessfulStrategy} failed, trying alternatives");
                    }
                }

                // Strategy 2: Try Option B (direct reference) first
                if (TryCompileWithStrategy(CompilationStrategy.DirectReference, component, out errors, out strategy))
                {
                    Log($"✅ Compilation succeeded using strategy: {strategy}");
                    _lastSuccessfulStrategy = strategy;
                    return true;
                }

                Log($"⚠️  Option B (direct reference) failed, falling back to reflection");
                errors.Clear(); // Clear errors from failed attempt

                // Strategy 3: Fallback to Option A (reflection)
                if (TryCompileWithStrategy(CompilationStrategy.Reflection, component, out errors, out strategy))
                {
                    Log($"✅ Compilation succeeded using strategy: {strategy}");
                    _lastSuccessfulStrategy = strategy;
                    return true;
                }

                Log($"❌ All compilation strategies failed");
                return false;
            }
            catch (Exception ex)
            {
                Log($"❌ Compilation exception: {ex.Message}");
                errors.Add($"Compilation exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Tries to compile using a specific strategy.
        /// </summary>
        private static bool TryCompileWithStrategy(
            CompilationStrategy requestedStrategy,
            GH_Component component,
            out List<string> errors,
            out CompilationStrategy usedStrategy)
        {
            errors = new List<string>();
            usedStrategy = requestedStrategy;

            try
            {
                switch (requestedStrategy)
                {
                    case CompilationStrategy.DirectReference:
                        return CSharpScriptCompiler_OptionB.TryCompile(component, out errors);

                    case CompilationStrategy.Reflection:
                        return CSharpScriptCompiler_OptionA.TryCompile(component, out errors);

                    default:
                        errors.Add("Unknown compilation strategy");
                        return false;
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Strategy {requestedStrategy} threw exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sets the script text on a C# component.
        /// </summary>
        /// <param name="component">The C# script component</param>
        /// <param name="scriptText">The new script text</param>
        /// <returns>True if the text was set successfully</returns>
        public static bool SetScriptText(GH_Component component, string scriptText)
        {
            try
            {
                // Try Option B first
                if (CSharpScriptCompiler_OptionB.SetScriptText(component, scriptText))
                {
                    Log($"✅ Script text set using Option B");
                    return true;
                }

                // Fallback to Option A
                if (CSharpScriptCompiler_OptionA.SetScriptText(component, scriptText))
                {
                    Log($"✅ Script text set using Option A (reflection)");
                    return true;
                }

                Log($"❌ Failed to set script text with all strategies");
                return false;
            }
            catch (Exception ex)
            {
                Log($"❌ SetScriptText exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the script text from a C# component.
        /// </summary>
        /// <param name="component">The C# script component</param>
        /// <returns>The script text, or null if unable to retrieve</returns>
        public static string GetScriptText(GH_Component component)
        {
            try
            {
                // Try Option B first
                var text = CSharpScriptCompiler_OptionB.GetScriptText(component);
                if (text != null)
                {
                    Log($"✅ Script text retrieved using Option B");
                    return text;
                }

                // Fallback to Option A
                text = CSharpScriptCompiler_OptionA.GetScriptText(component);
                if (text != null)
                {
                    Log($"✅ Script text retrieved using Option A (reflection)");
                    return text;
                }

                Log($"❌ Failed to get script text with all strategies");
                return null;
            }
            catch (Exception ex)
            {
                Log($"❌ GetScriptText exception: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Sets script text and compiles in one operation.
        /// </summary>
        /// <param name="component">The C# script component</param>
        /// <param name="scriptText">The new script text</param>
        /// <param name="errors">Output compilation errors if any</param>
        /// <returns>True if both set and compile succeeded</returns>
        public static bool SetAndCompile(GH_Component component, string scriptText, out List<string> errors)
        {
            errors = new List<string>();

            // Step 1: Set the script text
            if (!SetScriptText(component, scriptText))
            {
                errors.Add("Failed to set script text");
                return false;
            }

            // Step 2: Compile the script
            return Compile(component, out errors);
        }

        /// <summary>
        /// Runs the investigation tool to discover compilation methods.
        /// Useful for debugging and understanding the component structure.
        /// </summary>
        /// <param name="component">The C# script component to investigate</param>
        /// <returns>Investigation report as a string</returns>
        public static string Investigate(GH_Component component)
        {
            try
            {
                var investigator = new ReflectionInvestigator();
                var report = investigator.InvestigateComponent(component);
                Log($"Investigation completed for component: {component.Name}");
                return report;
            }
            catch (Exception ex)
            {
                Log($"❌ Investigation failed: {ex.Message}");
                return $"Investigation failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Tests various compilation methods to find what works.
        /// Returns a detailed test report.
        /// </summary>
        /// <param name="component">The C# script component to test</param>
        /// <returns>Test results</returns>
        public static CompilationTestResult TestCompilationMethods(GH_Component component)
        {
            try
            {
                var investigator = new ReflectionInvestigator();
                var result = investigator.TestCompilationMethods(component);
                Log($"Compilation method testing completed");
                return result;
            }
            catch (Exception ex)
            {
                Log($"❌ Testing failed: {ex.Message}");
                return new CompilationTestResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Log = ex.ToString()
                };
            }
        }

        /// <summary>
        /// Clears all caches (useful for testing or after version changes).
        /// </summary>
        public static void ClearCache()
        {
            CSharpScriptCompiler_OptionA.ClearCache();
            _lastSuccessfulStrategy = CompilationStrategy.Unknown;
            Log("🔄 Cache cleared");
        }

        /// <summary>
        /// Logs a message to the compilation log file.
        /// </summary>
        private static void Log(string message)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logMessage = $"[{timestamp}] {message}{Environment.NewLine}";

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
        /// Gets the current log file path.
        /// </summary>
        public static string GetLogPath() => LogPath;
    }
}
