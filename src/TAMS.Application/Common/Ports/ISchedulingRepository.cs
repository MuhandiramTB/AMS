using TAMS.Domain.Scheduling;

namespace TAMS.Application.Common.Ports;

/// <summary>Persistence port for shifts and their effective-dated assignments.</summary>
public interface ISchedulingRepository
{
    Task<Shift?> GetShiftByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<bool> ShiftCodeExistsAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Shift>> GetShiftsAsync(CancellationToken cancellationToken = default);
    Task AddShiftAsync(Shift shift, CancellationToken cancellationToken = default);

    Task AddAssignmentAsync(ShiftAssignment assignment, CancellationToken cancellationToken = default);

    /// <summary>Existing assignments for the same target (employee or department).</summary>
    Task<IReadOnlyList<ShiftAssignment>> GetAssignmentsForTargetAsync(
        long? employeeId, long? departmentId, CancellationToken cancellationToken = default);

    /// <summary>All assignments relevant to resolving an employee's shift on a date.</summary>
    Task<IReadOnlyList<ShiftAssignment>> GetAssignmentsForEmployeeAsync(
        long employeeId, long primaryDepartmentId, CancellationToken cancellationToken = default);
}
