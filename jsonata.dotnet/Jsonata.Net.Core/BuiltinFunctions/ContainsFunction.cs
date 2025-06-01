using Jsonata.Net.Core.Evaluation;
using Jsonata.Net.Core.Exceptions;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Linq;
using System.Text.RegularExpressions;

namespace Jsonata.Net.Core.BuiltinFunctions
{
    public sealed class ContainsFunction : IBuiltinFunction
    {
        private const string ERROR_CODE_BAD_ARG_COUNT = "T0410";
        private const string ERROR_CODE_ARG_STR_BAD_TYPE = "S0217";
        private const string ERROR_CODE_ARG_PATTERN_BAD_TYPE = "S0218";

        public Sequence Execute(List<Sequence> arguments, EvaluationContext context)
        {
            string functionName = "$contains";
            int argCount = arguments.Count;

            JsonNode? strNode;
            JsonNode? patternNode;

            if (argCount == 1) // $contains(pattern) -> str from context
            {
                strNode = context.CurrentInput;
                patternNode = arguments[0].FirstOrDefault() as JsonNode;
            }
            else if (argCount == 2) // $contains(str, pattern)
            {
                strNode = arguments[0].FirstOrDefault() as JsonNode;
                patternNode = arguments[1].FirstOrDefault() as JsonNode;
            }
            else
            {
                throw new JsonataEvaluationException($"{ERROR_CODE_BAD_ARG_COUNT}: {functionName} expects 1 or 2 arguments. Got {argCount}.");
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

            // Validate pattern
            string patternString;
            if (patternNode is JsonValue svPattern && svPattern.TryGetValue(out string? sValPattern))
            {
                patternString = sValPattern;
            }
            else if (patternNode == null || (patternNode is JsonValue tempPatternStr && tempPatternStr.ToJsonString() == "null"))
            {
                 throw new JsonataEvaluationException($"{ERROR_CODE_ARG_PATTERN_BAD_TYPE}: Argument 'pattern' to {functionName} must be a string or regex. Got null.");
            }
            else
            {
                throw new JsonataEvaluationException($"{ERROR_CODE_ARG_PATTERN_BAD_TYPE}: Argument 'pattern' to {functionName} must be a string or regex. Got {patternNode.GetType().Name}.");
            }

            bool contains;
            if (TryParseRegex(patternString, out Regex? regex))
            {
                contains = regex.IsMatch(sourceString);
            }
            else
            {
                contains = sourceString.Contains(patternString);
            }

            return new Sequence(JsonValue.Create(contains));
        }

        // Helper to parse regex like "/pattern/flags"
        // Returns true if successfully parsed as regex, false otherwise.
        private static bool TryParseRegex(string patternInput, out Regex? regex)
        {
            regex = null;
            if (patternInput.Length >= 2 && patternInput.StartsWith("/") && patternInput.LastIndexOf('/') > 0)
            {
                int lastSlash = patternInput.LastIndexOf('/');
                string pattern = patternInput.Substring(1, lastSlash - 1);
                string flags = patternInput.Substring(lastSlash + 1);

                RegexOptions options = RegexOptions.None;
                if (flags.Contains("i"))
                {
                    options |= RegexOptions.IgnoreCase;
                }
                if (flags.Contains("m")) // JSONata spec might not have 'm' but good to be aware
                {
                    options |= RegexOptions.Multiline;
                }
                // Add other flags if JSONata supports them e.g. 's' for Singleline.

                try
                {
                    regex = new Regex(pattern, options);
                    return true;
                }
                catch (ArgumentException) // Invalid regex pattern or options
                {
                    // As per JSONata spec, if regex is invalid, it's not an error,
                    // it's treated as a literal string if it can't be parsed as regex.
                    // However, some implementations might throw. For $contains, if it's not valid regex,
                    // it should probably be used as a literal string for Contains.
                    // The current $contains spec says "If pattern is a regular expression,
                    // then the function returns true if the string matches the regex"
                    // This implies invalid regex might be an error, or it falls back to string contains.
                    // For now, let's assume invalid regex means it's not a regex pattern.
                    return false;
                }
            }
            return false;
        }
    }
}
