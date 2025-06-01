using System.Collections.Generic;
using System.Text.Json.Nodes;
using Jsonata.Net.Core.Evaluation;

namespace Jsonata.Net.Core.AstNodes
{
    public sealed class FunctionDefinitionNode : AstNode
    {
        public List<VariableNode> Parameters { get; }
        public AstNode Body { get; }

        public FunctionDefinitionNode(List<VariableNode> parameters, AstNode body)
        {
            this.Parameters = parameters;
            this.Body = body;
        }

        public override Sequence Evaluate(JsonNode? input, EvaluationContext context)
        {
            // When a function definition is "evaluated" (e.g. in an assignment context $f := function(){...}),
            // it should produce a closure object that captures the current context.
            // For now, this will be a placeholder. The actual Closure object will be defined and returned later.
            // This also means CallFunctionNode will need to expect this closure object.

            // TODO: Create and return a FunctionClosure object here.
            // A FunctionClosure will bundle this FunctionDefinitionNode (or its parts)
            // with the current EvaluationContext 'context'.

            // Placeholder: For initial parsing and AST structure, returning the node itself or a simple marker.
            // This will need to be replaced by actual closure creation.
            // Jsonata.Net.Core.Function (from the original library) might be what we need to return,
            // wrapped in a sequence. That Function object would be the closure.

            // When a function definition is "evaluated" (e.g., in an assignment),
            // it produces a FunctionObject (closure).
            // This closure can be directly stored in a Sequence since Sequence now holds object?.
            // The 'input' is the specific data context item at definition time.
            // The 'context' is the broader evaluation context (for parent variable bindings).
            FunctionObject closure = new FunctionObject(this, context, input);
            return new Sequence(closure);
        }
    }
}
