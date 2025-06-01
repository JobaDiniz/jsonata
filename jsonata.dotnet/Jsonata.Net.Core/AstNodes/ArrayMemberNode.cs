using System.Text.Json.Nodes;
using Jsonata.Net.Core.Evaluation;
using Jsonata.Net.Core.Exceptions;
using System.Collections.Generic; // For List
using System.Linq; // For Cast (if needed)

namespace Jsonata.Net.Core.AstNodes
{
    public sealed class ArrayMemberNode : AstNode
    {
        public AstNode IndexOrNameExpr { get; }

        public ArrayMemberNode(AstNode indexOrNameExpr)
        {
            this.IndexOrNameExpr = indexOrNameExpr;
        }

        public override Sequence Evaluate(JsonNode? input, EvaluationContext context)
        {
            if (input == null) return Sequence.Undefined;

            if (input is JsonArray arr)
            {
                // Try to evaluate IndexOrNameExpr as a direct numeric index first.
                // This evaluation happens in the context of the array itself (input).
                Sequence indexSeq = this.IndexOrNameExpr.Evaluate(input, context);
                object? indexValObj = indexSeq.FirstOrDefault();

                if (indexValObj is JsonValue indexJsonVal && indexJsonVal.TryGetValue<int>(out int directIndex))
                {
                    // Direct integer index access
                    if (directIndex >= 0 && directIndex < arr.Count) return new Sequence(arr[directIndex]?.DeepClone());
                    if (directIndex < 0 && arr.Count + directIndex >= 0) return new Sequence(arr[arr.Count + directIndex]?.DeepClone());
                    return Sequence.Undefined; // Index out of bounds
                }
                else
                {
                    // If not a direct integer index, it's predicate logic for arrays.
                    // The IndexOrNameExpr (predicate) must be evaluated for EACH item in 'arr'.
                    List<object?> filteredItems = new List<object?>();
                    foreach (JsonNode? currentArrayItem in arr) // Iterate over nodes in the JsonArray
                    {
                        if (currentArrayItem == null) continue; // Skip null items if they exist in the JsonArray

                        // Predicate is evaluated with currentArrayItem as the context '$'
                        EvaluationContext itemContext = new EvaluationContext(currentArrayItem, context.Functions, context.RootInput, null, context);
                        Sequence predicateResultSeq = this.IndexOrNameExpr.Evaluate(currentArrayItem, itemContext);

                        if (Sequence.IsTruthy(predicateResultSeq))
                        {
                            filteredItems.Add(currentArrayItem.DeepClone());
                        }
                    }
                    return new Sequence(filteredItems); // Sequence constructor handles List<object?>
                }
            }
            else if (input is JsonObject obj)
            {
                // For objects, IndexOrNameExpr must evaluate to a string name.
                // It's evaluated once against the context of 'obj' (which is 'input').
                Sequence nameSeq = this.IndexOrNameExpr.Evaluate(input, context);
                object? nameValObj = nameSeq.FirstOrDefault();

                if (nameValObj is JsonValue nameJsonVal && nameValObj != null && nameJsonVal.TryGetValue<string>(out string? nameStr) && nameStr != null)
                {
                    return obj.TryGetPropertyValue(nameStr, out JsonNode? propertyValue) ? new Sequence(propertyValue?.DeepClone()) : Sequence.Undefined;
                }
                // S0211: If the key expression is not a string an error is thrown.
                // Also handles if nameValObj is null (e.g. from undefined sequence for name expression)
                throw new JsonataEvaluationException($"Object property accessor expression must evaluate to a non-null string. Got: {nameValObj?.GetType().Name ?? "null"}");
            }

            // Input is not an array or object, so pathing into it with [...] is undefined.
            return Sequence.Undefined;
        }
    }
}
