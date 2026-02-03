using System;
using System.Collections.Generic;

namespace GH_MCP.Client
{
    /// <summary>
    /// Normalizes component names from user input to Grasshopper component names
    /// </summary>
    public static class ComponentNormalizer
    {
        private static readonly Dictionary<string, string> ComponentMapping =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Number Slider variants
            ["number slider"] = "Number Slider",
            ["numeric slider"] = "Number Slider",
            ["num slider"] = "Number Slider",
            ["slider"] = "Number Slider",
            ["numberslider"] = "Number Slider",

            // MD Slider variants
            ["md slider"] = "MD Slider",
            ["mdslider"] = "MD Slider",
            ["multidimensional slider"] = "MD Slider",
            ["multi-dimensional slider"] = "MD Slider",

            // Graph Mapper
            ["graph mapper"] = "Graph Mapper",
            ["graphmapper"] = "Graph Mapper",

            // Math operations
            ["add"] = "Addition",
            ["addition"] = "Addition",
            ["plus"] = "Addition",
            ["sum"] = "Addition",
            ["subtract"] = "Subtraction",
            ["subtraction"] = "Subtraction",
            ["minus"] = "Subtraction",
            ["difference"] = "Subtraction",
            ["multiply"] = "Multiplication",
            ["multiplication"] = "Multiplication",
            ["times"] = "Multiplication",
            ["product"] = "Multiplication",
            ["divide"] = "Division",
            ["division"] = "Division",

            // Output
            ["panel"] = "Panel",
            ["text panel"] = "Panel",
            ["output panel"] = "Panel",
            ["display"] = "Panel",

            // Planes
            ["plane"] = "XY Plane",
            ["xyplane"] = "XY Plane",
            ["xy plane"] = "XY Plane",
            ["xy"] = "XY Plane",
            ["xzplane"] = "XZ Plane",
            ["xz plane"] = "XZ Plane",
            ["xz"] = "XZ Plane",
            ["yzplane"] = "YZ Plane",
            ["yz plane"] = "YZ Plane",
            ["yz"] = "YZ Plane",
            ["plane3pt"] = "Plane 3Pt",
            ["3ptplane"] = "Plane 3Pt",
            ["plane 3pt"] = "Plane 3Pt",

            // Basic geometry
            ["box"] = "Box",
            ["cube"] = "Box",
            ["rectangle"] = "Rectangle",
            ["rect"] = "Rectangle",
            ["circle"] = "Circle",
            ["circ"] = "Circle",
            ["sphere"] = "Sphere",
            ["cylinder"] = "Cylinder",
            ["cyl"] = "Cylinder",
            ["cone"] = "Cone",
            ["line"] = "Line",
            ["ln"] = "Line",

            // Parameters
            ["point"] = "Point",
            ["pt"] = "Point",
            ["curve"] = "Curve",
            ["crv"] = "Curve",
            ["number"] = "Number",
            ["num"] = "Number",

            // Construct Point
            ["construct point"] = "Construct Point",
            ["constructpoint"] = "Construct Point",
            ["pt xyz"] = "Construct Point",
            ["point xyz"] = "Construct Point",

            // Extrude
            ["extrude"] = "Extrude",
            ["extr"] = "Extrude",
        };

        /// <summary>
        /// Normalizes a component type name to its Grasshopper standard name
        /// </summary>
        public static string Normalize(string componentType)
        {
            if (string.IsNullOrWhiteSpace(componentType))
                return componentType;

            var normalized = componentType.Trim();

            if (ComponentMapping.TryGetValue(normalized, out var mapped))
                return mapped;

            // Try lowercase
            if (ComponentMapping.TryGetValue(normalized.ToLowerInvariant(), out mapped))
                return mapped;

            // Return original if no mapping found
            return componentType;
        }

        /// <summary>
        /// Checks if a component type is a multi-input math component
        /// </summary>
        public static bool IsMultiInputMathComponent(string componentType)
        {
            var normalized = Normalize(componentType);
            return normalized == "Addition"
                || normalized == "Subtraction"
                || normalized == "Multiplication"
                || normalized == "Division"
                || normalized == "Math";
        }

        /// <summary>
        /// Gets the list of input parameter names for multi-input components
        /// </summary>
        public static string[] GetMultiInputParameterNames()
        {
            return new[] { "A", "B", "C", "D", "E", "F", "G", "H" };
        }
    }
}
