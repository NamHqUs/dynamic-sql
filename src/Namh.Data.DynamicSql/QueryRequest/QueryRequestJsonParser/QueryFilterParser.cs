using System.Globalization;

namespace Namh.Data.DynamicSql.QueryRequestParsing;

internal sealed class QueryFilterParser
{
    private readonly List<Token> _tokens;
    private int _position;

    public QueryFilterParser(string text)
        => _tokens = Tokenize(text);

    public QueryFilter Parse()
    {
        if (_tokens.Count == 1)
            throw new FormatException("The filter cannot be empty.");

        var expression = ParseOr();
        Expect(TokenType.End);
        return expression as QueryFilter
            ?? new QueryFilter(LogicalOperator.And, expression);
    }

    private IQueryFilter ParseOr()
        => ParseGroup(ParseAnd, "||", LogicalOperator.Or);

    private IQueryFilter ParseAnd()
        => ParseGroup(ParsePrimary, "&&", LogicalOperator.And);

    private IQueryFilter ParseGroup(
        Func<IQueryFilter> operand,
        string separator,
        LogicalOperator logicalOperator)
    {
        var expression = operand();

        while (Match(separator))
        {
            var next = operand();
            if (next is QueryFilterItem filter)
                next = filter with { LogicalOperator = logicalOperator };

            expression = new QueryFilter(logicalOperator, expression, next);
        }

        return expression;
    }

    private IQueryFilter ParsePrimary()
    {
        if (Match("("))
        {
            var grouped = ParseOr();
            Expect(")");
            return grouped;
        }

        var field = Expect(TokenType.Identifier).Text;
        var operation = Next().Text.ToLowerInvariant();
        var value = ParseFilterValue();

        var filterOperator = operation switch
        {
            "==" => BinaryOperator.Equals,
            "!=" => BinaryOperator.NotEquals,
            "like" => BinaryOperator.Like,
            ">" => BinaryOperator.GreaterThan,
            "<" => BinaryOperator.LessThan,
            ">=" => BinaryOperator.GreaterThanOrEqual,
            "<=" => BinaryOperator.LessThanOrEqual,
            _ => throw new FormatException($"Unsupported filter operator [{operation}].")
        };

        return new QueryFilterItem(LogicalOperator.And, field, filterOperator, value)
        {
            Parameterized = true
        };
    }

    private object? ParseFilterValue()
        => Expect(TokenType.Value).Value;

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
        if (token.Text != text)
            throw new FormatException($"Expected [{text}], received [{token.Text}].");
    }

    private bool Match(string text)
    {
        if (_position >= _tokens.Count || _tokens[_position].Text != text)
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

            if (text[index] == '\'')
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

                tokens.Add(new Token(TokenType.Identifier, field, null));
                index++;
                continue;
            }

            if (index + 1 < text.Length &&
                new[] { "&&", "||", "==", "!=", ">=", "<=" }.Contains(text[index..(index + 2)]))
            {
                tokens.Add(new Token(TokenType.Operator, text[index..(index + 2)], null));
                index += 2;
                continue;
            }

            if ("()><+-*/".Contains(text[index]))
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
                identifier.Equals("like", StringComparison.OrdinalIgnoreCase)
                    ? TokenType.Operator
                    : int.TryParse(identifier, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ||
                      decimal.TryParse(identifier, NumberStyles.Number, CultureInfo.InvariantCulture, out _) ||
                      identifier.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                      identifier.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                      identifier.Equals("null", StringComparison.OrdinalIgnoreCase)
                        ? TokenType.Value
                    : TokenType.Identifier,
                identifier,
                identifier.Equals("like", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : ParseValue(identifier)));
        }

        tokens.Add(new Token(TokenType.End, string.Empty, null));
        return tokens;
    }

    private static void AddValueToken(
        string text,
        ref int index,
        ICollection<Token> tokens)
    {
        if (index < text.Length && text[index] == '\'')
        {
            index++;
            var value = new System.Text.StringBuilder();
            while (index < text.Length)
            {
                if (text[index] == '\'' && index + 1 < text.Length && text[index + 1] == '\'')
                {
                    value.Append('\'');
                    index += 2;
                    continue;
                }
                if (text[index] == '\'')
                    break;
                value.Append(text[index++]);
            }
            if (index >= text.Length)
                throw new FormatException("Unterminated string parameter.");

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

    private enum TokenType
    {
        Identifier,
        Operator,
        Parenthesis,
        Value,
        End
    }

    private sealed record Token(TokenType Type, string Text, object? Value);
}
