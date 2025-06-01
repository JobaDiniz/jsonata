using Jsonata.Net.Core.Evaluation;
using Jsonata.Net.Core.Exceptions;
using System; // For Math.Max, Math.Min
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Linq;

namespace Jsonata.Net.Core.BuiltinFunctions
{
    public sealed class SubstringFunction : IBuiltinFunction
    {
        private const string ERROR_CODE_ARG1_BAD_TYPE = "S0203"; // Argument 1 (string) must be a string
        private const string ERROR_CODE_ARG2_BAD_TYPE = "S0204"; // Argument 2 (start) must be a number
        private const string ERROR_CODE_ARG3_BAD_TYPE = "S0205"; // Argument 3 (length) must be a number
        private const string ERROR_CODE_BAD_ARG_COUNT = "T0410";

        public Sequence Execute(List<Sequence> arguments, EvaluationContext context)
        {
            string functionName = "$substring";
            JsonNode? sourceStringNode;
            JsonNode? startNode;
            JsonNode? lengthNode = null;

            int argCount = arguments.Count;

            if (argCount == 1)
            {
                sourceStringNode = context.CurrentInput;
                startNode = arguments[0].FirstOrDefault() as JsonNode;
            }
            else if (argCount == 2)
            {
                object? firstArgItem = arguments[0].FirstOrDefault();
                if (firstArgItem is JsonValue val && val.TryGetValue(out string _)) // Check if first arg is a string
                {
                    sourceStringNode = val;
                    startNode = arguments[1].FirstOrDefault() as JsonNode;
                }
                else // Assume first arg is start, second is length, string from context
                {
                    sourceStringNode = context.CurrentInput;
                    startNode = firstArgItem as JsonNode;
                    lengthNode = arguments[1].FirstOrDefault() as JsonNode;
                }
            }
            else if (argCount == 3)
            {
                sourceStringNode = arguments[0].FirstOrDefault() as JsonNode;
                startNode = arguments[1].FirstOrDefault() as JsonNode;
                lengthNode = arguments[2].FirstOrDefault() as JsonNode;
            }
            else
            {
                throw new JsonataEvaluationException($"{ERROR_CODE_BAD_ARG_COUNT}: {functionName} expects 1, 2, or 3 arguments. Got {argCount}.");
            }

            // Validate sourceStringNode
            string sourceString;
            if (sourceStringNode is JsonValue sv && sv.TryGetValue(out string? sVal))
            {
                sourceString = sVal;
            }
            else if (sourceStringNode == null || (sourceStringNode is JsonValue ssv && ssv.ToJsonString() == "null"))
            {
                 string from = argCount == 1 || (argCount == 2 && !(arguments[0].FirstOrDefault() is JsonValue val && val.TryGetValue(out string _)))? "context" : "argument";
                 throw new JsonataEvaluationException($"{ERROR_CODE_ARG1_BAD_TYPE}: Argument 'string' to {functionName} must be a string. Got {(sourceStringNode == null? "undefined" : "null")} from {from}.");
            }
            else
            {
                throw new JsonataEvaluationException($"{ERROR_CODE_ARG1_BAD_TYPE}: Argument 'string' to {functionName} must be a string. Got {sourceStringNode.GetType().Name}.");
            }

            // Validate startNode
            int start;
            if (startNode is JsonValue startValNode && startValNode.TryGetValue<int>(out int sInt))
            {
                start = sInt;
            }
            else if (startNode is JsonValue svn && svn.TryGetValue<decimal>(out decimal sDec) && sDec == Math.Truncate(sDec))
            {
                start = (int)sDec;
            }
            else
            {
                throw new JsonataEvaluationException($"{ERROR_CODE_ARG2_BAD_TYPE}: Argument 'start' to {functionName} must be an integer number. Got {(startNode?.GetType().Name ?? "undefined")}.");
            }

            // Validate lengthNode (if provided)
            int? length = null;
            if (lengthNode != null)
            {
                if (lengthNode is JsonValue lengthValNode && lengthValNode.TryGetValue<int>(out int lInt))
                {
                    length = lInt;
                }
                else if (lengthNode is JsonValue lvn && lvn.TryGetValue<decimal>(out decimal lDec) && lDec == Math.Truncate(lDec))
                {
                    length = (int)lDec;
                }
                else
                {
                    throw new JsonataEvaluationException($"{ERROR_CODE_ARG3_BAD_TYPE}: Argument 'length' to {functionName} must be an integer number. Got {(lengthNode.GetType().Name)}.");
                }
            }

            // Perform substring logic
            if (sourceString.Length == 0)
            {
                return new Sequence(JsonValue.Create(""));
            }

            int actualStart;
            if (start >= 0)
            {
                actualStart = start;
            }
            else // start is negative
            {
                actualStart = sourceString.Length + start;
            }
            actualStart = Math.Max(0, actualStart); // clamp to beginning of string

            if (actualStart >= sourceString.Length)
            {
                return new Sequence(JsonValue.Create("")); // start is beyond end of string
            }

            if (length == null) // Substring to the end
            {
                return new Sequence(JsonValue.Create(sourceString.Substring(actualStart)));
            }
            else // Length is specified
            {
                if (length.Value <= 0) // Negative or zero length returns empty string
                {
                    return new Sequence(JsonValue.Create(""));
                }
                int actualLength = Math.Min(length.Value, sourceString.Length - actualStart);
                return new Sequence(JsonValue.Create(sourceString.Substring(actualStart, actualLength)));
            }
        }
    }
}
