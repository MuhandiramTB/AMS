using TAMS.Domain.Leave;

namespace TAMS.Application.Common.Ports;

/// <summary>Persistence port for leave types, requests and balances.</summary>
public interface ILeaveRepository
{
    // Types
    Task<LeaveType?> GetTypeByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<bool> TypeCodeExistsAsync(string code, CancellationToken cancellationToken = default);
    /// <summary>Existence check by id without materializing the entity (hot write paths).</summary>
    Task<bool> TypeExistsAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeaveType>> GetTypesAsync(CancellationToken cancellationToken = default);
    Task AddTypeAsync(LeaveType type, CancellationToken cancellationToken = default);

    // Requests
    Task<LeaveRequest?> GetRequestByIdAsync(long id, CancellationToken cancellationToken = default);
    Task AddRequestAsync(LeaveRequest request, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<LeaveRequest> Items, int TotalCount)> GetRequestsPagedAsync(
        int page, int pageSize, long? employeeId, LeaveStatus? status, CancellationToken cancellationToken = default);

    /// <summary>Approved/applied leave requests for an employee overlapping a date. (FR-ATT-007.)</summary>
    Task<IReadOnlyList<LeaveRequest>> GetCoveringLeaveAsync(
        long employeeId, DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>True if the employee already has a non-cancelled/non-rejected request
    /// whose date range overlaps [start, end]. Prevents double-booking. (FR-LV-001.)</summary>
    Task<bool> HasOverlappingRequestAsync(
        long employeeId, DateOnly start, DateOnly end, CancellationToken cancellationToken = default);

    // Balances
    Task<LeaveBalance?> GetBalanceAsync(long employeeId, long leaveTypeId, short year, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LeaveBalance>> GetBalancesForEmployeeAsync(long employeeId, short year, CancellationToken cancellationToken = default);
    Task AddBalanceAsync(LeaveBalance balance, CancellationToken cancellationToken = default);
}
