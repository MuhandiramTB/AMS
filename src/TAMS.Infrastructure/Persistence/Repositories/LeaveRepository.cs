using Microsoft.EntityFrameworkCore;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Leave;

namespace TAMS.Infrastructure.Persistence.Repositories;

public sealed class LeaveRepository : ILeaveRepository
{
    private readonly TamsDbContext _db;

    public LeaveRepository(TamsDbContext db) => _db = db;

    // Types
    public Task<LeaveType?> GetTypeByIdAsync(long id, CancellationToken cancellationToken = default) =>
        _db.LeaveTypes.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<bool> TypeCodeExistsAsync(string code, CancellationToken cancellationToken = default) =>
        _db.LeaveTypes.AnyAsync(t => t.Code == code, cancellationToken);

    public async Task<IReadOnlyList<LeaveType>> GetTypesAsync(CancellationToken cancellationToken = default) =>
        await _db.LeaveTypes.AsNoTracking().OrderBy(t => t.Code).ToListAsync(cancellationToken);

    public async Task AddTypeAsync(LeaveType type, CancellationToken cancellationToken = default) =>
        await _db.LeaveTypes.AddAsync(type, cancellationToken);

    // Requests
    public Task<LeaveRequest?> GetRequestByIdAsync(long id, CancellationToken cancellationToken = default) =>
        _db.LeaveRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task AddRequestAsync(LeaveRequest request, CancellationToken cancellationToken = default) =>
        await _db.LeaveRequests.AddAsync(request, cancellationToken);

    public async Task<(IReadOnlyList<LeaveRequest> Items, int TotalCount)> GetRequestsPagedAsync(
        int page, int pageSize, long? employeeId, LeaveStatus? status, CancellationToken cancellationToken = default)
    {
        var query = _db.LeaveRequests.AsNoTracking().AsQueryable();
        if (employeeId is not null) query = query.Where(r => r.EmployeeId == employeeId);
        if (status is not null) query = query.Where(r => r.Status == status);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.StartDate).ThenBy(r => r.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<IReadOnlyList<LeaveRequest>> GetCoveringLeaveAsync(
        long employeeId, DateOnly date, CancellationToken cancellationToken = default) =>
        await _db.LeaveRequests.AsNoTracking()
            .Where(r => r.EmployeeId == employeeId
                && (r.Status == LeaveStatus.Approved || r.Status == LeaveStatus.Applied)
                && r.StartDate <= date && r.EndDate >= date)
            .ToListAsync(cancellationToken);

    // Balances
    public Task<LeaveBalance?> GetBalanceAsync(long employeeId, long leaveTypeId, short year, CancellationToken cancellationToken = default) =>
        _db.LeaveBalances.FirstOrDefaultAsync(
            b => b.EmployeeId == employeeId && b.LeaveTypeId == leaveTypeId && b.Year == year, cancellationToken);

    public async Task<IReadOnlyList<LeaveBalance>> GetBalancesForEmployeeAsync(long employeeId, short year, CancellationToken cancellationToken = default) =>
        await _db.LeaveBalances.AsNoTracking()
            .Where(b => b.EmployeeId == employeeId && b.Year == year).ToListAsync(cancellationToken);

    public async Task AddBalanceAsync(LeaveBalance balance, CancellationToken cancellationToken = default) =>
        await _db.LeaveBalances.AddAsync(balance, cancellationToken);
}
