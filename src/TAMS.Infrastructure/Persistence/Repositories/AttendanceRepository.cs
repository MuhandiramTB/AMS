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

    // The "day boundary" cutoff (UTC hour). A day owns punches from this hour up to
    // the same hour the next calendar day, so an overnight shift's early-morning OUT
    // (before the cutoff) is attributed to the day it started — and NEVER to the next
    // day as well. This makes each punch belong to exactly one work day (no double
    // attribution across the overnight boundary). (FR-ATT — overnight shifts.)
    private static readonly TimeOnly DayBoundaryCutoff = new(4, 0);

    public async Task<IReadOnlyList<PunchTransaction>> GetPunchesForDayAsync(
        long employeeId, DateOnly workDate, CancellationToken cancellationToken = default)
    {
        // [workDate 04:00, workDate+1 04:00) — a single, non-overlapping window per day.
        var from = workDate.ToDateTime(DayBoundaryCutoff, DateTimeKind.Utc);
        var to = workDate.AddDays(1).ToDateTime(DayBoundaryCutoff, DateTimeKind.Utc);

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
        // Split the Exceptions include into a second, key-based query so OFFSET/FETCH
        // is applied to the base table only. A single-query JOIN would fan each record
        // out by its exception count, transferring far more than `pageSize` rows and
        // degrading a hot list endpoint against NFR-01. (Perf hardening.)
        var query = _db.AttendanceRecords.AsNoTracking()
            .Include(r => r.Exceptions)
            .AsSplitQuery()
            .AsQueryable();

        if (employeeId is not null) query = query.Where(r => r.EmployeeId == employeeId);
        if (fromDate is not null) query = query.Where(r => r.WorkDate >= fromDate);
        if (toDate is not null) query = query.Where(r => r.WorkDate <= toDate);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.WorkDate).ThenBy(r => r.EmployeeId).ThenBy(r => r.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<(IReadOnlyList<PunchTransaction> Items, int TotalCount)> GetUnresolvedPunchesPagedAsync(
        int page, int pageSize, long? deviceId, CancellationToken ct = default)
    {
        var query = _db.Punches.AsNoTracking()
            .Where(p => p.EmployeeId == null);

        if (deviceId is not null) query = query.Where(p => p.DeviceId == deviceId);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.PunchedAtUtc).ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}
