using Xunit;
using Jsonata.Net.Core;
using Jsonata.Net.Core.Exceptions; // For JsonataEvaluationException
using System.Text.Json.Nodes; // Required for JsonNode
using System.Text.Json; // Required for JsonElement

namespace Jsonata.Net.Tests
{
    public sealed class BuiltinStringFunctionsTests
    {
        private static JsonNode? TryParseJsonNode(string? jsonString)
        {
            if (jsonString == null) return null;
            if (jsonString == "null") return JsonValue.Create((JsonElement?)null);
            try { return JsonNode.Parse(jsonString); }
            catch (JsonException) {
                // If it's not valid JSON, but was intended as a simple string context, wrap it.
                // This case might need care depending on test intent.
                // For many built-in functions, the context or arg is expected to be a JSON string if not other JSON type.
                // e.g. $length() with context "TestContext" (not "\"TestContext\"")
                // However, the original tests often passed "\"TestContext\"" for such cases.
                // Let's assume if it's not parseable as JSON, it was not meant to be a JSON structure.
                // The JsonataEvaluator.Evaluate(string?) used to handle this.
                // For tests, if inputJson is not a valid JSON doc string, it's often an error or specific value.
                // The most robust way is that `inputJson` in test methods should always be a *valid* JSON string,
                // or null. If it represents a raw string to be passed to JSONata, it should be quoted,
                // e.g. inputJson = "\"raw string\"".
                // For now, if unparseable, return null, and tests expecting direct string context might fail
                // or need adjustment in their inputJson values.
                // A better approach for tests: ensure inputJson is always a valid JSON representation.
                // For example, a raw string "TestContext" should be passed as "\"TestContext\"" in the inputJson parameter.
                return null; // Or throw, or handle as specific type if known (e.g. JsonValue.Create(jsonString) if it was meant as a literal string)
            }
        }

