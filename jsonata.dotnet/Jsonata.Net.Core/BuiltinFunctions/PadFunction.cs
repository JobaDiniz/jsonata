using Jsonata.Net.Core.Evaluation;
using Jsonata.Net.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Linq;

namespace Jsonata.Net.Core.BuiltinFunctions
{
    public sealed class PadFunction : IBuiltinFunction
    {
        private const string ERROR_CODE_BAD_ARG_COUNT = "T0410";
        private const string ERROR_CODE_ARG_STR_BAD_TYPE = "S0214";
        private const string ERROR_CODE_ARG_WIDTH_BAD_TYPE = "S0215";
        private const string ERROR_CODE_ARG_CHAR_BAD_TYPE = "S0216";

        public Sequence Execute(List<Sequence> arguments, EvaluationContext context)
        {
            string functionName = "$pad";
            int argCount = arguments.Count;

            JsonNode? strNode;
            JsonNode? widthNode;
            JsonNode? charNode = null;

            if (argCount == 1) // $pad(width) -> str from context
            {
                strNode = context.CurrentInput;
                widthNode = arguments[0].FirstOrDefault() as JsonNode;
            }
            else if (argCount == 2) // $pad(str, width) or $pad(width, char)
            {
                // If first arg is string, assume $pad(str, width)
                // If first arg is number, assume $pad(width, char) with str from context
                object? firstArgItem = arguments[0].FirstOrDefault();
                if (firstArgItem is JsonValue val && val.TryGetValue(out string _))
                {
                    strNode = val;
                    widthNode = arguments[1].FirstOrDefault() as JsonNode;
                }
                else
                {
                    strNode = context.CurrentInput;
                    widthNode = firstArgItem as JsonNode;
                    charNode = arguments[1].FirstOrDefault() as JsonNode;
                }
            }
            else if (argCount == 3) // $pad(str, width, char)
            {
                strNode = arguments[0].FirstOrDefault() as JsonNode;
                widthNode = arguments[1].FirstOrDefault() as JsonNode;
                charNode = arguments[2].FirstOrDefault() as JsonNode;
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

            // Validate width
            int width;
            if (widthNode is JsonValue widthValNode && widthValNode.TryGetValue<int>(out int wInt))
            {
                width = wInt;
            }
            else if (widthNode is JsonValue wvn && wvn.TryGetValue<decimal>(out decimal wDec) && wDec == Math.Truncate(wDec))
            {
                width = (int)wDec;
            }
            else
            {
                throw new JsonataEvaluationException($"{ERROR_CODE_ARG_WIDTH_BAD_TYPE}: Argument 'width' to {functionName} must be an integer number. Got {(widthNode?.GetType().Name ?? "undefined")}.");
            }

            // Validate char (optional)
            char padChar = ' ';
            if (charNode != null)
            {
                if (charNode is JsonValue charValNode && charValNode.TryGetValue(out string? cValStr))
                {
                    if (!string.IsNullOrEmpty(cValStr))
                    {
                        padChar = cValStr[0];
                    }
                    // If cValStr is empty, default space is used. Spec implies this.
                }
                else if (charNode is JsonValue tempCv && tempCv.ToJsonString() == "null") // null char is error
                {
                     throw new JsonataEvaluationException($"{ERROR_CODE_ARG_CHAR_BAD_TYPE}: Argument 'char' to {functionName} must be a string. Got null.");
                }
                else
                {
                    throw new JsonataEvaluationException($"{ERROR_CODE_ARG_CHAR_BAD_TYPE}: Argument 'char' to {functionName} must be a string. Got {charNode.GetType().Name}.");
                }
            }

            int numCharsToPad = Math.Abs(width) - sourceString.Length;
            if (numCharsToPad <= 0)
            {
                return new Sequence(JsonValue.Create(sourceString)); // No padding needed or string is already longer
            }

            string padding = new string(padChar, numCharsToPad);

            if (width > 0) // Pad right (append)
            {
                return new Sequence(JsonValue.Create(sourceString + padding));
            }
            else // Pad left (prepend) (width < 0)
            {
                return new Sequence(JsonValue.Create(padding + sourceString));
            }
            // width == 0 means Math.Abs(width) is 0, numCharsToPad would be negative or 0, handled by first check.
        }
    }
}
