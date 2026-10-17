using Demo.ServiceContracts.Dto;
using Demo.ServiceContracts.Services;
using Microsoft.AspNetCore.Mvc;

namespace Demo.WebApi.Application.Controllers;

[ApiController]
[Route("db")]
public sealed class DatabaseController(IRawDataService rawDataService) : ControllerBase
{
    [HttpGet("tables")]
    public ActionResult<string[]> GetTables() => Ok(rawDataService.GetExposedTables());

    [HttpGet("data/{tableName}")]
    public ActionResult<TableDataDto> GetTableData(string tableName)
        => Ok(rawDataService.GetTableData(tableName));
}
