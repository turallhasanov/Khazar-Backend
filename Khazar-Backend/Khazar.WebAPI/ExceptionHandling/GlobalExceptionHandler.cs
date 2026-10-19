using System.Diagnostics;
using Khazar.Application.Common.Exceptions;
using Khazar.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Khazar.WebAPI.ExceptionHandling;

/// <summary>
/// Translates application and domain exceptions into RFC 7807 ProblemDetails responses,
/// so controllers stay free of try-catch blocks.
/// Unexpected exceptions become a generic 500: the client receives only a trace id it can quote
/// to support, while the full exception is logged with the same trace id for correlation.
/// The response is always JSON, even when the client did not ask for it in the Accept header.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    // Non-standard but widely used (nginx) status for "client closed the request".
    private const int ClientClosedRequest = 499;

    private const string ProblemJsonContentType = "application/problem+json";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        var request = httpContext.Request;

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["TraceId"] = traceId,
            ["RequestMethod"] = request.Method,
            ["RequestPath"] = request.Path.Value,
            ["ExceptionType"] = exception.GetType().FullName,
        });

        // The caller is gone; there is nobody to send a response to and nothing went wrong on the server.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("Request {Method} {Path} was cancelled by the client.", request.Method, request.Path);
            httpContext.Response.StatusCode = ClientClosedRequest;
            return true;
        }

        var (statusCode, title, errorCode) = Classify(exception);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception,
                "Unhandled {ExceptionType} while processing {Method} {Path}{QueryString} (trace id {TraceId}).",
                exception.GetType().Name, request.Method, request.Path, request.QueryString, traceId);
        }
        else
        {
            logger.LogWarning(
                "Request {Method} {Path} was rejected with {StatusCode} {ErrorCode}: {Message}",
                request.Method, request.Path, statusCode, errorCode, exception.Message);
        }

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Instance = $"{request.Method} {request.Path}",

            // Messages of our own exceptions are written for clients; anything else may contain internals.
            Detail = statusCode >= StatusCodes.Status500InternalServerError
                ? "An unexpected error occurred. Please try again later and quote the trace id if the problem persists."
                : exception.Message,
        };

        problem.Extensions["errorCode"] = errorCode;
        problem.Extensions["traceId"] = traceId;

        if (exception is InsufficientStockException stockException)
            problem.Extensions["sku"] = stockException.Sku;

        // Developers get the real cause locally; production responses never include it.
        if (environment.IsDevelopment() && statusCode >= StatusCodes.Status500InternalServerError)
        {
            problem.Extensions["exception"] = new
            {
                type = exception.GetType().FullName,
                message = exception.Message,
            };
        }

        httpContext.Response.StatusCode = statusCode;

        try
        {
            var written = await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problem,
                Exception = exception,
            });

            // TryWriteAsync declines when the Accept header excludes JSON (e.g. text/html); fall back to raw JSON.
            if (!written)
                await httpContext.Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null,
                    ProblemJsonContentType, cancellationToken);
        }
        catch (Exception writeException)
        {
            // Returning true still prevents the original exception from being rethrown; the status code is already set.
            logger.LogError(writeException, "Failed to write the error response for trace id {TraceId}.", traceId);
        }

        return true;
    }

    private static (int StatusCode, string Title, string ErrorCode) Classify(Exception exception) => exception switch
    {
        // Order matters: InsufficientStockException derives from DomainException.
        ValidationException validation => (StatusCodes.Status400BadRequest, "Invalid request", validation.ErrorCode),
        UnauthorizedException unauthorized => (StatusCodes.Status401Unauthorized, "Unauthorized", unauthorized.ErrorCode),
        NotFoundException => (StatusCodes.Status404NotFound, "Resource not found", "not_found"),
        ConflictException conflict => (StatusCodes.Status409Conflict, "Conflict", conflict.ErrorCode),
        InsufficientStockException => (StatusCodes.Status409Conflict, "Insufficient stock", "insufficient_stock"),
        DomainException => (StatusCodes.Status400BadRequest, "Business rule violation", "business_rule_violation"),

        // Raised by Kestrel/MVC for malformed requests, e.g. an oversized body.
        BadHttpRequestException badRequest => (badRequest.StatusCode, "Bad request", "bad_request"),

        // The database or another dependency did not answer in time.
        TimeoutException or OperationCanceledException =>
            (StatusCodes.Status503ServiceUnavailable, "Service temporarily unavailable", "service_unavailable"),

        _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred", "internal_error"),
    };
}
