namespace TAMS.Application.Auth;

/// <summary>Authenticated user summary returned to the client. (05 §4.2.)</summary>
public sealed record AuthUserDto(
    long Id,
    string UserName,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);

/// <summary>Login response: tokens + user. Refresh token is returned to the
/// Api layer, which places it in an HttpOnly cookie. (06 §6.)</summary>
public sealed record LoginResultDto(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    string RefreshToken,
    AuthUserDto User);
