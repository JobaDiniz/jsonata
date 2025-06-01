using Jsonata.Net.Core.Evaluation;
using Jsonata.Net.Core.Exceptions;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Linq;
using System.Text; // For StringBuilder

namespace Jsonata.Net.Core.BuiltinFunctions
{
    public sealed class JoinFunction : IBuiltinFunction
    {
        private const string ERROR_CODE_BAD_ARG_COUNT = "T0410";
        private const string ERROR_CODE_ARG_ARRAY_BAD_TYPE = "S0219";
        private const string ERROR_CODE_ARG_SEP_BAD_TYPE = "S0220";
        private const string ERROR_CODE_ARRAY_ITEM_BAD_TYPE = "S0221";

        public Sequence Execute(List<Sequence> arguments, EvaluationContext context)
        {
            string functionName = "$join";
            int argCount = arguments.Count;

            JsonNode? arrayNode;
            JsonNode? separatorNode = null;

            if (argCount == 0) // $join() -> array from context, default separator
            {
                arrayNode = context.CurrentInput;
            }
            else if (argCount == 1) // $join(array) or $join(separator)
            {
                // If first arg is array, assume $join(array)
                // Else, assume $join(separator) with array from context
                object? firstArgItem = arguments[0].FirstOrDefault();
                if (firstArgItem is JsonArray)
                {
                    arrayNode = (JsonArray)firstArgItem;
                }
                else
                {
                    arrayNode = context.CurrentInput;
                    separatorNode = firstArgItem as JsonNode;
                }
            }
            else if (argCount == 2) // $join(array, separator)
            {
                arrayNode = arguments[0].FirstOrDefault() as JsonNode;
                separatorNode = arguments[1].FirstOrDefault() as JsonNode;
            }
            else
            {
                throw new JsonataEvaluationException($"{ERROR_CODE_BAD_ARG_COUNT}: {functionName} expects 0, 1, or 2 arguments. Got {argCount}.");
            }

            // Validate arrayNode
            if (!(arrayNode is JsonArray sourceArray))
            {
                if (arrayNode == null || (arrayNode is JsonValue tempArrNode && tempArrNode.ToJsonString() == "null"))
                {
                     throw new JsonataEvaluationException($"{ERROR_CODE_ARG_ARRAY_BAD_TYPE}: Argument 'array' to {functionName} must be an array of strings. Got {(arrayNode == null ? "undefined" : "null")}.");
                }
                throw new JsonataEvaluationException($"{ERROR_CODE_ARG_ARRAY_BAD_TYPE}: Argument 'array' to {functionName} must be an array of strings. Got {arrayNode.GetType().Name}.");
            }

            // Validate separator (optional)
            string separator = "";
            if (separatorNode != null)
            {
                if (separatorNode is JsonValue sepValNode && sepValNode.TryGetValue(out string? sVal))
                {
                    separator = sVal;
                }
                else if (separatorNode is JsonValue tempSepValNode && tempSepValNode.ToJsonString() == "null") // null separator is error
                {
                    throw new JsonataEvaluationException($"{ERROR_CODE_ARG_SEP_BAD_TYPE}: Argument 'separator' to {functionName} must be a string. Got null.");
                }
                else
                {
                    throw new JsonataEvaluationException($"{ERROR_CODE_ARG_SEP_BAD_TYPE}: Argument 'separator' to {functionName} must be a string. Got {separatorNode.GetType().Name}.");
                }
            }

            StringBuilder sb = new StringBuilder();
            bool first = true;
            foreach (JsonNode? itemNode in sourceArray)
            {
                string? itemStr;
                if (itemNode is JsonValue val && val.TryGetValue(out itemStr))
                {
                    // itemStr is already assigned
                }
                else if (itemNode == null || (itemNode is JsonValue jv && jv.ToJsonString() == "null"))
                {
                    itemStr = "null"; // $join([null]) -> "null"
                }
                else if (itemNode is JsonValue itemValue) // itemNode is a JsonValue but not a string or null
                {
                    if (itemValue.TryGetValue<bool>(out bool bVal))
                    {
                        itemStr = bVal ? "true" : "false";
                    }
                    // Check for various number types. Order might matter if TryGetValue is permissive.
                    else if (itemValue.TryGetValue<decimal>(out decimal decVal)) { itemStr = decVal.ToString(System.Globalization.CultureInfo.InvariantCulture); }
                    else if (itemValue.TryGetValue<double>(out double dblVal)) { itemStr = dblVal.ToString(System.Globalization.CultureInfo.InvariantCulture); }
                    else if (itemValue.TryGetValue<long>(out long longVal)) { itemStr = longVal.ToString(System.Globalization.CultureInfo.InvariantCulture); }
                    else if (itemValue.TryGetValue<int>(out int intVal)) { itemStr = intVal.ToString(System.Globalization.CultureInfo.InvariantCulture); }
                    // Add other numeric types if necessary (float, short, byte)
                    else // Non-string, non-null, non-bool, non-number JsonValue - should not occur with typical JSON
                    {
                         throw new JsonataEvaluationException($"{ERROR_CODE_ARRAY_ITEM_BAD_TYPE}: Items in 'array' argument to {functionName} must be strings or simple types. Found unhandled JsonValue: {itemValue.ToJsonString()}.");
                    }
                }
                else // Not a JsonValue (e.g. JsonObject, JsonArray)
                {
                    throw new JsonataEvaluationException($"{ERROR_CODE_ARRAY_ITEM_BAD_TYPE}: Items in 'array' argument to {functionName} must be strings or simple types. Found {itemNode?.GetType().Name ?? "null"}.");
                }

                if (!first)
                {
                    sb.Append(separator);
                }
                sb.Append(itemStr);
                first = false;
            }

            return new Sequence(JsonValue.Create(sb.ToString()));
        }
    }
}
