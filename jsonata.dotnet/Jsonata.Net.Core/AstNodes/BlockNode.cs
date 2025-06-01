using System.Collections.Generic;
using System.Text.Json.Nodes;
using Jsonata.Net.Core.Evaluation;

namespace Jsonata.Net.Core.AstNodes
{
    /**
     * Represents a block of expressions, typically found in parentheses like (expr1; expr2; expr3),
     * often used as a step in a path expression: path.(block).
     * The result of the block is the result of its last expression.
     * Assignments and other effects should persist between expressions in the block.
     */
    public sealed class BlockNode : AstNode
    {
        public List<AstNode> Expressions { get; }

        public BlockNode(List<AstNode> expressions)
        {
            this.Expressions = expressions ?? new List<AstNode>();
        }

        public override Sequence Evaluate(JsonNode? input, EvaluationContext context)
        {
            Sequence result = Sequence.Undefined; // Default if block is empty

            // Create a new context for this block so variable assignments are contained
            // unless JSONata spec implies assignments should leak out.
            // JSONata $() (block) implies a new scope for $, but assignments are tricky.
            // Let's assume assignments modify the passed 'context' for now,
            // consistent with how `$x := val; $x` would work in a sequence.
            // If a truly isolated scope is needed for the block, a child context would be created here.
            // For `a.($b := b; $c := c; $b+$c)`, assignments to $b, $c should be in a scope
            // that $b+$c can see. The context of `a` is passed to this BlockNode.

            // EvaluationContext blockContext = context.CreateChildContext(input); // Example if isolation needed for $
            // For now, assume assignments modify the current context or its accessible parents.

            foreach (AstNode exprNode in this.Expressions)
            {
                // The 'input' for each expression in the block is the same 'input' that the block received.
                // The 'context' carries forward any modifications from previous expressions in the block.
                result = exprNode.Evaluate(input, context);
            }
            return result; // Result of the last expression
        }
    }
}
