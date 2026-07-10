using TAMS.Domain.Workforce;

namespace TAMS.Application.Departments;

/// <summary>Response DTO for a department. (05 §1 — DTOs at the boundary.)</summary>
public sealed record DepartmentDto(
    long Id,
    string Code,
    string Name,
    long? ParentDepartmentId,
    bool IsActive)
{
    public static DepartmentDto FromEntity(Department d) =>
        new(d.Id, d.Code, d.Name, d.ParentDepartmentId, d.IsActive);
}
