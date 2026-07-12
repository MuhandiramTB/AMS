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

        var (status, title, type, errors, safeDetail) = Map(exception);

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
            // Use the mapped safe detail (never the raw exception message for infra
            // exceptions like DbUpdate*, which would leak schema/table/EF internals).
            Detail = safeDetail,
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

    private static (int Status, string Title, string Type, IDictionary<string, string[]>? Errors, string Detail) Map(
        Exception exception)
    {
        // Detail is only the raw exception message for OUR OWN application/domain
        // exceptions, whose messages are authored and user-safe. Infrastructure
        // exceptions (DbUpdate*, unknown) get a generic detail so no EF/DB internals leak.
        switch (exception)
        {
            case ValidationException v:
                var grouped = v.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
                return (StatusCodes.Status400BadRequest, "One or more validation errors occurred.",
                    "https://tams/errors/validation", grouped, "One or more validation errors occurred.");

            case NotFoundException:
                return (StatusCodes.Status404NotFound, "Resource not found.",
                    "https://tams/errors/not-found", null, exception.Message);

            case ConflictException:
                return (StatusCodes.Status409Conflict, "Resource conflict.",
                    "https://tams/errors/conflict", null, exception.Message);

            case BusinessRuleException:
                return (StatusCodes.Status422UnprocessableEntity, "Business rule violation.",
                    "https://tams/errors/business-rule", null, exception.Message);

            case AuthenticationException:
                return (StatusCodes.Status401Unauthorized, "Authentication failed.",
                    "https://tams/errors/authentication", null, exception.Message);

            case AccountLockedException:
                return (StatusCodes.Status423Locked, "Account locked.",
                    "https://tams/errors/locked", null, exception.Message);

            case ForbiddenException:
                return (StatusCodes.Status403Forbidden, "Forbidden.",
                    "https://tams/errors/forbidden", null, exception.Message);

            case ApplicationException:
                return (StatusCodes.Status400BadRequest, "Request error.",
                    "https://tams/errors/bad-request", null, exception.Message);

            // Safety net: a lost-update (RowVersion mismatch) that a handler did
            // not translate still surfaces as 409, never an opaque 500. (05 §8.)
            // Static detail — the raw EF message must not reach the client.
            case DbUpdateConcurrencyException:
                return (StatusCodes.Status409Conflict, "The record was modified by someone else. Reload and try again.",
                    "https://tams/errors/concurrency", null,
                    "The record was modified by someone else. Reload and try again.");

            // Safety net for uniqueness/constraint violations that slip past a
            // handler pre-check (e.g. a race on a unique index). (05 §5.)
            case DbUpdateException:
                return (StatusCodes.Status409Conflict, "Resource conflict.",
                    "https://tams/errors/conflict", null, "The operation conflicts with existing data.");

            default:
                return (StatusCodes.Status500InternalServerError, "Internal server error.",
                    "https://tams/errors/internal", null, "An unexpected error occurred.");
        }
    }
}
