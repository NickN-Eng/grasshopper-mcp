using Grasshopper.Kernel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ChatGHGPT.GH
{
    /// <summary>
    /// Class representing a parameter in the RunScript method.
    /// </summary>
    public class CsharpScriptParam
    {
        /// <summary>
        /// Name of the parameter.
        /// </summary>
        public string ParamName { get; set; }

        /// <summary>
        /// Access type of the parameter (item, list, tree).
        /// </summary>
        public GH_ParamAccess AccessType { get; set; }

        /// <summary>
        /// Type of the parameter without wrappers like List<> or DataTree<>.
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Constructor to initialize a CsharpScriptParam instance.
        /// </summary>
        /// <param name="paramName">Name of the parameter.</param>
        /// <param name="type">Type of the parameter.</param>
        /// <param name="accessType">Access type of the parameter.</param>
        public CsharpScriptParam(string paramName, string type, GH_ParamAccess accessType)
        {
            ParamName = paramName;
            Type = type;
            AccessType = accessType;
        }

        public CsharpScriptParam()
        {
                
        }
    }

    /// <summary>
    /// Class responsible for parsing and manipulating Grasshopper C# scripts.
    /// </summary>
    public class CSharpScriptParser
    {
        /// <summary>
        /// The RunScript method extracted from the script.
        /// Contains only the method declaration, its body, and associated comments.
        /// Excludes any regions or unrelated comments.
        /// </summary>
        public string RunScriptMethod { get; private set; }

        /// <summary>
        /// Concatenated non-RunScript methods extracted from the script.
        /// Each method is separated by one blank line.
        /// </summary>
        public string NonRunScriptMethods { get; private set; }

        /// <summary>
        /// List of all using directives present in the script.
        /// </summary>
        public List<string> UsingStatements { get; private set; }

        /// <summary>
        /// List of input parameters for the RunScript method.
        /// </summary>
        public List<CsharpScriptParam> RunScriptInputParams { get; private set; }

        /// <summary>
        /// List of output parameters for the RunScript method.
        /// </summary>
        public List<CsharpScriptParam> RunScriptOutputParams { get; private set; }

        // Internal syntax tree and root for processing
        private SyntaxTree SyntaxTree;
        private CompilationUnitSyntax Root;

        /// <summary>
        /// Initializes a new instance of the CSharpScriptParser class.
        /// Parses the provided C# script and extracts relevant components.
        /// </summary>
        /// <param name="csharpText">The C# script as a string.</param>
        public CSharpScriptParser(string csharpText)
        {
            UsingStatements = new List<string>();
            RunScriptInputParams = new List<CsharpScriptParam>();
            RunScriptOutputParams = new List<CsharpScriptParam>();

            // Parse the C# script using Roslyn
            SyntaxTree = CSharpSyntaxTree.ParseText(csharpText);
            Root = SyntaxTree.GetCompilationUnitRoot();

            // Extract using statements
            ParseUsings();

            // Extract methods
            ParseMethods();
        }

        /// <summary>
        /// Parses and extracts all using directives from the script.
        /// </summary>
        private void ParseUsings()
        {
            // Select all using directives and add their string representations
            var usings = Root.Usings.Select(u => u.ToString().Trim()).ToList();
            UsingStatements.AddRange(usings);
        }

        /// <summary>
        /// Parses and extracts the RunScript method and all non-RunScript methods.
        /// </summary>
        private void ParseMethods()
        {
            // Locate the first class declaration (assumes single class)
            var classNode = Root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (classNode == null)
                return;

            // Find all method declarations within the class
            var methods = classNode.DescendantNodes().OfType<MethodDeclarationSyntax>();

            StringBuilder nonRunScriptBuilder = new StringBuilder();

            foreach (var method in methods)
            {
                if (method.Identifier.Text == "RunScript")
                {
                    // Extract only the RunScript method, including its leading comments
                    RunScriptMethod = ExtractRunScriptMethod(method);
                    ParseRunScriptParameters(method);
                }
                else
                {
                    // Concatenate non-RunScript methods with one blank line between them
                    nonRunScriptBuilder.AppendLine(method.NormalizeWhitespace().ToFullString());
                    nonRunScriptBuilder.AppendLine(); // Add one blank line
                }
            }

            // Trim any trailing whitespace
            NonRunScriptMethods = nonRunScriptBuilder.ToString().Trim();
        }

        /// <summary>
        /// Extracts the RunScript method including its leading comments but excluding regions.
        /// </summary>
        /// <param name="method">The RunScript method syntax node.</param>
        /// <returns>The RunScript method as a string with its associated comments.</returns>
        private string ExtractRunScriptMethod(MethodDeclarationSyntax method)
        {
            // Get leading trivia (comments, whitespace, regions, etc.)
            var leadingTrivia = method.GetLeadingTrivia();

            // Filter out region directives and include only comments and whitespace
            var relevantTrivia = leadingTrivia.Where(trivia =>
                trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
                trivia.IsKind(SyntaxKind.EndOfLineTrivia) ||
                trivia.IsKind(SyntaxKind.WhitespaceTrivia));

            // Combine the relevant trivia into a single string
            var triviaString = string.Concat(relevantTrivia.Select(trivia => trivia.ToFullString()));

            // Combine the trivia with the method declaration
            var methodString = triviaString + method.NormalizeWhitespace().ToFullString();

            return methodString;
        }

        /// <summary>
        /// Parses the parameters of the RunScript method and categorizes them as input or output.
        /// </summary>
        /// <param name="runScriptMethod">The RunScript method syntax node.</param>
        private void ParseRunScriptParameters(MethodDeclarationSyntax runScriptMethod)
        {
            var parameters = runScriptMethod.ParameterList.Parameters;

            foreach (var param in parameters)
            {
                string paramName = param.Identifier.Text;
                string paramType = param.Type.ToString();

                // Determine if the parameter is an output parameter based on modifiers
                if (param.Modifiers.Any(SyntaxKind.RefKeyword) || param.Modifiers.Any(SyntaxKind.OutKeyword))
                {
                    // Clean the type by removing wrappers like List<> or DataTree<>
                    string cleanType = CleanType(paramType);
                    RunScriptOutputParams.Add(new CsharpScriptParam(paramName, cleanType, GH_ParamAccess.item));
                }
                else
                {
                    // Determine the access type based on the parameter type
                    var accessType = DetermineAccessType(paramType);
                    string cleanType = CleanType(paramType);
                    RunScriptInputParams.Add(new CsharpScriptParam(paramName, cleanType, accessType));
                }
            }
        }

        /// <summary>
        /// Cleans the type string by removing wrappers like List<> or DataTree<>.
        /// </summary>
        /// <param name="type">The original type string.</param>
        /// <returns>The cleaned type string.</returns>
        private string CleanType(string type)
        {
            if (type.StartsWith("List<") && type.EndsWith(">"))
            {
                return type.Substring(5, type.Length - 6);
            }
            if (type.StartsWith("DataTree<") && type.EndsWith(">"))
            {
                return type.Substring(9, type.Length - 10);
            }
            return type;
        }

        /// <summary>
        /// Determines the GH_ParamAccess based on the parameter type.
        /// </summary>
        /// <param name="type">The parameter type string.</param>
        /// <returns>The corresponding GH_ParamAccess enum value.</returns>
        private GH_ParamAccess DetermineAccessType(string type)
        {
            if (type.StartsWith("List<"))
                return GH_ParamAccess.list;
            if (type.StartsWith("DataTree<"))
                return GH_ParamAccess.tree;
            return GH_ParamAccess.item;
        }

        /// <summary>
        /// Updates an existing C# script by adding missing using statements,
        /// replacing the RunScript method, and updating non-RunScript methods.
        /// Other parts of the script remain unaffected.
        /// This method strictly handles pure C# scripts without considering markdown.
        /// </summary>
        /// <param name="existingScript">The existing C# script to be updated.</param>
        /// <returns>The updated C# script.</returns>
        public string UpdateScript(string existingScript)
        {
            // Parse the existing script
            var existingTree = CSharpSyntaxTree.ParseText(existingScript);
            var existingRoot = existingTree.GetCompilationUnitRoot();

            // Extract existing using statements
            var existingUsings = existingRoot.Usings.Select(u => u.ToString().Trim()).ToList();

            // Merge using statements without duplicates
            var allUsings = new HashSet<string>(existingUsings);
            foreach (var u in UsingStatements)
            {
                allUsings.Add(u);
            }

            // Reconstruct the using directives
            var usingBuilder = new StringBuilder();
            usingBuilder.AppendLine("#region Usings");
            foreach (var u in allUsings.OrderBy(u => u)) // Optional: sort for consistency
            {
                usingBuilder.AppendLine(u);
            }
            usingBuilder.AppendLine("#endregion");

            // Replace the existing using region in the script
            string updatedScript = existingScript;
            int usingStart = existingScript.IndexOf("#region Usings");
            int usingEnd = existingScript.IndexOf("#endregion", usingStart) + "#endregion".Length;

            if (usingStart != -1 && usingEnd != -1)
            {
                // Replace the existing using region
                updatedScript = existingScript.Remove(usingStart, usingEnd - usingStart);
                updatedScript = updatedScript.Insert(usingStart, usingBuilder.ToString());
            }
            else
            {
                // If no using region found, prepend the usings
                updatedScript = usingBuilder.ToString() + Environment.NewLine + existingScript;
            }

            // Parse the updated script
            var updatedTree = CSharpSyntaxTree.ParseText(updatedScript);
            var updatedRoot = updatedTree.GetCompilationUnitRoot();

            // Locate the class declaration
            var classNode = updatedRoot.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (classNode == null)
                return updatedScript; // If no class found, return as is

            // Find the existing RunScript method
            var existingRunScriptMethod = classNode.DescendantNodes().OfType<MethodDeclarationSyntax>()
                                                   .FirstOrDefault(m => m.Identifier.Text == "RunScript");

            if (existingRunScriptMethod != null && !string.IsNullOrEmpty(RunScriptMethod))
            {
                // Parse the new RunScript method
                var newRunScriptSyntax = CSharpSyntaxTree.ParseText(RunScriptMethod).GetRoot()
                                                     .DescendantNodes().OfType<MethodDeclarationSyntax>().First();

                // Replace the existing RunScript method with the new one
                updatedRoot = updatedRoot.ReplaceNode(existingRunScriptMethod, newRunScriptSyntax);
            }
            else if (!string.IsNullOrEmpty(RunScriptMethod))
            {
                // If no existing RunScript method, add it to the class
                var newRunScriptSyntax = CSharpSyntaxTree.ParseText(RunScriptMethod).GetRoot()
                                                     .DescendantNodes().OfType<MethodDeclarationSyntax>().First();

                // Append the new RunScript method to the class
                var newClassNode = classNode.AddMembers(newRunScriptSyntax);

                // Replace the old class node with the new one in the root
                updatedRoot = updatedRoot.ReplaceNode(classNode, newClassNode);
            }

            // Find all existing non-RunScript methods
            var existingNonRunScriptMethods = classNode.DescendantNodes().OfType<MethodDeclarationSyntax>()
                                                     .Where(m => m.Identifier.Text != "RunScript").ToList();

            // Remove existing non-RunScript methods
            foreach (var method in existingNonRunScriptMethods)
            {
                updatedRoot = updatedRoot.RemoveNode(method, SyntaxRemoveOptions.KeepNoTrivia);
            }

            // Add the updated non-RunScript methods
            if (!string.IsNullOrEmpty(NonRunScriptMethods))
            {
                // Parse the concatenated non-RunScript methods
                var nonRunScriptSyntax = SyntaxFactory.ParseMemberDeclaration(NonRunScriptMethods)
                                                     as MemberDeclarationSyntax;

                if (nonRunScriptSyntax != null)
                {
                    // Add the non-RunScript methods to the class
                    var updatedClassNode = updatedRoot.DescendantNodes().OfType<ClassDeclarationSyntax>().First()
                                                       .AddMembers(SyntaxFactory.ParseMemberDeclaration(NonRunScriptMethods));

                    // Replace the old class node with the updated one
                    updatedRoot = updatedRoot.ReplaceNode(classNode, updatedClassNode);
                }
            }

            // Return the updated script as a formatted string
            return updatedRoot.NormalizeWhitespace().ToFullString();
        }
    }
}

