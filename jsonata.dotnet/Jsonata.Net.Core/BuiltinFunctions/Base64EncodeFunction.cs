using Jsonata.Net.Core.Evaluation;
using Jsonata.Net.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Linq;
using System.Text;

namespace Jsonata.Net.Core.BuiltinFunctions
{
    public sealed class Base64EncodeFunction : IBuiltinFunction
    {
        private const string ERROR_CODE_BAD_ARG_COUNT = "T0410"; // Or specific code if available
        private const string ERROR_CODE_ARG_BAD_TYPE = "S0222"; // New code for this function

        public Sequence Execute(List<Sequence> arguments, EvaluationContext context)
        {
            string functionName = "$base64encode";
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
                // JSONata spec: "Each character in the string is treated as a byte value in the range 0x00 to 0xFF"
                // This is ambiguous. UTF-8 is a common standard. If a strict interpretation of
                // char -> byte (0-255) is needed, a different encoding or char validation would be required.
                // For now, using UTF-8.
                byte[] bytesToEncode = Encoding.UTF8.GetBytes(sourceString);
                string base64String = Convert.ToBase64String(bytesToEncode);
                return new Sequence(JsonValue.Create(base64String));
            }
            catch (Exception e) when (e is EncoderFallbackException || e is ArgumentNullException)
            {
                throw new JsonataEvaluationException($"S0100: Failed to encode string to Base64: {e.Message}", e);
            }
        }
    }
}
