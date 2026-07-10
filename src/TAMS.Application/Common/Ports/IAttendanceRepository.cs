using TAMS.Domain.Attendance;

namespace TAMS.Application.Common.Ports;

/// <summary>Persistence port for raw punches and processed attendance records.</summary>
public interface IAttendanceRepository
{
    /// <summary>
    /// Persists a punch if its idempotency key is new; returns false if it is a
    /// duplicate. This operation is self-contained and race-safe: it saves within
    /// the call and treats a concurrent unique-key violation as an idempotent
    /// no-op (returns false). (FR-ATT-008, ADR-011.)
    /// </summary>
    Task<bool> TryAddPunchAsync(PunchTransaction punch, CancellationToken cancellationToken = default);

    /// <summary>Punches attributable to an employee on a work date (covers overnight next-day punches).</summary>
    Task<IReadOnlyList<PunchTransaction>> GetPunchesForDayAsync(
        long employeeId, DateOnly workDate, CancellationToken cancellationToken = default);

    Task<AttendanceRecord?> GetRecordAsync(long employeeId, DateOnly workDate, CancellationToken cancellationToken = default);
    Task<AttendanceRecord?> GetRecordByIdAsync(long id, CancellationToken cancellationToken = default);
    Task AddRecordAsync(AttendanceRecord record, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the expected original RowVersion for a tracked record so the next save
    /// enforces optimistic concurrency (stale token → DbUpdateConcurrencyException →
    /// 409). Base64-decoded from the client's If-Match token. (05 §8.2, FR-ATT-006.)
    /// </summary>
    void SetOriginalConcurrencyToken(AttendanceRecord record, byte[] expectedRowVersion);

    Task<(IReadOnlyList<AttendanceRecord> Items, int TotalCount)> GetRecordsPagedAsync(
        int page, int pageSize, long? employeeId, DateOnly? fromDate, DateOnly? toDate,
        CancellationToken cancellationToken = default);
}
