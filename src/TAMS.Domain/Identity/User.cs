using TAMS.Domain.Common;

namespace TAMS.Domain.Identity;

/// <summary>
/// A login identity with one or more roles, optionally linked to an employee.
/// Owns brute-force lockout state (FR-AUTH-005). Credentials are stored only as
/// a strong one-way hash (FR-AUTH-004) — the hash is computed in Infrastructure
/// and assigned here; the domain never sees a plaintext password.
/// (02 §4.1, 06 §4, 04 Identity.User.)
/// </summary>
public sealed class User : AuditableEntity
{
    /// <summary>Failed attempts allowed before lockout. (FR-AUTH-005.)</summary>
    public const int MaxFailedAttempts = 5;

    private readonly List<Role> _roles = new();

    private User()
    {
    }

    public User(string userName, string email, string passwordHash, long? employeeId = null)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            throw new DomainException("Username is required.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("Email is required.");
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("Password hash is required.");
        }

        UserName = userName.Trim();
        Email = email.Trim();
        PasswordHash = passwordHash;
        EmployeeId = employeeId;
        IsActive = true;
    }

    public string UserName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public long? EmployeeId { get; private set; }
    public bool IsActive { get; private set; }
    public int FailedLoginCount { get; private set; }
    public DateTime? LockoutEndUtc { get; private set; }
    public DateTime? LastLoginUtc { get; private set; }

    public IReadOnlyCollection<Role> Roles => _roles.AsReadOnly();

    public IReadOnlyCollection<string> PermissionCodes =>
        _roles.SelectMany(r => r.Permissions).Select(p => p.Code).Distinct().ToList();

    public bool IsLockedOut(DateTime nowUtc) => LockoutEndUtc is not null && LockoutEndUtc > nowUtc;

    public void AssignRole(Role role)
    {
        if (_roles.Any(r => r.Name == role.Name))
        {
            return;
        }

        _roles.Add(role);
    }

    public void RemoveRole(string roleName) => _roles.RemoveAll(r => r.Name == roleName);

    /// <summary>Records a successful login; resets failure state.</summary>
    public void RegisterSuccessfulLogin(DateTime nowUtc)
    {
        FailedLoginCount = 0;
        LockoutEndUtc = null;
        LastLoginUtc = nowUtc;
    }

    /// <summary>
    /// Records a failed login and locks the account once the threshold is
    /// reached. Lockout duration is supplied by the caller (policy). (FR-AUTH-005.)
    /// </summary>
    public void RegisterFailedLogin(DateTime nowUtc, TimeSpan lockoutDuration)
    {
        FailedLoginCount++;
        if (FailedLoginCount >= MaxFailedAttempts)
        {
            LockoutEndUtc = nowUtc.Add(lockoutDuration);
        }
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("Password hash is required.");
        }

        PasswordHash = passwordHash;
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;
}
