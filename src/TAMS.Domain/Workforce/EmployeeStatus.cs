namespace TAMS.Domain.Workforce;

/// <summary>
/// Employee lifecycle status. Stored as TINYINT with a CHECK constraint.
/// (04 Workforce.Employee, CK-05.)
/// </summary>
public enum EmployeeStatus : byte
{
    Active = 1,
    Inactive = 2,
    Suspended = 3,
    Terminated = 4
}
