using Jsonata.Net.Core.Evaluation;
using Jsonata.Net.Core.Exceptions;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Linq;
using System.Net; // For WebUtility

namespace Jsonata.Net.Core.BuiltinFunctions
{
    public sealed class DecodeUrlFunction : IBuiltinFunction
    {
        private const string ERROR_CODE_BAD_ARG_COUNT = "T0410";
        private const string ERROR_CODE_ARG_BAD_TYPE = "S0227";

        public Sequence Execute(List<Sequence> arguments, EvaluationContext context)
        {
            string functionName = "$decodeUrl";
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
                // WebUtility.UrlDecode is generally suitable for both components and full URLs
                // in terms of decoding percent-encoded characters.
                string decodedString = WebUtility.UrlDecode(sourceString);
                return new Sequence(JsonValue.Create(decodedString));
            }
            catch (System.ArgumentNullException e)
            {
                 throw new JsonataEvaluationException($"S0103: Failed to decode URL: {e.Message}", e);
            }
        }
    }
}
