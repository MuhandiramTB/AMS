using TAMS.Domain.Common;

namespace TAMS.Domain.Scheduling;

/// <summary>
/// Effective-dated assignment of a shift to an employee OR a department (exactly
/// one). Effective-dating means historical attendance resolves against the shift
/// in force on that date. (02 FR-SFT-003, 04 §6.3, CK-02.)
/// </summary>
public sealed class ShiftAssignment : AuditableEntity
{
    private ShiftAssignment()
    {
    }

    private ShiftAssignment(long shiftId, long? employeeId, long? departmentId, DateOnly effectiveFrom, DateOnly? effectiveTo)
    {
        if ((employeeId is null) == (departmentId is null))
        {
            throw new DomainException("A shift assignment must target exactly one of employee or department.");
        }

        if (effectiveTo is not null && effectiveTo < effectiveFrom)
        {
            throw new DomainException("EffectiveTo cannot be before EffectiveFrom.");
        }

        ShiftId = shiftId;
        EmployeeId = employeeId;
        DepartmentId = departmentId;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
    }

    public static ShiftAssignment ForEmployee(long shiftId, long employeeId, DateOnly effectiveFrom, DateOnly? effectiveTo = null)
        => new(shiftId, employeeId, null, effectiveFrom, effectiveTo);

    public static ShiftAssignment ForDepartment(long shiftId, long departmentId, DateOnly effectiveFrom, DateOnly? effectiveTo = null)
        => new(shiftId, null, departmentId, effectiveFrom, effectiveTo);

    public long ShiftId { get; private set; }
    public long? EmployeeId { get; private set; }
    public long? DepartmentId { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }

    /// <summary>True if this assignment is in force on the given date.</summary>
    public bool IsEffectiveOn(DateOnly date)
        => date >= EffectiveFrom && (EffectiveTo is null || date <= EffectiveTo);

    /// <summary>True if this assignment's date range overlaps another's.</summary>
    public bool Overlaps(DateOnly otherFrom, DateOnly? otherTo)
    {
        var thisTo = EffectiveTo ?? DateOnly.MaxValue;
        var thatTo = otherTo ?? DateOnly.MaxValue;
        return EffectiveFrom <= thatTo && otherFrom <= thisTo;
    }

    public void Close(DateOnly effectiveTo)
    {
        if (effectiveTo < EffectiveFrom)
        {
            throw new DomainException("EffectiveTo cannot be before EffectiveFrom.");
        }

        EffectiveTo = effectiveTo;
    }
}
