using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Grasshopper;
using Grasshopper.Kernel;
using Rhino.Runtime;
using RhinoCodePluginGH;
using RhinoCodePluginGH.Components;

namespace ChatGHGPT.GH
{
    public class ParseToCSharpComponent
    {
        private const string ScriptSample = @"// Grasshopper Script Instance
#region Usings
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
#endregion

public class Script_Instance : GH_ScriptInstance
{
    #region Notes
    /* 
      Members:
        RhinoDoc RhinoDocument
        GH_Document GrasshopperDocument
        IGH_Component Component
        int Iteration

      Methods (Virtual & overridable):
        Print(string text)
        Print(string format, params object[] args)
        Reflect(object obj)
        Reflect(object obj, string method_name)
    */
    #endregion

    private void RunScript(object x, object y, ref object a)
    {
        // Write your logic here
        a = null;
    }
}
";

        private const string ScriptSample2 = @"// Grasshopper Script Instance
#region Usings
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
#endregion

public class Script_Instance : GH_ScriptInstance
{
    #region Notes
    /* 
      Members:
        RhinoDoc RhinoDocument
        GH_Document GrasshopperDocument
        IGH_Component Component
        int Iteration

      Methods (Virtual & overridable):
        Print(string text)
        Print(string format, params object[] args)
        Reflect(object obj)
        Reflect(object obj, string method_name)
    */
    #endregion

    private void RunScript(object x, ref object a)
    {
        // Write your logic here
        a = x;
    }
}
";

        /// <summary>
        /// Constructor for ParseToCSharpComponent.
        /// Initializes by serializing the component and retrieving property details.
        /// </summary>
        /// <param name="component">The Grasshopper component to parse.</param>
        public ParseToCSharpComponent(GH_Component component)
        {
            var typ = component.GetType();
            var assyyyyy = typ.Assembly;

            // Initialize a new GH_Archive to serialize the component
            var archive = new GH_IO.Serialization.GH_Archive();

            // Append the component object to the archive with the key "Component"
            archive.AppendObject(component, "Component");

            // Retrieve the root node data from the archive
            var rootNode = archive.GetRootNode;

            // Access the component's attributes
            var attributes = component.Attributes;

            // Retrieve nested property details for various property paths
            var propsContext = GetObjectDetails(component, new string[] { "Context" });
            var propsScript = GetObjectDetails(component, new string[] { "Context", "Script" });
            var propsText = GetObjectDetails(component, new string[] { "Context", "Script", "Text" });

            // Access the first input parameter of the component
            IGH_Param param = component.Params.Input[0];
            var paramDetails = GetObjectDetails(param, new string[] { });
            var paramConverter = GetObjectDetails(param, new string[] { "Converter" });

            // Example of setting a nested property value (commented out)
            // SetNestedPropertyValue(component, new string[] { "Context", "Script", "Text" }, "Hello World!");

            // Call a method named "GetText" at the specified property path
            var result = CallMethodAtPath(component, new string[] { "Context" }, "GetText");
            var parser = new CSharpScriptParser((string)result);
            var parser2 = new GrasshopperScriptParser((string)result);
            var parser3 = new CSharpScriptParserV2((string)result);

            // Call a method named "SetText" at the specified property path with a string parameter
            CallMethodAtPathWithStringParam(component, new string[] { "Context" }, "SetText", ScriptSample2);
            InvokeSaveScriptClicked(component);
            component.ExpireSolution(true);

            var dict = GHReflectionHelper.Converters;

            if(component is CSharpComponent csc)
            {
                var cs = csc;

            }

            //var code = new Rhino.Runtime.Code.
            
            //var assyInsepctor = new RhinoAssemblyInspector();
            //var assysName = assyInsepctor.GetGHAssemblies().Select(assy => assy.GetName().ToString()).ToList();
            //var assysFN = assyInsepctor.GetGHAssemblies().Select(assy => assy.FullName).ToList();

            //var classes = new List<string>();
            //var failedAssys = new List<string>();
            //var assys = AppDomain.CurrentDomain.GetAssemblies();
            //foreach (var assy in assys)
            //{
            //    Type[] types = null;
            //    try
            //    {
            //        types = assy.GetTypes();
            //    }
            //    catch (ReflectionTypeLoadException ex)
            //    {
            //        failedAssys.Add(assy.FullName);
            //    }
            //    if (types == null) continue;

            //    foreach (var clas in types)
            //    {
            //        classes.Add(clas.FullName + ", " + assy.FullName);
            //    }
            //}
        }

