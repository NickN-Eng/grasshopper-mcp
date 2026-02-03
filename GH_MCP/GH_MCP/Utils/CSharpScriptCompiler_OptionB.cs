using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Rhino;

// NOTE: This Option B file assumes you have added references to the RhinoCodePluginGH DLLs.
// Before using this, you need to:
// 1. Add references to the DLLs in the .csproj file
// 2. Decompile the DLLs to find the exact types and method signatures
// 3. Replace the placeholder comments below with actual type names

// Once you have the actual types from decompilation, uncomment and update these using statements:
// using RhinoCodePluginGH.Components;
// using RhinoCodePlatform.GH.Context;
// using Rhino.Runtime.Code;

namespace GH_MCP.Utils
{
    /// <summary>
    /// Option B: Direct DLL reference approach to compile C# script components.
    /// This approach uses direct references to RhinoCodePluginGH and related DLLs.
    /// Cleaner code but tightly coupled to specific DLL versions.
    ///
    /// CURRENT STATUS: Template only - requires DLL decompilation to complete.
    /// See implementation plan for next steps.
    /// </summary>
    public class CSharpScriptCompiler_OptionB
    {
        /// <summary>
        /// Attempts to compile a C# script component using direct DLL references.
        ///
        /// IMPLEMENTATION NOTE:
        /// Once you have decompiled the DLLs and found the exact types, replace the
        /// commented sections below with actual type-safe code.
        /// </summary>
        /// <param name="component">The C# script component</param>
        /// <param name="errors">Output compilation errors if any</param>
        /// <returns>True if compilation succeeded</returns>
        public static bool TryCompile(GH_Component component, out List<string> errors)
        {
            errors = new List<string>();

            try
            {
                // TODO: After decompiling RhinoCodePluginGH.dll, replace this with:
                // if (component is CSharpComponent csComponent)
                // {
                //     return TryCompileDirect(csComponent, out errors);
                // }
                // else
                // {
                //     errors.Add("Component is not a CSharpComponent");
                //     return false;
                // }

                // Fallback to reflection approach for now
                return CSharpScriptCompiler_OptionA.TryCompile(component, out errors);
            }
            catch (Exception ex)
            {
                errors.Add($"Compilation failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Direct compilation using typed references (to be implemented after decompilation).
        /// </summary>
        /// <example>
        /// Example implementation once types are known:
        ///
        /// private static bool TryCompileDirect(CSharpComponent component, out List<string> errors)
        /// {
        ///     errors = new List<string>();
        ///
        ///     bool success = false;
        ///     var localErrors = new List<string>();
        ///
        ///     RhinoApp.InvokeOnUiThread(new Action(() =>
        ///     {
        ///         try
        ///         {
        ///             // Get the script context (exact type TBD from decompilation)
        ///             var context = component.Context; // Type: IScriptContext or similar
        ///
        ///             // Call the compilation method (exact method TBD from decompilation)
        ///             // Option 1: If there's a Compile() method:
        ///             // context.Compile();
        ///
        ///             // Option 2: If there's a Build() method:
        ///             // context.Build();
        ///
        ///             // Option 3: If compilation is on a different object:
        ///             // var compiler = context.GetCompiler();
        ///             // compiler.Compile();
        ///
        ///             // Get compilation errors (exact property/method TBD)
        ///             // var compilationErrors = context.CompilationErrors;
        ///             // or
        ///             // var compilationErrors = context.GetErrors();
        ///
        ///             // if (compilationErrors != null && compilationErrors.Any())
        ///             // {
        ///             //     foreach (var error in compilationErrors)
        ///             //     {
        ///             //         localErrors.Add(error.Message);
        ///             //     }
        ///             // }
        ///             // else
        ///             // {
        ///             //     success = true;
        ///             // }
        ///
        ///             // Force recomputation
        ///             component.ExpireSolution(true);
        ///
        ///             success = true; // Placeholder
        ///         }
        ///         catch (Exception ex)
        ///         {
        ///             localErrors.Add($"Compilation error: {ex.Message}");
        ///         }
        ///     }));
        ///
        ///     errors = localErrors;
        ///     return success;
        /// }
        /// </example>

        /// <summary>
        /// Sets script text using direct type references (to be implemented).
        /// </summary>
        /// <example>
        /// Example implementation once types are known:
        ///
        /// public static bool SetScriptText(CSharpComponent component, string scriptText)
        /// {
        ///     try
        ///     {
        ///         var context = component.Context;
        ///         context.SetText(scriptText);
        ///         return true;
        ///     }
        ///     catch
        ///     {
        ///         return false;
        ///     }
        /// }
        /// </example>
        public static bool SetScriptText(GH_Component component, string scriptText)
        {
            // Fallback to reflection for now
            return CSharpScriptCompiler_OptionA.SetScriptText(component, scriptText);
        }

        /// <summary>
        /// Gets script text using direct type references (to be implemented).
        /// </summary>
        /// <example>
        /// Example implementation once types are known:
        ///
        /// public static string GetScriptText(CSharpComponent component)
        /// {
        ///     try
        ///     {
        ///         var context = component.Context;
        ///         return context.GetText();
        ///     }
        ///     catch
        ///     {
        ///         return null;
        ///     }
        /// }
        /// </example>
        public static string GetScriptText(GH_Component component)
        {
            // Fallback to reflection for now
            return CSharpScriptCompiler_OptionA.GetScriptText(component);
        }

        #region Decompilation Guide

        /*
         * DECOMPILATION GUIDE
         * ===================
         *
         * To complete Option B, follow these steps:
         *
         * 1. LOAD DLL IN ILSPY/DNSPY
         *    - Open RhinoCodePluginGH.dll in ILSpy or dnSpy
         *    - Also load RhinoCodePlatform.GH.dll and RhinoCodePlatform.GH1.dll
         *
         * 2. FIND C# COMPONENT CLASS
         *    - Search for "CSharp" or "ScriptComponent"
         *    - Look for a class that inherits from GH_Component
         *    - Common names: CSharpComponent, GH_ScriptComponent, Script_Component
         *
         * 3. EXAMINE CONTEXT PROPERTY
         *    - Find the Context property on the C# component class
         *    - Note its type (e.g., IScriptContext, ScriptContext, etc.)
         *    - Navigate to that type definition
         *
         * 4. FIND COMPILATION METHODS
         *    - In the Context type, look for methods like:
         *      - Compile()
         *      - Build()
         *      - CompileScript()
         *      - ExecuteScript()
         *      - Save()
         *    - Note the method signature (parameters, return type)
         *
         * 5. FIND ERROR RETRIEVAL
         *    - Look for properties or methods to get compilation errors:
         *      - CompilationErrors property
         *      - Errors property
         *      - GetErrors() method
         *    - Note the return type (List<string>, IEnumerable<Error>, etc.)
         *
         * 6. FIND TEXT GET/SET METHODS
         *    - Look for:
         *      - GetText() / SetText(string) methods
         *      - Script.Text property
         *      - Code property
         *
         * 7. UPDATE THIS FILE
         *    - Add appropriate using statements
         *    - Replace placeholder code with actual types
         *    - Implement TryCompileDirect method with discovered types
         *    - Implement SetScriptText and GetScriptText with discovered methods
         *
         * 8. ADD DLL REFERENCES TO .CSPROJ
         *    <ItemGroup>
         *      <Reference Include="RhinoCodePluginGH">
         *        <HintPath>..\csharpscriptAttempt\CustomPackages\RhinoCodePluginGH.dll</HintPath>
         *        <Private>false</Private>
         *      </Reference>
         *      <Reference Include="RhinoCodePlatform.GH">
         *        <HintPath>..\csharpscriptAttempt\CustomPackages\RhinoCodePlatform.GH.dll</HintPath>
         *        <Private>false</Private>
         *      </Reference>
         *      <Reference Include="RhinoCodePlatform.GH1">
         *        <HintPath>..\csharpscriptAttempt\CustomPackages\RhinoCodePlatform.GH1.dll</HintPath>
         *        <Private>false</Private>
         *      </Reference>
         *    </ItemGroup>
         *
         * EXPECTED FINDINGS (EXAMPLES)
         * ============================
         * Based on common patterns in Grasshopper plugins, you might find:
         *
         * // Component type
         * RhinoCodePluginGH.Components.CSharpComponent : GH_Component
         * {
         *     public IScriptContext Context { get; }
         * }
         *
         * // Context type
         * RhinoCodePlatform.GH.Context.IScriptContext
         * {
         *     string GetText();
         *     void SetText(string text);
         *     void Compile();
         *     IEnumerable<CompilationError> GetErrors();
         * }
         *
         * Once you have the actual types, replace the placeholders above.
         */

        #endregion
    }
}
