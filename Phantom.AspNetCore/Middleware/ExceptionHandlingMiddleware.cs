using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Phantom.Core.Exceptions;
using Phantom.AspNetCore.ProblemDetails;
using System.Net;
using System.Text.Json;
using FluentValidation;

namespace Phantom.AspNetCore.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger) { _next = next; _logger = logger; }

    public async Task InvokeAsync(HttpContext context)
    {
        try { await _next(context); }
        catch (Exception ex) { await HandleExceptionAsync(context, ex); }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, problemDetail) = exception switch
        {
            ValidationException vex => (HttpStatusCode.BadRequest, new PhantomProblemDetail { Status = 400, Title = "Validation Error", Detail = "One or more validation errors occurred", Errors = vex.Errors.Select(e => e.ErrorMessage).ToList() }),
            BusinessRuleException brex => (HttpStatusCode.UnprocessableEntity, new PhantomProblemDetail { Status = 422, Title = "Business Rule Violation", Detail = brex.Message }),
            NotFoundException nex => (HttpStatusCode.NotFound, new PhantomProblemDetail { Status = 404, Title = "Not Found", Detail = nex.Message }),
            ConcurrencyException cex => (HttpStatusCode.Conflict, new PhantomProblemDetail { Status = 409, Title = "Concurrency Conflict", Detail = cex.Message }),
            DomainException dex => (HttpStatusCode.UnprocessableEntity, new PhantomProblemDetail { Status = 422, Title = "Domain Error", Detail = dex.Message }),
            _ => (HttpStatusCode.InternalServerError, new PhantomProblemDetail { Status = 500, Title = "Internal Server Error", Detail = "An unexpected error occurred" })
        };

        _logger.LogError(exception, "[Phantom] {Title}: {Detail}", problemDetail.Title, problemDetail.Detail);
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetail, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}
