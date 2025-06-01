using System.Text.Json.Nodes;
using Jsonata.Net.Core.Evaluation;
using Jsonata.Net.Core.Exceptions;
using System.Collections.Generic; // For List

namespace Jsonata.Net.Core.AstNodes
{
    public sealed class PathOperatorNode : AstNode
    {
        public AstNode Lhs { get; }
        public AstNode Rhs { get; }

        public PathOperatorNode(AstNode lhs, AstNode rhs)
        {
            this.Lhs = lhs;
            this.Rhs = rhs;
        }

        public override Sequence Evaluate(JsonNode? input, EvaluationContext context)
        {
            Sequence lhsResult = this.Lhs.Evaluate(input, context);
            if (lhsResult == Sequence.Undefined || lhsResult.Count == 0)
            {
                return Sequence.Undefined;
            }
            List<object?> results = new List<object?>(); // Changed to List<object?>
            foreach (object? itemObj in lhsResult) // Changed to object?
            {
                // Path operator typically works on JsonObjects or JsonArrays.
                // If itemObj is not a JsonNode, then pathing into it is generally undefined or error.
                if (!(itemObj is JsonNode item))
                {
                    // According to JSONata spec, trying to path into non-object/array types
                    // (like string, number, boolean, null, function) usually yields undefined.
                    // We might add specific behavior for strings if needed (e.g., string indexing with path).
                    // For now, if it's not a JsonNode, skip or add Undefined to results?
                    // JSONata often effectively 'skips' items that don't match the path structure.
                    continue;
                }

                System.Console.WriteLine($"[PathOperatorNode] LHS item type: {item?.GetType().FullName ?? "null"}");
                System.Console.WriteLine($"[PathOperatorNode] LHS item value: {item?.ToJsonString() ?? "null"}");
                System.Console.WriteLine($"[PathOperatorNode] RHS node type: {this.Rhs?.GetType().FullName ?? "null"}");
                if (this.Rhs is NameNode nameNodeRhs)
                {
                    System.Console.WriteLine($"[PathOperatorNode] RHS NameNode value: {nameNodeRhs.Value}");
                }
                /* Each item from LHS becomes input for RHS */
                /* TODO: Context for RHS needs to be set correctly (e.g. if item is current focus) */
                Sequence rhsResult = this.Rhs.Evaluate(item, context); // item is now guaranteed JsonNode
                if (rhsResult != Sequence.Undefined && rhsResult.Count > 0) // Only add if RHS yields something
                {
                    foreach (object? rItem in rhsResult) // rhsResult is Sequence (IEnumerable<object?>)
                    {
                        results.Add(rItem);
                    }
                }
            }
            // If results list is empty, it means no path matched or RHS yielded nothing.
            // This should result in an undefined sequence.
            return results.Count == 0 ? Sequence.Undefined : new Sequence(results);
        }
    }
}
