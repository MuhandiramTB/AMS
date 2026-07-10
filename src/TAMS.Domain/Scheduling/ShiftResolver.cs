namespace TAMS.Domain.Scheduling;

/// <summary>
/// Pure domain service that resolves which shift applies to an employee on a
/// given date from a set of assignments. An employee-specific assignment takes
/// precedence over a department-level one; among candidates, the most recently
/// effective wins. (FR-SFT-003.)
/// </summary>
public sealed class ShiftResolver
{
    public long? ResolveShiftId(
        DateOnly workDate,
        long employeePrimaryDepartmentId,
        IReadOnlyList<ShiftAssignment> assignments)
    {
        var effective = assignments.Where(a => a.IsEffectiveOn(workDate)).ToList();
        if (effective.Count == 0)
        {
            return null;
        }

        // Employee-specific assignments win over department-level ones.
        var employeeAssignment = effective
            .Where(a => a.EmployeeId is not null)
            .OrderByDescending(a => a.EffectiveFrom)
            .FirstOrDefault();
        if (employeeAssignment is not null)
        {
            return employeeAssignment.ShiftId;
        }

        var departmentAssignment = effective
            .Where(a => a.DepartmentId == employeePrimaryDepartmentId)
            .OrderByDescending(a => a.EffectiveFrom)
            .FirstOrDefault();

        return departmentAssignment?.ShiftId;
    }
}
