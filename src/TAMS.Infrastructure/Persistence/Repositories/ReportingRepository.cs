using Microsoft.EntityFrameworkCore;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Attendance;

namespace TAMS.Infrastructure.Persistence.Repositories;

/// <summary>
/// Read-side reporting aggregates via EF projections (no domain-object hydration).
/// Present/Late/Absent/OnLeave are derived from the processed AttendanceRecord —
/// a record on the day whose worked minutes &gt; 0 is present; late if lateMinutes
/// &gt; 0; an Exception with no worked time is treated as absent; a leave-covered
/// Processed record with no punches is on-leave. (FR-RPT-001/002.)
/// </summary>
public sealed class ReportingRepository : IReportingRepository
{
    private readonly TamsDbContext _db;

    public ReportingRepository(TamsDbContext db) => _db = db;

    public async Task<AttendanceSummary> GetAttendanceSummaryAsync(
        DateOnly workDate, long? departmentId, long? employeeId, CancellationToken cancellationToken = default)
    {
        // Join records for the date to their employee (for department + scoping).
        var query =
            from r in _db.AttendanceRecords.AsNoTracking()
            join e in _db.Employees.AsNoTracking() on r.EmployeeId equals e.Id
            where r.WorkDate == workDate
            select new { r.EmployeeId, r.Status, r.LateMinutes, r.EarlyLeaveMinutes, r.WorkedMinutes, e.PrimaryDepartmentId };

        if (departmentId is not null)
        {
            query = query.Where(x => x.PrimaryDepartmentId == departmentId);
        }
        // Server-derived own-record scope: a restricted caller only sees themselves.
        if (employeeId is not null)
        {
            query = query.Where(x => x.EmployeeId == employeeId);
        }

        // Aggregate in SQL (GroupBy → conditional COUNTs) so the DB returns one row
        // per department, not one per employee. The status derivation is expressed in
        // the projection so EF translates it to CASE/SUM and no per-row app-side loop
        // runs on every dashboard refresh. (NFR-03 — scales with departments, not
        // headcount.) present = worked > 0; leave = Processed with no worked time;
        // absent = Exception with no worked time.
        var perDept = await query
            .GroupBy(x => x.PrimaryDepartmentId)
            .Select(g => new DepartmentAttendanceCount(
                g.Key,
                g.Count(x => x.WorkedMinutes > 0),
                g.Count(x => x.LateMinutes > 0),
                g.Count(x => x.Status == AttendanceStatus.Exception && x.WorkedMinutes == null),
                g.Count(x => x.Status == AttendanceStatus.Processed && x.WorkedMinutes == null)))
            .ToListAsync(cancellationToken);

        // EarlyLeave is a whole-day total not tracked per department in the DTO;
        // one extra scalar aggregate keeps it exact without materializing rows.
        var earlyLeave = await query.CountAsync(x => x.EarlyLeaveMinutes > 0, cancellationToken);

        var present = perDept.Sum(d => d.Present);
        var late = perDept.Sum(d => d.Late);
        var absent = perDept.Sum(d => d.Absent);
        var onLeave = perDept.Sum(d => d.OnLeave);

        var openExceptions = await _db.AttendanceRecords.AsNoTracking()
            .Where(r => r.WorkDate == workDate)
            .SelectMany(r => r.Exceptions)
            .CountAsync(x => !x.IsResolved, cancellationToken);

        return new AttendanceSummary(
            workDate, present, late, earlyLeave, absent, onLeave, openExceptions, perDept);
    }

    public async Task<(IReadOnlyList<DailyAttendanceRow> Items, int TotalCount)> GetDailyAttendanceAsync(
        int page, int pageSize, DateOnly? fromDate, DateOnly? toDate,
        long? employeeId, long? departmentId, string? status, CancellationToken cancellationToken = default)
    {
        var query =
            from r in _db.AttendanceRecords.AsNoTracking()
            join e in _db.Employees.AsNoTracking() on r.EmployeeId equals e.Id
            select new { r, e };

        if (fromDate is not null) query = query.Where(x => x.r.WorkDate >= fromDate);
        if (toDate is not null) query = query.Where(x => x.r.WorkDate <= toDate);
        if (employeeId is not null) query = query.Where(x => x.r.EmployeeId == employeeId);
        if (departmentId is not null) query = query.Where(x => x.e.PrimaryDepartmentId == departmentId);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AttendanceStatus>(status, out var st))
            query = query.Where(x => x.r.Status == st);

