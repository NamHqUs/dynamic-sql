using System.Text;
using Demo.ServiceContracts.Services;
using Microsoft.AspNetCore.Mvc;
using Namh.Data.DynamicSql;

namespace Demo.WebApi.Application.Controllers;

[ApiController]
[Route("dynamic-query/query")]
public sealed class DynamicQueryController(
    IDynamicQueryService dynamicQueryService,
    IQueryRequestService queryRequestService) : ControllerBase
{
    [HttpPost("to-string")]
    public IActionResult GetSqlQueryString([FromBody] QueryDefinition query)
        => Content(dynamicQueryService.GetSqlQueryString(query), "text/plain");

    [HttpPost]
    public async Task<IActionResult> ExecuteQuery(
        [FromBody] QueryDefinition query,
        CancellationToken cancellationToken)
        => Ok(await dynamicQueryService.ExecuteQueryAsync(query, cancellationToken));

    [HttpPost("to-string-from-text")]
    public async Task<IActionResult> GetSqlQueryStringFromText(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var text = await reader.ReadToEndAsync(cancellationToken);
        var query = queryRequestService.ParseQueryRequestString(text);
        return Content(dynamicQueryService.GetSqlQueryString(query), "text/plain");
    }

    [HttpPost("from-text")]
    public async Task<IActionResult> ExecuteQueryFromText(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var text = await reader.ReadToEndAsync(cancellationToken);
        var query = queryRequestService.ParseQueryRequestString(text);
        return Ok(await dynamicQueryService.ExecuteQueryAsync(query, cancellationToken));
    }
}
