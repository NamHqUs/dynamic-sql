using Microsoft.ApplicationInsights.Extensibility.Implementation;
using NUnit.Framework.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;

namespace Namh.Data.DynamicSql.Test.UnitTest;

[TestFixture]
public class _50_QueryContextDefintion : TestBase
{
    [Test]
    public void Sample()
    {
        /// WITH UserContext AS(
        ///     SELECT[Id], [Name] as "Alias Name" from[User]
        /// )
        /// SELECT t0.[Id], t0.[Name], t1.[Alias Name]
        /// FROM [User] t0
        ///     LEFT JOIN UserContext t1 On t0.[ManagerId] = t1.[Id]
        
        var query = new QueryDefinition
        {
            Entity = "User",
            Contexts = [
                new QueryContextDefintion(
                    Name: "UserContext", 
                    Query: new QueryDefinition
                        {
                            Entity = "User",
                            Projection = [
                                new ("Id"),
                                new ("Name", "Alias Name"),
                            ],
                        },
                    Condition: new ConditionStatementDefinition(
                        new FieldDefinition("ManagerId"), 
                        BinaryOperator.Equals, 
                        new FieldDefinition("Id")))
            ],
            Projection = [
                new ("Id"),
                new ("Name", "User Name"),
                new ("UserContext.Alias Name", "Manager Name")
            ],
        };

        var cmd = _db.Query(query).ToSql();

        Assert.That(cmd.CommandText, Contains.Value("WITH UserContext AS("));
        Assert.That(cmd.CommandText, Contains.Value("LEFT JOIN UserContext t1 On t0.[ManagerId] = t1.[Id])"));
    }
}
