using System.Collections.Generic;
using Newtonsoft.Json;

namespace GH_MCP.Client.Models
{
    /// <summary>
    /// Represents a command sent to the Grasshopper MCP plugin
    /// </summary>
    public class GrasshopperCommand
    {
        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("parameters")]
        public Dictionary<string, object?> Parameters { get; set; } = new Dictionary<string, object?>();

        public GrasshopperCommand() { }

        public GrasshopperCommand(string type, Dictionary<string, object?>? parameters = null)
        {
            Type = type;
            Parameters = parameters ?? new Dictionary<string, object?>();
        }

        /// <summary>
        /// Creates an add_component command
        /// </summary>
        public static GrasshopperCommand AddComponent(string componentType, float x, float y)
        {
            return new GrasshopperCommand("add_component", new Dictionary<string, object?>
            {
                ["type"] = componentType,
                ["x"] = x,
                ["y"] = y
            });
        }

        /// <summary>
        /// Creates a connect_components command
        /// </summary>
        public static GrasshopperCommand ConnectComponents(
            string sourceId,
            string targetId,
            string? sourceParam = null,
            string? targetParam = null,
            int? sourceParamIndex = null,
            int? targetParamIndex = null)
        {
            var parameters = new Dictionary<string, object?>
            {
                ["sourceId"] = sourceId,
                ["targetId"] = targetId
            };

            if (sourceParam != null) parameters["sourceParam"] = sourceParam;
            if (targetParam != null) parameters["targetParam"] = targetParam;
            if (sourceParamIndex != null) parameters["sourceParamIndex"] = sourceParamIndex;
            if (targetParamIndex != null) parameters["targetParamIndex"] = targetParamIndex;

            return new GrasshopperCommand("connect_components", parameters);
        }

        /// <summary>
        /// Creates a get_component_info command
        /// </summary>
        public static GrasshopperCommand GetComponentInfo(string componentId)
        {
            return new GrasshopperCommand("get_component_info", new Dictionary<string, object?>
            {
                ["componentId"] = componentId
            });
        }

        /// <summary>
        /// Creates a get_all_components command
        /// </summary>
        public static GrasshopperCommand GetAllComponents()
        {
            return new GrasshopperCommand("get_all_components");
        }

        /// <summary>
        /// Creates a get_connections command
        /// </summary>
        public static GrasshopperCommand GetConnections()
        {
            return new GrasshopperCommand("get_connections");
        }

        /// <summary>
        /// Creates a get_document_info command
        /// </summary>
        public static GrasshopperCommand GetDocumentInfo()
        {
            return new GrasshopperCommand("get_document_info");
        }

        /// <summary>
        /// Creates a clear_document command
        /// </summary>
        public static GrasshopperCommand ClearDocument()
        {
            return new GrasshopperCommand("clear_document");
        }

        /// <summary>
        /// Creates a save_document command
        /// </summary>
        public static GrasshopperCommand SaveDocument(string path)
        {
            return new GrasshopperCommand("save_document", new Dictionary<string, object?>
            {
                ["path"] = path
            });
        }

        /// <summary>
        /// Creates a load_document command
        /// </summary>
        public static GrasshopperCommand LoadDocument(string path)
        {
            return new GrasshopperCommand("load_document", new Dictionary<string, object?>
            {
                ["path"] = path
            });
        }

        /// <summary>
        /// Creates a search_components command
        /// </summary>
        public static GrasshopperCommand SearchComponents(string query)
        {
            return new GrasshopperCommand("search_components", new Dictionary<string, object?>
            {
                ["query"] = query
            });
        }

        /// <summary>
        /// Creates a get_component_parameters command
        /// </summary>
        public static GrasshopperCommand GetComponentParameters(string componentType)
        {
            return new GrasshopperCommand("get_component_parameters", new Dictionary<string, object?>
            {
                ["componentType"] = componentType
            });
        }

        /// <summary>
        /// Creates a validate_connection command
        /// </summary>
        public static GrasshopperCommand ValidateConnection(
            string sourceId,
            string targetId,
            string? sourceParam = null,
            string? targetParam = null)
        {
            var parameters = new Dictionary<string, object?>
            {
                ["sourceId"] = sourceId,
                ["targetId"] = targetId
            };

            if (sourceParam != null) parameters["sourceParam"] = sourceParam;
            if (targetParam != null) parameters["targetParam"] = targetParam;

            return new GrasshopperCommand("validate_connection", parameters);
        }

        /// <summary>
        /// Creates a create_pattern command
        /// </summary>
        public static GrasshopperCommand CreatePattern(string description)
        {
            return new GrasshopperCommand("create_pattern", new Dictionary<string, object?>
            {
                ["description"] = description
            });
        }

        /// <summary>
        /// Creates a get_available_patterns command
        /// </summary>
        public static GrasshopperCommand GetAvailablePatterns(string query)
        {
            return new GrasshopperCommand("get_available_patterns", new Dictionary<string, object?>
            {
                ["query"] = query
            });
        }

        #region Verification Commands (for AI Testing)

        /// <summary>
        /// Creates an export_document_state command
        /// Returns full JSON snapshot of all components, connections, and values
        /// </summary>
        public static GrasshopperCommand ExportDocumentState()
        {
            return new GrasshopperCommand("export_document_state");
        }

        /// <summary>
        /// Creates an assert_component_exists command
        /// </summary>
        public static GrasshopperCommand AssertComponentExists(string componentId)
        {
            return new GrasshopperCommand("assert_component_exists", new Dictionary<string, object?>
            {
                ["componentId"] = componentId
            });
        }

        /// <summary>
        /// Creates an assert_connection_exists command
        /// </summary>
        public static GrasshopperCommand AssertConnectionExists(
            string sourceId,
            string targetId,
            string? sourceParam = null,
            string? targetParam = null)
        {
            var parameters = new Dictionary<string, object?>
            {
                ["sourceId"] = sourceId,
                ["targetId"] = targetId
            };

            if (sourceParam != null) parameters["sourceParam"] = sourceParam;
            if (targetParam != null) parameters["targetParam"] = targetParam;

            return new GrasshopperCommand("assert_connection_exists", parameters);
        }

        /// <summary>
        /// Creates an assert_component_count command
        /// </summary>
        public static GrasshopperCommand AssertComponentCount(int expectedCount)
        {
            return new GrasshopperCommand("assert_component_count", new Dictionary<string, object?>
            {
                ["expected"] = expectedCount
            });
        }

        /// <summary>
        /// Creates a get_document_hash command
        /// Returns SHA256 hash of document state for quick comparison
        /// </summary>
        public static GrasshopperCommand GetDocumentHash()
        {
            return new GrasshopperCommand("get_document_hash");
        }

        #endregion
    }
}
