using System.Text.Json.Nodes;
using Jsonata.Net.Core.Evaluation;
using Jsonata.Net.Core.Exceptions;
using System.Collections.Generic;

namespace Jsonata.Net.Core.AstNodes
{
    public sealed class KeyValuePairNode
    {
        public AstNode Key { get; }
        public AstNode Value { get; }
        public KeyValuePairNode(AstNode key, AstNode value)
        {
            this.Key = key; this.Value = value;
        }
    }

    public sealed class ObjectConstructorNode : AstNode
    {
        public List<KeyValuePairNode> Pairs { get; }

        public ObjectConstructorNode(List<KeyValuePairNode> pairs)
        {
            this.Pairs = pairs;
        }

        public override Sequence Evaluate(JsonNode? input, EvaluationContext context)
        {
            JsonObject obj = new JsonObject();
            foreach (KeyValuePairNode pairNode in this.Pairs)
            {
                Sequence keySeq = pairNode.Key.Evaluate(input, context);
                object? keyObj = keySeq.AsSingle(); // AsSingle returns object?

                string? keyStr = null;
                if (keyObj is JsonValue keyValue && keyValue.TryGetValue<string>(out string? tempKeyStr))
                {
                    keyStr = tempKeyStr;
                }

                if (string.IsNullOrEmpty(keyStr))
                {
                    throw new JsonataEvaluationException("Object key must evaluate to a single non-empty string.");
                }

                Sequence valueSeq = pairNode.Value.Evaluate(input, context);

                if (valueSeq.IsUndefined())
                {
                    // If a path expression results in undefined, the binding is removed from the object structure. (S0206)
                    continue;
                }

                JsonNode? finalValueNode; // This must be a JsonNode to be added to JsonObject

                if (valueSeq.Count == 1)
                {
                    object? singleItem = valueSeq.FirstOrDefault();
                    if (singleItem is JsonNode jn)
                    {
                        finalValueNode = jn.DeepClone();
                    }
                    else if (singleItem is FunctionObject)
                    {
                        finalValueNode = JsonValue.Create("[JSONata Function]");
                    }
                    else if (singleItem == null) // Explicit JSON null in sequence
                    {
                        finalValueNode = null;
                    }
                    else
                    {
                        // Should not happen if sequence items are only JsonNode or FunctionObject
                        throw new JsonataEvaluationException($"Unexpected type in object construction value: {singleItem?.GetType().Name}");
                    }
                }
                else // valueSeq.Count == 0 (empty) or valueSeq.Count > 1 (array)
                {
                    // If valueSeq is empty (but not Undefined), it means an empty array `[]` in JSON output.
                    JsonArray valArray = new JsonArray();
                    foreach(object? item in valueSeq)
                    {
                        if (item is JsonNode jn)
                        {
                            valArray.Add(jn.DeepClone());
                        }
                        else if (item is FunctionObject)
                        {
                            valArray.Add(JsonValue.Create("[JSONata Function]"));
                        }
                        else if (item == null)
                        {
                            valArray.Add(null);
                        }
                        else
                        {
                            throw new JsonataEvaluationException($"Unexpected type in object construction array value: {item?.GetType().Name}");
                        }
                    }
                    finalValueNode = valArray;
                }
                obj[keyStr!] = finalValueNode; // keyStr is checked for null/empty above
            }
            return new Sequence(obj);
        }
    }
}