        // $length tests
        [Theory]
        // Cases with explicit argument
        [InlineData("$length(\"Hello\")", "5")]
        [InlineData("$length(\"\")", "0")]
        // Cases with context item
        [InlineData("$length()", "11")]  // Context item "TestContext"
        [InlineData("$length()", "0")]  // Context item ""
        public void LengthFunction_ValidInput_ReturnsCorrectLength(string expression, string expected)
        {
            var evaluator = new JsonataEvaluator(expression);
            string? inputJsonString = null;
            if (expression == "$length()")
            {
                // Determine context based on expected output for no-arg version
                if (expected == "11") inputJsonString = "\"TestContext\"";
                else if (expected == "0") inputJsonString = "\"\"";
                // Add more specific context cases if needed
            }
            JsonNode? resultNode = evaluator.Evaluate(TryParseJsonNode(inputJsonString));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), resultNode), $"Expr: {expression}, Expected: {expected}, Actual: {resultNode?.ToJsonString()}");
        }

        [Fact]
        public void LengthFunction_ContextEmptyString_ReturnsZero()
        {
            var evaluator = new JsonataEvaluator("$length()");
            JsonNode? resultNode = evaluator.Evaluate(JsonNode.Parse("\"\"")); // Context item is empty string
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse("0"), resultNode), $"Expected: 0, Actual: {resultNode?.ToJsonString()}");
        }

        [Theory]
        [InlineData("$length(123)")]
        [InlineData("$length(null)")]
        [InlineData("$length(true)")]
        [InlineData("$length({\"a\":1})")]
        [InlineData("$length([1,2])")]
        [InlineData("$length(foo, bar)")] // Too many args
        [InlineData("$length(undefinedvar)")] // Undefined variable
        public void LengthFunction_InvalidInput_ThrowsException(string expression)
        {
            var evaluator = new JsonataEvaluator(expression);
            Assert.Throws<JsonataEvaluationException>(() => evaluator.Evaluate((JsonNode?)null));
        }

        [Fact]
        public void LengthFunction_NoArgUndefinedContext_ThrowsException()
        {
            var evaluator = new JsonataEvaluator("$length()");
            Assert.Throws<JsonataEvaluationException>(() => evaluator.Evaluate((JsonNode?)null));
        }

        // $uppercase tests
        [Theory]
        [InlineData("$uppercase(\"Hello\")", "\"HELLO\"")] // Expect JSON string literal
        [InlineData("$uppercase(\"hELLo wOrLd\")", "\"HELLO WORLD\"")]
        [InlineData("$uppercase(\"\")", "\"\"")]
        [InlineData("$uppercase()", "\"TESTCONTEXT\"")] // Context item "TestContext"
        public void UppercaseFunction_ValidInput_ReturnsCorrectString(string expression, string expected)
        {
            var evaluator = new JsonataEvaluator(expression);
            string? inputJsonString = null;
            if (expression == "$uppercase()")
            {
                inputJsonString = "\"TestContext\"";
            }
            JsonNode? resultNode = evaluator.Evaluate(TryParseJsonNode(inputJsonString));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), resultNode), $"Expr: {expression}, Expected: {expected}, Actual: {resultNode?.ToJsonString()}");
        }

        [Theory]
        [InlineData("$uppercase(123)")]
        [InlineData("$uppercase(null)")]
        [InlineData("$uppercase(foo, bar)")]
        public void UppercaseFunction_InvalidInput_ThrowsException(string expression)
        {
            var evaluator = new JsonataEvaluator(expression);
            Assert.Throws<JsonataEvaluationException>(() => evaluator.Evaluate((JsonNode?)null));
        }

        // $lowercase tests
        [Theory]
        [InlineData("$lowercase(\"Hello\")", "\"hello\"")] // Expect JSON string literal
        [InlineData("$lowercase(\"hELLo wOrLd\")", "\"hello world\"")]
        [InlineData("$lowercase(\"\")", "\"\"")]
        [InlineData("$lowercase()", "\"testcontext\"")] // Context item "TestContext"
        public void LowercaseFunction_ValidInput_ReturnsCorrectString(string expression, string expected)
        {
            var evaluator = new JsonataEvaluator(expression);
            string? inputJsonString = null;
            if (expression == "$lowercase()")
            {
                inputJsonString = "\"TestContext\"";
            }
            JsonNode? resultNode = evaluator.Evaluate(TryParseJsonNode(inputJsonString));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), resultNode), $"Expr: {expression}, Expected: {expected}, Actual: {resultNode?.ToJsonString()}");
        }

        [Theory]
        [InlineData("$lowercase(123)")]
        [InlineData("$lowercase(null)")]
        [InlineData("$lowercase(foo, bar)")]
        public void LowercaseFunction_InvalidInput_ThrowsException(string expression)
        {
            var evaluator = new JsonataEvaluator(expression);
            Assert.Throws<JsonataEvaluationException>(() => evaluator.Evaluate((JsonNode?)null));
        }

        // $trim tests
        [Theory]
        [InlineData("$trim(\"  Hello  World  \")", "\"Hello World\"")]      // Expect JSON string literal
        [InlineData("$trim(\"\\tHello\\r\\nWorld\\t\")", "\"Hello World\"")]
        [InlineData("$trim(\" Hello\")", "\"Hello\"")]
        [InlineData("$trim(\"Hello \")", "\"Hello\"")]
        [InlineData("$trim(\"   \")", "\"\"")]
        [InlineData("$trim(\"\")", "\"\"")]
        [InlineData("$trim(\"\\t\\r\\n\")", "\"\"")]
        [InlineData("$trim(\"  H W  \")", "\"H W\"")]
        [InlineData("$trim()", "\"Test Context\"")] // Context item "  Test Context  "
        public void TrimFunction_ValidInput_ReturnsCorrectString(string expression, string expected)
        {
            var evaluator = new JsonataEvaluator(expression);
            string? inputJsonString = null;
            if (expression == "$trim()")
            {
                inputJsonString = "\"  Test Context  \"";
            }
            JsonNode? resultNode = evaluator.Evaluate(TryParseJsonNode(inputJsonString));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), resultNode), $"Expr: {expression}, Expected: {expected}, Actual: {resultNode?.ToJsonString()}");
        }

        [Theory]
        [InlineData("$trim(123)")]
        [InlineData("$trim(null)")]
        [InlineData("$trim(foo, bar)")]
        public void TrimFunction_InvalidInput_ThrowsException(string expression)
        {
            var evaluator = new JsonataEvaluator(expression);
            Assert.Throws<JsonataEvaluationException>(() => evaluator.Evaluate((JsonNode?)null));
        }

        // $substring tests
        [Theory]
        [InlineData("$substring(\"Hello World\", 0)", "\"Hello World\"")]    // Basic, full string
        [InlineData("$substring(\"Hello World\", 6)", "\"World\"")]      // Start index
        [InlineData("$substring(\"Hello World\", 0, 5)", "\"Hello\"")]   // Start and length
        [InlineData("$substring(\"Hello World\", 6, 5)", "\"World\"")]   // Start and length
        [InlineData("$substring(\"Hello World\", -5)", "\"World\"")]     // Negative start
        [InlineData("$substring(\"Hello World\", -5, 2)", "\"Wo\"")]    // Negative start, length
        [InlineData("$substring(\"Hello World\", 0, 0)", "\"\"")]       // Zero length
        [InlineData("$substring(\"Hello World\", 0, -2)", "\"\"")]      // Negative length
        [InlineData("$substring(\"Hello World\", 20)", "\"\"")]         // Start beyond length
        [InlineData("$substring(\"Hello World\", 0, 20)", "\"Hello World\"")] // Length beyond actual
        [InlineData("$substring(\"abc\", 0, 1)", "\"a\"")]
        [InlineData("$substring(\"abc\", 1, 1)", "\"b\"")]
        [InlineData("$substring(\"abc\", 2, 1)", "\"c\"")]
        [InlineData("$substring(\"abc\", 3, 1)", "\"\"")]
        [InlineData("$substring(\"abc\", -1, 1)", "\"c\"")]
        [InlineData("$substring(\"abc\", -2, 1)", "\"b\"")]
        [InlineData("$substring(\"abc\", -3, 1)", "\"a\"")]
        [InlineData("$substring(\"abc\", -4, 1)", "\"a\"")] // start becomes 0, length 1. Expect "a".
        // Contextual tests
        [InlineData("$substring(6)", "\"World\"")]                      // Context "Hello World", start=6
        [InlineData("$substring(0, 5)", "\"Hello\"")]                   // Context "Hello World", start=0, length=5
        [InlineData("$substring(-5, 2)", "\"Wo\"")]                    // Context "Hello World", start=-5, length=2
        public void SubstringFunction_ValidInput_ReturnsCorrectSubstring(string expression, string expected)
        {
            var evaluator = new JsonataEvaluator(expression);
            string? inputJsonString = null;
            if (expression == "$substring(6)"
                || expression == "$substring(0, 5)"
                || expression == "$substring(-5, 2)")
            {
                inputJsonString = "\"Hello World\"";
            }
            JsonNode? resultNode = evaluator.Evaluate(TryParseJsonNode(inputJsonString));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), resultNode), $"Expr: {expression}, Expected: {expected}, Actual: {resultNode?.ToJsonString()}");
        }

        [Theory]
        [InlineData("$substring(123, 0)")]                        // Non-string input
        [InlineData("$substring(\"Hello\", \"a\")")]                 // Non-numeric start
        [InlineData("$substring(\"Hello\", 0, \"a\")")]            // Non-numeric length
        [InlineData("$substring()")]                               // Not enough args (needs context + start)
        [InlineData("$substring(\"a\", \"b\", \"c\", \"d\")")]       // Too many args
        [InlineData("$substring(0, \"a\")")]                       // Contextual, non-numeric length
        public void SubstringFunction_InvalidInput_ThrowsException(string expression)
        {
            var evaluator = new JsonataEvaluator(expression);
            string? inputJsonString = expression.StartsWith("$substring(0") ? "\"Hello\"" : null; // Provide context for some
            Assert.Throws<JsonataEvaluationException>(() => evaluator.Evaluate(TryParseJsonNode(inputJsonString)));
        }

        // $substringBefore tests
        [Theory]
        [InlineData("$substringBefore(\"Hello World\", \"World\")", "\"Hello \"")]
        [InlineData("$substringBefore(\"Hello World\", \" \")", "\"Hello\"")]
        [InlineData("$substringBefore(\"Hello World\", \"X\")", "\"\"")] // Not found - spec says empty string
        [InlineData("$substringBefore(\"Hello World\", \"\")", "\"\"")]   // Empty chars - spec says empty string
        [InlineData("$substringBefore(\"\", \"World\")", "\"\"")]         // Empty source
        [InlineData("$substringBefore()", "\"Hello \"")]                // Context "Hello World", chars "World"
        public void SubstringBeforeFunction_ValidInput_ReturnsCorrectSubstring(string expression, string expected)
        {
            var evaluator = new JsonataEvaluator(expression);
            string? inputJsonString = null;
            if (expression == "$substringBefore()")
            {
                inputJsonString = "\"Hello World\""; // Context for the test
            }
            else if (expression == "$substringBefore()" && expected == "\"Hello \"")
            {
                 // This case logic was already complex, needs re-eval for direct JsonNode
            }

            if (expression == "$substringBefore()")
            {
                evaluator = new JsonataEvaluator("$substringBefore(\"World\")");
                inputJsonString = "\"Hello World\"";
            }

            JsonNode? resultNode = evaluator.Evaluate(TryParseJsonNode(inputJsonString));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), resultNode), $"Expr: {expression}, Expected: {expected}, Actual: {resultNode?.ToJsonString()}");
        }

        // Simplified SubstringBefore tests
        [Theory]
        [InlineData("$substringBefore(\"Hello World\", \"World\")", "\"Hello \"")]
        [InlineData("$substringBefore(\"abracadabra\", \"a\")", "\"\"")] // First char
        [InlineData("$substringBefore(\"abracadabra\", \"bra\")", "\"a\"")]
        public void SubstringBeforeFunction_SimpleCases(string expression, string expected)
        {
            var evaluator = new JsonataEvaluator(expression);
            JsonNode? resultNode = evaluator.Evaluate((JsonNode?)null);
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), resultNode), $"Expr: {expression}, Expected: {expected}, Actual: {resultNode?.ToJsonString()}");
        }


        [Theory]
        [InlineData("$substringBefore(123, \"a\")")]
        [InlineData("$substringBefore(\"Hello\", 123)")]
        [InlineData("$substringBefore(\"Hello\")")] // Not enough args
        public void SubstringBeforeFunction_InvalidInput_ThrowsException(string expression)
        {
            var evaluator = new JsonataEvaluator(expression);
            Assert.Throws<JsonataEvaluationException>(() => evaluator.Evaluate((JsonNode?)null));
        }

        // $substringAfter tests
        [Theory]
        [InlineData("$substringAfter(\"Hello World\", \"Hello\")", "\" World\"")]
        [InlineData("$substringAfter(\"Hello World\", \" \")", "\"World\"")]
        [InlineData("$substringAfter(\"Hello World\", \"X\")", "\"\"")]    // Not found - spec says empty
        [InlineData("$substringAfter(\"Hello World\", \"\")", "\"Hello World\"")] // Empty chars - spec says original string
        [InlineData("$substringAfter(\"\", \"World\")", "\"\"")]          // Empty source
        [InlineData("$substringAfter()", "\"World\"")]                 // Context "Hello World", chars "Hello "
        public void SubstringAfterFunction_ValidInput_ReturnsCorrectSubstring(string expression, string expected)
        {
            var evaluator = new JsonataEvaluator(expression);
            string? inputJsonString = null;
            if (expression == "$substringAfter()")
            {
                evaluator = new JsonataEvaluator("$substringAfter(\"Hello \")");
                inputJsonString = "\"Hello World\"";
            }
            JsonNode? resultNode = evaluator.Evaluate(TryParseJsonNode(inputJsonString));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), resultNode), $"Expr: {expression}, Expected: {expected}, Actual: {resultNode?.ToJsonString()}");
        }

        // Simplified SubstringAfter tests
        [Theory]
        [InlineData("$substringAfter(\"Hello World\", \"Hello\")", "\" World\"")]
        [InlineData("$substringAfter(\"abracadabra\", \"a\")", "\"bracadabra\"")]
        [InlineData("$substringAfter(\"abracadabra\", \"bra\")", "\"cadabra\"")]
        public void SubstringAfterFunction_SimpleCases(string expression, string expected)
        {
            var evaluator = new JsonataEvaluator(expression);
            JsonNode? resultNode = evaluator.Evaluate((JsonNode?)null);
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), resultNode), $"Expr: {expression}, Expected: {expected}, Actual: {resultNode?.ToJsonString()}");
        }

        [Theory]
        [InlineData("$substringAfter(123, \"a\")")]
        [InlineData("$substringAfter(\"Hello\", 123)")]
        [InlineData("$substringAfter(\"Hello\")")] // Not enough args
        public void SubstringAfterFunction_InvalidInput_ThrowsException(string expression)
        {
            var evaluator = new JsonataEvaluator(expression);
            Assert.Throws<JsonataEvaluationException>(() => evaluator.Evaluate((JsonNode?)null));
        }

        // $pad tests
        [Theory]
        [InlineData("$pad(\"Hello\", 10)", "\"Hello     \"")]    // Pad right with spaces
        [InlineData("$pad(\"Hello\", -10)", "\"     Hello\"")]   // Pad left with spaces
        [InlineData("$pad(\"Hello\", 10, \"*\")", "\"Hello*****\"")] // Pad right with char
        [InlineData("$pad(\"Hello\", -10, \"*\")", "\"*****Hello\"")]// Pad left with char, corrected expectation
        [InlineData("$pad(\"Hello\", 3)", "\"Hello\"")]          // Width less than str length
        [InlineData("$pad(\"Hi\", 5, \"123\")", "\"Hi111\"")]      // Char longer than 1, use first
        [InlineData("$pad(10)", "\"PadMe     \"")]      // Context "PadMe", width 10.
        [InlineData("$pad(10, \"#\")", "\"PadMe#####\"")]   // Context "PadMe", width 10, char #.
        public void PadFunction_ValidInput_ReturnsCorrectString(string expression, string expected)
        {
            var evaluator = new JsonataEvaluator(expression);
            string? inputJsonString = null;
            if (expression == "$pad(10)" || expression == "$pad(10, \"#\")")
            {
                inputJsonString = "\"PadMe\"";
            }
            JsonNode? resultNode = evaluator.Evaluate(TryParseJsonNode(inputJsonString));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), resultNode), $"Expr: {expression}, Expected: {expected}, Actual: {resultNode?.ToJsonString()}");
        }

        [Theory]
        [InlineData("$pad(\"Hello\", \"abc\")")]   // Invalid width type
        [InlineData("$pad(123, 10)")]          // Invalid str type (from context)
        [InlineData("$pad(\"Hello\", 10, 123)")] // Invalid char type
        [InlineData("$pad(\"Hello\")")]         // Not enough args
        [InlineData("$pad()")]                 // Context undefined, not enough args
        public void PadFunction_InvalidInput_ThrowsException(string expression)
        {
            var evaluator = new JsonataEvaluator(expression);
            string? inputJsonString = null;
            if (expression == "$pad()") inputJsonString = null; // Undefined context
            else if (expression == "$pad(123, 10)") inputJsonString = "123"; // Context is number
            Assert.Throws<JsonataEvaluationException>(() => evaluator.Evaluate(TryParseJsonNode(inputJsonString)));
        }

        // $contains tests
        [Theory]
        [InlineData("$contains(\"Hello World\", \"World\")", "true")]
        [InlineData("$contains(\"Hello World\", \"world\")", "false")] // Case sensitive
        [InlineData("$contains(\"Hello World\", \"X\")", "false")]
        [InlineData("$contains(\"Hello World\", \"\")", "true")]    // Empty pattern is always true
        [InlineData("$contains(\"\", \"World\")", "false")]
        [InlineData("$contains(\"abc\", \"/b/\")", "true")]         // Regex
        [InlineData("$contains(\"abc\", \"/B/\")", "false")]        // Regex case sensitive
        [InlineData("$contains(\"abc\", \"/B/i\")", "true")]        // Regex case insensitive
        [InlineData("$contains()", "true")] // Context "abc", pattern "b"
        public void ContainsFunction_ValidInput_ReturnsCorrectBoolean(string expression, string expected)
        {
            var evaluator = new JsonataEvaluator(expression);
            string? inputJsonString = null;
            if (expression == "$contains()")
            {
                inputJsonString = "\"abc\""; // Context for the test
                evaluator = new JsonataEvaluator("$contains(\"b\")");
            }
            JsonNode? resultNode = evaluator.Evaluate(TryParseJsonNode(inputJsonString));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), resultNode), $"Expr: {expression}, Expected: {expected}, Actual: {resultNode?.ToJsonString()}");
        }

        [Theory]
        [InlineData("$contains(123, \"a\")")]
        [InlineData("$contains(\"Hello\", 123)")]
        [InlineData("$contains(\"Hello\")")] // Not enough args
        [InlineData("$contains(\"Hello\", \"/invalid-regex[/\")")]
        public void ContainsFunction_InvalidInput_ThrowsOrSpecificBehavior(string expression)
        {
            var evaluator = new JsonataEvaluator(expression);
            if (expression.Contains("invalid-regex"))
            {
                JsonNode? resultNode = evaluator.Evaluate((JsonNode?)null);
                Assert.True(JsonNode.DeepEquals(JsonNode.Parse("false"), resultNode));
            }
            else
            {
                Assert.Throws<JsonataEvaluationException>(() => evaluator.Evaluate((JsonNode?)null));
            }
        }

        // $split tests
        [Theory]
        [InlineData("$split(\"Hello World\", \" \")", "[\"Hello\",\"World\"]")]
        [InlineData("$split(\"a-b-c-d\", \"-\", 2)", "[\"a\",\"b-c-d\"]")]
        [InlineData("$split(\"abc\", \"/\")", "[\"abc\"]")]
        [InlineData("$split(\"a,b,c\", \"/,/\")", "[\"a\",\"b\",\"c\"]")]
        [InlineData("$split(\"abracadabra\", \"/a/\")", "[\"\",\"br\",\"c\",\"d\",\"br\",\"\"]")]
        [InlineData("$split()", "[\"one\",\"two\"]")]
        public void SplitFunction_ValidInput_ReturnsCorrectArray(string expression, string expected)
        {
            var evaluator = new JsonataEvaluator(expression);
            string? inputJsonString = null;
            if (expression == "$split()")
            {
                inputJsonString = "\"one two\"";
                evaluator = new JsonataEvaluator("$split(\" \")");
            }
            JsonNode? resultNode = evaluator.Evaluate(TryParseJsonNode(inputJsonString));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), resultNode), $"Expr: {expression}, Expected: {expected}, Actual: {resultNode?.ToJsonString()}");
        }

        [Theory]
        [InlineData("$split(123, \"a\")")]
        [InlineData("$split(\"Hello\", 123)")]
        [InlineData("$split(\"Hello\")")]
        [InlineData("$split(\"Hello\", \" \", \"abc\")")]
        public void SplitFunction_InvalidInput_ThrowsException(string expression)
        {
            var evaluator = new JsonataEvaluator(expression);
            Assert.Throws<JsonataEvaluationException>(() => evaluator.Evaluate((JsonNode?)null));
        }

        // $join tests
        [Theory]
        [InlineData("$join([\"Hello\", \"World\"])", "\"HelloWorld\"")]
        [InlineData("$join([\"Hello\", \"World\"], \" \")", "\"Hello World\"")]
        [InlineData("$join([])", "\"\"")]
        [InlineData("$join([\"Hello\"])", "\"Hello\"")]
        [InlineData("$join([\"Hello\", null, \"World\"], \"-\")", "\"Hello-null-World\"")]
        [InlineData("$join([\"Hello\", 123, true], \":\")", "\"Hello:123:true\"")]
        [InlineData("$join()", "\"onetwo\"")] // Context ["one", "two"]
        public void JoinFunction_ValidInput_ReturnsCorrectString(string expression, string expected)
        {
            var evaluator = new JsonataEvaluator(expression);
            string? inputJsonString = null;
            if (expression == "$join()")
            {
                inputJsonString = "[\"one\", \"two\"]";
            }
            JsonNode? resultNode = evaluator.Evaluate(TryParseJsonNode(inputJsonString));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), resultNode), $"Expr: {expression}, Expected: {expected}, Actual: {resultNode?.ToJsonString()}");
        }

        [Theory]
        [InlineData("$join(\"Not an array\")")]
        [InlineData("$join([\"a\", {\"b\":1}], \" \")")]
        [InlineData("$join([\"a\"], 123)")]
        public void JoinFunction_InvalidInput_ThrowsException(string expression)
        {
            var evaluator = new JsonataEvaluator(expression);
            Assert.Throws<JsonataEvaluationException>(() => evaluator.Evaluate((JsonNode?)null));
        }

        // $match tests
        [Theory]
        [InlineData("$match(\"abracadabra\", \"/a(b*)/\")", "[{\"match\":\"ab\",\"index\":0,\"groups\":[\"b\"]},{\"match\":\"a\",\"index\":3,\"groups\":[\"\"]},{\"match\":\"a\",\"index\":5,\"groups\":[\"\"]},{\"match\":\"ab\",\"index\":7,\"groups\":[\"b\"]},{\"match\":\"a\",\"index\":10,\"groups\":[\"\"]}]")]
        [InlineData("$match(\"abracadabra\", \"/a(b*)/\", 2)", "[{\"match\":\"ab\",\"index\":0,\"groups\":[\"b\"]},{\"match\":\"a\",\"index\":3,\"groups\":[\"\"]}]")]
        [InlineData("$match(\"abracadabra\", \"/a(b*)/i\")", "[{\"match\":\"ab\",\"index\":0,\"groups\":[\"b\"]},{\"match\":\"a\",\"index\":3,\"groups\":[\"\"]},{\"match\":\"a\",\"index\":5,\"groups\":[\"\"]},{\"match\":\"ab\",\"index\":7,\"groups\":[\"b\"]},{\"match\":\"a\",\"index\":10,\"groups\":[\"\"]}]")]
        [InlineData("$match(\"AAAA\", \"/a+/i\")", "[{\"match\":\"AAAA\",\"index\":0,\"groups\":[]}]")]
        [InlineData("$match(\"abc\", \"/x/\")", "[]")]
        [InlineData("$match(\"abc\", \"b\")", "[{\"match\":\"b\",\"index\":1,\"groups\":[]}]")]
        [InlineData("$match(\"\", \"/a/\")", "[]")]
        [InlineData("$match()", "[{\"match\":\"atat\",\"index\":3,\"groups\":[]}]")]
        public void MatchFunction_ValidInput_ReturnsCorrectArray(string expression, string expected)
        {
            var evaluator = new JsonataEvaluator(expression);
            string? inputJsonString = null;
            if (expression == "$match()")
            {
                inputJsonString = "\"abratatat\"";
                evaluator = new JsonataEvaluator("$match(\"atat\")");
            }
            JsonNode? resultNode = evaluator.Evaluate(TryParseJsonNode(inputJsonString));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), resultNode), $"Expr: {expression}, Expected: {expected}, Actual: {resultNode?.ToJsonString()}");
        }

        [Theory]
        [InlineData("$match(\"abc\", \"/[/\")")]
        [InlineData("$match(\"abc\", \"/a/q\")")]
        [InlineData("$match(123, \"a\")")]
        [InlineData("$match(\"abc\", 123)")]
        [InlineData("$match(\"abc\")")]
        [InlineData("$match(\"abc\", \"a\", \"b\")")]
        [InlineData("$match(\"abc\", \"a\", -1)")]
        public void MatchFunction_InvalidInput_ThrowsException(string expression)
        {
            var evaluator = new JsonataEvaluator(expression);
            Assert.Throws<JsonataEvaluationException>(() => evaluator.Evaluate((JsonNode?)null));
        }

        // $replace tests
        [Theory]
        // String replacement
        [InlineData("$replace(\"Hello World\", \"World\", \"Universe\")", "\"Hello Universe\"")]
        [InlineData("$replace(\"abracadabra\", \"a\", \"-\")", "\"-bracadabra\"")]
        [InlineData("$replace(\"abracadabra\", \"a\", \"-\", 1)", "\"-bracadabra\"")]
        [InlineData("$replace(\"aaaaa\", \"a\", \"b\")", "\"baaaa\"")]
        [InlineData("$replace(\"aaaaa\", \"a\", \"b\", 3)", "\"bbbaa\"")]
        [InlineData("$replace(\"aaaaa\", \"a\", \"b\", 0)", "\"aaaaa\"")]
        [InlineData("$replace(\"abc\", \"$$\", \"-\")", "\"abc\"")]
        [InlineData("$replace(\"abc\", \"$\", \"-\")", "\"abc\"")]
        [InlineData("$replace(\"abc\", \"a\", \"$$\")", "\"$bc\"")]

        // Regex replacement
        [InlineData("$replace(\"abracadabra\", \"/a/\", \"-\")", "\"-br-c-d-br-\"")]
        [InlineData("$replace(\"abracadabra\", \"/a/\", \"-\", 2)", "\"-br-cadabra\"")]
        [InlineData("$replace(\"Hello World\", \"/(\\\\w+)\\\\s(\\\\w+)/\", \"$2 $1\")", "\"World Hello\"")]
        [InlineData("$replace(\"price: $10\", \"/\\\\$(\\\\d+)/\", \"£$1\")", "\"price: \\u00A310\"")]
        [InlineData("$replace(\"item10 item20\", \"/item(\\\\d+)/\", \"part $1, \")", "\"part 10,  part 20, \"")]
        [InlineData("$replace(\"abc\", \"/./\", \"$&$&\")", "\"aabbcc\"")]
        [InlineData("$replace(\"abc\", \"/(b)/\", \"[$1$1]\")", "\"a[bb]c\"")]
        [InlineData("$replace(\"abc\", \"/b/\", \"$$\")", "\"a$c\"")]

        // Contextual
        [InlineData("$replace(\"World\", \"Universe\")", "\"Hello Universe\"")]
        [InlineData("$replace(\"/a/\", \"-\")", "\"-br-c-d-br-\"")]
        public void ReplaceFunction_StringAndRegex_ValidInput_ReturnsCorrectString(string expression, string expected)
        {
            var evaluator = new JsonataEvaluator(expression);
            string? inputJsonString = null;
            if (expression == "$replace(\"World\", \"Universe\")") inputJsonString = "\"Hello World\"";
            if (expression == "$replace(\"/a/\", \"-\")") inputJsonString = "\"abracadabra\"";
            JsonNode? resultNode = evaluator.Evaluate(TryParseJsonNode(inputJsonString));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), resultNode), $"Expr: {expression}, Expected: {expected}, Actual: {resultNode?.ToJsonString()}");
        }

        // [Fact]
        // public void ReplaceFunction_WithFunctionReplacement_ReturnsCorrectString()
        // {
            // Note: In C# string literals for JSONata expressions, backslashes for regex need to be C# escaped (e.g. \\d)
            // so the JSONata parser receives a string containing \d.
        //     string expression = "($double := function($m) { $string($number($m.groups[0]) * 2) }; $replace('Product Code: Prod100, Test200', '/Prod(\\d+)/', $double))";
        //     var evaluator = new JsonataEvaluator(expression);
        //     JsonNode? resultNode = evaluator.Evaluate((JsonNode?)null);
        //     Assert.True(JsonNode.DeepEquals(JsonNode.Parse("\"Product Code: 200, Test200\""), resultNode));
        // }

        // [Fact]
        // public void ReplaceFunction_WithFunctionReplacementAndLimit_ReturnsCorrectString()
        // {
        //     string expression = "($toUpperCase := function($match) { $uppercase($match.match) }; $replace('abc abc abc', /b/, $toUpperCase, 2))";
        //     var evaluator = new JsonataEvaluator(expression);
        //     JsonNode? resultNode = evaluator.Evaluate((JsonNode?)null);
        //     Assert.True(JsonNode.DeepEquals(JsonNode.Parse("\"aBc aBc abc\""), resultNode));
        // }

        // [Fact]
        // public void ReplaceFunction_WithFunctionReplacement_MatchObjectStructure()
        // {
        //     string expression = "($assertMatch := function($m) { $m.match = 'b' and $m.index = 1 and $count($m.groups) = 0 }; $replace('abc', /b/, $assertMatch))";
        //     var evaluator = new JsonataEvaluator(expression);
        //     JsonNode? resultNode = evaluator.Evaluate((JsonNode?)null);
        //     Assert.True(JsonNode.DeepEquals(JsonNode.Parse("\"atruec\""), resultNode));
        // }

        [Theory]
        [InlineData("$replace(\"abc\", \"/[/\")")]
        [InlineData("$replace(123, \"a\", \"b\")")]
        [InlineData("$replace(\"abc\", 123, \"b\")")]
        [InlineData("$replace(\"abc\", \"a\", 123)")]
        [InlineData("$replace(\"abc\", \"a\", \"b\", \"x\")")]
        [InlineData("$replace(\"abc\", \"a\")")]
        [InlineData("$replace(\"abc\")")]
        public void ReplaceFunction_InvalidInput_ThrowsException(string expression)
        {
            var evaluator = new JsonataEvaluator(expression);
            Assert.Throws<JsonataEvaluationException>(() => evaluator.Evaluate((JsonNode?)null));
        }

        // $string with prettify tests
        [Fact]
        public void StringFunction_PrettifyObject_ReturnsFormattedJson()
        {
            var evaluator = new JsonataEvaluator("$string(payload, true)");
            string inputJsonString = "{\"payload\": {\"a\": 1, \"b\": [2,3]}}";
            string expectedJsonString = "\"{\\n  \\u0022a\\u0022: 1,\\n  \\u0022b\\u0022: [\\n    2,\\n    3\\n  ]\\n}\"";
            JsonNode? resultNode = evaluator.Evaluate(TryParseJsonNode(inputJsonString));
            // For prettify, the result is a JSON string containing formatted JSON.
            // So we parse the expected outer string, then compare its value.
            Assert.Equal(JsonNode.Parse(expectedJsonString)!.GetValue<string>().Replace("\\n", System.Environment.NewLine), resultNode!.GetValue<string>().Replace("\\n", System.Environment.NewLine) );
        }

        [Fact]
        public void StringFunction_PrettifyArray_ReturnsFormattedJson()
        {
            var evaluator = new JsonataEvaluator("$string([1, {\"a\": 2}], true)");
            string expectedJsonString = "\"[\\n  1,\\n  {\\n    \\u0022a\\u0022: 2\\n  }\\n]\"";
            JsonNode? resultNode = evaluator.Evaluate((JsonNode?)null);
            Assert.Equal(JsonNode.Parse(expectedJsonString)!.GetValue<string>().Replace("\\n", System.Environment.NewLine), resultNode!.GetValue<string>().Replace("\\n", System.Environment.NewLine) );
        }

        [Fact]
        public void StringFunction_ObjectWithoutPrettify_ThrowsError()
        {
            var evaluator = new JsonataEvaluator("$string({\"a\":1})");
            Assert.Throws<JsonataEvaluationException>(() => evaluator.Evaluate((JsonNode?)null));
        }


        // $base64encode tests
        [Theory]
        [InlineData("$base64encode(\"Hello World\")", "\"SGVsbG8gV29ybGQ=\"")]
        [InlineData("$base64encode(\"\")", "\"\"")]
        [InlineData("$base64encode()", "\"SGVsbG9Db250ZXh0\"")]
        public void Base64EncodeFunction_ValidInput_ReturnsCorrectString(string expression, string expected)
        {
            var evaluator = new JsonataEvaluator(expression);
            string? inputJsonString = expression == "$base64encode()" ? "\"HelloContext\"" : null;
            JsonNode? resultNode = evaluator.Evaluate(TryParseJsonNode(inputJsonString));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), resultNode), $"Expr: {expression}, Expected: {expected}, Actual: {resultNode?.ToJsonString()}");
        }

        [Theory]
        [InlineData("$base64encode(123)")]
        [InlineData("$base64encode(null)")]
        public void Base64EncodeFunction_InvalidInput_ThrowsException(string expression)
        {
            var evaluator = new JsonataEvaluator(expression);
            Assert.Throws<JsonataEvaluationException>(() => evaluator.Evaluate((JsonNode?)null));
        }

        // $base64decode tests
        [Theory]
        [InlineData("$base64decode(\"SGVsbG8gV29ybGQ=\")", "\"Hello World\"")]
        [InlineData("$base64decode(\"\")", "\"\"")]
        [InlineData("$base64decode()", "\"HelloContext\"")]
        public void Base64DecodeFunction_ValidInput_ReturnsCorrectString(string expression, string expected)
        {
            var evaluator = new JsonataEvaluator(expression);
            string? inputJsonString = expression == "$base64decode()" ? "\"SGVsbG9Db250ZXh0\"" : null;
            JsonNode? resultNode = evaluator.Evaluate(TryParseJsonNode(inputJsonString));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), resultNode), $"Expr: {expression}, Expected: {expected}, Actual: {resultNode?.ToJsonString()}");
        }

        [Theory]
        [InlineData("$base64decode(123)")]
        [InlineData("$base64decode(null)")]
        [InlineData("$base64decode(\"Not Base64!\")")]
        public void Base64DecodeFunction_InvalidInput_ThrowsException(string expression)
        {
            var evaluator = new JsonataEvaluator(expression);
            Assert.Throws<JsonataEvaluationException>(() => evaluator.Evaluate((JsonNode?)null));
        }

        // $encodeUrlComponent tests
        [Theory]
        [InlineData("$encodeUrlComponent(\"a b/c?d#e\")", "\"a\\u002Bb%2Fc%3Fd%23e\"")]
        [InlineData("$encodeUrlComponent(\"\")", "\"\"")]
        public void EncodeUrlComponentFunction_ValidInput_ReturnsCorrectString(string expression, string expected)
        {
            var evaluator = new JsonataEvaluator(expression);
            JsonNode? resultNode = evaluator.Evaluate((JsonNode?)null);
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), resultNode), $"Expr: {expression}, Expected: {expected}, Actual: {resultNode?.ToJsonString()}");
        }

        // $encodeUrl tests (using Uri.EscapeUriString which is more lenient for full URLs)
        [Theory]
        [InlineData("$encodeUrl(\"http://example.com/a b?c=d#e\")", "\"http://example.com/a%20b?c=d#e\"")]
        public void EncodeUrlFunction_ValidInput_ReturnsCorrectString(string expression, string expected)
        {
            var evaluator = new JsonataEvaluator(expression);
            JsonNode? resultNode = evaluator.Evaluate((JsonNode?)null);
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), resultNode), $"Expr: {expression}, Expected: {expected}, Actual: {resultNode?.ToJsonString()}");
        }

        // $decodeUrlComponent tests
        [Theory]
        [InlineData("$decodeUrlComponent(\"a+b%2Fc%3Fd%23e\")", "\"a b/c?d#e\"")]
        [InlineData("$decodeUrlComponent(\"a b/c?d#e\")", "\"a b/c?d#e\"")]
        [InlineData("$decodeUrlComponent(\"\")", "\"\"")]
        public void DecodeUrlComponentFunction_ValidInput_ReturnsCorrectString(string expression, string expected)
        {
            var evaluator = new JsonataEvaluator(expression);
            JsonNode? resultNode = evaluator.Evaluate((JsonNode?)null);
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), resultNode), $"Expr: {expression}, Expected: {expected}, Actual: {resultNode?.ToJsonString()}");
        }

        // $decodeUrl tests
        [Theory]
        [InlineData("$decodeUrl(\"http://example.com/a%20b?c=d#e\")", "\"http://example.com/a b?c=d#e\"")]
        public void DecodeUrlFunction_ValidInput_ReturnsCorrectString(string expression, string expected)
        {
            var evaluator = new JsonataEvaluator(expression);
            JsonNode? resultNode = evaluator.Evaluate((JsonNode?)null);
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), resultNode), $"Expr: {expression}, Expected: {expected}, Actual: {resultNode?.ToJsonString()}");
        }
    }
}
