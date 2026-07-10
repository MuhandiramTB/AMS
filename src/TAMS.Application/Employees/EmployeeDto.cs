using TAMS.Domain.Workforce;

namespace TAMS.Application.Employees;

/// <summary>Response DTO for an employee. (05 §1.)</summary>
public sealed record EmployeeDto(
    long Id,
    string EmployeeNo,
    string FirstName,
    string LastName,
    string? Email,
    long PrimaryDepartmentId,
    string Status,
    bool IsActive)
{
    public static EmployeeDto FromEntity(Employee e) =>
        new(e.Id, e.EmployeeNo, e.FirstName, e.LastName, e.Email,
            e.PrimaryDepartmentId, e.Status.ToString(), e.IsActive);
}
