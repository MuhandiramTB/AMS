using Microsoft.EntityFrameworkCore;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Workforce;

namespace TAMS.Infrastructure.Persistence.Repositories;

public sealed class EmployeeRepository : IEmployeeRepository
{
    private readonly TamsDbContext _db;

    public EmployeeRepository(TamsDbContext db) => _db = db;

    public Task<Employee?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        _db.Employees.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public Task<bool> EmployeeNoExistsAsync(string employeeNo, CancellationToken cancellationToken = default) =>
        _db.Employees.AnyAsync(e => e.EmployeeNo == employeeNo, cancellationToken);

    public Task<bool> ExistsAsync(long id, CancellationToken cancellationToken = default) =>
        _db.Employees.AnyAsync(e => e.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<Employee> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        long? departmentId,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Employees.AsNoTracking();

        if (departmentId is not null)
        {
            query = query.Where(e => e.PrimaryDepartmentId == departmentId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Prefix match (StartsWith → LIKE 'term%') is SARGable and can seek the
            // EmployeeNo/name indexes; a leading-wildcard Contains would force a full
            // table scan on every keystroke and blow the NFR-01 budget as the roster
            // grows. Substring search, if ever required, should use full-text search.
            var term = search.Trim();
            query = query.Where(e =>
                e.EmployeeNo.StartsWith(term) ||
                e.FirstName.StartsWith(term) ||
                e.LastName.StartsWith(term));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(e => e.EmployeeNo)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task AddAsync(Employee employee, CancellationToken cancellationToken = default) =>
        await _db.Employees.AddAsync(employee, cancellationToken);
}
