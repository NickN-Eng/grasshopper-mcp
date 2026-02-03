using Grasshopper.Kernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ChatGHGPT.GH
{
    public class GrasshopperScriptParser
    {
        private static readonly Regex UsingRegex = new Regex(
            @"^\s*using\s+[\w\.]+;\s*$",
            RegexOptions.Multiline | RegexOptions.Compiled);

        private static readonly Regex MethodRegex = new Regex(
            // This pattern attempts to capture a method signature and its body (including nested braces).
            // It captures the entire `private/public/.. void XXX(...) { ... }` block.
            @"(?<leading>(public|private|protected|internal)?\s+(static\s+)?(void|[\w\<\>]+)\s+(?<methodName>[\w\d_]+)\s*\([^\)]*\))\s*\{(?<body>(?>[^{}]+|(\{(?<c>)|\}(?<-c>)))*(?(c)(?!)))\}",
            RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex MethodSignatureRegex = new Regex(
            // Matches something like: void RunScript(object x, List<object> y, ref object a)
            @"(public|private|protected|internal)?\s+(static\s+)?(void|[\w\<\>]+)\s+(?<methodName>[\w\d_]+)\s*\((?<params>[^\)]*)\)",
            RegexOptions.Singleline | RegexOptions.Compiled);

        public string OriginalScript { get; }

        public List<string> UsingStatements { get; private set; } = new List<string>();
        public string RunScriptMethod { get; private set; } = string.Empty;
        public string NonRunScriptMethods { get; private set; } = string.Empty;
        public List<CsharpScriptParam> RunScriptInputParams { get; private set; } = new List<CsharpScriptParam>();
        public List<CsharpScriptParam> RunScriptOutputParams { get; private set; } = new List<CsharpScriptParam>();

        public GrasshopperScriptParser(string csharpText)
        {
            OriginalScript = csharpText ?? string.Empty;
            Parse();
        }

        /// <summary>
        /// Parse out all the properties:
        /// - using statements
        /// - runscript method (the first one found)
        /// - non-runscript methods
        /// - runscript input/output parameters
        /// </summary>
        private void Parse()
        {
            // 1. Extract using statements
            ExtractUsingStatements(OriginalScript);

            // 2. Extract methods and specifically pick out the first RunScript method
            ExtractMethods(OriginalScript);
        }

        private void ExtractUsingStatements(string text)
        {
            var matches = UsingRegex.Matches(text);
            foreach (Match m in matches)
            {
                var statement = m.Value.Trim();
                if (!UsingStatements.Contains(statement))
                {
                    UsingStatements.Add(statement);
                }
            }
        }

        private void ExtractMethods(string text)
        {
            var matches = MethodRegex.Matches(text);
            bool foundRunScript = false;

            var nonRunScriptMethodsList = new List<string>();

            foreach (Match match in matches)
            {
                if (!match.Success) continue;

                // Group "methodName"
                string methodName = match.Groups["methodName"].Value;
                // The entire matched method text: signature + { + body + }
                string methodFullText = match.Value;

                // If it's the first "RunScript", parse it and store it
                if (!foundRunScript && methodName.Equals("RunScript", StringComparison.OrdinalIgnoreCase))
                {
                    RunScriptMethod = methodFullText;
                    foundRunScript = true;

                    // Also parse the input and output parameters
                    ExtractRunScriptParameters(methodFullText);
                }
                else
                {
                    // It's not RunScript, so add it to the non-runscript methods
                    nonRunScriptMethodsList.Add(methodFullText);
                }
            }

            // Combine non-RunScript methods with a blank line in between
            NonRunScriptMethods = string.Join("\n\n", nonRunScriptMethodsList);
        }

        /// <summary>
        /// Extracts the parameters from the first RunScript method signature,
        /// populating RunScriptInputParams and RunScriptOutputParams.
        /// </summary>
        /// <param name="methodText">Full text of the RunScript method, including signature and body.</param>
        private void ExtractRunScriptParameters(string methodText)
        {
            // We'll extract just the signature first
            var signatureMatch = MethodSignatureRegex.Match(methodText);
            if (!signatureMatch.Success) return;

            // We have a group named "params" that contains the parameter list
            string paramList = signatureMatch.Groups["params"].Value.Trim();

            if (string.IsNullOrEmpty(paramList)) return;

            // Split by commas carefully
            // A simplistic approach is to just split on commas that are not within angle brackets, etc.
            // For demonstration, let's do a naive split on commas:
            var parts = paramList.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                string trimmedPart = part.Trim();

                // We can detect ref or out, but let's specifically check "ref" for Grasshopper
                bool isRef = trimmedPart.StartsWith("ref ", StringComparison.OrdinalIgnoreCase)
                             || trimmedPart.StartsWith("out ", StringComparison.OrdinalIgnoreCase);

                // Remove ref/out from the part for simpler type parsing
                string paramWithoutRef = trimmedPart.Replace("ref ", "")
                                                   .Replace("out ", "")
                                                   .Trim();

                // Typically the parameter portion looks like: "DataTree<object> z"
                // We can separate the type from the name by looking for the last space
                int lastSpaceIndex = paramWithoutRef.LastIndexOf(' ');
                if (lastSpaceIndex < 0) continue;

                string typePart = paramWithoutRef.Substring(0, lastSpaceIndex).Trim();
                string paramName = paramWithoutRef.Substring(lastSpaceIndex).Trim();

                // paramName might contain a trailing comma if splitting was naive,
                // so let's remove anything that's not valid variable chars:
                paramName = paramName.Trim(',', ';');

                // Check if it's a single item, list, or tree
                var (baseType, access) = DetermineParamType(typePart);

                var param = new CsharpScriptParam
                {
                    ParamName = paramName,
                    AccessType = access,
                    Type = baseType
                };

                // If it's ref, we treat it as an output param
                if (isRef)
                {
                    RunScriptOutputParams.Add(param);
                }
                else
                {
                    RunScriptInputParams.Add(param);
                }
            }
        }

        /// <summary>
        /// Determine base type and GH_ParamAccess from type string, e.g.:
        ///   "object" -> (object, item)
        ///   "List<object>" -> (object, list)
        ///   "DataTree<object>" -> (object, tree)
        ///   "List<double>" -> (double, list)
        /// </summary>
        private (string baseType, GH_ParamAccess accessType) DetermineParamType(string typeString)
        {
            // Very naive approach. You could refine with actual generics parsing or Roslyn.
            if (typeString.StartsWith("DataTree<", StringComparison.OrdinalIgnoreCase))
            {
                // e.g. DataTree<object>
                string inside = ExtractGenericType(typeString);
                if (string.IsNullOrEmpty(inside)) inside = "object";
                return (inside, GH_ParamAccess.tree);
            }
            else if (typeString.StartsWith("List<", StringComparison.OrdinalIgnoreCase))
            {
                // e.g. List<object>
                string inside = ExtractGenericType(typeString);
                if (string.IsNullOrEmpty(inside)) inside = "object";
                return (inside, GH_ParamAccess.list);
            }
            else
            {
                // fallback
                return (typeString, GH_ParamAccess.item);
            }
        }

        private string ExtractGenericType(string typeString)
        {
            // e.g. "DataTree<object>" => "object"
            var match = Regex.Match(typeString, @"<(?<inside>[^>]+)>");
            if (match.Success)
                return match.Groups["inside"].Value.Trim();
            return string.Empty;
        }

        /// <summary>
        /// Given an existing script, this method:
        /// 1) Ensures all parsed using statements are present.
        /// 2) Replaces the entire RunScript method with the newly parsed one.
        /// 3) Replaces all non-RunScript methods with the newly parsed ones (in one block).
        /// Other parts of the script (comments, notes, regions, fields, etc.) are left untouched.
        /// </summary>
        public string UpdateScript(string existingScript)
        {
            if (string.IsNullOrEmpty(existingScript))
                return existingScript;

            // 1) Merge `using` statements
            existingScript = MergeUsingStatements(existingScript);

            // 2) Replace the RunScript method
            if (!string.IsNullOrWhiteSpace(RunScriptMethod))
            {
                existingScript = ReplaceMethod(existingScript, "RunScript", RunScriptMethod);
            }

            // 3) Replace all non-RunScript methods
            if (!string.IsNullOrWhiteSpace(NonRunScriptMethods))
            {
                // One strategy: remove all known non-RunScript methods, then insert them
                // as a single block. Another is to do them individually by name.
                // For demonstration, we’ll do a naive approach:
                existingScript = ReplaceAllNonRunScriptMethods(
                    existingScript,
                    NonRunScriptMethods);
            }

            return existingScript;
        }

        /// <summary>
        /// Merges the using statements from this parser into the existing script.
        /// Only adds new statements if they do not already exist.
        /// </summary>
        private string MergeUsingStatements(string existingScript)
        {
            var existingUsings = new HashSet<string>(
                UsingRegex.Matches(existingScript)
                          .Cast<Match>()
                          .Select(m => m.Value.Trim()));

            var builder = new StringBuilder();

            // Optional: store the top-level lines for manipulations
            var lines = existingScript.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            // We'll try to insert any missing using statements right after the last using we encounter,
            // or at the top if none found.
            // For simplicity, let's just find the last index of a `using ...;` line, then insert after that.

            int lastUsingIndex = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (UsingRegex.IsMatch(lines[i]))
                {
                    lastUsingIndex = i;
                }
            }

            // Build the resulting script lines
            if (lastUsingIndex == -1)
            {
                // No usings found, so insert them at the top
                var additionalUsings = UsingStatements
                    .Where(u => !existingUsings.Contains(u))
                    .ToList();

                if (additionalUsings.Any())
                {
                    // Insert them at the start
                    var allNew = string.Join(Environment.NewLine, additionalUsings);
                    builder.AppendLine(allNew);
                    builder.AppendLine(); // blank line
                }
                // then the rest of the script
                builder.Append(string.Join(Environment.NewLine, lines));
            }
            else
            {
                // We have at least one using
                // We'll reconstruct lines up to lastUsingIndex
                for (int i = 0; i <= lastUsingIndex; i++)
                {
                    builder.AppendLine(lines[i]);
                }

                // Insert missing usings right after the last existing using
                var additionalUsings = UsingStatements
                    .Where(u => !existingUsings.Contains(u))
                    .ToList();

                if (additionalUsings.Any())
                {
                    var allNew = string.Join(Environment.NewLine, additionalUsings);
                    builder.AppendLine(allNew);
                }

                // Then append the rest of the script
                for (int i = lastUsingIndex + 1; i < lines.Length; i++)
                {
                    builder.AppendLine(lines[i]);
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Replaces the first occurrence of the method named 'methodName' in `script`
        /// with `newMethodText`. If none found, it does nothing (or optionally inserts it).
        /// </summary>
        private string ReplaceMethod(string script, string methodName, string newMethodText)
        {
            // We’ll use the same regex approach but specifically for that method name
            // So we do a single method match for "RunScript" or "SomeName".
            string pattern = $@"(public|private|protected|internal)?\s+(static\s+)?(void|[\w\<\>]+)\s+{methodName}\s*\([^\)]*\)\s*\{{(?<body>(?>[^{{}}]+|(\{{(?<c>)|\}}(?<-c>)))*(?(c)(?!)))\}}";

            Regex singleMethodRegex = new Regex(pattern, RegexOptions.Singleline | RegexOptions.Compiled);

            return singleMethodRegex.Replace(script, newMethodText, 1);
        }

        /// <summary>
        /// Naive approach: Remove all "private/public/protected/internal ... { }" blocks 
        /// that are not named RunScript, then replace them with `newMethodsBlock`.
        /// If you want per-method replacements, refine this approach.
        /// </summary>
        private string ReplaceAllNonRunScriptMethods(string script, string newMethodsBlock)
        {
            // A naive approach:
            //  - We find all method blocks (like in our main MethodRegex).
            //  - For each match, if its name is *not* RunScript, remove it.
            //  - Then, at the end, we add the entire new block.
            // This is simplistic, but demonstrates how you might proceed.

            var matches = MethodRegex.Matches(script);
            // We'll remove all non-RunScript methods by storing them and removing
            // from the script with a single pass (to avoid messing up indexes).
            List<(int start, int length)> toRemove = new List<(int start, int length)>();

            foreach (Match match in matches)
            {
                if (!match.Success) continue;
                string name = match.Groups["methodName"].Value;
                if (name.Equals("RunScript", StringComparison.OrdinalIgnoreCase))
                    continue; // do not remove
                // We'll remove this method from the final code
                toRemove.Add((match.Index, match.Length));
            }

            // Sort descending so we don't mess up subsequent indexes
            toRemove = toRemove.OrderByDescending(t => t.start).ToList();

            StringBuilder sb = new StringBuilder(script);
            foreach (var removal in toRemove)
            {
                sb.Remove(removal.start, removal.length);
            }

            // Now we add the `newMethodsBlock` (non-RunScript) *somewhere*. 
            // Typically, you'd want it inside the class that presumably ends with `}`.
            // For simplicity, let's just insert it right before the last `}` in the file
            // that presumably ends the main class.
            // You can refine if your structure is more complicated.

            int lastBraceIndex = sb.ToString().LastIndexOf('}');
            if (lastBraceIndex > 0)
            {
                // Insert a newline before the final brace
                sb.Insert(lastBraceIndex, "\n\n" + newMethodsBlock + "\n");
            }
            else
            {
                // If we can’t find a final brace, just append
                sb.Append("\n\n" + newMethodsBlock + "\n");
            }

            return sb.ToString();
        }
    }
}
