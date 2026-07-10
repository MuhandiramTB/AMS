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
        DateOnly workDate, long? departmentId, CancellationToken cancellationToken = default)
    {
        // Join records for the date to their employee (for department + scoping).
        var query =
            from r in _db.AttendanceRecords.AsNoTracking()
            join e in _db.Employees.AsNoTracking() on r.EmployeeId equals e.Id
            where r.WorkDate == workDate
            select new { r.Status, r.LateMinutes, r.EarlyLeaveMinutes, r.WorkedMinutes, e.PrimaryDepartmentId };

        if (departmentId is not null)
        {
            query = query.Where(x => x.PrimaryDepartmentId == departmentId);
        }

        var rows = await query.ToListAsync(cancellationToken);

        int present = 0, late = 0, earlyLeave = 0, absent = 0, onLeave = 0;
        var byDept = new Dictionary<long, (int Present, int Late, int Absent, int OnLeave)>();

        foreach (var x in rows)
        {
            var isLeave = x.Status == AttendanceStatus.Processed && x.WorkedMinutes is null;
            var isAbsent = x.Status == AttendanceStatus.Exception && x.WorkedMinutes is null && !isLeave;
            var isPresent = x.WorkedMinutes is > 0;

            if (isLeave) onLeave++;
            else if (isAbsent) absent++;
            else if (isPresent) present++;
            if (x.LateMinutes > 0) late++;
            if (x.EarlyLeaveMinutes > 0) earlyLeave++;

            var d = byDept.GetValueOrDefault(x.PrimaryDepartmentId);
            byDept[x.PrimaryDepartmentId] = (
                d.Present + (isPresent ? 1 : 0),
                d.Late + (x.LateMinutes > 0 ? 1 : 0),
                d.Absent + (isAbsent ? 1 : 0),
                d.OnLeave + (isLeave ? 1 : 0));
        }

        var openExceptions = await _db.AttendanceRecords.AsNoTracking()
            .Where(r => r.WorkDate == workDate)
            .SelectMany(r => r.Exceptions)
            .CountAsync(x => !x.IsResolved, cancellationToken);

        return new AttendanceSummary(
            workDate, present, late, earlyLeave, absent, onLeave, openExceptions,
            byDept.Select(kv => new DepartmentAttendanceCount(
                kv.Key, kv.Value.Present, kv.Value.Late, kv.Value.Absent, kv.Value.OnLeave)).ToList());
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

        var total = await query.CountAsync(cancellationToken);

        var take = pageSize == int.MaxValue ? int.MaxValue : pageSize;
        var page1 = page < 1 ? 1 : page;

        var items = await query
            .OrderByDescending(x => x.r.WorkDate).ThenBy(x => x.r.EmployeeId)
            .Skip((page1 - 1) * (take == int.MaxValue ? 0 : take))
            .Take(take)
            .Select(x => new DailyAttendanceRow(
                x.r.EmployeeId, x.e.EmployeeNo, x.e.FirstName + " " + x.e.LastName, x.e.PrimaryDepartmentId,
                x.r.WorkDate, x.r.FirstInUtc, x.r.LastOutUtc, x.r.WorkedMinutes,
                x.r.LateMinutes, x.r.OvertimeMinutes, x.r.Status.ToString()))
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<IReadOnlyList<ExceptionRow>> GetOpenExceptionsAsync(
        DateOnly? fromDate, DateOnly? toDate, long? departmentId, CancellationToken cancellationToken = default)
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