        /// <summary>
        /// Sets the value of a nested property specified by the property path.
        /// </summary>
        /// <param name="obj">The object containing the property.</param>
        /// <param name="propertyPath">An array representing the path to the property.</param>
        /// <param name="newValue">The new value to set.</param>
        static void SetNestedPropertyValue(object obj, string[] propertyPath, object newValue)
        {
            if (propertyPath == null || propertyPath.Length == 0)
            {
                // Property path is empty, can't proceed with setting a nested property
                return;
            }

            // Initialize current object and type for traversal
            object currentObject = obj;
            Type currentType = obj.GetType();

            // Traverse the property path up to the second last property
            for (int i = 0; i < propertyPath.Length - 1; i++)
            {
                // Retrieve the property information using reflection
                PropertyInfo prop = currentType.GetProperty(propertyPath[i],
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);

                if (prop == null)
                {
                    // Property not found, cannot continue traversal
                    return;
                }

                // Get the value of the current property
                currentObject = prop.GetValue(currentObject);
                if (currentObject == null)
                {
                    // Property value is null, cannot continue traversal
                    return;
                }

                // Update the current type for the next iteration
                currentType = currentObject.GetType();
            }

            // Retrieve the target property to set its value
            PropertyInfo targetProperty = currentType.GetProperty(propertyPath[propertyPath.Length - 1],
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);

            if (targetProperty != null)
            {
                // Set the value of the target property using reflection
                targetProperty.SetValue(currentObject, newValue);
            }
        }

