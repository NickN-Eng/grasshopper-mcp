using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rhino;
using GH_MCP.Models;

namespace GH_MCP.Utils
{
    /// <summary>
    /// Recognizes user intent and converts it into concrete components and connections.
    /// </summary>
    public class IntentRecognizer
    {
        private static JObject _knowledgeBase;
        private static readonly string _knowledgeBasePath = Path.Combine(
            Path.GetDirectoryName(typeof(IntentRecognizer).Assembly.Location),
            "Resources",
            "ComponentKnowledgeBase.json"
        );

        /// <summary>
        /// Initialize the knowledge base.
        /// </summary>
        public static void Initialize()
        {
            try
            {
                if (File.Exists(_knowledgeBasePath))
                {
                    string json = File.ReadAllText(_knowledgeBasePath);
                    _knowledgeBase = JObject.Parse(json);
                    RhinoApp.WriteLine($"Component knowledge base loaded from {_knowledgeBasePath}");
                }
                else
                {
                    RhinoApp.WriteLine($"Component knowledge base not found at {_knowledgeBasePath}");
                    _knowledgeBase = new JObject();
                }
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error loading component knowledge base: {ex.Message}");
                _knowledgeBase = new JObject();
            }
        }

        /// <summary>
        /// Recognize intent from a user description.
        /// </summary>
        /// <param name="description">User description.</param>
        /// <returns>Recognized pattern name, or null if no match.</returns>
        public static string RecognizeIntent(string description)
        {
            if (_knowledgeBase == null)
            {
                Initialize();
            }

            if (_knowledgeBase["intents"] == null)
            {
                return null;
            }

            // Convert the description to lowercase and split into words.
            string[] words = description.ToLowerInvariant().Split(
                new[] { ' ', ',', '.', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}' },
                StringSplitOptions.RemoveEmptyEntries
            );

            // Calculate match scores for each intent.
            var intentScores = new Dictionary<string, int>();

            foreach (var intent in _knowledgeBase["intents"])
            {
                string patternName = intent["pattern"].ToString();
                var keywords = intent["keywords"].ToObject<List<string>>();

                // Count matching keywords.
                int matchCount = words.Count(word => keywords.Contains(word));

                if (matchCount > 0)
                {
                    intentScores[patternName] = matchCount;
                }
            }

            // Return the intent with the highest score.
            if (intentScores.Count > 0)
            {
                return intentScores.OrderByDescending(pair => pair.Value).First().Key;
            }

            return null;
        }

        /// <summary>
        /// Get components and connections for a pattern.
        /// </summary>
        /// <param name="patternName">Pattern name.</param>
        /// <returns>Tuple containing components and connections.</returns>
        public static (List<ComponentInfo> Components, List<ConnectionInfo> Connections) GetPatternDetails(string patternName)
        {
            if (_knowledgeBase == null)
            {
                Initialize();
            }

            var components = new List<ComponentInfo>();
            var connections = new List<ConnectionInfo>();

            if (_knowledgeBase["patterns"] == null)
            {
                return (components, connections);
            }

            // Find the matching pattern.
            var pattern = _knowledgeBase["patterns"].FirstOrDefault(p => p["name"].ToString() == patternName);
            if (pattern == null)
            {
                return (components, connections);
            }

            // Get component info.
            foreach (var comp in pattern["components"])
            {
                var componentInfo = new ComponentInfo
                {
                    Type = comp["type"].ToString(),
                    X = comp["x"].Value<double>(),
                    Y = comp["y"].Value<double>(),
                    Id = comp["id"].ToString()
                };

                // If settings exist, add them.
                if (comp["settings"] != null)
                {
                    componentInfo.Settings = comp["settings"].ToObject<Dictionary<string, object>>();
                }

                components.Add(componentInfo);
            }

            // Get connection info.
            foreach (var conn in pattern["connections"])
            {
                connections.Add(new ConnectionInfo
                {
                    SourceId = conn["source"].ToString(),
                    SourceParam = conn["sourceParam"].ToString(),
                    TargetId = conn["target"].ToString(),
                    TargetParam = conn["targetParam"].ToString()
                });
            }

            return (components, connections);
        }

        /// <summary>
        /// Get all available component types.
        /// </summary>
        /// <returns>List of component types.</returns>
        public static List<string> GetAvailableComponentTypes()
        {
            if (_knowledgeBase == null)
            {
                Initialize();
            }

            var types = new List<string>();

            if (_knowledgeBase["components"] != null)
            {
                foreach (var comp in _knowledgeBase["components"])
                {
                    types.Add(comp["name"].ToString());
                }
            }

            return types;
        }

        /// <summary>
        /// Get detailed information for a component type.
        /// </summary>
        /// <param name="componentType">Component type.</param>
        /// <returns>Component details.</returns>
        public static JObject GetComponentDetails(string componentType)
        {
            if (_knowledgeBase == null)
            {
                Initialize();
            }

            if (_knowledgeBase["components"] != null)
            {
                var component = _knowledgeBase["components"].FirstOrDefault(
                    c => c["name"].ToString().Equals(componentType, StringComparison.OrdinalIgnoreCase)
                );

                if (component != null)
                {
                    return JObject.FromObject(component);
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Component information.
    /// </summary>
    public class ComponentInfo
    {
        public string Type { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public string Id { get; set; }
        public Dictionary<string, object> Settings { get; set; }
    }

    /// <summary>
    /// Connection information.
    /// </summary>
    public class ConnectionInfo
    {
        public string SourceId { get; set; }
        public string SourceParam { get; set; }
        public string TargetId { get; set; }
        public string TargetParam { get; set; }
    }
}
