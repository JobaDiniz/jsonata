using Jsonata.Net.Core.AstNodes;
using Jsonata.Net.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Jsonata.Net.Core.Parsing
{
    public sealed class JsonataParser
    {
        private readonly List<Token> _tokens;
        private int _currentTokenIndex;

        public JsonataParser(string expression)
        {
            Lexer lexer = new Lexer(expression);
            _tokens = lexer.Tokenize();
            _currentTokenIndex = 0;
            if (_tokens.Count == 0 || _tokens[_tokens.Count-1].Type != TokenType.EOF)
            {
                throw new JsonataParseException("Lexer did not produce EOF token or produced no tokens.");
            }
        }

        private Token CurrentToken => _tokens[_currentTokenIndex];

        private Token ConsumeToken()
        {
            Token token = this.CurrentToken;
            if (token.Type != TokenType.EOF) _currentTokenIndex++;
            return token;
        }

        private Token Expect(TokenType type)
        {
            Token token = ConsumeToken();
            if (token.Type != type)
            {
                throw new JsonataParseException($"Expected token type {type} but got {token.Type} ('{token.Value}') at position {token.Position}.");
            }
            return token;
        }

        public AstNode Parse()
        {
            AstNode node = ParseExpression(0);
            if (CurrentToken.Type != TokenType.EOF)
            {
                 throw new JsonataParseException($"Unexpected token {CurrentToken.Type} ('{CurrentToken.Value}') after parsing expression. Expected EOF.");
            }
            return node;
        }

        private AstNode ParseExpression(int precedence)
        {
            AstNode left = ParsePrefix();
            while (precedence < GetOperatorPrecedence(CurrentToken.Type))
            {
                Token operatorToken = CurrentToken;
                if (operatorToken.Type == TokenType.Question)
                {
                    if (GetOperatorPrecedence(TokenType.Question) <= precedence) break;
                    ConsumeToken();
                    AstNode thenExpr = ParseExpression(GetOperatorPrecedence(TokenType.Question) - 1);
                    Expect(TokenType.Colon);
                    AstNode elseExpr = ParseExpression(GetOperatorPrecedence(TokenType.Question) - 1);
                    left = new ConditionalNode(left, thenExpr, elseExpr);
                }
                else
                {
                    ConsumeToken();
                    left = ParseInfix(left, operatorToken);
                }
            }
            return left;
        }

        private int GetOperatorPrecedence(TokenType type)
        {
            switch (type)
            {
                case TokenType.Dot: return 80;
                // Postfix operators like '[', '(', '{' (transform) are handled in ParseAtom's loop
                case TokenType.Asterisk: case TokenType.Slash: case TokenType.Percent: return 60;
                case TokenType.Plus: case TokenType.Minus: return 50;
                case TokenType.Concatenate: return 40;
                case TokenType.Equals: case TokenType.NotEquals: case TokenType.LessThan:
                case TokenType.LessThanOrEqual: case TokenType.GreaterThan: case TokenType.GreaterThanOrEqual:
                case TokenType.In: return 30;
                case TokenType.And: return 20;
                case TokenType.Or: return 10;
                case TokenType.Question: return 6; // Ternary conditional
                case TokenType.Assign: return 5;
                default: return 0;
            }
        }

        private AstNode ParsePrefix()
        {
            Token token = CurrentToken;
            if (token.Type == TokenType.Minus)
            {
                ConsumeToken();
                AstNode operand = ParseExpression(70); // High precedence for unary minus operand
                // Represent unary minus as (0 - operand) for simplicity in BinaryOperatorNode
                return new BinaryOperatorNode(new NumericLiteralNode(0), "-", operand);
            }
            return ParseAtom();
        }

        private AstNode ParseInfix(AstNode left, Token operatorToken)
        {
            int currentPrecedence = GetOperatorPrecedence(operatorToken.Type);
            int nextPrecedence = (operatorToken.Type == TokenType.Assign || operatorToken.Type == TokenType.Question)
                               ? currentPrecedence - 1 // Right-associative for assign and ternary's condition part
                               : currentPrecedence;

            if (operatorToken.Type == TokenType.Dot)
            {
                AstNode rightStep;
                if (CurrentToken.Type == TokenType.LeftParen) // Path block: Account.(...)
                {
                    ConsumeToken(); // Consume '('
                    List<AstNode> expressionsInBlock = new List<AstNode>();
                    if (CurrentToken.Type != TokenType.RightParen)
                    {
                        while (true)
                        {
                            expressionsInBlock.Add(ParseExpression(0));
                            if (CurrentToken.Type == TokenType.RightParen) break;
                            Expect(TokenType.Semicolon);
                        }
                    }
                    Expect(TokenType.RightParen);
                    rightStep = new BlockNode(expressionsInBlock);
                }
                else
                {
                    // Check if the token following the dot is a StringLiteral
                    Token nextTokenForRhs = CurrentToken;
                    if (nextTokenForRhs.Type == TokenType.StringLiteral)
                    {
                        ConsumeToken(); // Consume the string literal token
                        rightStep = new NameNode(nextTokenForRhs.Value); // Treat its value as an identifier for NameNode
                    }
                    else
                    {
                        // Regular path step (e.g. NameNode, VariableNode which could be '$')
                        // Parse with high precedence to ensure `foo.bar.baz` groups left-to-right
                        // and `foo.bar` is LHS for next `.baz`
                        rightStep = ParseExpression(currentPrecedence);
                    }
                }
                return new PathOperatorNode(left, rightStep);
            }

            AstNode rightNode = ParseExpression(nextPrecedence);
            if (operatorToken.Type == TokenType.Assign)
            {
                if (left is VariableNode varNode)
                {
                    return new AssignmentNode(varNode, rightNode);
                }
                else
                {
                    throw new JsonataParseException("Left-hand side of assignment (:=) must be a variable.");
                }
            }
            return new BinaryOperatorNode(left, operatorToken.Value, rightNode);
        }

        private AstNode ParseAtom()
        {
            AstNode node;
            Token token = CurrentToken;
            switch (token.Type)
            {
                case TokenType.StringLiteral: ConsumeToken(); node = new StringLiteralNode(token.Value); break;
                case TokenType.NumericLiteral:
                    ConsumeToken();
                    if (decimal.TryParse(token.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal decVal))
                    {
                        node = new NumericLiteralNode(decVal);
                    }
                    else { throw new JsonataParseException($"Invalid numeric literal: {token.Value}"); }
                    break;
                case TokenType.BooleanLiteral: ConsumeToken(); node = new BooleanLiteralNode(token.Value == "true"); break;
                case TokenType.NullLiteral: ConsumeToken(); node = NullLiteralNode.Instance; break;
                case TokenType.Identifier: ConsumeToken(); node = new NameNode(token.Value); break;
                case TokenType.Variable: ConsumeToken(); node = new VariableNode(token.Value); break;
                case TokenType.DollarSign: ConsumeToken(); node = new VariableNode("$"); break; // $ as context variable
                case TokenType.Function:
                    ConsumeToken(); // Consume 'function'
                    Expect(TokenType.LeftParen);
                    List<VariableNode> parameters = new List<VariableNode>();
                    if (CurrentToken.Type != TokenType.RightParen)
                    {
                        while (true)
                        {
                            Token paramToken = Expect(TokenType.Variable);
                            parameters.Add(new VariableNode(paramToken.Value));
                            if (CurrentToken.Type == TokenType.RightParen) break;
                            Expect(TokenType.Comma);
                        }
                    }
                    Expect(TokenType.RightParen);
                    Expect(TokenType.LeftBrace);
                    AstNode body = ParseExpression(0);
                    Expect(TokenType.RightBrace);
                    node = new FunctionDefinitionNode(parameters, body);
                    break;
                case TokenType.LeftParen: // Grouping ( expr )
                    ConsumeToken();
                    node = ParseExpression(0);
                    Expect(TokenType.RightParen);
                    break;
                case TokenType.LeftBracket: // Array constructor [ expr, ... ]
                    ConsumeToken();
                    List<AstNode> elements = new List<AstNode>();
                    if (CurrentToken.Type != TokenType.RightBracket)
                    {
                        while (true)
                        {
                            elements.Add(ParseExpression(0));
                            if (CurrentToken.Type == TokenType.RightBracket) break;
                            Expect(TokenType.Comma);
                        }
                    }
                    Expect(TokenType.RightBracket);
                    node = new ArrayConstructorNode(elements);
                    break;
                case TokenType.LeftBrace: // Object constructor { key: val, ... } (NOT transform)
                    ConsumeToken();
                    List<KeyValuePairNode> pairs = new List<KeyValuePairNode>();
                    if (CurrentToken.Type != TokenType.RightBrace)
                    {
                        while (true)
                        {
                            AstNode keyNode = ParseExpression(0);
                            Expect(TokenType.Colon);
                            AstNode valueNode = ParseExpression(0);
                            pairs.Add(new KeyValuePairNode(keyNode, valueNode));
                            if (CurrentToken.Type == TokenType.RightBrace) break;
                            Expect(TokenType.Comma);
                        }
                    }
                    Expect(TokenType.RightBrace);
                    node = new ObjectConstructorNode(pairs); // This is the standalone object constructor
                    break;
                default:
                    throw new JsonataParseException($"Unexpected token {token.Type} ('{token.Value}') at position {token.Position} in ParseAtom.");
            }

            // Postfix operators (applied left-to-right, highest precedence)
            while (true)
            {
                if (CurrentToken.Type == TokenType.LeftBracket) { // Array/object accessor (predicate): node[expr]
                    ConsumeToken();
                    AstNode indexOrNameExpr = ParseExpression(0);
                    Expect(TokenType.RightBracket);
                    // This is a path step, so wrap it in PathOperatorNode, with 'node' as LHS
                    // and ArrayMemberNode representing the [expr] part.
                    node = new PathOperatorNode(node, new ArrayMemberNode(indexOrNameExpr));
                }
                else if (CurrentToken.Type == TokenType.LeftParen && IsCallableProcedure(node)) // Function call: node(args)
                {
                    ConsumeToken();
                    List<AstNode> arguments = new List<AstNode>();
                    if (CurrentToken.Type != TokenType.RightParen)
                    {
                        while (true)
                        {
                            arguments.Add(ParseExpression(0));
                            if (CurrentToken.Type == TokenType.RightParen) break;
                            Expect(TokenType.Comma);
                        }
                    }
                    Expect(TokenType.RightParen);
                    node = new FunctionCallNode(node, arguments);
                }
                else if (CurrentToken.Type == TokenType.LeftBrace) // Object Transform: node{map}
                {
                    ConsumeToken(); // Consume '{'
                    List<KeyValuePairNode> transformPairs = new List<KeyValuePairNode>();
                    if (CurrentToken.Type != TokenType.RightBrace)
                    {
                        while (true)
                        {
                            AstNode keyNode = ParseExpression(0);
                            Expect(TokenType.Colon);
                            AstNode valueNode = ParseExpression(0);
                            transformPairs.Add(new KeyValuePairNode(keyNode, valueNode));
                            if (CurrentToken.Type == TokenType.RightBrace) break;
                            Expect(TokenType.Comma);
                        }
                    }
                    Expect(TokenType.RightBrace);
                    node = new ObjectTransformNode(node, transformPairs);
                }
                else { break; }
            }
            return node;
        }

        // Helper to determine if a node can be the "procedure" part of a function call
        private bool IsCallableProcedure(AstNode node)
        {
            // NameNode (e.g. $sum), VariableNode (e.g. $myFunc), PathOperatorNode (e.g. lib.myFunc),
            // FunctionCallNode (if a function returns another function),
            // ArrayConstructorNode/ObjectConstructorNode (if spec allows calling them, less common for user funcs),
            // FunctionDefinitionNode (e.g. (function(){...})() - an IIFE)
            // VariableNode("$") for context function calls like $()
            return node is NameNode
                || node is VariableNode
                || node is PathOperatorNode
                || node is FunctionCallNode
                || node is FunctionDefinitionNode // Allows IIFE: (function(){...})()
                || node is ArrayConstructorNode   // Allows `[ ... ](...)` if array itself is callable (not standard JSONata)
                || node is ObjectConstructorNode; // Allows `{ ... }(...)` if object itself is callable (not standard JSONata)
        }
    }
}
