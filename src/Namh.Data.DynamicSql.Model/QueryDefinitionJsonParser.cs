using System.Globalization;
using System.Text.Json;

namespace Namh.Data.DynamicSql;

internal static class QueryDefinitionJsonParser
{
    public static QueryDefinition Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        return new QueryDefinition
        {
            Entity = root.GetProperty("entity").GetString()
                ?? throw new JsonException("The entity value cannot be null."),
            Columns = root.TryGetProperty("columns", out var columns)
                ? columns.EnumerateArray().Select(ParseColumn).ToList()
                : [],
            Filter = root.TryGetProperty("filter", out var filter)
                ? new QueryFilterParser(filter.GetString() ?? string.Empty).Parse()
                : null,
            Sorts = root.TryGetProperty("sorts", out var sorts)
                ? sorts.EnumerateArray().Select(ParseSort).ToList()
                : [],
            Page = ReadInt(root, "page"),
            PageSize = ReadInt(root, "pageSize")
        };
    }

    private static QueryColumn ParseColumn(JsonElement element)
    {
        var value = element.GetString()
            ?? throw new JsonException("A column must be a string.");
        var separator = value.IndexOf('|');

        return separator < 0
            ? new QueryColumn(value)
            : new QueryColumn(value[..separator], value[(separator + 1)..]);
    }

    private static QuerySort ParseSort(JsonElement element)
    {
        var value = element.GetString()
            ?? throw new JsonException("A sort must be a string.");
        var descending = value.StartsWith("-", StringComparison.Ordinal);

        return new QuerySort(
            descending ? value[1..] : value,
            descending ? QuerySortDirection.Descending : QuerySortDirection.Ascending);
    }

    private static int? ReadInt(JsonElement root, string name)
        => root.TryGetProperty(name, out var element) ? element.GetInt32() : null;
}

internal sealed class QueryFilterParser
{
    private readonly List<Token> _tokens;
    private int _position;

    public QueryFilterParser(string text)
        => _tokens = Tokenize(text);

    public QueryFilterExpression Parse()
    {
        if (_tokens.Count == 1)
            throw new FormatException("The filter cannot be empty.");

        var expression = ParseOr();
        Expect(TokenType.End);
        return expression;
    }

    private QueryFilterExpression ParseOr()
        => ParseGroup(ParseAnd, "||", QueryFilterLogicalOperator.Or);

    private QueryFilterExpression ParseAnd()
        => ParseGroup(ParsePrimary, "&&", QueryFilterLogicalOperator.And);

    private QueryFilterExpression ParseGroup(
        Func<QueryFilterExpression> operand,
        string separator,
        QueryFilterLogicalOperator logicalOperator)
    {
        var expressions = new List<QueryFilterExpression> { operand() };

        while (Match(separator))
            expressions.Add(operand());

        return expressions.Count == 1
            ? expressions[0]
            : new QueryFilterGroup(logicalOperator, expressions);
    }

    private QueryFilterExpression ParsePrimary()
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
            "==" => QueryFilterOperator.Equals,
            "!=" => QueryFilterOperator.NotEquals,
            "like" => QueryFilterOperator.Like,
            ">" => QueryFilterOperator.GreaterThan,
            "<" => QueryFilterOperator.LessThan,
            ">=" => QueryFilterOperator.GreaterThanOrEqual,
            "<=" => QueryFilterOperator.LessThanOrEqual,
            _ => throw new FormatException($"Unsupported filter operator [{operation}].")
        };

        return new QueryFilterCondition(new QueryFilter(field, filterOperator, value, true));
    }

    private object? ParseFilterValue()
    {
        if (!Match("("))
        {
            return Expect(TokenType.Value).Value;
        }

        var left = Expect(TokenType.Value).Value;
        var operation = Next().Text;
        var field = Expect(TokenType.Identifier).Text;
        Expect(")");
        return new QueryFilterArithmetic(left, operation, field);
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
            var start = ++index;
            while (index < text.Length && text[index] != '\'')
                index++;
            if (index >= text.Length)
                throw new FormatException("Unterminated string parameter.");

            var value = text[start..index];
            tokens.Add(new Token(TokenType.Value, value, value));
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