        // Export path (pageSize == int.MaxValue): the date window is already bounded
        // and validated at the command boundary, but we still cap the row count as a
        // safety net and skip the redundant COUNT (the caller streams all rows).
        var isExport = pageSize == int.MaxValue;
        const int ExportRowCap = 500_000;

        var total = isExport ? 0 : await query.CountAsync(cancellationToken);

        var take = isExport ? ExportRowCap : pageSize;
        var page1 = page < 1 ? 1 : page;

        var items = await query
            .OrderByDescending(x => x.r.WorkDate).ThenBy(x => x.r.EmployeeId)
            .Skip(isExport ? 0 : (page1 - 1) * take)
            .Take(take)
            .Select(x => new DailyAttendanceRow(
                x.r.EmployeeId, x.e.EmployeeNo, x.e.FirstName + " " + x.e.LastName, x.e.PrimaryDepartmentId,
                x.r.WorkDate, x.r.FirstInUtc, x.r.LastOutUtc, x.r.WorkedMinutes,
                x.r.LateMinutes, x.r.OvertimeMinutes, x.r.Status.ToString()))
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<IReadOnlyList<ExceptionRow>> GetOpenExceptionsAsync(
        DateOnly? fromDate, DateOnly? toDate, long? departmentId, long? employeeId, CancellationToken cancellationToken = default)
    {
        var query =
            from r in _db.AttendanceRecords.AsNoTracking()
            join e in _db.Employees.AsNoTracking() on r.EmployeeId equals e.Id
            from ex in r.Exceptions
            where !ex.IsResolved
            select new { r.Id, r.EmployeeId, r.WorkDate, ex.ExceptionType, e.PrimaryDepartmentId };

        if (fromDate is not null) query = query.Where(x => x.WorkDate >= fromDate);
        if (toDate is not null) query = query.Where(x => x.WorkDate <= toDate);
        if (departmentId is not null) query = query.Where(x => x.PrimaryDepartmentId == departmentId);
        // Own-record scope for restricted callers (OWASP A01 — mirrors the read path).
        if (employeeId is not null) query = query.Where(x => x.EmployeeId == employeeId);

        var rows = await query
            .OrderByDescending(x => x.WorkDate)
            .Select(x => new ExceptionRow(x.Id, x.EmployeeId, x.WorkDate, x.ExceptionType.ToString()))
            .ToListAsync(cancellationToken);
        return rows;
    }

    public async Task<IReadOnlyList<PayrollLine>> GetPayrollLinesAsync(
        DateOnly fromDate, DateOnly toDate, long? departmentId, CancellationToken cancellationToken = default)
    {
        var query =
            from r in _db.AttendanceRecords.AsNoTracking()
            join e in _db.Employees.AsNoTracking() on r.EmployeeId equals e.Id
            where r.WorkDate >= fromDate && r.WorkDate <= toDate
            select new { r, e };

        if (departmentId is not null) query = query.Where(x => x.e.PrimaryDepartmentId == departmentId);

        // Group per employee, summing worked/OT and counting present days.
        var grouped = await query
            .GroupBy(x => new { x.r.EmployeeId, x.e.EmployeeNo, x.e.FirstName, x.e.LastName })
            .Select(g => new PayrollLine(
                g.Key.EmployeeId, g.Key.EmployeeNo, g.Key.FirstName + " " + g.Key.LastName,
                g.Sum(x => x.r.WorkedMinutes ?? 0),
                g.Sum(x => x.r.OvertimeMinutes),
                g.Count(x => x.r.WorkedMinutes != null && x.r.WorkedMinutes > 0)))
            .ToListAsync(cancellationToken);

        return grouped;
    }
}
