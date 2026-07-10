using Microsoft.EntityFrameworkCore;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Workforce;

namespace TAMS.Infrastructure.Persistence.Repositories;

public sealed class DepartmentRepository : IDepartmentRepository
{
    private readonly TamsDbContext _db;

    public DepartmentRepository(TamsDbContext db) => _db = db;

    public Task<Department?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        _db.Departments.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default) =>
        _db.Departments.AnyAsync(d => d.Id == id, cancellationToken);

    public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default) =>
        _db.Departments.AnyAsync(d => d.Code == code, cancellationToken);

    public Task<bool> HasActiveEmployeesAsync(long departmentId, CancellationToken cancellationToken = default) =>
        _db.Employees.AnyAsync(e => e.PrimaryDepartmentId == departmentId && e.IsActive, cancellationToken);

    public async Task<IReadOnlyList<Department>> GetAllAsync(long? parentId, CancellationToken cancellationToken = default)
    {
        var query = _db.Departments.AsNoTracking();
        if (parentId is not null)
        {
            query = query.Where(d => d.ParentDepartmentId == parentId);
        }

        return await query.OrderBy(d => d.Code).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Department department, CancellationToken cancellationToken = default) =>
        await _db.Departments.AddAsync(department, cancellationToken);
}
