using Microsoft.EntityFrameworkCore;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Scheduling;

namespace TAMS.Infrastructure.Persistence.Repositories;

public sealed class SchedulingRepository : ISchedulingRepository
{
    private readonly TamsDbContext _db;

    public SchedulingRepository(TamsDbContext db) => _db = db;

    public Task<Shift?> GetShiftByIdAsync(long id, CancellationToken cancellationToken = default) =>
        _db.Shifts.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<bool> ShiftCodeExistsAsync(string code, CancellationToken cancellationToken = default) =>
        _db.Shifts.AnyAsync(s => s.Code == code, cancellationToken);

    public async Task<IReadOnlyList<Shift>> GetShiftsAsync(CancellationToken cancellationToken = default) =>
        await _db.Shifts.AsNoTracking().OrderBy(s => s.Code).ToListAsync(cancellationToken);

    public async Task AddShiftAsync(Shift shift, CancellationToken cancellationToken = default) =>
        await _db.Shifts.AddAsync(shift, cancellationToken);

    public async Task AddAssignmentAsync(ShiftAssignment assignment, CancellationToken cancellationToken = default) =>
        await _db.ShiftAssignments.AddAsync(assignment, cancellationToken);

    public async Task<IReadOnlyList<ShiftAssignment>> GetAssignmentsForTargetAsync(
        long? employeeId, long? departmentId, CancellationToken cancellationToken = default)
    {
        var query = _db.ShiftAssignments.AsNoTracking();
        query = employeeId is not null
            ? query.Where(a => a.EmployeeId == employeeId)
            : query.Where(a => a.DepartmentId == departmentId);
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ShiftAssignment>> GetAssignmentsForEmployeeAsync(
        long employeeId, long primaryDepartmentId, CancellationToken cancellationToken = default) =>
        await _db.ShiftAssignments.AsNoTracking()
            .Where(a => a.EmployeeId == employeeId || a.DepartmentId == primaryDepartmentId)
            .ToListAsync(cancellationToken);
}
