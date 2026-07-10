using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TAMS.Application.Common.Exceptions;
using TAMS.Application.Common.Ports;
using ApplicationException = TAMS.Application.Common.Exceptions.ApplicationException;

namespace TAMS.Api.Common;

/// <summary>
/// Translates all unhandled exceptions into RFC 9457 problem+json, mapping typed
/// application exceptions to the right status codes and never leaking internals.
/// Each response carries the correlation id for support. (05 §6, 06 §5/§11, 07 §5.1.)
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Resolve the scoped correlation accessor from the request scope rather
        // than constructor-injecting it (the handler itself is a singleton).
        var correlationId = httpContext.RequestServices
            .GetRequiredService<ICorrelationIdAccessor>().CorrelationId;

        var (status, title, type, errors) = Map(exception);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception,
                "Unhandled exception (CorrelationId: {CorrelationId})", correlationId);
        }
        else
        {
            _logger.LogWarning(
                "Request failed: {Message} (CorrelationId: {CorrelationId})",
                exception.Message, correlationId);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = type,
            Detail = status >= StatusCodes.Status500InternalServerError
                ? "An unexpected error occurred." // never leak internals
                : exception.Message,
            Instance = httpContext.Request.Path
        };
        problem.Extensions["correlationId"] = correlationId.ToString();
        if (errors is not null)
        {
            problem.Extensions["errors"] = errors;
        }

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    private static (int Status, string Title, string Type, IDictionary<string, string[]>? Errors) Map(
        Exception exception)
    {
        switch (exception)
        {
            case ValidationException v:
                var grouped = v.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
                return (StatusCodes.Status400BadRequest, "One or more validation errors occurred.",
                    "https://tams/errors/validation", grouped);

            case NotFoundException:
                return (StatusCodes.Status404NotFound, "Resource not found.",
                    "https://tams/errors/not-found", null);

            case ConflictException:
                return (StatusCodes.Status409Conflict, "Resource conflict.",
                    "https://tams/errors/conflict", null);

            case BusinessRuleException:
                return (StatusCodes.Status422UnprocessableEntity, "Business rule violation.",
                    "https://tams/errors/business-rule", null);

            case AuthenticationException:
                return (StatusCodes.Status401Unauthorized, "Authentication failed.",
                    "https://tams/errors/authentication", null);

            case AccountLockedException:
                return (StatusCodes.Status423Locked, "Account locked.",
                    "https://tams/errors/locked", null);

            case ForbiddenException:
                return (StatusCodes.Status403Forbidden, "Forbidden.",
                    "https://tams/errors/forbidden", null);

            case ApplicationException:
                return (StatusCodes.Status400BadRequest, "Request error.",
                    "https://tams/errors/bad-request", null);

            // Safety net: a lost-update (RowVersion mismatch) that a handler did
            // not translate still surfaces as 409, never an opaque 500. (05 §8.)
            case DbUpdateConcurrencyException:
                return (StatusCodes.Status409Conflict, "The record was modified by someone else. Reload and try again.",
                    "https://tams/errors/concurrency", null);

            // Safety net for uniqueness/constraint violations that slip past a
            // handler pre-check (e.g. a race on a unique index). (05 §5.)
            case DbUpdateException:
                return (StatusCodes.Status409Conflict, "Resource conflict.",
                    "https://tams/errors/conflict", null);

            default:
                return (StatusCodes.Status500InternalServerError, "Internal server error.",
                    "https://tams/errors/internal", null);
        }
    }
}
