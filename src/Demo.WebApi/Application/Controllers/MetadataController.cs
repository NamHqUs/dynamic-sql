using Demo.ServiceContracts.Dto;
using Demo.ServiceContracts.Services;
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
    public ActionResult<IReadOnlyList<Entity>> GetMetadata() => Ok(metadataService.GetMetadata());

    [HttpPost]
    public ActionResult<Entity> CreateMetadata([FromBody] Entity entity)
        => Ok(metadataService.CreateMetadata(entity));

    [HttpPost("reset")]
    public ActionResult<IReadOnlyList<Entity>> Reset()
        => Ok(metadataService.Reset());

    [HttpPut("{entityName}")]
    public ActionResult<Entity> UpdateMetadata(string entityName, [FromBody] Entity entity)
        => Ok(metadataService.UpdateMetadata(entityName, entity));

    [HttpDelete("{entityName}")]
    public IActionResult DeleteMetadata(string entityName)
    {
        metadataService.DeleteMetadata(entityName);
        return NoContent();
    }

    [HttpGet("functions")]
    public ActionResult<IReadOnlyList<FunctionDefinitionDto>> GetFunctionDefinitions()
        => Ok(functionDefinitionService.GetFunctionDefinitions());

    [HttpPost("functions")]
    public ActionResult<FunctionDefinitionDto> CreateFunctionDefinition([FromBody] FunctionDefinitionDto definition)
        => Created($"/metadata/functions/{Uri.EscapeDataString(definition.Name)}",
            functionDefinitionService.CreateFunctionDefinition(definition));

    [HttpPut("functions/{functionName}")]
    public ActionResult<FunctionDefinitionDto> UpdateFunctionDefinition(
        string functionName,
        [FromBody] FunctionDefinitionDto definition)
        => Ok(functionDefinitionService.UpdateFunctionDefinition(functionName, definition));

    [HttpDelete("functions/{functionName}")]
    public IActionResult DeleteFunctionDefinition(string functionName)
    {
        functionDefinitionService.DeleteFunctionDefinition(functionName);
        return NoContent();
    }
}
