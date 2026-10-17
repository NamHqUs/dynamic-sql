using Demo.Repositories;
using Demo.Contracts.Application;
using Demo.Services;
using Demo.WebApi.Application.Middlewares;
using Namh.Data.DynamicSql;
using System.Text.Json.Serialization;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables()
    .BuildTemplateConfig();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddConfiguration(configuration);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter<DataType>());
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter<FieldType>());
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter<RelationType>());
    });
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<DataType>());
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<FieldType>());
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<RelationType>());
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDemoRepositories(builder.Configuration);
builder.Services.AddDemoServices();
builder.Services.AddSingleton<IUserSectionManager, UserSectionManager>();

var app = builder.Build();
app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "";
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Dynamic SQL Demo v1");
});
app.MapControllers();
app.Run();
