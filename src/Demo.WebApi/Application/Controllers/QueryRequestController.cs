using Demo.Contracts.Dto;
using Demo.Contracts.Services;
using Microsoft.AspNetCore.Mvc;

namespace Demo.WebApi.Application.Controllers;

[ApiController]
[Route("query-request")]
public sealed class QueryRequestController(IQueryRequestService queryRequestService) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<QueryRequestDto>> List()
        => Ok(queryRequestService.List());

    [HttpPut("{name}")]
    public IActionResult Save(string name, [FromBody] QueryRequestDto queryRequest)
    {
        try
        {
            queryRequestService.Save(name, queryRequest);
            return NoContent();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
    }

    [HttpDelete("{name}")]
    public IActionResult Delete(string name)
    {
        var deleted = queryRequestService.Delete(name);
        if (deleted) return NoContent();
        return queryRequestService.IsBuiltin(name)
            ? Conflict("Built-in query requests cannot be deleted.")
            : NotFound();
    }
}
