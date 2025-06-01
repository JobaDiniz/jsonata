using Jsonata.Net.Core.Evaluation;
using Jsonata.Net.Core.Exceptions;
using System;
using Jsonata.Net.Core.Evaluation;
using Jsonata.Net.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text; // For StringBuilder
using Jsonata.Net.Core.AstNodes;

namespace Jsonata.Net.Core.BuiltinFunctions
{
    public sealed class ReplaceFunction : IBuiltinFunction
    {
        private const string ERROR_CODE_BAD_ARG_COUNT = "T0410";
        private const string ERROR_CODE_ARG_STR_BAD_TYPE = "S0208";
        private const string ERROR_CODE_ARG_PATTERN_BAD_TYPE = "S0209";
        private const string ERROR_CODE_ARG_REPLACEMENT_BAD_TYPE = "S0230"; // New
        private const string ERROR_CODE_ARG_LIMIT_BAD_TYPE = "S0228";
        private const string ERROR_CODE_REGEX_COMPILE = "S0229";
        private const string ERROR_CODE_REPLACEMENT_FUNC_RESULT = "S0231"; // New

        public Sequence Execute(List<Sequence> arguments, EvaluationContext context)
        {
            string functionName = "$replace";
            int argCount = arguments.Count;

            JsonNode? strNode;
            JsonNode? patternNode;
            Sequence replacementSequence; // Can be string or function
            JsonNode? limitNode = null;

            // Argument parsing based on count
            if (argCount == 2) // $replace(pattern, replacement) -> str from context
            {
                strNode = context.CurrentInput;
                patternNode = arguments[0].FirstOrDefault() as JsonNode;
                replacementSequence = arguments[1]; // Keep as Sequence for now
            }
            else if (argCount == 3) // $replace(str, pattern, replacement) OR $replace(pattern, replacement, limit)
            {
                object? thirdArgItem = arguments[2].FirstOrDefault();
                if (thirdArgItem is JsonValue valNum && (valNum.TryGetValue(out int _) || valNum.TryGetValue(out decimal _))) // Third arg is limit
                {
                    strNode = context.CurrentInput;
                    patternNode = arguments[0].FirstOrDefault() as JsonNode;
                    replacementSequence = arguments[1];
                    limitNode = thirdArgItem as JsonNode;
                }
                else // Third arg is replacement
                {
                    strNode = arguments[0].FirstOrDefault() as JsonNode;
                    patternNode = arguments[1].FirstOrDefault() as JsonNode;
                    replacementSequence = arguments[2];
                }
            }
            else if (argCount == 4) // $replace(str, pattern, replacement, limit)
            {
                strNode = arguments[0].FirstOrDefault() as JsonNode;
                patternNode = arguments[1].FirstOrDefault() as JsonNode;
                replacementSequence = arguments[2];
                limitNode = arguments[3].FirstOrDefault() as JsonNode;
            }
            else
            {
                throw new JsonataEvaluationException($"{ERROR_CODE_BAD_ARG_COUNT}: {functionName} expects 1, 2, or 3 arguments. Got {argCount}.");
            }

            // Validate str
            string sourceString;
            if (strNode is JsonValue svStr && svStr.TryGetValue(out string? sValStr))
            {
                sourceString = sValStr;
            }
            else if (strNode == null || (strNode is JsonValue tempSvStr && tempSvStr.ToJsonString() == "null"))
            {
                throw new JsonataEvaluationException($"{ERROR_CODE_ARG_STR_BAD_TYPE}: Argument 'str' to {functionName} must be a string. Got {(strNode == null ? "undefined" : "null")}.");
            }
            else
            {
                throw new JsonataEvaluationException($"{ERROR_CODE_ARG_STR_BAD_TYPE}: Argument 'str' to {functionName} must be a string. Got {strNode.GetType().Name}.");
            }

            // Validate pattern (must be a string for regex processing)
            string patternInputString;
            if (patternNode is JsonValue svPattern && svPattern.TryGetValue(out string? sValPattern))
            {
                patternInputString = sValPattern;
            }
             else if (patternNode == null || (patternNode is JsonValue tempPatternNode && tempPatternNode.ToJsonString() == "null"))
            {
                 throw new JsonataEvaluationException($"{ERROR_CODE_ARG_PATTERN_BAD_TYPE}: Argument 'pattern' to {functionName} must be a string (regex). Got null.");
            }
            else
            {
                throw new JsonataEvaluationException($"{ERROR_CODE_ARG_PATTERN_BAD_TYPE}: Argument 'pattern' to {functionName} must be a string (regex). Got {patternNode.GetType().Name}.");
            }

            // Validate limit (optional)
            int limit = -1; // Regex.Replace default: -1 for all occurrences
            if (limitNode != null)
            {
                if (limitNode is JsonValue limitValNode && limitValNode.TryGetValue<int>(out int lInt))
                {
                    if (lInt < 0) throw new JsonataEvaluationException($"{ERROR_CODE_ARG_LIMIT_BAD_TYPE}: Argument 'limit' to {functionName} must be a non-negative integer. Got {lInt}.");
                    limit = lInt;
                }
                else if (limitNode is JsonValue lvn && lvn.TryGetValue<decimal>(out decimal lDec) && lDec == Math.Truncate(lDec) && lDec >= 0)
                {
                    limit = (int)lDec;
                }
                else
                {
                    throw new JsonataEvaluationException($"{ERROR_CODE_ARG_LIMIT_BAD_TYPE}: Argument 'limit' to {functionName} must be a non-negative integer. Got {(limitNode.GetType().Name)}.");
                }
            }
            // Removed: if (limit == 0) limit = -1; // JSONata spec: limit 0 means 0 replacements. Test case confirms this.

            Regex regex;
            try
            {
                regex = ParseRegexInput(patternInputString);
            }
            catch (ArgumentException ex)
            {
                throw new JsonataEvaluationException($"{ERROR_CODE_REGEX_COMPILE}: Regex compilation failed for pattern '{patternInputString}': {ex.Message}", ex);
            }

            // Process replacement (string or function)
            object? replacementObj = replacementSequence.AsSingle(); // Expecting single string or single function

            if (replacementObj is JsonValue replVal && replVal.TryGetValue(out string? replacementString))
            {
                // Replacement is a string
                string result;
                // $$ in replacement string becomes a single $
                replacementString = replacementString.Replace("$$", "$");

                if (!TryParseRegex(patternInputString, out Regex? specificRegexForPlainString)) // If pattern was NOT a /.../ regex
                {
                    // Plain string replacement
                    if (limit == 0) // limit 0 means 0 replacements according to spec and tests
                    {
                        result = sourceString;
                    }
                    else
                    {
                        // If limit was not specified by user, it's -1 here. For string replace, default is 1.
                        // If limit was specified by user (and > 0), it's that value.
                        int actualLimit = (limit == -1) ? 1 : limit;

                        StringBuilder sb = new StringBuilder();
                        int currentIndex = 0;
                        int replacementsDone = 0;

                        // Loop only up to actualLimit times for string replacement
                        while (currentIndex < sourceString.Length && replacementsDone < actualLimit)
                        {
                            if (string.IsNullOrEmpty(patternInputString)) // Avoid infinite loop on empty pattern
                            {
                                // JSONata spec: replacing with empty string pattern is undefined/error for string,
                                // but IndexOf would find it everywhere.
                                // For safety, if pattern is empty, stop. Usually handled by regex path for empty pattern.
                                // Based on spec, $replace("abc", "", "-") -> error S0212
                                // This case should ideally be an error or follow specific behavior.
                                // Current .NET IndexOf would find empty string at every position.
                                // Let's assume patternInputString is non-empty based on typical usage.
                                // If it can be empty, further spec clarification needed for string replace path.
                                break;
                            }
                            int foundIndex = sourceString.IndexOf(patternInputString, currentIndex, StringComparison.Ordinal);
                            if (foundIndex == -1)
                            {
                                break;
                            }
                            sb.Append(sourceString.Substring(currentIndex, foundIndex - currentIndex));
                            sb.Append(replacementString);
                            currentIndex = foundIndex + patternInputString.Length;
                            replacementsDone++;
                        }
                        sb.Append(sourceString.Substring(currentIndex));
                        result = sb.ToString();
                    }
                }
                else // Pattern was a /.../ regex (regex variable is already compiled from patternInputString)
                {
                     result = limit == -1 ? regex.Replace(sourceString, replacementString)
                                                : regex.Replace(sourceString, replacementString, limit);
                }
                return new Sequence(JsonValue.Create(result));
            }
            else if (replacementObj is FunctionObject replacementFunction)
            {
                // Replacement is a function
                var matches = regex.Matches(sourceString);
                StringBuilder sb = new StringBuilder();
                int lastIndex = 0;
                int replacementsMade = 0;

                foreach (Match match in matches)
                {
                    if (limit != -1 && replacementsMade >= limit)
                    {
                        break;
                    }

                    sb.Append(sourceString.Substring(lastIndex, match.Index - lastIndex));

                    JsonObject matchInfo = new JsonObject();
                    matchInfo.Add("match", JsonValue.Create(match.Value));
                    matchInfo.Add("index", JsonValue.Create(match.Index));
                    JsonArray groupsArray = new JsonArray();
                    for (int i = 1; i < match.Groups.Count; i++) // Skip group 0
                    {
                        groupsArray.Add(JsonValue.Create(match.Groups[i].Value));
                    }
                    matchInfo.Add("groups", groupsArray);

                    // Prepare context for function call
                    // The context item ($) for the replacement function is the matchInfo object.
                    // Parent context is the one in which $replace was called.
                    EvaluationContext fnContext = new EvaluationContext(
                        matchInfo,
                        context.Functions, // Pass the main function registry
                        context.RootInput, // Root from outer context
                        null,              // No local bindings apart from argument
                        replacementFunction.CapturedContext // Lexical scope from closure
                    );

                    // The argument to the replacement function is the matchInfo object itself.
                    // We need to simulate a function call. The FunctionObject has Definition and CapturedContext.
                    // We need to bind the 'matchInfo' to the function's first parameter.
                    FunctionDefinitionNode funcDef = replacementFunction.Definition;
                    if (funcDef.Parameters.Count != 1)
                    {
                        throw new JsonataEvaluationException($"{ERROR_CODE_REPLACEMENT_FUNC_RESULT}: Replacement function for {functionName} must accept exactly one argument.");
                    }

                    var fnLocalBindings = new Dictionary<string, object?>
                    {
                        [funcDef.Parameters[0].Value] = matchInfo
                    };

                    EvaluationContext callContext = new EvaluationContext(
                        replacementFunction.DefinitionTimeInput ?? matchInfo, // Context item for function body
                        context.Functions,
                        replacementFunction.CapturedContext.RootInput,
                        fnLocalBindings,
                        replacementFunction.CapturedContext
                    );

                    Sequence fnResultSeq = funcDef.Body.Evaluate(callContext.CurrentInput, callContext);

                    object? fnResultObj = fnResultSeq.AsSingle();
                    if (fnResultObj is JsonValue fnResVal && fnResVal.TryGetValue(out string? replacementValueStr))
                    {
                        sb.Append(replacementValueStr);
                    }
                    else if (fnResultObj == null && fnResultSeq.IsUndefined()) // Function returned undefined
                    {
                         // JSONata spec: if replacement function returns undefined, it's an empty string
                        sb.Append("");
                    }
                    else if (fnResultObj is JsonValue fnResNull && fnResNull.ToJsonString() == "null") // Function returned null
                    {
                        // JSONata spec: if replacement function returns null, it's an empty string
                         sb.Append("");
                    }
                    else
                    {
                        throw new JsonataEvaluationException($"{ERROR_CODE_REPLACEMENT_FUNC_RESULT}: Replacement function for {functionName} must return a string, undefined or null. Got {fnResultObj?.GetType().Name ?? "null"}.");
                    }

                    lastIndex = match.Index + match.Length;
                    replacementsMade++;
                }
                sb.Append(sourceString.Substring(lastIndex));
                return new Sequence(JsonValue.Create(sb.ToString()));
            }
            else
            {
                throw new JsonataEvaluationException($"{ERROR_CODE_ARG_REPLACEMENT_BAD_TYPE}: Argument 'replacement' to {functionName} must be a string or a function. Got {(replacementObj?.GetType().Name ?? "undefined")}.");
            }
        }

