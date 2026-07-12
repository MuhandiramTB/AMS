using Microsoft.EntityFrameworkCore;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Identity;

namespace TAMS.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly TamsDbContext _db;

    public UserRepository(TamsDbContext db) => _db = db;

    public Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default) =>
        _db.Users
            .Include(u => u.Roles)
            .ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.UserName == userName, cancellationToken);

    public Task<User?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        _db.Users
            .Include(u => u.Roles)
            .ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default) =>
        await _db.Users.AsNoTracking()
            .Include(u => u.Roles)
            .OrderBy(u => u.UserName)
            .ToListAsync(cancellationToken);

    public Task<bool> UserNameExistsAsync(string userName, CancellationToken cancellationToken = default) =>
        _db.Users.AnyAsync(u => u.UserName == userName, cancellationToken);

    public Task<bool> EmployeeLinkExistsAsync(long employeeId, long? excludeUserId, CancellationToken cancellationToken = default) =>
        _db.Users.AnyAsync(u => u.EmployeeId == employeeId && (excludeUserId == null || u.Id != excludeUserId), cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default) =>
        await _db.Users.AddAsync(user, cancellationToken);

    public async Task<IReadOnlyList<Role>> GetRolesByNameAsync(IEnumerable<string> names, CancellationToken cancellationToken = default)
    {
        var set = names.ToList();
        return await _db.Roles
            .Include(r => r.Permissions)
            .Where(r => set.Contains(r.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Role>> GetAllRolesAsync(CancellationToken cancellationToken = default) =>
        await _db.Roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync(cancellationToken);

    public Task<int> CountActiveInRoleAsync(string roleName, CancellationToken cancellationToken = default) =>
        _db.Users.CountAsync(u => u.IsActive && u.Roles.Any(r => r.Name == roleName), cancellationToken);

    public async Task AddRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken = default) =>
        await _db.RefreshTokens.AddAsync(token, cancellationToken);

    public Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task RevokeAllRefreshTokensAsync(long userId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var active = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null && t.ExpiresAtUtc > nowUtc)
            .ToListAsync(cancellationToken);

        foreach (var token in active)
        {
            token.Revoke(nowUtc);
        }
    }
}
