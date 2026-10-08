using System.Linq.Expressions;

namespace Namh.Data.DynamicSql.Internal.Query.Expressions;

abstract class SqlExpression(Type type) : Expression
{
    public override ExpressionType NodeType => (ExpressionType)1000;
    public override Type Type { get; } = type;


    protected abstract string ToSql();
    public override string ToString() => ToSql();
}
