using System.Text.Json.Nodes;
using System.Text.Json; // Added for JsonElement
using Jsonata.Net.Core.Evaluation;
using Jsonata.Net.Core.Exceptions;
using System.Collections.Generic;
using System.Linq;
using System; // For NotImplementedException

namespace Jsonata.Net.Core.AstNodes
{
    public sealed class FunctionCallNode : AstNode
    {
        public AstNode Procedure { get; }
        public List<AstNode> Arguments { get; }

        public FunctionCallNode(AstNode procedure, List<AstNode> arguments)
        {
            this.Procedure = procedure;
            this.Arguments = arguments;
        }

        public override Sequence Evaluate(JsonNode? input, EvaluationContext context)
        {
            // Try to resolve Procedure as a built-in function name first
            string? potentialFuncName = null;
            if (this.Procedure is NameNode nn)
            {
                potentialFuncName = nn.Value;
            }
            else if (this.Procedure is VariableNode vn)
            {
                potentialFuncName = vn.Value; // e.g. $uppercase -> context.TryGetBinding("$uppercase") might return a string or FunctionObject
                                            // If it's a string that matches a built-in, it should be treated as such.
                                            // If it's a FunctionObject, that's handled later.
            }

            if (potentialFuncName != null && context.Functions.TryGetFunction(potentialFuncName, out IBuiltinFunction? builtinFunc))
            {
                List<Sequence> evaluatedArgsForBuiltin = new List<Sequence>();
                foreach (AstNode argNode in this.Arguments)
                {
                    evaluatedArgsForBuiltin.Add(argNode.Evaluate(input, context));
                }
                return builtinFunc.Execute(evaluatedArgsForBuiltin, context);
            }

            // If not a registered built-in function, evaluate the Procedure expression
            Sequence procedureSeq = this.Procedure.Evaluate(input, context);
            object? procedureObj = procedureSeq.FirstOrDefault();

            if (procedureObj is FunctionObject closure) // User-defined function (closure)
            {
                FunctionDefinitionNode funcDef = closure.Definition;
                EvaluationContext capturedContext = closure.CapturedContext;

                // Evaluate arguments in the current context (caller's context)
                List<object?> evaluatedArgs = new List<object?>();
                foreach (AstNode argNode in this.Arguments)
                {
                    Sequence argSeq = argNode.Evaluate(input, context);
                    evaluatedArgs.Add(argSeq.FirstOrDefault());
                }

                // Check arity
                if (evaluatedArgs.Count != funcDef.Parameters.Count)
                {
                    throw new JsonataEvaluationException(
                        $"E0412: Argument count mismatch for function '{GetProcedureName(funcDef)}'. Expected {funcDef.Parameters.Count}, got {evaluatedArgs.Count}."
                    );
                }

                // Create new context for function execution
                Dictionary<string, object?> functionLocalBindings = new Dictionary<string, object?>();
                for (int i = 0; i < funcDef.Parameters.Count; i++)
                {
                    functionLocalBindings[funcDef.Parameters[i].Value] = evaluatedArgs[i];
                }

                EvaluationContext functionCallContext = new EvaluationContext(
                    closure.DefinitionTimeInput ?? JsonValue.Create((JsonElement?)null), // Context item for the function body
                    context.Functions,                                // Pass the functions registry
                    capturedContext.RootInput,                        // RootInput from the captured context
                    functionLocalBindings,
                    capturedContext                                   // Parent for lexical scoping
                );

                return funcDef.Body.Evaluate(closure.DefinitionTimeInput ?? JsonValue.Create((JsonElement?)null), functionCallContext);
            }

            // Error handling if not a built-in or a user-defined function
            string procNameForError = GetProcedureName(null); // Get the original name/type of the procedure expression

            if (procedureSeq.IsUndefined())
            {
                throw new JsonataEvaluationException($"E0410: Function '{procNameForError}' does not exist or path expression did not resolve to a function.");
            }

            throw new JsonataEvaluationException($"E0410: Attempted to call a non-function. Procedure '{procNameForError}' evaluated to type '{procedureObj?.GetType().Name ?? "undefined"}'.");
        }

        // Helper to get a printable name for the procedure part of the function call
        private string GetProcedureName(FunctionDefinitionNode? funcDef) // funcDef is for user-defined functions if known
        {
            if (funcDef != null)
            {
                // Attempt to find a name if it was assigned, e.g. $f.
                // This is hard without knowing the variable it was assigned to.
                // For now, use a generic name for user-defined functions.
                return $"user-defined function with {funcDef.Parameters.Count} parameter(s)";
            }
            if (this.Procedure is NameNode nameNode) return nameNode.Value;
            if (this.Procedure is VariableNode varNode) return varNode.Value;
            return "<complex procedure>";
        }
    }
}
