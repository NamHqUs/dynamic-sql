import "./sql-statement.css";

type SqlStatementProps = {
  statement: string;
};

const formatSql = (statement: string) => {
  const literals: string[] = [];
  const protectedStatement = statement
    .replace(/'(?:''|[^'])*'/g, (literal) => {
      literals.push(literal);
      return `__SQL_LITERAL_${literals.length - 1}__`;
    })
    .replace(/\s+/g, " ")
    .replace(/\s+(FROM|WHERE|GROUP BY|HAVING|ORDER BY|LIMIT|OFFSET)\s+/gi, "\n$1 ")
    .replace(/\s+(AND|OR)\s+/gi, "\n  $1 ")
    .replace(/,\s*/g, ",\n  ")
    .trim();

  return protectedStatement.replace(/__SQL_LITERAL_(\d+)__/g, (_, index: string) => literals[Number(index)] ?? "");
};

const sqlKeywordPattern = /\b(SELECT|FROM|WHERE|GROUP|BY|HAVING|ORDER|LIMIT|OFFSET|ASC|DESC|AS|AND|OR|IN|IS|NULL|LIKE|JOIN|LEFT|RIGHT|INNER|OUTER|ON|DISTINCT|TOP)\b/i;
const sqlTokenPattern = /(\b(?:SELECT|FROM|WHERE|GROUP|BY|HAVING|ORDER|LIMIT|OFFSET|ASC|DESC|AS|AND|OR|IN|IS|NULL|LIKE|JOIN|LEFT|RIGHT|INNER|OUTER|ON|DISTINCT|TOP)\b)/gi;

export default function SqlStatement({ statement }: SqlStatementProps) {
  const formattedStatement = formatSql(statement);
  return (
    <pre className="sql-statement">
      <code>
        {formattedStatement.split(sqlTokenPattern).map((token, index) => sqlKeywordPattern.test(token)
          ? <span className="sql-keyword" key={`${token}-${index}`}>{token}</span>
          : token)}
      </code>
    </pre>
  );
}
