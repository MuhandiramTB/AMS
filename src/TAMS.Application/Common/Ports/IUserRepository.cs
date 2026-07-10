using TAMS.Domain.Identity;

namespace TAMS.Application.Common.Ports;

/// <summary>Persistence port for the User aggregate and refresh tokens.</summary>
public interface IUserRepository
{
    /// <summary>Loads a user with roles+permissions eagerly (for auth).</summary>
    Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default);

    Task<User?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task AddRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken = default);

    Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Revokes all active refresh tokens for a user (logout / reuse detection). (06 §6.)</summary>
    Task RevokeAllRefreshTokensAsync(long userId, DateTime nowUtc, CancellationToken cancellationToken = default);
}
