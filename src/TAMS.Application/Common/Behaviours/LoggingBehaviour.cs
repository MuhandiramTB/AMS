using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using TAMS.Application.Common.Ports;

namespace TAMS.Application.Common.Behaviours;

/// <summary>
/// Structured request logging with correlation id and duration, applied to every
/// request. Never logs request bodies (may contain secrets/PII). (NFR-25, 06 §11,
/// 07 §5.2, ADR-007.)
/// </summary>
public sealed class LoggingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehaviour<TRequest, TResponse>> _logger;
    private readonly ICorrelationIdAccessor _correlation;

    public LoggingBehaviour(
        ILogger<LoggingBehaviour<TRequest, TResponse>> logger,
        ICorrelationIdAccessor correlation)
    {
        _logger = logger;
        _correlation = correlation;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Handling {RequestName} (CorrelationId: {CorrelationId})",
            requestName,
            _correlation.CorrelationId);

        try
        {
            var response = await next();
            stopwatch.Stop();
            _logger.LogInformation(
                "Handled {RequestName} in {ElapsedMs}ms (CorrelationId: {CorrelationId})",
                requestName,
                stopwatch.ElapsedMilliseconds,
                _correlation.CorrelationId);
            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                ex,
                "{RequestName} failed after {ElapsedMs}ms (CorrelationId: {CorrelationId})",
                requestName,
                stopwatch.ElapsedMilliseconds,
                _correlation.CorrelationId);
            throw;
        }
    }
}
