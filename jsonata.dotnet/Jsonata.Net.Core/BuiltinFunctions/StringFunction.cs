using Jsonata.Net.Core.AstNodes;
using Jsonata.Net.Core.Evaluation;
using Jsonata.Net.Core.Exceptions; // For JsonataEvaluationException
using System.Text.Json.Nodes;
using System.Text.Json; // Added for JsonElement in debug logging
using System.Collections.Generic;
using System.Linq; // For FirstOrDefault

namespace Jsonata.Net.Core.BuiltinFunctions
{
    public sealed class StringFunction : IBuiltinFunction
    {
        public Sequence Execute(List<Sequence> arguments, EvaluationContext context)
        {
            if (arguments.Count == 0)
            {
                JsonNode? currentVal = context.CurrentInput;
                // ----------- END SIMPLIFIED DEBUGGING -----------

                if (currentVal == null)
                {
                    return Sequence.Undefined;
                }

                if (currentVal is JsonValue jsonValue)
                {
                    JsonElement element = jsonValue.GetValue<JsonElement>();
                    if (element.ValueKind == JsonValueKind.Null)
                    {
                        return new Sequence("null");
                    }
                    if (jsonValue.TryGetValue(out string? s))
                    {
                        return new Sequence(s);
                    }
                    return new Sequence(jsonValue.ToString());
                }
                else if (currentVal is JsonArray || currentVal is JsonObject)
                {
                    // No prettify argument provided, so throw error as per spec
                    throw new JsonataEvaluationException("S0201: $string function called without 'prettify' argument on an Array or Object");
                }
                throw new JsonataEvaluationException($"S0201: $string function cannot stringify context value of type {currentVal.GetType().Name}.");
            }

            // $string(arg1) or $string(arg1, prettify)
            object? arg1 = arguments[0].FirstOrDefault();

            bool prettify = false;
            if (arguments.Count == 2)
            {
                object? prettifyArgObj = arguments[1].FirstOrDefault();
                if (prettifyArgObj is JsonValue prettifyVal && prettifyVal.TryGetValue<bool>(out bool bVal))
                {
                    prettify = bVal;
                }
                else if (prettifyArgObj != null) // If prettify arg is present but not a boolean
                {
                    throw new JsonataEvaluationException("S0201: $string function 'prettify' argument must be a boolean.");
                }
            }

            if (arg1 == null)
            {
                if (arguments[0].IsUndefined())
                {
                    return Sequence.Undefined;
                }
                return new Sequence("null");
            }

            if (arg1 is JsonValue jsonVal)
            {
                // If TryGetValue<string> succeeds, it's a string.
                if (jsonVal.TryGetValue(out string? s)) return new Sequence(s);
                // If not a string, check if it's JSON null.
                else if (jsonVal.ToJsonString() == "null") return new Sequence("null");
                // Otherwise, it's a number or boolean JsonValue.
                else return new Sequence(jsonVal.ToString());
            }
            if (arg1 is string str) return new Sequence(str);
            if (arg1 is FunctionObject) throw new JsonataEvaluationException("S0201: $string function cannot stringify a function.");

            if (arg1 is JsonArray arr || arg1 is JsonObject obj)
            {
                if (prettify)
                {
                    JsonSerializerOptions options = new JsonSerializerOptions { WriteIndented = true };
                    // Return as a JSON string literal
                    return new Sequence(JsonValue.Create(JsonSerializer.Serialize(arg1 as JsonNode, options)));
                }
                else
                {
                    throw new JsonataEvaluationException("S0201: $string function called without 'prettify' argument on an Array or Object");
                }
            }

            if (arguments[0].IsUndefined()) return Sequence.Undefined;
            throw new JsonataEvaluationException($"S0201: $string function cannot stringify argument of type {arg1?.GetType().Name ?? "undefined"}.");
        }
    }
}
