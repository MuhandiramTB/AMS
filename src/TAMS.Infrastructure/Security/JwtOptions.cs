using TAMS.Application.Common.Ports;

namespace TAMS.Infrastructure.Security;

/// <summary>
/// JWT + auth-policy settings bound from configuration (12-Factor III). The signing
/// key comes from the secret store / environment, never source. (06 §6/§9.)
/// Implements IAuthPolicyOptions so the Application layer can read the lockout
/// duration without knowing about configuration frameworks.
/// </summary>
public sealed class JwtOptions : IAuthPolicyOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "TAMS";
    public string Audience { get; set; } = "TAMS.Client";

    /// <summary>Symmetric signing key. Supplied via secret store in real environments.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
    public int LockoutMinutes { get; set; } = 15;

    public TimeSpan LockoutDuration => TimeSpan.FromMinutes(LockoutMinutes);
}
