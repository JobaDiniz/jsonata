using Jsonata.Net.Core.Evaluation;
using Jsonata.Net.Core.Exceptions;
using System; // For Uri
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Linq;

namespace Jsonata.Net.Core.BuiltinFunctions
{
    public sealed class EncodeUrlFunction : IBuiltinFunction
    {
        private const string ERROR_CODE_BAD_ARG_COUNT = "T0410";
        private const string ERROR_CODE_ARG_BAD_TYPE = "S0225"; // New code

        public Sequence Execute(List<Sequence> arguments, EvaluationContext context)
        {
            string functionName = "$encodeUrl";
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

            // Uri.EscapeUriString is generally for escaping the whole URI string,
            // preserving reserved characters like ':', '/', '?', '#'.
            // WebUtility.UrlEncode is for query string components, encoding reserved chars.
            // JSONata spec is usually closer to component encoding, even for $encodeUrl.
            // Let's use WebUtility.UrlEncode for now, consistent with $encodeUrlComponent,
            // as JSONata spec usually implies aggressive encoding. If specific differences arise,
            // this might need to be EscapeUriString or a custom implementation.
            // Update: The prompt implies $encodeUrl might be different.
            // Let's try Uri.EscapeUriString for $encodeUrl.
            string encodedString = Uri.EscapeUriString(sourceString);
            return new Sequence(JsonValue.Create(encodedString));
        }
    }
}
