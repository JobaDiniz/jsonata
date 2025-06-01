using Jsonata.Net.Core.Evaluation;
using Jsonata.Net.Core.Exceptions;
using System.Collections.Generic;
using System.Text.Json.Nodes; // For JsonNode, JsonValue
using System.Linq; // For FirstOrDefault
using System.Text.Json; // For JsonElement

namespace Jsonata.Net.Core.BuiltinFunctions
{
    public sealed class LengthFunction : IBuiltinFunction
    {
        public Sequence Execute(List<Sequence> arguments, EvaluationContext context)
        {
            JsonNode? targetNode;
            string functionName = "$length";

            if (arguments.Count == 0)
            {
                targetNode = context.CurrentInput;
            }
            else if (arguments.Count == 1)
            {
                Sequence argSeq = arguments[0];
                if (argSeq.IsUndefined() || argSeq.Count == 0)
                {
                    // Spec: If the arg evaluates to undefined or an empty sequence, the function throws an error S0210
                    throw new JsonataEvaluationException($"S0210: Argument to {functionName} must not be undefined or an empty sequence.");
                }
                if (argSeq.Count > 1)
                {
                    // Spec: If the arg evaluates to a sequence of more than one item, the function throws an error S0210
                    throw new JsonataEvaluationException($"S0210: Argument to {functionName} must be a single string, not a sequence of multiple items.");
                }
                object? firstItem = argSeq.FirstOrDefault();
                if (firstItem == null)
                {
                     throw new JsonataEvaluationException($"S0210: Argument to {functionName} must be a string. Got null.");
                }
                if (!(firstItem is JsonValue)) // Check if it's a JsonValue before trying to get string
                {
                    throw new JsonataEvaluationException($"S0210: Argument to {functionName} must be a string. Got {firstItem.GetType().Name}.");
                }
                targetNode = (JsonValue)firstItem;
            }
            else
            {
                throw new JsonataEvaluationException($"T0410: Too many arguments for {functionName}. Expected 0 or 1.");
            }

            if (targetNode == null) // Handles context item being C# null (which means undefined context)
            {
                throw new JsonataEvaluationException($"S0210: Argument to {functionName} must be a string. Got undefined (from context).");
            }

            if (targetNode is JsonValue jsonValue)
            {
                if (jsonValue.TryGetValue(out string? strValue)) // Checks if it's a string JsonValue
                {
                    return new Sequence(JsonValue.Create(strValue.Length));
                }
                // If not directly a string, check its actual JSON kind.
                // Using ToJsonString() and comparing to "null" is a robust way to check for JsonNull
                // without issues from GetValue<JsonElement>() on directly created JsonValues from primitives.
                else if (jsonValue.ToJsonString() == "null")
                {
                    throw new JsonataEvaluationException($"S0210: Argument to {functionName} must be a string. Got null.");
                }
                else // It's some other JsonValue type (number, boolean)
                {
                    throw new JsonataEvaluationException($"S0210: Argument to {functionName} must be a string. Got a non-string JsonValue ({jsonValue.ToJsonString()}).");
                }
            }
            else // Not a JsonValue (e.g., JsonObject, JsonArray)
            {
                throw new JsonataEvaluationException($"S0210: Argument to {functionName} must be a string. Got {targetNode.GetType().Name}.");
            }
        }
    }
}
