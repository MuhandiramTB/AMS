using TAMS.Domain.Common;

namespace TAMS.Domain.Identity;

/// <summary>
/// A rotating, revocable refresh token. Only the HASH is stored, never the raw
/// token, so a DB leak yields no usable tokens. (06 §6, 04 Identity.RefreshToken.)
/// </summary>
public sealed class RefreshToken : Entity
{
    private RefreshToken()
    {
    }

    public RefreshToken(long userId, string tokenHash, DateTime expiresAtUtc)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAtUtc = expiresAtUtc;
    }

    public long UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }

    public bool IsActive(DateTime nowUtc) => RevokedAtUtc is null && ExpiresAtUtc > nowUtc;

    public void Revoke(DateTime nowUtc) => RevokedAtUtc ??= nowUtc;
}
