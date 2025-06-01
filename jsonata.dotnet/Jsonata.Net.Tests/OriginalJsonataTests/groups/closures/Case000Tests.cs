using Xunit;
using System.Text.Json.Nodes;
using Jsonata.Net.Core; // Assuming JsonataEvaluator is here or in a sub-namespace
using System.Collections.Generic;

namespace Jsonata.Net.Tests.OriginalJsonataTests.Groups.Closures
{
    public class Case000Tests
    {
        private const string Dataset5FireflyOrder104 = @"
        {
          ""Account"": {
            ""Account Name"": ""Firefly"",
            ""Order"": [
              {
                ""OrderID"": ""order104"",
                ""Product"": [
                  { ""ProductID"": 858383, ""Product Name"": ""Bowler Hat"" },
                  { ""ProductID"": 345664, ""Product Name"": ""Cloak"" }
                ]
              }
            ]
          }
        }";

        [Fact]
        public void Case000()
        {
            string expression = @"Account.(
                $AccName := function() { $.""Account Name"" };
                Order[OrderID = ""order104""].Product{
                    ""Account"": $AccName(),
                    ""SKU-"" & $string(ProductID): `Product Name`
                }
            )";

            JsonNode? input = JsonNode.Parse(Dataset5FireflyOrder104);

            // Assuming JsonataEvaluator can be instantiated directly.
            // If it requires specific setup or options, that might need adjustment.
            JsonataEvaluator evaluator = new JsonataEvaluator(expression);

            JsonNode? actualNode = evaluator.Evaluate(input); // Evaluate now returns JsonNode?

            // Expected result from case000.json
            string expectedJson = @"
            {
                ""Account"": ""Firefly"",
                ""SKU-858383"": ""Bowler Hat"",
                ""SKU-345664"": ""Cloak""
            }";

            // Normalize JSON strings for comparison (e.g., remove insignificant whitespace)
            // JsonNode? actualNode = JsonNode.Parse(resultJson); // Removed, actualNode is now the direct result
            JsonNode? expectedNode = JsonNode.Parse(expectedJson);

            Assert.True(JsonNode.DeepEquals(expectedNode, actualNode), $"Expected:\n{expectedNode?.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true })}\nActual:\n{actualNode?.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true })}");
        }
    }
}
