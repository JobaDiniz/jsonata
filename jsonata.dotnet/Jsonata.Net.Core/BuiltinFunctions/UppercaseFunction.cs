using Jsonata.Net.Core.Evaluation;
using Jsonata.Net.Core.Exceptions;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Linq;
using System.Text.Json; // For JsonElement

namespace Jsonata.Net.Core.BuiltinFunctions
{
    public sealed class UppercaseFunction : IBuiltinFunction
    {
        public Sequence Execute(List<Sequence> arguments, EvaluationContext context)
        {
            JsonNode? targetNode;
            string functionName = "$uppercase";

            if (arguments.Count == 0)
            {
                targetNode = context.CurrentInput;
            }
            else if (arguments.Count == 1)
            {
                Sequence argSeq = arguments[0];
                if (argSeq.IsUndefined() || argSeq.Count == 0)
                {
                    throw new JsonataEvaluationException($"S0211: Argument to {functionName} must not be undefined or an empty sequence.");
                }
                if (argSeq.Count > 1)
                {
                    throw new JsonataEvaluationException($"S0211: Argument to {functionName} must be a single string, not a sequence of multiple items.");
                }
                object? firstItem = argSeq.FirstOrDefault();
                if (firstItem == null)
                {
                     throw new JsonataEvaluationException($"S0211: Argument to {functionName} must be a string. Got null.");
                }
                if (!(firstItem is JsonValue))
                {
                    throw new JsonataEvaluationException($"S0211: Argument to {functionName} must be a string. Got {firstItem.GetType().Name}.");
                }
                targetNode = (JsonValue)firstItem;
            }
            else
            {
                throw new JsonataEvaluationException($"T0410: Too many arguments for {functionName}. Expected 0 or 1.");
            }

            if (targetNode == null)
            {
                throw new JsonataEvaluationException($"S0211: Argument to {functionName} must be a string. Got undefined (from context).");
            }

            if (targetNode is JsonValue jsonValue)
            {
                if (jsonValue.TryGetValue(out string? strValue))
                {
                    return new Sequence(JsonValue.Create(strValue.ToUpperInvariant()));
                }
                else if (jsonValue.ToJsonString() == "null")
                {
                    throw new JsonataEvaluationException($"S0211: Argument to {functionName} must be a string. Got null.");
                }
                else // It's some other JsonValue type (number, boolean)
                {
                    throw new JsonataEvaluationException($"S0211: Argument to {functionName} must be a string. Got a non-string JsonValue ({jsonValue.ToJsonString()}).");
                }
            }
            else
            {
                throw new JsonataEvaluationException($"S0211: Argument to {functionName} must be a string. Got {targetNode.GetType().Name}.");
            }
        }
    }
}
