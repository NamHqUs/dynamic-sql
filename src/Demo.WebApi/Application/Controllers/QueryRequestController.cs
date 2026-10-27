using Demo.Contracts.Dto;
using Demo.Contracts.Services;
using Microsoft.AspNetCore.Mvc;

namespace Demo.WebApi.Application.Controllers;

[ApiController]
[Route("query-request")]
public sealed class QueryRequestController(IQueryRequestService queryRequestService) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<QueryDto>> List()
        => Ok(queryRequestService.List());

    [HttpPut("{name}")]
    public IActionResult Save([FromRoute(Name = "name")] string id, [FromBody] QueryDto query)
    {
        try
        {
            queryRequestService.Save(id, query);
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
    public IActionResult Delete([FromRoute(Name = "name")] string id)
    {
        var deleted = queryRequestService.Delete(id);
        if (deleted) return NoContent();
        return queryRequestService.IsBuiltin(id)
            ? Conflict("Built-in query requests cannot be deleted.")
            : NotFound();
    }
}
