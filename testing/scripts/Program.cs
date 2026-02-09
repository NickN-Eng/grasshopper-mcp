using System;
using System.IO;
using GH_MCP.Utils;

namespace AnalyzeDlls
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== DLL Analysis Tool ===");
            Console.WriteLine();

            // Path to CustomPackages - use absolute path
            var customPackagesPath = @"c:\Users\nniem\source\repos\grasshopper-mcp\csharpscriptAttempt\CustomPackages";

            if (!Path.IsPathRooted(customPackagesPath))
            {
                customPackagesPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "..", "csharpscriptAttempt", "CustomPackages");
                customPackagesPath = Path.GetFullPath(customPackagesPath);
            }

            Console.WriteLine($"Analyzing DLLs in: {customPackagesPath}");
            Console.WriteLine();

            if (!Directory.Exists(customPackagesPath))
            {
                Console.WriteLine($"ERROR: Directory not found: {customPackagesPath}");
                return;
            }

            // Run the analysis
            try
            {
                string report = DllAnalyzer.AnalyzeCustomPackages(customPackagesPath);

                // Write to logs
                var logsPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "logs");
                Directory.CreateDirectory(logsPath);

                var outputPath = Path.Combine(logsPath, "dll-analysis-report.txt");
                File.WriteAllText(outputPath, report);

                Console.WriteLine("✅ Analysis complete!");
                Console.WriteLine($"📄 Report written to: {outputPath}");
                Console.WriteLine();
                Console.WriteLine("=== SUMMARY ===");

                // Print last 50 lines of report
                var lines = report.Split('\n');
                var startLine = Math.Max(0, lines.Length - 50);
                for (int i = startLine; i < lines.Length; i++)
                {
                    Console.WriteLine(lines[i]);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error during analysis: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
