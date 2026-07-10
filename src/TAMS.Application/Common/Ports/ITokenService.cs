using TAMS.Domain.Identity;

namespace TAMS.Application.Common.Ports;

/// <summary>Issued access + refresh token pair.</summary>
public sealed record TokenPair(
    string AccessToken,
    int ExpiresInSeconds,
    string RefreshToken,
    string RefreshTokenHash,
    DateTime RefreshExpiresAtUtc);

/// <summary>
/// Issues signed JWT access tokens and refresh tokens. (06 §6, FR-AUTH-001/002.)
/// </summary>
public interface ITokenService
{
    TokenPair IssueTokens(User user);

    /// <summary>Deterministic hash of a refresh token for safe storage/lookup.</summary>
    string HashRefreshToken(string refreshToken);
}
