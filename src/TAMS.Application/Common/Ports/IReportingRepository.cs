namespace TAMS.Application.Common.Ports;

/// <summary>Per-department attendance counts for a date (dashboard drill-down).</summary>
public sealed record DepartmentAttendanceCount(
    long DepartmentId, int Present, int Late, int Absent, int OnLeave);

/// <summary>Aggregated attendance snapshot for a date. (FR-RPT-001.)</summary>
public sealed record AttendanceSummary(
    DateOnly WorkDate,
    int Present,
    int Late,
    int EarlyLeave,
    int Absent,
    int OnLeave,
    int OpenExceptions,
    IReadOnlyList<DepartmentAttendanceCount> ByDepartment);

/// <summary>A flat daily-attendance report row. (FR-RPT-002.)</summary>
public sealed record DailyAttendanceRow(
    long EmployeeId,
    string EmployeeNo,
    string EmployeeName,
    long? DepartmentId,
    DateOnly WorkDate,
    DateTime? FirstInUtc,
    DateTime? LastOutUtc,
    int? WorkedMinutes,
    int LateMinutes,
    int OvertimeMinutes,
    string Status);

/// <summary>An open-exception report row. (FR-RPT-002.)</summary>
public sealed record ExceptionRow(
    long RecordId,
    long EmployeeId,
    DateOnly WorkDate,
    string ExceptionType);

/// <summary>Payroll worked-hours/overtime line per employee for a period. (FR-RPT-005.)</summary>
public sealed record PayrollLine(
    long EmployeeId,
    string EmployeeNo,
    string EmployeeName,
    int TotalWorkedMinutes,
    int TotalOvertimeMinutes,
    int DaysPresent);

/// <summary>
/// Read-side port for reporting aggregates. Implemented in Infrastructure with
/// efficient SQL projections (no domain-object hydration). Scoping (which
/// employees/departments the caller may see) is applied by the handlers via the
/// filters passed in — never widened here. (FR-RPT-*, 06 §5.)
/// </summary>
public interface IReportingRepository
{
    Task<AttendanceSummary> GetAttendanceSummaryAsync(
        DateOnly workDate, long? departmentId, long? employeeId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<DailyAttendanceRow> Items, int TotalCount)> GetDailyAttendanceAsync(
        int page, int pageSize, DateOnly? fromDate, DateOnly? toDate,
        long? employeeId, long? departmentId, string? status, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExceptionRow>> GetOpenExceptionsAsync(
        DateOnly? fromDate, DateOnly? toDate, long? departmentId, long? employeeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PayrollLine>> GetPayrollLinesAsync(
        DateOnly fromDate, DateOnly toDate, long? departmentId, CancellationToken cancellationToken = default);
}
