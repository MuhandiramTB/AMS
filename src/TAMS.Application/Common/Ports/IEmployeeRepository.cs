using TAMS.Domain.Workforce;

namespace TAMS.Application.Common.Ports;

/// <summary>
/// Persistence port for the Employee aggregate. Returns domain types, never
/// raw IQueryable across the layer boundary. (07 §4.2.)
/// </summary>
public interface IEmployeeRepository
{
    Task<Employee?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<bool> EmployeeNoExistsAsync(string employeeNo, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Employee> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        long? departmentId,
        string? search,
        CancellationToken cancellationToken = default);

    Task AddAsync(Employee employee, CancellationToken cancellationToken = default);
}
