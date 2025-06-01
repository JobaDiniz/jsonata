using Jsonata.Net.Core.AstNodes;
using Jsonata.Net.Core.Evaluation;
using Jsonata.Net.Core.Parsing;
using System.Text.Json.Nodes;
using System.Text.Json; // Added for JsonElement
using System.Linq; // Required for ToArray
using Jsonata.Net.Core.BuiltinFunctions; // Added for StringFunction

namespace Jsonata.Net.Core
{
    public sealed class JsonataEvaluator
    {
        private readonly AstNode _rootNode;
        private readonly BuiltinFunctionsRegistry _functionsRegistry;

        public JsonataEvaluator(string expression)
        {
            if (string.IsNullOrEmpty(expression))
            {
                throw new System.ArgumentNullException(nameof(expression));
            }
            JsonataParser parser = new JsonataParser(expression);
            this._rootNode = parser.Parse();
            this._functionsRegistry = new BuiltinFunctionsRegistry();
            this.RegisterDefaultBuiltinFunctions();
        }

        private void RegisterDefaultBuiltinFunctions()
        {
            this._functionsRegistry.RegisterFunction("$string", new StringFunction());
            this._functionsRegistry.RegisterFunction("$length", new LengthFunction());
            this._functionsRegistry.RegisterFunction("$uppercase", new UppercaseFunction());
            this._functionsRegistry.RegisterFunction("$lowercase", new LowercaseFunction());
            this._functionsRegistry.RegisterFunction("$trim", new TrimFunction());
            this._functionsRegistry.RegisterFunction("$substring", new SubstringFunction());
            this._functionsRegistry.RegisterFunction("$substringBefore", new SubstringBeforeFunction());
            this._functionsRegistry.RegisterFunction("$substringAfter", new SubstringAfterFunction());
            this._functionsRegistry.RegisterFunction("$pad", new PadFunction());
            this._functionsRegistry.RegisterFunction("$contains", new ContainsFunction());
            this._functionsRegistry.RegisterFunction("$split", new SplitFunction());
            this._functionsRegistry.RegisterFunction("$join", new JoinFunction());
            this._functionsRegistry.RegisterFunction("$base64encode", new Base64EncodeFunction());
            this._functionsRegistry.RegisterFunction("$base64decode", new Base64DecodeFunction());
            this._functionsRegistry.RegisterFunction("$encodeUrlComponent", new EncodeUrlComponentFunction());
            this._functionsRegistry.RegisterFunction("$encodeUrl", new EncodeUrlFunction());
            this._functionsRegistry.RegisterFunction("$decodeUrlComponent", new DecodeUrlComponentFunction());
            this._functionsRegistry.RegisterFunction("$decodeUrl", new DecodeUrlFunction());
            this._functionsRegistry.RegisterFunction("$match", new MatchFunction());
            this._functionsRegistry.RegisterFunction("$replace", new ReplaceFunction());
            // Other default functions will be registered here
        }

        public void RegisterFunction(string name, IBuiltinFunction function)
        {
            this._functionsRegistry.RegisterFunction(name, function);
        }

        public JsonNode? Evaluate(JsonNode? inputJson)
        {
            // With EvaluationContext.CurrentInput now nullable, we can pass inputJson directly.
            // RootInput is also inputJson.
            EvaluationContext context = new EvaluationContext(inputJson, this._functionsRegistry, inputJson);

            // The AstNode.Evaluate method's first parameter is the context item for that specific node's evaluation.
            // For the root node of the expression, this is the CurrentInput from the context.
            Sequence resultSequence = this._rootNode.Evaluate(context.CurrentInput, context);

            if (resultSequence.IsUndefined())
            {
                // If an expression evaluates to undefined, it means no result.
                return null;
            }

            if (resultSequence.Count == 0)
            {
                // An empty sequence.
                // If this sequence came from an empty array constructor like `[]`,
                // it should be an empty JsonArray.
                // If it's from a path that found nothing, it's more like undefined, so null.
                // For now, consistently return null for truly empty/undefined sequences.
                // The original perl script suggested this, but an empty sequence from `[]` should be `JsonArray`.
                // Let's assume for now if it's empty and not from an explicit array constructor it's null.
                // This might need refinement: if resultSequence.SourceNode is ArrayConstructorNode and count is 0, return new JsonArray().
                // For now, sticking to simpler "empty/undefined -> null".
                return null;
            }

            if (resultSequence.Count == 1)
            {
                object? singleItem = resultSequence.FirstOrDefault();
                if (singleItem is FunctionObject)
                {
                    return JsonValue.Create("[JSONata Function]");
                }
                if (singleItem is JsonNode jsonNode)
                {
                    return jsonNode.DeepClone();
                }
                // Handle primitives that might not be JsonNode directly but can be wrapped
                if (singleItem is string s) return JsonValue.Create(s);
                if (singleItem is bool b) return JsonValue.Create(b);
                if (singleItem is int i) return JsonValue.Create(i);
                if (singleItem is long l) return JsonValue.Create(l);
                if (singleItem is decimal d) return JsonValue.Create(d); // System.Text.Json supports decimal
                if (singleItem is double dbl) return JsonValue.Create(dbl);
                if (singleItem is float flt) return JsonValue.Create(flt);
                if (singleItem == null) return null; // Explicit JSON null if the single item is null

                // Fallback for other types, though ideally sequence items should be JSON-representable
                return JsonValue.Create(singleItem.ToString());
            }

            // Sequence of multiple values: return as a JSON array
            JsonArray array = new JsonArray();
            foreach (object? item in resultSequence)
            {
                if (item is FunctionObject)
                {
                    array.Add(JsonValue.Create("[JSONata Function]"));
                }
                else if (item is JsonNode jsonNodeItem)
                {
                    array.Add(jsonNodeItem.DeepClone());
                }
                else if (item is string s)
                {
                    array.Add(JsonValue.Create(s));
                }
                else if (item is bool b)
                {
                    array.Add(JsonValue.Create(b));
                }
                else if (item is int i)
                {
                    array.Add(JsonValue.Create(i));
                }
                else if (item is long l)
                {
                    array.Add(JsonValue.Create(l));
                }
                else if (item is decimal d)
                {
                    array.Add(JsonValue.Create(d));
                }
                else if (item is double dbl)
                {
                    array.Add(JsonValue.Create(dbl));
                }
                else if (item is float flt)
                {
                    array.Add(JsonValue.Create(flt));
                }
                else if (item == null) // Handles explicit nulls in the sequence
                {
                    array.Add(null);
                }
                else
                {
                    // Fallback for unexpected types in sequence
                    array.Add(JsonValue.Create(item.ToString()));
                }
            }
            return array;
        }
    }
}
