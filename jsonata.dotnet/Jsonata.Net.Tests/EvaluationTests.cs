using Xunit;
using Jsonata.Net.Core;
using Jsonata.Net.Core.Parsing;
using Jsonata.Net.Core.AstNodes;
using Jsonata.Net.Core.Evaluation;
using Jsonata.Net.Core.Exceptions;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Collections.Generic;
using System.Linq;

public class EvaluationTests
{
    // Helper to convert sequence items to string for assertions
    private string ConvertItemToString(object? item)
    {
        if (item == null) return "null";
        if (item is JsonNode jsonNode) return jsonNode.ToJsonString(new JsonSerializerOptions { WriteIndented = false }) ?? "null";
        if (item is FunctionObject) return "\"[JSONata Function]\""; // Consistent placeholder
        return item.ToString() ?? "null"; // Fallback
    }

    private Sequence EvaluateExpressionRaw(string expression, string? jsonInput, Dictionary<string, object?>? bindings = null) // bindings type changed
    {
        JsonNode? inputNode = string.IsNullOrEmpty(jsonInput) ? null : JsonNode.Parse(jsonInput);
        JsonNode contextInitialInput = inputNode ?? new JsonObject();
        BuiltinFunctionsRegistry functions = new BuiltinFunctionsRegistry(); // Create a new registry

        // TODO: Register default functions if EvaluateExpressionRaw is meant to test them.
        // For now, it will use an empty registry, meaning only user-defined functions via bindings work.
        // Or, this helper is for pure AST evaluation without $functions.

        // Create EvaluationContext with initial local bindings if provided
        EvaluationContext evalContext = new EvaluationContext(contextInitialInput, functions, inputNode);
        if (bindings != null)
        {
            foreach(var b in bindings)
            {
                // Clone JsonNode values, pass others (like FunctionObject) as is.
                evalContext.SetBinding(b.Key, b.Value is JsonNode jn ? jn.DeepClone() : b.Value);
            }
        }

        var parser = new JsonataParser(expression);
        AstNode ast = parser.Parse();
        return ast.Evaluate(inputNode, evalContext);
    }

    private void AssertSequenceReturnsSingleJson(string? expectedJson, string expression, string? jsonInput, Dictionary<string, object?>? bindings = null) // bindings type changed
    {
        Sequence resultSeq = EvaluateExpressionRaw(expression, jsonInput, bindings);

        if (expectedJson == "undefined")
        {
            Assert.True(resultSeq.IsUndefined(), $"Expected Undefined sequence for '{expression}', but got count {resultSeq.Count} with value '{ConvertItemToString(resultSeq.FirstOrDefault())}'.");
            return;
        }

        JsonNode? expectedNode = expectedJson == null ? null : JsonNode.Parse(expectedJson);
        string expectedStr = expectedNode?.ToJsonString(new JsonSerializerOptions{WriteIndented=false}) ?? "null";

        Assert.False(resultSeq.IsUndefined(), $"Expected a value '{expectedStr}' for '{expression}', but got Undefined sequence.");

        if (expectedNode == null)
        {
            Assert.Equal(1, resultSeq.Count);
            Assert.Null(resultSeq.FirstOrDefault()); // Check if the item itself is null
        }
        else
        {
            Assert.True(resultSeq.IsSingleValue(), $"Expected single value '{expectedStr}' for '{expression}', but sequence has count {resultSeq.Count} (first item: '{ConvertItemToString(resultSeq.FirstOrDefault())}').");
            string actualStr = ConvertItemToString(resultSeq.FirstOrDefault());
            Assert.Equal(expectedStr, actualStr);
        }
    }

    private void AssertSequenceIsEmptyOrUndefined(string expression, string? jsonInput, Dictionary<string, object?>? bindings = null) // bindings type changed
    {
        Sequence resultSeq = EvaluateExpressionRaw(expression, jsonInput, bindings);
        Assert.True(resultSeq.IsUndefined() || resultSeq.Count == 0, $"Expected empty or undefined sequence for '{expression}', but got count {resultSeq.Count} with value '{ConvertItemToString(resultSeq.FirstOrDefault())}'.");
    }

