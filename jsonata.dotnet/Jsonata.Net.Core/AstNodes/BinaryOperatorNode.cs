using System.Text.Json.Nodes;
using System.Text.Json; // Added for JsonElement and JsonValueKind
using Jsonata.Net.Core.Evaluation;
using Jsonata.Net.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Jsonata.Net.Core.AstNodes
{
    public sealed class BinaryOperatorNode : AstNode
    {
        public AstNode Lhs { get; }
        public string OperatorSymbol { get; }
        public AstNode Rhs { get; }

        public BinaryOperatorNode(AstNode lhs, string operatorSymbol, AstNode rhs)
        {
            this.Lhs = lhs;
            this.OperatorSymbol = operatorSymbol;
            this.Rhs = rhs;
        }

        public override Sequence Evaluate(JsonNode? input, EvaluationContext context)
        {
            // Handle logical operators separately due to short-circuiting
            if (OperatorSymbol == "and" || OperatorSymbol == "or")
            {
                Sequence lhsSeq = Lhs.Evaluate(input, context);
                bool lTruthy = Sequence.IsTruthy(lhsSeq); // IsTruthy handles new object? type in Sequence
                if (OperatorSymbol == "and")
                {
                    if (!lTruthy) return lhsSeq; // Return the sequence that determined the outcome
                    return Rhs.Evaluate(input, context);
                }
                else // OperatorSymbol must be "or"
                {
                    if (lTruthy) return lhsSeq; // Return the sequence that determined the outcome
                    return Rhs.Evaluate(input, context);
                }
            }

            Sequence lhsResultSeq = Lhs.Evaluate(input, context);
            Sequence rhsResultSeq = Rhs.Evaluate(input, context);

            if (lhsResultSeq.IsUndefined() && rhsResultSeq.IsUndefined())
            {
                if (OperatorSymbol == "=") return new Sequence(JsonValue.Create(true));
                if (OperatorSymbol == "!=") return new Sequence(JsonValue.Create(false));
                return Sequence.Undefined;
            }

            object? lhsValue = lhsResultSeq.FirstOrDefault(); // object?
            object? rhsValue = rhsResultSeq.FirstOrDefault(); // object?

            if (lhsResultSeq.IsUndefined())
            {
                if (OperatorSymbol == "=" || OperatorSymbol == "!=" || OperatorSymbol == "<" || OperatorSymbol == "<=" || OperatorSymbol == ">" || OperatorSymbol == ">=")
                {
                    return Sequence.Undefined;
                }
                if (OperatorSymbol == "&")
                {
                    return EvaluateConcatenation(null, rhsValue); // rhsValue is object?
                }
                return Sequence.Undefined;
            }

            if (rhsResultSeq.IsUndefined())
            {
                if (OperatorSymbol == "=") return new Sequence(JsonValue.Create(false));
                if (OperatorSymbol == "!=") return new Sequence(JsonValue.Create(true));
                if (OperatorSymbol == "<" || OperatorSymbol == "<=" || OperatorSymbol == ">" || OperatorSymbol == ">=")
                {
                    return Sequence.Undefined;
                }
                if (OperatorSymbol == "&")
                {
                    return EvaluateConcatenation(lhsValue, null); // lhsValue is object?
                }
                return Sequence.Undefined;
            }

            if ((lhsResultSeq.Count == 0 && !lhsResultSeq.IsUndefined()) || (rhsResultSeq.Count == 0 && !rhsResultSeq.IsUndefined()))
            {
                 if (OperatorSymbol == "&")
                 {
                    if (lhsResultSeq.Count == 0 && rhsResultSeq.Count == 0) return new Sequence(JsonValue.Create("")); // "" & ""
                    // If one is empty, effectively "" & value or value & ""
                    return EvaluateConcatenation(
                        lhsResultSeq.Count == 0 ? null : lhsValue,  // Pass null if empty, else the value
                        rhsResultSeq.Count == 0 ? null : rhsValue
                    );
                 }
                 if (OperatorSymbol == "=") return new Sequence(JsonValue.Create(lhsResultSeq.Count == 0 && rhsResultSeq.Count == 0)); // [] = [] is true
                 if (OperatorSymbol == "!=") return new Sequence(JsonValue.Create(!(lhsResultSeq.Count == 0 && rhsResultSeq.Count == 0))); // [] != [] is false
                 return Sequence.Undefined;
            }

            // Now operate on lhsValue and rhsValue (which are object?)
            switch (OperatorSymbol)
            {
                case "+": case "-": case "*": case "/": case "%":
                    return EvaluateArithmetic(lhsValue, rhsValue, OperatorSymbol);
                case "=": case "!=": case "<": case "<=": case ">": case ">=":
                    return EvaluateComparison(lhsValue, rhsValue, OperatorSymbol);
                case "&":
                    return EvaluateConcatenation(lhsValue, rhsValue);
                default:
                    throw new JsonataEvaluationException($"Operator '{OperatorSymbol}' evaluation not fully implemented for item types.");
            }
        }

        private Sequence EvaluateArithmetic(object? lObj, object? rObj, string op)
        {
            if (!(lObj is JsonNode lNode && rObj is JsonNode rNode))
            {
                return Sequence.Undefined; // Arithmetic undefined if operands are not JsonNodes (e.g. functions)
            }

            if (!(lNode is JsonValue lv && lv.TryGetValue<decimal>(out decimal ld)) ||
                !(rNode is JsonValue rv && rv.TryGetValue<decimal>(out decimal rd)))
            {
                return Sequence.Undefined; // Arithmetic ops on non-numbers yield undefined
            }
            switch (op)
            {
                case "+": return new Sequence(JsonValue.Create(ld + rd));
                case "-": return new Sequence(JsonValue.Create(ld - rd));
                case "*": return new Sequence(JsonValue.Create(ld * rd));
                case "/": return rd == 0 ? throw new JsonataEvaluationException("Division by zero.") : new Sequence(JsonValue.Create(ld / rd));
                case "%": return rd == 0 ? throw new JsonataEvaluationException("Modulo by zero.") : new Sequence(JsonValue.Create(ld % rd));
                default: return Sequence.Undefined;
            }
        }

        private Sequence EvaluateComparison(object? lObj, object? rObj, string op)
        {
            // Comparisons involving FunctionObjects are generally not defined in JSONata core spec,
            // except for pointer equality if they were comparable entities.
            // For now, if either is a FunctionObject, comparison is undefined unless it's '=' or '!=' for nulls.
            // JSON nulls (represented as null object? after FirstOrDefault) are handled.

            if (lObj is FunctionObject || rObj is FunctionObject)
            {
                if (op == "=") return new Sequence(JsonValue.Create(Object.ReferenceEquals(lObj, rObj)));
                if (op == "!=") return new Sequence(JsonValue.Create(!Object.ReferenceEquals(lObj, rObj)));
                return Sequence.Undefined; // Other comparisons with functions are undefined
            }

            // At this point, lObj and rObj should be JsonNode? or C# null (representing JSON null or undefined from FirstOrDefault)
            JsonNode? lNode = lObj as JsonNode; // Will be null if lObj was null or not a JsonNode
            JsonNode? rNode = rObj as JsonNode;

            if (lNode == null && rNode == null) {
                return new Sequence(JsonValue.Create(op == "=" || op == "<=" || op == ">="));
            }
            if (lNode == null || rNode == null) {
                if (op == "=") return new Sequence(JsonValue.Create(false));
                if (op == "!=") return new Sequence(JsonValue.Create(true));
                return Sequence.Undefined;
            }

            // Both are non-null JsonNodes
            bool lIsValue = lNode is JsonValue; bool rIsValue = rNode is JsonValue;
            bool lIsArray = lNode is JsonArray; bool rIsArray = rNode is JsonArray;
            bool lIsObject = lNode is JsonObject; bool rIsObject = rNode is JsonObject;

            if ((lIsArray || lIsObject) && rIsValue || (rIsArray || rIsObject) && lIsValue) {
                 return Sequence.Undefined;
            }

            if (lIsValue && rIsValue) {
                JsonValue lv = (JsonValue)lNode; JsonValue rv = (JsonValue)rNode;
                // Numerics
                if (lv.TryGetValue<decimal>(out decimal ld) && rv.TryGetValue<decimal>(out decimal rd)) {
                    switch (op) {
                        case "=": return new Sequence(JsonValue.Create(ld == rd)); case "!=": return new Sequence(JsonValue.Create(ld != rd));
                        case "<": return new Sequence(JsonValue.Create(ld < rd));  case "<=": return new Sequence(JsonValue.Create(ld <= rd));
                        case ">": return new Sequence(JsonValue.Create(ld > rd));  case ">=": return new Sequence(JsonValue.Create(ld >= rd));
                    }
                }
                // Strings
                if (lv.TryGetValue<string>(out string? ls) && rv.TryGetValue<string>(out string? rs)) {
                    int comp = string.CompareOrdinal(ls, rs);
                    switch (op) {
                        case "=": return new Sequence(JsonValue.Create(comp == 0)); case "!=": return new Sequence(JsonValue.Create(comp != 0));
                        case "<": return new Sequence(JsonValue.Create(comp < 0));  case "<=": return new Sequence(JsonValue.Create(comp <= 0));
                        case ">": return new Sequence(JsonValue.Create(comp > 0));  case ">=": return new Sequence(JsonValue.Create(comp >= 0));
                    }
                }
                // Booleans
                if (lv.TryGetValue<bool>(out bool lb) && rv.TryGetValue<bool>(out bool rb)) {
                    switch (op) {
                        case "=": return new Sequence(JsonValue.Create(lb == rb)); case "!=": return new Sequence(JsonValue.Create(lb != rb));
                        default: return Sequence.Undefined;
                    }
                }
                // Different JsonValue types
                if (op == "=") return new Sequence(JsonValue.Create(false));
                if (op == "!=") return new Sequence(JsonValue.Create(true));
                return Sequence.Undefined;
            }

            if ((lIsArray && rIsArray) || (lIsObject && rIsObject)) {
                bool sameInstance = object.ReferenceEquals(lNode, rNode); // JSONata compares arrays/objects by reference for =
                if (op == "=") return new Sequence(JsonValue.Create(sameInstance));
                if (op == "!=") return new Sequence(JsonValue.Create(!sameInstance));
                return Sequence.Undefined;
            }

            if (op == "=") return new Sequence(JsonValue.Create(false));
            if (op == "!=") return new Sequence(JsonValue.Create(true));
            return Sequence.Undefined;
        }

        private Sequence EvaluateConcatenation(object? lObj, object? rObj)
        {
            string StringifyObjectForConcat(object? obj) {
                if (obj == null) return "";

                if (obj is FunctionObject)
                {
                    throw new JsonataEvaluationException("String concatenation operator '&' cannot be applied to function objects.");
                }

                if (obj is JsonNode node)
                {
                    if (node is JsonArray || node is JsonObject)
                    {
                        throw new JsonataEvaluationException($"String concatenation operator '&' cannot be applied to {node.GetType().Name} types directly.");
                    }

                    if (node is JsonValue jv)
                    {
                        // Priority 1: Direct String
                        if (jv.TryGetValue<string>(out string? s))
                        {
                            return s ?? "";
                        }
                        // Priority 2: JsonElement
                        else if (jv.TryGetValue(out JsonElement element))
                        {
                            switch (element.ValueKind)
                            {
                                case JsonValueKind.String:
                                    return element.GetString() ?? "";
                                case JsonValueKind.Number:
                                    return element.GetRawText(); // Use GetRawText for accurate number representation
                                case JsonValueKind.True:
                                    return "true";
                                case JsonValueKind.False:
                                    return "false";
                                case JsonValueKind.Null:
                                    return "";
                                default:
                                    return ""; // Other ValueKind (Undefined, etc.)
                            }
                        }
                        // Priority 3: Other C# Primitives in JsonValue
                        else if (jv.TryGetValue<decimal>(out decimal dec))
                        {
                            return dec.ToString(CultureInfo.InvariantCulture);
                        }
                        else if (jv.TryGetValue<double>(out double dbl)) // JSONata spec often implies all numbers are "decimal" but good to be robust
                        {
                            return dbl.ToString(CultureInfo.InvariantCulture);
                        }
                        else if (jv.TryGetValue<long>(out long lng))
                        {
                            return lng.ToString(CultureInfo.InvariantCulture);
                        }
                        else if (jv.TryGetValue<int>(out int i))
                        {
                            return i.ToString(CultureInfo.InvariantCulture);
                        }
                        else if (jv.TryGetValue<bool>(out bool b)) // This case might be covered by JsonElement True/False if input is from parsed JSON
                        {
                            return b.ToString().ToLowerInvariant();
                        }
                        // Priority 4: Check for explicit JSON null if other types fail
                        else
                        {
                            // This check is a bit tricky. JsonValue.Create(null) results in a JsonValue whose GetValue<object>() is null.
                            // However, TryGetValue<T> for primitive types would have already returned false.
                            // If jv.ToString() is "null" for a JsonValue representing JSON null.
                            // A more direct check might be needed if JsonValue has an explicit IsNull property or similar.
                            // For now, if all specific TryGetValue fail, assume it's not a recognized primitive for stringification.
                            object? internalValue = null;
                            try { internalValue = jv.GetValue<object?>(); } catch (InvalidOperationException) { /* ignore */ }
                            if (internalValue == null) return ""; // Handles JsonValue created with explicit null

                            return ""; // Fallback for JsonValue if not a recognized primitive
                        }
                    }
                    // This should not be reached if 'node' is always a JsonValue, JsonArray, or JsonObject
                    throw new JsonataEvaluationException($"Unexpected JsonNode subtype '{node.GetType().Name}' in StringifyObjectForConcat that is not JsonValue.");
                }
                // If obj is not JsonNode and not FunctionObject (e.g. a raw C# type accidentally in a sequence)
                throw new JsonataEvaluationException($"String concatenation operator '&' cannot be applied to type {obj.GetType().Name}.");
            }

            try {
                string ls_val = StringifyObjectForConcat(lObj);
                string rs_val = StringifyObjectForConcat(rObj);
                return new Sequence(JsonValue.Create(ls_val + rs_val));
            } catch (JsonataEvaluationException e) { // Catch specific exception to allow others to pass for debugging
                // If StringifyObjectForConcat throws (e.g. on array/object/function), result is undefined.
                // Optionally log e.Message here if needed for debugging.
                return Sequence.Undefined;
            }
        }
    }
}
