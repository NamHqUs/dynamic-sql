using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Namh.Data.DynamicSql.Test.UnitTest;

[TestFixture]
public class _50_QueryContext
{
    public void Sample()
    {
        var q1 =new QueryDefinition
        {
            Entity = "User",
            Projection = [
                new ("Id"),
                new ("Name"),
                new ("Email")
            ],
        };

        var q2 = new QueryDefinition
        {
            Entity = "User",
            Projection = [
                new ("Id"),
                new ("Name"),
                new ("Email")
            ],
        };

    }
}
