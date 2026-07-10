using TAMS.Domain.Common;

namespace TAMS.Domain.Leave;

/// <summary>
/// An employee's request for time off, with a single-approver workflow and a
/// valid state machine. (02 FR-LV-001/002, §7.2, 04 §6.6, BRULE-06/07.)
/// </summary>
public sealed class LeaveRequest : AuditableEntity
{
    private LeaveRequest()
    {
    }

    public LeaveRequest(long employeeId, long leaveTypeId, DateOnly startDate, DateOnly endDate, string? reason)
    {
        if (endDate < startDate)
        {
            throw new DomainException("Leave end date cannot be before the start date.");
        }

        EmployeeId = employeeId;
        LeaveTypeId = leaveTypeId;
        StartDate = startDate;
        EndDate = endDate;
        Reason = reason;
        Status = LeaveStatus.Submitted;
    }

    public long EmployeeId { get; private set; }
    public long LeaveTypeId { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public string? Reason { get; private set; }
    public LeaveStatus Status { get; private set; }
    public long? ApproverUserId { get; private set; }
    public DateTime? DecisionAtUtc { get; private set; }

    /// <summary>Inclusive calendar-day span of the request. (Half-days/holidays deferred, OQ-03.)</summary>
    public int DayCount => EndDate.DayNumber - StartDate.DayNumber + 1;

    /// <summary>True if this approved/applied leave covers the given date. (BRULE-06.)</summary>
    public bool CoversDate(DateOnly date) =>
        (Status == LeaveStatus.Approved || Status == LeaveStatus.Applied) &&
        date >= StartDate && date <= EndDate;

    public void Approve(long approverUserId, DateTime nowUtc)
    {
        RequireStatus(LeaveStatus.Submitted, "approve");
        Status = LeaveStatus.Approved;
        ApproverUserId = approverUserId;
        DecisionAtUtc = nowUtc;
    }

    public void Reject(long approverUserId, DateTime nowUtc)
    {
        RequireStatus(LeaveStatus.Submitted, "reject");
        Status = LeaveStatus.Rejected;
        ApproverUserId = approverUserId;
        DecisionAtUtc = nowUtc;
    }

    public void Cancel()
    {
        // Submitted (not yet decided), Approved (decided, not yet reflected) and
        // Applied (reflected in attendance) can all be cancelled — e.g. an employee
        // returns early. Rejected/already-Cancelled cannot.
        if (Status is not (LeaveStatus.Submitted or LeaveStatus.Approved or LeaveStatus.Applied))
        {
            throw new DomainException($"Cannot cancel a leave request in status {Status}.");
        }

        Status = LeaveStatus.Cancelled;
    }

    /// <summary>Marks the leave as reflected in attendance. (FR-LV-005.)</summary>
    public void MarkApplied()
    {
        if (Status != LeaveStatus.Approved)
        {
            throw new DomainException("Only approved leave can be applied.");
        }

        Status = LeaveStatus.Applied;
    }

    private void RequireStatus(LeaveStatus expected, string action)
    {
        if (Status != expected)
        {
            throw new DomainException($"Cannot {action} a leave request in status {Status}.");
        }
    }
}
