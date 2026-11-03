using Microsoft.AspNetCore.Diagnostics;
using System.Net;
using System.Text.Json;

namespace TwoPhaseCommitDemo.WebApi.Middlewares;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Exceção não tratada capturada pelo GlobalExceptionHandler: {Message}", exception.Message);

        var statusCode = exception switch
        {
            ArgumentException or InvalidOperationException => (int)HttpStatusCode.BadRequest,
            KeyNotFoundException => (int)HttpStatusCode.NotFound,
            _ => (int)HttpStatusCode.InternalServerError
        };

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/json";

        var response = new
        {
            success = false,
            error = new
            {
                code = statusCode == 500 ? "INTERNAL_SERVER_ERROR" : "UNHANDLED_ERROR",
                message = "Ocorreu um erro no processamento da solicitação.",
                details = _environment.IsDevelopment() ? exception.Message : null,
                type = "UnhandledException"
            },
            traceId = httpContext.TraceIdentifier,
            timestampUtc = DateTime.UtcNow
        };

        await httpContext.Response.WriteAsync(
            JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
            cancellationToken
        );

        return true;
    }
}

