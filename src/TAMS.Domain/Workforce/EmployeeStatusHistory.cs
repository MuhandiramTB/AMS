using TAMS.Domain.Common;

namespace TAMS.Domain.Workforce;

/// <summary>
/// Insert-only record of an employee status change over time. (FR-EMP-005,
/// 04 Workforce.EmployeeStatusHistory — temporal, insert-only.)
/// </summary>
public sealed class EmployeeStatusHistory : Entity
{
    private EmployeeStatusHistory()
    {
    }

    public EmployeeStatusHistory(long employeeId, EmployeeStatus status, DateTime effectiveFromUtc, string? reason)
    {
        EmployeeId = employeeId;
        Status = status;
        EffectiveFromUtc = effectiveFromUtc;
        Reason = reason;
    }

    public long EmployeeId { get; private set; }
    public EmployeeStatus Status { get; private set; }
    public DateTime EffectiveFromUtc { get; private set; }
    public string? Reason { get; private set; }
}
