using Jsonata.Net.Core.Evaluation;
using Jsonata.Net.Core.Exceptions;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Linq;

namespace Jsonata.Net.Core.BuiltinFunctions
{
    public sealed class SubstringAfterFunction : IBuiltinFunction
    {
        private const string ERROR_CODE_ARG_BAD_TYPE = "S0207"; // Argument must be a string
        private const string ERROR_CODE_BAD_ARG_COUNT = "T0410";

        public Sequence Execute(List<Sequence> arguments, EvaluationContext context)
        {
            string functionName = "$substringAfter";
            JsonNode? strNode;
            JsonNode? charsNode;

            int argCount = arguments.Count;

            if (argCount == 1)
            {
                strNode = context.CurrentInput;
                charsNode = arguments[0].FirstOrDefault() as JsonNode;
            }
            else if (argCount == 2)
            {
                strNode = arguments[0].FirstOrDefault() as JsonNode;
                charsNode = arguments[1].FirstOrDefault() as JsonNode;
            }
            else
            {
                throw new JsonataEvaluationException($"{ERROR_CODE_BAD_ARG_COUNT}: {functionName} expects 1 or 2 arguments. Got {argCount}.");
            }

            string sourceString;
            if (strNode is JsonValue svStr && svStr.TryGetValue(out string? sValStr))
            {
                sourceString = sValStr;
            }
            else if (strNode == null || (strNode is JsonValue tempSvStr && tempSvStr.ToJsonString() == "null"))
            {
                throw new JsonataEvaluationException($"{ERROR_CODE_ARG_BAD_TYPE}: Argument 'string' to {functionName} must be a string. Got {(strNode == null ? "undefined" : "null")}.");
            }
            else
            {
                throw new JsonataEvaluationException($"{ERROR_CODE_ARG_BAD_TYPE}: Argument 'string' to {functionName} must be a string. Got {strNode.GetType().Name}.");
            }

            string charsToFind;
            if (charsNode is JsonValue svChars && svChars.TryGetValue(out string? sValChars))
            {
                charsToFind = sValChars;
            }
            else if (charsNode == null || (charsNode is JsonValue tempSvChars && tempSvChars.ToJsonString() == "null"))
            {
                throw new JsonataEvaluationException($"{ERROR_CODE_ARG_BAD_TYPE}: Argument 'chars' to {functionName} must be a string. Got {(charsNode == null ? "undefined" : "null")}.");
            }
            else
            {
                throw new JsonataEvaluationException($"{ERROR_CODE_ARG_BAD_TYPE}: Argument 'chars' to {functionName} must be a string. Got {charsNode.GetType().Name}.");
            }

            if (string.IsNullOrEmpty(charsToFind)) // If chars is empty string
            {
                return new Sequence(JsonValue.Create(sourceString)); // Return original string
            }

            int index = sourceString.IndexOf(charsToFind);
            if (index == -1) // Not found
            {
                return new Sequence(JsonValue.Create("")); // Return empty string (JSONata spec for $substringAfter if not found is empty string)
            }

            return new Sequence(JsonValue.Create(sourceString.Substring(index + charsToFind.Length)));
        }
    }
}
