/*
 * MCP Test Runner Script
 *
 * This script runs a comprehensive test suite against the GH_MCP plugin.
 * Results are logged to logs/test-results-{timestamp}.log
 *
 * Usage:
 *   dotnet script McpTestRunner.cs
 *
 * Or compile and run:
 *   dotnet build GH_MCP/GH_MCP.Client
 *   Copy this to a console project and run
 *
 * For AI-driven testing:
 *   1. AI runs this script
 *   2. AI reads the log file to see results
 *   3. AI reads state snapshot JSON for verification
 *   4. AI iterates based on failures
 */

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GH_MCP.Client;
using GH_MCP.Client.Logging;
using GH_MCP.Client.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class McpTestRunner
{
    private readonly GrasshopperClient _client;
    private readonly string _logFile;
    private int _passed = 0;
    private int _failed = 0;
    private readonly List<TestResult> _results = new();

    public McpTestRunner(string logDirectory)
    {
        _client = new GrasshopperClient();
        _logFile = Path.Combine(logDirectory, $"test-results-{DateTime.Now:yyyyMMdd-HHmmss}.log");

        // Configure client logging
        McpLogger.Configure(logDirectory, $"test-client-{DateTime.Now:yyyyMMdd-HHmmss}.log", LogLevel.Debug);
    }

    public async Task RunAllTests()
    {
        Log("=" + new string('=', 79));
        Log("MCP TEST SUITE STARTED");
        Log($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        Log("=" + new string('=', 79));
        Log("");

        // Connection test
        await TestConnection();

        // Document tests
        await TestClearDocument();

        // Component tests
        await TestAddComponents();
        await TestGetAllComponents();
        await TestGetComponentInfo();

        // Connection tests
        await TestConnectComponents();
        await TestGetConnections();

        // Search tests
        await TestSearchComponents();
        await TestGetComponentParameters();

        // Validation tests
        await TestValidateConnection();

        // Report
        Log("");
        Log("=" + new string('=', 79));
        Log("TEST SUMMARY");
        Log($"Passed: {_passed}");
        Log($"Failed: {_failed}");
        Log($"Total:  {_passed + _failed}");
        Log("=" + new string('=', 79));

        // Write JSON results for AI parsing
        var jsonResultsFile = _logFile.Replace(".log", ".json");
        var jsonResults = new
        {
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            summary = new { passed = _passed, failed = _failed, total = _passed + _failed },
            results = _results
        };
        File.WriteAllText(jsonResultsFile, JsonConvert.SerializeObject(jsonResults, Formatting.Indented));
        Log($"JSON results written to: {jsonResultsFile}");
    }

    #region Test Methods

    private async Task TestConnection()
    {
        var testName = "Connection Test";
        try
        {
            var connected = await _client.TestConnectionAsync();
            RecordResult(testName, connected, connected ? "Connected successfully" : "Connection failed");
        }
        catch (Exception ex)
        {
            RecordResult(testName, false, $"Exception: {ex.Message}");
        }
    }

    private async Task TestClearDocument()
    {
        var testName = "Clear Document";
        try
        {
            var response = await _client.ClearDocumentAsync();
            RecordResult(testName, response.Success, response.Success ? "Document cleared" : response.Error);
        }
        catch (Exception ex)
        {
            RecordResult(testName, false, $"Exception: {ex.Message}");
        }
    }

    private async Task TestAddComponents()
    {
        // Test adding various component types
        var components = new[]
        {
            ("Number Slider", 100f, 100f),
            ("Number Slider", 100f, 200f),
            ("Addition", 300f, 150f),
            ("Panel", 500f, 150f),
            ("XY Plane", 100f, 300f),
            ("Circle", 300f, 300f)
        };

        foreach (var (type, x, y) in components)
        {
            var testName = $"Add Component: {type}";
            try
            {
                var response = await _client.AddComponentAsync(type, x, y);
                var componentId = response.GetProperty<string>("componentId");
                RecordResult(testName, response.Success,
                    response.Success ? $"Added with ID: {componentId}" : response.Error);
            }
            catch (Exception ex)
            {
                RecordResult(testName, false, $"Exception: {ex.Message}");
            }
        }
    }

    private async Task TestGetAllComponents()
    {
        var testName = "Get All Components";
        try
        {
            var response = await _client.GetAllComponentsAsync();
            var components = response.GetResultAs<JArray>();
            var count = components?.Count ?? 0;
            RecordResult(testName, response.Success && count > 0,
                $"Found {count} components");
        }
        catch (Exception ex)
        {
            RecordResult(testName, false, $"Exception: {ex.Message}");
        }
    }

    private async Task TestGetComponentInfo()
    {
        var testName = "Get Component Info";
        try
        {
            // First get list of components
            var listResponse = await _client.GetAllComponentsAsync();
            var components = listResponse.GetResultAs<JArray>();

            if (components == null || components.Count == 0)
            {
                RecordResult(testName, false, "No components found to test");
                return;
            }

            var firstId = components[0]["id"]?.ToString();
            if (string.IsNullOrEmpty(firstId))
            {
                RecordResult(testName, false, "Component ID is empty");
                return;
            }

            var response = await _client.GetComponentInfoAsync(firstId);
            RecordResult(testName, response.Success,
                response.Success ? $"Got info for {firstId}" : response.Error);
        }
        catch (Exception ex)
        {
            RecordResult(testName, false, $"Exception: {ex.Message}");
        }
    }

    private async Task TestConnectComponents()
    {
        var testName = "Connect Components";
        try
        {
            // Get components and find slider + addition
            var listResponse = await _client.GetAllComponentsAsync();
            var components = listResponse.GetResultAs<JArray>();

            string? sliderId = null;
            string? additionId = null;

            if (components != null)
            {
                foreach (var comp in components)
                {
                    var type = comp["type"]?.ToString();
                    var id = comp["id"]?.ToString();

                    if (type == "Number Slider" && sliderId == null)
                        sliderId = id;
                    else if (type == "Addition" && additionId == null)
                        additionId = id;
                }
            }

            if (string.IsNullOrEmpty(sliderId) || string.IsNullOrEmpty(additionId))
            {
                RecordResult(testName, false, "Could not find slider and addition components");
                return;
            }

            var response = await _client.ConnectComponentsAsync(sliderId, additionId, null, "A");
            RecordResult(testName, response.Success,
                response.Success ? $"Connected {sliderId} to {additionId}" : response.Error);
        }
        catch (Exception ex)
        {
            RecordResult(testName, false, $"Exception: {ex.Message}");
        }
    }

    private async Task TestGetConnections()
    {
        var testName = "Get Connections";
        try
        {
            var response = await _client.GetConnectionsAsync();
            var connections = response.GetResultAs<JArray>();
            var count = connections?.Count ?? 0;
            RecordResult(testName, response.Success,
                $"Found {count} connections");
        }
        catch (Exception ex)
        {
            RecordResult(testName, false, $"Exception: {ex.Message}");
        }
    }

    private async Task TestSearchComponents()
    {
        var testName = "Search Components";
        try
        {
            var response = await _client.SearchComponentsAsync("slider");
            RecordResult(testName, response.Success,
                response.Success ? "Search completed" : response.Error);
        }
        catch (Exception ex)
        {
            RecordResult(testName, false, $"Exception: {ex.Message}");
        }
    }

    private async Task TestGetComponentParameters()
    {
        var testName = "Get Component Parameters";
        try
        {
            var response = await _client.GetComponentParametersAsync("Addition");
            RecordResult(testName, response.Success,
                response.Success ? "Got parameters for Addition" : response.Error);
        }
        catch (Exception ex)
        {
            RecordResult(testName, false, $"Exception: {ex.Message}");
        }
    }

    private async Task TestValidateConnection()
    {
        var testName = "Validate Connection";
        try
        {
            var listResponse = await _client.GetAllComponentsAsync();
            var components = listResponse.GetResultAs<JArray>();

            if (components == null || components.Count < 2)
            {
                RecordResult(testName, false, "Not enough components to validate");
                return;
            }

            var id1 = components[0]["id"]?.ToString();
            var id2 = components[1]["id"]?.ToString();

            if (string.IsNullOrEmpty(id1) || string.IsNullOrEmpty(id2))
            {
                RecordResult(testName, false, "Component IDs are empty");
                return;
            }

            var response = await _client.ValidateConnectionAsync(id1, id2);
            RecordResult(testName, response.Success,
                response.Success ? "Validation completed" : response.Error);
        }
        catch (Exception ex)
        {
            RecordResult(testName, false, $"Exception: {ex.Message}");
        }
    }

    #endregion

    #region Helpers

    private void RecordResult(string testName, bool passed, string? details = null)
    {
        if (passed) _passed++; else _failed++;

        var status = passed ? "PASS" : "FAIL";
        var result = new TestResult
        {
            Name = testName,
            Passed = passed,
            Details = details ?? ""
        };
        _results.Add(result);

        McpLogger.LogTestResult(testName, passed, details);
        Log($"[{status}] {testName}: {details}");
    }

    private void Log(string message)
    {
        Console.WriteLine(message);
        File.AppendAllText(_logFile, message + Environment.NewLine);
    }

    #endregion

    private class TestResult
    {
        public string Name { get; set; } = "";
        public bool Passed { get; set; }
        public string Details { get; set; } = "";
    }
}

// Entry point
public static class Program
{
    public static async Task Main(string[] args)
    {
        var logDir = args.Length > 0 ? args[0] : FindLogsDirectory() ?? ".";

        Console.WriteLine($"MCP Test Runner");
        Console.WriteLine($"Log directory: {logDir}");
        Console.WriteLine();

        var runner = new McpTestRunner(logDir);
        await runner.RunAllTests();
    }

    private static string? FindLogsDirectory()
    {
        var current = AppContext.BaseDirectory;
        for (int i = 0; i < 6; i++)
        {
            var logsPath = Path.Combine(current, "logs");
            if (Directory.Exists(logsPath))
                return logsPath;

            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }
        return null;
    }
}
