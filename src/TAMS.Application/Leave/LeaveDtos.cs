using TAMS.Domain.Leave;

namespace TAMS.Application.Leave;

public sealed record LeaveTypeDto(long Id, string Code, string Name, bool IsActive)
{
    public static LeaveTypeDto FromEntity(LeaveType t) => new(t.Id, t.Code, t.Name, t.IsActive);
}

public sealed record LeaveRequestDto(
    long Id,
    long EmployeeId,
    long LeaveTypeId,
    string StartDate,
    string EndDate,
    int DayCount,
    string Status,
    long? ApproverUserId,
    string? Reason)
{
    public static LeaveRequestDto FromEntity(LeaveRequest r) =>
        new(r.Id, r.EmployeeId, r.LeaveTypeId,
            r.StartDate.ToString("yyyy-MM-dd"), r.EndDate.ToString("yyyy-MM-dd"),
            r.DayCount, r.Status.ToString(), r.ApproverUserId, r.Reason);
}

public sealed record LeaveBalanceDto(
    long Id, long EmployeeId, long LeaveTypeId, short Year,
    decimal EntitledDays, decimal UsedDays, decimal RemainingDays)
{
    public static LeaveBalanceDto FromEntity(LeaveBalance b) =>
        new(b.Id, b.EmployeeId, b.LeaveTypeId, b.Year, b.EntitledDays, b.UsedDays, b.RemainingDays);
}
