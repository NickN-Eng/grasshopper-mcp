using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ChatGHGPT.GH
{
    public static class GHReflectionHelper
    {
        private static readonly Lazy<Dictionary<string, Type>> _converters = new Lazy<Dictionary<string, Type>>(InitializeConverters);

        /// <summary>
        /// Dictionary of converter types found in RhinoCodePlatform.Rhino3D.Languages.GH1.Converters.
        /// Key: Shortened class name without prefix and "Converter" suffix.
        /// Value: Corresponding Type.
        /// </summary>
        public static Dictionary<string, Type> Converters => _converters.Value;

        /// <summary>
        /// Searches for all classes in the namespace and constructs the dictionary.
        /// </summary>
        private static Dictionary<string, Type> InitializeConverters()
        {
            const string assemblyName = "RhinoCodePlatform.Rhino3D";
            const string namespacePrefix = "RhinoCodePlatform.Rhino3D.Languages.GH1.Converters.";
            const string suffix = "Converter";

            // Find the assembly
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name.Equals(assemblyName, StringComparison.OrdinalIgnoreCase));

            if (assembly == null)
            {
                return new Dictionary<string, Type>(); // Return empty dictionary if assembly is not found.
            }

            // Get all types in the desired namespace
            var converterTypes = assembly.GetTypes()
                .Where(t => t.IsClass && t.Namespace != null && t.Namespace.Equals(namespacePrefix.TrimEnd('.'), StringComparison.Ordinal))
                .Where(t => t.Name.EndsWith(suffix)) // Ensure it's a "Converter"
                .ToDictionary(
                    t => t.Name.Replace(suffix, ""), // Remove "Converter" suffix
                    t => t
                );

            return converterTypes;
        }
    }
}
