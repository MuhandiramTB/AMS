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
        // Fast path: skip the insert if the key is already present.
        var exists = await _db.Punches.AnyAsync(p => p.IdempotencyKey == punch.IdempotencyKey, cancellationToken);
        if (exists)
        {
            return false;
        }

        await _db.Punches.AddAsync(punch, cancellationToken);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // A concurrent request may have inserted the same punch between our
            // check and save. The UQ_Punch_IdempotencyKey index is the real guard.
            // Detach our failed insert and confirm the duplicate is now present;
            // if so, treat as an idempotent no-op rather than a 500. (ADR-011, 04 §11.1.)
            _db.Entry(punch).State = EntityState.Detached;

            var nowExists = await _db.Punches.AsNoTracking()
                .AnyAsync(p => p.IdempotencyKey == punch.IdempotencyKey, cancellationToken);
            if (nowExists)
            {
                return false;
            }

            throw; // a different DB error — let it surface (mapped to 409 by G1 safety net)
        }
    }

    public async Task<IReadOnlyList<string>> GetPunchKeysForDeviceAsync(long deviceId, CancellationToken cancellationToken = default) =>
        await _db.Punches.AsNoTracking()
            .Where(p => p.DeviceId == deviceId)
            .Select(p => p.IdempotencyKey)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DateOnly>> ResolveOrphanPunchesAsync(
        long deviceId, string deviceUserId, long employeeId, CancellationToken cancellationToken = default)
    {
        // Tracked (not AsNoTracking) so the EmployeeId back-fill is persisted.
        var orphans = await _db.Punches
            .Where(p => p.DeviceId == deviceId && p.DeviceUserId == deviceUserId && p.EmployeeId == null)
            .ToListAsync(cancellationToken);

        var affectedDates = new HashSet<DateOnly>();
        foreach (var punch in orphans)
        {
            punch.ResolveEmployee(employeeId);
            affectedDates.Add(DateOnly.FromDateTime(punch.PunchedAtUtc));
        }

        return affectedDates.ToList();
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

    public void SetOriginalConcurrencyToken(AttendanceRecord record, byte[] expectedRowVersion) =>
        _db.Entry(record).Property(r => r.RowVersion).OriginalValue = expectedRowVersion;

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
