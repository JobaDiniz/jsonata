using System.Text.Json.Nodes;
using Jsonata.Net.Core.Evaluation;
using Jsonata.Net.Core.Exceptions;
using System.Collections.Generic; // Not strictly needed in this file anymore

namespace Jsonata.Net.Core.AstNodes
{
    public sealed class AssignmentNode : AstNode
    {
        public VariableNode Variable { get; }
        public AstNode Expression { get; }

        public AssignmentNode(VariableNode variable, AstNode expression)
        {
            this.Variable = variable;
            this.Expression = expression;
        }

        public override Sequence Evaluate(JsonNode? input, EvaluationContext context)
        {
            Sequence rhsResultSeq = this.Expression.Evaluate(input, context);

            // What gets assigned to the variable?
            // If the RHS evaluates to Sequence.Undefined, then undefined is assigned (represented as null in bindings perhaps).
            // If it's a sequence with one item (JsonNode or FunctionObject), that item is assigned.
            // If it's an empty sequence (not Undefined), it's like assigning undefined (null).
            // If it's a sequence with multiple items, JSONata spec implies the whole sequence might be assignable
            // or it could be an error, or only the first item is used.
            // Current model: variables hold single "values" (JsonNode or FunctionObject).
            // So, we take the FirstOrDefault() from the RHS sequence.

            object? valueToAssign = null;
            if (!rhsResultSeq.IsUndefined() && rhsResultSeq.Count > 0)
            {
                valueToAssign = rhsResultSeq.FirstOrDefault(); // This can be JsonNode or FunctionObject
            }
            // If rhsResultSeq is Undefined or empty, valueToAssign remains null.
            // JSONata spec: "The value resulting from evaluation of the expression is bound to the variable name."
            // If expression results in undefined, then undefined is bound. null represents undefined in bindings.

            // If valueToAssign is a JsonNode that might be mutated later, and bindings should hold pristine copies,
            // then cloning is needed here. FunctionObjects are not cloned.
            if (valueToAssign is JsonNode jsonNodeToAssign)
            {
                context.SetBinding(this.Variable.Value, jsonNodeToAssign.DeepClone());
            }
            else
            {
                // This covers FunctionObject (not cloned) and null (for undefined/empty sequences)
                context.SetBinding(this.Variable.Value, valueToAssign);
            }

            // "The value of the assignment expression is the value that was bound to the variable."
            // This means we should return a sequence representing what was actually assigned.
            // If rhsResultSeq was Undefined or empty, valueToAssign is null, so return Sequence.Undefined.
            // Otherwise, return a sequence with valueToAssign.
            if (valueToAssign == null && (rhsResultSeq.IsUndefined() || rhsResultSeq.Count == 0))
            {
                 return Sequence.Undefined;
            }
            return new Sequence(valueToAssign); // This will be Sequence(null) if valueToAssign is null but sequence was not Undefined/empty (e.g. `a := null`)
        }
    }
}
