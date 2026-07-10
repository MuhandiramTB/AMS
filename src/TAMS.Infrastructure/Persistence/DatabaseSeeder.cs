using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Identity;

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

    public async Task SeedAsync(string bootstrapAdminUserName, string bootstrapAdminEmail, string bootstrapAdminPassword)
    {
        await SeedPermissionsAsync();
        await SeedRolesAsync();
        await SeedBootstrapAdminAsync(bootstrapAdminUserName, bootstrapAdminEmail, bootstrapAdminPassword);
        await _db.SaveChangesAsync();
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
                Permissions.EmployeeRead, Permissions.EmployeeWrite,
                Permissions.DepartmentRead, Permissions.DepartmentWrite,
                Permissions.ShiftRead, Permissions.ShiftWrite,
                Permissions.AttendanceRead, Permissions.AttendanceWrite, Permissions.AttendanceCorrect
            },
            [RoleNames.Manager] = new[]
            {
                Permissions.EmployeeRead, Permissions.DepartmentRead,
                Permissions.ShiftRead, Permissions.AttendanceRead
            },
            [RoleNames.Employee] = Array.Empty<string>(),
            [RoleNames.Auditor] = new[]
            {
                Permissions.EmployeeRead, Permissions.DepartmentRead,
                Permissions.ShiftRead, Permissions.AttendanceRead, Permissions.AuditRead
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
