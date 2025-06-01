using System.Text.Json.Nodes;
using Jsonata.Net.Core.Evaluation;
using Jsonata.Net.Core.Exceptions;
using System.Collections.Generic;
using System.Linq;

namespace Jsonata.Net.Core.AstNodes
{
    public sealed class NameNode : AstNode
    {
        public string Value { get; }
        private readonly string _originalValueForDiagnostic;

        public NameNode(string value)
        {
            this._originalValueForDiagnostic = value; // Store original value
            // Removed the exception to allow execution to proceed to Evaluate.
            // The fact that the exception was thrown in the previous step confirmed
            // that the constructor is called with "Product Name" (with space).
            this.Value = value;
        }

        public override Sequence Evaluate(JsonNode? input, EvaluationContext context)
        {
            // Unconditional diagnostic
            System.Console.WriteLine($"DIAGNOSTIC [NameNode.Evaluate]: Value='{this.Value}', OriginalValue='{this._originalValueForDiagnostic}'");

            if (string.IsNullOrEmpty(this.Value))
            {
                return new Sequence(input);
            }

            if (input is JsonArray arr)
            {
                List<object?> results = new List<object?>();
                foreach (JsonNode? itemInArray in arr)
                {
                    if (itemInArray is JsonObject objInArray && objInArray.TryGetPropertyValue(this.Value, out JsonNode? valFromArray))
                    {
                        results.Add(valFromArray?.DeepClone());
                    }
                }
                return new Sequence(results);
            }
            else if (input is JsonObject obj && obj.TryGetPropertyValue(this.Value, out JsonNode? val))
            {
                if (this.Value == "Product Name") // Check for "Product Name" with space
                {
                    System.Console.WriteLine($"DIAGNOSTIC [NameNode(\"{this.Value}\")]: Input: {input?.ToJsonString() ?? "null"}. obj.TryGetPropertyValue returned true. val: {val?.ToJsonString() ?? "null"}");
                }
                return new Sequence(val?.DeepClone());
            }

            if (this.Value == "Product Name") // Check for "Product Name" with space
            {
                 System.Console.WriteLine($"DIAGNOSTIC [NameNode(\"{this.Value}\")]: Input: {input?.ToJsonString() ?? "null"}. obj.TryGetPropertyValue returned false or input was not JsonObject.");
            }
            return Sequence.Undefined;
        }
    }
}
