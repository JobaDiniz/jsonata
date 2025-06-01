using Xunit;
using System.Text.Json.Nodes;
using Jsonata.Net.Core;
using System.Collections.Generic;
using System.Text.Json; // For JsonSerializerOptions if needed for DeepEquals comparison refinement

namespace Jsonata.Net.Tests.OriginalJsonataTests.Groups.Closures
{
    public class Case001Tests
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
        public void Case001()
        {
            // From test/test-suite/groups/closures/case001.json
            string expression = @"Account.(
                $AccName := function() { `Account Name` };
                Order[OrderID = ""order104""].Product{
                    ""Account"": $AccName(),
                    ""SKU-"" & $string(ProductID): `Product Name`
                }
            )";

            JsonNode? input = JsonNode.Parse(Dataset5FireflyOrder104);

            JsonataEvaluator evaluator = new JsonataEvaluator(expression);
            JsonNode? actualNode = evaluator.Evaluate(input); // Evaluate now returns JsonNode?

            string expectedJson = @"
            {
                ""Account"": ""Firefly"",
                ""SKU-858383"": ""Bowler Hat"",
                ""SKU-345664"": ""Cloak""
            }";

            // JsonNode? actualNode = JsonNode.Parse(resultJson); // Removed
            JsonNode? expectedNode = JsonNode.Parse(expectedJson);

            Assert.True(JsonNode.DeepEquals(expectedNode, actualNode), $"Expected:\n{expectedNode?.ToJsonString(new JsonSerializerOptions { WriteIndented = true })}\nActual:\n{actualNode?.ToJsonString(new JsonSerializerOptions { WriteIndented = true })}");
        }
    }
}
