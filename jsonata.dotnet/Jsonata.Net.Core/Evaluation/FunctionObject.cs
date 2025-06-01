using Jsonata.Net.Core.AstNodes;
using System.Text.Json.Nodes;

namespace Jsonata.Net.Core.Evaluation
{
    public sealed class FunctionObject
    {
        public FunctionDefinitionNode Definition { get; }
        public EvaluationContext CapturedContext { get; } // For lexical parent variable scope
        public JsonNode? DefinitionTimeInput { get; }     // For $ in function body

        public FunctionObject(FunctionDefinitionNode definition, EvaluationContext capturedContext, JsonNode? definitionTimeInput)
        {
            this.Definition = definition;
            this.CapturedContext = capturedContext;
            this.DefinitionTimeInput = definitionTimeInput;
        }
    }
}
