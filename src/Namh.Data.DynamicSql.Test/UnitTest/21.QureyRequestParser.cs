namespace Namh.Data.DynamicSql.Test.UnitTest;

[TestFixture]
internal class _21_QureyRequestParser : TestBase
{
    [Test]
    public void TestCases()
    {
        QueryRequest request = new()
        {
            Name = "5. Gross margin by product",
            Entity = "Products",
            Columns =
            [
                new FieldTranslator("Name"),
                new FieldTranslator(
                    null,
                    "UnitsSold",
                    new FieldExpressionTransaltion
                    {
                        Kind = "function",
                        Function = "SUM",
                        Arguments =
                        [
                            new FieldExpressionTransaltion
                            {
                                Kind = "field",
                                Field = "InvoiceItems.Quantity"
                            }
                        ]
                    }),
                new FieldTranslator(
                    null,
                    "GrossProfit",
                    new FieldExpressionTransaltion
                    {
                        Kind = "function",
                        Function = "SUM",
                        Arguments =
                        [
                            new FieldExpressionTransaltion
                            {
                                Kind = "binary",
                                Operator = "-",
                                Arguments =
                                [
                                    new FieldExpressionTransaltion
                                    {
                                        Kind = "field",
                                        Field = "InvoiceItems.LineTotal"
                                    },
                                    new FieldExpressionTransaltion
                                    {
                                        Kind = "binary",
                                        Operator = "*",
                                        Arguments =
                                        [
                                            new FieldExpressionTransaltion
                                            {
                                                Kind = "field",
                                                Field = "InvoiceItems.Quantity"
                                            },
                                            new FieldExpressionTransaltion
                                            {
                                                Kind = "field",
                                                Field = "CostPrice"
                                            }
                                        ]
                                    }
                                ]
                            }
                        ]
                    }),
                new FieldTranslator(
                    null,
                    "MarginPercent",
                    new FieldExpressionTransaltion
                    {
                        Kind = "function",
                        Function = "ROUND",
                        Arguments =
                        [
                            new FieldExpressionTransaltion
                            {
                                Kind = "binary",
                                Operator = "/",
                                Arguments =
                                [
                                    new FieldExpressionTransaltion
                                    {
                                        Kind = "binary",
                                        Operator = "*",
                                        Arguments =
                                        [
                                            new FieldExpressionTransaltion
                                            {
                                                Kind = "function",
                                                Function = "SUM",
                                                Arguments =
                                                [
                                                    new FieldExpressionTransaltion
                                                    {
                                                        Kind = "binary",
                                                        Operator = "-",
                                                        Arguments =
                                                        [
                                                            new FieldExpressionTransaltion
                                                            {
                                                                Kind = "field",
                                                                Field = "InvoiceItems.LineTotal"
                                                            },
                                                            new FieldExpressionTransaltion
                                                            {
                                                                Kind = "binary",
                                                                Operator = "*",
                                                                Arguments =
                                                                [
                                                                    new FieldExpressionTransaltion
                                                                    {
                                                                        Kind = "field",
                                                                        Field = "InvoiceItems.Quantity"
                                                                    },
                                                                    new FieldExpressionTransaltion
                                                                    {
                                                                        Kind = "field",
                                                                        Field = "CostPrice"
                                                                    }
                                                                ]
                                                            }
                                                        ]
                                                    }
                                                ]
                                            },
                                            new FieldExpressionTransaltion
                                            {
                                                Kind = "value",
                                                Value = 100.0m
                                            }
                                        ]
                                    },
                                    new FieldExpressionTransaltion
                                    {
                                        Kind = "function",
                                        Function = "NULLIF",
                                        Arguments =
                                        [
                                            new FieldExpressionTransaltion
                                            {
                                                Kind = "function",
                                                Function = "SUM",
                                                Arguments =
                                                [
                                                    new FieldExpressionTransaltion
                                                    {
                                                        Kind = "field",
                                                        Field = "InvoiceItems.LineTotal"
                                                    }
                                                ]
                                            },
                                            new FieldExpressionTransaltion
                                            {
                                                Kind = "value",
                                                Value = 0
                                            }
                                        ]
                                    }
                                ]
                            },
                            new FieldExpressionTransaltion
                            {
                                Kind = "value",
                                Value = 2
                            }
                        ]
                    })
            ],
            GroupBy = ["Id", "Name"],
            Filter = new QueryFilter(
                LogicalOperator.And,
                new QueryFilterItem(
                    LogicalOperator.And,
                    "InvoiceItems.Invoice.Status",
                    BinaryOperator.Equals,
                    "Paid")),
            Sorts = [new QuerySort("GrossProfit", QuerySortDirection.Descending)],
            Page = 1,
            PageSize = 50,
            IncludeArchive = false
        };
        var text = request.ToString();

    }
}
