using TAMS.Application.Common.Ports;

namespace TAMS.Api.Common;

/// <summary>
/// Assigns/propagates a correlation id per request (header X-Correlation-Id),
/// exposes it via ICorrelationIdAccessor, and echoes it on the response so the
/// client can quote it on errors. (05 §6, 06 §11, NAPI-05.)
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, CorrelationIdAccessor accessor)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var value)
            && Guid.TryParse(value, out var parsed)
                ? parsed
                : Guid.NewGuid();

        accessor.Set(correlationId);
        context.Response.Headers[HeaderName] = correlationId.ToString();

        await _next(context);
    }
}

/// <summary>Scoped holder for the current request's correlation id.</summary>
public sealed class CorrelationIdAccessor : ICorrelationIdAccessor
{
    public Guid CorrelationId { get; private set; } = Guid.Empty;

    public void Set(Guid id) => CorrelationId = id;
}
