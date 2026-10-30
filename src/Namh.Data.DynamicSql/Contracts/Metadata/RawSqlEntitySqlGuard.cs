using System.Text.RegularExpressions;

namespace Namh.Data.DynamicSql;

internal enum RawSqlEntityDialect
{
    SqlServer,
    Sqlite,
    Postgres
}

internal static class RawSqlEntitySqlGuard
{
    private static readonly IReadOnlyDictionary<RawSqlEntityDialect, Regex> ForbiddenKeywords =
        new Dictionary<RawSqlEntityDialect, Regex>
        {
            [RawSqlEntityDialect.SqlServer] = CreateKeywordRegex(
                "ALTER", "BACKUP", "BULK", "CREATE", "DBCC", "DELETE", "DENY", "DROP",
                "EXEC", "EXECUTE", "GRANT", "INSERT", "INTO", "MERGE", "RESTORE",
                "REVOKE", "TRUNCATE", "UPDATE"),
            [RawSqlEntityDialect.Sqlite] = CreateKeywordRegex(
                "ALTER", "ANALYZE", "ATTACH", "CREATE", "DELETE", "DETACH", "DROP",
                "INSERT", "INTO", "PRAGMA", "REINDEX", "REPLACE", "TRUNCATE", "UPDATE",
                "VACUUM"),
            [RawSqlEntityDialect.Postgres] = CreateKeywordRegex(
                "ALTER", "CALL", "COPY", "CREATE", "DELETE", "DO", "DROP", "GRANT",
                "INSERT", "INTO", "MERGE", "REFRESH", "REVOKE", "TRUNCATE", "UPDATE",
                "VACUUM")
        };

    public static string ValidateAndReturn(string rawSql, RawSqlEntityDialect dialect)
    {
        var maskedSql = MaskLiteralsIdentifiersAndComments(rawSql, dialect);
        var match = ForbiddenKeywords[dialect].Match(maskedSql);
        if (match.Success)
        {
            throw new ArgumentException(
                $"Raw SQL entity source contains the disallowed keyword '{match.Value}'.",
                nameof(rawSql));
        }

        return rawSql;
    }

