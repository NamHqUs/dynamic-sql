using System.Diagnostics;
using System.Linq.Expressions;

namespace Namh.Data.DynamicSql.Internal.Query.Expressions;

[DebuggerDisplay("{DebugView,nq}")]
abstract class SqlExpression(Type type) : Expression
{
    public override ExpressionType NodeType => (ExpressionType)1000;
    public override Type Type { get; } = type;

    internal abstract string RenderSql();

    internal virtual string DebugView
        => $"{GetType().Name} ({Type.Name})";
}
