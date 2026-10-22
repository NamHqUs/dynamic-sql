#pragma warning disable IDE0130
using Namh.Data.DynamicSql;

namespace Namh.Data.DynamicSql.QueryBuilderDto.Parsers;

internal static class FieldDefinitionParser
{
    public static IFieldDefinition Parse(string text)
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
            return new FunctionFieldDefinition(text[..opening].Trim(), arguments);
        }

        if (IsWrappedBy(text, '(', ')'))
        {
            return ParseSequence(text[1..^1]);
        }

        if (text[0] == '<' && text[^1] == '>')
        {
            var value = text[1..^1];
            return new ConstFieldDefinition(ParseConstant(value));
        }

        return ParseSequence(text);
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

    private static IFieldDefinition ParseSequence(string text)
    {
        var parts = SplitBinaryOperators(text);
        if (parts.Count == 1)
            return ParseAtom(parts[0]);

        var additiveOperators = new List<int>();
        for (var index = 1; index < parts.Count; index += 2)
        {
            var operation = MatchOperationExtensions.ParseArithmetic(parts[index]);
            if (operation is MatchOperation.Add or MatchOperation.Subtract)
                additiveOperators.Add(index);
        }

        if (additiveOperators.Count == 0)
            return ParseMultiplicativeSequence(parts, 0, parts.Count);

        var fields = new List<IFieldDefinition>
        {
            ParseMultiplicativeSequence(parts, 0, additiveOperators[0])
        };
        for (var index = 0; index < additiveOperators.Count; index++)
        {
            var operatorIndex = additiveOperators[index];
            var termEnd = index + 1 < additiveOperators.Count
                ? additiveOperators[index + 1]
                : parts.Count;
            var operation = MatchOperationExtensions.ParseArithmetic(parts[operatorIndex]);
            var operand = ParseMultiplicativeSequence(parts, operatorIndex + 1, termEnd);
            fields.Add(operand is GroupFieldDefintion group
                ? group with { Oper = operation }
                : new BinaryFieldDefinition(operation, operand));
        }

        return new GroupFieldDefintion(MatchOperation.Add, fields.ToArray());
    }

    private static IFieldDefinition ParseMultiplicativeSequence(
        IReadOnlyList<string> parts,
        int start,
        int end)
    {
        var fields = new List<IFieldDefinition> { ParseAtom(parts[start]) };
        for (var index = start + 1; index < end; index += 2)
        {
            var operation = MatchOperationExtensions.ParseArithmetic(parts[index]);
            var operand = ParseAtom(parts[index + 1]);
            fields.Add(operand is GroupFieldDefintion group
                ? group with { Oper = operation }
                : new BinaryFieldDefinition(operation, operand));
        }

        return fields.Count == 1
            ? fields[0]
            : new GroupFieldDefintion(MatchOperation.Add, fields.ToArray());
    }

    private static IFieldDefinition ParseAtom(string text)
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

        return new FieldDefinition(text);
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