    private static Regex CreateKeywordRegex(params string[] keywords)
    {
        var alternatives = string.Join("|", keywords);
        return new Regex(
            $@"(?<![\p{{L}}\p{{N}}_$@#])(?:{alternatives})(?![\p{{L}}\p{{N}}_$@#])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }

    private static string MaskLiteralsIdentifiersAndComments(
        string sql,
        RawSqlEntityDialect dialect)
    {
        var masked = sql.ToCharArray();
        var supportsBracketIdentifiers = dialect is RawSqlEntityDialect.SqlServer or RawSqlEntityDialect.Sqlite;
        var supportsBacktickIdentifiers = dialect == RawSqlEntityDialect.Sqlite;
        var supportsNestedBlockComments = dialect is RawSqlEntityDialect.SqlServer or RawSqlEntityDialect.Postgres;

        for (var index = 0; index < sql.Length;)
        {
            if (sql[index] == '-' && HasNext(sql, index, '-'))
            {
                var end = index + 2;
                while (end < sql.Length && sql[end] is not '\r' and not '\n')
                    end++;

                MaskRange(masked, index, end);
                index = end;
                continue;
            }

            if (sql[index] == '/' && HasNext(sql, index, '*'))
            {
                index = MaskBlockComment(sql, masked, index, supportsNestedBlockComments);
                continue;
            }

            if (dialect == RawSqlEntityDialect.Postgres &&
                sql[index] == '$' &&
                TryGetDollarQuoteDelimiter(sql, index, out var delimiter, out var contentStart))
            {
                var closingStart = sql.IndexOf(delimiter, contentStart, StringComparison.Ordinal);
                if (closingStart < 0)
                    throw UnterminatedRawSqlPart("dollar-quoted string");

                var end = closingStart + delimiter.Length;
                MaskRange(masked, index, end);
                index = end;
                continue;
            }

            if (sql[index] == '\'')
            {
                var backslashEscapes = dialect == RawSqlEntityDialect.Postgres &&
                    IsPostgresEscapeString(sql, index);
                index = MaskDelimited(sql, masked, index, '\'', backslashEscapes);
                continue;
            }

            if (sql[index] == '"')
            {
                index = MaskDelimited(sql, masked, index, '"', backslashEscapes: false);
                continue;
            }

            if (supportsBracketIdentifiers && sql[index] == '[')
            {
                index = MaskDelimited(
                    sql,
                    masked,
                    index,
                    ']',
                    backslashEscapes: false,
                    doubledClosingEscape: dialect == RawSqlEntityDialect.SqlServer);
                continue;
            }

            if (supportsBacktickIdentifiers && sql[index] == '`')
            {
                index = MaskDelimited(sql, masked, index, '`', backslashEscapes: false);
                continue;
            }

            index++;
        }

        return new string(masked);
    }

    private static int MaskBlockComment(
        string sql,
        char[] masked,
        int start,
        bool supportsNestedComments)
    {
        var depth = 1;
        var index = start + 2;

        while (index < sql.Length)
        {
            if (supportsNestedComments && sql[index] == '/' && HasNext(sql, index, '*'))
            {
                depth++;
                index += 2;
                continue;
            }

            if (sql[index] == '*' && HasNext(sql, index, '/'))
            {
                depth--;
                index += 2;
                if (depth == 0)
                {
                    MaskRange(masked, start, index);
                    return index;
                }

                continue;
            }

            index++;
        }

        throw UnterminatedRawSqlPart("block comment");
    }

    private static int MaskDelimited(
        string sql,
        char[] masked,
        int start,
        char closingCharacter,
        bool backslashEscapes,
        bool doubledClosingEscape = true)
    {
        var index = start + 1;

        while (index < sql.Length)
        {
            if (backslashEscapes && sql[index] == '\\')
            {
                index = Math.Min(index + 2, sql.Length);
                continue;
            }

            if (sql[index] == closingCharacter)
            {
                if (doubledClosingEscape && HasNext(sql, index, closingCharacter))
                {
                    index += 2;
                    continue;
                }

                index++;
                MaskRange(masked, start, index);
                return index;
            }

            index++;
        }

        throw UnterminatedRawSqlPart("quoted value or identifier");
    }

    private static bool TryGetDollarQuoteDelimiter(
        string sql,
        int start,
        out string delimiter,
        out int contentStart)
    {
        var index = start + 1;
        if (index < sql.Length && sql[index] == '$')
        {
            delimiter = "$$";
            contentStart = index + 1;
            return true;
        }

        if (index >= sql.Length || !(IsAsciiLetter(sql[index]) || sql[index] == '_'))
        {
            delimiter = string.Empty;
            contentStart = start;
            return false;
        }

        index++;
        while (index < sql.Length &&
            (IsAsciiLetter(sql[index]) || char.IsDigit(sql[index]) || sql[index] == '_'))
        {
            index++;
        }

        if (index >= sql.Length || sql[index] != '$')
        {
            delimiter = string.Empty;
            contentStart = start;
            return false;
        }

        delimiter = sql[start..(index + 1)];
        contentStart = index + 1;
        return true;
    }

    private static bool IsPostgresEscapeString(string sql, int quoteIndex)
        => quoteIndex > 0 &&
            sql[quoteIndex - 1] is 'e' or 'E' &&
            (quoteIndex == 1 || !IsIdentifierCharacter(sql[quoteIndex - 2]));

    private static bool IsIdentifierCharacter(char value)
        => char.IsLetterOrDigit(value) || value is '_' or '$';

    private static bool IsAsciiLetter(char value)
        => value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static bool HasNext(string text, int index, char expected)
        => index + 1 < text.Length && text[index + 1] == expected;

    private static void MaskRange(char[] text, int start, int end)
    {
        for (var index = start; index < end; index++)
        {
            if (text[index] is not '\r' and not '\n')
                text[index] = ' ';
        }
    }

    private static ArgumentException UnterminatedRawSqlPart(string part)
        => new($"Raw SQL entity source contains an unterminated {part}.", "rawSql");
}
