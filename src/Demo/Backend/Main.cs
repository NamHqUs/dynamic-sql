#!
#:sdk Microsoft.NET.Sdk.Web
#:property JsonSerializerIsReflectionEnabledByDefault=true
#:package Swashbuckle.AspNetCore@10.2.3

#:project ../../Namh.Data.DynamicSql/Namh.Data.DynamicSql.csproj
#:include SqliteServices.cs
#:include DymanicQueryServices.cs

using System.Text.Json.Serialization;
using Namh.Data.DynamicSql;

var builder = WebApplication.CreateBuilder(args);   
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
group.MapGet("/tables",SqliteServices.GetExposedTables);
group.MapGet("/data/{tableName}", (string tableName) => SqliteServices.GetTableData(tableName));

group = app.MapGroup("/dynamic-query").WithTags("Dynamic Query Services");
group.MapGet("/metadata", DynamicQueryServices.GetMetadata);
group.MapPost("/query", (QueryRequest query) => DynamicQueryServices.ExecuteQueryAsync(query));

app.Run();

