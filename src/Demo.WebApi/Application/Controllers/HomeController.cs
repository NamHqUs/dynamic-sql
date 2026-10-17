using Microsoft.AspNetCore.Mvc;

namespace Demo.WebApi.Application.Controllers;

[ApiController]
public sealed class HomeController : ControllerBase
{
    [HttpGet("/")]
    public IActionResult Get() => Content("Dynamic SQL Demo. /swagger to open Swagger UI");
}
