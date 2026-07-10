using TAMS.Domain.Common;

namespace TAMS.Domain.Workforce;

/// <summary>
/// Organisational unit. Supports a self-referencing hierarchy and soft delete.
/// (02 FR-DEP-*, 04 Workforce.Department.)
/// </summary>
public sealed class Department : AuditableEntity
{
    // EF Core materialisation constructor.
    private Department()
    {
    }

    public Department(string code, string name, long? parentDepartmentId = null)
    {
        SetCode(code);
        SetName(name);
        ParentDepartmentId = parentDepartmentId;
        IsActive = true;
    }

    /// <summary>Unique business code (UQ_Department_Code).</summary>
    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    /// <summary>Parent for hierarchy; null for a root department. (FR-DEP-002.)</summary>
    public long? ParentDepartmentId { get; private set; }

    public bool IsActive { get; private set; }

    public void Rename(string name) => SetName(name);

    public void MoveUnder(long? parentDepartmentId)
    {
        // A department cannot be its own parent (cycle guard; deeper cycle
        // detection is enforced in the application layer with the full tree).
        if (parentDepartmentId is not null && parentDepartmentId == Id)
        {
            throw new DomainException("A department cannot be its own parent.");
        }

        ParentDepartmentId = parentDepartmentId;
    }

    /// <summary>Soft delete — deactivate rather than remove. (DP-04.)</summary>
    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    private void SetCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("Department code is required.");
        }

        Code = code.Trim();
    }

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Department name is required.");
        }

        Name = name.Trim();
    }
}
