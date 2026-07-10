using Microsoft.EntityFrameworkCore;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Attendance;

namespace TAMS.Infrastructure.Persistence.Repositories;

public sealed class AttendanceRepository : IAttendanceRepository
{
    private readonly TamsDbContext _db;

    public AttendanceRepository(TamsDbContext db) => _db = db;

    public async Task<bool> TryAddPunchAsync(PunchTransaction punch, CancellationToken cancellationToken = default)
    {
        // Idempotent: if the key already exists, treat as a no-op. (FR-ATT-008.)
        var exists = await _db.Punches.AnyAsync(p => p.IdempotencyKey == punch.IdempotencyKey, cancellationToken);
        if (exists)
        {
            return false;
        }

        await _db.Punches.AddAsync(punch, cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<PunchTransaction>> GetPunchesForDayAsync(
        long employeeId, DateOnly workDate, CancellationToken cancellationToken = default)
    {
        // Window covers the work date plus early next-day punches (overnight shifts).
        var from = workDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = workDate.AddDays(1).ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);

        return await _db.Punches.AsNoTracking()
            .Where(p => p.EmployeeId == employeeId && p.PunchedAtUtc >= from && p.PunchedAtUtc < to)
            .OrderBy(p => p.PunchedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<AttendanceRecord?> GetRecordAsync(long employeeId, DateOnly workDate, CancellationToken cancellationToken = default) =>
        _db.AttendanceRecords
            .Include(r => r.Exceptions)
            .Include(r => r.Corrections)
            .FirstOrDefaultAsync(r => r.EmployeeId == employeeId && r.WorkDate == workDate, cancellationToken);

    public Task<AttendanceRecord?> GetRecordByIdAsync(long id, CancellationToken cancellationToken = default) =>
        _db.AttendanceRecords
            .Include(r => r.Exceptions)
            .Include(r => r.Corrections)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task AddRecordAsync(AttendanceRecord record, CancellationToken cancellationToken = default) =>
        await _db.AttendanceRecords.AddAsync(record, cancellationToken);

    public async Task<(IReadOnlyList<AttendanceRecord> Items, int TotalCount)> GetRecordsPagedAsync(
        int page, int pageSize, long? employeeId, DateOnly? fromDate, DateOnly? toDate,
        CancellationToken cancellationToken = default)
    {
        var query = _db.AttendanceRecords.AsNoTracking().Include(r => r.Exceptions).AsQueryable();

        if (employeeId is not null) query = query.Where(r => r.EmployeeId == employeeId);
        if (fromDate is not null) query = query.Where(r => r.WorkDate >= fromDate);
        if (toDate is not null) query = query.Where(r => r.WorkDate <= toDate);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.WorkDate).ThenBy(r => r.EmployeeId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }
}
