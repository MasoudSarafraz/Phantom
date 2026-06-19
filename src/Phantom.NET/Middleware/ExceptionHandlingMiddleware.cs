using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Phantom.Core.Exceptions;
using Phantom.NET.ProblemDetails;
using System.Net;
using System.Text.Json;
using FluentValidation;

namespace Phantom.NET.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonSerializerOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        var (statusCode, problemDetail) = exception switch
        {
            ValidationException vex => (
                HttpStatusCode.BadRequest,
                new PhantomProblemDetail
                {
                    Status = 400,
                    Title = "Validation Error",
                    Detail = "One or more validation errors occurred",
                    Type = "https://tools.ietf.org/html/rfc7807#section-3.1",
                    Errors = vex.Errors
                        .GroupBy(e => e.PropertyName)
                        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
                }
            ),
            BusinessRuleException brex => (
                HttpStatusCode.UnprocessableEntity,
                new PhantomProblemDetail
                {
                    Status = 422,
                    Title = "Business Rule Violation",
                    Detail = brex.Message,
                    Type = "https://tools.ietf.org/html/rfc4918#section-11.2"
                }
            ),
            NotFoundException nex => (
                HttpStatusCode.NotFound,
                new PhantomProblemDetail
                {
                    Status = 404,
                    Title = "Not Found",
                    Detail = nex.Message,
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4"
                }
            ),
            ConcurrencyException cex => (
                HttpStatusCode.Conflict,
                new PhantomProblemDetail
                {
                    Status = 409,
                    Title = "Concurrency Conflict",
                    Detail = cex.Message,
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8"
                }
            ),
            DomainException dex => (
                HttpStatusCode.UnprocessableEntity,
                new PhantomProblemDetail
                {
                    Status = 422,
                    Title = "Domain Error",
                    Detail = dex.Message,
                    Type = "https://tools.ietf.org/html/rfc4918#section-11.2"
                }
            ),
            OperationCanceledException => (
                HttpStatusCode.BadRequest,
                new PhantomProblemDetail
                {
                    Status = 499,
                    Title = "Request Canceled",
                    Detail = "The request was canceled by the client.",
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
                }
            ),
            _ => (
                HttpStatusCode.InternalServerError,
                new PhantomProblemDetail
                {
                    Status = 500,
                    Title = "Internal Server Error",
                    Detail = "An unexpected error occurred",
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1"
                }
            )
        };

        if (exception is OperationCanceledException)
        {
            _logger.LogDebug(exception, "[Phantom] Request canceled: {Path}", context.Request.Path);
        }
        else
        {
            _logger.LogError(exception, "[Phantom] {Title}: {Detail}", problemDetail.Title, problemDetail.Detail);
        }

        problemDetail.TraceId = context.TraceIdentifier;
        problemDetail.Instance = context.Request.Path;

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetail, JsonSerializerOptions));
    }
}
