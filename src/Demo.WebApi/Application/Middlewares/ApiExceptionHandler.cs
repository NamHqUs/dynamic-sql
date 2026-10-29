using Demo.Contracts.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Data.Common;
using System.Text.Json;

namespace Demo.WebApi.Application.Middlewares;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            DatabaseConnectionException or DbException =>
                (StatusCodes.Status503ServiceUnavailable, "Database connection required"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Not Found"),
            ArgumentException or FormatException or JsonException =>
                (StatusCodes.Status400BadRequest, "Bad Request"),
            InvalidOperationException => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (0, string.Empty)
        };
        if (statusCode == 0)
            return false;

        logger.LogWarning(exception, "Handled API exception: {Title}", title);
        httpContext.Response.StatusCode = statusCode;
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = exception is DatabaseConnectionException
                ? exception.Message
                : DatabaseConnectionException.UserMessage,
            Instance = httpContext.Request.Path
        };
        if (exception is DatabaseConnectionException or DbException)
            problem.Extensions["code"] = "database_connection_required";
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
