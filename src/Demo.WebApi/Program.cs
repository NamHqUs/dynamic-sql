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
    options.RoutePrefix = "";
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Dynamic SQL Demo v1");
});

var group = app.MapGroup("/db").WithTags("Raw Database Services");
group.MapGet("/tables", RawDataServices.GetExposedTables);
group.MapGet("/data/{tableName}", (string tableName) => RawDataServices.GetTableData(tableName));

group = app.MapGroup("/dynamic-query").WithTags("Dynamic Query Services");
group.MapGet("/metadata", MetadataServices.GetMetadata);
group.MapPost("/query/to-string", (QueryRequest query) => DynamicQueryServices.GetSqlQueryStringAsync(query));
group.MapPost("/query", (QueryRequest query) => DynamicQueryServices.ExecuteQueryAsync(query));

group = app.MapGroup("/query-request").WithTags("Query Request Services");
group.MapPost("/save", (QueryRequest query) => QueryRequestServices.SaveQueryRequest(query));
group.MapGet("/load/{fileName}", (string fileName) => QueryRequestServices.LoadQueryRequest(fileName));

app.Run();