using Xunit;
using Jsonata.Net.Core.Parsing;
using Jsonata.Net.Core.AstNodes;
using Jsonata.Net.Core.Exceptions;
using Jsonata.Net.Core.Evaluation;
using System.Text.Json.Nodes;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public class ParserTests
{
    private AstNode ParseString(string expression)
    {
        var parser = new JsonataParser(expression);
        return parser.Parse();
    }

    [Fact] public void TestStringLiteral() {
        var node = ParseString("\"hello\"");
        Assert.IsType<StringLiteralNode>(node);
        Assert.Equal("hello", ((StringLiteralNode)node).Value);
    }

    [Fact] public void TestFunctionCall_MultipleArgs() {
        var node = (FunctionCallNode)ParseString("calc(\"add\", 5, $var)");
        Assert.IsType<NameNode>(node.Procedure);
        Assert.Equal("calc", ((NameNode)node.Procedure).Value);
        Assert.Equal(3, node.Arguments.Count);
    }

    [Fact] public void TestObjectConstructor_WithExpressions() {
        var node = (ObjectConstructorNode)ParseString("{ \"sum\": 1+2, anotherKey: $data.name }");
        Assert.Equal(2, node.Pairs.Count);
        // Additional assertions for keys/values can be added if needed for this specific test
        var pair2 = node.Pairs[1];
        Assert.IsType<NameNode>(pair2.Key);
        Assert.Equal("anotherKey", ((NameNode)pair2.Key).Value);
    }

    [Fact] public void TestSimpleTernary() {
        var node = (ConditionalNode)ParseString("true ? \"yes\" : \"no\"");
        Assert.IsType<BooleanLiteralNode>(node.Condition);
        Assert.True(((BooleanLiteralNode)node.Condition).Value);
        Assert.IsType<StringLiteralNode>(node.ThenExpression);
        Assert.Equal("yes", ((StringLiteralNode)node.ThenExpression).Value);
        Assert.NotNull(node.ElseExpression);
        Assert.IsType<StringLiteralNode>(node.ElseExpression!); // Null forgiveness
        Assert.Equal("no", ((StringLiteralNode)node.ElseExpression!).Value);
    }

    [Fact] public void TestTernaryWithExpressions() {
        var node = (ConditionalNode)ParseString("$value > 10 ? $value * 2 : $value / 2");
        Assert.IsType<BinaryOperatorNode>(node.Condition);
        Assert.IsType<BinaryOperatorNode>(node.ThenExpression);
        Assert.NotNull(node.ElseExpression);
        Assert.IsType<BinaryOperatorNode>(node.ElseExpression!); // Null forgiveness
    }

    [Fact] public void TestNestedTernary_RightAssociative() {
        var node = (ConditionalNode)ParseString("condA ? thenA : condB ? thenB : elseB");
        Assert.IsType<NameNode>(node.Condition);
        Assert.IsType<NameNode>(node.ThenExpression);
        var nestedElse = Assert.IsType<ConditionalNode>(node.ElseExpression);
        Assert.IsType<NameNode>(nestedElse.Condition);
        Assert.IsType<NameNode>(nestedElse.ThenExpression);
        Assert.IsType<NameNode>(nestedElse.ElseExpression!); // Null forgiveness
    }

    [Fact] public void TestSimpleAssignment() {
        var node = (AssignmentNode)ParseString("$myVar := 42");
        Assert.IsType<VariableNode>(node.Variable);
        Assert.Equal("$myVar", node.Variable.Value);
        Assert.IsType<NumericLiteralNode>(node.Expression);
        Assert.Equal(42m, ((NumericLiteralNode)node.Expression).Value);
    }

    [Fact] public void TestAssignmentWithExpression() {
        var node = (AssignmentNode)ParseString("$data := {\"name\": \"Test\", \"value\": count + 1}");
        Assert.IsType<VariableNode>(node.Variable);
        Assert.Equal("$data", node.Variable.Value);
        Assert.IsType<ObjectConstructorNode>(node.Expression);
    }

    [Fact] public void TestAssignmentPrecedence() {
        var node = (AssignmentNode)ParseString("$a := 1 + 2");
        Assert.IsType<VariableNode>(node.Variable);
        var expr = Assert.IsType<BinaryOperatorNode>(node.Expression);
        Assert.Equal("+", expr.OperatorSymbol);
        Assert.Equal(1m, ((NumericLiteralNode)expr.Lhs).Value);
        Assert.Equal(2m, ((NumericLiteralNode)expr.Rhs).Value);
    }

    [Fact] public void TestChainedAssignment_RightAssociative() {
        var node = (AssignmentNode)ParseString("$a := $b := 5");
        Assert.Equal("$a", node.Variable.Value);
        var nestedAssignment = Assert.IsType<AssignmentNode>(node.Expression);
        Assert.Equal("$b", nestedAssignment.Variable.Value);
        Assert.Equal(5m, ((NumericLiteralNode)nestedAssignment.Expression).Value);
    }

    [Fact] public void TestTernaryAndOtherOperators() {
        var node = (BinaryOperatorNode)ParseString("1 + (true ? 10 : 20) * 2");
        Assert.Equal("+", node.OperatorSymbol);
        Assert.Equal(1m, ((NumericLiteralNode)node.Lhs).Value);
        var multNode = Assert.IsType<BinaryOperatorNode>(node.Rhs);
        Assert.Equal("*", multNode.OperatorSymbol);
        Assert.IsType<ConditionalNode>(multNode.Lhs);
        Assert.Equal(2m, ((NumericLiteralNode)multNode.Rhs).Value);
    }

    [Fact] public void TestInvalidAssignmentLHS() {
        Assert.Throws<JsonataParseException>(() => ParseString("myVar := 42"));
        Assert.Throws<JsonataParseException>(() => ParseString("10 := 42"));
    }
}
