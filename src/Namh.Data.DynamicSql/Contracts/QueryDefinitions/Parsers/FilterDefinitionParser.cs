using Namh.Data.DynamicSql.QueryBuilderDto.Parsers;
using System.Globalization;

namespace Namh.Data.DynamicSql.Contracts.QueryDefinitions.Parsers;

internal sealed class FilterDefinitionParser
{
    private readonly List<Token> _tokens;
    private int _position;

    public static FilterDefinition? Parse(string? text)
        => string.IsNullOrEmpty(text) ? null : new FilterDefinitionParser(text).Parse();

    private FilterDefinitionParser(string text)
        => _tokens = Tokenize(text);

    private FilterDefinition Parse()
    {
        if (_tokens.Count == 1)
            throw new FormatException("The filter cannot be empty.");

        var expression = ParseOr();
        Expect(TokenType.End);
        return expression as FilterDefinition
            ?? new FilterDefinition(LogicalOperator.And, expression);
    }

    private IFilterDto ParseOr()
        => ParseGroup(ParseAnd, "OR", LogicalOperator.Or);

    private IFilterDto ParseAnd()
        => ParseGroup(ParsePrimary, "AND", LogicalOperator.And);

    private IFilterDto ParseGroup(
        Func<IFilterDto> operand,
        string separator,
        LogicalOperator logicalOperator)
    {
        var expression = operand();

        while (Match(separator))
        {
            var next = operand();
            if (next is FilterItemDto filter)
                next = filter with { LogicalOperator = logicalOperator };

            expression = new FilterDefinition(logicalOperator, expression, next);
        }

        return expression;
    }

    private IFilterDto ParsePrimary()
    {
        if (Match("("))
        {
            var grouped = ParseOr();
            Expect(")");
            return grouped;
        }

        var left = Next();
        if (left.Type is not (TokenType.Identifier or TokenType.Field or TokenType.FieldExpression))
            throw new FormatException($"Expected a filter field, received [{left.Text}].");
        var leftField = new FieldDefinition(left.Text);
        var operationToken = Next();
        if (operationToken.Type != TokenType.Operator)
            throw new FormatException($"Expected a filter operator, received [{operationToken.Text}].");
        var operation = operationToken.Text.ToLowerInvariant();
        var value = ParseFilterValue(operation);

        var filterOperator = operation switch
        {
            "=" or "==" => BinaryOperator.Equals,
            "<>" or "!=" => BinaryOperator.NotEquals,
            "like" => BinaryOperator.Like,
            ">" => BinaryOperator.GreaterThan,
            "<" => BinaryOperator.LessThan,
            ">=" => BinaryOperator.GreaterThanOrEqual,
            "<=" => BinaryOperator.LessThanOrEqual,
            _ => throw new FormatException($"Unsupported filter operator [{operation}].")
        };

        return new FilterItemDto(LogicalOperator.And, leftField, filterOperator, value)
        {
            Parameterized = true
        };
    }

    private IFieldDto? ParseFilterValue(string operation)
    {
        var token = Next();
        return token.Type switch
        {
            TokenType.Value => new ConstFieldDefinition(token.Value),
            TokenType.Field => new FieldDefinition(token.Text),
            TokenType.FieldExpression => token.Text.Equals("<NULL>", StringComparison.OrdinalIgnoreCase)
                ? null
                : FieldDefinitionParser.Parse(token.Text),
            TokenType.Identifier => operation is "==" or "!="
                ? new ConstFieldDefinition(token.Text)
                : FieldDefinitionParser.Parse(token.Text),
            _ => throw new FormatException($"Expected a filter value, received [{token.Text}].")
        };
    }

    private Token Next()
    {
        if (_position >= _tokens.Count)
            throw new FormatException("Unexpected end of filter.");

        return _tokens[_position++];
    }

    private Token Expect(TokenType type)
    {
        var token = Next();
        if (token.Type != type)
            throw new FormatException($"Expected {type}, received [{token.Text}].");
        return token;
    }

