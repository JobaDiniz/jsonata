using System.Text.Json.Nodes;
using Jsonata.Net.Core.Evaluation;
using Jsonata.Net.Core.Exceptions;
using System.Collections.Generic;
using System.Linq;

namespace Jsonata.Net.Core.AstNodes
{
    public sealed class ArrayConstructorNode : AstNode
    {
        public List<AstNode> Elements { get; }

        public ArrayConstructorNode(List<AstNode> elements)
        {
            this.Elements = elements;
        }

        public override Sequence Evaluate(JsonNode? input, EvaluationContext context)
        {
            JsonArray array = new JsonArray();
            foreach (AstNode elementNode in this.Elements)
            {
                Sequence elementSeq = elementNode.Evaluate(input, context);
                if (elementSeq.IsUndefined())
                {
                    continue;
                }
                /* JSONata array constructor flattening: If an element evaluates to a sequence, its items are inserted.
                   If it's an actual JSON array value (as a single item in a sequence), it's added as a single element. */
                if (elementSeq.Count == 1 && elementSeq.FirstOrDefault() is JsonArray nestedArray)
                {
                    array.Add(nestedArray.DeepClone());
                }
                else
                {
                    foreach (JsonNode? value in elementSeq)
                    {
                        array.Add(value?.DeepClone());
                    }
                }
            }
            // Explicitly cast 'array' to JsonNode? to ensure the Sequence(JsonNode? item, ...) constructor is chosen,
            // rather than Sequence(IEnumerable<JsonNode?> items) which would flatten the array into the sequence.
            return new Sequence((JsonNode?)array);
        }
    }
}
