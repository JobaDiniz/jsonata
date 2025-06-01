using Jsonata.Net.Core.Evaluation;
using Jsonata.Net.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Linq;
using System.Text;

namespace Jsonata.Net.Core.BuiltinFunctions
{
    public sealed class Base64DecodeFunction : IBuiltinFunction
    {
        private const string ERROR_CODE_BAD_ARG_COUNT = "T0410";
        private const string ERROR_CODE_ARG_BAD_TYPE = "S0223";

        public Sequence Execute(List<Sequence> arguments, EvaluationContext context)
        {
            string functionName = "$base64decode";
            int argCount = arguments.Count;

            JsonNode? strNode;

            if (argCount == 0)
            {
                strNode = context.CurrentInput;
            }
            else if (argCount == 1)
            {
                strNode = arguments[0].FirstOrDefault() as JsonNode;
            }
            else
            {
                throw new JsonataEvaluationException($"{ERROR_CODE_BAD_ARG_COUNT}: {functionName} expects 0 or 1 argument. Got {argCount}.");
            }

            string sourceString;
            if (strNode is JsonValue svStr && svStr.TryGetValue(out string? sValStr))
            {
                sourceString = sValStr;
            }
            else if (strNode == null || (strNode is JsonValue tempSvStr && tempSvStr.ToJsonString() == "null"))
            {
                throw new JsonataEvaluationException($"{ERROR_CODE_ARG_BAD_TYPE}: Argument to {functionName} must be a string. Got {(strNode == null ? "undefined" : "null")}.");
            }
            else
            {
                throw new JsonataEvaluationException($"{ERROR_CODE_ARG_BAD_TYPE}: Argument to {functionName} must be a string. Got {strNode.GetType().Name}.");
            }

            try
            {
                byte[] decodedBytes = Convert.FromBase64String(sourceString);
                // Assuming UTF-8 for decoding, consistent with encoding choice.
                string originalString = Encoding.UTF8.GetString(decodedBytes);
                return new Sequence(JsonValue.Create(originalString));
            }
            catch (Exception e) when (e is FormatException || e is ArgumentNullException)
            {
                // FormatException if the base-64 string is invalid.
                throw new JsonataEvaluationException($"S0101: Failed to decode Base64 string: {e.Message}", e);
            }
        }
    }
}
