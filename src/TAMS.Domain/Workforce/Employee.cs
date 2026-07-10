using TAMS.Domain.Common;

namespace TAMS.Domain.Workforce;

/// <summary>
/// Workforce master aggregate. Invariants: exactly one primary department
/// (BRULE-01) and a unique employee number (FR-EMP-002). Deactivation is soft.
/// (02 FR-EMP-*, 04 Workforce.Employee.)
/// </summary>
public sealed class Employee : AuditableEntity
{
    private readonly List<EmployeeStatusHistory> _statusHistory = new();

    private Employee()
    {
    }

    public Employee(
        string employeeNo,
        string firstName,
        string lastName,
        long primaryDepartmentId,
        DateTime effectiveNowUtc,
        string? email = null,
        DateOnly? hireDate = null)
    {
        SetEmployeeNo(employeeNo);
        SetName(firstName, lastName);
        SetPrimaryDepartment(primaryDepartmentId);
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        HireDate = hireDate;
        Status = EmployeeStatus.Active;
        IsActive = true;
        _statusHistory.Add(new EmployeeStatusHistory(Id, Status, effectiveNowUtc, "Created"));
    }

    /// <summary>Unique business key (UQ_Employee_EmployeeNo).</summary>
    public string EmployeeNo { get; private set; } = string.Empty;

    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string? Email { get; private set; }

    /// <summary>Exactly one primary department at any time (BRULE-01).</summary>
    public long PrimaryDepartmentId { get; private set; }

    public DateOnly? HireDate { get; private set; }
    public EmployeeStatus Status { get; private set; }
    public bool IsActive { get; private set; }

    public IReadOnlyCollection<EmployeeStatusHistory> StatusHistory => _statusHistory.AsReadOnly();

    public string FullName => $"{FirstName} {LastName}";

    public void UpdateDetails(string firstName, string lastName, string? email)
    {
        SetName(firstName, lastName);
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
    }

    public void ChangePrimaryDepartment(long departmentId) => SetPrimaryDepartment(departmentId);

    public void ChangeStatus(EmployeeStatus status, DateTime effectiveUtc, string? reason)
    {
        if (status == Status)
        {
            return;
        }

        Status = status;
        IsActive = status == EmployeeStatus.Active;
        _statusHistory.Add(new EmployeeStatusHistory(Id, status, effectiveUtc, reason));
    }

    /// <summary>Soft delete — deactivate rather than remove. (DP-04, FR-EMP-001.)</summary>
    public void Deactivate(DateTime effectiveUtc, string? reason = null)
        => ChangeStatus(EmployeeStatus.Inactive, effectiveUtc, reason ?? "Deactivated");

    private void SetEmployeeNo(string employeeNo)
    {
        if (string.IsNullOrWhiteSpace(employeeNo))
        {
            throw new DomainException("Employee number is required.");
        }

        EmployeeNo = employeeNo.Trim();
    }

    private void SetName(string firstName, string lastName)
    {
        if (string.IsNullOrWhiteSpace(firstName))
        {
            throw new DomainException("First name is required.");
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            throw new DomainException("Last name is required.");
        }

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
    }

    private void SetPrimaryDepartment(long departmentId)
    {
        if (departmentId <= 0)
        {
            throw new DomainException("An employee must belong to exactly one primary department.");
        }

        PrimaryDepartmentId = departmentId;
    }
}
