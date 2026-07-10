namespace TAMS.Application.Common.Ports;

/// <summary>
/// Writes an explicit audit entry for actions that don't mutate domain data and
/// therefore aren't captured by the persistence interceptor — notably report/
/// export generation, which must be audited. (FR-RPT-007, FR-AUD-001.)
/// </summary>
public interface IAuditWriter
{
    Task RecordAsync(string action, string entityName, string entityId, CancellationToken cancellationToken = default);
}
