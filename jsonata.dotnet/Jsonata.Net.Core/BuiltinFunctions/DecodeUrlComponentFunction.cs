using Jsonata.Net.Core.Evaluation;
using Jsonata.Net.Core.Exceptions;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Linq;
using System.Net; // For WebUtility

namespace Jsonata.Net.Core.BuiltinFunctions
{
    public sealed class DecodeUrlComponentFunction : IBuiltinFunction
    {
        private const string ERROR_CODE_BAD_ARG_COUNT = "T0410";
        private const string ERROR_CODE_ARG_BAD_TYPE = "S0226";

        public Sequence Execute(List<Sequence> arguments, EvaluationContext context)
        {
            string functionName = "$decodeUrlComponent";
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
                string decodedString = WebUtility.UrlDecode(sourceString);
                return new Sequence(JsonValue.Create(decodedString));
            }
            catch (System.ArgumentNullException e) // Can be thrown by UrlDecode if input is null, though our checks should prevent this.
            {
                 throw new JsonataEvaluationException($"S0102: Failed to decode URL component: {e.Message}", e);
            }
        }
    }
}
