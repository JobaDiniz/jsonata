using Jsonata.Net.Core.Evaluation;
using Jsonata.Net.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Linq;
using System.Text.RegularExpressions;

namespace Jsonata.Net.Core.BuiltinFunctions
{
    public sealed class MatchFunction : IBuiltinFunction
    {
        private const string ERROR_CODE_BAD_ARG_COUNT = "T0410";
        private const string ERROR_CODE_ARG_STR_BAD_TYPE = "S0208"; // Reusing from $split for string arg
        private const string ERROR_CODE_ARG_PATTERN_BAD_TYPE = "S0209"; // Reusing from $split for pattern arg
        private const string ERROR_CODE_ARG_LIMIT_BAD_TYPE = "S0228"; // Reusing from $split for limit arg
        private const string ERROR_CODE_REGEX_COMPILE = "S0229"; // New for regex compilation issues

        public Sequence Execute(List<Sequence> arguments, EvaluationContext context)
        {
            string functionName = "$match";
            int argCount = arguments.Count;

            JsonNode? strNode;
            JsonNode? patternNode;
            JsonNode? limitNode = null;

            if (argCount == 1) // $match(pattern) -> str from context
            {
                strNode = context.CurrentInput;
                patternNode = arguments[0].FirstOrDefault() as JsonNode;
            }
            else if (argCount == 2) // $match(str, pattern) or $match(pattern, limit)
            {
                // To distinguish $match(str, pattern) from $match(pattern, limit):
                // If the second argument is a number, it's (pattern, limit) with str from context.
                // Otherwise, it's (str, pattern).
                object? secondArgItem = arguments[1].FirstOrDefault();
                if (secondArgItem is JsonValue valNum && (valNum.TryGetValue(out int _) || valNum.TryGetValue(out decimal _)))
                {
                    strNode = context.CurrentInput;
                    patternNode = arguments[0].FirstOrDefault() as JsonNode;
                    limitNode = secondArgItem as JsonNode;
                }
                else
                {
                    strNode = arguments[0].FirstOrDefault() as JsonNode;
                    patternNode = arguments[1].FirstOrDefault() as JsonNode;
                }
            }
            else if (argCount == 3) // $match(str, pattern, limit)
            {
                strNode = arguments[0].FirstOrDefault() as JsonNode;
                patternNode = arguments[1].FirstOrDefault() as JsonNode;
                limitNode = arguments[2].FirstOrDefault() as JsonNode;
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
            int? limit = null;
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

            Regex regex;
            try
            {
                regex = ParseRegexInput(patternInputString);
            }
            catch (ArgumentException ex)
            {
                throw new JsonataEvaluationException($"{ERROR_CODE_REGEX_COMPILE}: Regex compilation failed for pattern '{patternInputString}': {ex.Message}", ex);
            }

            MatchCollection matches = regex.Matches(sourceString);
            JsonArray resultArray = new JsonArray();

            int count = 0;
            foreach (Match match in matches)
            {
                if (limit.HasValue && count >= limit.Value)
                {
                    break;
                }

                JsonObject matchObject = new JsonObject();
                matchObject.Add("match", JsonValue.Create(match.Value));
                matchObject.Add("index", JsonValue.Create(match.Index));

                JsonArray groupsArray = new JsonArray();
                // Skip group 0 (the whole match) for "groups" property
                for (int i = 1; i < match.Groups.Count; i++)
                {
                    groupsArray.Add(JsonValue.Create(match.Groups[i].Value));
                }
                matchObject.Add("groups", groupsArray);

                resultArray.Add(matchObject);
                count++;
            }

            return new Sequence((object?)resultArray);
        }

        private static Regex ParseRegexInput(string patternInput)
        {
            // Based on ContainsFunction's helper, but throws on invalid regex pattern per $match spec
            if (patternInput.Length >= 2 && patternInput.StartsWith("/") && patternInput.LastIndexOf('/') > 0)
            {
                int lastSlash = patternInput.LastIndexOf('/');
                string pattern = patternInput.Substring(1, lastSlash - 1);
                string flags = patternInput.Substring(lastSlash + 1);

                RegexOptions options = RegexOptions.None;
                if (flags.Contains("i")) options |= RegexOptions.IgnoreCase;
                if (flags.Contains("m")) options |= RegexOptions.Multiline;
                // Add other flags if JSONata supports them e.g. 's' for Singleline.

                // Any other char in flags part is an error according to JSONata $match spec.
                foreach (char flagChar in flags)
                {
                    if (flagChar != 'i' && flagChar != 'm')
                    {
                        throw new ArgumentException($"Invalid regex flag '{flagChar}' found in $match pattern.");
                    }
                }
                return new Regex(pattern, options);
            }
            else
            {
                // No slashes, treat as simple regex pattern string with no flags
                return new Regex(patternInput);
            }
        }
    }
}
