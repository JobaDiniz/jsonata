using System.Text.Json.Nodes;
using System.Collections.Generic;

namespace Jsonata.Net.Core.Evaluation
{
    public sealed class EvaluationContext
    {
        public JsonNode? CurrentInput { get; } // Changed to nullable
        public JsonNode? RootInput { get; }
        public BuiltinFunctionsRegistry Functions { get; }

        // Bindings store JsonNode? or FunctionObject
        private readonly Dictionary<string, object?> _localBindings;

        public EvaluationContext? ParentContext { get; }

        public EvaluationContext(
            JsonNode? currentInput, // Changed to nullable
            BuiltinFunctionsRegistry functions,
            JsonNode? rootInput = null, // Ensured nullable for consistency
            Dictionary<string, object?>? initialLocalBindings = null,
            EvaluationContext? parent = null)
        {
            this.CurrentInput = currentInput;
            this.RootInput = rootInput ?? currentInput; // If root is null, it defaults to currentInput
            this._localBindings = initialLocalBindings ?? new Dictionary<string, object?>();
            this.ParentContext = parent;
            this.Functions = functions;
        }

        public bool TryGetBinding(string name, out object? value)
        {
            if (name == "$")
            {
                value = this.CurrentInput;
                return true;
            }
            if (this._localBindings.TryGetValue(name, out value))
            {
                return true;
            }

            if (this.ParentContext != null)
            {
                return this.ParentContext.TryGetBinding(name, out value);
            }

            value = null;
            return false;
        }

        public void SetBinding(string name, object? value) // Changed type
        {
            this._localBindings[name] = value;
        }

        // Note: This method's logic for merging/flattening bindings might need careful review
        // for strict lexical scoping if it were to be used for function closures' parent scopes.
        // However, for function calls, we are creating contexts by directly setting the ParentContext
        // to the closure's captured context, which is the correct approach for lexical scope.
        public EvaluationContext CreateChildContextWithMergedBindings(JsonNode? newCurrentInput, Dictionary<string, object?>? newBindingsToMerge = null) // newCurrentInput also nullable
        {
            var allParentBindings = new Dictionary<string, object?>();
            EvaluationContext? currentForMerge = this;
            // Iterate to gather all bindings from the hierarchy, current scope's bindings taking precedence at each level.
            // This loop needs to be reversed to ensure correct precedence (innermost scope first).
            Stack<EvaluationContext> contextStack = new Stack<EvaluationContext>();
            while(currentForMerge != null)
            {
                contextStack.Push(currentForMerge);
                currentForMerge = currentForMerge.ParentContext;
            }

            while(contextStack.Count > 0)
            {
                EvaluationContext contextNode = contextStack.Pop();
                foreach(var pair in contextNode._localBindings)
                {
                    allParentBindings[pair.Key] = pair.Value; // Deeper (closer to root) bindings are added first, potentially overridden by nearer ones
                }
            }

            if (newBindingsToMerge != null)
            {
                foreach (var pair in newBindingsToMerge)
                {
                    allParentBindings[pair.Key] = pair.Value; // New bindings overwrite
                }
            }
            // The new child's direct parent is this current context.
            return new EvaluationContext(newCurrentInput, this.Functions, this.RootInput, allParentBindings, this);
        }
    }
}
