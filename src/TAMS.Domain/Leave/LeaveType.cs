using TAMS.Domain.Common;

namespace TAMS.Domain.Leave;

/// <summary>
/// A category of leave (Annual, Sick, …). Accrual/carry-over policy is deferred
/// (OQ-03) and modelled as data on the balance for now. (02 FR-LV, 04 §6.6.)
/// </summary>
public sealed class LeaveType : AuditableEntity
{
    private LeaveType()
    {
    }

    public LeaveType(string code, string name)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("Leave type code is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Leave type name is required.");
        }

        Code = code.Trim();
        Name = name.Trim();
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    public void Deactivate() => IsActive = false;
}
