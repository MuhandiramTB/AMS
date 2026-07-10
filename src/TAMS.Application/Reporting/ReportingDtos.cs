using TAMS.Application.Common.Ports;

namespace TAMS.Application.Reporting;

// DTOs mirror the reporting port records (kept separate so the API contract is
// stable even if the read-side records evolve). (05 §1.)
public sealed record DepartmentCountDto(long DepartmentId, int Present, int Late, int Absent, int OnLeave);

public sealed record AttendanceSummaryDto(
    string WorkDate, int Present, int Late, int EarlyLeave, int Absent, int OnLeave,
    int OpenExceptions, IReadOnlyList<DepartmentCountDto> ByDepartment)
{
    public static AttendanceSummaryDto From(AttendanceSummary s) =>
        new(s.WorkDate.ToString("yyyy-MM-dd"), s.Present, s.Late, s.EarlyLeave, s.Absent,
            s.OnLeave, s.OpenExceptions,
            s.ByDepartment.Select(d => new DepartmentCountDto(d.DepartmentId, d.Present, d.Late, d.Absent, d.OnLeave)).ToList());
}

public sealed record DailyAttendanceRowDto(
    long EmployeeId, string EmployeeNo, string EmployeeName, long? DepartmentId, string WorkDate,
    string? FirstInUtc, string? LastOutUtc, int? WorkedMinutes, int LateMinutes, int OvertimeMinutes, string Status)
{
    public static DailyAttendanceRowDto From(DailyAttendanceRow r) =>
        new(r.EmployeeId, r.EmployeeNo, r.EmployeeName, r.DepartmentId, r.WorkDate.ToString("yyyy-MM-dd"),
            r.FirstInUtc?.ToString("O"), r.LastOutUtc?.ToString("O"),
            r.WorkedMinutes, r.LateMinutes, r.OvertimeMinutes, r.Status);
}

public sealed record ExceptionRowDto(long RecordId, long EmployeeId, string WorkDate, string ExceptionType)
{
    public static ExceptionRowDto From(ExceptionRow r) =>
        new(r.RecordId, r.EmployeeId, r.WorkDate.ToString("yyyy-MM-dd"), r.ExceptionType);
}
