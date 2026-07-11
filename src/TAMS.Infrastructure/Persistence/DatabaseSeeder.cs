using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Identity;
using TAMS.Domain.Workforce;

namespace TAMS.Infrastructure.Persistence;

/// <summary>
/// Idempotently seeds roles, permissions, the RBAC mapping (from the SRS §4.1
/// capability matrix) and a single bootstrap administrator. Runs at startup after
/// migrations. (04 §12.) The bootstrap password is supplied via configuration and
/// must be changed on first login in production.
/// </summary>
public sealed class DatabaseSeeder
{
    private readonly TamsDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(TamsDbContext db, IPasswordHasher passwordHasher, ILogger<DatabaseSeeder> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task SeedAsync(
        string bootstrapAdminUserName,
        string bootstrapAdminEmail,
        string bootstrapAdminPassword,
        bool seedDemoUsers = false)
    {
        await SeedPermissionsAsync();
        await SeedRolesAsync();
        await SeedBootstrapAdminAsync(bootstrapAdminUserName, bootstrapAdminEmail, bootstrapAdminPassword);

        // Development-only convenience: one login per role so each role's UI can be
        // seen. NEVER enabled in production (gated by the caller). Idempotent.
        if (seedDemoUsers)
        {
            await SeedDemoUsersAsync();
        }

        await _db.SaveChangesAsync();
    }

    private static readonly (string Role, string UserName, string Email)[] DemoUsers =
    {
        (RoleNames.HrOfficer, "hr", "hr@tams.local"),
        (RoleNames.Manager, "manager", "manager@tams.local"),
        (RoleNames.Employee, "employee", "employee@tams.local"),
        (RoleNames.Auditor, "auditor", "auditor@tams.local"),
    };

    /// <summary>
    /// Seeds one demo user per non-admin role (dev only). Shared password so it is
    /// easy to try each role; each user is created only if absent, so re-runs are
    /// safe. The manager/employee demo logins are LINKED to demo employee records so
    /// their own-record data scope resolves (otherwise scoped pages 403). Never call
    /// this in production — no default credentials should exist.
    /// </summary>
    private async Task SeedDemoUsersAsync()
    {
        const string demoPassword = "Demo!123";

        // A demo department + two demo employees the manager/employee logins link to.
        var employeeIds = await SeedDemoEmployeesAsync();

        foreach (var (roleName, userName, email) in DemoUsers)
        {
            if (await _db.Users.AnyAsync(u => u.UserName == userName))
            {
                continue;
            }

            // Link the employee-scoped roles to a real employee so their own records load.
            long? linkedEmployeeId = userName switch
            {
                "employee" => employeeIds.EmployeeUserEmpId,
                "manager" => employeeIds.ManagerUserEmpId,
                _ => null,
            };

            var role = await _db.Roles.Include(r => r.Permissions).FirstAsync(r => r.Name == roleName);
            var user = new User(userName, email, _passwordHasher.Hash(demoPassword), linkedEmployeeId);
            user.AssignRole(role);
            _db.Users.Add(user);
            _logger.LogInformation("Seeded demo user '{UserName}' (role {Role}, employee {Emp}).", userName, roleName, linkedEmployeeId);
        }
    }

    /// <summary>Creates a demo department + two demo employees (idempotent). Returns
    /// the employee ids to link to the manager/employee demo logins.</summary>
    private async Task<(long ManagerUserEmpId, long EmployeeUserEmpId)> SeedDemoEmployeesAsync()
    {
        var now = DateTime.UtcNow;

        var dept = await _db.Departments.FirstOrDefaultAsync(d => d.Code == "DEMO");
        if (dept is null)
        {
            dept = new Department("DEMO", "Demo Department");
            _db.Departments.Add(dept);
            await _db.SaveChangesAsync(); // assign the department id
        }

        async Task<long> EnsureEmployee(string no, string first, string last, string email)
        {
            var existing = await _db.Employees.FirstOrDefaultAsync(e => e.EmployeeNo == no);
            if (existing is not null)
            {
                return existing.Id;
            }
            var emp = new Employee(no, first, last, dept.Id, now, email);
            _db.Employees.Add(emp);
            await _db.SaveChangesAsync(); // assign the employee id
            return emp.Id;
        }

        var managerEmpId = await EnsureEmployee("DEMO-MGR", "Demo", "Manager", "manager@tams.local");
        var employeeEmpId = await EnsureEmployee("DEMO-EMP", "Demo", "Employee", "employee@tams.local");
        return (managerEmpId, employeeEmpId);
    }

    private async Task SeedPermissionsAsync()
    {
        var existing = await _db.Permissions.Select(p => p.Code).ToListAsync();
        foreach (var code in Permissions.All.Except(existing))
        {
            _db.Permissions.Add(new Permission(code));
            _logger.LogInformation("Seeding permission {Permission}", code);
        }

        await _db.SaveChangesAsync();
    }

    private async Task SeedRolesAsync()
    {
        var allPermissions = await _db.Permissions.ToListAsync();
        Permission Perm(string code) => allPermissions.First(p => p.Code == code);

        // RBAC mapping per SRS §4.1 (P1 subset).
        var map = new Dictionary<string, string[]>
        {
            [RoleNames.Administrator] = Permissions.All.ToArray(),
            [RoleNames.HrOfficer] = new[]
            {
                Permissions.EmployeeRead, Permissions.EmployeeReadAll, Permissions.EmployeeWrite,
                Permissions.DepartmentRead, Permissions.DepartmentWrite,
                Permissions.ShiftRead, Permissions.ShiftWrite,
                Permissions.AttendanceRead, Permissions.AttendanceReadAll,
                Permissions.AttendanceWrite, Permissions.AttendanceCorrect,
                Permissions.DeviceRead,
                Permissions.LeaveRead, Permissions.LeaveReadAll, Permissions.LeaveRequest,
                Permissions.LeaveApprove, Permissions.LeaveManage,
                Permissions.ReportRead, Permissions.ReportExport
            },
            // Manager: read capability but NOT the all-rows scope — confined to own
            // records now; the team tier lands with Manager features (06 §5 seam).
            [RoleNames.Manager] = new[]
            {
                Permissions.EmployeeRead, Permissions.DepartmentRead,
                Permissions.ShiftRead, Permissions.AttendanceRead,
                Permissions.LeaveRead, Permissions.LeaveApprove,
                Permissions.ReportRead
            },
            [RoleNames.Employee] = new[]
            {
                Permissions.AttendanceRead,
                Permissions.LeaveRead, Permissions.LeaveRequest
            },
            [RoleNames.Auditor] = new[]
            {
                Permissions.EmployeeRead, Permissions.EmployeeReadAll,
                Permissions.DepartmentRead, Permissions.ShiftRead,
                Permissions.AttendanceRead, Permissions.AttendanceReadAll,
                Permissions.LeaveRead, Permissions.LeaveReadAll, Permissions.ReportRead, Permissions.AuditRead
            }
        };

        foreach (var (roleName, permissionCodes) in map)
        {
            var role = await _db.Roles
                .Include(r => r.Permissions)
                .FirstOrDefaultAsync(r => r.Name == roleName);

            if (role is null)
            {
                role = new Role(roleName);
                _db.Roles.Add(role);
                _logger.LogInformation("Seeding role {Role}", roleName);
            }

            foreach (var code in permissionCodes)
            {
                role.GrantPermission(Perm(code));
            }
        }

        await _db.SaveChangesAsync();
    }

    private async Task SeedBootstrapAdminAsync(string userName, string email, string password)
    {
        if (await _db.Users.AnyAsync())
        {
            return; // Users already exist; do not reseed.
        }

        var adminRole = await _db.Roles
            .Include(r => r.Permissions)
            .FirstAsync(r => r.Name == RoleNames.Administrator);

        var admin = new User(userName, email, _passwordHasher.Hash(password));
        admin.AssignRole(adminRole);
        _db.Users.Add(admin);

        _logger.LogWarning(
            "Seeded bootstrap admin '{UserName}'. The password MUST be changed on first login.",
            userName);
    }
}
