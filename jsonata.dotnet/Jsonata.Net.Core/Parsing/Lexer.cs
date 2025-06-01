using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Jsonata.Net.Core.Exceptions;

namespace Jsonata.Net.Core.Parsing
{
    public sealed class Lexer
    {
        private readonly string _expression;
        private int _position;
        private int _length;

        public Lexer(string expression)
        {
            _expression = expression ?? throw new ArgumentNullException(nameof(expression));
            _length = _expression.Length;
            _position = 0;
        }

        public List<Token> Tokenize()
        {
            var tokens = new List<Token>();
            while (_position < _length)
            {
                char currentChar = _expression[_position];
                if (char.IsWhiteSpace(currentChar)) { _position++; continue; }

                if (currentChar == '"') { tokens.Add(ReadStringLiteral()); continue; }
                if (currentChar == '`') { tokens.Add(ReadBacktickedIdentifier()); continue; }

                if (char.IsDigit(currentChar) || (currentChar == '-' && _position + 1 < _length && char.IsDigit(_expression[_position + 1])))
                {
                    tokens.Add(ReadNumericLiteral()); continue;
                }

                if (TryReadKeyword("true", TokenType.BooleanLiteral, tokens)) continue;
                if (TryReadKeyword("false", TokenType.BooleanLiteral, tokens)) continue;
                if (TryReadKeyword("null", TokenType.NullLiteral, tokens)) continue;
                if (TryReadKeyword("function", TokenType.Function, tokens)) continue;
                if (TryReadKeyword("and", TokenType.And, tokens)) continue;
                if (TryReadKeyword("or", TokenType.Or, tokens)) continue;
                if (TryReadKeyword("in", TokenType.In, tokens)) continue;

                if (currentChar == '$')
                {
                    // If '$' is followed by a character that can start an identifier part, it's a variable.
                    // Otherwise, it's the context variable '$' itself.
                    if (_position + 1 < _length && IsIdentifierChar(_expression[_position + 1]))
                    {
                        tokens.Add(ReadVariableOrIdentifier(true));
                    }
                    else
                    {
                        tokens.Add(new Token(TokenType.DollarSign, "$", _position++));
                    }
                    continue;
                }

                if (currentChar == '.') { tokens.Add(new Token(TokenType.Dot, ".", _position++)); continue; }
                if (currentChar == ',') { tokens.Add(new Token(TokenType.Comma, ",", _position++)); continue; }
                if (currentChar == ':') { if (TryReadOperator(":=", TokenType.Assign, tokens)) continue; tokens.Add(new Token(TokenType.Colon, ":", _position++)); continue; }
                if (currentChar == ';') { tokens.Add(new Token(TokenType.Semicolon, ";", _position++)); continue; }
                if (currentChar == '(') { tokens.Add(new Token(TokenType.LeftParen, "(", _position++)); continue; }
                if (currentChar == ')') { tokens.Add(new Token(TokenType.RightParen, ")", _position++)); continue; }
                if (currentChar == '[') { tokens.Add(new Token(TokenType.LeftBracket, "[", _position++)); continue; }
                if (currentChar == ']') { tokens.Add(new Token(TokenType.RightBracket, "]", _position++)); continue; }
                if (currentChar == '{') { tokens.Add(new Token(TokenType.LeftBrace, "{", _position++)); continue; }
                if (currentChar == '}') { tokens.Add(new Token(TokenType.RightBrace, "}", _position++)); continue; }
                if (currentChar == '+') { tokens.Add(new Token(TokenType.Plus, "+", _position++)); continue; }
                if (currentChar == '-') { tokens.Add(new Token(TokenType.Minus, "-", _position++)); continue; }
                if (currentChar == '*') { tokens.Add(new Token(TokenType.Asterisk, "*", _position++)); continue; }
                if (currentChar == '/') { tokens.Add(new Token(TokenType.Slash, "/", _position++)); continue; }
                if (currentChar == '%') { tokens.Add(new Token(TokenType.Percent, "%", _position++)); continue; }
                if (currentChar == '=') { tokens.Add(new Token(TokenType.Equals, "=", _position++)); continue; }
                if (TryReadOperator("!=", TokenType.NotEquals, tokens)) continue;
                if (TryReadOperator("<=", TokenType.LessThanOrEqual, tokens)) continue;
                if (currentChar == '<') { tokens.Add(new Token(TokenType.LessThan, "<", _position++)); continue; }
                if (TryReadOperator(">=", TokenType.GreaterThanOrEqual, tokens)) continue;
                if (currentChar == '>') { tokens.Add(new Token(TokenType.GreaterThan, ">", _position++)); continue; }
                if (currentChar == '&') { tokens.Add(new Token(TokenType.Concatenate, "&", _position++)); continue; }
                if (currentChar == '?') { tokens.Add(new Token(TokenType.Question, "?", _position++)); continue; }

                if (char.IsLetter(currentChar) || currentChar == '_') { tokens.Add(ReadVariableOrIdentifier(false)); continue; }

                tokens.Add(new Token(TokenType.Unknown, currentChar.ToString(), _position++));
            }
            tokens.Add(new Token(TokenType.EOF, string.Empty, _position));
            return tokens;
        }