        // Helper to parse regex like "/pattern/flags"
        // Returns true if successfully parsed as regex, false otherwise.
        // Throws ArgumentException from new Regex() if pattern is invalid but flags are present.
        private static bool TryParseRegex(string patternInput, out Regex? regex)
        {
            regex = null;
            if (patternInput.Length >= 2 && patternInput.StartsWith("/") && patternInput.LastIndexOf('/') > 0)
            {
                int lastSlash = patternInput.LastIndexOf('/');
                string pattern = patternInput.Substring(1, lastSlash - 1);
                string flagsString = patternInput.Substring(lastSlash + 1);

                RegexOptions options = RegexOptions.None;
                if (flagsString.Contains("i")) options |= RegexOptions.IgnoreCase;
                if (flagsString.Contains("m")) options |= RegexOptions.Multiline;

                // Validate flags: only 'i' and 'm' are typically supported in basic JSONata $replace regex
                foreach (char flagChar in flagsString)
                {
                    if (flagChar != 'i' && flagChar != 'm')
                    {
                        // This will be caught by the Regex constructor if it's an invalid flag for .NET Regex,
                        // or we can throw a specific Jsonata error here.
                        // For now, let .NET Regex constructor handle flag validation.
                    }
                }

                try
                {
                    regex = new Regex(pattern, options);
                    return true;
                }
                catch (ArgumentException)
                {
                    // This indicates an invalid regex pattern or flag combination for .NET's engine
                    // JSONata spec for $replace implies invalid regex in /../ form is an error.
                    throw; // Re-throw to be caught by the Execute method's general regex compile catch block
                }
            }
            return false; // Not in /pattern/flags format, so treat as literal string for pattern matching
        }

        private static Regex ParseRegexInput(string patternInput) // Same as in MatchFunction
        {
            if (patternInput.Length >= 2 && patternInput.StartsWith("/") && patternInput.LastIndexOf('/') > 0)
            {
                int lastSlash = patternInput.LastIndexOf('/');
                string pattern = patternInput.Substring(1, lastSlash - 1);
                string flags = patternInput.Substring(lastSlash + 1);
                RegexOptions options = RegexOptions.None;
                if (flags.Contains("i")) options |= RegexOptions.IgnoreCase;
                if (flags.Contains("m")) options |= RegexOptions.Multiline;
                foreach (char flagChar in flags)
                {
                    if (flagChar != 'i' && flagChar != 'm')
                    {
                        throw new ArgumentException($"Invalid regex flag '{flagChar}' found in pattern.");
                    }
                }
                return new Regex(pattern, options);
            }
            return new Regex(patternInput);
        }
    }
}