    private void Expect(string text)
    {
        var token = Next();
        if (!token.Text.Equals(text, StringComparison.OrdinalIgnoreCase))
            throw new FormatException($"Expected [{text}], received [{token.Text}].");
    }

    private bool Match(string text)
    {
        if (_position >= _tokens.Count ||
            !_tokens[_position].Text.Equals(text, StringComparison.OrdinalIgnoreCase))
            return false;

        _position++;
        return true;
    }

    private static List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        for (var index = 0; index < text.Length;)
        {
            if (char.IsWhiteSpace(text[index]))
            {
                index++;
                continue;
            }

            if (text[index] == '@')
            {
                index++;
                AddValueToken(text, ref index, tokens);

                continue;
            }

            if (text[index] is '\'' or '"')
            {
                AddValueToken(text, ref index, tokens);
                continue;
            }

            if (text[index] == '[')
            {
                var start = ++index;
                while (index < text.Length && text[index] != ']')
                    index++;
                if (index >= text.Length)
                    throw new FormatException("Unterminated field identifier.");

                var field = text[start..index];
                if (field.Length == 0)
                    throw new FormatException("Field identifier cannot be empty.");

                tokens.Add(new Token(TokenType.Field, field, null));
                index++;
                continue;
            }

            if (text[index] == '<' &&
                tokens.Count > 0 &&
                tokens[^1].Type == TokenType.Operator &&
                IsComparisonOperator(tokens[^1].Text))
            {
                var closingBracket = text.IndexOf('>', index + 1);
                if (closingBracket >= 0)
                {
                    var expression = text[index..(closingBracket + 1)];
                    tokens.Add(new Token(TokenType.FieldExpression, expression, null));
                    index = closingBracket + 1;
                    continue;
                }
            }

            if (IsIdentifierStart(text[index]))
            {
                var identifierEnd = index + 1;
                while (identifierEnd < text.Length && IsIdentifierPart(text[identifierEnd]))
                    identifierEnd++;

                var functionName = text[index..identifierEnd];
                if (identifierEnd < text.Length &&
                    text[identifierEnd] == '(' &&
                    !IsFilterKeyword(functionName) &&
                    TryFindClosingParenthesis(text, identifierEnd, out var closingParenthesis))
                {
                    var expression = text[index..(closingParenthesis + 1)];
                    tokens.Add(new Token(TokenType.FieldExpression, expression, null));
                    index = closingParenthesis + 1;
                    continue;
                }
            }

            if (index + 1 < text.Length &&
                new[] { "&&", "||", "==", "!=", ">=", "<=", "<>" }.Contains(text[index..(index + 2)]))
            {
                var operation = text[index..(index + 2)] switch
                {
                    "&&" => "AND",
                    "||" => "OR",
                    var value => value
                };
                tokens.Add(new Token(TokenType.Operator, operation, null));
                index += 2;
                continue;
            }

            if ("()=><+-*/".Contains(text[index]))
            {
                var value = text[index].ToString();
                tokens.Add(new Token(
                    text[index] is '(' or ')' ? TokenType.Parenthesis : TokenType.Operator,
                    value,
                    null));
                index++;
                continue;
            }

            var startIdentifier = index;
            while (index < text.Length &&
                   (char.IsLetterOrDigit(text[index]) || text[index] is '_' or '.'))
                index++;
            if (startIdentifier == index)
                throw new FormatException($"Unexpected filter character [{text[index]}].");

            var identifier = text[startIdentifier..index];
            tokens.Add(new Token(
                identifier.Equals("like", StringComparison.OrdinalIgnoreCase) ||
                identifier.Equals("and", StringComparison.OrdinalIgnoreCase) ||
                identifier.Equals("or", StringComparison.OrdinalIgnoreCase)
                    ? TokenType.Operator
                    : int.TryParse(identifier, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ||
                      decimal.TryParse(identifier, NumberStyles.Number, CultureInfo.InvariantCulture, out _) ||
                      identifier.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                      identifier.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                      identifier.Equals("null", StringComparison.OrdinalIgnoreCase)
                        ? TokenType.Value
                    : TokenType.Identifier,
                identifier,
                identifier.Equals("like", StringComparison.OrdinalIgnoreCase) ||
                identifier.Equals("and", StringComparison.OrdinalIgnoreCase) ||
                identifier.Equals("or", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : ParseValue(identifier)));
        }

        tokens.Add(new Token(TokenType.End, string.Empty, null));
        return tokens;
    }

    private static bool TryFindClosingParenthesis(string text, int openingParenthesis, out int closingParenthesis)
    {
        var depth = 0;
        var quote = '\0';
        var inField = false;

        for (var index = openingParenthesis; index < text.Length; index++)
        {
            var character = text[index];
            if (quote != '\0')
            {
                if (character == quote)
                {
                    if (index + 1 < text.Length && text[index + 1] == quote)
                        index++;
                    else
                        quote = '\0';
                }
                continue;
            }

            if (inField)
            {
                if (character == ']')
                    inField = false;
                continue;
            }

            if (character is '\'' or '"')
            {
                quote = character;
                continue;
            }

            if (character == '[')
            {
                inField = true;
                continue;
            }

            if (character == '(')
                depth++;
            else if (character == ')' && --depth == 0)
            {
                closingParenthesis = index;
                return true;
            }
        }

        closingParenthesis = -1;
        return false;
    }

    private static bool IsIdentifierStart(char character)
        => char.IsLetter(character) || character == '_';

    private static bool IsIdentifierPart(char character)
        => char.IsLetterOrDigit(character) || character is '_' or '.';

    private static bool IsFilterKeyword(string identifier)
        => identifier.Equals("and", StringComparison.OrdinalIgnoreCase) ||
            identifier.Equals("or", StringComparison.OrdinalIgnoreCase) ||
            identifier.Equals("like", StringComparison.OrdinalIgnoreCase);

    private static void AddValueToken(
        string text,
        ref int index,
        ICollection<Token> tokens)
    {
        if (index < text.Length && text[index] is '\'' or '"')
        {
            var quote = text[index];
            index++;
            var value = new System.Text.StringBuilder();
            while (index < text.Length)
            {
                if (text[index] == quote && index + 1 < text.Length && text[index + 1] == quote)
                {
                    value.Append(quote);
                    index += 2;
                    continue;
                }
                if (text[index] == quote)
                    break;
                value.Append(text[index++]);
            }
            if (index >= text.Length)
                throw new FormatException("Unterminated quoted filter value.");

            var parsedValue = value.ToString();
            tokens.Add(new Token(TokenType.Value, parsedValue, parsedValue));
            index++;
            return;
        }

        var valueStart = index;
        while (index < text.Length && !char.IsWhiteSpace(text[index]) &&
               !"()&|=<>!".Contains(text[index]))
            index++;

        var valueText = text[valueStart..index];
        tokens.Add(new Token(TokenType.Value, valueText, ParseValue(valueText)));
    }

    private static object? ParseValue(string value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)
            ? integer
            : decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
                ? number
                : bool.TryParse(value, out var boolean)
                    ? boolean
                    : value.Equals("null", StringComparison.OrdinalIgnoreCase)
                        ? null
                        : value;

    private static bool IsComparisonOperator(string operation)
        => operation is "=" or "==" or "!=" or "<>" or ">" or "<" or ">=" or "<=" ||
            operation.Equals("LIKE", StringComparison.OrdinalIgnoreCase);

    private enum TokenType
    {
        Identifier,
        Field,
        FieldExpression,
        Operator,
        Parenthesis,
        Value,
        End
    }

    private sealed record Token(TokenType Type, string Text, object? Value);
}
