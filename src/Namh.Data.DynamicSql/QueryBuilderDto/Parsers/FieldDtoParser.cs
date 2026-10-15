#pragma warning disable IDE0130
using Namh.Data.DynamicSql;

namespace Namh.Data.DynamicSql.QueryBuilderDto.Parsers;

public static class FieldDtoParser
{
    public static IFieldDto Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        text = text.Trim();

        if (TryGetFunctionCall(text, out var opening))
        {
            var argumentsText = text[(opening + 1)..^1];
            var arguments = SplitTopLevel(argumentsText, ',')
                .Where(argument => !string.IsNullOrWhiteSpace(argument))
                .Select(Parse)
                .ToArray();
            return new FunctionFieldDto(text[..opening].Trim(), arguments);
        }

        if (IsWrappedBy(text, '(', ')'))
        {
            return new GroupFieldDto(ParseSequence(text[1..^1]).ToArray());
        }

        if (text[0] == '<' && text[^1] == '>')
        {
            var value = text[1..^1];
            return new ConstFieldDto(ParseConstant(value));
        }

        var sequence = ParseSequence(text);
        return sequence.Count == 1
            ? sequence[0]
            : new GroupFieldDto(sequence.ToArray());
    }

    private static bool TryGetFunctionCall(string text, out int opening)
    {
        var openingParenthesis = text.IndexOf('(');
        var openingBracket = text.IndexOf('[');

        if (openingParenthesis > 0 &&
            text[..openingParenthesis].All(character => !char.IsWhiteSpace(character)) &&
            IsWrappedBy(text[openingParenthesis..], '(', ')'))
        {
            opening = openingParenthesis;
            return true;
        }

        if (openingBracket > 0 && IsWrappedBy(text[openingBracket..], '[', ']'))
        {
            opening = openingBracket;
            return true;
        }

        opening = -1;
        return false;
    }

    private static object? ParseConstant(string value)
    {
        if (value.Length == 0 || value.Equals("null", StringComparison.OrdinalIgnoreCase))
            return null;
        if (int.TryParse(
            value,
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out var integer))
            return integer;
        if (decimal.TryParse(
            value,
            System.Globalization.NumberStyles.Number | System.Globalization.NumberStyles.AllowExponent,
            System.Globalization.CultureInfo.InvariantCulture,
            out var number))
            return number;
        return bool.TryParse(value, out var boolean) ? boolean : value;
    }

    private static List<IFieldDto> ParseSequence(string text)
    {
        var parts = SplitBinaryOperators(text);
        var fields = new List<IFieldDto> { ParseAtom(parts[0]) };
        for (var i = 1; i < parts.Count; i += 2)
        {
            fields.Add(new BinaryFieldDto(parts[i], ParseAtom(parts[i + 1])));
        }
        return fields;
    }

    private static IFieldDto ParseAtom(string text)
    {
        text = text.Trim();
        if (text.Length == 0)
        {
            throw new FormatException("A field expression cannot contain an empty operand.");
        }

        if (IsWrappedBy(text, '(', ')') ||
            (text[0] == '<' && text[^1] == '>') ||
            TryGetFunctionCall(text, out _))
        {
            return Parse(text);
        }

        return new FieldDto(text);
    }

    private static List<string> SplitBinaryOperators(string text)
    {
        var parts = new List<string>();
        var start = 0;
        var parentheses = 0;
        var brackets = 0;
        var constraints = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (parentheses == 0 && brackets == 0 && constraints == 0 &&
                IsBinaryOperator(text, i, out var operatorLength))
            {
                parts.Add(text[start..i].Trim());
                parts.Add(text.Substring(i, operatorLength));
                i += operatorLength - 1;
                start = i + 1;
                continue;
            }

            switch (text[i])
            {
                case '(':
                    parentheses++;
                    break;
                case ')':
                    parentheses--;
                    break;
                case '[':
                    brackets++;
                    break;
                case ']':
                    brackets--;
                    break;
                case '<' when parentheses == 0 && brackets == 0:
                    constraints++;
                    break;
                case '>' when parentheses == 0 && brackets == 0 && constraints > 0:
                    constraints--;
                    break;
            }
        }

        parts.Add(text[start..].Trim());
        if (parts.Count > 1 && (parts.Count % 2 == 0 || parts.Any(string.IsNullOrWhiteSpace)))
        {
            throw new FormatException("The field expression contains an invalid binary operator.");
        }
        return parts;
    }

    private static bool IsBinaryOperator(string text, int index, out int operatorLength)
    {
        operatorLength = 1;
        if (index == 0)
        {
            return false;
        }

        if (text[index] is '+' or '-' or '*' or '/' or '%')
        {
            operatorLength = 1;
        }
        else if (text[index] is '=' or '>' or '<')
        {
            if (index + 1 < text.Length && text[index + 1] == '=')
            {
                operatorLength = 2;
            }
            else if (text[index] == '<' && index + 1 < text.Length && text[index + 1] == '>')
            {
                operatorLength = 2;
            }
        }
        else
        {
            return false;
        }

        return index + operatorLength < text.Length &&
            char.IsWhiteSpace(text[index - 1]) &&
            char.IsWhiteSpace(text[index + operatorLength]);
    }

    internal static List<string> SplitTopLevel(string text, char delimiter)
    {
        var parts = new List<string>();
        var start = 0;
        var parentheses = 0;
        var brackets = 0;
        var constraints = 0;

        for (var i = 0; i < text.Length; i++)
        {
            switch (text[i])
            {
                case '(':
                    parentheses++;
                    break;
                case ')':
                    parentheses--;
                    break;
                case '[':
                    brackets++;
                    break;
                case ']':
                    brackets--;
                    break;
                case '<':
                    constraints++;
                    break;
                case '>':
                    if (constraints > 0)
                    {
                        constraints--;
                    }
                    break;
            }

            if (text[i] == delimiter && parentheses == 0 && brackets == 0 && constraints == 0)
            {
                parts.Add(text[start..i].Trim());
                start = i + 1;
            }
        }

        parts.Add(text[start..].Trim());
        return parts;
    }

    private static bool IsWrappedBy(string text, char opening, char closing)
    {
        if (text.Length < 2 || text[0] != opening || text[^1] != closing)
        {
            return false;
        }

        var depth = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == opening)
            {
                depth++;
            }
            else if (text[i] == closing && --depth == 0)
            {
                return i == text.Length - 1;
            }
        }
        return false;
    }
}
