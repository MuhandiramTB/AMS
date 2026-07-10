using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TAMS.Application.Common.Ports;
using TAMS.Domain.Common;
using TAMS.Domain.Identity;

namespace TAMS.Infrastructure.Security;

/// <summary>
/// Issues short-lived signed JWT access tokens carrying minimal claims (subject,
/// roles, permissions) plus a random refresh token whose HASH is persisted.
/// (06 §6, FR-AUTH-001/002.)
/// </summary>
public sealed class JwtTokenService : ITokenService
{
    /// <summary>Custom claim type for permission codes.</summary>
    public const string PermissionClaimType = "perm";

    private readonly JwtOptions _options;
    private readonly IClock _clock;

    public JwtTokenService(IOptions<JwtOptions> options, IClock clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    public TokenPair IssueTokens(User user)
    {
        var now = _clock.UtcNow;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(user.Roles.Select(r => new Claim(ClaimTypes.Role, r.Name)));
        claims.AddRange(user.PermissionCodes.Select(p => new Claim(PermissionClaimType, p)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: creds);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var refreshHash = HashRefreshToken(refreshToken);
        var refreshExpires = now.AddDays(_options.RefreshTokenDays);

        return new TokenPair(
            accessToken,
            _options.AccessTokenMinutes * 60,
            refreshToken,
            refreshHash,
            refreshExpires);
    }

    public string HashRefreshToken(string refreshToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToBase64String(bytes);
    }
}
