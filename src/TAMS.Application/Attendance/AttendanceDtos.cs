using TAMS.Domain.Attendance;

namespace TAMS.Application.Attendance;

public sealed record AttendanceExceptionDto(long Id, string Type, bool IsResolved, string? Notes);

public sealed record AttendanceRecordDto(
    long Id,
    long EmployeeId,
    DateOnly WorkDate,
    long? ResolvedShiftId,
    DateTime? FirstInUtc,
    DateTime? LastOutUtc,
    int? WorkedMinutes,
    int LateMinutes,
    int EarlyLeaveMinutes,
    int OvertimeMinutes,
    string Status,
    IReadOnlyList<AttendanceExceptionDto> Exceptions)
{
    public static AttendanceRecordDto FromEntity(AttendanceRecord r) =>
        new(r.Id, r.EmployeeId, r.WorkDate, r.ResolvedShiftId, r.FirstInUtc, r.LastOutUtc,
            r.WorkedMinutes, r.LateMinutes, r.EarlyLeaveMinutes, r.OvertimeMinutes, r.Status.ToString(),
            r.Exceptions.Select(e => new AttendanceExceptionDto(
                e.Id, e.ExceptionType.ToString(), e.IsResolved, e.Notes)).ToList());
}