    [Fact] public void TestArithmetic_Addition() {
        AssertSequenceReturnsSingleJson("5", "2 + 3", null);
    }

    [Fact] public void TestComparison_Numbers_Equals() {
        AssertSequenceReturnsSingleJson("true", "5 = 5", null);
    }

    [Fact] public void TestComparison_UndefinedWithUndefined_Equals_True() {
        // For $x = $y where both are undefined, TryGetBinding will return false for both,
        // leading to Sequence.Undefined for their evaluation as VariableNode.
        // BinaryOperatorNode handles (Undefined = Undefined) -> true.
        AssertSequenceReturnsSingleJson("true", "$x = $y", null);
    }

    [Fact] public void TestComparison_UndefinedWithNumber_IsUndefined() {
        AssertSequenceIsEmptyOrUndefined("$x = 5", null);
    }

    [Fact] public void TestComparison_NumberWithUndefined_Equals_False() {
        AssertSequenceReturnsSingleJson("false", "5 = $x", null);
    }

    [Fact] public void TestComparison_StringAndNumber_NotEquals_True() {
        AssertSequenceReturnsSingleJson("true", "\"5\" != 5", null);
    }

    [Fact] public void TestComparison_StringAndNumber_Equals_False() {
        AssertSequenceReturnsSingleJson("false", "\"5\" = 5", null);
    }

    [Fact] public void TestComparison_StringAndNumber_LessThan_IsUndefined() {
        AssertSequenceIsEmptyOrUndefined("\"5\" < 5", null);
    }

    [Fact] public void TestConcatenation_Strings() {
        AssertSequenceReturnsSingleJson("\"abcdef\"", "\"abc\" & \"def\"", null);
    }

    [Fact] public void TestConcatenation_StringAndNull_BecomesString() {
        AssertSequenceReturnsSingleJson("\"abc\"", "\"abc\" & null", null);
        AssertSequenceReturnsSingleJson("\"def\"", "null & \"def\"", null);
    }

    [Fact] public void TestConcatenation_NumberAndString() {
        AssertSequenceReturnsSingleJson("\"123test\"", "123 & \"test\"", null);
    }

    [Fact] public void TestConcatenation_BooleanAndString() {
        AssertSequenceReturnsSingleJson("\"trueworld\"", "true & \"world\"", null);
    }

    [Fact] public void TestConcatenation_ArrayWithString_IsUndefined() {
        AssertSequenceIsEmptyOrUndefined("[1,2] & \"test\"", null);
    }

    [Fact] public void TestBoolean_And_ShortCircuit_False() {
        Sequence res = EvaluateExpressionRaw("$foo and $bar.baz", null); // $foo is undefined
        Assert.True(res.IsUndefined());
    }

    [Fact] public void TestBoolean_And_TrueAndValue() {
        // JsonNode is compatible with object? for bindings
        var bindings = new Dictionary<string, object?> { { "$bar", JsonValue.Create("BarVal") } };
        AssertSequenceReturnsSingleJson("\"BarVal\"", "true and $bar", null, bindings);
    }

    [Fact] public void TestBoolean_Or_ShortCircuit_True() {
        AssertSequenceReturnsSingleJson("\"hello\"", "\"hello\" or $undefinedVar", null);
    }

    [Fact] public void TestBoolean_Or_FalseAndValue() {
        var bindings = new Dictionary<string, object?> { { "$bar", JsonValue.Create(false) } };
        AssertSequenceReturnsSingleJson("false", "false or $bar", null, bindings);
    }

    [Fact] public void TestArithmetic_OneOperandUndefined_IsUndefined() {
        AssertSequenceIsEmptyOrUndefined("$x + 5", null);
        AssertSequenceIsEmptyOrUndefined("5 + $y", null);
    }

    [Fact] public void TestArithmetic_OperandsEmptySequence_IsUndefinedOrEmpty() {
        AssertSequenceIsEmptyOrUndefined("a.b + 5", "{}", null);
    }
}
