using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GH_MCP.Client.Logging;
using GH_MCP.Client.Models;
using Newtonsoft.Json;

namespace GH_MCP.Client
{
    /// <summary>
    /// TCP client for communicating with the Grasshopper MCP plugin
    /// </summary>
    public class GrasshopperClient : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private bool _disposed;

        public string Host => _host;
        public int Port => _port;

        /// <summary>
        /// Creates a new GrasshopperClient with default settings (localhost:8080)
        /// </summary>
        public GrasshopperClient() : this("localhost", 8080) { }

        /// <summary>
        /// Creates a new GrasshopperClient with specified host and port
        /// </summary>
        public GrasshopperClient(string host, int port)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _port = port;
        }

        /// <summary>
        /// Sends a command to Grasshopper and returns the response
        /// </summary>
        public async Task<GrasshopperResponse> SendCommandAsync(GrasshopperCommand command, CancellationToken cancellationToken = default)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            var stopwatch = Stopwatch.StartNew();
            McpLogger.LogRequest(command.Type, command.Parameters);

            try
            {
                using (var client = new TcpClient())
                {
                    // Connect with timeout
                    McpLogger.Debug($"Connecting to {_host}:{_port}...");
                    var connectTask = client.ConnectAsync(_host, _port);
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);

                    var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                    if (completedTask == timeoutTask)
                    {
                        var errorResponse = GrasshopperResponse.CreateError("Connection timeout");
                        McpLogger.LogResponse(command.Type, errorResponse, stopwatch.Elapsed);
                        return errorResponse;
                    }

                    await connectTask; // Ensure any exceptions are thrown
                    McpLogger.Debug("Connected successfully");

                    using (var stream = client.GetStream())
                    {
                        // Send command
                        var jsonLine = JsonConvert.SerializeObject(command) + "\n";
                        var bytes = Encoding.UTF8.GetBytes(jsonLine);
                        await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
                        McpLogger.Debug($"Sent {bytes.Length} bytes");

                        // Read response (until newline)
                        var responseBuilder = new StringBuilder();
                        var buffer = new byte[4096];

                        while (true)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var readTask = stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                            var readTimeoutTask = Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

                            var completed = await Task.WhenAny(readTask, readTimeoutTask);
                            if (completed == readTimeoutTask)
                            {
                                var errorResponse = GrasshopperResponse.CreateError("Read timeout");
                                McpLogger.LogResponse(command.Type, errorResponse, stopwatch.Elapsed);
                                return errorResponse;
                            }

                            var bytesRead = await readTask;
                            if (bytesRead == 0) break;

                            var text = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                            responseBuilder.Append(text);

                            if (responseBuilder.ToString().Contains("\n"))
                                break;
                        }

                        // Parse response (handle BOM if present)
                        var responseStr = responseBuilder.ToString().Trim();
                        if (responseStr.StartsWith("\uFEFF"))
                            responseStr = responseStr.Substring(1);

                        McpLogger.Debug($"Received {responseStr.Length} chars");

                        var response = GrasshopperResponse.FromJson(responseStr);
                        var finalResponse = response ?? GrasshopperResponse.CreateError("Failed to parse response");

                        stopwatch.Stop();
                        McpLogger.LogResponse(command.Type, finalResponse, stopwatch.Elapsed);

                        return finalResponse;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                McpLogger.Warning($"Command {command.Type} was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                var errorResponse = GrasshopperResponse.CreateError($"Communication error: {ex.Message}");
                McpLogger.Error($"Command {command.Type} failed", ex);
                McpLogger.LogResponse(command.Type, errorResponse, stopwatch.Elapsed);
                return errorResponse;
            }
        }

        /// <summary>
        /// Sends a command synchronously (blocking)
        /// </summary>
        public GrasshopperResponse SendCommand(GrasshopperCommand command)
        {
            return SendCommandAsync(command).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Tests connectivity to the Grasshopper plugin
        /// </summary>
        public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await SendCommandAsync(GrasshopperCommand.GetDocumentInfo(), cancellationToken);
                return response.Success;
            }
            catch
            {
                return false;
            }
        }

        #region Convenience Methods

        /// <summary>
        /// Adds a component to the canvas
        /// </summary>
        public Task<GrasshopperResponse> AddComponentAsync(
            string componentType,
            float x,
            float y,
            CancellationToken cancellationToken = default)
        {
            var normalizedType = ComponentNormalizer.Normalize(componentType);
            return SendCommandAsync(GrasshopperCommand.AddComponent(normalizedType, x, y), cancellationToken);
        }

        /// <summary>
        /// Connects two components
        /// </summary>
        public Task<GrasshopperResponse> ConnectComponentsAsync(
            string sourceId,
            string targetId,
            string? sourceParam = null,
            string? targetParam = null,
            int? sourceParamIndex = null,
            int? targetParamIndex = null,
            CancellationToken cancellationToken = default)
        {
            return SendCommandAsync(
                GrasshopperCommand.ConnectComponents(sourceId, targetId, sourceParam, targetParam, sourceParamIndex, targetParamIndex),
                cancellationToken);
        }

        /// <summary>
        /// Gets information about a specific component
        /// </summary>
        public Task<GrasshopperResponse> GetComponentInfoAsync(string componentId, CancellationToken cancellationToken = default)
        {
            return SendCommandAsync(GrasshopperCommand.GetComponentInfo(componentId), cancellationToken);
        }

        /// <summary>
        /// Gets all components in the document
        /// </summary>
        public Task<GrasshopperResponse> GetAllComponentsAsync(CancellationToken cancellationToken = default)
        {
            return SendCommandAsync(GrasshopperCommand.GetAllComponents(), cancellationToken);
        }

        /// <summary>
        /// Gets all connections in the document
        /// </summary>
        public Task<GrasshopperResponse> GetConnectionsAsync(CancellationToken cancellationToken = default)
        {
            return SendCommandAsync(GrasshopperCommand.GetConnections(), cancellationToken);
        }

        /// <summary>
        /// Gets document information
        /// </summary>
        public Task<GrasshopperResponse> GetDocumentInfoAsync(CancellationToken cancellationToken = default)
        {
            return SendCommandAsync(GrasshopperCommand.GetDocumentInfo(), cancellationToken);
        }

        /// <summary>
        /// Clears the document
        /// </summary>
        public Task<GrasshopperResponse> ClearDocumentAsync(CancellationToken cancellationToken = default)
        {
            return SendCommandAsync(GrasshopperCommand.ClearDocument(), cancellationToken);
        }

        /// <summary>
        /// Saves the document
        /// </summary>
        public Task<GrasshopperResponse> SaveDocumentAsync(string path, CancellationToken cancellationToken = default)
        {
            return SendCommandAsync(GrasshopperCommand.SaveDocument(path), cancellationToken);
        }

        /// <summary>
        /// Loads a document
        /// </summary>
        public Task<GrasshopperResponse> LoadDocumentAsync(string path, CancellationToken cancellationToken = default)
        {
            return SendCommandAsync(GrasshopperCommand.LoadDocument(path), cancellationToken);
        }

        /// <summary>
        /// Searches for components
        /// </summary>
        public Task<GrasshopperResponse> SearchComponentsAsync(string query, CancellationToken cancellationToken = default)
        {
            return SendCommandAsync(GrasshopperCommand.SearchComponents(query), cancellationToken);
        }

        /// <summary>
        /// Gets parameters for a component type
        /// </summary>
        public Task<GrasshopperResponse> GetComponentParametersAsync(string componentType, CancellationToken cancellationToken = default)
        {
            return SendCommandAsync(GrasshopperCommand.GetComponentParameters(componentType), cancellationToken);
        }

        /// <summary>
        /// Validates a potential connection
        /// </summary>
        public Task<GrasshopperResponse> ValidateConnectionAsync(
            string sourceId,
            string targetId,
            string? sourceParam = null,
            string? targetParam = null,
            CancellationToken cancellationToken = default)
        {
            return SendCommandAsync(GrasshopperCommand.ValidateConnection(sourceId, targetId, sourceParam, targetParam), cancellationToken);
        }

        /// <summary>
        /// Creates a pattern from description
        /// </summary>
        public Task<GrasshopperResponse> CreatePatternAsync(string description, CancellationToken cancellationToken = default)
        {
            return SendCommandAsync(GrasshopperCommand.CreatePattern(description), cancellationToken);
        }

        /// <summary>
        /// Gets available patterns matching a query
        /// </summary>
        public Task<GrasshopperResponse> GetAvailablePatternsAsync(string query, CancellationToken cancellationToken = default)
        {
            return SendCommandAsync(GrasshopperCommand.GetAvailablePatterns(query), cancellationToken);
        }

        #endregion

        #region Verification Methods (for AI Testing)

        /// <summary>
        /// Exports full document state as JSON snapshot
        /// </summary>
        public Task<GrasshopperResponse> ExportDocumentStateAsync(CancellationToken cancellationToken = default)
        {
            return SendCommandAsync(GrasshopperCommand.ExportDocumentState(), cancellationToken);
        }

        /// <summary>
        /// Asserts that a component with the given ID exists
        /// </summary>
        public Task<GrasshopperResponse> AssertComponentExistsAsync(string componentId, CancellationToken cancellationToken = default)
        {
            return SendCommandAsync(GrasshopperCommand.AssertComponentExists(componentId), cancellationToken);
        }

        /// <summary>
        /// Asserts that a connection exists between two components
        /// </summary>
        public Task<GrasshopperResponse> AssertConnectionExistsAsync(
            string sourceId,
            string targetId,
            string? sourceParam = null,
            string? targetParam = null,
            CancellationToken cancellationToken = default)
        {
            return SendCommandAsync(GrasshopperCommand.AssertConnectionExists(sourceId, targetId, sourceParam, targetParam), cancellationToken);
        }

        /// <summary>
        /// Asserts that the document has a specific number of components
        /// </summary>
        public Task<GrasshopperResponse> AssertComponentCountAsync(int expectedCount, CancellationToken cancellationToken = default)
        {
            return SendCommandAsync(GrasshopperCommand.AssertComponentCount(expectedCount), cancellationToken);
        }

        /// <summary>
        /// Gets a hash of the document state for quick comparison
        /// </summary>
        public Task<GrasshopperResponse> GetDocumentHashAsync(CancellationToken cancellationToken = default)
        {
            return SendCommandAsync(GrasshopperCommand.GetDocumentHash(), cancellationToken);
        }

        #endregion

        #region Script Component Methods

        /// <summary>
        /// Gets all C# script components in the active document
        /// </summary>
        public Task<GrasshopperResponse> GetScriptComponentsAsync(CancellationToken cancellationToken = default)
        {
            return SendCommandAsync(GrasshopperCommand.GetScriptComponents(), cancellationToken);
        }

        /// <summary>
        /// Gets the source code from a script component
        /// </summary>
        public Task<GrasshopperResponse> GetScriptCodeAsync(string componentId, CancellationToken cancellationToken = default)
        {
            return SendCommandAsync(GrasshopperCommand.GetScriptCode(componentId), cancellationToken);
        }

        /// <summary>
        /// Sets new source code on a script component and optionally compiles it
        /// </summary>
        public Task<GrasshopperResponse> SetScriptCodeAsync(
            string componentId,
            string code,
            bool compile = false,
            CancellationToken cancellationToken = default)
        {
            return SendCommandAsync(GrasshopperCommand.SetScriptCode(componentId, code, compile), cancellationToken);
        }

        /// <summary>
        /// Triggers compilation of a script component
        /// </summary>
        public Task<GrasshopperResponse> CompileScriptAsync(string componentId, CancellationToken cancellationToken = default)
        {
            return SendCommandAsync(GrasshopperCommand.CompileScript(componentId), cancellationToken);
        }

        /// <summary>
        /// Comprehensive testing and investigation of script compilation
        /// </summary>
        public Task<GrasshopperResponse> TestScriptCompilationAsync(string componentId, CancellationToken cancellationToken = default)
        {
            return SendCommandAsync(GrasshopperCommand.TestScriptCompilation(componentId), cancellationToken);
        }

        #endregion

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }
    }
}
