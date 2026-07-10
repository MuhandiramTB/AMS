using TAMS.Domain.Workforce;

namespace TAMS.Application.Common.Ports;

/// <summary>Persistence port for the Department aggregate.</summary>
public interface IDepartmentRepository
{
    Task<Department?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default);

    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default);

    Task<bool> HasActiveEmployeesAsync(long departmentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Department>> GetAllAsync(long? parentId, CancellationToken cancellationToken = default);

    Task AddAsync(Department department, CancellationToken cancellationToken = default);
}
