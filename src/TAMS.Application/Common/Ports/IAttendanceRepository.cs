using TAMS.Domain.Attendance;

namespace TAMS.Application.Common.Ports;

/// <summary>Persistence port for raw punches and processed attendance records.</summary>
public interface IAttendanceRepository
{
    /// <summary>Adds a punch if its idempotency key is new; returns false if a duplicate. (FR-ATT-008.)</summary>
    Task<bool> TryAddPunchAsync(PunchTransaction punch, CancellationToken cancellationToken = default);

    /// <summary>Punches attributable to an employee on a work date (covers overnight next-day punches).</summary>
    Task<IReadOnlyList<PunchTransaction>> GetPunchesForDayAsync(
        long employeeId, DateOnly workDate, CancellationToken cancellationToken = default);

    Task<AttendanceRecord?> GetRecordAsync(long employeeId, DateOnly workDate, CancellationToken cancellationToken = default);
    Task<AttendanceRecord?> GetRecordByIdAsync(long id, CancellationToken cancellationToken = default);
    Task AddRecordAsync(AttendanceRecord record, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<AttendanceRecord> Items, int TotalCount)> GetRecordsPagedAsync(
        int page, int pageSize, long? employeeId, DateOnly? fromDate, DateOnly? toDate,
        CancellationToken cancellationToken = default);
}