        /// <summary>
        /// Retrieves property and field details of the final type in the property path.
        /// If the property path is empty, retrieves details of the object itself.
        /// </summary>
        /// <param name="obj">The object to inspect.</param>
        /// <param name="propertyPath">An array representing the path to traverse.</param>
        /// <returns>A list of strings describing the properties and fields.</returns>
        public static List<string> GetObjectDetails(object obj, string[] propertyPath)
        {
            List<string> details = new List<string>();

            // Check if the property path is empty
            if (propertyPath == null || propertyPath.Length == 0)
            {
                // If empty, retrieve and list properties and fields of the object itself
                Type objType = obj.GetType();
                details.AddRange(GetPropertiesAndFields(obj));
                details.AddRange(GetMethodDetails(objType));

                return details;
            }

            // Initialize current object and type for traversal
            object currentObject = obj;
            Type currentType = obj.GetType();

            // Traverse the property path to reach the final object
            foreach (string propName in propertyPath)
            {
                PropertyInfo prop = currentType.GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                FieldInfo field = currentType.GetField(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (prop != null)
                {
                    // Add property details to the list
                    details.Add($"Property: {prop.Name}, Access: {GetAccessModifier(prop)}, Type: {prop.PropertyType.Name}");
                    currentObject = prop.GetValue(currentObject);
                }
                else if (field != null)
                {
                    // Add field details to the list
                    details.Add($"Field: {field.Name}, Access: {GetAccessModifier(field)}, Type: {field.FieldType.Name}");
                    currentObject = field.GetValue(currentObject);
                }
                else
                {
                    // Property or field not found, add a message and terminate
                    //details.Add($"Property/Field '{propName}' not found.");
                    return details;
                }

                if (currentObject == null)
                {
                    // Current object is null, add a message and terminate
                    //details.Add($"Property/Field '{propName}' is null.");
                    return details;
                }

                // Update the current type for the next iteration
                currentType = currentObject.GetType();
            }

            // After traversal, retrieve properties and fields of the final type
            details.AddRange(GetPropertiesAndFields(currentObject));
            details.AddRange(GetMethodDetails(currentType));
            return details;
        }

        /// <summary>
        /// Retrieves and formats the properties and fields of a given object, including their values.
        /// </summary>
        /// <param name="obj">The object to inspect.</param>
        /// <returns>A list of strings describing the properties, fields, and their values.</returns>
        private static List<string> GetPropertiesAndFields(object obj)
        {
            List<string> details = new List<string>();

            if (obj == null)
            {
                details.Add("Object is null.");
                return details;
            }

            Type type = obj.GetType();

            // Retrieve all properties with specified binding flags
            PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (PropertyInfo prop in properties)
            {
                try
                {
                    object value = prop.GetValue(obj); // Retrieve the value of the property
                    details.Add($"Property: {prop.Name}, Access: {GetAccessModifier(prop)}, Type: {prop.PropertyType.Name}, Value: {FormatValue(value)}, Assy: {prop.PropertyType.Assembly}");
                }
                catch (Exception ex)
                {
                    details.Add($"Property: {prop.Name}, Access: {GetAccessModifier(prop)}, Type: {prop.PropertyType.Name}, Value: [Error retrieving value: {ex.Message}], Assy: {prop.PropertyType.Assembly}");
                }
            }

            // Retrieve all fields with specified binding flags
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (FieldInfo field in fields)
            {
                try
                {
                    object value = field.GetValue(obj); // Retrieve the value of the field
                    details.Add($"Field: {field.Name}, Access: {GetAccessModifier(field)}, Type: {field.FieldType.Name}, Value: {FormatValue(value)}, Assy: {field.FieldType.Assembly}");
                }
                catch (Exception ex)
                {
                    details.Add($"Field: {field.Name}, Access: {GetAccessModifier(field)}, Type: {field.FieldType.Name}, Value: [Error retrieving value: {ex.Message}], Assy: {field.FieldType.Assembly}");
                }
            }

            return details;
        }

        /// <summary>
        /// Formats an object's value for display, handling nulls and complex types.
        /// </summary>
        /// <param name="value">The value to format.</param>
        /// <returns>A formatted string representation of the value.</returns>
        private static string FormatValue(object value)
        {
            if (value == null) return "null";

            // If it's a primitive type or a string, return as is
            if (value is string || value.GetType().IsPrimitive)
            {
                return value.ToString();
            }

            // For collections, return the count
            if (value is System.Collections.ICollection collection)
            {
                return $"Collection (Count: {collection.Count})";
            }

            // If it's a complex object, return the type name instead of trying to print the entire object
            return $"Instance of {value.GetType().Name}";
        }

        /// <summary>
        /// Retrieves method details of the specified type.
        /// </summary>
        /// <param name="type">The type to inspect.</param>
        /// <returns>A list of strings describing the methods.</returns>
        private static List<string> GetMethodDetails(Type type)
        {
            List<string> methodDetails = new List<string>();

            // Retrieve all methods with specified binding flags
            MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (MethodInfo method in methods)
            {
                // Get access level, return type, and parameters of the method
                string accessLevel = GetAccessModifier(method);
                string returnType = method.ReturnType.Name;
                string parameters = string.Join(", ", method.GetParameters()
                    .Select(p => $"{p.ParameterType.Name} {p.Name}"));

                if (string.IsNullOrEmpty(parameters))
                    parameters = "None";

                // Add method details to the list
                methodDetails.Add($"Method: {method.Name}, Access: {accessLevel}, Return Type: {returnType}, Parameters: ({parameters})");
            }

            return methodDetails;
        }

        /// <summary>
        /// Determines the access modifier of a member.
        /// </summary>
        /// <param name="member">The member to inspect.</param>
        /// <returns>A string representing the access level.</returns>
        private static string GetAccessModifier(MemberInfo member)
        {
            if (member is MethodInfo method)
            {
                if (method.IsPublic) return "Public";
                if (method.IsFamily) return "Protected";
                if (method.IsPrivate) return "Private";
                return "Internal";
            }

            if (member is PropertyInfo prop)
            {
                // Determine access level based on getter and setter
                MethodInfo getMethod = prop.GetGetMethod(true);
                MethodInfo setMethod = prop.GetSetMethod(true);

                if ((getMethod != null && getMethod.IsPublic) || (setMethod != null && setMethod.IsPublic))
                    return "Public";
                if ((getMethod != null && getMethod.IsFamily) || (setMethod != null && setMethod.IsFamily))
                    return "Protected";
                if ((getMethod != null && getMethod.IsPrivate) || (setMethod != null && setMethod.IsPrivate))
                    return "Private";
                return "Internal";
            }

            if (member is FieldInfo field)
            {
                if (field.IsPublic) return "Public";
                if (field.IsFamily) return "Protected";
                if (field.IsPrivate) return "Private";
                return "Internal";
            }

            return "Unknown";
        }

        /// <summary>
        /// Calls a method at a specified property path and returns the result.
        /// If the property path is empty, calls the method on the object itself.
        /// </summary>
        /// <param name="obj">The object containing the method.</param>
        /// <param name="propertyPath">An array representing the path to traverse.</param>
        /// <param name="methodName">The name of the method to call.</param>
        /// <returns>The result of the method invocation.</returns>
        public static object CallMethodAtPath(object obj, string[] propertyPath, string methodName)
        {
            // Retrieve the target object based on the property path
            object targetObject = GetTargetObjectAtPath(obj, propertyPath);
            if (targetObject == null)
            {
                throw new InvalidOperationException("Target object is null or property path is invalid.");
            }

            // Retrieve the method information using reflection
            MethodInfo method = targetObject.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException($"Method '{methodName}' not found.");
            }

            // Invoke the method without parameters and return the result
            return method.Invoke(targetObject, null);
        }

