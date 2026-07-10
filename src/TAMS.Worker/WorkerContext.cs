using TAMS.Application.Common.Ports;

namespace TAMS.Worker;

/// <summary>
/// Correlation-id accessor for the worker: a fresh id per sync cycle so device
/// operations are traceable end-to-end in logs/audit, mirroring the API's
/// per-request id. (FR-ZK-009, 06 §11.)
/// </summary>
public sealed class WorkerCorrelationIdAccessor : ICorrelationIdAccessor
{
    public Guid CorrelationId { get; private set; } = Guid.NewGuid();

    public void NewCycle() => CorrelationId = Guid.NewGuid();
}

/// <summary>
/// The worker runs as the "system" principal — unauthenticated, no user, no
/// scope. Audit entries produced by worker writes are attributed to "system".
/// (Matches AuditTrailBuilder's system fallback.)
/// </summary>
public sealed class SystemUser : ICurrentUser
{
    public long? UserId => null;
    public string UserName => "system";
    public bool IsAuthenticated => false;
    public IReadOnlyCollection<string> Permissions => Array.Empty<string>();
    public long? EmployeeId => null;
    public bool HasPermission(string permission) => false;
}
