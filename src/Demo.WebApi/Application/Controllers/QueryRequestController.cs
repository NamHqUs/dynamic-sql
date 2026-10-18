using Demo.Contracts.Repositories;
using Demo.Contracts.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Namh.Data.DynamicSql;
using System.Text.Json;

namespace Demo.WebApi.Application.Controllers;

[ApiController]
[Route("query-request")]
public sealed class QueryRequestController(
    IQueryRequestService queryRequestService,
    IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> jsonOptions) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<StoredQueryRequest>> List()
        => Ok(queryRequestService.ListQueryRequests());

    [HttpPost("to-string")]
    public IActionResult GetQueryRequestString([FromBody] QueryDefinition query)
        => Content(queryRequestService.GetQueryRequestString(query), "text/plain");

    [HttpPost("parse")]
    public ActionResult<QueryDefinition> Parse([FromBody] QueryRequestTextRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text) || string.IsNullOrWhiteSpace(request.Entity))
            return BadRequest("Request text and entity are required.");

        return Ok(queryRequestService.ParseQueryRequestString(request.Text, request.Entity));
    }

    [HttpGet("{fileName}")]
    public ActionResult<StoredQueryRequest> Get(string fileName)
    {
        var request = queryRequestService.LoadQueryRequest(fileName);
        return request is null ? NotFound() : Ok(request);
    }

    [HttpPost]
    public ActionResult<StoredQueryRequest> Save([FromBody] JsonElement payload)
    {
        var (query, requestText) = ParseSavePayload(payload);
        var saved = queryRequestService.SaveQueryRequest(query, requestText: requestText);
        return Created($"/query-request/{saved.FileName}", saved);
    }

    [HttpPut("{fileName}")]
    public ActionResult<StoredQueryRequest> Update(string fileName, [FromBody] JsonElement payload)
    {
        var (query, requestText) = ParseSavePayload(payload);
        var request = queryRequestService.UpdateQueryRequest(fileName, query, requestText);
        return request is null ? NotFound() : Ok(request);
    }

    [HttpDelete("{fileName}")]
    public IActionResult Delete(string fileName)
        => queryRequestService.DeleteQueryRequest(fileName) ? NoContent() : NotFound();

    private (QueryDefinition Query, string? RequestText) ParseSavePayload(JsonElement payload)
    {
        var queryElement = payload.ValueKind == JsonValueKind.Object &&
            payload.TryGetProperty("query", out var wrappedQuery)
                ? wrappedQuery
                : payload;
        var query = JsonSerializer.Deserialize<QueryDefinition>(
            queryElement.GetRawText(),
            jsonOptions.Value.SerializerOptions)
            ?? throw new JsonException("The query request cannot be empty.");
        var requestText = payload.ValueKind == JsonValueKind.Object &&
            payload.TryGetProperty("requestText", out var textElement)
                ? textElement.ValueKind switch
                {
                    JsonValueKind.Null => null,
                    JsonValueKind.String => textElement.GetString(),
                    _ => throw new JsonException("Request text must be a string or null.")
                }
                : null;
        return (query, requestText);
    }

    /// <summary>Request payload for parsing edited query text against a fixed entity.</summary>
    public sealed record QueryRequestTextRequest(string? Text, string? Entity);
}
