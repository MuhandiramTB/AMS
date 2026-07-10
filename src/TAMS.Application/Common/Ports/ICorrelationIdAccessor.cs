namespace TAMS.Application.Common.Ports;

/// <summary>
/// Provides the correlation id for the current operation, threaded through logs
/// and audit entries. (05 §6, 06 §11, NAPI-05.)
/// </summary>
public interface ICorrelationIdAccessor
{
    Guid CorrelationId { get; }
}
