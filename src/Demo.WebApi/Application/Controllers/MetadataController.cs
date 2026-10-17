using Demo.Contracts.Dto;
using Demo.Contracts.Services;
using Microsoft.AspNetCore.Mvc;
using Namh.Data.DynamicSql;

namespace Demo.WebApi.Application.Controllers;

[ApiController]
[Route("metadata")]
public sealed class MetadataController(
    IMetadataService metadataService,
    IFunctionDefinitionService functionDefinitionService) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<MetadataEntityDto>> GetMetadata() => Ok(metadataService.GetAll());

    [HttpPost]
    public ActionResult<Entity> CreateMetadata([FromBody] Entity entity)
        => Ok(metadataService.Create(entity));

    [HttpPost("reset")]
    public ActionResult<IReadOnlyList<Entity>> Reset()
        => Ok(metadataService.Reset());

    [HttpPut("{entityName}")]
    public ActionResult<Entity> UpdateMetadata(string entityName, [FromBody] Entity entity)
        => Ok(metadataService.Update(entityName, entity));

    [HttpDelete("{entityName}")]
    public IActionResult DeleteMetadata(string entityName)
    {
        metadataService.Delete(entityName);
        return NoContent();
    }

    [HttpGet("functions")]
    public ActionResult<IReadOnlyList<FunctionDefinitionDto>> GetFunctionDefinitions()
        => Ok(functionDefinitionService.GetAll());

    [HttpPost("functions")]
    public ActionResult<FunctionDefinitionDto> CreateFunctionDefinition([FromBody] FunctionDefinitionDto definition)
        => Created($"/metadata/functions/{Uri.EscapeDataString(definition.Name)}",
            functionDefinitionService.Create(definition));

    [HttpPut("functions/{functionName}")]
    public ActionResult<FunctionDefinitionDto> UpdateFunctionDefinition(
        string functionName,
        [FromBody] FunctionDefinitionDto definition)
        => Ok(functionDefinitionService.Update(functionName, definition));

    [HttpDelete("functions/{functionName}")]
    public IActionResult DeleteFunctionDefinition(string functionName)
    {
        functionDefinitionService.Delete(functionName);
        return NoContent();
    }
}
