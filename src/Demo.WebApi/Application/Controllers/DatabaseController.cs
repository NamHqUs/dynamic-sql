using Demo.Contracts.Services;
using Demo.Contracts.Dto;
using Microsoft.AspNetCore.Mvc;

namespace Demo.WebApi.Application.Controllers;

[ApiController]
[Route("db")]
public sealed class DatabaseController(IRawDataService rawDataService) : ControllerBase
{
    [HttpGet("tables")]
    public ActionResult<string[]> GetTables() => Ok(rawDataService.GetExposedTables());

    [HttpGet("schema")]
    public ActionResult<DatabaseSchemaDto> GetSchema() => Ok(rawDataService.GetDatabaseSchema());

    [HttpGet("data/{tableName}")]
    public ActionResult<TableDataDto> GetTableData(
        string tableName,
        [FromQuery] string? schema = null,
        [FromQuery] int? limit = null)
        => Ok(rawDataService.GetTableData(tableName, schema, limit));
}
