using System.Text.Json.Nodes;
using Jsonata.Net.Core.Evaluation;
using Jsonata.Net.Core.Exceptions;

namespace Jsonata.Net.Core.AstNodes
{
    public sealed class VariableNode : AstNode
    {
        public string Value { get; }

        public VariableNode(string value)
        {
            this.Value = value;
        }

        public override Sequence Evaluate(JsonNode? input, EvaluationContext context)
        {
            if (context.TryGetBinding(this.Value, out object? boundValueObj))
            {
                // If the variable is "$", it refers to the current context input.
                // TryGetBinding should already correctly handle this by returning context.CurrentInput.
                // The additional logging here can be removed or kept for debugging if desired.
                if (this.Value == "$")
                {
                    // Optional: Keep debug logging if helpful, otherwise remove.
                    // System.Console.WriteLine($"[VariableNode:$] Context.CurrentInput type: {context.CurrentInput?.GetType().FullName ?? "null"}");
                    // System.Console.WriteLine($"[VariableNode:$] Context.CurrentInput value: {context.CurrentInput?.ToJsonString() ?? "null"}");
                    // System.Console.WriteLine($"[VariableNode:$] Bound value type: {boundValueObj?.GetType().FullName ?? "null"}");
                    // System.Console.WriteLine($"[VariableNode:$] Bound value: {(boundValueObj as JsonNode)?.ToJsonString() ?? boundValueObj?.ToString() ?? "null"}");
                }

                // If the bound value is a JsonNode, clone it to prevent modification of the original.
                // Otherwise, return the bound value as is (e.g., it could be a FunctionObject).
                if (boundValueObj is JsonNode jsonNode)
                {
                    return new Sequence(jsonNode.DeepClone());
                }
                return new Sequence(boundValueObj); // Handles JsonNode, FunctionObject, or other types
            }

            // If the variable is not found in bindings, it's undefined.
            return Sequence.Undefined;
        }
    }
}
