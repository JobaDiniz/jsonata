using Jsonata.Net.Core.Evaluation;
using Jsonata.Net.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Linq;
using System.Text.RegularExpressions;

namespace Jsonata.Net.Core.BuiltinFunctions
{
    public sealed class SplitFunction : IBuiltinFunction
    {
        private const string ERROR_CODE_BAD_ARG_COUNT = "T0410";
        private const string ERROR_CODE_ARG_STR_BAD_TYPE = "S0208";
        private const string ERROR_CODE_ARG_SEP_BAD_TYPE = "S0209";
        private const string ERROR_CODE_ARG_LIMIT_BAD_TYPE = "S0228"; // Using a new code for limit

        public Sequence Execute(List<Sequence> arguments, EvaluationContext context)
        {
            string functionName = "$split";
            int argCount = arguments.Count;

            JsonNode? strNode;
            JsonNode? separatorNode;
            JsonNode? limitNode = null;

            if (argCount == 1) // $split(separator) -> str from context
            {
                strNode = context.CurrentInput;
                separatorNode = arguments[0].FirstOrDefault() as JsonNode;
            }
            else if (argCount == 2) // $split(str, separator) or $split(separator, limit)
            {
                // If first arg is string, assume $split(str, separator)
                // Else, assume $split(separator, limit) with str from context
                object? firstArgItem = arguments[0].FirstOrDefault();
                if (firstArgItem is JsonValue val && val.TryGetValue(out string _))
                {
                    strNode = val;
                    separatorNode = arguments[1].FirstOrDefault() as JsonNode;
                }
                else
                {
                    strNode = context.CurrentInput;
                    separatorNode = firstArgItem as JsonNode;
                    limitNode = arguments[1].FirstOrDefault() as JsonNode;
                }
            }
            else if (argCount == 3) // $split(str, separator, limit)
            {
                strNode = arguments[0].FirstOrDefault() as JsonNode;
                separatorNode = arguments[1].FirstOrDefault() as JsonNode;
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

            // Validate separator
            string separatorString; // Can be plain string or regex pattern
            if (separatorNode is JsonValue svSep && svSep.TryGetValue(out string? sValSep))
            {
                separatorString = sValSep;
            }
             else if (separatorNode == null || (separatorNode is JsonValue tempSepStr && tempSepStr.ToJsonString() == "null"))
            {
                 throw new JsonataEvaluationException($"{ERROR_CODE_ARG_SEP_BAD_TYPE}: Argument 'separator' to {functionName} must be a string or regex. Got null.");
            }
            else
            {
                throw new JsonataEvaluationException($"{ERROR_CODE_ARG_SEP_BAD_TYPE}: Argument 'separator' to {functionName} must be a string or regex. Got {separatorNode.GetType().Name}.");
            }

            // Validate limit (optional)
            int limit = 0; // 0 means no limit by default in Regex.Split and String.Split
            if (limitNode != null)
            {
                if (limitNode is JsonValue limitValNode && limitValNode.TryGetValue<int>(out int lInt))
                {
                    limit = lInt;
                }
                else if (limitNode is JsonValue lvn && lvn.TryGetValue<decimal>(out decimal lDec) && lDec == Math.Truncate(lDec))
                {
                    limit = (int)lDec;
                }
                else
                {
                    throw new JsonataEvaluationException($"{ERROR_CODE_ARG_LIMIT_BAD_TYPE}: Argument 'limit' to {functionName} must be an integer number. Got {(limitNode.GetType().Name)}.");
                }
            }

            string[] resultParts;
            if (TryParseRegex(separatorString, out Regex? regex))
            {
                if (limit <= 0) limit = 0; // Regex.Split with 0 means split all
                resultParts = regex.Split(sourceString, limit == 0 ? 0 : limit); // If limit is specified N, N splits occur, N+1 results possible.
                                                                                // If limit is 1, 1 split, 2 results max.
                                                                                // If limit is 0 (or not specified), split all.
                                                                                // JSONata: "at most that many splits are made". If limit is N, N splits.
                                                                                // This means if limit is N > 0, we want N results, so StringSplitOptions might be better.
                                                                                // Or take N+1 from Regex.Split and then take first N if count > N?
                                                                                // The spec: "If limit is N, the string is split N times, to give N+1 substrings" - no, this is wrong.
                                                                                // "If limit is specified, then at most that many splits are made."
                                                                                // If limit = 1, one split, results in array of 2.
                                                                                // If limit = 0, same as not specified.
                                                                                // If limit > 0, it's the number of items in the resulting array. (This is String.Split(..., limit) behavior)
                                                                                // Let's use String.Split behavior for limit if it's not a regex.
                                                                                // For Regex.Split, limit is number of matches.
                                                                                // JSONata spec for $split refers to limit as "at most that many splits are made".
                                                                                // Let's re-evaluate limit. If limit is N, we want N substrings in the output array.
                                                                                // This means N-1 splits.
                                                                                // So if limit for $split is L, use L for String.Split(..., L, ...),
                                                                                // and for Regex.Split(..., L), this will give L results.
                if (limit > 0)
                {
                    resultParts = regex.Split(sourceString, limit);
                }
                else
                {
                     resultParts = regex.Split(sourceString);
                }
            }
            else // Plain string separator
            {
                if (limit > 0)
                {
                    resultParts = sourceString.Split(new string[] { separatorString }, limit, StringSplitOptions.None);
                }
                else
                {
                    resultParts = sourceString.Split(new string[] { separatorString }, StringSplitOptions.None);
                }
            }

            JsonArray resultArray = new JsonArray();
            foreach (string part in resultParts)
            {
                resultArray.Add(JsonValue.Create(part));
            }
            // Wrap the JsonArray itself as a single item in the sequence
            return new Sequence((object?)resultArray);
        }

        // Helper from ContainsFunction (can be moved to a shared utility if more widely needed)
        private static bool TryParseRegex(string patternInput, out Regex? regex)
        {
            regex = null;
            if (patternInput.Length >= 2 && patternInput.StartsWith("/") && patternInput.LastIndexOf('/') > 0)
            {
                int lastSlash = patternInput.LastIndexOf('/');
                string pattern = patternInput.Substring(1, lastSlash - 1);
                string flags = patternInput.Substring(lastSlash + 1);

                RegexOptions options = RegexOptions.None;
                if (flags.Contains("i")) options |= RegexOptions.IgnoreCase;
                if (flags.Contains("m")) options |= RegexOptions.Multiline;

                try
                {
                    regex = new Regex(pattern, options);
                    return true;
                }
                catch (ArgumentException) { return false; }
            }
            return false;
        }
    }
}