        /// <summary>
        /// Calls a method at a specified property path with a string parameter and returns the result.
        /// If the property path is empty, calls the method on the object itself.
        /// </summary>
        /// <param name="obj">The object containing the method.</param>
        /// <param name="propertyPath">An array representing the path to traverse.</param>
        /// <param name="methodName">The name of the method to call.</param>
        /// <param name="parameter">The string parameter to pass to the method.</param>
        /// <returns>The result of the method invocation.</returns>
        public static object CallMethodAtPathWithStringParam(object obj, string[] propertyPath, string methodName, string parameter)
        {
            // Retrieve the target object based on the property path
            object targetObject = GetTargetObjectAtPath(obj, propertyPath);
            if (targetObject == null)
            {
                throw new InvalidOperationException("Target object is null or property path is invalid.");
            }

            // Retrieve the method information using reflection
            MethodInfo method = targetObject.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new InvalidOperationException($"Method '{methodName}' not found.");
            }

            // Retrieve the method's parameters to ensure it accepts a single string parameter
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != 1 || parameters[0].ParameterType != typeof(string))
            {
                throw new InvalidOperationException($"Method '{methodName}' does not take a single string parameter.");
            }

            // Invoke the method with the provided string parameter and return the result
            return method.Invoke(targetObject, new object[] { parameter });
        }

        /// <summary>
        /// Traverses an object based on a property path and returns the final object.
        /// If the property path is empty, returns the object itself.
        /// </summary>
        /// <param name="obj">The object to traverse.</param>
        /// <param name="propertyPath">An array representing the path to traverse.</param>
        /// <returns>The final object after traversal, or null if traversal fails.</returns>
        private static object GetTargetObjectAtPath(object obj, string[] propertyPath)
        {
            if (propertyPath == null || propertyPath.Length == 0)
            {
                // If the property path is empty, return the object itself
                return obj;
            }

            // Initialize current object and type for traversal
            object currentObject = obj;
            Type currentType = obj.GetType();

            // Traverse the property path step by step
            foreach (string propName in propertyPath)
            {
                PropertyInfo prop = currentType.GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                FieldInfo field = currentType.GetField(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (prop != null)
                {
                    // Get the value of the current property
                    currentObject = prop.GetValue(currentObject);
                }
                else if (field != null)
                {
                    // Get the value of the current field
                    currentObject = field.GetValue(currentObject);
                }
                else
                {
                    // Property or field not found, traversal fails
                    return null;
                }

                if (currentObject == null)
                {
                    // Current object is null, traversal cannot continue
                    return null;
                }

                // Update the current type for the next iteration
                currentType = currentObject.GetType();
            }

            // Return the final object after successful traversal
            return currentObject;
        }

        public static void InvokeSaveScriptClicked(object targetObject)
        {
            if (targetObject == null)
            {
                Console.WriteLine("Target object is null.");
                return;
            }

            Type targetType = targetObject.GetType();
            MethodInfo methodInfo = targetType.GetMethod("Menu_SaveScriptClicked",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (methodInfo == null)
            {
                Console.WriteLine("Method 'Menu_SaveScriptClicked' not found on target object.");
                return;
            }

            try
            {
                object sender = null; // You can pass an actual sender object if needed
                EventArgs eventArgs = EventArgs.Empty; // Use an empty EventArgs instance
                methodInfo.Invoke(targetObject, new object[] { sender, eventArgs });
            }
            catch (TargetInvocationException ex)
            {
                Console.WriteLine($"Invocation failed: {ex.InnerException?.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error invoking method: {ex.Message}");
            }
        }
    }

    public class RhinoAssemblyInspector
    {
        /// <summary>
        /// Retrieves all assemblies loaded in Rhino, including those used by Grasshopper.
        /// </summary>
        public List<Assembly> GetGHAssemblies()
        {
            return Instances.ComponentServer.Libraries
                .Select(lib => lib.GetType().Assembly)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Finds an assembly by its name (case-insensitive).
        /// </summary>
        public Assembly FindAssemblyByName(string assemblyName)
        {
            return GetGHAssemblies().FirstOrDefault(a => a.GetName().Name.Equals(assemblyName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Finds a class by its fully qualified name within a specific assembly.
        /// </summary>
        public Type FindClassInAssembly(string assemblyName, string className)
        {
            Assembly assembly = FindAssemblyByName(assemblyName);
            return assembly?.GetType(className);
        }

        /// <summary>
        /// Lists all class names in a given assembly.
        /// </summary>
        public List<string> ListClassesInAssembly(string assemblyName)
        {
            Assembly assembly = FindAssemblyByName(assemblyName);
            return assembly != null ? assembly.GetTypes().Select(t => t.FullName).ToList() : new List<string>();
        }

        /// <summary>
        /// Lists all class names in a given assembly.
        /// </summary>
        public List<string> ListClassesInAssembly(Assembly assembly)
        {
            return assembly != null ? assembly.GetTypes().Select(t => t.FullName).ToList() : new List<string>();
        }
    }
}
