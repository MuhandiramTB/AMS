namespace TAMS.Domain.Identity;

/// <summary>
/// Canonical catalogue of permission codes, derived from the SRS capability
/// matrix (02 §4.1). Referenced by authorization policies and seed data so the
/// set is single-sourced. Only P1 (foundation) capabilities are wired now;
/// later phases add their own.
/// </summary>
public static class Permissions
{
    // Employee
    public const string EmployeeRead = "Employee.Read";
    public const string EmployeeWrite = "Employee.Write";

    // Department
    public const string DepartmentRead = "Department.Read";
    public const string DepartmentWrite = "Department.Write";

    // Shift / Scheduling
    public const string ShiftRead = "Shift.Read";
    public const string ShiftWrite = "Shift.Write";

    // Attendance
    public const string AttendanceRead = "Attendance.Read";
    public const string AttendanceWrite = "Attendance.Write";
    public const string AttendanceCorrect = "Attendance.Correct";

    // Unrestricted (all-rows) read scope — held by Admin/HR/Auditor, NOT Manager/
    // Employee, who are confined to their own records via server-derived scope. (06 §5.)
    public const string EmployeeReadAll = "Employee.ReadAll";
    public const string AttendanceReadAll = "Attendance.ReadAll";

    // Devices
    public const string DeviceRead = "Device.Read";
    public const string DeviceManage = "Device.Manage";

    // Administration
    public const string UserManage = "User.Manage";
    public const string RoleManage = "Role.Manage";

    // Audit
    public const string AuditRead = "Audit.Read";

    public static IReadOnlyList<string> All { get; } = new[]
    {
        EmployeeRead,
        EmployeeWrite,
        DepartmentRead,
        DepartmentWrite,
        ShiftRead,
        ShiftWrite,
        AttendanceRead,
        AttendanceWrite,
        AttendanceCorrect,
        EmployeeReadAll,
        AttendanceReadAll,
        DeviceRead,
        DeviceManage,
        UserManage,
        RoleManage,
        AuditRead
    };
}

/// <summary>Canonical role names (02 §4.1).</summary>
public static class RoleNames
{
    public const string Administrator = "Administrator";
    public const string HrOfficer = "HROfficer";
    public const string Manager = "Manager";
    public const string Employee = "Employee";
    public const string Auditor = "Auditor";
}
