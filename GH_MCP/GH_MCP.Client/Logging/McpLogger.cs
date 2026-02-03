using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace GH_MCP.Client.Logging
{
    /// <summary>
    /// Log levels for MCP logging
    /// </summary>
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3
    }

    /// <summary>
    /// Global logging configuration and logger for MCP operations
    /// </summary>
    public static class McpLogger
    {
        private static readonly object _lock = new object();
        private static string? _logDirectory;
        private static string? _logFilePath;
        private static bool _enabled = false;
        private static LogLevel _minLevel = LogLevel.Info;
        private static bool _logToConsole = false;

        /// <summary>
        /// Gets whether logging is enabled
        /// </summary>
        public static bool IsEnabled => _enabled;

        /// <summary>
        /// Gets the current log file path
        /// </summary>
        public static string? LogFilePath => _logFilePath;

        /// <summary>
        /// Configures the logger with a log directory
        /// </summary>
        /// <param name="logDirectory">Directory for log files</param>
        /// <param name="logFileName">Optional log file name (default: mcp-client-{date}.log)</param>
        /// <param name="minLevel">Minimum log level to record</param>
        /// <param name="logToConsole">Also write logs to console/stderr</param>
        public static void Configure(
            string logDirectory,
            string? logFileName = null,
            LogLevel minLevel = LogLevel.Info,
            bool logToConsole = false)
        {
            lock (_lock)
            {
                _logDirectory = logDirectory;
                _minLevel = minLevel;
                _logToConsole = logToConsole;

                // Create directory if needed
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                // Generate log file name
                var fileName = logFileName ?? $"mcp-client-{DateTime.Now:yyyy-MM-dd}.log";
                _logFilePath = Path.Combine(logDirectory, fileName);
                _enabled = true;

                // Write header
                WriteRaw($"\n{'=',-80}\n");
                WriteRaw($"MCP Client Log Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
                WriteRaw($"Log File: {_logFilePath}\n");
                WriteRaw($"{'=',-80}\n\n");
            }
        }

        /// <summary>
        /// Disables logging
        /// </summary>
        public static void Disable()
        {
            lock (_lock)
            {
                _enabled = false;
            }
        }

        /// <summary>
        /// Logs a debug message
        /// </summary>
        public static void Debug(string message) => Log(LogLevel.Debug, message);

        /// <summary>
        /// Logs an info message
        /// </summary>
        public static void Info(string message) => Log(LogLevel.Info, message);

        /// <summary>
        /// Logs a warning message
        /// </summary>
        public static void Warning(string message) => Log(LogLevel.Warning, message);

        /// <summary>
        /// Logs an error message
        /// </summary>
        public static void Error(string message) => Log(LogLevel.Error, message);

        /// <summary>
        /// Logs an exception
        /// </summary>
        public static void Error(string message, Exception ex)
        {
            Log(LogLevel.Error, $"{message}\nException: {ex.GetType().Name}: {ex.Message}\nStack: {ex.StackTrace}");
        }

        /// <summary>
        /// Logs a command request
        /// </summary>
        public static void LogRequest(string commandType, object? parameters)
        {
            if (!_enabled || _minLevel > LogLevel.Info) return;

            var entry = new
            {
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                direction = "REQUEST",
                type = commandType,
                parameters = parameters
            };

            var json = JsonConvert.SerializeObject(entry, Formatting.Indented);
            WriteRaw($">>> REQUEST [{DateTime.Now:HH:mm:ss.fff}] {commandType}\n{json}\n\n");
        }

        /// <summary>
        /// Logs a command response
        /// </summary>
        public static void LogResponse(string commandType, object? response, TimeSpan elapsed)
        {
            if (!_enabled || _minLevel > LogLevel.Info) return;

            var entry = new
            {
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                direction = "RESPONSE",
                type = commandType,
                elapsed_ms = elapsed.TotalMilliseconds,
                response = response
            };

            var json = JsonConvert.SerializeObject(entry, Formatting.Indented);
            WriteRaw($"<<< RESPONSE [{DateTime.Now:HH:mm:ss.fff}] {commandType} ({elapsed.TotalMilliseconds:F1}ms)\n{json}\n\n");
        }

        /// <summary>
        /// Logs a message with specified level
        /// </summary>
        public static void Log(LogLevel level, string message)
        {
            if (!_enabled || level < _minLevel) return;

            var levelStr = level.ToString().ToUpper().PadRight(7);
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{levelStr}] {message}";

            WriteRaw(line + "\n");
        }

        /// <summary>
        /// Writes a test result entry
        /// </summary>
        public static void LogTestResult(string testName, bool passed, string? details = null)
        {
            if (!_enabled) return;

            var status = passed ? "PASS" : "FAIL";
            var entry = new
            {
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                type = "TEST_RESULT",
                test = testName,
                status = status,
                passed = passed,
                details = details
            };

            var json = JsonConvert.SerializeObject(entry, Formatting.Indented);
            WriteRaw($"=== TEST [{DateTime.Now:HH:mm:ss.fff}] {testName}: {status}\n{json}\n\n");
        }

        /// <summary>
        /// Writes a separator line for readability
        /// </summary>
        public static void LogSeparator(string? label = null)
        {
            if (!_enabled) return;

            if (string.IsNullOrEmpty(label))
            {
                WriteRaw($"{new string('-', 80)}\n");
            }
            else
            {
                WriteRaw($"\n{new string('=', 80)}\n");
                WriteRaw($"  {label}\n");
                WriteRaw($"{new string('=', 80)}\n\n");
            }
        }

        private static void WriteRaw(string text)
        {
            lock (_lock)
            {
                if (!_enabled || string.IsNullOrEmpty(_logFilePath)) return;

                try
                {
                    File.AppendAllText(_logFilePath, text, Encoding.UTF8);

                    if (_logToConsole)
                    {
                        Console.Error.Write(text);
                    }
                }
                catch
                {
                    // Silently fail logging to avoid disrupting the main application
                }
            }
        }
    }
}
