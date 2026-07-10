using TAMS.Domain.Scheduling;

namespace TAMS.Application.Scheduling;

public sealed record ShiftDto(
    long Id,
    string Code,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int BreakMinutes,
    int GraceInMinutes,
    int GraceOutMinutes,
    int OvertimeThresholdMinutes,
    bool IsOvernight,
    bool IsActive)
{
    public static ShiftDto FromEntity(Shift s) =>
        new(s.Id, s.Code, s.Name, s.StartTime, s.EndTime, s.BreakMinutes,
            s.GraceInMinutes, s.GraceOutMinutes, s.OvertimeThresholdMinutes, s.IsOvernight, s.IsActive);
}

public sealed record ShiftAssignmentDto(
    long Id,
    long ShiftId,
    long? EmployeeId,
    long? DepartmentId,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo)
{
    public static ShiftAssignmentDto FromEntity(ShiftAssignment a) =>
        new(a.Id, a.ShiftId, a.EmployeeId, a.DepartmentId, a.EffectiveFrom, a.EffectiveTo);
}
