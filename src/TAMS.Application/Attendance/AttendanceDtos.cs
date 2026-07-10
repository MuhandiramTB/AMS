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
    IReadOnlyList<AttendanceExceptionDto> Exceptions,
    // Opaque concurrency token (base64 RowVersion) surfaced to clients as an
    // ETag for optimistic concurrency on correction. (05 §8.2, FR-ATT-006.)
    string ConcurrencyToken)
{
    public static AttendanceRecordDto FromEntity(AttendanceRecord r) =>
        new(r.Id, r.EmployeeId, r.WorkDate, r.ResolvedShiftId, r.FirstInUtc, r.LastOutUtc,
            r.WorkedMinutes, r.LateMinutes, r.EarlyLeaveMinutes, r.OvertimeMinutes, r.Status.ToString(),
            r.Exceptions.Select(e => new AttendanceExceptionDto(
                e.Id, e.ExceptionType.ToString(), e.IsResolved, e.Notes)).ToList(),
            r.RowVersion.Length == 0 ? string.Empty : Convert.ToBase64String(r.RowVersion));
}
