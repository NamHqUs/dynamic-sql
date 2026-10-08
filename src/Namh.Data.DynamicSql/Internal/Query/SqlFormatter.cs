using System.Text;

namespace Namh.Data.DynamicSql.Internal.Query;

internal static class SqlFormatter
{
    private static readonly string[] LineBreakKeywords =
    [
        "SELECT",
        "FROM",
        "WHERE",
        "AND",
        "OR",
        "GROUP BY",
        "ORDER BY",
        "HAVING",
        "UNION"
    ];

    public static string Format(string sql)
    {
        var result = new StringBuilder();
        var token = new StringBuilder();
        var indent = 0;
        var inSingleQuote = false;
        var inDoubleQuote = false;
        var inBracketQuote = false;
        var lineHasContent = false;

        void NewLine()
        {
            while (result.Length > 0 && result[^1] == ' ')
                result.Length--;

            if (lineHasContent)
                result.AppendLine();

            result.Append(' ', indent * 4);
            lineHasContent = false;
        }

        void WriteToken(string value)
        {
            if (value.Length == 0)
                return;

            var keyword = value.ToUpperInvariant();
            if (LineBreakKeywords.Contains(keyword))
                NewLine();
            else if (lineHasContent && result[^1] != ' ' && result[^1] != '(')
                result.Append(' ');

            result.Append(value);
            lineHasContent = true;
        }

        void FlushToken()
        {
            WriteToken(token.ToString());
            token.Clear();
        }

        for (var index = 0; index < sql.Length; index++)
        {
            var character = sql[index];

            if (inSingleQuote)
            {
                result.Append(character);
                if (character == '\'' && (index + 1 >= sql.Length || sql[index + 1] != '\''))
                    inSingleQuote = false;
                else if (character == '\'' && index + 1 < sql.Length)
                    result.Append(sql[++index]);
                lineHasContent = true;
                continue;
            }

            if (inDoubleQuote)
            {
                result.Append(character);
                if (character == '"' && (index + 1 >= sql.Length || sql[index + 1] != '"'))
                    inDoubleQuote = false;
                else if (character == '"' && index + 1 < sql.Length)
                    result.Append(sql[++index]);
                lineHasContent = true;
                continue;
            }

            if (inBracketQuote)
            {
                result.Append(character);
                if (character == ']')
                    inBracketQuote = false;
                lineHasContent = true;
                continue;
            }

            if (character == '\'')
            {
                FlushToken();
                if (lineHasContent && result[^1] != ' ' && result[^1] != '(')
                    result.Append(' ');
                result.Append(character);
                inSingleQuote = true;
                lineHasContent = true;
            }
            else if (character == '"')
            {
                FlushToken();
                if (lineHasContent && result[^1] != ' ' && result[^1] != '.')
                    result.Append(' ');
                result.Append(character);
                inDoubleQuote = true;
                lineHasContent = true;
            }
            else if (character == '[')
            {
                FlushToken();
                if (lineHasContent && result[^1] != ' ' && result[^1] != '.')
                    result.Append(' ');
                result.Append(character);
                inBracketQuote = true;
                lineHasContent = true;
            }
            else if (char.IsWhiteSpace(character))
            {
                FlushToken();
            }
            else if (character == '(')
            {
                FlushToken();
                if (lineHasContent && result[^1] != ' ')
                    result.Append(' ');
                result.Append('(');
                lineHasContent = true;
                indent++;
            }
            else if (character == ')')
            {
                FlushToken();
                indent = Math.Max(0, indent - 1);
                result.Append(')');
                lineHasContent = true;
            }
            else if (character == ',')
            {
                FlushToken();
                result.Append(',');
                result.Append(' ');
                lineHasContent = true;
            }
            else
            {
                token.Append(character);
            }
        }

        FlushToken();
        return result.ToString().Trim();
    }
}
