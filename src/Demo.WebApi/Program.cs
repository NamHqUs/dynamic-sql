using Demo.WebApi.Services;
using Namh.Data.DynamicSql;
using System.Text.Json.Serialization;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables()
    .BuildTemplateConfig();
DbProviderFactory.Initialize(configuration);

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddConfiguration(configuration);
builder.WebHost.UseUrls($"http://localhost:21210");

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<DataType>());
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<FieldType>());
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<RelationType>());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "swagger";
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Dynamic SQL Demo v1");
});

app.MapGet("/", () => Results.Text("Dynamic SQL Demo. /swagger to open Swagger UI"));

var group = app.MapGroup("/db").WithTags("Raw Database Services");
group.MapGet("/tables", RawDataServices.GetExposedTables);
group.MapGet("/data/{tableName}", (string tableName) => RawDataServices.GetTableData(tableName));

group = app.MapGroup("/metadata").WithTags("Defined Metadata");
group.MapGet("", MetadataServices.GetMetadata);
group.MapPost("", MetadataServices.CreateMetadata);
group.MapPost("/reset", MetadataServices.Reset);
group.MapPut("/{entityName}", (string entityName, Entity entity) => MetadataServices.UpdateMetadata(entityName, entity));
group.MapDelete("/{entityName}", (string entityName) =>
{
    MetadataServices.DeleteMetadata(entityName);
    return Results.NoContent();
});

group = app.MapGroup("/dynamic-query").WithTags("Dynamic Query Services");
group.MapPost("/query/to-string", (QueryDto query) => DynamicQueryServices.GetSqlQueryStringAsync(query));
group.MapPost("/query", (QueryDto query) => DynamicQueryServices.ExecuteQueryAsync(query));
group.MapPost("/query/to-string-from-text", async (HttpRequest request) =>
{
    var text = await new StreamReader(request.Body).ReadToEndAsync();
    try
    {
        return Results.Text(DynamicQueryServices.GetSqlQueryStringAsync(QueryRequestServices.ParseQueryRequestString(text)));
    }
    catch (FormatException error)
    {
        return Results.BadRequest(error.Message);
    }
    catch (System.Text.Json.JsonException error)
    {
        return Results.BadRequest(error.Message);
    }
});
group.MapPost("/query/from-text", async (HttpRequest request) =>
{
    var text = await new StreamReader(request.Body).ReadToEndAsync();
    try
    {
        return Results.Ok(await DynamicQueryServices.ExecuteQueryAsync(QueryRequestServices.ParseQueryRequestString(text)));
    }
    catch (FormatException error)
    {
        return Results.BadRequest(error.Message);
    }
    catch (System.Text.Json.JsonException error)
    {
        return Results.BadRequest(error.Message);
    }
});

group = app.MapGroup("/query-request").WithTags("Query Request Services");
group.MapGet("/", () => QueryRequestServices.ListQueryRequests());
group.MapPost("/to-string", (QueryDto query) => Results.Text(QueryRequestServices.GetQueryRequestString(query)));
group.MapPost("/parse", (QueryRequestTextRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Text) || string.IsNullOrWhiteSpace(request.Entity))
        return Results.BadRequest("Request text and entity are required.");

    try
    {
        return Results.Ok(QueryRequestServices.ParseQueryRequestString(request.Text, request.Entity));
    }
    catch (FormatException error)
    {
        return Results.BadRequest(error.Message);
    }
    catch (System.Text.Json.JsonException error)
    {
        return Results.BadRequest(error.Message);
    }
})
    .WithSummary("Parse an edited query request structure")
    .WithDescription("Converts the formatted query request text back into a structured query request. The entity is fixed.");
group.MapGet("{fileName}", (string fileName) =>
    QueryRequestServices.LoadQueryRequest(fileName) is { } request
        ? Results.Ok(request)
        : Results.NotFound());
group.MapPost("/", (QueryDto query) =>
{
    var saved = QueryRequestServices.SaveQueryRequest(query);
    return Results.Created($"/query-request/{saved.FileName}", saved);
});
group.MapPut("{fileName}", (string fileName, QueryDto query) =>
    QueryRequestServices.UpdateQueryRequest(fileName, query) is { } request
        ? Results.Ok(request)
        : Results.NotFound());
group.MapDelete("{fileName}", (string fileName) =>
    QueryRequestServices.DeleteQueryRequest(fileName) ? Results.NoContent() : Results.NotFound());

app.Run();

sealed record QueryRequestTextRequest(string? Text, string? Entity);