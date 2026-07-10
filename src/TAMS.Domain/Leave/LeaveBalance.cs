using TAMS.Domain.Common;

namespace TAMS.Domain.Leave;

/// <summary>
/// An employee's leave balance for one type in one year (fixed annual entitlement
/// model; accrual/carry-over deferred to OQ-03). Approvals consume days; a request
/// cannot be approved beyond the available balance unless explicitly overridden.
/// (02 FR-LV-003/004, BRULE-07, 04 §6.6.)
/// </summary>
public sealed class LeaveBalance : AuditableEntity
{
    private LeaveBalance()
    {
    }

    public LeaveBalance(long employeeId, long leaveTypeId, short year, decimal entitledDays)
    {
        if (entitledDays < 0)
        {
            throw new DomainException("Entitled days cannot be negative.");
        }

        EmployeeId = employeeId;
        LeaveTypeId = leaveTypeId;
        Year = year;
        EntitledDays = entitledDays;
        UsedDays = 0m;
    }

    public long EmployeeId { get; private set; }
    public long LeaveTypeId { get; private set; }
    public short Year { get; private set; }
    public decimal EntitledDays { get; private set; }
    public decimal UsedDays { get; private set; }

    public decimal RemainingDays => EntitledDays - UsedDays;

    public bool CanConsume(decimal days) => days <= RemainingDays;

    /// <summary>
    /// Consumes days against the balance. Rejects going over-balance unless the
    /// caller explicitly allows an override (policy decision made upstream). (BRULE-07.)
    /// </summary>
    public void Consume(decimal days, bool allowOverride = false)
    {
        if (days <= 0)
        {
            throw new DomainException("Days to consume must be positive.");
        }

        if (!allowOverride && !CanConsume(days))
        {
            throw new DomainException(
                $"Insufficient leave balance: requested {days}, remaining {RemainingDays}.");
        }

        UsedDays += days;
    }

    /// <summary>Returns days to the balance (e.g. when approved leave is cancelled).</summary>
    public void Release(decimal days)
    {
        if (days <= 0)
        {
            return;
        }

        UsedDays = Math.Max(0m, UsedDays - days);
    }

    public void SetEntitlement(decimal entitledDays)
    {
        if (entitledDays < 0)
        {
            throw new DomainException("Entitled days cannot be negative.");
        }

        EntitledDays = entitledDays;
    }
}