        private bool TryReadKeyword(string keyword, TokenType type, List<Token> tokens)
        {
            if (_position + keyword.Length <= _length &&
                string.Compare(_expression, _position, keyword, 0, keyword.Length) == 0)
            {
                if (_position + keyword.Length == _length || !IsIdentifierChar(_expression[_position + keyword.Length]))
                {
                    tokens.Add(new Token(type, keyword, _position));
                    _position += keyword.Length;
                    return true;
                }
            }
            return false;
        }

        private bool IsIdentifierChar(char ch)
        {
            return char.IsLetterOrDigit(ch) || ch == '_';
        }

        private bool TryReadOperator(string op, TokenType type, List<Token> tokens)
        {
            if (_position + op.Length <= _length && _expression.Substring(_position, op.Length) == op)
            {
                tokens.Add(new Token(type, op, _position));
                _position += op.Length;
                return true;
            }
            return false;
        }

        private Token ReadStringLiteral()
        {
            int startPos = _position;
            _position++;
            StringBuilder sb = new StringBuilder();
            bool closed = false;
            while (_position < _length)
            {
                char ch = _expression[_position++];
                if (ch == '"') { closed = true; break; }
                if (ch == '\\')
                {
                    if (_position >= _length) throw new JsonataParseException($"Unterminated escape sequence in string literal starting at {startPos}");
                    char nextCh = _expression[_position++];
                    switch (nextCh)
                    {
                        case '"': sb.Append('"'); break; case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break; case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break; case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break; case 't': sb.Append('\t'); break;
                        default: throw new JsonataParseException($"Invalid escape sequence: \\{nextCh} in string literal starting at {startPos}");
                    }
                } else { sb.Append(ch); }
            }
            if (!closed) throw new JsonataParseException($"Unterminated string literal starting at {startPos}");
            return new Token(TokenType.StringLiteral, sb.ToString(), startPos);
        }

        private Token ReadBacktickedIdentifier()
        {
            int startPos = _position;
            _position++;
            StringBuilder sb = new StringBuilder();
            bool closed = false;
            while (_position < _length)
            {
                char ch = _expression[_position++];
                if (ch == '`')
                {
                    if (_position < _length && _expression[_position] == '`')
                    {
                        sb.Append('`');
                        _position++;
                    }
                    else
                    {
                        closed = true;
                        break;
                    }
                }
                else
                {
                    sb.Append(ch);
                }
            }
            if (!closed) throw new JsonataParseException($"Unterminated backticked identifier starting at position {startPos}.");
            return new Token(TokenType.Identifier, sb.ToString(), startPos);
        }


        private Token ReadNumericLiteral()
        {
            int start = _position;
            if (_expression[_position] == '-') _position++;
            while (_position < _length && (char.IsDigit(_expression[_position]) || _expression[_position] == '.'))
            {
                _position++;
            }
            string value = _expression.Substring(start, _position - start);
            return new Token(TokenType.NumericLiteral, value, start);
        }

        private Token ReadVariableOrIdentifier(bool isVariablePrefixPresent)
        {
            int tokenStartPos = _position; // Start of '$' or first letter of identifier
            if (isVariablePrefixPresent)
            {
                _position++; // Consume '$'
            }

            int idActualStartPos = _position;
            while (_position < _length && IsIdentifierChar(_expression[_position]))
            {
                _position++;
            }

            if (_position == idActualStartPos) // No actual identifier characters were consumed after '$' or at all
            {
                 // This case should ideally be caught by the '$' standalone logic if !IsIdentifierChar after $
                 // If isVariablePrefixPresent is false, it means first char was IsLetter or '_', so idActualStartPos must advance.
                 // This error primarily targets malformed variables like "$" followed by non-identifier char not caught by DollarSign token logic
                 throw new JsonataParseException($"Invalid {(isVariablePrefixPresent? "variable" : "identifier")} name starting at position {tokenStartPos}. Expected identifier characters.");
            }

            string value = _expression.Substring(tokenStartPos, _position - tokenStartPos);
            return new Token(isVariablePrefixPresent ? TokenType.Variable : TokenType.Identifier, value, tokenStartPos);
        }
    }
}
