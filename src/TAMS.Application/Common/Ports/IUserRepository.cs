using TAMS.Domain.Identity;

namespace TAMS.Application.Common.Ports;

/// <summary>Persistence port for the User aggregate and refresh tokens.</summary>
public interface IUserRepository
{
    /// <summary>Loads a user with roles+permissions eagerly (for auth).</summary>
    Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default);

    Task<User?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>All users with their roles, for the admin user-management list.</summary>
    Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default);

    Task<bool> UserNameExistsAsync(string userName, CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>Resolves role entities (with permissions) by name, for assignment.</summary>
    Task<IReadOnlyList<Role>> GetRolesByNameAsync(IEnumerable<string> names, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Role>> GetAllRolesAsync(CancellationToken cancellationToken = default);

    /// <summary>Count of active users holding a given role (guards last-admin deactivation).</summary>
    Task<int> CountActiveInRoleAsync(string roleName, CancellationToken cancellationToken = default);

    Task AddRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken = default);

    Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Revokes all active refresh tokens for a user (logout / reuse detection). (06 §6.)</summary>
    Task RevokeAllRefreshTokensAsync(long userId, DateTime nowUtc, CancellationToken cancellationToken = default);
}
