using Grasshopper.Kernel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ChatGHGPT.GH
{
    // ------------------------------------------------------------------------
    // Main parser class
    // ------------------------------------------------------------------------
    public class CSharpScriptParserV2
    {
        /// <summary>
        /// The extracted RunScript method (code + comments). Empty if none found.
        /// </summary>
        public string RunScriptMethod { get; private set; } = string.Empty;

        /// <summary>
        /// Any non-RunScript methods found in the same block. 
        /// Each method is concatenated with one blank line separating them.
        /// </summary>
        public string NonRunScriptMethods { get; private set; } = string.Empty;

        /// <summary>
        /// The list of 'using' statements found in the relevant code block.
        /// </summary>
        public List<string> UsingStatements { get; private set; } = new List<string>();

        /// <summary>
        /// The list of input parameters from the RunScript method signature.
        /// </summary>
        public List<CsharpScriptParam> InputParams { get; private set; } = new List<CsharpScriptParam>();

        /// <summary>
        /// The list of output parameters from the RunScript method signature (ref parameters).
        /// </summary>
        public List<CsharpScriptParam> OutputParams { get; private set; } = new List<CsharpScriptParam>();

        /// <summary>
        /// Constructs a parser and immediately parses the input text.
        /// </summary>
        /// <param name="csharpText">Could be plain C# or Markdown with multiple code blocks.</param>
        public CSharpScriptParserV2(string csharpText)
        {
            ParseInputText(csharpText);
        }

        // --------------------------------------------------------------------
        // Public method to update an existing script using this parser's
        // parsed code & parameters.
        // --------------------------------------------------------------------
        /// <summary>
        /// Updates an existing Grasshopper C# script with the parsed RunScript method, non-RunScript methods,
        /// and any missing using statements. It preserves all other content in the existing script.
        /// </summary>
        /// <param name="existingCode">The existing Grasshopper C# code to update.</param>
        /// <returns>The updated script text.</returns>
        public string UpdateScript(string existingCode)
        {
            // 1. Parse the existing code with Roslyn to find:
            //    - Using statements
            //    - Where the RunScript method is
            //    - Where the non-RunScript methods are
            SyntaxTree oldTree = CSharpSyntaxTree.ParseText(existingCode);
            var root = oldTree.GetRoot() as CompilationUnitSyntax;
            if (root == null)
                return existingCode; // fallback if parse fails

            // -------------------------------------------------------------
            // 2. Collect existing 'using' statements
            // -------------------------------------------------------------
            var existingUsings = root.Usings
                .Select(u => u.ToFullString().Trim())
                .ToList();

            // 3. Add any missing using statements from this parser
            var mergedUsings = new List<string>(existingUsings);
            foreach (var newUsing in UsingStatements)
            {
                // e.g. "using System;" -> match by string ignoring trailing semicolon spaces
                if (!mergedUsings.Any(u => NormalizeUsing(u).Equals(NormalizeUsing(newUsing), StringComparison.OrdinalIgnoreCase)))
                {
                    mergedUsings.Add(newUsing);
                }
            }

            // Helper function to unify "using System;" and "using System ;" etc.
            string NormalizeUsing(string usingLine)
            {
                return usingLine.Replace(" ", "").Trim().TrimEnd(';') + ";";
            }

            // -------------------------------------------------------------
            // 4. Build a new list of members for the updated class/module
            //    We will remove old RunScript & old non-RunScript methods
            //    and then insert our new ones in their place.
            // -------------------------------------------------------------
            // For simplicity, let's just do a textual approach for methods. 
            // Alternatively, we could reconstruct the syntax tree carefully. 
            // But we keep it simpler for demonstration.

            string updatedCode = existingCode;

            // Remove old RunScript method if it exists
            // We'll do it by a naive approach: we parse method declarations and 
            // remove the first method named "RunScript" from the text. Then we'll 
            // remove all other methods that are inside the same class but are not named "RunScript".
            // Finally, we'll insert the new methods. This approach can be made more robust, but 
            // serves as an illustration.

            // Parse the top-level class or classes
            var methodNodes = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .ToList();

            // Collect textual spans for old methods
            var spansToRemove = new List<TextSpan>();

            // 4.1 Find the first RunScript method (if any) and mark it for removal.
            var oldRunScriptNode = methodNodes.FirstOrDefault(m => m.Identifier.Text == "RunScript");
            if (oldRunScriptNode != null)
            {
                spansToRemove.Add(oldRunScriptNode.FullSpan);
            }

            // 4.2 Remove any other methods that are in the same "Script_Instance" or same class
            //     to replicate "replace all non-RunScript methods" approach.
            //     If you only want to remove user code, you might refine logic here.
            //     For demonstration, let's remove all methods from the same class that
            //     contained the first RunScript. If there's no RunScript, we remove none.
            if (oldRunScriptNode != null)
            {
                var parentClass = oldRunScriptNode.Parent as ClassDeclarationSyntax;
                if (parentClass != null)
                {
                    var otherMethodsInClass = parentClass.Members
                        .OfType<MethodDeclarationSyntax>()
                        .Where(m => m != oldRunScriptNode)
                        .ToList();

                    foreach (var om in otherMethodsInClass)
                        spansToRemove.Add(om.FullSpan);
                }
            }

            // 4.3 Remove them from the text (from last span to first to avoid offset complications)
            var orderedSpans = spansToRemove.OrderByDescending(s => s.Start).ToList();
            foreach (var sp in orderedSpans)
            {
                updatedCode = updatedCode.Remove(sp.Start, sp.Length);
            }

            // 5. Insert the new RunScript + Non-RunScript methods if we actually have them
            // We'll try to find a nice insertion point: after the last using. 
            // Or specifically after the class open brace. You can refine as needed.

            // Re-parse after removal
            oldTree = CSharpSyntaxTree.ParseText(updatedCode);
            root = oldTree.GetRoot() as CompilationUnitSyntax;

            if (!string.IsNullOrWhiteSpace(RunScriptMethod) || !string.IsNullOrWhiteSpace(NonRunScriptMethods))
            {
                // Find the first class open brace as a naive insertion point
                var classNode = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                if (classNode != null)
                {
                    // We'll insert right after the class open brace
                    var classOpenBrace = classNode.OpenBraceToken.FullSpan.End;

                    // Build the string we want to insert
                    var toInsert = new StringBuilder();

                    // Possibly add a newline to separate from prior code
                    toInsert.AppendLine();

                    if (!string.IsNullOrWhiteSpace(RunScriptMethod))
                    {
                        toInsert.AppendLine(RunScriptMethod.Trim());
                        toInsert.AppendLine();
                    }
                    if (!string.IsNullOrWhiteSpace(NonRunScriptMethods))
                    {
                        // Trim extra whitespace, then separate by blank line
                        toInsert.AppendLine(NonRunScriptMethods.Trim());
                        toInsert.AppendLine();
                    }

                    updatedCode = updatedCode.Insert(classOpenBrace, toInsert.ToString());
                }
            }

            // 6. Rebuild the using region at the top with merged using statements
            // We'll remove all old usings from the text and replace them with the merged set.
            oldTree = CSharpSyntaxTree.ParseText(updatedCode);
            root = oldTree.GetRoot() as CompilationUnitSyntax;

            if (root == null)
                return updatedCode; // fallback

            var topUsingsSpan = new TextSpan(0, 0);
            var oldUsings = root.Usings;
            if (oldUsings.Count > 0)
            {
                // from first using start to last using end
                var start = oldUsings.First().FullSpan.Start;
                var end = oldUsings.Last().FullSpan.End;
                var length = end - start;
                topUsingsSpan = new TextSpan(start, length);
            }

            // merged usings text
            var usingBlock = string.Join(Environment.NewLine, mergedUsings) + Environment.NewLine;
            var finalCode = updatedCode;
            if (topUsingsSpan.Length > 0)
            {
                // remove old usings
                finalCode = finalCode.Remove(topUsingsSpan.Start, topUsingsSpan.Length);
                // insert new
                finalCode = finalCode.Insert(topUsingsSpan.Start, usingBlock);
            }
            else
            {
                // no existing usings, just prepend
                finalCode = usingBlock + Environment.NewLine + finalCode;
            }

            return finalCode;
        }

        // --------------------------------------------------------------------
        // Private methods
        // --------------------------------------------------------------------

        /// <summary>
        /// Main dispatcher that extracts code blocks (if markdown). 
        /// Once a RunScript is found in a code block, we parse that block
        /// and ignore subsequent blocks.
        /// If no code blocks are found, we parse the entire text as C#.
        /// </summary>
        /// <param name="rawText"></param>
        private void ParseInputText(string rawText)
        {
            var codeBlocks = ExtractCSharpCodeBlocks(rawText);

            // If no code blocks found, treat entire text as one block
            if (codeBlocks.Count == 0)
                codeBlocks.Add(rawText);

            // Parse each block in order. Stop if we found RunScript.
            foreach (var block in codeBlocks)
            {
                if (ParseOneCodeBlock(block))
                    break; // we found a RunScript, so ignore subsequent blocks
            }
        }

        /// <summary>
        /// Extracts triple-backtick ```csharp ...``` code blocks from a Markdown text.
        /// Returns them as a list of strings. Case-insensitive for "csharp" or "cs" etc.
        /// </summary>
        private List<string> ExtractCSharpCodeBlocks(string markdown)
        {
            var codeBlocks = new List<string>();

            // Regex that matches ```csharp ... ``` or ```cs ... ``` etc.
            // We'll be somewhat flexible. We capture group 1 as the code inside.
            var pattern = @"```(?:csharp|cs|CSharp|C#|C)?(.*?)```";
            // Single-line s-dot matches won't suffice, so we use Singleline mode or use RegexOptions.Singleline:
            var matches = Regex.Matches(markdown, pattern, RegexOptions.Singleline);

            foreach (Match m in matches)
            {
                // Group 1 is the code inside the triple backticks
                var code = m.Groups[1].Value;
                codeBlocks.Add(code);
            }

            return codeBlocks;
        }

        /// <summary>
        /// Parses a single block of C# code. If we find a RunScript method, we store it,
        /// as well as the non-run-script methods and using statements, and return true.
        /// Otherwise, we store partial data but keep looking in further blocks.
        /// </summary>
        private bool ParseOneCodeBlock(string code)
        {
            // Parse with Roslyn
            SyntaxTree tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot() as CompilationUnitSyntax;
            if (root == null) return false;

            // Gather using statements
            var usings = root.Usings.Select(u => u.ToFullString().Trim()).ToList();

            // Gather all method declarations
            var methodNodes = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .ToList();

            // Attempt to find first "RunScript" method
            var runScriptNode = methodNodes.FirstOrDefault(m => m.Identifier.Text == "RunScript");
            if (runScriptNode == null)
            {
                // No RunScript in this block, but let's keep track of the usings
                // in case we never find RunScript anywhere.
                UsingStatements.AddRange(usings);
                return false;
            }

            // We found a RunScript. We'll gather using statements from THIS block,
            // set the RunScript method, parse its parameters, and gather the other methods.
            UsingStatements.AddRange(usings);

            // 1) Extract the RunScript method text (including comments, but ignoring region directives).
            //    We'll rely on the node's leading/trailing trivia. Then remove region lines if needed.
            this.RunScriptMethod = ExtractMethodText(runScriptNode);

            // 2) Parse the parameters for run script
            ParseRunScriptParameters(runScriptNode);

            // 3) Extract all other methods from the same block that are not "RunScript"
            var nonRunScriptNodes = methodNodes
                .Where(m => m != runScriptNode)
                .ToList();

            // Concatenate their text with a blank line between
            var nonRunScriptSb = new StringBuilder();
            bool first = true;
            foreach (var node in nonRunScriptNodes)
            {
                if (!first) nonRunScriptSb.AppendLine();
                first = false;
                nonRunScriptSb.AppendLine(ExtractMethodText(node));
            }
            this.NonRunScriptMethods = nonRunScriptSb.ToString().TrimEnd();

            return true; // we found our run script in this block, so no need to look further
        }

        /// <summary>
        /// Extracts the full text (including comments) of a MethodDeclarationSyntax,
        /// removing any #region or #endregion lines from the leading trivia.
        /// </summary>
        private string ExtractMethodText(MethodDeclarationSyntax methodNode)
        {
            // The simplest approach is to use the node's .ToFullString().
            // Then remove #region/#endregion lines if desired.

            var fullText = methodNode.ToFullString();

            // Optionally remove region lines if present:
            var lines = fullText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Where(l => !l.TrimStart().StartsWith("#region", StringComparison.OrdinalIgnoreCase)
                         && !l.TrimStart().StartsWith("#endregion", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            // Rejoin
            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// Parses the parameters on the RunScript method to fill InputParams and OutputParams.
        /// </summary>
        private void ParseRunScriptParameters(MethodDeclarationSyntax runScript)
        {
            // Example:
            //    private void RunScript(object x, List<object> y, ref object a)
            //
            // We'll treat normal parameters as input, and 'ref' parameters as output.
            // We'll parse GH_ParamAccess from whether the type is object, List<>, DataTree<>, etc.
            this.InputParams.Clear();
            this.OutputParams.Clear();

            foreach (var p in runScript.ParameterList.Parameters)
            {
                // Check if it's ref, out, or neither
                bool isRef = p.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.RefKeyword))
                          || p.Modifiers.Any(m => m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.OutKeyword));

                // Get the parameter name
                var paramName = p.Identifier.Text;

                // Get the type name as written
                var typeSyntax = p.Type;
                if (typeSyntax == null) continue; // fallback
                string fullTypeName = typeSyntax.ToString().Trim();

                // Determine GH_ParamAccess
                var (baseType, access) = DetermineGhParamAccess(fullTypeName);

                var scriptParam = new CsharpScriptParam(paramName, baseType, access);

                if (isRef)
                {
                    // It's an output param
                    this.OutputParams.Add(scriptParam);
                }
                else
                {
                    // It's an input param
                    this.InputParams.Add(scriptParam);
                }
            }
        }

        /// <summary>
        /// Heuristic to figure out GH_ParamAccess and base type from a C# type name,
        /// e.g. "object" -> ( "object", GH_ParamAccess.item )
        ///      "List<object>" -> ( "object", GH_ParamAccess.list )
        ///      "DataTree<object>" -> ( "object", GH_ParamAccess.tree )
        ///      "List<int>" -> ( "int", GH_ParamAccess.list )
        ///      etc.
        /// </summary>
        private (string baseType, GH_ParamAccess access) DetermineGhParamAccess(string fullTypeName)
        {
            // Trim generics, check if starts with List<> or DataTree<>
            if (Regex.IsMatch(fullTypeName, @"^List\s*<(.+)>$"))
            {
                // It's a list
                var innerType = Regex.Replace(fullTypeName, @"^List\s*<(.*)>$", "$1").Trim();
                return (innerType, GH_ParamAccess.list);
            }
            else if (Regex.IsMatch(fullTypeName, @"^DataTree\s*<(.+)>$"))
            {
                // It's a data tree
                var innerType = Regex.Replace(fullTypeName, @"^DataTree\s*<(.*)>$", "$1").Trim();
                return (innerType, GH_ParamAccess.tree);
            }
            else
            {
                // Otherwise, assume single item
                return (fullTypeName, GH_ParamAccess.item);
            }
        }
    }
}

