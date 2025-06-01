using System.Collections.Generic;
using System.Text.Json.Nodes;
using Jsonata.Net.Core.Evaluation;
using Jsonata.Net.Core.Exceptions;

namespace Jsonata.Net.Core.AstNodes
{
    /**
     * Represents an object constructor applied as a transform to an input expression.
     * Syntax: input_expression { key_expr: value_expr, ... }
     * The input_expression provides the context for evaluating the key-value pairs.
     */
    public sealed class ObjectTransformNode : AstNode
    {
        public AstNode InputExpression { get; }
        public List<KeyValuePairNode> Pairs { get; }

        public ObjectTransformNode(AstNode inputExpression, List<KeyValuePairNode> pairs)
        {
            this.InputExpression = inputExpression;
            this.Pairs = pairs;
        }

        public override Sequence Evaluate(JsonNode? outerContextInput, EvaluationContext context)
        {
            Sequence lhsSequence = this.InputExpression.Evaluate(outerContextInput, context);

            if (lhsSequence.IsUndefined())
            {
                return Sequence.Undefined;
            }

            // Per JSONata spec: `expr{...}` - "If the selector returns an array, then the constructor is applied ...
            // and the resulting objects are merged into a single object".
            // This implies `[]{...}` results in `{}`.
            // If lhsSequence is an empty sequence (but not Undefined), it should result in an empty object.
            if (lhsSequence.Count == 0) // This covers empty arrays `[]` or paths yielding no results.
            {
                return new Sequence(new JsonObject());
            }

            IEnumerable<object?> itemsToProcess;
            object? firstLhsItem = lhsSequence.FirstOrDefault();

            // If the LHS sequence is a single item which is itself a JsonArray (e.g. `[[1,2,3]]{$ kvadrat : $ * $}`),
            // then we iterate over the elements of that inner JsonArray.
            // Otherwise, we iterate over the items in the LHS sequence directly.
            if (lhsSequence.Count == 1 && firstLhsItem is JsonArray arr)
            {
                itemsToProcess = arr.Cast<object?>();
                if (!itemsToProcess.Any()) // Handles `[[]]{...}` -> `{}`
                {
                    return new Sequence(new JsonObject());
                }
            }
            else
            {
                itemsToProcess = lhsSequence;
            }

            JsonObject mergedObject = new JsonObject();
            bool anyItemProcessedSuccessfully = false;

            foreach (object? currentItemAnyType in itemsToProcess)
            {
                if (!(currentItemAnyType is JsonNode currentItemJsonNode))
                {
                    // Non-JsonNode items in the sequence are effectively skipped / do not contribute to the merge.
                    continue;
                }
                anyItemProcessedSuccessfully = true;

                JsonObject currentItemConstructedObj = new JsonObject();
                EvaluationContext itemContext = new EvaluationContext(currentItemJsonNode, context.Functions, context.RootInput, null, context);

                foreach (KeyValuePairNode pairNode in this.Pairs)
                {
                    Sequence keySeq = pairNode.Key.Evaluate(currentItemJsonNode, itemContext);
                    object? keyObj = keySeq.AsSingle();
                    string? keyStr = null;

                    // Key stringification (simplified, assuming key must be string as per ObjectConstructorNode)
                    // A more robust key stringification might be needed if keys can be numbers etc.
                    // and need conversion similar to '&' operator. For now, align with ObjectConstructorNode.
                    if (keyObj is JsonValue kv_key && kv_key.TryGetValue<string>(out string? tempKey))
                    {
                        keyStr = tempKey;
                    }

                    if (string.IsNullOrEmpty(keyStr))
                    {
                        // Per JSONata spec for object constructors, if a key expression is not a string,
                        // or is an empty string, it's an error (S0205).
                        // Here, for transform, we might skip this pair for this item's contribution.
                        // Or, if strict, throw an error. Let's skip.
                        // If keySeq was undefined, keyObj is null, so keyStr is null/empty.
                        continue;
                    }

                    Sequence valueSeq = pairNode.Value.Evaluate(currentItemJsonNode, itemContext);
                    if (valueSeq.IsUndefined())
                    {
                        // If a value evaluates to undefined, that key-value pair is not included in the output for this item.
                        continue;
                    }

                    JsonNode? finalValueNode;
                    if (valueSeq.Count == 1)
                    {
                        object? singleItem = valueSeq.FirstOrDefault();
                        if (singleItem is JsonNode jn) finalValueNode = jn.DeepClone();
                        else if (singleItem is FunctionObject) finalValueNode = JsonValue.Create("[JSONata Function]");
                        else if (singleItem == null) finalValueNode = null; // Explicit JSON null
                        else throw new JsonataEvaluationException($"ObjectTransform: Unexpected type for single item value: {singleItem?.GetType().Name}");
                    }
                    else // sequence has 0 or >1 items, convert to JsonArray
                    {
                        JsonArray valArray = new JsonArray();
                        foreach(object? item in valueSeq)
                        {
                            if (item is JsonNode jn) valArray.Add(jn.DeepClone());
                            else if (item is FunctionObject) valArray.Add(JsonValue.Create("[JSONata Function]"));
                            else if (item == null) valArray.Add(null);
                            else throw new JsonataEvaluationException($"ObjectTransform: Unexpected type in array value: {item?.GetType().Name}");
                        }
                        finalValueNode = valArray;
                    }
                    currentItemConstructedObj[keyStr!] = finalValueNode;
                }

                // Merge properties from currentItemConstructedObj into mergedObject
                foreach (KeyValuePair<string, JsonNode?> prop in currentItemConstructedObj)
                {
                    mergedObject[prop.Key] = prop.Value?.DeepClone(); // Standard behavior: later values overwrite earlier ones.
                }
            }

            // If InputExpression was not undefined, but no processable JsonNode items were found in its sequence
            // (e.g., `("a", "b"){...}` or `(1,2){...}`), the result should be an empty object.
            if (!anyItemProcessedSuccessfully && lhsSequence.Count > 0)
            {
                return new Sequence(new JsonObject());
            }

            // The result of an object transform is always a single object (merged if input was array).
            return new Sequence(mergedObject);
        }
    }
}
